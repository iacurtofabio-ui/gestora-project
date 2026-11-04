using GestoraWebApi.Common;
using GestoraWebApi.Services.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GestoraWebApi.IntegrationTests.Infrastruttura
{
    /// <summary>
    /// Accende l'API vera, con il suo Program.cs, in memoria. Si sostituiscono solo i pezzi esterni:
    /// lo scheduler dei job (le tabelle Quartz non stanno nelle migration, i job si chiamano a
    /// mano), l'invio delle email e l'orologio.
    /// </summary>
    public sealed class FabbricaGestora : WebApplicationFactory<Program>
    {
        /// <summary>Stessa parola che il middleware di Vercel aggiunge in produzione.</summary>
        public const string SegretoInoltro = "parola-segreta-solo-per-i-test-di-integrazione";

        public PostaInMemoria Posta { get; } = new();

        public OrologioDiProva Orologio { get; } = new();

        public FabbricaGestora(string stringaDiConnessione)
        {
            // Program.cs legge la configurazione prima che la fabbrica possa intervenire: le
            // variabili d'ambiente sono la strada che arriva in tempo. L'ambiente non è
            // Development apposta, così non si caricano gli user secrets con le chiavi vere.
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", stringaDiConnessione);
            Environment.SetEnvironmentVariable("JwtSettings__Secret", "segreto-jwt-solo-per-i-test-di-integrazione");
            Environment.SetEnvironmentVariable("InoltroVercel__Segreto", SegretoInoltro);
            Environment.SetEnvironmentVariable("Email__Host", "smtp.non-usato.test");
            Environment.SetEnvironmentVariable("Email__MittenteEmail", "gestora@prova.test");
            Environment.SetEnvironmentVariable("Email__UrlFrontend", "http://localhost:5173");
            Environment.SetEnvironmentVariable("Pareri__Destinatario", "pareri@prova.test");
            Environment.SetEnvironmentVariable("Serilog__MinimumLevel__Default", "Warning");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Integrazione");

            builder.ConfigureTestServices(services =>
            {
                var scheduler = services.Where(s => s.ServiceType == typeof(IHostedService)
                                                    && s.ImplementationType?.Name == "QuartzHostedService").ToList();
                foreach (var servizio in scheduler)
                    services.Remove(servizio);

                services.AddSingleton<IClock>(Orologio);
                services.AddSingleton<IInvioEmail>(Posta);
            });
        }
    }
}
