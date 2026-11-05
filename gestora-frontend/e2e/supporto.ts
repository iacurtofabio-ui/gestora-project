import { readdirSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import AxeBuilder from '@axe-core/playwright'
import { expect, test as base, type APIRequestContext, type Page } from '@playwright/test'

export { expect }

const QUI = path.dirname(fileURLToPath(import.meta.url))
const POSTA = path.resolve(QUI, '../node_modules/.e2e/posta')

export const ADMIN = { email: 'admin@gestora.local', password: 'Sviluppo1!' }
export const STAFF = { email: 'staff.e2e@gestora.local', password: 'Sviluppo1!' }
export const PASSWORD = 'ProvaGestora1!'

export type Utente = { email: string; password: string }

/**
 * Ogni test ha il suo indirizzo: da una macchina sola il limite di 5 accessi al minuto si
 * raggiungerebbe subito. In sviluppo il backend legge l'indirizzo da questo header.
 */
export const test = base.extend({
  // eslint-disable-next-line no-empty-pattern
  extraHTTPHeaders: async ({}, fornisci) => {
    const parte = () => Math.floor(Math.random() * 254) + 1
    await fornisci({ 'X-Forwarded-For': `10.${parte()}.${parte()}.${parte()}` })
  },
})

export function emailNuova(prefisso: string) {
  return `${prefisso}-${Date.now()}-${Math.floor(Math.random() * 1000)}@e2e.test`
}

/** La data fra N giorni in Italia, nel formato dei campi data. */
export function dataFra(giorni: number) {
  const giorno = new Date(Date.now() + giorni * 86_400_000)
  return new Intl.DateTimeFormat('sv-SE', { timeZone: 'Europe/Rome' }).format(giorno)
}

/** Il testo dell'ultima email arrivata a questo indirizzo, aspettando che compaia. */
export async function leggiEmail(destinatario: string) {
  let testo = ''

  await expect
    .poll(() => {
      const email = readdirSync(POSTA)
        .map((file) => readFileSync(path.join(POSTA, file), 'utf8'))
        .filter((contenuto) => contenuto.startsWith(`A: ${destinatario}\n`))
      testo = email.at(-1) ?? ''
      return testo.length
    })
    .toBeGreaterThan(0)

  return testo
}

export function linkDa(testoEmail: string) {
  const link = testoEmail.match(/https?:\/\/\S+/)?.[0]
  if (!link) throw new Error('Nessun link nell’email')
  return link
}

/** Un cliente nuovo con l'email confermata, creato dall'API: serve a chi non prova la registrazione. */
export async function creaCliente(request: APIRequestContext): Promise<Utente> {
  const email = emailNuova('cliente')

  const registrazione = await request.post('/api/AuthenticationUser/register', {
    data: { username: email, email, password: PASSWORD, accettaPrivacy: true },
  })
  expect(registrazione.ok()).toBeTruthy()

  const link = new URL(linkDa(await leggiEmail(email)))
  const conferma = await request.post('/api/AuthenticationUser/conferma-email', {
    data: { userId: link.searchParams.get('userId'), token: link.searchParams.get('token') },
  })
  expect(conferma.ok()).toBeTruthy()

  return { email, password: PASSWORD }
}

export async function entra(page: Page, utente: Utente) {
  await page.goto('/login')
  await page.getByLabel('Email').fill(utente.email)
  await page.getByLabel('Password', { exact: true }).fill(utente.password)
  await page.getByRole('button', { name: 'Accedi' }).click()
  await expect(page.getByRole('button', { name: 'Menu utente' })).toBeVisible()
}

export async function esci(page: Page) {
  await page.getByRole('button', { name: 'Menu utente' }).click()
  await page.getByRole('menuitem', { name: 'Esci' }).click()
}

/** Compila il form di una prenotazione nel turno che copre tutta la giornata. */
export async function compilaPrenotazione(page: Page, dati: { data: string; coperti: number; nome?: string }) {
  const form = page.getByRole('dialog')
  await form.getByLabel('Data').fill(dati.data)
  await form.getByLabel('Fascia oraria').selectOption({ label: '00:05–23:59' })
  if (dati.nome) await form.getByLabel('Nome cliente (facoltativo)').fill(dati.nome)
  await form.getByLabel('Numero di coperti').fill(String(dati.coperti))
}

export async function prenota(page: Page, dati: { data: string; coperti: number; nome?: string }) {
  await page.getByRole('button', { name: 'Aggiungi prenotazione' }).first().click()
  await compilaPrenotazione(page, dati)
  await page.getByRole('button', { name: 'Crea prenotazione' }).click()
  await expect(page.getByRole('dialog')).toBeHidden()
}

/** Nessuna violazione seria di accessibilità sulla pagina aperta. */
export async function controllaAccessibilita(page: Page) {
  // Il mouse lontano dalle righe: axe deve misurare i colori veri, non quelli di un passaggio.
  await page.mouse.move(0, 0)

  const esito = await new AxeBuilder({ page }).analyze()
  const gravi = esito.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical')

  expect(gravi.map((v) => `${v.id}: ${v.nodes.map((n) => `${n.target.join(' ')} ${n.failureSummary}`).join(', ')}`)).toEqual([])
}
