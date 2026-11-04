using System.Net;
using System.Net.Http.Json;
using GestoraWebApi.Auth;
using GestoraWebApi.IntegrationTests.Infrastruttura;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GestoraWebApi.IntegrationTests
{
    /// <summary>
    /// La matrice ruoli × funzioni: ogni endpoint chiamato da ogni ruolo e senza sessione. La
    /// tabella è scritta a mano apposta: se un endpoint nuovo non c'è, il primo test fallisce.
    /// </summary>
    public class RuoliTests(GestoraFixture gestora) : TestIntegrazione(gestora)
    {
        private static readonly string[] Pubblico = [];
        private static readonly string[] Tutti = [Roles.Admin, Roles.Staff, Roles.Cliente];
        private static readonly string[] Gestione = [Roles.Admin, Roles.Staff];
        private static readonly string[] SoloAdmin = [Roles.Admin];
        private static readonly string[] SoloCliente = [Roles.Cliente];

        private static readonly (string Metodo, string Percorso, string[] Ruoli)[] Matrice =
        [
            ("GET", "/api/Account/mio-account", Tutti),
            ("POST", "/api/Account/cancella-account", Tutti),
            ("POST", "/api/Account/cambia-password", Tutti),

            ("POST", "/api/AuthenticationUser/register", Pubblico),
            ("POST", "/api/AuthenticationUser/conferma-email", Pubblico),
            ("POST", "/api/AuthenticationUser/reinvia-conferma", Pubblico),
            ("POST", "/api/AuthenticationUser/password-dimenticata", Pubblico),
            ("POST", "/api/AuthenticationUser/reimposta-password", Pubblico),
            ("POST", "/api/AuthenticationUser/login", Pubblico),
            ("POST", "/api/AuthenticationUser/logout", Pubblico),
            ("GET", "/api/AuthenticationUser/sessione", Tutti),
            ("POST", "/api/AuthenticationUser/rinnova-token", Tutti),
            ("GET", "/api/AuthenticationUser/stato-privacy", Tutti),
            ("POST", "/api/AuthenticationUser/accetta-privacy", Tutti),
            ("POST", "/api/AuthenticationUser/assign-role", SoloAdmin),
            ("DELETE", "/api/AuthenticationUser/remove-role", SoloAdmin),
            ("GET", "/api/AuthenticationUser/get-users", SoloAdmin),
            ("GET", "/api/AuthenticationUser/get-user/{id}", SoloAdmin),
            ("PUT", "/api/AuthenticationUser/update-user/{id}", SoloAdmin),
            ("DELETE", "/api/AuthenticationUser/delete-user/{id}", SoloAdmin),
            ("POST", "/api/AuthenticationUser/reset-password/{id}", SoloAdmin),

            ("GET", "/api/Dashboard/giornaliera", Gestione),
            ("GET", "/api/Dashboard/settimanale", Gestione),

            ("POST", "/api/FasceOrarie/crea-fascia", SoloAdmin),
            ("GET", "/api/FasceOrarie/get-fascia-id/{id}", Gestione),
            ("GET", "/api/FasceOrarie/fasce-attive", Tutti),
            ("GET", "/api/FasceOrarie/fasce-per-giorno", Tutti),
            ("GET", "/api/FasceOrarie/fasce-disponibili", Tutti),
            ("PUT", "/api/FasceOrarie/update-fascia", SoloAdmin),
            ("DELETE", "/api/FasceOrarie/delete-fascia", SoloAdmin),
            ("GET", "/api/FasceOrarie/get-all-fasce", Gestione),
            ("PATCH", "/api/FasceOrarie/update-stato/{id}", SoloAdmin),

            ("POST", "/api/Jobs/trigger/{jobName}", SoloAdmin),

            ("GET", "/api/LogActivity/get-log", SoloAdmin),
            ("GET", "/api/LogActivity/diagnostica-inoltro", SoloAdmin),

            ("POST", "/api/Pareri/invia", Pubblico),

            ("POST", "/api/Postazione/crea-postazione", SoloAdmin),
            ("GET", "/api/Postazione/get-postazione-id", Gestione),
            ("GET", "/api/Postazione/get-postazioni-attive", Tutti),
            ("GET", "/api/Postazione/riepilogo-sala", Gestione),
            ("GET", "/api/Postazione/get-postazioni-disponibili", Tutti),
            ("GET", "/api/Postazione/get-postazioni-per-zona", Tutti),
            ("GET", "/api/Postazione/get-tavoli-zona-gestione", Gestione),
            ("PUT", "/api/Postazione/update-postazione", SoloAdmin),
            ("PUT", "/api/Postazione/associa-postazione-a-zona", SoloAdmin),
            ("DELETE", "/api/Postazione/delete-postazione", SoloAdmin),

            ("GET", "/api/Prenotazione/report-csv", SoloAdmin),
            ("POST", "/api/Prenotazione/crea-prenotazione", Tutti),
            ("GET", "/api/Prenotazione/get-prenotazione", Tutti),
            ("GET", "/api/Prenotazione/get-all-prenotazioni", Gestione),
            ("GET", "/api/Prenotazione/get-prenotazioni-by-data", Gestione),
            ("GET", "/api/Prenotazione/get-mie-prenotazioni", SoloCliente),
            ("PUT", "/api/Prenotazione/update-prenotazione", Tutti),
            ("DELETE", "/api/Prenotazione/delete-prenotazione", SoloAdmin),
            ("PATCH", "/api/Prenotazione/conferma-prenotazione", Gestione),
            ("PATCH", "/api/Prenotazione/completa-prenotazione", Gestione),
            ("PATCH", "/api/Prenotazione/annulla-prenotazione", Tutti),
            ("POST", "/api/Prenotazione/check-disponibilita", Pubblico),
            ("GET", "/api/Prenotazione/limiti-prenotazione", Pubblico),

            ("GET", "/api/Setup/stato", Pubblico),
            ("POST", "/api/Setup/admin", Pubblico),

            ("GET", "/api/Zona/get-zona/{id}", Gestione),
            ("POST", "/api/Zona/crea-zona", SoloAdmin),
            ("PUT", "/api/Zona/update-zona", SoloAdmin),
            ("GET", "/api/Zona/get-zone-attive", Tutti),
            ("GET", "/api/Zona/get-all-zone", Gestione),
            ("GET", "/api/Zona/is-zona-usata", Gestione),
            ("PATCH", "/api/Zona/update-stato/{id}", SoloAdmin),
            ("DELETE", "/api/Zona/delete-zona/{id}", SoloAdmin)
        ];

        [Fact]
        public void LaMatrice_ContieneTuttiGliEndpointDellApi()
        {
            var sorgente = Gestora.Fabbrica.Services.GetRequiredService<EndpointDataSource>();
            var endpoint = sorgente.Endpoints.OfType<RouteEndpoint>()
                .Where(e => e.RoutePattern.RawText?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true)
                .SelectMany(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods
                    .Select(metodo => $"{metodo} /{e.RoutePattern.RawText}"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var inMatrice = Matrice.Select(r => $"{r.Metodo} {r.Percorso}").ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.Empty(endpoint.Except(inMatrice, StringComparer.OrdinalIgnoreCase));
            Assert.Empty(inMatrice.Except(endpoint, StringComparer.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task OgniEndpoint_RispondeComeDiceLaMatrice()
        {
            using var anonimo = Gestora.NuovoClient();
            var clientPerRuolo = new Dictionary<string, ClientGestora>
            {
                [Roles.Admin] = await Gestora.AccediComeAsync(Roles.Admin),
                [Roles.Staff] = await Gestora.AccediComeAsync(Roles.Staff),
                [Roles.Cliente] = await Gestora.AccediComeAsync(Roles.Cliente)
            };

            var errori = new List<string>();

            foreach (var (metodo, percorso, ruoli) in Matrice)
            {
                var stato = await ChiamaAsync(anonimo, metodo, percorso);
                var attesoAnonimo = ruoli.Length == 0 ? "non 401/403" : "401";

                if (ruoli.Length == 0 ? Rifiutato(stato) : stato != HttpStatusCode.Unauthorized)
                    errori.Add($"{metodo} {percorso} senza sessione: {(int)stato}, atteso {attesoAnonimo}");

                // Gli endpoint pubblici non dipendono dal ruolo, e logout toglierebbe il cookie ai client.
                if (ruoli.Length == 0)
                    continue;

                foreach (var (ruolo, client) in clientPerRuolo)
                {
                    stato = await ChiamaAsync(client, metodo, percorso);

                    if (ruoli.Contains(ruolo) ? Rifiutato(stato) : stato != HttpStatusCode.Forbidden)
                        errori.Add($"{metodo} {percorso} come {ruolo}: {(int)stato}");
                }
            }

            Assert.True(errori.Count == 0, string.Join(Environment.NewLine, errori));

            foreach (var client in clientPerRuolo.Values)
                client.Dispose();
        }

        private static bool Rifiutato(HttpStatusCode stato)
            => stato is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

        /// <summary>
        /// Chiamata con dati finti: a un ruolo ammesso l'API risponde 400 o 404, l'importante è che
        /// non risponda 401 o 403. I permessi si controllano prima di leggere i dati.
        /// </summary>
        private static async Task<HttpStatusCode> ChiamaAsync(ClientGestora client, string metodo, string percorso)
        {
            var indirizzo = percorso.Replace("{id}", "999999").Replace("{jobName}", "JobCheNonEsiste");
            using var richiesta = new HttpRequestMessage(new HttpMethod(metodo), indirizzo);

            if (metodo != "GET")
                richiesta.Content = JsonContent.Create(new { });

            var risposta = await client.Http.SendAsync(richiesta);
            return risposta.StatusCode;
        }
    }
}
