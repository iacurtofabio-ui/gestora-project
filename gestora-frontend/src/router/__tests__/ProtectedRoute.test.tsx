import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import ProtectedRoute from '@/router/ProtectedRoute'
import { AuthContext, type AuthUser } from '@/context/auth-context'

/**
 * REV-047 — accesso e ruoli.
 *
 * E' la regola su cui poggia tutta la separazione fra Admin, Staff e Cliente lato interfaccia:
 * finora era verificata solo aprendo l'app con tre utenti diversi. L'autorizzazione vera resta
 * quella del backend, ma se questa guardia si rompe l'utente vede voci e pulsanti che non gli
 * competono, e lo scopre solo con un 403.
 */
function renderConUtente(utente: AuthUser | null, ruoliAmmessi: string[]) {
  const valore = {
    user: utente,
    isAuthenticated: !!utente,
    login: () => utente as AuthUser,
    logout: () => {},
  }

  return render(
    <AuthContext.Provider value={valore}>
      <MemoryRouter initialEntries={['/protetta']}>
        <Routes>
          <Route path="/login" element={<p>pagina di login</p>} />
          <Route path="/unauthorized" element={<p>accesso negato</p>} />
          <Route
            path="/protetta"
            element={
              <ProtectedRoute allowedRoles={ruoliAmmessi}>
                <p>contenuto riservato</p>
              </ProtectedRoute>
            }
          />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>
  )
}

const admin: AuthUser = { id: '1', email: 'admin@gestora.it', roles: ['Admin'], scadenza: '2099-01-01T00:00:00Z' }
const cliente: AuthUser = { id: '2', email: 'cliente@gestora.it', roles: ['Cliente'], scadenza: '2099-01-01T00:00:00Z' }
const staffEAdmin: AuthUser = {
  id: '3',
  email: 'capo@gestora.it',
  roles: ['Staff', 'Admin'],
  scadenza: '2099-01-01T00:00:00Z',
}

describe('ProtectedRoute', () => {
  it('manda al login chi non ha effettuato l accesso', () => {
    renderConUtente(null, ['Admin'])

    expect(screen.getByText('pagina di login')).toBeInTheDocument()
    expect(screen.queryByText('contenuto riservato')).not.toBeInTheDocument()
  })

  it('mostra il contenuto a chi ha il ruolo richiesto', () => {
    renderConUtente(admin, ['Admin'])

    expect(screen.getByText('contenuto riservato')).toBeInTheDocument()
  })

  it('manda a /unauthorized chi e autenticato ma con il ruolo sbagliato', () => {
    renderConUtente(cliente, ['Admin'])

    expect(screen.getByText('accesso negato')).toBeInTheDocument()
    expect(screen.queryByText('contenuto riservato')).not.toBeInTheDocument()
  })

  it('basta uno dei ruoli ammessi, non servono tutti', () => {
    renderConUtente(cliente, ['Admin', 'Staff', 'Cliente'])

    expect(screen.getByText('contenuto riservato')).toBeInTheDocument()
  })

  it('un utente con piu ruoli passa se anche uno solo e ammesso', () => {
    renderConUtente(staffEAdmin, ['Admin'])

    expect(screen.getByText('contenuto riservato')).toBeInTheDocument()
  })

  it('nega l accesso quando la lista dei ruoli ammessi e vuota', () => {
    // Caso limite ma importante: una route configurata male non deve diventare aperta a tutti.
    renderConUtente(admin, [])

    expect(screen.getByText('accesso negato')).toBeInTheDocument()
  })
})
