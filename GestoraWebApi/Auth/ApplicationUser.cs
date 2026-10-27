using GestoraWebApi.Models;
using Microsoft.AspNetCore.Identity;

namespace GestoraWebApi.Auth
{
    public class ApplicationUser : IdentityUser
    {
        public virtual ICollection<Prenotazione> Prenotazioni { get; set; } = new List<Prenotazione>();

        /// <summary>
        /// V2-015: quando l'utenza è stata creata (UTC). Va valorizzata in ogni punto che crea un
        /// utente: è da qui che la pulizia notturna conta i giorni di un'utenza mai usata.
        /// </summary>
        public DateTime DataCreazione { get; set; }

        /// <summary>
        /// V2-015: ultimo login riuscito (UTC), <c>null</c> se l'utente non è mai entrato. Il
        /// rinnovo del token non lo aggiorna: serve a sapere se l'utenza è mai stata usata.
        /// </summary>
        public DateTime? UltimoAccesso { get; set; }

        /// <summary>
        /// V2-019: quando l'utente ha preso visione dell'informativa privacy (UTC), <c>null</c> se
        /// non l'ha ancora fatto (account creati prima della F3 o da un Admin).
        /// </summary>
        public DateTime? PrivacyAccettataIl { get; set; }

        /// <summary>
        /// V2-019: versione dell'informativa accettata. Se l'informativa cambia, chi ha una versione
        /// diversa la rivede al primo accesso.
        /// </summary>
        public string? PrivacyVersione { get; set; }

        /// <summary>
        /// V2-019: quando è partita l'email di avviso per inattività (UTC). Si invia una volta sola;
        /// torna <c>null</c> quando l'utente rientra.
        /// </summary>
        public DateTime? AvvisoInattivitaInviatoIl { get; set; }
    }
}
