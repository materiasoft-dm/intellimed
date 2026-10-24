# IntelliMed — User Guide

Written for clinic staff. Every screen and button described here exists in the app today; nothing in
this document is aspirational. File references are given so this stays maintainable as the UI moves.

For the single-visit walkthrough (booking → consult → checkout), see
[PATIENT_VISIT_WORKFLOW.md](PATIENT_VISIT_WORKFLOW.md) — this guide covers everything else and does
not repeat it.

**Contents**
1. [Getting started](#1-getting-started)
2. [Finding and managing patients](#2-finding-and-managing-patients)
3. [The patient record](#3-the-patient-record)
4. [Appointments](#4-appointments)
5. [Clinical records](#5-clinical-records)
6. [Prescribing](#6-prescribing)
7. [Billing and payments](#7-billing-and-payments)
8. [Administration](#8-administration)
9. [Reference](#9-reference)

---

## 1. Getting started

### Signing in
Go to the site root and sign in with your email and password. There is no self-registration — an
administrator creates your login and emails you an invite link (see [User management](#user-management)).

Forgotten passwords go through **Forgot password?** on the login screen, which emails a reset link.
Password rules: minimum 8 characters with upper case, lower case, a digit, and a symbol. Five failed
attempts locks the account for 15 minutes.

### Choosing your clinic
If your login belongs to **more than one clinic**, a **Select a Clinic** dialog appears immediately
after sign-in and blocks the rest of the app until you choose. This is deliberate — almost everything
(patient lists, appointments, invoices) is scoped to the clinic you are working in, so working in the
wrong one silently produces wrong data.

If you belong to exactly one clinic, it is selected for you and you will never see the dialog.

To change clinic mid-session, use the clinic name in the top bar. You will be asked to confirm,
because lists reload against the new clinic.

### Finding things quickly
Press **Ctrl+K** anywhere to open the command palette, type a few letters of a screen or action, and
press Enter. You can turn it off per-user under **Profile Settings**. The catalogue of actions it
searches is editable by admins at **Admin → Search Actions**.

### Your profile
**Profile Settings** (top-right menu) holds your own name, contact details, provider/AHPRA numbers,
and the command-palette preference. Changing your password is also done here.

---

## 2. Finding and managing patients

### Searching
**Find Client** (sidebar) searches by surname, given name, file number, and other identifiers. The
same search is embedded wherever a patient must be picked — for example when raising an invoice — so
you never have to leave a task to go looking one up.

### Creating a patient
**Find Client → New**, or navigate to *Add Client* directly. Only **Surname**, **Given** name and
**D.O.B** are required to save; everything else can be filled in later. Saving takes you straight to
the patient's record.

### Organisations
A client can be a **Person** or an **Organisation** (the type selector at the top of the record). An
organisation — an employer, a school, a company paying for its staff's check-ups — has a name
(*Family/Org*) but no date of birth. Patients link to organisations in two independent ways:

- **Employer** — on the Occupations and WC/TAC tabs ("works for").
- **Payer** — the *DF Payer* on the Details tab ("paid for by"). Invoices bill the payer automatically.

The same organisation can be both, or a patient can be paid for by an organisation they don't work for.

### Address book
**Address Book** (sidebar, under Clients) holds the people the practice deals with — GPs, specialists,
allied health — shared by every clinic. Each contact has a name, profession and speciality, provider
number, contact details, and optionally a **workplace**: a clinic or hospital from Clinic Manager.
Search covers name, provider number, profession, speciality and workplace. Contacts are archived, never
deleted, so referrals that point at them keep working. Organisations are not stored here — they are
clients (see above).

### Who can see which patients
Patients are visible to staff assigned to them via **Providers with Access** on the patient's
Details tab. A newly created patient with no providers assigned is visible to nobody until at least
one is added — the form warns you about this.

SuperAdmin and Admin roles bypass this restriction and see all patients.

---

## 3. The patient record

Opening a patient shows a row of tabs. Tabs other than **Details** are disabled until the patient has
been saved once, since they need a patient ID to attach to.

| Tab | What it holds |
|---|---|
| **Details** | Demographics, entitlements (Medicare/DVA/concession), addresses, contact details, health fund, account type, file number, assigned providers |
| **Referrals** | Inbound referrals *from* other providers. The referring doctor is picked from the [address book](#address-book) (or added there on the spot), which supplies their provider number. Referral date and period drive Medicare billing eligibility |
| **Allergies** | Recorded allergies and adverse reactions. Feeds the prescribing safety check |
| **Health Issues** | The problem list — ongoing and past conditions ([section 5](#health-issues-problem-list)) |
| **Prescriptions** | Every script written for this patient, current and ceased |
| **Progress Notes** | Clinical notes, drafts and signed ([section 5](#progress-notes)) |
| **WC/TAC** | WorkCover and Transport Accident Commission claim details. The employer is an [organisation](#organisations) client |
| **Family** | Family relationships, and linking family members to each other |
| **Occupations** | Employment history. The employer is an [organisation](#organisations) client, picked or added on the spot |
| **eHealth** | My Health Record / IHI identifiers (recording only — no live connection) |
| **UDF** | User-defined fields, configured clinic-wide by an admin |
| **Patient History** | A read-only timeline of everything that has happened — notes, prescriptions, appointments — newest first |

### Safety banner
Where relevant, a banner appears at the top of the patient record and the progress-note editor
flagging: deceased status, free-text warnings recorded on the patient, interpreter requirements, and
whether the patient is a minor. It is informational and never blocks you.

---

## 4. Appointments

### Booking
**New Appointment** — pick patient, practitioner, date and time. New bookings start with status
`Scheduled`.

### Calendar
**Calendar View** shows appointments per practitioner. You can:
- navigate by date,
- **drag** an appointment to reschedule it (the original duration is preserved),
- **drag the edge** to change its length,
- **right-click** for quick status actions.

Colour coding matches the status table in [section 9](#appointment-statuses).

### Appointment list
**Appointments** is the searchable/filterable list view — by date range, practitioner, patient, or
status. Use it for "what happened last Tuesday" questions rather than day-to-day flow.

### Waiting room
**Waiting Room** shows today's `Arrived` and `InProgress` patients across all practitioners, sorted
by arrival time, with a compact allergy/warning indicator per row. This is the clinical staff's
working screen: **Start** moves a patient to `InProgress`, and **Progress Note** does the same *and*
opens a note linked to that appointment in one click.

---

## 5. Clinical records

### Progress notes
Notes follow **SOAP**: Reason for Visit, then Subjective / Objective / Assessment / Plan, plus an
optional Review Date.

- **Save Draft** keeps the note editable.
- **Finalize & Sign** locks the content permanently and offers to mark the appointment `Completed`
  in the same action.

Once signed, a note can never be edited. Corrections are made by adding an **addendum**, which is
appended with its own timestamp and author, leaving the original text intact. This is deliberate —
a clinical record must show what was written at the time, not just the latest version.

**Episodes** group related notes under one heading (for example "Right knee injury — Mar 2026"). Pick
an existing episode or create one inline with **+ New**. Optional; most single-visit notes never use
one.

Notes started from the patient record with no prior booking automatically create a same-day
appointment on first save, so walk-ins still become real, reportable encounters — see
[PATIENT_VISIT_WORKFLOW.md](PATIENT_VISIT_WORKFLOW.md).

### Health issues (problem list)
The patient's ongoing and past conditions — the answer to "what is wrong with this patient?".

**To add one:** patient record → **Health Issues** → **+ New**. Only **Condition** is required.

- **Onset** is recorded as a year, with an optional month. There is deliberately no day field —
  real onset is usually "sometime in 2015", and forcing a full date would invent precision that
  isn't there.
- **Side** records laterality (Left / Right / Bilateral) for conditions where it matters.
- **Active** means the problem is current. Unticking it keeps the issue on the record as history.
- **Show on summary** pins the issue to the compact list displayed at the top of the progress-note
  editor, so it is in view while writing.
- **Confidential** marks sensitive entries.

The filter buttons — **All / Active / Inactive / Summary** — control which issues the list shows.

**Archive** is for entries made in error. It hides the issue from every clinical filter but does not
delete it, so the record stays auditable. Correcting a mistake and marking a problem resolved are
different actions: untick *Active* for the latter.

### Allergies
Recorded per patient with substance, type, reaction, severity, and onset. Where the substance matches
an ingredient in the medicine catalogue it is marked **confirmed**, and the prescribing safety check
can then match on it reliably rather than on text alone.

### Results inbox
**Results** (sidebar, under Clients) is where pathology and imaging reports from labs arrive. A red
number beside it counts what is still waiting.

**How a report gets in.** Staff can **Upload HL7 file** on the Results page. Lab feeds and relays send
reports to the clinic's ingest endpoint instead (see *Connections* below). Either way the same thing
happens: IntelliMed reads the report and tries to file it under a patient. It only does so when exactly
one patient fits — the lab's patient number remembered from an earlier match, or Medicare number plus
date of birth, or exact name plus date of birth. Anything less certain goes to **Unmatched**; it is
never filed on a guess. Sending the same report twice does nothing the second time.

**The tabs.**

| Tab | What is in it |
|---|---|
| **Inbox** | Filed under a patient, waiting for a doctor to acknowledge. Reports with an abnormal value show a red triangle and sort to the top. |
| **Unmatched** | Could not be matched safely. Open one, **Find patient**, and confirm — you see what the lab sent beside the patient you picked. Tick *Remember the lab's patient number* and that lab's next report for the patient files itself. |
| **Held** | Parked by a doctor to review later. Still counts as unacknowledged. |
| **Archived** | Acknowledged. Shows who acknowledged it, when, and their comment. Can be moved back to the inbox. |

**My results and unassigned** (ticked by default) shows reports for the doctor who ordered them plus
any whose ordering doctor could not be identified. A report is routed to a doctor by Medicare provider
number, or by exact name; otherwise it stays in the clinic-wide inbox. Untick it to see the whole clinic.

**Reviewing.** Click a report to open it. Abnormal values are highlighted, lab comments sit under the
test they belong to, and any PDF the lab attached can be downloaded. **Acknowledge** (with an optional
comment) archives it; **Hold for later** parks it. Nothing leaves the inbox any other way, and an
unmatched report cannot be acknowledged until it has a patient.

**Connections (Admin).** Admins see a **Connections** button. It creates an **ingest key** for the
selected clinic — the credential a lab feed or relay presents (`X-Api-Key`) when it POSTs HL7 to
`/api/results/ingest`. The key is shown **once**; IntelliMed keeps only a hash, so a lost key must be
replaced. Each key only delivers into the clinic it was created in, and can be revoked.

Not built yet: raising pathology and imaging *requests* and the outstanding-requests list, turning a
result into a progress note, recall or patient message in one click, a Results tab on the patient
record, and live connections to specific lab or messaging vendors.

---

## 6. Prescribing

Start from the patient record → **Prescriptions** → **New**. The wizard has up to seven steps, and
**skips the ones that don't apply** to the medicine you chose.

1. **Medicine** — search the catalogue inline. Results show schedule, PBS/RPBS listing, restriction
   level, generic name, strength, form and pack size, so you can tell similar items apart before
   selecting. If the item genuinely isn't in the catalogue, use the *free text* option — note that
   free-text items cannot be safety-checked automatically, and the wizard says so.
2. **Dosage** — dose, frequency, route, patient instruction, purpose, start date, and whether the
   script is **Long term** or **Limited**.
3. **Quantity & Repeats** — *shown only for Limited scripts.* Quantity and duration are both
   required here.
4. **Authority / Restricted Benefit** — *shown only when the medicine is PBS Authority Required or
   Restricted.* Authority items take an approval number; Restricted items take an indication
   confirmation.
5. **State Authority** — *shown only for Schedule 8 medicines.* A state authority code is
   **mandatory**; you cannot continue without it.
6. **Reason** — why it is being prescribed, optionally tagged to an Episode, plus prescribing notes
   and notes for the pharmacy.
7. **Review & Issue** — a read-only summary of everything entered, the prescriber selection, and the
   safety check.

**Back** returns to the previous step with everything retained.

### Safety checks
Before a script can be created, IntelliMed checks the medicine's ingredients against the patient's
other active prescriptions and recorded allergies. Warnings are **advisory, never blocking** — but to
proceed you must tick the acknowledgement and type a reason, which is stored on the prescription as
a permanent record that the warning was shown and consciously overridden.

Two situations are called out explicitly rather than passing silently:
- a free-text/compounded item cannot be checked at all;
- the interaction reference database being empty means "nothing to compare against", **not**
  "confirmed safe".

### Issuing and printing
Creating the prescription saves it. **Issue & Print Now** then produces the printable script and
records who issued it, when, and by what delivery method. Issuing also appends a line to the open
progress note for that visit automatically.

Once issued, the clinical parameters (dose, frequency, quantity, repeats) lock. To change the
therapy, cease the prescription and write a new one. Notes and reason stay editable.

---

## 7. Billing and payments

### Raising an invoice
**New Invoice**, or from a `Completed` appointment's details panel, which pre-fills the patient and
appointment. Line items are drawn from the active fee schedule.

### Payments and receipting
A payment can be recorded at the same time as the invoice, or later via **Payments**. Receipting is
built on a receipt → allocation model, so one receipt can pay several invoices and be split across
tender types (cash, EFTPOS, and so on).

### Invoice actions
From an invoice: **Print Invoice**, **Refund**, **Write Off**, **Cancel** (with a reason), and
**Split Invoice**. Receipts print separately.

### Fee schedules
**Fee Schedules** holds the item catalogue and pricing, with price history retained. **Derived Item
Configs** covers items whose fee is calculated from another item rather than set directly.

Account type (private, bulk-bill, DVA, concession, WorkCover, TAC…) determines which schedule
applies to a given patient. Background on Australian billing concepts is in
[BILLING_AND_CLAIMING.md](BILLING_AND_CLAIMING.md).

> Electronic claiming (Medicare Online / PRODA / Tyro) is **not** implemented. Invoices and receipts
> are produced locally and claimed by whatever process the practice already uses.

---

## 8. Administration

All of the following live under **Admin** in the sidebar and are restricted by role.

### User management
Create staff logins, assign roles, activate/deactivate accounts, and link a login to a
**Practitioner** record. That link matters: it drives per-provider patient visibility and pre-fills
the prescriber on clinical screens.

New users are invited by email and set their own password from the link — administrators never see
or type a user's password.

### Roles and permissions
**Admin → Roles** shows what each role can do as a tree. Pick a role, then tick what it may do:

```
Clinical                          ← category
  Appointments                    ← parent
    View · Create · Update · Remove · Waiting room
  Clients
    View · Create · Update · Remove
Financial
  Invoices
    View · Create · Update · Remove · Refund · Write off · Cancel · Split
```

- **Ticking a parent grants everything under it**; unticking it takes everything away. This works at
  both levels — tick *Appointments* for all five appointment permissions, or *Clinical* for every
  clinical one.
- A **partly ticked parent** (a dash instead of a tick) means only some of what is under it is
  granted. The count beside it (for example *4 of 5*) says how many. Clicking a partly ticked parent
  grants the rest.
- Tick or untick any single permission to fine-tune. A parent is never granted on its own — it simply
  reflects its children — so *View* only is a perfectly valid grant.
- A permission that stands alone (Address Book, Payments, Results Inbox, each Admin screen) appears
  directly under its category with no parent.
- **Search** narrows the tree to what matches, keeping the parents so you can see where a match lives;
  a parent still shows the state of *everything* under it, including what is hidden by the search.
  **Expand all / Collapse all** and the arrow beside each parent fold sections away without changing
  what is ticked. **Select All** and **Clear All** apply to the whole role.
- Nothing is saved until **Save Permissions**. Changes then take effect immediately.

Under the hood each role still holds a plain list of page keys (`appointments`, `appointments/create`
and so on) — the tree is just a faster way to set them, so existing roles carry over unchanged.

Five roles ship by default: **SuperAdmin** (all access, always — it bypasses the permission table
entirely), **Admin**, **Doctor**, **Nurse**, **Receptionist**. See the matrix in
[section 9](#who-can-do-what).

> A new feature needs its page keys granted here as well as in code. If a screen is invisible or an
> action returns a permissions error for a role that should have it, this is the first place to look.

### Medicines catalogue
**Admin → Medicines** is the drug catalogue behind prescribing.

- **Sync from Sources** pulls from the PBS API and Wikidata, imports, then enriches ingredients with
  ATC codes. It takes 10–15 minutes, almost all of it waiting out the PBS rate limit.
- **Import / Export** handles CSV round-trips.
- **Add Medicine** creates a manual entry.
- **Poisons Scheduling (TGA ARTG)** backfills the S2/S3/S4/S8 schedule, which PBS does not publish
  at all. Without it the prescribing wizard's Schedule 8 step cannot fire on synced medicines.

#### Running the poisons-scheduling backfill

1. Go to the [ARTG Search Visualisation Tool](https://compliance.health.gov.au/artg/).
2. **Apply the `Medicines` filter before exporting.** This matters: the unfiltered export has 8
   columns and carries no scheduling at all, and the upload will simply report nothing filled.
3. Export and save as CSV (the file is large — roughly 95 MB, around 37,000 registrations).
4. Upload it under *Poisons Scheduling* and press **Backfill Scheduling**. It takes a few seconds.
5. The result line reports how many medicines matched and what was filled, with a breakdown by
   schedule. A typical run fills around 5,400 schedules including ~253 Schedule 8.

Existing values are never overwritten, so re-running is safe and a schedule you have corrected by
hand always wins. Re-upload a fresh export every few months, and after any sync that adds medicines.

**Treat a backfilled schedule as a prompt to verify, not as authority.** Matching is by brand, so
all strengths of a brand share one schedule, and where a brand family spans schedules the strictest
one is applied to the whole family.

Synced rows are read-only by design; manual rows can be edited and deactivated. Medicines are never
hard-deleted, because prescriptions reference them.

PBS data carries Commonwealth of Australia copyright and must not be modified — the notice is
displayed on the page footer. See [README.md](README.md) for licensing of each source.

### Drug interactions
**Admin → Drug Interactions** maintains the reference data behind the prescribing safety check:
ingredient pair, severity, description, and source dataset. Add entries manually or import a CSV.
When editing an existing entry the ingredient pair is locked, since it identifies the row.

> This is an open-dataset, best-effort check — not a commercial clinical decision support product.
> It is advisory only.

### System settings
**Admin → System Settings** holds application-wide configuration, as distinct from **Clinic
Settings** which covers this practice's own details:

- **Password policy** — minimum length and which character classes are required.
- **Sign-in and sessions** — failed attempts before lockout, lockout duration, and how long a
  session lasts.
- **Defaults for new users** — default appointment slot length, and whether the Ctrl+K palette
  starts enabled.
- **Backups** — how many days of backups to keep before older ones are pruned.

Password and lockout changes take effect **the next time the application restarts**; the screen says
so. Session length applies at the next sign-in, and the other settings apply immediately. Existing
passwords are never invalidated by a policy change — the new rules apply to new and changed
passwords from that point on.

### Other admin screens
- **Appointment Types** — durations and defaults for booking.
- **Email Templates** — templates for outbound email, including the user invite.
- **Search Actions** — the catalogue behind the Ctrl+K palette.
- **Clinic Settings / Clinic Manager** — practice details, SMTP configuration, and multi-clinic setup. Each entry in Clinic Manager is a **Clinic** or a **Hospital**; external clinics and hospitals added here can be set as an address-book contact's workplace, and only appear in someone's clinic switcher if they are assigned to it.
- **Database Backups** — scheduled and manual backups, with interval configuration.

### Database admin console
A separate SQL console lives at **`/dbadmin`**. It is deliberately isolated from staff logins: it has
its own dedicated username and password, no staff role reaches it (not even SuperAdmin), and it is
not listed in the app's navigation. Any statement that could modify data triggers a verified backup
first, and every login and query is logged.

Treat it as a break-glass tool.

---

## 9. Reference

### Appointment statuses

| Status | Meaning |
|---|---|
| `Scheduled` | Booked, nothing else has happened |
| `Confirmed` | Booking confirmed with the patient (optional step) |
| `Arrived` | Patient checked in, waiting to be seen |
| `InProgress` | Patient currently with the practitioner |
| `Completed` | Visit finished — an invoice can now be raised |
| `Cancelled` | Cancelled before the visit happened |
| `NoShow` | Patient did not attend |

### Who can do what

Defaults as shipped. All of it is editable at **Admin → Roles**.

| Area | SuperAdmin | Admin | Doctor | Nurse | Receptionist |
|---|---|---|---|---|---|
| Patients | Full | Full + delete | View/add/edit | View/add/edit | View/add/edit |
| Appointments | Full | Full + delete | View/add/edit | View/add/edit | View/add/edit |
| Waiting room | Yes | Yes | Yes | Yes | Yes |
| Address book | Yes | Yes | Yes | Yes | Yes |
| Results inbox | Yes | Yes | Yes | Yes | — |
| Progress notes | Full | **View only** | Full | Full | — |
| Health issues | Full | **View only** | Full | Full | — |
| Prescriptions | Full | **View only** | Full | **View only** | — |
| Invoices | Full | Full (refund, write-off, void, split, delete) | **View only** | — | View + create |
| Payments | Full | Yes | — | — | Yes |
| Fee schedules | Full | Yes | — | — | — |
| Admin screens | Full | Yes | — | — | — |
| Reports *(no screens yet)* | Full | Financial + clinical | Clinical | — | — |
| `/dbadmin` console | **No** | **No** | **No** | **No** | **No** |

Worth noting, because they surprise people:
- **Admin is not a clinical role.** It can *read* notes, health issues and prescriptions but not
  author them. Clinical authoring is Doctor and Nurse only.
- **Nurses can write notes and health issues, but not prescriptions** — they get read access only.
- **Receptionists can raise an invoice and take a payment, but cannot refund, write off, void or
  split one.** Those are Admin actions.
- **Nobody reaches `/dbadmin` through a staff login**, by design — it has a separate credential.
- **The sidebar only shows what you can actually use.** Every nav link is filtered by these
  permissions, so a role that lacks Invoices simply won't see the Invoices section rather than
  seeing links that fail when clicked.
- **Email Templates, Appointment Types and Search Actions are genuinely delegatable.** Granting one
  of those to a non-admin role now works end to end — previously the toggle changed the menu but the
  underlying screen still refused the request.
- **Database Backups is the deliberate exception.** Its toggle controls whether the link appears,
  but the screen itself stays SuperAdmin/Admin only no matter who you grant it to — a backup is a
  complete copy of every patient and billing record, so it isn't delegatable by a menu tick.

### Things that are deliberately permanent

IntelliMed favours append-only clinical records. These actions cannot be undone:

- **Finalising a progress note** — corrections become addenda.
- **Issuing a prescription** — clinical parameters lock; change of therapy means cease and re-write.
- **Archiving** anything — hides the record, never deletes it.

### What is not built yet

So nobody goes looking: electronic prescribing (eRx) transmission, Medicare/PRODA electronic
claiming, My Health Record upload, pathology and radiology *requests* and live lab connections
(results can be received and reviewed — see *Results inbox*), immunisation records and AIR
submission, referral-letter authoring, care plans, recalls and reminders, and reporting screens.
Permission entries exist for Reports, but the screens themselves do not exist yet.

There is also **no application audit trail** — nothing records who changed a patient, invoice or
note. The only audit data captured is for the `/dbadmin` console (its own logins and queries), which
is deliberately not surfaced in the main app. An "Audit Log" nav entry used to appear here; it led
nowhere and has been removed until a real audit trail exists.

---

*Maintained alongside the app. When a screen changes, update the matching section here — the file
references throughout exist to make that quick.*
