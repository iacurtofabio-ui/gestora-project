namespace GestoraWebApi.Common
{
    /// <summary>
    /// V2-011: quante richieste accettano gli endpoint pubblici prima di rispondere 429.
    /// In configurazione e non nel codice: sono numeri da tarare sull'uso reale, e su Azure si
    /// cambiano senza ripubblicare (<c>LimitiRichieste__...</c>). Controllati all'avvio.
    /// </summary>
    public class LimitiRichiesteSettings
    {
        public const string Sezione = "LimitiRichieste";

        /// <summary>
        /// Registrazioni ammesse per indirizzo nella finestra. Un utente vero si registra una
        /// volta, con qualche errore di compilazione: il margine copre anche più persone sullo
        /// stesso wifi o sulla stessa rete mobile.
        /// </summary>
        public int RegistrazioniPerFinestra { get; set; }

        /// <summary>Durata della finestra della registrazione, in minuti.</summary>
        public int RegistrazioneFinestraMinuti { get; set; }

        /// <summary>
        /// V2-003: richieste ammesse per indirizzo, nella finestra, su conferma dell'email e
        /// reinvio del link. Limite a parte dalla registrazione: chi si registra ne consuma una
        /// per sé più qualche reinvio, e il reinvio manda una email a un indirizzo scelto da chi
        /// chiama, quindi non può restare senza freno.
        /// </summary>
        public int ConfermaEmailPerFinestra { get; set; }

        /// <summary>Durata della finestra della conferma email, in minuti.</summary>
        public int ConfermaEmailFinestraMinuti { get; set; }

        /// <summary>
        /// Verifiche di disponibilità di fila, senza attesa. Il form di prenotazione la chiama a
        /// ogni cambio di data o coperti, quindi chi compila ne fa parecchie in pochi secondi.
        /// </summary>
        public int DisponibilitaRaffica { get; set; }

        /// <summary>Verifiche al minuto che si recuperano dopo la raffica (ritmo sostenuto).</summary>
        public int DisponibilitaAlMinuto { get; set; }

        /// <summary>
        /// V2-022: pareri ammessi per indirizzo nella finestra. Chi scrive un parere lo fa una volta,
        /// raramente due: oltre è quasi certamente spam, e ogni parere è una email nella casella
        /// di chi gestisce Gestora.
        /// </summary>
        public int PareriPerFinestra { get; set; }

        /// <summary>Durata della finestra dei pareri, in giorni.</summary>
        public int PareriFinestraGiorni { get; set; }
    }
}
