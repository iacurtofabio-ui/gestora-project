using GestoraWebApi.Auth;

namespace GestoraWebApi.Repositories.Utenti
{
    public interface IUtentiRepository
    {
        /// <summary>
        /// V2-015: utenze mai usate, create prima di <paramref name="creatiPrimaDi"/> (UTC), dalla
        /// più vecchia, al massimo <paramref name="massimo"/>. Regola completa sull'implementazione.
        /// </summary>
        Task<List<ApplicationUser>> GetUtenzeMaiUsateAsync(DateTime creatiPrimaDi, int massimo);

        /// <summary>
        /// V2-019: Clienti entrati l'ultima volta prima di <paramref name="ultimoAccessoPrimaDi"/> e
        /// non ancora avvisati, dal più vecchio, al massimo <paramref name="massimo"/>.
        /// </summary>
        Task<List<ApplicationUser>> GetInattiviDaAvvisareAsync(DateTime ultimoAccessoPrimaDi, int massimo);

        /// <summary>
        /// V2-019: Clienti entrati l'ultima volta prima di <paramref name="ultimoAccessoPrimaDi"/> e
        /// avvisati prima di <paramref name="avvisatiPrimaDi"/>, dal più vecchio, al massimo
        /// <paramref name="massimo"/>.
        /// </summary>
        Task<List<ApplicationUser>> GetInattiviDaEliminareAsync(DateTime ultimoAccessoPrimaDi, DateTime avvisatiPrimaDi, int massimo);
    }
}
