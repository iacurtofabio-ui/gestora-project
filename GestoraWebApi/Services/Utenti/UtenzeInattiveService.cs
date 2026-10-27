using System.Globalization;
using GestoraWebApi.Auth;
using GestoraWebApi.Common;
using GestoraWebApi.Repositories.Utenti;
using GestoraWebApi.Services.Account;
using GestoraWebApi.Services.Email;
using GestoraWebApi.Services.LogActivity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GestoraWebApi.Services.Utenti
{
    /// <summary>
    /// V2-015: la registrazione è pubblica e nessuno eliminava mai un'utenza, quindi gli account
    /// creati e mai usati (veri o finti) restavano per sempre. Qui si tolgono quelli che dopo
    /// <see cref="UtenzeSettings.GiorniUtenzaMaiUsata"/> giorni non sono mai entrati e non hanno
    /// prenotazioni. Chi conta come "mai usata" lo decide <see cref="IUtentiRepository"/>.
    /// <para>
    /// V2-019: anche i Clienti entrati almeno una volta e poi spariti. Dopo
    /// <see cref="UtenzeSettings.MesiInattivita"/> mesi senza accessi meno
    /// <see cref="UtenzeSettings.SettimaneAvviso"/> settimane ricevono un'email di avviso; dopo i mesi
    /// pieni, e almeno quelle settimane dopo l'avviso, l'account si elimina come lo cancellerebbe
    /// l'utente (prenotazioni anonime, <see cref="IEliminazioneAccountService"/>).
    /// </para>
    /// </summary>
    public class UtenzeInattiveService : IUtenzeInattiveService
    {
        /// <summary>
        /// Tetto per esecuzione: dopo un'ondata di registrazioni finte il job non deve restare
        /// acceso per minuti. Il resto passa le notti successive.
        /// </summary>
        public const int MassimoPerEsecuzione = 500;

        public const string AzioneRegistro = "Utenza mai usata eliminata automaticamente";
        public const string AzioneAvviso = "Avviso di inattività inviato";
        public const string AzioneInattivo = "Account inattivo eliminato automaticamente";

        private readonly IUtentiRepository _repository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogActivityService _logActivityService;
        private readonly IEliminazioneAccountService _eliminazione;
        private readonly IInvioEmail _invioEmail;
        private readonly IClock _clock;
        private readonly UtenzeSettings _impostazioni;
        private readonly EmailSettings _email;
        private readonly ILogger<UtenzeInattiveService> _logger;

        public UtenzeInattiveService(IUtentiRepository repository,
                                     UserManager<ApplicationUser> userManager,
                                     ILogActivityService logActivityService,
                                     IEliminazioneAccountService eliminazione,
                                     IInvioEmail invioEmail,
                                     IClock clock,
                                     IOptions<UtenzeSettings> impostazioni,
                                     IOptions<EmailSettings> email,
                                     ILogger<UtenzeInattiveService> logger)
        {
            _repository = repository;
            _userManager = userManager;
            _logActivityService = logActivityService;
            _eliminazione = eliminazione;
            _invioEmail = invioEmail;
            _clock = clock;
            _impostazioni = impostazioni.Value;
            _email = email.Value;
            _logger = logger;
        }

        public async Task<int> AvvisaInattiviAsync()
        {
            var adesso = _clock.UtcNow;
            var soglia = adesso.AddMonths(-_impostazioni.MesiInattivita).AddDays(7 * _impostazioni.SettimaneAvviso);
            var candidati = await _repository.GetInattiviDaAvvisareAsync(soglia, MassimoPerEsecuzione);

            var avvisati = 0;
            foreach (var utente in candidati)
            {
                // Se l'email non parte non si segna niente: si riprova la notte dopo. Un errore su un
                // utente non ferma gli altri.
                try
                {
                    // Mai prima di SettimaneAvviso da adesso, anche per chi l'avviso lo riceve in ritardo.
                    var eliminazione = utente.UltimoAccesso!.Value.AddMonths(_impostazioni.MesiInattivita);
                    var minima = adesso.AddDays(7 * _impostazioni.SettimaneAvviso);
                    if (eliminazione < minima)
                        eliminazione = minima;

                    await _invioEmail.InviaAsync(utente.Email!, "Il tuo account Gestora sta per essere eliminato",
                        TestoAvviso(utente, eliminazione));

                    utente.AvvisoInattivitaInviatoIl = adesso;
                    var esito = await _userManager.UpdateAsync(utente);
                    if (!esito.Succeeded)
                    {
                        _logger.LogWarning("[{Service}] - [{Method}]: Avviso a {UserId} inviato ma non segnato - {Errors}",
                            nameof(UtenzeInattiveService), nameof(AvvisaInattiviAsync), utente.Id,
                            string.Join(", ", esito.Errors.Select(e => e.Description)));
                        continue;
                    }

                    await _logActivityService.LogAsync(utente.Id, AzioneAvviso);
                    avvisati++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{Service}] - [{Method}]: Errore avvisando l'utente {UserId}",
                        nameof(UtenzeInattiveService), nameof(AvvisaInattiviAsync), utente.Id);
                }
            }

            _logger.LogInformation("[{Service}] - [{Method}]: {Avvisati} Clienti inattivi avvisati su {Candidati} trovati (ultimo accesso prima del {Soglia:yyyy-MM-dd})",
                nameof(UtenzeInattiveService), nameof(AvvisaInattiviAsync), avvisati, candidati.Count, soglia);
            return avvisati;
        }

        public async Task<int> EliminaInattiviAsync()
        {
            var adesso = _clock.UtcNow;
            var soglia = adesso.AddMonths(-_impostazioni.MesiInattivita);
            var avvisatiPrimaDi = adesso.AddDays(-7 * _impostazioni.SettimaneAvviso);
            var candidati = await _repository.GetInattiviDaEliminareAsync(soglia, avvisatiPrimaDi, MassimoPerEsecuzione);

            var eliminati = 0;
            foreach (var utente in candidati)
            {
                try
                {
                    // Una prenotazione in programma vuol dire che il cliente c'è ancora (presa magari
                    // dallo Staff al telefono): l'account resta, si riguarda le notti successive.
                    if ((await _eliminazione.PrenotazioniInProgrammaAsync(utente.Id)).Count > 0)
                    {
                        _logger.LogInformation("[{Service}] - [{Method}]: Utente inattivo {UserId} non eliminato, ha prenotazioni in programma",
                            nameof(UtenzeInattiveService), nameof(EliminaInattiviAsync), utente.Id);
                        continue;
                    }

                    await _eliminazione.EliminaAsync(utente, AzioneInattivo, null);
                    eliminati++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{Service}] - [{Method}]: Errore eliminando l'utente inattivo {UserId}",
                        nameof(UtenzeInattiveService), nameof(EliminaInattiviAsync), utente.Id);
                }
            }

            _logger.LogInformation("[{Service}] - [{Method}]: {Eliminati} Clienti inattivi eliminati su {Candidati} trovati (ultimo accesso prima del {Soglia:yyyy-MM-dd})",
                nameof(UtenzeInattiveService), nameof(EliminaInattiviAsync), eliminati, candidati.Count, soglia);
            return eliminati;
        }

        private string TestoAvviso(ApplicationUser utente, DateTime eliminazioneUtc)
        {
            var it = new CultureInfo("it-IT");
            var giorno = TimeZoneInfo.ConvertTimeFromUtc(eliminazioneUtc, SystemClock.RomeTimeZone).ToString("d MMMM yyyy", it);
            var accesso = $"{_email.UrlFrontend.TrimEnd('/')}/login";

            return
                $"Ciao {utente.UserName},{Environment.NewLine}{Environment.NewLine}" +
                $"non entri in Gestora da quasi {_impostazioni.MesiInattivita} mesi. Per proteggere i tuoi dati, " +
                $"gli account inattivi vengono eliminati: il tuo lo sarà a partire dal giorno {giorno}.{Environment.NewLine}{Environment.NewLine}" +
                $"Se vuoi tenerlo, basta accedere una volta prima di quella data:{Environment.NewLine}{Environment.NewLine}" +
                $"{accesso}{Environment.NewLine}{Environment.NewLine}" +
                "Se non ti serve più, non devi fare niente. Le prenotazioni già passate restano al locale, senza il tuo nome.";
        }

        public async Task<int> EliminaUtenzeMaiUsateAsync()
        {
            var soglia = _clock.UtcNow.AddDays(-_impostazioni.GiorniUtenzaMaiUsata);
            var candidati = await _repository.GetUtenzeMaiUsateAsync(soglia, MassimoPerEsecuzione);

            var eliminati = 0;
            foreach (var utente in candidati)
            {
                // Un errore su un utente (es. una prenotazione arrivata nel frattempo, fermata
                // dalla FK) non deve bloccare gli altri: si annota e si passa al successivo.
                try
                {
                    var esito = await _userManager.DeleteAsync(utente);
                    if (!esito.Succeeded)
                    {
                        _logger.LogWarning("[{Service}] - [{Method}]: Utenza {UserId} non eliminata - {Errors}",
                            nameof(UtenzeInattiveService), nameof(EliminaUtenzeMaiUsateAsync), utente.Id,
                            string.Join(", ", esito.Errors.Select(e => e.Description)));
                        continue;
                    }

                    await _logActivityService.LogAsync(utente.Id, AzioneRegistro);
                    eliminati++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{Service}] - [{Method}]: Errore eliminando l'utenza {UserId}",
                        nameof(UtenzeInattiveService), nameof(EliminaUtenzeMaiUsateAsync), utente.Id);
                }
            }

            _logger.LogInformation("[{Service}] - [{Method}]: {Eliminati} utenze mai usate eliminate su {Candidati} trovate (create prima del {Soglia:yyyy-MM-dd})",
                nameof(UtenzeInattiveService), nameof(EliminaUtenzeMaiUsateAsync), eliminati, candidati.Count, soglia);

            return eliminati;
        }
    }
}
