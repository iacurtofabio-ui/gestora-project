using GestoraWebApi.Enums;
using GestoraWebApi.Models;
using GestoraWebApi.Repositories.Postazioni;
using GestoraWebApi.Repositories.Prenotazioni;
using GestoraWebApi.Repositories.Zone;
using GestoraWebApi.Services.Disponibilita;
using GestoraWebApi.Services.PostazioneAssignment;
using GestoraWebApi.Services.PrenotazioniPostazioni;
using Moq;

namespace GestoraWebApi.Tests.Services;

/// <summary>
/// Test di <see cref="DisponibilitaService"/> — checkpoint 2c: la verifica di disponibilità
/// deve usare lo stesso motore <see cref="AssegnazioneTavoli"/> dell'assegnazione e basare i
/// posti residui sul tetto della fascia (<c>MaxCoperti</c>), non sulla somma dei tavoli.
/// </summary>
public class DisponibilitaServiceTests
{
    private static readonly DateOnly Lunedi = new(2026, 9, 7); // è un lunedì

    private readonly Mock<IPrenotazioniRepository> _prenotazioni = new();
    private readonly Mock<IPostazioneRepository> _postazioni = new();
    private readonly Mock<IZonaRepository> _zone = new();

    // Prima del lunedi' di prova: tutte le fasce sono ancora da venire.
    private readonly TestClock _clock = new(new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc));

    private DisponibilitaService CreateService() =>
        new(_prenotazioni.Object, _postazioni.Object, _zone.Object, _clock);

    private static Zona Zona(long id, bool attiva = true) => new() { Id = id, Nome = $"Zona {id}", Attiva = attiva };

    private static Postazione Tavolo(long id, int capienza, long zonaId = 1) =>
        new() { Id = id, Numero = (int)id, CapienzaMassima = capienza, Attiva = true, ZonaId = zonaId };

    private static FasciaOraria Fascia(long id, int maxCoperti) => new()
    {
        Id = id,
        OrarioInizio = new TimeOnly(19, 0),
        OrarioFine = new TimeOnly(21, 0),
        GiornoSettimana = DayOfWeek.Monday,
        MaxCoperti = maxCoperti,
        Attiva = true
    };

    private static Prenotazione Prenotazione(long fasciaId, int coperti, params (long postazioneId, int posti)[] tavoli) => new()
    {
        NumeroCoperti = coperti,
        FasciaOrariaId = fasciaId,
        Stato = StatoPrenotazione.Attiva,
        PrenotazioniPostazioni = tavoli
            .Select(t => new PrenotazionePostazione { PostazioneId = t.postazioneId, NumeroPosti = t.posti })
            .ToList()
    };

    private void Setup(
        IEnumerable<FasciaOraria> fasce,
        IEnumerable<Postazione> postazioniAttive,
        IEnumerable<Prenotazione> prenotazioni,
        IEnumerable<Zona>? zoneAttive = null)
    {
        _prenotazioni.Setup(r => r.GetFasceOrarieByDayAsync(It.IsAny<DayOfWeek>())).ReturnsAsync(fasce.ToList());
        _prenotazioni.Setup(r => r.GetPrenotazioniByDataAsync(It.IsAny<DateOnly>())).ReturnsAsync(prenotazioni.ToList());
        _postazioni.Setup(r => r.GetPostazioniAttiveAsync()).ReturnsAsync(postazioniAttive.ToList());
        _zone.Setup(r => r.GetAllZoneAttiveAsync())
             .ReturnsAsync((zoneAttive ?? new[] { Zona(1) }).ToList());
    }

    private static FasciaDisponibilitaDTO Fascia(DisponibilitaResponseDTO r, long id) =>
        r.Fasce.Single(f => f.FasciaOrariaId == id);

    [Fact]
    public async Task PostiResidui_BasatiSulTettoDellaFascia_NonSullaSommaDeiTavoli()
    {
        // Tetto 6, tavoli in sala per 100 posti totali, 4 coperti già prenotati.
        Setup(
            fasce: new[] { Fascia(1, maxCoperti: 6) },
            postazioniAttive: new[] { Tavolo(1, 50), Tavolo(2, 50) },
            prenotazioni: new[] { Prenotazione(fasciaId: 1, coperti: 4, (1, 4)) });

        var res = await CreateService().CheckDisponibilitaAsync(new CheckDisponibilitaDTO
        {
            DataPrenotazione = Lunedi,
            NumeroCoperti = 2
        });

        Assert.Equal(2, Fascia(res, 1).PostiResiduiFascia);
    }

    [Fact]
    public async Task TettoEsaurito_MarcaNonDisponibile_ConMessaggioSullaCapienza()
    {
        Setup(
            fasce: new[] { Fascia(1, maxCoperti: 10) },
            postazioniAttive: new[] { Tavolo(1, 4), Tavolo(2, 4), Tavolo(3, 4) },
            prenotazioni: new[] { Prenotazione(fasciaId: 1, coperti: 10, (1, 4), (2, 4), (3, 2)) });

        var res = await CreateService().CheckDisponibilitaAsync(new CheckDisponibilitaDTO
        {
            DataPrenotazione = Lunedi,
            NumeroCoperti = 2
        });

        var fascia = Fascia(res, 1);
        Assert.False(fascia.DisponibilePerRichiesta);
        Assert.Equal(0, fascia.PostiResiduiFascia);
        Assert.Contains("capienza", fascia.Messaggio, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TettoLibero_MaTavoliInsufficienti_MessaggioDistingueILDueCasi()
    {
        // Tetto ampio (50), pochi coperti prenotati, ma resta libero solo un tavolo da 2
        // e la richiesta è per 8 persone.
        Setup(
            fasce: new[] { Fascia(1, maxCoperti: 50) },
            postazioniAttive: new[] { Tavolo(1, 8), Tavolo(2, 2) },
            prenotazioni: new[] { Prenotazione(fasciaId: 1, coperti: 6, (1, 6)) });

        var res = await CreateService().CheckDisponibilitaAsync(new CheckDisponibilitaDTO
        {
            DataPrenotazione = Lunedi,
            NumeroCoperti = 8
        });

        var fascia = Fascia(res, 1);
        Assert.False(fascia.DisponibilePerRichiesta);
        // V2-024: i posti rimasti sono quelli dei tavoli liberi (2), non il tetto residuo (44).
        Assert.Equal(2, fascia.PostiResiduiFascia);
        Assert.Equal(MotivoDisponibilita.TavoliInsufficienti, fascia.Motivo);
        Assert.Contains("tavoli", fascia.Messaggio, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StessaLogicaDellAssegnazione_LeCombinazioniCoincidonoConIlMotore()
    {
        var libere = new List<Postazione> { Tavolo(1, 8), Tavolo(2, 2), Tavolo(3, 2) };
        Setup(
            fasce: new[] { Fascia(1, maxCoperti: 50) },
            postazioniAttive: libere,
            prenotazioni: Array.Empty<Prenotazione>());

        var res = await CreateService().CheckDisponibilitaAsync(new CheckDisponibilitaDTO
        {
            DataPrenotazione = Lunedi,
            NumeroCoperti = 6
        });

        var attesa = AssegnazioneTavoli.TrovaMigliorCombinazione(libere, 6)!.Select(p => p.Id).OrderBy(x => x);
        var effettiva = Fascia(res, 1).Postazioni.Select(p => p.PostazioneId).OrderBy(x => x);
        Assert.Equal(attesa, effettiva);
    }

    [Fact]
    public async Task EscludeTavoliInZonaDisattivata()
    {
        Setup(
            fasce: new[] { Fascia(1, maxCoperti: 50) },
            postazioniAttive: new[] { Tavolo(1, 4, zonaId: 1), Tavolo(2, 4, zonaId: 2) },
            prenotazioni: Array.Empty<Prenotazione>(),
            zoneAttive: new[] { Zona(1) }); // zona 2 disattivata

        var res = await CreateService().CheckDisponibilitaAsync(new CheckDisponibilitaDTO
        {
            DataPrenotazione = Lunedi,
            NumeroCoperti = 4
        });

        Assert.All(Fascia(res, 1).Postazioni, p => Assert.NotEqual(2, p.PostazioneId));
    }

    [Fact]
    public async Task EscludePostazioniGiaOccupateNellaFascia()
    {
        Setup(
            fasce: new[] { Fascia(1, maxCoperti: 50) },
            postazioniAttive: new[] { Tavolo(1, 4), Tavolo(2, 4) },
            prenotazioni: new[] { Prenotazione(fasciaId: 1, coperti: 4, (1, 4)) });

        var res = await CreateService().CheckDisponibilitaAsync(new CheckDisponibilitaDTO
        {
            DataPrenotazione = Lunedi,
            NumeroCoperti = 4
        });

        var fascia = Fascia(res, 1);
        Assert.True(fascia.DisponibilePerRichiesta);
        Assert.All(fascia.Postazioni, p => Assert.NotEqual(1, p.PostazioneId));
    }

    // ── REV-053 — completamento della copertura ──────────────────────────────

    /// <summary>
    /// L'endpoint è pubblico e non passa da un validator: una richiesta a 0 (o negativa) va
    /// normalizzata a 1, altrimenti il motore riceverebbe un numero di coperti assurdo.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task CopertiNonPositivi_VengonoTrattatiComeUnoSolo(int richiesti)
    {
        Setup(
            fasce: new[] { Fascia(1, maxCoperti: 50) },
            postazioniAttive: new[] { Tavolo(1, 2) },
            prenotazioni: Array.Empty<Prenotazione>());

        var res = await CreateService().CheckDisponibilitaAsync(new CheckDisponibilitaDTO
        {
            DataPrenotazione = Lunedi,
            NumeroCoperti = richiesti
        });

        var fascia = Fascia(res, 1);
        Assert.True(fascia.DisponibilePerRichiesta);
        Assert.Single(fascia.Postazioni);
    }

    /// <summary>Giorno di chiusura: nessuna fascia, risposta vuota e non un errore.</summary>
    [Fact]
    public async Task NessunaFasciaNelGiorno_RestituisceRispostaVuota()
    {
        Setup(
            fasce: Array.Empty<FasciaOraria>(),
            postazioniAttive: new[] { Tavolo(1, 4) },
            prenotazioni: Array.Empty<Prenotazione>());

        var res = await CreateService().CheckDisponibilitaAsync(new CheckDisponibilitaDTO
        {
            DataPrenotazione = Lunedi,
            NumeroCoperti = 2
        });

        Assert.Empty(res.Fasce);
    }

    /// <summary>
    /// Quando la fascia non è disponibile non si propone nessun tavolo: una lista popolata
    /// insieme a <c>DisponibilePerRichiesta = false</c> darebbe al frontend un'indicazione
    /// contraddittoria.
    /// </summary>
    [Fact]
    public async Task FasciaNonDisponibile_NonPropostoNessunTavolo()
    {
        Setup(
            fasce: new[] { Fascia(1, maxCoperti: 50) },
            postazioniAttive: new[] { Tavolo(1, 4) },
            prenotazioni: new[] { Prenotazione(fasciaId: 1, coperti: 4, (1, 4)) });

        var res = await CreateService().CheckDisponibilitaAsync(new CheckDisponibilitaDTO
        {
            DataPrenotazione = Lunedi,
            NumeroCoperti = 4
        });

        var fascia = Fascia(res, 1);
        Assert.False(fascia.DisponibilePerRichiesta);
        Assert.Empty(fascia.Postazioni);
    }

    // Alle 16:00 il pranzo di oggi risultava ancora disponibile e la prenotazione poi falliva
    // con 409 "fascia gia' passata". Ora la verifica guarda l'ora come la prenotazione.
    [Fact]
    public async Task FasciaDiOggiGiaFinita_NonDisponibile()
    {
        Setup(fasce: new[] { Fascia(1, maxCoperti: 20) }, postazioniAttive: new[] { Tavolo(1, 4) },
              prenotazioni: Array.Empty<Prenotazione>());
        _clock.UtcNow = new DateTime(2026, 9, 7, 20, 0, 0, DateTimeKind.Utc); // 22:00 a Roma, fascia 19-21

        var res = await CreateService().CheckDisponibilitaAsync(new CheckDisponibilitaDTO { DataPrenotazione = Lunedi, NumeroCoperti = 2 });

        var fascia = Fascia(res, 1);
        Assert.False(fascia.DisponibilePerRichiesta);
        Assert.Empty(fascia.Postazioni);
        Assert.Contains("terminata", fascia.Messaggio);
    }

    [Fact]
    public async Task FasciaDiOggiInCorso_AncoraDisponibile()
    {
        Setup(fasce: new[] { Fascia(1, maxCoperti: 20) }, postazioniAttive: new[] { Tavolo(1, 4) },
              prenotazioni: Array.Empty<Prenotazione>());
        _clock.UtcNow = new DateTime(2026, 9, 7, 18, 0, 0, DateTimeKind.Utc); // 20:00 a Roma, fascia 19-21

        var res = await CreateService().CheckDisponibilitaAsync(new CheckDisponibilitaDTO { DataPrenotazione = Lunedi, NumeroCoperti = 2 });

        Assert.True(Fascia(res, 1).DisponibilePerRichiesta);
    }

    // ── V2-007 — il motivo come codice, per la pagina pubblica ───────────────

    private Task<DisponibilitaResponseDTO> Verifica(int coperti) =>
        CreateService().CheckDisponibilitaAsync(new CheckDisponibilitaDTO { DataPrenotazione = Lunedi, NumeroCoperti = coperti });

    [Fact]
    public async Task Motivo_Libera_QuandoTettoETavoliBastano()
    {
        Setup(fasce: new[] { Fascia(1, maxCoperti: 20) }, postazioniAttive: new[] { Tavolo(1, 4) },
              prenotazioni: Array.Empty<Prenotazione>());

        var fascia = Fascia(await Verifica(4), 1);

        Assert.True(fascia.DisponibilePerRichiesta);
        Assert.Equal(MotivoDisponibilita.Libera, fascia.Motivo);
    }

    [Fact]
    public async Task Motivo_Terminata_VinceSuOgniAltroMotivo()
    {
        // Fascia finita E tetto esaurito: per il cliente conta che il turno è concluso.
        Setup(fasce: new[] { Fascia(1, maxCoperti: 4) }, postazioniAttive: new[] { Tavolo(1, 4) },
              prenotazioni: new[] { Prenotazione(fasciaId: 1, coperti: 4, (1, 4)) });
        _clock.UtcNow = new DateTime(2026, 9, 7, 20, 0, 0, DateTimeKind.Utc); // 22:00 a Roma, fascia 19-21

        Assert.Equal(MotivoDisponibilita.Terminata, Fascia(await Verifica(2), 1).Motivo);
    }

    [Fact]
    public async Task Motivo_TettoEsaurito_QuandoNonRestaNessunCoperto()
    {
        Setup(fasce: new[] { Fascia(1, maxCoperti: 4) }, postazioniAttive: new[] { Tavolo(1, 4), Tavolo(2, 4) },
              prenotazioni: new[] { Prenotazione(fasciaId: 1, coperti: 4, (1, 4)) });

        Assert.Equal(MotivoDisponibilita.TettoEsaurito, Fascia(await Verifica(2), 1).Motivo);
    }

    [Fact]
    public async Task Motivo_PostiInsufficienti_QuandoRestanoMenoCopertiDiQuelliRichiesti()
    {
        Setup(fasce: new[] { Fascia(1, maxCoperti: 10) }, postazioniAttive: new[] { Tavolo(1, 8), Tavolo(2, 8) },
              prenotazioni: new[] { Prenotazione(fasciaId: 1, coperti: 7, (1, 7)) });

        var fascia = Fascia(await Verifica(4), 1);

        Assert.Equal(MotivoDisponibilita.PostiInsufficienti, fascia.Motivo);
        Assert.Equal(3, fascia.PostiResiduiFascia);
    }

    [Fact]
    public async Task Motivo_TavoliInsufficienti_IlCasoDelBugSegnalato()
    {
        // Il caso di V2-007: tetto 60, in sala solo 2 tavoli da 2, richiesta di 50 persone.
        Setup(fasce: new[] { Fascia(1, maxCoperti: 60) }, postazioniAttive: new[] { Tavolo(1, 2), Tavolo(2, 2) },
              prenotazioni: Array.Empty<Prenotazione>());

        var fascia = Fascia(await Verifica(50), 1);

        Assert.False(fascia.DisponibilePerRichiesta);
        Assert.Equal(MotivoDisponibilita.TavoliInsufficienti, fascia.Motivo);
    }

    // ── V2-024 — i posti rimasti devono stare su un tavolo libero ────────────

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public async Task UnSoloTavoloGiaAssegnato_NessunPostoRimasto_IlCasoDelBugSegnalato(int richiesti)
    {
        // V2-024: tetto 8, un solo tavolo da 8 già assegnato a una prenotazione da 5. Il tetto
        // lascerebbe 3 coperti, ma sono il resto di quel tavolo: non si possono prenotare.
        Setup(fasce: new[] { Fascia(1, maxCoperti: 8) }, postazioniAttive: new[] { Tavolo(1, 8) },
              prenotazioni: new[] { Prenotazione(fasciaId: 1, coperti: 5, (1, 5)) });

        var fascia = Fascia(await Verifica(richiesti), 1);

        Assert.False(fascia.DisponibilePerRichiesta);
        Assert.Equal(0, fascia.PostiResiduiFascia);
        Assert.Equal(0, fascia.TotalePostiDisponibili);
        Assert.Equal(MotivoDisponibilita.TavoliInsufficienti, fascia.Motivo);
    }

    [Fact]
    public async Task PostiRimasti_SonoIlMinoreFraTettoETavoliLiberi()
    {
        // Tetto residuo 20 - 4 = 16, ma i tavoli liberi accolgono al massimo 6 persone (2 + 4).
        Setup(fasce: new[] { Fascia(1, maxCoperti: 20) },
              postazioniAttive: new[] { Tavolo(1, 4), Tavolo(2, 2), Tavolo(3, 4) },
              prenotazioni: new[] { Prenotazione(fasciaId: 1, coperti: 4, (1, 4)) });

        var fascia = Fascia(await Verifica(8), 1);

        Assert.False(fascia.DisponibilePerRichiesta);
        Assert.Equal(6, fascia.PostiResiduiFascia);
        Assert.Equal(MotivoDisponibilita.TavoliInsufficienti, fascia.Motivo);
    }

    [Fact]
    public void Motivo_NelJsonArrivaComeTesto_NonComeNumero()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            new FasciaDisponibilitaDTO { Motivo = MotivoDisponibilita.TavoliInsufficienti });

        Assert.Contains("\"Motivo\":\"TavoliInsufficienti\"", json);
    }
}
