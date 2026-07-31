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
