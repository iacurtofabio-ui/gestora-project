using GestoraWebApi.Common;
using GestoraWebApi.Models;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GestoraWebApi.Infrastructure.Middleware
{
    /// <summary>
    /// V2-028 — il frontend chiama /api sul proprio indirizzo e Vercel inoltra ad Azure aggiungendo
    /// la parola segreta (<see cref="NomeHeader"/>). Dietro Vercel l'ultimo anello di
    /// <c>X-Forwarded-For</c> è un indirizzo di Vercel, diverso a ogni richiesta (misura di T02): i
    /// limiti di richieste e il blocco per indirizzo vedrebbero tutti come «Vercel».
    /// <para>
    /// Vercel però riscrive <c>X-Real-IP</c> con l'indirizzo vero del visitatore, sovrascrivendo
    /// quello che il visitatore prova a inventare. Quando la parola segreta è giusta, la richiesta è
    /// passata da Vercel e quell'header è affidabile: l'indirizzo va in <c>HttpContext.Items</c>, da
    /// dove lo prende <see cref="IndirizzoClient"/>. Senza parola segreta (o sbagliata) l'header non
    /// conta nulla, perché chiunque chiami Azure direttamente può scriverlo.
    /// </para>
    /// <para>
    /// Con <see cref="InoltroVercelSettings.BloccaChiamateDirette"/> acceso, le chiamate a /api senza
    /// parola segreta ricevono 403; <c>/health</c> resta libero per Azure e UptimeRobot. La parola
    /// segreta si toglie dalla richiesta in ogni caso: non deve finire in nessun log. Va prima dei
    /// limiti di richieste e dell'autenticazione, che usano l'indirizzo.
    /// </para>
    /// </summary>
    public class InoltroVercelMiddleware
    {
        public const string NomeHeader = "X-Gestora-Inoltro";
        public const string Messaggio = "Accesso non consentito: usa il sito di Gestora.";

        private const string HeaderIndirizzo = "X-Real-IP";

        private readonly RequestDelegate _next;
        private readonly InoltroVercelSettings _settings;

        public InoltroVercelMiddleware(RequestDelegate next, IOptions<InoltroVercelSettings> settings)
        {
            _next = next;
            _settings = settings.Value;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var request = context.Request;
            var parolaRicevuta = request.Headers[NomeHeader].ToString();
            request.Headers.Remove(NomeHeader);

            if (ParolaGiusta(parolaRicevuta))
            {
                var indirizzo = request.Headers[HeaderIndirizzo].ToString().Trim();
                if (!string.IsNullOrEmpty(indirizzo))
                    context.Items[IndirizzoClient.ChiaveIndirizzoVerificato] = indirizzo;
            }
            else if (_settings.BloccaChiamateDirette && request.Path.StartsWithSegments("/api"))
            {
                var risposta = new ErrorDetails
                {
                    StatusCode = (int)HttpStatusCode.Forbidden,
                    Message = Messaggio
                };

                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                context.Response.ContentType = "application/json";

                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                await context.Response.WriteAsync(JsonSerializer.Serialize(risposta, options));
                return;
            }

            await _next(context);
        }

        /// <summary>
        /// Confronto a tempo costante: dai tempi di risposta non si indovina la parola un carattere
        /// alla volta. Senza parola configurata (solo in locale) nessuna richiesta è «da Vercel».
        /// </summary>
        private bool ParolaGiusta(string ricevuta)
            => !string.IsNullOrEmpty(_settings.Segreto)
               && !string.IsNullOrEmpty(ricevuta)
               && CryptographicOperations.FixedTimeEquals(
                   Encoding.UTF8.GetBytes(ricevuta), Encoding.UTF8.GetBytes(_settings.Segreto));
    }
}
