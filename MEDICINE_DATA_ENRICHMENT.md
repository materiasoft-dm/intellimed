# Medicine catalogue enrichment — findings and roadmap

**Status:** Tier 1 implemented 2026-08-18, plus the interim honesty fix for §1. Tiers 2–4 outstanding.
**Researched:** 2026-08-18, against the live PBS API. Record counts and field lists below were read
from real responses that day, not from documentation — re-verify before relying on exact numbers.

This document exists so the investigation behind it doesn't have to be repeated. It covers one
confirmed defect that needs fixing, and a tiered plan for closing the gap between IntelliMed's
medicine data and what legacy Medinet gets from MIMS.

Read alongside:
- `PRESCRIBING_PARITY.md` — feature-level parity of the prescribing workflow vs Medinet.
- `README.md` — licensing of each medicine data source. **Read before adding a source.**
- `medinet/docs/PRESCRIBING-BUSINESS-RULES.md` (external repo) — the legacy rules being matched.

---

## 1. `Medicine.Schedule` is null for the entire synced catalogue — **RESOLVED 2026-08-21**

Was: severity high, the Schedule 8 mandatory state-authority gate could never fire on real
catalogue data. **Closed by the TGA ARTG backfill** (see "Fixing it" below). A correction to the
original analysis: this did *not* need SUSMP. The ARTG Search Visualisation Tool's export, with the
**Medicines** filter applied, carries a `Poison Schedule` column directly.

Everything below is the original analysis, kept because the root cause — PBS's `schedule_code`
meaning *publication edition*, not poisons schedule — is a trap worth not re-discovering.

### What's wrong

`PbsApiMedicineSourceProvider` maps the PBS "schedule" onto `MedicineSchedule` (S2/S3/S4/S8). But
PBS `schedule_code` / `schedule_name` refer to the **publication edition** of the PBS itself — the
monthly release — not the poisons schedule. They are unrelated concepts that happen to share a word.

Verified live response from `GET /api/v3/schedules?limit=1`:

```json
{"revision_number":2,"start_tsp":"2026-08-01T00:00:00.000+10:00","effective_date":"2026-08-01",
 "effective_month":"AUGUST","effective_year":2026,"publication_status":"PUBLISHED","schedule_code":4708}
```

There is no `schedule_name` field in the response at all. So in
`PbsApiMedicineSourceProvider.FetchAsync`:

```csharp
var name = GetString(s, "schedule_name");          // always null — field doesn't exist
if (code > 0 && !string.IsNullOrWhiteSpace(name))  // guard never passes
    scheduleMap[code] = name;                      // scheduleMap stays empty
```

`MapSchedule()` therefore always receives null and always returns null.

### Impact

- **Every PBS-synced medicine has `Schedule == null`** (~14,900 items on the current schedule).
- The wizard's **mandatory Schedule 8 state-authority step never fires** for any real catalogue
  drug. See `PrescribeNew.tsx`'s `IsStepApplicable` — `WizardStep.StateAuthority` is gated on
  `_selectedMedicine?.Schedule == MedicineSchedule.S8`.
- The schedule badge never renders on drug search results or the Medicines admin list for synced rows.
- Only medicines created by hand, where an admin picked S8 from the dropdown, trigger the gate.

### Why the tests didn't catch it

`HealthIssueTests` / `PrescriptionWizardTests.ScheduleEightMedicine_BlocksContinueUntilStateAuthorityCodeEntered`
creates a **manual** medicine with S8 selected explicitly, then asserts the gate fires. It does —
the mechanism is correct. The test never exercises a synced medicine, so the broken data path is
invisible to it.

Worth generalising: a test that builds its own fixture data can verify a mechanism while saying
nothing about whether real data ever reaches it.

### Fixing it — done, via TGA ARTG

PBS publishes no poisons scheduling, so this was never fixable by mapping a different PBS field.
The original note here proposed SUSMP; the cheaper and sufficient source turned out to be the
**ARTG Search Visualisation Tool** at <https://compliance.health.gov.au/artg/>, exported with the
**Medicines** filter applied. That export has 32 columns including `Poison Schedule`, `Dosage Form`
and `Route of Administration`. (The *unfiltered* export has only 8 columns and no scheduling — an
easy and silent mistake to make.)

**Implementation.** `ArtgCsvParser` (streaming RFC4180 — the Indications and Warnings columns
routinely contain commas and embedded newlines, which a line-based reader mis-aligns) feeds
`ArtgEnrichmentService`, exposed at `POST api/medicines/enrich-artg` and driven from
Admin > Medicines > *Poisons Scheduling (TGA ARTG)*. It is **fill-only** — an existing value is
never overwritten, so a curated schedule always outranks a brand-level match.

**First real run, 2026-08-21** — 36,911 ARTG rows against 6,194 catalogue medicines, 3.9 s for a
95 MB upload:

| | before | after |
|---|---|---|
| any Schedule | 31 | 5,452 |
| **S8** | ~0 | **253** |
| S4 / S3 / S2 | | 5,055 / 49 / 32 |
| ARTG ID, Route | 0 | 5,480 |
| unmatched | | 714 |

Spot-checked against clinical expectation: Endone, OxyContin, Targin and Ritalin all resolved S8;
Panadeine Forte and Valium S4. The 714 unmatched are overwhelmingly dressings and consumables
(Tubifast, Allevyn, DuoDERM), which correctly do not appear in a Medicines-filtered export, plus
generic-name biologicals with no brand token to key on.

**Known limits.** Matching is brand-level on the leading token, so every strength of a brand gets
the same ARTG ID and schedule, and where a brand family spans schedules the **strictest** wins.
That is deliberate — over-prompting for a state authority code is far cheaper than missing an S8 —
but it makes a backfilled `Schedule` a prompt to verify rather than a citation. It is also a manual
upload, not a sync: nothing refreshes it.

---

## 2. What PBS actually publishes that we don't use

The single biggest surprise from this research: a large share of what was assumed to be
"MIMS-only" is already available from PBS — same API, same subscription key, already wired up.

### Currently consumed

- Endpoints: `items`, `item-atc-relationships`, `schedules`, `organisations`
- Fields off `items`: `atc_code`, `benefit_type_code`, `brand_name`, `li_drug_name`, `li_form`,
  `organisation_id`, `organisation_name`, `pack_size`, `pbs_code`, `program_code`, `schedule_code`,
  `schedule_name`

### Available on `items` but unmapped

Confirmed present in the endpoint's `filter_params` (the API echoes its full filterable field set):

| PBS field | Closes |
|---|---|
| `number_of_repeats` | Legacy §3 overdose trigger — `Repeats > MAX_RP` |
| `maximum_quantity_units`, `maximum_prescribable_pack` | Legacy §3 overdose trigger — quantity half |
| `section100_only_indicator` | Legacy §3 Authority routing on `PBS_TYPE == "Sect. 100"` |
| `brand_substitution_group_id` / `_code`, `originator_brand_indicator`, `innovator_indicator` | Legacy §7 brand substitution (approximate LEMI/LMBC) |
| `therapeutic_group_id`, `therapeutic_group_title` | **Duplicate-therapy checking** |
| `manner_of_administration`, `moa_preferred_term` | Route — currently free-typed by the prescriber |
| `formulary` (F1/F2) | Formulary display |
| `safety_net_resupply_rule_days`, `safety_net_resup_rule_cnt_ind` | Safety-net resupply rules |
| `continued_dispensing_flag`, `continued_dispensing_emergency` | Continued dispensing eligibility |
| `pack_not_to_be_broken_ind`, `pack_content`, `vial_content`, `unit_of_measure` | Dispensing detail |
| `caution_indicator`, `note_indicator` | Prescriber cautions |
| `extemporaneous_indicator`, `extemporaneous_standard` | Compounded/extemporaneous items |
| `doctors_bag_only_indicator` | Doctor's bag items |
| `elect_med_chart_eligible_ind`, `paper_med_chart_eligible_ind` | Medication chart eligibility |

### Endpoints not called at all

Verified live, all responding on the existing key:

| Endpoint | Records (2026-08-18) | Value |
|---|---|---|
| `restrictions` | 3,595 | Authority / Restricted Benefit **criteria text** |
| `item-restriction-relationships` | 23,554 | Joins an item to its restrictions |
| `prescribing-texts` | 9,220 | Prescriber notes and cautions |

`criteria` is **not** a valid v3 endpoint — returns an error.

Restrictions are the interesting one: they carry the actual indication a prescriber attests to.
That would upgrade the Authority step from "type an approval number" into showing the real criteria
being claimed against.

---

## 3. Enrichment tiers

Ordered by cost. Tier 1 has no licensing or integration cost at all — the data is already in
responses we parse and discard.

### Tier 1 — ✅ DONE (2026-08-18)

Six fields now flow from PBS through to the wizard: `MaxRepeats`, `MaxQuantity`, `Route`,
`IsSection100`, `TherapeuticGroupId`, `TherapeuticGroupTitle` (migration
`AddPbsPrescribingLimits`). What they drive:

- **Overdose trigger** (legacy §3) — `PrescribeNew.tsx`'s `ExceedsPbsLimits`. Exceeding the
  listing's repeats or quantity routes the prescriber through the Authority step. Quantity is free
  text, so only a cleanly numeric value is compared; anything else is left alone rather than guessed.
- **Section 100 routing** (legacy §3) — Authority applies regardless of benefit type.
- **Route pre-fill** — was free text, now defaults from `moa_preferred_term` and stays editable.
- **Repeats default** (legacy §5) — defaults to the listing's `number_of_repeats`.
- **Duplicate-therapy checking** — `MedicationInteractionChecker.CheckDuplicateTherapyAsync` warns
  when an active prescription shares the candidate's PBS therapeutic group. Advisory like the other
  checks. Re-prescribing the same medicine is correctly a repeat, not a duplicate.

The Authority step now states which of the four triggers applied, since an unexplained Authority
screen after a quantity change would be baffling. Each block conditions on its own trigger, so a
Restricted item prescribed over its limit correctly asks for both an indication confirmation and an
approval number.

**Not taken:** `brand_substitution_group_id` / `originator_brand_indicator` were left unmapped.
Legacy §7's AIP/LEMI/LMBC logic needs real substitution rules, not just the grouping, so storing the
fields now would be data with no consumer.

**Licensing note:** PBS content must be stored verbatim and never modified — see `CLAUDE.md` and
`README.md`. New fields follow the same rule: store as published, split only for presentation.

### Tier 2 — free, already licensed, three new endpoints
Consume `restrictions`, `item-restriction-relationships`, `prescribing-texts`.

Delivers: real Authority/Restricted criteria text in the wizard, prescriber cautions.

Cost consideration: the sync already takes 10–15 minutes, almost all of it PBS rate limiting.
Three more paginated endpoints (~36k rows combined) will extend that. Worth measuring before
committing — the sync is already the slowest thing in the product.

### Tier 3 — free, new sources, real integration work

- **Poisons Standard (SUSMP)** — TGA. The fix for §1. Without it the S8 gate is decorative.
- **AMT (Australian Medicines Terminology)** via NCTS — the national medicines terminology.
  Free but requires registration and a licence agreement. Gives structured dose form, strength and
  substitutability. `Medicine.AmtConceptId` already exists and is never populated.
- **TGA ARTG** — registration IDs and sponsor detail. `Medicine.ArtgId` also already exists and is
  never populated. Previously deferred (see `CLAUDE.md`) because PBS carries no ARTG IDs, so this
  needs its own provider and its own licence review.

### Tier 4 — genuinely MIMS-or-equivalent (commercial)

Not obtainable from open data at meaningful quality:
- Drug↔drug interaction content at MIMS depth (DDInter is the current open substitute — thinner,
  and explicitly advisory)
- Drug↔disease interaction checking
- Pregnancy and breastfeeding categories
- Product Information (PI) and Consumer Medicine Information (CMI) documents
- Dose guidance

Tiers 1–3 deliver most of the *decision-support scaffolding*. Tier 4 is the curated clinical
judgement layer, which is what the MIMS subscription actually buys.

---

## 4. Suggested order

1. ~~Tier 1 field mapping~~ — **done**.
2. ~~Duplicate-therapy checking~~ — **done**, arrived with Tier 1.
3. ~~SUSMP schedule ingestion~~ — **done 2026-08-21**, and without SUSMP: the ARTG Medicines
   export carries `Poison Schedule` directly. See §1.
4. **Tier 2 restrictions** — measure the sync-time cost first.
5. **Catalogue expansion from ARTG** — the same export holds 36,911 registrations against our 6,194
   PBS-derived medicines, i.e. most of the non-PBS market (private scripts, OTC). Deferred because
   ARTG has no separate strength column, so it can't be matched on the existing
   `Name + Strength + Form + PackSize` key without real parsing work.
6. AMT / NCTS when there's a reason beyond completeness.

### Note on re-running the ARTG backfill

Both the PBS sync and the ARTG backfill only ever write where the target is empty, so they compose
in any order and re-running either is safe. A PBS sync that adds new medicines leaves them with no
schedule until the ARTG export is uploaded again — which is the correct degradation (the wizard
shows "scheduling unknown — verify"), not a bug.

---

## 5. How to re-verify

The PBS subscription key lives at `Pbs:ApiSubscriptionKey` in `appsettings.json` — it is the public
portal key, not a secret, and is committed deliberately.

```bash
# Full filterable field list for an endpoint (the API echoes it in filter_params)
curl -s "https://data-api.health.gov.au/pbs/api/v3/items?get_latest_schedule_only=true&limit=1" \
  -H "Subscription-Key: <key from appsettings>"

# Record count for any endpoint
curl -s "https://data-api.health.gov.au/pbs/api/v3/restrictions?get_latest_schedule_only=true&limit=1" \
  -H "Subscription-Key: <key>" | grep -o '"total_records":[0-9]*'
```

`get_latest_schedule_only=true` restricts to the current publication edition — without it `items`
returns roughly 190k historical rows instead of ~15k.
