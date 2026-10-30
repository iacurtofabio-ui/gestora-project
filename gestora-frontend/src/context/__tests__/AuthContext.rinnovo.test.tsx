import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { act, render, screen } from '@testing-library/react'
import { AxiosError, AxiosHeaders } from 'axios'
import { AuthProvider } from '@/context/AuthContext'
import { useAuth } from '@/hooks/useAuth'
import apiClient from '@/lib/axios'
import { segnaSessione, sessioneAttiva } from '@/lib/session'
import type { SessioneDTO } from '@/types/sessione'

/**
 * V2-010 — AuthProvider rinnova il token poco prima della scadenza, finche' la pagina e' aperta.
 * Prima lo Staff veniva buttato fuori ogni 60 minuti, anche a meta' di un form.
 *
 * V2-028 — il token sta in un cookie HttpOnly: all'avvio l'utente arriva da «chi sono», e dal
 * server arrivano anche utente e scadenza dopo ogni rinnovo.
 *
 * L'orologio e i timer sono finti; le chiamate al backend sono simulate. Il nome del cookie CSRF
 * e' uno senza prefisso: jsdom non accetta cookie `__Host-` su una pagina http.
 */
vi.mock('@/lib/axios', () => ({
  default: { get: vi.fn(), post: vi.fn() },
  COOKIE_CSRF: 'xsrf-prova',
}))

const get = vi.mocked(apiClient.get)
const post = vi.mocked(apiClient.post)

const ADESSO = Date.UTC(2026, 9, 9, 18, 0, 0)
const MINUTO = 60 * 1000

function sessioneCheScadeA(istante: number): SessioneDTO {
  return { id: 'utente-1', email: 'staff@gestora.it', ruoli: ['Staff'], scadenza: new Date(istante).toISOString() }
}

function MostraSessione() {
  const { user, logout } = useAuth()
  return (
    <>
      <span data-testid="scadenza">{user?.scadenza ?? 'nessuno'}</span>
      <button onClick={logout}>Esci</button>
    </>
  )
}

function monta() {
  render(
    <AuthProvider>
      <MostraSessione />
    </AuthProvider>
  )
}

/** Avvio con una sessione aperta: c'e' il cookie CSRF e «chi sono» risponde. */
async function avvia(sessione: SessioneDTO) {
  document.cookie = 'xsrf-prova=valore'
  get.mockResolvedValue({ data: sessione })
  monta()
  await act(() => vi.advanceTimersByTimeAsync(0))
}

function errore401() {
  const risposta = {
    status: 401, statusText: 'Unauthorized', data: '', headers: {},
    config: { headers: new AxiosHeaders() },
  }
  return new AxiosError('401', 'ERR_BAD_REQUEST', undefined, undefined, risposta)
}

describe('sessione all’avvio', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(ADESSO)
    get.mockReset()
    post.mockReset()
    post.mockResolvedValue({ data: '' })
    segnaSessione(false)
    document.cookie = 'xsrf-prova=; expires=Thu, 01 Jan 1970 00:00:00 GMT'
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('senza cookie non chiede «chi sono»: la vetrina non fa chiamate in piu', () => {
    monta()

    expect(get).not.toHaveBeenCalled()
    expect(screen.getByTestId('scadenza')).toHaveTextContent('nessuno')
  })

  it('con il cookie aspetta il server, poi mostra l’utente', async () => {
    const sessione = sessioneCheScadeA(ADESSO + 60 * MINUTO)
    document.cookie = 'xsrf-prova=valore'
    get.mockResolvedValue({ data: sessione })
    monta()

    expect(screen.getByText('Caricamento…')).toBeInTheDocument()
    await act(() => vi.advanceTimersByTimeAsync(0))

    expect(get).toHaveBeenCalledWith('/AuthenticationUser/sessione')
    expect(screen.getByTestId('scadenza')).toHaveTextContent(sessione.scadenza)
    expect(sessioneAttiva()).toBe(true)
  })

  it('cookie scaduto (401): si parte anonimi, senza errori', async () => {
    document.cookie = 'xsrf-prova=valore'
    get.mockRejectedValue(errore401())
    monta()
    await act(() => vi.advanceTimersByTimeAsync(0))

    expect(screen.getByTestId('scadenza')).toHaveTextContent('nessuno')
    expect(sessioneAttiva()).toBe(false)
  })

  it('toglie il vecchio token rimasto in localStorage dalla v3.0.0', () => {
    localStorage.setItem('token', 'vecchio')
    monta()

    expect(localStorage.getItem('token')).toBeNull()
  })

  it('l’uscita chiede al server di togliere il cookie', async () => {
    await avvia(sessioneCheScadeA(ADESSO + 60 * MINUTO))

    act(() => screen.getByRole('button', { name: 'Esci' }).click())

    expect(post).toHaveBeenCalledWith('/AuthenticationUser/logout')
    expect(screen.getByTestId('scadenza')).toHaveTextContent('nessuno')
    expect(sessioneAttiva()).toBe(false)
  })
})

describe('rinnovo del token', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(ADESSO)
    get.mockReset()
    post.mockReset()
    post.mockResolvedValue({ data: '' })
    segnaSessione(false)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('10 minuti prima della scadenza chiede un token nuovo e usa la scadenza nuova', async () => {
    const nuova = sessioneCheScadeA(ADESSO + 110 * MINUTO)
    await avvia(sessioneCheScadeA(ADESSO + 60 * MINUTO))
    post.mockResolvedValue({ data: nuova })

    await act(() => vi.advanceTimersByTimeAsync(50 * MINUTO - 1))
    expect(post).not.toHaveBeenCalled()

    await act(() => vi.advanceTimersByTimeAsync(1))
    expect(post).toHaveBeenCalledWith('/AuthenticationUser/rinnova-token')
    expect(screen.getByTestId('scadenza')).toHaveTextContent(nuova.scadenza)
  })

  it('al limite delle 12 ore la scadenza non si allunga: non richiede piu', async () => {
    const sessione = sessioneCheScadeA(ADESSO + 15 * MINUTO)
    await avvia(sessione)
    post.mockResolvedValue({ data: sessioneCheScadeA(ADESSO + 15 * MINUTO) })

    await act(() => vi.advanceTimersByTimeAsync(5 * MINUTO))
    await act(() => vi.advanceTimersByTimeAsync(9 * MINUTO))

    expect(post).toHaveBeenCalledTimes(1)
    expect(screen.getByTestId('scadenza')).toHaveTextContent(sessione.scadenza)
  })

  it('se il server non risponde riprova dopo un minuto', async () => {
    const nuova = sessioneCheScadeA(ADESSO + 80 * MINUTO)
    await avvia(sessioneCheScadeA(ADESSO + 20 * MINUTO))
    post.mockRejectedValueOnce(new AxiosError('Network Error')).mockResolvedValue({ data: nuova })

    await act(() => vi.advanceTimersByTimeAsync(10 * MINUTO))
    expect(post).toHaveBeenCalledTimes(1)

    await act(() => vi.advanceTimersByTimeAsync(MINUTO))
    expect(post).toHaveBeenCalledTimes(2)
    expect(screen.getByTestId('scadenza')).toHaveTextContent(nuova.scadenza)
  })

  it('su 401 non riprova: la sessione e finita, ci pensa l intercettore', async () => {
    await avvia(sessioneCheScadeA(ADESSO + 20 * MINUTO))
    post.mockRejectedValue(errore401())

    await act(() => vi.advanceTimersByTimeAsync(15 * MINUTO))

    expect(post).toHaveBeenCalledTimes(1)
  })

  it('dopo il logout non rinnova piu', async () => {
    await avvia(sessioneCheScadeA(ADESSO + 60 * MINUTO))

    act(() => screen.getByRole('button', { name: 'Esci' }).click())
    await act(() => vi.advanceTimersByTimeAsync(60 * MINUTO))

    expect(post).toHaveBeenCalledTimes(1)
    expect(post).toHaveBeenCalledWith('/AuthenticationUser/logout')
  })
})
