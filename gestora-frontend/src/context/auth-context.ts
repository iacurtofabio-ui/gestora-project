import { createContext } from 'react'
import type { SessioneDTO } from '@/types/sessione'

export type AuthUser = {
  id: string
  email: string
  roles: string[]
  /** V2-028: istante UTC (ISO) in cui scade il token nel cookie. Il token non è leggibile. */
  scadenza: string
}

export type AuthContextType = {
  user: AuthUser | null
  isAuthenticated: boolean
  /**
   * Apre la sessione con la risposta del server (login, cambio password) e restituisce l'utente,
   * cosi' chi effettua l'accesso non deve ricostruirlo per conto proprio. Solleva un'eccezione se
   * la risposta non e' utilizzabile o la sessione e' gia' scaduta.
   */
  login: (sessione: SessioneDTO) => AuthUser
  /** Chiude la sessione qui e chiede al server di togliere il cookie. */
  logout: () => void
}

export const AuthContext = createContext<AuthContextType | null>(null)
