import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import apiClient from '@/lib/axios'
import { Endpoints } from '@/lib/endpoints'
import { useAuth } from '@/hooks/useAuth'
import type { CambiaPasswordDTO, MioAccountDTO } from '@/types/account'

/** V2-019 — i dati dell'utente collegato. La chiave contiene l'id: un altro account non li riusa. */
export function useMioAccount() {
  const { user } = useAuth()
  return useQuery({
    queryKey: ['mio-account', user?.id],
    queryFn: () => apiClient.get<MioAccountDTO>(Endpoints.account.mioAccount).then((r) => r.data),
    enabled: user !== null,
  })
}

/**
 * V2-019 — cancellazione del proprio account. Dopo, la sessione non vale più (l'utente non esiste):
 * si esce, si svuota la cache e si torna alla pagina del locale. Gli errori (password sbagliata,
 * prenotazioni da servire, ultimo Admin) li mostra la finestra di conferma.
 */
export function useCancellaAccount() {
  const { logout } = useAuth()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  return useMutation({
    mutationFn: (password: string) => apiClient.post(Endpoints.account.cancellaAccount, { password }),
    onSuccess: () => {
      logout()
      queryClient.clear()
      toast.success('Il tuo account è stato cancellato.')
      navigate('/', { replace: true })
    },
  })
}

/**
 * V2-019 — cambio password. Il server cambia il security stamp, quindi il token in uso non vale
 * più: risponde con uno nuovo, che si salva subito al posto del vecchio. Gli errori sui campi li
 * mostra il form, per questo qui non c'è `segnalaErrore`.
 */
export function useCambiaPassword() {
  const { login } = useAuth()
  return useMutation({
    mutationFn: (data: CambiaPasswordDTO) =>
      apiClient.post<{ token: string }>(Endpoints.account.cambiaPassword, data).then((r) => r.data),
    onSuccess: ({ token }) => {
      login(token)
      toast.success('Password cambiata. Le altre sessioni aperte sono state chiuse.')
    },
  })
}
