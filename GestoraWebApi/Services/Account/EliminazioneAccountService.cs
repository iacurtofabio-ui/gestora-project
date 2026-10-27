using GestoraWebApi.Auth;
using GestoraWebApi.Common;
using GestoraWebApi.Models;
using GestoraWebApi.Repositories.Prenotazioni;
using GestoraWebApi.Services.LogActivity;
using Microsoft.AspNetCore.Identity;

namespace GestoraWebApi.Services.Account
{
    public interface IEliminazioneAccountService
    {
        /// <summary>Le prenotazioni dell'utente ancora in programma (Attive o In corso, da oggi): finché ci sono, l'account resta.</summary>
        Task<List<Prenotazione>> PrenotazioniInProgrammaAsync(string userId);

        /// <summary>
        /// Elimina l'utente lasciando al locale le sue prenotazioni, anonime: in una sola transazione
        /// le stacca, scrive <paramref name="azioneRegistro"/> nel registro e cancella l'utente.
        /// Chi chiama controlla prima le prenotazioni in programma.
        /// </summary>
        Task EliminaAsync(ApplicationUser user, string azioneRegistro, string? ip);
    }

    /// <summary>
    /// V2-019 — come si elimina un account senza falsare i conteggi del locale. La usano la
    /// cancellazione dell'utente da «Il mio account» e la pulizia notturna degli account inattivi.
    /// </summary>
    public class EliminazioneAccountService : IEliminazioneAccountService
    {
        /// <summary>Il nome che prendono le prenotazioni dell'account eliminato.</summary>
        public const string NomeClienteEliminato = "Cliente eliminato";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPrenotazioniRepository _prenotazioniRepository;
        private readonly ILogActivityService _logActivityService;
        private readonly IEsecutoreTransazione _transazione;
        private readonly IClock _clock;

        public EliminazioneAccountService(UserManager<ApplicationUser> userManager,
                                          IPrenotazioniRepository prenotazioniRepository,
                                          ILogActivityService logActivityService,
                                          IEsecutoreTransazione transazione,
                                          IClock clock)
        {
            _userManager = userManager;
            _prenotazioniRepository = prenotazioniRepository;
            _logActivityService = logActivityService;
            _transazione = transazione;
            _clock = clock;
        }

        public Task<List<Prenotazione>> PrenotazioniInProgrammaAsync(string userId) =>
            _prenotazioniRepository.GetDaServireDiUtenteAsync(userId, _clock.TodayInRome);

        public async Task EliminaAsync(ApplicationUser user, string azioneRegistro, string? ip)
        {
            // Tutto o niente. La riga del registro resta con un id che non corrisponde più a
            // nessuno, senza nome né email.
            await _transazione.EseguiAsync(async () =>
            {
                var staccate = await _prenotazioniRepository.StaccaDaUtenteAsync(user.Id, NomeClienteEliminato);
                await _logActivityService.LogAsync(user.Id, $"{azioneRegistro} ({staccate} prenotazioni rese anonime)", ip);

                var risultato = await _userManager.DeleteAsync(user);
                if (!risultato.Succeeded)
                    throw new InvalidOperationException(
                        "Eliminazione dell'account non riuscita: " + string.Join(", ", risultato.Errors.Select(e => e.Code)));
            });
        }
    }
}
