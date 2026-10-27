using GestoraWebApi.Context;
using GestoraWebApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading;

namespace GestoraWebApi.Repositories.LogActivity
{
    public class LogActivityRepository : ILogActivityRepository
    {
        private readonly GestoraContext _context;
        private readonly DbSet<Logging> _dbSet;

        public LogActivityRepository(GestoraContext context)
        {
            _context = context;
            _dbSet = _context.Set<Logging>();
        }

        public IQueryable<Logging> GetAllQueryable()
        {
            return _dbSet.AsNoTracking();
        }

        public async Task AddAsync(Logging log)
        {
            await _dbSet.AddAsync(log);
            await _context.SaveChangesAsync();
        }

        // A blocchi, come la pulizia delle utenze: la prima esecuzione dopo anni potrebbe trovare
        // molte righe, e un solo SaveChanges enorme terrebbe la memoria e il database occupati.
        // Niente ExecuteDelete: il database finto dei test non lo supporta.
        public async Task<int> EliminaPrimaDiAsync(DateTime limiteUtc, int massimo)
        {
            var vecchie = await _dbSet
                .Where(l => l.Timestamp < limiteUtc)
                .OrderBy(l => l.Timestamp).ThenBy(l => l.Id)
                .Take(massimo)
                .ToListAsync();

            _dbSet.RemoveRange(vecchie);
            await _context.SaveChangesAsync();
            return vecchie.Count;
        }
    }
}
