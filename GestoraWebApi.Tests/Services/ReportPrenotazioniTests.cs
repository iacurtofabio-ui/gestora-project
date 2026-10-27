using System.Text;
using GestoraWebApi.Auth;
using GestoraWebApi.Enums;
using GestoraWebApi.Infrastructure.Exceptions;
using GestoraWebApi.Models;
using GestoraWebApi.Repositories.Prenotazioni;
using GestoraWebApi.Services.Report;
using MockQueryable.Moq;
using Moq;

namespace GestoraWebApi.Tests.Services;

/// <summary>V2-025 — report CSV delle prenotazioni per l'Admin: il formato per Excel e il periodo.</summary>
public class ReportPrenotazioniTests
{
    private static readonly FasciaOraria Cena = new() { Id = 1, OrarioInizio = new TimeOnly(19, 30), OrarioFine = new TimeOnly(22, 0) };

    private static Prenotazione Prenotazione(long id, DateOnly data, StatoPrenotazione stato = StatoPrenotazione.Attiva,
                                             string? nomeCliente = null, string? note = null) => new()
    {
        Id = id,
        DataPrenotazione = data,
        NumeroCoperti = 4,
        Stato = stato,
        NomeCliente = nomeCliente,
        Note = note,
        User = new ApplicationUser { UserName = "mario.rossi" },
        FasciaOraria = Cena,
        PrenotazioniPostazioni = new List<PrenotazionePostazione>
        {
            new() { Postazione = new Postazione { Numero = 7, Zona = new Zona { Nome = "Dehors" } } },
            new() { Postazione = new Postazione { Numero = 3, Zona = new Zona { Nome = "Dehors" } } }
        }
    };

    private static string[] Righe(byte[] csv)
    {
        Assert.Equal(Encoding.UTF8.GetPreamble(), csv.Take(3));
        return Encoding.UTF8.GetString(csv, 3, csv.Length - 3).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
    }

    // --- Formato ---

    [Fact]
    public void Componi_IntestazioneERigaPerExcelItaliano()
    {
        var righe = Righe(CsvPrenotazioni.Componi([Prenotazione(1, new DateOnly(2026, 10, 30), StatoPrenotazione.NonPresentata)]));

        Assert.Equal("Data;Giorno;Turno;Persone;Cliente;Stato;Tavoli;Zona;Note", righe[0]);
        Assert.Equal("30/10/2026;venerdì;19:30-22:00;4;mario.rossi;Non presentata;3, 7;Dehors;", righe[1]);
    }

    [Fact]
    public void Componi_IlNomeScrittoDalloStaffVinceSullUtente()
    {
        var righe = Righe(CsvPrenotazioni.Componi([Prenotazione(1, new DateOnly(2026, 10, 30), nomeCliente: "Giulia Bianchi")]));

        Assert.Contains(";Giulia Bianchi;", righe[1]);
    }

    [Theory]
    [InlineData("Tavolo vicino; finestra", "\"Tavolo vicino; finestra\"")]
    [InlineData("Dice \"grazie\"", "\"Dice \"\"grazie\"\"\"")]
    [InlineData("=SOMMA(A1:A9)", "'=SOMMA(A1:A9)")]
    [InlineData("+39 333 1234567", "'+39 333 1234567")]
    [InlineData("@mario", "'@mario")]
    [InlineData("Seggiolone", "Seggiolone")]
    public void Cella_SeparatoreVirgoletteEFormuleNeutralizzati(string valore, string atteso)
    {
        Assert.Equal(atteso, CsvPrenotazioni.Cella(valore));
    }

    [Fact]
    public void Componi_NotaSuPiuRighe_RestaUnaCella()
    {
        var csv = Encoding.UTF8.GetString(CsvPrenotazioni.Componi([Prenotazione(1, new DateOnly(2026, 10, 30), note: "riga uno\nriga due")]));

        Assert.EndsWith(";\"riga uno\nriga due\"\r\n", csv);
    }

    // --- Periodo ---

    private static ReportPrenotazioniService Service(params Prenotazione[] prenotazioni)
    {
        var repository = new Mock<IPrenotazioniRepository>();
        repository.Setup(r => r.GetAllQueryableAsync()).Returns(prenotazioni.ToList().BuildMockDbSet().Object);
        return new ReportPrenotazioniService(repository.Object);
    }

    [Fact]
    public async Task Csv_SoloLePrenotazioniDelPeriodo_DateComprese_InOrdine()
    {
        var service = Service(
            Prenotazione(1, new DateOnly(2026, 9, 30)),
            Prenotazione(3, new DateOnly(2026, 10, 31), StatoPrenotazione.Annullata),
            Prenotazione(2, new DateOnly(2026, 10, 1), StatoPrenotazione.Completata),
            Prenotazione(4, new DateOnly(2026, 11, 1)));

        var righe = Righe(await service.CsvAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));

        Assert.Equal(3, righe.Length); // intestazione + 2
        Assert.StartsWith("01/10/2026;", righe[1]);
        Assert.StartsWith("31/10/2026;", righe[2]);
        Assert.Contains(";Annullata;", righe[2]);
    }

    [Fact]
    public async Task Csv_NessunaPrenotazione_SoloIntestazione()
    {
        var righe = Righe(await Service().CsvAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));

        Assert.Single(righe);
    }

    [Theory]
    [InlineData("2026-10-31", "2026-10-01")] // finale prima dell'iniziale
    [InlineData("2026-01-01", "2027-01-02")] // più di un anno
    [InlineData("0001-01-01", "2026-10-31")] // data iniziale mancante
    public async Task Csv_PeriodoNonValido_ErroreDiValidazione(string da, string a)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            Service().CsvAsync(DateOnly.Parse(da), DateOnly.Parse(a)));
    }
}
