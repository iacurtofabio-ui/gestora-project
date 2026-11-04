using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using GestoraWebApi.Services.Email;

namespace GestoraWebApi.IntegrationTests.Infrastruttura
{
    public sealed record EmailInviata(string Destinatario, string Oggetto, string Testo)
    {
        /// <summary>Il primo link del testo, quello che l'utente aprirebbe.</summary>
        public Uri Link => new(Regex.Match(Testo, @"https?://\S+").Value);
    }

    /// <summary>Al posto dell'SMTP: le email restano qui e il test le legge.</summary>
    public sealed class PostaInMemoria : IInvioEmail
    {
        private readonly ConcurrentQueue<EmailInviata> _email = new();

        public IReadOnlyCollection<EmailInviata> Email => _email;

        public Task InviaAsync(string destinatario, string oggetto, string testo, CancellationToken ct = default)
        {
            _email.Enqueue(new EmailInviata(destinatario, oggetto, testo));
            return Task.CompletedTask;
        }

        public void Svuota() => _email.Clear();
    }
}
