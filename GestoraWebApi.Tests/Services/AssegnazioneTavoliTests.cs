using GestoraWebApi.Models;
using GestoraWebApi.Services.PostazioneAssignment;

namespace GestoraWebApi.Tests.Services;

/// <summary>
/// Test del motore di assegnazione (<see cref="AssegnazioneTavoli"/>): logica pura,
/// nessun mock necessario.
/// </summary>
public class AssegnazioneTavoliTests
{
    private static Postazione Tavolo(long id, int capienza, long zonaId = 1) =>
        new Postazione { Id = id, Numero = (int)id, CapienzaMassima = capienza, ZonaId = zonaId };

    // --- Capienza di un'unione: bonus testate solo per unioni di soli tavoli da 2 ---

    [Fact]
    public void CalcolaCapienza_TavoloSingolo_NessunBonus()
    {
        var capienza = AssegnazioneTavoli.CalcolaCapienza(new[] { Tavolo(1, 2) });

        Assert.Equal(2, capienza);
    }

    [Fact]
    public void CalcolaCapienza_UnioneDiSoliTavoliDa2_AggiungeBonusTestate()
    {
        // 2 + 2 = 4, più i 2 posti sulle testate = 6
        var capienza = AssegnazioneTavoli.CalcolaCapienza(new[] { Tavolo(1, 2), Tavolo(2, 2) });

        Assert.Equal(6, capienza);
    }

    [Fact]
    public void CalcolaCapienza_UnioneMista_NonAggiungeBonus()
    {
        // Un tavolo da 2 unito a uno da 6: somma semplice, niente bonus.
        var capienza = AssegnazioneTavoli.CalcolaCapienza(new[] { Tavolo(1, 2), Tavolo(2, 6) });

        Assert.Equal(8, capienza);
    }

    // --- V2-024: il gruppo più grande che i tavoli liberi accolgono ---

    [Fact]
    public void GruppoMassimo_NessunTavoloLibero_Zero()
    {
        Assert.Equal(0, AssegnazioneTavoli.GruppoMassimo(new List<Postazione>()));
    }

    [Fact]
    public void GruppoMassimo_PiuZone_LaZonaMigliore_SenzaUnireZoneDiverse()
    {
        // Zona 1: 4 + 6 = 10. Zona 2: 8. Le due zone non si uniscono: 10, non 18.
        var libere = new List<Postazione> { Tavolo(1, 4, zonaId: 1), Tavolo(2, 6, zonaId: 1), Tavolo(3, 8, zonaId: 2) };

        Assert.Equal(10, AssegnazioneTavoli.GruppoMassimo(libere));
    }

    [Fact]
    public void GruppoMassimo_SoliTavoliDa2_ContaIlBonusTestate()
    {
        // 2 + 2 + 2 = 6, più i 2 posti sulle testate = 8
        var libere = new List<Postazione> { Tavolo(1, 2), Tavolo(2, 2), Tavolo(3, 2) };

        Assert.Equal(8, AssegnazioneTavoli.GruppoMassimo(libere));
    }

    // --- Scelta della combinazione: meno posti sprecati ---

    [Fact]
    public void TrovaMigliorCombinazione_PreferisceIlTavoloCheSprecaMeno()
    {
        var postazioni = new List<Postazione> { Tavolo(1, 8), Tavolo(2, 4) };

        var risultato = AssegnazioneTavoli.TrovaMigliorCombinazione(postazioni, numeroCoperti: 3);

        Assert.NotNull(risultato);
        Assert.Single(risultato!);
        Assert.Equal(2, risultato![0].Id);
    }

    [Fact]
    public void TrovaMigliorCombinazione_NonOccupaTavoloDa8PerDuePersone_SeEsisteAlternativa()
    {
        var postazioni = new List<Postazione> { Tavolo(1, 8), Tavolo(2, 2) };

        var risultato = AssegnazioneTavoli.TrovaMigliorCombinazione(postazioni, numeroCoperti: 2);

        Assert.NotNull(risultato);
        Assert.Single(risultato!);
        Assert.Equal(2, risultato![0].Id);
    }

    [Fact]
    public void TrovaMigliorCombinazione_PreferisceUnione_SeSprecaMenoDelSingolo()
    {
        // Per 6 coperti: il tavolo da 8 spreca 2, l'unione di due da 2 vale 6 e non spreca nulla.
        var postazioni = new List<Postazione> { Tavolo(1, 8), Tavolo(2, 2), Tavolo(3, 2) };

        var risultato = AssegnazioneTavoli.TrovaMigliorCombinazione(postazioni, numeroCoperti: 6);

        Assert.NotNull(risultato);
        Assert.Equal(2, risultato!.Count);
        Assert.DoesNotContain(risultato, p => p.Id == 1);
    }

    [Fact]
    public void TrovaMigliorCombinazione_APariSpreco_PreferisceMenoTavoli()
    {
        // Per 4 coperti: il tavolo da 4 non spreca nulla, come non sprecherebbe
        // l'unione di due da 2 (che varrebbe 6). Vince il tavolo singolo.
        var postazioni = new List<Postazione> { Tavolo(1, 4), Tavolo(2, 2), Tavolo(3, 2) };

        var risultato = AssegnazioneTavoli.TrovaMigliorCombinazione(postazioni, numeroCoperti: 4);

        Assert.NotNull(risultato);
        Assert.Single(risultato!);
        Assert.Equal(1, risultato![0].Id);
    }

    [Fact]
    public void TrovaMigliorCombinazione_UnisceFinoAQuattroTavoli()
    {
        // Quattro tavoli da 2: capienza 8 + 2 di testate = 10 coperti.
        var postazioni = new List<Postazione> { Tavolo(1, 2), Tavolo(2, 2), Tavolo(3, 2), Tavolo(4, 2) };

        var risultato = AssegnazioneTavoli.TrovaMigliorCombinazione(postazioni, numeroCoperti: 10);

        Assert.NotNull(risultato);
        Assert.Equal(4, risultato!.Count);
    }

    // --- V2-007: niente più limite di 4 tavoli per unione ---

    [Fact]
    public void TrovaMigliorCombinazione_UnisceAncheCinqueTavoli()
    {
        // Prima il limite era 4 (10 coperti): ora per 11 persone si uniscono 5 tavoli da 2 (12).
        var postazioni = Enumerable.Range(1, 5).Select(i => Tavolo(i, 2)).ToList();

        var risultato = AssegnazioneTavoli.TrovaMigliorCombinazione(postazioni, numeroCoperti: 11);

        Assert.NotNull(risultato);
        Assert.Equal(5, risultato!.Count);
    }

    [Fact]
    public void TrovaMigliorCombinazione_GruppoDiVentiSuSoliTavoliDa2()
    {
        // Il gruppo più grande che si può prenotare online: 9 tavoli da 2 = 18 + 2 di testate.
        var postazioni = Enumerable.Range(1, 15).Select(i => Tavolo(i, 2)).ToList();

        var risultato = AssegnazioneTavoli.TrovaMigliorCombinazione(postazioni, numeroCoperti: 20);

        Assert.NotNull(risultato);
        Assert.Equal(9, risultato!.Count);
        Assert.Equal(20, AssegnazioneTavoli.CalcolaCapienza(risultato));
    }

    [Fact]
    public void TrovaMigliorCombinazione_NonAllungaUnUnioneCheBastaGia()
    {
        // Per 6: due tavoli da 2 (6 con le testate) bastano. Unirne altri sprecherebbe posti.
        var postazioni = Enumerable.Range(1, 8).Select(i => Tavolo(i, 2)).ToList();

        var risultato = AssegnazioneTavoli.TrovaMigliorCombinazione(postazioni, numeroCoperti: 6);

        Assert.NotNull(risultato);
        Assert.Equal(2, risultato!.Count);
    }

    [Fact]
    public void TrovaMigliorCombinazione_SalaGrandeConCapienzeMiste_TrovaLaSoluzioneSenzaSpreco()
    {
        // 60 tavoli di 6 capienze diverse: senza limite di tavoli il calcolo deve restare
        // immediato (criterio di arresto) e trovare comunque l'unione esatta.
        var capienze = new[] { 1, 2, 3, 4, 6, 8 };
        var postazioni = Enumerable.Range(1, 60).Select(i => Tavolo(i, capienze[i % capienze.Length])).ToList();

        var risultato = AssegnazioneTavoli.TrovaMigliorCombinazione(postazioni, numeroCoperti: 50);

        Assert.NotNull(risultato);
        Assert.Equal(50, AssegnazioneTavoli.CalcolaCapienza(risultato!));
    }

    [Fact]
    public void TrovaMigliorCombinazione_TuttiITavoliDellaZonaNonBastano_RestituisceNull()
    {
        var postazioni = Enumerable.Range(1, 6).Select(i => Tavolo(i, 4)).ToList(); // 24 posti

        var risultato = AssegnazioneTavoli.TrovaMigliorCombinazione(postazioni, numeroCoperti: 25);

        Assert.Null(risultato);
    }

    [Fact]
    public void TrovaMigliorCombinazione_NonUnisceTavoliDiZoneDiverse()
    {
        var postazioni = new List<Postazione> { Tavolo(1, 2, zonaId: 1), Tavolo(2, 2, zonaId: 2) };

        var risultato = AssegnazioneTavoli.TrovaMigliorCombinazione(postazioni, numeroCoperti: 4);

        Assert.Null(risultato);
    }

    [Fact]
    public void TrovaMigliorCombinazione_ReturnsNull_QuandoLaCapienzaNonBasta()
    {
        var postazioni = new List<Postazione> { Tavolo(1, 2) };

        var risultato = AssegnazioneTavoli.TrovaMigliorCombinazione(postazioni, numeroCoperti: 5);

        Assert.Null(risultato);
    }

    [Fact]
    public void TrovaMigliorCombinazione_SupportaCapienzeNonStandard()
    {
        // Il vincolo "solo tavoli da 2, 4 e 8" è stato rimosso: qui vince il tavolo da 3.
        var postazioni = new List<Postazione> { Tavolo(1, 5), Tavolo(2, 3) };

        var risultato = AssegnazioneTavoli.TrovaMigliorCombinazione(postazioni, numeroCoperti: 3);

        Assert.NotNull(risultato);
        Assert.Equal(2, risultato![0].Id);
    }

    // --- Distribuzione dei coperti sui tavoli assegnati ---

    [Fact]
    public void DistribuisciCoperti_RipartisceSenzaSuperareLaCapienzaDelSingoloTavolo()
    {
        var combinazione = new List<Postazione> { Tavolo(1, 6), Tavolo(2, 4) };

        var distribuzione = AssegnazioneTavoli.DistribuisciCoperti(combinazione, numeroCoperti: 8);

        Assert.Equal(6, distribuzione[1]);
        Assert.Equal(2, distribuzione[2]);
    }

    [Fact]
    public void DistribuisciCoperti_AssegnaAncheIPostiDiTestata()
    {
        // Due tavoli da 2 uniti valgono 6: 2 + 2 nominali, più 1 + 1 sulle testate.
        var combinazione = new List<Postazione> { Tavolo(1, 2), Tavolo(2, 2) };

        var distribuzione = AssegnazioneTavoli.DistribuisciCoperti(combinazione, numeroCoperti: 6);

        Assert.Equal(6, distribuzione.Values.Sum());
        Assert.Equal(3, distribuzione[1]);
        Assert.Equal(3, distribuzione[2]);
    }

    [Fact]
    public void DistribuisciCoperti_LaSommaCorrispondeSempreAiCopertiRichiesti()
    {
        var combinazione = new List<Postazione> { Tavolo(1, 4), Tavolo(2, 4) };

        var distribuzione = AssegnazioneTavoli.DistribuisciCoperti(combinazione, numeroCoperti: 5);

        Assert.Equal(5, distribuzione.Values.Sum());
    }
}
