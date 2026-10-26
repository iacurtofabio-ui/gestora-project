import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import PrenotazioneModal from '@/components/PrenotazioneModal'
import type { FasciaDisponibilitaDTO } from '@/types/disponibilita'
import type { PrenotazioneDTO } from '@/types/prenotazione'

/**
 * V2-024 — con la fascia scelta non disponibile, in creazione «Salva» è spento: prima l'avviso
 * compariva ma si poteva salvare lo stesso, e il backend rispondeva 409. In modifica resta acceso,
 * perché la prenotazione stessa occupa i tavoli che la verifica conta come presi.
 */
vi.mock('@/hooks/useFasceOrarie', () => ({
  useFascePerGiorno: () => ({
    data: [{ id: 1, orarioInizio: '19:00', orarioFine: '21:00', giornoSettimana: 1, attiva: true, maxCoperti: 8 }],
  }),
}))

vi.mock('@/hooks/useZone', () => ({
  useZoneAttive: () => ({ data: [{ id: 1, nome: 'Sala', attiva: true }] }),
}))

vi.mock('@/hooks/usePrenotazioni', () => ({
  useCreaPrenotazione: () => ({ mutate: vi.fn(), isPending: false }),
  useModificaPrenotazione: () => ({ mutate: vi.fn(), isPending: false }),
}))

// Il caso segnalato: un tavolo da 8 già assegnato, nessun posto prenotabile.
const FASCIA_PIENA: FasciaDisponibilitaDTO = {
  fasciaOrariaId: 1,
  orarioInizio: '19:00',
  orarioFine: '21:00',
  maxCoperti: 8,
  postiResiduiFascia: 0,
  totalePostiDisponibili: 0,
  totaleCapienza: 8,
  disponibilePerRichiesta: false,
  messaggio: 'Nessun tavolo libero in questa fascia: sono già tutti assegnati.',
  motivo: 'TavoliInsufficienti',
}

vi.mock('@/hooks/useDisponibilita', () => ({
  useCheckDisponibilita: () => ({ data: { fasce: [FASCIA_PIENA] }, isLoading: false }),
  useLimitiPrenotazione: () => ({ data: undefined }),
}))

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({ user: { id: '1', email: 'staff@gestora.it', roles: ['Staff'], token: 't' } }),
}))

/** Lunedì 7 settembre 2026. */
const LUNEDI = '2026-09-07'

const PRENOTAZIONE: PrenotazioneDTO = {
  id: 1,
  dataPrenotazione: LUNEDI,
  numeroCoperti: 5,
  note: null,
  stato: 'Attiva',
  nomeUtente: 'rossi@mail.it',
  nomeCliente: 'Rossi',
  oraInizio: '19:00',
  oraFine: '21:00',
  fasciaOrariaId: 1,
  postazioni: [],
  numeroTurno: 1,
}

describe('PrenotazioneModal - fascia non disponibile', () => {
  it('in creazione mostra l avviso e non lascia salvare', async () => {
    const utente = userEvent.setup()
    render(<PrenotazioneModal isOpen onClose={() => {}} />)

    await utente.type(screen.getByLabelText('Data'), LUNEDI)
    await utente.selectOptions(screen.getAllByRole('combobox')[0], '1')

    expect(screen.getByText(FASCIA_PIENA.messaggio!)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Crea prenotazione' })).toBeDisabled()
  })

  it('in modifica lascia salvare', async () => {
    render(<PrenotazioneModal isOpen onClose={() => {}} prenotazione={PRENOTAZIONE} />)

    expect(await screen.findByRole('button', { name: 'Salva modifiche' })).toBeEnabled()
  })
})
