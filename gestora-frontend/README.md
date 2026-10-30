# Gestora — frontend

Interfaccia web di Gestora, gestionale di prenotazioni per attività con posti a sedere
(ristoranti, pub, pizzerie): vetrina pubblica, prenotazioni, dashboard, configurazione della sala.

React 19 + TypeScript + Vite · shadcn/ui + Tailwind CSS v4 · TanStack Query v5 ·
React Hook Form + Zod · React Router v7.

## Avvio in locale

```powershell
npm install
npm run dev
```

Richiede il backend (`GestoraWebApi`) attivo su `localhost:5099`: Vite gli gira le chiamate a
`/api` (`vite.config.ts`, dal `V2-028`). Ascolta su `localhost:5173`.

## Verifica

```powershell
npm run lint
npm test
npm run build
```

Documentazione specifica di questo frontend: `CLAUDE.md`.
