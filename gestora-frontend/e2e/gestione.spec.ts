import {
  ADMIN,
  PASSWORD,
  STAFF,
  controllaAccessibilita,
  dataFra,
  emailNuova,
  entra,
  esci,
  expect,
  prenota,
  test,
} from './supporto'

test.describe('Configurazione della sala', () => {
  test('l’Admin crea una zona e un tavolo, e il riepilogo si aggiorna', async ({ page }, info) => {
    const zona = `Veranda ${info.project.name}`

    await entra(page, ADMIN)
    await page.goto('/zone')
    await controllaAccessibilita(page)
    await page.getByRole('button', { name: /Aggiungi|Nuova/ }).first().click()
    await page.getByLabel('Nome').fill(zona)
    await page.getByRole('dialog').getByRole('button', { name: /Crea|Salva/ }).click()
    await expect(page.getByRole('cell', { name: zona, exact: true })).toBeVisible()

    await page.goto('/postazioni')
    const riepilogo = page.getByText(/tavoli/i).first()
    await expect(riepilogo).toBeVisible()
    await page.getByRole('combobox', { name: 'Scegli la zona' }).click()
    await page.getByRole('option', { name: zona }).click()

    await page.getByRole('button', { name: /Aggiungi/ }).first().click()
    const finestra = page.getByRole('dialog')
    const numero = info.project.name === 'chromium' ? '90' : '91'
    await finestra.getByLabel('Numero del tavolo').fill(numero)
    await finestra.getByLabel('Posti a sedere').fill('4')
    await finestra.getByLabel('Zona').selectOption({ label: zona })
    await finestra.getByRole('button', { name: 'Crea tavolo' }).click()
    await expect(finestra).toBeHidden()
    await expect(page.getByRole('cell', { name: numero, exact: true })).toBeVisible()
  })

  test('una fascia oltre i posti della sala non si salva', async ({ page }) => {
    await entra(page, ADMIN)
    await page.goto('/fasce-orarie')
    await page.getByRole('button', { name: /Aggiungi|Nuova/ }).first().click()

    await page.getByLabel('Capienza massima (coperti)').fill('5000')

    await expect(page.getByRole('dialog').getByRole('button', { name: /Crea|Salva/ })).toBeDisabled()
  })

  test('lo Staff vede la sala ma non la modifica', async ({ page }) => {
    await entra(page, STAFF)
    await page.goto('/zone')

    await expect(page.getByRole('cell', { name: 'Sala interna' })).toBeVisible()
    await expect(page.getByRole('button', { name: /Aggiungi|Nuova/ })).toHaveCount(0)
  })
})

test.describe('Utenti e ruoli', () => {
  test('l’Admin crea uno Staff che entra subito, poi gli dà e toglie un ruolo', async ({ page }) => {
    const email = emailNuova('staff')

    await entra(page, ADMIN)
    await page.goto('/admin-utenti')
    await controllaAccessibilita(page)
    await page.getByRole('button', { name: /Crea|Aggiungi/ }).first().click()
    await page.getByLabel('Nome').fill(email)
    await page.getByLabel('Email').fill(email)
    await page.getByLabel('Password').fill(PASSWORD)
    await page.getByLabel('Ruolo').selectOption('Staff')
    await page.getByRole('button', { name: 'Crea utente' }).click()
    await expect(page.getByRole('dialog')).toBeHidden()

    const riga = page.getByRole('row').filter({ hasText: email })
    await expect(riga).toContainText('Staff')

    await riga.getByRole('button', { name: /Altre azioni/ }).click()
    await page.getByRole('menuitem', { name: 'Gestisci i ruoli' }).click()
    const finestra = page.getByRole('dialog')
    await finestra.getByRole('button', { name: 'Assegna' }).first().click()
    await expect(finestra.getByRole('button', { name: 'Rimuovi' })).toHaveCount(2)
    await finestra.getByRole('button', { name: 'Rimuovi' }).first().click()
    await expect(finestra.getByRole('button', { name: 'Rimuovi' })).toHaveCount(1)
    await finestra.getByRole('button', { name: 'Ho finito' }).click()

    await esci(page)
    await entra(page, { email, password: PASSWORD })
  })
})

test.describe('Dashboard', () => {
  test('mostra in arrivo la prenotazione appena presa', async ({ page }, info) => {
    const nome = `Dashboard ${info.project.name}`

    await entra(page, STAFF)
    await page.goto('/prenotazioni')
    await prenota(page, { data: dataFra(1), coperti: 2, nome })

    await page.goto('/dashboard')
    await page.getByRole('button', { name: 'Giorno successivo' }).click()
    await expect(page.getByRole('heading', { name: 'In arrivo' })).toBeVisible()
    await expect(page.getByText(nome)).toBeVisible()

      })
})
