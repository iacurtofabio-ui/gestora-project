using System.Net;
using System.Net.Http.Json;
using GestoraWebApi.Auth;
using GestoraWebApi.Enums;
using GestoraWebApi.IntegrationTests.Infrastruttura;
using Microsoft.EntityFrameworkCore;

namespace GestoraWebApi.IntegrationTests
{
    /// <summary>
    /// Il blocco di riga sulla fascia (<c>SELECT ... FOR UPDATE</c>) esiste solo in PostgreSQL: i
    /// test unitari non possono provarlo. Qui dieci clienti prenotano nello stesso istante.
    /// </summary>
    public class ConcorrenzaTests(GestoraFixture gestora) : TestIntegrazione(gestora)
    {
        private const int Clienti = 10;

        private async Task<HttpStatusCode[]> PrenotanoInsiemeAsync(SalaDiProva sala, int coperti)
        {
            var clienti = new List<ClientGestora>();
            for (var i = 0; i < Clienti; i++)
                clienti.Add(await Gestora.AccediComeAsync(Roles.Cliente));

            var dati = new
            {
                dataPrenotazione = sala.Giorno,
                numeroCoperti = coperti,
                fasciaOrariaId = sala.FasciaId
            };

            var risposte = await Task.WhenAll(clienti.Select(
                c => c.Http.PostAsJsonAsync("/api/Prenotazione/crea-prenotazione", dati)));

            clienti.ForEach(c => c.Dispose());
            return risposte.Select(r => r.StatusCode).ToArray();
        }

        [Fact]
        public async Task UltimoTavolo_LoPrendeUnoSolo()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [4]);

            var esiti = await PrenotanoInsiemeAsync(sala, 2);

            Assert.Single(esiti, e => e == HttpStatusCode.Created);
            Assert.Equal(Clienti - 1, esiti.Count(e => e == HttpStatusCode.Conflict));

            var assegnazioni = await Gestora.ConContestoAsync(c => c.Prenotazioni
                .Where(p => p.Stato != StatoPrenotazione.Annullata)
                .SelectMany(p => p.PrenotazioniPostazioni)
                .CountAsync());
            Assert.Equal(1, assegnazioni);
        }

        [Fact]
        public async Task UltimiPostiDellaFascia_NonSiSuperaIlTetto()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), Enumerable.Repeat(2, 10).ToArray(), maxCoperti: 6);

            var esiti = await PrenotanoInsiemeAsync(sala, 2);

            Assert.Equal(3, esiti.Count(e => e == HttpStatusCode.Created));
            Assert.Equal(Clienti - 3, esiti.Count(e => e == HttpStatusCode.Conflict));

            var coperti = await Gestora.ConContestoAsync(c => c.Prenotazioni.SumAsync(p => p.NumeroCoperti));
            Assert.Equal(6, coperti);
        }

        [Fact]
        public async Task NessunTavolo_AssegnatoADuePrenotazioni()
        {
            var sala = await Gestora.CreaSalaAsync(Gestora.GiornoFuturo(), [2, 2, 2, 2]);

            var esiti = await PrenotanoInsiemeAsync(sala, 2);

            Assert.Equal(4, esiti.Count(e => e == HttpStatusCode.Created));

            var tavoli = await Gestora.ConContestoAsync(c => c.Prenotazioni
                .SelectMany(p => p.PrenotazioniPostazioni.Select(pp => pp.PostazioneId))
                .ToListAsync());
            Assert.Equal(tavoli.Count, tavoli.Distinct().Count());
        }
    }
}
