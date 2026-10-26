import { useEffect, useRef } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import { useFascePerGiorno } from '@/hooks/useFasceOrarie'
import { useZoneAttive } from '@/hooks/useZone'
import { useCreaPrenotazione, useModificaPrenotazione } from '@/hooks/usePrenotazioni'
import { useCheckDisponibilita, useLimitiPrenotazione } from '@/hooks/useDisponibilita'
import { useAuth } from '@/hooks/useAuth'
import type { PrenotazioneDTO } from '@/types/prenotazione'
import { interoObbligatorio } from '@/lib/validazioni'
import { oggiInItalia, ultimaDataPrenotabile } from '@/lib/date'

// AUD-M11: la data non aveva limiti. Una data passata o oltre un anno era rifiutata solo al
// salvataggio, e intanto la verifica dei posti falliva in silenzio: le fasce sembravano libere.
// I confronti fra stringhe YYYY-MM-DD seguono quelli fra date.
const schema = z.object({
  dataPrenotazione: z
    .string()
    .min(1, 'Data obbligatoria')
    .refine((d) => d >= oggiInItalia(), 'La data non può essere nel passato')
    .refine((d) => d <= ultimaDataPrenotabile(), 'Si può prenotare al massimo un anno in anticipo'),
  fasciaOrariaId: interoObbligatorio('Fascia oraria obbligatoria'),
  zonaId: z.number().nullable().optional(),
  numeroCoperti: interoObbligatorio('Almeno 1 coperto'),
  note: z.string().optional(),
  nomeCliente: z.string().max(200, 'Il nome non può superare 200 caratteri').optional(),
})

type FormValues = z.infer<typeof schema>

type Props = {
  isOpen: boolean
  onClose: () => void
  /**
   * NEW-001 - se valorizzata il modal lavora in modifica: campi precompilati e PUT al posto
   * del POST. Assente (creazione) e' il comportamento storico.
   */
  prenotazione?: PrenotazioneDTO
  /** V2-007: chiamata dopo una creazione riuscita, con la data scelta (la pagina ci si sposta). */
  onCreata?: (dataPrenotazione: string) => void
}

const VALORI_VUOTI: FormValues = {
  dataPrenotazione: '',
  fasciaOrariaId: undefined as unknown as number,
  zonaId: null,
  numeroCoperti: undefined as unknown as number,
  note: '',
  nomeCliente: '',
}

export default function PrenotazioneModal({ isOpen, onClose, prenotazione, onCreata }: Props) {
  const { user } = useAuth()
  const isStaff = user?.roles.includes('Admin') || user?.roles.includes('Staff')
  const zone = useZoneAttive()
  const creaPrenotazione = useCreaPrenotazione()
  const modificaPrenotazione = useModificaPrenotazione()

  const inModifica = prenotazione !== undefined
  // Le unioni di tavoli sono sempre nella stessa zona (checkpoint 2b), quindi la zona delle
  // postazioni assegnate e' univoca e si presta a precompilare il campo.
  // Lo 0 e' trattato come assenza: nessuna zona ha id 0, e prima che il mapping backend fosse
  // corretto era il valore che arrivava sempre. Meglio ricadere su "nessuna preferenza" che
  // impostare un valore senza opzione corrispondente, che lascia la select senza selezione.
  const zonaAssegnataId = prenotazione?.postazioni[0]?.zonaId || null

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    watch,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: VALORI_VUOTI,
  })

  const dataPrenotazione = watch('dataPrenotazione')
  const giornoSettimana = dataPrenotazione
    ? new Date(`${dataPrenotazione}T00:00:00Z`).getUTCDay()
    : undefined
  const fasceOrarie = useFascePerGiorno(giornoSettimana)

  // NEW-002 — un semaforo per fascia mentre si compila il form, invece di scoprire il rifiuto
  // solo all'invio. Una sola chiamata copre tutte le fasce del giorno: vedi useDisponibilita.
  const numeroCopertiScelto = watch('numeroCoperti')
  const fasciaOrariaIdScelta = watch('fasciaOrariaId')
  const disponibilita = useCheckDisponibilita(
    dataPrenotazione || undefined,
    Number.isFinite(numeroCopertiScelto) ? numeroCopertiScelto : undefined
  )
  const disponibilitaPerFascia = new Map(
    (disponibilita.data?.fasce ?? []).map((f) => [f.fasciaOrariaId, f])
  )
  const disponibilitaFasciaScelta = disponibilitaPerFascia.get(fasciaOrariaIdScelta)
  // V2-024: in creazione una fascia non disponibile non si salva, invece di arrivare al 409 del
  // backend. In modifica no: la prenotazione stessa occupa i tavoli che la verifica conta come presi,
  // quindi la fascia risulterebbe piena anche quando la modifica ci sta.
  const fasciaNonPrenotabile =
    !inModifica && disponibilitaFasciaScelta?.disponibilePerRichiesta === false

  // V2-007: il Cliente prenota da solo fino al limite online; oltre, lo diciamo subito invece di
  // aspettare il rifiuto del backend. Lo Staff (anche al telefono) resta al limite tecnico.
  const limiti = useLimitiPrenotazione()
  const limiteOnline = isStaff ? undefined : limiti.data?.maxCopertiPrenotazioneOnline
  const oltreLimiteOnline =
    limiteOnline !== undefined &&
    Number.isFinite(numeroCopertiScelto) &&
    numeroCopertiScelto > limiteOnline

  // Le due select dipendono da liste caricate in modo asincrono: impostarne il valore prima che
  // le <option> esistano lo farebbe cadere a vuoto. Si precompilano quindi in un secondo
  // momento, quando le liste sono arrivate.
  const selezioniDaPrecompilare = useRef(false)

  // Si dipende dall'id e non dall'oggetto: la lista viene rinfrescata da React Query e a ogni
  // refetch l'oggetto cambia identita', il che azzererebbe il form mentre l'utente scrive.
  const prenotazioneId = prenotazione?.id

  useEffect(() => {
    if (!isOpen) {
      reset(VALORI_VUOTI)
      selezioniDaPrecompilare.current = false
      return
    }
    if (prenotazione) {
      reset({
        ...VALORI_VUOTI,
        dataPrenotazione: prenotazione.dataPrenotazione,
        numeroCoperti: prenotazione.numeroCoperti,
        note: prenotazione.note ?? '',
        nomeCliente: prenotazione.nomeCliente ?? '',
      })
      selezioniDaPrecompilare.current = true
    }
    // prenotazione e' volutamente fuori dalle dipendenze: vedi nota su prenotazioneId.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen, prenotazioneId, reset])

  useEffect(() => {
    if (!selezioniDaPrecompilare.current || !prenotazione) return
    if (!fasceOrarie.data || !zone.data) return
    // Se la fascia della prenotazione non e' piu' attiva non ha un'opzione nella select: impostarla
    // la lasciava su "Scegli un turno" mentre il form inviava comunque la vecchia fascia, senza
    // avvisare. Restando vuota, la validazione chiede di sceglierne una attiva.
    if (fasceOrarie.data.some((f) => f.id === prenotazione.fasciaOrariaId)) {
      setValue('fasciaOrariaId', prenotazione.fasciaOrariaId)
    }
    setValue('zonaId', zonaAssegnataId)
    selezioniDaPrecompilare.current = false
  }, [fasceOrarie.data, zone.data, prenotazione, zonaAssegnataId, setValue])

  // Cambiando data cambiano le fasce disponibili, quindi la scelta precedente non vale piu'.
  // Si azzera solo su un cambio effettivo fra due giorni: al primo popolamento (undefined -> N)
  // non c'e' nulla da azzerare, e azzerare li' cancellerebbe la fascia appena precompilata in
  // modifica.
  const giornoPrecedente = useRef<number | undefined>(undefined)
  useEffect(() => {
    if (giornoPrecedente.current !== undefined && giornoPrecedente.current !== giornoSettimana) {
      setValue('fasciaOrariaId', undefined as unknown as number)
    }
    giornoPrecedente.current = giornoSettimana
  }, [giornoSettimana, setValue])

  const onSubmit = (values: FormValues) => {
    const payload = {
      dataPrenotazione: values.dataPrenotazione,
      fasciaOrariaId: values.fasciaOrariaId,
      zonaId: values.zonaId ?? null,
      numeroCoperti: values.numeroCoperti,
      note: values.note ?? null,
      nomeCliente: isStaff ? values.nomeCliente || null : null,
    }

    if (prenotazione) {
      modificaPrenotazione.mutate({ id: prenotazione.id, data: payload }, { onSuccess: onClose })
      return
    }
    creaPrenotazione.mutate(payload, {
      onSuccess: () => {
        onCreata?.(payload.dataPrenotazione)
        onClose()
      },
    })
  }

  const inCorso = creaPrenotazione.isPending || modificaPrenotazione.isPending

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="text-titolo">
            {inModifica ? 'Modifica prenotazione' : 'Nuova prenotazione'}
          </DialogTitle>
          <DialogDescription className="text-corpo text-muted-foreground">
            {inModifica
              ? 'Salvando, i tavoli vengono riassegnati in base a data, fascia e coperti.'
              : 'Il tavolo lo sceglie Gestora: serve solo il giorno, il turno e quante persone.'}
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
          <div className="space-y-1">
            <Label htmlFor="prenotazione-data">Data</Label>
            <Input
              id="prenotazione-data"
              type="date"
              min={oggiInItalia()}
              max={ultimaDataPrenotabile()}
              {...register('dataPrenotazione')}
            />
            {errors.dataPrenotazione && (
              <p className="text-nota text-destructive">{errors.dataPrenotazione.message}</p>
            )}
          </div>

          <div className="space-y-1">
            <Label htmlFor="prenotazione-fascia">Fascia oraria</Label>
            <NativeSelect
              id="prenotazione-fascia"
              {...register('fasciaOrariaId', { valueAsNumber: true })}
              disabled={giornoSettimana === undefined}
            >
              <option value="">
                {giornoSettimana === undefined
                  ? 'Scegli prima una data'
                  : 'Scegli un turno'}
              </option>
              {fasceOrarie.data?.map((f) => {
                const disp = disponibilitaPerFascia.get(f.id)
                const suffisso = disp ? (disp.disponibilePerRichiesta ? '' : ' · esaurita') : ''
                return (
                  <option key={f.id} value={f.id}>
                    {f.orarioInizio.slice(0, 5)}–{f.orarioFine.slice(0, 5)}
                    {suffisso}
                  </option>
                )
              })}
            </NativeSelect>
            {giornoSettimana !== undefined && fasceOrarie.data?.length === 0 && (
              <p className="text-nota text-muted-foreground">
                Nessuna fascia oraria attiva per questo giorno.
              </p>
            )}
            {/* NEW-002: il motivo del rifiuto si vede subito, non solo dopo aver premuto Salva. */}
            {disponibilitaFasciaScelta && !disponibilitaFasciaScelta.disponibilePerRichiesta && (
              <p className="text-nota text-warning">{disponibilitaFasciaScelta.messaggio}</p>
            )}
            {errors.fasciaOrariaId && (
              <p className="text-nota text-destructive">{errors.fasciaOrariaId.message}</p>
            )}
          </div>

          <div className="space-y-1">
            <Label htmlFor="prenotazione-zona">Zona preferita (facoltativa)</Label>
            {/* REV-015: il campo era gestito a mano con onChange + setValue e non era
                            registrato nel form. Il valore arrivava al submit, ma la select restava
                            fuori dal controllo di react-hook-form: reset() non la ripuliva, e
                            riaprendo il modal si vedeva ancora la zona scelta prima mentre il form
                            era tornato a "nessuna preferenza". Ora e' un campo registrato come gli altri. */}
            <NativeSelect
              id="prenotazione-zona"
              {...register('zonaId', {
                // La select restituisce sempre stringhe: la stringa vuota e'
                // l'assenza di preferenza, che il backend si aspetta come null.
                setValueAs: (v) => (v === '' || v === null ? null : Number(v)),
              })}
            >
              <option value="">Nessuna preferenza</option>
              {zone.data?.map((z) => (
                <option key={z.id} value={z.id}>
                  {z.nome}
                </option>
              ))}
            </NativeSelect>
            {inModifica && (
              <p className="text-nota text-muted-foreground">
                Salvando, i tavoli vengono riassegnati in base a questa preferenza.
              </p>
            )}
          </div>

          {isStaff && (
            <div className="space-y-1">
              <Label htmlFor="prenotazione-nome-cliente">Nome cliente (facoltativo)</Label>
              <Input
                id="prenotazione-nome-cliente"
                type="text"
                placeholder="Es. prenotazione presa telefonicamente"
                {...register('nomeCliente')}
              />
              {errors.nomeCliente && (
                <p className="text-nota text-destructive">{errors.nomeCliente.message}</p>
              )}
            </div>
          )}

          <div className="space-y-1">
            <Label htmlFor="prenotazione-coperti">Numero di coperti</Label>
            <Input
              id="prenotazione-coperti"
              type="number"
              {...register('numeroCoperti', { valueAsNumber: true })}
            />
            {errors.numeroCoperti && (
              <p className="text-nota text-destructive">{errors.numeroCoperti.message}</p>
            )}
            {oltreLimiteOnline && (
              <p className="text-nota text-destructive">
                Per prenotazioni superiori a {limiteOnline} persone contatta direttamente il
                ristorante.
              </p>
            )}
          </div>

          <div className="space-y-1">
            <Label htmlFor="prenotazione-note">Note (facoltative)</Label>
            <textarea
              id="prenotazione-note"
              {...register('note')}
              rows={3}
              placeholder="Allergie, seggiolone, tavolo tranquillo…"
              className="text-corpo w-full rounded-md border border-input bg-transparent px-2.5 py-1.5 outline-none transition-colors placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 dark:bg-input/30"
            />
          </div>

          {/* Il verbo del pulsante e' quello dell'azione, non "Invia": chi legge sa gia' cosa
              succede quando lo preme. */}
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="ghost" onClick={onClose}>
              Annulla
            </Button>
            <Button type="submit" disabled={inCorso || oltreLimiteOnline || fasciaNonPrenotabile}>
              {inCorso
                ? 'Salvataggio…'
                : inModifica
                  ? 'Salva modifiche'
                  : 'Crea prenotazione'}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}
