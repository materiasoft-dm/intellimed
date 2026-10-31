import { useCallback, useState } from 'react';
import { useNavigate } from 'react-router';
import { ClientApi, ClientClaimApi, ClientFamilyApi } from '@/api/clients';
import { defaultCreateClientCompensationClaimDto, defaultCreateClientDto, type ClientCompensationClaimDto, type ClientDto, type ClientFamilyRelationshipDto, type CreateClientCompensationClaimDto } from '@/api/types.gen';
import { OrganisationPicker } from '@/components/ContactPickers';
import { DateInput, TextArea, TextInput } from '@/components/Inputs';
import { Modal } from '@/components/Modal';
import { EmptyState, LoadingState } from '@/components/States';
import { formatDate, todayIso } from '@/lib/dates';
import { useLatestSearch } from '@/lib/hooks';
import { noneMessage, TabToolbar, tick, useTabData } from './shared';

export function ClaimsTab({ clientId }: { clientId: number }) {
  const { data: claims, loading, reload } = useTabData(() => ClientClaimApi.list(clientId), [] as ClientCompensationClaimDto[]);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [form, setForm] = useState<{ editingId: number | null; dto: CreateClientCompensationClaimDto; employerName: string | null } | null>(null);
  const [employerPickerOpen, setEmployerPickerOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const visible = claims.filter((c) => includeArchived || !c.isArchived);

  const openNew = () => setForm({
    editingId: null, employerName: null,
    dto: { ...defaultCreateClientCompensationClaimDto(), clientId },
  });
  const openEdit = (c: ClientCompensationClaimDto) => setForm({
    editingId: c.id, employerName: c.employerName,
    dto: { clientId: c.clientId, claimNum: c.claimNum, dateOfInjury: c.dateOfInjury, employerClientId: c.employerClientId, caseManagerName: c.caseManagerName, payerName: c.payerName, isDefault: c.isDefault, publicNote: c.publicNote, privateNote: c.privateNote } as CreateClientCompensationClaimDto,
  });
  const set = (patch: Partial<CreateClientCompensationClaimDto>) => setForm((f) => (f ? { ...f, dto: { ...f.dto, ...patch } } : f));

  const save = async () => {
    if (!form) return;
    setSaving(true);
    const { clientId: _c, ...rest } = form.dto;
    void _c;
    const ok = form.editingId != null
      ? await ClientClaimApi.update(clientId, form.editingId, { ...rest, isArchived: false })
      : await ClientClaimApi.create(form.dto);
    setSaving(false);
    if (ok) { setForm(null); await reload(); }
  };

  return (
    <div>
      <TabToolbar archivedId="chkInclArchivedClaim" includeArchived={includeArchived} onIncludeArchived={setIncludeArchived} onNew={openNew} />
      {loading && claims.length === 0 ? <LoadingState text="Loading claims..." /> : visible.length === 0 ? (
        <EmptyState message={noneMessage('WC/TAC claims', claims.length, includeArchived)} icon="bi-briefcase" />
      ) : (
        <div className="table-responsive">
          <table className="table table-sm table-hover">
            <thead><tr><th>Claim#</th><th>Date of Injury</th><th>Employer</th><th>Case Manager</th><th>Payer</th><th>Default</th><th /></tr></thead>
            <tbody>
              {visible.map((c) => (
                <tr key={c.id} className={c.isArchived ? 'text-muted' : ''}>
                  <td><a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); openEdit(c); }}>{c.claimNum || '(no number)'}</a></td>
                  <td>{formatDate(c.dateOfInjury, 'd/M/yy')}</td>
                  <td>{c.employerName}</td>
                  <td>{c.caseManagerName}</td>
                  <td>{c.payerName}</td>
                  <td>{tick(c.isDefault)}</td>
                  <td className="text-end text-nowrap">
                    {!c.isArchived && <button type="button" className="btn btn-sm btn-outline-danger" onClick={async () => { await ClientClaimApi.archive(clientId, c.id); await reload(); }}>Archive</button>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {form && (
        <Modal title={form.editingId != null ? 'Edit Claim' : 'New Claim'} scrollable={false} onClose={() => setForm(null)}
               footer={<>
                 <button type="button" className="btn btn-sm btn-secondary" onClick={() => setForm(null)}>Cancel</button>
                 <button type="button" className="btn btn-sm btn-primary" disabled={saving} onClick={save}>Save</button>
               </>}>
          <div className="row g-2">
            <div className="col-6"><label className="form-label small mb-0">Claim#</label><TextInput className="form-control form-control-sm" maxLength={30} value={form.dto.claimNum} onChange={(v) => set({ claimNum: v ?? '' })} /></div>
            <div className="col-6"><label className="form-label small mb-0">Date of Injury</label><DateInput className="form-control form-control-sm" value={form.dto.dateOfInjury} onChange={(v) => set({ dateOfInjury: v })} /></div>
            <div className="col-6">
              <label className="form-label small mb-0">Employer</label>
              <div className="input-group input-group-sm">
                <input className="form-control form-control-sm" value={form.employerName ?? ''} readOnly placeholder="No employer" />
                <button type="button" className="btn btn-outline-secondary" onClick={() => setEmployerPickerOpen(true)}>Find</button>
                {form.dto.employerClientId != null && (
                  <button type="button" className="btn btn-outline-secondary" title="Clear employer" onClick={() => setForm({ ...form, dto: { ...form.dto, employerClientId: null }, employerName: null })}>x</button>
                )}
              </div>
            </div>
            <div className="col-6"><label className="form-label small mb-0">Case Manager</label><TextInput className="form-control form-control-sm" value={form.dto.caseManagerName} onChange={(v) => set({ caseManagerName: v })} /></div>
            <div className="col-6"><label className="form-label small mb-0">Payer</label><TextInput className="form-control form-control-sm" value={form.dto.payerName} onChange={(v) => set({ payerName: v })} /></div>
            <div className="col-6 d-flex align-items-end">
              <div className="form-check"><input type="checkbox" className="form-check-input" id="chkClaimDefault" checked={form.dto.isDefault} onChange={(e) => set({ isDefault: e.target.checked })} /><label className="form-check-label small" htmlFor="chkClaimDefault">Default</label></div>
            </div>
            <div className="col-12"><label className="form-label small mb-0">Public Note (printed)</label><TextArea className="form-control form-control-sm" rows={2} value={form.dto.publicNote} onChange={(v) => set({ publicNote: v })} /></div>
            <div className="col-12"><label className="form-label small mb-0">Private Note (not printed)</label><TextArea className="form-control form-control-sm" rows={2} value={form.dto.privateNote} onChange={(v) => set({ privateNote: v })} /></div>
          </div>
        </Modal>
      )}
      {form && employerPickerOpen && (
        <OrganisationPicker title="Find Employer" onClose={() => setEmployerPickerOpen(false)}
                            onSelect={(o) => { setForm({ ...form, dto: { ...form.dto, employerClientId: o.id }, employerName: o.name }); setEmployerPickerOpen(false); }} />
      )}
    </div>
  );
}

/** Family members: link an existing client, or create a new one, with the relationship. */
export function FamilyTab({ clientId }: { clientId: number }) {
  const navigate = useNavigate();
  const { data: family, loading, reload } = useTabData(() => ClientFamilyApi.list(clientId), [] as ClientFamilyRelationshipDto[]);
  const [adding, setAdding] = useState(false);
  const [linkExisting, setLinkExisting] = useState(true);
  const [relationship, setRelationship] = useState('');
  const [term, setTerm] = useState('');
  const [newMember, setNewMember] = useState({ lastName: '', firstName: '', dateOfBirth: null as string | null });
  const [error, setError] = useState<string | null>(null);
  const search = useCallback(async (q: string) => (await ClientApi.search({ query: q, pageSize: 10 })).items.filter((c) => c.id !== clientId), [clientId]);
  const { results, searching, run, clear } = useLatestSearch<ClientDto[]>(search, []);

  const start = () => {
    setRelationship('');
    setLinkExisting(true);
    setTerm('');
    clear();
    setNewMember({ lastName: '', firstName: '', dateOfBirth: null });
    setError(null);
    setAdding(true);
  };

  const link = async (relativeClientId: number) => {
    await ClientFamilyApi.create({ clientId, relativeClientId, relationshipType: relationship });
    setAdding(false);
    await reload();
  };

  const createNew = async () => {
    const id = await ClientApi.create({ ...defaultCreateClientDto(), firstName: newMember.firstName, lastName: newMember.lastName, dateOfBirth: newMember.dateOfBirth ?? todayIso() });
    if (id == null) { setError('Failed to create family member.'); return; }
    await link(id);
  };

  return (
    <div>
      <div className="d-flex justify-content-end mb-1">
        <button type="button" className="btn btn-sm btn-outline-primary" onClick={start}>+ New</button>
      </div>
      {loading && family.length === 0 ? <LoadingState text="Loading family..." /> : family.length === 0 ? (
        <EmptyState message={'No family members linked. Use "+ New" above to add one.'} icon="bi-people" />
      ) : (
        <div className="table-responsive">
          <table className="table table-sm table-hover">
            <thead><tr><th>Name</th><th>Address</th><th>Relationship</th><th /></tr></thead>
            <tbody>
              {family.map((f) => (
                <tr key={f.id}>
                  <td>{f.relativeName}</td>
                  <td>{f.relativeAddress}</td>
                  <td>{f.relationshipType}</td>
                  <td className="text-end text-nowrap">
                    <button type="button" className="btn btn-sm btn-outline-secondary me-1" onClick={() => navigate(`/clients/edit/${f.relativeClientId}`)}>Open</button>
                    <button type="button" className="btn btn-sm btn-outline-danger" onClick={async () => { await ClientFamilyApi.remove(clientId, f.id); await reload(); }}>Remove</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {adding && (
        <Modal title="New Family Member" scrollable={false} onClose={() => setAdding(false)}
               footer={<>
                 <button type="button" className="btn btn-sm btn-secondary" onClick={() => setAdding(false)}>Cancel</button>
                 {!linkExisting && <button type="button" className="btn btn-sm btn-primary" onClick={createNew}>Create</button>}
               </>}>
          <ul className="nav nav-pills mb-2">
            <li className="nav-item"><a className={`nav-link ${linkExisting ? 'active' : ''}`} href="#" onClick={(e) => { e.preventDefault(); setLinkExisting(true); }}>Link Existing</a></li>
            <li className="nav-item"><a className={`nav-link ${!linkExisting ? 'active' : ''}`} href="#" onClick={(e) => { e.preventDefault(); setLinkExisting(false); }}>Create New</a></li>
          </ul>
          <div className="mb-2">
            <label className="form-label small mb-0">Relationship</label>
            <input className="form-control form-control-sm" placeholder="e.g. Spouse, Child, Parent" value={relationship} onChange={(e) => setRelationship(e.target.value)} />
          </div>
          {linkExisting ? (
            <>
              <input className="form-control form-control-sm mb-2" placeholder="Search by name..." value={term} onChange={(e) => { setTerm(e.target.value); run(e.target.value); }} />
              <div style={{ maxHeight: 260, overflowY: 'auto' }}>
                {searching ? <LoadingState text="Searching..." /> : results.length === 0 ? <div className="text-muted small">No matches.</div> : (
                  <ul className="list-group">
                    {results.map((c) => (
                      <li key={c.id} className="list-group-item list-group-item-action" style={{ cursor: 'pointer' }} onClick={() => link(c.id)}>
                        {c.fullName} <span className="text-muted small">({formatDate(c.dateOfBirth, 'dd/MM/yyyy')})</span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </>
          ) : (
            <div className="row g-2">
              <div className="col-6"><label className="form-label small mb-0">Surname</label><input className="form-control form-control-sm" value={newMember.lastName} onChange={(e) => setNewMember({ ...newMember, lastName: e.target.value })} /></div>
              <div className="col-6"><label className="form-label small mb-0">Given</label><input className="form-control form-control-sm" value={newMember.firstName} onChange={(e) => setNewMember({ ...newMember, firstName: e.target.value })} /></div>
              <div className="col-6"><label className="form-label small mb-0">Date of Birth</label><DateInput className="form-control form-control-sm" value={newMember.dateOfBirth} onChange={(v) => setNewMember({ ...newMember, dateOfBirth: v })} /></div>
            </div>
          )}
          {error && <div className="small text-danger mt-2">{error}</div>}
        </Modal>
      )}
    </div>
  );
}
