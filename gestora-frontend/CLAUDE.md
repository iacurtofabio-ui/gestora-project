# gestora-frontend — Frontend

Questo file vale solo quando si lavora dentro `gestora-frontend/`. Per stato del progetto,
decisioni e regole vedi il `CLAUDE.md` alla radice — resta valido sempre. Per gli endpoint
backend vedi `lib/endpoints.ts` (fonte di verità) o `GestoraWebApi/CLAUDE.md` per le trappole.

## Stack

React 19 + TypeScript + Vite, shadcn/ui + Tailwind CSS, TanStack Query v5 (React Query),
React Hook Form + Zod, **React Router v7** (`createBrowserRouter` — attenzione: qualunque nota
che parla di v6 è sbagliata), Axios con interceptor JWT (attach token da localStorage,
logout + redirect su 401).

## Verifica prima di dire "fatto"

```
npm run lint      # zero errori
npm test          # 132 test, Vitest + Testing Library
npm run build     # controlla i tipi — npm test NON lo fa (vedi sezione Test)
```
Se hai toccato un componente/pagina visibile e non hai verificato a mano nel browser, dillo
esplicitamente: non dare per scontato che "compila" equivalga a "funziona nell'interfaccia".

## Config — due ambienti separati, non uno che cambia

`.env.local` (non versionato) → `VITE_API_URL`, letto **solo** da Vite in locale (`npm run dev`),
stabile su `http://localhost:5099/api`. Richiede il backend locale attivo (`dotnet run` in
`GestoraWebApi/`).

La produzione (Vercel) **non legge mai `.env.local`**: usa la propria `VITE_API_URL` impostata
nella dashboard Vercel, puntata al backend Azure. **Deve essere di tipo *Config* (o *Plaintext*),
non *Secret***: un valore salvato come Secret non arriva alla build nel modo che Vite si aspetta
(vedi `RUNBOOK.md` §8). Modificare `.env.local` non ha alcun effetto sulla build di produzione.
Aprire `localhost:5173` mostra sempre i dati del DB locale — voluto.

## Header di sicurezza su Vercel (`V2-018`)

`vercel.json` manda su ogni pagina una **Content-Security-Policy** (l'elenco di ciò che il browser
può caricare ed eseguire) più `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy` e
`Permissions-Policy`. Tre punti che si rompono senza errori di build:

- **Lo script del tema in `index.html` è ammesso per la sua impronta** (`sha256-...`), non con
  `'unsafe-inline'`. Cambiarlo, anche di uno spazio, cambia l'impronta: il test
  `lib/__tests__/vercelHeaders.test.ts` fallisce e dice di aggiornare `vercel.json`. Non risolvere
  aggiungendo `'unsafe-inline'` agli script.
- **`connect-src` contiene l'indirizzo del backend Azure, scritto a mano.** Se il backend cambia
  indirizzo, va cambiato anche lì, oltre che in `VITE_API_URL`: altrimenti il sito si carica ma
  ogni chiamata all'API viene bloccata dal browser.
- **`style-src` ha `'unsafe-inline'`** perché `sonner` (le notifiche) inserisce da solo un blocco
  di stili nella pagina. Per gli stili il rischio è basso; per gli script no.

Non si prova con `npm run dev` (Vite non legge `vercel.json`): si prova servendo `dist` con gli
stessi header, oppure in produzione dagli strumenti del browser (scheda *Console*: nessun
«Refused to ...»).

## Tavolozza e tema — leggere prima di scrivere un colore

**Non scrivere mai un colore fisso** (`bg-white`, `text-gray-500`, `text-red-500`). Si usano
sempre i token del tema, altrimenti quella zona resta bianca in tema scuro.

| Serve... | Si usa |
|---|---|
| testo normale | `text-foreground` |
| testo secondario | `text-muted-foreground` |
| sfondo della pagina | `bg-background` |
| sfondo di una card | `bg-card` |
| dialog, menu, popover | `bg-popover` |
| azione principale | `bg-primary` / `text-primary` |
| errore, eliminazione | `text-destructive` / `bg-destructive` |
| esito positivo, disponibile | `text-success` |
| avviso, quasi pieno | `text-warning` |
| **riempimento** della banda | `bg-banda-attenzione` / `bg-banda-pieno` |
| fondo su cui corre la banda | `bg-traccia` |
| voce di menu evidenziata | `bg-evidenza` |
| bordo decorativo | `border` |
| bordo di un campo | `border-input` |

### Tipografia

**Inter Variable**, self-hosted via Fontsource. Sei ruoli dichiarati come token,
da usare al posto delle classi improvvisate:

`text-readout` (numeri grandi) · `text-readout-sm` · `text-titolo` (uno per schermata) ·
`text-sezione` (intestazione di blocco) · `text-corpo` (0.9375rem) ·
`text-orario` (orari/quantità in tabella, `tabular-nums`) · `text-nota`.

Il ripiego metrico (`@font-face 'Inter Ripiego'` in `index.css`, script
`scripts/metriche-ripiego.mjs`) va rigenerato solo se si cambia carattere o si aggiorna il
pacchetto Fontsource.

### Raggi

`2px` bande e righe · `6px` tutto ciò che è controllo o card ·
`12px` ciò che galleggia (dialog, menu). I nomi dei token (`--radius-md`, `--radius-2xl`, ecc.)
non corrispondono alla misura: si cambia il valore del token, non la classe nei componenti.
Una sola ombra, riservata a ciò che galleggia davvero. Il raggio dice *che cosa è* una cosa.

### Barra superiore, non sidebar

`layouts/AppLayout.tsx` ha una barra superiore fissa (`h-14`, sticky), non una sidebar laterale:
logo a sinistra, voci di menu orizzontali al centro (voce attiva
sottolineata in `primary`, non sfondo pieno), tema e menu utente (avatar con iniziali) a
destra. Sotto i 1024px le voci vanno in un pannello a scomparsa sotto la barra, aperto dal
pulsante hamburger. Il filtro per ruolo delle voci di menu è in un solo array (`vociMenu` dentro
`AppLayout.tsx`), con `ruoli` e `gruppo` per voce — non toccare la logica di filtro altrove.

### Le azioni di riga

In tabella non si mettono pulsanti affiancati. Si usa `components/AzioniRiga.tsx`: **una**
azione in chiaro (variante `azione` del Button), il resto nel menu `…`, la distruttiva sotto un
separatore in rosso. La `<TableRow>` deve avere `className="group/riga"`, altrimenti il pulsante
non reagisce alla riga. Il **pieno** resta all'azione primaria della pagina, che è una sola.

**Tema chiaro/scuro**: `context/ThemeContext.tsx` aggiunge/toglie la classe `dark`; interruttore
in `components/ThemeToggle.tsx`, ricordato in `localStorage`. Tre stati: **chiaro**, **scuro**,
**come il sistema** (predefinito) — nomi da usare ovunque, anche nel codice.

## Pagina pubblica

`/` mostra `pages/LandingPage.tsx`, raggiungibile **senza account**.

> ⚠️ **In quella pagina non c'è nessun token.** Gli unici endpoint chiamabili sono
> `check-disponibilita` e `limiti-prenotazione` (il tetto dei coperti per il campo «Persone», hook
> `useLimitiPrenotazione`: si usa `maxCopertiPrenotazioneOnline`, 20, e oltre compare l'invito a
> contattare il ristorante; `maxCopertiPerPrenotazione` è il limite tecnico dello Staff).
> `get-zone-attive`, `get-all-fasce` e tutto il resto **richiedono
> l'accesso**: chiamarli da lì produce un 401. Per questo le zone in vetrina sono testo scritto
> nella pagina, non dati del database.

Una fascia non prenotabile mostra **un solo stato**, scelto da `motivo` (`V2-007`): «Turno
concluso», «Pieno», «Restano N posti», «Pieno per N persone». Non mostrare `messaggio` (è scritto
per lo Staff) e non rimettere i posti residui accanto a «Pieno»: era il bug.

**Informativa e termini** (`V2-019`): `/privacy` e `/termini` (`PrivacyPage`, `TerminiPage`, guscio
`components/legale/PaginaLegale`) sono pubbliche e fuori da `SetupGuard`, come `/conferma-email`.
Titolare, email di contatto e versione stanno solo in `lib/privacy.ts`. I testi descrivono cosa
Gestora fa davvero (dati salvati, tempi di conservazione dei job, fornitori, regole delle
prenotazioni): se cambia una di queste cose, si cambia anche il testo e si alza
`INFORMATIVA_VERSIONE`, insieme alla versione del backend (`Privacy:Versione`). La registrazione
ha la casella obbligatoria «Ho letto l'informativa» (`accettaPrivacy`). Nell'area riservata
`components/PresaVisionePrivacy`, dentro `AppLayout` al posto della pagina, mostra l'informativa a chi
non l'ha ancora accettata (account creati da un Admin, primo Admin, informativa cambiata); se la
verifica fallisce lascia passare, per non chiudere fuori lo Staff durante il servizio.

Due condizioni attive sulla radice: `SetupGuard` (senza un Admin si va a `/setup`) e
`RedirectSeAutenticato` (chi ha sessione aperta va alla sua pagina, non alla vetrina).

## Routing

`src/router/index.tsx` — route protette per ruolo tramite `ProtectedRoute` che avvolge gruppi di
route: Admin/Staff, Admin/Staff/Cliente, Admin-only. Nuove pagine vanno registrate qui, nel
gruppo di ruolo corretto — non creare controlli di ruolo ad-hoc nella pagina stessa.

## Ruoli utente — sempre array, mai stringa singola

⚠️ Un utente **può avere più ruoli** (vedi `GestoraWebApi/CLAUDE.md` sezione RBAC). Il JWT
serializza il claim ruolo come **stringa singola** se l'utente ha un solo ruolo, come **array**
se ne ha più di uno — comportamento standard di ASP.NET Identity, non un bug.

`AuthUser.roles` è quindi tipizzato `string[]`, **mai** `string`. `AuthContext.tsx` normalizza
sempre il claim grezzo con `normalizeRoles()` — non leggerlo mai direttamente altrove.
- singolo controllo: `user?.roles.includes('Admin')`
- su un elenco di ruoli consentiti: `allowedRoles.some(r => user.roles.includes(r))`

Mai `user.roles === 'Admin'` o `allowedRoles.includes(user.roles)` — confronterebbe un array con
una stringa, sempre falso.

## Pattern CRUD standard

Ogni modulo CRUD segue questa struttura a 4 file (esempio di riferimento: **Zone**):

```
src/types/{modulo}.ts             - tipi TypeScript (DTO ricevuto + form DTO inviato)
src/hooks/use{Modulo}.ts          - query + mutation (React Query)
src/components/{Modulo}Modal.tsx  - form create/edit (React Hook Form)
src/pages/{Modulo}Page.tsx        - pagina principale con tabella e stato UI
```

**Types** — due tipi per modulo: DTO ricevuto dal backend (include id) e DTO inviato (solo campi
form, senza id). Il backend decide l'id, mai il frontend.

**Hook**:
- query: `queryKey` include tutti i parametri che cambiano il risultato; `queryFn` chiama
  `apiClient` e restituisce `r.data`
- mutation: `onSuccess` invalida la cache con `invalidateQueries` + `toast.success`;
  `onError: segnalaErrore('testo di ripiego')` — **mai** scrivere la gestione errore a mano. Se
  la scrittura cambia anche numeri mostrati altrove (es. una prenotazione tocca la Dashboard),
  invalidare anche quelle query — vedi `invalidaPrenotazioniEDashboard` in `usePrenotazioni.ts`
- i path degli endpoint si prendono da `lib/endpoints.ts`, **mai scritti inline**
- **i filtri di una pagina a elenco vivono nell'URL** (`useSearchParams`), non in `useState`
  locale — così sono condivisibili e sopravvivono a un ricaricamento. Esempio di riferimento:
  `PrenotazionePage.tsx`

**Modal** — un solo modal gestisce create ed edit: prop oggetto `undefined` = create (form
vuoto), valorizzata = edit (form ripopolato via `useEffect` + `reset`).

**Stato UI pagina** — pattern standard: `oggettoSelezionato` (undefined | DTO), `modalAperto`
(boolean), `idDaEliminare` (undefined | id, controlla `ConfirmDialog`).

## Helper e componenti condivisi — usare questi, non riscriverli

| Dove | Cosa fa |
|---|---|
| `lib/apiError.ts` | `messaggioErrore`/`segnalaErrore` per le **scritture**, `messaggioErroreCaricamento` per le **letture**. Distingue anche "richiesta mai partita" da un errore del server |
| `lib/endpoints.ts` | Tutti i path degli endpoint, raggruppati per area — **fonte di verità** per gli endpoint disponibili |
| `lib/validazioni.ts` | Regole condivise dei form, password inclusa: 8 caratteri, maiuscola, numero, carattere speciale |
| `lib/date.ts` | `oggiInItalia`, `lunediSettimanaCorrenteInItalia`. Fuso `Europe/Rome` fissato nel codice. **Mai** `toISOString()` per una data di calendario |
| `lib/jwt.ts` | Unico punto che legge il token. È una **lettura**, non una verifica: la firma non è controllabile dal browser |
| `lib/session.ts` | Ponte fra l'intercettore Axios (fuori da React) e i componenti, per la scadenza sessione senza ricaricare |
| `lib/rinnovoSessione.ts` | Quando rinnovare il token (`V2-010`): 10 minuti prima della scadenza. Il rinnovo lo fa `AuthProvider` finché la pagina è aperta; si ferma al logout e quando il server non allunga più la scadenza (12 ore dal login) |
| `lib/giorni.ts` | `GIORNI_SETTIMANA` |
| `lib/invalidazioni.ts` | `invalidaVisteDellaSala`: Dashboard, disponibilità e riepilogo sala da rinfrescare dopo ogni scrittura su fasce, tavoli e zone |
| `lib/validazioni.ts` → `interoObbligatorio` | Campo numerico dei form: con `valueAsNumber` un campo vuoto è `NaN` e Zod v4 darebbe un messaggio in inglese. **Non usare `z.number()` nudo** |
| `lib/queryClient.ts` | Niente nuovi tentativi sui 4xx, nessun tentativo sulle mutation |
| `components/PageState.tsx` | `PageLoading`/`PageError` (contenuto solo), `ErroreForm`, `TableSkeleton`/`DashboardSkeleton` (`righe`/`colonne` sulla forma reale della pagina) |
| `components/EmptyState.tsx` | Messaggio per liste vuote |
| `components/Paginazione.tsx` | Barra di navigazione delle pagine |
| `components/ConfirmDialog.tsx` | Conferma prima di un'azione distruttiva. Accetta `titolo`/`testoConferma`: annullare ed eliminare non devono avere la stessa faccia |
| `components/StatoBadge.tsx` | Stato prenotazione: un punto più la parola, mai solo il colore |
| `components/StatoAttivo.tsx` | Attiva/non attiva, per zone/tavoli/fasce |
| `components/AzioniRiga.tsx` / `AzioniPrenotazione.tsx` | Azioni di riga generiche / quale azione ha senso in quale stato |
| `components/BandaCoperti.tsx` | La banda che si riempie di coperti, soglie in `lib/coperti.ts` |
| `components/IntestazionePagina.tsx` | Titolo + conteggio + azione primaria |
| `components/SchermataMessaggio.tsx` | Schermata a tutta pagina: errore, accesso negato, config mancante, manutenzione |
| `lib/manutenzione.ts` + `components/PortaleInManutenzione.tsx` | `V2-016`: con `VITE_MANUTENZIONE=true` (Vercel, entra nella build) `main.tsx` non monta l'app e mostra solo la schermata di manutenzione |
| `components/Logo.tsx` | Il marchio, SVG che prende i colori del tema |
| `components/ErrorBoundary.tsx` | Rete di sicurezza **sopra** il router (unico componente a classe: React non ha equivalente a hook) |
| `router/RouteErrorPage.tsx` | Rete di sicurezza **dentro** il router (React Router cattura gli errori prima dell'ErrorBoundary) |

Tutti i pulsanti d'azione usano `<Button>` di shadcn, senza eccezioni (anche il logout).

**Due menu a tendina diversi, di proposito:**
- `components/ui/select.tsx` (Radix) — nei **filtri delle pagine**. ⚠️ Radix non ammette stringa
  vuota come valore: per "nessun filtro" serve un valore vero (es. `'tutti'`) tradotto in
  `undefined` prima della chiamata.
- `components/ui/native-select.tsx` — nei **form**. `<select>` vero vestito come `Input`:
  funziona con `{...register('campo')}` senza `Controller`, sul telefono apre la rotella di
  sistema, nei test si pilota con `userEvent.selectOptions`. Radix in test richiede sostituzioni
  manuali che jsdom non implementa — nei form non ne vale la pena.

Tutte le etichette dei form sono collegate al campo con `htmlFor`/`id`. Su Login/Registrazione
l'etichetta è `sr-only` (design con segnaposto).

## Test

**132 test** con Vitest + Testing Library: impronta dello script del tema nella CSP di `vercel.json` (2, `V2-018`), portale in manutenzione (1 + 4 della lettura di `VITE_MANUTENZIONE`, `V2-016`), quali richieste portano il token (4, `V2-003`), lettura difensiva del token (10), rinnovo del token in
`AuthProvider` (5) e suo momento (3), helper errori (5 + 5 del login + 2 del login con email non
confermata, `V2-003` + 1 per campo + 2 del limite di richieste), pagina di conferma dell'email (3), messaggio della pagina pubblica sul 429 (1), regole di nome utente e campi numerici (8),
`ProtectedRoute` su accesso e ruoli (6), scelta fascia oraria in `PrenotazioneModal` (5), limite
online dei coperti in `PrenotazioneModal` (3), lunghezza del nome cliente in `PrenotazioneModal` (2, `V2-013`), «Salva» spento su fascia non disponibile solo in creazione in `PrenotazioneModal` (2, `V2-024`), stato delle fasce e limite online nella pagina
pubblica `VerificaDisponibilita` (6), azioni
di riga e tastiera in `AzioniPrenotazione` (16, incluso l'eliminazione solo sulle annullate e niente Conferma/Annulla fuori tempo), scelta della
pagina di casa per ruolo (5), navigazione per giorno della Dashboard (1), filtri nell'URL di
`PrenotazionePage` e salto al giorno dopo una creazione (3), informativa e termini (2, `V2-019`), presa visione nell'area riservata (3) e casella in registrazione (2), recupero password (4), «Il mio account» (3), cancellazione dell'account (3, `V2-019`), report CSV e date del periodo (5, `V2-025`), modulo dei pareri (5, `V2-022`).

> ⚠️ `npm test` **non controlla i tipi**. Serve anche `npm run build`.
>
> ⚠️ `hooks/usePrenotazioni.ts` è **mockato per intero** nei test di `PrenotazioneModal`. Per
> questo `useCheckDisponibilita` sta in un modulo separato: messo lì dentro sparirebbe con lo
> stesso mock.

## Moduli implementati

| Modulo | types | hook | modal | page |
|---|---|---|---|---|
| Zone | zona.ts | useZone.ts | ZonaModal.tsx | ZonePage.tsx |
| Postazioni | postazione.ts | usePostazioni.ts | PostazioneModal.tsx | PostazionePage.tsx |
| FasceOrarie | fasciaOraria.ts | useFasceOrarie.ts | FasciaOrariaModal.tsx | FasciaOrariaPage.tsx |
| Prenotazioni | prenotazione.ts | usePrenotazioni.ts | PrenotazioneModal.tsx | PrenotazionePage.tsx |
| AdminUtenti | utente.ts | useAdminUtenti.ts | 4 modal (Edit/GestisciRuoli/ResetPassword/CreateUser) | AdminUtentiPage.tsx |

Nuovi moduli CRUD seguono questa struttura — varianti solo se motivate qui. AdminUtenti ha 4
modal invece di 1 per via della *creazione utenti* qui sotto: resta comunque nella struttura a 4 file.

### Creazione utenti (non un CRUD standard)

Il backend non ha un endpoint "crea utente con ruolo": `POST register` è pubblico e assegna
sempre `Cliente`. `useCreateUser` compone tre chiamate esistenti: `register` → `get-users` (per
recuperare l'id, `register` non lo restituisce) → `assign-role`/`remove-role` se l'Admin ha
scelto un ruolo diverso da Cliente. Pagina pubblica `/register` chiama `register` direttamente,
senza passare da `useCreateUser`.

### Modulo dei pareri (`V2-022`)

`/pareri` (`PareriPage`) è pubblica e fuori da `SetupGuard`, come `/privacy`; ci portano il piè di
pagina della vetrina e il menu utente («Lascia un parere»). Messaggio obbligatorio (contatore fino a
2000), nome ed email facoltativi. Il campo `sitoWeb` è una **trappola** per i programmi automatici:
fuori dallo schermo, `tabIndex={-1}`, `aria-hidden`; non renderlo visibile e non toglierlo. Il contatore
usa `useWatch`, non `watch`, per non perdere l'ottimizzazione del React Compiler.

### Report CSV delle prenotazioni (`V2-025`)

Nella pagina Prenotazioni, **solo per l'Admin**, «Scarica report» (`components/ReportPrenotazioni`) apre
una finestra con le date «Dal» e «Al», all'inizio il mese in corso (`meseDi`, `lib/date.ts`). Il periodo
si controlla anche qui (date presenti, ordine, al massimo 366 giorni, `giorniCompresi`), perché un errore
del server arriva come blob e non si legge. `useScaricaReport` (`hooks/useReportPrenotazioni`) salva il
file con un link temporaneo e un nome costruito dal frontend.

### «Il mio account» (`V2-019`)

`/account` (`AccountPage`, tutti i ruoli) si apre dal menu utente nella barra. Mostra i dati
dell'account, il cambio password e «Cancella l'account» (`CancellaAccountCard`: conferma con la password in una finestra, poi logout e ritorno alla pagina del locale). Dopo il cambio il server manda un token nuovo e `useCambiaPassword`
lo salva con `login(token)`: senza, la sessione in uso finirebbe insieme alle altre. Le date-ora del
server si scrivono con `istanteInItalia` (`lib/date.ts`).

### Recupero password (`V2-019`)

Dal login «Password dimenticata?» porta a `/password-dimenticata` (dietro `SetupGuard`, come il
login), che mostra sempre lo stesso messaggio, esista o no l'email. Il link dell'email porta a
`/reimposta-password?userId=…&token=…` (pubblica, fuori da `SetupGuard`, come `/conferma-email`).
Entrambi gli endpoint sono in `SENZA_TOKEN`. Gli errori di Identity sulla nuova password arrivano sul
campo `password` e la pagina li mostra sotto «Nuova password».

### Conferma dell'email (`V2-003`)

Chi si registra da `/register` non entra finché non conferma l'email: la pagina non rimanda al
login ma mostra «Controlla la tua email». Il link dell'email porta a `/conferma-email`
(`ConfermaEmailPage`, pubblica e fuori da `SetupGuard`), che legge `userId` e `token` dall'indirizzo
e conferma da sola. Il login con email non confermata risponde 403 con `codice`
`EMAIL_NON_CONFERMATA`: si riconosce con `emailNonConfermata()` di `lib/apiError.ts` (mai dal
testo) e `LoginPage` mostra il pulsante «Rimanda l'email di conferma». L'Admin che crea lo Staff
da `useCreateUser` non cambia nulla: quegli account nascono già confermati. ⚠️ Perché il server lo
riconosca, **`register` deve viaggiare con il token**: non rimetterlo in `SENZA_TOKEN` di
`lib/axios.ts` (c'era, e rendeva l'Admin un anonimo anche per il limite di `V2-011`).
