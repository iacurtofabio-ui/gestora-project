using GestoraWebApi.Auth;
using GestoraWebApi.Common;
using GestoraWebApi.Models;
using GestoraWebApi.Repositories.Utenti;
using GestoraWebApi.Services.Account;
using GestoraWebApi.Services.Email;
using GestoraWebApi.Services.LogActivity;
using GestoraWebApi.Services.Utenti;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GestoraWebApi.Tests.Services;

/// <summary>
/// V2-015: chi è candidato lo decide il repository (UtentiRepositoryTests). Qui si prova cosa fa
/// il service con i candidati: la soglia giusta, l'eliminazione, la traccia nel registro, e che
/// un utente che non si riesce a eliminare non fermi gli altri.
/// </summary>
public class UtenzeInattiveServiceTests
{
    private static readonly DateTime Adesso = new(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IUtentiRepository> _repository = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManager =
        new(Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
    private readonly Mock<ILogActivityService> _registro = new();
    private readonly Mock<IEliminazioneAccountService> _eliminazione = new();
    private readonly Mock<IInvioEmail> _invioEmail = new();

    private UtenzeInattiveService Service() => new(
        _repository.Object,
        _userManager.Object,
        _registro.Object,
        _eliminazione.Object,
        _invioEmail.Object,
        new TestClock(Adesso),
        Options.Create(new UtenzeSettings { GiorniUtenzaMaiUsata = 180, MesiInattivita = 24, SettimaneAvviso = 4 }),
        Options.Create(new EmailSettings { UrlFrontend = "https://gestora.example/" }),
        NullLogger<UtenzeInattiveService>.Instance);

    private void Candidati(params ApplicationUser[] utenti) =>
        _repository.Setup(r => r.GetUtenzeMaiUsateAsync(It.IsAny<DateTime>(), It.IsAny<int>()))
                   .ReturnsAsync(utenti.ToList());

    [Fact]
    public async Task ChiedeAlRepositoryLeUtenzeCreatePrimaDi180Giorni_ConIlTetto()
    {
        Candidati();

        await Service().EliminaUtenzeMaiUsateAsync();

        _repository.Verify(r => r.GetUtenzeMaiUsateAsync(
            Adesso.AddDays(-180), UtenzeInattiveService.MassimoPerEsecuzione), Times.Once);
    }

    [Fact]
    public async Task EliminaICandidati_ELasciaUnaRigaNelRegistroPerOgnuno()
    {
        var a = new ApplicationUser { Id = "a" };
        var b = new ApplicationUser { Id = "b" };
        Candidati(a, b);
        _userManager.Setup(m => m.DeleteAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

        var eliminati = await Service().EliminaUtenzeMaiUsateAsync();

        Assert.Equal(2, eliminati);
        _registro.Verify(l => l.LogAsync("a", UtenzeInattiveService.AzioneRegistro, null), Times.Once);
        _registro.Verify(l => l.LogAsync("b", UtenzeInattiveService.AzioneRegistro, null), Times.Once);
    }

    [Fact]
    public async Task UnUtenteNonEliminabile_NonFermaGliAltri_ENonFinisceNelRegistro()
    {
        var rifiutato = new ApplicationUser { Id = "rifiutato" };
        var inErrore = new ApplicationUser { Id = "in-errore" };
        var ok = new ApplicationUser { Id = "ok" };
        Candidati(rifiutato, inErrore, ok);
        _userManager.Setup(m => m.DeleteAsync(rifiutato))
                    .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "no" }));
        // Es. una prenotazione arrivata nel frattempo: la FK Restrict fa fallire il DELETE.
        _userManager.Setup(m => m.DeleteAsync(inErrore)).ThrowsAsync(new InvalidOperationException("FK"));
        _userManager.Setup(m => m.DeleteAsync(ok)).ReturnsAsync(IdentityResult.Success);

        var eliminati = await Service().EliminaUtenzeMaiUsateAsync();

        Assert.Equal(1, eliminati);
        _registro.Verify(l => l.LogAsync("ok", It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
        _registro.Verify(l => l.LogAsync("rifiutato", It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        _registro.Verify(l => l.LogAsync("in-errore", It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task NessunCandidato_NonEliminaNulla()
    {
        Candidati();

        var eliminati = await Service().EliminaUtenzeMaiUsateAsync();

        Assert.Equal(0, eliminati);
        _userManager.Verify(m => m.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    // ── V2-019 — account inattivi: avviso ed eliminazione ──────────────────

    private static ApplicationUser Inattivo(string id, DateTime ultimoAccesso) =>
        new() { Id = id, UserName = id, Email = $"{id}@x.it", UltimoAccesso = ultimoAccesso };

    [Fact]
    public async Task Avvisa_ChiedeIClientiFermiDa24MesiMeno4Settimane_EMandaLaData()
    {
        var mario = Inattivo("mario", Adesso.AddMonths(-24).AddDays(30));
        _repository.Setup(r => r.GetInattiviDaAvvisareAsync(Adesso.AddMonths(-24).AddDays(28), UtenzeInattiveService.MassimoPerEsecuzione))
                   .ReturnsAsync(new List<ApplicationUser> { mario });
        _userManager.Setup(m => m.UpdateAsync(mario)).ReturnsAsync(IdentityResult.Success);
        string? testo = null;
        _invioEmail.Setup(i => i.InviaAsync("mario@x.it", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .Callback<string, string, string, CancellationToken>((_, _, t, _) => testo = t)
                   .Returns(Task.CompletedTask);

        var avvisati = await Service().AvvisaInattiviAsync();

        Assert.Equal(1, avvisati);
        Assert.Equal(Adesso, mario.AvvisoInattivitaInviatoIl);
        // Ultimo accesso + 24 mesi = 11 novembre 2026, oltre le 4 settimane da adesso: vale quella.
        Assert.Contains("a partire dal giorno 11 novembre 2026", testo);
        Assert.Contains("https://gestora.example/login", testo);
        _registro.Verify(l => l.LogAsync("mario", UtenzeInattiveService.AzioneAvviso, null), Times.Once);
    }

    [Fact]
    public async Task Avvisa_AvvisoInRitardo_LaDataNonEPrimaDi4Settimane()
    {
        // Fermo da 26 mesi (il job era spento): l'eliminazione slitta a 4 settimane da oggi.
        var luca = Inattivo("luca", Adesso.AddMonths(-26));
        _repository.Setup(r => r.GetInattiviDaAvvisareAsync(It.IsAny<DateTime>(), It.IsAny<int>()))
                   .ReturnsAsync(new List<ApplicationUser> { luca });
        _userManager.Setup(m => m.UpdateAsync(luca)).ReturnsAsync(IdentityResult.Success);
        string? testo = null;
        _invioEmail.Setup(i => i.InviaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .Callback<string, string, string, CancellationToken>((_, _, t, _) => testo = t)
                   .Returns(Task.CompletedTask);

        await Service().AvvisaInattiviAsync();

        Assert.Contains("a partire dal giorno 9 novembre 2026", testo); // 12/10 + 28 giorni
    }

    [Fact]
    public async Task Avvisa_EmailNonPartita_NonSegnaNienteERiprovaDomani()
    {
        var anna = Inattivo("anna", Adesso.AddMonths(-24).AddDays(20));
        _repository.Setup(r => r.GetInattiviDaAvvisareAsync(It.IsAny<DateTime>(), It.IsAny<int>()))
                   .ReturnsAsync(new List<ApplicationUser> { anna });
        _invioEmail.Setup(i => i.InviaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new InvalidOperationException("SMTP giù"));

        Assert.Equal(0, await Service().AvvisaInattiviAsync());
        Assert.Null(anna.AvvisoInattivitaInviatoIl);
        _userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task Elimina_Fermi24MesiEAvvisatiDa4Settimane_SaltaChiHaPrenotazioniInProgramma()
    {
        var conPrenotazione = Inattivo("con-prenotazione", Adesso.AddMonths(-25));
        var daEliminare = Inattivo("da-eliminare", Adesso.AddMonths(-25));
        _repository.Setup(r => r.GetInattiviDaEliminareAsync(Adesso.AddMonths(-24), Adesso.AddDays(-28), UtenzeInattiveService.MassimoPerEsecuzione))
                   .ReturnsAsync(new List<ApplicationUser> { conPrenotazione, daEliminare });
        _eliminazione.Setup(e => e.PrenotazioniInProgrammaAsync("con-prenotazione"))
                     .ReturnsAsync(new List<Prenotazione> { new() { NumeroCoperti = 2 } });
        _eliminazione.Setup(e => e.PrenotazioniInProgrammaAsync("da-eliminare")).ReturnsAsync(new List<Prenotazione>());

        var eliminati = await Service().EliminaInattiviAsync();

        Assert.Equal(1, eliminati);
        _eliminazione.Verify(e => e.EliminaAsync(daEliminare, UtenzeInattiveService.AzioneInattivo, null), Times.Once);
        _eliminazione.Verify(e => e.EliminaAsync(conPrenotazione, It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }
}
