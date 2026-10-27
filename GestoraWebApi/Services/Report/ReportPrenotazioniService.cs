using GestoraWebApi.Infrastructure.Exceptions;
using GestoraWebApi.Repositories.Prenotazioni;
using Microsoft.EntityFrameworkCore;

namespace GestoraWebApi.Services.Report
{
    public interface IReportPrenotazioniService
    {
        /// <summary>Il CSV delle prenotazioni da <paramref name="da"/> ad <paramref name="a"/> compresi, tutti gli stati.</summary>
        Task<byte[]> CsvAsync(DateOnly da, DateOnly a);
    }

    /// <summary>
    /// V2-025 — report delle prenotazioni per l'Admin, al posto dell'export dei dati per l'utente
    /// tolto dalla F3. Legge e ordina; il formato lo decide <see cref="CsvPrenotazioni"/>.
    /// </summary>
    public class ReportPrenotazioniService : IReportPrenotazioniService
    {
        /// <summary>Un anno al massimo: oltre, il file diventa pesante e serve a poco aprirlo in Excel.</summary>
        public const int GiorniMassimi = 366;

        private readonly IPrenotazioniRepository _repository;

        public ReportPrenotazioniService(IPrenotazioniRepository repository)
        {
            _repository = repository;
        }

        public async Task<byte[]> CsvAsync(DateOnly da, DateOnly a)
        {
            // Un parametro mancante arriva come 01/01/0001: meglio dirlo che restituire un file vuoto.
            if (da == default)
                throw ErrorePeriodo("da", "Indica la data iniziale.");
            if (a == default)
                throw ErrorePeriodo("a", "Indica la data finale.");
            if (a < da)
                throw ErrorePeriodo("a", "La data finale non può essere prima di quella iniziale.");
            if (a.DayNumber - da.DayNumber + 1 > GiorniMassimi)
                throw ErrorePeriodo("a", "Il periodo può essere al massimo di un anno.");

            var prenotazioni = await _repository.GetAllQueryableAsync()
                .Where(p => p.DataPrenotazione >= da && p.DataPrenotazione <= a)
                .Include(p => p.User)
                .Include(p => p.FasciaOraria)
                .Include(p => p.PrenotazioniPostazioni)
                    .ThenInclude(pp => pp.Postazione)
                        .ThenInclude(po => po.Zona)
                .OrderBy(p => p.DataPrenotazione)
                .ThenBy(p => p.FasciaOraria.OrarioInizio)
                .ThenBy(p => p.Id)
                .ToListAsync();

            return CsvPrenotazioni.Componi(prenotazioni);
        }

        private static ValidationException ErrorePeriodo(string campo, string messaggio) =>
            new("Periodo del report non valido.", new Dictionary<string, string[]> { [campo] = [messaggio] });
    }
}
