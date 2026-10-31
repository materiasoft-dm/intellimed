import { useState, type FormEvent } from 'react';
import { ClinicApi } from '@/api/admin';
import { ClinicType, type ClinicDto, type ClinicUserDto } from '@/api/types.gen';
import { NumberSelect, TextInput } from '@/components/Inputs';
import { Modal } from '@/components/Modal';
import { EmptyState, LoadingState } from '@/components/States';
import { useToast } from '@/components/Toast';
import { useMountEffect, usePageTitle } from '@/lib/hooks';
import { FieldMessage, hasErrors, rules, validate, ValidationSummary } from '@/lib/validation';

interface ClinicForm extends Record<string, unknown> {
  name: string;
  type: ClinicType;
  abn: string | null;
  phone: string | null;
  fax: string | null;
  email: string | null;
  address: string | null;
  suburb: string | null;
  postcode: string | null;
  state: string | null;
  isActive: boolean;
}

const blank = (): ClinicForm => ({
  name: '', type: ClinicType.Clinic, abn: null, phone: null, fax: null, email: null, address: null, suburb: null, postcode: null, state: null, isActive: true,
});

export default function ClinicManager() {
  usePageTitle('Clinic Manager - IntelliMed');
  const toast = useToast();
  const [clinics, setClinics] = useState<ClinicDto[] | null>(null);
  const [loading, setLoading] = useState(true);

  const [form, setForm] = useState<ClinicForm | null>(null);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);
  const [tried, setTried] = useState(false);

  const [usersClinic, setUsersClinic] = useState<ClinicDto | null>(null);
  const [clinicUsers, setClinicUsers] = useState<ClinicUserDto[] | null>(null);
  const [selectedUserIds, setSelectedUserIds] = useState<Set<string>>(new Set());
  const [usersSaving, setUsersSaving] = useState(false);

  const load = async () => {
    setLoading(true);
    setClinics(await ClinicApi.getAll());
    setLoading(false);
  };
  useMountEffect(load);

  const openCreate = () => { setEditingId(null); setForm(blank()); setTried(false); };
  const openEdit = (c: ClinicDto) => {
    setEditingId(c.id);
    setTried(false);
    setForm({
      name: c.name, type: c.type, abn: c.abn, phone: c.phone, fax: c.fax, email: c.email, address: c.address,
      suburb: c.suburb, postcode: c.postcode, state: c.state, isActive: c.isActive,
    });
  };

  const spec = { name: [rules.required('Name')] };
  const errors = tried && form ? validate(form, spec) : {};
  const set = <K extends keyof ClinicForm>(key: K) => (value: ClinicForm[K]) => setForm((f) => (f ? { ...f, [key]: value } : f));

  const saveClinic = async (e: FormEvent) => {
    e.preventDefault();
    if (!form) return;
    setTried(true);
    if (hasErrors(validate(form, spec))) return;
    setSaving(true);
    try {
      const { isActive, ...common } = form;
      const ok = editingId != null
        ? await ClinicApi.update(editingId, { ...common, isActive })
        : await ClinicApi.create(common);
      if (ok) {
        toast.success(editingId != null ? 'Clinic updated successfully.' : 'Clinic created successfully.');
        setForm(null);
        await load();
      } else {
        toast.error('Failed to save clinic.');
      }
    } finally {
      setSaving(false);
    }
  };

  const openUsers = async (c: ClinicDto) => {
    setUsersClinic(c);
    setClinicUsers(null);
    setSelectedUserIds(new Set());
    const users = (await ClinicApi.getUsers(c.id)) ?? [];
    setClinicUsers(users);
    setSelectedUserIds(new Set(users.filter((u) => u.isAssigned).map((u) => u.id)));
  };

  const saveUsers = async () => {
    if (!usersClinic) return;
    setUsersSaving(true);
    try {
      const ok = await ClinicApi.setUsers(usersClinic.id, { userIds: [...selectedUserIds] });
      if (ok) {
        toast.success('Clinic users updated successfully.');
        setUsersClinic(null);
        await load();
      } else {
        toast.error('Failed to update clinic users.');
      }
    } finally {
      setUsersSaving(false);
    }
  };

  return (
    <>
      <h4 className="mb-4">Clinic Manager</h4>
      <div className="card mb-4">
        <div className="card-body d-flex align-items-center justify-content-between">
          <span className="d-flex align-items-center"><i className="bi bi-diagram-3 me-2" />Total Clinics: {clinics?.length ?? 0}</span>
          <button className="btn btn-primary" onClick={openCreate}><i className="bi bi-plus-lg me-1" /> Add Clinic</button>
        </div>
      </div>

      {loading ? (
        <div className="card"><div className="card-body"><LoadingState text="Loading clinics..." compact={false} /></div></div>
      ) : clinics === null ? (
        <div className="alert alert-danger">You don't have access to this page.</div>
      ) : (
        <div className="card">
          {clinics.length === 0 ? (
            <div className="card-body"><EmptyState message="No clinics yet." icon="bi-building" compact={false} /></div>
          ) : (
            <div className="table-responsive">
              <table className="table table-hover table-striped table-sm mb-0">
                <thead className="table-light">
                  <tr><th>Name</th><th>Type</th><th>Phone</th><th>Address</th><th>Users</th><th>Status</th><th className="text-end">Actions</th></tr>
                </thead>
                <tbody>
                  {clinics.map((c) => (
                    <tr key={c.id}>
                      <td>
                        <i className="bi bi-building me-2 text-muted" />
                        <a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); openEdit(c); }}>{c.name}</a>
                      </td>
                      <td>{ClinicType[c.type]}</td>
                      <td>{c.phone}</td>
                      <td>{c.suburb?.trim() ? `${c.address ?? ''}, ${c.suburb}` : c.address}</td>
                      <td>{c.userCount}</td>
                      <td>{c.isActive ? <span className="badge bg-success">Active</span> : <span className="badge bg-danger">Inactive</span>}</td>
                      <td className="text-end">
                        <button className="btn btn-sm btn-outline-secondary" title="Manage Users" aria-label={`Manage users of ${c.name}`} onClick={() => openUsers(c)}>
                          <i className="bi bi-people" />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {form && (
        <Modal title={editingId != null ? 'Edit Clinic' : 'Add Clinic'} titleTag="h5" headerClassName="" scrollable={false} onClose={() => setForm(null)}
               footer={<>
                 <button type="button" className="btn btn-secondary" onClick={() => setForm(null)}>Cancel</button>
                 <button type="submit" form="clinicDialogForm" className="btn btn-primary" disabled={saving}>
                   {saving ? <><span className="spinner-border spinner-border-sm me-2" /><span>Saving...</span></> : <span>{editingId != null ? 'Save Changes' : 'Create Clinic'}</span>}
                 </button>
               </>}>
          <form id="clinicDialogForm" onSubmit={saveClinic} noValidate>
            <ValidationSummary errors={errors} className="text-danger small mb-3" />
            <div className="row g-3 mb-3">
              <div className="col-md-6">
                <label className="form-label">Name <span className="text-danger">*</span></label>
                <input className="form-control" value={form.name} onChange={(e) => set('name')(e.target.value)} />
                <FieldMessage messages={errors.name} />
              </div>
              <div className="col-md-3">
                <label className="form-label">Type</label>
                <NumberSelect enumType={ClinicType} className="form-select" nullable={false} value={form.type} onChange={(v) => set('type')((v ?? 0) as ClinicType)}>
                  <option value={ClinicType.Clinic}>Clinic</option>
                  <option value={ClinicType.Hospital}>Hospital</option>
                </NumberSelect>
              </div>
              <div className="col-md-3">
                <label className="form-label">ABN</label>
                <TextInput className="form-control" value={form.abn} onChange={set('abn')} />
              </div>
            </div>
            <div className="row g-3 mb-3">
              <div className="col-md-4"><label className="form-label">Phone</label><TextInput className="form-control" value={form.phone} onChange={set('phone')} /></div>
              <div className="col-md-4"><label className="form-label">Fax</label><TextInput className="form-control" value={form.fax} onChange={set('fax')} /></div>
              <div className="col-md-4"><label className="form-label">Email</label><TextInput className="form-control" value={form.email} onChange={set('email')} /></div>
            </div>
            <div className="row g-3 mb-2">
              <div className="col-md-12"><label className="form-label small mb-0">Address</label><TextInput className="form-control form-control-sm" value={form.address} onChange={set('address')} /></div>
            </div>
            <div className="row g-3 mb-3">
              <div className="col-md-6"><label className="form-label small mb-0">Suburb</label><TextInput className="form-control form-control-sm" value={form.suburb} onChange={set('suburb')} /></div>
              <div className="col-md-3"><label className="form-label small mb-0">Postcode</label><TextInput className="form-control form-control-sm" value={form.postcode} onChange={set('postcode')} /></div>
              <div className="col-md-3"><label className="form-label small mb-0">State</label><TextInput className="form-control form-control-sm" value={form.state} onChange={set('state')} /></div>
            </div>
            {editingId != null && (
              <div className="form-check form-switch">
                <input className="form-check-input" type="checkbox" role="switch" id="clinicActiveSwitch" checked={form.isActive}
                       onChange={(e) => set('isActive')(e.target.checked)} />
                <label className="form-check-label" htmlFor="clinicActiveSwitch">Active</label>
              </div>
            )}
          </form>
        </Modal>
      )}

      {usersClinic && (
        <Modal title={`Manage Users — ${usersClinic.name}`} titleTag="h5" headerClassName="" scrollable={false} onClose={() => setUsersClinic(null)}
               footer={<>
                 <button type="button" className="btn btn-secondary" onClick={() => setUsersClinic(null)}>Cancel</button>
                 <button type="button" className="btn btn-primary" onClick={saveUsers} disabled={usersSaving || clinicUsers === null}>
                   {usersSaving ? <><span className="spinner-border spinner-border-sm me-2" /><span>Saving...</span></> : <span>Save</span>}
                 </button>
               </>}>
          {clinicUsers === null ? (
            <LoadingState text="Loading users..." />
          ) : (
            <div className="card bg-light">
              <div className="card-body" style={{ maxHeight: 320, overflowY: 'auto' }}>
                {clinicUsers.length === 0 && <span className="text-muted small">No users found.</span>}
                {clinicUsers.map((u) => (
                  <div key={u.id} className="form-check mb-2">
                    <input className="form-check-input" type="checkbox" id={`user_${u.id}`} checked={selectedUserIds.has(u.id)}
                           onChange={(e) => setSelectedUserIds((s) => {
                             const next = new Set(s);
                             if (e.target.checked) next.add(u.id); else next.delete(u.id);
                             return next;
                           })} />
                    <label className="form-check-label" htmlFor={`user_${u.id}`}>{u.fullName} <span className="text-muted small">({u.email})</span></label>
                  </div>
                ))}
              </div>
            </div>
          )}
        </Modal>
      )}
    </>
  );
}
