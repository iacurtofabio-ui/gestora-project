using GestoraWebApi.Auth;
using GestoraWebApi.Common;
using GestoraWebApi.Context;
using GestoraWebApi.Enums;
using GestoraWebApi.Models;
using GestoraWebApi.Services.PostazioneAssignment;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GestoraWebApi.Development
{
    /// <summary>
    /// Popolamento del database LOCALE con un locale credibile, per screenshot e dimostrazioni.
    ///
    /// PERCHE' ESISTE
    /// SeedSviluppo e' fatto per rompere l'interfaccia (nomi lunghissimi, 14 fasce in un giorno,
    /// utenti senza ruoli): ottimo per provarla, inadatto a mostrarla. Questo seed descrive una
    /// pizzeria normale: tre zone, pranzo e cena, clienti con nomi veri, una settimana di storico.
    /// Nato con V2-023 (vetrina della v3.0.0).
    ///
    /// NON E' UN SEED DI PRODUZIONE. Cancella i dati di dominio prima di riscriverli, con le stesse
    /// tre protezioni di SeedSviluppo: solo Development, solo con l'argomento esplicito, solo su un
    /// database di questa macchina.
    ///
    /// I tavoli si assegnano con il motore vero (AssegnazioneTavoli), dentro il tetto di ogni fascia:
    /// le unioni che si vedono sono quelle che Gestora sceglierebbe davvero.
    ///
    /// Uso (dalla cartella GestoraWebApi):
    ///   dotnet run -- --seed-vetrina
    /// Accesso: le stesse credenziali dell'Admin di SeedSviluppo.
    /// </summary>
    public static class SeedVetrina
    {
        public const string ArgomentoSeed = "--seed-vetrina";

        private const string StaffEmail = "giulia.ferri@gestora.local";

        // Le prime tre coincidono con le zone presentate in LandingPage.tsx.
        private static readonly (string Zona, int[] Capienze)[] Sala =
        {
            ("Sala interna", new[] { 2, 2, 2, 2, 4, 4, 4, 6 }),
            ("Dehors", new[] { 2, 2, 4, 4, 4 }),
            ("Sala privata", new[] { 4, 6, 8 }),
        };

        private const int TettoPranzo = 40;
        private const int TettoCena = 56;

        private static readonly string[] Clienti =
        {
            "Rossi", "Bianchi", "Esposito", "Romano", "Colombo", "Ricci", "Marino", "Greco",
            "Bruno", "Gallo", "Conti", "De Luca", "Mancini", "Costa", "Giordano", "Rizzo",
            "Lombardi", "Moretti", "Barbieri", "Fontana", "Santoro", "Mariani", "Rinaldi", "Caruso",
            "Ferrara", "Galli", "Martini", "Leone", "Longo", "Gentile", "Martinelli", "Vitale",
        };

        private static readonly string?[] Note =
        {
            // Nessun dato sulla salute (allergie, intolleranze): Gestora non li raccoglie (V2-026).
            "Compleanno: candelina sul dolce", "Un seggiolone", "Anniversario",
            "Tavolo vicino alla finestra, se possibile", "Arrivano 15 minuti dopo",
        };

        // Gruppi tipici di una pizzeria: tante coppie, qualche famiglia, poche tavolate.
        private static readonly int[] Gruppi = { 2, 4, 2, 3, 6, 2, 5, 4, 2, 8, 3, 4, 2, 7, 4, 2, 10, 3 };

        public static async Task EseguiAsync(IServiceProvider servizi, ILogger logger)
        {
            using var scope = servizi.CreateScope();
            var configurazione = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            SeedSviluppo.VerificaCheSiaLocale(configurazione.GetConnectionString("DefaultConnection"));

            var db = scope.ServiceProvider.GetRequiredService<GestoraContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            logger.LogInformation("Seed vetrina: cancello i dati di dominio esistenti.");

            db.PrenotazioniPostazioni.RemoveRange(db.PrenotazioniPostazioni);
            db.Prenotazioni.RemoveRange(db.Prenotazioni);
            db.Postazioni.RemoveRange(db.Postazioni);
            db.Zone.RemoveRange(db.Zone);
            db.FasciaOrarie.RemoveRange(db.FasciaOrarie);
            await db.SaveChangesAsync();

            var versionePrivacy = configurazione["Privacy:Versione"];
            var admin = await PreparaUtenteAsync(userManager, "admin", SeedSviluppo.AdminEmail, Roles.Admin, versionePrivacy, clock);
            var staff = await PreparaUtenteAsync(userManager, "Giulia Ferri", StaffEmail, Roles.Staff, versionePrivacy, clock);

            var tavoli = await CreaSalaAsync(db);
            var fasce = await CreaFasceAsync(db);
            var create = await CreaPrenotazioniAsync(db, tavoli, fasce, new[] { admin.Id, staff.Id }, clock);

            logger.LogInformation(
                "Seed vetrina completato: {Tavoli} tavoli, {Fasce} fasce, {Prenotazioni} prenotazioni. Accesso: {Email} / {Password}",
                tavoli.Count, fasce.Count, create, SeedSviluppo.AdminEmail, SeedSviluppo.AdminPassword);
        }

        /// <summary>
        /// Crea l'utente o lo riallinea (password, ruolo unico). L'informativa risulta gia' vista,
        /// cosi' la finestra di presa visione non copre le schermate.
        /// </summary>
        private static async Task<ApplicationUser> PreparaUtenteAsync(
            UserManager<ApplicationUser> userManager, string nome, string email, string ruolo,
            string? versionePrivacy, IClock clock)
        {
            var utente = await userManager.FindByEmailAsync(email);
            if (utente is null)
            {
                utente = new ApplicationUser
                {
                    UserName = nome,
                    Email = email,
                    EmailConfirmed = true,
                    DataCreazione = clock.UtcNow
                };
                var esito = await userManager.CreateAsync(utente, SeedSviluppo.AdminPassword);
                if (!esito.Succeeded)
                    throw new InvalidOperationException(
                        $"Utente {email} non creato: {string.Join(" | ", esito.Errors.Select(e => e.Description))}");
            }
            else
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(utente);
                await userManager.ResetPasswordAsync(utente, token, SeedSviluppo.AdminPassword);
            }

            await userManager.RemoveFromRolesAsync(utente, await userManager.GetRolesAsync(utente));
            await userManager.AddToRoleAsync(utente, ruolo);

            utente.PrivacyAccettataIl = clock.UtcNow;
            utente.PrivacyVersione = versionePrivacy;
            await userManager.UpdateAsync(utente);

            return utente;
        }

        private static async Task<List<Postazione>> CreaSalaAsync(GestoraContext db)
        {
            var tavoli = new List<Postazione>();
            var numero = 1;

            foreach (var (nomeZona, capienze) in Sala)
            {
                var zona = new Zona { Nome = nomeZona, Attiva = true };
                db.Zone.Add(zona);
                await db.SaveChangesAsync();

                tavoli.AddRange(capienze.Select(c => new Postazione
                {
                    Numero = numero++,
                    CapienzaMassima = c,
                    Attiva = true,
                    ZonaId = zona.Id
                }));
            }

            db.Postazioni.AddRange(tavoli);
            await db.SaveChangesAsync();
            return tavoli;
        }

        /// <summary>Pranzo e cena tutti i giorni, lunedi' chiuso. I tetti stanno sotto i posti della sala.</summary>
        private static async Task<List<FasciaOraria>> CreaFasceAsync(GestoraContext db)
        {
            var fasce = new List<FasciaOraria>();
            foreach (var giorno in Enum.GetValues<DayOfWeek>().Where(g => g != DayOfWeek.Monday))
            {
                fasce.Add(new FasciaOraria
                {
                    GiornoSettimana = giorno,
                    OrarioInizio = new TimeOnly(12, 0),
                    OrarioFine = new TimeOnly(14, 30),
                    MaxCoperti = TettoPranzo,
                    Attiva = true
                });
                fasce.Add(new FasciaOraria
                {
                    GiornoSettimana = giorno,
                    OrarioInizio = new TimeOnly(19, 30),
                    OrarioFine = new TimeOnly(23, 0),
                    MaxCoperti = TettoCena,
                    Attiva = true
                });
            }

            db.FasciaOrarie.AddRange(fasce);
            await db.SaveChangesAsync();
            return fasce;
        }

        /// <summary>
        /// Una settimana di storico, oggi e le due settimane successive. Ogni fascia si riempie fino
        /// a una quota del tetto; i tavoli li sceglie AssegnazioneTavoli fra quelli ancora liberi, e
        /// un gruppo che non trova posto viene semplicemente saltato.
        /// </summary>
        private static async Task<int> CreaPrenotazioniAsync(
            GestoraContext db, List<Postazione> tavoli, List<FasciaOraria> fasce, string[] autori, IClock clock)
        {
            var casuale = new Random(20261028);   // seme fisso: due esecuzioni danno lo stesso esito
            var oggi = clock.TodayInRome;
            var adesso = TimeOnly.FromDateTime(clock.NowInRome);
            var prenotazioni = new List<Prenotazione>();
            var associazioni = new List<(Prenotazione Prenotazione, List<Postazione> Tavoli)>();
            var indiceGruppo = 0;
            var indiceCliente = 0;

            for (var scarto = -7; scarto <= 14; scarto++)
            {
                var data = oggi.AddDays(scarto);
                foreach (var fascia in fasce.Where(f => f.GiornoSettimana == data.DayOfWeek))
                {
                    // Oggi la cena e' quasi piena, il resto oscilla fra meta' e quasi tutto; piu'
                    // avanti nel calendario le prenotazioni si diradano, come succede davvero.
                    var quota = scarto == 0 && fascia.MaxCoperti == TettoCena ? 0.9
                        : scarto > 7 ? 0.2 + casuale.NextDouble() * 0.3
                        : 0.5 + casuale.NextDouble() * 0.4;
                    var obiettivo = (int)(fascia.MaxCoperti * quota);

                    var liberi = new List<Postazione>(tavoli);
                    var coperti = 0;
                    var tentativi = 0;

                    while (coperti < obiettivo && tentativi++ < 30)
                    {
                        var gruppo = Gruppi[indiceGruppo++ % Gruppi.Length];
                        if (coperti + gruppo > fascia.MaxCoperti) continue;

                        var scelti = AssegnazioneTavoli.TrovaMigliorCombinazione(liberi, gruppo);
                        if (scelti is null) continue;

                        var prenotazione = new Prenotazione
                        {
                            DataPrenotazione = data,
                            NumeroCoperti = gruppo,
                            FasciaOrariaId = fascia.Id,
                            UserId = autori[casuale.Next(autori.Length)],
                            NomeCliente = Clienti[indiceCliente++ % Clienti.Length],
                            Note = casuale.Next(6) == 0 ? Note[casuale.Next(Note.Length)] : null,
                            Stato = Stato(scarto, fascia, adesso, casuale)
                        };

                        prenotazioni.Add(prenotazione);
                        associazioni.Add((prenotazione, scelti));
                        liberi.RemoveAll(scelti.Contains);
                        coperti += gruppo;
                    }
                }
            }

            db.Prenotazioni.AddRange(prenotazioni);
            await db.SaveChangesAsync();

            foreach (var (prenotazione, scelti) in associazioni)
            {
                var distribuzione = AssegnazioneTavoli.DistribuisciCoperti(scelti, prenotazione.NumeroCoperti);
                db.PrenotazioniPostazioni.AddRange(scelti.Select(t => new PrenotazionePostazione
                {
                    PrenotazioneId = prenotazione.Id,
                    PostazioneId = t.Id,
                    NumeroPosti = distribuzione[t.Id],
                    DataPrenotazione = prenotazione.DataPrenotazione,
                    FasciaOrariaId = prenotazione.FasciaOrariaId
                }));
            }
            await db.SaveChangesAsync();

            return prenotazioni.Count;
        }

        /// <summary>
        /// Solo stati che l'applicazione puo' davvero raggiungere: il passato e' completato (con
        /// qualche tavolo mai arrivato), il turno in corso e' confermato, il futuro e' da servire.
        /// </summary>
        private static StatoPrenotazione Stato(int scarto, FasciaOraria fascia, TimeOnly adesso, Random casuale)
        {
            var passato = scarto < 0 || (scarto == 0 && adesso > fascia.OrarioFine);
            if (passato)
                return casuale.Next(12) == 0 ? StatoPrenotazione.NonPresentata : StatoPrenotazione.Completata;

            var inCorso = scarto == 0 && adesso >= fascia.OrarioInizio;
            if (inCorso)
                return casuale.Next(4) == 0 ? StatoPrenotazione.Attiva : StatoPrenotazione.InCorso;

            return StatoPrenotazione.Attiva;
        }
    }
}
