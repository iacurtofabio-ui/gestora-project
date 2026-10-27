import { useEffect, useState } from 'react'
import { Outlet, NavLink, Link, useNavigate, useLocation } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { LogOutIcon, MenuIcon, UserIcon, XIcon } from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'
import { onSessionExpired } from '@/lib/session'
import { Button } from '@/components/ui/button'
import { Logo } from '@/components/Logo'
import { ThemeToggle } from '@/components/ThemeToggle'
import { PresaVisionePrivacy } from '@/components/PresaVisionePrivacy'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { cn } from '@/lib/utils'

/**
 * RESTYLE (Fase 6): barra superiore fissa al posto della sidebar laterale — direzione Docker
 * Hub, dove la navigazione principale è orizzontale, sempre in vista, e la voce attiva si segna
 * con una sottolineatura invece di uno sfondo pieno (lo stesso linguaggio dei loro tab).
 *
 * Sotto i 1024px le voci vanno in un pannello a scomparsa: la logica di apertura/chiusura
 * (`menuAperto`) è la stessa di prima, cambia solo cosa si apre.
 */
function linkClass({ isActive }: { isActive: boolean }) {
  return cn(
    'text-corpo relative px-1 py-2 font-medium transition-colors whitespace-nowrap',
    'after:absolute after:inset-x-0 after:-bottom-px after:h-0.5 after:rounded-full after:transition-colors',
    isActive
      ? 'text-foreground after:bg-primary'
      : 'text-muted-foreground after:bg-transparent hover:text-foreground'
  )
}

function linkClassMobile({ isActive }: { isActive: boolean }) {
  return cn(
    'text-corpo rounded-md px-3 py-2 font-medium transition-colors',
    isActive ? 'bg-evidenza text-foreground' : 'text-muted-foreground hover:bg-evidenza/60'
  )
}

/** Le iniziali dell'email per l'avatar del menu utente: "mario.rossi@..." → "MR" se c'è un
 * punto/separatore prima della @, altrimenti le prime due lettere della parte locale. */
function iniziali(email: string | undefined): string {
  if (!email) return '?'
  const locale = email.split('@')[0]
  const parti = locale.split(/[._-]/).filter(Boolean)
  const testo = parti.length >= 2 ? parti[0][0] + parti[1][0] : locale.slice(0, 2)
  return testo.toUpperCase()
}

export default function AppLayout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const queryClient = useQueryClient()
  const [menuAperto, setMenuAperto] = useState(false)

  // Un cambio di pagina chiude il pannello mobile: altrimenti restava aperto sopra la pagina
  // appena raggiunta. Aggiornamento durante il render (non in un effetto): e' il pattern che
  // React consiglia per "adeguare lo stato quando cambia un input", niente giro in piu' di commit.
  const [pathnamePrecedente, setPathnamePrecedente] = useState(location.pathname)
  if (location.pathname !== pathnamePrecedente) {
    setPathnamePrecedente(location.pathname)
    setMenuAperto(false)
  }

  // REV-025 — scadenza della sessione gestita in modo pulito.
  // Il token e' gia' stato rimosso dall'interceptor: qui si allinea lo stato di React (logout),
  // si svuota la cache delle query per non lasciare i dati del vecchio utente in memoria, si
  // spiega all'utente cosa e' successo e si naviga al login senza ricaricare la pagina.
  useEffect(
    () =>
      onSessionExpired(() => {
        logout()
        queryClient.clear()
        toast.error("Sessione scaduta. Effettua di nuovo l'accesso.")
        navigate('/login', { replace: true })
      }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [navigate, queryClient]
  )

  function handleLogout() {
    logout()
    queryClient.clear()
    navigate('/login', { replace: true })
  }

  // Un unico elenco, con ruoli e gruppo per voce: il filtro per ruolo sta in un posto solo, non
  // sparso fra tre condizioni diverse come prima. Ordine per flusso di lavoro: uso quotidiano
  // (Dashboard, Prenotazioni), poi la sala (Zone, Tavoli, Fasce orarie), poi l'amministrazione.
  const vociMenu = [
    { to: '/dashboard', label: 'Dashboard', ruoli: ['Admin', 'Staff'], gruppo: null },
    { to: '/prenotazioni', label: 'Prenotazioni', ruoli: ['Admin', 'Staff', 'Cliente'], gruppo: null },
    { to: '/zone', label: 'Zone', ruoli: ['Admin', 'Staff'], gruppo: 'Sala' },
    // La rotta resta /postazioni (REV-056, non si riapre): la voce di menu si chiama "Tavoli"
    // perché è cosi' che la pagina si intitola gia'.
    { to: '/postazioni', label: 'Tavoli', ruoli: ['Admin', 'Staff'], gruppo: 'Sala' },
    { to: '/fasce-orarie', label: 'Fasce orarie', ruoli: ['Admin', 'Staff'], gruppo: 'Sala' },
    { to: '/admin-utenti', label: 'Utenti', ruoli: ['Admin'], gruppo: 'Amministrazione' },
  ] as const

  const ruoliUtente = user?.roles ?? []
  const linksVisibili = vociMenu.filter((voce) => voce.ruoli.some((r) => ruoliUtente.includes(r)))

  return (
    <div className="flex min-h-screen flex-col bg-background">
      <header className="sticky top-0 z-40 h-14 shrink-0 border-b bg-card">
        <div className="mx-auto flex h-full max-w-6xl items-center gap-4 px-4 md:px-6">
          <Button
            variant="ghost"
            size="icon-sm"
            className="lg:hidden"
            onClick={() => setMenuAperto((a) => !a)}
            aria-label={menuAperto ? 'Chiudi menu' : 'Apri menu'}
          >
            {menuAperto ? <XIcon className="h-5 w-5" /> : <MenuIcon className="h-5 w-5" />}
          </Button>

          <Link to="/dashboard" className="flex shrink-0 items-center gap-2" aria-label="Gestora">
            <Logo className="text-foreground" />
          </Link>

          {/* Voci di menu, orizzontali, da 1024px in su. I due gruppi (uso quotidiano / sala) si
              distinguono con un piccolo distacco, non con un'etichetta: in barra orizzontale
              un'etichetta di gruppo affollerebbe. */}
          <nav className="hidden flex-1 items-center gap-5 lg:flex">
            {linksVisibili.map((link, indice) => (
              <span
                key={link.to}
                className={cn(
                  indice > 0 &&
                    link.gruppo !== null &&
                    link.gruppo !== linksVisibili[indice - 1]?.gruppo &&
                    'ml-2 border-l pl-6'
                )}
              >
                <NavLink to={link.to} className={linkClass}>
                  {link.label}
                </NavLink>
              </span>
            ))}
          </nav>

          <div className="ml-auto flex shrink-0 items-center gap-1">
            <ThemeToggle />
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="ghost"
                  size="icon-sm"
                  className="ml-1 rounded-full bg-secondary text-secondary-foreground"
                  aria-label="Menu utente"
                >
                  <span className="text-nota font-semibold">{iniziali(user?.email)}</span>
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-56">
                <DropdownMenuLabel className="font-normal">
                  <p className="text-corpo truncate text-foreground">{user?.email}</p>
                  <p className="text-nota truncate text-muted-foreground">{user?.roles.join(', ')}</p>
                </DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem onSelect={() => navigate('/account')}>
                  <UserIcon />
                  Il mio account
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem onSelect={handleLogout} variant="destructive">
                  <LogOutIcon />
                  Esci
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>

        {/* Pannello mobile: le stesse voci, verticali, sotto la barra. */}
        {menuAperto && (
          <nav className="border-t bg-card p-3 lg:hidden">
            <div className="mx-auto flex max-w-6xl flex-col gap-1">
              {linksVisibili.map((link, indice) => {
                const gruppoPrecedente = linksVisibili[indice - 1]?.gruppo
                const mostraSeparatore = link.gruppo !== null && link.gruppo !== gruppoPrecedente
                return (
                  <div key={link.to}>
                    {mostraSeparatore && (
                      <p className="text-nota text-muted-foreground mt-2 mb-1 px-3 uppercase">
                        {link.gruppo}
                      </p>
                    )}
                    <NavLink to={link.to} className={linkClassMobile}>
                      {link.label}
                    </NavLink>
                  </div>
                )
              })}
            </div>
          </nav>
        )}
      </header>

      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-4 md:px-6 md:py-6">
        <PresaVisionePrivacy>
          <Outlet />
        </PresaVisionePrivacy>
      </main>
    </div>
  )
}
