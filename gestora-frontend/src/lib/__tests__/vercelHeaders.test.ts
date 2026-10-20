/// <reference types="node" />
import { describe, it, expect } from 'vitest'
import { readFileSync } from 'node:fs'
import { createHash } from 'node:crypto'
import { resolve } from 'node:path'

/**
 * V2-018: la Content-Security-Policy di vercel.json ammette lo script del tema in index.html per
 * la sua impronta (sha256), non con 'unsafe-inline'. Basta cambiare un carattere dello script,
 * anche uno spazio, perché l'impronta non corrisponda più: in produzione il browser lo
 * bloccherebbe senza errori visibili, e al caricamento tornerebbe il lampo bianco del tema scuro.
 * Questo test se ne accorge prima: se fallisce, ricalcolare l'impronta e aggiornare vercel.json.
 */
const radice = resolve(process.cwd())

function cspDiVercel(): string {
  const config = JSON.parse(readFileSync(resolve(radice, 'vercel.json'), 'utf-8'))
  const header = config.headers
    .flatMap((regola: { headers: { key: string; value: string }[] }) => regola.headers)
    .find((h: { key: string }) => h.key === 'Content-Security-Policy')
  return header.value
}

function improntaScriptInterni(): string[] {
  // Su Vercel il file arriva con fine riga LF, come nel repository; su Windows la copia locale
  // puo' averli CRLF. L'impronta va calcolata come la calcolerà il browser in produzione.
  const html = readFileSync(resolve(radice, 'index.html'), 'utf-8').replace(/\r\n/g, '\n')
  return [...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(
    (m) => `'sha256-${createHash('sha256').update(m[1]).digest('base64')}'`,
  )
}

describe('vercel.json: Content-Security-Policy', () => {
  it("ammette ogni script interno di index.html per la sua impronta", () => {
    const csp = cspDiVercel()
    const impronte = improntaScriptInterni()

    expect(impronte.length).toBeGreaterThan(0)
    for (const impronta of impronte) {
      expect(csp).toContain(impronta)
    }
  })

  it("non apre gli script a qualsiasi codice interno", () => {
    const scriptSrc = cspDiVercel()
      .split(';')
      .find((direttiva) => direttiva.trim().startsWith('script-src'))

    expect(scriptSrc).toBeDefined()
    expect(scriptSrc).not.toContain("'unsafe-inline'")
    expect(scriptSrc).not.toContain("'unsafe-eval'")
  })
})
