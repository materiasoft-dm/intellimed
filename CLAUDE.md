# IntelliMed — Notes for Claude Code

IntelliMed is a from-scratch rebuild of a legacy Australian practice-management system
("Pracnet"), targeting ASP.NET Core Web API + a React web client (native clients later). Clean
Architecture: `Core` (entities/DTOs/interfaces, no EF) → `Infrastructure` (EF Core/SQLite, repositories,
business-logic services) → `Api` (controllers, and it serves the web client) / `Client` (React 19 + Vite +
TypeScript SPA in `src/IntelliMed.Client`, Bootstrap 5 styling). The client replaced the original Blazor WASM
UI (`IntelliMed.Web`, removed 2026-10-31) page for page — see `src/IntelliMed.Client/PORT_STATUS.md`.

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

- API + web client: `dotnet run` from `src/IntelliMed.Api` (client is served at `/`). Building the API also
  builds the React client (`npm run build` in `src/IntelliMed.Client`, skipped when its sources are unchanged)
  and copies `dist/` to the output `wwwroot`, so Node/npm must be installed. For client work with hot reload,
  run `npm run dev` in `src/IntelliMed.Client` (Vite on :5173, proxies `/api` to :5284) alongside the API.
- Client types: `src/IntelliMed.Client/src/api/types.gen.ts` is generated from the C# DTOs/entities —
  regenerate with `python -I scripts/generate-client-types.py` after changing a DTO. Never hand-edit it.
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
  the web client as of 2026-10-31) — this is just the agreed name for when it gets built.

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

### Querying this graph well

`graphify query` matches the words of a question against node names literally (case-folded
substring, no synonyms), so a sentence like "what stops a controller shipping without a permission
check" lands on generic nodes (`Action`, `ControllerBase`, `Task`) that connect to everything and
returns noise. What works:

1. **Query with identifiers, not sentences** — `graphify query "RequirePage PageAccess coverage"`.
   Pick the words from the entry points below or from `graphify god-nodes`.
2. **Start from a node** — `graphify explain "ResultIngestService"`, or
   `graphify query "<topic>" --seed ResultIngestService`.
3. **"What depends on X / what breaks if X changes"** — `graphify affected "RequirePageAttribute"`
   (reverse traversal; the most precise tool here).
4. **Ambiguous names** (exit 3) exist as both code and a doc concept — retry as
   `src/IntelliMed.Api/Authorization/RequirePageAttribute.cs::RequirePageAttribute` (path::Name).
5. **Never `query`/traverse from a framework stub** (`ControllerBase`, `Task`, `List`,
   `IEnumerable`, `DateTime`): they have no source file and link hundreds of unrelated nodes. Use
   `--context call` to follow calls only.
6. **Attribute usages** (`[RequirePage("…")]`, `[Authorize]`) link to a stub named WITHOUT the
   "Attribute" suffix, so ask `graphify affected "RequirePage"` — it lists every gated action across
   the 14 controllers — not `affected "RequirePageAttribute"`, which only shows the class's own
   tests. `affected` is the right tool for stubs; `query` is not.

Entry points (exact node names; ones marked † also exist as a doc concept, so use `path::Name`):

| Area | Start from |
|---|---|
| Permissions | `RequirePageAttribute`, `PageAccess`, `RolePermissionsController`, `PermissionTree`, `RoleEnforcementApiTests`, `AuthorizationCoverageTests`†, `RolePermissions`† |
| Results inbox | `ResultsController`, `ResultIngestController`, `ResultIngestService`, `ResultRepository`, `ResultReport`, `Results.tsx`, `Hl7ResultParser`† |
| Billing | `BillingCalculator`, `InvoiceRepository`, `InvoicesController`, `ReceiptRepository`, `FeeSchedulesController`, `InvoiceDetail.tsx`, `DerivedFeeCalculator`† |
| Prescribing | `PrescribeNew.tsx`, `PrescriptionsController`, `PrescriptionRepository`, `MedicationInteractionChecker`†, `MedicineCsvAssemblyService`†, `ArtgEnrichmentService`† |
| Clients & appointments | `AddClient.tsx`, `ClientsController`, `ClientRepository`, `AppointmentsController`, `AppointmentRepository`, `AppointmentCalendar.tsx` |
| Data | `AppDbContext`, `EntityMapper`, `ServiceExtensions` |

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- When graphify-out/graph.json exists, run `graphify query "<question>"` before your first grep, search or source-file read for a codebase question, and put this rule in every subagent prompt that explores code. Read raw files only for lines the graph or an earlier read already located. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
- A committed graphify-out/graph.json is rewritten whole on every build, so a large diff to it in a pull request is expected. Do not flag it as churn or suggest skipping the graph rebuild or its hooks.
