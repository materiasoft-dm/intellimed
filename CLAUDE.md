# IntelliMed — Notes for Claude Code

IntelliMed is a from-scratch rebuild of a legacy Australian practice-management system
("Pracnet"), targeting ASP.NET Core Web API + Blazor (WASM web today, MAUI Blazor Hybrid for
native later). Clean Architecture: `Core` (entities/DTOs/interfaces, no EF) → `Infrastructure`
(EF Core/SQLite, repositories, business-logic services) → `Api` (controllers) / `Web` (Blazor
WASM UI).

Read these before making non-trivial changes — don't duplicate their content here, they're kept
up to date independently:

- `ARCHITECTURE.md` — system architecture, roles/permissions, deployment.
- `STRUCTURE.md` — project/folder layout and code patterns (repository pattern, DTO mapping).
- `REBUILD_PLAN.md` — modernization plan, MAUI setup gotchas, deployment environments.
- `HANDOFF.md` — deep-dive reference on the **legacy** Pracnet system (billing engine, Medicare
  Online claiming, database update tooling, etc.) that this rebuild is ported from/against. Long;
  search it rather than reading top to bottom.
- `BILLING_AND_CLAIMING.md` — primer on Australian medical billing/claiming (MBS, bulk billing,
  DVA, gap, WorkCover/TAC) and how it maps onto this codebase's `BillingCalculator` and friends.

## Build / run

- API + Blazor WASM client: `dotnet run` from `src/IntelliMed.Api` (client is served at `/`).
  Local API: `http://localhost:5284`. DB migrations run automatically on startup (SQLite,
  `intellimed.db`).
- Docker (matches the Render.com staging deploy): `docker build` from the repo root, see
  `Dockerfile` — `dotnet publish src/IntelliMed.Api -c Release`, entrypoint
  `dotnet IntelliMed.Api.dll`.
- Tests: `IntelliMed.Tests` project (`dotnet test`).

## Notes / planned features

- **IntelliSearch** — a global-search button planned for the bottom-right of the screen that pops
  up a general search overlay. Not built yet (no floating search button/modal exists in
  `IntelliMed.Web` as of 2026-07-31) — this is just the agreed name for when it gets built.

## Medicine catalog data sources & licensing

The medicine catalog is assembled from external sources via `IMedicineSourceProvider` (see
`MedicineCsvAssemblyService`). Each source has its own licence — read it before adding a new one or
modifying how an existing one is consumed.

- **PBS API Public v3** (`data-api.health.gov.au/pbs/api/v3/`) — Commonwealth of Australia
  copyright. **Permission is granted to use and redistribute**, provided (1) the copyright statement
  is retained with the data and (2) the content is not modified. In practice: PBS brand/generic/
  strength/form fields are stored verbatim — never normalised, never rewritten. Any splitting
  (e.g. "Tablet 550 mg" → Form/Strength) is presentational only. The copyright notice must be
  surfaced wherever PBS-sourced data is displayed (Medicines admin page footer) and stored
  alongside the data. Full wording is in `README.md` under "Medicine data sources & licensing".
- **Wikidata** — CC0. Safe for commercial reuse. Used for ingredient enrichment and as a fallback
  medicine source.
- **TGA ARTG** — not currently ingested. PBS does not carry ARTG IDs; if/when added, this will be
  a separate provider with its own licence review.

When adding a new source: register it in `ServiceExtensions.AddInfrastructure` as another
`IMedicineSourceProvider` HttpClient registration. `MedicineCsvAssemblyService` resolves all of
them via `IEnumerable<IMedicineSourceProvider>` and merges by name (first source to report a name
wins, so register the most authoritative first).
