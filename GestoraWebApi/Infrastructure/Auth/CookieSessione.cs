using System.IdentityModel.Tokens.Jwt;

namespace GestoraWebApi.Infrastructure.Auth
{
    /// <summary>
    /// V2-028 — il token di accesso viaggia in un cookie <c>HttpOnly</c>, non più nel corpo delle
    /// risposte. Prima il frontend lo teneva in <c>localStorage</c>, dove qualsiasi script della
    /// pagina poteva leggerlo; il cookie <c>HttpOnly</c> il browser lo allega da solo alle chiamate
    /// ma non lo mostra mai a JavaScript.
    /// <para>
    /// Funziona perché il frontend chiama <c>/api</c> sul proprio indirizzo e Vercel inoltra ad
    /// Azure: per il browser è lo stesso sito. Con le chiamate dirette ad Azure (sito diverso)
    /// Safari scarterebbe il cookie.
    /// </para>
    /// </summary>
    public static class CookieSessione
    {
        /// <summary>
        /// Il prefisso <c>__Host-</c> obbliga il browser ad accettare il cookie solo se è
        /// <c>Secure</c>, vale per tutto il sito (<c>Path=/</c>) e non indica un dominio: nessun
        /// altro sito, nemmeno un sottodominio, può scriverne uno con lo stesso nome.
        /// </summary>
        public const string Nome = "__Host-gestora-sessione";

        /// <summary>
        /// Scrive il cookie con il token e restituisce la scadenza del token, che il frontend usa
        /// per sapere quando rinnovarlo (il token non lo vede più).
        /// </summary>
        public static DateTime Scrivi(HttpResponse response, string token)
        {
            // ValidTo è già in UTC. Il cookie scade con il token: oltre non servirebbe a nulla.
            var scadenza = new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo;
            response.Cookies.Append(Nome, token, Opzioni(scadenza));
            CookieCsrf.Scrivi(response, scadenza);
            return scadenza;
        }

        /// <summary>
        /// Toglie il cookie, e con lui quello della protezione CSRF. Il browser lo cancella solo se
        /// nome, percorso e attributi coincidono con quelli usati per scriverlo, per questo si
        /// passano le stesse opzioni.
        /// </summary>
        public static void Cancella(HttpResponse response)
        {
            response.Cookies.Delete(Nome, Opzioni(scadenza: null));
            CookieCsrf.Cancella(response);
        }

        /// <summary>
        /// Il token da controllare, letto dal cookie. Se la richiesta porta già l'header
        /// <c>Authorization</c> (Swagger, la diagnostica con curl) restituisce null e il controllo
        /// del token usa quello, come prima.
        /// </summary>
        public static string? TokenDallaRichiesta(HttpRequest request)
        {
            if (!string.IsNullOrEmpty(request.Headers.Authorization))
                return null;

            return request.Cookies.TryGetValue(Nome, out var token) && !string.IsNullOrEmpty(token)
                ? token
                : null;
        }

        private static CookieOptions Opzioni(DateTime? scadenza) => new()
        {
            HttpOnly = true,
            // In locale il sito è http://localhost, che i browser trattano come sicuro: il cookie
            // Secure viene accettato anche lì.
            Secure = true,
            // Strict: il browser non lo manda in nessuna richiesta partita da un altro sito. È la
            // prima difesa contro le richieste fatte a nome dell'utente (CSRF); la seconda è CookieCsrf.
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = scadenza is null ? null : new DateTimeOffset(scadenza.Value, TimeSpan.Zero),
            // Necessario al funzionamento dell'applicazione: non dipende dal consenso ai cookie.
            IsEssential = true
        };
    }
}
