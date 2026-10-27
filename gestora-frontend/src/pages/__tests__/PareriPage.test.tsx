import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { AxiosError, AxiosHeaders } from 'axios'
import PareriPage from '@/pages/PareriPage'
import apiClient from '@/lib/axios'
import { Endpoints } from '@/lib/endpoints'

/** V2-022 — modulo pubblico dei pareri. */
vi.mock('@/lib/axios', () => ({ default: { post: vi.fn() } }))

const post = vi.mocked(apiClient.post)

function apri() {
  render(<PareriPage />, { wrapper: MemoryRouter })
}

describe('PareriPage', () => {
  beforeEach(() => {
    post.mockReset()
    post.mockResolvedValue({ data: 'ok' })
  })

  it('senza messaggio non invia e lo dice', async () => {
    const utente = userEvent.setup()
    apri()

    await utente.click(screen.getByRole('button', { name: 'Invia il parere' }))

    expect(post).not.toHaveBeenCalled()
    expect(screen.getByText('Scrivi il tuo parere.')).toBeInTheDocument()
  })

  it('email scritta male: errore sotto il campo, niente invio', async () => {
    const utente = userEvent.setup()
    apri()

    await utente.type(screen.getByLabelText('Il tuo parere'), 'Bravi')
    await utente.type(screen.getByLabelText(/^Email/), 'non-una-email')
    await utente.click(screen.getByRole('button', { name: 'Invia il parere' }))

    expect(post).not.toHaveBeenCalled()
    expect(screen.getByText('Inserire un indirizzo email valido.')).toBeInTheDocument()
  })

  it('solo il messaggio: invia con nome ed email vuoti e ringrazia', async () => {
    const utente = userEvent.setup()
    apri()

    await utente.type(screen.getByLabelText('Il tuo parere'), 'Ottimo servizio')
    await utente.click(screen.getByRole('button', { name: 'Invia il parere' }))

    expect(post).toHaveBeenCalledWith(Endpoints.pareri.invia, {
      messaggio: 'Ottimo servizio',
      nome: null,
      email: null,
      sitoWeb: null,
    })
    expect(screen.getByRole('heading', { name: 'Grazie per il tuo parere' })).toBeInTheDocument()
  })

  it('oltre il limite mensile: lo dice, senza invitare a riprovare', async () => {
    const utente = userEvent.setup()
    post.mockRejectedValue(
      new AxiosError('429', '429', undefined, undefined, {
        status: 429,
        statusText: 'Too Many Requests',
        headers: {},
        config: { headers: new AxiosHeaders() },
        data: '',
      })
    )
    apri()

    await utente.type(screen.getByLabelText('Il tuo parere'), 'Terzo parere')
    await utente.click(screen.getByRole('button', { name: 'Invia il parere' }))

    expect(screen.getByText(/Hai già lasciato i tuoi pareri per questo mese/)).toBeInTheDocument()
  })

  it('il campo trappola non è raggiungibile con il tabulatore', () => {
    apri()

    expect(screen.getByLabelText('Sito web', { selector: 'input' })).toHaveAttribute('tabindex', '-1')
  })
})
