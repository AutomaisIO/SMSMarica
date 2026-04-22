# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository state

This is a **greenfield multi-project repository**. At time of writing, every subproject (`SMSMarica.server/`, `SMSMarica.front/`, `SMSMarica.cidadao.app/`, `SMSMarica.agente.app/`) contains only a `README.md` describing intent. No source code, build files, tests, or toolchain configuration exist yet — so there are **no build/test/lint commands to document here**. When bootstrapping any subproject, stay within the stack the READMEs commit to (below) rather than introducing a different one.

Documentation in this repo is written in **Portuguese (pt-BR)**. Match that language for README/doc edits, commit messages, and code comments unless the user says otherwise.

## The four subprojects

| Path | Stack | Audience |
|------|-------|----------|
| `SMSMarica.server/` | ASP.NET Core + Entity Framework Core + PostgreSQL | Backend API — source of truth for all three clients |
| `SMSMarica.front/` | React + Vite | Web admin — two profiles: **operador** (daily ops) and **gestor** (dashboards) |
| `SMSMarica.cidadao.app/` | Flutter (iOS + Android) | Patient app — schedule, ride ETA, seat view, driver rating |
| `SMSMarica.agente.app/` | Flutter, **Android only** | Driver app — routes, GPS posting, geofencing, external navigation (Waze/Maps) |

All three clients consume the `SMSMarica.server` API exclusively. The clients do not talk to each other or directly to the database.

## Database rule — non-negotiable

The backend connects to a **PostgreSQL cluster/database shared with other products** (notably Automais.IO), conventionally called `defaultdb`. Isolation is **by schema, not by database**:

- All SMSMarica tables, views, indexes, and FKs live in schema **`smsmarica`** (lowercase).
- EF Core migrations must emit DDL only against `smsmarica`. Set this centrally — typically `modelBuilder.HasDefaultSchema("smsmarica")` in `OnModelCreating`, or per-entity `[Table(..., Schema = "smsmarica")]`.
- **Never** reference, join, or FK tables in other schemas (e.g., Automais.IO's). Zero cross-schema dependencies.
- Connection strings may point at the shared `defaultdb` — do not treat the shared DB as a reason to relax the schema rule.

If future high-volume GPS ingestion needs a time-series store (InfluxDB or similar), that is a separate system; the administrative model stays in `smsmarica`.

## Domain concepts that shape the model

These are product decisions the code must reflect — they are not derivable from any existing file because there is no code yet:

- **Paciente** — cadastral fields must be sufficient for CNS (Cadastro Nacional de Saúde) equivalence (identification, documents, contacts, address, care links).
- **Tratamento + periodicidade** — a treatment attached to a patient declares a cadence (e.g. daily from date X for N sessions, every 2 days, 1×/week). The system **auto-generates** the per-day transport demand (pickup at home → destination → return) from this cadence. Daily lists drive operator workflow.
- **Unidade** and **Paciente** both carry **GPS coordinates** — required for routing and future algorithms.
- **Veículo → Fileiras → Assentos** — model vehicles like aircraft seating but **do not assume uniform rows**. Each row declares its own seat count. The UI later renders a faithful layout (e.g. a specific van) and allocates patient + optional acompanhante to marked seats.
- **Rastreamento / geofencing** — driver app posts GPS periodically; geofences mark arrivals at residences and units. High-frequency GPS may later move to a time-series store; keep the model open to that.
- **Cidadão app features** — confirm pickup availability, see allocated seat, ETA for pickup and return trip (Uber-style), rate driver, submit suggestions/complaints. WhatsApp integration is planned (channel TBD).
- **Agente app constraint** — Android only this phase; uses native intents to launch Waze/Google Maps; foreground/background location must respect Google Play policy and LGPD.

## When adding code to a subproject

- `SMSMarica.server`: ASP.NET Core Web API project + EF Core + Npgsql. Expose OpenAPI/Swagger for contract sharing with front and apps. First migration should establish the `smsmarica` schema default.
- `SMSMarica.front`: React + Vite SPA. Plan for two role-scoped experiences (operador / gestor) — route-level or module-level separation is fine; do not hardcode role checks scattered through components.
- Flutter apps: target the stated platforms only. `agente.app` must not silently grow an iOS build target — that is a product decision, not a cleanup.

## Commit / branch conventions

Not yet established. If you are the one setting them up, record the decision in the root `README.md` section 9 ("Próximos passos") rather than inventing a convention silently.
