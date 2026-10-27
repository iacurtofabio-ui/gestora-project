using GestoraWebApi.Auth;
using GestoraWebApi.Context;
using Microsoft.EntityFrameworkCore;

namespace GestoraWebApi.Repositories.Utenti
{
    public class UtentiRepository : IUtentiRepository
    {
        private readonly GestoraContext _context;

        public UtentiRepository(GestoraContext context)
        {
            _context = context;
        }

        /// <summary>
        /// V2-015: un'utenza è "mai usata" quando valgono tutte e quattro le condizioni:
        /// <list type="number">
        /// <item>non ha nessun ruolo diverso da Cliente: Staff e Admin non si toccano mai, nemmeno
        /// se hanno anche Cliente (uno Staff appena creato dall'Admin può non essere ancora entrato);</item>
        /// <item>non ha mai fatto login;</item>
        /// <item>è stata creata prima della soglia;</item>
        /// <item>non ha nessuna prenotazione, in nessuno stato (la FK Restrict lo impedirebbe comunque).</item>
        /// </list>
        /// Le righe sono tracciate: il chiamante le passa a <c>UserManager.DeleteAsync</c>.
        /// </summary>
        public async Task<List<ApplicationUser>> GetUtenzeMaiUsateAsync(DateTime creatiPrimaDi, int massimo)
        {
            return await SoloClienti()
                .Where(u => u.UltimoAccesso == null
                         && u.DataCreazione < creatiPrimaDi
                         && !_context.Prenotazioni.Any(p => p.UserId == u.Id))
                .OrderBy(u => u.DataCreazione)
                .ThenBy(u => u.Id)
                .Take(massimo)
                .ToListAsync();
        }

        /// <summary>
        /// V2-019: stesso perimetro delle utenze mai usate (solo Clienti, Staff e Admin mai), ma per
        /// chi è entrato almeno una volta. Chi rientra torna con l'avviso a null (vedi il login) e
        /// non compare più finché non torna inattivo.
        /// </summary>
        public async Task<List<ApplicationUser>> GetInattiviDaAvvisareAsync(DateTime ultimoAccessoPrimaDi, int massimo)
        {
            return await SoloClienti()
                .Where(u => u.UltimoAccesso != null
                         && u.UltimoAccesso < ultimoAccessoPrimaDi
                         && u.AvvisoInattivitaInviatoIl == null)
                .OrderBy(u => u.UltimoAccesso)
                .ThenBy(u => u.Id)
                .Take(massimo)
                .ToListAsync();
        }

        /// <summary>
        /// V2-019: si elimina solo chi è stato avvisato e ha avuto il tempo di rientrare: se il job
        /// è rimasto fermo, l'avviso tardivo sposta l'eliminazione invece di farla il giorno dopo.
        /// </summary>
        public async Task<List<ApplicationUser>> GetInattiviDaEliminareAsync(DateTime ultimoAccessoPrimaDi, DateTime avvisatiPrimaDi, int massimo)
        {
            return await SoloClienti()
                .Where(u => u.UltimoAccesso != null
                         && u.UltimoAccesso < ultimoAccessoPrimaDi
                         && u.AvvisoInattivitaInviatoIl != null
                         && u.AvvisoInattivitaInviatoIl < avvisatiPrimaDi)
                .OrderBy(u => u.UltimoAccesso)
                .ThenBy(u => u.Id)
                .Take(massimo)
                .ToListAsync();
        }

        /// <summary>Utenti senza ruoli diversi da Cliente. Righe tracciate: il chiamante le aggiorna o le elimina.</summary>
        private IQueryable<ApplicationUser> SoloClienti()
        {
            var ruoliProtetti = _context.Roles
                .Where(r => r.Name != Roles.Cliente)
                .Select(r => r.Id);

            return _context.Users
                .Where(u => !_context.UserRoles.Any(ur => ur.UserId == u.Id && ruoliProtetti.Contains(ur.RoleId)));
        }
    }
}
