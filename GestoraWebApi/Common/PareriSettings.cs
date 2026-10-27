namespace GestoraWebApi.Common
{
    public class PareriSettings
    {
        public const string Sezione = "Pareri";

        /// <summary>
        /// V2-022: a chi arrivano i pareri del modulo pubblico (su Azure <c>Pareri__Destinatario</c>).
        /// Fuori da Development è obbligatorio, controllato all'avvio: senza, i pareri andrebbero persi.
        /// In locale si imposta con <c>dotnet user-secrets</c>.
        /// </summary>
        public string Destinatario { get; set; } = string.Empty;
    }
}
