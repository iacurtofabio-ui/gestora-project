using GestoraWebApi.Auth;
using GestoraWebApi.Infrastructure.RateLimiting;
using GestoraWebApi.Services.Disponibilita;
using GestoraWebApi.Services.Prenotazioni;
using GestoraWebApi.Services.Prenotazioni.DTOs;
using GestoraWebApi.Services.PrenotazioniPostazioni;
using GestoraWebApi.Services.Report;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using GestoraWebApi.Common;
using Microsoft.Extensions.Options;

namespace GestoraWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PrenotazioneController : ControllerBase
    {
        private readonly IPrenotazioniService _prenotazioniService;
        private readonly IDisponibilitaService _disponibilitaService;
        private readonly IReportPrenotazioniService _report;
        private readonly PrenotazioniSettings _impostazioni;
        private readonly ILogger<PrenotazioneController> _logger;

        public PrenotazioneController(IPrenotazioniService prenotazioneService,
                                      IDisponibilitaService disponibilitaService,
                                      IReportPrenotazioniService report,
                                      IOptions<PrenotazioniSettings> impostazioni,
                                      ILogger<PrenotazioneController> logger)
        {
            _prenotazioniService = prenotazioneService;
            _disponibilitaService = disponibilitaService;
            _report = report;
            _impostazioni = impostazioni.Value;
            _logger = logger;
        }

        /// <summary>
        /// V2-025 — report CSV delle prenotazioni da <paramref name="da"/> ad <paramref name="a"/>
        /// (date comprese, al massimo un anno), tutti gli stati. Solo Admin: contiene i nomi dei clienti.
        /// </summary>
        [Authorize(Roles = Roles.Admin)]
        [HttpGet("report-csv")]
        public async Task<IActionResult> ReportCsv([FromQuery] DateOnly da, [FromQuery] DateOnly a)
        {
            var csv = await _report.CsvAsync(da, a);
            return File(csv, "text/csv; charset=utf-8", $"prenotazioni_{da:yyyy-MM-dd}_{a:yyyy-MM-dd}.csv");
        }

        /// <summary>Crea una nuova prenotazione per l'utente autenticato. Assegna automaticamente le postazioni disponibili.</summary>
        [Authorize(Roles = Roles.AdminOrStaffOrCliente)]
        [HttpPost("crea-prenotazione")]
        public async Task<IActionResult> CreatePrenotazioniAsync([FromBody] PrenotazioneCreateDTO dto)
        {
            await _prenotazioniService.AddAsync(dto);

            _logger.LogInformation("[{Controller}] - [{Method}]: Prenotazione creata con successo",
                nameof(PrenotazioneController), nameof(CreatePrenotazioniAsync));

            return Created("", new { Message = "Prenotazione creata con successo" });
        }

        /// <summary>Restituisce il dettaglio di una prenotazione tramite ID. Admin/Staff su
        /// qualsiasi prenotazione; il Cliente solo sulla propria (REV-034).</summary>
        [Authorize(Roles = Roles.AdminOrStaffOrCliente)]
        [HttpGet("get-prenotazione")]
        public async Task<IActionResult> GetById(long id)
        {
            var prenotazione = await _prenotazioniService.GetByIdAsync(id);

            if (prenotazione == null)
                return NotFound(new { message = $"Prenotazione con ID {id} non trovata." });

            return Ok(prenotazione);
        }

        /// <summary>Restituisce la lista paginata di tutte le prenotazioni con filtri opzionali per data e stato.</summary>
        [Authorize(Roles = Roles.AdminOrStaff)]
        [HttpGet("get-all-prenotazioni")]
        public async Task<IActionResult> GetAllPrenotazioni([FromQuery] PrenotazioniQueryParams query)
        {
            var result = await _prenotazioniService.GetAllPrenotazioniAsync(query);
            return Ok(result);
        }

        /// <summary>Restituisce tutte le prenotazioni per una data specifica (formato yyyy-MM-dd).</summary>
        [Authorize(Roles = Roles.AdminOrStaff)]
        [HttpGet("get-prenotazioni-by-data")]
        public async Task<IActionResult> GetPrenotazioniByData(DateOnly data)
        {
            var prenotazioni = await _prenotazioniService.GetPrenotazioniByDataAsync(data);

            return Ok(prenotazioni);
        }

        /// <summary>Restituisce tutte le prenotazioni dell'utente autenticato.</summary>
        [Authorize(Roles = Roles.Cliente)]
        [HttpGet("get-mie-prenotazioni")]
        public async Task<IActionResult> GetMiePrenotazioni()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var prenotazioni = await _prenotazioniService.GetMiePrenotazioniAsync(userId);

            return Ok(prenotazioni);
        }

        /// <summary>Aggiorna i dati di una prenotazione esistente. Solo se in stato Attiva. Admin/Staff senza
        /// limiti; il Cliente solo sulla propria prenotazione e fino a 2 ore prima dell'orario (RBAC-002).</summary>
        [Authorize(Roles = Roles.AdminOrStaffOrCliente)]
        [HttpPut("update-prenotazione")]
        public async Task<IActionResult> UpdatePrenotazione(long id, [FromBody] PrenotazioneCreateDTO dto)
        {
            await _prenotazioniService.UpdateAsync(id, dto);

            _logger.LogInformation("[{Controller}] - [{Method}]: Prenotazione {Id} aggiornata con successo",
                nameof(PrenotazioneController), nameof(UpdatePrenotazione), id);

            return Ok(new { message = "Prenotazione aggiornata con successo." });
        }

        /// <summary>Elimina una prenotazione. Solo se in stato Attiva o Annullata. Solo Admin.</summary>
        [Authorize(Roles = Roles.Admin)]
        [HttpDelete("delete-prenotazione")]
        public async Task<IActionResult> DeletePrenotazione(long id)
        {
            await _prenotazioniService.DeleteAsync(id);

            _logger.LogInformation("[{Controller}] - [{Method}]: Prenotazione {Id} eliminata con successo",
                nameof(PrenotazioneController), nameof(DeletePrenotazione), id);

            return Ok(new { message = "Prenotazione eliminata con successo." });
        }

        /// <summary>Conferma una prenotazione: transizione stato Attiva → InCorso. Solo Admin e Staff.</summary>
        [Authorize(Roles = Roles.AdminOrStaff)]
        [HttpPatch("conferma-prenotazione")]
        public async Task<IActionResult> ConfermaPrenotazione(long id)
        {
            await _prenotazioniService.ConfermaPrenotazioneAsync(id);

            _logger.LogInformation("[{Controller}] - [{Method}]: Prenotazione {Id} confermata con successo",
                nameof(PrenotazioneController), nameof(ConfermaPrenotazione), id);

            return Ok(new { message = "Prenotazione confermata con successo." });
        }

        /// <summary>Completa una prenotazione: transizione InCorso → Completata. Verificabile solo dopo la fine della fascia oraria.</summary>
        [Authorize(Roles = Roles.AdminOrStaff)]
        [HttpPatch("completa-prenotazione")]
        public async Task<IActionResult> CompletePrenotazione(long id)
        {
            await _prenotazioniService.CompletePrenotazioneAsync(id);

            _logger.LogInformation("[{Controller}] - [{Method}]: Prenotazione {Id} completata manualmente",
                nameof(PrenotazioneController), nameof(CompletePrenotazione), id);

            return Ok(new { message = "Prenotazione completata con successo." });
        }

        /// <summary>Annulla una prenotazione. Non annullabile se già Completata. Admin/Staff senza limiti;
        /// il Cliente solo sulla propria prenotazione e fino a 2 ore prima dell'orario (RBAC-002).</summary>
        [Authorize(Roles = Roles.AdminOrStaffOrCliente)]
        [HttpPatch("annulla-prenotazione")]
        public async Task<IActionResult> AnnullaPrenotazione(long id)
        {
            await _prenotazioniService.AnnullaPrenotazioneAsync(id);

            _logger.LogInformation("[{Controller}] - [{Method}]: Prenotazione {Id} annullata con successo",
                nameof(PrenotazioneController), nameof(AnnullaPrenotazione), id);

            return Ok(new { message = "Prenotazione annullata con successo." });
        }

        /// <summary>Endpoint pubblico (no login richiesto). Verifica disponibilità fasce e postazioni per data e numero coperti.</summary>
        [AllowAnonymous]
        [EnableRateLimiting(LimitiRichieste.Disponibilita)]
        [HttpPost("check-disponibilita")]
        public async Task<IActionResult> CheckDisponibilita([FromBody] CheckDisponibilitaDTO dto)
        {
            var disponibilita = await _disponibilitaService.CheckDisponibilitaAsync(dto);

            // REV-031: "in quella data non c'e' nessuna fascia" e' una risposta legittima della
            // verifica di disponibilita', non una risorsa mancante: il locale puo' essere chiuso
            // quel giorno. Restituendo 404 il chiamante - qui anche un client pubblico non
            // autenticato - doveva distinguere un giorno di chiusura da un errore vero.
            return Ok(disponibilita ?? new DisponibilitaResponseDTO());
        }

        /// <summary>Endpoint pubblico (no login richiesto). Restituisce i limiti delle prenotazioni
        /// letti dalla configurazione, così il frontend non deve scriverli a mano.</summary>
        [AllowAnonymous]
        [HttpGet("limiti-prenotazione")]
        public IActionResult GetLimitiPrenotazione()
        {
            return Ok(new LimitiPrenotazioneDTO
            {
                MaxCopertiPerPrenotazione = _impostazioni.MaxCopertiPerPrenotazione,
                MaxCopertiPrenotazioneOnline = _impostazioni.MaxCopertiPrenotazioneOnline
            });
        }
    }
}