import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router';
import { AppointmentApi } from '@/api/appointments';
import { AppointmentStatus, type AppointmentDto, type UpdateAppointmentDto } from '@/api/types.gen';
import { DateInput, TextArea, TimeInput } from './Inputs';
import { Modal } from './Modal';
import { LoadingState } from './States';
import { appointmentStatusClass } from './StatusBadges';
import { formatUtc } from '@/lib/dates';

const S = AppointmentStatus;

/**
 * View/edit one appointment and move it through the visit (Confirm → Arrived → Start → Complete, or
 * Cancel / No-show). `onChanged` fires after a save or status change (the appointment still exists);
 * `onClosed` when the flow ends and it may be gone (a series was cancelled).
 */
export function AppointmentDetailsPanel({ appointmentId, onChanged, onClosed }: {
  appointmentId: number;
  onChanged?: () => void;
  onClosed?: () => void;
}) {
  const navigate = useNavigate();
  const [appointment, setAppointment] = useState<AppointmentDto | null>(null);
  const [edit, setEdit] = useState<UpdateAppointmentDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [busyStatus, setBusyStatus] = useState<AppointmentStatus | null>(null);
  const [status, setStatus] = useState<{ message: string; error: boolean } | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    const a = await AppointmentApi.getById(appointmentId);
    setAppointment(a);
    if (a) {
      setEdit({
        clientId: a.clientId, practitionerId: a.practitionerId, appointmentDate: a.appointmentDate, startTime: a.startTime,
        endTime: a.endTime, status: a.status, type: a.type, appointmentTypeSettingId: a.appointmentTypeSettingId, notes: a.notes,
        isBulkBill: a.isBulkBill, fee: a.fee,
      } as UpdateAppointmentDto);
    }
    setLoading(false);
  }, [appointmentId]);

  useEffect(() => { load(); }, [load]);

  const save = async () => {
    if (!edit) return;
    setSaving(true);
    const ok = await AppointmentApi.update(appointmentId, edit);
    setSaving(false);
    if (ok) {
      setStatus({ message: 'Saved.', error: false });
      await load();
      onChanged?.();
    } else {
      setStatus({ message: 'Failed to save changes.', error: true });
    }
  };

  const setVisitStatus = async (next: AppointmentStatus) => {
    setBusyStatus(next);
    const ok = await AppointmentApi.updateStatus(appointmentId, next);
    setBusyStatus(null);
    if (ok) {
      setStatus({ message: `Status updated to ${AppointmentStatus[next]}.`, error: false });
      await load();
      onChanged?.();
    } else {
      setStatus({ message: 'Failed to update status.', error: true });
    }
  };

  const cancelSeries = async () => {
    if (!appointment?.recurrenceSeriesId) return;
    const count = await AppointmentApi.removeSeries(appointment.recurrenceSeriesId, appointment.appointmentDate);
    setStatus({ message: `Cancelled ${count} upcoming appointment(s) in the series.`, error: false });
    onClosed?.();
  };

  if (loading && !appointment) return <LoadingState text="Loading appointment..." />;
  if (!appointment || !edit) return <div className="alert alert-danger">Appointment not found.</div>;

  const st = appointment.status;
  const statusButton = (label: string, next: AppointmentStatus, cls: string, disabled: boolean) => (
    <button type="button" className={`btn btn-sm ${cls}`} disabled={disabled || busyStatus !== null} onClick={() => setVisitStatus(next)}>
      {busyStatus === next && <span className="spinner-border spinner-border-sm me-1" />}{label}
    </button>
  );

  return (
    <>
      {status && <div className="mb-2"><span className={`small ${status.error ? 'text-danger' : 'text-success'}`}>{status.message}</span></div>}
      <div className="row g-2 mb-3">
        <div className="col-lg-6">
          <div className="card h-100">
            <div className="card-header py-1"><strong>Details</strong></div>
            <div className="card-body py-2">
              <div className="row g-2">
                <div className="col-6">
                  <label className="form-label small mb-0">Client</label>
                  <div>{appointment.clientName}</div>
                </div>
                <div className="col-6">
                  <label className="form-label small mb-0">Status</label>
                  <div><span className={`badge ${appointmentStatusClass(st)}`}>{appointment.statusName}</span></div>
                </div>
                <div className="col-6">
                  <label className="form-label small mb-0">Practitioner</label>
                  <div>{appointment.practitionerName}</div>
                </div>
                <div className="col-6">
                  <label className="form-label small mb-0">Date</label>
                  <DateInput className="form-control form-control-sm" value={edit.appointmentDate}
                             onChange={(v) => v && setEdit({ ...edit, appointmentDate: v })} />
                </div>
                <div className="col-3">
                  <label className="form-label small mb-0">Start</label>
                  <TimeInput className="form-control form-control-sm" value={edit.startTime} onChange={(v) => v && setEdit({ ...edit, startTime: v })} />
                </div>
                <div className="col-3">
                  <label className="form-label small mb-0">End</label>
                  <TimeInput className="form-control form-control-sm" value={edit.endTime} onChange={(v) => v && setEdit({ ...edit, endTime: v })} />
                </div>
                <div className="col-12">
                  <label className="form-label small mb-0">Notes</label>
                  <TextArea className="form-control form-control-sm" rows={2} value={edit.notes} onChange={(v) => setEdit({ ...edit, notes: v })} />
                </div>
                <div className="col-12">
                  <div className="form-check">
                    <input className="form-check-input" type="checkbox" id={`chkEditBulkBill${appointmentId}`} checked={edit.isBulkBill}
                           onChange={(e) => setEdit({ ...edit, isBulkBill: e.target.checked })} />
                    <label className="form-check-label small" htmlFor={`chkEditBulkBill${appointmentId}`}>Bulk Bill</label>
                  </div>
                </div>
              </div>
              <div className="d-flex justify-content-end mt-2">
                <button type="button" className="btn btn-sm btn-primary" onClick={save} disabled={saving}>
                  {saving && <span className="spinner-border spinner-border-sm me-1" />} Save Changes
                </button>
              </div>
            </div>
          </div>
        </div>
        <div className="col-lg-6">
          <div className="card h-100">
            <div className="card-header py-1"><strong>Visit Status</strong></div>
            <div className="card-body py-2">
              <div className="d-flex flex-wrap gap-2 mb-2">
                {statusButton('Confirm', S.Confirmed, 'btn-outline-info', st !== S.Scheduled)}
                {statusButton('Mark Arrived', S.Arrived, 'btn-outline-warning', [S.Arrived, S.InProgress, S.Completed, S.Cancelled, S.NoShow].includes(st))}
                {statusButton('Start', S.InProgress, 'btn-outline-primary', st !== S.Arrived)}
                {statusButton('Complete', S.Completed, 'btn-outline-success', st !== S.InProgress)}
                {statusButton('Cancel', S.Cancelled, 'btn-outline-danger', [S.Completed, S.Cancelled].includes(st))}
                {statusButton('No-show', S.NoShow, 'btn-outline-dark', [S.Completed, S.Cancelled, S.NoShow].includes(st))}
              </div>
              <div className="row g-1 small text-muted">
                {appointment.arrivedAt && <><div className="col-6">Arrived</div><div className="col-6">{formatUtc(appointment.arrivedAt, 'h:mm tt')}</div></>}
                {appointment.seenAt && <><div className="col-6">Seen</div><div className="col-6">{formatUtc(appointment.seenAt, 'h:mm tt')}</div></>}
                {appointment.completedAt && <><div className="col-6">Completed</div><div className="col-6">{formatUtc(appointment.completedAt, 'h:mm tt')}</div></>}
              </div>
              {st === S.Completed && (
                <div className="mt-2">
                  <button type="button" className="btn btn-sm btn-success"
                          onClick={() => navigate(`/invoices/new?clientId=${appointment.clientId}&appointmentId=${appointmentId}`)}>
                    <i className="bi bi-receipt" /> Start Invoice
                  </button>
                </div>
              )}
              {appointment.recurrenceSeriesId && (
                <div className="mt-2 small text-muted">
                  <i className="bi bi-arrow-repeat" /> Part of a recurring series.{' '}
                  <button type="button" className="btn btn-link btn-sm p-0" onClick={cancelSeries}>Cancel this and following</button>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

/** The details panel in a modal (waiting room, appointment list, calendar). */
export function AppointmentDetailsModal({ appointmentId, onClose, onChanged }: {
  appointmentId: number | null;
  onClose: () => void;
  onChanged?: () => void;
}) {
  if (appointmentId == null) return null;
  return (
    <Modal title="Appointment Details" size="lg" scrollable={false} onClose={onClose}
           footer={<button type="button" className="btn btn-sm btn-secondary" onClick={onClose}>Close</button>}>
      <AppointmentDetailsPanel appointmentId={appointmentId} onChanged={onChanged} onClosed={onClose} />
    </Modal>
  );
}
