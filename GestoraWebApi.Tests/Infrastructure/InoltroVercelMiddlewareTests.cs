using GestoraWebApi.Common;
using GestoraWebApi.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace GestoraWebApi.Tests.Infrastructure
{
    /// <summary>
    /// V2-028: con la parola segreta di Vercel l'indirizzo è X-Real-IP; senza, X-Real-IP non conta
    /// e resta la lettura di prima. Con il blocco acceso le chiamate dirette a /api ricevono 403.
    /// Le catene sono quelle misurate in T02: in fondo a X-Forwarded-For un indirizzo di Vercel.
    /// </summary>
    public class InoltroVercelMiddlewareTests
    {
        private const string Segreto = "parola-segreta-di-prova-di-almeno-32-caratteri";
        private const string Visitatore = "79.30.166.123";
        private const string IndirizzoVercel = "3.70.12.34";

        private static async Task<(DefaultHttpContext Contesto, bool ProseguitoOltre)> Esegui(
            string? parola, string? realIp = Visitatore, bool blocca = false, string percorso = "/api/AuthenticationUser/login",
            string segretoConfigurato = Segreto)
        {
            var proseguito = false;
            var middleware = new InoltroVercelMiddleware(
                _ => { proseguito = true; return Task.CompletedTask; },
                Options.Create(new InoltroVercelSettings { Segreto = segretoConfigurato, BloccaChiamateDirette = blocca }));

            var contesto = new DefaultHttpContext();
            contesto.Request.Path = percorso;
            contesto.Connection.RemoteIpAddress = IPAddress.Parse("169.254.129.1");
            contesto.Request.Headers["X-Forwarded-For"] = $"{Visitatore}, {IndirizzoVercel}:41234";
            if (realIp != null) contesto.Request.Headers["X-Real-IP"] = realIp;
            if (parola != null) contesto.Request.Headers[InoltroVercelMiddleware.NomeHeader] = parola;
            contesto.Response.Body = new MemoryStream();

            await middleware.InvokeAsync(contesto);
            return (contesto, proseguito);
        }

        [Fact]
        public async Task ParolaGiusta_IndirizzoDelVisitatore()
        {
            var (contesto, proseguito) = await Esegui(Segreto);

            Assert.True(proseguito);
            Assert.Equal(Visitatore, IndirizzoClient.Ottieni(contesto));
        }

        [Fact]
        public async Task ParolaSbagliata_XRealIpInventatoNonConta()
        {
            var (contesto, proseguito) = await Esegui("parola-sbagliata", realIp: "1.2.3.4");

            Assert.True(proseguito);
            // Resta la lettura di sempre: l'ultimo anello della catena.
            Assert.Equal(IndirizzoVercel, IndirizzoClient.Ottieni(contesto));
        }

        [Fact]
        public async Task SenzaParola_XRealIpInventatoNonConta()
        {
            var (contesto, _) = await Esegui(parola: null, realIp: "1.2.3.4");

            Assert.Equal(IndirizzoVercel, IndirizzoClient.Ottieni(contesto));
        }

        [Fact]
        public async Task SenzaSegretoConfigurato_ParolaVuotaNonApreNulla()
        {
            var (contesto, _) = await Esegui(parola: "", realIp: "1.2.3.4", segretoConfigurato: "");

            Assert.Equal(IndirizzoVercel, IndirizzoClient.Ottieni(contesto));
        }

        [Fact]
        public async Task ParolaGiusta_SenzaXRealIp_RestaLaLetturaDiPrima()
        {
            var (contesto, _) = await Esegui(Segreto, realIp: null);

            Assert.Equal(IndirizzoVercel, IndirizzoClient.Ottieni(contesto));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("parola-sbagliata")]
        [InlineData(Segreto)]
        public async Task LaParolaSiToglieDallaRichiesta(string? parola)
        {
            var (contesto, _) = await Esegui(parola);

            Assert.False(contesto.Request.Headers.ContainsKey(InoltroVercelMiddleware.NomeHeader));
        }

        [Fact]
        public async Task BloccoAcceso_ChiamataDirettaAApi_403ESiFerma()
        {
            var (contesto, proseguito) = await Esegui(parola: null, blocca: true);

            Assert.False(proseguito);
            Assert.Equal(StatusCodes.Status403Forbidden, contesto.Response.StatusCode);

            contesto.Response.Body.Position = 0;
            using var json = await JsonDocument.ParseAsync(contesto.Response.Body);
            Assert.Equal(403, json.RootElement.GetProperty("statusCode").GetInt32());
            Assert.Equal(InoltroVercelMiddleware.Messaggio, json.RootElement.GetProperty("message").GetString());
        }

        [Fact]
        public async Task BloccoAcceso_ParolaGiusta_Prosegue()
        {
            var (_, proseguito) = await Esegui(Segreto, blocca: true);

            Assert.True(proseguito);
        }

        [Fact]
        public async Task BloccoAcceso_HealthResta_Libero()
        {
            var (_, proseguito) = await Esegui(parola: null, blocca: true, percorso: "/health");

            Assert.True(proseguito);
        }

        [Fact]
        public async Task BloccoSpento_ChiamataDiretta_Prosegue()
        {
            var (_, proseguito) = await Esegui(parola: null, blocca: false);

            Assert.True(proseguito);
        }
    }
}
