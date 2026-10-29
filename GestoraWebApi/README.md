# Gestora — backend

API per Gestora, gestionale di prenotazioni per attività con posti a sedere (ristoranti, pub,
pizzerie): configurazione della sala, prenotazioni, assegnazione automatica dei tavoli.

ASP.NET Core 10 · Entity Framework Core 10 + PostgreSQL · ASP.NET Identity + JWT · Quartz.NET ·
FluentValidation · AutoMapper · Serilog.

## Avvio in locale

```powershell
dotnet run
```

Richiede PostgreSQL attivo e i due User Secrets (`ConnectionStrings:DefaultConnection`,
`JwtSettings:Secret`) — vedi `RUNBOOK.md` §7 nella cartella principale. Ascolta su `localhost:5099`.

## Verifica

Dalla cartella principale (i test stanno in `GestoraWebApi.Tests/`, accanto a questa cartella):

```powershell
dotnet test Gestora.sln
```

Documentazione specifica di questo backend: `CLAUDE.md`.
