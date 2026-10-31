# Prescribing — parity with legacy Medinet

**Assessed:** 2026-08-18, by reading current code against
`medinet/docs/PRESCRIBING-BUSINESS-RULES.md` (external repo, `F:\OneDrive - MSFT\GlobalHealth\
Source\PrimaryClinic\medinet`). Section numbers below refer to that document.

That spec is worth reading directly before doing prescribing work — it was written by the Medinet
team to support porting their own WinForms wizard to HTML, so it is a deliberate, `file:line`-cited
behavioural contract rather than incidental documentation.

Related: `MEDICINE_DATA_ENRICHMENT.md` (why several gaps below exist, and how to close them).

---

## Ported faithfully

| Legacy | IntelliMed |
|---|---|
| §1 Wizard sequence: search → dose → qty → authority → S8 → reason → issue | Same 7 steps in `PrescribeNew.tsx`, skipping non-applicable ones |
| §3 Authority vs Restricted Benefit routing | `RestrictionLevel == AuthorityRequired` → Authority step; `Restricted` → RB acknowledgement |
| §4 Schedule 8 mandatory state authority | Gated on `Schedule == S8`, code required to advance — mirrors legacy's "MANDATORY … add is aborted" |
| §5 Quantity/repeats only for Limited scripts | `IsLimitedDuration`, mirrors legacy's `Limited` |
| §6 Dosage fields | `Dose`, `Frequency`, `Instruction`, `Route`, `Purpose` (`PrescriptionSourceCode` ≈ `SOURCE_CODE`), `StartDate` |
| §8 Reason step | `Reason` free text + optional `EpisodeId` |
| §3 overdose trigger (`Repeats > MAX_RP`, `Qty > PACK_SIZE`) | `ExceedsPbsLimits` — routes to Authority. Added 2026-08-18 with the PBS `MaxRepeats`/`MaxQuantity` fields |
| §3 Section 100 Authority routing | `IsSection100` — Authority applies regardless of benefit type |
| §5 repeats default from `MAX_RP` | Pre-filled from `MaxRepeats` on medicine selection, editable |
| §10 duplicate therapy | `CheckDuplicateTherapyAsync`, keyed on PBS therapeutic group |
| §12 Persistence | Legacy's *own* rewrite decision was "persist on add, not only on issue". IntelliMed does this from the start — the in-memory-until-issue data loss described in §12 does not exist here |

> ⚠️ §4 is **currently ineffective on real data** — `Medicine.Schedule` is null for every
> PBS-synced medicine. See `MEDICINE_DATA_ENRICHMENT.md` §1. The gate works; nothing reaches it.

## Deliberately simplified

- **§2 Script code derivation.** The `P`/`R`/`$`/`X`/`V`/`D` PBS/DVA state machine is not
  implemented. `IsPbsListed` / `IsRpbsListed` / `RestrictionLevel` are snapshotted at creation
  instead. Neither system does PBS *claiming* (that happens pharmacy-side at dispense), so this
  mainly affects printed-form layout, which IntelliMed does not replicate pixel-for-pixel.
- **§7 Brand substitution (AIP / LEMI / LMBC).** Not ported — depends on MIMS supplementary flags.
  PBS `brand_substitution_group_id` and `originator_brand_indicator` would give an approximation
  (Tier 1 in the enrichment doc).
- **§10 CDS scope.** `MedicationInteractionChecker` covers drug↔drug, drug↔allergy and (since
  2026-08-18) duplicate therapy. Legacy's MIMS-delegated check also covers drug↔disease, pregnancy,
  breastfeeding, gluten and sport — all curated clinical content, so Tier 4 in the enrichment doc.
  Drug↔disease is the most plausible next one now that a coded problem list exists, but it still
  needs an interaction dataset that DDInter doesn't provide.

## Not ported, by design

- **§9 eRx / NPDR / ASL / PCEHR.** `IPrescriptionTransmissionService` exists as a seam with exactly
  one implementation, `NotConfiguredPrescriptionTransmissionService`, which always declines with a
  clear message. Nothing is faked or half-built.
- **SafeScript RTPM** real-time monitored-drug checking.
- **§11 MIMS licence gating** — moot, there is no MIMS subscription. That is the deliberate premise
  of this rebuild (see `CLAUDE.md`).
- **Direct-to-printer GDI+ stationery.** Printing is browser-native, not a pixel match to
  pre-printed PBS/DVA script paper.

## Assessment

Workflow **shape and gating logic** — what a prescriber experiences step to step, including both
hard clinical gates — has real parity. The gaps are almost entirely **data richness**, and trace to
a single root cause: a PBS+Wikidata catalogue rather than a MIMS-fed one.

Two things follow from that:
1. Several gaps are cheaper to close than they look, because PBS publishes the data and it simply
   isn't mapped yet.
2. The remainder (clinical interaction depth, pregnancy categories, PI documents) is curated
   clinical content and is not obtainable from open data at usable quality.
