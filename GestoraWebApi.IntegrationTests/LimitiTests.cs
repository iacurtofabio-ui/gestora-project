using System.Net;
using System.Net.Http.Json;
using GestoraWebApi.Auth;
using GestoraWebApi.Common;
using GestoraWebApi.Infrastructure.Auth;
using GestoraWebApi.IntegrationTests.Infrastruttura;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace GestoraWebApi.IntegrationTests
{
    /// <summary>Limiti di richieste, blocco per indirizzo e parola segreta di Vercel.</summary>
    public class LimitiTests(GestoraFixture gestora) : TestIntegrazione(gestora)
    {
        private const string Email = "cliente@prova.test";

        [Fact]
        public async Task Login_DalSestoTentativoInUnMinuto_Risponde429()
        {
            using var client = Gestora.NuovoClient();

            for (var i = 0; i < 5; i++)
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.AccediAsync(Email, "Sbagliata1!")).StatusCode);

            Assert.Equal(HttpStatusCode.TooManyRequests, (await client.AccediAsync(Email, "Sbagliata1!")).StatusCode);
        }

        [Fact]
        public async Task BloccoPerIndirizzo_FermaAncheLaPasswordGiusta_MaNonUnAltroIndirizzo()
        {
            await Gestora.CreaUtenteAsync(Email, Roles.Cliente);
            using var bloccato = Gestora.NuovoClient();
            using var altro = Gestora.NuovoClient();

            // Il contatore si riempie da qui: dal login il sesto tentativo si fermerebbe al 429.
            var tentativi = Gestora.Fabbrica.Services.GetRequiredService<ITentativiAccesso>();
            for (var i = 0; i < TentativiAccesso.ErroriMassimi; i++)
                tentativi.RegistraErrore(Email, bloccato.Indirizzo);

            Assert.Equal(HttpStatusCode.Locked, (await bloccato.AccediAsync(Email, GestoraFixture.Password)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await altro.AccediAsync(Email, GestoraFixture.Password)).StatusCode);
        }

        [Fact]
        public async Task Disponibilita_OltreLaRaffica_Risponde429()
        {
            var giorno = Gestora.GiornoFuturo();
            await Gestora.CreaSalaAsync(giorno, [4]);
            using var client = Gestora.NuovoClient();
            var dati = new { dataPrenotazione = giorno, numeroCoperti = 2 };

            var stati = new List<HttpStatusCode>();
            for (var i = 0; i < 40; i++)
                stati.Add((await client.Http.PostAsJsonAsync("/api/Prenotazione/check-disponibilita", dati)).StatusCode);

            Assert.Equal(HttpStatusCode.OK, stati[0]);
            Assert.Contains(HttpStatusCode.TooManyRequests, stati);
        }

        [Fact]
        public async Task Pareri_IlTerzoIn30Giorni_Rifiutato()
        {
            using var client = Gestora.NuovoClient();
            var parere = new { messaggio = "Tutto molto bello, grazie." };

            var stati = new List<HttpStatusCode>();
            for (var i = 0; i < 3; i++)
                stati.Add((await client.Http.PostAsJsonAsync("/api/Pareri/invia", parere)).StatusCode);

            Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], stati);
            Assert.Equal(2, Gestora.Posta.Email.Count);
            Assert.All(Gestora.Posta.Email, e => Assert.Equal("pareri@prova.test", e.Destinatario));
        }

        [Fact]
        public async Task ParolaSegreta_SoloConQuella_SiCredeAdXRealIp()
        {
            using var admin = await Gestora.AccediComeAsync(Roles.Admin);

            var conSegreto = await admin.Http.GetStringAsync("/api/LogActivity/diagnostica-inoltro");
            Assert.Contains(admin.Indirizzo, conSegreto);

            admin.PassaDaVercel = false;
            using var falso = new HttpRequestMessage(HttpMethod.Get, "/api/LogActivity/diagnostica-inoltro");
            falso.Headers.Add("X-Real-IP", "6.6.6.6");
            falso.Headers.Add("X-Gestora-Inoltro", "parola-sbagliata");

            var senzaSegreto = await (await admin.Http.SendAsync(falso)).Content.ReadAsStringAsync();
            Assert.Contains("\"passatoDaVercel\":false", senzaSegreto);
            Assert.DoesNotContain("\"indirizzoUsatoDallApplicazione\":\"6.6.6.6\"", senzaSegreto);
        }

        [Fact]
        public async Task ChiamateDirette_ConIlBloccoAcceso_403_MaHealthResta()
        {
            await using var bloccata = Gestora.Fabbrica.WithWebHostBuilder(builder => builder.ConfigureTestServices(
                services => services.PostConfigure<InoltroVercelSettings>(s => s.BloccaChiamateDirette = true)));

            using var dalSito = new ClientGestora(bloccata);
            using var diretto = new ClientGestora(bloccata) { PassaDaVercel = false };

            Assert.Equal(HttpStatusCode.OK, (await dalSito.Http.GetAsync("/api/Setup/stato")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await diretto.Http.GetAsync("/api/Setup/stato")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await diretto.Http.GetAsync("/health")).StatusCode);
        }
    }
}
