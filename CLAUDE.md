# IntelliMed — Notes for Claude Code

IntelliMed is a from-scratch rebuild of a legacy Australian practice-management system
("Pracnet"), targeting ASP.NET Core Web API + Blazor (WASM web today, MAUI Blazor Hybrid for
native later). Clean Architecture: `Core` (entities/DTOs/interfaces, no EF) → `Infrastructure`
(EF Core/SQLite, repositories, business-logic services) → `Api` (controllers) / `Web` (Blazor
WASM UI).

**Start here if you're picking up work:** `OPEN_THREADS.md` — known defects, decisions waiting on
the user, and what's agreed but unbuilt. It is the current-state file; the rest below is reference.

Read these before making non-trivial changes — don't duplicate their content here, they're kept
up to date independently:

- `OPEN_THREADS.md` — **live state**: known defects, blocked decisions, repo/deployment status.
- `USER_GUIDE.md` — every shipped feature and how it's used. The fastest way to learn what exists.
- `ARCHITECTURE.md` — system architecture, roles/permissions, deployment, **required config
  (`Jwt__Key` — the app won't start without it outside Development)**.
- `STRUCTURE.md` — project/folder layout and code patterns (repository pattern, DTO mapping).
- `REBUILD_PLAN.md` — modernization plan, MAUI setup gotchas, deployment environments.
- `HANDOFF.md` — deep-dive reference on the **legacy** Pracnet system (billing engine, Medicare
  Online claiming, database update tooling, etc.) that this rebuild is ported from/against. Long;
  search it rather than reading top to bottom.
- `BILLING_AND_CLAIMING.md` — primer on Australian medical billing/claiming (MBS, bulk billing,
  DVA, gap, WorkCover/TAC) and how it maps onto this codebase's `BillingCalculator` and friends.
- `PATIENT_VISIT_WORKFLOW.md` — the single-visit walkthrough (book → consult → prescribe → bill),
  written for clinic staff.
- `PRESCRIBING_PARITY.md` — what the prescribing wizard does and doesn't match vs legacy Medinet,
  section by section against that system's own business-rules spec.
- `MEDICINE_DATA_ENRICHMENT.md` — **contains a confirmed high-severity defect** (`Medicine.Schedule`
  is null for the entire synced catalogue, disabling the Schedule 8 gate), plus a costed roadmap for
  closing the MIMS data gap using PBS fields already available on the current subscription.

## Build / run

- API + Blazor WASM client: `dotnet run` from `src/IntelliMed.Api` (client is served at `/`).
  Local API: `http://localhost:5284`. DB migrations run automatically on startup (SQLite,
  `intellimed.db`).
- Docker: `docker build` from the repo root, see `Dockerfile` — `dotnet publish src/IntelliMed.Api
  -c Release`, entrypoint `dotnet IntelliMed.Api.dll`. (It was written for the Render.com staging
  deploy, which is no longer used.)
- IIS upload (e.g. the bsite.net test site): `scripts/publish-selfcontained.ps1` builds a
  self-contained Windows folder in `publish/` (gitignored), puts the JWT key in its `appsettings.json`
  and boots it to prove it starts. Upload the whole folder; skip only `intellimed.db` after the first
  deploy. Details in `OPEN_THREADS.md` → Deployment.
- Tests: `IntelliMed.Tests` project (`dotnet test`). The UI tests are slow (~25 min, a visible
  browser); `HEADLESS=1` runs them unattended.

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
- **TGA ARTG** — ingested, but **not** as an `IMedicineSourceProvider`. It is a manual admin upload
  (Admin > Medicines > *Poisons Scheduling*), not part of Sync, because the data arrives as a CSV
  export the operator downloads from <https://compliance.health.gov.au/artg/> rather than an API.
  It backfills `Schedule`, `ArtgId`, `Route` and `Form` and never overwrites an existing value —
  it adds no new medicines. Commonwealth of Australia copyright, same terms as PBS. **The export
  must have the `Medicines` filter applied**; the unfiltered one has 8 columns and no scheduling.
  See `ArtgEnrichmentService` and `MEDICINE_DATA_ENRICHMENT.md` §1.

When adding a new source: register it in `ServiceExtensions.AddInfrastructure` as another
`IMedicineSourceProvider` HttpClient registration. `MedicineCsvAssemblyService` resolves all of
them via `IEnumerable<IMedicineSourceProvider>` and merges by name (first source to report a name
wins, so register the most authoritative first).

## graphify — use the knowledge graph before grepping

`graphify-out/` (gitignored, built locally) holds a knowledge graph of the code and the docs:
~8.5k nodes, with `GRAPH_REPORT.md` listing the god nodes, communities and suggested questions.
For any question about how the codebase fits together, check the graph first, then read the files
it points to:

- `graphify query "<question>"` — traverse the graph for context (`--dfs` to follow one path,
  `--budget N` to cap the answer).
- `graphify path "A" "B"` — how two things connect; `graphify explain "X"` — one node and its
  neighbours.
- Read `graphify-out/GRAPH_REPORT.md` before broad architecture work.

Keep it current: after code changes run `graphify update .` (no LLM, no API key); after changing
docs run `/graphify --update`, which re-extracts the changed docs. The graph was last refreshed on
2026-10-30 at commit `cd255c7`. The graph can lag the code, so confirm what it says against the source.
