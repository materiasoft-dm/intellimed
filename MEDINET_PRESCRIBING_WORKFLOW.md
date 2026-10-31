# Medinet Prescribing Workflow — Business Rules & Integration Points

**Source:** `medinet/docs/PRESCRIBING-BUSINESS-RULES.md` (external repo, `F:\OneDrive - MSFT\GlobalHealth\Source\PrimaryClinic\medinet`)
**Assessed:** 2026-08-18 against IntelliMed's current prescribing wizard (`PrescribeNew.tsx`)

---

## Overview

Medinet is the separate clinical (GP consulting) product that runs alongside Pracnet. Pracnet owns demographics, appointments and billing; **Medinet owns the clinical record** — progress notes, prescriptions, pathology, immunisations, letters and clinical documents.

At a linked site, the two products run over their own SQL Server databases and are stitched together by:
1. **In-process linking layer** (`Abaki.LinkingMedinetAdaptor*`) — per-record, save-time facade keeping demographics in step
2. **Batched replication** (`Abaki.Sync` / Microsoft Sync Framework) — whole-table + document file sync for offline laptops
3. **One-way document backfill** (`Abaki.LinkingMedinetPracnetLetterDocument`) — historical Pracnet documents → Medinet
4. **Single sign-on** — shared-memory segment so Medinet session → Pracnet auto-auth

---

## Prescribing Business Rules (from Medinet spec)

### §1 Wizard Sequence — 7 Steps
```
Search → Dose → Qty → Authority → S8 → Reason → Issue
```
- IntelliMed mirrors this in `PrescribeNew.tsx`, skipping non-applicable steps
- **Ported faithfully**

### §2 Script Code Derivation (PBS/DVA State Machine)
- `P`/`R`/`$`/`X`/`V`/`D` codes based on benefit type, authority, concessional status
- **Deliberately simplified** in IntelliMed — `IsPbsListed`/`IsRpbsListed`/`RestrictionLevel` snapshotted at creation
- Neither system does PBS claiming (pharmacy-side at dispense), so mainly affects printed-form layout

### §3 Authority vs Restricted Benefit Routing
| Condition | Legacy | IntelliMed |
|-----------|--------|------------|
| `RestrictionLevel == AuthorityRequired` | Authority step | Authority step |
| `RestrictionLevel == Restricted` | RB acknowledgement | RB acknowledgement |
| `Repeats > MAX_RP` | Overdose trigger → Authority | `ExceedsPbsLimits` → Authority |
| `Qty > PACK_SIZE` | Overdose trigger → Authority | `ExceedsPbsLimits` → Authority |
| `IsSection100` | Authority regardless of benefit type | Authority regardless of benefit type |

### §4 Schedule 8 Mandatory State Authority
- **Gate:** `Medicine.Schedule == S8` → mandatory state authority code to advance
- **⚠️ Currently ineffective on real data** — `Medicine.Schedule` was null for all PBS-synced medicines
- **Fixed 2026-08-21** via TGA ARTG backfill (`ArtgEnrichmentService`) — 253 S8 medicines now resolved

### §5 Quantity/Repeats for Limited Scripts
- `IsLimitedDuration` mirrors legacy's `Limited`
- Repeats default from `MAX_RP` (PBS `number_of_repeats`) — pre-filled on medicine selection, editable

### §6 Dosage Fields
| Legacy | IntelliMed |
|--------|------------|
| Dose | `Dose` |
| Frequency | `Frequency` |
| Instruction | `Instruction` |
| Route | `Route` (now pre-filled from PBS `moa_preferred_term`) |
| Purpose | `PrescriptionSourceCode` ≈ `SOURCE_CODE` |
| Start Date | `StartDate` |

### §7 Brand Substitution (AIP / LEMI / LMBC)
- **Not ported** — depends on MIMS supplementary flags
- PBS `brand_substitution_group_id` + `originator_brand_indicator` would give approximation (Tier 1 enrichment)

### §8 Reason Step
- `Reason` free text + optional `EpisodeId`
- **Ported faithfully**

### §9 eRx / NPDR / ASL / PCEHR
- `IPrescriptionTransmissionService` exists as seam with single implementation: `NotConfiguredPrescriptionTransmissionService`
- **Not ported, by design** — nothing faked or half-built

### §10 Duplicate Therapy
- Legacy: MIMS-delegated check
- IntelliMed: `MedicationInteractionChecker.CheckDuplicateTherapyAsync` keyed on PBS therapeutic group
- **Ported faithfully** (advisory only)

### §11 MIMS Licence Gating
- **Not ported, by design** — no MIMS subscription (deliberate premise of rebuild)

### §12 Persistence
- Legacy: "persist on add, not only on issue" — in-memory-until-issue data loss described
- IntelliMed: persists from the start — no data loss

---

## Medinet Integration Points for Prescribing

### 1. Cross-Product Record Linkage (In-Process)
**Projects:** `Abaki.LinkingMedinetAdaptor*` + `Abaki.MedinetAdaptor`

| Entity | Pracnet Column | Medinet Column | Linking Logic |
|--------|----------------|----------------|---------------|
| Contacts (patients, providers, referral doctors) | `Contacts.MedinetContactGuid` | `TContacts.PracnetContactGuid` | `LinkingPatientBusiness.LinkPatientInfo` |
| Security Users | `SecurityUsers.MedinetUserGuid` | Same column (reverse mapping) | Used by document tools |

**Flow (Patient example):**
1. `LinkPatientInfo(pracnetObj, medinetObj, replaceByPracnet)` loads both sides via EF
2. Field-by-field merge using `DatabaseConnectionManagement.MixValue()` — prefers non-empty; `replaceByPracnet` decides winner on conflict
3. Maps account types via `LinkingConstants.GetMedinetAccountType` / `GetPracnetAccountType`
4. Writes cross-reference GUIDs
5. Batched `SaveChanges()` every 1000 records (static `nCount`)

**Incremental save path:** `UpdatePatientInfo` → `CheckLinkPatientExist` → `LinkCode = "U"` (update) vs `"A"` (add) → 113-element `SqlParameter[]` → `sp_UpdateMedinetPatient` / `sp_UpdatePracnetPatient` via `DatabaseHelper.ExecNonQueryProc` → output param returns new external GUID → written back to linking column

### 2. Batched Replication (Abaki.Sync)
**Tables in sync scope (explicit allow-list):**

| Pracnet (`T*`) | Medinet Clinical (`CN_*`) |
|----------------|---------------------------|
| `TContacts`, `TPatients` | `CN_TPROGRESS` (progress notes) |
| `TAppointments`, `TVisits` | `CN_TPRESCRIPTION` (prescriptions) |
| `TSecurityUser*` | `CN_PATHOLOGY` |
| `TWorkCoverTACClaims` | `CN_IMMUNISATION` |
| ... | `CN_FILES`, `CN_LETTER_TEMPLATE`, ... |

**Prescription sync:** `CN_TPRESCRIPTION` is in the Medinet sync table list — prescriptions replicate bidirectionally between server and laptop via Sync Framework scope `"Medinet"`

### 3. Document Backfill (One-Way: Pracnet → Medinet)
- Tool: `Abaki.LinkingMedinetPracnetLetterDocument`
- Reads Pracnet `Documents` (excludes `Tags='P2K RESULT'`)
- Creates Medinet `CN_FILE` entities with translated GUIDs
- Copies physical files from Pracnet `Documents`/`Letters` → Medinet `Docs{yyyyMM}` folders
- **Requires linkage GUIDs already populated** (run Link Auto Existing Data first)

### 4. Single Sign-On
- Shared-memory key derived from Medinet install path: `PracnetLocalSetting.GetClinicalSoftwareShareKey()`
- Medinet writes logged-in user's Pracnet-security GUID into shared memory
- Pracnet reads it at startup via `MedinetAuthenticate()` → auto-authenticates, skips `frmLogin`
- On interactive login, Pracnet writes user GUID back to shared memory for next SSO

---

## IntelliMed Prescribing Architecture (Current)

### Core Entities
- `Prescription` — clinical prescription record (links to `Appointment`, `Client`, `Episode`)
- `PrescriptionIssue` — issued prescription with prescriber details (AHPRA, HPI-I, provider number, qualifications)
- `PrescriptionDeliveryMethod` — Electronic / Paper / SendToPharmacy
- `PrescriptionSourceCode` — AdvisedOrPrescribedElsewhere / HandwrittenPrescription / PrintedPrescription

### Services
- `IPrescriptionService` / `PrescriptionService` — CRUD + business logic
- `IPrescriptionTransmissionService` — seam for eRx (currently `NotConfiguredPrescriptionTransmissionService`)
- `MedicationInteractionChecker` — drug↔drug, drug↔allergy, duplicate therapy (PBS therapeutic group)

### Wizard (`PrescribeNew.tsx`)
**7 Steps:**
1. **Search** — medicine lookup (PBS/Wikidata/ARTG catalogue)
2. **Dose** — `Dose`, `Frequency`, `Instruction`, `Route`, `Purpose`, `StartDate`
3. **Qty** — quantity, repeats (defaults from PBS `MaxRepeats`/`MaxQuantity`), `IsLimitedDuration`
4. **Authority** — triggered by: `RestrictionLevel==AuthorityRequired`, `ExceedsPbsLimits`, `IsSection100`
5. **S8** — gated on `Medicine.Schedule == S8` (now works via ARTG backfill)
6. **Reason** — free text + optional `EpisodeId`
7. **Issue** — delivery method, prescriber details, auto-log to open progress note

### Data Enrichment Pipeline
```
PBS API (items, item-atc-relationships, schedules, organisations)
  → PbsApiMedicineSourceProvider
  → MedicineCsvAssemblyService (merges with Wikidata, manual entries)
  → ArtgEnrichmentService (manual upload, fill-only)
  → Medicine entity (Schedule, ArtgId, Route, Form, TherapeuticGroup)
```

---

## Porting Checklist for IntelliMed

### Already Ported (Faithful)
- [x] 7-step wizard sequence
- [x] Authority vs Restricted Benefit routing
- [x] Schedule 8 gate (now functional via ARTG)
- [x] Limited script quantity/repeats
- [x] Dosage fields (Dose, Frequency, Instruction, Route, Purpose, StartDate)
- [x] Reason step
- [x] Overdose triggers (repeats > MAX_RP, qty > PACK_SIZE)
- [x] Section 100 routing
- [x] Repeats default from PBS `number_of_repeats`
- [x] Duplicate therapy checking (PBS therapeutic group)
- [x] Persistence on add (not just issue)

### Deliberately Simplified
- [ ] Script code derivation (P/R/$/X/V/D) — snapshotted flags instead
- [ ] Brand substitution (AIP/LEMI/LMBC) — PBS grouping fields available but no consumer

### Not Ported (By Design)
- [ ] eRx / NPDR / ASL / PCEHR — `IPrescriptionTransmissionService` seam exists
- [ ] SafeScript RTPM real-time monitored-drug checking
- [ ] MIMS licence gating
- [ ] Direct-to-printer GDI+ stationery (browser-native printing)

### Data Gaps (Root Cause: PBS+Wikidata vs MIMS)
| Gap | Source | Status |
|-----|--------|--------|
| Schedule 8 (S8) | PBS `schedule_code` = publication edition, not poisons schedule | **Fixed via ARTG** |
| Authority/Restricted criteria text | PBS `restrictions` + `item-restriction-relationships` | Tier 2 (3 new endpoints) |
| Prescriber cautions | PBS `prescribing-texts` | Tier 2 |
| Brand substitution rules | MIMS only | Tier 4 (commercial) |
| Drug↔disease interactions | MIMS only | Tier 4 |
| Pregnancy/breastfeeding categories | MIMS only | Tier 4 |
| PI/CMI documents | MIMS only | Tier 4 |
| Dose guidance | MIMS only | Tier 4 |

---

## Integration Requirements for IntelliMed

When porting Medinet prescribing to IntelliMed, consider:

1. **Demographic linkage** — IntelliMed's `Client` entity must link to Medinet's `TContacts` via GUID cross-reference (like Pracnet's `Contacts.MedinetContactGuid`)

2. **Prescription sync** — If IntelliMed runs alongside Medinet, `Prescription`/`PrescriptionIssue` should be in the sync scope (like `CN_TPRESCRIPTION`)

3. **SSO** — Shared-memory or token-based SSO so prescriber session flows between systems

4. **Document backfill** — Historical prescriptions/documents from legacy → IntelliMed

5. **In-process linking** — Real-time demographic updates (patient details, provider info) should propagate to Medinet clinical DB

6. **Medicine catalogue** — IntelliMed's PBS+Wikidata+ARTG catalogue should be the source of truth; Medinet's MIMS data (if any) is supplementary

---

## Key Files to Reference

| File | Purpose |
|------|---------|
| `medinet/docs/PRESCRIBING-BUSINESS-RULES.md` | Legacy behavioural contract (file:line cited) |
| `PRESCRIBING_PARITY.md` | IntelliMed vs Medinet feature parity |
| `MEDICINE_DATA_ENRICHMENT.md` | Data gap analysis & roadmap |
| `HANDOFF.md` §12 | Medinet integration architecture |
| `src/IntelliMed.Client/src/pages/PrescribeNew.tsx` | 7-step wizard implementation |
| `src/IntelliMed.Infrastructure/Services/MedicationInteractionChecker.cs` | Safety checks |
| `src/IntelliMed.Infrastructure/Services/ArtgEnrichmentService.cs` | S8 gate fix |
| `src/IntelliMed.Core/Interfaces/IPrescriptionTransmissionService.cs` | eRx seam |