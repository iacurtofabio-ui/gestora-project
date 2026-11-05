using GestoraWebApi.Common;
using GestoraWebApi.Services.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GestoraWebApi.Tests.Services;

public class InvioEmailSuLogTests : IDisposable
{
    private readonly string _cartella = Path.Combine(Path.GetTempPath(), $"gestora-posta-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_cartella))
            Directory.Delete(_cartella, recursive: true);
    }

    private static InvioEmailSuLog Crea(string cartella) => new(NullLogger<InvioEmailSuLog>.Instance,
        Options.Create(new EmailSettings { CartellaSviluppo = cartella }));

    [Fact]
    public async Task ConLaCartella_SalvaLEmailComeFile()
    {
        await Crea(_cartella).InviaAsync("cliente@prova.test", "Conferma", "Apri http://localhost/link");

        var contenuto = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(_cartella)));

        Assert.Contains("A: cliente@prova.test", contenuto);
        Assert.Contains("http://localhost/link", contenuto);
    }

    [Fact]
    public async Task IlNomeDelFile_NonDipendeDallIndirizzo()
    {
        await Crea(_cartella).InviaAsync("../../fuori@prova.test", "Conferma", "testo");

        Assert.Single(Directory.GetFiles(_cartella));
    }

    [Fact]
    public async Task SenzaCartella_NonScriveNiente()
    {
        await Crea(string.Empty).InviaAsync("cliente@prova.test", "Conferma", "testo");

        Assert.False(Directory.Exists(_cartella));
    }
}
