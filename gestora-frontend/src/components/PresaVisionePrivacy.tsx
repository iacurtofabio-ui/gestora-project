import { useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'
import { useAccettaPrivacy, useStatoPrivacy } from '@/hooks/usePrivacy'
import { PageLoading } from '@/components/PageState'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

/**
 * V2-019 — nell'area riservata, prima delle pagine, l'informativa privacy se l'utente non ne ha
 * ancora preso visione: account creati da un Admin, primo Admin, account precedenti alla F3 o
 * informativa cambiata. Sta dentro AppLayout, sotto la barra: il menu utente resta, quindi si
 * può sempre uscire senza accettare.
 *
 * Se la verifica fallisce si mostrano comunque le pagine: un problema di rete non deve chiudere
 * fuori lo Staff durante il servizio. La presa visione verrà chiesta al prossimo ingresso.
 */
export function PresaVisionePrivacy({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const stato = useStatoPrivacy(user?.id)
  const accetta = useAccettaPrivacy()
  const [letta, setLetta] = useState(false)

  if (stato.isPending) return <PageLoading />
  if (!stato.data?.daAccettare) return <>{children}</>

  return (
    <div className="flex justify-center py-6">
      <Card className="w-full max-w-lg">
        <CardHeader>
          <CardTitle className="text-titolo">Prima di continuare</CardTitle>
          <CardDescription className="text-corpo text-muted-foreground">
            Leggi come Gestora tratta i tuoi dati e le regole del servizio.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <p className="text-corpo">
            <Link to="/privacy" target="_blank" className="underline underline-offset-4">
              Informativa privacy
            </Link>
            {' · '}
            <Link to="/termini" target="_blank" className="underline underline-offset-4">
              Termini di servizio
            </Link>
          </p>
          <label className="text-corpo flex items-start gap-2">
            <input
              type="checkbox"
              className="mt-1 size-4 accent-primary"
              checked={letta}
              onChange={(e) => setLetta(e.target.checked)}
            />
            Ho letto l’informativa privacy e accetto i termini di servizio.
          </label>
          <Button
            className="w-full"
            disabled={!letta || accetta.isPending}
            onClick={() => accetta.mutate()}
          >
            {accetta.isPending ? 'Salvataggio…' : 'Continua'}
          </Button>
        </CardContent>
      </Card>
    </div>
  )
}
