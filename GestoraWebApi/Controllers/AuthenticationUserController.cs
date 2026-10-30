using GestoraWebApi.Auth;
using GestoraWebApi.Common;
using GestoraWebApi.Context;
using GestoraWebApi.Extensions;
using GestoraWebApi.Infrastructure.Auth;
using GestoraWebApi.Infrastructure.Exceptions;
using GestoraWebApi.Infrastructure.RateLimiting;
using GestoraWebApi.Services.Auth.DTOs;
using GestoraWebApi.Services.Email;
using GestoraWebApi.Services.LogActivity;
using GestoraWebApi.Services.Privacy;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GestoraWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthenticationUserController : Controller
    {
        private readonly IJwtTokenGenerator _tokenGenerator;
        private readonly ILogger<AuthenticationUserController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITentativiAccesso _tentativiAccesso;
        private readonly ILogActivityService _logActivityService;
        private readonly GestoraContext _context;
        private readonly IClock _clock;
        private readonly IEsecutoreTransazione _transazione;
        private readonly IConfermaEmailService _confermaEmail;
        private readonly PrivacySettings _privacy;
        private readonly IRecuperoPasswordService _recuperoPassword;

        public AuthenticationUserController(IJwtTokenGenerator tokenGenerator,
                                            ILogger<AuthenticationUserController> logger,
                                            UserManager<ApplicationUser> userManager,
                                            ITentativiAccesso tentativiAccesso,
                                            ILogActivityService logActivityService,
                                            GestoraContext context,
                                            IClock clock,
                                            IEsecutoreTransazione transazione,
                                            IConfermaEmailService confermaEmail,
                                            IOptions<PrivacySettings> privacy,
                                            IRecuperoPasswordService recuperoPassword)
        {
            _recuperoPassword = recuperoPassword;
            _privacy = privacy.Value;
            _confermaEmail = confermaEmail;
            _transazione = transazione;
            _clock = clock;
            _tokenGenerator = tokenGenerator;
            _logger = logger;
            _userManager = userManager;
            _tentativiAccesso = tentativiAccesso;
            _logActivityService = logActivityService;
            _context = context;
        }

        /// <summary>
        /// Registrazione — assegna automaticamente il ruolo Cliente. V2-003: dalla registrazione
        /// pubblica l'utente riceve un link per confermare l'email e senza conferma non entra.
        /// Se a chiamare è un Admin (crea gli account dello Staff) l'account nasce già confermato
        /// e non parte nessuna email: l'indirizzo lo ha scelto l'Admin, non va dimostrato.
        /// </summary>
        [EnableRateLimiting(LimitiRichieste.Registrazione)]
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDTO request)
        {
            var creatoDaAdmin = User.IsInRole(Roles.Admin);

            // V2-019: chi si registra da solo deve dichiarare di aver letto l'informativa. Non sta
            // nel validator perche' dipende da chi chiama: l'Admin che crea lo Staff non la
            // accetta al posto suo, l'utente la vedra' al primo accesso.
            if (!creatoDaAdmin && !request.AccettaPrivacy)
                throw new ValidationException("Registrazione non riuscita: controlla i dati inseriti.",
                    new Dictionary<string, string[]>
                    {
                        ["accettaPrivacy"] = ["Per registrarti devi dichiarare di aver letto l'informativa privacy."]
                    });

            var user = new ApplicationUser
            {
                UserName = request.Username,
                Email = request.Email,
                EmailConfirmed = creatoDaAdmin,
                DataCreazione = _clock.UtcNow
            };
            if (!creatoDaAdmin)
                PresaVisionePrivacy.Registra(user, _privacy.Versione, _clock.UtcNow);
            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                // REV-070: nessun identificativo personale nei log. Su una registrazione fallita
                // l'email non appartiene nemmeno a un nostro utente: restano il motivo e l'IP.
                _logger.LogWarning("[{Controller}] - [{Method}]: Registrazione fallita da {Ip} - {Errors}",
                    nameof(AuthenticationUserController), nameof(Register),
                    HttpContext.GetIpAddress(), string.Join(", ", result.Errors.Select(e => e.Description)));

                throw ErroriIdentity.ComeValidationException(result, "Registrazione non riuscita: controlla i dati inseriti.");
            }

            // Ogni nuovo utente registrato riceve il ruolo Cliente di default. Il risultato era
            // ignorato: se falliva, l'utente restava senza ruolo e riceveva 403 ovunque, senza
            // traccia nei log. Si elimina l'utente appena creato, cosi' puo' riprovare.
            var roleResult = await _userManager.AddToRoleAsync(user, Roles.Cliente);
            if (!roleResult.Succeeded)
            {
                _logger.LogError("[{Controller}] - [{Method}]: Ruolo Cliente non assegnato a {UserId} - {Errors}",
                    nameof(AuthenticationUserController), nameof(Register), user.Id,
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));

                await _userManager.DeleteAsync(user);
                throw new InvalidOperationException("Assegnazione del ruolo Cliente non riuscita.");
            }

            _logger.LogInformation("[{Controller}] - [{Method}]: Registrazione riuscita per {UserId}",
                nameof(AuthenticationUserController), nameof(Register), user.Id);

            // L'audit log su database identifica gia' l'utente con user.Id (primo parametro):
            // ripetere l'email nel testo del messaggio la duplicherebbe senza aggiungere nulla.
            await _logActivityService.LogAsync(user.Id, "Registrazione nuovo utente", HttpContext.GetIpAddress());

            // Se l'invio fallisce l'utente resta registrato (non si butta via un account per un
            // problema di posta): la risposta lo dice e dalla schermata di login si puo' chiedere il reinvio.
            var emailInviata = !creatoDaAdmin && await _confermaEmail.InviaConfermaAsync(user);

            return Ok(new
            {
                Messaggio = $"Registrazione di '{user.UserName}' avvenuta con successo.",
                RichiedeConferma = !creatoDaAdmin,
                EmailInviata = emailInviata
            });
        }

        /// <summary>V2-003 — conferma dell'email dal link ricevuto. Pubblico: chi clicca non ha ancora fatto il login.</summary>
        [EnableRateLimiting(LimitiRichieste.ConfermaEmail)]
        [HttpPost("conferma-email")]
        public async Task<IActionResult> ConfermaEmail(ConfermaEmailDTO request)
        {
            var confermata = await _confermaEmail.ConfermaAsync(request.UserId, request.Token, HttpContext.GetIpAddress());
            if (!confermata)
                return BadRequest("Il link di conferma non è valido o è scaduto. Richiedine uno nuovo dalla pagina di accesso.");

            return Ok("Email confermata. Ora puoi accedere.");
        }

        /// <summary>
        /// V2-003 — nuovo link di conferma. Risponde sempre allo stesso modo, esista o no l'email e
        /// sia o no già confermata: altrimenti si potrebbe scoprire chi è registrato.
        /// </summary>
        [EnableRateLimiting(LimitiRichieste.ConfermaEmail)]
        [HttpPost("reinvia-conferma")]
        public async Task<IActionResult> ReinviaConferma(ReinviaConfermaDTO request)
        {
            await _confermaEmail.ReinviaAsync(request.Email);
            return Ok("Se l'indirizzo è registrato e non ancora confermato, riceverai una nuova email.");
        }

        /// <summary>
        /// V2-019 — link per reimpostare la password. Risponde sempre allo stesso modo, esista o no
        /// l'email: altrimenti si potrebbe scoprire chi è registrato.
        /// </summary>
        [EnableRateLimiting(LimitiRichieste.RecuperoPassword)]
        [HttpPost("password-dimenticata")]
        public async Task<IActionResult> PasswordDimenticata(PasswordDimenticataDTO request)
        {
            await _recuperoPassword.RichiediAsync(request.Email, HttpContext.GetIpAddress());
            return Ok("Se l'indirizzo è registrato, riceverai un'email con il link per scegliere una nuova password.");
        }

        /// <summary>V2-019 — nuova password dal link ricevuto per email. Pubblico: chi lo usa non riesce ad accedere.</summary>
        [EnableRateLimiting(LimitiRichieste.RecuperoPassword)]
        [HttpPost("reimposta-password")]
        public async Task<IActionResult> ReimpostaPassword(ReimpostaPasswordDTO request)
        {
            var reimpostata = await _recuperoPassword.ReimpostaAsync(request.UserId, request.Token,
                request.NuovaPassword, HttpContext.GetIpAddress());
            if (!reimpostata)
                return BadRequest("Il link non è valido o è scaduto. Chiedine uno nuovo dalla pagina di accesso.");

            return Ok("Password aggiornata. Ora puoi accedere con la nuova password.");
        }

        /// <summary>Login — restituisce il token JWT con i ruoli dell'utente</summary>
        [EnableRateLimiting("LoginPolicy")]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            // REV-070: l'email non entra nei log. Su un login fallito e' l'indirizzo di chi
            // prova, non necessariamente di un nostro utente: registrarla significa conservare
            // dati personali di terzi a ogni tentativo, anche di un attacco a dizionario.
            // Per correlare i tentativi basta l'IP; a login riuscito si usa l'UserId.
            var ip = HttpContext.GetIpAddress();
            _logger.LogInformation("[{Controller}] - [{Method}]: Tentativo di login da {Ip}",
                nameof(AuthenticationUserController), nameof(Login), ip);

            // V2-027: il blocco riguarda email + indirizzo di chi sbaglia, non l'account. Prima
            // (blocco di Identity) bastavano 5 password sbagliate apposta per chiudere fuori l'Admin
            // per 15 minuti. Il controllo viene prima di cercare l'utente: vale anche per le email
            // che non esistono, così la risposta non dice quali sono registrate.
            if (_tentativiAccesso.Bloccato(request.Email, ip))
            {
                _logger.LogWarning("[{Controller}] - [{Method}]: Login bloccato per troppi tentativi falliti da {Ip}",
                    nameof(AuthenticationUserController), nameof(Login), ip);

                return StatusCode(StatusCodes.Status423Locked,
                    "Troppi tentativi sbagliati da questo dispositivo. Riprova tra 15 minuti.");
            }

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                _tentativiAccesso.RegistraErrore(request.Email, ip);
                _logger.LogWarning("[{Controller}] - [{Method}]: Accesso non autorizzato da {Ip} (utente inesistente)",
                    nameof(AuthenticationUserController), nameof(Login), ip);

                return Unauthorized("Credenziali non valide.");
            }

            // CheckPasswordAsync e non SignInManager.CheckPasswordSignInAsync: quello rifiuterebbe
            // comunque un account con il blocco di Identity attivo, che qui non si usa più.
            if (!await _userManager.CheckPasswordAsync(user, request.Password))
            {
                _tentativiAccesso.RegistraErrore(request.Email, ip);
                _logger.LogWarning("[{Controller}] - [{Method}]: Accesso non autorizzato per {UserId} (password errata, da {Ip})",
                    nameof(AuthenticationUserController), nameof(Login), user.Id, ip);

                return Unauthorized("Credenziali non valide.");
            }

            _tentativiAccesso.Azzera(request.Email, ip);

            // V2-003: senza email confermata non si entra. Il controllo viene DOPO la password,
            // cosi' chi non la conosce non scopre se un indirizzo e' registrato. Il codice e'
            // stabile: il frontend lo riconosce e propone il reinvio del link.
            if (!user.EmailConfirmed)
            {
                _logger.LogInformation("[{Controller}] - [{Method}]: Login rifiutato per {UserId}, email non confermata",
                    nameof(AuthenticationUserController), nameof(Login), user.Id);

                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    Codice = CodiciErrore.EmailNonConfermata,
                    Messaggio = "Devi confermare l'email prima di accedere. Controlla la tua posta."
                });
            }

            // V2-015: un'utenza che ha fatto almeno un login non e' "mai usata" e la pulizia
            // notturna non la tocca. UpdateAsync non cambia il security stamp: i token gia'
            // emessi restano validi. Se fallisce si avvisa e si entra lo stesso: un campo di
            // manutenzione non deve impedire l'accesso.
            user.UltimoAccesso = _clock.UtcNow;
            // V2-019: chi rientra non è più inattivo. Se un giorno lo ridiventa, riceverà un avviso nuovo.
            user.AvvisoInattivitaInviatoIl = null;
            var aggiornamento = await _userManager.UpdateAsync(user);
            if (!aggiornamento.Succeeded)
            {
                _logger.LogWarning("[{Controller}] - [{Method}]: Ultimo accesso non salvato per {UserId} - {Errors}",
                    nameof(AuthenticationUserController), nameof(Login), user.Id,
                    string.Join(", ", aggiornamento.Errors.Select(e => e.Description)));
            }

            var roles = await _userManager.GetRolesAsync(user);
            var token = _tokenGenerator.GenerateToken(user.Id, user.Email!, roles,
                await _userManager.GetSecurityStampAsync(user));

            await _logActivityService.LogAsync(user.Id, "Login", HttpContext.GetIpAddress());

            _logger.LogInformation("[{Controller}] - [{Method}]: Login riuscito per {UserId}",
                nameof(AuthenticationUserController), nameof(Login), user.Id);

            // V2-028: il token va nel cookie HttpOnly, non nella risposta.
            var scadenza = CookieSessione.Scrivi(Response, token);
            return Ok(new SessioneDTO(user.Id, user.Email!, roles, scadenza));
        }

        /// <summary>
        /// V2-028 — chi è collegato. Il frontend non può più leggere il token (sta in un cookie
        /// HttpOnly) e chiede qui, all'avvio, se c'è una sessione aperta e di chi è. Email e ruoli
        /// sono riletti adesso; la scadenza è quella del token in uso, che non cambia.
        /// </summary>
        [Authorize]
        [HttpGet("sessione")]
        public async Task<IActionResult> Sessione()
        {
            var user = await _userManager.FindByIdAsync(User.GetAuthenticatedUserId());
            var exp = User.FindFirstValue(JwtRegisteredClaimNames.Exp);
            if (user == null || !long.TryParse(exp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var secondi))
                return Unauthorized("Sessione scaduta. Effettua di nuovo l'accesso.");

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new SessioneDTO(user.Id, user.Email!, roles,
                DateTimeOffset.FromUnixTimeSeconds(secondi).UtcDateTime));
        }

        /// <summary>
        /// V2-028 — uscita: toglie il cookie con il token. Anonimo apposta: deve funzionare anche
        /// con la sessione già scaduta. Il token in sé resta valido fino alla sua scadenza (al
        /// massimo un'ora), ma non sta più da nessuna parte.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            CookieSessione.Cancella(Response);
            return NoContent();
        }

        /// <summary>
        /// V2-010 — rinnovo del token, per qualsiasi utente con un token ancora valido. Il frontend
        /// lo chiama da solo poco prima della scadenza, finché la pagina è aperta: lo Staff non viene
        /// più buttato fuori ogni ora. Il nuovo token conserva l'inizio della sessione, quindi non va
        /// mai oltre JwtSettings:MaxSessionHours dal login; superato quel limite si rifà il login.
        /// Ruoli e security stamp sono riletti adesso: un account modificato è già respinto da
        /// OnTokenValidated prima di arrivare qui. Nessuna riga nel registro attività: sarebbe una
        /// ogni ora per ogni utente collegato.
        /// </summary>
        [Authorize]
        [HttpPost("rinnova-token")]
        public async Task<IActionResult> RinnovaToken()
        {
            var userId = User.GetAuthenticatedUserId();
            var inizioSessione = JwtTokenGenerator.LeggiInizioSessione(User);

            if (inizioSessione is null || !_tokenGenerator.SessioneAncoraValida(inizioSessione.Value))
            {
                _logger.LogInformation("[{Controller}] - [{Method}]: Rinnovo rifiutato per {UserId}, sessione al limite massimo",
                    nameof(AuthenticationUserController), nameof(RinnovaToken), userId);

                return Unauthorized("Sessione scaduta. Effettua di nuovo l'accesso.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Unauthorized("Sessione scaduta. Effettua di nuovo l'accesso.");

            var roles = await _userManager.GetRolesAsync(user);
            var token = _tokenGenerator.GenerateToken(user.Id, user.Email!, roles,
                await _userManager.GetSecurityStampAsync(user), inizioSessione.Value);

            var scadenza = CookieSessione.Scrivi(Response, token);
            return Ok(new SessioneDTO(user.Id, user.Email!, roles, scadenza));
        }

        /// <summary>
        /// V2-019 — se l'utente deve ancora prendere visione dell'informativa in vigore (account
        /// creati da un Admin, primo Admin, informativa cambiata). Il frontend lo chiede entrando
        /// nell'area riservata e, se serve, mostra l'informativa prima di tutto il resto.
        /// </summary>
        [Authorize]
        [HttpGet("stato-privacy")]
        public async Task<IActionResult> StatoPrivacy()
        {
            var user = await _userManager.FindByIdAsync(User.GetAuthenticatedUserId());
            if (user == null)
                return Unauthorized("Sessione scaduta. Effettua di nuovo l'accesso.");

            return Ok(new
            {
                DaAccettare = PresaVisionePrivacy.DaAccettare(user, _privacy.Versione),
                Versione = _privacy.Versione
            });
        }

        /// <summary>V2-019 — presa visione dell'informativa in vigore da parte dell'utente collegato.</summary>
        [Authorize]
        [HttpPost("accetta-privacy")]
        public async Task<IActionResult> AccettaPrivacy()
        {
            var user = await _userManager.FindByIdAsync(User.GetAuthenticatedUserId());
            if (user == null)
                return Unauthorized("Sessione scaduta. Effettua di nuovo l'accesso.");

            // Gia' accettata: nessuna scrittura e nessuna riga doppia nel registro.
            if (!PresaVisionePrivacy.DaAccettare(user, _privacy.Versione))
                return NoContent();

            PresaVisionePrivacy.Registra(user, _privacy.Versione, _clock.UtcNow);
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new InvalidOperationException("Presa visione dell'informativa non salvata.");

            await _logActivityService.LogAsync(user.Id, $"Presa visione informativa privacy {_privacy.Versione}",
                HttpContext.GetIpAddress());

            return NoContent();
        }

        /// <summary>Assegna un ruolo a un utente — solo Admin</summary>
        [Authorize(Roles = Roles.Admin)]
        [HttpPost("assign-role")]
        public async Task<IActionResult> AssignRole([FromBody] AssignRoleDTO dto)
        {
            if (!new[] { Roles.Admin, Roles.Staff, Roles.Cliente }.Contains(dto.Role))
                return BadRequest($"Ruolo '{dto.Role}' non valido. Ruoli disponibili: Admin, Staff, Cliente.");

            var user = await _userManager.FindByIdAsync(dto.UserId);
            if (user == null)
                return NotFound($"Utente con ID '{dto.UserId}' non trovato.");

            if (await _userManager.IsInRoleAsync(user, dto.Role))
                return BadRequest($"L'utente è già nel ruolo '{dto.Role}'.");

            var result = await _userManager.AddToRoleAsync(user, dto.Role);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            // AUD-M6: i token gia' emessi portano i ruoli vecchi. Cambiare lo stamp li invalida:
            // l'utente rifa' l'accesso e riceve un token con i ruoli giusti.
            await _userManager.UpdateSecurityStampAsync(user);

            _logger.LogInformation("[{Controller}] - [{Method}]: Ruolo '{Role}' assegnato all'utente {UserId}",
                nameof(AuthenticationUserController), nameof(AssignRole), dto.Role, dto.UserId);

            await _logActivityService.LogAsync(User.GetAuthenticatedUserId(),
                $"Ruolo '{dto.Role}' assegnato a utente ID {dto.UserId}",
                IndirizzoClient.Ottieni(HttpContext));

            return Ok(new { message = $"Ruolo '{dto.Role}' assegnato con successo." });
        }

        /// <summary>Rimuove un ruolo da un utente — solo Admin</summary>
        [Authorize(Roles = Roles.Admin)]
        [HttpDelete("remove-role")]
        public async Task<IActionResult> RemoveRole([FromBody] AssignRoleDTO dto)
        {
            // Chi chiama e' sempre un Admin e non puo' eliminare se stesso (DeleteUser): vietando
            // anche di togliersi il ruolo Admin, almeno un Admin resta sempre. Senza questo blocco,
            // rimasti a zero Admin, la schermata di primo avvio (SetupController, anonima) si
            // riaprirebbe a chiunque.
            if (dto.Role == Roles.Admin && dto.UserId == User.GetAuthenticatedUserId())
                return BadRequest("Non è possibile togliere a se stessi il ruolo Admin: chiedilo a un altro Admin.");

            var user = await _userManager.FindByIdAsync(dto.UserId);
            if (user == null)
                return NotFound($"Utente con ID '{dto.UserId}' non trovato.");

            if (!await _userManager.IsInRoleAsync(user, dto.Role))
                return BadRequest($"L'utente non è nel ruolo '{dto.Role}'.");

            var result = await _userManager.RemoveFromRoleAsync(user, dto.Role);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            // AUD-M6: senza questo l'utente conservava il ruolo tolto fino alla scadenza del token.
            await _userManager.UpdateSecurityStampAsync(user);

            _logger.LogInformation("[{Controller}] - [{Method}]: Ruolo '{Role}' rimosso dall'utente {UserId}",
                nameof(AuthenticationUserController), nameof(RemoveRole), dto.Role, dto.UserId);

            await _logActivityService.LogAsync(User.GetAuthenticatedUserId(),
                $"Ruolo '{dto.Role}' rimosso da utente ID {dto.UserId}",
                IndirizzoClient.Ottieni(HttpContext));

            return Ok(new { message = $"Ruolo '{dto.Role}' rimosso con successo." });
        }

        /// <summary>Lista tutti gli utenti con i loro ruoli — solo Admin</summary>
        [Authorize(Roles = Roles.Admin)]
        [HttpGet("get-users")]
        public async Task<IActionResult> GetUsers()
        {
            // REV-021: prima si caricavano tutti gli utenti e poi si chiamava GetRolesAsync
            // dentro il ciclo, cioe' una query al database per ogni utente (N+1). Con pochi
            // utenti non si nota; il costo cresce pero' in modo lineare e questa e' la pagina
            // che l'Admin apre per gestirli. Ora sono due query fisse: gli utenti, e in un solo
            // colpo tutte le assegnazioni di ruolo, che poi si accostano in memoria.
            var users = await _context.Users
                .AsNoTracking()
                .Select(u => new { u.Id, u.UserName, u.Email })
                .ToListAsync();

            var assegnazioni = await (from ur in _context.UserRoles
                                      join r in _context.Roles on ur.RoleId equals r.Id
                                      select new { ur.UserId, RoleName = r.Name })
                                     .AsNoTracking()
                                     .ToListAsync();

            var ruoliPerUtente = assegnazioni
                .GroupBy(a => a.UserId)
                .ToDictionary(g => g.Key, g => (IList<string>)g.Select(a => a.RoleName!).ToList());

            var result = users.Select(u => new UserResponseDTO
            {
                Id = u.Id,
                UserName = u.UserName!,
                Email = u.Email!,
                // Un utente senza alcun ruolo non compare fra le assegnazioni: va reso con una
                // lista vuota, non saltato, altrimenti sparirebbe dall'elenco dell'Admin.
                Roles = ruoliPerUtente.TryGetValue(u.Id, out var ruoli) ? ruoli : new List<string>()
            }).ToList();

            return Ok(result);
        }

        /// <summary>Dettaglio singolo utente — solo Admin</summary>
        [Authorize(Roles = Roles.Admin)]
        [HttpGet("get-user/{id}")]
        public async Task<IActionResult> GetUserById(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound($"Utente con ID '{id}' non trovato.");

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new UserResponseDTO
            {
                Id = user.Id,
                UserName = user.UserName!,
                Email = user.Email!,
                Roles = roles
            });
        }

        /// <summary>Aggiorna username e/o email di un utente — solo Admin</summary>
        [Authorize(Roles = Roles.Admin)]
        [HttpPut("update-user/{id}")]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserDTO dto)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound($"Utente con ID '{id}' non trovato.");

            var nomeCambiato = !string.IsNullOrWhiteSpace(dto.UserName) &&
                               !string.Equals(dto.UserName, user.UserName, StringComparison.Ordinal);

            var emailCambiata = !string.IsNullOrWhiteSpace(dto.Email) &&
                                !string.Equals(dto.Email, user.Email, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(dto.UserName))
                user.UserName = dto.UserName;

            if (!string.IsNullOrWhiteSpace(dto.Email))
                user.Email = dto.Email;

            // V2-014: nel registro vanno i NOMI dei campi cambiati, non i valori (l'email e' un
            // dato personale e il registro non si svuota).
            var campi = new List<string>();
            if (nomeCambiato) campi.Add("nome utente");
            if (emailCambiata) campi.Add("email");

            // Modifica, aggiornamento del security stamp e traccia: una sola operazione.
            IdentityResult result = IdentityResult.Success;
            await _transazione.EseguiAsync(async () =>
            {
                result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                    return;

                // Il token porta l'email: se cambia, quelli gia' emessi vanno rifatti (AUD-M6).
                if (emailCambiata)
                    await _userManager.UpdateSecurityStampAsync(user);

                if (campi.Count > 0)
                    await _logActivityService.LogAsync(User.GetAuthenticatedUserId(),
                        $"Modificato utente ID {id} ({string.Join(", ", campi)})",
                        IndirizzoClient.Ottieni(HttpContext));
            });

            if (!result.Succeeded)
                throw ErroriIdentity.ComeValidationException(result, "Modifica dell'utente non riuscita: controlla i dati inseriti.");

            _logger.LogInformation("[{Controller}] - [{Method}]: Utente {Id} aggiornato",
                nameof(AuthenticationUserController), nameof(UpdateUser), id);

            return Ok(new { message = "Utente aggiornato con successo." });
        }

        /// <summary>Elimina un utente dal sistema — solo Admin. Non è possibile eliminare se stessi.</summary>
        [Authorize(Roles = Roles.Admin)]
        [HttpDelete("delete-user/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var currentUserId = User.GetAuthenticatedUserId();
            if (currentUserId == id)
                return BadRequest("Non è possibile eliminare il proprio account.");

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound($"Utente con ID '{id}' non trovato.");

            // REV-038: la relazione utente -> prenotazioni non e' piu' a cascata, quindi il
            // database rifiuterebbe comunque l'eliminazione. Il controllo si fa qui per dare un
            // messaggio comprensibile invece di un errore tecnico di chiave esterna, e perche'
            // il motivo del rifiuto ("ha delle prenotazioni") e' un'informazione utile.
            var haPrenotazioni = await _context.Prenotazioni.AnyAsync(p => p.UserId == id);
            if (haPrenotazioni)
            {
                return Conflict(new
                {
                    message = "Impossibile eliminare l'utente: ha prenotazioni registrate. " +
                              "Lo storico delle prenotazioni non va cancellato perché è la base " +
                              "dei dati di riepilogo."
                });
            }

            // V2-014: eliminazione e traccia sono una sola operazione. Prima, se il registro
            // falliva, l'utente era gia' stato eliminato senza nessuna traccia.
            IdentityResult result = IdentityResult.Success;
            await _transazione.EseguiAsync(async () =>
            {
                result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                    return;

                await _logActivityService.LogAsync(User.GetAuthenticatedUserId(),
                    $"Eliminato utente ID {id}",
                    IndirizzoClient.Ottieni(HttpContext));
            });

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            _logger.LogWarning("[{Controller}] - [{Method}]: Utente {Id} eliminato da Admin {AdminId}",
                nameof(AuthenticationUserController), nameof(DeleteUser), id, currentUserId);

            return Ok(new { message = "Utente eliminato con successo." });
        }

        /// <summary>Reset password di un utente — solo Admin</summary>
        [Authorize(Roles = Roles.Admin)]
        [HttpPost("reset-password/{id}")]
        public async Task<IActionResult> ResetPassword(string id, [FromBody] AdminResetPasswordDTO dto)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound($"Utente con ID '{id}' non trovato.");

            // Genera un token di reset e applica subito la nuova password
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            _logger.LogWarning("[{Controller}] - [{Method}]: Password resettata per utente {Id}",
                nameof(AuthenticationUserController), nameof(ResetPassword), id);

            await _logActivityService.LogAsync(User.GetAuthenticatedUserId(),
                $"Password resettata per utente ID {id}",
                IndirizzoClient.Ottieni(HttpContext));

            return Ok(new { message = "Password resettata con successo." });
        }
    }
}