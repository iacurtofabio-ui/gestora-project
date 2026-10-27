using GestoraWebApi.Extensions;
using GestoraWebApi.Infrastructure.Auth;
using GestoraWebApi.Services.Account;
using GestoraWebApi.Services.Account.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using GestoraWebApi.Auth;

namespace GestoraWebApi.Controllers
{
    /// <summary>
    /// V2-019 — «Il mio account»: ogni utente collegato, di qualsiasi ruolo, sui propri dati.
    /// L'utente è sempre quello del token, mai un id passato da fuori.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly IJwtTokenGenerator _tokenGenerator;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(IAccountService accountService,
                                 IJwtTokenGenerator tokenGenerator,
                                 UserManager<ApplicationUser> userManager)
        {
            _accountService = accountService;
            _tokenGenerator = tokenGenerator;
            _userManager = userManager;
        }

        /// <summary>I dati dell'utente collegato.</summary>
        [HttpGet("mio-account")]
        public async Task<IActionResult> MioAccount()
            => Ok(await _accountService.GetMioAccountAsync(User.GetAuthenticatedUserId()));

        /// <summary>
        /// Cancellazione del proprio account, confermata con la password. 409 se ci sono prenotazioni
        /// da servire o se è l'ultimo Admin. Il token in uso smette di valere da solo: l'utente non
        /// esiste più e OnTokenValidated lo respinge.
        /// </summary>
        [HttpPost("cancella-account")]
        public async Task<IActionResult> CancellaAccount(CancellaAccountDTO request)
        {
            await _accountService.CancellaAccountAsync(User.GetAuthenticatedUserId(), request.Password,
                HttpContext.GetIpAddress());
            return NoContent();
        }

        /// <summary>
        /// Cambio password con quella attuale. Le altre sessioni si chiudono (security stamp nuovo);
        /// questa continua con il token restituito, che conserva l'inizio della sessione: il limite
        /// delle 12 ore dal login resta quello di prima.
        /// </summary>
        [HttpPost("cambia-password")]
        public async Task<IActionResult> CambiaPassword(CambiaPasswordDTO request)
        {
            var user = await _accountService.CambiaPasswordAsync(User.GetAuthenticatedUserId(),
                request.PasswordAttuale, request.NuovaPassword, HttpContext.GetIpAddress());

            var roles = await _userManager.GetRolesAsync(user);
            var stamp = await _userManager.GetSecurityStampAsync(user);
            var inizioSessione = JwtTokenGenerator.LeggiInizioSessione(User);
            var token = inizioSessione is null
                ? _tokenGenerator.GenerateToken(user.Id, user.Email!, roles, stamp)
                : _tokenGenerator.GenerateToken(user.Id, user.Email!, roles, stamp, inizioSessione.Value);

            return Ok(new { Email = user.Email, Token = token });
        }
    }
}
