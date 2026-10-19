using GestoraWebApi.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Moq;

namespace GestoraWebApi.Tests.Infrastructure
{
    /// <summary>
    /// V2-018: gli header di sicurezza ci sono su ogni risposta, la richiesta prosegue sempre, e
    /// Strict-Transport-Security compare solo fuori da Development.
    /// </summary>
    public class SicurezzaHeaderMiddlewareTests
    {
        private static async Task<(DefaultHttpContext Contesto, bool ProseguitoOltre)> Esegui(string ambiente, string percorso)
        {
            var environment = new Mock<IHostEnvironment>();
            environment.Setup(e => e.EnvironmentName).Returns(ambiente);

            var proseguito = false;
            var middleware = new SicurezzaHeaderMiddleware(_ => { proseguito = true; return Task.CompletedTask; }, environment.Object);
            var contesto = new DefaultHttpContext();
            contesto.Request.Path = percorso;

            await middleware.InvokeAsync(contesto);
            return (contesto, proseguito);
        }

        [Theory]
        [InlineData("/api/Prenotazione/check-disponibilita")]
        [InlineData("/health")]
        public async Task SuOgniPercorso_AggiungeGliHeaderEProsegue(string percorso)
        {
            var (contesto, proseguito) = await Esegui(Environments.Production, percorso);

            var headers = contesto.Response.Headers;
            Assert.True(proseguito);
            Assert.Equal("nosniff", headers.XContentTypeOptions);
            Assert.Equal("DENY", headers.XFrameOptions);
            Assert.Equal("default-src 'none'; frame-ancestors 'none'", headers.ContentSecurityPolicy);
            Assert.Equal("no-referrer", headers["Referrer-Policy"]);
        }

        [Fact]
        public async Task FuoriDaDevelopment_AggiungeHsts()
        {
            var (contesto, _) = await Esegui(Environments.Production, "/api/Zona/get-all-zone");

            Assert.Equal("max-age=31536000", contesto.Response.Headers.StrictTransportSecurity);
        }

        // In locale si lavora in HTTP: HSTS non serve e su localhost darebbe solo fastidio.
        [Fact]
        public async Task InDevelopment_NienteHsts_MaGliAltriHeaderSi()
        {
            var (contesto, _) = await Esegui(Environments.Development, "/api/Zona/get-all-zone");

            Assert.False(contesto.Response.Headers.ContainsKey("Strict-Transport-Security"));
            Assert.Equal("nosniff", contesto.Response.Headers.XContentTypeOptions);
        }
    }
}
