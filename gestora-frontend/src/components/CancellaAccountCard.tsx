import { useState } from 'react'
import { useCancellaAccount } from '@/hooks/useAccount'
import { erroriPerCampo, messaggioErroreCaricamento } from '@/lib/apiError'
import { ErroreForm } from '@/components/PageState'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

/**
 * V2-019 — cancellazione del proprio account, da «Il mio account». Si conferma con la password in
 * una finestra a parte: è un'azione che non si annulla. Il server la blocca (409) se ci sono
 * prenotazioni da servire o se si è l'ultimo Admin, e il motivo si legge nella finestra.
 */
export function CancellaAccountCard() {
  const cancella = useCancellaAccount()
  const [aperta, setAperta] = useState(false)
  const [password, setPassword] = useState('')
  const [errorePassword, setErrorePassword] = useState<string>()
  const [errore, setErrore] = useState<string>()

  function chiudi(apri: boolean) {
    setAperta(apri)
    if (!apri) {
      setPassword('')
      setErrorePassword(undefined)
      setErrore(undefined)
    }
  }

  function conferma(e: React.FormEvent) {
    e.preventDefault()
    setErrorePassword(undefined)
    setErrore(undefined)
    if (!password) {
      setErrorePassword('Scrivi la tua password per confermare.')
      return
    }
    cancella.mutate(password, {
      onError: (err) => {
        const { perCampo } = erroriPerCampo(err, ['password'] as const)
        if (perCampo.password) setErrorePassword(perCampo.password)
        else setErrore(messaggioErroreCaricamento(err, 'Account non cancellato. Riprova.'))
      },
    })
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sezione">Cancella l’account</CardTitle>
        <CardDescription className="text-corpo text-muted-foreground">
          I tuoi dati personali vengono eliminati. Le prenotazioni già passate restano al locale per i
          suoi conteggi, senza il tuo nome. Se ne hai ancora in programma, prima annullale.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Button variant="destructive" onClick={() => chiudi(true)}>
          Cancella il mio account
        </Button>
      </CardContent>

      <Dialog open={aperta} onOpenChange={chiudi}>
        <DialogContent className="sm:max-w-md">
          <form onSubmit={conferma} className="space-y-4">
            <DialogHeader>
              <DialogTitle className="text-titolo">Cancellare l’account?</DialogTitle>
              <DialogDescription className="text-corpo text-muted-foreground">
                Non si può tornare indietro. Per confermare scrivi la tua password.
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-1">
              <Label htmlFor="cancella-password">Password</Label>
              <Input
                id="cancella-password"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
              {errorePassword && <p className="text-nota text-destructive">{errorePassword}</p>}
            </div>
            <ErroreForm messaggio={errore} />
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => chiudi(false)}>
                Annulla
              </Button>
              <Button type="submit" variant="destructive" disabled={cancella.isPending}>
                {cancella.isPending ? 'Cancellazione…' : 'Cancella definitivamente'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </Card>
  )
}
