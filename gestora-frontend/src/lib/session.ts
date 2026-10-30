/**
 * REV-025 — ponte fra l'interceptor Axios (che vive fuori da React) e l'albero dei componenti.
 *
 * Prima la scadenza della sessione veniva gestita con `window.location.href = '/login'`: un
 * ricaricamento a pagina intera, senza alcun messaggio, che faceva sembrare l'app crashata.
 * Qui l'interceptor si limita a segnalare l'evento; chi e' dentro React decide come reagire
 * (logout, pulizia della cache, toast, navigazione).
 *
 * V2-028 — il token non è più leggibile da JavaScript, quindi l'interceptor non può guardare in
 * localStorage per sapere se c'era una sessione. Lo tiene questo modulo: AuthProvider la segna
 * aperta quando conosce l'utente e chiusa all'uscita.
 */
type SessionExpiredHandler = () => void

let handler: SessionExpiredHandler | null = null
let aperta = false

/** Registra il gestore. Restituisce la funzione di deregistrazione, comoda in useEffect. */
export function onSessionExpired(nuovoHandler: SessionExpiredHandler): () => void {
  handler = nuovoHandler
  return () => {
    if (handler === nuovoHandler) handler = null
  }
}

export function segnaSessione(attiva: boolean): void {
  aperta = attiva
}

export function sessioneAttiva(): boolean {
  return aperta
}

/**
 * La sessione è scaduta: si segna chiusa (le altre richieste in volo non la segnalano di nuovo)
 * e si avvisa React. Restituisce false se nessuno e' in ascolto: in quel caso il chiamante deve
 * ripiegare sul vecchio comportamento (redirect duro), per non lasciare l'utente su una pagina morta.
 */
export function chiudiSessione(): boolean {
  aperta = false
  if (!handler) return false
  handler()
  return true
}
