import { defineConfig, devices } from '@playwright/test'

const FRONTEND = 'http://localhost:5174'
const BACKEND = 'http://localhost:5199'

/**
 * Prove nel browser (V3-001-T03). Backend, frontend e database sono tutti loro: porte diverse e
 * database gestora_e2e, così si possono lanciare con l'ambiente di sviluppo acceso. Tutto quello
 * che producono sta sotto node_modules, già escluso da Git.
 *
 * Un browser alla volta e un test alla volta: i test condividono lo stesso database.
 */
export default defineConfig({
  testDir: 'e2e',
  outputDir: 'node_modules/.e2e/risultati',
  workers: 1,
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  reporter: 'list',
  timeout: 30_000,
  use: {
    baseURL: FRONTEND,
    locale: 'it-IT',
    timezoneId: 'Europe/Rome',
    trace: 'retain-on-failure',
    // Senza animazioni: axe misurerebbe il contrasto di una notifica mentre sta ancora comparendo.
    contextOptions: { reducedMotion: 'reduce' },
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'firefox', use: { ...devices['Desktop Firefox'] } },
  ],
  webServer: [
    {
      command: 'node e2e/avvia-backend.mjs',
      url: `${BACKEND}/health`,
      timeout: 240_000,
      reuseExistingServer: false,
      stdout: 'pipe',
    },
    {
      // La build vera e non il server di sviluppo: si prova quello che si pubblica, e il server di
      // sviluppo si è spento due volte a metà giro.
      command: 'npm run build && npx vite preview --port 5174 --strictPort',
      url: FRONTEND,
      timeout: 120_000,
      reuseExistingServer: false,
      env: { API_LOCALE: BACKEND },
    },
  ],
})
