using Microsoft.AspNetCore.Http;

namespace GestoraWebApi.Common
{
    /// <summary>
    /// REV-029: ricava l'indirizzo di chi ha fatto la richiesta, tenendo conto del proxy che sta
    /// davanti all'applicazione in produzione.
    /// <para>
    /// <b>Perche' non si usa <c>UseForwardedHeaders</c>.</b> Il middleware standard e' stato
    /// provato per primo ed e' la scelta idiomatica, ma in questo ambiente non elabora l'header:
    /// verificato in produzione con un endpoint diagnostico, <c>X-Forwarded-For</c> arrivava
    /// valorizzato, <c>X-Original-Forwarded-For</c> restava vuoto (segno che il middleware non
    /// aveva consumato nulla) e <c>Connection.RemoteIpAddress</c> continuava a essere quello del
    /// proxy. Sono stati esclusi i sospetti sulle liste di proxy noti (svuotate, quindi il
    /// controllo e' disattivato), sull'ordine nella pipeline (e' il primo middleware) e sulla
    /// presenza di <c>X-Forwarded-Proto</c>. Invece di continuare a indagare su un componente che
    /// non e' ispezionabile dall'esterno, la lettura e' fatta qui: e' esplicita, non dipende da
    /// configurazioni implicite ed e' coperta da test.
    /// </para>
    /// <para>
    /// <b>Quale elemento della catena si prende.</b> <c>X-Forwarded-For</c> e' una lista in cui
    /// ogni proxy attraversato aggiunge in coda l'indirizzo da cui ha ricevuto la richiesta. Non
    /// va preso il primo: quello e' l'elemento che il client stesso puo' aver scritto, e
    /// leggerlo permetterebbe di falsificare l'indirizzo nell'audit trail e di aggirare il rate
    /// limit del login, che partiziona proprio su questo valore. Si prende l'ultimo anello
    /// rimasto dopo aver scartato quelli che appartengono all'infrastruttura.
    /// </para>
    /// <para>
    /// V2-008 — catena osservata su Azure App Service l'08/10/2026 (endpoint diagnostico):
    /// <code>
    /// X-Forwarded-For: 79.30.166.123:64673            (richiesta normale)
    /// X-Forwarded-For: 1.2.3.4, 79.30.166.123:51750   (client che si inventa "1.2.3.4")
    ///                           ^ client, scritto da Azure, con la porta
    /// Connection.RemoteIpAddress: ::ffff:169.254.129.1   (rete interna, uguale per tutti)
    /// </code>
    /// Azure aggiunge un solo anello, ed e' gia' il client: <see cref="AnelliDaScartare"/> vale 0.
    /// La proprieta' di sicurezza regge: l'elemento inventato finisce davanti, l'ultimo lo scrive
    /// Azure. Su Railway (misura del 07/09/2026) gli anelli erano due e se ne scartava uno: dopo il
    /// passaggio ad Azure quel valore faceva ricadere tutti sull'indirizzo interno (rate limit del
    /// login di fatto globale) e accettava l'indirizzo inventato.
    /// </para>
    /// <para>
    /// ⚠️ <b>Se un domani la piattaforma cambia, questo valore va rimisurato</b> con l'endpoint
    /// diagnostico (<c>GET api/LogActivity/diagnostica-inoltro</c>, una richiesta normale e una con
    /// un <c>X-Forwarded-For</c> inventato), leggendo l'header <b>prima</b> che qualcuno lo
    /// consumi: <c>UseForwardedHeaders</c> rimuove l'anello che elabora, quindi guardando l'header
    /// a valle la catena sembra piu' corta.
    /// </para>
    /// <para>
    /// V2-028 — dalla v3.1 il sito chiama /api attraverso Vercel, e in fondo alla catena c'e' un
    /// indirizzo di Vercel. Per quelle richieste l'indirizzo vero lo mette
    /// <c>InoltroVercelMiddleware</c> in <c>HttpContext.Items</c>, solo se la parola segreta di
    /// Vercel e' giusta, e ha la precedenza. La lettura della catena resta per le chiamate dirette
    /// ad Azure (Swagger, curl) finche' non si bloccano.
    /// </para>
    /// </summary>
    public static class IndirizzoClient
    {
        /// <summary>Chiave di <c>HttpContext.Items</c> con l'indirizzo verificato da InoltroVercelMiddleware.</summary>
        public const string ChiaveIndirizzoVerificato = "Gestora.IndirizzoVerificato";

        private const string HeaderInoltro = "X-Forwarded-For";

        /// <summary>
        /// Quanti anelli in fondo alla catena appartengono all'infrastruttura e non al client.
        /// Misurato su Azure l'08/10/2026: nessuno, l'unico anello aggiunto e' il client (V2-008).
        /// </summary>
        private const int AnelliDaScartare = 0;

        /// <summary>
        /// Indirizzo del chiamante, o <c>null</c> se non determinabile.
        /// In locale, dove non c'e' alcun proxy e l'header non esiste, ricade su
        /// <c>Connection.RemoteIpAddress</c>, che li' e' gia' il valore corretto.
        /// </summary>
        public static string? Ottieni(HttpContext? context)
        {
            if (context is null)
                return null;

            if (context.Items.TryGetValue(ChiaveIndirizzoVerificato, out var verificato)
                && verificato is string indirizzoVerificato)
                return Normalizza(indirizzoVerificato);

            if (context.Request.Headers.TryGetValue(HeaderInoltro, out var inoltrati))
            {
                // L'header puo' arrivare come piu' righe, e ogni riga puo' contenere piu'
                // indirizzi separati da virgola: vanno appiattiti prima di contare gli anelli.
                var catena = inoltrati
                    .SelectMany(riga => (riga ?? string.Empty).Split(','))
                    .Select(v => v.Trim())
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToList();

                // Scartati gli anelli dell'infrastruttura, l'ultimo rimasto e' il client.
                var indirizzo = catena
                    .Take(catena.Count - AnelliDaScartare)
                    .LastOrDefault();

                if (!string.IsNullOrWhiteSpace(indirizzo))
                    return Normalizza(indirizzo);

                // Header presente ma vuoto (o piu' corto degli anelli da scartare): si ricade
                // sull'indirizzo della connessione invece di restituire un dato inventato.
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }

        /// <summary>
        /// Toglie l'eventuale porta. Alcuni proxy scrivono "1.2.3.4:5678"; per IPv6 la forma con
        /// porta e' "[::1]:5678", mentre un IPv6 nudo contiene due punti ovunque e non va toccato.
        /// </summary>
        private static string Normalizza(string indirizzo)
        {
            if (indirizzo.StartsWith('['))
            {
                var chiusura = indirizzo.IndexOf(']');
                if (chiusura > 0)
                    return indirizzo[1..chiusura];
            }

            // Un solo ':' significa IPv4 con porta. Piu' di uno significa IPv6 senza porta.
            var separatore = indirizzo.IndexOf(':');
            if (separatore > 0 && indirizzo.IndexOf(':', separatore + 1) < 0)
                return indirizzo[..separatore];

            return indirizzo;
        }
    }
}
