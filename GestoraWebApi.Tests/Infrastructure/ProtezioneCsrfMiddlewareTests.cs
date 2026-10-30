using GestoraWebApi.Infrastructure.Auth;
using GestoraWebApi.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace GestoraWebApi.Tests.Infrastructure
{
    /// <summary>
    /// V2-028: chi scrive con il cookie di sessione deve rimandare nell'header il valore del cookie
    /// CSRF. Le letture, chi usa l'header Authorization e chi non ha la sessione passano senza.
    /// </summary>
    public class ProtezioneCsrfMiddlewareTests
    {
        private const string ValoreCsrf = "valore-csrf";

        private static async Task<(DefaultHttpContext Contesto, bool ProseguitoOltre)> Esegui(
            string metodo, bool sessione = true, string? cookieCsrf = ValoreCsrf, string? header = null,
            string? authorization = null)
        {
            var proseguito = false;
            var middleware = new ProtezioneCsrfMiddleware(_ => { proseguito = true; return Task.CompletedTask; });
            var contesto = new DefaultHttpContext();
            contesto.Request.Method = metodo;
            contesto.Request.Path = "/api/Prenotazione/crea-prenotazione";

            var cookie = new List<string>();
            if (sessione) cookie.Add($"{CookieSessione.Nome}=token");
            if (cookieCsrf != null) cookie.Add($"{CookieCsrf.Nome}={cookieCsrf}");
            if (cookie.Count > 0) contesto.Request.Headers.Cookie = string.Join("; ", cookie);
            if (header != null) contesto.Request.Headers[CookieCsrf.NomeHeader] = header;
            if (authorization != null) contesto.Request.Headers.Authorization = authorization;

            contesto.Response.Body = new MemoryStream();
            await middleware.InvokeAsync(contesto);
            return (contesto, proseguito);
        }

        [Theory]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("PATCH")]
        [InlineData("DELETE")]
        public async Task ScritturaConSessione_SenzaHeader_400ESiFerma(string metodo)
        {
            var (contesto, proseguito) = await Esegui(metodo);

            Assert.False(proseguito);
            Assert.Equal(StatusCodes.Status400BadRequest, contesto.Response.StatusCode);
        }

        [Theory]
        [InlineData("POST")]
        [InlineData("DELETE")]
        public async Task ScritturaConSessione_HeaderUgualeAlCookie_Prosegue(string metodo)
        {
            var (_, proseguito) = await Esegui(metodo, header: ValoreCsrf);

            Assert.True(proseguito);
        }

        [Fact]
        public async Task HeaderDiversoDalCookie_400()
        {
            var (contesto, proseguito) = await Esegui("POST", header: "valore-inventato");

            Assert.False(proseguito);
            Assert.Equal(StatusCodes.Status400BadRequest, contesto.Response.StatusCode);
        }

        [Fact]
        public async Task SenzaCookieCsrf_AncheConHeader_400()
        {
            var (_, proseguito) = await Esegui("POST", cookieCsrf: null, header: ValoreCsrf);

            Assert.False(proseguito);
        }

        [Fact]
        public async Task Rifiuto_CorpoConIlMessaggio_ECookieTolti()
        {
            var (contesto, _) = await Esegui("POST");

            contesto.Response.Body.Position = 0;
            using var json = await JsonDocument.ParseAsync(contesto.Response.Body);
            Assert.Equal(400, json.RootElement.GetProperty("statusCode").GetInt32());
            Assert.Equal(ProtezioneCsrfMiddleware.Messaggio, json.RootElement.GetProperty("message").GetString());

            var setCookie = contesto.Response.Headers.SetCookie.ToString();
            Assert.Contains($"{CookieSessione.Nome}=;", setCookie);
            Assert.Contains($"{CookieCsrf.Nome}=;", setCookie);
        }

        [Theory]
        [InlineData("GET")]
        [InlineData("HEAD")]
        [InlineData("OPTIONS")]
        public async Task Letture_PassanoSenzaHeader(string metodo)
        {
            var (_, proseguito) = await Esegui(metodo);

            Assert.True(proseguito);
        }

        [Fact]
        public async Task ConAuthorization_PassaSenzaHeader()
        {
            var (_, proseguito) = await Esegui("POST", authorization: "Bearer token-dall-header");

            Assert.True(proseguito);
        }

        [Fact]
        public async Task SenzaSessione_PassaSenzaHeader()
        {
            var (_, proseguito) = await Esegui("POST", sessione: false, cookieCsrf: null);

            Assert.True(proseguito);
        }
    }
}
