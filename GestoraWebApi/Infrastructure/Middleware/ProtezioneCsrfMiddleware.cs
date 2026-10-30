using GestoraWebApi.Infrastructure.Auth;
using GestoraWebApi.Models;
using System.Net;
using System.Text.Json;

namespace GestoraWebApi.Infrastructure.Middleware
{
    /// <summary>
    /// V2-028 — controlla il doppio invio di <see cref="CookieCsrf"/> sulle richieste che cambiano
    /// qualcosa (tutto tranne GET, HEAD, OPTIONS, TRACE) e che si presentano con il cookie di
    /// sessione: sono le sole che un altro sito potrebbe far partire a nome dell'utente.
    /// <para>
    /// Non si controlla chi manda l'header <c>Authorization</c> (Swagger, curl): un altro sito non
    /// può aggiungerlo, e in quel caso il cookie non viene nemmeno letto. Né chi non ha il cookie di
    /// sessione: per lui non c'è niente da rubare (login, registrazione, pagina pubblica).
    /// </para>
    /// <para>
    /// Se il controllo fallisce: 400 e i due cookie si tolgono. Così una sessione rimasta a metà
    /// (un cookie senza l'altro) non blocca per sempre: al tentativo dopo si rifà l'accesso.
    /// Sta prima di <c>UseAuthentication</c>: una richiesta rifiutata non arriva a leggere il token.
    /// </para>
    /// </summary>
    public class ProtezioneCsrfMiddleware
    {
        public const string Messaggio = "Richiesta non valida: ricarica la pagina ed effettua di nuovo l'accesso.";

        private readonly RequestDelegate _next;

        public ProtezioneCsrfMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var request = context.Request;
            var daControllare = !HttpMethods.IsGet(request.Method)
                                && !HttpMethods.IsHead(request.Method)
                                && !HttpMethods.IsOptions(request.Method)
                                && !HttpMethods.IsTrace(request.Method)
                                && CookieSessione.TokenDallaRichiesta(request) != null;

            if (!daControllare || CookieCsrf.RichiestaValida(request))
            {
                await _next(context);
                return;
            }

            CookieSessione.Cancella(context.Response);

            var risposta = new ErrorDetails
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Message = Messaggio
            };

            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            await context.Response.WriteAsync(JsonSerializer.Serialize(risposta, options));
        }
    }
}
