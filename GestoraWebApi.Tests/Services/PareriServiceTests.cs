using GestoraWebApi.Common;
using GestoraWebApi.Services.Email;
using GestoraWebApi.Services.Pareri;
using GestoraWebApi.Validators;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GestoraWebApi.Tests.Services;

/// <summary>V2-022 — modulo pubblico dei pareri: invio via email, campo trappola, regole dei campi.</summary>
public class PareriServiceTests
{
    private const string Ip = "10.0.0.1";

    private readonly Mock<IInvioEmail> _invioEmail = new();

    private PareriService Service(string destinatario = "fabio@example.com") => new(
        _invioEmail.Object,
        new TestClock(new DateTime(2026, 10, 27, 17, 30, 0, DateTimeKind.Utc)),
        Options.Create(new PareriSettings { Destinatario = destinatario }),
        NullLogger<PareriService>.Instance);

    [Fact]
    public async Task Invia_MandaAlDestinatarioNomeEmailMessaggioEOraItaliana()
    {
        string? testo = null;
        _invioEmail.Setup(i => i.InviaAsync("fabio@example.com", "Nuovo parere su Gestora", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .Callback<string, string, string, CancellationToken>((_, _, t, _) => testo = t)
                   .Returns(Task.CompletedTask);

        await Service().InviaAsync(new ParereDTO { Messaggio = "  Ottimo servizio!  ", Nome = "Anna", Email = "anna@x.it" }, Ip);

        Assert.NotNull(testo);
        Assert.Contains("27/10/2026 18:30", testo); // 17:30 UTC = 18:30 a Roma (ora solare)
        Assert.Contains("Nome: Anna", testo);
        Assert.Contains("Email: anna@x.it", testo);
        Assert.EndsWith("Ottimo servizio!", testo);
    }

    [Fact]
    public async Task Invia_SenzaNomeNeEmail_LoDiceNelTesto()
    {
        string? testo = null;
        _invioEmail.Setup(i => i.InviaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .Callback<string, string, string, CancellationToken>((_, _, t, _) => testo = t)
                   .Returns(Task.CompletedTask);

        await Service().InviaAsync(new ParereDTO { Messaggio = "Bravi" }, Ip);

        Assert.Contains("Nome: (non indicato)", testo);
        Assert.Contains("Email: (non indicata: non si può rispondere)", testo);
    }

    [Fact]
    public async Task Invia_CampoTrappolaCompilato_NessunaEmail()
    {
        await Service().InviaAsync(new ParereDTO { Messaggio = "Compra ora!", SitoWeb = "http://spam.example" }, Ip);

        _invioEmail.Verify(i => i.InviaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Invia_SenzaDestinatario_NessunaEmailNessunErrore()
    {
        // Solo in locale: fuori da Development l'app non parte senza destinatario.
        await Service(destinatario: "").InviaAsync(new ParereDTO { Messaggio = "Bravi" }, Ip);

        _invioEmail.Verify(i => i.InviaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Invia_ErroreDiInvio_ArrivaAlChiamante()
    {
        _invioEmail.Setup(i => i.InviaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new InvalidOperationException("SMTP giù"));

        // Chi scrive deve sapere che il parere non è partito: niente «grazie» falso.
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().InviaAsync(new ParereDTO { Messaggio = "Bravi" }, Ip));
    }

    // --- Regole dei campi ---

    [Theory]
    [InlineData("", null, false)]
    [InlineData("   ", null, false)]
    [InlineData("Bravi", null, true)]
    [InlineData("Bravi", "", true)]
    [InlineData("Bravi", "anna@x.it", true)]
    [InlineData("Bravi", "non-una-email", false)]
    public void Validatore_MessaggioObbligatorio_EmailFacoltativaMaValida(string messaggio, string? email, bool valido)
    {
        var esito = new ParereDTOValidator().Validate(new ParereDTO { Messaggio = messaggio, Email = email });

        Assert.Equal(valido, esito.IsValid);
    }

    [Fact]
    public void Validatore_MessaggioTroppoLungo_Rifiutato()
    {
        var esito = new ParereDTOValidator().Validate(new ParereDTO { Messaggio = new string('a', ParereDTOValidator.MessaggioMassimo + 1) });

        Assert.False(esito.IsValid);
    }
}
