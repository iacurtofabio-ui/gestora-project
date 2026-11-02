# Gestora v2 — cosa resta da fare

Aggiornato il **02/11/2026**. Questo è **l'unico elenco valido** delle cose aperte: se una cosa
non è scritta qui, non è in programma.

Il foglio *Fix e Bug* del tracker resta il registro dettagliato dei difetti, il foglio *Task v3* la
sequenza dei passi da fare (il prossimo è in cima alla Dashboard); questo file è la
vista d'insieme che si guarda per decidere cosa fare.

---

## Come si legge

**Sigla**: una sola sequenza, `V2-001`, `V2-002`… Non si riusa mai un numero, nemmeno se una voce
viene scartata. Dal 02/11/2026, con il piano di chiusura e il branch `v3`, le voci nuove (difetti
del collaudo compresi) proseguono con `V3-000`, `V3-001`…; le `V2-xxx` restano lo storico.

**Tipo**: `bug` (qualcosa che non funziona come dovrebbe) · `richiesta` (una cosa nuova chiesta
da chi usa Gestora) · `miglioramento` (una cosa che c'è, fatta meglio) · `refactoring` (stesso
comportamento, codice più pulito) · `studio` (documentazione, analisi, crescita).

**Chi sviluppa**: si decide **prima di iniziare**, ogni volta (vedi `CLAUDE.md`, *Come
lavoriamo*): `io` (Fabio, guidato da Claude) oppure `Claude`. Finché non si è deciso, resta `—`.

**Stato**: `da fare` · `in corso` · `fatto` · `scartato` · `in pausa`. Le voci `fatto` e `scartato` restano
qui fino al rilascio successivo, poi si tolgono.

---

## 📥 Segnalazioni dal locale — in arrivo

Quello che emerge dall'uso nei turni di servizio. Si annota qui **così come arriva**, senza
analizzarlo. Quando lo si prende in mano diventa una voce `V2-xxx` nella sezione sotto e si decide
chi la sviluppa.

Modello da copiare:

```
- Data: gg/mm/aaaa — Turno: pranzo/cena — Chi: (ruolo: Admin / Staff / Cliente)
  Schermata: (es. Prenotazioni, Dashboard, pagina pubblica)
  Cosa è successo: ...
  Cosa ci si aspettava: ...
  Quanto blocca: blocca il servizio / fastidioso / si può aspettare
```

*Nessuna segnalazione ancora.*

---

## 🔵 Da fare

### Il piano di chiusura del progetto (v3)

Deciso da Fabio il **02/11/2026**. Gestora è un prodotto finito: ora deve vivere sia come software
per i locali veri sia come **progetto portfolio** per recruiter e aziende. Il piano porta a termine
cinque cose e poi chiude il progetto, che passa in manutenzione. Si lavora sul branch **`v3`**; le
voci nuove hanno sigla `V3-xxx`. Tempo di Fabio: 3-4 ore a settimana, chiusura prevista fra fine
gennaio e metà febbraio 2027. I passi, con l'ordine, sono nel foglio *Task v3* (ordine 900-1620).

| Sigla | Fase | Tipo | Cosa | Chi sviluppa | Stato |
|---|---|---|---|---|---|
| `V3-000` | 0 | studio | Riordino: tag `v3.1.0` con la sola `V2-028`, branch `v3`, piano scritto nei documenti e nel tracker | Fabio + Claude | in corso |
| `V3-001` | 1 | studio | **Piano di test e collaudo completo**: documento con casi di test, test d'integrazione dell'API su PostgreSQL vero, prove nel browser con Playwright, controlli non funzionali, collaudo manuale, ripristino di prova del backup, correzione dei difetti (`v3.1.x`) | da decidere passo per passo | da fare |
| `V3-002` | 2 | studio | **Studio di fattibilità multi-locale** (solo studio e decisione, lo sviluppo sarebbe una v4) | Claude scrive, Fabio decide | da fare |
| `V3-003` | 3 | studio | **Piano di pubblicazione**: README, caso di studio, LinkedIn, video, CV, racconto da colloquio | Claude scrive, Fabio pubblica | da fare |
| `V3-004` | 3 | nuova funzione | **Demo** per recruiter e visitatori, descritta sotto; poi tag `v3.2.0` | da decidere | da fare |
| `V3-005` | 4 | studio | **Il libro**: studio per i colloqui tecnici e guida per altri progetti, in stile Clean Code, su .NET con React e Angular | Claude scrive, Fabio studia | da fare |
| `V3-006` | 5 | studio | **Chiusura**: progetto in manutenzione, copie finali, attività ricorrenti | Claude + Fabio | da fare |

Documenti di `V3-001`, `V3-002`, `V3-003` e `V3-005` su Claude Docs, come lo studio della v3.

Ancora qui fino al tag `v3.1.0` (`V3.1-T02`):

| Sigla | Tipo | Cosa | Chi sviluppa | Stato |
|---|---|---|---|---|
| `V2-028` | miglioramento | Token di accesso in un cookie `HttpOnly` con protezione CSRF, chiamate a `/api` che passano dal middleware di Vercel con una parola segreta. **In produzione dal 30/10/2026** (merge `c3f6e9c`), blocco delle chiamate dirette ad Azure acceso; variabili in `RUNBOOK.md` §5 | Claude | fatto |

Sostituite il 02/11/2026 dal piano di chiusura: `V2-030` (demo come tour di schermate, 8-10 ore)
da `V3-004`; `V2-005` (documentazione come linea guida per altri progetti) da `V3-005`. Scartata
lo stesso giorno: `V2-004` (agente/MCP personalizzato), non serve a chiudere il progetto.

Tolte il 29/10/2026, perché chiuse e in produzione con la `v3.0.0`: `V2-016` (F0, chiusura v2 e
portale in manutenzione), `V2-017` (F1, .NET 10), `V2-018` (F2, robustezza), `V2-019` (F3, privacy e
account), `V2-022` (F4, modulo dei pareri), `V2-023` (F5, vetrina e rilascio, con il primo avvio in
produzione e la prova completa della registrazione rimasta in sospeso dal `V2-003`), `V2-024` (bug
della disponibilità), `V2-025` (report CSV), `V2-026` (niente dati sulla salute), `V2-027` (blocco per
indirizzo) e `V2-029` (migliorie dell'audit). Scartate il 15/10/2026, prima di iniziare: `V2-020` e
`V2-021` (prova pubblica con una sandbox in memoria per visitatore).

### `V3-004`: la demo

Chiesta da Fabio il 02/11/2026 al posto del tour di `V2-030`. È **Gestora vera, usabile** da N
visitatori insieme (recruiter, contatti LinkedIn), senza che niente si scriva in un database: come
provare un prodotto prima di comprarlo. Le azioni che non si possono fare davvero (link di conferma
dell'email dopo la registrazione e simili) si spiegano con una **guida a schermate**.

Direzione proposta, da confermare in `V3-004-T01`:
- **tutto nel browser**: la stessa app React costruita in «modalità demo», con un finto backend in
  TypeScript che tiene i dati in memoria nella scheda del visitatore, così ognuno ha la sua sala;
  le regole necessarie (assegnazione dei tavoli da `AssegnazioneTavoli`) stanno in una sola cartella
  `demo/`. Scartato per ora un secondo backend .NET: Azure F1 ha un tetto di CPU giornaliero
  condiviso con la produzione e il backend è legato a PostgreSQL;
- **progetto Vercel separato** dallo stesso repository: in produzione non entra codice della demo;
- ingresso con «Entra come Admin / Staff / Cliente», fascia «Demo» sempre visibile, pulsante
  «Ricomincia la demo»;
- guida a schermate per registrazione e conferma email, recupero password, cancellazione account,
  job notturni, report CSV.

Stima da rifare in `V3-004-T01`: a occhio **25-40 ore** di sviluppo (Gestora ha 70 endpoint).

---

## ⚪ Idee

Non sono impegni: sono la lista da cui pescare quando una segnalazione dal locale o una scelta
nostra le rende utili.

**Prenotazioni**
- Turnover del tavolo: durata della seduta, due turni nella stessa fascia
- Lista d'attesa quando la fascia è piena
- No-show con storico e regole per cliente (oggi c'è solo lo stato `NonPresentata`)
- Overbooking controllato per fascia
- Zona come preferenza con ripiego, invece che come vincolo
- Chiusure straordinarie e orari speciali
- Vincolo "una prenotazione al giorno" per il Cliente nel database, non solo nell'applicazione:
  serve una colonna che distingua chi ha creato la prenotazione. Da riprendere insieme a
  un'eventuale app dedicata al cliente, non da sola

**Sala**
- Creazione automatica dei tavoli in base ai coperti richiesti
- Unione e separazione tavoli come azione manuale dello Staff
- Campo `PostiCapotavola` sul singolo tavolo, se un giorno serve precisione piena sul bonus
  testate anche per le unioni miste

**Crescita del database** (emerse con `V2-011` e `V2-015`)
- Tetto sugli account **creati** (non tentati) dallo stesso indirizzo, es. 3 in 24 ore, contati
  dal registro attività che salva già l'IP
- CAPTCHA sulla registrazione (es. Cloudflare Turnstile): l'unica difesa contro chi cambia IP

**Utenti e comunicazione**
- Promemoria della prenotazione per email (la conferma dell'indirizzo c'è dal 14/10/2026, `V2-003`)
- Pagina del registro attività nel frontend (oggi il registro si legge solo dal database)

**Codice**
- Estrarre la logica di autenticazione da `AuthenticationUserController` in un service dedicato,
  così da poterla coprire con test (oggi è l'unico controller senza test). Buon esercizio di
  refactoring + testing

**Altro**
- Export dei report della dashboard (PDF) — il CSV delle prenotazioni per l'Admin è diventato `V2-025`
- Gestione di più locali sullo stesso impianto (*multi-tenant*), da approfondire nello studio
  `V3-002`. Valutata il 15/10/2026: **per ora
  un locale**, la v3.0.0 resta così. Se arriva un secondo locale vero: subito si può dare
  un'installazione per locale (stesso codice, un'app Azure, un database Neon e un progetto Vercel
  ciascuno); il multi-tenant vero (colonna «locale» ovunque, filtri, ruoli per locale, privacy con
  ogni locale titolare e Fabio responsabile) è stimato 40-60 ore, da fare come v4

---

## 🚫 Rischi accettati e cose decise di non fare

Scritte qui perché tornano fuori ogni volta che qualcuno rilegge il codice. Si possono riaprire,
ma con una decisione esplicita (vedi `CLAUDE.md`, *Decisioni di prodotto*).

**AutoMapper resta alla 12.0.1**, con un avviso di sicurezza noto (`GHSA-rvv3-g6hj-g44x`, non
sfruttabile da remoto). La versione che lo risolve ha una licenza a pagamento oltre una soglia di
fatturato, e non si vuole pagare nessuna licenza. Da riaprire solo se si cambia libreria di
mapping.

**`deepmerge` 4.3.1 nel frontend**, avviso «low» di Dependabot (29/10/2026). Lo porta solo `shadcn`,
lo strumento che aggiunge i componenti al progetto: è fra le dipendenze di sviluppo, non entra nella
build e non arriva al browser (`npm audit --omit=dev`: 0 avvisi). La 4.3.1 è l'ultima versione
uscita, non c'è una correzione da installare. Da riaprire quando `shadcn` o `deepmerge` si aggiornano.

**"Una prenotazione al giorno" per il Cliente è un controllo dell'applicazione.** Con due
richieste parallele su fasce diverse si può aggirare. Rischio accettato (decisione di prodotto
10); non riguarda il doppio tavolo, che è invece protetto da un vincolo vero nel database.

**Job notturni senza «Always On».** Sul piano gratuito F1 di Azure l'applicazione si spegne dopo
circa 20 minuti senza traffico, e i job Quartz partono solo ad applicazione accesa. Dal 29/10/2026
UptimeRobot chiama `/health` ogni 5 minuti e la tiene sveglia, quindi i job partono all'ora giusta.
Se UptimeRobot si ferma, i job tornano a partire alla prima richiesta del mattino; la garanzia vera
è «Always On», cioè un piano a pagamento.

**Email unica controllata solo dall'applicazione** (Identity, `RequireUniqueEmail`), senza un
indice unico nel database: basta così.

**Nome "fascia oraria" non uniformato.** Lo stesso concetto si chiama in quattro modi diversi fra
namespace, classe, indirizzo dell'endpoint e tabella. Uniformarlo vorrebbe dire cambiare
l'indirizzo di un endpoint, cioè rompere i collegamenti del frontend, per un guadagno di sola
leggibilità. Cartelle e namespace sono già allineati, l'indirizzo resta com'è.

**Test automatici di concorrenza.** Il database finto usato nei test non applica gli indici unici
né i lock, e introdurre un database vero nei test è sproporzionato. La verifica resta manuale
(`RUNBOOK.md` §2). **Riaperto il 02/11/2026**: i test d'integrazione su PostgreSQL vero
(`V3-001-T02`) proveranno anche la concorrenza sul doppio tavolo; fatto quel passo, questo punto si
toglie.
