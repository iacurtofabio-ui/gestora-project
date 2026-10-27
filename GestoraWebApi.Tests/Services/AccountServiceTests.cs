using GestoraWebApi.Auth;
using GestoraWebApi.Enums;
using GestoraWebApi.Infrastructure.Exceptions;
using GestoraWebApi.Models;
using GestoraWebApi.Services.Account;
using GestoraWebApi.Services.LogActivity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GestoraWebApi.Tests.Services
{
    /// <summary>V2-019: «Il mio account». Lo UserManager è finto: si prova la logica del servizio.</summary>
    public class AccountServiceTests
    {
        private const string Ip = "10.0.0.1";

        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly Mock<ILogActivityService> _log = new();
        private readonly Mock<IEliminazioneAccountService> _eliminazione = new();
        private readonly AccountService _service;
        private readonly ApplicationUser _utente = new()
        {
            Id = "utente-1",
            UserName = "Mario",
            Email = "mario@example.com",
            DataCreazione = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            PrivacyVersione = "2026-10-26"
        };

        public AccountServiceTests()
        {
            _userManager = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
            _userManager.Setup(m => m.FindByIdAsync("utente-1")).ReturnsAsync(_utente);
            _service = new AccountService(_userManager.Object, _log.Object, _eliminazione.Object,
                NullLogger<AccountService>.Instance);
        }

        private static string CampoErrore(ValidationException ex) => Assert.Single(ex.Errors).Key;

        [Fact]
        public async Task MioAccount_DatiERuoliDellUtente()
        {
            _userManager.Setup(m => m.GetRolesAsync(_utente)).ReturnsAsync(new List<string> { "Cliente" });

            var account = await _service.GetMioAccountAsync("utente-1");

            Assert.Equal("Mario", account.UserName);
            Assert.Equal("mario@example.com", account.Email);
            Assert.Equal(new[] { "Cliente" }, account.Ruoli);
            Assert.Equal("2026-10-26", account.PrivacyVersione);
        }

        [Fact]
        public async Task MioAccount_UtenteSparito_NotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetMioAccountAsync("altro"));
        }

        [Fact]
        public async Task CambiaPassword_Ok_AzzeraITentativiERegistra()
        {
            _userManager.Setup(m => m.IsLockedOutAsync(_utente)).ReturnsAsync(false);
            _userManager.Setup(m => m.CheckPasswordAsync(_utente, "Vecchia1!")).ReturnsAsync(true);
            _userManager.Setup(m => m.ChangePasswordAsync(_utente, "Vecchia1!", "Nuova1!pass")).ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(m => m.ResetAccessFailedCountAsync(_utente)).ReturnsAsync(IdentityResult.Success);

            var user = await _service.CambiaPasswordAsync("utente-1", "Vecchia1!", "Nuova1!pass", Ip);

            Assert.Same(_utente, user);
            _userManager.Verify(m => m.ResetAccessFailedCountAsync(_utente), Times.Once);
            _log.Verify(l => l.LogAsync("utente-1", It.IsAny<string>(), Ip), Times.Once);
        }

        [Fact]
        public async Task CambiaPassword_AttualeSbagliata_ContaIlTentativoEErroreSulCampo()
        {
            _userManager.Setup(m => m.IsLockedOutAsync(_utente)).ReturnsAsync(false);
            _userManager.Setup(m => m.CheckPasswordAsync(_utente, "Sbagliata1!")).ReturnsAsync(false);
            _userManager.Setup(m => m.AccessFailedAsync(_utente)).ReturnsAsync(IdentityResult.Success);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                _service.CambiaPasswordAsync("utente-1", "Sbagliata1!", "Nuova1!pass", Ip));

            Assert.Equal("passwordAttuale", CampoErrore(ex));
            _userManager.Verify(m => m.AccessFailedAsync(_utente), Times.Once);
            _userManager.Verify(m => m.ChangePasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CambiaPassword_AccountBloccato_NonProvaNemmenoLaPassword()
        {
            _userManager.Setup(m => m.IsLockedOutAsync(_utente)).ReturnsAsync(true);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                _service.CambiaPasswordAsync("utente-1", "Vecchia1!", "Nuova1!pass", Ip));

            Assert.Equal("passwordAttuale", CampoErrore(ex));
            _userManager.Verify(m => m.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CambiaPassword_NuovaRifiutataDaIdentity_ErroreSullaPassword()
        {
            _userManager.Setup(m => m.IsLockedOutAsync(_utente)).ReturnsAsync(false);
            _userManager.Setup(m => m.CheckPasswordAsync(_utente, "Vecchia1!")).ReturnsAsync(true);
            _userManager.Setup(m => m.ChangePasswordAsync(_utente, "Vecchia1!", It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityErrorDescriber().PasswordRequiresDigit()));

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                _service.CambiaPasswordAsync("utente-1", "Vecchia1!", "SenzaNumeri!", Ip));

            Assert.Equal("password", CampoErrore(ex));
        }

        // --- Cancellazione dell'account ---

        private void PasswordGiusta(bool admin = false, int adminTotali = 1, params Prenotazione[] daServire)
        {
            _userManager.Setup(m => m.IsLockedOutAsync(_utente)).ReturnsAsync(false);
            _userManager.Setup(m => m.CheckPasswordAsync(_utente, "Mia1!pass")).ReturnsAsync(true);
            _userManager.Setup(m => m.IsInRoleAsync(_utente, Roles.Admin)).ReturnsAsync(admin);
            _userManager.Setup(m => m.GetUsersInRoleAsync(Roles.Admin))
                .ReturnsAsync(Enumerable.Range(0, adminTotali).Select(_ => new ApplicationUser()).ToList());
            _eliminazione.Setup(e => e.PrenotazioniInProgrammaAsync("utente-1")).ReturnsAsync(daServire.ToList());
        }

        [Fact]
        public async Task Cancella_Ok_EliminaConLePrenotazioniAnonime()
        {
            PasswordGiusta();

            await _service.CancellaAccountAsync("utente-1", "Mia1!pass", Ip);

            _eliminazione.Verify(e => e.EliminaAsync(_utente, "Account cancellato dall'utente", Ip), Times.Once);
        }

        [Fact]
        public async Task Cancella_PasswordSbagliata_ErroreSulCampoNienteEliminato()
        {
            _userManager.Setup(m => m.IsLockedOutAsync(_utente)).ReturnsAsync(false);
            _userManager.Setup(m => m.CheckPasswordAsync(_utente, "Sbagliata1!")).ReturnsAsync(false);
            _userManager.Setup(m => m.AccessFailedAsync(_utente)).ReturnsAsync(IdentityResult.Success);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                _service.CancellaAccountAsync("utente-1", "Sbagliata1!", Ip));

            Assert.Equal("password", CampoErrore(ex));
            _userManager.Verify(m => m.AccessFailedAsync(_utente), Times.Once);
            _eliminazione.Verify(e => e.EliminaAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task Cancella_ConPrenotazioniDaServire_Bloccata()
        {
            PasswordGiusta(daServire: new Prenotazione
            {
                NumeroCoperti = 4,
                DataPrenotazione = new DateOnly(2026, 10, 31),
                Stato = StatoPrenotazione.Attiva,
                FasciaOraria = new FasciaOraria { OrarioInizio = new TimeOnly(20, 0), OrarioFine = new TimeOnly(22, 0) }
            });

            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                _service.CancellaAccountAsync("utente-1", "Mia1!pass", Ip));

            Assert.Equal("Hai ancora una prenotazione in programma: sabato 31/10/2026, 20:00-22:00 (4 persone). " +
                         "Annullala prima di cancellare l'account.", ex.Message);
            _eliminazione.Verify(e => e.EliminaAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task Cancella_ConPiuPrenotazioniInProgramma_DiceQuanteELaPrima()
        {
            Prenotazione Una(int giorno) => new()
            {
                NumeroCoperti = 2,
                DataPrenotazione = new DateOnly(2026, 11, giorno),
                Stato = StatoPrenotazione.Attiva,
                FasciaOraria = new FasciaOraria { OrarioInizio = new TimeOnly(12, 30), OrarioFine = new TimeOnly(14, 30) }
            };
            PasswordGiusta(daServire: [Una(2), Una(9), Una(16)]);

            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                _service.CancellaAccountAsync("utente-1", "Mia1!pass", Ip));

            Assert.Equal("Hai ancora 3 prenotazioni in programma, la prima lunedì 02/11/2026, 12:30-14:30 (2 persone). " +
                         "Annullale prima di cancellare l'account.", ex.Message);
        }

        [Fact]
        public async Task Cancella_UltimoAdmin_Bloccata()
        {
            PasswordGiusta(admin: true, adminTotali: 1);

            await Assert.ThrowsAsync<ConflictException>(() => _service.CancellaAccountAsync("utente-1", "Mia1!pass", Ip));
            _eliminazione.Verify(e => e.EliminaAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task Cancella_AdminConUnAltroAdmin_Permessa()
        {
            PasswordGiusta(admin: true, adminTotali: 2);

            await _service.CancellaAccountAsync("utente-1", "Mia1!pass", Ip);

            _eliminazione.Verify(e => e.EliminaAsync(_utente, It.IsAny<string>(), Ip), Times.Once);
        }
    }
}
