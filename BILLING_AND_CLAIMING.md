# Australian Medical Billing & Claiming — Reference Guide

> Written as a primer for someone who doesn't come from an Australian medical-billing background.
> Part 1 explains how billing/claiming actually works in Australia. Part 2 explains how a full
> practice-management system electronically submits claims (legacy Pracnet, for future reference —
> **not yet built in this repo**). Part 3 maps all of it onto IntelliMed's actual code. Part 4 is a
> glossary you can jump to directly.
>
> Dollar figures below are illustrative round numbers, not current real MBS rates — rates are
> indexed periodically by Services Australia. Get current rates from the MBS Online catalog, not
> from this document.

---

## Part 1 — How Australian medical billing actually works

### 1.1 The Medicare Benefits Schedule (MBS)

The **MBS** is the government's master list of billable medical services. Every service (a GP
consult, a blood test, a surgical procedure) has an **item number** (e.g. item 23 = a standard GP
consult) and a **Schedule Fee** — the amount Medicare has decided that service is "worth" for
rebate purposes. Doctors are free to charge more than the Schedule Fee (that's where "the gap"
comes from — see 1.3), but Medicare will only ever rebate a percentage of the Schedule Fee, never
of what was actually charged.

Some items are marked as **100% rebate items** (mostly GP attendance items) — MBS publishes an
explicit "Benefit 100%" amount for these, called `Benefit100` throughout this codebase.
Everything else follows a flat percentage rule (see 1.2).

### 1.2 The Medicare rebate percentage rule

For an item *not* explicitly marked as 100%, Medicare rebates:

- **75%** of the Schedule Fee for services provided as part of an admitted hospital episode
  (in-hospital), regardless of provider type.
- **100%** of the Schedule Fee for GP attendance items MBS has flagged as 100%-rebate items
  (`Benefit100`).
- **85%** of the Schedule Fee for everything else provided in the community (specialist rooms,
  pathology, etc.).

This is a flat percentage of the *Schedule Fee*, not of what the doctor actually charged.

### 1.3 The "gap"

**Gap = what the patient actually pays out of pocket** = (amount charged) − (Medicare rebate).

If a GP charges exactly the Schedule Fee for a 100%-rebate item, the gap is $0 — this is
effectively what "bulk billing" achieves (see 1.4). If a specialist charges more than the Schedule
Fee, or the item only attracts an 85%/75% rebate, the patient is left with a gap even after the
rebate lands. Health funds and DVA can each also define their own gap arrangements on top of
this — see 1.6.

### 1.4 Bulk billing

**Bulk billing** means the provider agrees to accept the Medicare rebate *as full payment* — the
patient pays nothing at the counter, and the claim is submitted directly to Medicare by the
practice (electronically, via **BBSW** — Bulk Bill Simplified Web, see Part 2). Because the
provider is accepting the rebate as full payment, fee = rebate by definition and the gap is
always $0. This is a decision the provider makes per-service, not a fixed policy — a practice can
bulk-bill one patient's consult and charge another privately for the exact same item on the same
day.

### 1.5 Private billing / patient claims

The alternative to bulk billing: the patient pays the practice's own fee at the counter (which can
be higher than the Schedule Fee), and either:

- The practice submits an **electronic patient claim** on the patient's behalf at the point of
  care (common with EFTPOS-integrated terminals — the patient's Medicare rebate is deposited
  straight to their bank account within about a day), or
- The patient claims the rebate themselves later, via the Medicare app / online / in person.

Either way, the rebate follows the 75/85/100% rule from 1.2, and the difference between what was
charged and what Medicare pays back is the gap (1.3).

### 1.6 Private health insurance (health funds)

Private health insurance is separate from Medicare and comes in two parts: **hospital cover**
(admitted care) and **extras cover** (dental, physio, optical, etc. — not really relevant to
MBS-item billing). For hospital-related medical services, health funds negotiate their own
fee/gap arrangements with providers and publish their own fee schedules, which is why this
codebase's `FeeSchedule` entity can be tagged to a specific `HealthFund`. Two funds can, and often
do, charge different prices for the identical MBS item.

Health funds can also **inherit** another schedule's prices rather than publishing their own full
list (e.g. a fund borrowing a state's general "gap cover" schedule and only overriding a handful
of items) — that's what `FeeSchedule.FeeTableId` (parent-schedule inheritance) models.

**Eclipse** is the Medicare-operated electronic gateway that also carries private-health-fund
claims for in-hospital medical services — this is the "IMC" claim family (1.8).

### 1.7 DVA (Department of Veterans' Affairs)

DVA card holders (Gold Card / White Card) are entitled to have eligible treatment paid for by
DVA rather than Medicare. DVA publishes its **own fee schedule** (not derived from the MBS
Schedule Fee at all — it's an independently negotiated, published list). Like bulk billing,
DVA accepts its own fee as full payment: fee = rebate, gap = $0. Claims are submitted
electronically to DVA in much the same shape as a bulk-bill claim, just against a different fee
table and payer.

### 1.8 IMC, OVS, OEC — the other Medicare Online claim families

Beyond ordinary bulk-bill/patient/DVA claims, Services Australia's Medicare Online web services
also carry:

- **IMC (In-Patient Medical Claim)** — claims for services provided as part of a private hospital
  admission, billed against the patient's health fund via the same Medicare Online/Eclipse
  gateway. IMC has its own claim-type codes (AG, SC, MO, MB, PC) crossed with service type
  (General/Pathology/Specialist).
- **OVS (Overseas Visitor claims)** — for patients covered by a Reciprocal Health Care Agreement,
  overseas student health cover, or otherwise self-funded overseas visitors.
- **OEC (Online Eligibility Check)** — *not* a claim at all, an advisory pre-treatment check
  against a patient's health fund cover (used from a quoting screen before a procedure, not
  auto-run before every claim like OPV is).

### 1.9 Compensable claims — WorkCover and TAC

Some injuries aren't billed to Medicare/DVA/a health fund at all — they're billed to a
**third-party compensation payer**:

- **WorkCover** (a workplace-injury insurance scheme; state-run, so "WorkCover Qld", "WorkCover
  Vic" etc. each have their own rules and fee schedules) — different states even define the MBS
  "Multiple Operation Rule" (1.11) differently; see the WorkCover Qld split-pool example there.
- **TAC** (Transport Accident Commission — Victoria's motor-vehicle-accident compensation scheme;
  other states have equivalent schemes under different names).

For these, the practice tracks a **claim number**, the employer/insurer details, and a case
manager, and bills the compensation payer directly at negotiated rates — there's no Medicare
rebate involved and no electronic Medicare Online claim family for this; it's typically invoiced
like any other business-to-business invoice.

### 1.10 Concession / Pensioner

These describe the *patient's* circumstances (holding a concession or pensioner card), which can
affect government policy elsewhere (e.g. PBS medicine costs), but for MBS item billing purposes
they don't change the rebate math from 1.2 — a practice still chooses whether to bulk-bill or
privately bill a concession-card patient, same as anyone else. They exist as their own category
mainly for reporting/eligibility purposes.

### 1.11 The Multiple Operation Rule

When two or more surgical (MBS Group T8 "Operations") items are performed in the same session,
Medicare doesn't pay full rebate on all of them — it **abates** (reduces) every item after the
first:

- **100%** of normal fee for the highest-value item,
- **50%** for the next,
- **25%** for every item after that.

**Amputation items (SubGroup 12) are excluded entirely** — never abated. WorkCover Qld runs a
different rule: standard surgical subgroups still taper 100/50/25%, but Orthopaedic/Hand
(SubGroups 14–15) get 100% for the highest item then a **flat 75%** (no further taper) for every
other item in that pool — and the two pools rank independently, so a mixed session can land two
separate items at 100%.

### 1.12 Derived / formula-driven items

Some MBS items aren't priced on their own — their fee is *derived* from another item on the same
invoice by a formula. Common real-world examples:

- **Assistant at surgery** — priced as a percentage of the primary surgeon's (already-abated) fee.
- **Anaesthesia items** — priced by "basic units" plus time, often gated on a matching surgical
  item being present.
- **Multiple patients seen** (e.g. nursing home visits) — a base fee plus a per-patient add-on,
  which changes shape once a patient-count threshold is crossed.

### 1.13 GST

Most medical services in Australia are **GST-exempt**. This is why GST defaults to zero throughout
the billing model here rather than being calculated like a normal 10% sales tax.

---

## Part 2 — How claims actually get transmitted electronically (reference — not built in this repo)

This section is **not implemented in IntelliMed today** (see 3.8 for exactly what's missing). It's
included because it's what a finished system eventually needs, and it's documented in detail from
the legacy Pracnet system in `HANDOFF.md` §8–9 if you need the full depth later.

A real Medicare Online integration needs:

1. **PRODA (Provider Digital Access) authentication** — an OAuth `jwt-bearer` flow where the
   software proves its identity to Services Australia with an RSA-signed JWT (no client secret —
   proof of possession of a private key generated on the machine). This yields a bearer token used
   on every subsequent Medicare API call.
2. **Two separate credentials**, easy to confuse: a PRODA `client_id` (authenticates to PRODA
   itself) and a Medicare `X-IBM-Client-Id` (authorises the *software product* against Services
   Australia's API gateway subscription) — each with independent Production and Test variants.
3. **An OPV (Online Patient Verification) pre-check** before almost every claim family transmits —
   confirms the patient's Medicare eligibility before you're allowed to submit.
4. **One "processor" per claim family** (Bulk Bill / DVA / DVA Allied Health / IMC / OVS / OEC),
   each building a family-specific JSON payload, POSTing it with the right headers, and parsing a
   family-specific response shape.
5. **Careful claim-status bookkeeping** — every transmit attempt (success, rejection, or network
   failure) has to update both the claim's own status and every linked invoice's claim status, so
   nothing gets stuck in limbo where the system doesn't know if a claim actually went through.
6. **Endpoint versioning** — Services Australia periodically moves individual services from one
   API version to the next on a fixed date, and not every service moves at the same time, so the
   code has to track version per-service rather than as one global switch.

None of this exists in IntelliMed yet — see 3.8.

---

## Part 3 — How IntelliMed models this today

### 3.1 The data model, at a glance

| Real-world concept | IntelliMed type | Where |
|---|---|---|
| An MBS catalog item | `BillingItem` (`ItemNumber`, `ScheduleFee`, `Benefit100`, `Group`/`SubGroup`) | `Core/Entities/BillingItem.cs` |
| A price list (health fund's, DVA's, bulk-bill's, etc.) | `FeeSchedule` + `FeeScheduleItem` | `Core/Entities/FeeSchedule.cs` |
| The billing category of an invoice/client | `AccountTypeEnum` | `Core/Entities/ClientEnums.cs` |
| Which price list a clinic uses for a given category | `AccountTypeFeeScheduleMapping` | `Core/Entities/AccountTypeFeeScheduleMapping.cs` |
| A compensable (WorkCover/TAC) claim record | `ClientCompensationClaim` | `Core/Entities/ClientCompensationClaim.cs` |
| The actual fee/rebate resolution engine | `BillingCalculator` (`IBillingCalculator`) | `Infrastructure/Services/BillingCalculator.cs` |
| The Multiple Operation Rule | `MultipleOperationRuleCalculator` | `Infrastructure/Services/MultipleOperationRuleCalculator.cs` |
| Formula-driven (derived) items | `DerivedFeeCalculator` | `Infrastructure/Services/DerivedFeeCalculator.cs` |

`AccountTypeEnum` values: `PrivatePatient`, `Concession`, `Pensioner`, `Veteran` (DVA),
`WorkCover`, `Tac`, `BulkBill`, `Other`, `Imc`.

### 3.2 Setting it up: Account Type → Fee Schedule mapping

In **Clinic Settings** (`IntelliMed.Web/Pages/ClinicSettings.razor`), an admin maps each
`AccountType` to the `FeeSchedule` that should price its invoice lines — e.g. "PrivatePatient →
our standard private schedule", "WorkCover → the WorkCover Qld schedule". This mapping is the
clinic-wide default; a per-invoice-line override is still possible (an invoice can bulk-bill one
item and privately bill another, exactly like real life — see 3.4).

### 3.3 The three special "virtual" schedule families

Auto-seeded by `FeeScheduleRepository` (see `Core/Interfaces/IFeeScheduleRepository.cs` and the
implementation for the exact seeding logic):

- **`BBGP` / `BBO` / `BBI`** (Bulk Bill — General Practice / Specialist Rooms / In-Hospital) —
  auto-populated straight from each `BillingItem.Benefit100` (falling back to `ScheduleFee`), no
  manual entry required. These exist purely so the calculator can look up "what would the bulk-bill
  rebate be for this item, in this context" (GP vs specialist, rooms vs hospital).
- **`VAGP` / `VASO` / `VASI`** (DVA — General Practice / Specialist Rooms / Specialist Hospital) —
  created as **empty shells**; DVA has no public machine-readable fee feed, so an admin has to
  import DVA's published fee list manually (CSV import or the generic "Fetch Now" from a
  `SourceUrl`, once one is exported from dva.gov.au).
- **`WCQLD`** (WorkCover Qld) — same story: an empty shell, populated manually, because WorkCover
  Qld only publishes its rates as a PDF, not a feed.

### 3.4 The calculation engine — `BillingCalculator.ResolveLineAsync`

For every invoice line, in order:

1. **Resolve which schedule prices this line.** Precedence: a per-line override (an invoice can
   mix bulk-billed and privately-billed items) → the schedule tagged to the client's health fund
   (only if exactly one active schedule matches that fund — an ambiguous match deliberately falls
   through rather than guessing) → the clinic's `AccountTypeFeeScheduleMapping` default.
2. **Branch on `AccountType`:**
   - `BulkBill` → `Fee = <mapped BB schedule's price, else Benefit100, else ScheduleFee>`;
     `Rebate = Fee`. Gap is always $0 (1.4).
   - `Veteran` (DVA) → `Fee = <mapped DVA schedule's price, else the VAGP/VASO/VASI lookup by
     provider type + place of service, else Benefit100/ScheduleFee>`; `Rebate = Fee`. Gap is
     always $0 (1.7).
   - Everything else (`PrivatePatient`, `Concession`, `Pensioner`, `WorkCover`, `Tac`, `Other`,
     `Imc`) → `Fee = <mapped schedule's price, else raw MBS ScheduleFee>`; `Rebate` computed
     independently via the 75/85/100% rule (1.2). This is the one branch where `Fee` and `Rebate`
     genuinely come from two different places, which is exactly what creates a real gap.
3. **Fee-schedule parent inheritance**: if the resolved schedule has no price for this item, the
   calculator walks up `FeeTableId` (its parent schedule) looking for one, up to 5 hops, before
   falling back to the MBS `ScheduleFee`.
4. **Rounding**: each `FeeSchedule` has its own `RoundingType` (Exact / nearest 1c / nearest 5c),
   applied to whichever schedule actually supplied the fee.

A worked (illustrative) example: a GP consult item with `ScheduleFee = $85`, `Benefit100 = $42`.

| AccountType | Fee | Rebate | Gap |
|---|---|---|---|
| BulkBill | $42 (from `Benefit100`, via `BBGP`) | $42 | $0 |
| Veteran | DVA's own published rate for this item, say $45 (via `VAGP`) | $45 | $0 |
| PrivatePatient | $85 (clinic's private schedule, or raw `ScheduleFee`) | $42 (`Benefit100` rule) | $43 |

### 3.5 The Multiple Operation Rule — `MultipleOperationRuleCalculator`

Runs over every invoice's Group `T8` (Operations) items **before** the derived-item calculator (so
formulas like "assistant at surgery" — which is defined as a fraction of the *already-abated* fee —
see the correct, reduced number). Ranks items by fee descending and applies 100%/50%/25% (1.11),
except SubGroup 12 (amputations) which is always excluded, and `AccountType.WorkCover` which uses
the split-pool Orthopaedic/Hand vs Surgical rule instead of the flat taper.

### 3.6 Derived (formula) items — `DerivedFeeCalculator`

Runs as a post-pass after both the base calculator and the Multiple Operation Rule. Implements 8
formula "strategies" (percentage-of-associated-item, assistant-at-surgery style gating,
patients-seen tiers, field-quantity/time-duration overages, etc.) — see the doc-comment on
`DerivedCalculationType` (`Core/Entities/BillingEnums.cs`) for the exact shape of each one, and
`DerivedFeeCalculator.cs`'s own header comment for what was deliberately simplified relative to
legacy Pracnet's two-pass version.

### 3.7 Compensable claims — `ClientCompensationClaim`

Just structured record-keeping today: claim number, date of injury, employer, case manager, payer
name, per client (1.9). There's no fee-schedule-driven pricing difference beyond the
`AccountType.WorkCover`/`Tac` mapping in 3.2 — no separate "compensable calculator."

### 3.8 What's explicitly NOT built yet

- **No electronic claim transmission of any kind.** `Invoice` has no claim-status field at all —
  no PRODA integration, no BBSW/DVA/IMC/OVS/OEC processors, nothing from Part 2. Everything
  described in Part 3 so far is **fee/rebate/gap calculation only** — it tells you what a line
  *should* cost and what Medicare/DVA *should* rebate, but doesn't send anything to Services
  Australia, a health fund, or DVA electronically. That's tracked as future work; when it's built,
  `HANDOFF.md` §8–9 is the detailed reference for how legacy Pracnet did it.
- **No receipting-against-a-transmitted-claim workflow** (legacy's "receipting funded claims").
- **No DVA/WorkCover fee data out of the box** — those schedules are seeded as empty shells (3.3)
  that an admin must populate manually until an automated feed exists.

---

## Part 4 — Glossary

| Term | Meaning |
|---|---|
| **MBS** | Medicare Benefits Schedule — the government's catalog of billable item numbers and their Schedule Fees. |
| **Schedule Fee** | The MBS-published reference price for an item; rebates are a percentage of this, not of what was actually charged. |
| **Benefit100** | MBS's published 100%-rebate amount for items eligible for full rebate (mostly GP attendances). |
| **Bulk billing** | Provider accepts the Medicare rebate as full payment; patient pays nothing; gap = $0. |
| **Gap** | What the patient pays out of pocket = amount charged − rebate received. |
| **Patient claim** | Patient pays the practice, then claims the Medicare rebate themselves (or it's lodged electronically on their behalf). |
| **DVA** | Department of Veterans' Affairs — pays its own published fee for eligible card holders; fee = rebate, gap = $0. |
| **Health fund / private health insurance** | Separate from Medicare; negotiates its own fee/gap schedules per provider for hospital-related services. |
| **Eclipse** | Medicare-operated gateway that also carries private-health-fund in-hospital claims. |
| **IMC** | In-Patient Medical Claim — a private-hospital-admission claim billed via Medicare Online/Eclipse to the patient's health fund. |
| **OVS** | Overseas Visitor claim (reciprocal agreement / overseas student cover / self-funded). |
| **OEC** | Online Eligibility Check — an advisory pre-treatment check against a health fund, not a claim. |
| **OPV** | Online Patient Verification — a required Medicare eligibility pre-check before most claim families can transmit. |
| **BBSW** | Bulk Bill Simplified Web — the Medicare Online service used to submit bulk-bill claims. |
| **PRODA** | Provider Digital Access — the government identity/auth system practice software must authenticate through to call Medicare Online. |
| **WorkCover** | State-run workplace-injury compensation insurance; billed directly to the insurer, not Medicare. |
| **TAC** | Transport Accident Commission — Victoria's motor-vehicle-accident compensation scheme (other states have equivalents). |
| **Multiple Operation Rule** | Medicare rule abating (reducing) the fee of the 2nd, 3rd, etc. surgical item performed in the same session. |
| **Derived item** | An MBS item whose fee is computed from another item on the same invoice via a formula (e.g. assistant at surgery). |
| **Concession / Pensioner** | Patient-circumstance categories; don't change the MBS rebate math themselves. |
| **GST** | Goods and Services Tax — most medical services are exempt, hence GST defaults to 0 in this system. |

---

## Where to look in the code

- `src/IntelliMed.Core/Entities/BillingItem.cs`, `FeeSchedule.cs`, `ClientEnums.cs`,
  `AccountTypeFeeScheduleMapping.cs`, `BillingEnums.cs`, `ClientCompensationClaim.cs`
- `src/IntelliMed.Infrastructure/Services/BillingCalculator.cs`,
  `MultipleOperationRuleCalculator.cs`, `DerivedFeeCalculator.cs`
- `src/IntelliMed.Infrastructure/Repositories/FeeScheduleRepository.cs` (BBGP/BBO/BBI/VAGP/VASO/VASI/WCQLD seeding)
- `src/IntelliMed.Web/Pages/ClinicSettings.razor` (Account Type → Fee Schedule mapping UI)
- `HANDOFF.md` §7 (legacy billing engine deep-dive) and §8–9 (legacy Medicare Online claiming —
  reference for when electronic transmission gets built here)
