import axios from 'axios'
import { notifySessionExpired } from '@/lib/session'
import { Endpoints } from '@/lib/endpoints'

// REV-017 - senza l'indirizzo del backend Axios userebbe URL relativi: le chiamate finirebbero
// sull'host del frontend, che risponde con l'HTML della SPA, e l'utente vedrebbe errori di
// parsing incomprensibili invece della causa vera (variabile d'ambiente non configurata).
//
// La mancanza viene segnalata come *dato*, non con un throw a livello di modulo: quello
// impedirebbe il caricamento del bundle e lascerebbe la pagina bianca, senza che nessun Error
// Boundary possa intervenire. Chi monta l'app (main.tsx) mostra la schermata di spiegazione.
const baseURL = import.meta.env.VITE_API_URL

export const configurazioneMancante = !baseURL

if (configurazioneMancante) {
  console.error(
    "Configurazione mancante: la variabile d'ambiente VITE_API_URL non e' impostata. " +
      "Impostarla con l'indirizzo del backend (es. https://.../api) nel file .env.local in " +
      'sviluppo o nelle variabili di progetto in produzione, poi ricostruire il frontend.'
  )
}

const apiClient = axios.create({ baseURL: baseURL ?? '' })

// Login, conferma dell'email e suo reinvio non hanno bisogno del token. Allegarlo era dannoso: con
// una sessione ancora aperta (per esempio aprendo /login a mano) una password sbagliata dava 401
// su una richiesta "con token", trattata come sessione scaduta: token cancellato e redirect.
//
// `register` NON è in questa lista, di proposito (V2-003): quando a chiamarlo è un Admin che crea
// un account Staff, il server deve riconoscerlo dal token. Senza, l'account nascerebbe da
// confermare e l'esenzione dell'Admin dal limite di registrazioni (V2-011) non scatterebbe mai.
// Su un endpoint anonimo un token scaduto non dà 401: il server lo ignora.
export const SENZA_TOKEN: readonly string[] = [
  Endpoints.auth.login,
  Endpoints.auth.confermaEmail,
  Endpoints.auth.reinviaConferma,
  Endpoints.auth.passwordDimenticata,
  Endpoints.auth.reimpostaPassword,
]

apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token && !SENZA_TOKEN.includes(config.url ?? '')) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    // Il redirect automatico serve per la sessione scaduta (token presente ma rifiutato),
    // non per un 401 su una richiesta anonima come il login stesso: altrimenti una password
    // sbagliata provoca un reload a pagina intera invece di mostrare l'errore nel form.
    const hadToken = Boolean(error.config?.headers?.Authorization)
    // Piu' richieste in volo (la Dashboard ne fa tre) ricevono 401 insieme: solo la prima trova
    // ancora il token e segnala la scadenza, le altre no. Prima ognuna mostrava il suo avviso
    // "Sessione scaduta" e avviava la sua navigazione.
    const primaASegnalare = localStorage.getItem('token') !== null
    if (error.response?.status === 401 && hadToken && primaASegnalare) {
      localStorage.removeItem('token')
      // REV-025: uscita pulita gestita da React (toast + navigazione). Il reload duro resta
      // solo come rete di sicurezza, se nessun componente e' in ascolto.
      if (!notifySessionExpired()) {
        window.location.href = '/login'
      }
    }
    return Promise.reject(error)
  }
)

export default apiClient
