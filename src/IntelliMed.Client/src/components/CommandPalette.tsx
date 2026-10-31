import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router';
import { SearchActionApi } from '@/api/settings';
import { SearchActionType, type SearchActionDto } from '@/api/types.gen';

/**
 * Every whitespace-separated token must appear somewhere in Title/Keywords/Description — order-independent,
 * so "create patient" matches "Add Client" when its keywords include both. (Core's CommandPaletteMatcher.)
 */
export function matchesAction(action: SearchActionDto, query: string): boolean {
  if (!query.trim()) return true;
  const haystack = `${action.title} ${action.keywords ?? ''} ${action.description ?? ''}`.toLowerCase();
  const tokens = query.toLowerCase().split(' ').filter(Boolean);
  return tokens.length > 0 && tokens.every((t) => haystack.includes(t));
}

/** The floating search button (bottom-right) and the Ctrl+K / Cmd+K action palette. */
export function CommandPalette({ accessiblePages }: { accessiblePages: Set<string> }) {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [loadFailed, setLoadFailed] = useState(false);
  const [all, setAll] = useState<SearchActionDto[] | null>(null);
  const [query, setQuery] = useState('');
  const [highlighted, setHighlighted] = useState(0);
  const [info, setInfo] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLUListElement>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadFailed(false);
    const result = await SearchActionApi.list();
    // null means the fetch failed (e.g. a database without this feature's migration) — say so, rather
    // than showing the same "No matching actions." as a successful fetch with zero rows
    setLoadFailed(result === null);
    setAll(result ?? []);
    setLoading(false);
  }, []);

  const show = useCallback(() => {
    setOpen(true);
    setInfo(null);
    setHighlighted(0);
    if (all === null) load();
  }, [all, load]);

  const close = useCallback(() => {
    setOpen(false);
    setQuery('');
    setInfo(null);
  }, []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        if (open) close();
        else show();
      }
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open, show, close]);

  useEffect(() => {
    if (open) requestAnimationFrame(() => inputRef.current?.focus());
  }, [open, loading]);

  const filtered = useMemo(() => {
    const visible = (all ?? []).filter((a) => !a.pageKey || accessiblePages.has(a.pageKey.toLowerCase()));
    return visible.filter((a) => matchesAction(a, query));
  }, [all, accessiblePages, query]);

  useEffect(() => {
    listRef.current?.querySelector('.cp-result-active')?.scrollIntoView({ block: 'nearest' });
  }, [highlighted]);

  const activate = (action: SearchActionDto) => {
    if (action.actionType === SearchActionType.Navigate) {
      close();
      navigate(action.target);
    } else {
      // Modal/Info actions with no registered handler yet — show the description in place
      setInfo(action.description?.trim() ? action.description : `"${action.title}" isn't wired up to an action yet.`);
    }
  };

  const onKeyDown = (e: React.KeyboardEvent) => {
    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        if (filtered.length) setHighlighted((i) => (i + 1) % filtered.length);
        break;
      case 'ArrowUp':
        e.preventDefault();
        if (filtered.length) setHighlighted((i) => (i - 1 + filtered.length) % filtered.length);
        break;
      case 'Enter':
        if (highlighted >= 0 && highlighted < filtered.length) activate(filtered[highlighted]);
        break;
      case 'Escape':
        close();
        break;
    }
  };

  return (
    <>
      <button type="button" className="cp-trigger-btn" title="Search actions (Ctrl+K)" aria-label="Search actions" onClick={show}>
        <i className="bi bi-search" />
      </button>

      {open && (
        <>
          <div className="cp-backdrop" onClick={close} />
          <div className="cp-panel" role="dialog" aria-modal="true" aria-label="Search actions">
            <div className="cp-search-row">
              <i className="bi bi-search cp-search-icon" />
              <input ref={inputRef} className="cp-search-input" placeholder="Search actions… (try 'patient' or 'create')"
                     value={query} autoComplete="off" onKeyDown={onKeyDown}
                     onChange={(e) => { setQuery(e.target.value); setInfo(null); setHighlighted(0); }} />
              <kbd className="cp-kbd d-none d-sm-inline">Esc</kbd>
              <button type="button" className="btn-close" onClick={close} aria-label="Close" />
            </div>

            {loading ? (
              <div className="cp-empty"><span className="spinner-border spinner-border-sm me-2" />Loading…</div>
            ) : loadFailed ? (
              <div className="cp-empty text-danger">
                <i className="bi bi-exclamation-triangle me-2" />Couldn't load actions.{' '}
                <a href="#" onClick={(e) => { e.preventDefault(); load(); }}>Retry</a>
              </div>
            ) : info !== null ? (
              <div className="cp-info-banner"><i className="bi bi-info-circle me-2" />{info}</div>
            ) : filtered.length === 0 ? (
              <div className="cp-empty">No matching actions.</div>
            ) : (
              <ul className="cp-results" ref={listRef}>
                {filtered.map((action, index) => (
                  <li key={action.id} className={`cp-result ${index === highlighted ? 'cp-result-active' : ''}`}
                      onMouseEnter={() => setHighlighted(index)} onClick={() => activate(action)}>
                    <div className="cp-result-title">{action.title}</div>
                    {action.description?.trim() && <div className="cp-result-desc">{action.description}</div>}
                    <span className="cp-result-category">{action.category}</span>
                  </li>
                ))}
              </ul>
            )}
            <div className="cp-footer d-none d-sm-flex">
              <span><kbd>↑</kbd><kbd>↓</kbd> to move</span><span><kbd>Enter</kbd> to open</span><span><kbd>Ctrl</kbd>+<kbd>K</kbd> to toggle</span>
            </div>
          </div>
        </>
      )}
    </>
  );
}
