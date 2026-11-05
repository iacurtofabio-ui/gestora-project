import {
  STAFF,
  compilaPrenotazione,
  controllaAccessibilita,
  creaCliente,
  dataFra,
  entra,
  expect,
  prenota,
  test,
} from './supporto'

test.describe('Prenotazioni del Cliente', () => {
  test('prenota, modifica, annulla', async ({ page, request }) => {
    await entra(page, await creaCliente(request))
    await page.goto('/prenotazioni')

    await prenota(page, { data: dataFra(3), coperti: 2 })
    const riga = page.getByRole('row').filter({ hasText: '00:05–23:59' })
    await expect(riga).toContainText('Attiva')
    await expect(riga).toContainText('Tavolo')
    await controllaAccessibilita(page)

    await riga.getByRole('button', { name: /Altre azioni/ }).click()
    await page.getByRole('menuitem', { name: 'Modifica prenotazione' }).click()
    await page.getByLabel('Numero di coperti').fill('3')
    await page.getByRole('button', { name: 'Salva modifiche' }).click()
    await expect(page.getByRole('dialog')).toBeHidden()
    await expect(riga.getByRole('cell').nth(3)).toHaveText('3')

    await riga.getByRole('button', { name: /Altre azioni/ }).click()
    await page.getByRole('menuitem', { name: 'Annulla prenotazione' }).click()
    await page.getByRole('alertdialog').getByRole('button', { name: 'Annulla prenotazione' }).click()
    await expect(riga).toContainText('Annullata')
  })

  test('il form avvisa oltre 20 persone e sui dati di salute', async ({ page, request }) => {
    await entra(page, await creaCliente(request))
    await page.goto('/prenotazioni')
    await page.getByRole('button', { name: 'Aggiungi prenotazione' }).first().click()

    await expect(page.getByText('Non scrivere qui dati sulla salute')).toBeVisible()

    await compilaPrenotazione(page, { data: dataFra(3), coperti: 21 })
    await expect(page.getByText('contatta direttamente il ristorante')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Crea prenotazione' })).toBeDisabled()
  })
})

test.describe('Prenotazioni dello Staff', () => {
  test('prende una prenotazione al telefono e segna l’arrivo', async ({ page }, info) => {
    const nome = `Telefono ${info.project.name}`

    await entra(page, STAFF)
    await page.goto('/prenotazioni')
    await prenota(page, { data: dataFra(0), coperti: 4, nome })

    const riga = page.getByRole('row').filter({ hasText: nome })
    await riga.getByRole('button', { name: 'Conferma' }).click()
    await expect(riga).toContainText('Confermata')
  })

  test('completa una prenotazione a turno finito', async ({ page }, info) => {
    const nome = `Da completare ${info.project.name}`

    await entra(page, STAFF)
    await page.goto(`/prenotazioni?data=${dataFra(0)}`)

    const riga = page.getByRole('row').filter({ hasText: nome })
    await riga.getByRole('button', { name: 'Completa' }).click()
    await expect(riga).toContainText('Completata')
  })
})
