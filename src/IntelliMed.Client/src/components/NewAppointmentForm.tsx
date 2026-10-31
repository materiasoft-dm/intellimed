import { useEffect, useRef, useState } from 'react';
import { AppointmentApi, AppointmentTypeApi, PractitionerApi } from '@/api/appointments';
import { ClientApi } from '@/api/clients';
import {
  defaultCreateAppointmentDto, RecurrenceFrequency, type AppointmentTypeSettingDto, type ClientDto, type CreateAppointmentDto,
  type PractitionerDto,
} from '@/api/types.gen';
import { ClientPicker } from '@/components/ClientPicker';
import { ClientSafetyBanner } from '@/components/ClientSafetyBanner';
import { DateInput, EnumOptions, NumberInput, NumberSelect, TextArea, TimeInput } from '@/components/Inputs';
import { fromDateInput, minutesToSpan, spanMinutes, todayIso, toDate } from '@/lib/dates';
import { useMountEffect } from '@/lib/hooks';

/** Where a new booking starts: a calendar slot (date/time/duration, maybe a practitioner) or a client record. */
export interface AppointmentPrefill {
  date?: string | null;
  /** HH:mm */
  time?: string | null;
  /** minutes */
  duration?: number | null;
  practitionerId?: number | null;
  clientId?: number | null;
}

export type AppointmentSaved = { id: number } | { seriesIds: number[] };

/**
 * The booking form — client, practitioner, type, slot, notes, bulk bill and an optional repeat — with the
 * availability / working-hours check. Used by the New Appointment page and by the calendar's
 * right-click "Add Appointment" dialog.
 */
export function NewAppointmentForm({ prefill = {}, onSaved, onCancel }: {
  prefill?: AppointmentPrefill;
  onSaved: (saved: AppointmentSaved) => void;
  onCancel: () => void;
}) {
  const [model, setModel] = useState<CreateAppointmentDto>(() => ({
    ...defaultCreateAppointmentDto(), appointmentDate: todayIso(), startTime: '09:00:00', endTime: '09:15:00',
  }));
  const [client, setClient] = useState<ClientDto | null>(null);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [practitioners, setPractitioners] = useState<PractitionerDto[]>([]);
  const [typeSettings, setTypeSettings] = useState<AppointmentTypeSettingDto[]>([]);
  const [repeat, setRepeat] = useState(false);
  const [frequency, setFrequency] = useState<RecurrenceFrequency>(RecurrenceFrequency.Weekly);
  const [occurrences, setOccurrences] = useState<number | null>(4);
  const [untilDate, setUntilDate] = useState<string | null>(null);
  const [slot, setSlot] = useState<{ checked: boolean; available: boolean; outsideHours: boolean }>({ checked: false, available: true, outsideHours: false });
  const [saving, setSaving] = useState(false);
  const [status, setStatus] = useState<{ message: string; error: boolean } | null>(null);
  const checkSeq = useRef(0);

  useMountEffect(async (alive) => {
    const [p, t] = await Promise.all([PractitionerApi.active(), AppointmentTypeApi.list()]);
    if (!alive()) return;
    setPractitioners(p);
    setTypeSettings(t);

    // prefill from the calendar slot or a client record
    const next = { ...model };
    if (prefill.clientId) {
      next.clientId = prefill.clientId;
      const c = await ClientApi.getById(prefill.clientId);
      if (alive()) setClient(c);
    }
    if (prefill.practitionerId) next.practitionerId = prefill.practitionerId;
    if (prefill.date && toDate(prefill.date)) next.appointmentDate = fromDateInput(prefill.date.slice(0, 10))!;
    if (prefill.time && /^\d{1,2}:\d{2}/.test(prefill.time)) {
      const start = spanMinutes(prefill.time.length === 4 ? `0${prefill.time}` : prefill.time);
      const duration = prefill.duration && prefill.duration > 0 ? prefill.duration : 15;
      next.startTime = minutesToSpan(start);
      next.endTime = minutesToSpan(start + duration);
    }
    if (alive()) setModel(next);
  });

  // availability + working-hours check whenever the slot changes
  useEffect(() => {
    const mine = ++checkSeq.current;
    setSlot({ checked: false, available: true, outsideHours: false });
    if (!model.practitionerId || spanMinutes(model.endTime) <= spanMinutes(model.startTime)) return;
    (async () => {
      const available = await AppointmentApi.checkAvailability(model.practitionerId, model.appointmentDate, model.startTime, model.endTime);
      const schedule = await AppointmentApi.practitionerSchedule(model.practitionerId, model.appointmentDate);
      if (mine !== checkSeq.current) return;
      const outsideHours = !!schedule && (!schedule.isActive
        || spanMinutes(model.startTime) < spanMinutes(schedule.startTime) || spanMinutes(model.endTime) > spanMinutes(schedule.endTime));
      setSlot({ checked: true, available, outsideHours });
    })();
  }, [model.practitionerId, model.appointmentDate, model.startTime, model.endTime]);

  const onTypeSetting = (id: number | null) => {
    const setting = typeSettings.find((t) => t.id === id);
    setModel((m) => ({
      ...m,
      appointmentTypeSettingId: id,
      endTime: setting ? minutesToSpan(spanMinutes(m.startTime) + setting.defaultDurationMinutes) : m.endTime,
    }));
  };

  // moving the start keeps the same duration
  const onStart = (value: string | null) => {
    if (!value) return;
    setModel((m) => {
      let duration = spanMinutes(m.endTime) - spanMinutes(m.startTime);
      if (duration <= 0) duration = 15;
      return { ...m, startTime: value, endTime: minutesToSpan(spanMinutes(value) + duration) };
    });
  };

  const save = async () => {
    setSaving(true);
    setStatus(null);
    try {
      if (repeat) {
        const ids = await AppointmentApi.createSeries({ ...model, frequency, occurrences, untilDate });
        if (ids && ids.length > 0) { onSaved({ seriesIds: ids }); return; }
        setStatus({ message: 'Failed to create the appointment series.', error: true });
      } else {
        const id = await AppointmentApi.create(model);
        if (id != null) { onSaved({ id }); return; }
        setStatus({ message: 'Failed to create the appointment.', error: true });
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
    <div className="card mb-4">
      <div className="card-header"><strong>Booking Details</strong></div>
      <div className="card-body">
        <div className="mb-3">
          <label className="form-label small mb-0">Client</label>
          <div className="input-group">
            <input className="form-control" value={client?.fullName ?? ''} readOnly placeholder="Search for a client..." />
            <button type="button" className="btn btn-outline-secondary" onClick={() => setPickerOpen(true)}>Find</button>
          </div>
          {client && (
            <ClientSafetyBanner deceased={client.deceased} warnings={client.warnings} interpreterRequired={client.interpreterRequired}
                                interpreterLanguage={client.interpreterLanguage} age={client.age}
                                nextOfKinName={client.nextOfKinName} nextOfKinPhone={client.nextOfKinPhone} />
          )}
        </div>
        <div className="row">
          <div className="col-md-6 mb-3">
            <label className="form-label small mb-0">Practitioner</label>
            <NumberSelect className="form-select" nullable={false} value={model.practitionerId}
                          onChange={(v) => setModel({ ...model, practitionerId: v ?? 0 })}>
              <option value={0}>Select...</option>
              {practitioners.map((p) => <option key={p.id} value={p.id}>{p.fullName}</option>)}
            </NumberSelect>
          </div>
          <div className="col-md-6 mb-3">
            <label className="form-label small mb-0">Appointment Type</label>
            <NumberSelect className="form-select" value={model.appointmentTypeSettingId} onChange={onTypeSetting}>
              <option value="">Custom duration</option>
              {typeSettings.map((t) => <option key={t.id} value={t.id}>{t.name} ({t.defaultDurationMinutes} min)</option>)}
            </NumberSelect>
          </div>
        </div>
        <div className="row">
          <div className="col-md-4 mb-3">
            <label className="form-label small mb-0">Date</label>
            <DateInput className="form-control" value={model.appointmentDate} onChange={(v) => v && setModel({ ...model, appointmentDate: v })} />
          </div>
          <div className="col-md-4 mb-3">
            <label className="form-label small mb-0">Start Time</label>
            <TimeInput className="form-control" value={model.startTime} onChange={onStart} />
          </div>
          <div className="col-md-4 mb-3">
            <label className="form-label small mb-0">End Time</label>
            <TimeInput className="form-control" value={model.endTime} onChange={(v) => v && setModel({ ...model, endTime: v })} />
          </div>
        </div>
        {slot.checked && !slot.available && (
          <div className="alert alert-danger py-2 small"><i className="bi bi-exclamation-triangle me-1" /> This overlaps with another appointment for this practitioner.</div>
        )}
        {slot.outsideHours && (
          <div className="alert alert-warning py-2 small">
            <i className="bi bi-info-circle me-1" /> This falls outside the practitioner's usual working hours (set in their Profile Settings). You can still book it.
          </div>
        )}
        <div className="mb-3">
          <label className="form-label small mb-0">Notes</label>
          <TextArea className="form-control" rows={2} value={model.notes} onChange={(v) => setModel({ ...model, notes: v })} />
        </div>
        <div className="form-check">
          <input className="form-check-input" type="checkbox" id="chkBulkBill" checked={model.isBulkBill}
                 onChange={(e) => setModel({ ...model, isBulkBill: e.target.checked })} />
          <label className="form-check-label" htmlFor="chkBulkBill">Bulk Bill</label>
        </div>
      </div>
    </div>

    <div className="card mb-4">
      <div className="card-header">
        <div className="form-check">
          <input className="form-check-input" type="checkbox" id="chkRepeat" checked={repeat} onChange={(e) => setRepeat(e.target.checked)} />
          <label className="form-check-label" htmlFor="chkRepeat"><strong>Repeat this appointment</strong></label>
        </div>
      </div>
      {repeat && (
        <div className="card-body">
          <div className="row">
            <div className="col-md-4 mb-2">
              <label className="form-label small mb-0">Frequency</label>
              <NumberSelect enumType={RecurrenceFrequency} className="form-select form-select-sm" nullable={false} value={frequency}
                            onChange={(v) => setFrequency((v ?? 0) as RecurrenceFrequency)}>
                <EnumOptions enumType={RecurrenceFrequency} />
              </NumberSelect>
            </div>
            <div className="col-md-4 mb-2">
              <label className="form-label small mb-0">Ends after (occurrences)</label>
              <NumberInput min={1} max={104} nullable className="form-control form-control-sm" value={occurrences}
                           onChange={(v) => { setOccurrences(v); setUntilDate(null); }} />
            </div>
            <div className="col-md-4 mb-2">
              <label className="form-label small mb-0">Or ends by date</label>
              <DateInput className="form-control form-control-sm" value={untilDate} onChange={(v) => { setUntilDate(v); setOccurrences(null); }} />
            </div>
          </div>
          <div className="small text-muted">Set exactly one of "occurrences" or "ends by date".</div>
        </div>
      )}
    </div>

    {status && <div className={`alert ${status.error ? 'alert-danger' : 'alert-success'} py-2 small`}>{status.message}</div>}
    <div className="d-flex justify-content-end gap-2">
      <button type="button" className="btn btn-secondary" onClick={onCancel}>Cancel</button>
      <button type="button" className="btn btn-primary" onClick={save}
              disabled={saving || model.clientId === 0 || model.practitionerId === 0 || (client?.deceased ?? false)}>
        {saving && <span className="spinner-border spinner-border-sm me-1" />} Save
      </button>
    </div>

    {pickerOpen && (
      <ClientPicker onClose={() => setPickerOpen(false)}
                    onSelect={(c) => { setModel((m) => ({ ...m, clientId: c.id })); setClient(c); setPickerOpen(false); }} />
    )}
    </>
  );
}
