using GestoraWebApi.Common;
using GestoraWebApi.Context;
using GestoraWebApi.Models;
using GestoraWebApi.Repositories.LogActivity;
using GestoraWebApi.Services.LogActivity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GestoraWebApi.Tests.Services;

/// <summary>
/// V2-019 — il registro attività si tiene 24 mesi. Il repository gira su un database InMemory vero:
/// la regola sta nella query (quali righe, quante per giro).
/// </summary>
public class RegistroAttivitaCleanupServiceTests
{
    private static readonly DateTime Adesso = new(2026, 10, 26, 3, 30, 0, DateTimeKind.Utc);

    private readonly GestoraContext _context = new(new DbContextOptionsBuilder<GestoraContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private RegistroAttivitaCleanupService Service() => new(
        new LogActivityRepository(_context),
        new TestClock(Adesso),
        Options.Create(new RegistroAttivitaSettings { MesiConservazione = 24 }),
        NullLogger<RegistroAttivitaCleanupService>.Instance);

    private static Logging Riga(long id, DateTime quando) =>
        new() { Id = id, UserId = "u", Action = "Login", Timestamp = quando, IPAddress = "1.2.3.4" };

    [Fact]
    public async Task EliminaSoloLeRighePiuVecchieDi24Mesi()
    {
        _context.LogActivities.AddRange(
            Riga(1, Adesso.AddMonths(-24).AddDays(-1)),  // scaduta
            Riga(2, Adesso.AddMonths(-30)),               // scaduta
            Riga(3, Adesso.AddMonths(-24).AddDays(1)),   // ancora dentro
            Riga(4, Adesso.AddDays(-1)));                 // recente
        await _context.SaveChangesAsync();

        var eliminate = await Service().EliminaScaduteAsync();

        Assert.Equal(2, eliminate);
        Assert.Equal(new long[] { 3, 4 }, await _context.LogActivities.Select(l => l.Id).OrderBy(id => id).ToListAsync());
    }

    [Fact]
    public async Task PiuDiUnBlocco_LeEliminaTutteAGiri()
    {
        var scadute = RegistroAttivitaCleanupService.Blocco + 5;
        _context.LogActivities.AddRange(Enumerable.Range(1, scadute).Select(i => Riga(i, Adesso.AddMonths(-25).AddMinutes(i))));
        _context.LogActivities.Add(Riga(scadute + 1, Adesso.AddDays(-1)));
        await _context.SaveChangesAsync();

        var eliminate = await Service().EliminaScaduteAsync();

        Assert.Equal(scadute, eliminate);
        Assert.Equal(1, await _context.LogActivities.CountAsync());
    }

    [Fact]
    public async Task NienteDaEliminare_Zero()
    {
        _context.LogActivities.Add(Riga(1, Adesso.AddDays(-1)));
        await _context.SaveChangesAsync();

        Assert.Equal(0, await Service().EliminaScaduteAsync());
    }
}
