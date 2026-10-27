using GestoraWebApi.Auth;
using GestoraWebApi.Services.Privacy;

namespace GestoraWebApi.Tests.Services;

/// <summary>
/// V2-019 — quando un utente deve (ri)vedere l'informativa privacy.
/// </summary>
public class PresaVisionePrivacyTests
{
    private const string Versione = "2026-10-26";

    [Fact]
    public void MaiAccettata_DaAccettare()
    {
        // Account creato da un Admin, primo Admin o precedente alla F3.
        Assert.True(PresaVisionePrivacy.DaAccettare(new ApplicationUser(), Versione));
    }

    [Fact]
    public void VersioneInVigoreAccettata_NonDaAccettare()
    {
        var user = new ApplicationUser();
        PresaVisionePrivacy.Registra(user, Versione, new DateTime(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc));

        Assert.False(PresaVisionePrivacy.DaAccettare(user, Versione));
        Assert.Equal(new DateTime(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc), user.PrivacyAccettataIl);
        Assert.Equal(Versione, user.PrivacyVersione);
    }

    [Fact]
    public void InformativaCambiata_DaAccettareDiNuovo()
    {
        var user = new ApplicationUser { PrivacyAccettataIl = DateTime.UtcNow, PrivacyVersione = "2026-01-01" };

        Assert.True(PresaVisionePrivacy.DaAccettare(user, Versione));
    }
}
