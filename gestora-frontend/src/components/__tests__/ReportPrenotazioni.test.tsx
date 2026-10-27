import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ReportPrenotazioni } from '@/components/ReportPrenotazioni'
import { giorniCompresi, meseDi } from '@/lib/date'

/** V2-025 — report CSV per l'Admin: periodo proposto, controlli e richiesta di scaricamento. */
const mutate = vi.fn()

vi.mock('@/hooks/useReportPrenotazioni', () => ({
  useScaricaReport: () => ({ mutate, isPending: false }),
}))

vi.mock('@/lib/date', async (originale) => ({
  ...(await originale<typeof import('@/lib/date')>()),
  oggiInItalia: () => '2026-02-14',
}))

async function apri(utente: ReturnType<typeof userEvent.setup>) {
  render(<ReportPrenotazioni />)
  await utente.click(screen.getByRole('button', { name: 'Scarica report' }))
}

async function imposta(utente: ReturnType<typeof userEvent.setup>, campo: string, valore: string) {
  const input = screen.getByLabelText(campo)
  await utente.clear(input)
  await utente.type(input, valore)
}

describe('ReportPrenotazioni', () => {
  beforeEach(() => {
    mutate.mockReset()
  })

  it('propone il mese in corso e scarica quel periodo', async () => {
    const utente = userEvent.setup()
    await apri(utente)

    expect(screen.getByLabelText('Dal')).toHaveValue('2026-02-01')
    expect(screen.getByLabelText('Al')).toHaveValue('2026-02-28')

    await utente.click(screen.getByRole('button', { name: 'Scarica CSV' }))

    expect(mutate).toHaveBeenCalledWith({ da: '2026-02-01', a: '2026-02-28' }, expect.anything())
  })

  it('data finale prima di quella iniziale: errore, niente scaricamento', async () => {
    const utente = userEvent.setup()
    await apri(utente)
    await imposta(utente, 'Al', '2026-01-31')

    await utente.click(screen.getByRole('button', { name: 'Scarica CSV' }))

    expect(mutate).not.toHaveBeenCalled()
    expect(screen.getByText('La data finale non può essere prima di quella iniziale.')).toBeInTheDocument()
  })

  it('più di un anno: errore, niente scaricamento', async () => {
    const utente = userEvent.setup()
    await apri(utente)
    await imposta(utente, 'Dal', '2025-01-01')

    await utente.click(screen.getByRole('button', { name: 'Scarica CSV' }))

    expect(mutate).not.toHaveBeenCalled()
    expect(screen.getByText('Il periodo può essere al massimo di un anno.')).toBeInTheDocument()
  })
})

describe('date del report', () => {
  it('meseDi: primo e ultimo giorno, anche a febbraio bisestile', () => {
    expect(meseDi('2026-10-27')).toEqual({ primo: '2026-10-01', ultimo: '2026-10-31' })
    expect(meseDi('2028-02-10')).toEqual({ primo: '2028-02-01', ultimo: '2028-02-29' })
  })

  it('giorniCompresi: estremi compresi', () => {
    expect(giorniCompresi('2026-10-01', '2026-10-01')).toBe(1)
    expect(giorniCompresi('2026-10-01', '2026-10-31')).toBe(31)
    expect(giorniCompresi('2026-01-01', '2026-12-31')).toBe(365)
  })
})
