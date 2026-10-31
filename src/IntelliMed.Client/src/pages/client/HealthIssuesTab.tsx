import { useState } from 'react';
import { ClientHealthIssueApi } from '@/api/clients';
import { BodySide, type ClientHealthIssueDto, type CreateClientHealthIssueDto } from '@/api/types.gen';
import { EnumOptions, NumberInput, NumberSelect, TextArea } from '@/components/Inputs';
import { Modal } from '@/components/Modal';
import { EmptyState, LoadingState } from '@/components/States';
import { useTabData } from './shared';

type Filter = 'All' | 'Active' | 'Inactive' | 'Summary';
const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** The problem list (ported from Medinet's PastHistoryCtrl). Archived rows are fetched too and filtered here. */
export function HealthIssuesTab({ clientId }: { clientId: number }) {
  const { data: issues, loading, reload } = useTabData(() => ClientHealthIssueApi.list(clientId, true), [] as ClientHealthIssueDto[]);
  const [filter, setFilter] = useState<Filter>('Active');
  const [form, setForm] = useState<{ editingId: number | null; dto: CreateClientHealthIssueDto } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const filtered = issues.filter((h) => {
    switch (filter) {
      case 'Active': return h.isActive && !h.isArchived;
      case 'Inactive': return !h.isActive && !h.isArchived;
      case 'Summary': return h.isSummary && !h.isArchived;
      default: return true;
    }
  });

  const blank = (): CreateClientHealthIssueDto => ({
    clientId, condition: '', code: null, codingSystem: null, side: null, onsetYear: null, onsetMonth: null, isActive: true, isSummary: false, isConfidential: false, note: null,
  } as CreateClientHealthIssueDto);
  const openNew = () => { setError(null); setForm({ editingId: null, dto: blank() }); };
  const openEdit = (h: ClientHealthIssueDto) => {
    setError(null);
    setForm({
      editingId: h.id,
      dto: {
        clientId: h.clientId, condition: h.condition, code: h.code, codingSystem: h.codingSystem, side: h.side, onsetYear: h.onsetYear,
        onsetMonth: h.onsetMonth, isActive: h.isActive, isSummary: h.isSummary, isConfidential: h.isConfidential, note: h.note,
      } as CreateClientHealthIssueDto,
    });
  };
  const set = (patch: Partial<CreateClientHealthIssueDto>) => setForm((f) => (f ? { ...f, dto: { ...f.dto, ...patch } } : f));

  const save = async () => {
    if (!form) return;
    if (!form.dto.condition?.trim()) { setError('Condition is required.'); return; }
    setSaving(true);
    const { clientId: _c, ...rest } = form.dto;
    void _c;
    const ok = form.editingId != null
      ? await ClientHealthIssueApi.update(clientId, form.editingId, { ...rest, isArchived: false })
      : await ClientHealthIssueApi.create({ ...form.dto, clientId });
    setSaving(false);
    if (ok) {
      setForm(null);
      await reload();
    } else {
      setError('Save failed — check your permissions.');
    }
  };

  return (
    <div>
      <div className="d-flex justify-content-between mb-2">
        <div className="btn-group btn-group-sm" role="group" aria-label="Filter health issues">
          {(['All', 'Active', 'Inactive', 'Summary'] as const).map((f) => (
            <button key={f} type="button" className={`btn ${filter === f ? 'btn-primary' : 'btn-outline-secondary'}`} aria-pressed={filter === f} onClick={() => setFilter(f)}>{f}</button>
          ))}
        </div>
        <button type="button" className="btn btn-sm btn-outline-primary" onClick={openNew}>+ New</button>
      </div>

      {loading && issues.length === 0 ? <LoadingState text="Loading health issues..." /> : filtered.length === 0 ? (
        <EmptyState message="No health issues recorded." icon="bi-heart-pulse" />
      ) : (
        <div className="table-responsive">
          <table className="table table-sm table-hover">
            <thead><tr><th>Condition</th><th>Onset</th><th>Side</th><th>Status</th><th>Note</th><th /></tr></thead>
            <tbody>
              {filtered.map((h) => (
                <tr key={h.id} className={h.isArchived ? 'text-muted' : ''}>
                  <td>
                    <a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); openEdit(h); }}>{h.condition}</a>
                    {h.isSummary && <span className="badge bg-info text-dark ms-1" title="Shown on the patient summary">summary</span>}
                    {h.isConfidential && <span className="badge bg-secondary ms-1">confidential</span>}
                  </td>
                  <td>{h.onsetDisplay}</td>
                  <td>{h.side != null ? BodySide[h.side] : ''}</td>
                  <td><span className={`badge ${h.isActive ? 'bg-success' : 'bg-secondary'}`}>{h.isActive ? 'Active' : 'Inactive'}</span></td>
                  <td>{h.note}</td>
                  <td className="text-end text-nowrap">
                    {!h.isArchived && <button type="button" className="btn btn-sm btn-outline-warning" onClick={async () => { await ClientHealthIssueApi.archive(clientId, h.id); await reload(); }}>Archive</button>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {form && (
        <Modal title={form.editingId != null ? 'Edit Health Issue' : 'New Health Issue'} scrollable={false} onClose={() => setForm(null)}
               footer={<>
                 <button type="button" className="btn btn-sm btn-secondary" onClick={() => setForm(null)}>Cancel</button>
                 <button type="button" className="btn btn-sm btn-primary" disabled={saving} onClick={save}>Save</button>
               </>}>
          <div className="row g-2">
            <div className="col-12">
              <label className="form-label small mb-0">Condition</label>
              <input className="form-control form-control-sm" placeholder="e.g. Type 2 diabetes" value={form.dto.condition ?? ''} onChange={(e) => set({ condition: e.target.value })} />
            </div>
            <div className="col-4">
              <label className="form-label small mb-0">Onset year</label>
              <NumberInput nullable className="form-control form-control-sm" placeholder="e.g. 2015" value={form.dto.onsetYear} onChange={(v) => set({ onsetYear: v })} />
            </div>
            <div className="col-4">
              <label className="form-label small mb-0">Onset month</label>
              <NumberSelect className="form-select form-select-sm" value={form.dto.onsetMonth} onChange={(v) => set({ onsetMonth: v })}>
                <option value="">—</option>
                {MONTHS.map((m, i) => <option key={m} value={i + 1}>{m}</option>)}
              </NumberSelect>
            </div>
            <div className="col-4">
              <label className="form-label small mb-0">Side</label>
              <NumberSelect enumType={BodySide} className="form-select form-select-sm" value={form.dto.side} onChange={(v) => set({ side: v as BodySide | null })}>
                <option value="">N/A</option>
                <EnumOptions enumType={BodySide} />
              </NumberSelect>
            </div>
            <div className="col-12">
              <label className="form-label small mb-0">Note</label>
              <TextArea className="form-control form-control-sm" rows={2} value={form.dto.note} onChange={(v) => set({ note: v })} />
            </div>
            <div className="col-12 d-flex gap-3">
              <div className="form-check"><input className="form-check-input" type="checkbox" id="chkHiActive" checked={form.dto.isActive} onChange={(e) => set({ isActive: e.target.checked })} /><label className="form-check-label small" htmlFor="chkHiActive">Active</label></div>
              <div className="form-check"><input className="form-check-input" type="checkbox" id="chkHiSummary" checked={form.dto.isSummary} onChange={(e) => set({ isSummary: e.target.checked })} /><label className="form-check-label small" htmlFor="chkHiSummary">Show on summary</label></div>
              <div className="form-check"><input className="form-check-input" type="checkbox" id="chkHiConfidential" checked={form.dto.isConfidential} onChange={(e) => set({ isConfidential: e.target.checked })} /><label className="form-check-label small" htmlFor="chkHiConfidential">Confidential</label></div>
            </div>
          </div>
          {error && <div className="small text-danger mt-2">{error}</div>}
        </Modal>
      )}
    </div>
  );
}
