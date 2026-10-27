import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { PresaVisionePrivacy } from '@/components/PresaVisionePrivacy'
import { useStatoPrivacy } from '@/hooks/usePrivacy'

/**
 * V2-019 — nell'area riservata le pagine si vedono solo dopo la presa visione dell'informativa,
 * ma un errore nella verifica non deve chiudere fuori nessuno.
 */
const accetta = vi.fn()

vi.mock('@/hooks/usePrivacy', () => ({
  useStatoPrivacy: vi.fn(),
  useAccettaPrivacy: () => ({ mutate: accetta, isPending: false }),
}))

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({ user: { id: '1', email: 'staff@gestora.it', roles: ['Staff'], token: 't' } }),
}))

const stato = vi.mocked(useStatoPrivacy)

function monta() {
  render(
    <MemoryRouter>
      <PresaVisionePrivacy>
        <p>Pagina riservata</p>
      </PresaVisionePrivacy>
    </MemoryRouter>
  )
}

describe('PresaVisionePrivacy', () => {
  beforeEach(() => {
    accetta.mockReset()
    stato.mockReset()
  })

  it('da accettare: mostra l informativa al posto della pagina e salva solo con la casella', async () => {
    const utente = userEvent.setup()
    stato.mockReturnValue({ isPending: false, data: { daAccettare: true, versione: 'v' } } as ReturnType<typeof useStatoPrivacy>)
    monta()

    expect(screen.queryByText('Pagina riservata')).not.toBeInTheDocument()
    const continua = screen.getByRole('button', { name: 'Continua' })
    expect(continua).toBeDisabled()

    await utente.click(screen.getByRole('checkbox'))
    await utente.click(continua)

    expect(accetta).toHaveBeenCalledOnce()
  })

  it('gia accettata: mostra la pagina', () => {
    stato.mockReturnValue({ isPending: false, data: { daAccettare: false, versione: 'v' } } as ReturnType<typeof useStatoPrivacy>)
    monta()

    expect(screen.getByText('Pagina riservata')).toBeInTheDocument()
  })

  it('verifica non riuscita: mostra comunque la pagina', () => {
    stato.mockReturnValue({ isPending: false, data: undefined, isError: true } as ReturnType<typeof useStatoPrivacy>)
    monta()

    expect(screen.getByText('Pagina riservata')).toBeInTheDocument()
  })
})
