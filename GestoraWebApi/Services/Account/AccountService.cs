using System.Globalization;
using GestoraWebApi.Auth;
using GestoraWebApi.Infrastructure.Auth;
using GestoraWebApi.Infrastructure.Exceptions;
using GestoraWebApi.Models;
using GestoraWebApi.Services.Account.DTOs;
using GestoraWebApi.Services.LogActivity;
using Microsoft.AspNetCore.Identity;

namespace GestoraWebApi.Services.Account
{
    public interface IAccountService
    {
        /// <summary>I dati dell'utente collegato.</summary>
        Task<MioAccountDTO> GetMioAccountAsync(string userId);

        /// <summary>
        /// Cambia la password verificando quella attuale. Restituisce l'utente aggiornato: chi chiama
        /// genera il token nuovo, perché il cambio invalida tutti quelli già emessi.
        /// </summary>
        Task<ApplicationUser> CambiaPasswordAsync(string userId, string passwordAttuale, string nuovaPassword, string ip);

        /// <summary>
        /// Cancella l'account dell'utente, confermato con la password. Bloccata (409) se ha
        /// prenotazioni da servire o se è l'ultimo Admin. Le prenotazioni passate restano, staccate
        /// dall'utente e con il nome reso anonimo.
        /// </summary>
        Task CancellaAccountAsync(string userId, string password, string ip);
    }

    /// <summary>
    /// V2-019 — «Il mio account»: quello che l'utente fa da solo sui propri dati. L'export per
    /// l'utente (T07) è stato tolto il 26/10/2026: troppo tecnico per chi prenota; le richieste di
    /// accesso ai dati arrivano per email.
    /// </summary>
    public class AccountService : IAccountService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogActivityService _logActivityService;
        private readonly IEliminazioneAccountService _eliminazione;
        private readonly ILogger<AccountService> _logger;

        public AccountService(UserManager<ApplicationUser> userManager,
                              ILogActivityService logActivityService,
                              IEliminazioneAccountService eliminazione,
                              ILogger<AccountService> logger)
        {
            _userManager = userManager;
            _logActivityService = logActivityService;
            _eliminazione = eliminazione;
            _logger = logger;
        }

        public async Task<MioAccountDTO> GetMioAccountAsync(string userId)
        {
            var user = await TrovaAsync(userId);

            return new MioAccountDTO
            {
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Ruoli = await _userManager.GetRolesAsync(user),
                DataCreazione = user.DataCreazione,
                UltimoAccesso = user.UltimoAccesso,
                PrivacyAccettataIl = user.PrivacyAccettataIl,
                PrivacyVersione = user.PrivacyVersione
            };
        }

        public async Task<ApplicationUser> CambiaPasswordAsync(string userId, string passwordAttuale, string nuovaPassword, string ip)
        {
            var user = await TrovaAsync(userId);
            await VerificaPasswordAsync(user, passwordAttuale, "passwordAttuale", "Password non cambiata.",
                "La password attuale non è corretta.", ip);

            // ChangePasswordAsync cambia anche il security stamp: le altre sessioni si chiudono.
            var risultato = await _userManager.ChangePasswordAsync(user, passwordAttuale, nuovaPassword);
            if (!risultato.Succeeded)
                throw ErroriIdentity.ComeValidationException(risultato, "Password non valida: controlla i requisiti.");

            await _userManager.ResetAccessFailedCountAsync(user);
            await _logActivityService.LogAsync(user.Id, "Password cambiata dall'account", ip);
            return user;
        }

        public async Task CancellaAccountAsync(string userId, string password, string ip)
        {
            var user = await TrovaAsync(userId);
            await VerificaPasswordAsync(user, password, "password", "Account non cancellato.",
                "La password non è corretta.", ip);

            // Senza Admin la schermata di primo avvio, anonima, si riaprirebbe (vedi remove-role).
            if (await _userManager.IsInRoleAsync(user, Roles.Admin)
                && (await _userManager.GetUsersInRoleAsync(Roles.Admin)).Count <= 1)
                throw new ConflictException(
                    "Sei l'unico amministratore: prima di cancellare il tuo account nomina un altro Admin.");

            // Un tavolo prenotato per stasera non deve restare a nome di nessuno: prima si annulla.
            var inProgramma = await _eliminazione.PrenotazioniInProgrammaAsync(user.Id);
            if (inProgramma.Count > 0)
                throw new ConflictException(MessaggioPrenotazioniInProgramma(inProgramma));

            await _eliminazione.EliminaAsync(user, "Account cancellato dall'utente", ip);

            _logger.LogWarning("[{Service}] - [{Method}]: Account {UserId} cancellato dall'utente",
                nameof(AccountService), nameof(CancellaAccountAsync), user.Id);
        }

        /// <summary>
        /// Il motivo del blocco scritto per chi prenota, non per la sala: «in programma», non «da
        /// servire» (DescrizioneImpegni parla allo Staff e non si usa qui).
        /// </summary>
        private static string MessaggioPrenotazioniInProgramma(IReadOnlyList<Prenotazione> prenotazioni)
        {
            var it = new CultureInfo("it-IT");
            var prima = prenotazioni[0];
            var quando = $"{it.DateTimeFormat.GetDayName(prima.DataPrenotazione.DayOfWeek)} " +
                         prima.DataPrenotazione.ToString("dd/MM/yyyy", it) +
                         (prima.FasciaOraria == null
                             ? ""
                             : $", {prima.FasciaOraria.OrarioInizio:HH\\:mm}-{prima.FasciaOraria.OrarioFine:HH\\:mm}") +
                         $" ({prima.NumeroCoperti} persone)";

            return prenotazioni.Count == 1
                ? $"Hai ancora una prenotazione in programma: {quando}. Annullala prima di cancellare l'account."
                : $"Hai ancora {prenotazioni.Count} prenotazioni in programma, la prima {quando}. " +
                  "Annullale prima di cancellare l'account.";
        }

        /// <summary>
        /// Controlla la password di chi è già collegato, con lo stesso blocco del login: chi trova una
        /// sessione aperta non deve poter provare password all'infinito da qui.
        /// </summary>
        private async Task VerificaPasswordAsync(ApplicationUser user, string password, string campo,
                                                 string titolo, string sbagliata, string ip)
        {
            if (await _userManager.IsLockedOutAsync(user))
                throw ErroreSulCampo(campo, titolo, "Troppi tentativi sbagliati. Riprova tra qualche minuto.");

            if (!await _userManager.CheckPasswordAsync(user, password))
            {
                await _userManager.AccessFailedAsync(user);
                _logger.LogWarning("[{Service}] - [{Method}]: Password errata per {UserId} da {Ip}",
                    nameof(AccountService), nameof(VerificaPasswordAsync), user.Id, ip);
                throw ErroreSulCampo(campo, titolo, sbagliata);
            }
        }

        private async Task<ApplicationUser> TrovaAsync(string userId) =>
            await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("Utente non trovato.");

        private static ValidationException ErroreSulCampo(string campo, string titolo, string messaggio) =>
            new(titolo, new Dictionary<string, string[]> { [campo] = [messaggio] });
    }
}
