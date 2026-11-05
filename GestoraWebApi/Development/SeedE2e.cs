using GestoraWebApi.Auth;
using GestoraWebApi.Common;
using GestoraWebApi.Context;
using GestoraWebApi.Enums;
using GestoraWebApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestoraWebApi.Development
{
    /// <summary>
    /// Dati noti per le prove nel browser con Playwright (V3-001-T03). Lo lancia da solo
    /// gestora-frontend/e2e/avvia-backend.mjs prima di ogni giro.
    ///
    /// Le protezioni sono quelle di SeedSviluppo, più una: parte solo sul database gestora_e2e,
    /// perché qui si cancellano anche gli utenti.
    ///
    /// Ogni giorno ha due fasce: «Giornata» copre tutto il giorno, così si prenota a qualsiasi ora;
    /// la prima dura un minuto ed è già finita, così una prenotazione di oggi si può completare.
    /// Fra dieci giorni la Giornata è piena, per il caso «esaurita» della pagina pubblica.
    /// </summary>
    public static class SeedE2e
    {
        public const string ArgomentoSeed = "--seed-e2e";

        private const string NomeDatabase = "gestora_e2e";
        private const string StaffEmail = "staff.e2e@gestora.local";
        private const int TettoGiornata = 70;
        private const int GiorniAllaFasciaPiena = 10;

        private static readonly string[] Browser = { "chromium", "firefox" };

        private static readonly (string Zona, int[] Capienze)[] Sala =
        {
            ("Sala interna", new[] { 2, 2, 2, 2, 2, 2, 4, 4, 4, 4, 4, 4, 6, 6 }),
            ("Dehors", new[] { 2, 2, 2, 2, 4, 4, 4, 4 }),
        };

        public static async Task EseguiAsync(IServiceProvider servizi, ILogger logger)
        {
            using var scope = servizi.CreateScope();
            var configurazione = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var stringa = configurazione.GetConnectionString("DefaultConnection");
            SeedSviluppo.VerificaCheSiaLocale(stringa);

            if (new NpgsqlConnectionStringBuilder(stringa).Database != NomeDatabase)
                throw new InvalidOperationException($"Il seed delle prove parte solo sul database {NomeDatabase}.");

            var db = scope.ServiceProvider.GetRequiredService<GestoraContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var ambiente = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

            // Il backend delle prove gira con lo scheduler acceso, e le tabelle Quartz non stanno
            // nelle migration. Lo script le cancella e le ricrea, quindi si può ripetere.
            var scriptQuartz = Path.Combine(ambiente.ContentRootPath, "Scripts", "quartz_postgres.sql");
            await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(scriptQuartz));

            db.PrenotazioniPostazioni.RemoveRange(db.PrenotazioniPostazioni);
            db.Prenotazioni.RemoveRange(db.Prenotazioni);
            db.Postazioni.RemoveRange(db.Postazioni);
            db.Zone.RemoveRange(db.Zone);
            db.FasciaOrarie.RemoveRange(db.FasciaOrarie);
            db.LogActivities.RemoveRange(db.LogActivities);
            db.Users.RemoveRange(db.Users);
            await db.SaveChangesAsync();

            var versionePrivacy = configurazione["Privacy:Versione"];
            var admin = await SeedVetrina.PreparaUtenteAsync(userManager, "admin", SeedSviluppo.AdminEmail, Roles.Admin, versionePrivacy, clock);
            await SeedVetrina.PreparaUtenteAsync(userManager, "Staff E2E", StaffEmail, Roles.Staff, versionePrivacy, clock);

            var tavoli = await CreaSalaAsync(db);
            var fasce = await CreaFasceAsync(db);
            await CreaPrenotazioniAsync(db, tavoli, fasce, admin.Id, clock);

            logger.LogInformation("Seed e2e completato: {Tavoli} tavoli, {Fasce} fasce.", tavoli.Count, fasce.Count);
        }

        private static async Task<List<Postazione>> CreaSalaAsync(GestoraContext db)
        {
            var tavoli = new List<Postazione>();
            var numero = 1;

            foreach (var (nomeZona, capienze) in Sala)
            {
                var zona = new Zona { Nome = nomeZona, Attiva = true };
                zona.Postazioni = capienze
                    .Select(capienza => new Postazione { Numero = numero++, CapienzaMassima = capienza, Attiva = true })
                    .ToList();

                db.Zone.Add(zona);
                tavoli.AddRange(zona.Postazioni);
            }

            await db.SaveChangesAsync();
            return tavoli;
        }

        private static async Task<List<FasciaOraria>> CreaFasceAsync(GestoraContext db)
        {
            var fasce = new List<FasciaOraria>();

            foreach (var giorno in Enum.GetValues<DayOfWeek>())
            {
                fasce.Add(new FasciaOraria
                {
                    OrarioInizio = new TimeOnly(0, 0),
                    OrarioFine = new TimeOnly(0, 1),
                    GiornoSettimana = giorno,
                    MaxCoperti = 10,
                    Attiva = true
                });
                fasce.Add(new FasciaOraria
                {
                    OrarioInizio = new TimeOnly(0, 5),
                    OrarioFine = new TimeOnly(23, 59),
                    GiornoSettimana = giorno,
                    MaxCoperti = TettoGiornata,
                    Attiva = true
                });
            }

            db.FasciaOrarie.AddRange(fasce);
            await db.SaveChangesAsync();
            return fasce;
        }

        private static async Task CreaPrenotazioniAsync(
            GestoraContext db, List<Postazione> tavoli, List<FasciaOraria> fasce, string adminId, IClock clock)
        {
            var oggi = clock.TodayInRome;
            var finitaOggi = fasce.First(f => f.GiornoSettimana == oggi.DayOfWeek && f.MaxCoperti != TettoGiornata);

            // Una per browser: i due giri lavorano sullo stesso database, uno dopo l'altro.
            foreach (var (browser, indice) in Browser.Select((b, i) => (b, i)))
            {
                var prenotazione = new Prenotazione
                {
                    DataPrenotazione = oggi,
                    NumeroCoperti = 2,
                    Stato = StatoPrenotazione.InCorso,
                    NomeCliente = $"Da completare {browser}",
                    UserId = adminId,
                    FasciaOrariaId = finitaOggi.Id
                };
                prenotazione.PrenotazioniPostazioni = new List<PrenotazionePostazione>
                {
                    new() { PostazioneId = tavoli[indice].Id, NumeroPosti = 2, DataPrenotazione = oggi, FasciaOrariaId = finitaOggi.Id }
                };
                db.Prenotazioni.Add(prenotazione);
            }

            var giornoPieno = oggi.AddDays(GiorniAllaFasciaPiena);
            var fasciaPiena = fasce.First(f => f.GiornoSettimana == giornoPieno.DayOfWeek && f.MaxCoperti == TettoGiornata);

            db.Prenotazioni.Add(new Prenotazione
            {
                DataPrenotazione = giornoPieno,
                NumeroCoperti = TettoGiornata,
                Stato = StatoPrenotazione.Attiva,
                NomeCliente = "Sala riservata",
                UserId = adminId,
                FasciaOrariaId = fasciaPiena.Id
            });

            await db.SaveChangesAsync();
        }
    }
}
