namespace GestoraWebApi.Common
{
    public class InoltroVercelSettings
    {
        public const string Sezione = "InoltroVercel";

        /// <summary>
        /// V2-028: la parola segreta che Vercel aggiunge a ogni chiamata a /api che inoltra ad Azure
        /// (header <c>X-Gestora-Inoltro</c>). Solo con questa il backend si fida dell'indirizzo del
        /// visitatore scritto da Vercel. Obbligatoria fuori da Development, almeno 32 caratteri; su
        /// Azure <c>InoltroVercel__Segreto</c>, su Vercel la stessa in <c>INOLTRO_SEGRETO</c>.
        /// </summary>
        public string Segreto { get; set; } = string.Empty;

        /// <summary>
        /// Con true le chiamate a /api che non passano da Vercel (senza la parola segreta) ricevono
        /// 403. Spento di default: si accende quando il sito chiama solo attraverso Vercel. Swagger e
        /// curl diretti ad Azure smettono di funzionare.
        /// </summary>
        public bool BloccaChiamateDirette { get; set; }
    }
}
