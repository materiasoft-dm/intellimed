import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router';
import { AppointmentApi } from '@/api/appointments';
import { AppointmentStatus, type AppointmentDto } from '@/api/types.gen';
import { AppointmentDetailsModal } from '@/components/AppointmentDetails';
import { EmptyState, LoadingState } from '@/components/States';
import { toUtcDate } from '@/lib/dates';
import { useMountEffect, usePageTitle } from '@/lib/hooks';

function waitTime(a: AppointmentDto, now: number): string {
  const arrived = toUtcDate(a.arrivedAt);
  if (!arrived) return '-';
  const end = toUtcDate(a.seenAt)?.getTime() ?? now;
  const minutes = Math.max(0, Math.floor((end - arrived.getTime()) / 60000));
  return minutes >= 60 ? `${Math.floor(minutes / 60)}h ${minutes % 60}m` : `${minutes}m`;
}

export function clientFlagsSummary(a: Pick<AppointmentDto, 'clientDeceased' | 'clientWarnings' | 'clientInterpreterRequired' | 'clientInterpreterLanguage' | 'clientAge'>): string {
  const flags: string[] = [];
  if (a.clientDeceased) flags.push('Recorded as deceased');
  if (a.clientWarnings?.trim()) flags.push(a.clientWarnings);
  if (a.clientInterpreterRequired) flags.push(`Interpreter required${a.clientInterpreterLanguage?.trim() ? ` (${a.clientInterpreterLanguage})` : ''}`);
  if (a.clientAge != null && a.clientAge < 18) flags.push(`Minor (age ${a.clientAge})`);
  return flags.join(' · ');
}

const statusClass = (s: AppointmentStatus) =>
  s === AppointmentStatus.Arrived ? 'bg-warning text-dark' : s === AppointmentStatus.InProgress ? 'bg-primary' : 'bg-secondary';

export default function WaitingRoom() {
  usePageTitle('Waiting Room - IntelliMed');
  const navigate = useNavigate();
  const [appointments, setAppointments] = useState<AppointmentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState<number | null>(null);
  const [now, setNow] = useState(Date.now());

  const load = async () => {
    setLoading(true);
    setAppointments(await AppointmentApi.waitingRoom());
    setLoading(false);
  };
  useMountEffect(load);

  // re-render every 30 s so wait times stay current without a manual refresh
  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), 30_000);
    return () => clearInterval(t);
  }, []);

  const advance = async (a: AppointmentDto, status: AppointmentStatus) => {
    await AppointmentApi.updateStatus(a.id, status);
    await load();
  };

  // opening the note is itself "starting" the consult if it hasn't been marked that way yet
  const goToProgressNote = async (a: AppointmentDto) => {
    if (a.status === AppointmentStatus.Arrived) await AppointmentApi.updateStatus(a.id, AppointmentStatus.InProgress);
    navigate(`/clients/${a.clientId}/progress-notes/new?appointmentId=${a.id}`);
  };

  return (
    <>
      <div className="d-flex align-items-center justify-content-between mb-3">
        <h4 className="mb-0">Waiting Room</h4>
        <button className="btn btn-sm btn-outline-primary" onClick={load} disabled={loading}>
          <i className={`bi bi-arrow-clockwise me-1 ${loading ? 'imed-spin' : ''}`} /> Refresh
        </button>
      </div>

      {loading && appointments.length === 0 ? (
        <div className="card"><div className="card-body"><LoadingState text="Loading..." /></div></div>
      ) : appointments.length === 0 ? (
        <div className="card"><div className="card-body">
          <EmptyState message="No one is currently waiting or with a practitioner." icon="bi-people" />
        </div></div>
      ) : (
        <div className="card">
          <div className="table-responsive">
            <table className="table table-sm mb-0 align-middle">
              <thead>
                <tr><th>Client</th><th>Practitioner</th><th>Appointment Time</th><th>Status</th><th>Wait Time</th><th /></tr>
              </thead>
              <tbody>
                {appointments.map((a) => {
                  const flags = clientFlagsSummary(a);
                  return (
                    <tr key={a.id}>
                      <td>
                        {a.clientName}
                        {flags && <i className="bi bi-exclamation-triangle-fill text-warning ms-1" title={flags} aria-label={flags} />}
                      </td>
                      <td>{a.practitionerName}</td>
                      <td>{a.timeRangeFormatted}</td>
                      <td><span className={`badge ${statusClass(a.status)}`}>{a.statusName}</span></td>
                      <td className="imed-num">{waitTime(a, now)}</td>
                      <td className="text-end text-nowrap">
                        {a.status === AppointmentStatus.Arrived && (
                          <button className="btn btn-sm btn-outline-primary me-1" onClick={() => advance(a, AppointmentStatus.InProgress)}>Start</button>
                        )}
                        {a.status === AppointmentStatus.InProgress && (
                          <button className="btn btn-sm btn-outline-success me-1" onClick={() => advance(a, AppointmentStatus.Completed)}>Complete</button>
                        )}
                        <button className="btn btn-sm btn-outline-secondary me-1" onClick={() => goToProgressNote(a)}>Progress Note</button>
                        <button className="btn btn-sm btn-link" onClick={() => setSelected(a.id)}>Details</button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <AppointmentDetailsModal appointmentId={selected} onClose={() => setSelected(null)} onChanged={load} />
    </>
  );
}
