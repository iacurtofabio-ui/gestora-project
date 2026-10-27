import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AxiosError, AxiosHeaders } from 'axios'
import { CancellaAccountCard } from '@/components/CancellaAccountCard'

/**
 * V2-019 — cancellazione dell'account: si conferma con la password, e il motivo di un rifiuto
 * (password sbagliata, prenotazioni da servire) si legge nella finestra.
 */
const mutate = vi.fn()

vi.mock('@/hooks/useAccount', () => ({
  useCancellaAccount: () => ({ mutate, isPending: false }),
}))

function erroreServer(status: number, data: unknown) {
  return new AxiosError(String(status), String(status), undefined, undefined, {
    status,
    statusText: '',
    headers: {},
    config: { headers: new AxiosHeaders() },
    data,
  })
}

async function apriEConferma(utente: ReturnType<typeof userEvent.setup>, password?: string) {
  await utente.click(screen.getByRole('button', { name: 'Cancella il mio account' }))
  if (password) await utente.type(screen.getByLabelText('Password'), password)
  await utente.click(screen.getByRole('button', { name: 'Cancella definitivamente' }))
}

describe('CancellaAccountCard', () => {
  beforeEach(() => {
    mutate.mockReset()
  })

  it('senza password non invia', async () => {
    const utente = userEvent.setup()
    render(<CancellaAccountCard />)

    await apriEConferma(utente)

    expect(mutate).not.toHaveBeenCalled()
    expect(screen.getByText('Scrivi la tua password per confermare.')).toBeInTheDocument()
  })

  it('password sbagliata: il messaggio va sotto il campo', async () => {
    const utente = userEvent.setup()
    mutate.mockImplementation((_password: string, { onError }: { onError: (e: unknown) => void }) => {
      onError(erroreServer(400, { errors: [{ field: 'password', error: 'La password non è corretta.' }] }))
    })
    render(<CancellaAccountCard />)

    await apriEConferma(utente, 'Sbagliata1!')

    expect(mutate).toHaveBeenCalledWith('Sbagliata1!', expect.anything())
    expect(screen.getByText('La password non è corretta.')).toBeInTheDocument()
  })

  it('prenotazioni da servire: si legge il motivo del blocco', async () => {
    const utente = userEvent.setup()
    mutate.mockImplementation((_password: string, { onError }: { onError: (e: unknown) => void }) => {
      onError(erroreServer(409, { message: 'Hai ancora prenotazioni da servire: annullale prima.' }))
    })
    render(<CancellaAccountCard />)

    await apriEConferma(utente, 'Mia1!pass')

    expect(screen.getByText('Hai ancora prenotazioni da servire: annullale prima.')).toBeInTheDocument()
  })
})
