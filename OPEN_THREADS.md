# Open threads

Live state as at **2026-08-21**. Things that are unfinished, blocked on a decision, or known-broken.
Completed work is not listed here — it lives in `ARCHITECTURE.md` (Implementation Status) and
`USER_GUIDE.md`.

Keep this file honest: delete an item when it's genuinely done, not when it's been worked around.

---

## Known defects

### 1. `Medicine.Schedule` — **RESOLVED 2026-08-21**, kept here for the caveat
The Schedule 8 mandatory state-authority gate was inert on synced data because PBS publishes no
poisons scheduling. Closed by backfilling from a **TGA ARTG "Medicines"-filtered export**, which
carries a `Poison Schedule` column directly (an earlier note here claimed SUSMP was required — it
isn't). Admin > Medicines > *Poisons Scheduling (TGA ARTG)*; see `USER_GUIDE.md`.

First real run, 2026-08-21 (36,911 ARTG rows against 6,194 catalogue medicines):

| | before | after |
|---|---|---|
| any Schedule | 31 | 5,452 |
| **S8** | ~0 | **253** |
| S4 / S3 / S2 | | 5,055 / 49 / 32 |
| ARTG ID, Route | 0 | 5,480 |

714 catalogue rows matched nothing — overwhelmingly dressings and consumables (Tubifast, Allevyn,
DuoDERM) that correctly aren't in a Medicines-filtered export, plus generic-name biologicals with no
brand to key on.

**Two caveats worth knowing before trusting a value:**

- **Matching is brand-level (leading token), so it's coarse.** All strengths of a brand receive the
  same ARTG ID and schedule, and where a brand family spans schedules the **strictest** wins
  (`PANADEINE` inherits `PANADEINE FORTE`'s). Deliberate — over-prompting for a state authority code
  is cheaper than missing an S8 — but it means a `Schedule` is a *prompt to verify*, not a citation.
- **It's a manual upload, not a sync.** Nothing refreshes it; re-upload a fresh export periodically.
  Fill-only, so re-running never overwrites a curated value.

Full analysis in **`MEDICINE_DATA_ENRICHMENT.md` §1**.

### 2. Lifecard tab has no way to save — **needs a product decision**
`AddClient.razor`'s `<EditForm>` (and the only `💾 Save` button) sits inside
`@if (_activeTab == "Details")`. Switching to the Lifecard tab unmounts it, leaving an editable
field with no save control. The edit isn't lost — `_model` survives the tab switch, so returning to
Details and saving persists it — but nothing tells the user that.

Two candidate fixes, both product calls:
- give each tab its own save action, or
- lift the action bar out of the Details conditional so Save is always present.

`ClientUpdateTests.EditClient_UpdatingLifecard_Persists` fails because of this. Leave it failing
until the behaviour is decided — it is currently an accurate signal, not a stale test.

### 3. `MedicinesAdminTests.SourceFilter_ShowsOnlyMatchingRows` — order-dependent, unexplained
Passes alone and in every pairing tried (ClientCreate + ClientList, ClientUpdate, CommandPalette,
EmailInfrastructure, and all four newer UI classes). Fails only in the full run, which points at
accumulated fixture-database state rather than one interaction. Not root-caused. Predates the
2026-08 work. Pinning it down needs a full-suite run with Playwright tracing (~13 min per cycle).

---

## Deployment

**`Jwt__Key` is now required.** The app deliberately refuses to start without it outside
Development. Supply it as an environment variable or in an untracked
`appsettings.Production.json` beside the binary. Full rationale in `ARCHITECTURE.md`
("Configuration and secrets").

The generated production settings file already sits in `publish-freeasp/` (gitignored) — it must be
uploaded alongside the binaries or the deployed app will not boot.

**The old `/dbadmin` password should be rotated.** Its PBKDF2 hash was previously committed and is
still in git history.

---

## Repository state

~227 changed or untracked paths. A large uncommitted feature set is in the working tree —
prescribing, progress notes, episodes, health issues, allergies, drug interactions — plus this
documentation. None of it is committed.

`src/IntelliMed.Api/intellimed.db` is **deliberately tracked** (shared test database). Don't
"helpfully" untrack it.

---

## Decided, not yet built

From the Medinet feature-gap comparison, in priority order:

1. **Investigations & results** — pathology/radiology inbox, HL7 v2 ingestion, result-to-patient
   matching, acknowledge/action workflow. Biggest daily-workflow gap; also the largest lift.
2. **Recalls & reminders** — safety plus recurring revenue. Cheaper now that the Health Issues
   problem list exists to drive it.
3. **Referral letters** — outbound authoring. Note `ClientReferral` is *inbound* referral tracking
   for Medicare eligibility, a different thing.
4. **Care plans (GPMP/TCA)** — billable MBS items, also keyed off the problem list.

Medicine data: Tier 1 PBS enrichment and duplicate-therapy checking are **done**. Next is SUSMP
scheduling, then the Tier 2 restriction/criteria endpoints — see `MEDICINE_DATA_ENRICHMENT.md`.

Financial track (receipting, printing, lifecycle actions and the invoice data model are all done):
financial reporting → fee-calculation edge cases → electronic claiming last, since it involves real
government certification.

Deferred indefinitely: eRx transmission, My Health Record — both need conformance programmes rather
than just implementation effort.

Reporting page keys (`reports`, `reports/clinical`, `reports/financial`) are seeded but **no report
screens exist**. They are placeholders for the above, deliberately left in place.

---

## Documentation drift

The staff handout artifact (published 2026-08-14) is one revision behind `USER_GUIDE.md` — it
predates the RBAC changes and the System Settings section. Republish from
`USER_GUIDE.md` when convenient. Artifact URL is in the session history; recover it with the
Artifact tool's `list` action if lost.
