namespace GestoraWebApi.Common
{
    /// <summary>
    /// V2-003: configurazione dell'invio delle email (SMTP). Il fornitore (oggi Brevo) si cambia
    /// solo con questi valori. In sviluppo, con <see cref="Host"/> vuoto, le email non partono: il
    /// link finisce nei log. Fuori da Development l'Host è obbligatorio, controllato all'avvio.
    /// </summary>
    public class EmailSettings
    {
        public const string Sezione = "Email";

        public string Host { get; set; } = string.Empty;
        public int Porta { get; set; } = 587;
        public string Utente { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string MittenteEmail { get; set; } = string.Empty;
        public string MittenteNome { get; set; } = "Gestora";

        /// <summary>Indirizzo del frontend, usato per costruire i link nelle email (senza "/" finale).</summary>
        public string UrlFrontend { get; set; } = string.Empty;

        /// <summary>
        /// V3-001: solo sviluppo, con <see cref="Host"/> vuoto. Se valorizzata, ogni email si salva
        /// anche come file in questa cartella: le prove nel browser leggono da lì il link.
        /// </summary>
        public string CartellaSviluppo { get; set; } = string.Empty;
    }
}
