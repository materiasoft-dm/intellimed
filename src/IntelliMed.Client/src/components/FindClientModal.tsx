import { useRef, useState } from 'react';
import { ClientApi } from '@/api/clients';
import { GenderEnum, type ClientDto } from '@/api/types.gen';
import { formatDate } from '@/lib/dates';
import { DateInput, NumberSelect } from './Inputs';
import { Modal } from './Modal';
import { LoadingState } from './States';

const blank = { surname: '', givenName: '', medicareNumber: '', dvaNumber: '', dob: null as string | null, gender: null as GenderEnum | null, fileNumber: '', mobilePhone: '' };
const nullIfEmpty = (v: string) => (v.trim() ? v : null);

/** Multi-field client search (surname, given, Medicare#, DVA#, DOB, gender, file#, mobile); Enter searches. */
export function FindClientModal({ title = 'Find Client', onSelect, onClose }: {
  title?: string;
  onSelect: (client: ClientDto) => void;
  onClose: () => void;
}) {
  const [f, setF] = useState(blank);
  const [results, setResults] = useState<ClientDto[]>([]);
  const [hasSearched, setHasSearched] = useState(false);
  const [searching, setSearching] = useState(false);
  const seq = useRef(0);

  const search = async () => {
    const mine = ++seq.current;
    setHasSearched(true);
    setSearching(true);
    try {
      const result = await ClientApi.search({
        pageSize: 20, surname: nullIfEmpty(f.surname), givenName: nullIfEmpty(f.givenName), medicareNumber: nullIfEmpty(f.medicareNumber),
        dvaNumber: nullIfEmpty(f.dvaNumber), dobFrom: f.dob, dobTo: f.dob, gender: f.gender, fileNumber: nullIfEmpty(f.fileNumber),
        mobilePhone: nullIfEmpty(f.mobilePhone),
      });
      if (mine === seq.current) setResults(result.items);
    } finally {
      if (mine === seq.current) setSearching(false);
    }
  };

  const text = (key: keyof typeof blank, label: string) => (
    <div className="col-6 col-md-3">
      <label className="form-label small mb-0">{label}</label>
      <input className="form-control form-control-sm" value={f[key] as string} onChange={(e) => setF({ ...f, [key]: e.target.value })} />
    </div>
  );

  return (
    <Modal title={title} size="lg" scrollable={false} onClose={onClose}
           footer={<button type="button" className="btn btn-sm btn-secondary" onClick={onClose}>Cancel</button>}>
      <div className="row g-2" onKeyDown={(e) => { if (e.key === 'Enter') search(); }}>
        {text('surname', 'Surname')}
        {text('givenName', 'Given Name')}
        {text('medicareNumber', 'Medicare#')}
        {text('dvaNumber', 'DVA#')}
        <div className="col-6 col-md-3">
          <label className="form-label small mb-0">D.O.B.</label>
          <DateInput className="form-control form-control-sm" value={f.dob} onChange={(v) => setF({ ...f, dob: v })} />
        </div>
        <div className="col-6 col-md-3">
          <label className="form-label small mb-0">Gender</label>
          <NumberSelect enumType={GenderEnum} className="form-select form-select-sm" value={f.gender} onChange={(v) => setF({ ...f, gender: v as GenderEnum | null })}>
            <option value="">Any</option>
            <option value={GenderEnum.Male}>Male</option>
            <option value={GenderEnum.Female}>Female</option>
            <option value={GenderEnum.Other}>Other</option>
          </NumberSelect>
        </div>
        {text('fileNumber', 'File#')}
        {text('mobilePhone', 'Mobile Phone')}
      </div>
      <div className="d-flex justify-content-end gap-2 mt-2 mb-2">
        <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => { setF(blank); setResults([]); setHasSearched(false); }}>Clear</button>
        <button type="button" className="btn btn-sm btn-primary" onClick={search}>Search</button>
      </div>
      <div style={{ maxHeight: 300, overflowY: 'auto' }}>
        {searching ? (
          <LoadingState text="Searching..." />
        ) : !hasSearched ? (
          <div className="text-muted small">Enter search criteria and click Search (or press Enter).</div>
        ) : results.length === 0 ? (
          <div className="text-muted small">No matches.</div>
        ) : (
          <table className="table table-sm table-hover mb-0">
            <thead><tr><th>Name</th><th>D.O.B.</th><th>Medicare#</th><th>DVA#</th><th>File#</th><th>Mobile</th></tr></thead>
            <tbody>
              {results.map((c) => (
                <tr key={c.id} style={{ cursor: 'pointer' }} onClick={() => { onSelect(c); onClose(); }}>
                  <td>{c.fullName}</td>
                  <td>{formatDate(c.dateOfBirth, 'dd/MM/yyyy')}</td>
                  <td>{c.medicareNumber}</td>
                  <td>{c.dvaNumber}</td>
                  <td>{c.fileNumber}</td>
                  <td>{c.mobilePhone}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </Modal>
  );
}
