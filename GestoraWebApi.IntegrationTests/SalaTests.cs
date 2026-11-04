using System.Net;
using System.Net.Http.Json;
using GestoraWebApi.Auth;
using GestoraWebApi.IntegrationTests.Infrastruttura;
using GestoraWebApi.Services.Postazioni.DTOs;
using Microsoft.EntityFrameworkCore;

namespace GestoraWebApi.IntegrationTests
{
    /// <summary>Le regole di coerenza fra fasce, tavoli e zone: leggono la sala vera dal database.</summary>
    public class SalaTests(GestoraFixture gestora) : TestIntegrazione(gestora)
    {
        private static object Fascia(DateOnly giorno, string inizio, string fine, int maxCoperti) => new
        {
            orarioInizio = inizio,
            orarioFine = fine,
            giornoSettimana = (int)giorno.DayOfWeek,
            maxCoperti,
            attiva = true
        };

        private Task<(long Id, int Numero)> PrimoTavoloAsync(long zonaId)
            => Gestora.ConContestoAsync(async c =>
            {
                var tavolo = await c.Postazioni.FirstAsync(p => p.ZonaId == zonaId);
                return (tavolo.Id, tavolo.Numero);
            });

        [Fact]
        public async Task Fascia_SuSalaVuota_Rifiutata()
        {
            using var admin = await Gestora.AccediComeAsync(Roles.Admin);

            var risposta = await admin.Http.PostAsJsonAsync("/api/FasceOrarie/crea-fascia",
                Fascia(Gestora.GiornoFuturo(), "12:00", "14:00", 10));

            Assert.Equal(HttpStatusCode.Conflict, risposta.StatusCode);
        }

        [Fact]
        public async Task Fascia_ConTettoOltreIPostiDellaSala_Rifiutata()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [4, 2]);
            using var admin = await Gestora.AccediComeAsync(Roles.Admin);

            var risposta = await admin.Http.PostAsJsonAsync("/api/FasceOrarie/crea-fascia",
                Fascia(sala.Giorno, "12:00", "14:00", 7));

            Assert.Equal(HttpStatusCode.Conflict, risposta.StatusCode);
        }

        [Fact]
        public async Task Fascia_SovrappostaAUnAltraDelloStessoGiorno_Rifiutata()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [4, 2]);
            using var admin = await Gestora.AccediComeAsync(Roles.Admin);

            var risposta = await admin.Http.PostAsJsonAsync("/api/FasceOrarie/crea-fascia",
                Fascia(sala.Giorno, "21:00", "23:00", 6));

            Assert.False(risposta.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Tavolo_RidottoSottoIlTettoDiUnaFascia_Bloccato()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [4, 2]);
            var tavolo = await PrimoTavoloAsync(sala.ZonaId);
            using var admin = await Gestora.AccediComeAsync(Roles.Admin);

            var risposta = await admin.Http.PutAsJsonAsync("/api/Postazione/update-postazione", new
            {
                id = tavolo.Id,
                numero = tavolo.Numero,
                capienzaMassima = 1,
                zonaId = sala.ZonaId,
                attiva = true
            });

            Assert.Equal(HttpStatusCode.Conflict, risposta.StatusCode);
        }

        [Fact]
        public async Task Zona_ConPrenotazioniDaServire_NonSiSpegne()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [4]);
            using var admin = await Gestora.AccediComeAsync(Roles.Admin);
            await Gestora.PrenotaAsync(admin, sala, 2, "Rossi");

            var risposta = await admin.Http.PatchAsync($"/api/Zona/update-stato/{sala.ZonaId}?attiva=false", null);

            Assert.Equal(HttpStatusCode.Conflict, risposta.StatusCode);
        }

        [Fact]
        public async Task ZonaSpenta_NonOffreTavoliAChiPrenota()
        {
            var giorno = Gestora.GiornoFuturo();
            var accesa = await Gestora.CreaSalaAsync(giorno, [4], maxCoperti: 4);
            var spenta = await Gestora.CreaSalaAsync(giorno.AddDays(1), [8], maxCoperti: 1);
            using var admin = await Gestora.AccediComeAsync(Roles.Admin);

            await Gestora.ConContestoAsync(async c =>
            {
                var fascia = await c.FasciaOrarie.SingleAsync(f => f.Id == spenta.FasciaId);
                c.Remove(fascia);
                return await c.SaveChangesAsync();
            });
            var spegnimento = await admin.Http.PatchAsync($"/api/Zona/update-stato/{spenta.ZonaId}?attiva=false", null);
            Assert.True(spegnimento.IsSuccessStatusCode);

            var id = await Gestora.PrenotaAsync(admin, accesa, 4, "Rossi");

            var tavoli = await Gestora.ConContestoAsync(c => c.Prenotazioni.Where(p => p.Id == id)
                .SelectMany(p => p.PrenotazioniPostazioni.Select(pp => pp.Postazione.ZonaId)).ToListAsync());
            Assert.Equal([accesa.ZonaId], tavoli);
        }

        [Fact]
        public async Task Riepilogo_ContaITavoliAttiviDelleZoneAttive_SenzaBonus()
        {
            await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [2, 2, 4]);
            using var staff = await Gestora.AccediComeAsync(Roles.Staff);

            var riepilogo = await staff.Http.GetFromJsonAsync<RiepilogoSalaDTO>("/api/Postazione/riepilogo-sala");

            Assert.Equal(3, riepilogo!.TavoliAttivi);
            Assert.Equal(8, riepilogo.PostiTotali);
        }
    }
}
