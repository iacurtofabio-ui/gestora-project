using System.Diagnostics;
using System.Net.Http.Json;
using GestoraWebApi.Auth;
using GestoraWebApi.IntegrationTests.Infrastruttura;
using Xunit.Abstractions;

namespace GestoraWebApi.IntegrationTests
{
    /// <summary>
    /// Una misura leggera, non una prova di carico: 100 chiamate di fila con una sala grande.
    /// Il tempo misura l'API e il database, senza la rete: in produzione va aggiunta quella.
    /// </summary>
    public class PrestazioniTests(GestoraFixture gestora, ITestOutputHelper output) : TestIntegrazione(gestora)
    {
        private const int Chiamate = 100;
        private const int SogliaMillisecondi = 300;

        private static readonly int[] SalaGrande = Enumerable.Range(0, 60).Select(i => i % 3 == 0 ? 4 : 2).ToArray();

        private void ControllaTempi(string nome, List<long> tempi)
        {
            tempi.Sort();
            var novantacinquesimo = tempi[(int)(tempi.Count * 0.95) - 1];
            output.WriteLine($"{nome}: mediana {tempi[tempi.Count / 2]} ms, 95 su 100 sotto {novantacinquesimo} ms, massimo {tempi[^1]} ms");

            Assert.True(novantacinquesimo < SogliaMillisecondi, $"{nome}: 95 su 100 sotto {novantacinquesimo} ms");
        }

        [Fact]
        public async Task VerificaDisponibilita_95ChiamateSu100SottoLaSoglia()
        {
            var giorno = Gestora.GiornoFuturo();
            await Gestora.CreaSalaAsync(giorno, SalaGrande);
            using var client = Gestora.NuovoClient();
            var dati = new { dataPrenotazione = giorno, numeroCoperti = 7 };
            var tempi = new List<long>();

            for (var i = 0; i < Chiamate; i++)
            {
                // Un indirizzo nuovo per ogni chiamata, altrimenti risponderebbe il limite di richieste.
                client.Indirizzo = $"172.16.{i / 250}.{i % 250 + 1}";

                var cronometro = Stopwatch.StartNew();
                var risposta = await client.Http.PostAsJsonAsync("/api/Prenotazione/check-disponibilita", dati);
                cronometro.Stop();

                risposta.EnsureSuccessStatusCode();
                tempi.Add(cronometro.ElapsedMilliseconds);
            }

            ControllaTempi("check-disponibilita", tempi);
        }

        [Fact]
        public async Task CreaPrenotazione_95ChiamateSu100SottoLaSoglia()
        {
            var giorno = Gestora.GiornoFuturo();
            var sala = await Gestora.CreaSalaAsync(giorno, SalaGrande);
            using var staff = await Gestora.AccediComeAsync(Roles.Staff);
            var tempi = new List<long>();

            for (var i = 0; i < Chiamate; i++)
            {
                // Quattro prenotazioni per settimana sulla stessa fascia, per 25 settimane.
                var dati = new
                {
                    dataPrenotazione = giorno.AddDays(7 * (i / 4)),
                    numeroCoperti = 7,
                    fasciaOrariaId = sala.FasciaId,
                    nomeCliente = $"Cliente {i}"
                };

                var cronometro = Stopwatch.StartNew();
                var risposta = await staff.Http.PostAsJsonAsync("/api/Prenotazione/crea-prenotazione", dati);
                cronometro.Stop();

                risposta.EnsureSuccessStatusCode();
                tempi.Add(cronometro.ElapsedMilliseconds);
            }

            ControllaTempi("crea-prenotazione", tempi);
        }
    }
}
