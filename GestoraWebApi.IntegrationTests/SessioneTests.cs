using System.Net;
using System.Net.Http.Json;
using GestoraWebApi.Auth;
using GestoraWebApi.Infrastructure.Auth;
using GestoraWebApi.IntegrationTests.Infrastruttura;

namespace GestoraWebApi.IntegrationTests
{
    /// <summary>La catena intera di accesso: cookie, CSRF, autenticazione. Si vede solo con l'app accesa.</summary>
    public class SessioneTests(GestoraFixture gestora) : TestIntegrazione(gestora)
    {
        private const string Sessione = "/api/AuthenticationUser/sessione";

        [Fact]
        public async Task Login_ScriveCookieDiSessioneHttpOnly_ESenzaTokenNelCorpo()
        {
            await Gestora.CreaUtenteAsync("cliente@prova.test", Roles.Cliente);
            using var client = Gestora.NuovoClient();

            var risposta = await client.AccediAsync("cliente@prova.test", GestoraFixture.Password);

            Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
            Assert.DoesNotContain("token", await risposta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

            var sessione = client.Cookie(CookieSessione.Nome);
            Assert.NotNull(sessione);
            Assert.True(sessione.HttpOnly);
            Assert.True(sessione.Secure);

            var csrf = client.Cookie(CookieCsrf.Nome);
            Assert.NotNull(csrf);
            Assert.False(csrf.HttpOnly);
        }

        [Fact]
        public async Task Sessione_ConIlCookie_DiceChiECollegato()
        {
            using var client = await Gestora.AccediComeAsync(Roles.Staff, "staff@prova.test");

            var risposta = await client.Http.GetFromJsonAsync<Dictionary<string, object>>(Sessione);

            Assert.Equal("staff@prova.test", risposta!["email"].ToString());
        }

        [Fact]
        public async Task Sessione_SenzaCookie_Risponde401()
        {
            using var client = Gestora.NuovoClient();

            var risposta = await client.Http.GetAsync(Sessione);

            Assert.Equal(HttpStatusCode.Unauthorized, risposta.StatusCode);
        }

        [Fact]
        public async Task Scrittura_SenzaHeaderCsrf_Risponde400_EChiudeLaSessione()
        {
            using var client = await Gestora.AccediComeAsync(Roles.Cliente);
            client.InviaCsrf = false;

            var risposta = await client.Http.PostAsync("/api/AuthenticationUser/rinnova-token", null);

            Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
            Assert.Null(client.Cookie(CookieSessione.Nome));
        }

        [Fact]
        public async Task Scrittura_ConHeaderCsrf_Passa()
        {
            using var client = await Gestora.AccediComeAsync(Roles.Cliente);

            var risposta = await client.Http.PostAsync("/api/AuthenticationUser/rinnova-token", null);

            Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        }

        [Fact]
        public async Task Logout_ToglieIlCookie_ELaSessioneNonValePiu()
        {
            using var client = await Gestora.AccediComeAsync(Roles.Cliente);

            var uscita = await client.Http.PostAsync("/api/AuthenticationUser/logout", null);

            Assert.Equal(HttpStatusCode.NoContent, uscita.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.Http.GetAsync(Sessione)).StatusCode);
        }

        [Fact]
        public async Task CambioRuolo_InvalidaIlTokenGiaEmesso()
        {
            var email = "cambia-ruolo@prova.test";
            using var admin = await Gestora.AccediComeAsync(Roles.Admin);
            using var client = await Gestora.AccediComeAsync(Roles.Cliente, email);
            var utente = Gestora.ConContestoAsync(c => Task.FromResult(c.Users.Single(u => u.Email == email).Id));

            var assegnazione = await admin.Http.PostAsJsonAsync("/api/AuthenticationUser/assign-role",
                new { userId = await utente, role = Roles.Staff });

            Assert.Equal(HttpStatusCode.OK, assegnazione.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.Http.GetAsync(Sessione)).StatusCode);
        }
    }
}
