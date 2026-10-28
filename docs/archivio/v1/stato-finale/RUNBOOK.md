# Gestora — procedure operative

Come si fanno le operazioni del progetto, con i comandi veri e **cosa va storto se si sbaglia**.
Ogni trappola scritta qui è già costata tempo almeno una volta.

Tutti i comandi sono per **PowerShell su Windows**, su una riga sola.

---

## Indice

1. [Avviare il progetto in locale](#1-avviare-il-progetto-in-locale)
2. [Verificare che sia tutto a posto](#2-verificare-che-sia-tutto-a-posto)
3. [Reset dei database](#3-reset-dei-database)
4. [Applicare una modifica al database in produzione](#4-applicare-una-modifica-al-database-in-produzione)
5. [Pubblicare un rilascio](#5-pubblicare-un-rilascio)
6. [Accedere al database di produzione e fare un backup](#6-accedere-al-database-di-produzione-e-fare-un-backup)
7. [User Secrets: le credenziali locali](#7-user-secrets-le-credenziali-locali)
8. [Trappole da conoscere](#8-trappole-da-conoscere)
9. [Sicurezza](#9-sicurezza)

---

## 1. Avviare il progetto in locale

Servono due terminali, più PostgreSQL già attivo sulla macchina.

**Backend** (porta 5099, fissata in `Properties/launchSettings.json`):
```powershell
cd GestoraWebApi; dotnet run
```

**Frontend** (porta 5173):
```powershell
cd gestora-frontend; npm run dev
```

Il frontend locale legge `.env.local`, che deve contenere `VITE_API_URL=http://localhost:5099/api`.
Quel file **non è versionato**: su un computer nuovo va ricreato.

> Aprendo `localhost:5173` si vedono sempre i dati del **database locale**, mai quelli di
> produzione. È voluto.

---

## 2. Verificare che sia tutto a posto

Da fare prima di chiudere una fase. Tutti e quattro devono essere puliti.

```powershell
cd "...\Gestora"; dotnet test Gestora.sln
cd "...\gestora-frontend"; npm test
cd "...\gestora-frontend"; npm run build
cd "...\gestora-frontend"; npm run lint
```

Valori attesi oggi: **292** test backend, **64** frontend, build senza errori, lint senza errori.
I test del backend stanno in `GestoraWebApi.Tests/`, accanto all'API: si lanciano dalla radice
attraverso `Gestora.sln` (dal 02/10/2026, prima stavano dentro `GestoraWebApi/`).

> ⚠️ `npm test` **non controlla i tipi**, `npm run build` sì. Un test verde non sostituisce una
> build pulita: in Fase 8 la build ha trovato un campo scritto male che i test non vedevano.

> ⚠️ Se un test contraddice quello che vedi succedere davvero, **sospetta la build prima del
> codice**: un file ripristinato da un backup ha un timestamp vecchio e il compilatore non lo
> ricompila. Si risolve con `dotnet build -t:Rebuild`.

Esiste anche la scorciatoia `/verifica-gestora`, che lancia questi controlli e riporta l'esito.

### Prova manuale del tetto dei coperti (`CAP-001`)

Il lock sulla riga della fascia (`SELECT ... FOR UPDATE`) **non si prova con i test automatici**:
il database finto dei test non ha lock, come non ha gli indici unici. Si prova a mano, così:

1. Fascia con **4 coperti residui** (es. tetto 20, già prenotati 16).
2. Due browser (o una finestra normale e una in incognito), stesso giorno, stessa fascia, utenti
   diversi (`staff@gestora.local` e `admin@gestora.local` vanno bene).
3. In entrambi compilare una prenotazione da **4 coperti** e premere «Salva» nello stesso istante.
4. **Atteso**: una passa, l'altra riceve il 409 «La fascia oraria ha raggiunto la capienza
   massima». In dashboard la fascia segna `20 / 20 · pieno`, mai `24 / 20`.

Se comparisse «N oltre il tetto» in rosso nella riga della fascia, il lock non ha funzionato: è
il segnale che il dato è incoerente, messo apposta per non nasconderlo (`copertiOltreIlTetto`).

---

## 3. Reset dei database

Cancellare tutto e ripartire da zero. **Da fare solo a implementazioni concluse** (voce `OPS-001`
in `BACKLOG.md`).

### ⚠️ Le tre cose che si dimenticano

1. **Le tabelle `QRTZ_*` non stanno nelle migration di Entity Framework.** Dopo aver ricreato il
   database va rieseguito a mano `Scripts/quartz_postgres.sql`, **altrimenti l'applicazione non
   parte**.
2. **Sparisce anche il registro delle attività** (tabella `Logging`), non solo i dati di prova.
   È una conseguenza voluta della scelta, non un effetto collaterale da scoprire dopo.
3. **Il primo amministratore si ricrea da solo**: `POST /api/Setup/admin` si riapre
   automaticamente quando non esiste nessun Admin. Nessuna variabile da toccare su Azure o
   Vercel.

### Locale

**Il backend va spento prima**, altrimenti la connessione aperta blocca la cancellazione.

```powershell
cd "...\GestoraWebApi"; dotnet ef database drop
```
Senza `--force`: così stampa quale database sta per cancellare e chiede conferma. È la rete di
sicurezza, non toglierla.

```powershell
cd "...\GestoraWebApi"; dotnet ef database update
psql -U postgres -d gestora_db -f Scripts\quartz_postgres.sql
```

Poi si apre l'applicazione, che porterà alla schermata di primo avvio per ricreare l'Admin.

### Produzione

Stessa sequenza, ma il database è su Neon e **la esegue Fabio**. A differenza di Railway, Neon
**ha un indirizzo pubblico**: si entra direttamente da locale, non serve una sessione remota.

```powershell
psql "postgresql://<utente>:<password>@<host>.neon.tech/<database>?sslmode=require"
```

(la stringa esatta si legge dal pannello Neon, sezione *Connection string* — formato `postgresql://`,
non quello con `Host=;Database=;...` usato da .NET). Da lì: `dotnet ef database update` puntato
alla stessa connection string (via variabile d'ambiente, vedi §7), poi
`Scripts/quartz_postgres.sql` con `psql -f`.

Prima di iniziare: **backup** (`pg_dump`, vedi §6), anche se si sta cancellando apposta. Se
qualcosa va storto a metà si resta con un database mezzo vuoto e nessun modo di tornare indietro.

**Come è andata il 01/10/2026** (tre differenze rispetto al locale):
- **Fermare l'App Service** su Azure prima di iniziare, e riavviarlo alla fine (poi `/health`).
- **Al posto di `dotnet ef database drop`**: `DROP SCHEMA public CASCADE; CREATE SCHEMA public;`
  con `psql -c`. Su Neon il ruolo `neondb_owner` può non riuscire a cancellare il database stesso.
  Porta via anche le tabelle `QRTZ_*`: poi `dotnet ef database update` e `quartz_postgres.sql`.
- **`pg_dump` 17 non funziona con Neon (server 18)**: dà «la versione del server non corrisponde».
  Per un backup serve il client PostgreSQL 18, oppure un branch Neon (*Branches → Create branch*).
- La password si passa con `$env:PGPASSWORD` (inserita con `Read-Host -AsSecureString`) e a EF con
  `$env:ConnectionStrings__DefaultConnection`; a fine lavoro `Remove-Item` di entrambe.

---

## 4. Applicare una modifica al database in produzione

Le modifiche allo schema (migration) **si applicano a mano**: è la decisione di prodotto 2.
Claude prepara la migration, Fabio la applica.

**Perché serve una sequenza stretta.** Fra "modifica applicata" e "nuova versione online" c'è un
intervallo (il tempo della catena Actions → Docker Hub → webhook → Azure, vedi §5) in cui la
versione vecchia interroga uno schema che non corrisponde più, e risponde errore 500 su tutto ciò
che tocca quella tabella.

Per il traffico di Gestora la soluzione giusta è fare i passaggi **in fila stretta**, non a
distanza di ore:

1. **Backup** (`pg_dump` da locale, vedi §6) — non negoziabile, anche per una modifica banale
2. Applicare la modifica (script idempotente, vedi sotto)
3. Pubblicare la nuova versione dell'applicazione (push su `main`, vedi §5)
4. Verificare: `GET /health` deve rispondere `Healthy`, più una chiamata vera sulla parte toccata

**Lo script si genera così:**
```powershell
cd "...\GestoraWebApi"; dotnet ef migrations script --idempotent -o Scripts\nome_script.sql
```

> ⚠️ **Togliere il BOM dallo script prima di usarlo.** Il file generato ha un carattere invisibile
> in testa che psql attacca alla prima istruzione: `START TRANSACTION` fallisce e **tutto lo
> script gira senza transazione**, quindi senza possibilità di annullare se qualcosa va male.
> È già successo in produzione il 02/09/2026.

> ⚠️ **Se una modifica si applica a mano, usare sempre lo script generato**, non i comandi SQL
> scritti a mano: lo script include anche la riga che registra la modifica come applicata. Senza
> quella riga lo schema è giusto ma Entity Framework continua a considerarla da fare, e un futuro
> aggiornamento proverà a rieseguirla. Successo il 31/08, sistemato il 04/09.

---

## 5. Pubblicare un rilascio

**Il commit, il push, il merge e il tag li fa Fabio.** Claude fornisce solo il messaggio.

1. Commit su `dev`
2. Merge `dev` → `main`
3. Tag sulla punta di `main`, poi push del tag
4. Verifica in produzione

**Cosa si ripubblica da solo:**
- push su `main` che tocca `gestora-frontend/**` → **Vercel** ricostruisce e pubblica il frontend
  (pochi minuti)
- push su `main` che tocca `GestoraWebApi/**` → **GitHub Actions** (`docker-publish.yml`) builda
  l'immagine e la pubblica su **Docker Hub** (`fabioiacurto/gestora-api:latest`) → il **webhook di
  distribuzione continua** configurato su Azure App Service vede la nuova immagine e riavvia il
  container (qualche minuto in più della sola build)
- il **tag non fa niente**: è solo un segnalibro sulla storia

Quindi: se la modifica è solo di backend, Vercel ricostruisce ma non cambia niente (il frontend
resta lo stesso), e viceversa. La catena backend ha due passaggi automatici in serie (Actions poi
webhook Azure): se `/health` non risponde subito dopo il push, aspettare qualche minuto prima di
sospettare un problema — è la catena che sta ancora girando, non necessariamente un guasto.

**Numerazione:** si alza l'ultimo numero (`v1.0.5` → `v1.0.6`) per fix e correzioni. Una fase di
solo riordino interno può restare **senza tag**, come la Fase 9.

**Verifica dopo il deploy:**
```powershell
curl https://gestora-api-emdvdqegg7g8gmaq.canadacentral-01.azurewebsites.net/health
```
Deve rispondere `Healthy` — copre anche la raggiungibilità del database (Neon), non solo il
processo. Il frontend si verifica aprendo `https://gestora-project-xi.vercel.app`.

Poi login dal frontend con i tre ruoli.

> ⚠️ **Prima di confermare un commit, contare i file inclusi.** `git status` breve raggruppa le
> cartelle nuove e ne mostra meno di quanti ce ne sono davvero; Visual Studio fa lo stesso, e i
> file nuovi vanno spuntati a mano. Per l'elenco reale:
> ```powershell
> git status --untracked-files=all
> ```
> In Fase 7 un commit ha lasciato fuori 12 file nuovi, che sono stati persi.

---

## 6. Accedere al database di produzione e fare un backup

A differenza di Railway, il Postgres di Neon **ha un indirizzo pubblico**: si lavora da locale,
non serve entrare in una sessione remota.

```powershell
pg_dump "postgresql://<utente>:<password>@<host>.neon.tech/<database>?sslmode=require" -f backup_completo.dump -Fc
```

Per una singola tabella (più veloce da controllare a occhio):
```powershell
psql "postgresql://<utente>:<password>@<host>.neon.tech/<database>?sslmode=require" -c '\copy "FasceOrarie" TO ''backup_FasceOrarie.csv'' CSV HEADER'
```

> ⚠️ I file di backup **non vanno mai committati**. Il `.gitignore` di radice li esclude già
> (`backup_*.csv`, `backup_*.dump`, `*.dump`), ma controllare prima del commit. Il 02/09/2026 tre
> file di backup sono finiti in un repository pubblico e la storia di Git ha dovuto essere
> riscritta. Lo stesso è successo il 18/09/2026 con `envDBNeon.txt` (credenziali di connessione,
> non un backup, ma stesso principio: qualsiasi cosa con dentro una password sta fuori da Git).

### Rotazione della password del database

Dashboard Neon → progetto → **Roles** → `neondb_owner` → **Reset password**. Copiare la nuova
connection string che compare (formato `postgresql://...`), poi:

1. **Azure** — App Service `gestora-api` → *Environment variables* → aggiornare
   `ConnectionStrings__DefaultConnection` (**doppio** underscore — vedi trappola sotto) con la
   stessa stringa riscritta nel formato che .NET si aspetta
   (`Host=...;Database=...;Username=...;Password=...;SSL Mode=Require`, non `postgresql://...`)
   → *Apply* → l'App Service si riavvia da solo.
2. Verificare: `curl .../health` deve tornare a dire `Healthy`. Se dice `Unhealthy`, quasi sempre
   è la stringa scritta nel formato sbagliato (URI invece di coppie `chiave=valore`).

> ⚠️ **Le variabili di Azure App Service vogliono il doppio underscore** (`ConnectionStrings__DefaultConnection`,
> non `ConnectionStrings:DefaultConnection`): è così che ASP.NET Core legge le variabili
> d'ambiente come se fossero sezioni annidate della configurazione. Un singolo underscore o i due
> punti vengono ignorati senza errore, e l'app parte comunque — ma con la stringa vuota o quella
> vecchia. Già successo durante la migrazione del 16-17/09/2026, insieme a un secondo bug simile:
> variabili lasciate come testo segnaposto invece del valore vero.

---

## 7. User Secrets: le credenziali locali

Le credenziali di sviluppo vivono **fuori dal repository**, in un file sulla macchina:

```
%APPDATA%\Microsoft\UserSecrets\aa9f6e84-1217-48e1-9783-b07f152f7874\secrets.json
```

Comandi, da eseguire dentro `GestoraWebApi\`:
```powershell
dotnet user-secrets list
dotnet user-secrets set "JwtSettings:Secret" "nuovo-valore"
dotnet user-secrets remove "JwtSettings:Secret"
```

Su un computer nuovo vanno ricreati da zero (`dotnet user-secrets init` e poi `set`).

> ⚠️ **Per digitare una password usare sempre:**
> ```powershell
> $sec = Read-Host "Password" -AsSecureString
> ```
> `Read-Host` da solo **mostra la password a schermo**. Il 07/09/2026 una password di produzione
> è finita così in chat ed è stata sostituita.

---

## 8. Trappole da conoscere

### psql chiamato da PowerShell mangia le virgolette

PowerShell 5.1 toglie i doppi apici dagli argomenti passati a un programma esterno. Quindi
`-c 'SELECT * FROM "Utenti";'` arriva a psql **senza** virgolette, e PostgreSQL cerca la tabella
`utenti` in minuscolo: "relazione non esiste".

Vanno protette con il backslash, dentro gli apici singoli:
```powershell
psql -U postgres -d gestora_db -c 'SELECT * FROM \"Utenti\";'
```

> **Nomi delle tabelle degli utenti**: in questo progetto si chiamano `Utenti` e `Ruoli`,
> **non** `AspNetUsers` / `AspNetRoles`.

### Comandi PowerShell lunghi

Vanno dati **su una riga sola**: i blocchi su più righe si concatenano male nel terminale.
Se un comando è davvero lungo, si mette in un file `.ps1` salvato in **UTF-8 con BOM**, altrimenti
PowerShell 5.1 lo legge con la codifica sbagliata e gli accenti si rompono.

### Excel COM da PowerShell (per il tracker)

- I fogli si indirizzano per **numero**, non per nome: i nomi contengono emoji e falliscono
- I valori si scrivono sempre convertiti a testo: `[string]"..."`
- Le date vanno scritte come date vere (`[datetime]::new(2026,9,8)`), non come stringhe:
  scritte come testo vengono lette all'americana e il giorno diventa il mese
- Per copiare la formattazione di una riga: `Rows.Item(x).Copy(Rows.Item(y))`, mai `PasteSpecial`
- Il colore di una cella si legge come numero, non come testo: per riusare un colore esistente
  si legge da una cella che ce l'ha già, invece di scriverlo a mano

### Azure App Service: le trappole della migrazione (16-18/09/2026)

- **Variabili con doppio underscore.** `ConnectionStrings__DefaultConnection`, non
  `ConnectionStrings:DefaultConnection`: è così che Azure passa le variabili d'ambiente come
  sezioni annidate della configurazione .NET. Un solo underscore non dà errore, dà solo un valore
  vuoto — l'app parte lo stesso e fallisce più avanti, in un punto che sembra scollegato dalla
  causa vera.
- **Valori lasciati come segnaposto.** Durante la configurazione iniziale è facile copiare un
  nome di variabile d'esempio (`<il-tuo-valore-qui>`) e dimenticare di sostituirlo col valore
  vero. L'app parte comunque, con un errore che dipende da cosa manca.
- **`VITE_API_URL` su Vercel deve essere di tipo *Config* (o *Plaintext*), non *Secret*.** Un
  valore salvato come *Secret* non viene esposto al build del frontend nel modo che Vite si
  aspetta: la build riesce, ma l'app compilata punta a un indirizzo vuoto o sbagliato. Deve anche
  finire con `/api`.
- **SCM Basic Auth necessaria per il webhook di distribuzione continua.** Senza, Azure non
  accetta la notifica che arriva da Docker Hub quando c'è una nuova immagine, e il backend resta
  fermo alla versione precedente finché non si riavvia a mano dal pannello.

### Gli indirizzi degli endpoint

La rotta base è `/api/[nome del controller senza "Controller"]`.
Quindi `AuthenticationUserController` risponde su `/api/AuthenticationUser/...` — **non**
`/api/AuthenticationUserController/...` e **non** `/api/Auth/...`, come dice documentazione
vecchia.

Eccezione da ricordare: le fasce orarie rispondono su `/api/FasceOrarie/...` anche se la classe
si chiama `FasciaOrariaService`. È il naming incoerente lasciato apposta (vedi `BACKLOG.md`,
sezione *Deciso di NON fare*).

---

## 9. Sicurezza

### Il token nel browser, spiegato in chiaro

Il primo appunto di Fabio su questo progetto era un dubbio preciso: se il token di accesso (JWT)
si legge dagli strumenti sviluppatore del browser (F12 → Application → Local Storage), chiunque
arrivi a quella schermata non ottiene forse l'accesso come quell'utente?

Risposta: **sì, ma solo chi ha già in mano quel browser con quella sessione aperta.** Non è un
modo per entrare da fuori. In dettaglio:

- **Un altro sito non può leggerlo.** Ogni sito web ha il proprio `localStorage`, isolato dagli
  altri: è una regola del browser (same-origin policy), non qualcosa che Gestora fa da sé.
- **Un altro computer non può leggerlo.** Il `localStorage` vive solo sulla macchina dove è stato
  scritto.
- **Chi vede quel token ha comunque bisogno di avere davanti quel browser già loggato** — cioè la
  stessa situazione di chi si siede a una tastiera con la sessione già aperta. Copiare il token
  dagli strumenti sviluppatore non è diverso, in pratica, da usare direttamente quella finestra.
- **Il server verifica la firma.** Un token modificato a mano (per esempio per cambiare il proprio
  ruolo) viene rifiutato: la firma non corrisponderebbe più.

Quindi non è un bug: è come funziona ogni applicazione web con login che non usa un sistema più
complesso (cookie `HttpOnly` + protezione CSRF, che è un'architettura diversa, non una correzione
a questa). La protezione vera sta in due cose, entrambe già presenti:

1. **La scadenza**, 60 minuti (`JwtSettings:ExpiryMinutes`) — con la correzione di questa fase
   (`ClockSkew = TimeSpan.Zero`) scade esattamente a 60 minuti, non a 65.
2. **Il logout**, che cancella il token e svuota anche la cache di React Query
   (`AppLayout.handleLogout` e il gestore di sessione scaduta fanno entrambi le due cose insieme),
   così non restano dati del vecchio utente in memoria dopo l'uscita.

**Idea per la v2.0** (non una correzione da fare ora): passare a cookie `HttpOnly` con protezione
CSRF è una riprogettazione dell'autenticazione, non un fix — vedi `BACKLOG.md`, sezione *Idee per
la v2.0*.
