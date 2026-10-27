namespace GestoraWebApi.Common
{
    public class RegistroAttivitaSettings
    {
        public const string Sezione = "RegistroAttivita";

        /// <summary>
        /// V2-019: per quanti mesi si tengono le righe del registro attività (con gli indirizzi IP);
        /// le più vecchie le elimina il job notturno. Scritto anche nell'informativa privacy:
        /// cambiandolo va cambiato il testo. Controllato all'avvio (&gt; 0).
        /// </summary>
        public int MesiConservazione { get; set; }
    }
}
