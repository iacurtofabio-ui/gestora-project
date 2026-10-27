import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Logo } from '@/components/Logo'
import { ThemeToggle } from '@/components/ThemeToggle'
import { INFORMATIVA_DATA } from '@/lib/privacy'

/**
 * V2-019 — guscio comune di informativa privacy e termini: pagine pubbliche da leggere, senza
 * token e fuori da SetupGuard, come /conferma-email. Il testo vive nelle due pagine, qui solo
 * l'impaginazione: colonna stretta, titoli di sezione, data della versione in cima.
 */
export function PaginaLegale({ titolo, children }: { titolo: string; children: ReactNode }) {
  return (
    <div className="min-h-screen flex flex-col bg-background">
      <header className="border-b">
        <div className="mx-auto flex h-14 max-w-3xl items-center justify-between px-4">
          <Link to="/" aria-label="Torna alla pagina iniziale">
            <Logo className="text-foreground" />
          </Link>
          <ThemeToggle />
        </div>
      </header>

      <main className="mx-auto w-full max-w-3xl flex-1 px-4 py-10">
        <h1 className="text-titolo">{titolo}</h1>
        <p className="text-nota mt-1 text-muted-foreground">Versione del {INFORMATIVA_DATA}</p>
        <div className="text-corpo mt-8 space-y-8 text-pretty [&_a]:underline [&_a]:underline-offset-4 [&_h2]:text-sezione [&_h2]:mb-2 [&_li]:mt-1 [&_p+p]:mt-2 [&_ul]:list-disc [&_ul]:pl-5">
          {children}
        </div>
      </main>

      <footer className="border-t">
        <div className="text-corpo mx-auto flex max-w-3xl flex-wrap gap-x-6 gap-y-2 px-4 py-6 text-muted-foreground">
          <Link to="/" className="hover:text-foreground">
            ← Torna alla pagina del locale
          </Link>
          <Link to="/privacy" className="hover:text-foreground">
            Informativa privacy
          </Link>
          <Link to="/termini" className="hover:text-foreground">
            Termini di servizio
          </Link>
        </div>
      </footer>
    </div>
  )
}
