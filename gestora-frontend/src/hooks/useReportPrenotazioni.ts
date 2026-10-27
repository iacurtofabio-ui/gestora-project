import { useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'
import apiClient from '@/lib/axios'
import { Endpoints } from '@/lib/endpoints'

/**
 * V2-025 — scarica il report CSV delle prenotazioni (solo Admin). Il file arriva come blob e si
 * salva con un link temporaneo. Il nome lo costruiamo qui: l'header del server con il nome del file
 * non è leggibile da un'altra origine senza esporlo nel CORS.
 *
 * Gli errori di periodo li ferma già il form; un errore del server arriva come blob, quindi qui
 * c'è solo un messaggio generico.
 */
export function useScaricaReport() {
  return useMutation({
    mutationFn: ({ da, a }: { da: string; a: string }) =>
      apiClient.get<Blob>(Endpoints.prenotazione.reportCsv(da, a), { responseType: 'blob' }).then((r) => ({
        file: r.data,
        nome: `prenotazioni_${da}_${a}.csv`,
      })),
    onSuccess: ({ file, nome }) => {
      const url = URL.createObjectURL(file)
      const link = document.createElement('a')
      link.href = url
      link.download = nome
      link.click()
      URL.revokeObjectURL(url)
    },
    onError: () => toast.error('Report non scaricato. Riprova.'),
  })
}
