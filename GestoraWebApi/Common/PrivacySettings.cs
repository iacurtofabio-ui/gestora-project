namespace GestoraWebApi.Common
{
    public class PrivacySettings
    {
        public const string Sezione = "Privacy";

        /// <summary>
        /// V2-019: versione dell'informativa privacy in vigore (es. «2026-10-26»). Va cambiata
        /// insieme a INFORMATIVA_VERSIONE del frontend (lib/privacy.ts): chi ha accettato una
        /// versione diversa la rivede al primo accesso. Controllata all'avvio (1-20 caratteri).
        /// </summary>
        public string Versione { get; set; } = string.Empty;
    }
}
