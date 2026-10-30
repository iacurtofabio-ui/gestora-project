import { useEffect, useState } from 'react'
import { isAxiosError } from 'axios'
import { AuthContext, type AuthUser } from './auth-context'
import apiClient, { COOKIE_CSRF } from '@/lib/axios'
import { Endpoints } from '@/lib/endpoints'
import { RIPROVA_RINNOVO_MS, ritardoRinnovo } from '@/lib/rinnovoSessione'
import { segnaSessione } from '@/lib/session'
import type { SessioneDTO } from '@/types/sessione'

function normalizeRoles(claim: unknown): string[] {
  if (!claim) return []
  if (Array.isArray(claim)) return claim.filter((r): r is string => typeof r === 'string')
  return typeof claim === 'string' ? [claim] : []
}

/** Secondi dall'epoca UTC, come il vecchio `exp` del token; undefined se illeggibile. */
function secondiScadenza(scadenza: string): number | undefined {
  const ms = Date.parse(scadenza)
  return Number.isNaN(ms) ? undefined : ms / 1000
}

/**
 * REV-014 — lettura difensiva: una risposta incompleta o una sessione gia' scaduta diventano
 * `null`, cioe' «utente anonimo», invece di far fallire il render.
 *
 * V2-028 — prima si decodificava il token da localStorage; ora il token sta in un cookie HttpOnly
 * e i dati arrivano dal server (SessioneDTO).
 */
function utenteDaSessione(sessione: SessioneDTO | null | undefined): AuthUser | null {
  if (!sessione?.id || typeof sessione.scadenza !== 'string') return null
  const scadenza = secondiScadenza(sessione.scadenza)
  if (scadenza === undefined || scadenza * 1000 <= Date.now()) return null

  return {
    id: sessione.id,
    email: typeof sessione.email === 'string' ? sessione.email : '',
    roles: normalizeRoles(sessione.ruoli),
    scadenza: sessione.scadenza,
  }
}

/**
 * Il cookie CSRF (leggibile) nasce e muore con quello di sessione: se non c'e', non c'e' nemmeno
 * una sessione, e si evita di chiedere «chi sono» al server per ogni visitatore della vetrina.
 */
function forseInSessione(): boolean {
  return document.cookie.split(';').some((c) => c.trim().startsWith(`${COOKIE_CSRF}=`))
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null)
  // All'avvio la sessione puo' esistere solo nel cookie: finche' il server non risponde non si sa
  // chi e' l'utente, e le pagine protette rimanderebbero al login chi e' gia' entrato.
  const [inVerifica, setInVerifica] = useState(forseInSessione)

  // Fino alla v3.0.0 il token stava in localStorage: al primo avvio dopo il rilascio si toglie,
  // invece di lasciarlo li', leggibile da qualsiasi script finche' non scade.
  useEffect(() => {
    try {
      localStorage.removeItem('token')
    } catch {
      // localStorage non disponibile (navigazione privata in alcuni browser): niente da togliere.
    }
  }, [])

  useEffect(() => {
    if (!inVerifica) return
    let annullato = false

    apiClient
      .get<SessioneDTO>(Endpoints.auth.sessione)
      .then(({ data }) => {
        if (annullato) return
        const utente = utenteDaSessione(data)
        segnaSessione(utente !== null)
        setUser(utente)
      })
      // 401 (cookie scaduto, gia' tolto dal server) o server irraggiungibile: si parte anonimi.
      .catch(() => {})
      .finally(() => {
        if (!annullato) setInVerifica(false)
      })

    return () => {
      annullato = true
    }
  }, [inVerifica])

  // V2-010 — rinnovo del token poco prima della scadenza, finche' la pagina e' aperta. Riparte a
  // ogni sessione nuova (login, rinnovo) e si ferma al logout.
  const scadenzaSessione = user?.scadenza
  useEffect(() => {
    if (!scadenzaSessione) return
    const scadenza = secondiScadenza(scadenzaSessione)
    const ritardo = ritardoRinnovo(scadenza)
    if (ritardo === null || scadenza === undefined) return
    const scadenzaAttuale = scadenza

    let annullato = false
    let timer = setTimeout(rinnova, ritardo)

    async function rinnova() {
      try {
        const { data } = await apiClient.post<SessioneDTO>(Endpoints.auth.rinnovaToken)
        if (annullato) return
        // Al limite delle 12 ore dal login il server non puo' allungare la scadenza: si tiene la
        // sessione che c'e' e non si richiede piu'. Alla scadenza si torna al login.
        const nuovaScadenza = secondiScadenza(data.scadenza)
        if (nuovaScadenza === undefined || nuovaScadenza <= scadenzaAttuale) return
        const utente = utenteDaSessione(data)
        if (!utente) return
        setUser(utente)
      } catch (errore) {
        // 401 = sessione finita: ci pensa gia' l'intercettore (avviso e ritorno al login).
        if (annullato || (isAxiosError(errore) && errore.response?.status === 401)) return
        // Rete o server momentaneamente giu': si riprova, finche' il token vale ancora.
        if (scadenzaAttuale * 1000 - Date.now() > RIPROVA_RINNOVO_MS) {
          timer = setTimeout(rinnova, RIPROVA_RINNOVO_MS)
        }
      }
    }

    return () => {
      annullato = true
      clearTimeout(timer)
    }
  }, [scadenzaSessione])

  function login(sessione: SessioneDTO) {
    const utente = utenteDaSessione(sessione)
    if (!utente) {
      segnaSessione(false)
      setUser(null)
      throw new Error("La risposta del server non e' utilizzabile o la sessione e' gia' scaduta.")
    }
    segnaSessione(true)
    setUser(utente)
    return utente
  }

  function logout() {
    segnaSessione(false)
    setUser(null)
    // Il cookie HttpOnly lo puo' togliere solo il server. Se la chiamata fallisce il cookie scade
    // comunque da solo con il token.
    apiClient.post(Endpoints.auth.logout).catch(() => {})
  }

  if (inVerifica) {
    return (
      <div className="min-h-screen flex items-center justify-center text-corpo text-muted-foreground" aria-busy="true">
        Caricamento…
      </div>
    )
  }

  return (
    <AuthContext.Provider value={{ user, isAuthenticated: !!user, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}
