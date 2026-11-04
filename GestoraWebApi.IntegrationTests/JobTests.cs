using GestoraWebApi.Auth;
using GestoraWebApi.Enums;
using GestoraWebApi.IntegrationTests.Infrastruttura;
using GestoraWebApi.Models;
using GestoraWebApi.Services.LogActivity;
using GestoraWebApi.Services.Prenotazioni;
using GestoraWebApi.Services.Utenti;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestoraWebApi.IntegrationTests
{
    /// <summary>I lavori notturni sulle date vere: i filtri delle query si provano solo sul database.</summary>
    public class JobTests(GestoraFixture gestora) : TestIntegrazione(gestora)
    {
        private async Task EseguiAsync<TServizio>(Func<TServizio, Task> lavoro) where TServizio : notnull
        {
            using var scope = Gestora.Fabbrica.Services.CreateScope();
            await lavoro(scope.ServiceProvider.GetRequiredService<TServizio>());
        }

        private Task<StatoPrenotazione> StatoAsync(long id)
            => Gestora.ConContestoAsync(async c => (await c.Prenotazioni.SingleAsync(p => p.Id == id)).Stato);

        [Fact]
        public async Task PrenotazioniJob_ChiudeInCorso_ESegnaIMaiArrivati()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [4, 4]);
            using var staff = await Gestora.AccediComeAsync(Roles.Staff);
            var arrivata = await Gestora.PrenotaAsync(staff, sala, 2, "Arrivata");
            var maiArrivata = await Gestora.PrenotaAsync(staff, sala, 2, "Mai arrivata");

            Gestora.Orologio.VaiA(sala.Giorno, new TimeOnly(19, 30));
            await staff.Http.PatchAsync($"/api/Prenotazione/conferma-prenotazione?id={arrivata}", null);

            Gestora.Orologio.VaiA(sala.Giorno.AddDays(1), new TimeOnly(2, 0));
            await EseguiAsync<IPrenotazioniService>(s => s.AutomaticCompletPrenotazioniAsync());

            Assert.Equal(StatoPrenotazione.Completata, await StatoAsync(arrivata));
            Assert.Equal(StatoPrenotazione.NonPresentata, await StatoAsync(maiArrivata));
        }

        [Fact]
        public async Task PrenotazioniJob_NonToccaLePrenotazioniDiStasera()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [4]);
            using var staff = await Gestora.AccediComeAsync(Roles.Staff);
            var id = await Gestora.PrenotaAsync(staff, sala, 2, "Stasera");

            Gestora.Orologio.VaiA(sala.Giorno, new TimeOnly(2, 0));
            await EseguiAsync<IPrenotazioniService>(s => s.AutomaticCompletPrenotazioniAsync());

            Assert.Equal(StatoPrenotazione.Attiva, await StatoAsync(id));
        }

        [Fact]
        public async Task Pulizia_EliminaLeChiuseOltreISeiMesi_ETieneLeAltre()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [4, 4]);
            using var staff = await Gestora.AccediComeAsync(Roles.Staff);
            var vecchia = await Gestora.PrenotaAsync(staff, sala, 2, "Vecchia");

            Gestora.Orologio.VaiA(sala.Giorno.AddDays(1), new TimeOnly(2, 0));
            await EseguiAsync<IPrenotazioniService>(s => s.AutomaticCompletPrenotazioniAsync());

            var recente = await Gestora.CreaSalaAsync(sala.Giorno.AddMonths(7), [4]);
            Gestora.Orologio.VaiA(sala.Giorno.AddMonths(7).AddDays(-1), new TimeOnly(12, 0));
            var tenuta = await Gestora.PrenotaAsync(staff, recente, 2, "Recente");

            await EseguiAsync<IPrenotazioniService>(s => s.AutomaticDeletePrenotazioniAsync());

            Assert.False(await Gestora.ConContestoAsync(c => c.Prenotazioni.AnyAsync(p => p.Id == vecchia)));
            Assert.True(await Gestora.ConContestoAsync(c => c.Prenotazioni.AnyAsync(p => p.Id == tenuta)));
        }

        [Fact]
        public async Task UtenzeMaiUsate_EliminateDopoISeiMesi_SoloSeMaiEntrate()
        {
            await Gestora.CreaUtenteAsync("mai-entrato@prova.test", Roles.Cliente);
            using var entrato = await Gestora.AccediComeAsync(Roles.Cliente, "entrato@prova.test");

            Gestora.Orologio.Spostamento = TimeSpan.FromDays(181);
            await EseguiAsync<IUtenzeInattiveService>(s => s.EliminaUtenzeMaiUsateAsync());

            var email = await Gestora.ConContestoAsync(c => c.Users.Select(u => u.Email).ToListAsync());
            Assert.DoesNotContain("mai-entrato@prova.test", email);
            Assert.Contains("entrato@prova.test", email);
        }

        [Fact]
        public async Task ClienteInattivo_RiceveLAvviso_EPoiVieneEliminato()
        {
            using var cliente = await Gestora.AccediComeAsync(Roles.Cliente, "inattivo@prova.test");
            var ultimoAccesso = Gestora.Orologio.UtcNow;

            Gestora.Orologio.Spostamento = ultimoAccesso.AddMonths(24).AddDays(-20) - DateTime.UtcNow;
            await EseguiAsync<IUtenzeInattiveService>(s => s.AvvisaInattiviAsync());

            Assert.Equal("inattivo@prova.test", Assert.Single(Gestora.Posta.Email).Destinatario);

            Gestora.Orologio.Spostamento = ultimoAccesso.AddMonths(24).AddDays(10) - DateTime.UtcNow;
            await EseguiAsync<IUtenzeInattiveService>(s => s.EliminaInattiviAsync());

            Assert.False(await Gestora.ConContestoAsync(c => c.Users.AnyAsync(u => u.Email == "inattivo@prova.test")));
        }

        [Fact]
        public async Task RegistroAttivita_EliminaSoloLeRigheOltreI24Mesi()
        {
            var adesso = Gestora.Orologio.UtcNow;
            await Gestora.ConContestoAsync(async c =>
            {
                c.LogActivities.Add(new Logging { UserId = "x", Action = "Vecchia", Timestamp = adesso.AddMonths(-25) });
                c.LogActivities.Add(new Logging { UserId = "x", Action = "Recente", Timestamp = adesso.AddMonths(-23) });
                return await c.SaveChangesAsync();
            });

            await EseguiAsync<IRegistroAttivitaCleanupService>(s => s.EliminaScaduteAsync());

            var azioni = await Gestora.ConContestoAsync(c => c.LogActivities.Select(l => l.Action).ToListAsync());
            Assert.Equal(["Recente"], azioni);
        }
    }
}
