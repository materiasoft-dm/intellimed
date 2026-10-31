import { useCallback, useEffect, useRef, useState } from 'react';

/** Blazor's <PageTitle>. */
export function usePageTitle(title: string) {
  useEffect(() => {
    document.title = title;
  }, [title]);
}

/** Calls `fn` once after mount (Blazor's OnInitializedAsync), ignoring results that land after unmount. */
export function useMountEffect(fn: (alive: () => boolean) => void | Promise<void>) {
  const ref = useRef(fn);
  ref.current = fn;
  useEffect(() => {
    let live = true;
    void ref.current(() => live);
    return () => { live = false; };
  }, []);
}

/**
 * State for a server-paged, infinitely scrolling grid: `load(1)` starts a search, `loadMore()` appends the next
 * page (DataGrid calls it as the end of the list scrolls into view), `reload()` re-reads what's loaded.
 */
export function usePagedGrid<T>(fetchPage: (page: number, pageSize: number) => Promise<{ items: T[]; totalCount: number }>, pageSize: number) {
  const [rows, setRows] = useState<T[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);          // a fresh search (page 1) is in flight
  const [loadingMore, setLoadingMore] = useState(false); // the next page is being appended
  const [error, setError] = useState<string | null>(null);
  const seq = useRef(0);
  const fetchRef = useRef(fetchPage);
  fetchRef.current = fetchPage;
  const pageRef = useRef(1);
  const rowCount = useRef(0);
  const busy = useRef(false);

  /**
   * Infinite scroll: page 1 starts a fresh search and replaces the rows; a later page is appended to them.
   * A newer page-1 load supersedes anything still in flight, so a slow response never lands on a new search.
   */
  const load = useCallback(async (p: number) => {
    const fresh = p <= 1;
    const mine = fresh ? ++seq.current : seq.current;
    if (!fresh && busy.current) return;
    busy.current = true;
    if (fresh) setLoading(true); else setLoadingMore(true);
    setError(null);
    try {
      const result = await fetchRef.current(fresh ? 1 : p, pageSize);
      if (mine !== seq.current) return;
      pageRef.current = fresh ? 1 : p;
      setPage(pageRef.current);
      setRows((all) => {
        const next = fresh ? result.items : [...all, ...result.items];
        rowCount.current = next.length;
        return next;
      });
      setTotal(result.totalCount);
    } catch (e) {
      if (mine === seq.current) setError(e instanceof Error ? e.message : String(e));
    } finally {
      if (mine === seq.current) {
        busy.current = false;
        setLoading(false);
        setLoadingMore(false);
      }
    }
  }, [pageSize]);

  const hasMore = rows.length < total;
  const loadMore = useCallback(() => { if (hasMore && !busy.current) void load(pageRef.current + 1); }, [hasMore, load]);

  /** Re-read everything loaded so far (after an edit), in one request, keeping the scroll depth. */
  const reload = useCallback(async () => {
    const mine = ++seq.current;
    const pages = Math.max(1, Math.ceil(rowCount.current / pageSize));
    busy.current = true;
    setLoading(true);
    try {
      const result = await fetchRef.current(1, pages * pageSize);
      if (mine !== seq.current) return;
      pageRef.current = pages;
      setPage(pages);
      rowCount.current = result.items.length;
      setRows(result.items);
      setTotal(result.totalCount);
    } finally {
      if (mine === seq.current) { busy.current = false; setLoading(false); }
    }
  }, [pageSize]);

  return { rows, setRows, total, page, pageSize, loading, loadingMore, hasMore, error, load, loadMore, reload };
}

/** A value that settles `delay` ms after the last change — for search-as-you-type. */
export function useDebounced<T>(value: T, delay = 250): T {
  const [settled, setSettled] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setSettled(value), delay);
    return () => clearTimeout(t);
  }, [value, delay]);
  return settled;
}

/**
 * Search where the latest request wins: a slow earlier response must not overwrite newer results, and the
 * caller can show "Searching..." (instead of a misleading "No matches.") while a request is in flight.
 */
export function useLatestSearch<T>(search: (term: string) => Promise<T>, empty: T) {
  const [results, setResults] = useState<T>(empty);
  const [searching, setSearching] = useState(false);
  const seq = useRef(0);
  const run = useCallback(async (term: string) => {
    const mine = ++seq.current;
    if (!term.trim()) {
      setResults(empty);
      setSearching(false);
      return;
    }
    setSearching(true);
    try {
      const found = await search(term);
      if (mine === seq.current) setResults(found);
    } finally {
      if (mine === seq.current) setSearching(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);
  const clear = useCallback(() => {
    seq.current++;
    setResults(empty);
    setSearching(false);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  return { results, searching, run, clear, setResults };
}
