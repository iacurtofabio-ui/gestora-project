using GestoraWebApi.Auth;
using GestoraWebApi.Models;
using GestoraWebApi.Repositories.Prenotazioni;
using GestoraWebApi.Services.Account;
using GestoraWebApi.Services.LogActivity;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace GestoraWebApi.Tests.Services
{
    /// <summary>
    /// V2-019: l'eliminazione comune a «Il mio account» e alla pulizia degli inattivi. Che le
    /// prenotazioni si stacchino davvero lo prova PrenotazioniRepositoryTests; qui l'ordine e il tutto-o-niente.
    /// </summary>
    public class EliminazioneAccountServiceTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _userManager = new(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        private readonly Mock<IPrenotazioniRepository> _prenotazioni = new();
        private readonly Mock<ILogActivityService> _log = new();
        private readonly EsecutoreTransazioneFinto _transazione = new();
        private readonly ApplicationUser _utente = new() { Id = "utente-1" };

        private EliminazioneAccountService Service() => new(_userManager.Object, _prenotazioni.Object, _log.Object,
            _transazione, new TestClock(new DateTime(2026, 10, 26, 23, 30, 0, DateTimeKind.Utc)));

        [Fact]
        public async Task PrenotazioniInProgramma_DaOggiInItalia()
        {
            // 23:30 UTC del 26 è già il 27 a Roma (ora solare, +1).
            _prenotazioni.Setup(r => r.GetDaServireDiUtenteAsync("utente-1", new DateOnly(2026, 10, 27)))
                .ReturnsAsync(new List<Prenotazione> { new() { NumeroCoperti = 2 } });

            Assert.Single(await Service().PrenotazioniInProgrammaAsync("utente-1"));
        }

        [Fact]
        public async Task Elimina_StaccaRegistraECancella_InUnaTransazione()
        {
            _prenotazioni.Setup(r => r.StaccaDaUtenteAsync("utente-1", EliminazioneAccountService.NomeClienteEliminato)).ReturnsAsync(3);
            _userManager.Setup(m => m.DeleteAsync(_utente)).ReturnsAsync(IdentityResult.Success);

            await Service().EliminaAsync(_utente, "Account cancellato dall'utente", "10.0.0.1");

            Assert.Equal(1, _transazione.Chiamate);
            _prenotazioni.Verify(r => r.StaccaDaUtenteAsync("utente-1", "Cliente eliminato"), Times.Once);
            _log.Verify(l => l.LogAsync("utente-1", "Account cancellato dall'utente (3 prenotazioni rese anonime)", "10.0.0.1"), Times.Once);
            _userManager.Verify(m => m.DeleteAsync(_utente), Times.Once);
        }

        [Fact]
        public async Task Elimina_CancellazioneRifiutata_Lancia()
        {
            // L'eccezione dentro la transazione annulla anche il distacco e la riga del registro.
            _userManager.Setup(m => m.DeleteAsync(_utente)).ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "X" }));

            await Assert.ThrowsAsync<InvalidOperationException>(() => Service().EliminaAsync(_utente, "azione", null));
        }
    }
}
