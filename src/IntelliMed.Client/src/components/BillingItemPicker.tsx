import { useCallback, useState } from 'react';
import { BillingItemApi } from '@/api/billing';
import type { BillingItemDto } from '@/api/types.gen';
import { money } from '@/lib/format';
import { useLatestSearch } from '@/lib/hooks';
import { Modal } from './Modal';
import { LoadingState } from './States';

/** "Find Billing Item": search the MBS-derived catalogue by item number or description. */
export function BillingItemPicker({ onSelect, onClose, title = 'Find Billing Item' }: {
  onSelect: (item: BillingItemDto) => void;
  onClose: () => void;
  title?: string;
}) {
  const [term, setTerm] = useState('');
  const search = useCallback((q: string) => BillingItemApi.search(q), []);
  const { results, searching, run } = useLatestSearch<BillingItemDto[]>(search, []);

  return (
    <Modal title={title} scrollable={false} onClose={onClose}
           footer={<button type="button" className="btn btn-sm btn-secondary" onClick={onClose}>Cancel</button>}>
      <input className="form-control form-control-sm mb-2" placeholder="Search by item number or description..." autoFocus value={term}
             onChange={(e) => { setTerm(e.target.value); run(e.target.value); }} />
      <div style={{ maxHeight: 300, overflowY: 'auto' }}>
        {searching ? (
          <LoadingState text="Searching..." />
        ) : results.length === 0 ? (
          <div className="text-muted small">
            {term.trim() ? <>No matches. Try "Update Fees &amp; Bill Items" on Clinic Settings if the catalog hasn't been synced yet.</> : 'Start typing to search the billing catalog.'}
          </div>
        ) : (
          <ul className="list-group">
            {results.map((c) => (
              <li key={c.id} className="list-group-item list-group-item-action" style={{ cursor: 'pointer' }} role="button" tabIndex={0}
                  onClick={() => onSelect(c)} onKeyDown={(e) => { if (e.key === 'Enter') onSelect(c); }}>
                <strong>{c.itemNumber}</strong> — {c.description} <span className="text-muted small">(Schedule: {money(c.scheduleFee)})</span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Modal>
  );
}
