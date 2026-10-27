using GestoraWebApi.Common;
using GestoraWebApi.Services.Email;
using Microsoft.Extensions.Options;

namespace GestoraWebApi.Services.Pareri
{
    public interface IPareriService
    {
        /// <summary>
        /// Manda il parere via email al destinatario in configurazione. Un parere col campo trappola
        /// compilato si scarta in silenzio: a chi lo manda la risposta sembra uguale.
        /// </summary>
        Task InviaAsync(ParereDTO parere, string ip);
    }

    /// <summary>
    /// V2-022 — modulo pubblico dei pareri. Nessun salvataggio nel database: il parere esiste solo
    /// nell'email che arriva a <see cref="PareriSettings.Destinatario"/>. Nei log solo l'esito, mai
    /// il testo o l'email di chi scrive.
    /// </summary>
    public class PareriService : IPareriService
    {
        private readonly IInvioEmail _invioEmail;
        private readonly IClock _clock;
        private readonly PareriSettings _settings;
        private readonly ILogger<PareriService> _logger;

        public PareriService(IInvioEmail invioEmail, IClock clock, IOptions<PareriSettings> settings, ILogger<PareriService> logger)
        {
            _invioEmail = invioEmail;
            _clock = clock;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task InviaAsync(ParereDTO parere, string ip)
        {
            if (!string.IsNullOrWhiteSpace(parere.SitoWeb))
            {
                _logger.LogInformation("[{Service}] - [{Method}]: Parere scartato da {Ip}: campo trappola compilato",
                    nameof(PareriService), nameof(InviaAsync), ip);
                return;
            }

            if (string.IsNullOrWhiteSpace(_settings.Destinatario))
            {
                // Solo in Development: fuori l'app non parte senza destinatario (controllo all'avvio).
                _logger.LogWarning("[{Service}] - [{Method}]: Pareri:Destinatario vuoto, parere non inviato",
                    nameof(PareriService), nameof(InviaAsync));
                return;
            }

            await _invioEmail.InviaAsync(_settings.Destinatario, "Nuovo parere su Gestora", Testo(parere));

            _logger.LogInformation("[{Service}] - [{Method}]: Parere inviato da {Ip}",
                nameof(PareriService), nameof(InviaAsync), ip);
        }

        private string Testo(ParereDTO parere)
        {
            var quando = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, SystemClock.RomeTimeZone).ToString("dd/MM/yyyy HH:mm");
            var nome = string.IsNullOrWhiteSpace(parere.Nome) ? "(non indicato)" : parere.Nome.Trim();
            var email = string.IsNullOrWhiteSpace(parere.Email) ? "(non indicata: non si può rispondere)" : parere.Email.Trim();

            return
                $"Nuovo parere dal modulo di Gestora, {quando}.{Environment.NewLine}{Environment.NewLine}" +
                $"Nome: {nome}{Environment.NewLine}" +
                $"Email: {email}{Environment.NewLine}{Environment.NewLine}" +
                $"Parere:{Environment.NewLine}{parere.Messaggio.Trim()}";
        }
    }
}
