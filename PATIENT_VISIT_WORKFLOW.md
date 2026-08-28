# Patient Visit Workflow

A step-by-step walkthrough of a single patient visit in IntelliMed, from booking through to
checkout — written for staff training. Every step below maps to a real, working screen/button in
the app (not aspirational); file references are given so this doc can be kept in sync as the UI
changes.

An HTML version formatted as a printable training handout is generated from this content —
ask whoever ran the last training session for the current copy, or regenerate it from this file.

## The visit, start to finish

### 1. Booking (before the visit)
**Who:** Reception
Book via **New Appointment** or the **Calendar View**, picking Client, Practitioner, date and
time. The appointment starts in `Scheduled` status.
*(`NewAppointment.razor` / `AppointmentCalendar.razor`)*

### 2. Confirm (optional)
**Who:** Reception
If the practice calls to confirm the booking ahead of time, open the appointment's **Details**
panel and click **Confirm**. Status → `Confirmed`. This step is optional — skipping it doesn't
block anything later.
*(`AppointmentDetailsPanel.razor`)*

### 3. Check-in
**Who:** Reception
When the patient physically arrives, open the appointment **Details** panel and click
**Mark Arrived**. Status → `Arrived`, and an arrival timestamp is recorded. The patient now
appears in the **Waiting Room** list.
*(`AppointmentDetailsPanel.razor` → `WaitingRoom.razor`)*

### 4. Doctor calls the patient in
**Who:** Doctor / Nurse
From the Waiting Room, click either:
- **Start** — flips status to `InProgress`, or
- **Progress Note** — does the same status flip *and* opens a fresh clinical note for this visit,
  already linked to the appointment.
*(`WaitingRoom.razor`)*

### 5. The consult
**Who:** Doctor / Nurse
Document the visit in the Progress Note: Reason for Visit, then Subjective / Objective /
Assessment / Plan. If it's part of an ongoing issue (e.g. "Pre-natal care", "Right knee injury"),
tag the note to an Episode — pick an existing one or start a new one inline.

If a prescription is written during this same visit, issuing it automatically adds a line to the
still-open note (e.g. "Prescribed: Amoxil 500mg tds") — nothing needs to be re-typed.
*(`ProgressNoteEditor.razor`, `PrescribeNew.razor`)*

### 6. Finalize the note
**Who:** Doctor / Nurse
Click **Finalize & Sign**. This locks the note's content — any correction after this point is
added as a timestamped addendum, never an edit to the original — and offers a checkbox to also
mark the appointment **Completed** in the same click.
*(`ProgressNoteEditor.razor`)*

### 7. Checkout / billing
**Who:** Reception
Once the appointment is `Completed`, a **New Invoice** button appears on its Details panel,
already linked to the client and the appointment — no need to re-search for either.
*(`AppointmentDetailsPanel.razor` → `AddInvoice.razor`)*

### 8. Payment
**Who:** Reception
The same invoice screen has a "record payment now" option (cash, EFTPOS, etc.), so payment can be
taken immediately — or left open if the patient is claiming through a health fund.
*(`AddInvoice.razor`)*

### 9. Patient leaves
The full trail for the visit — arrival/start/completion timestamps, the signed note, any
prescriptions, the invoice, and the payment — is all linked back to that one appointment.

## Walk-ins (no prior booking)

The Waiting Room only lists appointments that were already booked, so there's no "check a walk-in
in" step there. The actual flow:

1. **Find or create the patient.** Use **Find Client**. If they're brand new, create the client
   record first — same as any other new patient.
2. **Open the client's Progress Notes tab and click + New.** Not through the Waiting Room — direct
   from the client record.
3. **Nothing is created yet.** The editor opens empty. If staff leave without saving, no
   appointment or note is left behind.
4. **The first save creates the appointment.** The moment **Save Draft** or **Finalize & Sign** is
   clicked for the first time, IntelliMed creates a same-day appointment (dated today, starting
   now, tagged `"Auto-created for a walk-in progress note"`), immediately marks it `InProgress`,
   and links the note to it. From this point it's a real appointment like any other — it shows up
   in appointment lists and reports.
5. **Everything from here is identical to a booked visit** — consult, prescribing (still
   auto-logs into the note), Finalize & Sign (still offers to mark the appointment Completed),
   checkout, and payment.

**Watch for:** the auto-created appointment is attributed to whichever Practitioner is selected in
the note *at the moment of that first save*. Changing the Practitioner on the note afterward
updates the note but does **not** re-attribute the underlying appointment — get the practitioner
right before the first save.

**Edge case:** if a patient has a booking later today but walks in early for something unrelated,
use this same **+ New** path anyway — it creates a second, separate appointment just for the
walk-in, distinct from their existing booking.

*(`AddClient.razor` → `ProgressNoteEditor.razor`, `ResolveAppointmentIdForCreateAsync`)*

## Appointment status reference

The colored badge on an appointment always reflects one of these states:

| Status | Meaning |
|---|---|
| `Scheduled` | Booked, nothing else has happened yet |
| `Confirmed` | Booking confirmed with the patient (optional step) |
| `Arrived` | Patient has checked in, waiting to be seen |
| `InProgress` | Patient is currently with the doctor/nurse |
| `Completed` | Visit finished — invoice can now be raised |
| `Cancelled` | Booking cancelled before the visit happened |
| `NoShow` | Patient didn't attend |
