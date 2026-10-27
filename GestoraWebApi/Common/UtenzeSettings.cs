namespace GestoraWebApi.Common
{
    public class UtenzeSettings
    {
        public const string Sezione = "Utenze";

        /// <summary>
        /// V2-015: dopo quanti giorni dalla registrazione un Cliente mai entrato e senza
        /// prenotazioni viene eliminato dalla pulizia notturna. Controllato all'avvio (&gt; 0).
        /// </summary>
        public int GiorniUtenzaMaiUsata { get; set; }

        /// <summary>
        /// V2-019: dopo quanti mesi senza accessi un Cliente viene eliminato (24, come dice
        /// l'informativa privacy). Controllato all'avvio (&gt; 0).
        /// </summary>
        public int MesiInattivita { get; set; }

        /// <summary>
        /// V2-019: quante settimane prima dell'eliminazione parte l'email di avviso, ed è anche il
        /// tempo minimo fra avviso ed eliminazione. Controllato all'avvio (&gt; 0).
        /// </summary>
        public int SettimaneAvviso { get; set; }
    }
}
