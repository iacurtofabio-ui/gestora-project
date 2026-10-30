/**
 * V2-028 — su Vercel ogni chiamata a /api passa da qui e prosegue verso il backend su Azure.
 *
 * Perché: il token ora sta in un cookie HttpOnly. Se il browser chiamasse Azure direttamente
 * (un altro sito), Safari scarterebbe il cookie. Così per il browser /api è lo stesso sito delle
 * pagine.
 *
 * In più si aggiunge la parola segreta (`X-Gestora-Inoltro`): il backend la controlla e solo allora
 * si fida dell'indirizzo del visitatore che Vercel scrive in `X-Real-IP` (InoltroVercelMiddleware).
 * La parola sta solo nelle variabili di Vercel, mai nel codice né nel bundle del browser: questo
 * file gira sui server di Vercel, non nella pagina.
 *
 * Variabili su Vercel (Settings → Environment Variables):
 * - `API_AZURE`: l'indirizzo del backend, senza /api in fondo (es. https://gestora-api-....net)
 * - `INOLTRO_SEGRETO`: la stessa parola di `InoltroVercel__Segreto` su Azure
 *
 * In locale non si usa: ci pensa il proxy di Vite (vite.config.ts).
 *
 * Il «rewrite» è scritto a mano: è quello del pacchetto @vercel/functions (stesse intestazioni
 * `x-middleware-*`), che porta con sé dipendenze con avvisi di sicurezza. Sono poche righe.
 */
export const config = { matcher: '/api/:path*' }

export default function middleware(request: Request): Response {
  const apiAzure = process.env.API_AZURE
  const segreto = process.env.INOLTRO_SEGRETO

  if (!apiAzure || !segreto) {
    // Stessa forma degli errori del backend (ErrorDetails): il frontend sa mostrarla.
    return Response.json(
      { statusCode: 500, message: 'Configurazione mancante sul sito: il servizio non è raggiungibile.' },
      { status: 500 },
    )
  }

  const url = new URL(request.url)
  const destinazione = new URL(url.pathname + url.search, apiAzure)

  // Vercel sostituisce le intestazioni della richiesta con l'elenco passato qui, quindi si parte da
  // tutte quelle ricevute. Tranne `host`: è il nome del sito Vercel, e Azure lo usa per scegliere
  // l'app (e AllowedHosts lo rifiuterebbe). Senza, vale quello di `destinazione`.
  const intestazioni = new Headers(request.headers)
  intestazioni.delete('host')
  intestazioni.set('x-gestora-inoltro', segreto)

  return inoltra(destinazione, intestazioni)
}

/** Chiede a Vercel di girare la richiesta a `destinazione`, con le intestazioni indicate. */
function inoltra(destinazione: URL, intestazioni: Headers): Response {
  const risposta = new Headers()
  risposta.set('x-middleware-rewrite', destinazione.toString())

  const nomi: string[] = []
  for (const [nome, valore] of intestazioni) {
    risposta.set(`x-middleware-request-${nome}`, valore)
    nomi.push(nome)
  }
  risposta.set('x-middleware-override-headers', nomi.join(','))

  return new Response(null, { headers: risposta })
}
