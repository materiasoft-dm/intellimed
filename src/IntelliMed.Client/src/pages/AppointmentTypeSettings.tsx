import { useState } from 'react';
import { AppointmentTypeApi } from '@/api/appointments';
import { defaultCreateAppointmentTypeSettingDto, type AppointmentTypeSettingDto, type UpdateAppointmentTypeSettingDto } from '@/api/types.gen';
import { Modal } from '@/components/Modal';
import { EmptyState, LoadingState } from '@/components/States';
import { useToast } from '@/components/Toast';
import { useMountEffect, usePageTitle } from '@/lib/hooks';

export default function AppointmentTypeSettings() {
  usePageTitle('Appointment Types - IntelliMed');
  const toast = useToast();
  const [types, setTypes] = useState<AppointmentTypeSettingDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<UpdateAppointmentTypeSettingDto | null>(null);
  const [saving, setSaving] = useState(false);

  const load = async () => {
    try {
      setTypes(await AppointmentTypeApi.list());
    } finally {
      setLoading(false);
    }
  };
  useMountEffect(load);

  const openNew = () => {
    setEditingId(null);
    setForm({ name: '', defaultDurationMinutes: 15, colorHex: '#4272d7', isActive: true, sortOrder: 0 } as UpdateAppointmentTypeSettingDto);
  };

  const openEdit = (t: AppointmentTypeSettingDto) => {
    setEditingId(t.id);
    setForm({ name: t.name, defaultDurationMinutes: t.defaultDurationMinutes, colorHex: t.colorHex, isActive: t.isActive, sortOrder: t.sortOrder } as UpdateAppointmentTypeSettingDto);
  };

  const save = async () => {
    if (!form) return;
    setSaving(true);
    const ok = editingId != null
      ? await AppointmentTypeApi.update(editingId, form)
      : (await AppointmentTypeApi.create({ ...defaultCreateAppointmentTypeSettingDto(), name: form.name, defaultDurationMinutes: form.defaultDurationMinutes, colorHex: form.colorHex, sortOrder: form.sortOrder })) != null;
    setSaving(false);
    if (!ok) toast.error('Failed to save appointment type.');
    setForm(null);
    await load();
  };

  const archive = async (t: AppointmentTypeSettingDto) => {
    if (!(await AppointmentTypeApi.archive(t.id))) toast.error(`Failed to archive '${t.name}'.`);
    await load();
  };

  return (
    <>
      <h4 className="mb-4">Appointment Types</h4>
      <p className="text-muted small">Configure the appointment types staff can pick when booking, each with a default duration used to prefill the end time.</p>

      <div className="card">
        <div className="card-header d-flex justify-content-between align-items-center py-2">
          <strong>Types</strong>
          <button className="btn btn-sm btn-primary" onClick={openNew}><i className="bi bi-plus-lg" /> New Type</button>
        </div>
        <div className="card-body py-2">
          {loading ? (
            <LoadingState text="Loading appointment types..." />
          ) : types.length === 0 ? (
            <EmptyState message="No appointment types configured yet." icon="bi-calendar-event" />
          ) : (
            <table className="table table-sm align-middle mb-0">
              <thead>
                <tr><th /><th>Name</th><th>Default Duration</th><th /></tr>
              </thead>
              <tbody>
                {types.map((t) => (
                  <tr key={t.id}>
                    <td style={{ width: 28 }}><span className="imed-color-swatch" style={{ background: t.colorHex ?? undefined }} /></td>
                    <td><a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); openEdit(t); }}>{t.name}</a></td>
                    <td>{t.defaultDurationMinutes} min</td>
                    <td className="text-end"><button className="btn btn-sm btn-outline-danger" onClick={() => archive(t)}>Archive</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {form && (
        <Modal title={`${editingId != null ? 'Edit' : 'New'} Appointment Type`} onClose={() => setForm(null)} scrollable={false}
               footer={<>
                 <button type="button" className="btn btn-sm btn-secondary" onClick={() => setForm(null)}>Cancel</button>
                 <button type="button" className="btn btn-sm btn-primary" disabled={saving} onClick={save}>Save</button>
               </>}>
          <div className="mb-2">
            <label className="form-label small mb-0">Name</label>
            <input className="form-control form-control-sm" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
          </div>
          <div className="mb-2">
            <label className="form-label small mb-0">Default Duration (minutes)</label>
            <input type="number" min={1} className="form-control form-control-sm" value={form.defaultDurationMinutes}
                   onChange={(e) => setForm({ ...form, defaultDurationMinutes: Number(e.target.value) || 0 })} />
          </div>
          <div className="mb-2">
            <label className="form-label small mb-0">Color</label>
            <input type="color" className="form-control form-control-sm form-control-color" value={form.colorHex ?? '#4272d7'}
                   onChange={(e) => setForm({ ...form, colorHex: e.target.value })} />
          </div>
          {editingId != null && (
            <div className="form-check">
              <input className="form-check-input" type="checkbox" id="chkActive" checked={form.isActive}
                     onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />
              <label className="form-check-label small" htmlFor="chkActive">Active</label>
            </div>
          )}
        </Modal>
      )}
    </>
  );
}
