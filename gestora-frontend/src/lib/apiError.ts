import { isAxiosError, type AxiosError } from 'axios'
import { toast } from 'sonner'
import type { ApiErrorResponse } from '@/types/apiError'

/**
 * REV-041 — un solo punto per tradurre l'errore dell'API in un messaggio da mostrare.
 *
 * Lo stesso blocco di sei righe era ripetuto identico in 26 punti fra hook e componenti: cambiava
 * soltanto la frase di ripiego. Oltre alla duplicazione il problema era pratico - il giorno in cui
 * il backend cambia la forma della risposta d'errore, o si vuole distinguere un caso in piu',
 * bisogna ricordarsi di 26 posti.
 *
 * L'ordine di lettura riflette quello che il backend produce davvero (vedi ExceptionMiddleware):
 * gli errori di validazione arrivano nell'array `errors` con un messaggio per campo, tutto il
 * resto in `message`.
 */
export function messaggioErrore(error: AxiosError<ApiErrorResponse>, fallback: string): string {
  // Nessuna risposta significa che la richiesta non e' mai arrivata: backend spento, rete assente
  // o CORS. Dire "errore durante la creazione" qui manderebbe fuori strada, perche' la creazione
  // non e' nemmeno stata tentata.
  if (!error.response) {
    return 'Server non raggiungibile. Controlla la connessione e riprova.'
  }

  const data = error.response.data
  const errors = data?.errors ?? []

  if (errors.length > 0) {
    return errors.map((e) => e.error).join(', ')
  }

  return data?.message ?? fallback
}

/**
 * Gestore di errore pronto da passare a `onError` di React Query.
 *
 * Uso: `onError: segnalaErrore('Errore durante la creazione')`.
 */
export function segnalaErrore(fallback: string) {
  return (error: AxiosError<ApiErrorResponse>) => {
    toast.error(messaggioErrore(error, fallback))
  }
}

/**
 * Errori di validazione del server divisi per campo del form, per mostrarli sotto il campo giusto
 * ("questa email e' gia' registrata" sotto Email, non in fondo al form). Il nome del campo arriva
 * in maiuscolo dal validator automatico ("Username") e in minuscolo dagli errori di Identity
 * ("username"): si confronta senza distinguere maiuscole e minuscole. Restano fuori, e vanno
 * mostrati in generale, gli errori di campi che il form non ha.
 */
export function erroriPerCampo<C extends string>(
  error: unknown,
  campi: readonly C[]
): { perCampo: Partial<Record<C, string>>; altri: string[] } {
  const perCampo: Partial<Record<C, string>> = {}
  const altri: string[] = []
  if (!isAxiosError<ApiErrorResponse>(error)) return { perCampo, altri }

  for (const e of error.response?.data?.errors ?? []) {
    const campo = campi.find((c) => c.toLowerCase() === e.field.toLowerCase())
    if (campo && !perCampo[campo]) perCampo[campo] = e.error
    else if (!campo) altri.push(e.error)
  }
  return { perCampo, altri }
}

/**
 * V2-003 — il login ha rifiutato una password giusta perché l'email non è ancora confermata (403).
 * Si riconosce dal codice stabile, non dal testo: il messaggio può cambiare, il codice no.
 */
export const CODICE_EMAIL_NON_CONFERMATA = 'EMAIL_NON_CONFERMATA'

export function emailNonConfermata(error: unknown): boolean {
  if (!isAxiosError<{ codice?: string }>(error)) return false
  return (
    error.response?.status === 403 && error.response.data?.codice === CODICE_EMAIL_NON_CONFERMATA
  )
}

/**
 * Messaggio del login. Prima ogni errore Axios diventava "email o password errate": anche
 * l'account bloccato (423), il limite di tentativi (429) e il server spento o in avvio. Chi era
 * bloccato riprovava all'infinito una password giusta. Il backend risponde a 423/401 con una
 * stringa semplice, non con `{ message }`: per questo i testi sono scritti qui.
 */
export function messaggioErroreLogin(error: unknown): string {
  if (!isAxiosError(error)) {
    // Una risposta illeggibile non e' un problema di credenziali.
    return 'Accesso non riuscito: la risposta del server non risulta utilizzabile.'
  }
  if (!error.response) {
    return 'Server non raggiungibile. Controlla la connessione e riprova.'
  }
  if (emailNonConfermata(error)) {
    return 'Devi confermare l’email prima di accedere. Controlla la tua posta, oppure fatti rimandare il link.'
  }
  switch (error.response.status) {
    case 400:
    case 401:
      return 'Email o password non corrispondono a nessun account. Controlla e riprova.'
    case 423:
      // V2-027: il blocco è per email + indirizzo di chi sbaglia, non per account.
      return 'Troppi tentativi sbagliati da questo dispositivo. Riprova tra 15 minuti.'
    case 429:
      return 'Troppi tentativi in poco tempo. Attendi un minuto e riprova.'
    default:
      return 'Il server non ha risposto correttamente. Riprova tra qualche istante.'
  }
}

/**
 * V2-011 — il server ha fermato la richiesta per il limite di richieste (429). Serve dove il
 * testo è scritto nella pagina e non preso dal server, come la verifica della disponibilità
 * pubblica: lì «non riesco a controllare» farebbe pensare a un guasto, invece basta aspettare.
 */
export function eTroppeRichieste(error: unknown): boolean {
  return isAxiosError(error) && error.response?.status === 429
}

/**
 * NEW-006 — stessa idea di `messaggioErrore`, ma per gli errori di *caricamento* (React Query
 * `error` di una query, non di una mutation). Prima ogni pagina mostrava lo stesso testo fisso
 * ("Errore nel caricamento") a prescindere dalla causa: backend spento, permessi mancanti o
 * database in errore erano indistinguibili. `error` di una query non è tipizzato come
 * `AxiosError`: va verificato con `isAxiosError` prima di riusare `messaggioErrore`.
 */
export function messaggioErroreCaricamento(error: unknown, fallback: string): string {
  if (isAxiosError<ApiErrorResponse>(error)) {
    return messaggioErrore(error, fallback)
  }
  return fallback
}
