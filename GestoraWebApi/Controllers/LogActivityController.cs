using GestoraWebApi.Auth;
using GestoraWebApi.Common;
using GestoraWebApi.Services.LogActivity;
using GestoraWebApi.Services.LogActivity.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestoraWebApi.Controllers
{
    /// <summary>
    /// REV-037: lettura dell'audit trail. Finora la tabella <c>LogActivities</c> si scriveva e
    /// basta: per sapere chi aveva fatto cosa bisognava collegarsi al database di produzione,
    /// cioe' in pratica non si guardava mai.
    /// Riservato all'Admin: e' il registro di chi ha fatto cosa, e comprende gli indirizzi IP.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Roles.Admin)]
    public class LogActivityController : ControllerBase
    {
        private readonly ILogActivityService _logActivityService;
        private readonly ILogger<LogActivityController> _logger;

        public LogActivityController(ILogActivityService logActivityService,
                                     ILogger<LogActivityController> logger)
        {
            _logActivityService = logActivityService;
            _logger = logger;
        }

        /// <summary>
        /// Elenco paginato del registro attivita', dal piu' recente. Filtri opzionali: utente,
        /// intervallo di date (UTC) e ricerca libera sul testo dell'azione.
        /// </summary>
        [HttpGet("get-log")]
        public async Task<IActionResult> GetLog([FromQuery] LogActivityQueryParams query)
        {
            var risultato = await _logActivityService.GetLogAsync(query);

            _logger.LogInformation("[{Controller}] - [{Method}]: letta pagina {Page} del registro attività ({Count} righe su {Totale})",
                nameof(LogActivityController), nameof(GetLog), risultato.Page, risultato.Items.Count, risultato.TotalCount);

            // Nessun 404 sulla lista vuota: un intervallo senza eventi e' una risposta, non un
            // errore (REV-031).
            return Ok(risultato);
        }

        /// <summary>
        /// Strumento diagnostico per REV-029: mostra affiancati l'indirizzo che l'applicazione
        /// usa davvero e gli header di inoltro grezzi.
        /// <para>
        /// Era nato temporaneo, ed e' stato tenuto di proposito. La costante
        /// <c>IndirizzoClient.AnelliDaScartare</c> dipende da quanti proxy la piattaforma mette
        /// davanti all'applicazione: se quel numero cambia, l'indirizzo registrato torna
        /// silenziosamente sbagliato e l'unico modo di accorgersene e' guardare la catena
        /// dall'interno dell'ambiente reale — in locale questi header non esistono affatto.
        /// Senza questo endpoint, rimisurarla richiede di scriverlo e rilasciarlo di nuovo:
        /// e' esattamente il giro che nel settembre 2026 e' costato tre deploy e tre giorni.
        /// </para>
        /// <para>
        /// V2-028: dice anche se la richiesta è passata da Vercel con la parola segreta giusta; in
        /// quel caso l'indirizzo usato è <c>X-Real-IP</c>, non l'ultimo anello della catena.
        /// </para>
        /// <para>
        /// Riservato all'Admin e volutamente limitato alla <b>richiesta corrente</b>: mostra solo
        /// i dati di chi sta chiamando, non tocca il traffico di altri utenti.
        /// </para>
        /// </summary>
        [HttpGet("diagnostica-inoltro")]
        public IActionResult DiagnosticaInoltro()
        {
            // Header grezzi, come arrivano.
            string? Header(string nome) =>
                Request.Headers.TryGetValue(nome, out var valore) ? valore.ToString() : null;

            return Ok(new
            {
                // ⬅️ Il valore che finisce davvero nell'audit trail e nella partizione del
                // rate limit: e' quello che deve corrispondere all'indirizzo di chi chiama.
                indirizzoUsatoDallApplicazione = IndirizzoClient.Ottieni(HttpContext),

                // V2-028: true se InoltroVercelMiddleware ha riconosciuto la parola segreta.
                passatoDaVercel = HttpContext.Items.ContainsKey(IndirizzoClient.ChiaveIndirizzoVerificato),

                // Quello che vedrebbe l'applicazione senza la lettura esplicita dell'header:
                // dietro il proxy e' il suo indirizzo, uguale per tutti i client.
                remoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),

                // La catena di inoltro: e' da qui che IndirizzoClient prende l'ultimo anello.
                xForwardedFor = Header("X-Forwarded-For"),

                // Se valorizzato, significa che un middleware ha consumato l'header prima di noi:
                // e' il caso da tenere d'occhio se un domani si reintroduce UseForwardedHeaders.
                xOriginalForwardedFor = Header("X-Original-Forwarded-For"),

                xForwardedProto = Header("X-Forwarded-Proto"),

                // Header alternativi di alcune piattaforme (Envoy, Cloudflare), tenuti per
                // riferimento: qui risultano tutti vuoti.
                xEnvoyExternalAddress = Header("X-Envoy-External-Address"),
                xRealIp = Header("X-Real-IP"),
                cfConnectingIp = Header("CF-Connecting-IP")
            });
        }
    }
}
