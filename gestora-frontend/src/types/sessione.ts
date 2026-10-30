/**
 * V2-028 — rispecchia SessioneDTO del backend (Services/Auth/DTOs): la risposta di login, rinnovo,
 * cambio password e «chi sono». Il token non c'è: sta nel cookie HttpOnly. `scadenza` è l'istante
 * UTC in cui il token scade, per sapere quando rinnovarlo.
 */
export type SessioneDTO = {
  id: string
  email: string
  ruoli: string[]
  scadenza: string
}
