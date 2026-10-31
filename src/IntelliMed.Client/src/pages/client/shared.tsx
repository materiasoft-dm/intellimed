import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';

/** A client-record tab's own data: fetched when the tab opens, `reload()` after a change. */
export function useTabData<T>(load: () => Promise<T>, initial: T) {
  const [data, setData] = useState<T>(initial);
  const [loading, setLoading] = useState(true);
  const loadRef = useRef(load);
  loadRef.current = load;
  const seq = useRef(0);

  const reload = useCallback(async () => {
    const mine = ++seq.current;
    setLoading(true);
    try {
      const result = await loadRef.current();
      if (mine === seq.current) setData(result);
    } finally {
      if (mine === seq.current) setLoading(false);
    }
  }, []);

  useEffect(() => { reload(); }, [reload]);
  return { data, setData, loading, reload };
}

/**
 * The message for an empty tab grid. When the list is only empty because archived rows are hidden, say so
 * and how to see them, rather than implying nothing was ever recorded.
 */
export function noneMessage(noun: string, total: number, includeArchived: boolean): string {
  return total > 0 && !includeArchived ? `No active ${noun}. Tick "Include archived" to see archived ones.` : `No ${noun} recorded.`;
}

/** "Include archived" checkbox + the tab's "+ New" button, the header every list tab shares. */
export function TabToolbar({ archivedId, includeArchived, onIncludeArchived, onNew, children }: {
  archivedId?: string;
  includeArchived?: boolean;
  onIncludeArchived?: (v: boolean) => void;
  onNew?: () => void;
  children?: ReactNode;
}) {
  return (
    <div className="d-flex justify-content-between align-items-center mb-1 gap-2">
      <div className="d-flex align-items-center gap-3">
        {archivedId && onIncludeArchived && (
          <div className="form-check mb-0">
            <input type="checkbox" className="form-check-input" id={archivedId} checked={!!includeArchived} onChange={(e) => onIncludeArchived(e.target.checked)} />
            <label className="form-check-label small" htmlFor={archivedId}>Include archived</label>
          </div>
        )}
        {children}
      </div>
      {onNew && <button type="button" className="btn btn-sm btn-outline-primary" onClick={onNew}>+ New</button>}
    </div>
  );
}

export const tick = (b: boolean) => (b ? '✓' : '');
