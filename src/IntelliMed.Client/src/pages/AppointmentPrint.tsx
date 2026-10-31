import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router';
import { AppointmentApi, PractitionerApi } from '@/api/appointments';
import type { AppointmentDto, PractitionerDto } from '@/api/types.gen';
import { DateInput } from '@/components/Inputs';
import { EmptyState, LoadingState } from '@/components/States';
import { formatDate, fromDateInput, todayIso, toDate } from '@/lib/dates';
import { usePageTitle } from '@/lib/hooks';

/** Printable day list: /appointments/print?date=yyyy-MM-dd&practitionerId=n */
export default function AppointmentPrint() {
  usePageTitle('Day List - IntelliMed');
  const [params] = useSearchParams();
  const initialDate = params.get('date');
  const [date, setDate] = useState<string>(() => (initialDate && toDate(initialDate) ? fromDateInput(initialDate.slice(0, 10))! : todayIso()));
  const [practitionerId, setPractitionerId] = useState<number | null>(() => {
    const p = params.get('practitionerId');
    return p ? Number(p) : null;
  });
  const [practitioners, setPractitioners] = useState<PractitionerDto[]>([]);
  const [appointments, setAppointments] = useState<AppointmentDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => { PractitionerApi.active().then(setPractitioners); }, []);

  useEffect(() => {
    let live = true;
    setLoading(true);
    AppointmentApi.search({ fromDate: date, toDate: date, practitionerId, pageSize: 500 })
      .then((r) => { if (live) setAppointments(r.items); })
      .finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [date, practitionerId]);

  const sorted = [...appointments].sort((a, b) => a.startTime.localeCompare(b.startTime));

  return (
    <div className="imed-print-page">
      <div className="no-print p-3 d-flex flex-wrap gap-2 imed-print-toolbar">
        <DateInput className="form-control" style={{ maxWidth: 200 }} value={date} onChange={(v) => v && setDate(v)} aria-label="Date" />
        <select className="form-select" style={{ maxWidth: 260 }} value={practitionerId ?? ''} aria-label="Practitioner"
                onChange={(e) => setPractitionerId(e.target.value ? Number(e.target.value) : null)}>
          <option value="">All Practitioners</option>
          {practitioners.map((p) => <option key={p.id} value={p.id}>{p.fullName}</option>)}
        </select>
        <button className="btn btn-primary" onClick={() => window.print()}><i className="bi bi-printer" /> Print</button>
      </div>
      <div className="p-4">
        <h4>Day List — {formatDate(date, 'dddd, d MMMM yyyy')}</h4>
        {practitionerId != null && <p className="text-muted">{practitioners.find((p) => p.id === practitionerId)?.fullName}</p>}
        {loading ? (
          <LoadingState text="Loading appointments..." />
        ) : sorted.length === 0 ? (
          <EmptyState message="No appointments scheduled." icon="bi-calendar-x" />
        ) : (
          <table className="table table-bordered table-sm">
            <thead><tr><th>Time</th><th>Client</th><th>Practitioner</th><th>Type</th><th>Status</th><th>Notes</th></tr></thead>
            <tbody>
              {sorted.map((a) => (
                <tr key={a.id}>
                  <td className="text-nowrap">{a.timeRangeFormatted}</td>
                  <td>{a.clientName}</td>
                  <td>{a.practitionerName}</td>
                  <td>{a.typeName}</td>
                  <td>{a.statusName}</td>
                  <td>{a.notes}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
