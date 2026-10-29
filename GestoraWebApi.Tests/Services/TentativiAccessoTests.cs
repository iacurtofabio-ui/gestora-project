using GestoraWebApi.Infrastructure.Auth;
using Microsoft.Extensions.Caching.Memory;

namespace GestoraWebApi.Tests.Services
{
    /// <summary>
    /// V2-027 — il blocco dopo 5 password sbagliate riguarda email + indirizzo di chi sbaglia, non
    /// l'account: l'Admin vero, da un altro indirizzo, deve poter entrare anche mentre qualcuno sta
    /// provando password a suo nome.
    /// </summary>
    public class TentativiAccessoTests
    {
        private const string Email = "admin@locale.it";
        private const string Attaccante = "203.0.113.7";
        private const string Admin = "198.51.100.20";

        private readonly TestClock _clock = new(new DateTime(2026, 10, 29, 20, 0, 0, DateTimeKind.Utc));
        private readonly TentativiAccesso _tentativi;

        public TentativiAccessoTests()
        {
            _tentativi = new TentativiAccesso(new MemoryCache(new MemoryCacheOptions()), _clock);
        }

        private void Sbaglia(int volte, string email = Email, string ip = Attaccante)
        {
            for (var i = 0; i < volte; i++)
                _tentativi.RegistraErrore(email, ip);
        }

        [Fact]
        public void QuattroErrori_NonBlocca()
        {
            Sbaglia(TentativiAccesso.ErroriMassimi - 1);

            Assert.False(_tentativi.Bloccato(Email, Attaccante));
        }

        [Fact]
        public void CinqueErrori_BloccaChiSbaglia()
        {
            Sbaglia(TentativiAccesso.ErroriMassimi);

            Assert.True(_tentativi.Bloccato(Email, Attaccante));
        }

        [Fact]
        public void CinqueErroriDiUnAltro_LAdminDaUnAltroIndirizzoEntra()
        {
            Sbaglia(TentativiAccesso.ErroriMassimi);

            Assert.False(_tentativi.Bloccato(Email, Admin));
        }

        [Fact]
        public void BloccoSuUnEmail_NonTocca_LeAltreEmailDalloStessoIndirizzo()
        {
            Sbaglia(TentativiAccesso.ErroriMassimi);

            Assert.False(_tentativi.Bloccato("staff@locale.it", Attaccante));
        }

        [Fact]
        public void EmailScrittaDiversa_StessoBersaglio()
        {
            Sbaglia(TentativiAccesso.ErroriMassimi, email: " ADMIN@Locale.it ");

            Assert.True(_tentativi.Bloccato(Email, Attaccante));
        }

        [Fact]
        public void DopoQuindiciMinuti_SiRiparteDaZero()
        {
            Sbaglia(TentativiAccesso.ErroriMassimi);
            _clock.UtcNow = _clock.UtcNow.Add(TentativiAccesso.Durata);

            Assert.False(_tentativi.Bloccato(Email, Attaccante));

            // Un errore dopo la scadenza non riblocca subito: il conto è ripartito.
            _tentativi.RegistraErrore(Email, Attaccante);
            Assert.False(_tentativi.Bloccato(Email, Attaccante));
        }

        [Fact]
        public void AccessoRiuscito_AzzeraGliErrori()
        {
            Sbaglia(TentativiAccesso.ErroriMassimi - 1);
            _tentativi.Azzera(Email, Attaccante);
            Sbaglia(TentativiAccesso.ErroriMassimi - 1);

            Assert.False(_tentativi.Bloccato(Email, Attaccante));
        }
    }
}
