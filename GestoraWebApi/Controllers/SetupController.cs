using GestoraWebApi.Auth;
using GestoraWebApi.Common;
using GestoraWebApi.Extensions;
using GestoraWebApi.Infrastructure.Auth;
using GestoraWebApi.Infrastructure.Exceptions;
using GestoraWebApi.Services.Auth.DTOs;
using GestoraWebApi.Services.LogActivity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GestoraWebApi.Controllers
{
    /// <summary>
    /// Primo avvio dell'installazione (REV-007).
    ///
    /// Gestora viene installata per un singolo locale: la prima volta che qualcuno apre l'app
    /// non esiste ancora nessun amministratore, e l'unica cosa possibile e' crearlo. Appena
    /// l'Admin esiste questi endpoint si chiudono da soli e restano chiusi per sempre.
    ///
    /// Sostituisce il vecchio POST /api/AuthenticationUser/seed-admin, che era pubblico,
    /// documentato in Swagger insieme agli endpoint di autenticazione e non aveva ne' un modo
    /// per il frontend di sapere se il setup fosse gia' stato fatto, ne' rate limiting.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SetupController : Controller
    {
        // Il controllo "esiste gia' un Admin?" e la creazione non sono atomici: due richieste
        // simultanee potrebbero vedere entrambe il sistema vuoto e creare due Admin. Il semaforo
        // serializza le richieste dentro il processo, che e' quanto basta qui: il primo avvio e'
        // un'operazione singola e assistita, e l'app gira su una sola istanza. Se un domani si
        // scalasse su piu' repliche servirebbe un vincolo a database, come per REV-003.
        private static readonly SemaphoreSlim SetupLock = new(1, 1);

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogActivityService _logActivityService;
        private readonly ILogger<SetupController> _logger;
        private readonly IClock _clock;

        public SetupController(UserManager<ApplicationUser> userManager,
                               RoleManager<IdentityRole> roleManager,
                               ILogActivityService logActivityService,
                               ILogger<SetupController> logger,
                               IClock clock)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logActivityService = logActivityService;
            _logger = logger;
            _clock = clock;
        }

        /// <summary>
        /// Dice se l'installazione e' gia' configurata. Pubblico e volutamente muto: risponde
        /// solo si'/no, senza rivelare quanti utenti esistono o chi sono.
        /// </summary>
        [HttpGet("stato")]
        public async Task<IActionResult> Stato()
            => Ok(new SetupStatoDTO { SetupCompletato = await EsisteAdminAsync() });

        /// <summary>
        /// Crea il primo Admin dell'installazione. Aperto solo finche' un Admin non esiste.
        /// Non restituisce un token: l'utente fa subito il login con le credenziali appena
        /// scelte, cosi' le verifica mentre ha ancora l'assistenza davanti.
        /// </summary>
        [EnableRateLimiting("LoginPolicy")]
        [HttpPost("admin")]
        public async Task<IActionResult> CreaPrimoAdmin([FromBody] RegisterDTO request)
        {
            await SetupLock.WaitAsync();
            try
            {
                if (await EsisteAdminAsync())
                {
                    _logger.LogWarning("[{Controller}] - [{Method}]: Tentativo di primo avvio da {Ip} su installazione gia' configurata",
                        nameof(SetupController), nameof(CreaPrimoAdmin), HttpContext.GetIpAddress());

                    return this.Errore(StatusCodes.Status409Conflict, "L'installazione e' gia' configurata: esiste gia' un amministratore.");
                }

                var user = new ApplicationUser
                {
                    UserName = request.Username,
                    Email = request.Email,
                    // V2-003: il primo Admin e' l'unico che puo' confermare gli altri; se dovesse
                    // aspettare un'email non potrebbe entrare nemmeno a configurare l'invio.
                    EmailConfirmed = true,
                    DataCreazione = _clock.UtcNow
                };
                var result = await _userManager.CreateAsync(user, request.Password);

                if (!result.Succeeded)
                    throw ErroriIdentity.ComeValidationException(result, MessaggioErrore);

                // Il ruolo lo crea gia' RoleSeeder all'avvio, ma questo e' l'unico endpoint che
                // gira per definizione su un database appena creato: non deve dipendere
                // dall'ordine di esecuzione di un altro pezzo di startup.
                await AssicuraRuoloAdminAsync();

                var roleResult = await _userManager.AddToRoleAsync(user, Roles.Admin);

                if (!roleResult.Succeeded)
                {
                    // Un utente creato ma senza ruolo bloccherebbe il setup per sempre: la
                    // prossima chiamata troverebbe zero Admin ma l'username gia' occupato.
                    await _userManager.DeleteAsync(user);
                    throw ErroriIdentity.ComeValidationException(roleResult, MessaggioErrore);
                }

                // REV-070: nei log resta l'UserId, non l'email.
                _logger.LogWarning("[{Controller}] - [{Method}]: Primo avvio completato, Admin {UserId} creato da {Ip}",
                    nameof(SetupController), nameof(CreaPrimoAdmin), user.Id, HttpContext.GetIpAddress());

                await _logActivityService.LogAsync(user.Id, "Primo avvio: creazione dell'amministratore iniziale", HttpContext.GetIpAddress());

                return Ok(new { Messaggio = $"Amministratore '{user.UserName}' creato. Effettua il login per iniziare." });
            }
            finally
            {
                SetupLock.Release();
            }
        }

        private const string MessaggioErrore = "Dati non validi per la creazione dell'amministratore.";

        /// <summary>
        /// GetUsersInRoleAsync solleva InvalidOperationException se il ruolo non esiste, e da
        /// REV-026 quella eccezione e' un 500: su un database appena creato la schermata di primo
        /// avvio morirebbe proprio nel caso per cui esiste. Se il ruolo non c'e' ancora, non puo'
        /// esistere nessun Admin.
        /// </summary>
        private async Task<bool> EsisteAdminAsync()
            => await _roleManager.RoleExistsAsync(Roles.Admin)
               && (await _userManager.GetUsersInRoleAsync(Roles.Admin)).Any();

        private async Task AssicuraRuoloAdminAsync()
        {
            if (!await _roleManager.RoleExistsAsync(Roles.Admin))
                await _roleManager.CreateAsync(new IdentityRole(Roles.Admin));
        }
    }
}
