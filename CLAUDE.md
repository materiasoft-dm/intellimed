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

## graphify — how it is set up here

The block below is written by `graphify claude install` (re-run it to refresh, `graphify claude
uninstall` to remove). What is specific to this repo:

- `graphify-out/` is **gitignored and local** (~8.5k nodes, built 2026-10-30), so each machine builds
  its own: `graphify update .` for code, or `/graphify --update` in Claude Code when docs changed.
- Git hooks keep the code part current after every commit, checkout, merge and rewrite. They live in
  `.git/hooks` and are not committed, so on a new clone run `graphify hook install` once.
  `.claude/settings.json` (also local, it hard-codes this machine's graphify path) registers the
  Claude Code hooks that nudge towards the graph.
- Docs are **not** refreshed by the hooks — they need `/graphify --update` (semantic extraction).
- The graph can lag the code; confirm what it says against the source before relying on it.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- When graphify-out/graph.json exists, run `graphify query "<question>"` before your first grep, search or source-file read for a codebase question, and put this rule in every subagent prompt that explores code. Read raw files only for lines the graph or an earlier read already located. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
- A committed graphify-out/graph.json is rewritten whole on every build, so a large diff to it in a pull request is expected. Do not flag it as churn or suggest skipping the graph rebuild or its hooks.
