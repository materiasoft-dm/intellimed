import { useCallback, useEffect, useState } from 'react';
import { ClinicApi } from '@/api/admin';
import { AddressBookApi } from '@/api/clients';
import {
  ClinicType, defaultSaveAddressBookContactDto, GenderEnum, type AddressBookContactDto, type MyClinicDto,
  type SaveAddressBookContactDto,
} from '@/api/types.gen';
import { NumberSelect } from '@/components/Inputs';
import { Modal } from '@/components/Modal';
import { EmptyState, SkeletonRows } from '@/components/States';
import { useToast } from '@/components/Toast';
import { joinNonBlank, plural } from '@/lib/format';
import { useDebounced, usePageTitle } from '@/lib/hooks';
import { LoadMoreSentinel, useInfiniteList } from '@/lib/infinite';

const emptyForm = defaultSaveAddressBookContactDto;

/** The editable part of a contact: every field the save DTO has, copied from the loaded contact. */
const toForm = (c: AddressBookContactDto): SaveAddressBookContactDto => {
  const form = emptyForm();
  for (const key of Object.keys(form) as (keyof SaveAddressBookContactDto)[]) {
    (form as unknown as Record<string, unknown>)[key] = c[key as keyof AddressBookContactDto];
  }
  return form;
};

function Section({ title }: { title: string }) {
  return <div className="col-12 imed-section-label mt-2 mb-0">{title}</div>;
}

/** Text input bound to a nullable string field: blank becomes null, as Blazor's InputText did. */
function Field({ label, value, onChange, placeholder, col, help, area }: {
  label: string; value: string | null; onChange: (v: string | null) => void; placeholder?: string; col: string; help?: string; area?: boolean;
}) {
  return (
    <div className={col}>
      <label className="form-label small mb-0">{label}</label>
      {area
        ? <textarea className="form-control form-control-sm" rows={2} value={value ?? ''} onChange={(e) => onChange(e.target.value || null)} />
        : <input className="form-control form-control-sm" placeholder={placeholder} value={value ?? ''} onChange={(e) => onChange(e.target.value || null)} />}
      {help && <div className="form-text small">{help}</div>}
    </div>
  );
}

export default function AddressBook() {
  usePageTitle('Address Book - IntelliMed');
  const toast = useToast();
  const [query, setQuery] = useState('');
  const [includeArchived, setIncludeArchived] = useState(false);
  const [clinics, setClinics] = useState<MyClinicDto[]>([]);
  const debouncedQuery = useDebounced(query, 200);

  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<SaveAddressBookContactDto | null>(null);
  const [saving, setSaving] = useState(false);

  // Infinite scroll: the API caps a page at 100, and this used to fetch one page of 100 and stop — the
  // count said more existed, but there was no way to reach them.
  const list = useInfiniteList<AddressBookContactDto>(
    (page, pageSize) => AddressBookApi.search({ query: debouncedQuery, includeArchived, page, pageSize }), 50);
  const { reset } = list;

  useEffect(() => { ClinicApi.getLookup().then(setClinics); }, []);
  useEffect(() => { reset(); }, [debouncedQuery, includeArchived, reset]);

  const openNew = () => {
    setForm(emptyForm());
    setEditingId(null);
  };

  const openEdit = (c: AddressBookContactDto) => {
    setForm(toForm(c));
    setEditingId(c.id);
  };

  const set = useCallback(<K extends keyof SaveAddressBookContactDto>(key: K) => (value: SaveAddressBookContactDto[K]) =>
    setForm((f) => (f ? { ...f, [key]: value } : f)), []);

  const save = async () => {
    if (!form) return;
    setSaving(true);
    const ok = editingId != null ? await AddressBookApi.update(editingId, form) : (await AddressBookApi.create(form)) != null;
    setSaving(false);
    toast.show(ok ? (editingId != null ? 'Contact updated.' : 'Contact added.') : 'Failed to save contact.', !ok);
    if (ok) {
      setForm(null);
      reset();
    }
  };

  const archive = async (c: AddressBookContactDto) => {
    const ok = await AddressBookApi.archive(c.id);
    toast.show(ok ? `'${c.fullName}' archived.` : 'Failed to archive contact.', !ok);
    if (ok) reset();
  };

  return (
    <>
      <h4 className="mb-1">Address Book</h4>
      <p className="text-muted small mb-3">
        GPs, specialists and other providers the practice refers to and receives referrals from. Organisations
        (employers, companies that pay for their staff) are clients of type Organisation; a contact's workplace is
        a clinic or hospital from Clinic Manager.
      </p>

      <div className="card">
        <div className="card-header d-flex flex-wrap align-items-center gap-2 py-2">
          <div className="input-group input-group-sm" style={{ maxWidth: 360 }}>
            <span className="input-group-text bg-white"><i className="bi bi-search" /></span>
            <input className="form-control" placeholder="Search name, provider number, speciality..."
                   value={query} onChange={(e) => setQuery(e.target.value)} />
            {query && (
              <button type="button" className="btn btn-outline-secondary" title="Clear search" aria-label="Clear search" onClick={() => setQuery('')}>
                <i className="bi bi-x-lg" />
              </button>
            )}
          </div>
          <div className="form-check form-check-inline small mb-0">
            <input className="form-check-input" type="checkbox" id="chkShowArchived" checked={includeArchived}
                   onChange={(e) => setIncludeArchived(e.target.checked)} />
            <label className="form-check-label" htmlFor="chkShowArchived">Show archived</label>
          </div>
          <span className="small text-muted ms-auto">{list.loading ? ' ' : plural(list.total, 'contact')}</span>
          <button className="btn btn-sm btn-primary" onClick={openNew}><i className="bi bi-plus-lg me-1" /> New Contact</button>
        </div>

        {!list.loading && list.items.length === 0 ? (
          <div className="card-body">
            <EmptyState message={query.trim() ? 'No contacts match your search.' : 'No contacts found.'} icon="bi-person-lines-fill" />
          </div>
        ) : (
          <div className="imed-table-scroll" style={{ maxHeight: 'calc(100vh - 260px)' }}>
            <table className="table table-sm table-hover mb-0">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Profession / Speciality</th>
                  <th>Provider#</th>
                  <th>Workplace</th>
                  <th>Phone</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {list.loading ? <SkeletonRows columns={6} rows={8} /> : list.items.map((c) => (
                  <tr key={c.id} className={c.isArchived ? 'text-muted' : ''}>
                    <td>
                      <a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); openEdit(c); }}>{c.fullName}</a>
                    </td>
                    <td>{joinNonBlank(' · ', c.profession, c.speciality)}</td>
                    <td>{c.providerNumber}</td>
                    <td>
                      {c.clinicName}
                      {c.clinicType === ClinicType.Hospital && <span className="badge bg-secondary ms-1">Hospital</span>}
                    </td>
                    <td>{c.phone}</td>
                    <td className="text-end">
                      {c.isArchived
                        ? <span className="badge bg-secondary">Archived</span>
                        : <button className="btn btn-sm btn-outline-danger" title="Archive" aria-label={`Archive ${c.fullName}`} onClick={() => archive(c)}><i className="bi bi-archive" /></button>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {!list.loading && (
              <LoadMoreSentinel onVisible={list.loadMore} loading={list.loadingMore} hasMore={list.hasMore}
                                shown={list.items.length} total={list.total} noun="contacts" />
            )}
          </div>
        )}
      </div>

      {form && (
        <Modal title={editingId != null ? 'Edit Contact' : 'New Contact'} size="lg" onClose={() => setForm(null)}
               footer={<>
                 <button type="button" className="btn btn-sm btn-secondary" onClick={() => setForm(null)}>Cancel</button>
                 <button type="button" className="btn btn-sm btn-primary" disabled={saving || !form.lastName.trim()} onClick={save}>
                   {saving && <span className="spinner-border spinner-border-sm me-1" aria-hidden="true" />}Save
                 </button>
               </>}>
          <div className="row g-2">
            <Section title="Person" />
            <Field col="col-md-2" label="Title" value={form.title} onChange={set('title')} />
            <Field col="col-md-5" label="First Name" value={form.firstName} onChange={set('firstName')} />
            <div className="col-md-5">
              <label className="form-label small mb-0">Last Name</label>
              <input className="form-control form-control-sm" value={form.lastName} onChange={(e) => set('lastName')(e.target.value)} />
            </div>
            <Field col="col-md-5" label="Preferred Name" value={form.preferredName} onChange={set('preferredName')} />
            <div className="col-md-3">
              <label className="form-label small mb-0">Gender</label>
              <NumberSelect enumType={GenderEnum} className="form-select form-select-sm" value={form.gender} onChange={(v) => set('gender')(v as GenderEnum | null)}>
                <option value="">—</option>
                <option value={GenderEnum.Male}>Male</option>
                <option value={GenderEnum.Female}>Female</option>
                <option value={GenderEnum.Other}>Other</option>
                <option value={GenderEnum.Unspecified}>Unspecified</option>
              </NumberSelect>
            </div>

            <Section title="Practice" />
            <Field col="col-md-4" label="Profession" placeholder="e.g. GP, Specialist" value={form.profession} onChange={set('profession')} />
            <Field col="col-md-4" label="Speciality" value={form.speciality} onChange={set('speciality')} />
            <Field col="col-md-4" label="Provider Number" value={form.providerNumber} onChange={set('providerNumber')} />
            <Field col="col-md-4" label="HPI-I" placeholder="16 digits" value={form.hpii} onChange={set('hpii')} />
            <Field col="col-md-4" label="Provider Type Code" help="HI Service type code for the HPI-I" value={form.providerTypeCode} onChange={set('providerTypeCode')} />
            <Field col="col-md-2" label="ABN" value={form.abn} onChange={set('abn')} />
            <Field col="col-md-2" label="ACN" value={form.acn} onChange={set('acn')} />
            <div className="col-md-6">
              <label className="form-label small mb-0">Workplace</label>
              <select className="form-select form-select-sm" value={form.clinicId ?? ''}
                      onChange={(e) => set('clinicId')(e.target.value ? Number(e.target.value) : null)}>
                <option value="">— None —</option>
                {clinics.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}{c.type === ClinicType.Hospital ? ' (Hospital)' : ''}</option>
                ))}
              </select>
              <div className="form-text small">Add a clinic or hospital in Clinic Manager first if it isn't listed.</div>
            </div>
            <Field col="col-md-6" label="Attention" help="Who correspondence is marked for, e.g. a practice manager" value={form.attention} onChange={set('attention')} />

            <Section title="Contact" />
            <Field col="col-md-3" label="Phone" value={form.phone} onChange={set('phone')} />
            <Field col="col-md-3" label="Mobile" value={form.mobile} onChange={set('mobile')} />
            <Field col="col-md-3" label="Fax" value={form.fax} onChange={set('fax')} />
            <Field col="col-md-9" label="Email" value={form.email} onChange={set('email')} />
            <Field col="col-md-6" label="Address" value={form.address} onChange={set('address')} />
            <Field col="col-md-6" label="Address Line 2" value={form.addressLine2} onChange={set('addressLine2')} />
            <Field col="col-md-6" label="Suburb" value={form.suburb} onChange={set('suburb')} />
            <Field col="col-md-3" label="State" value={form.state} onChange={set('state')} />
            <Field col="col-md-3" label="Postcode" value={form.postcode} onChange={set('postcode')} />

            <Section title="Secure messaging" />
            <Field col="col-md-5" label="Messaging Id" placeholder="e.g. ReferralNet / HealthLink id" value={form.messagingId} onChange={set('messagingId')} />
            <Field col="col-md-4" label="Network" value={form.messagingNetwork} onChange={set('messagingNetwork')} />
            <div className="col-md-3 d-flex align-items-end">
              <div className="form-check mb-1">
                <input className="form-check-input" type="checkbox" id="chkAcceptsSecure" checked={form.acceptsSecureMessaging}
                       onChange={(e) => set('acceptsSecureMessaging')(e.target.checked)} />
                <label className="form-check-label small" htmlFor="chkAcceptsSecure">Accepts secure messages</label>
              </div>
            </div>

            <Section title="Other" />
            <Field col="col-md-4" label="External Id" value={form.externalId} onChange={set('externalId')} />
            <Field col="col-12" label="Notes" value={form.notes} onChange={set('notes')} area />
          </div>
        </Modal>
      )}
    </>
  );
}
