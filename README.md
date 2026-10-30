# IntelliMed

A from-scratch rebuild of Pracnet, an Australian medical practice-management system — clinical
records, scheduling, billing, and receipting today, with electronic claiming as the next major
frontier.

Pracnet, the legacy system this replaces, is a mature Windows desktop application handling patient
records, scheduling, and Medicare/DVA/health-fund billing for Australian practices. IntelliMed
rebuilds it as a modern web application first — Clean Architecture, ASP.NET Core API, Blazor WASM
client — with native MAUI clients and offline/on-premise deployment planned for later phases rather
than built up front.

The rebuild deliberately follows Pracnet's own scope: clinical record-keeping and demographics, not
clinical charting (that lived in a separate legacy product, Medinet). What IntelliMed owns is the
administrative and financial spine of a practice — who the patient is, when they're seen, what it
costs, what's been paid, and — the piece still being built — what's actually claimed back from
Medicare, DVA, a health fund, or a compensation insurer.

## Getting started

```bash
dotnet run --project src/IntelliMed.Api
```

API + Blazor WASM client are served together at `http://localhost:5284`. SQLite database migrations
run automatically on startup. See [CLAUDE.md](CLAUDE.md) for Docker/staging deployment notes, and its
documentation map for deeper reference: [ARCHITECTURE.md](ARCHITECTURE.md) (system architecture,
roles/permissions), [STRUCTURE.md](STRUCTURE.md) (code layout and patterns),
[REBUILD_PLAN.md](REBUILD_PLAN.md) (modernization plan), [HANDOFF.md](HANDOFF.md) (legacy Pracnet
deep-dive), and [BILLING_AND_CLAIMING.md](BILLING_AND_CLAIMING.md) (Australian medical billing
primer).

## What's built

Six working domains, all live in the current codebase:

- **Client records** — demographics, Medicare/DVA/pension entitlement, health fund membership,
  eHealth IHI numbers, referrals, family relationships, occupations, WorkCover/TAC compensation-claim
  tracking, per-practitioner visibility grants, custom fields, full-text search.
- **Scheduling** — calendar with date navigation, drag-to-span booking, right-click actions,
  configurable appointment types/durations, per-provider weekly hours, waiting room view.
- **Billing & fees** — MBS item catalog synced from the free government feed on demand, fee schedules
  with inheritance and per-health-fund pricing, auto-refreshing CSV feeds, the Multiple Operation Rule,
  and formula-driven "derived" items (assistant, anaesthesia, multi-patient).
- **Payments & receipting** — invoices, write-offs, void, split, and a full receipt/allocation model.
  Consent and payee fields already exist on every invoice (see [Electronic claims](#the-next-frontier-electronic-claims)
  below), and `PaymentMethod` already distinguishes Medicare/DVA rebates from cash/EFTPOS.
- **Medicine catalog** — searchable medicine/ingredient reference, built without a MIMS subscription:
  CSV import, manual entry, live Wikidata ATC-code enrichment, and a PBS API Public v3 sync that
  pulls the official Australian Pharmaceutical Benefits Scheme listing (brand, generic, strength,
  form, manufacturer, AMT concept, ATC code, RPBS flag, restriction level, schedule). Identification
  only — no dosing or interaction checking yet. See
  [Medicine data sources & licensing](#medicine-data-sources--licensing) below.
- **Practice administration** — role-based access (5 roles), multi-clinic support, audit log, email
  templates, notifications, global command-palette search, scheduled SQLite backups, and an embedded
  DB admin tool.

## Architecture & access

```
Core → Infrastructure → Api → Web (Blazor WASM)  →  MAUI (planned)
```

Deployed today from `main` to a Docker container on Render.com (staging) alongside plain `dotnet run`
for local development, both on SQLite; production hosting and a SQL Server migration are still open
decisions. Native desktop clients, a Windows Service host, and offline sync are explicitly scoped as
later-phase work, not gaps in the current plan.

| Role | Scope |
|---|---|
| SuperAdmin | Everything, including user & role management. Cannot be deleted. |
| Admin | Everything except a few SuperAdmin-only screens. |
| Doctor | Clients, appointments, invoices (view) |
| Nurse | Clients, appointments — the only role without invoice access |
| Receptionist | Clients, appointments, invoices — front-desk scope |

## The next frontier: electronic claims

Billing already calculates the right fee, rebate, and gap for every line via `BillingCalculator`.
What doesn't exist yet is sending that claim electronically to whoever pays it, and getting the money
reconciled automatically when they do.

More is already in place than it looks: `Invoice` already carries `ClaimStatus`,
`ClaimSubmissionAuthorised`, `SubmissionAuthorityReceived`, `BenefitAssignmentRequested`, and a
`PayeePractitionerId` override — the consent and paperwork fields a real claim needs. The code's own
comment on `ClaimStatus` says it exactly: nothing drives real transitions until real electronic
claiming exists. It is shown read-only on the invoice and there is no way to set it by hand — it will
follow the claim itself. The paperwork model is done; the wire is not.

| Claim family | Payer | Gateway | Status |
|---|---|---|---|
| Bulk Bill (BBSW) | Medicare | PRODA / Medicare Online | Not transmitted |
| Patient Claim | Medicare, via patient | PRODA / Medicare Online | Not transmitted |
| DVA | Dept of Veterans' Affairs | PRODA / Medicare Online | Not transmitted |
| IMC / OVS | Health fund / overseas cover | PRODA / Medicare Online (Eclipse) | Not transmitted |
| WorkCover / TAC | State insurer / TAC | Third-party biller (e.g. LanternPay) | Tracked, not transmitted |

Fee/rebate/gap math for every row above is already correct. The gap is uniformly the transmission
layer, not the arithmetic.

## The plan: getting to real claiming

No written roadmap for this exists yet elsewhere in the repo — what follows is a proposed sequence,
grounded in how the legacy Pracnet system actually solved the same problem (its Medicare Online/PRODA
integration and its LanternPay integration for WorkCover/TAC), ordered by how much new ground each
phase has to break.

0. **Claim-ready data model — already built.** Consent, authorisation, payee, and a simplified
   claim-status lifecycle already live on `Invoice`. This groundwork doesn't need to be redone, only
   automated.
1. **Claim batching & credential storage.** A dedicated `Claim` entity — one submission can bundle
   several invoices or lines, which a single status enum on `Invoice` can't represent — plus secure,
   clinic-scoped storage for whatever credentials each gateway needs. Every later phase needs this;
   legacy kept per-attempt history too thin (notifications were deleted immediately after being read,
   so "a crash mid-processing can lose status updates") — worth not repeating.
2. **WorkCover/TAC via a billing gateway.** Extends `ClientCompensationClaim`, which already tracks
   these claims, so this phase adds transmission on top of an existing feature instead of starting
   cold. Legacy used LanternPay (a HICAPS product): OAuth2 client-credentials auth, submit → poll for
   notifications → auto-receipt once funded. **Recommended first** — the simplest auth of any family
   here, and it proves the submit/poll/receipt pattern before the harder government gateway.
3. **Medicare Online — Bulk Bill + eligibility check.** The highest-value family: most GP visits are
   bulk-billed. Requires PRODA — an RSA keypair generated per device, activated with a one-time code,
   JWT assertions signed with that key (deliberately no client secret), and a separate
   product-subscription ID sent on every claim. An eligibility pre-check runs before each submission.
   Build against API V2 only — the V1/V2 cutover legacy had to straddle already took effect
   1 July 2026, so there's no dual-version logic to carry.
4. **DVA claiming.** Architecturally the same PRODA transport and processor shape as Bulk Bill, just a
   different fee table and payer — a comparatively small increment once Phase 3 exists, not a new
   integration.
5. **Automated receipting-against-claim.** Closes the loop: when a claim comes back assessed or paid,
   generate the `Receipt`/`ReceiptAllocation` rows automatically instead of by hand. This is the
   specific workflow the project's own billing documentation flags as still missing.
6. **In-patient, overseas & patient claims — later.** IMC (health-fund in-patient claims via Eclipse),
   OVS (overseas visitor cover), and Medicare patient claims — lower priority for a general practice,
   worth adding once the above is proven in production.

**A parallel option worth weighing, not a phase in sequence:** EFTPOS-integrated terminal claiming
(e.g. Tyro) can submit Bulk Bill and health-fund claims over the terminal's own network link,
sidestepping PRODA entirely, at the cost of a specific hardware and merchant relationship. Whether
that trade is worth it is a business decision as much as a technical one.

## Also not yet built

- **Native/offline clients** — MAUI desktop, Windows Service host, offline sync are explicitly Phase
  2+ work.
- **Production hosting** — provider and SQL Server migration are still TBD.
- **Medicine dosing & interactions** — the catalog identifies drugs, not how to prescribe them safely.
- **Patient medication lists** — the medicine catalog isn't attached to any client record yet.

## Medicine data sources & licensing

The medicine catalog is assembled from multiple external sources, each with its own licence. Every
source is registered as an `IMedicineSourceProvider` and aggregated by `MedicineCsvAssemblyService`
into a CSV that "Sync from Sources" (Medicines admin page) imports directly and then runs Wikidata
ingredient enrichment on, all as one background job — adding a new source is one DI line, not a
rewrite. A separate manual Import/Export lets staff upload a hand-edited CSV or download the current
catalog in the same format, independent of the external sources.

| Source | What it provides | Licence / terms | Notes |
|---|---|---|---|
| **PBS API Public v3** (`data-api.health.gov.au/pbs/api/v3/`) | Brand name, generic name, strength, form, manufacturer, AMT concept ID, ATC code, RPBS flag, restriction level, schedule, PBS item code | Commonwealth of Australia copyright — **permission granted to use and redistribute**, provided copyright statements are retained and content is not modified | Public subscription key is acceptable for low-volume use; full data is ~190k items. PBS data is stored verbatim — names/strengths are not normalised. The copyright notice is surfaced in the Medicines admin UI. |
| **Wikidata** (`query.wikidata.org/sparql`) | Identification-layer cross-references: ATC code, Q-id | CC0 | Safe for commercial reuse. Used both for ingredient enrichment and as a fallback medicine source. |
| **TGA ARTG** (`compliance.health.gov.au/artg/`) | Poisons schedule (S2–S8), ARTG ID, dosage form, route of administration | Commonwealth of Australia copyright — same terms as PBS | Manual CSV upload by an admin, not part of Sync — the ARTG tool publishes an export, not an API. Backfills empty fields on existing medicines only; never overwrites, never adds rows. The export needs the **Medicines** filter applied. |
| **Self-compiled starter list** (`medicines_starter.csv`) | A small hand-curated set for first-run / offline use | Project-owned | Used until at least one external source has populated the catalog. |

### PBS copyright — exact wording

Every PBS API response carries this notice in `_meta.info.messages` and it must be retained with any
redistributed data:

> Copyright (c) 2024 Department of Health, Commonwealth of Australia.
> Permission is granted to use and redistribute this content, as long as all copyright statements
> are retained. Permission is not granted to modify this content.
> For further copyright information of this content, please refer to:
> https://www.health.gov.au/using-our-websites/copyright

In practice this means:

1. **Retain** — the copyright statement is displayed wherever PBS-sourced data is shown (Medicines
   admin page footer) and stored alongside the data in the database.
2. **Do not modify** — PBS brand names, generic names, strengths, and forms are stored verbatim. Any
   display formatting (e.g. splitting "Tablet 550 mg" into separate form/strength columns) is a
   presentational concern only; the underlying fields are never rewritten.
