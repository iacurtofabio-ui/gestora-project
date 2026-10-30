import { describe, it, expect, beforeEach, afterEach } from 'vitest'
import { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import apiClient, { COOKIE_CSRF } from '@/lib/axios'
import { Endpoints } from '@/lib/endpoints'
import { onSessionExpired, segnaSessione, sessioneAttiva } from '@/lib/session'

/**
 * V2-028 — il token sta in un cookie HttpOnly: Axios non lo allega più, rimanda invece il cookie
 * CSRF, e capisce la sessione scaduta da un 401 su una chiamata qualsiasi, non su login & co.
 */
async function richiesta(url: string, stato = 200): Promise<InternalAxiosRequestConfig> {
  let inviata: InternalAxiosRequestConfig | undefined
  await apiClient
    .post(url, {}, {
      adapter: async (config: InternalAxiosRequestConfig) => {
        inviata = config
        const risposta = { data: '', status: stato, statusText: '', headers: {}, config }
        if (stato >= 400) throw new AxiosError(String(stato), 'ERR_BAD_REQUEST', config, null, risposta)
        return risposta
      },
    })
    .catch(() => {})
  return inviata!
}

describe('richieste verso /api', () => {
  it('vanno sullo stesso sito, sotto /api', () => {
    expect(apiClient.defaults.baseURL).toBe('/api')
  })

  it('rimandano il cookie CSRF nell’header che il backend controlla', () => {
    expect(apiClient.defaults.xsrfCookieName).toBe(COOKIE_CSRF)
    expect(COOKIE_CSRF).toBe('__Host-gestora-xsrf')
    expect(apiClient.defaults.xsrfHeaderName).toBe('X-XSRF-TOKEN')
  })

  it('non allegano più un token, nemmeno se ne resta uno vecchio in localStorage', async () => {
    localStorage.setItem('token', 'vecchio')
    const config = await richiesta(Endpoints.auth.register)
    expect(config.headers.Authorization).toBeUndefined()
    localStorage.clear()
  })
})

describe('sessione scaduta (401)', () => {
  let segnalazioni = 0
  let smetti: () => void

  beforeEach(() => {
    segnalazioni = 0
    smetti = onSessionExpired(() => {
      segnalazioni++
    })
    segnaSessione(true)
  })

  afterEach(() => {
    smetti()
    segnaSessione(false)
  })

  it('su una chiamata qualsiasi chiude la sessione e avvisa una volta sola', async () => {
    await richiesta(Endpoints.account.cambiaPassword, 401)
    await richiesta(Endpoints.account.cambiaPassword, 401)

    expect(segnalazioni).toBe(1)
    expect(sessioneAttiva()).toBe(false)
  })

  it.each([Endpoints.auth.login, Endpoints.auth.sessione, Endpoints.auth.confermaEmail])(
    '%s: un 401 non vuol dire sessione scaduta',
    async (url) => {
      await richiesta(url, 401)

      expect(segnalazioni).toBe(0)
      expect(sessioneAttiva()).toBe(true)
    }
  )

  it('senza sessione aperta un 401 non avvisa', async () => {
    segnaSessione(false)
    await richiesta(Endpoints.account.cambiaPassword, 401)

    expect(segnalazioni).toBe(0)
  })

  it('un errore diverso da 401 lascia la sessione aperta', async () => {
    await richiesta(Endpoints.account.cambiaPassword, 400)

    expect(segnalazioni).toBe(0)
    expect(sessioneAttiva()).toBe(true)
  })
})
