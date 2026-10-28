# Gestora — piano di chiusura v1.1 (prompt per Claude Code, modello Fable)

> **Come si usa.** Aprire Claude Code dalla radice del progetto (`Gestora`),
> impostare `/model fable`, incollare **tutto il testo da «PROMPT» in giù**. Il piano è stato scritto
> il 18/09/2026 dopo lettura completa di CLAUDE.md (radice, backend, frontend), BACKLOG.md, RUNBOOK.md,
> STORICO_FASI.md, GestoraDocs/verifica-redesign.md, AppuntiFix.txt e del codice coinvolto.

---

# PROMPT

## Chi sei e cosa devi fare

Sei lo sviluppatore che porta **Gestora** (gestionale prenotazioni per ristoranti: ASP.NET Core 9 +
EF Core 9 + PostgreSQL, frontend React 19 + TypeScript + Vite + shadcn/ui + Tailwind v4) dalla
versione `v1.0.6` alla **`v1.1.0`, la versione di chiusura**. Lavori in **totale autonomia**: non
fai domande, prendi le decisioni ragionevoli, le documenti, vai avanti. Fabio (il proprietario) farà
un giro di test sul portale alla fine e rilascerà lui su `main`.

Prima di toccare qualsiasi cosa leggi, nell'ordine: `CLAUDE.md` (radice), `GestoraWebApi/CLAUDE.md`,
`gestora-frontend/CLAUDE.md`, `BACKLOG.md`, `RUNBOOK.md`, `docs/archivio/STORICO_FASI.md` (sezione
«Le regole di metodo»). Sono la memoria del progetto: contengono trappole già pagate a caro prezzo.

## Regole di ingaggio — non negoziabili

1. **Branch: `dev`.** Verifica con `git branch --show-current` a inizio di **ogni fase**, non solo
   all'inizio. Se non sei su `dev`, fermati e passa a `dev`.
2. **Mai `main`. Mai `git push`.** Nessun merge, nessun tag, nessun push, di nessun tipo, per nessun
   motivo. `main` lo tocca solo Fabio.
3. **Commit locali su `dev`, uno per fase**, con messaggio `feat: <descrizione>` (o `fix:`/`docs:`/
   `chore:`), che elenca le modifiche. Prima di ogni commit: `git status --untracked-files=all` e
   conta i file (in Fase 7 del passato 12 file nuovi sono andati persi perché non spuntati). Firma il
   commit come indicato dal sistema. Questa deroga alla regola «commit li fa Fabio» è stata data da
   Fabio il 18/09/2026 per questo lavoro: vale solo per `dev`, solo in locale.
4. **Produzione (Azure, Neon, Vercel, Docker Hub, GitHub) non si tocca.** Prepari istruzioni, le
   esegue Fabio. Stessa cosa per credenziali, `.env`, variabili d'ambiente, rimozione di file dal
   tracciamento Git: prepari, spieghi, non esegui.
5. **Database locale**: puoi usarlo liberamente (`dotnet ef database update`, `--seed-sviluppo`,
   `dotnet ef database drop` **senza** `--force`). Nessuna nuova migration è prevista da questo piano:
   se ne servisse una, la prepari con `dotnet ef migrations add` e la applichi **solo in locale**;
   per Neon generi lo script (`dotnet ef migrations script --idempotent`, **togli il BOM**) e lo
   consegni.
6. **Stop ai loop**: se un test o un tentativo fallisce più di 3 volte di fila, fermati, scrivi il
   problema in `GestoraDocs/CONSEGNA_v1.1.md` (sezione «Bloccato») e passa alla fase successiva. Non
   ritentare alla cieca.
7. **Verifica prima di dire «fatto»** (regola di metodo 3 e 5 dello storico): ogni fase si chiude
   solo con `dotnet test` verde nel backend e `npm run lint && npm test && npm run build` verdi nel
   frontend. Quando aggiungi un test, fai la **controprova**: rompi il codice di proposito e verifica
   che fallisca proprio quel test, poi ripristina.
8. **PowerShell: comandi su una riga sola.** Niente blocchi multi-riga nel terminale. Script lunghi
   in file `.ps1` salvati UTF-8 con BOM.
9. **Niente colori fissi nel frontend** (`bg-white`, `text-red-500`…): solo token del tema (tabella in
   `gestora-frontend/CLAUDE.md`). Dopo ogni modifica a `index.css`: `node scripts/contrasto.mjs`.
10. **Le 10 decisioni di prodotto** (`CLAUDE.md` §4) non si riaprono. Le voci «Deciso di NON fare» del
    `BACKLOG.md` restano tali.
11. **Clean code, niente sovra-ingegneria**: nessuna libreria nuova salvo necessità motivata, nessuna
    astrazione per un uso solo, commenti solo dove spiegano un *perché* non ovvio. Lingua del codice
    e dei commenti: quella già usata nel file che tocchi (italiano nei nomi di dominio).
12. **Diario di consegna**: crea `GestoraDocs/CONSEGNA_v1.1.md` all'inizio e aggiornalo alla fine di
    ogni fase (cosa fatto, decisioni prese, cosa verificare a mano, cosa resta a Fabio). È il
    documento che Fabio leggerà per primo.
13. **Dopo ogni fase**: `graphify update .` se disponibile (non è bloccante se manca).

## Prerequisiti ambiente (Fase 0a, 10 minuti)

Verifica e, se manca qualcosa, sistemalo o annotalo in CONSEGNA:

- PostgreSQL locale attivo; `dotnet user-secrets list` in `GestoraWebApi/` mostra
  `ConnectionStrings:DefaultConnection` e `JwtSettings:Secret` (se mancano, Fabio deve ricrearli:
  istruzioni in `RUNBOOK.md` §7 — scrivi in CONSEGNA e prosegui con le fasi che non richiedono il
  database).
- `gestora-frontend/.env.local` contiene `VITE_API_URL=http://localhost:5099/api`.
- `dotnet --version` (9.x), `node --version` (20+), `npm ci` in `gestora-frontend/`.
- Linea di base: `dotnet test` → **239** verdi; `npm test` → **35** verdi; `npm run build` e
  `npm run lint` puliti. Se la base non è verde, sistema prima quello.
- Database locale allineato: `dotnet ef database update` in `GestoraWebApi/`, poi
  `dotnet run -- --seed-sviluppo` (utenti `admin|staff|cliente@gestora.local` / `Sviluppo1!`).
- Strumenti browser: hai a disposizione i tool Playwright MCP (`mcp__playwright__browser_*`). Li
  userai in Fase 6 (studio del riferimento e screenshot) e in Fase 8 (verifica visiva). Se non
  rispondono, le due fasi si fanno con `npm run build` + ragionamento sul codice e lo annoti.

---

## Fase 0b — SICUREZZA: password del database Neon nel repository (prima di tutto)

**Problema.** `envDBNeon.txt` nella radice contiene `PGUSER` e `PGPASSWORD` del database di
produzione Neon ed **è tracciato da Git** (commit `8b86523`, già su GitHub su `dev` e `main`; il
repository è pubblico). È una credenziale di produzione esposta.

**Cosa fai tu:**
1. Aggiungi a `.gitignore` di radice, sotto il blocco «backup di database»:
   `envDBNeon.txt` e `env*.txt` con un commento di una riga.
2. **Non** eseguire `git rm --cached`: lo fa Fabio (regola 4). Scrivi in CONSEGNA, sezione
   «DA FARE SUBITO — Fabio», questa sequenza esatta:
   - su Neon: Dashboard → progetto → *Roles* → `neondb_owner` → **Reset password**, copiare la
     nuova connection string;
   - su Azure: App Service `gestora-api` → *Environment variables* → aggiornare
     `ConnectionStrings__DefaultConnection` (doppio underscore) con la nuova stringa → *Apply* →
     riavvio → `curl https://gestora-api-emdvdqegg7g8gmaq.canadacentral-01.azurewebsites.net/health`
     deve rispondere `Healthy`;
   - in locale: `git rm --cached envDBNeon.txt` poi spostare il file fuori dal repository (o
     cancellarlo), commit `chore: rimuove credenziali dal tracciamento`;
   - opzionale: riscrittura della storia con `git filter-repo` (già fatta il 02-03/09, procedura
     nello storico). **Con la password ruotata non è indispensabile**: la vecchia non apre più
     niente. Dirlo chiaramente.
3. Controlla che non ci siano altri segreti tracciati: `git ls-files | Select-String -Pattern
   "env|secret|password|\.pfx|\.key"` e `git grep -i "PGPASSWORD\|Password=" -- ':!*.md'`. Annota
   l'esito.
4. Nella radice c'è anche `backup_LogActivities_20260904.csv`: non è tracciato (gitignore), ma
   segnala a Fabio di cancellarlo.

Commit: `chore: esclude file di credenziali dal tracciamento` (solo `.gitignore` + CONSEGNA).

---

## Fase 1 — `CAP-001`: il tetto dei coperti diventa un vincolo vero

**Il difetto.** `PrenotazioniService.ValidatePrenotazioneAsync` (`Services/Prenotazioni/PrenotazioniService.cs`
~r.504-555) fa `SUM(NumeroCoperti)` sulle prenotazioni non annullate della fascia+data e confronta con
`MaxCoperti`. PostgreSQL è in READ COMMITTED e `EseguiInTransazioneAsync` (~r.474) apre la transazione
senza lock: due richieste simultanee leggono lo stesso totale, passano entrambe, sforano. In
`UpdateAsync` (~r.136-203) la validazione sta **fuori** dalla transazione (r.161, la transazione parte
a r.166). Il tavolo è protetto dall'unique index `UX_PrenotazionePostazione_Slot`; i coperti da niente.

**Scelta tecnica: lock pessimistico sulla riga della fascia** (`SELECT … FOR UPDATE`), come indicato
in `BACKLOG.md`. Serializza le prenotazioni sulla stessa fascia (tutte le date: granularità
accettabile per un locale), è semplice, non richiede migration e sfrutta la transazione che c'è già.

**Passi:**
1. `Repositories/FasciaOrarie/IFasciaOrariaRepository.cs` + `FasciaOrariaRepository.cs`: nuovo
   metodo `Task<FasciaOraria?> GetByIdConLockAsync(long id)` che esegue
   `_dbSet.FromSqlInterpolated($"SELECT * FROM \"FasceOrarie\" WHERE \"Id\" = {id} FOR UPDATE").FirstOrDefaultAsync()`
   (tabella `FasceOrarie`, vedi `Context/GestoraContext.cs` r.61). Commento di due righe sul *perché*
   (il lock vive fino al commit della transazione chiamante; fuori da una transazione è inutile).
   Togli anche l'`using static System.Runtime.InteropServices.JavaScript.JSType;` spurio a r.6.
2. `ValidatePrenotazioneAsync`: usa `GetByIdConLockAsync` al posto di `GetByIdAsync`. Il metodo va
   chiamato **solo dentro** `EseguiInTransazioneAsync`: aggiungi una riga di commento sul contratto.
3. `UpdateAsync`: sposta `ValidatePrenotazioneAsync` e `GuardUnaPrenotazioneAlGiornoAsync` **dentro**
   la lambda di `EseguiInTransazioneAsync`, prima di `AssegnaPostazioneDisponibileAsync`. I controlli
   di stato/permessi/cutoff che non toccano il tetto possono restare fuori.
4. `Services/Dashboard/Dashboardservice.cs` ~r.101: **non nascondere lo sforamento**. Aggiungi a
   `CopertiFasciaDTO` (`Services/Dashboard/DTOs/DashboardDTO.cs`) il campo
   `int CopertiOltreIlTetto` = `Math.Max(0, copertiPrenotati - f.MaxCoperti)`; `CopertiDisponibili`
   resta a 0 quando si sfora (il frontend lo usa per «pieno»). Fai lo stesso in
   `FasciaOrariaService.VerificaDisponibilitaPerFasciaAsync` (~r.127) solo se il DTO di quella
   risposta arriva al frontend: se non arriva, lascia.
5. Frontend `gestora-frontend/src/types/dashboard.ts` + `pages/DashboardPage.tsx` (`RigaFascia`):
   se `copertiOltreIlTetto > 0` mostra accanto al contatore `· N oltre il tetto` in `text-destructive`
   (stesso linguaggio già usato nella banda del giorno). Verifica con
   `dotnet run -- --seed-sviluppo --stato-incoerente` e poi rifai il seed normale.
6. **Test** (`GestoraWebApi.Tests/Services/PrenotazioniServiceTests.cs`): gli `Arrange…` mockano
   `_fasciaRepoMock.GetByIdAsync(1)`; aggiungi/aggiorna il setup per `GetByIdConLockAsync` e un test
   che verifica con `Verify(…, Times.Once)` che `AddAsync` e `UpdateAsync` usino il metodo con lock e
   **non** quello senza. In `DashboardServiceTests.cs` un test «fascia oltre il tetto» (`Fascia(1, 10)`
   + 15 coperti → `CopertiOltreIlTetto == 5`, `CopertiDisponibili == 0`). Il lock vero non si prova
   con InMemory (stessa situazione dell'unique index, documentata): scrivi in `RUNBOOK.md` §2 la prova
   manuale — due browser, stessa fascia, coperti residui 4, due prenotazioni da 4 inviate insieme:
   una passa, l'altra riceve 409.
7. Aggiorna `GestoraWebApi/CLAUDE.md` (sezione «Note tecniche», voce Concorrenza) e chiudi `CAP-001`
   in `BACKLOG.md`.

Commit: `feat: tetto coperti protetto da lock sulla fascia (CAP-001)`.

---

## Fase 2 — Difetti piccoli e certi del frontend (dagli appunti d'uso)

Tutti file singoli, nessuna decisione aperta. Ordine libero.

1. **Segnaposto pubblicato** — `src/pages/RegisterPage.tsx` ~r.56: `CardDescription` dice
   `SOTTOCrea il tuo account`. Sostituisci con una frase vera («Bastano email e password: le
   prenotazioni le gestisci da qui.») con `className="text-corpo text-muted-foreground"` come in
   `LoginPage.tsx`. Cerca `SOTTO` in tutto `src/` per altri residui.
2. **Loop dell'utente senza ruolo** — un utente registrato a cui l'Admin ha tolto tutti i ruoli entra
   in un ciclo `/unauthorized` → «Torna alle tue pagine» → `/prenotazioni` → `/unauthorized`. Cause:
   `LoginPage.tsx` ~r.40 e `router/RedirectSeAutenticato.tsx` ~r.13 con `roles = []` mandano a
   `/dashboard`; `pages/UnauthorizedPage.tsx` ~r.16 con `roles = []` manda a `/prenotazioni`.
   Fix: in `UnauthorizedPage`, se `user.roles.length === 0` cambia titolo («Il tuo account non ha
   ancora un ruolo»), testo («Chiedi a un amministratore di assegnartene uno, poi accedi di nuovo.»)
   e **unica azione «Esci»** che fa `logout()` + `navigate('/login')`. In `LoginPage` e
   `RedirectSeAutenticato`, con zero ruoli vai direttamente a `/unauthorized`. Estrai la scelta della
   «pagina di casa» in una funzione `paginaDiCasa(roles)` in `src/lib/` usata dai tre punti (oggi la
   logica è duplicata). Test: aggiungi in `router/__tests__/ProtectedRoute.test.tsx` (o nuovo file)
   il caso «utente con `roles: []` non entra in loop».
3. **Staff e «Configura le fasce orarie»** — `pages/DashboardPage.tsx` ~r.175-188: il pulsante
   compare quando non ci sono fasce per oggi, a prescindere dal ruolo, ma lo Staff non può crearle.
   Usa `useAuth()` e mostra il pulsante solo ad Admin; allo Staff resta il testo, riformulato
   («Chiedi a un amministratore di configurarle.»).
4. **La vetrina non si trova** — da `/login` e `/register` si torna a `/` solo cliccando il logo
   (`aria-label` c'è, ma nessuno lo sa). Aggiungi sotto la card, in entrambe le pagine, un link
   testuale `← Torna alla pagina del locale` (`text-nota text-muted-foreground`, `Link to="/"`).
5. **Tendina del form prenotazione poco leggibile** — `components/PrenotazioneModal.tsx` usa
   `components/ui/native-select.tsx` (`<select>` nativo). Il valore scelto è `bg-transparent`
   `dark:bg-input/30` e le `<option>` le disegna il sistema operativo: in tema scuro il menu può
   uscire chiaro con testo chiaro. Fix in tre punti: (a) in `index.css` aggiungi `color-scheme: light`
   su `:root` e `color-scheme: dark` su `.dark` (i controlli nativi seguono il tema); (b) in
   `native-select.tsx` aggiungi `text-foreground` e `[&>option]:bg-popover [&>option]:text-popover-foreground`;
   (c) l'orario nella tendina della fascia (`PrenotazioneModal.tsx` ~r.206) stampa
   `{f.orarioInizio} - {f.orarioFine}` con i secondi: allinealo al resto dell'app
   (`slice(0,5)` e trattino `–`). Verifica a schermo in Fase 8 (X4), in entrambi i temi.
6. **Ordine e nomi del menu** — `layouts/AppLayout.tsx` ~r.69-92. Ordine nuovo, per flusso di lavoro:
   **Dashboard · Prenotazioni** (uso quotidiano) — separatore con etichetta «Sala» (`text-nota
   uppercase text-muted-foreground`) — **Zone · Tavoli · Fasce orarie** — separatore «Amministrazione»
   — **Utenti** (solo Admin). Rinomina la voce «Postazioni» in **«Tavoli»** (la pagina si intitola
   già «Tavoli»; la rotta `/postazioni` resta) e «Admin Utenti» in «Utenti». Metti tutte le voci in
   un unico array con `ruoli` e `gruppo`, così il filtro per ruolo è in un posto solo. Il Cliente vede
   solo Prenotazioni, senza separatori.
7. **Pagina 404** — `router/index.tsx` non ha una rotta `*`: aggiungi `path: '*'` con
   `SchermataMessaggio` («Questa pagina non esiste», pulsante verso la pagina di casa o `/`).

Commit: `fix: segnaposto register, loop utente senza ruolo, menu riordinato, tendine leggibili, 404`.

---

## Fase 3 — Le prenotazioni mai confermate: stato «Non presentata» (no-show)

**Il difetto.** Una prenotazione `Attiva` (creata, mai confermata) resta tale per sempre anche a
data passata: in tabella continua a proporre «Conferma»/«Annulla». Il job notturno
`AutomaticCompletPrenotazioniAsync` (~r.354) completa solo le `InCorso`; il cleanup elimina solo le
`Completata`. La dashboard settimanale (`Dashboardservice.cs` ~r.172-181) conta come no-show le
`Attiva` con data < oggi: la semantica esiste già, ma solo implicita.

**Scelta.** Rendere il no-show uno stato esplicito, **senza migration**: `Prenotazione.Stato` è
salvato come stringa (`GestoraContext.cs` ~r.104-107, `HasConversion<string>()`), quindi un nuovo
valore dell'enum non tocca lo schema. Le «regole per cliente» sul no-show restano idea v2.0
(`BACKLOG.md`): qui si chiude solo il buco.

**Passi:**
1. `Enums/StatoPrenotazione.cs`: aggiungi `NonPresentata = 4` (nome italiano coerente con l'enum;
   in italiano a schermo «Non presentata»).
2. `PrenotazioniService.AutomaticCompletPrenotazioniAsync`: nella stessa esecuzione, dopo il
   completamento delle `InCorso`, seleziona le `Attiva` con `(DataPrenotazione < today) OR
   (DataPrenotazione == today AND oraAttuale > FasciaOraria.OrarioFine)` e portale a `NonPresentata`
   con `AggiornaStatoAsync`. Logga il conteggio. Le righe di collegamento ai tavoli restano (storico).
3. Transizioni: `AnnullaPrenotazioneAsync` rifiuta `NonPresentata` (409, come `Completata`);
   `ConfermaPrenotazioneAsync` rifiuta anche una `Attiva` con data/ora già passata (409 «La
   prenotazione è già passata»); `DeleteAsync` ammette `NonPresentata`; `UpdateAsync` la rifiuta.
   `AutomaticDeletePrenotazioniAsync`: elimina anche le `NonPresentata` oltre i 6 mesi.
4. Dashboard settimanale: `noShow = Stato == NonPresentata || (Stato == Attiva && Data < oggi)`
   (la seconda condizione copre la finestra prima del job). `prenotazioniCheOccupano` e i conteggi
   coperti: `NonPresentata` **non** occupa tavoli e **non** somma coperti (come `Annullata`).
   Aggiorna la `SUM` di `ValidatePrenotazioneAsync` e `CountNumeroCopertiFasciaOrariaAsync`:
   escludi `Annullata` **e** `NonPresentata`. Anche `DisponibilitaService`, se filtra per stato.
5. Test backend: job (`PrenotazioniServiceTests`, area `AutomaticComplet…`) — «Attiva scaduta →
   NonPresentata», «Attiva di oggi con fascia non finita resta Attiva»; conferma di una passata → 409;
   dashboard no-show con il nuovo stato; cleanup elimina le NonPresentata vecchie.
6. Frontend: `types/prenotazione.ts` (`STATI_PRENOTAZIONE`, `STATO_LABELS` → `'Non presentata'`),
   `components/StatoBadge.tsx` (pallino `text-muted-foreground` con parola in corsivo o simile: deve
   distinguersi da Completata e Annullata, senza colore nuovo), `components/AzioniPrenotazione.tsx`
   (nessuna azione di riga; nel menu «…» solo «Elimina definitivamente» per Admin), filtro stato in
   `PrenotazionePage.tsx` (nuova opzione). Test: aggiorna `AzioniPrenotazione.test.tsx`.
7. Documenta in `GestoraWebApi/CLAUDE.md` (grafo degli stati) e in `RUNBOOK.md` §3 (la nota su
   `Completata` non eliminabile vale anche per `NonPresentata`? No: è eliminabile — scrivilo).

Commit: `feat: stato Non presentata per le prenotazioni mai confermate`.

---

## Fase 4 — Dati che il server ha e non manda: turno e coperti per tavolo

Dagli appunti: «mostrare il turno accanto all'orario», «coperti per tavolo», «giorno prima
dell'orario nelle fasce», «separatore per turno in tabella».

1. **Coperti per tavolo** — `Services/Prenotazioni/DTOs/PrenotazioneDTO.cs`: aggiungi
   `int NumeroPosti` a `PostazioneAssegnataDTO`; `Mappings/PrenotazioneMappingProfile.cs`: mappa da
   `PrenotazionePostazione.NumeroPosti`. Test in `PrenotazioneMappingProfileTests.cs` (r.50 già
   imposta il valore sul modello: aggiungi l'assert sul DTO — è proprio il caso REV-001/NEW-001
   «campo dichiarato e mai valorizzato»). Frontend `types/prenotazione.ts` + `PrenotazionePage.tsx`
   colonna Tavoli: `4 (3) · 8 (2) · Sala` → formato `numero (coperti)`, i coperti in `text-nota
   text-muted-foreground`; se `numeroPosti === 0` (dato vecchio) non stampare la parentesi.
   Correggi anche `Development/SeedSviluppo.cs` ~r.608, che scrive `NumeroPosti = 0` sul secondo
   tavolo di un'unione: distribuisci i coperti come farebbe il motore.
2. **Numero del turno** — `PrenotazioneDTO`: aggiungi `int NumeroTurno` = posizione (1-based) della
   fascia fra le fasce **attive dello stesso giorno della settimana**, ordinate per `OrarioInizio`.
   Calcolalo in `PrenotazioniService` nei metodi di lettura (lista, mie prenotazioni, singola) con
   una sola query sulle fasce (già in cache: usa il repository, non `_context` diretto), non in
   AutoMapper. Test: due fasce lunedì 12:00 e 20:00 → prenotazione sulle 20:00 ha `NumeroTurno = 2`.
   Frontend: colonna Orario `20:00–21:30` con sotto, in `text-nota text-muted-foreground`,
   `2º turno`. Stesso dato nella dashboard (`RigaFascia`) se costa poco.
3. **Fasce orarie: giorno prima dell'orario e ordinamento** — `pages/FasciaOrariaPage.tsx`: inverti
   le colonne (Giorno · Orario · Tetto · Stato) e `nomeFascia()` → «lunedì 19:00–23:00». Backend
   `FasciaOrariaRepository.GetAllFasceAsync` (e le altre liste): ordina per `GiornoSettimana` poi
   `OrarioInizio` con ordinamento totale (`.ThenBy(f => f.Id)`), lunedì per primo (`DayOfWeek` parte
   da domenica: usa `((int)g + 6) % 7`). Il modal delle fasce (`FasciaOrariaModal.tsx`) elenca i
   giorni da domenica: falli partire da lunedì mantenendo il valore numerico giusto.
4. **Separatore in tabella Prenotazioni** (solo vista Staff/Admin): l'elenco è ordinato per data e
   orario; quando cambia la **data** inserisci una riga di intestazione a tutta larghezza
   (`bg-muted text-nota uppercase`, es. «giovedì 18 settembre · 3 turni · 27 coperti»). Niente
   raggruppamento per turno: la colonna Orario ora dice già il turno. Se l'ordinamento del backend
   non è per data+orario, sistemalo lì (ordinamento totale).

Commit: `feat: turno e coperti per tavolo nell'API e in tabella, fasce ordinate per giorno`.

---

## Fase 5 — Log leggibili

Appunto: «ottimizzazione della gestione e della leggibilità dei log». Oggi (`Program.cs` ~r.80-85,
`appsettings.json` ~r.21-34): Serilog su Console senza `outputTemplate`, enricher solo
`FromLogContext`, nessun log di richiesta; in Azure si legge dal Log stream.

1. `appsettings.json`: `outputTemplate` sul sink Console:
   `[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext:l} {Message:lj}{NewLine}{Exception}` — e
   accorcia il `SourceContext` al solo nome della classe con un enricher minimo (o usa
   `{SourceContext}` così com'è se l'enricher costa più di quanto rende: decidi e annota).
2. `Program.cs`: `app.UseSerilogRequestLogging()` subito dopo `UseGlobalExceptionHandler`, con
   `MessageTemplate = "{RequestMethod} {RequestPath} → {StatusCode} in {Elapsed:0}ms"` e livello
   `Warning` per status ≥ 500, `Information` altrimenti. Escludi `/health` (Azure lo chiama di
   continuo) alzando il livello a `Verbose` per quel path.
3. Riduci il rumore: gli `LogInformation` che ripetono il nome del metodo fra parentesi quadre
   (`[DashboardService] GetDashboardGiornaliero…`) sono duplicati del `SourceContext`: togli il
   prefisso fra quadre, lascia il messaggio. Non toccare i messaggi che registrano decisioni di
   dominio (job, 409, lock).
4. Riavvia in locale, fai login + una prenotazione + `POST /api/Jobs/trigger/PrenotazioniJob` e
   incolla 15 righe di log «prima» e «dopo» in CONSEGNA.

Commit: `feat: log di richiesta e formato leggibile`.

---

## Fase 6 — Restyle: un'app dall'aspetto moderno, con Docker Hub come riferimento

**Richiesta di Fabio (18/09/2026):** «Voglio che sia un'app dall'aspetto moderno. Prendi come
esempio il sito https://hub.docker.com/». È una richiesta di **direzione visiva**, non di copia: il
riferimento serve per palette, spaziature, gerarchia, componenti e sensazione generale. Nessun logo,
nome, icona o testo di Docker entra nel progetto.

**Cosa resta del redesign «Turno»** (fatto il 09/09, mai verificato): l'architettura a token in
`index.css`, la scala tipografica (`text-readout` … `text-nota`), `BandaCoperti` come elemento
dominante della Dashboard (D1), `AzioniRiga`/menu «…», gli stati vuoti/errore/scheletro,
l'accessibilità (fuoco da tastiera, aree di tocco, `prefers-reduced-motion`), lo script
`contrasto.mjs`. **Cosa cambia**: la tavolozza (oggi «calda» in tema chiaro, grigio neutro in scuro),
il carattere, il raggio dei controlli, la struttura di navigazione, la vetrina.

### 6.1 — Studia il riferimento, non immaginarlo

Con Playwright MCP apri `https://hub.docker.com/` e almeno una pagina interna (es. la pagina di
un'immagine ufficiale come `https://hub.docker.com/_/postgres` e la pagina *Explore*). Per ognuna:
`browser_take_screenshot` a 1440 e 375 px, e `browser_evaluate` con `getComputedStyle` su body,
barra superiore, una card, un pulsante primario, un input, una tabella, un tab attivo, un chip: leggi
**colore di sfondo, colore del testo, bordo, raggio, ombra, font-family, dimensione e peso, spaziature
interne**. Se il sito ha un tema scuro, ripeti in scuro. Salva gli screenshot in
`GestoraDocs/restyle/riferimento/` e scrivi `GestoraDocs/restyle/ANALISI.md`: 20-30 righe con i
valori misurati e le **cinque scelte** che rendono quel sito «moderno» ai tuoi occhi (probabilmente:
un solo blu deciso come colore d'azione su superfici bianche/grigio-chiarissime; bordi sottili al
posto delle ombre; raggio unico e contenuto sugli angoli; molta aria fra i blocchi; tipografia sans
pulita con pesi netti fra titolo e corpo; barra superiore fissa con navigazione orizzontale; tabelle
e liste con intestazioni discrete e righe alte). Se il sito non risponde ai tool, lavora sui valori
che conosci del design system Docker (blu `#1D63ED`-`#2496ED` come primario, superfici
bianco/`#F7F8FA`, testo `#17191E`, bordi `#E1E2E6`, scuro su navy neutro) e annotalo.

### 6.2 — I token nuovi (`gestora-frontend/src/index.css`)

Riscrivi i valori di `:root` e `.dark` **mantenendo gli stessi nomi di token** (così nessun
componente si rompe). Linee guida, da adattare alle misure fatte:
- **primary**: un blu saturo, uno solo, usato per azione primaria, link, tab attivo, fuoco, banda
  «normale». Niente altro colore d'accento.
- **background / card / popover / superficie**: chiaro = bianco e grigio quasi bianco, **freddo o
  neutro**, mai caldo; scuro = navy-neutro a gradini regolari (i quattro livelli X1 della checklist
  restano distinguibili).
- **border**: sottile e visibile su ogni card e tabella; le ombre restano solo su ciò che galleggia.
- **success / warning / destructive / banda-attenzione / banda-pieno**: verde, ambra, rosso pieni e
  chiari, stesso livello di saturazione del primary.
- **sidebar-***: se scegli la barra superiore (vedi 6.3), la sidebar diventa il pannello laterale
  secondario: superfici uguali alla card, non un blocco più scuro.
- **Raggio**: porta i tre scaglioni a due — `6px` per tutto ciò che è controllo o card, `12px` per
  ciò che galleggia; le bande restano a `2px`.
- **Carattere**: sostituisci Archivo con **Inter Variable** (`@fontsource-variable/inter`,
  self-hosted come oggi); aggiorna `@font-face` di ripiego e `scripts/metriche-ripiego.mjs`;
  `tabular-nums` resta sugli orari e sui numeri (X9). Dimensioni: alza leggermente il corpo
  (`text-corpo` 0.9375rem) e abbassa il contrasto di peso fra titolo e sezione.
- Alla fine: `node scripts/contrasto.mjs` **deve** passare in entrambi i temi. Se una coppia cade,
  correggi il token, non il componente.

### 6.3 — Struttura di navigazione

Passa dal layout «sidebar fissa a sinistra + intestazione» a **barra superiore fissa** (`h-14`,
`bg-card`, `border-b`): a sinistra `Logo` + nome, al centro le voci di menu (nell'ordine deciso in
Fase 2.6, i due gruppi separati da un punto o da spazio, voce attiva con sottolineatura di 2px in
`primary` come i tab di Docker Hub), a destra `ThemeToggle` e un menu utente (avatar con le iniziali
dell'email, `DropdownMenu` con email, ruoli, «Esci»). Sotto i 1024px le voci vanno in un pannello a
scomparsa (l'attuale `menuAperto` in `layouts/AppLayout.tsx`), aperto da un pulsante «Menu» a
sinistra. Contenuto centrato con `max-w-6xl` e margine laterale `px-4 md:px-6`. Tutte le pagine
tengono `IntestazionePagina` (titolo + conteggio + azione primaria), ma il titolo scende a
`text-2xl font-semibold`, come i titoli di repository su Docker Hub.

### 6.4 — Componenti

Rivedi **una volta sola** i componenti shadcn in `components/ui/` e i condivisi in `components/`,
non le pagine: `button.tsx` (primario blu pieno `rounded-md h-9`, secondario bianco con bordo,
`azione` = bordo + testo primary come oggi), `card.tsx` (bordo 1px, niente ombra, `p-5`),
`table.tsx` (intestazione `bg-muted/50 text-nota uppercase tracking-wide`, righe `h-12`,
separatori `border-b`), `input.tsx`/`native-select.tsx` (h-9, bordo `border-input`, fuoco ring
primary a 2px), `badge.tsx`/`StatoBadge.tsx` (chip con sfondo tenue e testo pieno dello stesso
colore, come i tag di Docker Hub; il pallino resta perché il colore da solo non basta),
`dialog.tsx` (raggio 12px, intestazione con `border-b`, piè con `border-t`), `tabs` se servono,
`sonner` (toast con bordo, non ombra pesante), `PageState.tsx` (scheletri con il nuovo raggio),
`EmptyState.tsx` (icona `lucide` grande e tenue, titolo, frase, azione). Poi apri ogni pagina e
sistema **solo** ciò che stona: spaziature, allineamenti, classi rimaste con valori vecchi.

### 6.5 — La vetrina (`pages/LandingPage.tsx`)

È la prima cosa che vede chi non ha un account: deve sembrare un prodotto. Struttura ispirata alla
home di Docker Hub: barra superiore con «Accedi» e «Registrati» a destra; **hero** a due colonne
(titolo grande e diretto, sottotitolo, pulsante primario «Controlla la disponibilità» che scorre
al modulo; a destra una card che mostra il modulo di verifica stesso, già pronto); sotto, la sezione
«Come funziona» a tre passaggi (tieni la linea con i pallini, L2) e «Le zone» (tre card con bordo,
non un unico riquadro diviso); piè di pagina minimale. Tutto con i token, nessuna immagine esterna,
nessun colore fisso. Il modulo continua a chiamare **solo** `check-disponibilita`.

### 6.6 — Chiusura della fase

- `npm run lint`, `npm test`, `npm run build`, `node scripts/contrasto.mjs` puliti.
- Con Playwright: screenshot di **ogni** pagina (vetrina, login, registrazione, setup, dashboard,
  prenotazioni, zone, tavoli, fasce, utenti, non autorizzato, 404) a 1440 e 375 px, nei due temi, in
  `GestoraDocs/restyle/dopo/`. Guardali davvero: se una pagina sembra ancora quella di prima, non
  hai finito.
- Aggiorna `gestora-frontend/CLAUDE.md` (sezione «Tavolozza e tema»: nuovi valori, carattere,
  raggi, barra superiore) e `GestoraDocs/verifica-redesign.md`: le voci legate al vecchio aspetto
  (X0 «non marrone», D5 «silenziosa», riferimenti alla sidebar) vanno riscritte per il nuovo, non
  cancellate — la verifica della Fase 8 si fa su questa checklist aggiornata.
- In CONSEGNA: le cinque scelte di stile, i token prima/dopo, gli screenshot di confronto.

Commit: `feat: restyle moderno — token, carattere, barra superiore, componenti, vetrina`.

---

## Fase 7 — Dashboard più viva

Appunto: «troppo statica, funzionalità limitate: più dinamica, interattiva, visivamente più vivace».
Confini: **nessuna libreria di grafici**, solo `BandaCoperti` e i token della Fase 6; la barra del
giorno resta l'elemento dominante (decisione del redesign «Turno», D1). Realizza queste sei cose,
nell'ordine:

1. **Navigazione per giorno** — in cima, accanto alla data: `‹ ieri` · `Oggi` · `domani ›` e un
   `<input type="date">` (stile `native-select`). `useDashboardGiornaliera(data)` prende già la data.
   La settimana segue il giorno scelto (`lunediSettimanaDi(data)` in `lib/date.ts`).
2. **Righe cliccabili** — ogni fascia in «Le fasce di oggi» e ogni giorno in «Questa settimana»
   portano a `/prenotazioni?data=YYYY-MM-DD` (e `&fascia=id` per la fascia). `PrenotazionePage`
   legge i parametri dall'URL con `useSearchParams` e li applica ai filtri (oggi i filtri vivono
   solo nello stato locale: spostali nell'URL, così sono anche condivisibili).
3. **Aggiornamento automatico** — `refetchInterval: 60_000` sulle due query della dashboard, con
   `refetchIntervalInBackground: false`, e in alto a destra «aggiornato alle HH:MM» in `text-nota`.
   Se una prenotazione viene creata/confermata da un'altra pagina, la cache va comunque invalidata
   (controlla che le mutation in `usePrenotazioni.ts` invalidino anche `['dashboard-*']`).
4. **Prossime in arrivo** — nuovo blocco fra le fasce e «Il resto della giornata»: le prossime 5
   prenotazioni `Attiva`/`InCorso` del giorno scelto a partire dall'ora corrente (nome, orario,
   coperti, tavoli), con l'azione di riga «Conferma» per le `Attiva` (riusa `AzioniPrenotazione`).
   Dati: `GET /Prenotazione/get-all-prenotazioni?data=…&pageSize=…` già esistente; se serve un
   ordinamento per fascia lato server, aggiungilo lì.
5. **Settimana con le bande** — nella tabella dei sette giorni aggiungi una colonna con
   `BandaCoperti` (coperti prenotati / capienza del giorno) e, per i giorni passati, il conteggio
   `NonPresentata` in `text-muted-foreground`. Serve `CapienzaGiorno` in `GiornoSettimanaleDTO`:
   calcolalo nel `DashboardService` (somma `MaxCoperti` delle fasce attive di quel giorno). Test.
6. **Vivacità senza rumore** — le bande delle fasce già si animano all'ingresso; aggiungi la
   transizione anche al cambio di giorno (la classe `.banda-in-aggiornamento` esiste). Rispetta
   `prefers-reduced-motion`. Niente altro che si muova.

Test frontend: almeno uno per la navigazione per giorno (il pulsante «domani» cambia la `queryKey`)
e uno per i parametri URL su `PrenotazionePage`. `node scripts/contrasto.mjs` se tocchi un colore.

Commit: `feat: dashboard navigabile per giorno, prossime prenotazioni, settimana con bande`.

---

## Fase 8 — `UI-001`: verifica visiva, fatta davvero

La checklist è `GestoraDocs/verifica-redesign.md` (~90 voci, con le «prime dieci» in cima,
aggiornata in Fase 6.6 al nuovo aspetto). Nessuno l'ha mai eseguita. La esegui tu con Playwright
MCP, poi Fabio ripeterà a mano le voci che segnali.

**Preparazione**: `dotnet run -- --seed-sviluppo` (da `GestoraWebApi/`), backend in ascolto su
`localhost:5099`, frontend `npm run dev` su `localhost:5173`. Con Playwright:
`browser_resize` per le tre larghezze (desktop 1440×900, 768×1024, 375×667); il tema scuro si attiva
dal `ThemeToggle` in alto a destra; il touch «vero» (P4/P15/X2) non è emulabile dal solo resize —
verifica lo stato a riposo con `browser_snapshot`/screenshot e annota che il touch va provato da
Fabio sul telefono.

**Procedura**: per ogni voce della checklist, nell'ordine del documento, esegui l'azione, fai
`browser_take_screenshot` (salvale in `GestoraDocs/verifica-redesign/screenshots/<voce>-<larghezza>-<tema>.png`
— cartella nuova, aggiungila al `.gitignore` se supera i 5 MB totali, altrimenti versionala) e
registra l'esito **direttamente nel file `verifica-redesign.md`**, aggiungendo una riga
`**Esito 18/09 (Fable):** ✅ / ⚠️ / ❌ — nota breve` sotto ogni voce. Le voci S1/S2/S6/S7 (backend
spento, rete lenta, `.env.local` mancante) provale davvero: fermare il backend è consentito.

**Correzioni**: ogni ❌ diventa una modifica **in questa fase**, se è contenuta (spaziature, classi,
troncamento, posizionamento del menu, colonne dello scheletro). Se è un problema di concetto
(esempio: D9, «la barra a 375px non dice più niente»), non riprogettare: annota in CONSEGNA con uno
screenshot e una proposta in tre righe. Priorità agli ⚠️ dichiarati dal documento (P2, T4, U4, P4,
X2, X4, T1, P14, S1).

**Chiusura**: `UI-001` chiusa in `BACKLOG.md` con il conteggio (✅/⚠️/❌) e il rimando al file.

Commit: `fix: correzioni dalla verifica visiva del redesign (UI-001)`.

---

## Fase 9 — Pulizie tecniche

1. `OPS-005`: `@tanstack/react-query-devtools` da `dependencies` a `devDependencies` in
   `gestora-frontend/package.json` (`npm install -D …`, poi `npm uninstall` dalla sezione sbagliata
   o modifica a mano + `npm install`). Verifica che `npm run build` passi e che `ReactQueryDevtools`
   non compaia in `dist/` (`Select-String -Path dist\assets\*.js -Pattern ReactQueryDevtools` deve
   restituire nulla). Nota: `components/DevtoolsQuery.tsx` importa dinamicamente solo con
   `import.meta.env.DEV`, quindi è già sicuro.
2. Residui Railway nel codice: `GestoraWebApi/railway.json` (eliminalo: Railway non esiste più),
   commenti nel `Dockerfile` che citano Railway (riscrivili per Azure: «la porta arriva da
   `ASPNETCORE_URLS`/`WEBSITES_PORT`»), `GestoraWebApi/.github/workflows/` vuota (eliminala).
   Cerca `railway` in tutto il repo (esclusi `docs/archivio/` e i tracker) e sistema ogni
   occorrenza che descrive lo stato attuale; quelle storiche restano.
3. `RUNBOOK.md` §2 dice «26 test frontend»: allinea i numeri reali (backend e frontend) dopo le
   fasi precedenti, in RUNBOOK e nei tre CLAUDE.md.
4. `npm audit` e `dotnet list package --vulnerable`: devono restare a zero. Se qualcosa è comparso,
   aggiorna solo patch/minor.
5. Le pulizie che restano a Fabio (in CONSEGNA, sezione «Pulizie facoltative»): `OPS-002`
   cancellare `Gestora_BACKUP_20260903\` (393 MB, non tracciata); `OPS-003` `git gc --prune=now` a
   PC appena riavviato; `OPS-004` tag `v1.0.0` disallineato (locale `11bc56d`, GitHub `8ba6a2f`):
   `git push --force origin refs/tags/v1.0.0`.

Commit: `chore: devtools in devDependencies, residui Railway rimossi, numeri allineati`.

---

## Fase 10 — Documentazione allineata alla realtà

1. **`RUNBOOK.md` riscritto per Azure + Neon** (oggi è ancora tutto Railway). Sezioni da rifare:
   §3 *Reset dei database* (Neon: `psql "<connection string>"` da locale — Neon **ha** un indirizzo
   pubblico, a differenza di Railway — poi `dotnet ef database update` con la connection string di
   Neon passata via variabile d'ambiente, poi `Scripts/quartz_postgres.sql`); §4 *Migration in
   produzione* (sequenza: backup con `pg_dump` da locale → script idempotente senza BOM → push su
   `main` che fa partire `docker-publish.yml` → Docker Hub → webhook → Azure); §5 *Pubblicare un
   rilascio* (cosa si ripubblica da solo: push su `main` → Vercel per il frontend; push su `main`
   che tocca `GestoraWebApi/**` → GitHub Actions → Docker Hub → Azure per il backend; `curl …/health`
   nuovo URL); §6 *Backup* (`pg_dump` da locale, file `backup_*.dump` già ignorati); §8 *Trappole*:
   sostituisci le trappole Railway con quelle Azure già pagate (variabili con **doppio** underscore;
   valori lasciati come segnaposto; `VITE_API_URL` su Vercel deve essere di tipo *Config* non
   *Secret* e finire con `/api`; SCM Basic Auth necessario per il webhook). Aggiungi la sezione
   **Sicurezza** (vedi punto 3).
2. **I tre `CLAUDE.md`**: stato §1 (v1.1.0 in preparazione, numeri test), «Produzione: Vercel +
   Railway» → Azure/Neon in radice §3, `GestoraWebApi/CLAUDE.md` (HTTPS/Railway → Azure termina
   HTTPS; «env var Railway» → Azure; grafo stati con `NonPresentata`; endpoint nuovi campi),
   `gestora-frontend/CLAUDE.md` (Vercel «puntata a Railway» → Azure; menu; stato nuovo; parametri
   URL dei filtri; numeri test).
3. **Il dubbio sul token** (primo appunto di Fabio, va chiuso per iscritto). Scrivi in `RUNBOOK.md`
   §Sicurezza, in linguaggio semplice: il token JWT in `localStorage` è leggibile **solo** da chi ha
   già in mano quel browser con quella sessione aperta — è la stessa cosa che avere davanti la
   pagina già loggata; un altro sito non può leggerlo (ogni origine ha il suo `localStorage`); un
   altro computer non può leggerlo. Non è un bug: è come funziona ogni web app con login. La
   protezione vera sono la scadenza a 60 minuti (`JwtSettings:ExpiryMinutes`) e il logout che lo
   cancella. La firma è verificata dal server: un token modificato viene rifiutato. L'alternativa
   (cookie `HttpOnly` + protezione CSRF) è una riprogettazione: idea per la v2.0, annotala in
   `BACKLOG.md`. Come piccolo miglioramento **fai** questo: `ClockSkew = TimeSpan.Zero` in
   `Extensions/AuthenticationExtensions.cs` (oggi il token vive 65 minuti invece di 60) e verifica
   che il logout svuoti anche la cache di React Query (già fatto in `AppLayout.handleLogout`).
4. **`BACKLOG.md`**: rimuovi le voci chiuse (`CAP-001`, `UI-001`, `DOC-001`, `OPS-005`), lascia
   `OPS-001/002/003/004` con lo stato «in attesa di Fabio», aggiorna le idee v2.0 (cookie HttpOnly,
   regole no-show per cliente). Aggiorna la data in testa.
5. **`docs/archivio/STORICO_FASI.md`**: aggiungi «Fase 12 — Migrazione Azure/Neon (16-18/09)» con
   la storia della fase (dal `BACKLOG.md` di oggi: trial Railway scaduto, i due bug delle variabili,
   `VITE_API_URL` Config/Secret, verifica end-to-end del 18/09, incidente della password in
   `envDBNeon.txt`) e «Fase 13 — Chiusura v1.1 (18/09→)» con quello che hai fatto tu, difetti
   emersi, regole di metodo nuove se ne sono nate. Tabella «Riepilogo veloce» aggiornata.
6. `AppuntiFix.txt`: **non cancellarlo** (è di Fabio, ignorato da Git). In CONSEGNA metti la tabella
   «appunto → cosa è diventato → fase» per ognuna delle 13 righe del file, «SEPARARE FE E BE» inclusa.
7. Tracker Excel `TrackGestora_v2.xlsx`: **non toccarlo** (aggiornamento manuale di Fabio). In
   CONSEGNA prepara l'elenco delle righe da aggiungere nel foglio *Fix e Bug* (sigla, titolo, stato).

Commit: `docs: runbook per Azure/Neon, CLAUDE.md, backlog e storico aggiornati alla v1.1`.

---

## Fase 11 — Separare frontend e backend, reset dei database, consegna

### 11a — «SEPARARE FE E BE» (primo appunto del file, in maiuscolo)

**Interpretazione adottata** (Fabio non ha specificato: se sbagliata la correggerà): due repository
Git indipendenti, `gestora-api` e `gestora-frontend`, ciascuno con la propria CI e il proprio deploy,
al posto del monorepo. Motivi: Vercel e Docker Hub già lavorano su cartelle separate; le CI hanno già
`paths:` separati; un push al backend oggi ricostruisce inutilmente anche il frontend. **Va fatto per
ultimo** perché spezza la storia condivisa.

**Cosa fai tu (senza creare repository né pushare):**
1. Prepara in ciascuna cartella i file che oggi vivono solo alla radice e che servono a un repo
   autonomo: `.gitignore` proprio (derivato da quello di radice, solo le regole pertinenti),
   `README.md` breve (cos'è, come si avvia, dove sono le procedure), i workflow spostati
   (`ci-backend.yml` + `docker-publish.yml` → `GestoraWebApi/.github/workflows/` con `paths:` e
   `working-directory` **tolti** perché la radice è già il progetto; `ci-frontend.yml` →
   `gestora-frontend/.github/workflows/`). I workflow alla radice restano finché il monorepo esiste.
2. I documenti trasversali (`CLAUDE.md` radice, `BACKLOG.md`, `RUNBOOK.md`, `docs/`, tracker,
   `GestoraDocs/`) vanno nel repo del **backend** in una cartella `docs/progetto/` (è il repo «di
   testa»); il `CLAUDE.md` del frontend rimanda lì con il link GitHub. Prepara le copie, non
   spostare gli originali.
3. Scrivi `RUNBOOK.md` §9 *Separare i repository*: procedura passo passo per Fabio, una riga per
   comando: `git subtree split --prefix=GestoraWebApi -b split-api` (conserva la storia del solo
   backend), creazione repo su GitHub, `git push <nuovo-remote> split-api:main`, idem frontend;
   poi su Vercel *Settings → Git* ricollegare al nuovo repo con root `.`; su GitHub Actions
   ricreare i secret `DOCKERHUB_USERNAME`/`DOCKERHUB_TOKEN` nel repo api; verifica finale (`/health`,
   login, un push di prova per catena Actions → Docker Hub → Azure); infine archiviare il monorepo
   (GitHub *Archive*, non cancellare). Segnala che i tag `v1.0.x` restano nel monorepo archiviato
   e che il primo tag dei nuovi repo sarà `v1.1.0`.

### 11b — `OPS-001` reset dei database

- **Locale**: fallo tu, a lavoro finito e test verdi: `dotnet ef database drop` (senza `--force`,
  conferma), `dotnet ef database update`, `psql -U postgres -d gestora_db -f Scripts\quartz_postgres.sql`,
  poi `dotnet run -- --seed-sviluppo` così Fabio trova dati per provare.
- **Neon (produzione)**: lo fa Fabio. Il database è nato il 16/09 e contiene solo la prova del
  18/09 (una zona, un tavolo, una fascia, una prenotazione, un utente Staff e uno Cliente di prova).
  Prepara `GestoraWebApi/Scripts/reset_dati_prova.sql`: `TRUNCATE` di `PrenotazioniPostazioni`,
  `Prenotazioni`, `Postazioni`, `FasceOrarie`, `Zone`, `LogActivities` (con `RESTART IDENTITY
  CASCADE`) e `DELETE` degli utenti che **non** hanno il ruolo Admin (tabelle `Utenti`, `Ruoli`,
  `AspNetUserRoles`… verifica i nomi veri in `GestoraContext.cs` r.31-32 e nelle migration). Non
  toccare `QRTZ_*` né `__EFMigrationsHistory`. In CONSEGNA: comando `psql "<connection string>" -f
  Scripts\reset_dati_prova.sql` e la nota di fare prima un `pg_dump`.

### 11c — Consegna finale

`GestoraDocs/CONSEGNA_v1.1.md` deve avere, in quest'ordine:
1. **DA FARE SUBITO — Fabio**: rotazione password Neon (Fase 0b), rimozione di `envDBNeon.txt`.
2. **Riepilogo per fase**: cosa è cambiato, decisioni prese, cosa verificare a mano (con i percorsi
   dell'interfaccia: «Dashboard → frecce del giorno → …»).
3. **Giro di test consigliato per Fabio** sul portale locale (`localhost:5173`, seed caricato):
   20-30 controlli in ordine, uno per riga, ognuno con «devi vedere…».
4. **Rilascio** — sequenza per Fabio: `git log --oneline main..dev` per vedere i commit; merge
   `dev` → `main` (da Visual Studio); tag `v1.1.0` sulla punta di `main` e push del tag; attendere
   la catena Actions → Docker Hub → Azure; `curl …/health`; Vercel ripubblica da solo; login con
   i tre ruoli; `POST /api/Jobs/trigger/PrenotazioniJob`.
5. **Appunti → esito** (tabella dalla Fase 10.6) e righe da aggiungere al tracker.
6. **Pulizie facoltative** e **Separazione dei repository** (Fase 11a) come passi a parte.
7. **Bloccato / non fatto**: tutto ciò che hai lasciato e perché. Meglio una riga onesta che una
   fase «finita» a metà.

Commit finale: `docs: consegna v1.1 — reset locale, script reset Neon, preparazione split repo`.

---

## Verifica finale prima di fermarti

Esegui tutto e incolla i numeri in CONSEGNA:
- `git branch --show-current` → `dev`; `git status` pulito; `git log --oneline main..dev` mostra i
  commit di fase; **nessun** commit su `main`, **nessun** push (`git status -sb` deve dire
  `ahead`, mai `up to date` con origin per `dev`… a meno che Fabio non abbia pushato lui).
- `dotnet test` → tutti verdi, numero annotato; `npm run lint`, `npm test`, `npm run build` puliti;
  `node scripts/contrasto.mjs` senza coppie sotto soglia; `npm audit` e
  `dotnet list package --vulnerable` a zero.
- Backend e frontend avviati insieme: login con i tre ruoli, una prenotazione creata → confermata →
  completata, una lasciata `Attiva` a data passata → job forzato → «Non presentata»; dashboard
  navigata avanti/indietro; menu nel nuovo ordine; utente senza ruolo → schermata con «Esci».
- `git ls-files | Select-String envDBNeon` → vuoto **solo dopo** che Fabio avrà fatto la sua parte:
  finché non lo fa, in CONSEGNA resta in cima in rosso.

## Cosa NON fare

- Non creare repository, non pushare, non toccare `main`, non taggare.
- Non aprire nessun pannello di produzione (Azure, Neon, Vercel, Docker Hub, GitHub settings).
- Non riaprire le 10 decisioni di prodotto, `REV-056`, `REV-004`, i test di concorrenza automatici.
- Non aggiungere librerie di grafici, di stato globale, di UI diverse da shadcn. L'unica
  dipendenza nuova ammessa è il carattere (`@fontsource-variable/inter`).
- Il restyle è **solo** la Fase 6: la Fase 8 corregge, non ridisegna. Non riaprire lo stile dopo.
- Non copiare nulla di Docker Hub che non sia una scelta di stile: niente logo, nome, icone, testi.
- Non modificare i file `.xlsx` né `AppuntiFix.txt`.
- Non scrivere `DateTime.Now`: esiste `IClock`. Non usare `toISOString()` per date di calendario:
  esiste `lib/date.ts`.
- Non fermarti a chiedere. Decidi, annota, prosegui. Se qualcosa non ti convince, lo scrivi in
  CONSEGNA e vai avanti.

Buon lavoro. Quando hai finito, l'ultima riga della tua risposta deve essere il conteggio: fasi
completate / fasi con riserva / fasi bloccate, e i numeri dei test backend e frontend.
