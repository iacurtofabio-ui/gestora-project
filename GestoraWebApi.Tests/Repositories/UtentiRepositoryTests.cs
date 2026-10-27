using GestoraWebApi.Auth;
using GestoraWebApi.Context;
using GestoraWebApi.Enums;
using GestoraWebApi.Models;
using GestoraWebApi.Repositories.Utenti;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GestoraWebApi.Tests.Repositories;

/// <summary>
/// V2-015: la regola "utenza mai usata" vive tutta nella query, quindi si prova su un database
/// InMemory vero. Un errore qui elimina utenti che non andavano toccati: ogni condizione ha il
/// suo caso.
/// </summary>
public class UtentiRepositoryTests
{
    private static readonly DateTime Soglia = new(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Prima = Soglia.AddDays(-1);
    private static readonly DateTime Dopo = Soglia.AddDays(1);

    private static GestoraContext NewContext()
    {
        var ctx = new GestoraContext(new DbContextOptionsBuilder<GestoraContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        ctx.Roles.AddRange(
            new IdentityRole { Id = "r-admin", Name = Roles.Admin },
            new IdentityRole { Id = "r-staff", Name = Roles.Staff },
            new IdentityRole { Id = "r-cliente", Name = Roles.Cliente });
        return ctx;
    }

    private static void Utente(GestoraContext ctx, string id, DateTime creato,
                               DateTime? ultimoAccesso = null, params string[] ruoli)
    {
        ctx.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = id,
            Email = $"{id}@x.it",
            DataCreazione = creato,
            UltimoAccesso = ultimoAccesso
        });
        foreach (var ruolo in ruoli)
            ctx.UserRoles.Add(new IdentityUserRole<string> { UserId = id, RoleId = ruolo });
    }

    private static async Task<string[]> Candidati(GestoraContext ctx, int massimo = 500)
    {
        await ctx.SaveChangesAsync();
        var risultato = await new UtentiRepository(ctx).GetUtenzeMaiUsateAsync(Soglia, massimo);
        return risultato.Select(u => u.Id).OrderBy(id => id).ToArray();
    }

    [Fact]
    public async Task ClienteMaiEntrato_CreatoPrimaDellaSoglia_SenzaPrenotazioni_ECandidato()
    {
        using var ctx = NewContext();
        Utente(ctx, "vecchio", Prima, null, "r-cliente");

        Assert.Equal(new[] { "vecchio" }, await Candidati(ctx));
    }

    [Fact]
    public async Task CreatoDopoLaSoglia_Resta()
    {
        using var ctx = NewContext();
        Utente(ctx, "recente", Dopo, null, "r-cliente");

        Assert.Empty(await Candidati(ctx));
    }

    [Fact]
    public async Task HaFattoAlmenoUnLogin_Resta()
    {
        using var ctx = NewContext();
        Utente(ctx, "entrato", Prima, Prima.AddDays(-30), "r-cliente");

        Assert.Empty(await Candidati(ctx));
    }

    [Fact]
    public async Task HaUnaPrenotazione_AncheAnnullata_Resta()
    {
        using var ctx = NewContext();
        Utente(ctx, "prenotato", Prima, null, "r-cliente");
        ctx.Prenotazioni.Add(new Prenotazione
        {
            Id = 1,
            UserId = "prenotato",
            NumeroCoperti = 2,
            Stato = StatoPrenotazione.Annullata,
            DataPrenotazione = new DateOnly(2026, 4, 1),
            FasciaOrariaId = 1,
            PrenotazioniPostazioni = new List<PrenotazionePostazione>()
        });

        Assert.Empty(await Candidati(ctx));
    }

    [Fact]
    public async Task StaffEAdmin_RestanoAncheConIlRuoloCliente()
    {
        // Uno Staff appena creato dall'Admin può non essere ancora entrato: non va eliminato.
        using var ctx = NewContext();
        Utente(ctx, "staff", Prima, null, "r-staff");
        Utente(ctx, "admin", Prima, null, "r-admin");
        Utente(ctx, "staff-cliente", Prima, null, "r-staff", "r-cliente");

        Assert.Empty(await Candidati(ctx));
    }

    [Fact]
    public async Task UtenteSenzaRuoli_ECandidato()
    {
        // Senza ruoli non può fare nulla: tenerlo non protegge niente.
        using var ctx = NewContext();
        Utente(ctx, "senza-ruoli", Prima);

        Assert.Equal(new[] { "senza-ruoli" }, await Candidati(ctx));
    }

    [Fact]
    public async Task RispettaIlMassimo_PartendoDaiPiuVecchi()
    {
        using var ctx = NewContext();
        Utente(ctx, "c", Prima, null, "r-cliente");
        Utente(ctx, "a", Prima.AddDays(-10), null, "r-cliente");
        Utente(ctx, "b", Prima.AddDays(-5), null, "r-cliente");

        Assert.Equal(new[] { "a", "b" }, await Candidati(ctx, massimo: 2));
    }

    // ── V2-019 — account inattivi ──────────────────────────────────────────

    private static async Task<string[]> DaAvvisare(GestoraContext ctx)
    {
        await ctx.SaveChangesAsync();
        return (await new UtentiRepository(ctx).GetInattiviDaAvvisareAsync(Soglia, 500)).Select(u => u.Id).OrderBy(id => id).ToArray();
    }

    private static async Task<string[]> DaEliminare(GestoraContext ctx, DateTime avvisatiPrimaDi)
    {
        await ctx.SaveChangesAsync();
        return (await new UtentiRepository(ctx).GetInattiviDaEliminareAsync(Soglia, avvisatiPrimaDi, 500)).Select(u => u.Id).OrderBy(id => id).ToArray();
    }

    [Fact]
    public async Task DaAvvisare_SoloClientiEntratiPrimaDellaSoglia_NonAncoraAvvisati()
    {
        using var ctx = NewContext();
        Utente(ctx, "fermo", Prima.AddYears(-1), Prima, "r-cliente");
        Utente(ctx, "attivo", Prima.AddYears(-1), Dopo, "r-cliente");
        Utente(ctx, "mai-entrato", Prima.AddYears(-1), null, "r-cliente");
        Utente(ctx, "staff-fermo", Prima.AddYears(-1), Prima, "r-staff");
        Utente(ctx, "gia-avvisato", Prima.AddYears(-1), Prima, "r-cliente");
        await ctx.SaveChangesAsync();
        (await ctx.Users.FindAsync("gia-avvisato"))!.AvvisoInattivitaInviatoIl = Prima;

        Assert.Equal(new[] { "fermo" }, await DaAvvisare(ctx));
    }

    [Fact]
    public async Task DaEliminare_SoloAvvisatiPrimaDelLimite_EFermiPrimaDellaSoglia()
    {
        using var ctx = NewContext();
        Utente(ctx, "avvisato-da-tempo", Prima.AddYears(-1), Prima, "r-cliente");
        Utente(ctx, "avvisato-ieri", Prima.AddYears(-1), Prima, "r-cliente");
        Utente(ctx, "mai-avvisato", Prima.AddYears(-1), Prima, "r-cliente");
        Utente(ctx, "rientrato", Prima.AddYears(-1), Dopo, "r-cliente");
        Utente(ctx, "admin-fermo", Prima.AddYears(-1), Prima, "r-admin");
        await ctx.SaveChangesAsync();
        var limiteAvviso = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        (await ctx.Users.FindAsync("avvisato-da-tempo"))!.AvvisoInattivitaInviatoIl = limiteAvviso.AddDays(-10);
        (await ctx.Users.FindAsync("avvisato-ieri"))!.AvvisoInattivitaInviatoIl = limiteAvviso.AddDays(1);
        (await ctx.Users.FindAsync("rientrato"))!.AvvisoInattivitaInviatoIl = limiteAvviso.AddDays(-10);
        (await ctx.Users.FindAsync("admin-fermo"))!.AvvisoInattivitaInviatoIl = limiteAvviso.AddDays(-10);

        Assert.Equal(new[] { "avvisato-da-tempo" }, await DaEliminare(ctx, limiteAvviso));
    }
}
