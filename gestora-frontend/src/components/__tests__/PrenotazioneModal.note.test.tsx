import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import PrenotazioneModal from '@/components/PrenotazioneModal'
import { useAuth } from '@/hooks/useAuth'

/**
 * V2-026 — Gestora non raccoglie dati sulla salute: il campo note non suggerisce più le
 * allergie e sotto dice di comunicarle al locale. L'avviso è collegato al campo, così lo legge
 * anche chi usa un lettore di schermo.
 *
 * Gli hook sono finti: qui conta solo cosa vede chi compila il form.
 */
vi.mock('@/hooks/useFasceOrarie', () => ({
  useFascePerGiorno: () => ({ data: [] }),
}))

vi.mock('@/hooks/useZone', () => ({
  useZoneAttive: () => ({ data: [] }),
}))

vi.mock('@/hooks/usePrenotazioni', () => ({
  useCreaPrenotazione: () => ({ mutate: vi.fn(), isPending: false }),
  useModificaPrenotazione: () => ({ mutate: vi.fn(), isPending: false }),
}))

vi.mock('@/hooks/useDisponibilita', () => ({
  useCheckDisponibilita: () => ({ data: undefined, isLoading: false }),
  useLimitiPrenotazione: () => ({
    data: { maxCopertiPerPrenotazione: 50, maxCopertiPrenotazioneOnline: 20 },
  }),
}))

vi.mock('@/hooks/useAuth', () => ({
  useAuth: vi.fn(),
}))

describe('PrenotazioneModal - note senza dati sulla salute', () => {
  it('il campo note avvisa di non scrivere allergie e non le suggerisce', () => {
    vi.mocked(useAuth).mockReturnValue({
      user: { id: '1', email: 'cliente@gestora.it', roles: ['Cliente'], token: 't' },
    } as unknown as ReturnType<typeof useAuth>)

    render(<PrenotazioneModal isOpen onClose={() => {}} />)
    const note = screen.getByLabelText('Note (facoltative)')

    expect(note).toHaveAccessibleDescription(/Non scrivere qui dati sulla salute/)
    expect(note.getAttribute('placeholder')).not.toMatch(/allergi/i)
  })
})
