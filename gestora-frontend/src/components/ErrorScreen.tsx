/**
 * REV-014 — schermata di errore condivisa.
 *
 * La usano in due: l'ErrorBoundary a classe, che copre quello che sta *fuori* dal router
 * (AuthProvider compreso, il punto in cui un dato di sessione corrotto faceva pagina bianca), e
 * RouteErrorPage, che copre gli errori *dentro* le route. Servono entrambi: React Router ha un
 * proprio error boundary che intercetta gli errori delle pagine prima che arrivino a quello di
 * React, e senza un errorElement mostrerebbe la sua schermata di sviluppo con lo stack trace.
 */
import apiClient from '@/lib/axios'
import { Endpoints } from '@/lib/endpoints'
import { Button } from '@/components/ui/button'
import { SchermataMessaggio } from '@/components/SchermataMessaggio'

export default function ErrorScreen({ messaggio }: { messaggio?: string }) {
  // V2-028: la sessione sta in un cookie HttpOnly, che solo il server puo' togliere. Si esce in
  // ogni caso: anche se la chiamata fallisce, la pagina di login si ricarica da capo.
  function ripartiDaLogin() {
    apiClient
      .post(Endpoints.auth.logout)
      .catch(() => {})
      .finally(() => {
        window.location.href = '/login'
      })
  }

  return (
    <SchermataMessaggio
      tono="errore"
      titolo="Questa pagina non si e' aperta"
      dettaglio={messaggio}
      azioni={
        <>
          <Button type="button" onClick={() => window.location.reload()}>
            Ricarica la pagina
          </Button>
          <Button type="button" variant="outline" onClick={ripartiDaLogin}>
            Riparti dall'accesso
          </Button>
        </>
      }
    >
      {/* L'errore dice cosa fare, in ordine di costo: prima la cosa che quasi sempre basta, poi
          quella che costa un nuovo accesso. Non si scusa: non serve a chi ha il lavoro fermo. */}
      <p>
        Qualcosa si e' inceppato mentre la pagina veniva disegnata. Nove volte su dieci basta
        ricaricare.
      </p>
      <p>
        Se ricaricando succede di nuovo, ripartire dall'accesso ripulisce i dati di sessione
        rimasti a meta', che sono la causa piu' comune.
      </p>
    </SchermataMessaggio>
  )
}
