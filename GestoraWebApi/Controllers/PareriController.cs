using GestoraWebApi.Extensions;
using GestoraWebApi.Infrastructure.RateLimiting;
using GestoraWebApi.Services.Pareri;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GestoraWebApi.Controllers
{
    /// <summary>
    /// V2-022 — modulo pubblico dei pareri: chiunque, anche senza account. Il limite di richieste per
    /// indirizzo e il campo trappola fermano gli invii automatici.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PareriController : ControllerBase
    {
        private readonly IPareriService _pareriService;

        public PareriController(IPareriService pareriService)
        {
            _pareriService = pareriService;
        }

        /// <summary>Manda un parere. Risponde sempre allo stesso modo, anche quando lo scarta.</summary>
        [EnableRateLimiting(LimitiRichieste.Pareri)]
        [HttpPost("invia")]
        public async Task<IActionResult> Invia(ParereDTO parere)
        {
            await _pareriService.InviaAsync(parere, HttpContext.GetIpAddress());
            return Ok("Grazie, il tuo parere è arrivato.");
        }
    }
}
