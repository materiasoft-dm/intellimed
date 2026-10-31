import { useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { PrescriptionApi } from '@/api/clinical';
import { defaultIssuePrescriptionDto, PrescriptionStatus, type PrescriptionDto } from '@/api/types.gen';
import { EmptyState, LoadingState } from '@/components/States';
import { toUtcDate } from '@/lib/dates';
import { useTabData } from './shared';

export function PrescriptionsTab({ clientId }: { clientId: number }) {
  const navigate = useNavigate();
  const [includeInactive, setIncludeInactive] = useState(false);
  const inactiveRef = useRef(false);
  const { data: prescriptions, loading, reload } = useTabData(() => PrescriptionApi.listForClient(clientId, inactiveRef.current), [] as PrescriptionDto[]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<number | null>(null);

  const toggleInactive = (v: boolean) => {
    setIncludeInactive(v);
    inactiveRef.current = v;
    reload();
  };

  const cease = async (id: number) => {
    setError(null);
    setBusy(id);
    const ok = await PrescriptionApi.cease(clientId, id);
    setBusy(null);
    if (!ok) { setError('Cease failed — check your permissions.'); return; }
    reload();
  };

  /** First issue of a not-yet-issued script, under the practitioner it was written by. */
  const issueAndPrint = async (p: PrescriptionDto) => {
    setError(null);
    setBusy(p.id);
    const issueId = await PrescriptionApi.issue(clientId, p.id, { ...defaultIssuePrescriptionDto(), issuedByPractitionerId: p.prescribedByPractitionerId });
    setBusy(null);
    if (issueId == null) { setError('Issue failed — check your permissions.'); return; }
    navigate(`/prescriptions/print/${issueId}`);
  };

  const reprint = async (prescriptionId: number) => {
    setError(null);
    const issues = await PrescriptionApi.issues(clientId, prescriptionId);
    const latest = [...issues].sort((a, b) => (toUtcDate(b.issuedAt)?.getTime() ?? 0) - (toUtcDate(a.issuedAt)?.getTime() ?? 0))[0];
    if (!latest) { setError('No issue history found for this prescription.'); return; }
    navigate(`/prescriptions/print/${latest.id}`);
  };

  return (
    <div>
      <div className="d-flex justify-content-between mb-1">
        <div className="form-check">
          <input type="checkbox" className="form-check-input" id="chkInclInactiveRx" checked={includeInactive} onChange={(e) => toggleInactive(e.target.checked)} />
          <label className="form-check-label small" htmlFor="chkInclInactiveRx">Include ceased/cancelled</label>
        </div>
        <Link to={`/clients/${clientId}/prescriptions/new`} className="btn btn-sm btn-outline-primary">+ New</Link>
      </div>
      {error && <div className="small text-danger mb-1">{error}</div>}
      {loading ? (
        <LoadingState text="Loading prescriptions..." />
      ) : prescriptions.length === 0 ? (
        <EmptyState message={includeInactive ? 'No prescriptions recorded.' : 'No active prescriptions. Tick "Include ceased/cancelled" to include past ones.'} icon="bi-capsule" />
      ) : (
        <div className="table-responsive">
          <table className="table table-sm table-hover">
            <thead><tr><th>Medicine</th><th>Dose</th><th>Repeats</th><th>Script No</th><th>Status</th><th /></tr></thead>
            <tbody>
              {prescriptions.map((p) => (
                <tr key={p.id} className={p.status !== PrescriptionStatus.Active ? 'text-muted' : ''}>
                  <td>{p.medicineName}</td>
                  <td>{[p.dose, p.frequency].filter(Boolean).join(' ')}</td>
                  <td>{p.repeats}</td>
                  <td>{p.scriptNumber}</td>
                  <td>{PrescriptionStatus[p.status]}</td>
                  <td className="text-end text-nowrap">
                    {p.isIssued ? (
                      <button type="button" className="btn btn-sm btn-outline-secondary me-1" onClick={() => reprint(p.id)}>Reprint</button>
                    ) : p.status === PrescriptionStatus.Active && (
                      <button type="button" className="btn btn-sm btn-outline-primary me-1" disabled={busy === p.id} onClick={() => issueAndPrint(p)}>Issue &amp; Print</button>
                    )}
                    {p.status === PrescriptionStatus.Active && (
                      <button type="button" className="btn btn-sm btn-outline-warning" disabled={busy === p.id} onClick={() => cease(p.id)}>Cease</button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
