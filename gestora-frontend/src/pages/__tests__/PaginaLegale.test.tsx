import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import PrivacyPage from '@/pages/PrivacyPage'
import TerminiPage from '@/pages/TerminiPage'
import { EMAIL_PRIVACY, INFORMATIVA_DATA, TITOLARE } from '@/lib/privacy'

/**
 * V2-019 — informativa e termini si aprono senza accesso e mostrano titolare, contatto e
 * versione presi da lib/privacy.ts, non scritti a mano nel testo.
 */
// L'interruttore del tema non è oggetto del test e vuole il ThemeProvider.
vi.mock('@/components/ThemeToggle', () => ({ ThemeToggle: () => null }))

function apri(percorso: '/privacy' | '/termini') {
  const router = createMemoryRouter(
    [
      { path: '/privacy', element: <PrivacyPage /> },
      { path: '/termini', element: <TerminiPage /> },
    ],
    { initialEntries: [percorso] }
  )
  render(<RouterProvider router={router} />)
}

describe('Pagine legali', () => {
  it('l informativa mostra titolare, contatto e versione', () => {
    apri('/privacy')

    expect(screen.getByRole('heading', { level: 1, name: 'Informativa privacy' })).toBeInTheDocument()
    expect(screen.getByText(new RegExp(`titolare del trattamento è ${TITOLARE}`))).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: EMAIL_PRIVACY })[0]).toHaveAttribute('href', `mailto:${EMAIL_PRIVACY}`)
    expect(screen.getByText(`Versione del ${INFORMATIVA_DATA}`)).toBeInTheDocument()
  })

  it('i termini rimandano all informativa', () => {
    apri('/termini')

    expect(screen.getByRole('heading', { level: 1, name: 'Termini di servizio' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'informativa privacy' })).toHaveAttribute('href', '/privacy')
  })
})
