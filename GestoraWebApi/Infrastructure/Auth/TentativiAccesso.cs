using GestoraWebApi.Common;
using Microsoft.Extensions.Caching.Memory;

namespace GestoraWebApi.Infrastructure.Auth
{
    /// <summary>
    /// V2-027 — conta le password sbagliate per <b>email + indirizzo IP</b>, non per account.
    ///
    /// Prima si usava il blocco di Identity (5 errori = account bloccato 15 minuti, chiunque li
    /// avesse fatti): chi conosceva l'email dell'Admin poteva tenerlo chiuso fuori sbagliando la
    /// password apposta. Qui si ferma chi sbaglia, da quell'indirizzo: l'Admin vero, da un altro
    /// indirizzo, entra lo stesso.
    ///
    /// Si contano anche le email che non esistono, così il blocco non rivela quali indirizzi sono
    /// registrati. Contro chi prova molte email dallo stesso indirizzo resta <c>LoginPolicy</c>
    /// (5 richieste al minuto per indirizzo, in Program.cs).
    ///
    /// Il contatore sta in memoria: un riavvio lo azzera. Va bene con una sola istanza del backend;
    /// con più istanze ognuna avrebbe il suo (vedi la nota su Quartz e le repliche).
    /// </summary>
    public interface ITentativiAccesso
    {
        /// <summary>True se da questo indirizzo, su questa email, si è sbagliato troppo di recente.</summary>
        bool Bloccato(string? email, string? ip);

        /// <summary>Conta una password sbagliata (o un'email inesistente). Al quinto errore scatta il blocco.</summary>
        void RegistraErrore(string? email, string? ip);

        /// <summary>Accesso riuscito: si riparte da zero.</summary>
        void Azzera(string? email, string? ip);
    }

    public class TentativiAccesso : ITentativiAccesso
    {
        /// <summary>Errori ammessi prima del blocco: gli stessi 5 del blocco di Identity di prima.</summary>
        public const int ErroriMassimi = 5;

        /// <summary>Quanto dura il blocco, e quanto a lungo si ricordano gli errori dall'ultimo.</summary>
        public static readonly TimeSpan Durata = TimeSpan.FromMinutes(15);

        private readonly IMemoryCache _cache;
        private readonly IClock _clock;
        private readonly object _lucchetto = new();

        public TentativiAccesso(IMemoryCache cache, IClock clock)
        {
            _cache = cache;
            _clock = clock;
        }

        private sealed class Stato
        {
            public int Errori;
            public DateTime? BloccatoFino;
        }

        // L'email si confronta senza maiuscole e spazi, come la cerca Identity: «Mario@x.it » e
        // «mario@x.it» sono lo stesso bersaglio.
        private static string Chiave(string? email, string? ip) =>
            $"tentativi-accesso|{(email ?? string.Empty).Trim().ToUpperInvariant()}|{ip ?? "sconosciuto"}";

        public bool Bloccato(string? email, string? ip)
        {
            lock (_lucchetto)
            {
                return _cache.TryGetValue(Chiave(email, ip), out Stato? stato)
                       && stato!.BloccatoFino > _clock.UtcNow;
            }
        }

        public void RegistraErrore(string? email, string? ip)
        {
            var chiave = Chiave(email, ip);
            lock (_lucchetto)
            {
                var stato = _cache.TryGetValue(chiave, out Stato? letto) ? letto! : new Stato();

                // Un blocco scaduto non conta più: si riparte da zero, non da un errore al blocco.
                if (stato.BloccatoFino is { } fino && fino <= _clock.UtcNow)
                    stato = new Stato();

                stato.Errori++;
                if (stato.Errori >= ErroriMassimi)
                    stato.BloccatoFino = _clock.UtcNow.Add(Durata);

                _cache.Set(chiave, stato, Durata);
            }
        }

        public void Azzera(string? email, string? ip)
        {
            lock (_lucchetto)
            {
                _cache.Remove(Chiave(email, ip));
            }
        }
    }
}
