using System.Text;
using GestoraWebApi.Auth;
using GestoraWebApi.Common;
using GestoraWebApi.Infrastructure.Auth;
using GestoraWebApi.Services.LogActivity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace GestoraWebApi.Services.Email
{
    public interface IRecuperoPasswordService
    {
        /// <summary>
        /// Manda il link per reimpostare la password. Non dice mai se l'email esiste: chi chiama
        /// risponde sempre allo stesso modo, così non si può scoprire chi è registrato.
        /// </summary>
        Task RichiediAsync(string email, string ip);

        /// <summary>
        /// Imposta la nuova password dal link. False se utente o token non sono validi (o scaduti);
        /// una password che Identity rifiuta arriva come ValidationException sul campo.
        /// </summary>
        Task<bool> ReimpostaAsync(string userId, string token, string nuovaPassword, string ip);
    }

    /// <summary>
    /// V2-019 — recupero password autonomo: token di Identity (2 ore, RecuperoPasswordTokenProvider),
    /// link per email con lo stesso schema della conferma (V2-003).
    /// </summary>
    public class RecuperoPasswordService : IRecuperoPasswordService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IInvioEmail _invioEmail;
        private readonly ILogActivityService _logActivityService;
        private readonly EmailSettings _settings;
        private readonly ILogger<RecuperoPasswordService> _logger;

        private readonly ITentativiAccesso _tentativiAccesso;

        public RecuperoPasswordService(UserManager<ApplicationUser> userManager,
                                       IInvioEmail invioEmail,
                                       ILogActivityService logActivityService,
                                       IOptions<EmailSettings> settings,
                                       ITentativiAccesso tentativiAccesso,
                                       ILogger<RecuperoPasswordService> logger)
        {
            _tentativiAccesso = tentativiAccesso;
            _userManager = userManager;
            _invioEmail = invioEmail;
            _logActivityService = logActivityService;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task RichiediAsync(string email, string ip)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return;

            try
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var tokenInUrl = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var link = $"{_settings.UrlFrontend.TrimEnd('/')}/reimposta-password?userId={Uri.EscapeDataString(user.Id)}&token={tokenInUrl}";
                var ore = (int)RecuperoPasswordTokenProvider<ApplicationUser>.Durata.TotalHours;

                var testo =
                    $"Ciao {user.UserName},{Environment.NewLine}{Environment.NewLine}" +
                    $"per scegliere una nuova password di Gestora apri questo link:{Environment.NewLine}{Environment.NewLine}" +
                    $"{link}{Environment.NewLine}{Environment.NewLine}" +
                    $"Il link è valido per {ore} ore e si può usare una volta sola. " +
                    "Se non l'hai chiesto tu, ignora questo messaggio: la tua password resta quella di prima.";

                await _invioEmail.InviaAsync(user.Email!, "Reimposta la password di Gestora", testo);
                await _logActivityService.LogAsync(user.Id, "Richiesto il link per reimpostare la password", ip);
            }
            catch (Exception ex)
            {
                // Chi chiama non deve accorgersene (stessa risposta sempre): resta il log, con l'UserId.
                _logger.LogError(ex, "[{Service}] - [{Method}]: Invio del link di recupero password non riuscito per {UserId}",
                    nameof(RecuperoPasswordService), nameof(RichiediAsync), user.Id);
            }
        }

        public async Task<bool> ReimpostaAsync(string userId, string token, string nuovaPassword, string ip)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return false;

            string tokenOriginale;
            try
            {
                tokenOriginale = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            }
            catch (FormatException)
            {
                return false;
            }

            // ResetPasswordAsync cambia anche il security stamp: i token di accesso già emessi non
            // valgono più (OnTokenValidated), quindi chi era entrato con la vecchia password è fuori.
            var risultato = await _userManager.ResetPasswordAsync(user, tokenOriginale, nuovaPassword);
            if (!risultato.Succeeded)
            {
                if (risultato.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
                {
                    _logger.LogWarning("[{Service}] - [{Method}]: Link di recupero password non valido per {UserId} da {Ip}",
                        nameof(RecuperoPasswordService), nameof(ReimpostaAsync), user.Id, ip);
                    return false;
                }

                throw ErroriIdentity.ComeValidationException(risultato, "Password non valida: controlla i requisiti.");
            }

            // Chi ha aperto il link ha dimostrato di leggere quella casella: l'email è confermata, e
            // il blocco per troppi tentativi sbagliati non ha più motivo di restare.
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }
            // V2-027: il blocco ora è per email + indirizzo; le due righe di Identity sbloccano un
            // account rimasto bloccato con il meccanismo di prima.
            _tentativiAccesso.Azzera(user.Email, ip);
            await _userManager.ResetAccessFailedCountAsync(user);
            await _userManager.SetLockoutEndDateAsync(user, null);

            await _logActivityService.LogAsync(user.Id, "Password reimpostata con il link email", ip);
            return true;
        }
    }
}
