import { StrictMode, Suspense } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { router } from './router/index'
import { AuthProvider } from '@/context/AuthContext'
import ErrorBoundary from '@/components/ErrorBoundary'
import { ThemeProvider } from '@/context/ThemeContext'
import PortaleInManutenzione from '@/components/PortaleInManutenzione'
import { inManutenzione } from '@/lib/manutenzione'
import { queryClient } from '@/lib/queryClient'
import { DevtoolsQuery } from '@/components/DevtoolsQuery'
import { PageLoading } from '@/components/PageState'
import './index.css'
import { Toaster } from 'sonner'

// V3-009: dopo un rilascio i file delle pagine cambiano nome. Chi aveva il sito aperto chiederebbe
// un file che non c'e' piu': si ricarica la pagina e prende quelli nuovi.
window.addEventListener('vite:preloadError', () => window.location.reload())

const radice = createRoot(document.getElementById('root')!)

// V2-016: in manutenzione l'app non si monta affatto, quindi nessuna chiamata al backend.
// V2-028: tolta la schermata «configurazione mancante» (REV-017): l'indirizzo del backend non e'
// piu' una variabile del frontend, /api sta sullo stesso sito.
if (inManutenzione) {
  radice.render(<PortaleInManutenzione />)
} else {
  radice.render(
    <StrictMode>
      {/* REV-014: l'ErrorBoundary sta piu' in alto di AuthProvider, che e' proprio il punto in cui
        un dato di sessione corrotto faceva fallire il primo render lasciando la pagina bianca. */}
      <ErrorBoundary>
        <ThemeProvider>
          <AuthProvider>
            <QueryClientProvider client={queryClient}>
              <Suspense fallback={<PageLoading />}>
                <RouterProvider router={router} />
              </Suspense>
              <DevtoolsQuery />
              <Toaster richColors position="top-right" />
            </QueryClientProvider>
          </AuthProvider>
        </ThemeProvider>
      </ErrorBoundary>
    </StrictMode>
  )
}
