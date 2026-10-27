import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import RegisterPage from '@/pages/RegisterPage'
import apiClient from '@/lib/axios'

/**
 * V2-019 — senza la casella dell'informativa la registrazione non parte; con la casella la
 * richiesta porta accettaPrivacy, che il backend ricontrolla.
 */
vi.mock('@/lib/axios', () => ({ default: { post: vi.fn() } }))

const post = vi.mocked(apiClient.post)

async function compila(utente: ReturnType<typeof userEvent.setup>) {
  await utente.type(screen.getByPlaceholderText('Username'), 'Mario Rossi')
  await utente.type(screen.getByPlaceholderText('Email'), 'mario@example.com')
  await utente.type(screen.getByPlaceholderText('Password'), 'Password1!')
}

describe('RegisterPage - presa visione dell informativa', () => {
  beforeEach(() => {
    post.mockReset()
    post.mockResolvedValue({ data: { emailInviata: true } })
  })

  it('senza la casella non invia e spiega perche', async () => {
    const utente = userEvent.setup()
    render(<RegisterPage />, { wrapper: MemoryRouter })

    await compila(utente)
    await utente.click(screen.getByRole('button', { name: 'Crea account' }))

    expect(post).not.toHaveBeenCalled()
    expect(
      screen.getByText('Per registrarti devi dichiarare di aver letto l’informativa privacy.')
    ).toBeInTheDocument()
  })

  it('con la casella invia accettaPrivacy', async () => {
    const utente = userEvent.setup()
    render(<RegisterPage />, { wrapper: MemoryRouter })

    await compila(utente)
    await utente.click(screen.getByRole('checkbox'))
    await utente.click(screen.getByRole('button', { name: 'Crea account' }))

    expect(post).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({ accettaPrivacy: true })
    )
  })
})
