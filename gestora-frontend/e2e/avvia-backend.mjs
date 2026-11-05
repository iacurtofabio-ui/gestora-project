// Prepara il database delle prove e accende il backend. Lo lancia Playwright a ogni giro.
//
// Il database è gestora_e2e sul PostgreSQL di questa macchina: i dati si cancellano a ogni giro,
// quindi i due controlli sulla stringa di connessione non sono formalità.
import { execFileSync, spawn } from 'node:child_process'
import { mkdirSync, rmSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const QUI = path.dirname(fileURLToPath(import.meta.url))
const API = path.resolve(QUI, '../../GestoraWebApi')
const POSTA = path.resolve(QUI, '../node_modules/.e2e/posta')

function stringaDiConnessione() {
  let sorgente = process.env.GESTORA_TEST_DB
  if (!sorgente) {
    const segreti = execFileSync('dotnet', ['user-secrets', 'list', '--project', API], { encoding: 'utf8' })
    sorgente = segreti.match(/^ConnectionStrings:DefaultConnection = (.+)$/m)?.[1]?.trim()
  }
  if (!sorgente) throw new Error('Stringa di connessione mancante: GESTORA_TEST_DB o user secrets dell’API.')

  const host = sorgente.match(/(?:^|;)\s*Host\s*=\s*([^;]+)/i)?.[1]?.trim().toLowerCase()
  if (!['localhost', '127.0.0.1', '::1'].includes(host)) {
    throw new Error('Le prove cancellano i dati: partono solo su un PostgreSQL di questa macchina.')
  }

  const senzaDatabase = sorgente.replace(/(?:^|;)\s*Database\s*=\s*[^;]*/i, '')
  return `${senzaDatabase.replace(/;+$/, '')};Database=gestora_e2e`
}

const stringa = stringaDiConnessione()

const ambiente = {
  ...process.env,
  ASPNETCORE_ENVIRONMENT: 'Development',
  ASPNETCORE_URLS: 'http://localhost:5199',
  ConnectionStrings__DefaultConnection: stringa,
  // Uno spazio e non la stringa vuota: su Windows una variabile vuota non sovrascriverebbe gli
  // user secrets, e le email delle prove partirebbero davvero.
  Email__Host: ' ',
  Email__CartellaSviluppo: POSTA,
  Email__UrlFrontend: 'http://localhost:5174',
  Serilog__MinimumLevel__Default: 'Warning',
}

rmSync(POSTA, { recursive: true, force: true })
mkdirSync(POSTA, { recursive: true })

const esegui = (argomenti) => execFileSync('dotnet', argomenti, { cwd: API, env: ambiente, stdio: 'inherit' })

esegui(['build', '-v', 'q', '--nologo'])
esegui(['ef', 'database', 'update', '--no-build', '--connection', stringa])
esegui(['run', '--no-build', '--no-launch-profile', '--', '--seed-e2e'])

const backend = spawn('dotnet', ['run', '--no-build', '--no-launch-profile'], { cwd: API, env: ambiente, stdio: 'inherit' })
backend.on('exit', (codice) => process.exit(codice ?? 0))
