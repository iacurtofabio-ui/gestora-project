using GestoraWebApi.Context;
using GestoraWebApi.Enums;
using GestoraWebApi.Models;
using Microsoft.EntityFrameworkCore;


namespace GestoraWebApi.Repositories.Prenotazioni
{
    public class PrenotazioniRepository : IPrenotazioniRepository
    {
        private readonly GestoraContext _context;
        public PrenotazioniRepository(GestoraContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Prenotazione entity)
        {
            await _context.Prenotazioni.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<Prenotazione?> GetByIdAsync(long id)
        {
            return await _context.Prenotazioni
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.FasciaOraria)
                .Include(p => p.PrenotazioniPostazioni)
                    .ThenInclude(pp => pp.Postazione)
                        .ThenInclude(po => po.Zona)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public IQueryable<Prenotazione> GetAllQueryableAsync()
        {
            return _context.Prenotazioni.AsNoTracking();
        }

        public async Task UpdateAsync(Prenotazione entity)
        {
            _context.Prenotazioni.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Prenotazione entity)
        {
            _context.Prenotazioni.Remove(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Prenotazione>> GetDaServireDiUtenteAsync(string userId, DateOnly daData)
        {
            return await _context.Prenotazioni
                .AsNoTracking()
                .Where(p => p.UserId == userId
                         && p.DataPrenotazione >= daData
                         && (p.Stato == StatoPrenotazione.Attiva || p.Stato == StatoPrenotazione.InCorso))
                .Include(p => p.FasciaOraria)
                .OrderBy(p => p.DataPrenotazione)
                .ThenBy(p => p.FasciaOraria.OrarioInizio)
                .ThenBy(p => p.Id)
                .ToListAsync();
        }

        // Caricate tracciate e salvate con un solo SaveChanges, come AggiornaStatoAsync: si toccano
        // solo le colonne che cambiano. Niente ExecuteUpdate: il database finto dei test non lo supporta.
        public async Task<int> StaccaDaUtenteAsync(string userId, string nomeAnonimo)
        {
            var prenotazioni = await _context.Prenotazioni
                .Where(p => p.UserId == userId)
                .ToListAsync();

            foreach (var p in prenotazioni)
            {
                if (string.IsNullOrWhiteSpace(p.NomeCliente))
                {
                    p.NomeCliente = nomeAnonimo;
                    p.Note = null;
                }
                p.UserId = null;
            }

            await _context.SaveChangesAsync();
            return prenotazioni.Count;
        }

        // REV-022: le entita' vengono caricate tracciate in un'unica query e salvate con un solo
        // SaveChanges. Si evita di proposito UpdateRange su entita' staccate: quel metodo marca
        // Modified l'intero grafo raggiungibile, quindi riscriverebbe anche le fasce orarie
        // caricate con Include. Qui si tocca solo la colonna che cambia davvero.
        public async Task<int> AggiornaStatoAsync(IReadOnlyCollection<long> ids, StatoPrenotazione nuovoStato)
        {
            if (ids.Count == 0)
                return 0;

            var prenotazioni = await _context.Prenotazioni
                .Where(p => ids.Contains(p.Id))
                .ToListAsync();

            foreach (var prenotazione in prenotazioni)
                prenotazione.Stato = nuovoStato;

            await _context.SaveChangesAsync();

            return prenotazioni.Count;
        }

        public async Task<int> EliminaPerIdAsync(IReadOnlyCollection<long> ids)
        {
            if (ids.Count == 0)
                return 0;

            var prenotazioni = await _context.Prenotazioni
                .Where(p => ids.Contains(p.Id))
                .ToListAsync();

            _context.Prenotazioni.RemoveRange(prenotazioni);
            await _context.SaveChangesAsync();

            return prenotazioni.Count;
        }

        public async Task<Prenotazione?> GetTrackedByIdAsync(long id)
        {
            return await _context.Prenotazioni
                .Include(p => p.PrenotazioniPostazioni)
                .Include(p => p.FasciaOraria)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        /// <summary>
        /// AUD-M4: SELECT ... FOR UPDATE sulla riga della prenotazione. Due operazioni sulla
        /// stessa prenotazione (modifica e annullamento, conferma e job) passano una alla volta:
        /// la seconda legge lo stato lasciato dalla prima invece di sovrascriverlo. Va chiamato
        /// dentro una transazione, prima di leggere l'entita'. Il provider InMemory dei test non
        /// esegue SQL: li' il lock non si puo' provare e viene saltato.
        /// </summary>
        public async Task BloccaPerModificaAsync(long id)
        {
            if (!_context.Database.IsRelational())
                return;

            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Prenotazioni\" WHERE \"Id\" = {id} FOR UPDATE");
        }

        public async Task<List<Prenotazione>> GetPrenotazioniByDataAsync(DateOnly data)
        {
            return await _context.Prenotazioni
                .AsNoTracking()
                .Where(p => p.DataPrenotazione == data
                         && p.Stato != StatoPrenotazione.Annullata
                         && p.Stato != StatoPrenotazione.NonPresentata)
                .Include(p => p.PrenotazioniPostazioni)
                .ThenInclude(pp => pp.Postazione)
                .ToListAsync();
        }

        public async Task<List<Prenotazione>> GetAllPrenotazioniAsync()
        {
            return await _context.Prenotazioni
                .AsNoTracking()
                .OrderBy(p => p.DataPrenotazione)
                .ToListAsync();
        }

        public async Task<List<FasciaOraria>> GetFasceOrarieByDayAsync(DayOfWeek giorno)
        {
            return await _context.FasciaOrarie
                .Where(f => f.Attiva && f.GiornoSettimana == giorno)
                .OrderBy(f => f.OrarioInizio)
                .ToListAsync();
        }
    }
}
