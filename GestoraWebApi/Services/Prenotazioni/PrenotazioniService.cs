using AutoMapper;
using GestoraWebApi.Auth;
using GestoraWebApi.Common;
using GestoraWebApi.Context;
using GestoraWebApi.Enums;
using GestoraWebApi.Extensions;
using GestoraWebApi.Models;
using GestoraWebApi.Repositories.FasciaOrarie;
using GestoraWebApi.Repositories.Prenotazioni;
using GestoraWebApi.Repositories.Zone;
using GestoraWebApi.Services.LogActivity;
using GestoraWebApi.Services.PostazioneAssignment;
using GestoraWebApi.Services.Prenotazioni.DTOs;
using GestoraWebApi.Services.PrenotazioniPostazioni;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using GestoraWebApi.Infrastructure.Exceptions;

namespace GestoraWebApi.Services.Prenotazioni
{
    public class PrenotazioniService : IPrenotazioniService
    {
        private readonly IPrenotazioniRepository _prenotazioniRepository;
        private readonly IPostazioneAssignmentService _postazioneAssignmentService;
        private readonly IFasciaOrariaRepository _fasciaOrariaRepository;
        private readonly IZonaRepository _zonaRepository;
        private readonly IMapper _mapper;
        private readonly GestoraContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<PrenotazioniService> _logger;
        private readonly ILogActivityService _logActivity;
        private readonly IClock _clock;
        private readonly PrenotazioniSettings _impostazioni;

        /// <summary>REV-061: retention delle prenotazioni completate, prima un -6 hardcoded in mezzo al metodo.</summary>
        private const int MesiRetentionCompletate = 6;

        public PrenotazioniService(IPrenotazioniRepository prenotazioniRepository,
                                   IPostazioneAssignmentService postazioneAssignmentService,
                                   IFasciaOrariaRepository fasciaOrariaRepository,
                                   IMapper mapper,
                                   GestoraContext context,
                                   IHttpContextAccessor httpContextAccessor,
                                   IZonaRepository zonaRepository,
                                   ILogger<PrenotazioniService> logger,
                                   ILogActivityService logActivity,
                                   IClock clock,
                                   IOptions<PrenotazioniSettings> impostazioni)
        {
            _impostazioni = impostazioni.Value;
            _prenotazioniRepository = prenotazioniRepository;
            _postazioneAssignmentService = postazioneAssignmentService;
            _fasciaOrariaRepository = fasciaOrariaRepository;
            _mapper = mapper;
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _zonaRepository = zonaRepository;
            _logger = logger;
            _logActivity = logActivity;
            _clock = clock;
        }

        public async Task AddAsync(PrenotazioneCreateDTO dto)
        {
            var userId = _httpContextAccessor.HttpContext.GetAuthenticatedUserId();

            // REV-003: verifica di disponibilita', scelta del tavolo e scrittura sono una sola
            // operazione atomica. Fuori dalla transazione, fra "il tavolo risulta libero" e
            // "la riga e' scritta" c'e' una finestra in cui un altro utente puo' prendersi lo
            // stesso tavolo; e' l'unique index a chiudere la corsa, la transazione a garantire
            // che non resti scritto niente a meta'.
            await EseguiInTransazioneAsync(async () =>
            {
                await ValidatePrenotazioneAsync(dto);

                var selfService = IsSelfServiceCliente();

                if (selfService)
                {
                    GuardLimiteOnline(dto.NumeroCoperti);
                    await GuardUnaPrenotazioneAlGiornoAsync(userId, dto.DataPrenotazione);
                }

                var postazioniAssegnate = await _postazioneAssignmentService.AssegnaPostazioneDisponibileAsync(dto);

                var prenotazione = new Prenotazione
                {
                    UserId = userId,
                    DataPrenotazione = (DateOnly)dto.DataPrenotazione,
                    NumeroCoperti = dto.NumeroCoperti,
                    FasciaOrariaId = dto.FasciaOrariaId,
                    // REV-033: NomeCliente serve a Staff/Admin per annotare a nome di chi e' la
                    // prenotazione presa al telefono. Nel self-service il cliente e' l'utente
                    // autenticato, quindi il campo si ignora invece di rifiutare la richiesta:
                    // il form del Cliente non lo espone, un 403 sarebbe solo rumore.
                    NomeCliente = selfService ? null : dto.NomeCliente,
                    Note = dto.Note,
                    Stato = StatoPrenotazione.Attiva,
                };

                prenotazione.PrenotazioniPostazioni = postazioniAssegnate
                    .Select(a => CreaRigaPostazione(prenotazione, a))
                    .ToList();

                await _prenotazioniRepository.AddAsync(prenotazione);

                // REV-032 (parziale): il log sta nella stessa transazione della scrittura che
                // registra. Se fallisce, la prenotazione non resta scritta e non tracciata.
                await _logActivity.LogAsync(userId, $"Creata prenotazione per data {dto.DataPrenotazione:yyyy-MM-dd}, {dto.NumeroCoperti} coperti", _httpContextAccessor.HttpContext.GetIpAddress());
            });
        }

        public async Task DeleteAsync(long id)
        {
            // Entita' tracciata di proposito: GetByIdAsync e' AsNoTracking e porta con se User,
            // tavoli e zone. Con due tavoli della stessa zona la stessa Zona compare come due
            // istanze distinte e Remove() va in errore ("cannot be tracked") -> 500.
            var prenotazione = await _prenotazioniRepository.GetTrackedByIdAsync(id);

            if (prenotazione == null)
                throw new KeyNotFoundException($"Prenotazione con Id {id} non trovata.");

            // Si elimina solo cio' che e' stato annullato: le altre restano nello storico.
            if (prenotazione.Stato != StatoPrenotazione.Annullata)
                throw new ConflictException("Si possono eliminare solo le prenotazioni annullate. Annulla prima la prenotazione.");

            // V2-014: eliminazione e traccia sono una sola operazione, come in tutte le altre
            // scritture. Prima, se il registro falliva, la prenotazione era gia' sparita senza
            // lasciare nessuna traccia di chi l'aveva eliminata.
            await EseguiInTransazioneAsync(async () =>
            {
                await _prenotazioniRepository.DeleteAsync(prenotazione);
                await _logActivity.LogAsync(_httpContextAccessor.HttpContext.GetAuthenticatedUserId(), $"Eliminata prenotazione ID {id}", _httpContextAccessor.HttpContext.GetIpAddress());
            });
        }

        public async Task<PrenotazioneDTO> GetByIdAsync(long id)
        {
            var prenotazione = await _prenotazioniRepository.GetByIdAsync(id);

            if (prenotazione == null)
                throw new KeyNotFoundException("Prenotazione non trovata.");

            // REV-034: il Cliente può leggere il dettaglio solo della propria prenotazione.
            // Admin/Staff nessun limite. Per il Cliente quella altrui risulta "non trovata", come
            // un Id inesistente: con 403 contro 404 si capiva quali Id esistono.
            if (IsSelfServiceCliente()
                && !string.Equals(prenotazione.UserId, _httpContextAccessor.HttpContext.GetAuthenticatedUserId(), StringComparison.OrdinalIgnoreCase))
                throw new KeyNotFoundException("Prenotazione non trovata.");

            var dtoSingola = _mapper.Map<PrenotazioneDTO>(prenotazione);
            await ApplicaNumeroTurnoAsync(new[] { prenotazione }, new[] { dtoSingola });
            return dtoSingola;
        }

        public async Task UpdateAsync(long id, PrenotazioneCreateDTO dto)
        {
            var userId = _httpContextAccessor.HttpContext.GetAuthenticatedUserId();

            await EseguiInTransazioneAsync(async () =>
            {
                // AUD-M4: lettura e controlli dentro la transazione, dopo il lock sulla riga.
                // Prima stato e permessi si leggevano fuori: un annullamento arrivato nel mezzo
                // veniva sovrascritto e la prenotazione tornava "Attiva".
                var prenotazione = await LeggiConLockAsync(id, "Prenotazione non trovata.");
                NascondiSeAltrui(prenotazione, "Prenotazione non trovata.");

                if (prenotazione.Stato == StatoPrenotazione.InCorso
                    || prenotazione.Stato == StatoPrenotazione.Annullata
                    || prenotazione.Stato == StatoPrenotazione.Completata
                    || prenotazione.Stato == StatoPrenotazione.NonPresentata)
                    throw new ConflictException($"Non è possibile modificare una prenotazione nello stato {prenotazione.Stato}.");

                // REV-002: il vincolo di ownership vale solo per il self-service del Cliente.
                // Admin e Staff possono modificare la prenotazione di qualunque cliente (creata
                // sotto un altro UserId), come previsto dai ruoli.
                if (IsSelfServiceCliente())
                {
                    GuardCutoffAsync(prenotazione);
                    GuardLimiteOnline(dto.NumeroCoperti);
                }

                // CAP-001: la verifica del tetto stava fuori dalla transazione, quindi il lock
                // sulla fascia non la copriva. Dentro, come in AddAsync.
                await ValidatePrenotazioneAsync(dto, prenotazione.Id);

                if (IsSelfServiceCliente() && dto.DataPrenotazione != prenotazione.DataPrenotazione)
                    await GuardUnaPrenotazioneAlGiornoAsync(userId, dto.DataPrenotazione, excludePrenotazioneId: prenotazione.Id);

                var postazioniAssegnate = await _postazioneAssignmentService.AssegnaPostazioneDisponibileAsync(dto, prenotazione.Id);

                prenotazione.DataPrenotazione = (DateOnly)dto.DataPrenotazione;
                prenotazione.NumeroCoperti = dto.NumeroCoperti;
                prenotazione.FasciaOrariaId = dto.FasciaOrariaId;
                prenotazione.Note = dto.Note;

                // REV-033: in self-service il campo resta com'e', il Cliente non puo' ne'
                // impostarlo ne' cancellarlo modificando la propria prenotazione.
                if (!IsSelfServiceCliente())
                    prenotazione.NomeCliente = dto.NomeCliente;

                if (prenotazione.PrenotazioniPostazioni != null && prenotazione.PrenotazioniPostazioni.Any())
                {
                    _context.PrenotazioniPostazioni.RemoveRange(prenotazione.PrenotazioniPostazioni);

                    // I DELETE devono arrivare al database PRIMA degli INSERT: EF non garantisce
                    // quest'ordine dentro una singola SaveChanges, e con l'unique index sullo slot
                    // una modifica che riassegna lo stesso tavolo verrebbe rifiutata da sola.
                    // Siamo dentro la transazione: se l'INSERT poi fallisce, il DELETE torna
                    // indietro con tutto il resto.
                    await _context.SaveChangesAsync();
                    prenotazione.PrenotazioniPostazioni = new List<PrenotazionePostazione>();
                }

                prenotazione.PrenotazioniPostazioni = postazioniAssegnate
                    .Select(a => CreaRigaPostazione(prenotazione, a))
                    .ToList();

                await _prenotazioniRepository.UpdateAsync(prenotazione);

                // REV-006: la modifica era l'unica scrittura su prenotazione non tracciata.
                // REV-032 (parziale): ora il log e' nella stessa transazione della modifica.
                await _logActivity.LogAsync(userId, $"Modificata prenotazione ID {id}", _httpContextAccessor.HttpContext.GetIpAddress());
            });
        }

        public async Task ConfermaPrenotazioneAsync(long id)
        {
            await EseguiInTransazioneAsync(async () =>
            {
                var prenotazione = await LeggiConLockAsync(id, "Prenotazione non trovata.");

                if (prenotazione.Stato != StatoPrenotazione.Attiva)
                    throw new ConflictException("Solo prenotazioni con stato 'Attiva' possono essere confermate.");

                // FASE 3: una "Attiva" con data/fascia gia' passate non e' piu' confermabile: il job
                // notturno non e' ancora passato a portarla a NonPresentata, ma il momento e' gia'
                // andato. Stessa condizione usata da AutomaticCompletPrenotazioniAsync per il no-show.
                if (prenotazione.FasciaOraria == null)
                    throw new ConflictException("La fascia oraria associata alla prenotazione non è disponibile.");

                var oraAttualeConferma = _clock.NowInRome;
                var fineFasciaConferma = prenotazione.DataPrenotazione.ToDateTime(TimeOnly.MinValue)
                                              .Add(prenotazione.FasciaOraria.OrarioFine.ToTimeSpan());
                if (oraAttualeConferma >= fineFasciaConferma)
                    throw new ConflictException("La prenotazione è già passata.");

                // Si conferma l'arrivo del cliente, quindi solo nel giorno della prenotazione. Prima
                // si poteva confermare per sbaglio una prenotazione della settimana dopo: passava
                // "In corso", il cliente non poteva piu' modificarla e il tetto restava occupato.
                if (prenotazione.DataPrenotazione != _clock.TodayInRome)
                    throw new ConflictException("Si può confermare una prenotazione solo nel giorno in cui è prevista.");

                prenotazione.Stato = StatoPrenotazione.InCorso;
                await _prenotazioniRepository.UpdateAsync(prenotazione);
                await _logActivity.LogAsync(_httpContextAccessor.HttpContext.GetAuthenticatedUserId(), $"Confermata prenotazione ID {id}", _httpContextAccessor.HttpContext.GetIpAddress());
            });
        }

        public async Task CompletePrenotazioneAsync(long id)
        {
            await EseguiInTransazioneAsync(async () =>
            {
                var prenotazione = await LeggiConLockAsync(id, "Prenotazione non trovata.");

                if (prenotazione.FasciaOraria == null)
                    throw new ConflictException("La fascia oraria associata alla prenotazione non è disponibile.");

                if (prenotazione.Stato != StatoPrenotazione.InCorso)
                    throw new ConflictException("Solo prenotazioni 'In corso' possono essere completate.");

                var now = _clock.NowInRome;
                var endDateTime = prenotazione.DataPrenotazione.ToDateTime(TimeOnly.MinValue)
                                              .Add(prenotazione.FasciaOraria.OrarioFine.ToTimeSpan());

                if (now < endDateTime)
                    throw new ConflictException("Non è possibile completare: la prenotazione non è ancora terminata.");

                prenotazione.Stato = StatoPrenotazione.Completata;
                await _prenotazioniRepository.UpdateAsync(prenotazione);
                await _logActivity.LogAsync(_httpContextAccessor.HttpContext.GetAuthenticatedUserId(), $"Completata prenotazione ID {id}", _httpContextAccessor.HttpContext.GetIpAddress());
            });
        }

        public async Task AnnullaPrenotazioneAsync(long id)
        {
            await EseguiInTransazioneAsync(async () =>
            {
                // AUD-M4: lettura e controlli dopo il lock sulla riga, come in UpdateAsync.
                var prenotazione = await LeggiConLockAsync(id, "Prenotazione non trovata nel sistema.");
                NascondiSeAltrui(prenotazione, "Prenotazione non trovata nel sistema.");

                if (prenotazione.Stato == StatoPrenotazione.Completata)
                    throw new ConflictException("Non è possibile annullare una prenotazione già completata.");

                if (prenotazione.Stato == StatoPrenotazione.NonPresentata)
                    throw new ConflictException("Non è possibile annullare una prenotazione mai confermata: è già segnata come non presentata.");

                if (prenotazione.Stato == StatoPrenotazione.Annullata)
                    throw new ConflictException("La prenotazione è già annullata.");

                // AUD-M3: una prenotazione la cui fascia e' finita non si annulla piu', nemmeno da
                // Staff/Admin. Il job notturno la porta a Completata o NonPresentata: annullandola
                // prima (e poi eliminandola) il no-show o il coperto servito sparivano dallo storico.
                if (prenotazione.FasciaOraria != null &&
                    _clock.NowInRome >= prenotazione.DataPrenotazione.ToDateTime(TimeOnly.MinValue)
                                            .Add(prenotazione.FasciaOraria.OrarioFine.ToTimeSpan()))
                    throw new ConflictException("La prenotazione è già passata: non si può più annullare.");

                if (IsSelfServiceCliente())
                    GuardCutoffAsync(prenotazione);

                prenotazione.Stato = StatoPrenotazione.Annullata;

                // REV-003: una prenotazione annullata libera il tavolo. Le righe join vanno
                // eliminate, non lasciate in tabella: con l'unique index pieno (senza WHERE)
                // continuerebbero a occupare lo slot e bloccherebbero ogni nuova prenotazione
                // su quel tavolo.
                if (prenotazione.PrenotazioniPostazioni != null && prenotazione.PrenotazioniPostazioni.Any())
                    _context.PrenotazioniPostazioni.RemoveRange(prenotazione.PrenotazioniPostazioni);

                await _prenotazioniRepository.UpdateAsync(prenotazione);
                await _logActivity.LogAsync(_httpContextAccessor.HttpContext.GetAuthenticatedUserId(), $"Annullata prenotazione ID {id}", _httpContextAccessor.HttpContext.GetIpAddress());
            });
        }

        public async Task<List<PrenotazioneDTO>> GetPrenotazioniByDataAsync(DateOnly data)
        {
            var prenotazioni = await _prenotazioniRepository.GetAllQueryableAsync()
                .Where(p => p.DataPrenotazione == data)
                .Include(p => p.User)
                .Include(p => p.FasciaOraria)
                .Include(p => p.PrenotazioniPostazioni)
                    .ThenInclude(pp => pp.Postazione)
                        .ThenInclude(po => po.Zona)
                .AsNoTracking()
                .ToListAsync();

            // REV-031: nessuna prenotazione in quella data non e' un errore, e' un risultato.
            // Il 404 su collezione vuota costringeva il chiamante a trattare uno stato normale
            // come eccezione: si restituisce una lista vuota.
            var dtos = _mapper.Map<List<PrenotazioneDTO>>(prenotazioni);
            await ApplicaNumeroTurnoAsync(prenotazioni, dtos);
            return dtos;
        }

        public async Task<List<PrenotazioneDTO>> GetMiePrenotazioniAsync(string userId)
        {
            var prenotazioni = await _prenotazioniRepository.GetAllQueryableAsync()
                .Where(p => p.UserId == userId)
                .Include(p => p.User)
                .Include(p => p.FasciaOraria)
                .Include(p => p.PrenotazioniPostazioni)
                    .ThenInclude(pp => pp.Postazione)
                        .ThenInclude(po => po.Zona)
                .AsNoTracking()
                .ToListAsync();

            var dtos = _mapper.Map<List<PrenotazioneDTO>>(prenotazioni);
            await ApplicaNumeroTurnoAsync(prenotazioni, dtos);
            return dtos;
        }


        public async Task<PagedResult<PrenotazioneDTO>> GetAllPrenotazioniAsync(PrenotazioniQueryParams query)
        {
            var queryable = _prenotazioniRepository.GetAllQueryableAsync();

            if (query.Data.HasValue)
                queryable = queryable.Where(p => p.DataPrenotazione == query.Data.Value);

            if (query.Stato.HasValue)
                queryable = queryable.Where(p => p.Stato == query.Stato.Value);

            if (query.FasciaOrariaId.HasValue)
                queryable = queryable.Where(p => p.FasciaOrariaId == query.FasciaOrariaId.Value);

            var totalCount = await queryable.CountAsync();

            // REV-020: l'ordinamento era per sola DataPrenotazione. Con piu' prenotazioni nello
            // stesso giorno - il caso normale - il database non garantisce un ordine stabile fra
            // righe di pari chiave: le stesse righe possono cadere in pagine diverse a ogni
            // richiesta, quindi navigando le pagine si vedono duplicati e si perdono righe mai
            // mostrate. Id come ultimo criterio rende l'ordine totale e quindi deterministico.
            // L'ordinamento va applicato dopo i Where: prima veniva riapplicato dentro ogni
            // ramo del filtro, il che lo faceva ripartire da capo perdendo i criteri successivi.
            // V2-007: prima oggi e i giorni a venire (dal più vicino), poi il passato (dal più
            // recente). In ordine di data puro la prima pagina era lo storico più vecchio e chi
            // lavora in sala doveva sfogliare tutto per arrivare al turno di oggi.
            var oggi = _clock.TodayInRome;
            var items = await queryable
                .OrderBy(p => p.DataPrenotazione < oggi)
                .ThenBy(p => p.DataPrenotazione >= oggi ? p.DataPrenotazione : oggi)
                .ThenByDescending(p => p.DataPrenotazione)
                .ThenBy(p => p.Id)
                .Include(p => p.User)
                .Include(p => p.FasciaOraria)
                .Include(p => p.PrenotazioniPostazioni)
                    .ThenInclude(pp => pp.Postazione)
                        .ThenInclude(po => po.Zona)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            var itemsDto = _mapper.Map<List<PrenotazioneDTO>>(items);
            await ApplicaNumeroTurnoAsync(items, itemsDto);

            return new PagedResult<PrenotazioneDTO>
            {
                Items = itemsDto,
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        /// <summary>
        /// FASE 4: NumeroTurno = posizione (1-based) della fascia fra le fasce attive dello
        /// stesso giorno della settimana, ordinate per OrarioInizio. Una sola query per ogni
        /// giorno della settimana effettivamente coinvolto (di solito uno o due), non una per
        /// prenotazione. Usa il repository (GetFasceByGiornoAsync), non il DbContext diretto:
        /// e lo stesso metodo usato da FasciaOrariaController per la stessa domanda.
        /// Se la fascia non compare piu tra quelle attive del giorno (disattivata dopo la
        /// prenotazione), NumeroTurno resta 0: il frontend non stampa nulla in quel caso.
        /// </summary>
        private async Task ApplicaNumeroTurnoAsync(IReadOnlyCollection<Prenotazione> prenotazioni, IReadOnlyCollection<PrenotazioneDTO> dtos)
        {
            if (prenotazioni.Count == 0) return;

            var fasceIdOrdinatePerGiorno = new Dictionary<DayOfWeek, List<long>>();
            foreach (var giorno in prenotazioni.Select(p => p.DataPrenotazione.DayOfWeek).Distinct())
            {
                var fasce = await _fasciaOrariaRepository.GetFasceByGiornoAsync(giorno);
                fasceIdOrdinatePerGiorno[giorno] = fasce.Select(f => f.Id).ToList();
            }

            var dtoPerId = dtos.ToDictionary(d => d.Id);
            foreach (var p in prenotazioni)
            {
                if (!dtoPerId.TryGetValue(p.Id, out var dto)) continue;

                var indice = fasceIdOrdinatePerGiorno[p.DataPrenotazione.DayOfWeek].IndexOf(p.FasciaOrariaId);
                dto.NumeroTurno = indice >= 0 ? indice + 1 : 0;
            }
        }

        public async Task AutomaticCompletPrenotazioniAsync()
        {
            var now = _clock.NowInRome;
            var today = DateOnly.FromDateTime(now);
            var oraAttuale = TimeOnly.FromTimeSpan(now.TimeOfDay);

            // REV-022: si legge il solo Id. Prima si caricavano le entita' intere e poi, per
            // ognuna, si faceva una seconda query (GetTrackedByIdAsync) e un SaveChanges
            // dedicato: 1 + 2N viaggi al database per una notte di prenotazioni. Ora sono due
            // query in tutto, indipendenti da quante righe ci sono.
            var idsDaCompletare = await _prenotazioniRepository
                .GetAllQueryableAsync()
                .Include(p => p.FasciaOraria)
                .Where(p => p.Stato == StatoPrenotazione.InCorso &&
                      (p.DataPrenotazione < today ||
                      (p.DataPrenotazione == today && oraAttuale > p.FasciaOraria.OrarioFine)))
                .Select(p => p.Id)
                .ToListAsync();

            if (idsDaCompletare.Count == 0)
            {
                _logger.LogInformation("Nessuna prenotazione da completare.");
            }
            else
            {
                var completate = await _prenotazioniRepository
                    .AggiornaStatoAsync(idsDaCompletare, StatoPrenotazione.Completata);

                // Un solo log riepilogativo invece di uno per riga: gli Id restano tracciati, ma
                // non riempiono il log della piattaforma con una linea per prenotazione.
                _logger.LogInformation("{Count} prenotazioni completate automaticamente: {Ids}",
                    completate, string.Join(", ", idsDaCompletare));
            }

            // FASE 3: gira sempre, indipendentemente da quante InCorso ci fossero da completare -
            // altrimenti con zero completamenti (il caso comune: la maggior parte delle notti non
            // ha turni da chiudere) il no-show non verrebbe mai marcato.
            await AutomaticMarcaNonPresentateAsync(now, today, oraAttuale);
        }

        /// <summary>
        /// FASE 3: una prenotazione "Attiva" mai confermata, con data/fascia ormai passate, non
        /// diventa mai "Completata" (quel percorso parte solo da "InCorso"): senza questo passo
        /// restava "Attiva" per sempre, proponendo in tabella "Conferma"/"Annulla" su un turno
        /// gia' finito. Stessa condizione temporale del completamento automatico.
        /// </summary>
        private async Task AutomaticMarcaNonPresentateAsync(DateTime now, DateOnly today, TimeOnly oraAttuale)
        {
            var idsNonPresentate = await _prenotazioniRepository
                .GetAllQueryableAsync()
                .Include(p => p.FasciaOraria)
                .Where(p => p.Stato == StatoPrenotazione.Attiva &&
                      (p.DataPrenotazione < today ||
                      (p.DataPrenotazione == today && oraAttuale > p.FasciaOraria.OrarioFine)))
                .Select(p => p.Id)
                .ToListAsync();

            if (idsNonPresentate.Count == 0)
            {
                _logger.LogInformation("Nessuna prenotazione da segnare come non presentata.");
                return;
            }

            var marcate = await _prenotazioniRepository
                .AggiornaStatoAsync(idsNonPresentate, StatoPrenotazione.NonPresentata);

            _logger.LogInformation("{Count} prenotazioni segnate come non presentate: {Ids}",
                marcate, string.Join(", ", idsNonPresentate));
        }

        public async Task AutomaticDeletePrenotazioniAsync()
        {
            var now = _clock.NowInRome;
            var cutoffDate = DateOnly.FromDateTime(now).AddMonths(-MesiRetentionCompletate);

            // REV-022: come sopra, una DELETE con un solo SaveChanges invece di una per riga.
            // Qui il guadagno e' maggiore: la pulizia gira su sei mesi di storico, quindi e'
            // proprio il caso in cui le righe sono tante.
            var idsDaEliminare = await _prenotazioniRepository
                .GetAllQueryableAsync()
                .Where(p => (p.Stato == StatoPrenotazione.Completata || p.Stato == StatoPrenotazione.NonPresentata)
                         && p.DataPrenotazione <= cutoffDate)
                .Select(p => p.Id)
                .ToListAsync();

            if (idsDaEliminare.Count == 0)
            {
                _logger.LogInformation("Nessuna prenotazione da eliminare.");
                return;
            }

            var eliminate = await _prenotazioniRepository.EliminaPerIdAsync(idsDaEliminare);

            _logger.LogInformation("{Count} prenotazioni eliminate automaticamente: {Ids}",
                eliminate, string.Join(", ", idsDaEliminare));
        }

        // Il vincolo "una prenotazione attiva al giorno" ha senso solo per il self-service:
        // Staff/Admin creano prenotazioni per conto di clienti diversi (es. telefonate) sotto
        // il proprio UserId, quindi per loro il vincolo non deve valere.
        private bool IsSelfServiceCliente()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user != null && !user.IsInRole(Roles.Admin) && !user.IsInRole(Roles.Staff);
        }

        // SEC-009 (V2-029): per il Cliente la prenotazione di un altro è "non trovata", come un Id
        // inesistente e come in GetByIdAsync. Va chiamato subito dopo la lettura, prima dei controlli
        // sullo stato: un 403, o un 409 sullo stato, diceva che quell'Id esiste e com'è messo.
        private void NascondiSeAltrui(Prenotazione prenotazione, string messaggio)
        {
            if (IsSelfServiceCliente()
                && !string.Equals(prenotazione.UserId, _httpContextAccessor.HttpContext.GetAuthenticatedUserId(),
                                  StringComparison.OrdinalIgnoreCase))
                throw new KeyNotFoundException(messaggio);
        }

        // RBAC-002: il Cliente può modificare/annullare una propria prenotazione self-service
        // solo fino a CutoffOreClienteSelfService ore prima dell'inizio della fascia prenotata.
        // Oltre la soglia l'azione è bloccata del tutto (nessuna approvazione Staff): deve
        // contattare il locale. Il vincolo non si applica ad Admin/Staff (IsSelfServiceCliente
        // è già false per loro, non serve un controllo separato qui).
        private const int CutoffOreClienteSelfService = 2;

        private void GuardCutoffAsync(Prenotazione prenotazione)
        {
            if (prenotazione.FasciaOraria == null)
                throw new ConflictException("La fascia oraria associata alla prenotazione non è disponibile.");

            var inizioPrenotazione = prenotazione.DataPrenotazione.ToDateTime(prenotazione.FasciaOraria.OrarioInizio);
            var limiteModifica = inizioPrenotazione.AddHours(-CutoffOreClienteSelfService);

            if (_clock.NowInRome > limiteModifica)
                throw new ConflictException(
                    $"Non è più possibile modificare o annullare autonomamente questa prenotazione: mancano meno di " +
                    $"{CutoffOreClienteSelfService} ore dall'orario prenotato. Contatta il locale per assistenza.");
        }

        // V2-007: chi prenota da solo arriva fino al limite online; oltre, si parla col locale.
        // Staff/Admin restano al limite tecnico, già applicato dai validatori.
        private void GuardLimiteOnline(int numeroCoperti)
        {
            var limite = _impostazioni.MaxCopertiPrenotazioneOnline;
            if (numeroCoperti > limite)
                throw new ConflictException(
                    $"Per prenotazioni superiori a {limite} persone contatta direttamente il ristorante.");
        }

        private async Task GuardUnaPrenotazioneAlGiornoAsync(string userId, DateOnly data, long? excludePrenotazioneId = null)
        {
            bool esisteGiaAttiva = await _prenotazioniRepository.GetAllQueryableAsync()
                .AnyAsync(p =>
                    p.UserId == userId &&
                    p.DataPrenotazione == data &&
                    p.Stato != StatoPrenotazione.Annullata &&
                    (!excludePrenotazioneId.HasValue || p.Id != excludePrenotazioneId.Value));

            if (esisteGiaAttiva)
                throw new ConflictException("Hai già una prenotazione attiva per questo giorno. Annullala prima di crearne una nuova, oppure modificala.");
        }

        /// <summary>Nome dell'unique index che protegge lo slot (PostazioneId + data + fascia).</summary>
        private const string SlotConstraintName = "UX_PrenotazionePostazione_Slot";

        /// <summary>
        /// Crea la riga di legame prenotazione-tavolo, copiandoci lo slot: sono le colonne su cui
        /// insiste UX_PrenotazionePostazione_Slot, se restassero vuote il vincolo non varrebbe.
        /// </summary>
        private static PrenotazionePostazione CreaRigaPostazione(Prenotazione prenotazione, PostazioneAssegnata assegnata)
            => new()
            {
                PostazioneId = assegnata.Postazione.Id,
                NumeroPosti = assegnata.PostiOccupati,
                Prenotazione = prenotazione,
                DataPrenotazione = prenotazione.DataPrenotazione,
                FasciaOrariaId = prenotazione.FasciaOrariaId
            };

        /// <summary>
        /// Esegue l'operazione in un'unica transazione e traduce la violazione dell'unique index
        /// dello slot in un 409 leggibile. La transazione va aperta dentro
        /// CreateExecutionStrategy().ExecuteAsync: con EnableRetryOnFailure attivo (vedi
        /// Program.cs) EF deve poter ritentare l'intero blocco, non una singola query.
        /// </summary>
        /// <summary>
        /// AUD-M4: blocca la riga e poi la legge, dentro la transazione aperta da
        /// EseguiInTransazioneAsync. Lo stato letto qui non puo' cambiare fino al commit.
        /// </summary>
        private async Task<Prenotazione> LeggiConLockAsync(long id, string messaggioNonTrovata)
        {
            await _prenotazioniRepository.BloccaPerModificaAsync(id);

            return await _prenotazioniRepository.GetTrackedByIdAsync(id)
                   ?? throw new KeyNotFoundException(messaggioNonTrovata);
        }

        private async Task EseguiInTransazioneAsync(Func<Task> operazione)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            var tentativo = 0;

            await strategy.ExecuteAsync(async () =>
            {
                // Un nuovo tentativo dopo un errore transitorio deve ripartire pulito: le entita'
                // lette e modificate al tentativo precedente restavano nel change tracker (righe
                // tavolo gia' staccate, nuove righe in stato Added) e si scrivevano dati incoerenti.
                // Per questo le operazioni leggono tutto dentro la transazione.
                if (tentativo++ > 0)
                    _context.ChangeTracker.Clear();

                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    await operazione();
                    await transaction.CommitAsync();
                }
                catch (DbUpdateException ex) when (DbExceptionTranslator.IsUniqueViolation(ex, SlotConstraintName))
                {
                    // Un altro utente ha vinto la corsa sullo stesso tavolo fra la verifica di
                    // disponibilita' e la scrittura. Il rollback avviene nel Dispose della
                    // transazione, mai committata.
                    throw new ConflictException(
                        "Il tavolo è stato appena assegnato a un'altra prenotazione. Riprova: verrà cercata una nuova disponibilità.", ex);
                }
            });
        }

        /// <summary>
        /// Contratto: va chiamato <b>solo dentro</b> EseguiInTransazioneAsync. Il lock sulla fascia
        /// (CAP-001) e' quello che rende il tetto un vincolo vero: due richieste simultanee sulla
        /// stessa fascia leggono la SUM una alla volta, non insieme.
        /// </summary>
        private async Task ValidatePrenotazioneAsync(PrenotazioneCreateDTO dto, long? excludePrenotazioneId = null)
        {
            var fasciaOraria = await _fasciaOrariaRepository.GetByIdConLockAsync(dto.FasciaOrariaId);

            if (fasciaOraria == null)
                throw new ArgumentException("La fascia oraria specificata non esiste.");

            if (!fasciaOraria.Attiva)
                throw new ConflictException("La fascia oraria selezionata non è attiva.");

            var giornoIt = new System.Globalization.CultureInfo("it-IT")
                .DateTimeFormat.GetDayName(fasciaOraria.GiornoSettimana);

            if (fasciaOraria.GiornoSettimana != dto.DataPrenotazione.DayOfWeek)
                throw new ConflictException($"La fascia oraria selezionata è valida solo per il giorno {giornoIt}.");

            // Stessa regola di ConfermaPrenotazioneAsync: una fascia finita non si prenota piu'.
            // Vale anche per lo Staff che registra al telefono; se la fascia e' in corso si puo'.
            var fineFascia = dto.DataPrenotazione.ToDateTime(TimeOnly.MinValue)
                                 .Add(fasciaOraria.OrarioFine.ToTimeSpan());
            if (_clock.NowInRome >= fineFascia)
                throw new ConflictException("La fascia oraria selezionata è già passata.");

            int copertiGiaPrenotati = await _prenotazioniRepository.GetAllQueryableAsync()
                .Where(p =>
                    p.DataPrenotazione == dto.DataPrenotazione &&
                    p.FasciaOrariaId == dto.FasciaOrariaId &&
                    p.Stato != StatoPrenotazione.Annullata &&
                    p.Stato != StatoPrenotazione.NonPresentata &&
                    (!excludePrenotazioneId.HasValue || p.Id != excludePrenotazioneId.Value))
                .SumAsync(p => p.NumeroCoperti);

            int copertiDisponibili = fasciaOraria.MaxCoperti - copertiGiaPrenotati;
            if (dto.NumeroCoperti > copertiDisponibili)
                throw new ConflictException(
                    copertiDisponibili <= 0
                        ? "La fascia oraria ha raggiunto la capienza massima. Non ci sono coperti disponibili."
                        : $"Coperti richiesti ({dto.NumeroCoperti}) superiori a quelli disponibili ({copertiDisponibili}) per questa fascia oraria.");

            if (dto.ZonaId.HasValue)
            {
                var zona = await _zonaRepository.GetByIdAsync(dto.ZonaId.Value);
                if (zona == null || !zona.Attiva)
                    throw new ConflictException("La zona selezionata non è attiva o non esiste.");

                bool zonaHaPostazioni = await _context.Postazioni
                    .AsNoTracking()
                    .AnyAsync(p => p.ZonaId == dto.ZonaId.Value && p.Attiva);

                if (!zonaHaPostazioni)
                    throw new ConflictException("La zona preferita selezionata non ha postazioni attive.");
            }

            bool almenoUnaPostazioneAttiva = await _context.Postazioni
                .AsNoTracking()
                .AnyAsync(p => p.Attiva);

            if (!almenoUnaPostazioneAttiva)
                throw new ArgumentException("Non ci sono postazioni attive nel sistema.");
        }
    }
}