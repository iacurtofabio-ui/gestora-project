using System.Net;
using System.Net.Http.Json;
using System.Web;
using GestoraWebApi.Auth;
using GestoraWebApi.IntegrationTests.Infrastruttura;
using Microsoft.EntityFrameworkCore;

namespace GestoraWebApi.IntegrationTests
{
    /// <summary>Registrazione, link via email e cancellazione: il giro completo, con le transazioni vere.</summary>
    public class AccountTests(GestoraFixture gestora) : TestIntegrazione(gestora)
    {
        private const string Email = "nuovo@prova.test";

        private async Task<HttpResponseMessage> RegistraAsync(ClientGestora client, bool accettaPrivacy = true)
            => await client.Http.PostAsJsonAsync("/api/AuthenticationUser/register", new
            {
                username = "nuovo",
                email = Email,
                password = GestoraFixture.Password,
                accettaPrivacy
            });

        private static (string UserId, string Token) DatiDelLink(Uri link)
        {
            var parametri = HttpUtility.ParseQueryString(link.Query);
            return (parametri["userId"]!, parametri["token"]!);
        }

        [Fact]
        public async Task PrimoAvvio_CreaLAdmin_PoiSiChiude()
        {
            using var client = Gestora.NuovoClient();
            var dati = new { username = "admin", email = "admin@prova.test", password = GestoraFixture.Password };

            var primo = await client.Http.PostAsJsonAsync("/api/Setup/admin", dati);
            var secondo = await client.Http.PostAsJsonAsync("/api/Setup/admin",
                new { username = "altro", email = "altro@prova.test", password = GestoraFixture.Password });

            Assert.Equal(HttpStatusCode.OK, primo.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, secondo.StatusCode);
        }

        [Fact]
        public async Task Registrazione_SenzaInformativa_Rifiutata()
        {
            using var client = Gestora.NuovoClient();

            var risposta = await RegistraAsync(client, accettaPrivacy: false);

            Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
            Assert.Empty(Gestora.Posta.Email);
        }

        [Fact]
        public async Task Registrazione_SenzaConferma_NonEntra_ConIlLinkSi()
        {
            using var client = Gestora.NuovoClient();

            Assert.Equal(HttpStatusCode.OK, (await RegistraAsync(client)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.AccediAsync(Email, GestoraFixture.Password)).StatusCode);

            var (userId, token) = DatiDelLink(Assert.Single(Gestora.Posta.Email).Link);
            var conferma = await client.Http.PostAsJsonAsync("/api/AuthenticationUser/conferma-email", new { userId, token });

            Assert.Equal(HttpStatusCode.OK, conferma.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.AccediAsync(Email, GestoraFixture.Password)).StatusCode);
        }

        [Fact]
        public async Task Conferma_ConLinkAlterato_Rifiutata()
        {
            using var client = Gestora.NuovoClient();
            await RegistraAsync(client);
            var (userId, _) = DatiDelLink(Assert.Single(Gestora.Posta.Email).Link);

            var risposta = await client.Http.PostAsJsonAsync("/api/AuthenticationUser/conferma-email",
                new { userId, token = "token-inventato" });

            Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
        }

        [Fact]
        public async Task PasswordDimenticata_LaNuovaFunziona_LaVecchiaNo_IlLinkNonSiRiusa()
        {
            await Gestora.CreaUtenteAsync(Email, Roles.Cliente);
            using var client = Gestora.NuovoClient();
            const string nuovaPassword = "NuovaGestora2!";

            await client.Http.PostAsJsonAsync("/api/AuthenticationUser/password-dimenticata", new { email = Email });
            var (userId, token) = DatiDelLink(Assert.Single(Gestora.Posta.Email).Link);
            var dati = new { userId, token, nuovaPassword };

            var primo = await client.Http.PostAsJsonAsync("/api/AuthenticationUser/reimposta-password", dati);
            var secondo = await client.Http.PostAsJsonAsync("/api/AuthenticationUser/reimposta-password", dati);

            Assert.Equal(HttpStatusCode.OK, primo.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, secondo.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.AccediAsync(Email, GestoraFixture.Password)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.AccediAsync(Email, nuovaPassword)).StatusCode);
        }

        [Fact]
        public async Task PasswordDimenticata_EmailInesistente_StessaRisposta_NessunaEmail()
        {
            using var client = Gestora.NuovoClient();

            var risposta = await client.Http.PostAsJsonAsync("/api/AuthenticationUser/password-dimenticata",
                new { email = "nessuno@prova.test" });

            Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
            Assert.Empty(Gestora.Posta.Email);
        }

        [Fact]
        public async Task CambioPassword_ChiudeLeAltreSessioni_ETieneApertaQuesta()
        {
            using var questa = await Gestora.AccediComeAsync(Roles.Cliente, Email);
            using var altra = Gestora.NuovoClient();
            await altra.AccediAsync(Email, GestoraFixture.Password);

            var cambio = await questa.Http.PostAsJsonAsync("/api/Account/cambia-password",
                new { passwordAttuale = GestoraFixture.Password, nuovaPassword = "NuovaGestora2!" });

            Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await questa.Http.GetAsync("/api/Account/mio-account")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await altra.Http.GetAsync("/api/Account/mio-account")).StatusCode);
        }

        [Fact]
        public async Task Cancellazione_ConPrenotazioneInProgramma_Bloccata()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [2]);
            using var cliente = await Gestora.AccediComeAsync(Roles.Cliente, Email);
            await Gestora.PrenotaAsync(cliente, sala, 2);

            var risposta = await cliente.Http.PostAsJsonAsync("/api/Account/cancella-account",
                new { password = GestoraFixture.Password });

            Assert.Equal(HttpStatusCode.Conflict, risposta.StatusCode);
            Assert.True(await Gestora.ConContestoAsync(c => c.Users.AnyAsync(u => u.Email == Email)));
        }

        [Fact]
        public async Task Cancellazione_LePrenotazioniPassateRestanoSenzaNome()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [2]);
            using var cliente = await Gestora.AccediComeAsync(Roles.Cliente, Email);
            var id = await Gestora.PrenotaAsync(cliente, sala, 2);

            Gestora.Orologio.VaiA(sala.Giorno.AddDays(1), new TimeOnly(12, 0));
            var risposta = await cliente.Http.PostAsJsonAsync("/api/Account/cancella-account",
                new { password = GestoraFixture.Password });

            Assert.True(risposta.IsSuccessStatusCode, await risposta.Content.ReadAsStringAsync());

            var prenotazione = await Gestora.ConContestoAsync(c => c.Prenotazioni.SingleAsync(p => p.Id == id));
            Assert.Null(prenotazione.UserId);
            Assert.Equal("Cliente eliminato", prenotazione.NomeCliente);
            Assert.False(await Gestora.ConContestoAsync(c => c.Users.AnyAsync(u => u.Email == Email)));
        }

        [Fact]
        public async Task Cancellazione_DellUltimoAdmin_Bloccata()
        {
            using var admin = await Gestora.AccediComeAsync(Roles.Admin);

            var risposta = await admin.Http.PostAsJsonAsync("/api/Account/cancella-account",
                new { password = GestoraFixture.Password });

            Assert.Equal(HttpStatusCode.Conflict, risposta.StatusCode);
        }

        [Fact]
        public async Task EliminazioneDiUnaPrenotazione_LasciaLaTracciaNelRegistro()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [2]);
            using var admin = await Gestora.AccediComeAsync(Roles.Admin);
            var id = await Gestora.PrenotaAsync(admin, sala, 2, "Rossi");
            await admin.Http.PatchAsync($"/api/Prenotazione/annulla-prenotazione?id={id}", null);

            var prima = await Gestora.ConContestoAsync(c => c.LogActivities.CountAsync());
            await admin.Http.DeleteAsync($"/api/Prenotazione/delete-prenotazione?id={id}");

            Assert.Equal(prima + 1, await Gestora.ConContestoAsync(c => c.LogActivities.CountAsync()));
        }
    }
}
