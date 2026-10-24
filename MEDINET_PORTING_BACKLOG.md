# Medinet → IntelliMed porting backlog

What legacy **Medinet** (PrimaryClinic Medical, the clinical half of the legacy suite) does that
IntelliMed does not, as a working to-do list. Built 2026-10-22 from Medinet's own screens and record
controls (`SourceCode/ClinNet/` forms and controls, `ClinNetContentGUI`, and the integration projects
listed in Medinet's `HANDOFF.md` §1), checked against IntelliMed's `USER_GUIDE.md`.

The first four items match the priorities already agreed in `ARCHITECTURE.md` ("Next Steps") and
`OPEN_THREADS.md`; this list adds what that comparison did not cover. Prescribing detail is tracked
separately in `PRESCRIBING_PARITY.md` and is only summarised here.

**Size** is a rough guide only: **S** ≈ days, **M** ≈ 1–2 weeks, **L** ≈ several weeks,
**XL** = needs an external conformance or certification programme, not just build effort.

---

## Already in IntelliMed — not on this list

Appointments (booking, calendar, list, waiting room, recurring series), patient demographics and
entitlements, allergies, the problem list (health issues), progress notes with episodes and addenda,
the prescribing wizard and interaction checks, invoicing/receipting/fee schedules, referrals
*received*, WorkCover/TAC, family, occupations, user-defined fields, eHealth identifiers (recorded
only), and the patient history timeline.

---

## Tier 1 — core daily clinical workflow

| # | Feature | What Medinet has | Depends on | Size |
|---|---|---|---|---|
| 1 | **Results inbox (pathology & radiology)** — *inbox, HL7 ingest, matching, unmatched queue, acknowledge/hold built; history/compare, holding files per doctor and vendor feeds still open* | HL7 v2 ingestion (`Abaki.Core.HL7`), inbox (`frmInbox`), result viewer with history and side-by-side compare (`ShowResultsCtrl`, `PathologyCompareCtrl`, `HistoryResultCtrl`), match an unmatched result to a patient (`MatchPatientCtrl`), archived results, a doctor's **holding files** of items still to review | #5 contacts (labs as senders) | L |
| 2 | **Pathology & imaging requests** | Request forms with test sets and favourite tests (`PathologyRequestCtrl`, `SetupFavouritePathoTestsCtrl`, `MedicalImagingRequestCtrl`), printing, and an **outstanding requests** list that clears as results arrive (`frmOutstandingRequest`) | #1 to close the loop | M |
| 3 | **Recalls & reminders** | Set up a recall from a note or result (`frmSetupRecall`), recall/reminder lists and views (`frmMNRecall`, `frmMNReminders`, `frmRecallView`), pop-up prompts on opening a patient (`RecallMsgCtrl`, `ReminderMsgCtrl`) | #20 SMS / #4 letters to notify patients | M |
| 4 | **Letters** | Letter authoring from templates (`Abaki.Core.Letter`, Word templates), letter list and search, **holding letters**, a **typing list** (letters queued for typing, linked to the visit's invoice), email with template (`frmSendEmailWithTemplate`) | #5 contacts; reuse IntelliMed's email templates | M–L |
| 5 | **Contacts** | A contact is one of three kinds: a **patient** (an existing client), an **address-book person** (GP, specialist, allied health) or an **organisation** (lab, hospital, pharmacy, insurer, employer). Legacy's address book holds the last two (`eAddressBookType`: `Person`, `Organisation`); patients are separate records. Medinet adds contact management and quick search (`ContactManagementCtrl`, `frmFindContact`) and import from Best Practice's address book (`BP_ImportAddressBookCtrl`). In IntelliMed today, referring providers (`ClientReferral`) and employers (`ClientOccupation`) are free text and health funds are their own table — candidates to link to contacts | — | M |
| 6 | **Care plans & health assessments** | GP management plans and team care arrangements with tasks (`CarePlanCtrl`, `CareTaskCtrl`, print), health assessments (`HealthAssessmentCtrl`), medication reviews (`MedicationReviewCtrl`) — all billable MBS items | Problem list (done); invoicing (done) | L |
| 7 | **Immunisations** | Record given vaccines and schedules (`ImmunisationCtrl`, `ImmScheduleCtrl`), vaccine defaults, natural immunity and medical contraindications, "at risk" prompts that offer to prescribe or update (`frmAtRisk`), influenza/pneumococcal at-risk searches | — | M |
| 7a | **AIR submission** | Report to the Australian Immunisation Register and pull history (`AIRCtrl`, `AIRImmunisationHistoryCtrl`, `frmNewImmunisationClaim`) — the legacy Pracnet code for this is documented in `HANDOFF.md` §13 | #7; Services Australia conformance | XL |

## Tier 2 — depth of the clinical record

| # | Feature | What Medinet has | Size |
|---|---|---|---|
| 8 | **Observations & charts** | Blood pressure entry (`frmClinicalBP`), measurement history (weight, BMI, BP, etc.) and trend graphs (`MeasureHistoryCtrl`, `ClinicalGraphicViewCtrl`), unit conversion | M |
| 9 | **Family & social history** | Family history (`FamilyHistoryCtrl`), smoking and alcohol (`SmokingAlcoholCtrl`), past history (`PastHistoryCtrl` — check overlap with inactive health issues first) | S–M |
| 10 | **Clinical assessment tools** | K10, Edinburgh postnatal depression, geriatric depression, mental state exam, physical activity, pain record (`Assessments` folder, `Abaki.Exam`). Best built as one scored-questionnaire engine | M |
| 11 | **Chronic disease cycles** | Diabetes and asthma records and annual-cycle tracking (`DiabetesCtrl`, `DiabetesAssessmentCtrl`, `AsthmaCtrl`) plus their population searches | M each |
| 12 | **Preventive health & screening** | Cervical screening register and results (`PapSmearsCtrl`, `CytologyCtrl`), preventive activities (`PreventiveCtrl`) | M |
| 13 | **Antenatal / pregnancy** | Pregnancy record, visits, parity, past pregnancies, pregnancy pathology, gestational-age calculator (`PregnancyCtrl` and related) | M |
| 14 | **Documents & scanning** | Attach and view documents per patient (`DocumentCtrl`, `DocumentImportCtrl`, `DocumentSearchCtrl`, `frmViewDoc`), scan storage, patient photo. In a browser app, TWAIN scanning becomes upload | M |
| 15 | **Patient summary printout** | Demographic and clinical summary (`frmDemographicSumary`, `SumaryCtrl`) — also the basis for a My Health Record shared health summary later | S |

## Tier 3 — prescribing extras (see `PRESCRIBING_PARITY.md` before starting)

| # | Feature | What Medinet has | Size |
|---|---|---|---|
| 16 | **Prescribing productivity** | Quick scripts / favourites (`QuickScriptCtrl`), dose calculator (`frmDoseCalculator`), emergency supply (`frmEmergencySupply`), batch printing of scripts e.g. for nursing homes (`frmRxBatchManager`, `NursingHomeMedCtrl`) | S–M each |
| 17 | **Compounded medicines (recipes)** | Custom recipes (`RecipeCtrl`, `AddRecipeCtrl`) | S–M |
| 18 | **Patient leaflets** | Custom leaflets (`CustomLeafletCtrl`) are portable; MIMS consumer leaflets (CMI) need a MIMS licence — IntelliMed's catalogue deliberately avoids MIMS (`MEDICINE_DATA_ENRICHMENT.md`) | S (custom) |
| 19 | **eRx / electronic scripts** | eScript tokens, send to pharmacy, Active Script List (`PrimaryClinic.Erx.Adapter`, `frmSendToPharmacy`) — already "deferred indefinitely" | XL |

## Tier 4 — communications and integrations

| # | Feature | What Medinet has | Size |
|---|---|---|---|
| 20 | **SMS** | SMS sending (`Abaki.Common.SMS`) for appointment reminders and recall notices. IntelliMed already stores SMS consent on the patient | M |
| 21 | **Secure messaging & fax** | Argus secure messaging (message monitor, Argus address book), fax (`Abaki.Fax`), viewing sent email | L |
| 22 | **My Health Record & HI Service** | IHI lookup (`Abaki.HICOnline`), MHR document upload and indicator (`PCEHR.Adaptor`, `PCEHRCtrl`) — already "deferred indefinitely" | XL |
| 23 | **Medical device results** | ECG, echocardiography, stress echo, DICOM and X-ray viewing (`MedicalDataDevices`) — niche; only if a customer needs it | L |

## Tier 5 — practice operations and reporting

| # | Feature | What Medinet has | Size |
|---|---|---|---|
| 24 | **Clinical searches & reports** | Population searches (`frmClinicalSearch`; diabetes, asthma, influenza, pneumococcal, prescription summary). IntelliMed has Reports permission keys but no screens | M |
| 25 | **PIP QI / PenCAT data extract** | Practice Incentives Program Quality Improvement extract (`PIPQIExtractLib`) and PenCAT export — tied to practice incentive payments | M |
| 26 | **Finalise visit** | End-of-consultation wrap-up (`frmFinalizeVisit`): closes the visit and hands off to billing. IntelliMed already has "Start Invoice" from a completed appointment — extend rather than copy | S–M |
| 27 | **Lock screen / re-authentication** | Quick lock for shared workstations (`frmLockScreen`, `frmReAuthentication`) | S |
| 28 | **Clinical coding** | ICPC-2 coding support for the problem list — improves search and PIP QI data quality | S–M |
| 29 | **Patient tags** | Free-form tags on patients (`TagsCtrl`) | S |

## Niche — only on customer demand

Travel medicine (`TravelMedicineCtrl`, travel diseases), acupuncture records (`AcupunctureCtrl`),
handwritten notes and freehand drawing (`HandWriteNotes`, `FHDrawing`), camera capture
(`CanonCameraAppLib`), vaccine-trial history.

## Not to port

| Medinet component | Why not |
|---|---|
| Server/workstation data sync (`Abaki.Sync`) | IntelliMed is a single server-hosted app; there's nothing to sync |
| MIMS FastTrack COM engine, MIMS monthly database builds | Already being retired in Medinet itself (MED-1267); IntelliMed sources medicines from PBS/TGA |
| Licensing, EULA, installer, workstation auto-updater, DDE server | Desktop-distribution concerns that a web app doesn't have |
| P2K and template/letter converters | One-off migrations from an older product |

## Suggested order

1. **#5 Contacts**, then **#1 Results inbox** with **#2 Requests** — the biggest daily gap, and
   contacts unblock letters and results.
2. **#3 Recalls & reminders**, with **#20 SMS** for notifications.
3. **#4 Letters**.
4. **#6 Care plans & health assessments** and **#7 Immunisations** — billable and preventive.
5. Tier 2 record depth (#8–#15), then reporting (#24, #25).
6. The XL items (#7a AIR, #19 eRx, #22 MHR) only as funded conformance projects.
