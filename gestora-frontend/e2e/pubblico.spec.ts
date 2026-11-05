import { controllaAccessibilita, dataFra, expect, test } from './supporto'

test.describe('Pagina pubblica', () => {
  test('la verifica mostra i turni liberi di un giorno', async ({ page }) => {
    await page.goto('/')
    await page.getByLabel('Persone').fill('2')
    await page.locator('#disp-data').fill(dataFra(3))
    await page.getByRole('button', { name: 'Vedi i turni liberi' }).click()

    await expect(page.getByText('00:05–23:59')).toBeVisible()
    await expect(page.getByRole('link', { name: 'Prenota ora' })).toBeVisible()
    await controllaAccessibilita(page)
  })

  test('un turno pieno non si offre', async ({ page }) => {
    await page.goto('/')
    await page.getByLabel('Persone').fill('2')
    await page.locator('#disp-data').fill(dataFra(10))
    await page.getByRole('button', { name: 'Vedi i turni liberi' }).click()

    const turno = page.getByRole('listitem').filter({ hasText: '00:05–23:59' })
    await expect(turno).toBeVisible()
    await expect(turno).not.toContainText('Libero')
  })

  test('oltre 20 persone invita a contattare il ristorante', async ({ page }) => {
    await page.goto('/')
    await page.locator('#disp-data').fill(dataFra(3))
    await page.getByLabel('Persone').fill('21')

    await expect(page.getByText('contatta direttamente il ristorante')).toBeVisible()
  })

  test('le pagine legali e la 404 si aprono', async ({ page }) => {
    await page.goto('/privacy')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
    await controllaAccessibilita(page)

    await page.goto('/termini')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()

    await page.goto('/pagina-che-non-esiste')
    await expect(page.getByRole('link').first()).toBeVisible()
  })
})

test.describe('Pareri', () => {
  test('si invia un parere, e il terzo trova il limite', async ({ page }) => {
    const invia = async () => {
      await page.goto('/pareri')
      await page.getByLabel('Il tuo parere').fill('Ottima pizza, servizio gentile e veloce.')
      await page.getByRole('button', { name: /Invia/ }).click()
    }

    await page.goto('/pareri')
    await controllaAccessibilita(page)

    await invia()
    await expect(page.getByText('Grazie per il tuo parere')).toBeVisible()
    await invia()
    await expect(page.getByText('Grazie per il tuo parere')).toBeVisible()

    await invia()
    await expect(page.getByText('Hai già lasciato i tuoi pareri')).toBeVisible()
  })
})
