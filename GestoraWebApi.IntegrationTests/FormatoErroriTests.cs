using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GestoraWebApi.Auth;
using GestoraWebApi.IntegrationTests.Infrastruttura;

namespace GestoraWebApi.IntegrationTests
{
    /// <summary>
    /// Il frontend legge il motivo di un errore da <c>message</c>: una risposta con una stringa
    /// semplice lo perde, e l'utente vede solo una frase generica. Qui si controllano gli errori
    /// scritti a mano nei controller, che non passano dal gestore delle eccezioni.
    /// </summary>
    public class FormatoErroriTests(GestoraFixture gestora) : TestIntegrazione(gestora)
    {
        private const string Admin = "admin@prova.test";

        private static async Task VerificaAsync(HttpResponseMessage risposta, HttpStatusCode stato, string testo)
        {
            Assert.Equal(stato, risposta.StatusCode);

            using var corpo = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
            Assert.Equal((int)stato, corpo.RootElement.GetProperty("statusCode").GetInt32());
            Assert.Contains(testo, corpo.RootElement.GetProperty("message").GetString());
        }

        [Fact]
        public async Task PasswordSbagliata_HaIlFormatoComune()
        {
            await Gestora.CreaUtenteAsync("cliente@prova.test", Roles.Cliente);
            using var client = Gestora.NuovoClient();

            var risposta = await client.AccediAsync("cliente@prova.test", "PasswordSbagliata1!");

            await VerificaAsync(risposta, HttpStatusCode.Unauthorized, "Credenziali non valide");
        }

        [Fact]
        public async Task LinkDiConfermaNonValido_DiceIlMotivo()
        {
            using var client = Gestora.NuovoClient();

            var risposta = await client.Http.PostAsJsonAsync("/api/AuthenticationUser/conferma-email",
                new { userId = "inesistente", token = "dG9rZW4tZmFsc28" });

            await VerificaAsync(risposta, HttpStatusCode.BadRequest, "non è valido o è scaduto");
        }

        [Fact]
        public async Task LinkDiRecuperoNonValido_DiceIlMotivo()
        {
            var utente = await Gestora.CreaUtenteAsync("cliente@prova.test", Roles.Cliente);
            using var client = Gestora.NuovoClient();

            var risposta = await client.Http.PostAsJsonAsync("/api/AuthenticationUser/reimposta-password",
                new { userId = utente.Id, token = "dG9rZW4tZmFsc28", nuovaPassword = GestoraFixture.Password });

            await VerificaAsync(risposta, HttpStatusCode.BadRequest, "non è valido o è scaduto");
        }

        [Fact]
        public async Task RuoloGiaAssegnato_DiceIlMotivo()
        {
            var utente = await Gestora.CreaUtenteAsync("cliente@prova.test", Roles.Cliente);
            using var admin = await Gestora.AccediComeAsync(Roles.Admin, Admin);

            var risposta = await admin.Http.PostAsJsonAsync("/api/AuthenticationUser/assign-role",
                new { userId = utente.Id, role = Roles.Cliente });

            await VerificaAsync(risposta, HttpStatusCode.BadRequest, "già nel ruolo");
        }

        [Fact]
        public async Task UtenteInesistente_DiceIlMotivo()
        {
            using var admin = await Gestora.AccediComeAsync(Roles.Admin, Admin);

            var risposta = await admin.Http.GetAsync("/api/AuthenticationUser/get-user/inesistente");

            await VerificaAsync(risposta, HttpStatusCode.NotFound, "non trovato");
        }

        [Fact]
        public async Task AdminCheEliminaSeStesso_DiceIlMotivo()
        {
            using var admin = await Gestora.AccediComeAsync(Roles.Admin, Admin);
            var id = await Gestora.ConContestoAsync(c => Task.FromResult(c.Users.Single(u => u.Email == Admin).Id));

            var risposta = await admin.Http.DeleteAsync($"/api/AuthenticationUser/delete-user/{id}");

            await VerificaAsync(risposta, HttpStatusCode.BadRequest, "proprio account");
        }

        [Fact]
        public async Task PrimoAvvioGiaFatto_DiceIlMotivo()
        {
            await Gestora.CreaUtenteAsync(Admin, Roles.Admin);
            using var client = Gestora.NuovoClient();

            var risposta = await client.Http.PostAsJsonAsync("/api/Setup/admin",
                new { username = "altro", email = "altro@prova.test", password = GestoraFixture.Password });

            await VerificaAsync(risposta, HttpStatusCode.Conflict, "configurata");
        }
    }
}
