using GestoraWebApi.Auth;

namespace GestoraWebApi.Services.Privacy
{
    /// <summary>
    /// V2-019 — regola della presa visione dell'informativa privacy. Pura, senza database: la
    /// usano la registrazione e gli endpoint stato-privacy/accetta-privacy, e si prova da sola.
    /// </summary>
    public static class PresaVisionePrivacy
    {
        /// <summary>
        /// Vero se l'utente non ha mai preso visione dell'informativa (account creati da un Admin,
        /// primo Admin, account precedenti alla F3) o ha accettato una versione diversa da quella
        /// in vigore.
        /// </summary>
        public static bool DaAccettare(ApplicationUser user, string versioneInVigore) =>
            user.PrivacyAccettataIl is null
            || !string.Equals(user.PrivacyVersione, versioneInVigore, StringComparison.Ordinal);

        /// <summary>Segna la presa visione della versione in vigore, all'istante dato (UTC).</summary>
        public static void Registra(ApplicationUser user, string versioneInVigore, DateTime istanteUtc)
        {
            user.PrivacyAccettataIl = istanteUtc;
            user.PrivacyVersione = versioneInVigore;
        }
    }
}
