/// <reference types="node" />
import { describe, it, expect, beforeEach, afterEach } from 'vitest'
import middleware, { config } from '../../../middleware'

/**
 * V2-028 — il middleware di Vercel gira solo sui server di Vercel, non con `npm run dev`. Qui si
 * controlla cosa chiede a Vercel di fare: girare la richiesta ad Azure con la parola segreta,
 * senza il nome del sito Vercel (Azure lo rifiuterebbe).
 */
const API_AZURE = 'https://gestora-api.esempio.net'
const SEGRETO = 'parola-segreta-di-prova'

function chiama(url: string, intestazioni: Record<string, string> = {}): Response {
  return middleware(new Request(url, { headers: intestazioni }))
}

describe('middleware di Vercel', () => {
  beforeEach(() => {
    process.env.API_AZURE = API_AZURE
    process.env.INOLTRO_SEGRETO = SEGRETO
  })

  afterEach(() => {
    delete process.env.API_AZURE
    delete process.env.INOLTRO_SEGRETO
  })

  it('vale solo per /api', () => {
    expect(config.matcher).toBe('/api/:path*')
  })

  it('gira ad Azure lo stesso percorso, con la ricerca', () => {
    const risposta = chiama('https://gestora.vercel.app/api/Prenotazione/report-csv?da=2026-10-01&a=2026-10-31')

    expect(risposta.headers.get('x-middleware-rewrite')).toBe(
      `${API_AZURE}/api/Prenotazione/report-csv?da=2026-10-01&a=2026-10-31`
    )
  })

  it('aggiunge la parola segreta e tiene le intestazioni del browser', () => {
    const risposta = chiama('https://gestora.vercel.app/api/AuthenticationUser/login', {
      cookie: '__Host-gestora-sessione=abc',
      'x-xsrf-token': 'valore',
    })

    expect(risposta.headers.get('x-middleware-request-x-gestora-inoltro')).toBe(SEGRETO)
    expect(risposta.headers.get('x-middleware-request-cookie')).toBe('__Host-gestora-sessione=abc')
    expect(risposta.headers.get('x-middleware-request-x-xsrf-token')).toBe('valore')
    const elenco = risposta.headers.get('x-middleware-override-headers')!.split(',')
    expect(elenco).toEqual(expect.arrayContaining(['cookie', 'x-xsrf-token', 'x-gestora-inoltro']))
  })

  it('una parola inventata dal browser viene sovrascritta', () => {
    const risposta = chiama('https://gestora.vercel.app/api/Setup/stato', { 'x-gestora-inoltro': 'inventata' })

    expect(risposta.headers.get('x-middleware-request-x-gestora-inoltro')).toBe(SEGRETO)
  })

  it('non passa il nome del sito Vercel ad Azure', () => {
    const risposta = chiama('https://gestora.vercel.app/api/Setup/stato', { host: 'gestora.vercel.app' })

    expect(risposta.headers.get('x-middleware-request-host')).toBeNull()
    expect(risposta.headers.get('x-middleware-override-headers')!.split(',')).not.toContain('host')
  })

  it('senza variabili risponde 500 con un messaggio, invece di girare a vuoto', async () => {
    delete process.env.INOLTRO_SEGRETO
    const risposta = chiama('https://gestora.vercel.app/api/Setup/stato')

    expect(risposta.status).toBe(500)
    expect(risposta.headers.get('x-middleware-rewrite')).toBeNull()
    expect((await risposta.json()).message).toContain('Configurazione mancante')
  })
})
