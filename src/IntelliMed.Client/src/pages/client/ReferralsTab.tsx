import { useState } from 'react';
import { ClientReferralApi } from '@/api/clients';
import type { ClientReferralDto, CreateClientReferralDto } from '@/api/types.gen';
import { AddressBookPicker } from '@/components/ContactPickers';
import { DateInput, TextArea, TextInput } from '@/components/Inputs';
import { Modal } from '@/components/Modal';
import { EmptyState, LoadingState } from '@/components/States';
import { formatDate, todayIso } from '@/lib/dates';
import { noneMessage, TabToolbar, tick, useTabData } from './shared';

export function ReferralsTab({ clientId }: { clientId: number }) {
  const { data: referrals, loading, reload } = useTabData(() => ClientReferralApi.list(clientId), [] as ClientReferralDto[]);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [form, setForm] = useState<{ editingId: number | null; dto: CreateClientReferralDto; contactName: string | null; providerNumber: string | null } | null>(null);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [saving, setSaving] = useState(false);

  const visible = referrals.filter((r) => includeArchived || !r.isArchived);

  const openNew = () => setForm({
    editingId: null, contactName: null, providerNumber: null,
    dto: { clientId, referralDate: todayIso(), referralPeriod: null, isDefault: false, isGP: false, referringContactId: 0, requestTypeCde: null, firstVisitDate: null, note: null } as CreateClientReferralDto,
  });
  const openEdit = (r: ClientReferralDto) => setForm({
    editingId: r.id, contactName: r.referringProviderName, providerNumber: r.referringProviderNumber,
    dto: {
      clientId: r.clientId, referralDate: r.referralDate, referralPeriod: r.referralPeriod, isDefault: r.isDefault, isGP: r.isGP,
      referringContactId: r.referringContactId, requestTypeCde: r.requestTypeCde, firstVisitDate: r.firstVisitDate, note: r.note,
    } as CreateClientReferralDto,
  });
  const set = (patch: Partial<CreateClientReferralDto>) => setForm((f) => (f ? { ...f, dto: { ...f.dto, ...patch } } : f));

  const save = async () => {
    if (!form) return;
    setSaving(true);
    const { clientId: _ignored, ...rest } = form.dto;
    void _ignored;
    const ok = form.editingId != null
      ? await ClientReferralApi.update(clientId, form.editingId, { ...rest, isArchived: false })
      : await ClientReferralApi.create(form.dto);
    setSaving(false);
    if (ok) {
      setForm(null);
      await reload();
    }
  };

  return (
    <div>
      <TabToolbar archivedId="chkInclArchivedRef" includeArchived={includeArchived} onIncludeArchived={setIncludeArchived} onNew={openNew} />
      {loading && referrals.length === 0 ? <LoadingState text="Loading referrals..." /> : visible.length === 0 ? (
        <EmptyState message={noneMessage('referrals', referrals.length, includeArchived)} icon="bi-person-lines-fill" />
      ) : (
        <div className="table-responsive">
          <table className="table table-sm table-hover">
            <thead><tr><th>Provider</th><th>GP</th><th>Issue</th><th>Period</th><th>1st Visit</th><th>Type</th><th>Default</th><th>Note</th><th /></tr></thead>
            <tbody>
              {visible.map((r) => (
                <tr key={r.id} className={r.isArchived ? 'text-muted' : ''}>
                  <td><a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); openEdit(r); }}>{r.referringProviderName}</a></td>
                  <td>{tick(r.isGP)}</td>
                  <td>{formatDate(r.referralDate, 'd/M/yy')}</td>
                  <td>{r.referralPeriod}</td>
                  <td>{formatDate(r.firstVisitDate, 'd/M/yy')}</td>
                  <td>{r.requestTypeCde}</td>
                  <td>{tick(r.isDefault)}</td>
                  <td>{r.note}</td>
                  <td className="text-end text-nowrap">
                    {!r.isArchived && <button type="button" className="btn btn-sm btn-outline-warning me-1" onClick={async () => { await ClientReferralApi.archive(clientId, r.id); await reload(); }}>Archive</button>}
                    <button type="button" className="btn btn-sm btn-outline-danger" onClick={async () => { await ClientReferralApi.remove(clientId, r.id); await reload(); }}>Delete</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {form && (
        <Modal title={form.editingId != null ? 'Edit Referral' : 'New Referral'} scrollable={false} onClose={() => setForm(null)}
               footer={<>
                 <button type="button" className="btn btn-sm btn-secondary" onClick={() => setForm(null)}>Cancel</button>
                 <button type="button" className="btn btn-sm btn-primary" disabled={saving || form.dto.referringContactId === 0} onClick={save}>Save</button>
               </>}>
          <div className="row g-2">
            <div className="col-8">
              <label className="form-label small mb-0">Referring Provider</label>
              <div className="input-group input-group-sm">
                <input className="form-control form-control-sm" value={form.contactName ?? ''} readOnly placeholder="Choose from the address book" />
                <button type="button" className="btn btn-outline-secondary" onClick={() => setPickerOpen(true)}>Find</button>
              </div>
            </div>
            <div className="col-4"><label className="form-label small mb-0">Provider#</label><input className="form-control form-control-sm" value={form.providerNumber ?? ''} readOnly /></div>
            <div className="col-4"><label className="form-label small mb-0">Issue Date</label><DateInput className="form-control form-control-sm" value={form.dto.referralDate} onChange={(v) => v && set({ referralDate: v })} /></div>
            <div className="col-4"><label className="form-label small mb-0">Period</label><TextInput className="form-control form-control-sm" maxLength={2} value={form.dto.referralPeriod} onChange={(v) => set({ referralPeriod: v })} /></div>
            <div className="col-4"><label className="form-label small mb-0">1st Visit</label><DateInput className="form-control form-control-sm" value={form.dto.firstVisitDate} onChange={(v) => set({ firstVisitDate: v })} /></div>
            <div className="col-6"><label className="form-label small mb-0">Type</label><TextInput className="form-control form-control-sm" maxLength={1} value={form.dto.requestTypeCde} onChange={(v) => set({ requestTypeCde: v })} /></div>
            <div className="col-6 d-flex align-items-end gap-3">
              <div className="form-check"><input type="checkbox" className="form-check-input" id="chkRefGp" checked={form.dto.isGP} onChange={(e) => set({ isGP: e.target.checked })} /><label className="form-check-label small" htmlFor="chkRefGp">GP</label></div>
              <div className="form-check"><input type="checkbox" className="form-check-input" id="chkRefDefault" checked={form.dto.isDefault} onChange={(e) => set({ isDefault: e.target.checked })} /><label className="form-check-label small" htmlFor="chkRefDefault">Default</label></div>
            </div>
            <div className="col-12"><label className="form-label small mb-0">Note</label><TextArea className="form-control form-control-sm" rows={2} value={form.dto.note} onChange={(v) => set({ note: v })} /></div>
          </div>
        </Modal>
      )}
      {form && pickerOpen && (
        <AddressBookPicker title="Find Referring Provider" onClose={() => setPickerOpen(false)}
                           onSelect={(c) => { setForm({ ...form, dto: { ...form.dto, referringContactId: c.id }, contactName: c.fullName, providerNumber: c.providerNumber }); setPickerOpen(false); }} />
      )}
    </div>
  );
}
