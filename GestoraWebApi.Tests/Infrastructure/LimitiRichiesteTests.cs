using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Threading.RateLimiting;
using GestoraWebApi.Auth;
using GestoraWebApi.Common;
using GestoraWebApi.Controllers;
using GestoraWebApi.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace GestoraWebApi.Tests.Infrastructure
{
    /// <summary>
    /// V2-011. Il rate limiter vero vive nella pipeline di ASP.NET e non si avvia in un test
    /// unitario: qui si prova la parte che decide (chi si conta, con quale regola, quante richieste
    /// passano), creando il limitatore di ogni partizione e consumandone i permessi a mano.
    /// La prova sulla pipeline completa resta manuale (Swagger, fino al 429).
    /// </summary>
    public class LimitiRichiesteTests
    {
        private static readonly LimitiRichiesteSettings Limiti = new()
        {
            RegistrazioniPerFinestra = 3,
            RegistrazioneFinestraMinuti = 60,
            ConfermaEmailPerFinestra = 2,
            ConfermaEmailFinestraMinuti = 60,
            DisponibilitaRaffica = 5,
            DisponibilitaAlMinuto = 30,
            PareriPerFinestra = 2,
            PareriFinestraGiorni = 30
        };

        private static HttpContext Richiesta(string ip = "79.30.166.123", string? userId = null, params string[] ruoli)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse(ip);

            if (userId is not null)
            {
                var claims = new List<Claim> { new("sub", userId) };
                claims.AddRange(ruoli.Select(r => new Claim(ClaimTypes.Role, r)));
                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
            }

            return context;
        }

        /// <summary>Quante richieste di fila passano, su un massimo di <paramref name="tentativi"/>.</summary>
        private static int RichiesteAccettate(RateLimitPartition<string> partizione, int tentativi)
        {
            using var limitatore = partizione.Factory(partizione.PartitionKey);
            var accettate = 0;
            for (var i = 0; i < tentativi; i++)
            {
                using var permesso = limitatore.AttemptAcquire();
                if (permesso.IsAcquired) accettate++;
            }
            return accettate;
        }

        // --- Registrazione ---

        [Fact]
        public void Registrazione_Anonimo_SiFermaAlLimiteDellaFinestra()
        {
            var partizione = LimitiRichieste.PartizioneRegistrazione(Richiesta(), Limiti);

            Assert.Equal(3, RichiesteAccettate(partizione, 10));
        }

        [Fact]
        public void Registrazione_SiContaPerIndirizzo()
        {
            var primo = LimitiRichieste.PartizioneRegistrazione(Richiesta("79.30.166.123"), Limiti);
            var secondo = LimitiRichieste.PartizioneRegistrazione(Richiesta("93.40.1.2"), Limiti);

            Assert.Equal("ip:79.30.166.123", primo.PartitionKey);
            Assert.NotEqual(primo.PartitionKey, secondo.PartitionKey);
        }

        [Fact]
        public void Registrazione_Admin_NonHaLimiti()
        {
            // L'Admin crea gli account dello Staff passando dallo stesso endpoint.
            var partizione = LimitiRichieste.PartizioneRegistrazione(
                Richiesta(userId: "admin-1", ruoli: Roles.Admin), Limiti);

            Assert.Equal(50, RichiesteAccettate(partizione, 50));
        }

        [Fact]
        public void Registrazione_UtenteNonAdmin_ContatoComeUnAnonimo()
        {
            var partizione = LimitiRichieste.PartizioneRegistrazione(
                Richiesta(userId: "staff-1", ruoli: Roles.Staff), Limiti);

            Assert.StartsWith("ip:", partizione.PartitionKey);
            Assert.Equal(3, RichiesteAccettate(partizione, 10));
        }

        // --- Conferma email e reinvio (V2-003) ---

        [Fact]
        public void ConfermaEmail_SiFermaAlLimiteDellaFinestra()
        {
            var partizione = LimitiRichieste.PartizioneConfermaEmail(Richiesta(), Limiti);

            Assert.Equal("ip:79.30.166.123", partizione.PartitionKey);
            Assert.Equal(2, RichiesteAccettate(partizione, 10));
        }

        // --- Pareri (V2-022) ---

        [Fact]
        public void Pareri_SiFermaADuePerIndirizzo()
        {
            var partizione = LimitiRichieste.PartizionePareri(Richiesta(), Limiti);

            Assert.Equal("ip:79.30.166.123", partizione.PartitionKey);
            Assert.Equal(2, RichiesteAccettate(partizione, 10));
        }

        // --- Verifica della disponibilità ---

        [Fact]
        public void Disponibilita_Anonimo_SiContaPerIndirizzo()
        {
            var partizione = LimitiRichieste.PartizioneDisponibilita(Richiesta(), Limiti);

            Assert.Equal("ip:79.30.166.123", partizione.PartitionKey);
        }

        [Fact]
        public void Disponibilita_DueColleghiSulloStessoWifi_NonSiConsumanoIGettoni()
        {
            // Lo Staff lavora tutto dallo stesso indirizzo del locale: contato per indirizzo, un
            // collega che compila il form esaurirebbe i gettoni dell'altro.
            var primo = LimitiRichieste.PartizioneDisponibilita(
                Richiesta(userId: "staff-1", ruoli: Roles.Staff), Limiti);
            var secondo = LimitiRichieste.PartizioneDisponibilita(
                Richiesta(userId: "staff-2", ruoli: Roles.Staff), Limiti);

            Assert.Equal("utente:staff-1", primo.PartitionKey);
            Assert.NotEqual(primo.PartitionKey, secondo.PartitionKey);
        }

        [Fact]
        public void Disponibilita_SiFermaQuandoFinisceLaRaffica()
        {
            var partizione = LimitiRichieste.PartizioneDisponibilita(Richiesta(), Limiti);

            Assert.Equal(5, RichiesteAccettate(partizione, 20));
        }

        // --- I due endpoint usano davvero le regole ---

        [Theory]
        [InlineData(typeof(AuthenticationUserController), nameof(AuthenticationUserController.Register), LimitiRichieste.Registrazione)]
        [InlineData(typeof(AuthenticationUserController), nameof(AuthenticationUserController.ConfermaEmail), LimitiRichieste.ConfermaEmail)]
        [InlineData(typeof(AuthenticationUserController), nameof(AuthenticationUserController.ReinviaConferma), LimitiRichieste.ConfermaEmail)]
        [InlineData(typeof(PrenotazioneController), nameof(PrenotazioneController.CheckDisponibilita), LimitiRichieste.Disponibilita)]
        [InlineData(typeof(PareriController), nameof(PareriController.Invia), LimitiRichieste.Pareri)]
        public void Endpoint_HaLaSuaRegola(Type controller, string metodo, string regola)
        {
            var attributo = controller.GetMethod(metodo)!.GetCustomAttribute<EnableRateLimitingAttribute>();

            Assert.NotNull(attributo);
            Assert.Equal(regola, attributo.PolicyName);
        }
    }
}
