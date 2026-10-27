import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import apiClient from '@/lib/axios'
import { Endpoints } from '@/lib/endpoints'
import { segnalaErrore } from '@/lib/apiError'

export type StatoPrivacyDTO = {
  daAccettare: boolean
  versione: string
}

/**
 * V2-019 — se l'utente collegato deve ancora prendere visione dell'informativa in vigore. La
 * chiave contiene l'id dell'utente: dopo un cambio di account non si riusa la risposta del
 * precedente.
 */
export function useStatoPrivacy(userId: string | undefined) {
  return useQuery({
    queryKey: ['stato-privacy', userId],
    queryFn: () => apiClient.get<StatoPrivacyDTO>(Endpoints.auth.statoPrivacy).then((r) => r.data),
    enabled: userId !== undefined,
    staleTime: Infinity,
  })
}

export function useAccettaPrivacy() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => apiClient.post(Endpoints.auth.accettaPrivacy),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['stato-privacy'] }),
    onError: segnalaErrore('Presa visione non salvata. Riprova.'),
  })
}
