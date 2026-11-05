using GestoraWebApi.Common;
using Microsoft.Extensions.Options;

namespace GestoraWebApi.Services.Email
{
    /// <summary>
    /// V2-003: solo sviluppo, quando Email:Host è vuoto. Non invia nulla: scrive nei log il
    /// contenuto della email, così il link di conferma si copia dalla console. V3-001: con
    /// Email:CartellaSviluppo la salva anche come file, per le prove automatiche nel browser.
    /// </summary>
    public class InvioEmailSuLog : IInvioEmail
    {
        private readonly ILogger<InvioEmailSuLog> _logger;
        private readonly string _cartella;

        public InvioEmailSuLog(ILogger<InvioEmailSuLog> logger, IOptions<EmailSettings> settings)
        {
            _logger = logger;
            _cartella = settings.Value.CartellaSviluppo;
        }

        public async Task InviaAsync(string destinatario, string oggetto, string testo, CancellationToken ct = default)
        {
            _logger.LogInformation("[Email di sviluppo, non inviata] A: {Destinatario} | Oggetto: {Oggetto}{NewLine}{Testo}",
                destinatario, oggetto, Environment.NewLine, testo);

            if (string.IsNullOrWhiteSpace(_cartella))
                return;

            Directory.CreateDirectory(_cartella);

            // Il nome non contiene l'indirizzo: arriva da chi si registra e non deve diventare un percorso.
            var file = Path.Combine(_cartella, $"{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(file, $"A: {destinatario}\nOggetto: {oggetto}\n\n{testo}", ct);
        }
    }
}
