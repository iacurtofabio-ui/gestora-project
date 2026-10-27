import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { AxiosError, AxiosHeaders } from 'axios'
import AccountPage from '@/pages/AccountPage'
import { useMioAccount } from '@/hooks/useAccount'

/** V2-019 — «Il mio account»: dati dell'utente e cambio password. */
const mutate = vi.fn()

vi.mock('@/hooks/useAccount', () => ({
  useMioAccount: vi.fn(),
  useCambiaPassword: () => ({ mutate, isPending: false }),
  useCancellaAccount: () => ({ mutate: vi.fn(), isPending: false }),
}))

vi.mocked(useMioAccount).mockReturnValue({
  isPending: false,
  isError: false,
  data: {
    userName: 'Mario Rossi',
    email: 'mario@example.com',
    ruoli: ['Cliente'],
    dataCreazione: '2026-10-01T08:00:00Z',
    ultimoAccesso: null,
    privacyAccettataIl: null,
    privacyVersione: null,
  },
} as unknown as ReturnType<typeof useMioAccount>)

async function compila(utente: ReturnType<typeof userEvent.setup>, conferma = 'Nuova1!pass') {
  await utente.type(screen.getByLabelText('Password attuale'), 'Vecchia1!')
  await utente.type(screen.getByLabelText('Nuova password'), 'Nuova1!pass')
  await utente.type(screen.getByLabelText('Ripeti la nuova password'), conferma)
  await utente.click(screen.getByRole('button', { name: 'Cambia password' }))
}

describe('AccountPage', () => {
  beforeEach(() => {
    mutate.mockReset()
  })

  it('mostra i dati dell utente', () => {
    render(<AccountPage />, { wrapper: MemoryRouter })

    expect(screen.getByText('Mario Rossi')).toBeInTheDocument()
    expect(screen.getByText('mario@example.com')).toBeInTheDocument()
    expect(screen.getByText(/non ancora letta/)).toBeInTheDocument()
  })

  it('password ripetuta diversa: non invia', async () => {
    const utente = userEvent.setup()
    render(<AccountPage />, { wrapper: MemoryRouter })

    await compila(utente, 'Altra1!pass')

    expect(mutate).not.toHaveBeenCalled()
    expect(screen.getByText('Le due password non coincidono.')).toBeInTheDocument()
  })

  it('password attuale sbagliata: il messaggio del server va sotto il campo', async () => {
    const utente = userEvent.setup()
    mutate.mockImplementation((_dati: unknown, { onError }: { onError: (e: unknown) => void }) => {
      onError(
        new AxiosError('400', '400', undefined, undefined, {
          status: 400,
          statusText: 'Bad Request',
          headers: {},
          config: { headers: new AxiosHeaders() },
          data: { errors: [{ field: 'passwordAttuale', error: 'La password attuale non è corretta.' }] },
        })
      )
    })
    render(<AccountPage />, { wrapper: MemoryRouter })

    await compila(utente)

    expect(mutate).toHaveBeenCalledWith(
      { passwordAttuale: 'Vecchia1!', nuovaPassword: 'Nuova1!pass' },
      expect.anything()
    )
    expect(screen.getByText('La password attuale non è corretta.')).toBeInTheDocument()
  })
})
