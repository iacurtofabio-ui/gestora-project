import { useState } from 'react'
import { DownloadIcon } from 'lucide-react'
import { useScaricaReport } from '@/hooks/useReportPrenotazioni'
import { giorniCompresi, meseDi, oggiInItalia } from '@/lib/date'
import { ErroreForm } from '@/components/PageState'
import { Button } from '@/components/ui/button'
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

/** Come ReportPrenotazioniService.GiorniMassimi nel backend. */
const GIORNI_MASSIMI = 366

/**
 * V2-025 — «Scarica report» nella pagina Prenotazioni, solo per l'Admin: un file CSV da aprire in
 * Excel con le prenotazioni di un periodo, tutti gli stati. All'apertura propone il mese in corso.
 * Il periodo si controlla qui come nel backend, così l'errore si legge nella finestra.
 */
export function ReportPrenotazioni() {
  const scarica = useScaricaReport()
  const [aperta, setAperta] = useState(false)
  const [da, setDa] = useState('')
  const [a, setA] = useState('')
  const [errore, setErrore] = useState<string>()

  function apri() {
    const mese = meseDi(oggiInItalia())
    setDa(mese.primo)
    setA(mese.ultimo)
    setErrore(undefined)
    setAperta(true)
  }

  function conferma(e: React.FormEvent) {
    e.preventDefault()
    if (!da || !a) return setErrore('Indica le due date.')
    if (a < da) return setErrore('La data finale non può essere prima di quella iniziale.')
    if (giorniCompresi(da, a) > GIORNI_MASSIMI) return setErrore('Il periodo può essere al massimo di un anno.')

    setErrore(undefined)
    scarica.mutate({ da, a }, { onSuccess: () => setAperta(false) })
  }

  return (
    <>
      <Button size="sm" variant="outline" onClick={apri}>
        <DownloadIcon />
        Scarica report
      </Button>

      <Dialog open={aperta} onOpenChange={setAperta}>
        <DialogContent className="sm:max-w-sm">
          <form onSubmit={conferma} className="space-y-4">
            <DialogHeader>
              <DialogTitle className="text-titolo">Report delle prenotazioni</DialogTitle>
              <DialogDescription className="text-corpo text-muted-foreground">
                Un file da aprire in Excel con tutte le prenotazioni del periodo, in ogni stato.
              </DialogDescription>
            </DialogHeader>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label htmlFor="report-da">Dal</Label>
                <Input id="report-da" type="date" value={da} onChange={(e) => setDa(e.target.value)} />
              </div>
              <div className="space-y-1">
                <Label htmlFor="report-a">Al</Label>
                <Input id="report-a" type="date" value={a} onChange={(e) => setA(e.target.value)} />
              </div>
            </div>
            <ErroreForm messaggio={errore} />
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setAperta(false)}>
                Annulla
              </Button>
              <Button type="submit" disabled={scarica.isPending}>
                {scarica.isPending ? 'Preparazione…' : 'Scarica CSV'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  )
}
