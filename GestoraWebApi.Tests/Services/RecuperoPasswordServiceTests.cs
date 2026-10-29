using System.Text;
using GestoraWebApi.Auth;
using GestoraWebApi.Common;
using GestoraWebApi.Infrastructure.Auth;
using GestoraWebApi.Infrastructure.Exceptions;
using GestoraWebApi.Services.Email;
using GestoraWebApi.Services.LogActivity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GestoraWebApi.Tests.Services
{
    /// <summary>V2-019: recupero password. Lo UserManager e l'invio sono finti: si prova la logica del servizio.</summary>
    public class RecuperoPasswordServiceTests
    {
        private const string TokenIdentity = "CfDJ8+token/con=caratteri+speciali";
        private const string Ip = "10.0.0.1";

        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly Mock<IInvioEmail> _invioEmail = new();
        private readonly Mock<ILogActivityService> _log = new();
        private readonly Mock<ITentativiAccesso> _tentativi = new();
        private readonly RecuperoPasswordService _service;

        public RecuperoPasswordServiceTests()
        {
            _userManager = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);

            var settings = Options.Create(new EmailSettings { UrlFrontend = "https://gestora.example/" });
            _service = new RecuperoPasswordService(_userManager.Object, _invioEmail.Object, _log.Object,
                settings, _tentativi.Object, NullLogger<RecuperoPasswordService>.Instance);
        }

        private static ApplicationUser Utente(bool confermato = true) => new()
        {
            Id = "utente-1",
            UserName = "Mario",
            Email = "mario@example.com",
            EmailConfirmed = confermato
        };

        private static string InUrl(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        // --- Richiesta del link ---

        [Fact]
        public async Task Richiedi_EmailSconosciuta_NessunaEmailNessunErrore()
        {
            _userManager.Setup(m => m.FindByEmailAsync("nessuno@example.com")).ReturnsAsync((ApplicationUser?)null);

            await _service.RichiediAsync("nessuno@example.com", Ip);

            _invioEmail.Verify(i => i.InviaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Richiedi_MandaIlLinkConTokenCodificatoEDurata()
        {
            var utente = Utente();
            _userManager.Setup(m => m.FindByEmailAsync(utente.Email!)).ReturnsAsync(utente);
            _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(utente)).ReturnsAsync(TokenIdentity);
            string? testoInviato = null;
            _invioEmail.Setup(i => i.InviaAsync(utente.Email!, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, CancellationToken>((_, _, testo, _) => testoInviato = testo)
                .Returns(Task.CompletedTask);

            await _service.RichiediAsync(utente.Email!, Ip);

            Assert.NotNull(testoInviato);
            Assert.Contains($"https://gestora.example/reimposta-password?userId=utente-1&token={InUrl(TokenIdentity)}", testoInviato);
            Assert.Contains("valido per 2 ore", testoInviato);
            _log.Verify(l => l.LogAsync("utente-1", It.IsAny<string>(), Ip), Times.Once);
        }

        [Fact]
        public async Task Richiedi_InvioFallito_NonLancia()
        {
            var utente = Utente();
            _userManager.Setup(m => m.FindByEmailAsync(utente.Email!)).ReturnsAsync(utente);
            _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(utente)).ReturnsAsync(TokenIdentity);
            _invioEmail.Setup(i => i.InviaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("SMTP giù"));

            // Chi chiama risponde sempre allo stesso modo: un errore di posta non deve emergere.
            await _service.RichiediAsync(utente.Email!, Ip);
        }

        // --- Nuova password ---

        [Fact]
        public async Task Reimposta_TokenValido_ConfermaEmailSbloccaERegistra()
        {
            var utente = Utente(confermato: false);
            _userManager.Setup(m => m.FindByIdAsync("utente-1")).ReturnsAsync(utente);
            _userManager.Setup(m => m.ResetPasswordAsync(utente, TokenIdentity, "Nuova1!pass")).ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(m => m.UpdateAsync(utente)).ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(m => m.ResetAccessFailedCountAsync(utente)).ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(m => m.SetLockoutEndDateAsync(utente, null)).ReturnsAsync(IdentityResult.Success);

            var ok = await _service.ReimpostaAsync("utente-1", InUrl(TokenIdentity), "Nuova1!pass", Ip);

            Assert.True(ok);
            Assert.True(utente.EmailConfirmed);
            _userManager.Verify(m => m.SetLockoutEndDateAsync(utente, null), Times.Once);
            _tentativi.Verify(t => t.Azzera(utente.Email, Ip), Times.Once);
            _log.Verify(l => l.LogAsync("utente-1", It.IsAny<string>(), Ip), Times.Once);
        }

        [Fact]
        public async Task Reimposta_TokenScadutoONonValido_False()
        {
            var utente = Utente();
            _userManager.Setup(m => m.FindByIdAsync("utente-1")).ReturnsAsync(utente);
            _userManager.Setup(m => m.ResetPasswordAsync(utente, TokenIdentity, It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityErrorDescriber().InvalidToken()));

            Assert.False(await _service.ReimpostaAsync("utente-1", InUrl(TokenIdentity), "Nuova1!pass", Ip));
        }

        [Theory]
        [InlineData("utente-inesistente", "dG9rZW4")]
        [InlineData("utente-1", "non base64 %%%")]
        public async Task Reimposta_UtenteOTokenIllegibili_False(string userId, string token)
        {
            _userManager.Setup(m => m.FindByIdAsync("utente-1")).ReturnsAsync(Utente());

            Assert.False(await _service.ReimpostaAsync(userId, token, "Nuova1!pass", Ip));
            _userManager.Verify(m => m.ResetPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Reimposta_PasswordRifiutataDaIdentity_ErroreSulCampo()
        {
            var utente = Utente();
            _userManager.Setup(m => m.FindByIdAsync("utente-1")).ReturnsAsync(utente);
            _userManager.Setup(m => m.ResetPasswordAsync(utente, TokenIdentity, It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityErrorDescriber().PasswordRequiresDigit()));

            await Assert.ThrowsAsync<ValidationException>(() =>
                _service.ReimpostaAsync("utente-1", InUrl(TokenIdentity), "SenzaNumeri!", Ip));
        }

        [Fact]
        public void IlLinkScadeIn2Ore_NonIn24()
        {
            Assert.Equal(TimeSpan.FromHours(2), new RecuperoPasswordTokenProviderOptions().TokenLifespan);
        }
    }
}
