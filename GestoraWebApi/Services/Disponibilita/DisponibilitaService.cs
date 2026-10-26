using GestoraWebApi.Common;
using GestoraWebApi.Enums;
using GestoraWebApi.Models;
using GestoraWebApi.Repositories.Postazioni;
using GestoraWebApi.Repositories.Prenotazioni;
using GestoraWebApi.Repositories.Zone;
using GestoraWebApi.Services.PostazioneAssignment;
using GestoraWebApi.Services.PrenotazioniPostazioni;

namespace GestoraWebApi.Services.Disponibilita
{
    /// <summary>
    /// Verifica di disponibilità dell'endpoint pubblico. Checkpoint 2c: usa lo stesso motore
    /// <see cref="AssegnazioneTavoli"/> dell'assegnazione reale e basa i posti residui sul tetto
    /// della fascia (<c>MaxCoperti</c>), non sulla somma delle capienze dei tavoli.
    /// </summary>
    public class DisponibilitaService : IDisponibilitaService
    {
        private readonly IPrenotazioniRepository _prenotazioniRepository;
        private readonly IPostazioneRepository _postazioneRepository;
        private readonly IZonaRepository _zonaRepository;
        private readonly IClock _clock;

        public DisponibilitaService(IPrenotazioniRepository prenotazioniRepository,
                                    IPostazioneRepository postazioneRepository,
                                    IZonaRepository zonaRepository,
                                    IClock clock)
        {
            _prenotazioniRepository = prenotazioniRepository;
            _postazioneRepository = postazioneRepository;
            _zonaRepository = zonaRepository;
            _clock = clock;
        }

        public async Task<DisponibilitaResponseDTO> CheckDisponibilitaAsync(CheckDisponibilitaDTO dto)
        {
            var richiesti = Math.Max(1, dto.NumeroCoperti);

            var fasce = await _prenotazioniRepository.GetFasceOrarieByDayAsync(dto.DataPrenotazione.DayOfWeek);
            var prenotazioni = await _prenotazioniRepository.GetPrenotazioniByDataAsync(dto.DataPrenotazione);

            // REV-024: solo tavoli attivi in zone attive concorrono alla disponibilità, come
            // per l'assegnazione reale.
            var zoneAttiveIds = (await _zonaRepository.GetAllZoneAttiveAsync()).Select(z => z.Id).ToHashSet();
            var postazioni = (await _postazioneRepository.GetPostazioniAttiveAsync())
                .Where(p => zoneAttiveIds.Contains(p.ZonaId))
                .ToList();

            var response = new DisponibilitaResponseDTO();

            foreach (var f in fasce)
            {
                var prenotazioniFascia = prenotazioni.Where(p => p.FasciaOrariaId == f.Id).ToList();

                // Stessa regola di ValidatePrenotazioneAsync: una fascia finita non si prenota
                // piu'. Prima qui non si guardava l'ora: alle 16:00 il pranzo 12:00-15:00 di oggi
                // risultava disponibile e la prenotazione poi falliva con 409.
                var fasciaFinita = _clock.NowInRome >=
                    dto.DataPrenotazione.ToDateTime(TimeOnly.MinValue).Add(f.OrarioFine.ToTimeSpan());

                // Tavoli fisicamente liberi in questa fascia.
                var occupateIds = prenotazioniFascia
                    .SelectMany(p => p.PrenotazioniPostazioni ?? Enumerable.Empty<PrenotazionePostazione>())
                    .Select(pp => pp.PostazioneId)
                    .ToHashSet();

                var libere = postazioni.Where(p => !occupateIds.Contains(p.Id)).ToList();

                // Tetto della fascia: coperti già impegnati (non il conteggio delle prenotazioni).
                var copertiPrenotati = prenotazioniFascia.Sum(p => p.NumeroCoperti);
                var tettoResiduo = Math.Max(0, f.MaxCoperti - copertiPrenotati);

                // V2-024: i posti rimasti devono stare su un tavolo libero. Prima erano solo il tetto
                // residuo: con un tavolo da 8 occupato da 5 persone risultavano «3 posti», che sono il
                // resto di quel tavolo e non si possono prenotare.
                var gruppoMassimo = AssegnazioneTavoli.GruppoMassimo(libere);
                var postiResidui = Math.Min(tettoResiduo, gruppoMassimo);
                var limitanoITavoli = gruppoMassimo < tettoResiduo;
                var tettoSufficiente = tettoResiduo >= richiesti;

                // Stesso motore dell'assegnazione reale.
                var combinazione = AssegnazioneTavoli.TrovaMigliorCombinazione(libere, richiesti);
                var tavoliSufficienti = combinazione != null && !fasciaFinita;

                var fasciaDto = new FasciaDisponibilitaDTO
                {
                    FasciaOrariaId = f.Id,
                    OrarioInizio = f.OrarioInizio,
                    OrarioFine = f.OrarioFine,
                    MaxCoperti = f.MaxCoperti,
                    PostiResiduiFascia = postiResidui,
                    TotalePostiDisponibili = postiResidui,
                    TotaleCapienza = postazioni.Sum(p => p.CapienzaMassima),
                    DisponibilePerRichiesta = tettoSufficiente && tavoliSufficienti
                };

                // V2-007: Motivo e Messaggio nascono negli stessi rami, così non possono divergere.
                // Messaggio è per lo Staff; Motivo serve alla pagina pubblica per la sua frase.
                if (fasciaFinita)
                {
                    fasciaDto.Motivo = MotivoDisponibilita.Terminata;
                    fasciaDto.Messaggio = "La fascia oraria è già terminata.";
                }
                else if (limitanoITavoli && postiResidui < richiesti)
                {
                    // V2-024: a fermare la richiesta sono i tavoli, non il tetto.
                    fasciaDto.Motivo = MotivoDisponibilita.TavoliInsufficienti;
                    fasciaDto.Messaggio = postiResidui <= 0
                        ? "Nessun tavolo libero in questa fascia: sono già tutti assegnati."
                        : $"I tavoli liberi accolgono al massimo {postiResidui} persone, non sufficienti per le {richiesti} richieste.";
                }
                else if (!tettoSufficiente)
                {
                    if (postiResidui <= 0)
                    {
                        fasciaDto.Motivo = MotivoDisponibilita.TettoEsaurito;
                        fasciaDto.Messaggio = "La fascia oraria ha raggiunto la capienza massima: nessun coperto disponibile.";
                    }
                    else
                    {
                        fasciaDto.Motivo = MotivoDisponibilita.PostiInsufficienti;
                        fasciaDto.Messaggio = $"La fascia oraria ha ancora {postiResidui} coperti disponibili, non sufficienti per i {richiesti} richiesti.";
                    }
                }
                else if (!tavoliSufficienti)
                {
                    fasciaDto.Motivo = MotivoDisponibilita.TavoliInsufficienti;
                    fasciaDto.Messaggio =
                        $"Il tetto della fascia lascerebbe spazio ({postiResidui} coperti), ma nessuna combinazione di tavoli liberi copre {richiesti} persone.";
                }
                else
                {
                    fasciaDto.Motivo = MotivoDisponibilita.Libera;
                }

                if (tavoliSufficienti)
                {
                    var occupatiPerTavolo = prenotazioniFascia
                        .SelectMany(p => p.PrenotazioniPostazioni ?? Enumerable.Empty<PrenotazionePostazione>())
                        .GroupBy(pp => pp.PostazioneId)
                        .ToDictionary(g => g.Key, g => g.Sum(pp => pp.NumeroPosti));

                    fasciaDto.Postazioni = combinazione!
                        .Select(p => new PostazioneDisponibilitaDTO
                        {
                            PostazioneId = p.Id,
                            Numero = p.Numero,
                            Capienza = p.CapienzaMassima,
                            PostiOccupati = occupatiPerTavolo.TryGetValue(p.Id, out var occ) ? occ : 0,
                            PostiDisponibili = Math.Max(0, p.CapienzaMassima - (occupatiPerTavolo.TryGetValue(p.Id, out var o2) ? o2 : 0)),
                            DisponibilePerRichiesta = true
                        })
                        .ToList();
                }

                response.Fasce.Add(fasciaDto);
            }

            return response;
        }
    }
}
