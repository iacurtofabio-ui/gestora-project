import { Link } from 'react-router-dom'
import { Logo } from '@/components/Logo'
import { ThemeToggle } from '@/components/ThemeToggle'
import { Button } from '@/components/ui/button'
import { VerificaDisponibilita } from '@/components/landing/VerificaDisponibilita'

/**
 * Fase 13 — la porta d'ingresso pubblica. Fase 3 del redesign — direzione «Turno».
 *
 * Prima `/` rimandava direttamente a `/login`: chi apriva il link trovava un form di accesso
 * senza credenziali e se ne andava.
 *
 * ⚠️ Da sapere prima di aggiungere contenuti dinamici: qui **non c'è nessun token**. L'unico
 * endpoint chiamabile è `check-disponibilita`, che il backend dichiara pubblico. Zone, tavoli e
 * fasce richiedono l'accesso, quindi quello che si legge sotto è contenuto scritto nella pagina,
 * non dati del database. Per cambiarlo servirebbe aprire quegli endpoint: è una decisione a sé.
 */

const ZONE_VETRINA = [
  { nome: 'Sala interna', descrizione: 'Il posto giusto tutto l’anno.' },
  { nome: 'Dehors', descrizione: 'Tavoli all’aperto.' },
  { nome: 'Sala privata', descrizione: 'Per cene di gruppo e occasioni.' },
]

/**
 * I tre passaggi sono una sequenza vera, quindi restano numerati — ma non sono piu' tre blocchi
 * identici affiancati, ognuno con il suo numero ripetuto sopra il titolo.
 *
 * Diventano una sequenza sola su un filo continuo, con i numeri come tacche sul filo: e' lo
 * stesso vocabolario della banda oraria, dove la progressione si legge dalla posizione e non
 * dalla ripetizione di un'etichetta. Su schermo stretto il filo si raddrizza in verticale e
 * resta la stessa cosa.
 */
const PASSAGGI = [
  {
    titolo: 'Verifica la disponibilità',
    testo: 'Scegli giorno e numero di persone. I turni liberi si vedono subito.',
  },
  {
    titolo: 'Registrati',
    testo: 'L’account si crea una volta sola. Poi scegli il turno e, se vuoi, la zona.',
  },
  {
    titolo: 'Prenota il tavolo',
    testo: 'Il posto viene assegnato in automatico, unendo piu’ tavoli quando serve.',
  },
]

export default function LandingPage() {
  return (
    <div className="flex min-h-screen flex-col bg-background">
      {/* RESTYLE: stessa barra fissa dell'area autenticata (h-14, sticky), cosi' il passaggio
          da vetrina a dashboard non cambia grammatica visiva. */}
      <header className="sticky top-0 z-40 border-b bg-card">
        <div className="mx-auto flex h-14 max-w-6xl items-center justify-between gap-4 px-4 md:px-6">
          <Logo />
          <div className="flex items-center gap-2">
            <ThemeToggle />
            <Button asChild variant="ghost" size="sm">
              <Link to="/login">Accedi</Link>
            </Button>
            <Button asChild size="sm">
              <Link to="/register">Registrati</Link>
            </Button>
          </div>
        </div>
      </header>

      <main className="flex-1">
        {/* Apertura e verifica insieme: la cosa che si puo' provare subito sta nella prima
            schermata, non dopo aver scorso. */}
        <section className="mx-auto grid max-w-5xl gap-10 px-4 py-12 md:grid-cols-2 md:items-center md:py-20">
          <div className="space-y-5">
            <h1 className="text-4xl font-semibold tracking-tight text-balance md:text-5xl">
              Prenota il tuo tavolo, senza telefonate.
            </h1>
            <p className="text-lg text-muted-foreground text-pretty">
              Verifica la disponibilità e conferma la prenotazione. Al tavolo ci pensa Gestora.
            </p>
            <div className="flex flex-wrap gap-3">
              {/* RESTYLE: il pulsante primario scorre al modulo gia' in vista (a destra su
                  desktop, subito sotto su mobile), invece di saltare a un altra pagina: il modulo
                  E' gia' la prova che serve, non va nascosto dietro la registrazione. */}
              <Button asChild size="lg">
                <a href="#verifica-disponibilita">Controlla la disponibilità</a>
              </Button>
              <Button asChild size="lg" variant="outline">
                <Link to="/login">Ho già un account</Link>
              </Button>
            </div>
          </div>

          <div id="verifica-disponibilita" className="flex scroll-mt-20 md:justify-end">
            <VerificaDisponibilita />
          </div>
        </section>

        <section className="border-y bg-card">
          <div className="mx-auto max-w-5xl px-4 py-12">
            <h2 className="text-titolo mb-8">Come funziona</h2>

            <ol className="relative grid gap-8 md:grid-cols-3 md:gap-6">
              {/* Il filo. Verticale su schermo stretto, orizzontale da md in su: sta dietro le
                  tacche, che lo coprono con il colore della superficie. */}
              <span
                aria-hidden="true"
                className="absolute top-0 bottom-0 left-[11px] w-px bg-border md:top-[11px] md:right-0 md:bottom-auto md:left-0 md:h-px md:w-auto"
              />

              {PASSAGGI.map(({ titolo, testo }, i) => (
                <li key={titolo} className="relative pl-9 md:pt-9 md:pl-0">
                  <span className="text-nota absolute top-0 left-0 flex size-6 items-center justify-center rounded-full border bg-card tabular-nums text-muted-foreground">
                    {i + 1}
                  </span>
                  <h3 className="text-sezione">{titolo}</h3>
                  <p className="text-corpo mt-1 text-muted-foreground text-pretty">{testo}</p>
                </li>
              ))}
            </ol>
          </div>
        </section>

        <section className="mx-auto max-w-5xl px-4 py-12">
          <h2 className="text-titolo mb-2">Le zone</h2>
          <p className="text-muted-foreground mb-8 text-pretty">
            Puoi indicare dove preferisci sedere: se in quella zona c’è posto, il tavolo si assegna
            lì.
          </p>
          {/* RESTYLE: tre card separate, ognuna col proprio bordo - prima erano un unico
              riquadro diviso da filetti, che le faceva leggere come un elenco unico invece che
              come tre scelte a se' stanti (coerente con card.tsx: bordo 1px, niente ombra). */}
          <ul className="grid gap-4 md:grid-cols-3">
            {ZONE_VETRINA.map((zona) => (
              <li key={zona.nome} className="rounded-xl border bg-card p-5 ring-1 ring-foreground/10">
                <h3 className="text-sezione">{zona.nome}</h3>
                <p className="text-corpo mt-1 text-muted-foreground text-pretty">
                  {zona.descrizione}
                </p>
              </li>
            ))}
          </ul>
        </section>
      </main>

      <footer className="border-t">
        <div className="text-corpo mx-auto flex max-w-5xl flex-col justify-between gap-4 px-4 py-8 text-muted-foreground sm:flex-row sm:items-center">
          <Logo className="text-foreground" />
          <div className="flex flex-wrap gap-x-6 gap-y-2">
            <Link to="/privacy" className="hover:text-foreground">
              Informativa privacy
            </Link>
            <Link to="/termini" className="hover:text-foreground">
              Termini di servizio
            </Link>
            <Link to="/login" className="underline underline-offset-4 hover:text-foreground">
              Area riservata
            </Link>
          </div>
        </div>
      </footer>
    </div>
  )
}
