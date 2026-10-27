using GestoraWebApi.Enums;
using GestoraWebApi.Models;

namespace GestoraWebApi.Repositories.Prenotazioni
{
    public interface IPrenotazioniRepository : IRepository<Prenotazione>
    {
        Task<Prenotazione?> GetTrackedByIdAsync(long id);
        Task BloccaPerModificaAsync(long id);
        IQueryable<Prenotazione> GetAllQueryableAsync();

        /// <summary>
        /// REV-022: porta allo stato indicato tutte le prenotazioni elencate, con una sola
        /// scrittura. Serve ai job notturni, che prima aggiornavano una riga per volta.
        /// Restituisce il numero di righe effettivamente modificate.
        /// </summary>
        Task<int> AggiornaStatoAsync(IReadOnlyCollection<long> ids, StatoPrenotazione nuovoStato);

        /// <summary>
        /// REV-022: elimina in un colpo solo le prenotazioni elencate.
        /// Restituisce il numero di righe eliminate.
        /// </summary>
        Task<int> EliminaPerIdAsync(IReadOnlyCollection<long> ids);
        Task<List<Prenotazione>> GetPrenotazioniByDataAsync(DateOnly data);
        Task<List<Prenotazione>> GetAllPrenotazioniAsync();
        Task<List<FasciaOraria>> GetFasceOrarieByDayAsync(DayOfWeek giorno);

        /// <summary>
        /// V2-019: le prenotazioni ancora da servire (Attive o In corso, da <paramref name="daData"/>
        /// in avanti) legate all'utente. Con la fascia, per il messaggio di blocco.
        /// </summary>
        Task<List<Prenotazione>> GetDaServireDiUtenteAsync(string userId, DateOnly daData);

        /// <summary>
        /// V2-019: stacca dall'utente tutte le sue prenotazioni (UserId a null). Quelle sue, senza
        /// NomeCliente, prendono <paramref name="nomeAnonimo"/> e perdono le note; quelle prese per
        /// altri tengono il nome del cliente. Restituisce quante ne ha toccate.
        /// </summary>
        Task<int> StaccaDaUtenteAsync(string userId, string nomeAnonimo);
    }
}
