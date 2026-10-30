using System.Security.Cryptography;
using System.Text;

namespace GestoraWebApi.Infrastructure.Auth
{
    /// <summary>
    /// V2-028 — seconda difesa contro le richieste fatte a nome dell'utente da un altro sito (CSRF),
    /// dopo <c>SameSite=Strict</c> del cookie di sessione. Il browser allega il cookie di sessione da
    /// solo, quindi il cookie da solo non basta a dire che la richiesta l'ha voluta la nostra pagina.
    /// <para>
    /// «Doppio invio»: insieme al cookie di sessione si scrive un secondo cookie con un valore a caso,
    /// questo sì leggibile da JavaScript. Axios lo legge e lo rimanda nell'header
    /// <see cref="NomeHeader"/>; <c>ProtezioneCsrfMiddleware</c> controlla che i due coincidano. Un
    /// altro sito può far partire la richiesta, ma non può leggere il cookie, quindi non sa cosa
    /// scrivere nell'header.
    /// </para>
    /// </summary>
    public static class CookieCsrf
    {
        /// <summary>
        /// Con il prefisso <c>__Host-</c> nessun altro sito, nemmeno un sottodominio, può scrivere un
        /// cookie con questo nome: senza, un attaccante potrebbe imporne uno suo e conoscerne il valore.
        /// Va impostato uguale in <c>xsrfCookieName</c> di Axios.
        /// </summary>
        public const string Nome = "__Host-gestora-xsrf";

        /// <summary>Il nome che Axios usa di default per rimandare il valore.</summary>
        public const string NomeHeader = "X-XSRF-TOKEN";

        /// <summary>
        /// Scrive un valore nuovo, a caso, con la stessa scadenza del cookie di sessione. Lo chiama
        /// <see cref="CookieSessione.Scrivi"/>: i due cookie nascono e muoiono insieme.
        /// </summary>
        public static void Scrivi(HttpResponse response, DateTime scadenza)
        {
            var valore = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            response.Cookies.Append(Nome, valore, Opzioni(scadenza));
        }

        public static void Cancella(HttpResponse response)
            => response.Cookies.Delete(Nome, Opzioni(scadenza: null));

        /// <summary>
        /// Vero se cookie e header ci sono e coincidono. Il confronto dura sempre lo stesso tempo,
        /// così dai tempi di risposta non si indovina il valore un carattere alla volta.
        /// </summary>
        public static bool RichiestaValida(HttpRequest request)
        {
            var cookie = request.Cookies[Nome];
            var header = request.Headers[NomeHeader].ToString();

            if (string.IsNullOrEmpty(cookie) || string.IsNullOrEmpty(header))
                return false;

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(cookie), Encoding.UTF8.GetBytes(header));
        }

        private static CookieOptions Opzioni(DateTime? scadenza) => new()
        {
            // Leggibile da JavaScript apposta: è Axios che deve rimandarlo. Da solo non apre nulla.
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = scadenza is null ? null : new DateTimeOffset(scadenza.Value, TimeSpan.Zero),
            IsEssential = true
        };
    }
}
