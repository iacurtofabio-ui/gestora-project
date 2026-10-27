using GestoraWebApi.Services.LogActivity;
using Quartz;

namespace GestoraWebApi.Background
{
    // V2-019: stessa struttura di UtenzeInattiveCleanupJob. Due esecuzioni insieme (cron e avvio
    // manuale da JobsController) proverebbero a eliminare le stesse righe.
    [DisallowConcurrentExecution]
    public class RegistroAttivitaCleanupJob : IJob
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RegistroAttivitaCleanupJob> _logger;

        public RegistroAttivitaCleanupJob(IServiceProvider serviceProvider, ILogger<RegistroAttivitaCleanupJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("RegistroAttivitaCleanupJob started at {Time}", DateTime.UtcNow);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRegistroAttivitaCleanupService>();
                await service.EliminaScaduteAsync();

                _logger.LogInformation("RegistroAttivitaCleanupJob completed at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la pulizia automatica del registro attività.");
            }
        }
    }
}
