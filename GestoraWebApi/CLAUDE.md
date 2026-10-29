# GestoraWebApi — Backend

Questo file vale solo quando si lavora dentro `GestoraWebApi/`. Per stato sessione, decisioni di
prodotto e regole del progetto vedi il `CLAUDE.md` alla radice del repo — resta valido sempre.

## Stack

ASP.NET Core 10 (dal `V2-017`), C#, Entity Framework Core 10 + Npgsql (PostgreSQL), ASP.NET Identity + JWT
Bearer (3 ruoli: Admin, Staff, Cliente), FluentValidation 11, AutoMapper, Serilog,
IMemoryCache (30 min, invalidazione su write), Quartz.NET 3.22 (persistenza su Postgres,
tabelle `QRTZ_*` — **non create automaticamente**, va eseguito `Scripts/quartz_postgres.sql`).

## Verifica prima di dire "fatto"

Dalla cartella principale del repo (i test stanno in `GestoraWebApi.Tests/`, accanto a questa
cartella: lanciato da qui dentro, `dotnet test` non trova nessun test).

```
dotnet build Gestora.sln      # deve completare senza errori
dotnet test Gestora.sln       # confronta "Passed" col numero atteso (foglio BE · Test di TrackGestora.xlsx, solo locale)
```
Se hai toccato un endpoint, verifica anche a mano su Swagger (`/swagger`) prima di considerarlo
concluso, non solo via test unitari.

## Architettura

Pattern: Controller → Service → Repository (layered).

- `Controllers/` — 9 controller, un endpoint REST per azione
- `Services/{Area}/` — logica di dominio + `DTOs/` per area
- `Repositories/{Area}/` — accesso dati via EF Core
- `Infrastructure/Middleware/ExceptionMiddleware.cs` — mapping eccezione → status code
  centralizzato: `NotFoundException`/`KeyNotFoundException` → 404, `ValidationException`/
  `ArgumentException` → 400 (con `errors[]` per campo), `ForbiddenException` → 403,
  `ConflictException` → 409 (**unica** eccezione che genera 409: le regole di dominio che
  rifiutano un'operazione sollevano questa), resto → 500.
  **Non lanciare risposte HTTP dai service** — solo eccezioni tipizzate.
  ⚠️ `InvalidOperationException` **non è mappata apposta**: se arriva al middleware è un bug
  interno vero, deve restare un 500 (non "correggerla" aggiungendola alla mappa).
- `Migrations/` — EF Core. **Applicate solo da Fabio** (decisione 2 di radice): Claude prepara
  la migration con `dotnet ef migrations add`, non esegue mai `dotnet ef database update`.

## Endpoint — trappole, non un catalogo

Per l'elenco aggiornato usa il comando `/verifica-endpoint` o `graphify query`, non affidarti a
liste scritte a mano: invecchiano. Quello che il codice non dice da solo:

- La rotta base è sempre `api/[nome della classe controller]` — es. `AuthenticationUserController`
  → `/api/AuthenticationUser/...` (non `/api/Auth/...`), `FasceOrarieController` →
  `/api/FasceOrarie/...` (non `/api/FasciaOraria/...`).
- **`GET /health`** sta fuori dai controller (registrato in `Program.cs`), pubblico.
  Azure App Service lo chiama di continuo per il proprio controllo di salute; il log di richiesta
  lo tiene a livello `Verbose` apposta, per non intasare i log con una riga ogni pochi
  secondi.
- **`check-disponibilita`** e **`limiti-prenotazione`** (Prenotazione) e **`Setup`** (`GET stato`,
  `POST admin`) sono pubblici, senza auth — necessario per la pagina pubblica e il primo avvio.
- **Limiti di richieste** (429): `login` e `Setup/admin` 5 al minuto per indirizzo (`LoginPolicy`,
  in `Program.cs`); `register` e `check-disponibilita` con le regole di
  `Infrastructure/RateLimiting/LimitiRichieste` (`V2-011`), numeri nella sezione `LimitiRichieste`
  della configurazione (su Azure `LimitiRichieste__...`). La registrazione non limita l'Admin (crea
  lì gli account dello Staff); la disponibilità conta per **utente** chi ha fatto l'accesso, per
  indirizzo gli altri (lo Staff è tutto sullo stesso wifi). Non partizionare per indirizzo gli
  utenti autenticati. Il 429 ha il corpo `ErrorDetails` e, dove il limitatore lo sa, `Retry-After`.
- **Conferma dell'email** (`V2-003`): `register` pubblico crea l'utente non confermato e manda il link
  (`Services/Email/ConfermaEmailService`, invio dietro `IInvioEmail`: SMTP con MailKit se
  `Email:Host` è valorizzato, altrimenti solo in Development il contenuto va nei log). Se a
  chiamare `register` è un **Admin** l'account nasce già confermato e non parte nessuna email;
  anche il primo Admin di `Setup/admin`. Il token di Identity viaggia in Base64 adatto agli URL
  (`conferma-email` lo decodifica). `login` con password giusta ma email non confermata risponde
  **403** con `{ codice: "EMAIL_NON_CONFERMATA", messaggio }` (`Common/CodiciErrore`), dopo il
  controllo della password: chi non la conosce non scopre se l'indirizzo esiste. `conferma-email` e
  `reinvia-conferma` sono pubblici, con la regola `LimitiRichieste.ConfermaEmail`; il reinvio
  risponde **sempre allo stesso modo**. Configurazione: sezione `Email` (vedi `RUNBOOK.md` §5);
  fuori da Development l'app non parte senza `Email:Host` e `Email:UrlFrontend`. Se l'invio
  fallisce alla registrazione l'utente resta e la risposta ha `emailInviata: false`.
- **Modulo dei pareri** (`V2-022`): `POST /api/Pareri/invia`, pubblico, con la regola `LimitiRichieste.Pareri`
  (`PartizionePareri`: **2 pareri in 30 giorni per indirizzo**, `LimitiRichieste:PareriPerFinestra` e
  `PareriFinestraGiorni`; contatore in memoria, un riavvio lo azzera). Messaggio obbligatorio (max 2000), nome ed email
  facoltativi (`ParereDTOValidator`). `Services/Pareri/PareriService` manda il parere via `IInvioEmail` a
  `Pareri:Destinatario` (su Azure `Pareri__Destinatario`, **obbligatorio fuori da Development**, controllato
  all'avvio) e **non salva nulla nel database**. Il campo `SitoWeb` è una trappola: il modulo lo nasconde,
  se arriva compilato il parere si scarta con la stessa risposta di un invio riuscito. Nei log solo
  l'esito e l'IP, mai testo o email di chi scrive. Se l'invio fallisce l'errore arriva al chiamante
  (500): niente «grazie» per un parere perso.
- **Report CSV delle prenotazioni** (`V2-025`): `GET /api/Prenotazione/report-csv?da=…&a=…`, **solo Admin**
  (contiene i nomi dei clienti). Date comprese, al massimo 366 giorni, tutti gli stati; date mancanti o
  periodo sbagliato danno 400 sul campo. Lettura in `Services/Report/ReportPrenotazioniService`, formato in
  `CsvPrenotazioni` (puro, testato): separatore `;`, UTF-8 **con BOM** e righe CRLF, perché Excel in italiano
  lo apra giusto con un doppio clic; le celle che cominciano con `= + - @` prendono un apice davanti, così
  un nome o una nota non diventano una formula di Excel. Non togliere né il BOM né l'apice.
- **«Il mio account»** (`V2-019`): `AccountController` (`/api/Account/...`, qualsiasi utente autenticato),
  logica in `Services/Account/AccountService`. L'utente è **sempre quello del token**, mai un id
  passato da fuori. `GET mio-account` dà i dati; `POST cambia-password` vuole la password attuale (con
  lo stesso blocco del login, per email + indirizzo, errori sul campo `passwordAttuale`), cambia il security
  stamp e restituisce un **token nuovo** con lo stesso inizio di sessione: le altre sessioni si chiudono,
  questa continua. `POST cancella-account` (con la password, stesso blocco) elimina l'account: 409 se ci sono prenotazioni da servire (`GetDaServireDiUtenteAsync`; il messaggio parla al cliente: «prenotazioni in programma», non usa `DescrizioneImpegni`) o se è l'ultimo Admin. L'eliminazione vera sta in `Services/Account/EliminazioneAccountService`, comune alla pulizia degli inattivi: in una transazione stacca tutte le prenotazioni (`StaccaDaUtenteAsync`: `UserId` a null, le sue prendono «Cliente eliminato» e perdono le note, quelle prese per altri tengono il nome del cliente), scrive la traccia e cancella l'utente. Le righe del registro restano con un id che non corrisponde più a nessuno. L'export dei propri dati per l'utente è stato tolto il 26/10/2026 (troppo tecnico): le richieste arrivano per email.
- **Recupero password** (`V2-019`): `password-dimenticata` e `reimposta-password`, pubblici, con la regola
  `LimitiRichieste.RecuperoPassword` (numeri della conferma email, contatore proprio). Logica in
  `Services/Email/RecuperoPasswordService`, stesso schema della conferma (token in Base64 adatto agli URL,
  link a `/reimposta-password`). La richiesta risponde **sempre allo stesso modo**. Il token scade in
  **2 ore** (`Infrastructure/Auth/RecuperoPasswordTokenProvider`, registrato come
  `PasswordResetTokenProvider`; la conferma email resta a 24 ore). Il reset cambia il security stamp
  (le sessioni aperte si chiudono), conferma l'email e toglie il blocco per tentativi sbagliati (`TentativiAccesso.Azzera` per
  quell'indirizzo, più il vecchio blocco di Identity se ancora attivo). Le
  regole della password stanno in `Validators/RegolePassword` (`PasswordSicura()`), usate da
  registrazione, reset da Admin e recupero.
- **Presa visione dell'informativa privacy** (`V2-019`): `register` pubblico vuole `accettaPrivacy: true`
  (altrimenti 400 sul campo `accettaPrivacy`) e salva su `Utenti` data e versione accettata. Il controllo
  sta nel controller e non nel validator perché dipende da chi chiama: se registra un **Admin** non
  serve, l'utente la vedrà al primo accesso. `GET stato-privacy` e `POST accetta-privacy` (utente
  autenticato) dicono se va (ri)vista e la registrano; la regola è in `Services/Privacy/PresaVisionePrivacy`.
  Versione in vigore: `Privacy:Versione` (su Azure `Privacy__Versione`), controllata all'avvio; va
  cambiata **insieme** a `INFORMATIVA_VERSIONE` del frontend (`lib/privacy.ts`).
- **Portale in manutenzione** (`V2-016`): con `Manutenzione:Attiva = true` (su Azure
  `Manutenzione__Attiva`) `Infrastructure/Middleware/ManutenzioneMiddleware` risponde **503** a ogni
  `/api` con il corpo `ErrorDetails`; `/health` resta libero. Sta dopo `UseCors` (il browser deve
  leggere il 503) e prima di autenticazione e limiti. Spento di default in `appsettings.json`.
- **Blocco dopo 5 password sbagliate** (`V2-027`): `Infrastructure/Auth/TentativiAccesso` (singleton,
  in memoria, un riavvio lo azzera) conta gli errori per **email + indirizzo**, non per account: 5
  errori fermano quell'indirizzo su quell'email per 15 minuti (423), l'Admin vero da un altro
  indirizzo entra. Conta anche le email inesistenti, così il 423 non rivela chi è registrato. Lo usano
  `login` (controllo **prima** di cercare l'utente, poi `CheckPasswordAsync`) e «Il mio account». Il
  blocco di Identity è spento (`Lockout.AllowedForNewUsers = false`): **non riattivarlo** e non tornare
  a `SignInManager.CheckPasswordSignInAsync`, che rifiuterebbe un account con il vecchio blocco. Contro
  chi prova molte email dallo stesso indirizzo resta `LoginPolicy` (5 al minuto per indirizzo).
- **`JobsController`** (`POST trigger/{jobName}`, solo Admin) è **attivo anche in produzione**,
  nessun filtro per ambiente: forza un job Quartz già registrato senza aspettare il cron.
- **`update-prenotazione`/`annulla-prenotazione`**: aperte anche al Cliente, ma solo sulla propria
  prenotazione e solo fino a 2 ore prima dell'orario (`PrenotazioniService.GuardCutoffAsync`,
  costante `CutoffOreClienteSelfService`); oltre la soglia 409, deve contattare il locale. La
  prenotazione di un altro è **404 «non trovata»** (`NascondiSeAltrui`, `V2-029`), controllata subito
  dopo la lettura e prima dello stato: un 403 o un 409 rivelerebbero che quell'Id esiste e com'è messo.

## RBAC — perimetro ruoli (vedi `Auth/Roles.cs`)

- **Admin**: tutti i permessi, nessuna eccezione.
- **Staff**: lettura completa su Zone/Postazioni/FasceOrarie/Prenotazioni. Scrittura solo su
  Prenotazioni (crea, modifica, conferma, completa, annulla) — **non** elimina, **non** scrive su
  Zone/Postazioni/FasceOrarie (resta solo Admin).
- **Cliente**: crea-prenotazione + lettura propria + lettura di supporto (zone/postazioni/fasce
  attive). Può modificare/annullare solo entro il cutoff di 2 ore (vedi sopra).

Un utente **può avere più ruoli contemporaneamente** (many-to-many `UserRoles`) — caso d'uso
legittimo, non un'anomalia. Il JWT serializza il claim `role` come stringa se l'utente ha un solo
ruolo, come array se ne ha più di uno — il frontend normalizza sempre a array, vedi
`gestora-frontend/CLAUDE.md`.

## Assegnazione tavoli

`Services/PostazioneAssignment/AssegnazioneTavoli.cs` — motore **puro e statico**, nessuna
dipendenza da repository o DbContext, testato direttamente (`AssegnazioneTavoliTests`, 22 test).
`PostazioneAssignmentService` legge solo i dati (tavoli attivi, tavoli occupati) e delega al
motore. **Non rimettere logica di scelta dentro il service**: rendeva l'algoritmo precedente non
testabile.

Regole (decisioni di prodotto, vedi `CLAUDE.md` di radice §4 — non riaprirle):
- capienza di un'unione = somma delle capienze, **+2 (`BonusTestate`) solo se composta
  esclusivamente da tavoli da 2 posti** e almeno 2 tavoli; ogni altra combinazione = somma semplice
- si uniscono **quanti tavoli servono** (nessun limite dal `V2-007`), sempre **della stessa zona**
- vince la combinazione con **meno posti sprecati**; a parità, quella con meno tavoli
- nessun vincolo sulle capienze ammesse (qualsiasi numero da 1 in su)

Le combinazioni si generano sulle **capienze distinte**, non sui singoli tavoli: il costo non
cresce col numero di tavoli in sala. Al posto del vecchio limite di 4 tavoli c'è un **criterio di
arresto**: un'unione che copre già il gruppo non viene allungata. È corretto perché le capienze si
generano in ordine crescente, quindi aggiungere un tavolo non abbassa mai la capienza (nemmeno col
bonus: a soli tavoli da 2 si aggiunge un 2 o più). **Non togliere il criterio**: senza, con molti
tavoli il numero di combinazioni esplode.

`DisponibilitaService` chiama lo stesso motore (`TrovaMigliorCombinazione`) usato
dall'assegnazione reale — non un algoritmo parallelo. I posti residui sono il **minore** fra il
tetto residuo della fascia (`MaxCoperti`, decisione 8) e il gruppo più grande che i tavoli liberi
accolgono (`AssegnazioneTavoli.GruppoMassimo`, `V2-024`): prima erano solo il tetto, e il resto di un
tavolo già assegnato sembrava prenotabile. Quando a limitare sono i tavoli il motivo è
`TavoliInsufficienti`, altrimenti `TettoEsaurito`/`PostiInsufficienti`. Esclude tavoli in zone
disattivate. Per ogni fascia scrive il
**motivo** due volte, negli stessi rami: `Messaggio` (testo per lo Staff, lo usa
`PrenotazioneModal`) e `Motivo` (`Enums/MotivoDisponibilita`: `Libera`, `Terminata`,
`TettoEsaurito`, `PostiInsufficienti`, `TavoliInsufficienti`, nel JSON come testo) da cui la pagina
pubblica sceglie la propria frase. Non far interpretare `Messaggio` al frontend.

## Coerenza fra fasce e sala (`Services/Sala/`)

Il tetto di una fascia **attiva** non può superare i posti della sala (decisione 8):
- `CapienzaSala.Posti` — **unico** punto in cui si contano i posti: tavoli attivi in zone attive,
  somma semplice **senza** bonus testate. Lo usa anche il riepilogo della pagina Postazioni.
- `CoerenzaSalaService.VerificaTettoFasciaAsync` — da `FasciaOrariaService` su creazione e modifica
  di una fascia attiva e sulla sua riattivazione. Sala vuota → «crea prima le zone e i tavoli».
- `CoerenzaSalaService.VerificaModificaSalaAsync` — da `PostazioneService` (modifica, eliminazione)
  e `ZonaService` (spegnimento): riceve la sala **come sarà dopo** la modifica e dà 409 nominando
  le fasce se i posti scendono sotto un tetto (fino a 3, poi «e altre N»). Va chiamato **prima** di
  toccare l'entità. Una modifica che non toglie posti passa sempre, anche su dati già incoerenti
  (è il modo di sistemarli).
- Il seed di sviluppo scrive senza passare dai service: per questo `VerificaCoerenzaAsync` rifà il
  controllo e si ferma se un tetto supera i posti.

## Stati di una prenotazione (`Enums/StatoPrenotazione.cs`)

```
Attiva ──(ConfermaPrenotazioneAsync)──> InCorso ──(job: fascia finita)──> Completata
  │                                        │
  │ (job: data/fascia passate,             │ non torna mai indietro
  │  mai confermata)                       │
  ▼                                        │
NonPresentata                              │
  │                                        │
  └── entrambe: stati chiusi, non eliminabili ┘
Annullata: raggiungibile da Attiva/InCorso (AnnullaPrenotazioneAsync), unica eliminabile da Admin
```

`Prenotazione.Stato` è salvato come **stringa** (`HasConversion<string>()` in `GestoraContext`):
aggiungere un valore all'enum, come `NonPresentata`, non tocca lo schema, nessuna
migration. Regole di transizione, tutte in `PrenotazioniService`:
- `AutomaticCompletPrenotazioniAsync` (job notturno) porta `InCorso` → `Completata` quando la
  fascia è finita, **e nella stessa esecuzione** porta `Attiva` → `NonPresentata` quando
  data/fascia sono passate e nessuno l'ha mai confermata (girano sempre insieme: il secondo passo
  non dipende da quante righe ha completato il primo).
- `ConfermaPrenotazioneAsync` rifiuta anche una `Attiva` con data/fascia già passate (409 «La
  prenotazione è già passata»), non solo uno stato diverso da `Attiva`: altrimenti si potrebbe
  confermare un turno di ieri sera nella finestra prima che il job notturno sia girato.
- `AnnullaPrenotazioneAsync`/`UpdateAsync` rifiutano `NonPresentata` come rifiutano `Completata`.
- `DeleteAsync` ammette **solo** `Annullata`: `Completata` e `NonPresentata` sono storico
  e non si eliminano a mano (le toglie solo il cleanup a 6 mesi).
- `AutomaticDeletePrenotazioniAsync` (cleanup 6 mesi) elimina anche le `NonPresentata`, non solo
  le `Completata`.
- Coperti e occupazione tavoli (`ValidatePrenotazioneAsync`, `PostazioneAssignmentService`,
  `DashboardService`, `CountNumeroCopertiFasciaOrariaAsync`): `NonPresentata` esclusa ovunque
  `Annullata` lo è già — non ha mai occupato la sala.
- Dashboard settimanale: il no-show conta sia le `NonPresentata` esplicite sia le `Attiva` su
  data passata (la finestra prima che il job sia girato), senza doppio conteggio (sono stati
  diversi per definizione).

## Orologio unico

`Common/IClock` (`SystemClock`, singleton): `UtcNow`, `NowInRome`, `TodayInRome`. Database e
logica interna in UTC; conversione a `Europe/Rome` solo al confine. **Non reintrodurre
`DateTime.Now`/`DateTime.Today` privati**: iniettare `IClock`. Nei test: `TestClock` (istante
fisso).

## Note tecniche da tenere a mente

- HTTPS: Azure App Service termina HTTPS a livello proxy → `UseHttpsRedirection` resta
  commentato in `Program.cs`, **non riattivarlo** in produzione.
- CORS: origin letti da `AllowedOrigins` in appsettings/env var, mai hardcoded.
- **Enum su DB: non tutti mappati allo stesso modo** (`Context/GestoraContext.cs`).
  `Prenotazione.Stato` è **stringa** (`'Completata'` in colonna); `FasciaOraria.GiornoSettimana`
  è **intero**. Attenzione scrivendo SQL a mano (query dirette, seed, fix su Neon): usare il
  valore giusto per la colonna giusta.
- **Transazioni**: `Common/IEsecutoreTransazione` avvolge scrittura + audit log in un'unica
  operazione atomica (usato da `ZonaService`, `PostazioneService`, `FasciaOrariaService` e, dal
  `V2-014`, da `update-user` e `delete-user` in `AuthenticationUserController`). Anche
  l'eliminazione di una prenotazione (`PrenotazioniService.DeleteAsync`) e la sua traccia stanno
  nella stessa transazione. `update-user` scrive nel registro i **nomi** dei campi cambiati
  («nome utente», «email»), mai i valori.
  `PrenotazioniService` ha il proprio `EseguiInTransazioneAsync` (traduce anche la violazione
  dell'unique index sullo slot in 409). **La cache si invalida dopo il commit**, mai dentro il
  blocco.
- **Indirizzo IP reale** — `Common/IndirizzoClient`. **Non usare `Connection.RemoteIpAddress`
  direttamente**: dietro il proxy è un indirizzo di rete interna, uguale per chiunque. Su Azure la
  catena ha un solo anello, già il client (`V2-008`): l'helper non ne scarta nessuno. Non
  `UseForwardedHeaders` (prende l'anello sbagliato, cioè il proxy). Per rimisurare la catena:
  `GET /api/LogActivity/diagnostica-inoltro` (Admin), con il middleware disattivato (altrimenti
  legge un header già consumato).
- **Utenze mai usate** (`V2-015`): `UtenzeInattiveCleanupJob` (ogni notte alle 3:00, dopo i due
  job delle prenotazioni) elimina fino a 500 utenze per volta che **non hanno ruoli diversi da
  Cliente**, non hanno mai fatto login (`UltimoAccesso` null), sono state create prima di
  `Utenze:GiorniUtenzaMaiUsata` (180) giorni e non hanno prenotazioni. La regola sta nella query
  di `UtentiRepository.GetUtenzeMaiUsateAsync`. Una riga nel registro per ogni eliminata.
  **Chi crea un utente valorizza `DataCreazione`, chi fa entrare un utente `UltimoAccesso`**
  (oggi: `register`, `Setup/admin`, `login`). Se `DataCreazione` manca, il default del database
  (`now()`) evita che l'utenza risulti vecchia di duemila anni ed eliminata subito.
- **Account inattivi** (`V2-019`): lo stesso `UtenzeInattiveCleanupJob` fa tre passi, ognuno con il suo
  `try` (un errore non salta gli altri): utenze mai usate, poi eliminazione degli inattivi, poi avvisi.
  Solo Clienti, mai Staff o Admin (`UtentiRepository.SoloClienti`). A chi non entra da
  `Utenze:MesiInattivita` (24) mesi meno `Utenze:SettimaneAvviso` (4) settimane parte una sola email
  con la data dell'eliminazione (mai prima di 4 settimane da oggi), e si segna
  `AvvisoInattivitaInviatoIl`; se l'email non parte non si segna e si riprova la notte dopo. Si elimina
  chi è fermo da 24 mesi **e** è stato avvisato da almeno 4 settimane, con `EliminazioneAccountService`;
  chi ha prenotazioni in programma resta. **Il login rimette `AvvisoInattivitaInviatoIl` a null**: chi
  rientra non è più inattivo. Numeri scritti anche nell'informativa privacy.
- **Registro attività a 24 mesi** (`V2-019`): `RegistroAttivitaCleanupJob` (ogni notte alle 3:30, dopo la
  pulizia delle utenze) elimina le righe di `LogActivities` più vecchie di
  `RegistroAttivita:MesiConservazione` mesi (24, controllato all'avvio), a blocchi di 1000
  (`RegistroAttivitaCleanupService`, `LogActivityRepository.EliminaPrimaDiAsync`). Il riepilogo va nel log
  dell'applicazione, non nel registro. Il periodo è scritto anche nell'informativa privacy: cambiandolo
  si cambia anche il testo.
- **Storico utenti**: FK `Prenotazioni → Utenti` è `Restrict`, non `Cascade`. Un utente con
  prenotazioni **non si elimina** (409): lo storico regge i conteggi.
- **Quartz e repliche**: scheduler **non** in cluster mode. Con una sola istanza va bene; prima di
  aggiungere repliche va abilitato `store.UseClustering()` (dettaglio commentato in `Program.cs`).
- **Liste vuote**: una collezione vuota è sempre `200 []`, mai 404 (il 404 resta per la singola
  entità non trovata).
- **Paginazione**: `Page`/`PageSize` fuori range vengono riportati dentro i limiti, non generano
  errore. L'ordinamento delle liste paginate deve sempre essere **totale**
  (`.OrderBy(...).ThenBy(x => x.Id)`), altrimenti pagine duplicate/perse.
- **Tavoli e prenotazioni future** (`V2-009`): contano solo le prenotazioni **da servire**
  (Attiva, InCorso) da oggi in avanti (`GetImpegniFuturiAsync`), e bloccano solo la modifica che
  le danneggia. Numero e aumento dei posti passano sempre; disattivare o spostare di zona no, se
  il tavolo è prenotato; ridurre i posti sì, se ogni prenotazione ci sta ancora (posti assegnati
  al tavolo e capienza dell'unione, testate comprese). Non reintrodurre un blocco totale: un
  tavolo di un locale aperto ha quasi sempre una prenotazione futura. **Stesso criterio per le fasce**: tetto e stato si cambiano
  sempre, giorno e orari solo senza prenotazioni future Attive/InCorso. **Stesso criterio per le
  zone** (`V2-012`): spegnerne una (`UpdateAsync` o `UpdateStatoZonaAsync`) è un 409 se qualche
  prenotazione da servire ha un tavolo in quella zona (`ZonaRepository.GetImpegniFuturiAsync`);
  riattivarla o rinominarla passa sempre. Il testo della prenotazione che blocca è in
  `Common/DescrizioneImpegni`, condiviso con i tavoli.
- **Fasce dentro un solo giorno**: il dominio calcola la fine come data + `OrarioFine`,
  quindi il validator esige fine > inizio (niente «00:00» come fine, al massimo 23:59).
- **Almeno un Admin**: un Admin non può togliersi il ruolo Admin (e già non può
  eliminarsi). Senza Admin la schermata di primo avvio, anonima, si riaprirebbe.
- **Lock sulla prenotazione**: modifica, annullamento, conferma e completamento
  leggono la prenotazione con `LeggiConLockAsync` (`FOR UPDATE` + lettura) **dentro**
  `EseguiInTransazioneAsync`. Non riportare lettura e controlli di stato fuori dalla transazione.
  `EseguiInTransazioneAsync` svuota il change tracker a ogni nuovo tentativo automatico.
- **Annullamento**: rifiutato se la fascia è già finita, anche per Staff/Admin; la
  conferma si fa solo nel giorno della prenotazione.
- **Token e security stamp**: il JWT porta il claim `stamp`, confrontato a ogni
  richiesta in `OnTokenValidated`. Chi cambia ruoli o email di un utente deve chiamare
  `UpdateSecurityStampAsync` (già fatto in assign/remove-role e update-user).
- **Rinnovo del token** (`V2-010`): `POST rinnova-token` (qualsiasi utente autenticato) dà un token
  nuovo con ruoli e stamp riletti adesso. Il claim `inizio_sessione` (istante del login, secondi
  Unix) passa invariato da un rinnovo all'altro e la scadenza non va mai oltre
  `JwtSettings:MaxSessionHours` (12) da lì: oltre, 401 e si rifà il login. La regola sta in
  `JwtTokenGenerator` (con `IClock`, test in `JwtTokenGeneratorTests`), non nel controller. Il
  claim ha un nome proprio e non `auth_time`, che il middleware JWT rinominerebbe. Il rinnovo non
  scrive nel registro attività: sarebbe una riga ogni ora per ogni utente collegato.
- **Nome utente**: ammessi spazi, apostrofi e lettere accentate (`AllowedUserNameCharacters`), non
  in testa/coda né doppi (`RegoleUsername`). Errori di Identity in italiano
  (`IdentityErrorDescriberItaliano`) e sempre restituiti con `ErroriIdentity.ComeValidationException`,
  mai `BadRequest(result.Errors)`: il frontend non saprebbe leggerli.
- **Due elenchi dei tavoli di una zona**: `get-postazioni-per-zona` dà solo i tavoli attivi di una
  zona attiva; `get-tavoli-zona-gestione` (Admin/Staff, `V2-007`) li dà tutti, anche su zona spenta,
  ed è quello della pagina Tavoli. Non far tornare la pagina sul primo: un tavolo disattivato
  sparirebbe e non si potrebbe più riattivare.
- **Elenco prenotazioni senza filtri** (`get-all-prenotazioni`): prima oggi e i giorni a venire,
  poi il passato dal più recente, infine `Id` (`V2-007`). Non tornare all'ordine di data puro: la
  prima pagina sarebbe lo storico più vecchio.
- **Elenco tavoli attivi**: non espone `PrenotazioneId` (starebbe in cache, quindi vecchio, e
  sarebbe visibile al Cliente). Non reintrodurlo.
- **Email unica**: `RequireUniqueEmail = true` in `AuthenticationExtensions`. Il login
  usa `FindByEmailAsync`, che con due account sulla stessa email solleva un'eccezione.
- **Cache**: chiavi in `Common/CacheKeys.cs` — invalidare **tutte** le chiavi derivate (es.
  `FascePerGiorno+giorno`), non solo quella base.
- **Avvio**: `Program.cs` valida la configurazione **prima** di registrare i servizi (fail-fast):
  se manca `ConnectionStrings:DefaultConnection`/`JwtSettings:Secret`, o il segreto è più corto di
  32 caratteri, l'app si ferma con un messaggio esplicito. Stessa cosa per la durata del token e
  della sessione (`ValidateOnStart`: entrambe > 0, token non più lungo della sessione). **Non rimuovere quei controlli.**
- **Tetto dei coperti per prenotazione**: non è nel codice ma in configurazione
  (`Common/PrenotazioniSettings`), con **due valori** (`V2-007`):
  - `Prenotazioni:MaxCopertiPerPrenotazione` — limite **tecnico**, oggi 50, per tutti i ruoli. Lo
    applicano i due validatori (`PrenotazioneCreateDTOValidator`, `CheckDisponibilitaDTOValidator`).
  - `Prenotazioni:MaxCopertiPrenotazioneOnline` — limite **online**, oggi 20, solo per il Cliente
    in self-service (`PrenotazioniService.GuardLimiteOnline`, in creazione e modifica: 409 «contatta
    direttamente il ristorante»). Lo Staff al telefono non lo subisce.

  Su Azure: `Prenotazioni__MaxCopertiPerPrenotazione` e `Prenotazioni__MaxCopertiPrenotazioneOnline`.
  Controllati all'avvio (`ValidateOnStart`): entrambi > 0 e online ≤ tecnico, altrimenti l'app non
  parte. `GET limiti-prenotazione` li passa entrambi al frontend. Non riscrivere i numeri a mano
  da nessuna parte.
- **Connessione DB**: `EnableRetryOnFailure` attivo (5 tentativi/10s). Le transazioni esplicite
  vanno dentro `CreateExecutionStrategy().ExecuteAsync(...)` — usare l'helper
  `EseguiInTransazioneAsync`, non aprire transazioni a mano.
- **Concorrenza sul tavolo**: unique index **pieno** `UX_PrenotazionePostazione_Slot` su
  `(PostazioneId, DataPrenotazione, FasciaOrariaId)`. Chi scrive una riga join deve valorizzare
  entrambi i campi (passare da `CreaRigaPostazione`); annullare una prenotazione cancella le sue
  righe join.
- **Concorrenza sul tetto dei coperti**: `ValidatePrenotazioneAsync` legge la fascia
  con `GetByIdConLockAsync` (`SELECT ... FOR UPDATE`): chi prenota sulla stessa fascia passa uno
  alla volta, quindi la `SUM` dei coperti non può essere letta «vecchia» da due richieste insieme.
  Il metodo va chiamato **solo dentro** `EseguiInTransazioneAsync` (il lock vive fino al commit),
  ed è per questo che in `UpdateAsync` la validazione sta dentro la transazione. Non tornare a
  `GetByIdAsync` lì dentro: un test lo verifica (`AddAsync_LeggeLaFasciaConIlLock_NonSenza`). Il
  lock vero non si prova con InMemory: procedura manuale in `RUNBOOK.md` §2. La dashboard espone
  `CopertiOltreIlTetto` per non nascondere un eventuale sforamento.
- **Errori del database**: `DbExceptionTranslator` riconosce il codice Postgres `23505`
  (violazione unicità). Il provider InMemory **non** applica gli unique index: nei test la
  violazione va simulata a mano.
- **Log**: in produzione **solo console** (filesystem del container effimero). Il sink su file
  resta in `appsettings.Development.json` (versionato, senza segreti) — la configurazione .NET
  sovrascrive gli array **per posizione**, quindi il sink Console va riconfermato all'indice 0.
- **Segreti**: connection string e JWT Secret di sviluppo in **User Secrets**
  (`dotnet user-secrets`), non in `appsettings.Development.json` (solo placeholder vuoti).
  Percorso e comandi in `RUNBOOK.md`. In produzione: solo variabili d'ambiente di Azure App
  Service (doppio underscore, vedi `RUNBOOK.md` §6).
- **Chiavi di Data Protection** (`V2-018`): firmano i token di Identity (il link di conferma
  email, poi il recupero password). Stanno nel database, tabella `DataProtectionKeys` (migration 9),
  registrate da `Extensions/DataProtectionExtensions.AddChiaviDataProtection` con nome
  applicazione fisso «Gestora». **Non cambiare quel nome**: le chiavi salvate non verrebbero più
  riconosciute e tutti i link già inviati smetterebbero di funzionare. Sono in chiaro nel
  database (scelta voluta): l'avviso «No XML encryptor configured» all'avvio è atteso.
- **Header di sicurezza** (`V2-018`): `Infrastructure/Middleware/SicurezzaHeaderMiddleware` li mette
  su ogni risposta (`nosniff`, `X-Frame-Options: DENY`, `Content-Security-Policy: default-src
  'none'; frame-ancestors 'none'`, `Referrer-Policy: no-referrer`; `Strict-Transport-Security`
  solo fuori da Development). Sta **dopo** Swagger, che con quella CSP non si caricherebbe, e
  prima di tutto il resto. HSTS è scritto a mano: **non sostituirlo con `UseHsts`**, che dietro il
  proxy di Azure vede HTTP e non lo aggiungerebbe mai. L'header `Server` è spento in `Program.cs`
  (`AddServerHeader = false`).

## Test

`GestoraWebApi.Tests/Services/` — xUnit + Moq, Arrange/Act/Assert. Un file per service, più il
motore puro, i job, i validator, il mapping, il repository, il modello, la configurazione di Data Protection, i middleware, la presa visione della privacy, il recupero password, «Il mio account», la pulizia del registro, gli account inattivi, il report CSV, i pareri. **492 test totali.**

- `PrenotazioniServiceTests` configura l'InMemory con
  `ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))` — senza questa
  riga tutti i test della classe falliscono (l'InMemory non supporta transazioni).
- Orologio nei test: `TestClock` (istante fisso).
- Nessun test su `AuthenticationUserController`: la logica di auth non è ancora estratta in un
  service dedicato (idea in `BACKLOG.md`, sezione *Idee → Codice*). Fa eccezione la conferma
  dell'email (`V2-003`), che sta in `ConfermaEmailService` ed è coperta da
  `ConfermaEmailServiceTests` (`UserManager` finto con Moq); il blocco del login per email non
  confermata resta nel controller, quindi si prova a vista.
- Per mockare `IQueryable<T>` dai repository: pacchetto `MockQueryable.Moq` (10.x, dal `V2-017`) —
  pattern `lista.BuildMockDbSet().Object`, direttamente sulla lista: dalla 10 `BuildMock` e
  `BuildMockDbSet` non accettano più un `IQueryable`, quindi niente `.AsQueryable()` prima.
