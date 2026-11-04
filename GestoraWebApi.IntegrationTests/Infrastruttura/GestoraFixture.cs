using System.Net;
using System.Net.Http.Json;
using GestoraWebApi.Auth;
using GestoraWebApi.Context;
using GestoraWebApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Respawn.Graph;

namespace GestoraWebApi.IntegrationTests.Infrastruttura
{
    /// <summary>L'API accesa una volta sola per tutti i test, con il database pulito prima di ognuno.</summary>
    public sealed class GestoraFixture : IAsyncLifetime
    {
        public const string Password = "ProvaGestora1!";

        private string _stringaDiConnessione = string.Empty;
        private Respawner _pulizia = null!;

        public FabbricaGestora Fabbrica { get; private set; } = null!;

        public PostaInMemoria Posta => Fabbrica.Posta;

        public OrologioDiProva Orologio => Fabbrica.Orologio;

        public async Task InitializeAsync()
        {
            _stringaDiConnessione = DatabaseDiProva.StringaDiConnessione();
            await DatabaseDiProva.CreaSeMancaAsync(_stringaDiConnessione);

            // Prima le migration, poi l'API: all'avvio il seed dei ruoli vuole già le tabelle.
            var opzioni = new DbContextOptionsBuilder<GestoraContext>().UseNpgsql(_stringaDiConnessione).Options;
            await using (var contesto = new GestoraContext(opzioni))
                await contesto.Database.MigrateAsync();

            Fabbrica = new FabbricaGestora(_stringaDiConnessione);
            _ = Fabbrica.Services;

            await using var connessione = new NpgsqlConnection(_stringaDiConnessione);
            await connessione.OpenAsync();
            _pulizia = await Respawner.CreateAsync(connessione, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                // I ruoli li crea l'avvio dell'API e le chiavi di Data Protection stanno anche in memoria.
                TablesToIgnore = [new Table("__EFMigrationsHistory"), new Table("Ruoli"), new Table("DataProtectionKeys")]
            });
        }

        public async Task DisposeAsync() => await Fabbrica.DisposeAsync();

        /// <summary>Riporta tutto all'inizio: tabelle, cache delle fasce, posta e orologio.</summary>
        public async Task AzzeraAsync()
        {
            await using var connessione = new NpgsqlConnection(_stringaDiConnessione);
            await connessione.OpenAsync();
            await _pulizia.ResetAsync(connessione);

            ((MemoryCache)Fabbrica.Services.GetRequiredService<IMemoryCache>()).Clear();
            Posta.Svuota();
            Orologio.Spostamento = TimeSpan.Zero;
        }

        public ClientGestora NuovoClient() => new(Fabbrica);

        /// <summary>Un utente con l'email già confermata, creato senza passare dall'API.</summary>
        public async Task<ApplicationUser> CreaUtenteAsync(string email, params string[] ruoli)
        {
            using var scope = Fabbrica.Services.CreateScope();
            var utenti = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var utente = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DataCreazione = Orologio.UtcNow
            };

            var creazione = await utenti.CreateAsync(utente, Password);
            Assert.True(creazione.Succeeded, string.Join(", ", creazione.Errors.Select(e => e.Description)));

            foreach (var ruolo in ruoli)
                await utenti.AddToRoleAsync(utente, ruolo);

            return utente;
        }

        /// <summary>Un client già entrato con un utente nuovo del ruolo indicato.</summary>
        public async Task<ClientGestora> AccediComeAsync(string ruolo, string? email = null)
        {
            email ??= $"{ruolo.ToLowerInvariant()}-{Guid.NewGuid():N}@prova.test";
            await CreaUtenteAsync(email, ruolo);

            var client = NuovoClient();
            var risposta = await client.AccediAsync(email, Password);
            risposta.EnsureSuccessStatusCode();
            return client;
        }

        /// <summary>
        /// Una zona con i tavoli delle capienze indicate e una fascia 19:00-22:00 nel giorno dato,
        /// con il tetto pari ai posti della sala se non se ne indica uno.
        /// </summary>
        public Task<SalaDiProva> CreaSalaAsync(DateOnly giorno, int[] capienze, int? maxCoperti = null)
            => ConContestoAsync(async contesto =>
            {
                var zona = new Zona { Nome = $"Sala {Guid.NewGuid():N}" };
                zona.Postazioni = capienze
                    .Select((capienza, indice) => new Postazione { Numero = indice + 1, CapienzaMassima = capienza })
                    .ToList();

                var fascia = new FasciaOraria
                {
                    OrarioInizio = new TimeOnly(19, 0),
                    OrarioFine = new TimeOnly(22, 0),
                    GiornoSettimana = giorno.DayOfWeek,
                    MaxCoperti = maxCoperti ?? capienze.Sum(),
                    Attiva = true
                };

                contesto.Add(zona);
                contesto.Add(fascia);
                await contesto.SaveChangesAsync();

                return new SalaDiProva(zona.Id, fascia.Id, giorno);
            });

        /// <summary>Prenota dall'API e restituisce l'Id della prenotazione appena creata.</summary>
        public async Task<long> PrenotaAsync(ClientGestora client, SalaDiProva sala, int coperti, string? nomeCliente = null)
        {
            var risposta = await client.Http.PostAsJsonAsync("/api/Prenotazione/crea-prenotazione", new
            {
                dataPrenotazione = sala.Giorno,
                numeroCoperti = coperti,
                fasciaOrariaId = sala.FasciaId,
                nomeCliente
            });

            Assert.Equal(HttpStatusCode.Created, risposta.StatusCode);
            return await ConContestoAsync(c => c.Prenotazioni.MaxAsync(p => p.Id));
        }

        public async Task<T> ConContestoAsync<T>(Func<GestoraContext, Task<T>> azione)
        {
            using var scope = Fabbrica.Services.CreateScope();
            return await azione(scope.ServiceProvider.GetRequiredService<GestoraContext>());
        }

        /// <summary>Un giorno abbastanza lontano da non toccare nessun limite di orario.</summary>
        public DateOnly GiornoFuturo(int giorniAvanti = 7) => Orologio.TodayInRome.AddDays(giorniAvanti);
    }

    public sealed record SalaDiProva(long ZonaId, long FasciaId, DateOnly Giorno);

    [CollectionDefinition(Nome)]
    public sealed class CollezioneGestora : ICollectionFixture<GestoraFixture>
    {
        public const string Nome = "Gestora";
    }

    /// <summary>Base dei test: stessa API per tutti, un test alla volta, database pulito prima di ognuno.</summary>
    [Collection(CollezioneGestora.Nome)]
    public abstract class TestIntegrazione(GestoraFixture gestora) : IAsyncLifetime
    {
        protected GestoraFixture Gestora { get; } = gestora;

        public Task InitializeAsync() => Gestora.AzzeraAsync();

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
