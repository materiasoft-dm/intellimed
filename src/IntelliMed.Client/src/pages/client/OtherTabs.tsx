import { useState } from 'react';
import { ClientOccupationApi, ClientUdfValueApi, LookupApi } from '@/api/clients';
import { defaultCreateClientOccupationDto, defaultCreateClientUserDefinedFieldValueDto, defaultUpdateClientUserDefinedFieldValueDto, type ClientOccupationDto, type ClientUserDefinedFieldValueDto, type CreateClientOccupationDto, type UpdateClientDto, type UserDefinedFieldTypeDto } from '@/api/types.gen';
import { OrganisationPicker } from '@/components/ContactPickers';
import { NumberInput, NumberSelect, TextArea, TextInput } from '@/components/Inputs';
import { Modal } from '@/components/Modal';
import { EmptyState, LoadingState } from '@/components/States';
import { formatDate } from '@/lib/dates';
import { noneMessage, TabToolbar, useTabData } from './shared';

const hazardSummary = (o: ClientOccupationDto) =>
  [o.hasAsbestos && 'Asbestos', o.hasDust && 'Dust', o.hasRadiation && 'Radiation', o.hasAnimals && 'Animals'].filter(Boolean).join(', ');

export function OccupationsTab({ clientId }: { clientId: number }) {
  const { data: occupations, loading, reload } = useTabData(() => ClientOccupationApi.list(clientId), [] as ClientOccupationDto[]);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [form, setForm] = useState<{ editingId: number | null; dto: CreateClientOccupationDto; employerName: string | null } | null>(null);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const visible = occupations.filter((o) => includeArchived || !o.isArchived);

  const openNew = () => setForm({ editingId: null, employerName: null, dto: { ...defaultCreateClientOccupationDto(), clientId } });
  const openEdit = (o: ClientOccupationDto) => setForm({
    editingId: o.id, employerName: o.employerName,
    dto: { clientId: o.clientId, occupation: o.occupation, employerClientId: o.employerClientId, startedYear: o.startedYear, stoppedYear: o.stoppedYear, hasAsbestos: o.hasAsbestos, hasDust: o.hasDust, hasRadiation: o.hasRadiation, hasAnimals: o.hasAnimals, comment: o.comment },
  });
  const set = (patch: Partial<CreateClientOccupationDto>) => setForm((f) => (f ? { ...f, dto: { ...f.dto, ...patch } } : f));

  const save = async () => {
    if (!form) return;
    setSaving(true);
    const { clientId: _c, ...rest } = form.dto;
    void _c;
    const ok = form.editingId != null
      ? await ClientOccupationApi.update(clientId, form.editingId, { ...rest, isArchived: false })
      : await ClientOccupationApi.create(form.dto);
    setSaving(false);
    if (ok) { setForm(null); await reload(); }
  };

  const hazard = (key: 'hasAsbestos' | 'hasDust' | 'hasRadiation' | 'hasAnimals', id: string, label: string) => form && (
    <div className="form-check">
      <input type="checkbox" className="form-check-input" id={id} checked={form.dto[key]} onChange={(e) => set({ [key]: e.target.checked })} />
      <label className="form-check-label small" htmlFor={id}>{label}</label>
    </div>
  );

  return (
    <div>
      <TabToolbar archivedId="chkInclArchivedOcc" includeArchived={includeArchived} onIncludeArchived={setIncludeArchived} onNew={openNew} />
      {loading && occupations.length === 0 ? <LoadingState text="Loading occupations..." /> : visible.length === 0 ? (
        <EmptyState message={noneMessage('occupations', occupations.length, includeArchived)} icon="bi-building" />
      ) : (
        <div className="table-responsive">
          <table className="table table-sm table-hover">
            <thead><tr><th>Occupation</th><th>Employer</th><th>Started</th><th>Ended</th><th>Hazards</th><th>Comment</th><th /></tr></thead>
            <tbody>
              {visible.map((o) => (
                <tr key={o.id} className={o.isArchived ? 'text-muted' : ''}>
                  <td><a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); openEdit(o); }}>{o.occupation || '(unnamed)'}</a></td>
                  <td>{o.employerName}</td>
                  <td>{o.startedYear}</td>
                  <td>{o.stoppedYear}</td>
                  <td>{hazardSummary(o)}</td>
                  <td>{o.comment}</td>
                  <td className="text-end text-nowrap">
                    {!o.isArchived && <button type="button" className="btn btn-sm btn-outline-danger" onClick={async () => { await ClientOccupationApi.archive(clientId, o.id); await reload(); }}>Archive</button>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {form && (
        <Modal title={form.editingId != null ? 'Edit Occupation' : 'New Occupation'} scrollable={false} onClose={() => setForm(null)}
               footer={<>
                 <button type="button" className="btn btn-sm btn-secondary" onClick={() => setForm(null)}>Cancel</button>
                 <button type="button" className="btn btn-sm btn-primary" disabled={saving} onClick={save}>Save</button>
               </>}>
          <div className="row g-2">
            <div className="col-6"><label className="form-label small mb-0">Occupation</label><TextInput className="form-control form-control-sm" value={form.dto.occupation} onChange={(v) => set({ occupation: v ?? '' })} /></div>
            <div className="col-6">
              <label className="form-label small mb-0">Employer</label>
              <div className="input-group input-group-sm">
                <input className="form-control form-control-sm" value={form.employerName ?? ''} readOnly placeholder="No employer" />
                <button type="button" className="btn btn-outline-secondary" onClick={() => setPickerOpen(true)}>Find</button>
                {form.dto.employerClientId != null && (
                  <button type="button" className="btn btn-outline-secondary" title="Clear employer" onClick={() => setForm({ ...form, dto: { ...form.dto, employerClientId: null }, employerName: null })}>x</button>
                )}
              </div>
            </div>
            <div className="col-6"><label className="form-label small mb-0">Started (year)</label><NumberInput nullable className="form-control form-control-sm" value={form.dto.startedYear} onChange={(v) => set({ startedYear: v })} /></div>
            <div className="col-6"><label className="form-label small mb-0">Ended (year)</label><NumberInput nullable className="form-control form-control-sm" value={form.dto.stoppedYear} onChange={(v) => set({ stoppedYear: v })} /></div>
            <div className="col-12 d-flex flex-wrap gap-3">
              {hazard('hasAsbestos', 'chkAsbestos', 'Asbestos')}
              {hazard('hasDust', 'chkDust', 'Dust')}
              {hazard('hasRadiation', 'chkRadiation', 'Radiation')}
              {hazard('hasAnimals', 'chkAnimals', 'Animals')}
            </div>
            <div className="col-12"><label className="form-label small mb-0">Comment</label><TextArea className="form-control form-control-sm" rows={2} value={form.dto.comment} onChange={(v) => set({ comment: v })} /></div>
          </div>
        </Modal>
      )}
      {form && pickerOpen && (
        <OrganisationPicker title="Find Employer" onClose={() => setPickerOpen(false)}
                            onSelect={(o) => { setForm({ ...form, dto: { ...form.dto, employerClientId: o.id }, employerName: o.name }); setPickerOpen(false); }} />
      )}
    </div>
  );
}

/** IHI details. The IHI number is part of the client record and is saved with Details' Save, as before. */
export function EHealthTab({ model, onChange, onUnderConstruction }: {
  model: UpdateClientDto;
  onChange: (patch: Partial<UpdateClientDto>) => void;
  onUnderConstruction: (title: string) => void;
}) {
  return (
    <div className="row g-2">
      <div className="col-4"><label className="form-label small mb-0">IHI Number</label><TextInput className="form-control form-control-sm" maxLength={16} value={model.ihiNumber} onChange={(v) => onChange({ ihiNumber: v })} /></div>
      <div className="col-4"><label className="form-label small mb-0">IHI Status</label><input className="form-control form-control-sm" value={model.ihiNumberStatus ?? ''} disabled /></div>
      <div className="col-4"><label className="form-label small mb-0">Record Status</label><input className="form-control form-control-sm" value={model.ihiRecordStatus ?? ''} disabled /></div>
      <div className="col-4"><label className="form-label small mb-0">Assigned Date</label><input className="form-control form-control-sm" value={formatDate(model.ihiAssignedDate, 'd/M/yyyy')} disabled /></div>
      <div className="col-4"><label className="form-label small mb-0">Unresolved Date</label><input className="form-control form-control-sm" value={formatDate(model.ihiUnresolvedDate, 'd/M/yyyy')} disabled /></div>
      <div className="col-4 d-flex align-items-end gap-1">
        <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => onUnderConstruction('IHI Lookup / Validate')}>Lookup</button>
        <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => onUnderConstruction('IHI History')}>History</button>
      </div>
      <div className="col-12"><div className="form-text">The IHI number is saved with the client record — use Save on the Details tab.</div></div>
    </div>
  );
}

export function UdfTab({ clientId }: { clientId: number }) {
  const { data, loading, reload } = useTabData(async () => {
    const [values, definitions] = await Promise.all([ClientUdfValueApi.list(clientId), LookupApi.udfDefinitions()]);
    return { values, definitions };
  }, { values: [] as ClientUserDefinedFieldValueDto[], definitions: [] as UserDefinedFieldTypeDto[] });
  const [form, setForm] = useState<{ editingId: number | null; fieldTypeId: number | null; value: string | null; note: string | null } | null>(null);
  const [saving, setSaving] = useState(false);

  const save = async () => {
    if (!form || form.fieldTypeId == null) return;
    setSaving(true);
    const ok = form.editingId != null
      ? await ClientUdfValueApi.update(clientId, form.editingId, { ...defaultUpdateClientUserDefinedFieldValueDto(), value: form.value, note: form.note })
      : await ClientUdfValueApi.create({ ...defaultCreateClientUserDefinedFieldValueDto(), clientId, userDefinedFieldTypeId: form.fieldTypeId, value: form.value, note: form.note });
    setSaving(false);
    if (ok) { setForm(null); await reload(); }
  };

  const hasDefinitions = data.definitions.length > 0;
  return (
    <div>
      <div className="d-flex justify-content-end mb-1">
        <button type="button" className="btn btn-sm btn-outline-primary" disabled={!hasDefinitions}
                onClick={() => setForm({ editingId: null, fieldTypeId: data.definitions[0]?.id ?? null, value: null, note: null })}>+ New</button>
      </div>
      {loading && data.values.length === 0 ? <LoadingState text="Loading fields..." /> : (
        <>
          {!hasDefinitions && <div className="text-muted small">No user-defined fields configured yet.</div>}
          {data.values.length === 0 ? (
            hasDefinitions && <EmptyState message="No user-defined field values recorded." icon="bi-sliders" />
          ) : (
            <div className="table-responsive">
              <table className="table table-sm table-hover">
                <thead><tr><th>Field Name</th><th>Value</th><th>Note</th><th /></tr></thead>
                <tbody>
                  {data.values.map((v) => (
                    <tr key={v.id}>
                      <td><a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); setForm({ editingId: v.id, fieldTypeId: v.userDefinedFieldTypeId, value: v.value, note: v.note }); }}>{v.fieldName}</a></td>
                      <td>{v.value}</td>
                      <td>{v.note}</td>
                      <td className="text-end text-nowrap"><button type="button" className="btn btn-sm btn-outline-danger" onClick={async () => { await ClientUdfValueApi.remove(clientId, v.id); await reload(); }}>Delete</button></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}

      {form && (
        <Modal title={form.editingId != null ? 'Edit Field Value' : 'New Field Value'} scrollable={false} onClose={() => setForm(null)}
               footer={<>
                 <button type="button" className="btn btn-sm btn-secondary" onClick={() => setForm(null)}>Cancel</button>
                 <button type="button" className="btn btn-sm btn-primary" disabled={saving} onClick={save}>Save</button>
               </>}>
          <div className="row g-2">
            <div className="col-12">
              <label className="form-label small mb-0">Field Name</label>
              <NumberSelect className="form-select form-select-sm" nullable={false} disabled={form.editingId != null} value={form.fieldTypeId} onChange={(v) => setForm({ ...form, fieldTypeId: v })}>
                {data.definitions.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
              </NumberSelect>
            </div>
            <div className="col-12"><label className="form-label small mb-0">Value</label><TextInput className="form-control form-control-sm" value={form.value} onChange={(v) => setForm({ ...form, value: v })} /></div>
            <div className="col-12"><label className="form-label small mb-0">Note</label><TextArea className="form-control form-control-sm" rows={2} value={form.note} onChange={(v) => setForm({ ...form, note: v })} /></div>
          </div>
        </Modal>
      )}
    </div>
  );
}
