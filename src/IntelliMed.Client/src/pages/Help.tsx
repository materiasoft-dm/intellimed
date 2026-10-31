import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { AppointmentStatus } from '@/api/types.gen';
import { AppointmentStatusBadge } from '@/components/StatusBadges';
import { usePageTitle } from '@/lib/hooks';

function Step({ n, title, who, optional, accent = 'primary', children }: {
  n: number | 'done'; title: string; who?: string; optional?: boolean; accent?: 'primary' | 'success' | null; children: ReactNode;
}) {
  return (
    <div className={`list-group-item ${accent ? `border-start border-3 border-${accent}` : ''} ps-3`}>
      {n === 'done'
        ? <span className="badge bg-success me-1"><i className="bi bi-check-lg" /></span>
        : <span className="badge bg-secondary me-1">{n}</span>}{' '}
      <strong>{title}</strong>
      {optional && <span className="text-muted small"> — optional</span>}
      {who && <span className="badge bg-light text-dark border ms-1">{who}</span>}
      <p className="mb-0 mt-1 small">{children}</p>
    </div>
  );
}

const STATUS_LEGEND: [AppointmentStatus, string][] = [
  [AppointmentStatus.Scheduled, 'Booked, awaiting the visit'],
  [AppointmentStatus.Confirmed, 'Booking confirmed with the patient'],
  [AppointmentStatus.Arrived, 'Checked in, waiting to be seen'],
  [AppointmentStatus.InProgress, 'Currently with the doctor/nurse'],
  [AppointmentStatus.Completed, 'Visit finished, ready to invoice'],
  [AppointmentStatus.Cancelled, 'Booking cancelled before the visit happened'],
  [AppointmentStatus.NoShow, "Patient didn't attend"],
];

export default function Help() {
  usePageTitle('User Guide - IntelliMed');
  return (
    <>
      <h4 className="mb-1">User Guide</h4>
      <p className="text-muted small mb-4">Step-by-step walkthroughs for common tasks in IntelliMed.</p>

      <div className="card mb-3">
        <div className="card-header"><strong>The Patient Visit, Start to Finish</strong></div>
        <div className="card-body">
          <p className="text-muted small">From booking through to checkout — what to click, and when.</p>
          <div className="list-group list-group-flush">
            <Step n={1} title="Booking" who="Reception">
              Book via <strong>New Appointment</strong> or the <strong>Calendar View</strong> — pick the client, practitioner, date and time.
              Starts as <AppointmentStatusBadge status={AppointmentStatus.Scheduled} />.
            </Step>
            <Step n={2} title="Confirm" who="Reception" optional>
              If you call ahead to confirm the booking, open the appointment's <strong>Details</strong> panel and click <strong>Confirm</strong>.
              Skipping this doesn't block anything later.
            </Step>
            <Step n={3} title="Check-in" who="Reception">
              When the patient arrives, open <strong>Details</strong> and click <strong>Mark Arrived</strong>.
              They now appear in the <Link to="/appointments/waiting-room">Waiting Room</Link> list.
            </Step>
            <Step n={4} title="Doctor / Nurse Calls Them In" who="Doctor / Nurse" accent="success">
              From the Waiting Room, click <strong>Start</strong> — or click <strong>Progress Note</strong> to do the same thing and open the note in one step.
            </Step>
            <Step n={5} title="The Consult" who="Doctor / Nurse" accent="success">
              Document Reason for Visit, then Subjective / Objective / Assessment / Plan. Part of an ongoing issue?
              Tag the note to an <strong>Episode</strong> — pick an existing one or start a new one right there.
              Prescribe something during the visit and it's <strong>logged into the note automatically</strong> the moment it's issued.
            </Step>
            <Step n={6} title="Finalize the Note" who="Doctor / Nurse" accent="success">
              Click <strong>Finalize &amp; Sign</strong>. The note locks — later corrections are added as a dated addendum, never an edit to
              what was originally written. Tick the box to also mark the appointment <AppointmentStatusBadge status={AppointmentStatus.Completed} /> in the same click.
            </Step>
            <Step n={7} title="Checkout / Billing" who="Reception">
              Once the visit is <AppointmentStatusBadge status={AppointmentStatus.Completed} />, a <strong>New Invoice</strong> button appears right on the
              appointment — already linked to the client and the visit.
            </Step>
            <Step n={8} title="Payment" who="Reception">
              The invoice screen has a "record payment now" option — cash, EFTPOS, etc. Take it on the spot, or leave the invoice open
              if the patient is claiming through a health fund.
            </Step>
            <Step n="done" title="Patient Leaves" accent={null}>
              Everything from this visit — timestamps, the signed note, prescriptions, the invoice, the payment — stays linked to this
              one appointment for good.
            </Step>
          </div>
        </div>
      </div>

      <div className="card mb-3">
        <div className="card-header"><strong><i className="bi bi-signpost-split me-1" />Walk-ins (No Prior Booking)</strong></div>
        <div className="card-body">
          <p className="text-muted small">The Waiting Room only lists appointments booked in advance, so there's no check-in step there for a walk-in. Here's the actual flow:</p>
          <ol className="small ps-3 mb-3">
            <li className="mb-2"><strong>Find or create the patient</strong> — <strong>Find Client</strong>, search for them. Brand new patient? Create the client record first, same as normal.</li>
            <li className="mb-2"><strong>Open Progress Notes → + New</strong> — on the client's record, not through the Waiting Room.</li>
            <li className="mb-2"><strong>Nothing is created yet</strong> — the editor opens empty. Leave without saving and nothing is left behind.</li>
            <li className="mb-2">
              <strong>First save creates the appointment</strong> — the moment <strong>Save Draft</strong> or <strong>Finalize &amp; Sign</strong> is
              clicked for the first time, IntelliMed creates a same-day appointment (dated today, starting now), immediately marks it{' '}
              <AppointmentStatusBadge status={AppointmentStatus.InProgress} />, and links the note to it. From here it's a real appointment like any other.
            </li>
            <li><strong>Everything after that is identical to a booked visit</strong> — consult, prescribing, Finalize &amp; Sign, checkout, payment.</li>
          </ol>
          <div className="alert alert-warning small py-2 mb-2">
            <i className="bi bi-exclamation-triangle me-1" />
            <strong>Double-check the Practitioner field before that first save.</strong> The auto-created appointment is attributed to
            whichever practitioner is selected at that moment — changing it on the note afterward does <em>not</em> update the appointment.
          </div>
          <div className="alert alert-secondary small py-2 mb-0">
            <i className="bi bi-info-circle me-1" />
            If a patient has a booking later today but walks in early for something unrelated, use this same <strong>+ New</strong> path —
            it creates a second, separate appointment for the walk-in.
          </div>
        </div>
      </div>

      <div className="card">
        <div className="card-header"><strong>Appointment Status Badges</strong></div>
        <div className="card-body">
          <p className="text-muted small">These colours appear throughout the app on every appointment.</p>
          <table className="table table-sm align-middle mb-0">
            <tbody>
              {STATUS_LEGEND.map(([status, text]) => (
                <tr key={status}>
                  <td style={{ width: 160 }}><AppointmentStatusBadge status={status} /></td>
                  <td className="small text-muted">{text}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </>
  );
}
