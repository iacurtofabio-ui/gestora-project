using System.Net;
using System.Net.Http.Json;
using GestoraWebApi.Infrastructure.Auth;
using GestoraWebApi.Infrastructure.Middleware;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;

namespace GestoraWebApi.IntegrationTests.Infrastruttura
{
    /// <summary>
    /// Chi usa l'API come farebbe il sito: tiene i cookie, rimanda il valore CSRF nell'header e
    /// passa dal «middleware di Vercel» con un indirizzo tutto suo. L'indirizzo proprio serve anche
    /// a non consumare i limiti di richieste degli altri test.
    /// </summary>
    public sealed class ClientGestora : IDisposable
    {
        private static readonly Uri Sito = new("https://localhost");
        private static int _ultimoIndirizzo;

        private readonly CookieContainerHandler _cookie = new();

        public HttpClient Http { get; }

        public string Indirizzo { get; set; } = NuovoIndirizzo();

        /// <summary>Spento, il client si comporta come una pagina di un altro sito: niente header CSRF.</summary>
        public bool InviaCsrf { get; set; } = true;

        /// <summary>Spento, il client chiama Azure direttamente, senza passare da Vercel.</summary>
        public bool PassaDaVercel { get; set; } = true;

        internal ClientGestora(WebApplicationFactory<Program> fabbrica)
        {
            Http = fabbrica.CreateDefaultClient(Sito, new IntestazioniDelSito(this), _cookie);
        }

        public Cookie? Cookie(string nome) => _cookie.Container.GetCookies(Sito)[nome];

        public Task<HttpResponseMessage> AccediAsync(string email, string password)
            => Http.PostAsJsonAsync("/api/AuthenticationUser/login", new { email, password });

        public void Dispose() => Http.Dispose();

        private static string NuovoIndirizzo()
        {
            var numero = Interlocked.Increment(ref _ultimoIndirizzo);
            return $"10.{numero / 65536 % 256}.{numero / 256 % 256}.{numero % 256}";
        }

        private sealed class IntestazioniDelSito(ClientGestora client) : DelegatingHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                if (client.PassaDaVercel)
                {
                    request.Headers.Add(InoltroVercelMiddleware.NomeHeader, FabbricaGestora.SegretoInoltro);
                    request.Headers.Add("X-Real-IP", client.Indirizzo);
                }

                var csrf = client.Cookie(CookieCsrf.Nome)?.Value;
                if (client.InviaCsrf && csrf is not null)
                    request.Headers.Add(CookieCsrf.NomeHeader, csrf);

                return base.SendAsync(request, ct);
            }
        }
    }
}
