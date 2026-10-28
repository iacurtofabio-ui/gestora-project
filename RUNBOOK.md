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

Servono due terminali, più PostgreSQL già attivo sulla macchina. I comandi partono dalla
cartella del progetto (`Gestora`).

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

Da fare prima di dire chiusa un'attività. Tutti e quattro devono essere puliti.

```powershell
cd "...\Gestora"; dotnet test Gestora.sln
cd "...\gestora-frontend"; npm test
cd "...\gestora-frontend"; npm run build
cd "...\gestora-frontend"; npm run lint
```

Valori attesi oggi: **484** test backend, **132** frontend, build senza errori, lint senza errori.
I test del backend stanno in `GestoraWebApi.Tests/`, accanto all'API: si lanciano dalla radice
attraverso `Gestora.sln`.

> ⚠️ `npm test` **non controlla i tipi**, `npm run build` sì. Un test verde non sostituisce una
> build pulita: è già capitato che la build trovasse un campo scritto male che i test non vedevano.

> ⚠️ Se un test contraddice quello che vedi succedere davvero, **sospetta la build prima del
> codice**: un file ripristinato da un backup ha un timestamp vecchio e il compilatore non lo
> ricompila. Si risolve con `dotnet build -t:Rebuild`.

Per i soli test del backend esiste la scorciatoia `/test-backend`, che li lancia e riassume l'esito.

### Prova manuale del tetto dei coperti

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

Cancellare tutto e ripartire da zero. **Da fare solo se serve davvero**: in produzione si perdono
anche i dati reali del locale.

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

Stessa sequenza, ma il database è su Neon e **la esegue Fabio**. Neon **ha un indirizzo
pubblico**: si entra direttamente da locale, non serve una sessione remota.

```powershell
psql "postgresql://<utente>:<password>@<host>.neon.tech/<database>?sslmode=require"
```

(la stringa esatta si legge dal pannello Neon, sezione *Connection string* — formato `postgresql://`,
non quello con `Host=;Database=;...` usato da .NET). Da lì: `dotnet ef database update` puntato
alla stessa connection string (via variabile d'ambiente, vedi §7), poi
`Scripts/quartz_postgres.sql` con `psql -f`.

Prima di iniziare: **backup** (`pg_dump`, vedi §6), anche se si sta cancellando apposta. Se
qualcosa va storto a metà si resta con un database mezzo vuoto e nessun modo di tornare indietro.

**Differenze rispetto al locale:**
- **Fermare l'App Service** su Azure prima di iniziare, e riavviarlo alla fine (poi `/health`).
- **Al posto di `dotnet ef database drop`**: `DROP SCHEMA public CASCADE; CREATE SCHEMA public;`
  con `psql -c`. Su Neon il ruolo `neondb_owner` può non riuscire a cancellare il database stesso.
  Porta via anche le tabelle `QRTZ_*`: poi `dotnet ef database update` e `quartz_postgres.sql`.
- **`pg_dump` 17 non funziona con Neon (server 18)**: dà «la versione del server non corrisponde».
  Il client 18 è installato: si chiama con il percorso completo,
  `C:\Program Files\PostgreSQL\18\bin\pg_dump.exe` (procedura in §6). In alternativa un branch
  Neon (*Branches → Create branch*).
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
> È già successo in produzione.

**Senza riga di comando** (la migration 8, il 12/10/2026, è passata dal SQL Editor): backup con un **branch di
Neon** (*Branches* → *Create branch* dal principale, es. `backup-2026-10-12`), poi il contenuto
dello script incollato nel **SQL Editor** di Neon. Incollando il testo il problema del BOM non si
pone. Prima e dopo, controllo su `"__EFMigrationsHistory"`.

> ⚠️ **Se una modifica si applica a mano, usare sempre lo script generato**, non i comandi SQL
> scritti a mano: lo script include anche la riga che registra la modifica come applicata. Senza
> quella riga lo schema è giusto ma Entity Framework continua a considerarla da fare, e un futuro
> aggiornamento proverà a rieseguirla. È già successo.

---

## 5. Pubblicare un rilascio

**Il commit, il push, il merge e il tag li fa Fabio.** Claude fornisce solo il messaggio.

1. Commit su `v2`
2. Merge `v2` → `main`
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

**Come vedere che Azure ha preso l'immagine nuova** (aggiunto il 27/10/2026). Su Docker Hub il tag
`latest` di `fabioiacurto/gestora-api` mostra **«Last pushed»**, l'ora in cui l'Action l'ha pubblicata.
Nel portale Azure, App Service → **Centro distribuzione** (*Deployment Center*) → scheda **Log**: ci
deve essere una distribuzione riuscita **dopo** quell'ora. In alternativa **Flusso di log** (*Log
stream*): durante l'aggiornamento compaiono «Pulling image…» e l'avvio del container. Le ore possono
essere in UTC. Se dopo una decina di minuti non succede niente, il webhook non è partito.
Nel log, `Image … is pulled from registry` vuol dire che Azure ha scaricato davvero un'immagine
nuova; se manca, ha riusato quella che aveva già.

**Comportamento noto: il primo avvio dopo un rilascio fallisce** (visto il 27/10/2026 due volte su
due). Nel log: `Site startup probe failed after 0.x seconds`, `ContainerStartupFailure`, poi
`Site: gestora-api stopped` (si spegne anche il contenitore vecchio). Azure controlla l'app dopo meno
di un secondo, prima che .NET abbia finito di partire. **Non è un errore del codice**: alla prima
richiesta successiva (anche `/health` o il controllo di UptimeRobot) il sito riparte con
l'immagine nuova, il controllo riesce in circa 11 secondi e il log dice `Site started`. Quindi,
dopo un rilascio: aprire `/health` e aspettare `Healthy`. Fino a quella richiesta il backend resta
spento: con la produzione aperta al pubblico va tenuto presente (da rivedere prima della `v3.0.0`).

Quindi: se la modifica è solo di backend, Vercel ricostruisce ma non cambia niente (il frontend
resta lo stesso), e viceversa. La catena backend ha due passaggi automatici in serie (Actions poi
webhook Azure): se `/health` non risponde subito dopo il push, aspettare qualche minuto prima di
sospettare un problema — è la catena che sta ancora girando, non necessariamente un guasto.

**Numerazione:** la v2 si è chiusa con il tag `v2.1.0` (15/10/2026); il prossimo tag è `v3.0.0`,
alla fine del piano della v3 (`BACKLOG.md`). Fino ad allora i rilasci delle singole fasi vanno su
`main` **senza tag**, dietro il «portale in manutenzione» (sotto). In generale: `vX.y.0` per un
gruppo di funzionalità o segnalazioni, `vX.y.z` per fix e correzioni. Un
riordino interno, senza effetti sull'applicazione, può restare **senza tag**.

**Verifica dopo il deploy:**
```powershell
curl https://gestora-api-emdvdqegg7g8gmaq.canadacentral-01.azurewebsites.net/health
```
Deve rispondere `Healthy` — copre anche la raggiungibilità del database (Neon), non solo il
processo. Il frontend si verifica aprendo `https://gestora-project-xi.vercel.app`.

Poi login dal frontend con i tre ruoli.

**Controllo automatico (`V2-018`).** Un monitor **UptimeRobot** (account di Fabio, piano gratuito)
chiama `/health` ogni 30 minuti e manda un'email se non risponde o risponde con un errore. Dopo un
rilascio, il monitor deve tornare verde da solo. Il risveglio ogni 30 minuti fa partire anche i job
notturni entro mezz'ora dall'orario previsto (vedi *Job notturni* in `BACKLOG.md`, *Rischi
accettati*). Per provare l'avviso senza toccare la produzione: un monitor temporaneo su un
indirizzo che non esiste (es. `/health-prova-inesistente`, risponde 404), da eliminare dopo
l'email.

> ⚠️ **Prima di confermare un commit, contare i file inclusi.** `git status` breve raggruppa le
> cartelle nuove e ne mostra meno di quanti ce ne sono davvero; Visual Studio fa lo stesso, e i
> file nuovi vanno spuntati a mano. Per l'elenco reale:
> ```powershell
> git status --untracked-files=all
> ```
> È già successo che un commit lasciasse fuori 12 file nuovi, poi persi.

### I limiti delle prenotazioni su Azure

I due limiti dei coperti per prenotazione stanno in `appsettings.json`, dentro l'immagine:
`MaxCopertiPerPrenotazione` (limite tecnico, 50, per lo Staff) e `MaxCopertiPrenotazioneOnline`
(20, per il Cliente e la pagina pubblica). Su Azure **non serve** impostarli: le variabili
d'ambiente servono solo per cambiare un valore senza un nuovo rilascio.

Se le si usa (*Impostazioni → Variabili di ambiente* dell'App Service, doppio underscore):
- `Prenotazioni__MaxCopertiPerPrenotazione`
- `Prenotazioni__MaxCopertiPrenotazioneOnline`

> ⚠️ All'avvio l'app controlla che entrambi siano maggiori di zero e che **l'online non superi il
> tecnico**: se no **non parte** e `/health` non risponde. Prima di un rilascio, se su Azure esiste
> già `Prenotazioni__MaxCopertiPerPrenotazione`, controllare che valga almeno 20 (o almeno quanto
> `MaxCopertiPrenotazioneOnline`, se è stato cambiato).

### La durata della sessione su Azure

Stessa logica per il token (`V2-010`): `JwtSettings:ExpiryMinutes` (60, durata di un token) e
`JwtSettings:MaxSessionHours` (12, massimo dal login, rinnovi compresi) stanno in
`appsettings.json`. Su Azure **non serve** impostarli; se li si usa:
`JwtSettings__ExpiryMinutes`, `JwtSettings__MaxSessionHours`.

> ⚠️ All'avvio l'app controlla che entrambi siano maggiori di zero e che **un token non duri più
> della sessione** (`ExpiryMinutes` ≤ `MaxSessionHours` × 60): se no **non parte**.

### Le email (conferma dell'indirizzo) su Azure e in locale

Dal `V2-003` chi si registra da solo riceve una email con il link per confermare l'indirizzo, e
senza conferma non entra. L'invio usa SMTP; il fornitore previsto è **Brevo** (piano gratuito),
ma si cambia solo con questi valori. Gli account creati da un Admin (primo Admin del `/setup`,
Staff) nascono già confermati.

| Variabile su Azure (doppio underscore) | Cosa è | Esempio |
|---|---|---|
| `Email__Host` | server SMTP | `smtp-relay.brevo.com` |
| `Email__Porta` | porta | `587` |
| `Email__Utente` | utente SMTP | l'account Brevo |
| `Email__Password` | chiave SMTP (**segreta**) | la chiave SMTP di Brevo, non la password di accesso |
| `Email__MittenteEmail` | indirizzo che compare come mittente | quello verificato su Brevo |
| `Email__MittenteNome` | nome del mittente | `Gestora` (già il predefinito) |
| `Email__UrlFrontend` | indirizzo del frontend, per il link nell'email | `https://gestora-project-xi.vercel.app` (senza `/` finale) |
| `Pareri__Destinatario` | a chi arrivano i pareri del modulo `/pareri` (`V2-022`) | l'indirizzo di chi gestisce Gestora |

> ⚠️ **`Pareri__Destinatario` va creata su Azure prima del rilascio della F4**: fuori da Development
> l'app controlla all'avvio che sia un indirizzo email valido e, se manca, **non parte**. In locale
> può mancare (il parere non parte e lo dice il log); per riceverlo davvero:
> `dotnet user-secrets set "Pareri:Destinatario" "indirizzo"` da dentro `GestoraWebApi\`.

> ⚠️ **Prima le variabili, poi il deploy.** All'avvio l'app controlla che `Email__UrlFrontend` sia un
> indirizzo completo e, fuori da Development, che `Email__Host` ci sia (con `Email__Porta` e
> `Email__MittenteEmail`): se no **non parte** e `/health` non risponde. La password non va in chat
> né nei file: vedi §7 per digitarla senza mostrarla.

**In locale** non serve nessun account: con `Email:Host` vuoto (è così) le email **non partono** e
il contenuto, link compreso, compare nei log della console del backend (`[Email di sviluppo, non
inviata]`). Per provare l'invio vero con Brevo: `dotnet user-secrets set "Email:Host" ...` (e gli
altri valori della tabella, con i nomi con i due punti) da dentro `GestoraWebApi\`.

**Utenti già presenti nel database locale.** Gli account creati prima di `V2-003` non hanno l'email
confermata e non possono più entrare, Admin compreso. Una sola volta, sul database **locale**:

```powershell
psql -U postgres -d gestora_db -c 'UPDATE \"AspNetUsers\" SET \"EmailConfirmed\" = true WHERE \"EmailConfirmed\" = false;'
```

(In alternativa si rifà il database con `dotnet run -- --seed-sviluppo`, §3.) In produzione non
serve: al momento non ci sono utenti.

Il link vale 24 ore. Chi non lo trova può farselo rimandare dalla pagina di accesso. Limite di
richieste su conferma e reinvio: `LimitiRichieste:ConfermaEmailPerFinestra` (10) ogni
`ConfermaEmailFinestraMinuti` (60) per indirizzo.

Il modulo dei pareri (`V2-022`) accetta `LimitiRichieste:PareriPerFinestra` (2) pareri ogni
`PareriFinestraGiorni` (30) per indirizzo; oltre risponde 429 e la pagina dice che i pareri del mese
sono già arrivati. Il contatore sta in memoria: un riavvio del backend lo azzera. Per provarlo in
locale basta mandare tre pareri di fila; per ricominciare, riavviare il backend.

### Il portale in manutenzione (`V2-016`)

Finché la v3 non è rilasciata, la produzione resta chiusa. Il blocco ha **due interruttori**, uno
per parte, e non richiede modifiche al codice:

| Dove | Variabile | Effetto |
|---|---|---|
| Azure App Service (*Impostazioni → Variabili di ambiente*) | `Manutenzione__Attiva` = `true` | ogni chiamata a `/api` risponde **503** «Gestora è in manutenzione: torna presto.»; `/health` resta libero |
| Vercel (*Settings → Environment Variables*, tipo *Config*, ambiente Production) | `VITE_MANUTENZIONE` = `true` | ogni pagina mostra solo «Gestora è in manutenzione» e nessuna chiamata parte verso il backend |

**Accendere:** impostare le due variabili. Azure riavvia l'app da solo al salvataggio; su Vercel
serve una nuova pubblicazione (*Deployments → Create Deployment* con `main`): la variabile entra
nella build, non viene letta a ogni apertura.

**Verificare:**
```powershell
curl https://gestora-api-emdvdqegg7g8gmaq.canadacentral-01.azurewebsites.net/health
```
deve rispondere `Healthy`; una chiamata a `/api` (per esempio `.../api/Setup/stato`) deve
rispondere 503. Aprendo il frontend si vede solo la schermata di manutenzione.

**Spegnere** (al rilascio `v3.0.0`): togliere le due variabili, o metterle a `false`, e ripubblicare
il frontend.

> ⚠️ Spenta di default: in `appsettings.json` `Manutenzione:Attiva` vale `false`, e senza la
> variabile su Vercel il frontend si apre normalmente. In locale non cambia niente.

### Il primo avvio di un locale

Dal `V2-007` il tetto di una fascia non può superare i posti della sala: l'ordine è **zone → tavoli
→ fasce**. Partendo dalle fasce, il salvataggio è rifiutato con «crea prima le zone e i tavoli».

---

## 6. Accedere al database di produzione e fare un backup

Il Postgres di Neon **ha un indirizzo pubblico**: si lavora da locale, non serve entrare in una
sessione remota.

### Il backup di produzione (`V2-018`)

**Quando.** Sempre prima di una migration, di un reset o di un rilascio che tocca il database
(§3, §4). Con il locale aperto, anche **una volta a settimana**. Il primo vero backup si fa al
rilascio della F2, prima della migration 9.

**Dove.** Fuori dal progetto, in
`..\Gestora documentazione\Gestora_backup\` (accanto alla cartella del progetto; i comandi partono da lì), con il
nome `gestora_AAAA-MM-GG.dump`. Un backup contiene **dati personali** (email e nomi dei clienti,
indirizzi IP nel registro attività) e le **chiavi che firmano i link email**: mai su Git, mai in
una cartella condivisa. Si tengono le ultime 4 settimanali più quella fatta prima dell'ultima
migration; le altre si cancellano. Quanto si conservano va anche scritto nell'informativa privacy
della F3.

**Cosa serve.** Il `pg_dump` della versione **18**, come il server di Neon. Sul PC ci sono la 17 e
la 18 e il comando `pg_dump` scritto da solo parte con la 17, che con Neon si rifiuta di lavorare:
per questo nei comandi c'è il percorso completo. Dal pannello Neon serve la *Connection string*
**con *Connection pooling* spento**: l'host **senza** `-pooler` nel nome (`pg_dump` con il pooler
può fallire).

**1. Cartella (solo la prima volta):**
```powershell
New-Item -ItemType Directory -Force "..\Gestora documentazione\Gestora_backup"
```

**2. Backup.** Nella stringa **non** si scrive la password: `pg_dump` la chiede, così non resta
nella cronologia del terminale. Al posto di `<host>` e `<database>` i valori del pannello Neon.
```powershell
& "C:\Program Files\PostgreSQL\18\bin\pg_dump.exe" "postgresql://neondb_owner@<host>.neon.tech/<database>?sslmode=require" -Fc -f "..\Gestora documentazione\Gestora_backup\gestora_2026-10-19.dump"
```
`-Fc` è il formato compresso di PostgreSQL: si legge solo con `pg_restore`, ma permette di
ripristinare anche una tabella sola.

**3. Controllo veloce** che il file sia leggibile e contenga le tabelle (elenco del contenuto,
non tocca nessun database):
```powershell
& "C:\Program Files\PostgreSQL\18\bin\pg_restore.exe" --list "..\Gestora documentazione\Gestora_backup\gestora_2026-10-19.dump" | Select-String "TABLE DATA"
```
Devono comparire le tabelle di Gestora (`Prenotazioni`, `Utenti`, `DataProtectionKeys`,
`__EFMigrationsHistory`…) e quelle `qrtz_*` di Quartz.

**Senza riga di comando:** un **branch di Neon** (*Branches* → *Create branch* dal principale, es.
`backup-2026-10-19`). Va bene come rete di sicurezza prima di una migration, ma **non sostituisce
il file**: resta sullo stesso fornitore, e se si perde il progetto Neon si perde anche lui.

**La «macchina del tempo» di Neon** (*instant restore*: riportare il database a un momento
passato) sul piano gratuito copre poche ore: controllare la durata in *Settings* del progetto.
Serve per un errore appena fatto, non come backup.

### Prova di ripristino (in locale)

Un backup mai ripristinato non si sa se funziona. Si prova **la prima volta** e poi ogni qualche
mese, su un database **apposta**, `gestora_ripristino`, sul server locale 18 (porta 5432).
**Mai su `gestora_db`**, che è il database di sviluppo.

1. Database vuoto per la prova:
```powershell
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -p 5432 -c 'CREATE DATABASE gestora_ripristino;'
```
2. Ripristino. `--no-owner --no-acl` perché il proprietario su Neon (`neondb_owner`) in locale non
   esiste: le tabelle diventano di `postgres`.
```powershell
& "C:\Program Files\PostgreSQL\18\bin\pg_restore.exe" -U postgres -p 5432 -d gestora_ripristino --no-owner --no-acl "..\Gestora documentazione\Gestora_backup\gestora_2026-10-19.dump"
```
   Non deve stampare errori. Un avviso su un'estensione o un ruolo propri di Neon si può ignorare;
   un errore su una tabella di Gestora no.
3. Controllo: le migration registrate devono essere le stesse di Neon (l'elenco è nel foglio
   *BE · Migration*, colonna *Produzione*), e le tabelle devono esserci tutte.
```powershell
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -p 5432 -d gestora_ripristino -c 'SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY 1;'
```
```powershell
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -p 5432 -d gestora_ripristino -c '\dt'
```
4. Si cancella il database di prova:
```powershell
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -p 5432 -c 'DROP DATABASE gestora_ripristino;'
```

Per ripristinare **davvero** la produzione si usa lo stesso `pg_restore`, verso Neon e con
l'App Service fermo, dopo aver svuotato lo schema come in §3 (*Produzione*). È un'operazione da
decidere insieme, mai da fare di corsa.

### Una tabella sola

Per una singola tabella (più veloce da controllare a occhio):
```powershell
psql "postgresql://<utente>:<password>@<host>.neon.tech/<database>?sslmode=require" -c '\copy "FasceOrarie" TO ''backup_FasceOrarie.csv'' CSV HEADER'
```

> ⚠️ I file di backup **non vanno mai committati**. Il `.gitignore` di radice li esclude già
> (`backup_*.csv`, `backup_*.dump`, `*.dump`, `env*.txt`), ma controllare prima del commit. È già
> successo che file di backup finissero in un repository pubblico, e la storia di Git ha dovuto
> essere riscritta; lo stesso con un file di credenziali di connessione (`envDBNeon.txt`).
> Principio: qualsiasi cosa con dentro una password sta fuori da Git.

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
> vecchia. Già successo, insieme a un secondo errore simile: variabili lasciate come testo
> segnaposto invece del valore vero.

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
> `Read-Host` da solo **mostra la password a schermo**. È già successo che una password di
> produzione finisse così in chat e andasse sostituita.

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
- Le date vanno scritte come date vere (`[datetime]::new(2026,10,5)`), non come stringhe:
  scritte come testo vengono lette all'americana e il giorno diventa il mese
- Per copiare la formattazione di una riga: `Rows.Item(x).Copy(Rows.Item(y))`, mai `PasteSpecial`
- Il colore di una cella si legge come numero, non come testo: per riusare un colore esistente
  si legge da una cella che ce l'ha già, invece di scriverlo a mano

### Azure App Service e Vercel: le trappole della configurazione

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
- **L'indirizzo del backend sta anche in `gestora-frontend/vercel.json`** (dal `V2-018`), nella
  regola `connect-src` della Content-Security-Policy. Se il backend cambia indirizzo, va cambiato
  lì oltre che in `VITE_API_URL`: altrimenti il sito si apre, ma il browser blocca ogni chiamata
  all'API (in console: «Refused to connect ...»).
- **SCM Basic Auth necessaria per il webhook di distribuzione continua.** Senza, Azure non
  accetta la notifica che arriva da Docker Hub quando c'è una nuova immagine, e il backend resta
  fermo alla versione precedente finché non si riavvia a mano dal pannello.
- **Vercel può non ripubblicare dopo il push su `main`.** Il 12/10/2026, dopo il merge della PR #4
  fatto dalla pagina di GitHub, il backend si è aggiornato ma su Vercel non è comparsa nessuna
  pubblicazione. *Create Deployment* ha poi dato `403 ... secondary rate limit` (un blocco
  temporaneo di GitHub verso Vercel); dopo circa 15 minuti di attesa è andato. Dopo ogni rilascio
  che tocca il frontend, controllare in *Deployments* che ci sia una pubblicazione del commit
  appena mandato su `main`. Se manca: *Deployments* → **⋯** → *Create Deployment* con `main`.
  Mai *Redeploy* su una pubblicazione vecchia: ricostruisce il codice vecchio.

### Gli indirizzi degli endpoint

La rotta base è `/api/[nome del controller senza "Controller"]`.
Quindi `AuthenticationUserController` risponde su `/api/AuthenticationUser/...` — **non**
`/api/AuthenticationUserController/...` e **non** `/api/Auth/...`.

Eccezione da ricordare: le fasce orarie rispondono su `/api/FasceOrarie/...` anche se la classe
si chiama `FasciaOrariaService`. È il naming incoerente lasciato apposta (vedi `BACKLOG.md`,
sezione *Rischi accettati e cose decise di non fare*).

---

## 9. Sicurezza

### Il token nel browser, spiegato in chiaro

La domanda: se il token di accesso (JWT) si legge dagli strumenti sviluppatore del browser
(F12 → Application → Local Storage), chiunque arrivi a quella schermata non ottiene forse
l'accesso come quell'utente?

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

1. **La scadenza**, 60 minuti (`JwtSettings:ExpiryMinutes`) — con `ClockSkew = TimeSpan.Zero`
   scade esattamente a 60 minuti, non a 65. Dal `V2-010` il frontend lo rinnova da solo 10 minuti
   prima, finché la pagina è aperta (`POST api/AuthenticationUser/rinnova-token`), ma mai oltre
   **12 ore dal login** (`JwtSettings:MaxSessionHours`): poi si rifà l'accesso. Un token copiato
   si potrebbe quindi rinnovare fino a quel limite; a chiudere davvero una sessione resta il
   security stamp, che respinge subito i token di un utente modificato o eliminato.
2. **Il logout**, che cancella il token e svuota anche la cache di React Query
   (`AppLayout.handleLogout` e il gestore di sessione scaduta fanno entrambi le due cose insieme),
   così non restano dati del vecchio utente in memoria dopo l'uscita.

**Idea** (non una correzione): passare a cookie `HttpOnly` con protezione CSRF è una
riprogettazione dell'autenticazione, non un fix — vedi `BACKLOG.md`, sezione *Idee*.
