# Gestora v2 — cosa resta da fare

Aggiornato il **29/10/2026**. Questo è **l'unico elenco valido** delle cose aperte: se una cosa
non è scritta qui, non è in programma.

Il foglio *Fix e Bug* del tracker resta il registro dettagliato dei difetti, il foglio *Task v3* la
sequenza dei passi da fare (il prossimo è in cima alla Dashboard); questo file è la
vista d'insieme che si guarda per decidere cosa fare.

---

## Come si legge

**Sigla**: una sola sequenza, `V2-001`, `V2-002`… Non si riusa mai un numero, nemmeno se una voce
viene scartata.

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

### Dopo la v3.0.0

La `v3.0.0` è in produzione dal **29/10/2026** (merge con la PR #8, `8ab0db0`, tag `v3.0.0`): il
piano F0-F5 è chiuso e il portale è aperto. Il primo lavoro della v3.1 è `V2-028`; con la stessa
decisione si sceglie se fare anche la demo qui sotto (passo `V3.1-T01` nel foglio *Task v3*).

| Sigla | Fase | Tipo | Cosa | Chi sviluppa | Stato |
|---|---|---|---|---|---|
| `V2-028` | v3.1 | miglioramento | Token di accesso in un cookie `HttpOnly` con protezione CSRF, al posto di `localStorage`. Era un rischio accettato, riaperto da Fabio il 28/10/2026 dopo l'audit. Analisi del 29/10/2026: Safari blocca i cookie fra siti diversi (`vercel.app` e `azurewebsites.net`), quindi servono o il passaggio delle chiamate da Vercel con un middleware e una parola segreta per fidarsi dell'indirizzo del visitatore (gratis, 10-14 ore), o un dominio proprio con Azure a pagamento (il piano F1 non accetta domini propri). **Rimandata da Fabio a dopo la `v3.0.0`**: primo lavoro della v3.1, strada da scegliere allora | Claude | in pausa |

Tolte il 29/10/2026, perché chiuse e in produzione con la `v3.0.0`: `V2-016` (F0, chiusura v2 e
portale in manutenzione), `V2-017` (F1, .NET 10), `V2-018` (F2, robustezza), `V2-019` (F3, privacy e
account), `V2-022` (F4, modulo dei pareri), `V2-023` (F5, vetrina e rilascio, con il primo avvio in
produzione e la prova completa della registrazione rimasta in sospeso dal `V2-003`), `V2-024` (bug
della disponibilità), `V2-025` (report CSV), `V2-026` (niente dati sulla salute), `V2-027` (blocco per
indirizzo) e `V2-029` (migliorie dell'audit). Scartate il 15/10/2026, prima di iniziare: `V2-020` e
`V2-021` (prova pubblica con una sandbox in memoria per visitatore).

### Da decidere ora: la v3.1.0 con una demo interattiva

**La v3.0.0 è rilasciata: si decide se fare la v3.1.0.** Non è un impegno. La proposta è una
**demo interattiva** (*product tour*): una pagina pubblica `/demo` nel frontend di produzione, senza
login, senza backend, senza dati salvati.
1. Tour guidato: 6-7 schermate vere di Gestora (zone → tavoli → fasce → prenotazione → dashboard),
   ognuna con un punto evidenziato e una frase semplice.
2. Riquadro «Quanti siete?»: il visitatore sceglie le persone e vede su una piantina finta quali
   tavoli Gestora unisce. Richiede una piccola copia in TypeScript della regola di
   `AssegnazioneTavoli`, dichiarata come dimostrativa.
3. Il modulo dei pareri (`V2-022`) in fondo.

Circa **8-10 ore** con il riquadro, **6** con solo tour e pareri: 2-3 settimane dopo la v3.0.0.
Niente seconda app su Azure né secondo progetto Vercel; nessun dato dei visitatori. Scartati gli
strumenti esterni (Arcade, Storylane): aggiungono i loro cookie. Se si decide di farla, diventa una
voce `V2-0xx` nuova.

### In pausa

| Sigla | Tipo | Cosa | Chi sviluppa | Stato |
|---|---|---|---|---|
| `V2-004` | studio | Agente/MCP personalizzato per lavorare su Gestora | — | in pausa |
| `V2-005` | studio | Documentazione del progetto come linea guida per metterne in piedi altri | — | in pausa |

Tolta il 14/10/2026, perché chiusa e in produzione (rilascio del 14/10/2026, senza tag): `V2-003`
(conferma dell'email alla registrazione, merge con la PR #5, `915c748`; il commit `086db11`,
intitolato «invio email conferma registrazione», contiene tutta la voce). Provata a vista in locale
e con una email reale via Brevo; la prova completa in produzione non è stata fatta, per non
sporcare un database che ancora non ha il primo Admin.

Tolte il 13/10/2026, perché chiuse e in produzione (rilascio del 13/10/2026, senza tag): `V2-012`
(blocco dello spegnimento di una zona con prenotazioni da servire), `V2-013` (limite di 200
caratteri su `NomeCliente`) e `V2-014` (registro attività: eliminazioni atomiche, modifica utente
registrata).

Tolte il 12/10/2026, perché chiuse e in produzione (rilascio del 12/10/2026, senza tag):
`V2-011` (limiti di richieste su registrazione e verifica della disponibilità) e `V2-015`
(pulizia notturna delle utenze mai usate, migration 8 applicata su Neon). Prima, il 09/10/2026:
`V2-001`, `V2-002`, `V2-007`, `V2-008`, `V2-009`, `V2-010` e `V2-006` scartata. La loro storia
resta nel tracker (fogli *Fix e Bug* e *Task v2*) e nella cronologia Git di questo file.

### `V2-004`, `V2-005`
Arrivano dal foglio *Appunti e Step* del tracker, dove erano segnate «Da fare». Da precisare
insieme prima di iniziare.

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
- Gestione di più locali sullo stesso impianto (*multi-tenant*). Valutata il 15/10/2026: **per ora
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

**Token di accesso in `localStorage`, fino alla v3.1** (`V2-028`). Uno script estraneo nella pagina
potrebbe leggerlo; la CSP rigida del frontend (solo script del sito, nessuno script scritto nella
pagina senza impronta) è la difesa. Accettato **con scadenza** il 29/10/2026: si chiude con `V2-028`,
primo lavoro dopo la `v3.0.0`.

**Email unica controllata solo dall'applicazione** (Identity, `RequireUniqueEmail`), senza un
indice unico nel database: basta così.

**Nome "fascia oraria" non uniformato.** Lo stesso concetto si chiama in quattro modi diversi fra
namespace, classe, indirizzo dell'endpoint e tabella. Uniformarlo vorrebbe dire cambiare
l'indirizzo di un endpoint, cioè rompere i collegamenti del frontend, per un guadagno di sola
leggibilità. Cartelle e namespace sono già allineati, l'indirizzo resta com'è.

**Test automatici di concorrenza.** Il database finto usato nei test non applica gli indici unici
né i lock, e introdurre un database vero nei test è sproporzionato. La verifica resta manuale
(`RUNBOOK.md` §2).
