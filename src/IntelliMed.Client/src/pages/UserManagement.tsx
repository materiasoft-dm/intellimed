import { useMemo, useState, type FormEvent } from 'react';
import { AdminApi, ClinicApi } from '@/api/admin';
import { PractitionerApi } from '@/api/appointments';
import type { MyClinicDto, PractitionerDto, RoleDto, UserDto } from '@/api/types.gen';
import { LoadMoreSentinel } from '@/lib/infinite';
import { Modal } from '@/components/Modal';
import { EmptyState, LoadingState, SkeletonRows } from '@/components/States';
import { useToast } from '@/components/Toast';
import { formatDate, formatUtc } from '@/lib/dates';
import { useMountEffect, usePageTitle } from '@/lib/hooks';
import { FieldMessage, hasErrors, rules, validate, ValidationSummary } from '@/lib/validation';

type SortKey = 'fullName' | 'email' | 'roles' | 'isActive' | 'lastLoginAt';
const PAGE_SIZE = 25;

interface UserForm extends Record<string, unknown> {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  isActive: boolean;
  roles: string[];
  clinicIds: number[];
  practitionerId: number | null;
}

function roleBadgeClass(role: string): string {
  switch (role) {
    case 'SuperAdmin': return 'bg-danger';
    case 'Admin': return 'bg-warning text-dark';
    case 'Doctor': return 'bg-primary';
    case 'Nurse': return 'bg-success';
    case 'Receptionist': return 'bg-info text-dark';
    default: return 'bg-secondary';
  }
}

/** A preview password shown (read-only) when creating a user; meets the policy Identity enforces server-side. */
function generatePassword(length = 12): string {
  const upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ', lower = 'abcdefghijkmnpqrstuvwxyz', digits = '23456789', special = '!@#$%^&*';
  const all = upper + lower + digits + special;
  const rand = (n: number) => {
    const buf = new Uint32Array(1);
    crypto.getRandomValues(buf);
    return buf[0] % n;
  };
  const chars = [upper[rand(upper.length)], lower[rand(lower.length)], digits[rand(digits.length)], special[rand(special.length)]];
  while (chars.length < length) chars.push(all[rand(all.length)]);
  for (let i = chars.length - 1; i > 0; i--) {
    const j = rand(i + 1);
    [chars[i], chars[j]] = [chars[j], chars[i]];
  }
  return chars.join('');
}

function StatusBadge({ user }: { user: UserDto }) {
  if (!user.isActive) return <span className="badge bg-danger" style={{ cursor: 'help' }} title="This account has been deactivated and can no longer log in.">Inactive</span>;
  if (!user.lastLoginAt && user.inviteSentAt) {
    return <span className="badge bg-info text-dark" style={{ cursor: 'help' }}
                 title={`Invite emailed ${formatUtc(user.inviteSentAt, 'dd/MM/yyyy HH:mm')}. Waiting for the user to set their password and log in for the first time.`}>Invite Sent</span>;
  }
  if (!user.lastLoginAt) return <span className="badge bg-secondary" style={{ cursor: 'help' }} title="Account created but no invite email has been sent yet. Click the envelope icon to send one.">Pending</span>;
  return <span className="badge bg-success" style={{ cursor: 'help' }} title="The user has logged in successfully at least once.">Active</span>;
}

export default function UserManagement() {
  usePageTitle('User Management - IntelliMed');
  const toast = useToast();
  const [users, setUsers] = useState<UserDto[] | null>(null);
  const [roles, setRoles] = useState<RoleDto[] | null>(null);
  const [clinics, setClinics] = useState<MyClinicDto[] | null>(null);
  const [practitioners, setPractitioners] = useState<PractitionerDto[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState('');
  const [sort, setSort] = useState<{ key: SortKey; asc: boolean } | null>(null);
  const [page, setPage] = useState(1);
  const [sendingInvite, setSendingInvite] = useState<Set<string>>(new Set());

  const [dialog, setDialog] = useState<{ editing: UserDto | null; form: UserForm } | null>(null);
  const [justCreated, setJustCreated] = useState<UserDto | null>(null);
  const [dialogSaving, setDialogSaving] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [tried, setTried] = useState(false);

  const [resetFor, setResetFor] = useState<UserDto | null>(null);
  const [resetValue, setResetValue] = useState('');
  const [showReset, setShowReset] = useState(false);
  const [resetSaving, setResetSaving] = useState(false);

  const [deleteFor, setDeleteFor] = useState<UserDto | null>(null);
  const [deleteSaving, setDeleteSaving] = useState(false);

  const loadUsers = async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await AdminApi.getUsers();
      setUsers(list);
      if (!list) setError('Failed to load users. The API may be unavailable or you may not have admin permissions.');
    } finally {
      setLoading(false);
    }
  };

  useMountEffect(async () => {
    await loadUsers();
    const [r, c, p] = await Promise.all([AdminApi.getRoles(), ClinicApi.getLookup(), PractitionerApi.active()]);
    setRoles(r);
    setClinics(c);
    setPractitioners(p);
  });

  const filtered = useMemo(() => {
    let list = users ?? [];
    const term = filter.trim().toLowerCase();
    if (term) list = list.filter((u) => u.fullName?.toLowerCase().includes(term) || u.email?.toLowerCase().includes(term));
    if (sort) {
      const value = (u: UserDto): string | number => {
        switch (sort.key) {
          case 'fullName': return u.fullName ?? '';
          case 'email': return u.email ?? '';
          case 'roles': return [...u.roles].sort().join(',');
          case 'isActive': return u.isActive ? 1 : 0;
          case 'lastLoginAt': return u.lastLoginAt ?? '';
        }
      };
      list = [...list].sort((a, b) => {
        const x = value(a), y = value(b);
        const c = typeof x === 'number' && typeof y === 'number' ? x - y : String(x).localeCompare(String(y));
        return sort.asc ? c : -c;
      });
    }
    return list;
  }, [users, filter, sort]);

  // the whole list is loaded already, so "infinite scroll" here just reveals PAGE_SIZE more rows at a time
  const paged = filtered.slice(0, page * PAGE_SIZE);

  const toggleSort = (key: SortKey) => {
    setSort((s) => (s?.key === key ? { key, asc: !s.asc } : { key, asc: true }));
    setPage(1);
  };
  const sortHeader = (key: SortKey, label: string) => (
    <th role="button" className="imed-sortable" onClick={() => toggleSort(key)} aria-sort={sort?.key === key ? (sort.asc ? 'ascending' : 'descending') : undefined}>
      {label} {sort?.key === key && <span className="imed-sort-indicator">{sort.asc ? '▲' : '▼'}</span>}
    </th>
  );

  const openCreate = () => {
    setJustCreated(null);
    setTried(false);
    setShowPassword(false);
    setDialog({ editing: null, form: { firstName: '', lastName: '', email: '', password: generatePassword(), isActive: true, roles: [], clinicIds: [], practitionerId: null } });
  };
  const openEdit = (u: UserDto) => {
    setJustCreated(null);
    setTried(false);
    setDialog({
      editing: u,
      form: { firstName: u.firstName, lastName: u.lastName, email: u.email, password: '', isActive: u.isActive, roles: [...u.roles], clinicIds: [...u.clinicIds], practitionerId: u.practitionerId },
    });
  };
  const closeDialog = () => { setDialog(null); setJustCreated(null); };

  const setForm = (patch: Partial<UserForm>) => setDialog((d) => (d ? { ...d, form: { ...d.form, ...patch } } : d));
  const toggleRole = (role: string, on: boolean) => setDialog((d) => {
    if (!d) return d;
    const next = on ? [...new Set([...d.form.roles, role])] : d.form.roles.filter((r) => r !== role);
    // a Doctor/Nurse-only link must not survive both roles being removed — it would silently keep restricting visibility
    const practitionerId = next.includes('Doctor') || next.includes('Nurse') ? d.form.practitionerId : null;
    return { ...d, form: { ...d.form, roles: next, practitionerId } };
  });
  const toggleClinic = (id: number, on: boolean) => setDialog((d) => d && {
    ...d, form: { ...d.form, clinicIds: on ? [...new Set([...d.form.clinicIds, id])] : d.form.clinicIds.filter((c) => c !== id) },
  });

  const check = (f: UserForm, editing: boolean) => {
    const errors = validate(f, {
      firstName: [rules.required('FirstName')],
      lastName: [rules.required('LastName')],
      email: [rules.required('Email'), rules.email('Email')],
    });
    if (!editing && f.password.length < 8) errors.password = ['Password must be at least 8 characters.'];
    return errors;
  };
  const errors = tried && dialog ? check(dialog.form, !!dialog.editing) : {};

  const saveUser = async (e: FormEvent) => {
    e.preventDefault();
    if (!dialog) return;
    setTried(true);
    if (hasErrors(check(dialog.form, !!dialog.editing))) return;
    setDialogSaving(true);
    try {
      const f = dialog.form;
      if (dialog.editing) {
        const result = await AdminApi.updateUser(dialog.editing.id, {
          firstName: f.firstName, lastName: f.lastName, isActive: f.isActive, roles: f.roles, clinicIds: f.clinicIds, practitionerId: f.practitionerId,
        });
        if (result.success) {
          toast.success('User updated successfully.');
          setDialog(null);
          await loadUsers();
        } else {
          toast.error(result.message ?? 'Failed to update user.');
        }
      } else {
        const result = await AdminApi.createUser({
          email: f.email, password: f.password, firstName: f.firstName, lastName: f.lastName, roles: f.roles, clinicIds: f.clinicIds,
        });
        if (result.success) {
          toast.success('User created successfully.');
          setJustCreated(result.user ?? null);
          await loadUsers();
        } else {
          toast.error(result.message ?? 'Failed to create user.');
        }
      }
    } catch (err) {
      toast.error(`Error: ${err instanceof Error ? err.message : String(err)}`);
    } finally {
      setDialogSaving(false);
    }
  };

  const sendInvite = async (u: UserDto) => {
    setSendingInvite((s) => new Set(s).add(u.id));
    try {
      const result = await AdminApi.sendInvite(u.id);
      toast.show(result.message ?? (result.success ? 'Invite sent.' : 'Failed to send invite.'), !result.success);
      if (result.success) await loadUsers();
    } finally {
      setSendingInvite((s) => { const n = new Set(s); n.delete(u.id); return n; });
    }
  };

  const resetPassword = async () => {
    if (!resetFor || !resetValue.trim()) return;
    setResetSaving(true);
    try {
      const result = await AdminApi.resetPassword(resetFor.id, { newPassword: resetValue });
      if (result.success) {
        toast.success('Password reset successfully.');
        setResetFor(null);
      } else {
        toast.error(result.message ?? 'Failed to reset password.');
      }
    } finally {
      setResetSaving(false);
    }
  };

  const deactivate = async () => {
    if (!deleteFor) return;
    setDeleteSaving(true);
    try {
      const result = await AdminApi.deleteUser(deleteFor.id);
      if (result.success) {
        toast.success('User deactivated successfully.');
        setDeleteFor(null);
        await loadUsers();
      } else {
        toast.error(result.message ?? 'Failed to deactivate user.');
      }
    } finally {
      setDeleteSaving(false);
    }
  };

  const f = dialog?.form;
  const showPractitioner = !!dialog?.editing && !!f && (f.roles.includes('Doctor') || f.roles.includes('Nurse'));

  return (
    <>
      <h4 className="mb-4">User Management</h4>
      <div className="card mb-4">
        <div className="card-body d-flex align-items-center justify-content-between">
          <span className="d-flex align-items-center"><i className="bi bi-people me-2" />Total Users: {users?.length ?? 0}</span>
          <button className="btn btn-primary" onClick={openCreate}><i className="bi bi-plus-lg me-1" /> Add User</button>
        </div>
      </div>

      {error && (
        <div className="alert alert-danger d-flex align-items-center mb-4">
          <i className="bi bi-exclamation-triangle me-1" /> {error}
          <button className="btn btn-outline-danger btn-sm ms-auto" onClick={loadUsers}>Retry</button>
        </div>
      )}

      {(users !== null || loading) && !error && (
        <div className="card">
          <div className="card-header d-flex align-items-center justify-content-between">
            <h6 className="mb-0">Users</h6>
            <div className="input-group" style={{ maxWidth: 300 }}>
              <span className="input-group-text"><i className="bi bi-search" /></span>
              <input type="text" className="form-control form-control-sm" placeholder="Search users..." value={filter}
                     onChange={(e) => { setFilter(e.target.value); setPage(1); }} />
            </div>
          </div>
          <div className="imed-scroll-section">
            <table className="table table-hover table-striped table-sm mb-0">
              <thead className="table-light">
                <tr>
                  {sortHeader('fullName', 'Name')}
                  {sortHeader('email', 'Email')}
                  {sortHeader('roles', 'Roles')}
                  {sortHeader('isActive', 'Status')}
                  {sortHeader('lastLoginAt', 'Last Login')}
                  <th className="text-end">Actions</th>
                </tr>
              </thead>
              <tbody>
                {loading && !users ? <SkeletonRows columns={6} rows={6} /> : paged.map((u) => (
                  <tr key={u.id}>
                    <td>
                      <i className="bi bi-person me-2 text-muted" />
                      <a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); openEdit(u); }}>{u.fullName}</a>
                    </td>
                    <td>{u.email}</td>
                    <td>
                      {[...u.roles].sort().map((r) => <span key={r} className={`badge ${roleBadgeClass(r)} me-1 mb-1`}>{r}</span>)}
                      {u.roles.length === 0 && <span className="badge bg-secondary">No Roles</span>}
                    </td>
                    <td><StatusBadge user={u} /></td>
                    <td className="text-nowrap">{u.lastLoginAt ? formatDate(u.lastLoginAt, 'yyyy-MM-dd HH:mm') : 'Never'}</td>
                    <td className="text-end text-nowrap">
                      <button className="btn btn-sm btn-outline-success me-1" title="Send Invite" aria-label={`Email an invite to ${u.fullName}`}
                              disabled={sendingInvite.has(u.id)} onClick={() => sendInvite(u)}>
                        {sendingInvite.has(u.id) ? <span className="spinner-border spinner-border-sm" /> : <i className="bi bi-envelope" />}
                      </button>
                      <button className="btn btn-sm btn-outline-warning me-1" title="Reset Password" aria-label={`Set a new password for ${u.fullName}`}
                              onClick={() => { setResetFor(u); setResetValue(''); setShowReset(false); }}><i className="bi bi-key" /></button>
                      <button className="btn btn-sm btn-outline-danger" title="Deactivate" aria-label={`Deactivate ${u.fullName}`}
                              onClick={() => setDeleteFor(u)}><i className="bi bi-trash" /></button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {filtered.length > 0 && (
              <LoadMoreSentinel onVisible={() => setPage((p) => p + 1)} loading={false} hasMore={paged.length < filtered.length}
                                shown={paged.length} total={filtered.length} noun="users" />
            )}
          </div>
          {!loading && filtered.length === 0 && (
            <div className="card-body"><EmptyState message="No users found matching your search." icon="bi-search" compact={false} /></div>
          )}
        </div>
      )}

      {dialog && f && (
        <Modal title={dialog.editing ? 'Edit User' : 'Create New User'} titleTag="h5" headerClassName="" onClose={closeDialog}
               footer={justCreated ? (
                 <>
                   <button type="button" className="btn btn-secondary" onClick={closeDialog}>Done</button>
                   <button type="button" className="btn btn-success" disabled={sendingInvite.has(justCreated.id)}
                           onClick={async () => { await sendInvite(justCreated); setDialog(null); }}>
                     {sendingInvite.has(justCreated.id) ? <><span className="spinner-border spinner-border-sm me-2" /><span>Sending...</span></> : <span><i className="bi bi-envelope me-1" />Send Invite</span>}
                   </button>
                 </>
               ) : (
                 <>
                   <button type="button" className="btn btn-secondary" onClick={closeDialog}>Cancel</button>
                   <button type="submit" form="userDialogForm" className="btn btn-primary" disabled={dialogSaving}>
                     {dialogSaving ? <><span className="spinner-border spinner-border-sm me-2" /><span>Saving...</span></> : <span>{dialog.editing ? 'Save Changes' : 'Create User'}</span>}
                   </button>
                 </>
               )}>
          {justCreated ? (
            <div className="text-center py-3">
              <i className="bi bi-check-circle-fill text-success" style={{ fontSize: '2.5rem' }} />
              <h6 className="mt-3 mb-1">{justCreated.fullName} created successfully</h6>
              <p className="text-muted small">Send them an invite email now, or do it later from the Actions column.</p>
            </div>
          ) : (
            <form id="userDialogForm" onSubmit={saveUser} noValidate>
              <ValidationSummary errors={errors} className="text-danger small mb-3" />
              <div className="mb-3">
                <label className="form-label">First Name <span className="text-danger">*</span></label>
                <input className="form-control" value={f.firstName} onChange={(e) => setForm({ firstName: e.target.value })} />
                <FieldMessage messages={errors.firstName} />
              </div>
              <div className="mb-3">
                <label className="form-label">Last Name <span className="text-danger">*</span></label>
                <input className="form-control" value={f.lastName} onChange={(e) => setForm({ lastName: e.target.value })} />
                <FieldMessage messages={errors.lastName} />
              </div>
              {!dialog.editing && (
                <>
                  <div className="mb-3">
                    <label className="form-label">Email Address <span className="text-danger">*</span></label>
                    <input className="form-control" type="email" autoComplete="off" value={f.email} onChange={(e) => setForm({ email: e.target.value })} />
                    <FieldMessage messages={errors.email} />
                  </div>
                  <div className="mb-3">
                    <label className="form-label">Password <span className="text-danger">*</span></label>
                    <div className="input-group">
                      <input type={showPassword ? 'text' : 'password'} className="form-control" readOnly value={f.password} autoComplete="new-password" />
                      <button type="button" className="btn btn-outline-secondary" aria-label={showPassword ? 'Hide password' : 'Show password'} onClick={() => setShowPassword((s) => !s)}>
                        <i className={`bi ${showPassword ? 'bi-eye-slash' : 'bi-eye'}`} />
                      </button>
                    </div>
                    <div className="form-text">Auto-generated. Use "Send Invite" after creating the user to email it to them.</div>
                    <FieldMessage messages={errors.password} />
                  </div>
                </>
              )}

              <h6 className="mt-4 mb-2">Role Assignment</h6>
              <div className="card bg-light">
                <div className="card-body">
                  {roles === null ? <LoadingState text="Loading roles..." /> : [...roles].sort((a, b) => a.name.localeCompare(b.name)).map((r) => (
                    <div key={r.name} className="form-check mb-2">
                      <input className="form-check-input" type="checkbox" id={`role_${r.name}`} checked={f.roles.includes(r.name)}
                             onChange={(e) => toggleRole(r.name, e.target.checked)} />
                      <label className="form-check-label" htmlFor={`role_${r.name}`}>{r.name}</label>
                      {r.description && <i className="bi bi-info-circle ms-1 text-muted" title={r.description} style={{ cursor: 'help' }} />}
                    </div>
                  ))}
                </div>
              </div>

              <h6 className="mt-4 mb-2">Clinic Assignment</h6>
              <div className="card bg-light">
                <div className="card-body">
                  {clinics === null ? <LoadingState text="Loading clinics..." /> : clinics.length === 0 ? (
                    <span className="text-muted small">No clinics configured yet.</span>
                  ) : [...clinics].sort((a, b) => a.name.localeCompare(b.name)).map((c) => (
                    <div key={c.id} className="form-check mb-2">
                      <input className="form-check-input" type="checkbox" id={`clinic_${c.id}`} checked={f.clinicIds.includes(c.id)}
                             onChange={(e) => toggleClinic(c.id, e.target.checked)} />
                      <label className="form-check-label" htmlFor={`clinic_${c.id}`}>{c.name}</label>
                    </div>
                  ))}
                </div>
              </div>

              {showPractitioner && (
                <>
                  <h6 className="mt-4 mb-2">Linked Practitioner</h6>
                  <div className="card bg-light">
                    <div className="card-body">
                      <p className="small text-muted mb-2">
                        Links this login to a Practitioner record, which drives per-provider patient visibility — a Doctor/Nurse only sees
                        patients assigned to them unless linked here.
                      </p>
                      {practitioners === null ? <LoadingState text="Loading practitioners..." /> : (
                        <select className="form-select" aria-label="Linked practitioner" value={f.practitionerId ?? ''}
                                onChange={(e) => setForm({ practitionerId: e.target.value ? Number(e.target.value) : null })}>
                          <option value="">— Not linked —</option>
                          {[...practitioners].sort((a, b) => a.lastName.localeCompare(b.lastName)).map((p) => (
                            <option key={p.id} value={p.id}>{[p.title, p.firstName, p.lastName].filter(Boolean).join(' ')}</option>
                          ))}
                        </select>
                      )}
                    </div>
                  </div>
                </>
              )}

              {dialog.editing && (
                <div className="form-check form-switch mt-4">
                  <input className="form-check-input" type="checkbox" role="switch" id="activeSwitch" checked={f.isActive}
                         onChange={(e) => setForm({ isActive: e.target.checked })} />
                  <label className="form-check-label" htmlFor="activeSwitch">Active Account</label>
                </div>
              )}
            </form>
          )}
        </Modal>
      )}

      {resetFor && (
        <Modal title="Reset Password" titleTag="h5" headerClassName="" scrollable={false} onClose={() => setResetFor(null)}
               footer={<>
                 <button type="button" className="btn btn-secondary" onClick={() => setResetFor(null)}>Cancel</button>
                 <button type="button" className="btn btn-warning" onClick={resetPassword} disabled={!resetValue.trim() || resetSaving}>
                   {resetSaving ? <><span className="spinner-border spinner-border-sm me-2" /><span>Resetting...</span></> : <span>Reset Password</span>}
                 </button>
               </>}>
          <p>Reset password for <strong>{resetFor.fullName}</strong> ({resetFor.email})</p>
          <div className="mb-3">
            <label className="form-label">New Password <span className="text-danger">*</span></label>
            <div className="input-group">
              <input type={showReset ? 'text' : 'password'} className="form-control" autoComplete="new-password" value={resetValue}
                     onChange={(e) => setResetValue(e.target.value)} />
              <button type="button" className="btn btn-outline-secondary" aria-label={showReset ? 'Hide password' : 'Show password'} onClick={() => setShowReset((s) => !s)}>
                <i className={`bi ${showReset ? 'bi-eye-slash' : 'bi-eye'}`} />
              </button>
            </div>
            <div className="form-text">Min 8 chars: upper, lower, digit, special</div>
          </div>
        </Modal>
      )}

      {deleteFor && (
        <Modal title="Deactivate User" titleTag="h5" headerClassName="" scrollable={false} onClose={() => setDeleteFor(null)}
               footer={<>
                 <button type="button" className="btn btn-secondary" onClick={() => setDeleteFor(null)}>Cancel</button>
                 <button type="button" className="btn btn-danger" onClick={deactivate} disabled={deleteSaving}>
                   {deleteSaving ? <><span className="spinner-border spinner-border-sm me-2" /><span>Deactivating...</span></> : <span>Deactivate User</span>}
                 </button>
               </>}>
          <div className="alert alert-warning d-flex align-items-center mb-3">
            <i className="bi bi-exclamation-triangle me-1" /> This will deactivate the user account. The user will no longer be able to log in. No data is permanently deleted.
          </div>
          <p>Are you sure you want to deactivate <strong>{deleteFor.fullName}</strong> ({deleteFor.email})?</p>
        </Modal>
      )}
    </>
  );
}
