using GestoraWebApi.Context;
using GestoraWebApi.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace GestoraWebApi.Tests.Infrastructure;

/// <summary>
/// V2-018: le chiavi di Data Protection stanno nel database. Ogni "app" qui sotto è un insieme di
/// servizi costruito da zero, come dopo un riavvio del container; il database InMemory è
/// l'unica cosa che le due app hanno in comune.
/// </summary>
public class DataProtectionExtensionsTests
{
    private const string Scopo = "test-conferma-email";

    private static ServiceProvider NuovaApp(InMemoryDatabaseRoot database, string nomeDatabase)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<GestoraContext>(o => o.UseInMemoryDatabase(nomeDatabase, database));
        services.AddChiaviDataProtection();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void ProteggereUnDato_SalvaLaChiaveNelDatabase()
    {
        var database = new InMemoryDatabaseRoot();
        var nome = Guid.NewGuid().ToString();
        using var app = NuovaApp(database, nome);

        app.GetRequiredService<IDataProtectionProvider>().CreateProtector(Scopo).Protect("dato");

        using var scope = app.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GestoraContext>();
        Assert.NotEmpty(context.DataProtectionKeys);
    }

    // Il caso del link di conferma: firmato prima di un riavvio, letto dopo.
    [Fact]
    public void UnDatoProtetto_SiLeggeAncoraDopoUnRiavvio()
    {
        var database = new InMemoryDatabaseRoot();
        var nome = Guid.NewGuid().ToString();

        string protetto;
        using (var primaDelRiavvio = NuovaApp(database, nome))
        {
            protetto = primaDelRiavvio.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector(Scopo).Protect("dato");
        }

        using var dopoIlRiavvio = NuovaApp(database, nome);
        var letto = dopoIlRiavvio.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(Scopo).Unprotect(protetto);

        Assert.Equal("dato", letto);
    }
}
