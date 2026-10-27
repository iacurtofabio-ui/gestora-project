using GestoraWebApi.Services.Utenti;
using Quartz;

namespace GestoraWebApi.Background
{
    // V2-015: stessa struttura di PrenotazioniCleanupJob. Due esecuzioni insieme (cron e avvio
    // manuale da JobsController) proverebbero a eliminare gli stessi utenti.
    [DisallowConcurrentExecution]
    public class UtenzeInattiveCleanupJob : IJob
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UtenzeInattiveCleanupJob> _logger;

        public UtenzeInattiveCleanupJob(IServiceProvider serviceProvider, ILogger<UtenzeInattiveCleanupJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("UtenzeInattiveCleanupJob started at {Time}", DateTime.UtcNow);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IUtenzeInattiveService>();

                // V2-019: tre passi indipendenti. Un errore in uno non deve saltare gli altri, ed
                // eliminare prima di avvisare evita di mandare un avviso a chi sta per sparire.
                await Esegui(service.EliminaUtenzeMaiUsateAsync, "la pulizia delle utenze mai usate");
                await Esegui(service.EliminaInattiviAsync, "l'eliminazione degli account inattivi");
                await Esegui(service.AvvisaInattiviAsync, "l'avviso agli account inattivi");

                _logger.LogInformation("UtenzeInattiveCleanupJob completed at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la pulizia automatica delle utenze.");
            }
        }

        private async Task Esegui(Func<Task<int>> passo, string descrizione)
        {
            try
            {
                await passo();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante {Passo}.", descrizione);
            }
        }
    }
}
