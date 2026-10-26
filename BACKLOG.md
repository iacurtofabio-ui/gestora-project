# Gestora v2 — cosa resta da fare

Aggiornato il **26/10/2026**. Questo è **l'unico elenco valido** delle cose aperte: se una cosa
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

### Verso la v3.0.0 — production ready

Piano del 15/10/2026 nello studio [Gestora – Studio di fattibilità e piano di attacco per la v3](https://claude.ai/code/artifact/f31f316c-733c-4a5e-a93f-2f139c0518c1):
sei fasi (F0-F5), circa 37 ore a 3-4 ore a settimana, fino a fine gennaio 2027. La v2 si chiude con
il tag `v2.1.0` e la produzione resta in «Portale in manutenzione» fino al rilascio `v3.0.0`. Le
fasi vanno su `main` una alla volta, dietro la manutenzione.

| Sigla | Fase | Tipo | Cosa | Chi sviluppa | Stato |
|---|---|---|---|---|---|
| `V2-016` | F0 | richiesta | Chiusura v2 (tag `v2.1.0`) e «Portale in manutenzione» attivabile da variabili, frontend e backend. In produzione dal 15/10/2026 (PR #7) | Claude | fatto |
| `V2-017` | F1 | miglioramento | Passaggio a .NET 10 LTS: framework, pacchetti, Dockerfile, CI. In produzione dal 16/10/2026 (commit `f6aeeda`, merge `0fb6c52`) | Claude | fatto |
| `V2-018` | F2 | miglioramento | Robustezza: chiavi Data Protection salvate nel database (migration 9), header di sicurezza nel backend e su Vercel, procedura di backup, UptimeRobot su `/health` ogni 30 minuti. Passi precisati il 19/10/2026; la build nella CI del frontend c'era già. In produzione dal 20/10/2026 (migration 9 su Neon, merge `112ef84`); UptimeRobot attivo dal 20/10/2026 | Claude | fatto |
| `V2-024` | — | bug | Disponibilità sbagliata con un solo tavolo occupato: una zona con un tavolo da 8 e una prenotazione da 5 mostra 3 posti liberi, ma la prenotazione poi fallisce (foglio *Fix e Bug*). Causa: i posti rimasti guardavano solo il tetto della fascia, non i tavoli liberi. Correzione: posti rimasti = il minore fra tetto residuo e gruppo più grande che i tavoli liberi accolgono; nel form dello Staff «Salva» spento in creazione se la fascia non è disponibile. In produzione dal 26/10/2026 (commit `e9213f2`, merge `7b4ae5f`) | Claude | fatto |
| `V2-019` | F3 | richiesta | Privacy e account: informativa e termini, presa visione in registrazione, cancellazione ed export dei propri dati, registro attività a 24 mesi, recupero password, «Il mio account», account inattivi da 24 mesi eliminati dopo un'email di avviso. Passi precisati il 26/10/2026 (`V2-019-T02`…`T13` nel foglio *Task v3*): una migration (10); un account con prenotazioni da servire non si cancella, quelle passate restano senza utente | Claude | in corso |
| `V2-022` | F4 | richiesta | Modulo pubblico dei pareri con invio email a Fabio | Claude | da fare |
| `V2-023` | F5 | studio | Vetrina e rilascio: README in inglese, screenshot, LICENSE, CHANGELOG, anteprime social, `v3.0.0` | Claude | da fare |

Scartate il 15/10/2026, prima di iniziare: `V2-020` e `V2-021` (prova pubblica con una sandbox in
memoria per visitatore, backend e frontend, circa 21 ore). Al loro posto la nota qui sotto.

### Da decidere a v3.0.0 chiusa: la v3.1.0 con una demo interattiva

**Quando la v3.0.0 è rilasciata, decidere se fare la v3.1.0.** Non è un impegno. La proposta è una
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

### In pausa fino al rilascio `v3.0.0`

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

### Eliminazione delle utenze inattive — dentro `V2-019`
Deciso il 26/10/2026: si fa nella F3, passo `V2-019-T10`. Nota del 12/10/2026, il ragionamento di partenza. Oggi `V2-015`
elimina solo le utenze *mai usate* (nessun login). Un Cliente entrato almeno una volta resta per
sempre, anche se non torna da anni. Estendere la pulizia a chi non entra da N mesi (es. 24) è
la pratica corretta anche per il GDPR, ma va fatto **con un'email di avviso** qualche settimana
prima («il tuo account verrà eliminato, entra per tenerlo»): senza, un cliente che viene una
volta l'anno ritrova l'account cancellato. L'invio delle email c'è dal 14/10/2026 (`V2-003`,
`IInvioEmail`), quindi si può fare come voce a sé. Lato codice è piccolo: una condizione in più in
`UtentiRepository.GetUtenzeMaiUsateAsync` (`UltimoAccesso` più vecchio della soglia) e un valore
in `UtenzeSettings`, nessuna migration. Chi ha prenotazioni resta comunque protetto dalla FK, e
le prenotazioni concluse spariscono dopo 6 mesi.

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
- Retention del registro attività (`LogActivities`): oggi cresce a ogni operazione e non si
  svuota mai. Periodo già scelto: **24 mesi**, nel job notturno
- Tetto sugli account **creati** (non tentati) dallo stesso indirizzo, es. 3 in 24 ore, contati
  dal registro attività che salva già l'IP
- CAPTCHA sulla registrazione (es. Cloudflare Turnstile): l'unica difesa contro chi cambia IP

**Utenti e comunicazione**
- Promemoria della prenotazione per email (la conferma dell'indirizzo c'è dal 14/10/2026, `V2-003`)
- Pagina del registro attività nel frontend (oggi il registro si legge solo dal database)
- Recupero password autonomo
- Autenticazione con cookie `HttpOnly` + protezione CSRF, al posto del token in `localStorage`.
  È una riprogettazione, non un fix: il token in `localStorage` non è un bug (vedi `RUNBOOK.md`
  §9), ma un cookie `HttpOnly` toglie anche la possibilità teorica di leggerlo da JavaScript

**Codice**
- Estrarre la logica di autenticazione da `AuthenticationUserController` in un service dedicato,
  così da poterla coprire con test (oggi è l'unico controller senza test). Buon esercizio di
  refactoring + testing

**Altro**
- Export CSV/PDF dei report — oggi **non esiste**: esistono solo i due endpoint della dashboard
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

**"Una prenotazione al giorno" per il Cliente è un controllo dell'applicazione.** Con due
richieste parallele su fasce diverse si può aggirare. Rischio accettato (decisione di prodotto
10); non riguarda il doppio tavolo, che è invece protetto da un vincolo vero nel database.

**Blocco del login dopo 5 tentativi in 15 minuti.** Chi conosce l'email dell'Admin può tenerlo
bloccato sbagliando la password apposta. È il prezzo della protezione contro i tentativi a forza
bruta, accettato.

**Job notturni al primo accesso del giorno.** Sul piano gratuito F1 di Azure l'applicazione si
spegne senza traffico, quindi i job Quartz partono quando arriva la prima richiesta. Per farli
partire all'ora giusta serve «Always On», cioè un piano a pagamento.

**Email unica controllata solo dall'applicazione** (Identity, `RequireUniqueEmail`), senza un
indice unico nel database: basta così.

**Nome "fascia oraria" non uniformato.** Lo stesso concetto si chiama in quattro modi diversi fra
namespace, classe, indirizzo dell'endpoint e tabella. Uniformarlo vorrebbe dire cambiare
l'indirizzo di un endpoint, cioè rompere i collegamenti del frontend, per un guadagno di sola
leggibilità. Cartelle e namespace sono già allineati, l'indirizzo resta com'è.

**Test automatici di concorrenza.** Il database finto usato nei test non applica gli indici unici
né i lock, e introdurre un database vero nei test è sproporzionato. La verifica resta manuale
(`RUNBOOK.md` §2).
