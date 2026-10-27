using System.Security.Claims;
using System.Threading.RateLimiting;
using GestoraWebApi.Auth;
using GestoraWebApi.Common;

namespace GestoraWebApi.Infrastructure.RateLimiting
{
    /// <summary>
    /// V2-011: limiti di richieste sugli endpoint pubblici che non ne avevano (registrazione e
    /// verifica della disponibilità). Il login ha già <c>LoginPolicy</c> in <c>Program.cs</c>.
    /// <para>
    /// Qui sta solo la scelta di <b>chi</b> si conta e <b>con quale regola</b>: è logica che si
    /// prova con un test, senza avviare l'applicazione. I numeri vengono dalla configurazione
    /// (<see cref="LimitiRichiesteSettings"/>).
    /// </para>
    /// </summary>
    public static class LimitiRichieste
    {
        public const string Registrazione = "RegistrazionePolicy";
        public const string Disponibilita = "DisponibilitaPolicy";
        public const string ConfermaEmail = "ConfermaEmailPolicy";

        /// <summary>V2-019: password-dimenticata e reimposta-password, con i numeri della conferma email.</summary>
        public const string RecuperoPassword = "RecuperoPasswordPolicy";

        /// <summary>
        /// V2-003: conferma email e reinvio del link. Finestra scorrevole per indirizzo, come la
        /// registrazione ma con un contatore proprio. Nessuna esenzione per l'Admin: non usa
        /// mai questi endpoint.
        /// </summary>
        public static RateLimitPartition<string> PartizioneConfermaEmail(
            HttpContext httpContext, LimitiRichiesteSettings limiti)
        {
            return RateLimitPartition.GetSlidingWindowLimiter(
                "ip:" + Indirizzo(httpContext),
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = limiti.ConfermaEmailPerFinestra,
                    Window = TimeSpan.FromMinutes(limiti.ConfermaEmailFinestraMinuti),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0
                });
        }

        /// <summary>
        /// Registrazione: <b>finestra scorrevole</b> per indirizzo. A differenza della finestra
        /// fissa del login, non si possono sommare due finestre a cavallo del cambio (fino al
        /// doppio delle richieste in pochi secondi): conta sempre l'ultima ora intera.
        /// <para>
        /// L'Admin non ha limiti: crea gli account dello Staff passando dallo stesso endpoint
        /// (<c>useCreateUser</c> nel frontend), di solito dal wifi del locale, e non deve
        /// consumare il margine dei clienti né restarne bloccato.
        /// </para>
        /// </summary>
        public static RateLimitPartition<string> PartizioneRegistrazione(
            HttpContext httpContext, LimitiRichiesteSettings limiti)
        {
            if (httpContext.User.IsInRole(Roles.Admin))
                return RateLimitPartition.GetNoLimiter("admin");

            return RateLimitPartition.GetSlidingWindowLimiter(
                "ip:" + Indirizzo(httpContext),
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = limiti.RegistrazioniPerFinestra,
                    Window = TimeSpan.FromMinutes(limiti.RegistrazioneFinestraMinuti),
                    // La finestra avanza a passi di un sesto (10 minuti su un'ora): abbastanza
                    // fine da non fare differenza per chi usa l'app.
                    SegmentsPerWindow = 6,
                    QueueLimit = 0
                });
        }

        /// <summary>
        /// Disponibilità: <b>secchiello di gettoni</b> (token bucket). Si parte con
        /// <see cref="LimitiRichiesteSettings.DisponibilitaRaffica"/> gettoni, ogni verifica ne
        /// consuma uno e se ne recuperano <see cref="LimitiRichiesteSettings.DisponibilitaAlMinuto"/>
        /// al minuto. È la forma adatta a un endpoint di ricerca: chi compila il form fa molte
        /// verifiche in pochi secondi e poi si ferma, uno script invece non si ferma mai.
        /// <para>
        /// Chi ha fatto l'accesso si conta per <b>utente</b>, non per indirizzo: lo Staff lavora
        /// tutto dallo stesso wifi del locale, e contato per indirizzo un collega che compila un
        /// form esaurirebbe i gettoni dell'altro nel pieno del servizio.
        /// </para>
        /// </summary>
        public static RateLimitPartition<string> PartizioneDisponibilita(
            HttpContext httpContext, LimitiRichiesteSettings limiti)
        {
            var userId = httpContext.User.FindFirstValue("sub")
                      ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            var chiave = string.IsNullOrWhiteSpace(userId)
                ? "ip:" + Indirizzo(httpContext)
                : "utente:" + userId;

            return RateLimitPartition.GetTokenBucketLimiter(
                chiave,
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = limiti.DisponibilitaRaffica,
                    // Un gettone alla volta, a intervalli regolari: con 30 al minuto, uno ogni
                    // 2 secondi, invece di 30 tutti insieme allo scoccare del minuto.
                    TokensPerPeriod = 1,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(60.0 / limiti.DisponibilitaAlMinuto),
                    AutoReplenishment = true,
                    QueueLimit = 0
                });
        }

        // REV-029: l'indirizzo del client, non quello del proxy di Azure (uguale per tutti).
        private static string Indirizzo(HttpContext httpContext) =>
            IndirizzoClient.Ottieni(httpContext) ?? "unknown";
    }
}
