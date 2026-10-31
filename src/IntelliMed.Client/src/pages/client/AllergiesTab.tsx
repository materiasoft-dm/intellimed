import { useState } from 'react';
import { ClientAllergyApi } from '@/api/clients';
import { AllergySeverity, AllergyType, type ClientAllergyDto, type CreateClientAllergyDto } from '@/api/types.gen';
import { DateInput, EnumOptions, NumberSelect, TextArea, TextInput } from '@/components/Inputs';
import { Modal } from '@/components/Modal';
import { EmptyState, LoadingState } from '@/components/States';
import { formatDate } from '@/lib/dates';
import { noneMessage, TabToolbar, useTabData } from './shared';

const severityClass = (s: AllergySeverity) => (s === AllergySeverity.Severe ? 'bg-danger' : s === AllergySeverity.Moderate ? 'bg-warning text-dark' : 'bg-secondary');

export function AllergiesTab({ clientId }: { clientId: number }) {
  const { data: allergies, loading, reload } = useTabData(() => ClientAllergyApi.list(clientId), [] as ClientAllergyDto[]);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [form, setForm] = useState<{ editingId: number | null; dto: CreateClientAllergyDto } | null>(null);
  const [saving, setSaving] = useState(false);
  const visible = allergies.filter((a) => includeArchived || !a.isArchived);

  const openNew = () => setForm({
    editingId: null,
    dto: { clientId, activeIngredientId: null, substance: '', type: AllergyType.Medication, reaction: null, severity: null, onsetDate: null, note: null } as CreateClientAllergyDto,
  });
  const openEdit = (a: ClientAllergyDto) => setForm({
    editingId: a.id,
    dto: { clientId: a.clientId, activeIngredientId: a.activeIngredientId, substance: a.substance, type: a.type, reaction: a.reaction, severity: a.severity, onsetDate: a.onsetDate, note: a.note } as CreateClientAllergyDto,
  });
  const set = (patch: Partial<CreateClientAllergyDto>) => setForm((f) => (f ? { ...f, dto: { ...f.dto, ...patch } } : f));

  const save = async () => {
    if (!form) return;
    setSaving(true);
    const { clientId: _c, ...rest } = form.dto;
    void _c;
    const ok = form.editingId != null
      ? await ClientAllergyApi.update(clientId, form.editingId, { ...rest, isArchived: false })
      : await ClientAllergyApi.create(form.dto);
    setSaving(false);
    if (ok) { setForm(null); await reload(); }
  };

  return (
    <div>
      <TabToolbar archivedId="chkInclArchivedAllergy" includeArchived={includeArchived} onIncludeArchived={setIncludeArchived} onNew={openNew} />
      {loading && allergies.length === 0 ? <LoadingState text="Loading allergies..." /> : visible.length === 0 ? (
        <EmptyState message={noneMessage('allergies', allergies.length, includeArchived)} icon="bi-exclamation-triangle" />
      ) : (
        <div className="table-responsive">
          <table className="table table-sm table-hover">
            <thead><tr><th>Substance</th><th>Type</th><th>Reaction</th><th>Severity</th><th>Onset</th><th>Note</th><th /></tr></thead>
            <tbody>
              {visible.map((a) => (
                <tr key={a.id} className={a.isArchived ? 'text-muted' : ''}>
                  <td>
                    <a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); openEdit(a); }}>{a.substance}</a>
                    {a.activeIngredientId != null && <span className="badge bg-light text-dark border ms-1" title="Matched to catalog ingredient">confirmed</span>}
                  </td>
                  <td>{AllergyType[a.type]}</td>
                  <td>{a.reaction}</td>
                  <td>{a.severity != null && <span className={`badge ${severityClass(a.severity)}`}>{AllergySeverity[a.severity]}</span>}</td>
                  <td>{formatDate(a.onsetDate, 'd/M/yy')}</td>
                  <td>{a.note}</td>
                  <td className="text-end text-nowrap">
                    {!a.isArchived && <button type="button" className="btn btn-sm btn-outline-warning me-1" onClick={async () => { await ClientAllergyApi.archive(clientId, a.id); await reload(); }}>Archive</button>}
                    <button type="button" className="btn btn-sm btn-outline-danger" onClick={async () => { await ClientAllergyApi.remove(clientId, a.id); await reload(); }}>Delete</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {form && (
        <Modal title={form.editingId != null ? 'Edit Allergy' : 'New Allergy'} scrollable={false} onClose={() => setForm(null)}
               footer={<>
                 <button type="button" className="btn btn-sm btn-secondary" onClick={() => setForm(null)}>Cancel</button>
                 <button type="button" className="btn btn-sm btn-primary" disabled={saving} onClick={save}>Save</button>
               </>}>
          <div className="row g-2">
            <div className="col-6"><label className="form-label small mb-0">Substance</label><input className="form-control form-control-sm" placeholder="e.g. Penicillin" value={form.dto.substance ?? ''} onChange={(e) => set({ substance: e.target.value })} /></div>
            <div className="col-6">
              <label className="form-label small mb-0">Type</label>
              <NumberSelect enumType={AllergyType} className="form-select form-select-sm" nullable={false} value={form.dto.type} onChange={(v) => set({ type: (v ?? 0) as AllergyType })}><EnumOptions enumType={AllergyType} /></NumberSelect>
            </div>
            <div className="col-6"><label className="form-label small mb-0">Reaction</label><TextInput className="form-control form-control-sm" value={form.dto.reaction} onChange={(v) => set({ reaction: v })} /></div>
            <div className="col-6">
              <label className="form-label small mb-0">Severity</label>
              <NumberSelect enumType={AllergySeverity} className="form-select form-select-sm" value={form.dto.severity} onChange={(v) => set({ severity: v as AllergySeverity | null })}>
                <option value="">Unknown</option><EnumOptions enumType={AllergySeverity} />
              </NumberSelect>
            </div>
            <div className="col-6"><label className="form-label small mb-0">Onset Date</label><DateInput className="form-control form-control-sm" value={form.dto.onsetDate} onChange={(v) => set({ onsetDate: v })} /></div>
            <div className="col-12"><label className="form-label small mb-0">Note</label><TextArea className="form-control form-control-sm" rows={2} value={form.dto.note} onChange={(v) => set({ note: v })} /></div>
          </div>
        </Modal>
      )}
    </div>
  );
}
