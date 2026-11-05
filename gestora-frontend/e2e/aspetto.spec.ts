import { controllaAccessibilita, creaCliente, dataFra, entra, expect, prenota, test } from './supporto'

test.describe('Tema', () => {
  test('il tema scuro si applica e resta leggibile', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'dark' })
    await page.goto('/')
    await expect(page.locator('html')).toHaveClass(/dark/)
    await controllaAccessibilita(page)
  })

  test('il tema chiaro si applica', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'light' })
    await page.goto('/')
    await expect(page.locator('html')).not.toHaveClass(/dark/)
  })
})

test.describe('Telefono', () => {
  test.use({ viewport: { width: 375, height: 812 } })

  test('la pagina pubblica non scorre di lato', async ({ page }) => {
    await page.goto('/')
    await expect(page.getByRole('button', { name: 'Vedi i turni liberi' })).toBeVisible()

    const larghezza = await page.evaluate(() => document.documentElement.scrollWidth)
    expect(larghezza).toBeLessThanOrEqual(375)
  })

  test('il Cliente prenota dal telefono', async ({ page, request }) => {
    await entra(page, await creaCliente(request))
    await page.goto('/prenotazioni')

    await prenota(page, { data: dataFra(3), coperti: 2 })

    await expect(page.getByRole('row').filter({ hasText: '00:05–23:59' })).toContainText('Attiva')
  })
})
