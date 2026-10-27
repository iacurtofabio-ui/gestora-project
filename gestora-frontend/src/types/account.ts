/** Rispecchia MioAccountDTO del backend (Services/Account/DTOs). Istanti in UTC. */
export type MioAccountDTO = {
  userName: string
  email: string
  ruoli: string[]
  dataCreazione: string
  ultimoAccesso: string | null
  privacyAccettataIl: string | null
  privacyVersione: string | null
}

/** Rispecchia CambiaPasswordDTO del backend. */
export type CambiaPasswordDTO = {
  passwordAttuale: string
  nuovaPassword: string
}
