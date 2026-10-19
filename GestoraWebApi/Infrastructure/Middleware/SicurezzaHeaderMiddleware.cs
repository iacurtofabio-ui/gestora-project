namespace GestoraWebApi.Infrastructure.Middleware
{
    /// <summary>
    /// V2-018 — header di sicurezza su ogni risposta, quelli consigliati da OWASP per un'API che
    /// risponde solo JSON: il browser non indovina il tipo del contenuto, non mostra le risposte
    /// dentro un'altra pagina, non esegue niente di quello che arriva da qui e non rivela da quale
    /// indirizzo parte la chiamata.
    ///
    /// Strict-Transport-Security e' scritto a mano e non con UseHsts: UseHsts lo aggiunge solo
    /// se la richiesta risulta HTTPS, ma dietro il proxy di Azure al backend arriva HTTP (lo
    /// stesso motivo per cui UseForwardedHeaders non e' usato, vedi Program.cs). In Development
    /// non serve: in locale si lavora in HTTP.
    ///
    /// Va registrato dopo Swagger: la sua pagina ha script e stili, e con questa
    /// Content-Security-Policy non si caricherebbe.
    /// </summary>
    public class SicurezzaHeaderMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly bool _conHsts;

        public SicurezzaHeaderMiddleware(RequestDelegate next, IHostEnvironment environment)
        {
            _next = next;
            _conHsts = !environment.IsDevelopment();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            headers["Referrer-Policy"] = "no-referrer";

            if (_conHsts)
            {
                // Un anno, senza includeSubDomains ne' preload: l'indirizzo e' un sottodominio
                // di azurewebsites.net, che non e' nostro.
                headers.StrictTransportSecurity = "max-age=31536000";
            }

            await _next(context);
        }
    }
}
