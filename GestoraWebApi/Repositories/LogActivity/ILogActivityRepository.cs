using GestoraWebApi.Models;

namespace GestoraWebApi.Repositories.LogActivity
{
    public interface ILogActivityRepository
    {
        IQueryable<Logging> GetAllQueryable();
        Task AddAsync(Logging log);

        /// <summary>
        /// V2-019: elimina al massimo <paramref name="massimo"/> righe più vecchie di
        /// <paramref name="limiteUtc"/>, dalle più vecchie. Restituisce quante ne ha eliminate.
        /// </summary>
        Task<int> EliminaPrimaDiAsync(DateTime limiteUtc, int massimo);
    }
}
