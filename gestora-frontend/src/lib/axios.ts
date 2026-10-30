import axios from 'axios'
import { chiudiSessione, sessioneAttiva } from '@/lib/session'
import { Endpoints } from '@/lib/endpoints'

/**
 * V2-028 — /api sta sullo stesso sito delle pagine: in locale lo gira ad Azure il proxy di Vite,
 * su Vercel il middleware (middleware.ts). Non serve più VITE_API_URL.
 *
 * Il token non passa più da qui: sta in un cookie HttpOnly che il browser allega da solo e che
 * JavaScript non può leggere. Axios rimanda invece il valore del cookie CSRF nell'header
 * X-XSRF-TOKEN (solo verso lo stesso sito): il backend rifiuta con 400 le scritture senza.
 */
export const COOKIE_CSRF = '__Host-gestora-xsrf'

const apiClient = axios.create({
  baseURL: '/api',
  xsrfCookieName: COOKIE_CSRF,
  xsrfHeaderName: 'X-XSRF-TOKEN',
})

/**
 * Richieste su cui un 401 non vuol dire «sessione scaduta»: una password sbagliata al login,
 * un link di conferma scaduto, la verifica della sessione all'avvio. Lì l'errore lo mostra la
 * pagina, senza avviso né ritorno al login.
 */
export const SENZA_SCADENZA: readonly string[] = [
  Endpoints.auth.login,
  Endpoints.auth.sessione,
  Endpoints.auth.logout,
  Endpoints.auth.confermaEmail,
  Endpoints.auth.reinviaConferma,
  Endpoints.auth.passwordDimenticata,
  Endpoints.auth.reimpostaPassword,
]

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    // Più richieste in volo (la Dashboard ne fa tre) ricevono 401 insieme: solo la prima trova la
    // sessione ancora aperta e la segnala come scaduta; le altre no.
    const url = error.config?.url ?? ''
    if (error.response?.status === 401 && !SENZA_SCADENZA.includes(url) && sessioneAttiva()) {
      // REV-025: uscita pulita gestita da React (toast + navigazione). Il reload duro resta
      // solo come rete di sicurezza, se nessun componente e' in ascolto.
      if (!chiudiSessione()) {
        window.location.href = '/login'
      }
    }
    return Promise.reject(error)
  }
)

export default apiClient
