using System.Net;
using System.Net.Http.Json;
using GestoraWebApi.Auth;
using GestoraWebApi.Enums;
using GestoraWebApi.IntegrationTests.Infrastruttura;
using GestoraWebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace GestoraWebApi.IntegrationTests
{
    /// <summary>Il percorso vero di una prenotazione: API, regole, query di EF Core, tavoli salvati.</summary>
    public class PrenotazioniTests(GestoraFixture gestora) : TestIntegrazione(gestora)
    {
        private Task<List<int>> CapienzeAssegnateAsync(long prenotazioneId)
            => Gestora.ConContestoAsync(c => c.Prenotazioni
                .Where(p => p.Id == prenotazioneId)
                .SelectMany(p => p.PrenotazioniPostazioni.Select(pp => pp.Postazione.CapienzaMassima))
                .OrderBy(capienza => capienza)
                .ToListAsync());

        private Task<Prenotazione> LeggiAsync(long id)
            => Gestora.ConContestoAsync(c => c.Prenotazioni.SingleAsync(p => p.Id == id));

        private static object Dati(SalaDiProva sala, int coperti) => new
        {
            dataPrenotazione = sala.Giorno,
            numeroCoperti = coperti,
            fasciaOrariaId = sala.FasciaId
        };

        [Fact]
        public async Task Cliente_Prenota_ELaPrenotazioneHaIlTavoloCheSprecaMeno()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [4, 2, 8]);
            using var cliente = await Gestora.AccediComeAsync(Roles.Cliente);

            var id = await Gestora.PrenotaAsync(cliente, sala, 2);

            Assert.Equal(StatoPrenotazione.Attiva, (await LeggiAsync(id)).Stato);
            Assert.Equal([2], await CapienzeAssegnateAsync(id));
        }

        [Fact]
        public async Task GruppoDaSei_SuTavoliDa4E2_UnisceLaCombinazioneSenzaSpreco()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [4, 4, 2]);
            using var staff = await Gestora.AccediComeAsync(Roles.Staff);

            var id = await Gestora.PrenotaAsync(staff, sala, 6, "Tavolata");

            Assert.Equal([2, 4], await CapienzeAssegnateAsync(id));
        }

        [Fact]
        public async Task GruppoDaSei_SuSoliTavoliDa2_DueTavoliConIlBonusDelleTestate()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [2, 2, 2, 2]);
            using var staff = await Gestora.AccediComeAsync(Roles.Staff);

            var id = await Gestora.PrenotaAsync(staff, sala, 6, "Tavolata");

            Assert.Equal([2, 2], await CapienzeAssegnateAsync(id));
        }

        [Fact]
        public async Task Cliente_SecondaPrenotazioneNelloStessoGiorno_Rifiutata()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [2, 2]);
            using var cliente = await Gestora.AccediComeAsync(Roles.Cliente);
            await Gestora.PrenotaAsync(cliente, sala, 2);

            var risposta = await cliente.Http.PostAsJsonAsync("/api/Prenotazione/crea-prenotazione", Dati(sala, 2));

            Assert.Equal(HttpStatusCode.Conflict, risposta.StatusCode);
        }

        [Fact]
        public async Task LimiteOnline_FermaIlClienteOltre20_MaNonLoStaff()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [30]);
            using var cliente = await Gestora.AccediComeAsync(Roles.Cliente);
            using var staff = await Gestora.AccediComeAsync(Roles.Staff);

            var dalCliente = await cliente.Http.PostAsJsonAsync("/api/Prenotazione/crea-prenotazione", Dati(sala, 21));

            Assert.Equal(HttpStatusCode.Conflict, dalCliente.StatusCode);
            await Gestora.PrenotaAsync(staff, sala, 21, "Compleanno");
        }

        [Fact]
        public async Task Cliente_PrenotazioneDiUnAltro_Risponde404()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [2, 2]);
            using var proprietario = await Gestora.AccediComeAsync(Roles.Cliente);
            using var altro = await Gestora.AccediComeAsync(Roles.Cliente);
            var id = await Gestora.PrenotaAsync(proprietario, sala, 2);

            var lettura = await altro.Http.GetAsync($"/api/Prenotazione/get-prenotazione?id={id}");
            var annullamento = await altro.Http.PatchAsync($"/api/Prenotazione/annulla-prenotazione?id={id}", null);

            Assert.Equal(HttpStatusCode.NotFound, lettura.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, annullamento.StatusCode);
            Assert.Equal(StatoPrenotazione.Attiva, (await LeggiAsync(id)).Stato);
        }

        [Fact]
        public async Task Annullamento_LiberaIlTavolo_PerChiPrenotaDopo()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [2]);
            using var primo = await Gestora.AccediComeAsync(Roles.Cliente);
            using var secondo = await Gestora.AccediComeAsync(Roles.Cliente);
            var id = await Gestora.PrenotaAsync(primo, sala, 2);

            var occupato = await secondo.Http.PostAsJsonAsync("/api/Prenotazione/crea-prenotazione", Dati(sala, 2));
            await primo.Http.PatchAsync($"/api/Prenotazione/annulla-prenotazione?id={id}", null);

            Assert.Equal(HttpStatusCode.Conflict, occupato.StatusCode);
            await Gestora.PrenotaAsync(secondo, sala, 2);
        }

        [Fact]
        public async Task Stati_DaAttivaAInCorsoACompletata_PoiNonSiModificaPiu()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [4]);
            using var staff = await Gestora.AccediComeAsync(Roles.Staff);
            var id = await Gestora.PrenotaAsync(staff, sala, 2, "Rossi");

            Gestora.Orologio.VaiA(sala.Giorno, new TimeOnly(19, 30));
            var arrivo = await staff.Http.PatchAsync($"/api/Prenotazione/conferma-prenotazione?id={id}", null);
            Assert.Equal(HttpStatusCode.OK, arrivo.StatusCode);
            Assert.Equal(StatoPrenotazione.InCorso, (await LeggiAsync(id)).Stato);

            Gestora.Orologio.VaiA(sala.Giorno, new TimeOnly(22, 30));
            var fine = await staff.Http.PatchAsync($"/api/Prenotazione/completa-prenotazione?id={id}", null);
            Assert.Equal(HttpStatusCode.OK, fine.StatusCode);
            Assert.Equal(StatoPrenotazione.Completata, (await LeggiAsync(id)).Stato);

            var annullamento = await staff.Http.PatchAsync($"/api/Prenotazione/annulla-prenotazione?id={id}", null);
            Assert.Equal(HttpStatusCode.Conflict, annullamento.StatusCode);
        }

        [Fact]
        public async Task Conferma_DiUnaPrenotazioneGiaPassata_Rifiutata()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [4]);
            using var staff = await Gestora.AccediComeAsync(Roles.Staff);
            var id = await Gestora.PrenotaAsync(staff, sala, 2, "Rossi");

            Gestora.Orologio.VaiA(sala.Giorno.AddDays(1), new TimeOnly(12, 0));
            var risposta = await staff.Http.PatchAsync($"/api/Prenotazione/conferma-prenotazione?id={id}", null);

            Assert.Equal(HttpStatusCode.Conflict, risposta.StatusCode);
        }

        [Fact]
        public async Task Admin_EliminaSoloLeAnnullate()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [4]);
            using var admin = await Gestora.AccediComeAsync(Roles.Admin);
            var id = await Gestora.PrenotaAsync(admin, sala, 2, "Rossi");

            var attiva = await admin.Http.DeleteAsync($"/api/Prenotazione/delete-prenotazione?id={id}");
            await admin.Http.PatchAsync($"/api/Prenotazione/annulla-prenotazione?id={id}", null);
            var annullata = await admin.Http.DeleteAsync($"/api/Prenotazione/delete-prenotazione?id={id}");

            Assert.Equal(HttpStatusCode.Conflict, attiva.StatusCode);
            Assert.True(annullata.IsSuccessStatusCode);
            Assert.False(await Gestora.ConContestoAsync(c => c.Prenotazioni.AnyAsync(p => p.Id == id)));
        }

        [Fact]
        public async Task Disponibilita_NonPromette_IlRestoDiUnTavoloOccupato()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [8]);
            using var staff = await Gestora.AccediComeAsync(Roles.Staff);
            using var visitatore = Gestora.NuovoClient();
            await Gestora.PrenotaAsync(staff, sala, 5, "Rossi");

            var risposta = await visitatore.Http.PostAsJsonAsync("/api/Prenotazione/check-disponibilita",
                new { dataPrenotazione = sala.Giorno, numeroCoperti = 3 });
            var testo = await risposta.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
            Assert.Contains("TavoliInsufficienti", testo);
        }

        [Fact]
        public async Task ReportCsv_ContieneLePrenotazioniDellIntervallo()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [4]);
            using var admin = await Gestora.AccediComeAsync(Roles.Admin);
            await Gestora.PrenotaAsync(admin, sala, 2, "Cliente Zoè");

            var giorno = sala.Giorno.ToString("yyyy-MM-dd");
            var risposta = await admin.Http.GetAsync($"/api/Prenotazione/report-csv?da={giorno}&a={giorno}");

            Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
            Assert.Contains("Cliente Zoè", await risposta.Content.ReadAsStringAsync());
        }
    }
}
