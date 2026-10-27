import { Fragment, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { isAxiosError } from 'axios'
import { PlusIcon } from 'lucide-react'
import { ReportPrenotazioni } from '@/components/ReportPrenotazioni'
import {
  usePrenotazioni,
  useConfermaPrenotazione,
  useCompletaPrenotazione,
  useAnnullaPrenotazione,
  useDeletePrenotazione,
} from '@/hooks/usePrenotazioni'
import PrenotazioneModal from '@/components/PrenotazioneModal'
import ConfirmDialog from '@/components/ConfirmDialog'
import NotaPrenotazioneDialog from '@/components/NotaPrenotazioneDialog'
import Paginazione from '@/components/Paginazione'
import { PageError, TableSkeleton } from '@/components/PageState'
import { EmptyState } from '@/components/EmptyState'
import { StatoBadge } from '@/components/StatoBadge'
import { AzioniPrenotazione } from '@/components/AzioniPrenotazione'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { STATI_PRENOTAZIONE, type PrenotazioneDTO } from '@/types/prenotazione'
import { useAuth } from '@/hooks/useAuth'
import { dataBreveInItalia, dataEstesaInItalia } from '@/lib/date'

// REV-043: 20 righe stanno in una schermata senza scorrere. Il backend accetta al massimo 100.
const RIGHE_PER_PAGINA = 20

// Radix non ammette la stringa vuota come valore di un'opzione: la usa internamente per "niente
// selezionato". Serve quindi un valore vero per "nessun filtro", tradotto in `undefined` prima di
// arrivare alla chiamata.
const STATO_TUTTI = 'tutti'

const OPZIONI_STATO = [
  { value: STATO_TUTTI, label: 'Tutti gli stati' },
  { value: STATI_PRENOTAZIONE.ATTIVA, label: 'Attiva' },
  { value: STATI_PRENOTAZIONE.IN_CORSO, label: 'Confermata' },
  { value: STATI_PRENOTAZIONE.COMPLETATA, label: 'Completata' },
  { value: STATI_PRENOTAZIONE.ANNULLATA, label: 'Annullata' },
  { value: STATI_PRENOTAZIONE.NON_PRESENTATA, label: 'Non presentata' },
]

/**
 * Le larghezze reali delle colonne. Servono due volte: alla tabella, perche' le colonne non
 * ballino da una pagina all'altra, e allo scheletro di caricamento, perche' disegni la forma
 * che poi arriva davvero (vedi TableSkeleton).
 */
const COLONNE = [
  '7.5rem',
  'minmax(8rem,1fr)',
  '7.5rem',
  '5.5rem',
  '8rem',
  'minmax(8rem,1fr)',
  '8rem',
]

const postiTotali = (postazioni: { numeroPosti: number }[]) =>
  postazioni.reduce((somma, pos) => somma + pos.numeroPosti, 0)

export default function PrenotazionePage() {
  const { user } = useAuth()
  const isStaff = Boolean(user?.roles.includes('Admin') || user?.roles.includes('Staff'))
  // NEW-004: l'eliminazione e' riservata all'Admin (l'endpoint e' [Authorize(Roles = Admin)]).
  const isAdmin = Boolean(user?.roles.includes('Admin'))

  // FASE 7: i filtri vivono nell'URL, non solo nello stato del componente — cosi' sono
  // condivisibili (un link con ?data=... apre la pagina gia' filtrata) e sopravvivono a un
  // ricaricamento della pagina, cosa che lo stato locale non faceva.
  const [searchParams, setSearchParams] = useSearchParams()
  const filtroData = searchParams.get('data') ?? ''
  const filtroStato = searchParams.get('stato') ?? STATO_TUTTI
  const filtroFascia = searchParams.get('fascia')
  const pagina = Number(searchParams.get('pagina') ?? '1') || 1

  // Cambiando filtro il numero di pagine cambia: restando sulla pagina corrente si puo' finire
  // oltre l'ultima e vedere una tabella vuota che sembra "nessun risultato".
  function cambiaFiltroData(valore: string) {
    setSearchParams((sp) => {
      const nuovi = new URLSearchParams(sp)
      if (valore) nuovi.set('data', valore)
      else nuovi.delete('data')
      nuovi.delete('pagina')
      return nuovi
    })
  }

  function cambiaFiltroStato(valore: string) {
    setSearchParams((sp) => {
      const nuovi = new URLSearchParams(sp)
      if (valore === STATO_TUTTI) nuovi.delete('stato')
      else nuovi.set('stato', valore)
      nuovi.delete('pagina')
      return nuovi
    })
  }

  function cambiaPagina(numero: number) {
    setSearchParams((sp) => {
      const nuovi = new URLSearchParams(sp)
      if (numero <= 1) nuovi.delete('pagina')
      else nuovi.set('pagina', String(numero))
      return nuovi
    })
  }

  // V2-007: dopo una creazione lo Staff va al giorno della prenotazione. Senza, la nuova riga
  // poteva finire in fondo a molte pagine e sembrava non salvata. Via anche stato e fascia, che
  // potrebbero nasconderla.
  function mostraGiorno(data: string) {
    setSearchParams({ data })
  }

  function azzeraFiltri() {
    setSearchParams({})
  }

  const filtriAttivi = filtroData !== '' || filtroStato !== STATO_TUTTI || filtroFascia !== null

  const [isModalOpen, setIsModalOpen] = useState(false)
  // NEW-001: lo stesso modal serve creazione e modifica. Se questa e' valorizzata il modal si
  // apre precompilato e salva con PUT, altrimenti crea.
  const [prenotazioneDaModificare, setPrenotazioneDaModificare] = useState<
    PrenotazioneDTO | undefined
  >(undefined)
  const [idDaAnnullare, setIdDaAnnullare] = useState<number | undefined>(undefined)
  const [idDaEliminare, setIdDaEliminare] = useState<number | undefined>(undefined)
  const [notaAperta, setNotaAperta] = useState<{ cliente: string; testo: string } | undefined>(
    undefined,
  )

  function apriNuovaPrenotazione() {
    setPrenotazioneDaModificare(undefined)
    setIsModalOpen(true)
  }

  function apriModificaPrenotazione(p: PrenotazioneDTO) {
    setPrenotazioneDaModificare(p)
    setIsModalOpen(true)
  }

  function chiudiModal() {
    setIsModalOpen(false)
    setPrenotazioneDaModificare(undefined)
  }

  const prenotazioni = usePrenotazioni({
    data: filtroData || undefined,
    stato: filtroStato === STATO_TUTTI ? undefined : filtroStato,
    fasciaOrariaId: filtroFascia ? Number(filtroFascia) : undefined,
    page: pagina,
    pageSize: RIGHE_PER_PAGINA,
  })

  const conferma = useConfermaPrenotazione()
  const completa = useCompletaPrenotazione()
  const annulla = useAnnullaPrenotazione()
  const elimina = useDeletePrenotazione()

  // Il 403 e' un caso a parte: non e' un'attesa ne' un guasto, e' un permesso negato. Su questa
  // rotta condivisa fra i tre ruoli non ha senso mostrare filtri e pulsante "Aggiungi" a chi non
  // puo' vedere nessuna riga.
  if (prenotazioni.isError) {
    const status = isAxiosError(prenotazioni.error)
      ? prenotazioni.error.response?.status
      : undefined
    if (status === 403)
      return (
        <div className="mx-auto max-w-md rounded-xl border bg-card p-6 text-center">
          <p className="text-sezione">Questa sezione non e' aperta al tuo ruolo</p>
          <p className="text-corpo mt-1 text-muted-foreground text-pretty">
            Serve un profilo Staff o Admin per vedere le prenotazioni della sala. Chiedi
            all'amministratore di aggiungerti il ruolo.
          </p>
        </div>
      )
  }

  const numeroColonne = COLONNE.length

  return (
    <div className="mx-auto max-w-6xl space-y-4">
      {/* Il titolo sta FUORI dal contenitore della tabella: e' il titolo della schermata, non
          l'intestazione di una card fra le tante. */}
      <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
        <h1 className="text-titolo">Prenotazioni</h1>
        {!prenotazioni.isLoading && !prenotazioni.isError && (
          <p className="text-nota text-muted-foreground tabular-nums">
            {prenotazioni.data?.totalCount ?? 0} in elenco
          </p>
        )}
      </div>

      <div className="overflow-hidden rounded-xl border bg-card">
        {/* Barra dei filtri — REV-074: resta visibile durante il caricamento e in caso di errore. */}
        <div className="flex flex-col gap-3 border-b p-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex flex-wrap items-center gap-2">
            {isStaff && (
              <>
                <Input
                  type="date"
                  aria-label="Filtra per data"
                  className="h-8 w-auto"
                  value={filtroData}
                  onChange={(e) => cambiaFiltroData(e.target.value)}
                />
                <Select value={filtroStato} onValueChange={cambiaFiltroStato}>
                  <SelectTrigger className="h-8 w-[168px]" aria-label="Filtra per stato">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {OPZIONI_STATO.map((o) => (
                      <SelectItem key={o.value} value={o.value}>
                        {o.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {filtriAttivi && (
                  <Button size="sm" variant="ghost" onClick={azzeraFiltri}>
                    Azzera i filtri
                  </Button>
                )}
              </>
            )}
          </div>

          <div className="flex flex-wrap items-center gap-2 self-start sm:self-auto">
            {/* V2-025: il report contiene i nomi dei clienti, quindi solo per l'Admin. */}
            {isAdmin && <ReportPrenotazioni />}
            <Button size="sm" onClick={apriNuovaPrenotazione}>
              <PlusIcon />
              Aggiungi prenotazione
            </Button>
          </div>
        </div>

        {prenotazioni.isLoading ? (
          <div className="p-3">
            <TableSkeleton righe={8} colonne={COLONNE} />
          </div>
        ) : prenotazioni.isError ? (
          <PageError
            error={prenotazioni.error}
            fallback="Errore nel caricamento delle prenotazioni."
            onRiprova={() => prenotazioni.refetch()}
          />
        ) : (
          <div className="overflow-x-auto">
            <Table style={{ tableLayout: 'fixed', minWidth: '56rem' }}>
              <colgroup>
                {COLONNE.map((larghezza, i) => (
                  <col key={i} style={{ width: larghezza }} />
                ))}
              </colgroup>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead className="text-nota font-medium text-muted-foreground">Data</TableHead>
                  <TableHead className="text-nota font-medium text-muted-foreground">
                    Cliente
                  </TableHead>
                  <TableHead className="text-nota font-medium text-muted-foreground">
                    Orario
                  </TableHead>
                  <TableHead className="text-nota text-right font-medium text-muted-foreground">
                    Coperti
                  </TableHead>
                  <TableHead className="text-nota font-medium text-muted-foreground">
                    Stato
                  </TableHead>
                  <TableHead className="text-nota font-medium text-muted-foreground">
                    Tavoli
                  </TableHead>
                  <TableHead className="text-nota text-right font-medium text-muted-foreground">
                    Azioni
                  </TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {prenotazioni.data?.items.length === 0 ? (
                  <EmptyState
                    colSpan={numeroColonne}
                    messaggio={
                      filtriAttivi
                        ? 'Nessuna prenotazione con questi filtri. Prova con un altro giorno o un altro stato.'
                        : 'Non c’e’ ancora nessuna prenotazione. La prima la puoi inserire da qui, anche per una richiesta arrivata al telefono.'
                    }
                    azione={
                      filtriAttivi ? (
                        <Button size="sm" variant="outline" onClick={azzeraFiltri}>
                          Azzera i filtri
                        </Button>
                      ) : (
                        <Button size="sm" onClick={apriNuovaPrenotazione}>
                          <PlusIcon />
                          Aggiungi prenotazione
                        </Button>
                      )
                    }
                  />
                ) : (
                  prenotazioni.data?.items.map((p, indice) => {
                    // FASE 4: separatore a tutta larghezza quando cambia la data - solo per
                    // Staff/Admin, dove la tabella scorre su più giornate e più turni.
                    const precedente = prenotazioni.data?.items[indice - 1]
                    const cambiaGiorno = isStaff && p.dataPrenotazione !== precedente?.dataPrenotazione

                    return (
                      <Fragment key={p.id}>
                        {cambiaGiorno && (
                          <TableRow className="hover:bg-transparent">
                            <TableCell
                              colSpan={COLONNE.length}
                              className="text-nota bg-muted py-1.5 uppercase text-muted-foreground"
                            >
                              {dataEstesaInItalia(p.dataPrenotazione)}
                            </TableCell>
                          </TableRow>
                        )}
                        <TableRow className="group/riga">
                          <TableCell className="text-corpo whitespace-nowrap">
                            {dataBreveInItalia(p.dataPrenotazione)}
                          </TableCell>
                          <TableCell className="text-corpo font-medium">
                            <div className="truncate">{p.nomeCliente ?? p.nomeUtente}</div>
                            {/* Anteprima di due righe; un clic apre la nota per intero. */}
                            {p.note && (
                              <button
                                type="button"
                                className="text-nota line-clamp-2 w-full cursor-pointer whitespace-normal text-left font-normal text-muted-foreground hover:text-foreground focus-visible:outline-2 focus-visible:outline-ring"
                                title="Clicca per leggere la nota per intero"
                                onClick={() =>
                                  setNotaAperta({
                                    cliente: p.nomeCliente ?? p.nomeUtente ?? 'Cliente',
                                    testo: p.note!,
                                  })
                                }
                              >
                                {p.note}
                              </button>
                            )}
                          </TableCell>
                          {/* L'orario e' il dato che si cerca per primo: un gradino piu' grande e
                              piu' pesante del resto della riga. Il turno sotto, in piccolo: la
                              colonna Data non basta piu' a distinguere due turni della stessa sera. */}
                          <TableCell className="text-orario tabular-nums">
                            <div>
                              {p.oraInizio?.slice(0, 5)}–{p.oraFine?.slice(0, 5)}
                            </div>
                            {p.numeroTurno > 0 && (
                              <div className="text-nota text-muted-foreground">
                                {p.numeroTurno}º turno
                              </div>
                            )}
                          </TableCell>
                          <TableCell className="text-orario text-right tabular-nums">
                            {p.numeroCoperti}
                          </TableCell>
                          <TableCell>
                            <StatoBadge stato={p.stato} />
                          </TableCell>
                          <TableCell className="text-corpo">
                            {p.postazioni.length === 0 ? (
                              <span className="text-muted-foreground">—</span>
                            ) : (
                              <>
                                {/* Zona in piccolo sopra, poi i tavoli con i posti in totale. */}
                                {p.postazioni[0]?.nomeZona && (
                                  <div className="text-nota text-muted-foreground">
                                    {p.postazioni[0].nomeZona}
                                  </div>
                                )}
                                {/* Più tavoli uniti: un'unica riga "Tavolo 1-2 · 6 posti" col totale. */}
                                <div className="whitespace-nowrap">
                                  Tavolo {p.postazioni.map((pos) => pos.numero).join('-')}
                                  {postiTotali(p.postazioni) > 0 && (
                                    <span className="text-nota text-muted-foreground">
                                      {' '}
                                      · {postiTotali(p.postazioni)}{' '}
                                      {postiTotali(p.postazioni) === 1 ? 'posto' : 'posti'}
                                    </span>
                                  )}
                                </div>
                              </>
                            )}
                          </TableCell>
                          <TableCell>
                            <AzioniPrenotazione
                              prenotazione={p}
                              isStaff={isStaff}
                              isAdmin={isAdmin}
                              inCorso={
                                (conferma.isPending && conferma.variables === p.id) ||
                                (completa.isPending && completa.variables === p.id)
                              }
                              onConferma={() => conferma.mutate(p.id)}
                              onCompleta={() => completa.mutate(p.id)}
                              onModifica={() => apriModificaPrenotazione(p)}
                              onAnnulla={() => setIdDaAnnullare(p.id)}
                              onElimina={() => setIdDaEliminare(p.id)}
                            />
                          </TableCell>
                        </TableRow>
                      </Fragment>
                    )
                  })
                )}
              </TableBody>
            </Table>
          </div>
        )}

        {!prenotazioni.isLoading && !prenotazioni.isError && (
          <Paginazione
            pagina={prenotazioni.data?.page ?? 1}
            paginePresenti={prenotazioni.data?.totalPages ?? 1}
            totaleElementi={prenotazioni.data?.totalCount ?? 0}
            elementiInPagina={prenotazioni.data?.items.length ?? 0}
            pageSize={RIGHE_PER_PAGINA}
            inCaricamento={prenotazioni.isFetching}
            onCambioPagina={cambiaPagina}
          />
        )}
      </div>

      <PrenotazioneModal
        isOpen={isModalOpen}
        onClose={chiudiModal}
        prenotazione={prenotazioneDaModificare}
        onCreata={isStaff ? mostraGiorno : undefined}
      />

      {/* Il dialogo di conferma resta riservato alle sole azioni che non si possono rifare.
          Il verbo del pulsante e' lo stesso della voce di menu che ha portato qui. */}
      <ConfirmDialog
        open={idDaAnnullare !== undefined}
        titolo="Annullare questa prenotazione?"
        testoConferma="Annulla prenotazione"
        descrizione="I tavoli assegnati tornano subito disponibili per altre prenotazioni. La prenotazione resta nello storico come annullata."
        onConfirm={() => {
          annulla.mutate(idDaAnnullare!)
          setIdDaAnnullare(undefined)
        }}
        onCancel={() => setIdDaAnnullare(undefined)}
      />
      <NotaPrenotazioneDialog nota={notaAperta} onClose={() => setNotaAperta(undefined)} />
      <ConfirmDialog
        open={idDaEliminare !== undefined}
        titolo="Eliminare definitivamente?"
        testoConferma="Elimina definitivamente"
        descrizione="La prenotazione annullata sparisce dallo storico e non si puo' recuperare."
        onConfirm={() => {
          elimina.mutate(idDaEliminare!)
          setIdDaEliminare(undefined)
        }}
        onCancel={() => setIdDaEliminare(undefined)}
      />
    </div>
  )
}
