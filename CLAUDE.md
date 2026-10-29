# Gestora

> **Documento di ingresso del progetto.** Si legge a inizio di ogni sessione.
> Cosa è aperto → `BACKLOG.md` · Come si fa una cosa → `RUNBOOK.md`

---

## 1. Stato — aggiornato al 28/10/2026

**Verso la v3.0.0, production ready** (decisione del 15/10/2026): Gestora va resa pronta per
chiunque, con privacy, sicurezza, modulo dei pareri e vetrina. Piano in sei fasi (F0-F5:
`V2-016`…`V2-019`, `V2-022`, `V2-023` in `BACKLOG.md`), nello
[studio di fattibilità](https://claude.ai/code/artifact/f31f316c-733c-4a5e-a93f-2f139c0518c1):
circa 37 ore a 3-4 ore a settimana, fino a fine gennaio 2027. La v2 è chiusa con il tag `v2.1.0`
e la produzione è in **«portale in manutenzione»** dal 15/10/2026 fino al rilascio `v3.0.0`
(`RUNBOOK.md` §5). **Il piano si segue dal foglio *Task v3* del tracker** (§8): il prossimo passo è in
cima alla Dashboard.
A v3.0.0 chiusa si decide se fare la **v3.1.0**, una demo interattiva `/demo` per i visitatori
(nota in `BACKLOG.md`).
Backend su **.NET 10 LTS** in produzione dal 16/10/2026 (`V2-017`), prima della fine del supporto
a .NET 9 (10/11/2026).

**Gestora v2** dal 05/10/2026. Gestora è pensata per un locale vero, nei turni di
servizio: le segnalazioni che arriveranno da lì, insieme alle nuove richieste, sono il lavoro della
v2 (vedi §4).

**Produzione chiusa per manutenzione** dal 15/10/2026: il sito mostra solo «Gestora è in
manutenzione», ogni `/api` risponde 503, `/health` risponde `Healthy` (variabili
`VITE_MANUTENZIONE` su Vercel e `Manutenzione__Attiva` su Azure). Il primo avvio (`/setup`) non è
stato fatto e su Neon non ci sono zone, tavoli, fasce né prenotazioni: si fa al rilascio `v3.0.0`.

| | |
|---|---|
| Backend | `https://gestora-api-emdvdqegg7g8gmaq.canadacentral-01.azurewebsites.net` — Azure App Service (F1), distribuzione continua da Docker Hub attiva |
| Frontend | `https://gestora-project-xi.vercel.app` — Vercel, punta al backend Azure |
| Database | PostgreSQL su Neon (gratuito permanente): 10 migration EF + tabelle Quartz |
| Test | **492** backend (xUnit) + **133** frontend (Vitest), tutti verdi |
| Modifiche al database | 10, tutte applicate in locale e su Neon (l'ultima, `PrivacyEAccount`, `V2-019`, su Neon il 27/10/2026). Elenco nel foglio *BE · Migration* del tracker |
| Vulnerabilità note nelle librerie | 1 — AutoMapper 12.0.1, accettata (vedi `BACKLOG.md`, *Rischi accettati*) |
| Branch | si lavora su **`v2`**; `main` è quello pubblicato in produzione (ultimo tag **`v2.1.0`**, 15/10/2026, chiusura della v2; le fasi della v3 vanno su `main` senza tag fino a `v3.0.0`) |

**Ultima cosa fatta** (27/10/2026): **chiusa la F4 del piano v3** (`V2-022`, modulo dei pareri),
sviluppata da Claude. Pagina pubblica `/pareri` (link dalla vetrina e dal menu utente) e
`POST /api/Pareri/invia`: il parere arriva via email a `Pareri:Destinatario` (Brevo) e non si salva nel
database; campo trappola contro i programmi automatici; limite di **2 pareri in 30 giorni per
indirizzo**, deciso da Fabio al posto dei 10 all'ora (contatore in memoria, un riavvio lo azzera);
informativa con i pareri, versione `2026-10-27`. Prova a vista di Fabio superata, commit `d2b2b1e`
su `v2`. Rilascio: prima la variabile `Pareri__Destinatario` su Azure, poi merge su `main` `c7acaf7`;
controllati l'immagine nuova scaricata, `/health` Healthy, `/api` ancora 503, Vercel *Ready*. Nessuna
migration.

**F5 in corso** (28/10/2026, `V2-023`, vetrina e rilascio `v3.0.0`, sviluppa Claude): passi nel
foglio *Task v3*. Fatti `T02`…`T05`: `README.md` in inglese con screenshot (`docs/screenshots/`, dati
del nuovo `--seed-vetrina`), `LICENSE` «All rights reserved» (codice solo da guardare), `CHANGELOG.md`,
anteprima social (`og-image.png`, tag `og:`/`twitter:`), `robots.txt` e `sitemap.xml` (indicizzate
solo `/`, `/pareri`, `/privacy`, `/termini`), favicon riportata al blu. Fatto anche `T10`: il
repository su GitHub è **pubblico**, quindi tolti i percorsi personali dai documenti versionati; i
documenti di lavoro restano pubblici. Tolto il doppione `GestoraWebApi/Gestora.sln` e il tag locale
`2.1.0`. Per sicurezza Fabio ha ruotato la password di Neon (quella vecchia era nella storia Git).
Commit `d4dea65` su `v2`. Lo stesso giorno **audit di sicurezza** (`SEC-001`…`SEC-009`, esito in
chat): nessun segreto attivo esposto; da lì `V2-026` (niente dati sulla salute nelle note, fatto) e
due rischi prima accettati, riaperti da Fabio e da fare **prima** della `v3.0.0`: `V2-027` (blocco
dell'Admin) e `V2-028` (token in cookie `HttpOnly`). Restano da valutare `SEC-003`…`SEC-009`.
Il 29/10/2026 fatto `V2-027` (sviluppa Claude): le password sbagliate si contano per email +
indirizzo (`TentativiAccesso`), non più per account; provato dal vivo (l'indirizzo che sbaglia riceve
423, l'Admin da un altro indirizzo entra). Prove a vista di Fabio superate (`T06`, `V2-026`, `V2-027`).
`V2-028` analizzata e **rimandata a dopo la `v3.0.0`** (Safari blocca i cookie fra siti diversi:
serve il passaggio da Vercel o un dominio proprio). **Prossimo passo: `V2-023-T08`**, rilascio
`v3.0.0` (prima, se Fabio vuole, `SEC-003`…`SEC-009` dell'audit).

**Prima** (22-27/10/2026): **chiusa la F3 del piano v3** (`V2-019`, privacy e account),
sviluppata da Claude. Migration 10 `PrivacyEAccount`; informativa privacy e termini (`/privacy`,
`/termini`); presa visione in registrazione e al primo accesso; recupero password con link a 2 ore;
«Il mio account» (`/account`) con cambio password e cancellazione dell'account (prenotazioni passate
anonime, bloccata con prenotazioni in programma o per l'ultimo Admin); registro attività a 24 mesi
(job alle 3:30); account inattivi (email di avviso 4 settimane prima, eliminazione a 24 mesi).
L'export dei propri dati per l'utente (`T07`) è stato fatto e poi tolto: al suo posto la voce nuova
**`V2-025`**, report CSV delle prenotazioni per l'Admin. Ogni passo provato a vista da Fabio, i due
job con i dati di `GestoraWebApi/Scripts/prove-f3`. Commit `1927a5c` su `v2`. Rilascio il 27/10:
backup con un branch di Neon (`backup-2026-10-27`), migration 10 dal SQL Editor (script idempotente
della sola migration 10), merge su `main` `a909ac9`; controllati l'immagine nuova scaricata da Azure,
`/health` Healthy e `/api` ancora 503. Il 27/10 anche `V2-025-T01`, report CSV delle prenotazioni
per l'Admin, sviluppato da Claude e provato a vista da Fabio: commit `a569dee`, merge su `main`
`839939c`, in produzione (nessuna migration). Dal log di Azure: il primo avvio dopo un rilascio
fallisce e il sito riparte alla prima richiesta (comportamento noto, `RUNBOOK.md` §5).
Prima della F3 (22-26/10/2026) **chiuso il bug `V2-024`** (disponibilità), sviluppato da Claude. Con un
tavolo da 8 occupato da 5 persone la verifica diceva «restano 3 posti», ma erano il resto di quel
tavolo: la prenotazione poi falliva. Ora i posti rimasti sono il minore fra tetto residuo e gruppo
più grande che i tavoli liberi accolgono (`AssegnazioneTavoli.GruppoMassimo`, decisione 8 precisata)
e nel form dello Staff «Salva» è spento in creazione su una fascia non disponibile. Commit `e9213f2`,
merge su `main` `7b4ae5f` il 26/10. Nessuna migration.

**Prima ancora** (20/10/2026): **chiusa la F2 del piano v3** (`V2-018`, robustezza),
sviluppata da Claude: chiavi Data Protection nel database (migration 9 `ChiaviDataProtection`),
header di sicurezza nel backend (`SicurezzaHeaderMiddleware`) e su Vercel (`vercel.json`, CSP),
procedura di backup in `RUNBOOK.md` §6. Passi precisati il 19/10/2026, prova a vista in locale
superata, commit `30cfc95` su `v2`. Rilascio di Fabio in fila stretta: primo backup vero di Neon
(`gestora_2026-10-20.dump`, controllato con `pg_restore --list`), migration 9 dal SQL Editor di
Neon (script idempotente della sola migration 9), poi merge su `main` `112ef84`. Controllati
`/health` Healthy, gli header online (backend con HSTS e senza `Server`, frontend con CSP),
`/api` ancora 503, la chiave salvata in `DataProtectionKeys` su Neon. Poi `V2-018-T09`: monitor
UptimeRobot (account di Fabio, piano gratuito) su `/health` ogni 30 minuti con avviso via email,
verde.

**Ancora prima** (16/10/2026): **chiusa la F1 del piano v3** (`V2-017`, passaggio a .NET 10),
sviluppata da Claude: API e test su `net10.0`, pacchetti a 10.x, Swashbuckle 10 (Microsoft.OpenApi
2.x), MockQueryable 10, `System.Security.Cryptography.Pkcs` fissato a 10.0.13 per gli avvisi di
sicurezza portati da MimeKit, Dockerfile su `sdk`/`aspnet` 10.0 (Ubuntu 24.04), CI su `10.0.x`.
Nessuna migration. Commit `f6aeeda` su `v2`, merge su `main` `0fb6c52`; immagine scaricata da
Azure alle 09:45 UTC, `/health` Healthy e `/api` ancora 503 (manutenzione).

**Più indietro** (15/10/2026): **chiusa la F0 del piano v3** (`V2-016`). Studio di fattibilità
e piano, voci in `BACKLOG.md`, foglio *Task v3* nel tracker con la colonna «Ordine» e il «Prossimo
passo» in Dashboard. Tag `v2.1.0` su `main` (PR #6, `7823a85`). «Portale in manutenzione»
sviluppato da Claude (commit `f5cdbe1`, PR #7, `42d7103`): prima le variabili su Azure e Vercel, poi
il merge; controllati `/health`, il 503 su `/api` e la schermata del sito. Analisi per .NET 10
fatta (`V2-017-T01`, esito nelle note del task).

**Ancora più indietro** (14/10/2026): rilascio in produzione, senza tag, di **`V2-003`**
(conferma dell'email alla registrazione), sviluppata da Claude; nessuna migration. Il commit
`086db11`, intitolato «invio email conferma registrazione», contiene tutta la voce; merge con la
PR #5 (`915c748`). Prima le variabili `Email__*` su Azure, poi il merge. Provata a vista in locale
e con una email reale via Brevo; in produzione sono stati controllati `/health`, l'endpoint nuovo e
il frontend pubblicato, ma **la registrazione completa non è stata provata**, per non sporcare un
database ancora senza primo Admin. Resta da fare in produzione il `/setup`. Non c'è un ambiente
di collaudo (valutato e scartato il 14/10/2026).

**Prima della F0** (12-13/10/2026): il 13/10 rilascio in produzione, senza tag, di **`V2-013`** (limite di
200 caratteri su `NomeCliente`, backend e form, commit `ceb1d68`) e **`V2-014`** (registro
attività: eliminazione di prenotazioni e utenti nella stessa transazione della traccia, modifica
di nome/email utente registrata, commit `22c7625`), sviluppati da Claude; nessuna migration. Con
questi si chiude la code review dell'08/10/2026. In giornata anche **`V2-012`** (spegnere una zona
con prenotazioni da servire è bloccato), commit `1ff6c57`, merge `b2d0e88`.
Il 12/10 rilascio in produzione, senza tag, di **`V2-011`** (limiti di
richieste su registrazione e verifica della disponibilità) e **`V2-015`** (pulizia notturna delle
utenze mai usate), entrambi sviluppati da Claude, commit `73b7a95`, merge con la PR #4
(`94a9f8e`). Prima la migration 8 su Neon, poi il deploy. Il frontend Vercel è stato pubblicato a
mano (*Create Deployment*): la pubblicazione automatica non era partita (vedi `RUNBOOK.md` §8).
Prima ancora, il 09/10/2026, `V2-009` e `V2-010`, e l'08/10/2026 il rilascio **`v2.0.0`**. I
punti 1–4 della code review dell'08/10/2026 sono `V2-008`…`V2-011`; i punti 5–7 sono
`V2-012`…`V2-014`, tutti in produzione. I rilasci li decide Fabio (`RUNBOOK.md` §5); `dev` non si usa
più.

Per dati su cui provare: `dotnet run -- --seed-sviluppo` da `GestoraWebApi` (casi limite, per
mettere in difficoltà l'interfaccia). Per screenshot e dimostrazioni: `dotnet run -- --seed-vetrina`
(una pizzeria credibile, tavoli assegnati dal motore vero; `V2-023`). Entrambi cancellano i dati di
dominio locali e partono solo su un database di questa macchina.

---

## 2. Cos'è Gestora

Applicativo per gestire l'organizzazione di un'attività commerciale con posti a sedere —
ristorante, pub, pizzeria.

Fa tre cose:
1. **Configurare la sala**: zone, tavoli e loro capienza, fasce orarie con un tetto di coperti
2. **Prendere prenotazioni**, sia dallo staff (anche al telefono) sia dal cliente da solo
3. **Assegnare il tavolo da solo**, unendo più tavoli quando serve, scegliendo la combinazione
   che spreca meno posti

Tre ruoli: **Admin** (configura tutto e gestisce gli utenti), **Staff** (prenotazioni e
dashboard), **Cliente** (solo le proprie prenotazioni).

Un utente può avere **più ruoli insieme** — è normale, non un errore.

---

## 3. Com'è fatto

### Struttura

**A livelli, dentro un progetto solo**: `Controller → Service → Repository → database`.
Non è "Clean Architecture" né "DDD". Per un progetto di questa dimensione la struttura a livelli
è la scelta giusta.

```
Gestora/
├── GestoraWebApi/        backend (.NET) — vedi il suo CLAUDE.md
├── GestoraWebApi.Tests/  test del backend (xUnit)
├── gestora-frontend/     frontend (React) — vedi il suo CLAUDE.md
├── docs/archivio/        documenti chiusi, sola lettura
├── Gestora.sln           soluzione .NET: API + test, si apre questa in Visual Studio
├── global.json           versione dell'SDK .NET
├── CLAUDE.md             questo file
├── BACKLOG.md            cosa resta da fare
├── RUNBOOK.md            come si fanno le operazioni
└── TrackGestora.xlsx     il tracker (solo locale, non versionato)
```

In radice stanno solo i documenti vivi. Appunti personali, credenziali e backup **non vanno
messi in questa cartella**, nemmeno ignorati da Git.

### Backend — ASP.NET Core 10

Entity Framework Core 10 su PostgreSQL, autenticazione con ASP.NET Identity + token JWT.
In più: **Quartz** per i quattro lavori notturni programmati, **FluentValidation** per i controlli
sui dati in ingresso, **AutoMapper**, **Serilog** per i log, cache in memoria.
Test con **xUnit + Moq**. Deploy con **Dockerfile** (immagine su Docker Hub, Azure la scarica da
solo).

### Frontend — React 19 + TypeScript + Vite

**React Router v7** (attenzione: qualsiasi nota che parli di v6 è sbagliata), **TanStack Query
v5** per le chiamate al server, **React Hook Form + Zod** per i form, **shadcn/ui + Tailwind CSS
v4** per l'aspetto, **Axios** con un intercettore che allega il token.
Tavolozza propria (un solo blu di marca, neutri freddi) con tema chiaro/scuro, verificata per
leggibilità con `gestora-frontend/scripts/contrasto.mjs`. Direzione visiva ispirata a Docker Hub
(barra superiore fissa, bordi sottili, raggio contenuto), carattere Inter.
Test con **Vitest + Testing Library**. Deploy su **Vercel**, che ripubblica da solo a ogni push
su `main` che tocca `gestora-frontend/**`.

**`/` è pubblica**: presenta il locale e lascia controllare la disponibilità senza registrarsi.
⚠️ Su quella pagina non c'è nessun token: gli unici endpoint chiamabili sono `check-disponibilita`
e `limiti-prenotazione`.

### Due ambienti separati in modo stabile

- **Locale**: frontend su `localhost:5173`, backend su `localhost:5099`, database PostgreSQL
  sulla macchina. Il frontend locale legge `.env.local` (non versionato).
- **Produzione**: Vercel (frontend) + Azure App Service (backend) + Neon (database). **Non legge
  mai** `.env.local`: usa la propria variabile `VITE_API_URL` impostata nel pannello Vercel (deve
  essere di tipo *Config*, non *Secret* — vedi `RUNBOOK.md` §8).

Cambiare `.env.local` non ha nessun effetto sulla produzione. Aprire `localhost:5173` mostra
sempre i dati del database locale: è voluto.

⚠️ La produzione non è ancora in uso (vedi §1), ma il locale può fare il primo avvio in qualsiasi
momento e da lì i dati sono veri: niente reset del database di produzione e niente prove "sporche"
su Neon senza averlo deciso insieme.

---

## 4. Come lavoriamo

### Da dove arriva il lavoro

1. **Segnalazioni dal locale**: problemi, dubbi, richieste che emergono nei turni. Si annotano
   così come arrivano in `BACKLOG.md`, sezione *Segnalazioni dal locale*.
2. **Nuove richieste e idee nostre**: miglioramenti, refactoring, attività di studio.

Ogni attività presa in mano diventa una voce `V2-xxx` in `BACKLOG.md`. Nessuna soluzione già
presente è intoccabile: si valuta caso per caso.

### La regola fondamentale: prima si decide chi sviluppa

La v2 è anche un percorso di crescita come sviluppatore full stack (struttura del progetto,
backend, frontend, database, Entity Framework, API, architettura, gestione degli errori,
debugging, refactoring, test, Git). Claude **non è un semplice esecutore**.

Per **ogni** nuova richiesta, bug, segnalazione o modifica, **prima di toccare qualsiasi file**
Claude chiede come affrontarla, proponendo esplicitamente le due strade:

**1. La sviluppo io** — Claude fa da senior developer che guida:
- spiega cosa va modificato, dove e **perché** (il motivo tecnico)
- procede a **piccoli passi**, con le istruzioni per eseguire di persona ogni passaggio
- controlla insieme il risultato di ogni passo prima di andare avanti
- **non modifica il codice al posto mio**, salvo quando lo chiedo esplicitamente

**2. La sviluppi tu** — Claude implementa, ma prima:
- analizza la richiesta e il codice coinvolto
- individua dipendenze e conseguenze sulle altre parti del progetto
- spiega in breve l'approccio che intende usare
- poi implementa, verifica il risultato e dice chiaramente cosa ha modificato

La scelta si scrive nella colonna *Chi sviluppa* della voce in `BACKLOG.md`.

---

## 5. Decisioni di prodotto

Sono le regole con cui funziona Gestora oggi. **Valgono finché una richiesta non le riapre**: se
succede, si decide insieme e la nuova regola si scrive qui, al posto della vecchia. Il testo
originale con le motivazioni è in `docs/archivio/`.

1. **La capienza della fascia oraria è in coperti**, non in numero di prenotazioni (campo
   `MaxCoperti`)
2. **Le modifiche al database si applicano a mano**: Claude prepara, Fabio applica seguendo il
   `RUNBOOK.md`
3. **Bonus dei posti di testata solo per unioni di soli tavoli da 2** (2 tavoli = 6 posti,
   3 tavoli = 8). Per qualsiasi unione mista, capienza = somma semplice
4. **Vince sempre la combinazione con meno posti sprecati**, tavolo singolo o unione che sia.
   Si uniscono quanti tavoli servono (nessun limite dal 07/10/2026, `V2-007`), tutti della stessa
   zona; a parità di spreco, meno tavoli
5. **La capienza di un tavolo è un numero qualsiasi da 1 in su**
6. **Il primo amministratore si crea da una schermata di primo avvio**, non da un endpoint pubblico
7. **I tavoli si creano a mano** dall'Admin (la creazione automatica è un'idea in `BACKLOG.md`)
8. **È il tetto della fascia a decidere quando è esaurita**, non il numero di tavoli. Ma il tetto
   di una fascia attiva **non può superare i posti della sala** (tavoli attivi nelle zone attive,
   somma semplice senza bonus testate): si controlla salvando o attivando una fascia e modificando
   tavoli o zone, e la modifica che porterebbe i posti sotto un tetto è bloccata. Quindi prima i
   tavoli, poi le fasce (`V2-007`). I **posti rimasti** mostrati sono il minore fra il tetto
   residuo e il gruppo più grande che i tavoli liberi accolgono (`V2-024`, 22/10/2026)
9. **La pagina Postazioni mostra in cima il riepilogo della sala**: è solo informativo
10. **"Una prenotazione al giorno" per il Cliente è un controllo dell'applicazione**, non un
    vincolo del database
11. **Online si prenota fino a 20 persone** (Cliente e pagina pubblica): oltre, «contatta
    direttamente il ristorante». Lo Staff, che prende anche le tavolate al telefono, arriva al
    limite tecnico di 50. Entrambi in configurazione (`V2-007`)
12. **Chi si registra da solo deve confermare l'email prima di entrare** (`V2-003`, in produzione dal
    14/10/2026): riceve un link valido 24 ore e fino ad allora il login
    risponde 403. Gli account creati da un Admin (primo Admin del `/setup`, Staff) nascono già
    confermati. Le email partono via SMTP (Brevo); in locale finiscono nei log
13. **Gestora non raccoglie dati sulla salute** (`V2-026`, 28/10/2026): allergie e intolleranze non
    si scrivono nelle note della prenotazione, si comunicano al locale. Lo dicono il form e
    l'informativa. Riaprire questa regola vuol dire consenso esplicito e informativa nuova

---

## 6. Cosa è aperto

Il dettaglio aggiornato è in **`BACKLOG.md`**; i difetti singoli nel foglio **Fix e Bug** del
tracker.

---

## 7. Regole non negoziabili del progetto

1. **Commit e push li fa sempre Fabio, mai Claude.** Claude fornisce il messaggio di commit con
   l'elenco delle modifiche. Formato: `feat: descrizione` oppure `feat: WIP - descrizione` se
   incompleto (`fix:`, `docs:`, `chore:`, `refactor:`, `test:` per gli altri tipi).
2. **La produzione non si tocca in autonomia.** Database, variabili d'ambiente, deploy: si
   preparano le istruzioni e le esegue Fabio.
3. **Operazioni delicate** (`.env`, `.gitignore`, credenziali, variabili d'ambiente, rimozione di
   file dal tracciamento Git): Claude spiega cosa succede e perché, poi passa le istruzioni a
   Fabio. Non le esegue.
4. **Controllare il branch** a inizio di ogni blocco di modifiche, non solo a inizio sessione:
   si lavora su **`v2`**, mai direttamente su `main`.
5. **Comandi PowerShell su una riga sola**: i blocchi su più righe si concatenano male nel suo
   terminale.
6. **Prima si decide chi sviluppa, poi si procede** (§4). Nessun file si modifica appena arriva
   una richiesta.

---

## 8. Il tracker

**`TrackGestora.xlsx`**, in questa cartella, è il tracker unico e ufficiale. **Solo locale**: è nel
`.gitignore` e non sta nel repository (dal 07/10/2026), quindi va salvato a parte. Aggiornamento
manuale, nessuna automazione.

Una **Dashboard** con indice cliccabile e conteggi automatici, poi un foglio per ogni parte del
progetto, con backend e frontend separati (prefisso `BE ·` e `FE ·`: Models, DTO, Repository,
Services, Controllers, Method, Validators, Infrastruttura, Test, Auth e Security, Jobs, Migration
· Pagine, Componenti, Hook e API, Auth, Test). **Appunti e Step**, **Fix e Bug**, **Task v2** e
**Task v3** si compilano a mano; in *Fix e Bug* si usa la stessa sigla `V2-xxx` di `BACKLOG.md`, nei
fogli dei task ogni voce è divisa in attività `V2-xxx-Tnn` con categoria, dipendenze, criteri di
accettazione e stato. Quando il codice cambia (un
endpoint, un test, una migration) va aggiornato il foglio corrispondente.

**Il piano si segue da un solo posto: il foglio Task v3** (dal 15/10/2026). La colonna **Ordine**
(10, 20, 30…: si può inserire un passo in mezzo senza rinumerare) dà la sequenza; il **prossimo
passo** è la prima riga, in ordine, non *Fatto* né *Bloccato*, e la Dashboard lo mostra in cima
(«Prossimo passo»). A inizio sessione Claude legge *Task v3* e dice qual è il prossimo passo; a
fine passo lo segna *Fatto* con date e nota. Quando si apre una fase, il suo «Precisare la
richiesta» diventa i passi veri, già con il loro numero d'ordine. Un'urgenza (segnalazione dal
locale) entra con un numero prima degli altri. *Task v2* è lo storico della v2: non si modifica,
restano lì solo `V2-004` e `V2-005` in pausa. Il tracker non sta su Git: a ogni fase chiusa se ne
salva una copia con la data in `Progetti_Tech\Personali\Gestora documentazione\Gestora_tracker_copie\` (fuori dal progetto;
prima copia: `TrackGestora_2026-10-15_F0-chiusa.xlsx`).

**Cosa vuol dire lo Stato in ogni foglio** (dall'08/10/2026). Una cosa da fare si scrive come
aperta in **un solo posto**, il foglio dei task: così riaprendo il file si vede subito cosa manca.
- **Task v3** (e *Task v2* per lo storico): l'unico foglio che segue il lavoro ancora da fare,
  rilascio compreso. Ogni passo è un task: *Da fare*, *In corso*, *Fatto*; *Bloccato* per una voce
  scartata o in pausa, con il motivo nelle note.
- **Fix e Bug**: *Risolto* quando il difetto è corretto e verificato, anche se è solo su `v2`.
  Se manca il rilascio lo si scrive nella soluzione, con il task che lo segue (es. «su v2,
  rilascio nel task V2-009-T07»), non nello stato. *Aperto* = non ancora corretto.
- **Appunti e Step**: diario di cosa si è fatto quel giorno, quindi la riga è *Fatto*. Quello che
  resta si legge in «Prossimo passo». *Da fare* solo per un'idea annotata e mai iniziata.
- **Date** scritte via script: in formato `2026-10-08`. Come testo «08/10/2026» Excel le legge
  all'americana (10 agosto).

---

## 9. Dove sta cosa

| Cerchi... | Vai in... |
|---|---|
| Stato del progetto, regole di lavoro, decisioni di prodotto | questo file |
| Cosa resta da fare, segnalazioni dal locale | `BACKLOG.md` |
| Come si resetta il database, si applica una migration, si pubblica | `RUNBOOK.md` |
| Endpoint, architettura e note del backend | `GestoraWebApi/CLAUDE.md` |
| Pattern, routing e note del frontend | `gestora-frontend/CLAUDE.md` |
| Mappa del progetto foglio per foglio, diario, difetti, task | `TrackGestora.xlsx` (solo locale) |
| Storico delle versioni precedenti | `docs/archivio/` (indice nel suo `README.md`) |

### Il grafo del codice (graphify)

In `graphify-out/` c'è una mappa del codice generata automaticamente. Per domande sul codice
conviene partire da lì invece di cercare a mano nei file:

- `graphify query "<domanda>"` — la porzione di codice che riguarda la domanda
- `graphify path "<A>" "<B>"` — come sono collegate due cose
- `graphify explain "<concetto>"` — spiegazione mirata

Dopo aver modificato il codice: `graphify update .` (non costa nulla, legge solo i file).
