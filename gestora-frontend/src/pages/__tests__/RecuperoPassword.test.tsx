import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import PasswordDimenticataPage from '@/pages/PasswordDimenticataPage'
import ReimpostaPasswordPage from '@/pages/ReimpostaPasswordPage'
import apiClient from '@/lib/axios'
import { Endpoints } from '@/lib/endpoints'

/** V2-019 — recupero password: richiesta del link e scelta della nuova password. */
vi.mock('@/lib/axios', () => ({ default: { post: vi.fn() } }))

const post = vi.mocked(apiClient.post)

function apriReimposta(indirizzo: string) {
  render(
    <MemoryRouter initialEntries={[indirizzo]}>
      <ReimpostaPasswordPage />
    </MemoryRouter>
  )
}

describe('Recupero password', () => {
  beforeEach(() => {
    post.mockReset()
    post.mockResolvedValue({ data: 'ok' })
  })

  it('password dimenticata: invia l email e risponde senza dire se esiste', async () => {
    const utente = userEvent.setup()
    render(<PasswordDimenticataPage />, { wrapper: MemoryRouter })

    await utente.type(screen.getByPlaceholderText('Email'), 'mario@example.com')
    await utente.click(screen.getByRole('button', { name: 'Mandami il link' }))

    expect(post).toHaveBeenCalledWith(Endpoints.auth.passwordDimenticata, { email: 'mario@example.com' })
    expect(screen.getByRole('heading', { name: 'Controlla la tua email' })).toBeInTheDocument()
    expect(screen.getByText(/riceverai un’email con il link/)).toHaveTextContent('Se mario@example.com è registrata')
  })

  it('link senza userId o token: niente form, si chiede un nuovo link', () => {
    apriReimposta('/reimposta-password?userId=u1')

    expect(screen.getByRole('heading', { name: 'Link non valido' })).toBeInTheDocument()
    expect(screen.queryByPlaceholderText('Nuova password')).not.toBeInTheDocument()
  })

  it('password ripetuta diversa: non invia', async () => {
    const utente = userEvent.setup()
    apriReimposta('/reimposta-password?userId=u1&token=abc')

    await utente.type(screen.getByPlaceholderText('Nuova password'), 'Nuova1!pass')
    await utente.type(screen.getByPlaceholderText('Ripeti la password'), 'Altra1!pass')
    await utente.click(screen.getByRole('button', { name: 'Salva la nuova password' }))

    expect(post).not.toHaveBeenCalled()
    expect(screen.getByText('Le due password non coincidono.')).toBeInTheDocument()
  })

  it('password valida: invia userId e token del link e conferma', async () => {
    const utente = userEvent.setup()
    apriReimposta('/reimposta-password?userId=u1&token=abc')

    await utente.type(screen.getByPlaceholderText('Nuova password'), 'Nuova1!pass')
    await utente.type(screen.getByPlaceholderText('Ripeti la password'), 'Nuova1!pass')
    await utente.click(screen.getByRole('button', { name: 'Salva la nuova password' }))

    expect(post).toHaveBeenCalledWith(Endpoints.auth.reimpostaPassword, {
      userId: 'u1',
      token: 'abc',
      nuovaPassword: 'Nuova1!pass',
    })
    expect(screen.getByRole('heading', { name: 'Password aggiornata' })).toBeInTheDocument()
  })
})
