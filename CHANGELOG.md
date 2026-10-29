# Changelog

All notable changes to Gestora. Dates are in `YYYY-MM-DD` format; versions follow
[Semantic Versioning](https://semver.org/). Development started in May 2026.

## [3.0.0] — 2026-10-29

Production-ready release: privacy, security and a public showcase.

### Added
- **Privacy and accounts**: privacy notice and terms of service, accepted on sign-up and again on
  first sign-in after a change; password reset with a 2-hour link; a "My account" page to change
  password or delete the account (past bookings are kept anonymously; deletion is blocked with
  upcoming bookings or for the last Admin).
- **Data retention**: activity log kept for 24 months; inactive customer accounts deleted after 24
  months, with a warning email four weeks before.
- **Bookings report** for the Admin: CSV export for any date range, ready for Excel.
- **Public feedback form** at `/pareri`, delivered by email and never stored, with a honeypot field
  and a limit of 2 messages per 30 days per address.
- **Maintenance mode**, switched on by configuration on both backend (503 on `/api`) and frontend.
- After 5 wrong passwords the **address** that failed is blocked for 15 minutes, not the account:
  nobody can keep the Admin locked out by failing on purpose, and the block no longer reveals
  which emails are registered.
- Security headers on API and frontend (CSP, HSTS, `X-Frame-Options`, `nosniff`); Data Protection
  keys stored in the database, so email links survive restarts.
- Database backup procedure and uptime monitoring of `/health`.
- English README with screenshots, changelog, copyright notice and a showcase data seed
  (`--seed-vetrina`).

### Changed
- Backend upgraded to **.NET 10 LTS** (ASP.NET Core and Entity Framework Core 10).
- Hardening from a security audit: a customer touching someone else's booking now gets "not found"
  instead of revealing that it exists; the same password rules (8 characters, uppercase, number,
  symbol) enforced at every layer; frontend dependencies updated, with no known advisory left in
  what reaches the browser.

### Fixed
- "Seats left" could promise a place no free table could host (e.g. 3 seats left over on a table
  of 8 taken by 5). It is now the smaller of the remaining cover cap and the largest party the free
  tables can seat.

## [2.1.0] — 2026-10-15

### Added
- **Email confirmation** on self sign-up (24-hour link); accounts created by an Admin are
  confirmed from the start.
- Rate limits on sign-up and on the public availability check.
- Nightly cleanup of accounts that never signed in.
- Automatic token renewal, within a 12-hour maximum session.

### Changed
- Tables can be edited even with future bookings: only changes that would break a booking are
  blocked.
- Switching off a zone is blocked while it still has bookings to serve.
- Deleting bookings or users and their activity-log entry now happen in a single transaction;
  user name and email changes are logged.
- Customer name limited to 200 characters.

## [2.0.0] — 2026-10-08

### Added
- Cover limits per booking moved to configuration: up to 20 guests online (customers and public
  page), up to 50 for staff taking bookings by phone.

### Changed
- **No limit on joined tables**: as many tables of the same zone as needed, always the
  combination that wastes the fewest seats.
- A time slot's cover cap can no longer exceed the seats in the room; changes to tables or zones
  that would push the seats below a cap are blocked.

### Fixed
- The real client IP address is now read correctly behind Azure's proxy, for login rate limiting
  and the activity log.

## [1.2.0] — 2026-10-02

### Fixed
- Corrections from a full code audit of backend and frontend, including the sign-up flow.

## [1.1.0] — 2026-09-30

### Added
- New visual design with light and dark theme; navigable bookings and dashboard.
- Cover cap protected against concurrent bookings with a row lock.

### Changed
- Hosting moved from Railway to Azure App Service and Neon (PostgreSQL), with Docker images
  published by GitHub Actions.

## [1.0.1] – [1.0.6] — 2026-09-03 to 2026-09-08

An end-to-end review in eleven phases (99 findings):
- Security hardening and a one-time setup screen for the first Admin.
- Bug fixes and robustness work on backend and frontend.
- Responsive layout, accessibility and an availability indicator.
- Time-slot overlap check that cannot be bypassed.

## [1.0.0] — 2026-08-27

First release: zones, tables and time slots; bookings from staff and customers; automatic table
assignment joining up to four tables; Admin, Staff and Customer roles; daily and weekly
dashboard; public availability check; nightly jobs for completing bookings, marking no-shows and
cleaning up old data.
