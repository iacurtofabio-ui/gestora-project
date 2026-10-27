/**
 * REV-016 — le date "di calendario" del dominio sono sempre in ora italiana.
 *
 * Il difetto: `new Date().toISOString().split('T')[0]` produce la data in UTC, non quella del
 * locale. Fra mezzanotte e le due (ora legale) l'Italia e' gia' al giorno dopo mentre UTC e'
 * ancora al giorno prima, quindi la Dashboard chiedeva al backend i dati del giorno sbagliato —
 * proprio il problema che lato server era gia' stato risolto con IClock.TodayInRome.
 *
 * Qui il fuso e' fissato a Europe/Rome e non preso dal browser: il locale del ristorante e' un
 * dato dell'applicazione, non del dispositivo di chi guarda. Un Admin che consulta la dashboard
 * dall'estero deve vedere la giornata del ristorante, non la propria.
 */
const FUSO_ITALIA = 'Europe/Rome'

// 'en-CA' formatta le date come YYYY-MM-DD, cioe' gia' nel formato che l'API si aspetta.
const formattatoreIso = new Intl.DateTimeFormat('en-CA', {
  timeZone: FUSO_ITALIA,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
})

/** Data odierna in Italia, come stringa YYYY-MM-DD. */
export function oggiInItalia(): string {
  return formattatoreIso.format(new Date())
}

/**
 * Lunedi' della settimana corrente (in Italia), come stringa YYYY-MM-DD.
 *
 * I conti si fanno in UTC su una data "pura" (mezzanotte Z): la parte di fuso e' gia' stata
 * risolta da oggiInItalia, e lavorare in UTC evita che l'aritmetica sui giorni scivoli quando
 * cambia l'ora legale.
 */
export function lunediSettimanaCorrenteInItalia(): string {
  return lunediSettimanaDi(oggiInItalia())
}

/**
 * FASE 7 — come sopra, ma per una data qualsiasi, non solo "oggi". Serve alla dashboard
 * navigabile: la settimana mostrata segue il giorno scelto, non resta fissa sulla corrente.
 */
export function lunediSettimanaDi(iso: string): string {
  const data = new Date(`${iso}T00:00:00Z`)
  const giorno = data.getUTCDay() // 0 = domenica
  data.setUTCDate(data.getUTCDate() - (giorno === 0 ? 6 : giorno - 1))
  return data.toISOString().split('T')[0]
}

/** Una data YYYY-MM-DD spostata di N giorni (N negativo per andare indietro). */
export function aggiungiGiorni(iso: string, n: number): string {
  const data = new Date(`${iso}T00:00:00Z`)
  data.setUTCDate(data.getUTCDate() + n)
  return data.toISOString().split('T')[0]
}

/**
 * Una data YYYY-MM-DD scritta per esteso in italiano: "martedi 9 settembre".
 *
 * Serve solo a presentare: il calcolo del giorno resta quello sopra. Il fuso e' fissato come
 * nel resto del file, altrimenti una data "pura" letta a mezzanotte Z scivolerebbe al giorno
 * prima per chi guarda da un fuso indietro rispetto a Roma.
 */
const formattatoreEsteso = new Intl.DateTimeFormat('it-IT', {
  timeZone: FUSO_ITALIA,
  weekday: 'long',
  day: 'numeric',
  month: 'long',
})

export function dataEstesaInItalia(iso: string): string {
  return formattatoreEsteso.format(new Date(`${iso}T12:00:00Z`))
}

/**
 * Una data YYYY-MM-DD in forma breve: "mer 9 set".
 *
 * In tabella la data ISO grezza (2026-09-09) e' precisa e illeggibile: costringe a contare le
 * cifre per capire che giorno e'. La forma breve si legge di colpo e occupa meno spazio.
 */
const formattatoreBreve = new Intl.DateTimeFormat('it-IT', {
  timeZone: FUSO_ITALIA,
  weekday: 'short',
  day: 'numeric',
  month: 'short',
})

export function dataBreveInItalia(iso: string): string {
  return formattatoreBreve.format(new Date(`${iso}T12:00:00Z`)).replace(/\.$/, '')
}

/** V2-025 — primo e ultimo giorno del mese di una data YYYY-MM-DD, come YYYY-MM-DD. */
export function meseDi(iso: string): { primo: string; ultimo: string } {
  const [anno, mese] = iso.split('-').map(Number)
  const ultimoGiorno = new Date(Date.UTC(anno, mese, 0)).getUTCDate()
  const mm = String(mese).padStart(2, '0')
  return { primo: `${anno}-${mm}-01`, ultimo: `${anno}-${mm}-${String(ultimoGiorno).padStart(2, '0')}` }
}

/** V2-025 — quanti giorni da `da` ad `a`, estremi compresi (date YYYY-MM-DD). */
export function giorniCompresi(da: string, a: string): number {
  return Math.round((Date.parse(`${a}T00:00:00Z`) - Date.parse(`${da}T00:00:00Z`)) / 86_400_000) + 1
}

/**
 * V2-019 — un istante del server (data e ora UTC, es. "2026-10-26T10:34:57Z") scritto in ora
 * italiana: "26 ottobre 2026, 11:34". Per date di eventi (registrazione, ultimo accesso), non per
 * le date di calendario delle prenotazioni.
 */
const formattatoreIstante = new Intl.DateTimeFormat('it-IT', {
  timeZone: FUSO_ITALIA,
  day: 'numeric',
  month: 'long',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

export function istanteInItalia(isoUtc: string): string {
  return formattatoreIstante.format(new Date(isoUtc))
}

/** Come CheckDisponibilitaDTOValidator.GiorniMassimiInAvanti nel backend. */
export const GIORNI_MASSIMI_IN_AVANTI = 365

/** Ultima data prenotabile (oggi in Italia + 365 giorni), come stringa YYYY-MM-DD. */
export function ultimaDataPrenotabile(): string {
  return aggiungiGiorni(oggiInItalia(), GIORNI_MASSIMI_IN_AVANTI)
}

const formattatoreOra = new Intl.DateTimeFormat('en-GB', {
  timeZone: FUSO_ITALIA,
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
})

/** Ora attuale in Italia, come stringa HH:mm (confrontabile con gli orari delle fasce). */
export function oraInItalia(): string {
  return formattatoreOra.format(new Date())
}

/**
 * La fascia di una prenotazione e' gia' finita? Stessa regola del backend (data + fine fascia
 * contro l'ora di Roma): oltre quel momento non si conferma e non si annulla piu'.
 */
export function fasciaGiaFinita(dataPrenotazione: string, oraFine: string | null): boolean {
  const oggi = oggiInItalia()
  if (dataPrenotazione < oggi) return true
  if (dataPrenotazione > oggi || !oraFine) return false
  return oraInItalia() >= oraFine.slice(0, 5)
}
