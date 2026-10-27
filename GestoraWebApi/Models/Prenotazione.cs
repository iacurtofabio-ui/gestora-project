using GestoraWebApi.Auth;
using GestoraWebApi.Enums;

namespace GestoraWebApi.Models
{
    public class Prenotazione : Entita
    {
        public DateOnly DataPrenotazione { get; set; }
        public required int NumeroCoperti { get; set; }
        public StatoPrenotazione Stato { get; set; } = StatoPrenotazione.Attiva;
        public string? Note { get; set; }

        // Valorizzato solo quando la prenotazione è creata da Staff/Admin per conto di un
        // cliente senza account (es. telefonata) — la prenotazione resta comunque legata
        // a UserId di chi l'ha creata, questo campo serve solo a identificare chi si presenta.
        public string? NomeCliente { get; set; }

        // V2-019: null quando l'account che l'ha creata è stato cancellato. La prenotazione resta
        // (conteggi del locale veri) con NomeCliente «Cliente eliminato», senza dati personali.
        public string? UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }

        public long FasciaOrariaId { get; set; }
        public virtual FasciaOraria FasciaOraria { get; set; }


        public ICollection<PrenotazionePostazione> PrenotazioniPostazioni { get; set; }
    }
}
