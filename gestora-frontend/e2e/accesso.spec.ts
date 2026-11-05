import {
  ADMIN,
  PASSWORD,
  STAFF,
  controllaAccessibilita,
  creaCliente,
  emailNuova,
  entra,
  esci,
  expect,
  leggiEmail,
  linkDa,
  test,
} from './supporto'

test.describe('Registrazione e accesso', () => {
  test('dalla registrazione al primo accesso, passando dal link dell’email', async ({ page }) => {
    const email = emailNuova('nuovo')

    await page.goto('/register')
    await controllaAccessibilita(page)
    await page.getByLabel('Username').fill(email)
    await page.getByLabel('Email').fill(email)
    await page.getByLabel('Password', { exact: true }).fill(PASSWORD)
    await page.getByRole('checkbox').check()
    await page.getByRole('button', { name: /Registrati|Crea/ }).click()

    await page.goto(linkDa(await leggiEmail(email)))
    await expect(page.getByText(/confermata/i).first()).toBeVisible()

    await entra(page, { email, password: PASSWORD })
    await expect(page).toHaveURL(/prenotazioni/)
  })

  test('senza conferma non si entra', async ({ page, request }) => {
    const email = emailNuova('non-confermato')
    await request.post('/api/AuthenticationUser/register', {
      data: { username: email, email, password: PASSWORD, accettaPrivacy: true },
    })

    await page.goto('/login')
    await page.getByLabel('Email').fill(email)
    await page.getByLabel('Password', { exact: true }).fill(PASSWORD)
    await page.getByRole('button', { name: 'Accedi' }).click()

    await expect(page.getByText(/conferma/i).first()).toBeVisible()
    await expect(page.getByRole('button', { name: 'Menu utente' })).toBeHidden()
  })

  test('il menu mostra solo le voci del ruolo', async ({ page, request }) => {
    const menu = page.getByRole('navigation')

    await entra(page, ADMIN)
    await expect(menu.getByRole('link', { name: 'Utenti' })).toBeVisible()
    await esci(page)

    await entra(page, STAFF)
    await expect(menu.getByRole('link', { name: 'Dashboard' })).toBeVisible()
    await expect(menu.getByRole('link', { name: 'Utenti' })).toBeHidden()
    await esci(page)

    await entra(page, await creaCliente(request))
    await expect(menu.getByRole('link', { name: 'Dashboard' })).toBeHidden()
    await expect(menu.getByRole('link', { name: 'Zone' })).toBeHidden()
  })

  test('le pagine fuori ruolo non si aprono', async ({ page, request }) => {
    await page.goto('/prenotazioni')
    await expect(page).toHaveURL(/login/)

    await entra(page, await creaCliente(request))
    for (const pagina of ['/zone', '/dashboard', '/admin-utenti']) {
      await page.goto(pagina)
      await expect(page).toHaveURL(/unauthorized/)
    }
  })

  test('dopo l’uscita le pagine protette non si vedono', async ({ page, request }) => {
    await entra(page, await creaCliente(request))
    await esci(page)
    await expect(page.getByRole('button', { name: 'Menu utente' })).toBeHidden()

    await page.goto('/prenotazioni')
    await expect(page).toHaveURL(/login/)
  })

  test('il cambio password vale al nuovo accesso', async ({ page, request }) => {
    const cliente = await creaCliente(request)
    const nuova = 'NuovaGestora2!'

    await entra(page, cliente)
    await page.goto('/account')
    await controllaAccessibilita(page)
    await page.getByLabel('Password attuale').fill(cliente.password)
    await page.getByLabel('Nuova password', { exact: true }).fill(nuova)
    await page.getByLabel('Ripeti la nuova password').fill(nuova)
    await page.getByRole('button', { name: /Cambia password|Salva/ }).click()
    await expect(page.getByText(/aggiornata|cambiata/i).first()).toBeVisible()

    await esci(page)
    await entra(page, { email: cliente.email, password: nuova })
  })
})
