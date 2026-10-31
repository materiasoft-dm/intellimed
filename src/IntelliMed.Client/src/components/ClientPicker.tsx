import { useCallback, useState } from 'react';
import { ClientApi } from '@/api/clients';
import type { ClientDto, ClientTypeEnum } from '@/api/types.gen';
import { formatDate } from '@/lib/dates';
import { useLatestSearch } from '@/lib/hooks';
import { Modal } from './Modal';
import { LoadingState } from './States';

/**
 * The quick "Find Client" picker (search-as-you-type by name), used to choose a client for a filter or a
 * payer. For the full multi-field search use FindClientModal.
 */
export function ClientPicker({ title = 'Find Client', onSelect, onClose, clientType, pageSize = 10, placeholder = 'Search by name...', describe }: {
  title?: string;
  onSelect: (client: ClientDto) => void;
  onClose: () => void;
  clientType?: ClientTypeEnum;
  pageSize?: number;
  placeholder?: string;
  /** Extra text after the name; defaults to the date of birth. */
  describe?: (c: ClientDto) => string;
}) {
  const [term, setTerm] = useState('');
  const search = useCallback(
    async (q: string) => (await ClientApi.search({ query: q, pageSize, clientType: clientType ?? null })).items,
    [pageSize, clientType]);
  const { results, searching, run } = useLatestSearch<ClientDto[]>(search, []);

  return (
    <Modal title={title} onClose={onClose} scrollable={false}
           footer={<button type="button" className="btn btn-sm btn-secondary" onClick={onClose}>Cancel</button>}>
      <input className="form-control form-control-sm mb-2" placeholder={placeholder} autoFocus value={term}
             onChange={(e) => { setTerm(e.target.value); run(e.target.value); }} />
      <div style={{ maxHeight: 300, overflowY: 'auto' }}>
        {searching ? (
          <LoadingState text="Searching..." />
        ) : results.length === 0 ? (
          <div className="text-muted small">{term.trim() ? 'No matches.' : 'Start typing a name to search.'}</div>
        ) : (
          <ul className="list-group">
            {results.map((c) => (
              <li key={c.id} className="list-group-item list-group-item-action" style={{ cursor: 'pointer' }} role="button" tabIndex={0}
                  onClick={() => onSelect(c)} onKeyDown={(e) => { if (e.key === 'Enter') onSelect(c); }}>
                {c.fullName}{' '}
                <span className="text-muted small">({describe ? describe(c) : formatDate(c.dateOfBirth, 'dd/MM/yyyy')})</span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Modal>
  );
}
