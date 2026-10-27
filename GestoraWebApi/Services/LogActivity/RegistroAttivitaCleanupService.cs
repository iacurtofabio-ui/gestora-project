using GestoraWebApi.Common;
using GestoraWebApi.Repositories.LogActivity;
using Microsoft.Extensions.Options;

namespace GestoraWebApi.Services.LogActivity
{
    public interface IRegistroAttivitaCleanupService
    {
        /// <summary>Elimina le righe del registro più vecchie del periodo di conservazione. Restituisce quante.</summary>
        Task<int> EliminaScaduteAsync();
    }

    /// <summary>
    /// V2-019 — il registro attività contiene gli indirizzi IP: si tiene per
    /// <c>RegistroAttivita:MesiConservazione</c> mesi (24, come dice l'informativa), poi il job
    /// notturno lo svuota dalle righe più vecchie. Il riepilogo va nel log dell'applicazione, non
    /// nel registro stesso: sarebbe una riga in più ogni notte per dire che se ne sono tolte altre.
    /// </summary>
    public class RegistroAttivitaCleanupService : IRegistroAttivitaCleanupService
    {
        /// <summary>Righe eliminate per ogni giro: abbastanza per finire in fretta, poche per la memoria.</summary>
        public const int Blocco = 1000;

        private readonly ILogActivityRepository _repository;
        private readonly IClock _clock;
        private readonly RegistroAttivitaSettings _settings;
        private readonly ILogger<RegistroAttivitaCleanupService> _logger;

        public RegistroAttivitaCleanupService(ILogActivityRepository repository,
                                              IClock clock,
                                              IOptions<RegistroAttivitaSettings> settings,
                                              ILogger<RegistroAttivitaCleanupService> logger)
        {
            _repository = repository;
            _clock = clock;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<int> EliminaScaduteAsync()
        {
            var limite = _clock.UtcNow.AddMonths(-_settings.MesiConservazione);
            var totale = 0;

            int eliminate;
            do
            {
                eliminate = await _repository.EliminaPrimaDiAsync(limite, Blocco);
                totale += eliminate;
            }
            while (eliminate == Blocco);

            _logger.LogInformation("[{Service}] - [{Method}]: Eliminate {Totale} righe del registro attività precedenti al {Limite:yyyy-MM-dd}",
                nameof(RegistroAttivitaCleanupService), nameof(EliminaScaduteAsync), totale, limite);
            return totale;
        }
    }
}
