import { useCallback, useState } from 'react';
import { AddressBookApi, ClientApi } from '@/api/clients';
import {
  ClientTypeEnum, defaultCreateClientDto, defaultSaveAddressBookContactDto, type AddressBookContactDto, type ClientDto,
  type SaveAddressBookContactDto,
} from '@/api/types.gen';
import { joinNonBlank } from '@/lib/format';
import { useLatestSearch } from '@/lib/hooks';
import { TextInput } from './Inputs';
import { Modal } from './Modal';
import { LoadingState } from './States';

const blankContact = (lastName = ''): SaveAddressBookContactDto => ({ ...defaultSaveAddressBookContactDto(), lastName });

/**
 * Search the address book and pick a person (e.g. a referring provider), with a quick-add so a missing
 * referrer can be created without leaving the client record.
 */
export function AddressBookPicker({ title = 'Find Contact', onSelect, onClose }: {
  title?: string;
  onSelect: (contact: AddressBookContactDto) => void;
  onClose: () => void;
}) {
  const [query, setQuery] = useState('');
  const [adding, setAdding] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [draft, setDraft] = useState<SaveAddressBookContactDto>(blankContact);
  const search = useCallback(async (q: string) => (await AddressBookApi.search({ query: q, pageSize: 15 })).items, []);
  const { results, searching, run } = useLatestSearch<AddressBookContactDto[]>(search, []);

  const addAndSelect = async () => {
    setSaving(true);
    setError(null);
    const id = await AddressBookApi.create(draft);
    const created = id != null ? await AddressBookApi.getById(id) : null;
    setSaving(false);
    if (created) onSelect(created);
    else setError("Couldn't add the contact.");
  };

  return (
    <Modal title={title} size="lg" scrollable={false} onClose={onClose}
           footer={!adding ? (
             <>
               <button type="button" className="btn btn-sm btn-outline-primary me-auto" onClick={() => { setDraft(blankContact(query.trim())); setError(null); setAdding(true); }}>+ New Contact</button>
               <button type="button" className="btn btn-sm btn-secondary" onClick={onClose}>Cancel</button>
             </>
           ) : (
             <>
               <button type="button" className="btn btn-sm btn-secondary" onClick={() => setAdding(false)}>Back</button>
               <button type="button" className="btn btn-sm btn-primary" disabled={saving || !draft.lastName.trim()} onClick={addAndSelect}>
                 {saving && <span className="spinner-border spinner-border-sm me-1" />}Add &amp; Select
               </button>
             </>
           )}>
      {!adding ? (
        <>
          <input className="form-control form-control-sm mb-2" placeholder="Search by name, provider number or speciality..." autoFocus value={query}
                 onChange={(e) => { setQuery(e.target.value); run(e.target.value); }} />
          <div style={{ maxHeight: 320, overflowY: 'auto' }}>
            {searching ? <LoadingState text="Searching..." /> : results.length === 0 ? (
              <div className="text-muted small">{query.trim() ? 'No matching contacts.' : 'Type to search the address book.'}</div>
            ) : (
              <ul className="list-group">
                {results.map((c) => (
                  <li key={c.id} className="list-group-item list-group-item-action address-book-result" style={{ cursor: 'pointer' }} role="button" tabIndex={0}
                      onClick={() => onSelect(c)} onKeyDown={(e) => { if (e.key === 'Enter') onSelect(c); }}>
                    <div>
                      <span className="fw-semibold">{c.fullName}</span>{' '}
                      {/* the provider number is what a referral is claimed against, so it sits beside the name */}
                      {c.providerNumber?.trim()
                        ? <span className="imed-provider-no">({c.providerNumber})</span>
                        : <span className="small text-muted fst-italic">(no provider #)</span>}
                    </div>
                    {joinNonBlank(' · ', c.speciality ?? c.profession, c.clinicName) && (
                      <div className="small text-muted">{joinNonBlank(' · ', c.speciality ?? c.profession, c.clinicName)}</div>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </div>
        </>
      ) : (
        <>
          <div className="small text-muted mb-2">New address book contact</div>
          <div className="row g-2">
            <div className="col-md-2"><label className="form-label small mb-0">Title</label><TextInput className="form-control form-control-sm" value={draft.title} onChange={(v) => setDraft({ ...draft, title: v })} /></div>
            <div className="col-md-5"><label className="form-label small mb-0">First Name</label><TextInput className="form-control form-control-sm" value={draft.firstName} onChange={(v) => setDraft({ ...draft, firstName: v })} /></div>
            <div className="col-md-5"><label className="form-label small mb-0">Last Name</label><input className="form-control form-control-sm" value={draft.lastName} onChange={(e) => setDraft({ ...draft, lastName: e.target.value })} /></div>
            <div className="col-md-4"><label className="form-label small mb-0">Provider Number</label><TextInput className="form-control form-control-sm" value={draft.providerNumber} onChange={(v) => setDraft({ ...draft, providerNumber: v })} /></div>
            <div className="col-md-4"><label className="form-label small mb-0">Profession</label><TextInput className="form-control form-control-sm" placeholder="e.g. GP, Specialist" value={draft.profession} onChange={(v) => setDraft({ ...draft, profession: v })} /></div>
            <div className="col-md-4"><label className="form-label small mb-0">Speciality</label><TextInput className="form-control form-control-sm" value={draft.speciality} onChange={(v) => setDraft({ ...draft, speciality: v })} /></div>
          </div>
          {error && <div className="small text-danger mt-2">{error}</div>}
        </>
      )}
    </Modal>
  );
}

/** Search organisation clients (e.g. a patient's employer), with a quick-add that creates one by name. */
export function OrganisationPicker({ title = 'Find Organisation', onSelect, onClose }: {
  title?: string;
  onSelect: (org: { id: number; name: string }) => void;
  onClose: () => void;
}) {
  const [query, setQuery] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const search = useCallback(async (q: string) => (await ClientApi.search({ query: q, clientType: ClientTypeEnum.Organisation, pageSize: 15 })).items, []);
  const { results, searching, run } = useLatestSearch<ClientDto[]>(search, []);

  const addAndSelect = async () => {
    setSaving(true);
    setError(null);
    const name = query.trim();
    const id = await ClientApi.create({ ...defaultCreateClientDto(), type: ClientTypeEnum.Organisation, lastName: name, dateOfBirth: null });
    setSaving(false);
    if (id != null) onSelect({ id, name });
    else setError("Couldn't add the organisation.");
  };

  return (
    <Modal title={title} scrollable={false} onClose={onClose}
           footer={<>
             <button type="button" className="btn btn-sm btn-outline-primary me-auto text-truncate" style={{ maxWidth: '70%' }} disabled={saving || !query.trim()} onClick={addAndSelect}>
               + Add "{query.trim()}" as a new organisation
             </button>
             <button type="button" className="btn btn-sm btn-secondary" onClick={onClose}>Cancel</button>
           </>}>
      <input className="form-control form-control-sm mb-2" placeholder="Search organisations by name..." autoFocus value={query}
             onChange={(e) => { setQuery(e.target.value); run(e.target.value); }} />
      <div style={{ maxHeight: 300, overflowY: 'auto' }}>
        {searching ? <LoadingState text="Searching..." /> : results.length === 0 ? (
          <div className="text-muted small">{query.trim() ? 'No matching organisations.' : 'Type to search organisations.'}</div>
        ) : (
          <ul className="list-group">
            {results.map((o) => (
              <li key={o.id} className="list-group-item list-group-item-action organisation-result" style={{ cursor: 'pointer' }} role="button" tabIndex={0}
                  onClick={() => onSelect({ id: o.id, name: o.lastName })} onKeyDown={(e) => { if (e.key === 'Enter') onSelect({ id: o.id, name: o.lastName }); }}>
                {o.lastName}{o.suburb?.trim() && <span className="text-muted small"> — {o.suburb}</span>}
              </li>
            ))}
          </ul>
        )}
      </div>
      {error && <div className="small text-danger mt-2">{error}</div>}
    </Modal>
  );
}
