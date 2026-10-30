using FluentValidation;
using FluentValidation.AspNetCore;
using GestoraWebApi.Auth;
using GestoraWebApi.Background;
using GestoraWebApi.Common;
using GestoraWebApi.Context;
using GestoraWebApi.Development;
using GestoraWebApi.Extensions;
using GestoraWebApi.Infrastructure.Auth;
using GestoraWebApi.Infrastructure.Middleware;
using GestoraWebApi.Infrastructure.RateLimiting;
using GestoraWebApi.Mappings;
using GestoraWebApi.Models;
using GestoraWebApi.Repositories.FasciaOrarie;
using GestoraWebApi.Repositories.LogActivity;
using GestoraWebApi.Repositories.Postazioni;
using GestoraWebApi.Repositories.Prenotazioni;
using GestoraWebApi.Repositories.Utenti;
using GestoraWebApi.Repositories.Zone;
using GestoraWebApi.Services.Dashboard;
using GestoraWebApi.Services.Disponibilita;
using GestoraWebApi.Services.FasciaOrarie;
using GestoraWebApi.Services.LogActivity;
using GestoraWebApi.Services.PostazioneAssignment;
using GestoraWebApi.Services.Postazioni;
using GestoraWebApi.Services.Prenotazioni;
using GestoraWebApi.Services.Utenti;
using GestoraWebApi.Services.Zone;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Quartz;
using Quartz.Impl;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;



var builder = WebApplication.CreateBuilder(args);

// V2-018: niente header "Server: Kestrel", che dice a chiunque quale server c'e' dietro.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// Configurazione obbligatoria: senza questi valori il servizio non puo funzionare.
// Si valida subito, prima di registrare qualsiasi servizio, per fallire con un messaggio
// esplicito invece che piu avanti con errori opachi del driver
// (es. "The ConnectionString property has not been initialized" dentro il seed dei ruoli).
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configurazione mancante: ConnectionStrings:DefaultConnection. " +
        "In sviluppo impostarla con 'dotnet user-secrets set', in produzione come variabile " +
        "d'ambiente ConnectionStrings__DefaultConnection.");
}

var jwtSecret = builder.Configuration["JwtSettings:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    throw new InvalidOperationException(
        "Configurazione mancante: JwtSettings:Secret. " +
        "In sviluppo impostarlo con 'dotnet user-secrets set', in produzione come variabile " +
        "d'ambiente JwtSettings__Secret.");
}

// HMAC-SHA256 richiede una chiave di almeno 256 bit: sotto questa soglia la generazione
// del token fallirebbe solo al primo login, non all'avvio.
if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException(
        "JwtSettings:Secret troppo corto: servono almeno 32 caratteri (256 bit) per HMAC-SHA256.");
}

builder.Services.AddDbContext<GestoraContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        // Connection resiliency: la rete privata tra i container non e' immediatamente
        // disponibile all'avvio e puo avere interruzioni transitorie. Senza retry il primo
        // avvio puo fallire anche con la configurazione corretta.
        npgsql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null)));

// V2-018: chiavi di Data Protection nel database, vedi DataProtectionExtensions.
builder.Services.AddChiaviDataProtection();

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

//JWT Authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddAuthorization();

builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

// V2-010: durata del token e limite della sessione controllati all'avvio. Con MaxSessionHours a
// zero (variabile dimenticata) ogni login darebbe un token già scaduto.
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection("JwtSettings"))
    .Validate(s => s.ExpiryMinutes > 0,
        "Configurazione non valida: JwtSettings:ExpiryMinutes deve essere maggiore di zero.")
    .Validate(s => s.MaxSessionHours > 0,
        "Configurazione non valida: JwtSettings:MaxSessionHours deve essere maggiore di zero.")
    .Validate(s => s.ExpiryMinutes <= s.MaxSessionHours * 60,
        "Configurazione non valida: JwtSettings:ExpiryMinutes non può superare MaxSessionHours.")
    .ValidateOnStart();

builder.Services.AddOptions<PrenotazioniSettings>()
    .Bind(builder.Configuration.GetSection(PrenotazioniSettings.Sezione))
    .Validate(s => s.MaxCopertiPerPrenotazione > 0,
        "Configurazione non valida: Prenotazioni:MaxCopertiPerPrenotazione deve essere maggiore di zero.")
    .Validate(s => s.MaxCopertiPrenotazioneOnline > 0,
        "Configurazione non valida: Prenotazioni:MaxCopertiPrenotazioneOnline deve essere maggiore di zero.")
    .Validate(s => s.MaxCopertiPrenotazioneOnline <= s.MaxCopertiPerPrenotazione,
        "Configurazione non valida: Prenotazioni:MaxCopertiPrenotazioneOnline non può superare MaxCopertiPerPrenotazione.")
    .ValidateOnStart();

// V2-015: con zero giorni la pulizia notturna eliminerebbe ogni Cliente appena registrato.
builder.Services.AddOptions<UtenzeSettings>()
    .Bind(builder.Configuration.GetSection(UtenzeSettings.Sezione))
    .Validate(s => s.GiorniUtenzaMaiUsata > 0,
        "Configurazione non valida: Utenze:GiorniUtenzaMaiUsata deve essere maggiore di zero.")
    // V2-019: senza mesi o settimane l'avviso e l'eliminazione degli inattivi partirebbero subito.
    .Validate(s => s.MesiInattivita > 0 && s.SettimaneAvviso > 0,
        "Configurazione non valida: Utenze:MesiInattivita e Utenze:SettimaneAvviso devono essere maggiori di zero.")
    .ValidateOnStart();

// V2-019: senza versione nessuno potrebbe prendere visione dell'informativa; oltre 20 caratteri
// non entrerebbe nella colonna Utenti.PrivacyVersione.
builder.Services.AddOptions<PrivacySettings>()
    .Bind(builder.Configuration.GetSection(PrivacySettings.Sezione))
    .Validate(s => !string.IsNullOrWhiteSpace(s.Versione) && s.Versione.Length <= 20,
        "Configurazione non valida: Privacy:Versione è obbligatoria e non può superare 20 caratteri.")
    .ValidateOnStart();

// V2-019: con zero mesi il job notturno svuoterebbe l'intero registro attività.
builder.Services.AddOptions<RegistroAttivitaSettings>()
    .Bind(builder.Configuration.GetSection(RegistroAttivitaSettings.Sezione))
    .Validate(s => s.MesiConservazione > 0,
        "Configurazione non valida: RegistroAttivita:MesiConservazione deve essere maggiore di zero.")
    .ValidateOnStart();

// V2-011: con un limite a zero (variabile dimenticata) l'endpoint risponderebbe 429 a tutti.
builder.Services.AddOptions<LimitiRichiesteSettings>()
    .Bind(builder.Configuration.GetSection(LimitiRichiesteSettings.Sezione))
    .Validate(s => s.RegistrazioniPerFinestra > 0 && s.RegistrazioneFinestraMinuti > 0,
        "Configurazione non valida: LimitiRichieste:RegistrazioniPerFinestra e RegistrazioneFinestraMinuti devono essere maggiori di zero.")
    .Validate(s => s.DisponibilitaRaffica > 0 && s.DisponibilitaAlMinuto > 0,
        "Configurazione non valida: LimitiRichieste:DisponibilitaRaffica e DisponibilitaAlMinuto devono essere maggiori di zero.")
    .Validate(s => s.ConfermaEmailPerFinestra > 0 && s.ConfermaEmailFinestraMinuti > 0,
        "Configurazione non valida: LimitiRichieste:ConfermaEmailPerFinestra e ConfermaEmailFinestraMinuti devono essere maggiori di zero.")
    .Validate(s => s.PareriPerFinestra > 0 && s.PareriFinestraGiorni > 0,
        "Configurazione non valida: LimitiRichieste:PareriPerFinestra e PareriFinestraGiorni devono essere maggiori di zero.")
    .ValidateOnStart();

// V2-016: «portale in manutenzione», spento di default. Nessun controllo: true o false.
builder.Services.AddOptions<ManutenzioneSettings>()
    .Bind(builder.Configuration.GetSection(ManutenzioneSettings.Sezione));

// V2-028: fuori da Development la parola segreta di Vercel è obbligatoria. Senza, il backend non
// riconoscerebbe le richieste passate da Vercel e vedrebbe tutti con l'indirizzo di Vercel: i limiti
// di richieste e il blocco per indirizzo colpirebbero tutti gli utenti insieme.
builder.Services.AddOptions<InoltroVercelSettings>()
    .Bind(builder.Configuration.GetSection(InoltroVercelSettings.Sezione))
    .Validate(s => builder.Environment.IsDevelopment() ? s.Segreto.Length is 0 or >= 32 : s.Segreto.Length >= 32,
        "Configurazione non valida: InoltroVercel:Segreto deve avere almeno 32 caratteri (obbligatorio fuori da Development).")
    .Validate(s => !s.BloccaChiamateDirette || s.Segreto.Length > 0,
        "Configurazione non valida: InoltroVercel:BloccaChiamateDirette richiede InoltroVercel:Segreto.")
    .ValidateOnStart();

// V2-003: l'indirizzo del frontend serve per i link nelle email. Fuori da Development l'Host SMTP
// e' obbligatorio: senza, nessuno potrebbe confermare l'email e registrarsi sarebbe inutile,
// quindi l'app non parte (meglio un errore all'avvio che utenti bloccati in silenzio).
var emailSoloLog = builder.Environment.IsDevelopment();
builder.Services.AddOptions<EmailSettings>()
    .Bind(builder.Configuration.GetSection(EmailSettings.Sezione))
    .Validate(s => Uri.TryCreate(s.UrlFrontend, UriKind.Absolute, out _),
        "Configurazione non valida: Email:UrlFrontend deve essere un indirizzo completo (es. https://...).")
    .Validate(s => emailSoloLog || !string.IsNullOrWhiteSpace(s.Host),
        "Configurazione non valida: Email:Host obbligatorio fuori da Development.")
    .Validate(s => string.IsNullOrWhiteSpace(s.Host) || (s.Porta > 0 && !string.IsNullOrWhiteSpace(s.MittenteEmail)),
        "Configurazione non valida: con Email:Host servono anche Email:Porta e Email:MittenteEmail.")
    .ValidateOnStart();

// V2-022: fuori da Development i pareri devono avere un destinatario, altrimenti andrebbero persi
// in silenzio. In locale può mancare: il parere non parte e lo dice il log.
builder.Services.AddOptions<PareriSettings>()
    .Bind(builder.Configuration.GetSection(PareriSettings.Sezione))
    .Validate(s => emailSoloLog
                   || (!string.IsNullOrWhiteSpace(s.Destinatario) && System.Net.Mail.MailAddress.TryCreate(s.Destinatario, out _)),
        "Configurazione non valida: Pareri:Destinatario deve essere un indirizzo email valido fuori da Development.")
    .ValidateOnStart();

// Json Serializza TimeSpan come stringa "HH:mm"
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new TimeSpanToStringConverter());
    });

// Configura Quartz con persistenza su PostgreSQL.
//
// REV-028 — ⚠️ lo scheduler NON e' in cluster mode (manca store.UseClustering()).
// Oggi non e' un problema: la piattaforma esegue una sola istanza del servizio, quindi c'e' un
// solo scheduler e ogni job parte una volta. Diventa un problema nel momento in cui le repliche
// diventano due o piu': senza clustering ogni istanza legge le stesse tabelle QRTZ_ ma non
// concorre per acquisire il trigger, quindi lo stesso job partirebbe su ognuna. Le conseguenze
// non sono simmetriche fra i due job:
//   - PrenotazioniJob completa prenotazioni gia' scadute: rieseguirlo e' innocuo, la seconda
//     passata non trova piu' nulla da fare;
//   - PrenotazioniCleanupJob cancella fisicamente righe: due esecuzioni in parallelo sugli
//     stessi Id sono una corsa fra DELETE, con errori nei log per righe gia' sparite.
// Se un giorno si aggiungono repliche, prima di farlo va abilitato il clustering
// (store.UseClustering(...) e un SchedulerId distinto per istanza). Non si anticipa qui perche'
// il clustering ha un costo — heartbeat e lock a database a ogni ciclo — che oggi si pagherebbe
// senza alcun ritorno.
builder.Services.AddQuartz(q =>
{
    q.UsePersistentStore(store =>
    {
        store.UsePostgres(pg =>
        {
            pg.ConnectionString = connectionString;
            pg.TablePrefix = "QRTZ_";
        });
        store.UseNewtonsoftJsonSerializer();
    });

    var jobKey = new JobKey("PrenotazioniJob");
    var jobCleanupKey = new JobKey("PrenotazioniCleanupJob");
    var jobUtenzeKey = new JobKey("UtenzeInattiveCleanupJob");
    var jobRegistroKey = new JobKey("RegistroAttivitaCleanupJob");

    q.AddJob<PrenotazioniJob>(opts => opts.WithIdentity(jobKey).StoreDurably());
    q.AddJob<PrenotazioniCleanupJob>(opts => opts.WithIdentity(jobCleanupKey).StoreDurably());
    q.AddJob<UtenzeInattiveCleanupJob>(opts => opts.WithIdentity(jobUtenzeKey).StoreDurably());
    q.AddJob<RegistroAttivitaCleanupJob>(opts => opts.WithIdentity(jobRegistroKey).StoreDurably());

    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("PrenotazioniJob-trigger")
        // AUD-M10: senza InTimeZone il cron usava il fuso del container (UTC): "02:00" era
        // 03:00 o 04:00 in Italia. Il job ragiona gia' in ora di Roma (IClock).
        .WithCronSchedule("0 00 2 * * ?", x => x.InTimeZone(GestoraWebApi.Common.SystemClock.RomeTimeZone)));

    q.AddTrigger(opts => opts
        .ForJob(jobCleanupKey)
        .WithIdentity("PrenotazioniCleanupJob-trigger")
        .WithCronSchedule("0 30 2 * * ?", x => x.InTimeZone(GestoraWebApi.Common.SystemClock.RomeTimeZone)));

    // V2-015: dopo i due job delle prenotazioni.
    q.AddTrigger(opts => opts
        .ForJob(jobUtenzeKey)
        .WithIdentity("UtenzeInattiveCleanupJob-trigger")
        .WithCronSchedule("0 0 3 * * ?", x => x.InTimeZone(GestoraWebApi.Common.SystemClock.RomeTimeZone)));

    // V2-019: dopo la pulizia delle utenze, che scrive nel registro una riga per ogni eliminata.
    q.AddTrigger(opts => opts
        .ForJob(jobRegistroKey)
        .WithIdentity("RegistroAttivitaCleanupJob-trigger")
        .WithCronSchedule("0 30 3 * * ?", x => x.InTimeZone(GestoraWebApi.Common.SystemClock.RomeTimeZone)));
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);



// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

//In-memory cache
builder.Services.AddMemoryCache();

// Health check: endpoint interrogato dalla piattaforma di hosting dopo ogni deploy.
// Se non risponde, il deploy viene marcato come fallito e resta online la versione precedente.
// AddDbContextCheck verifica anche che il database sia raggiungibile: senza, un deploy con DB
// giù o rete privata non ancora pronta risultava comunque "Healthy" (il check controllava solo
// che il processo rispondesse, non che potesse fare il suo lavoro).
builder.Services.AddHealthChecks()
    .AddDbContextCheck<GestoraContext>("database");

// Rate limiting sul login: senza, un brute force sulla password non ha alcun freno lato server
// (il lockout di Identity, configurato in AddJwtAuthentication, blocca l'account dopo N
// tentativi falliti; questo limita anche il volume di richieste per IP, indipendentemente
// dall'account preso di mira).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("LoginPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            // REV-029: la partizione deve essere il client, non il proxy. Con
            // Connection.RemoteIpAddress dietro il proxy tutti finivano nella stessa partizione,
            // quindi questo era di fatto un limite globale di 5 tentativi al minuto per l'intera
            // applicazione: non fermava chi attacca un singolo account e poteva bloccare gli
            // utenti legittimi.
            partitionKey: GestoraWebApi.Common.IndirizzoClient.Ottieni(httpContext) ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // V2-011: registrazione e verifica della disponibilità, pubbliche. Regole e motivazioni in
    // LimitiRichieste, numeri in configurazione (sezione LimitiRichieste).
    options.AddPolicy(LimitiRichieste.Registrazione, httpContext =>
        LimitiRichieste.PartizioneRegistrazione(httpContext, LimitiDaConfigurazione(httpContext)));
    options.AddPolicy(LimitiRichieste.ConfermaEmail, httpContext =>
        LimitiRichieste.PartizioneConfermaEmail(httpContext, LimitiDaConfigurazione(httpContext)));
    // V2-019: stessi numeri della conferma email, contatore proprio (ogni policy ha il suo).
    options.AddPolicy(LimitiRichieste.RecuperoPassword, httpContext =>
        LimitiRichieste.PartizioneConfermaEmail(httpContext, LimitiDaConfigurazione(httpContext)));
    // V2-022: modulo dei pareri, stessi numeri e contatore proprio.
    options.AddPolicy(LimitiRichieste.Pareri, httpContext =>
        LimitiRichieste.PartizionePareri(httpContext, LimitiDaConfigurazione(httpContext)));
    options.AddPolicy(LimitiRichieste.Disponibilita, httpContext =>
        LimitiRichieste.PartizioneDisponibilita(httpContext, LimitiDaConfigurazione(httpContext)));

    // Prima il 429 arrivava senza corpo: il frontend non aveva un messaggio da mostrare e nei log
    // non restava traccia di chi era stato fermato. Il corpo ha la stessa forma degli altri
    // errori (ExceptionMiddleware), e Retry-After dice quando riprovare.
    options.OnRejected = async (context, cancellationToken) =>
    {
        var http = context.HttpContext;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var attesa))
            http.Response.Headers.RetryAfter = ((int)Math.Ceiling(attesa.TotalSeconds)).ToString();

        http.RequestServices.GetRequiredService<ILogger<Program>>().LogWarning(
            "Limite di richieste superato su {Path} da {Ip}",
            http.Request.Path, GestoraWebApi.Common.IndirizzoClient.Ottieni(http));

        await http.Response.WriteAsJsonAsync(new ErrorDetails
        {
            StatusCode = StatusCodes.Status429TooManyRequests,
            Message = "Troppe richieste in poco tempo. Attendi qualche minuto e riprova."
        }, cancellationToken);
    };
});

static LimitiRichiesteSettings LimitiDaConfigurazione(HttpContext httpContext) =>
    httpContext.RequestServices.GetRequiredService<IOptions<LimitiRichiesteSettings>>().Value;

var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

//Altri servizi
builder.Services.AddSwaggerDocumentation();

builder.Services.AddAutoMapper(typeof(PostazioneMappingProfile));

builder.Services.AddHttpContextAccessor();

builder.Services.ConfigureCustomInvalidModelState();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Un solo orologio per tutto il progetto (REV-016 / REV-092)
builder.Services.AddSingleton<GestoraWebApi.Common.IClock, GestoraWebApi.Common.SystemClock>();

// REV-032: helper condiviso per eseguire scrittura + audit log in una sola transazione.
// Scoped come il DbContext: deve essere lo stesso contesto della richiesta, altrimenti la
// transazione non avvolgerebbe le scritture dei repository.
builder.Services.AddScoped<GestoraWebApi.Common.IEsecutoreTransazione, GestoraWebApi.Common.EsecutoreTransazione>();

// V2-003: con Email:Host valorizzato le email partono davvero (SMTP); altrimenti, e solo in
// Development (lo garantisce la validazione sopra), il contenuto finisce nei log.
if (string.IsNullOrWhiteSpace(builder.Configuration[$"{EmailSettings.Sezione}:Host"]))
    builder.Services.AddScoped<GestoraWebApi.Services.Email.IInvioEmail, GestoraWebApi.Services.Email.InvioEmailSuLog>();
else
    builder.Services.AddScoped<GestoraWebApi.Services.Email.IInvioEmail, GestoraWebApi.Services.Email.InvioEmailSmtp>();

builder.Services.AddScoped<GestoraWebApi.Services.Email.IConfermaEmailService, GestoraWebApi.Services.Email.ConfermaEmailService>();
builder.Services.AddScoped<GestoraWebApi.Services.Email.IRecuperoPasswordService, GestoraWebApi.Services.Email.RecuperoPasswordService>();
builder.Services.AddScoped<GestoraWebApi.Services.Account.IAccountService, GestoraWebApi.Services.Account.AccountService>();
// V2-027: un solo contatore per tutta l'app (login e «Il mio account»), quindi singleton.
builder.Services.AddSingleton<GestoraWebApi.Infrastructure.Auth.ITentativiAccesso, GestoraWebApi.Infrastructure.Auth.TentativiAccesso>();
builder.Services.AddScoped<GestoraWebApi.Services.Account.IEliminazioneAccountService, GestoraWebApi.Services.Account.EliminazioneAccountService>();
builder.Services.AddScoped<GestoraWebApi.Services.Report.IReportPrenotazioniService, GestoraWebApi.Services.Report.ReportPrenotazioniService>();
builder.Services.AddScoped<GestoraWebApi.Services.Pareri.IPareriService, GestoraWebApi.Services.Pareri.PareriService>();

//REPOSITORY
builder.Services.AddScoped<ILogActivityRepository, LogActivityRepository>();
builder.Services.AddScoped<IPostazioneRepository, PostazioneRepository>();
builder.Services.AddScoped<IFasciaOrariaRepository, FasciaOrariaRepository>();
builder.Services.AddScoped<IPrenotazioniRepository, PrenotazioniRepository>();
builder.Services.AddScoped<IZonaRepository, ZonaRepository>();
builder.Services.AddScoped<IUtentiRepository, UtentiRepository>();


//SERVICES
builder.Services.AddScoped<ILogActivityService, LogActivityService>();
builder.Services.AddScoped<IPostazioneService, PostazioneService>();
builder.Services.AddScoped<IFasciaOrariaService, FasciaOrariaService>();
builder.Services.AddScoped<IPrenotazioniService, PrenotazioniService>();
builder.Services.AddScoped<IPostazioneAssignmentService, PostazioneAssignmentService>();
builder.Services.AddScoped<IDisponibilitaService, DisponibilitaService>();
builder.Services.AddScoped<IZonaService, ZonaService>();
builder.Services.AddScoped<GestoraWebApi.Services.Sala.ICoerenzaSalaService, GestoraWebApi.Services.Sala.CoerenzaSalaService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IUtenzeInattiveService, UtenzeInattiveService>();
builder.Services.AddScoped<GestoraWebApi.Services.LogActivity.IRegistroAttivitaCleanupService, GestoraWebApi.Services.LogActivity.RegistroAttivitaCleanupService>();

var app = builder.Build();

// Seed ruoli al primo avvio (Admin, Staff, Cliente)
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await RoleSeeder.SeedAsync(roleManager);
}

// ---------------------------------------------------------------------------------------
// Comandi di sviluppo. Non fanno partire il server: eseguono e basta.
//
// Vivono qui e non in un progetto separato perche' hanno bisogno degli stessi servizi gia'
// registrati (UserManager per l'hashing delle password, il DbContext per il dominio):
// duplicarne la configurazione altrove significherebbe tenerne due allineate a mano.
//
// Nessuno dei due parte senza il proprio argomento esplicito, e nessuno dei due parte fuori da
// Development. Il seed ha in piu' un controllo sull'host del database: vedi SeedSviluppo.
// ---------------------------------------------------------------------------------------
if (app.Environment.IsDevelopment() && args.Contains(SeedSviluppo.ArgomentoSeed))
{
    var seedLogger = app.Services.GetRequiredService<ILogger<Program>>();
    var statoIncoerente = args.Contains(SeedSviluppo.ArgomentoStatoIncoerente);
    await SeedSviluppo.EseguiAsync(app.Services, seedLogger, statoIncoerente);
    return;
}

// Seed per screenshot e dimostrazioni (V2-023): stesse protezioni, vedi SeedVetrina.
if (app.Environment.IsDevelopment() && args.Contains(SeedVetrina.ArgomentoSeed))
{
    await SeedVetrina.EseguiAsync(app.Services, app.Services.GetRequiredService<ILogger<Program>>());
    return;
}

if (app.Environment.IsDevelopment() && args.Contains(SeedSviluppo.ArgomentoPassword))
{
    var passwordLogger = app.Services.GetRequiredService<ILogger<Program>>();
    var posizione = Array.IndexOf(args, SeedSviluppo.ArgomentoPassword);

    if (posizione + 2 >= args.Length)
    {
        passwordLogger.LogError(
            "Uso: dotnet run -- {Argomento} <email> <nuova password>", SeedSviluppo.ArgomentoPassword);
        return;
    }

    await SeedSviluppo.ReimpostaPasswordAsync(
        app.Services, args[posizione + 1], args[posizione + 2], passwordLogger);
    return;
}

// Avviso esplicito se il database non è allineato al codice. Le migration restano applicate a
// mano (decisione di progetto), quindi questo non blocca l'avvio: serve solo a non scoprire il
// disallineamento dal primo errore su un endpoint qualsiasi, che sarebbe molto meno leggibile.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GestoraContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            startupLogger.LogWarning(
                "ATTENZIONE: il database ha {Count} migration non applicate: {Migrations}. " +
                "Il codice si aspetta uno schema più recente di quello presente — applicare con " +
                "'dotnet ef database update' prima di considerare l'ambiente allineato.",
                pending.Count, string.Join(", ", pending));
        }
    }
    catch (Exception ex)
    {
        // Non deve mai impedire l'avvio: se il controllo stesso fallisce (es. DB
        // temporaneamente irraggiungibile), meglio un avviso in log che un crash.
        startupLogger.LogWarning(ex, "Impossibile verificare l'allineamento delle migration all'avvio.");
    }
}

// REV-029 — indirizzo reale del client.
//
// Qui c'era UseForwardedHeaders, la scelta idiomatica, ed e' stata la prima strada tentata. In
// questo ambiente pero' il middleware non elabora l'header: verificato in produzione con un
// endpoint diagnostico, X-Forwarded-For arrivava valorizzato con l'indirizzo giusto,
// X-Original-Forwarded-For restava vuoto - segno che non era stato consumato nulla - e
// Connection.RemoteIpAddress continuava a essere quello del proxy. Esclusi nell'ordine: le liste
// di proxy noti (svuotate, quindi il controllo e' disattivato), la posizione nella pipeline (era
// il primo middleware) e l'assenza di X-Forwarded-Proto (l'header risulta presente, vale
// "https").
//
// La lettura e' quindi fatta esplicitamente in Common/IndirizzoClient, usato sia dall'audit
// trail sia dalla partizione del rate limit del login. E' meno idiomatico, ma non dipende dal
// comportamento di un componente che dall'esterno non e' ispezionabile, ed e' coperto da test.
// Se un domani si volesse tornare al middleware, va prima riprodotto il problema in un ambiente
// di prova: non ripetere i tentativi a colpi di deploy in produzione, e' costato tre giri.

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerDocumentation();
}

// V2-018: header di sicurezza su ogni risposta, anche sugli errori e sui 503 della manutenzione.
// Dopo Swagger (la sua pagina non reggerebbe la Content-Security-Policy), prima di tutto il resto.
app.UseMiddleware<SicurezzaHeaderMiddleware>();

// FASE 5: una riga per richiesta, invece di dover ricostruire "cosa e' successo" da piu' log
// sparsi nel controller/service. /health lo chiama Azure di continuo (keepalive): alzato a
// Verbose per non intasare il log con una riga ogni pochi secondi che non dice nulla di nuovo.
//
// Va PRIMA di UseGlobalExceptionHandler, non dopo (nonostante la bozza iniziale dicesse il
// contrario): il middleware registrato per primo e' il piu' esterno, quindi vede la risposta
// FINALE. Con l'ordine inverso, un 409 di dominio (ConflictException) veniva loggato come "500"
// qui: l'eccezione risale a UseSerilogRequestLogging prima che UseGlobalExceptionHandler la
// traduca nel codice giusto. Verificato con una richiesta vera che genera un 409: con l'ordine
// sbagliato il log diceva 500, con questo dice 409.
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "{RequestMethod} {RequestPath} → {StatusCode} in {Elapsed:0}ms";
    options.GetLevel = (httpContext, _, ex) => ex != null || httpContext.Response.StatusCode >= 500
        ? Serilog.Events.LogEventLevel.Warning
        : httpContext.Request.Path.StartsWithSegments("/health")
            ? Serilog.Events.LogEventLevel.Verbose
            : Serilog.Events.LogEventLevel.Information;
});

app.UseGlobalExceptionHandler();

app.UseCors("FrontendPolicy");

// V2-016: dopo UseCors (il browser deve poter leggere il 503), prima di autenticazione e limiti
// di richieste: in manutenzione nessuna chiamata a /api arriva ai controller.
app.UseMiddleware<ManutenzioneMiddleware>();

//app.UseHttpsRedirection(); (verificare se serve abilitare in produzione)

// V2-028: riconosce le richieste passate da Vercel (parola segreta) e ne prende l'indirizzo vero.
// Prima dei limiti di richieste e dell'autenticazione, che usano l'indirizzo.
app.UseMiddleware<InoltroVercelMiddleware>();

// V2-028: chi scrive con il cookie di sessione deve rimandare anche il valore del cookie CSRF
// nell'header. Prima dell'autenticazione: una richiesta rifiutata non arriva a leggere il token.
app.UseMiddleware<ProtezioneCsrfMiddleware>();

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

// Endpoint pubblico e senza autenticazione: deve poter rispondere anche a chi non ha un token.
app.MapHealthChecks("/health");

app.MapControllers();

app.Run();
