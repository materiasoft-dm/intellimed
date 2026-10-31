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
 * State for a server-paged grid (what RadzenDataGrid's LoadData did): `load(page)` fetches a page,
 * `reload()` refetches the current one. Only the newest request's result is kept.
 */
export function usePagedGrid<T>(fetchPage: (page: number, pageSize: number) => Promise<{ items: T[]; totalCount: number }>, pageSize: number) {
  const [rows, setRows] = useState<T[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const seq = useRef(0);
  const fetchRef = useRef(fetchPage);
  fetchRef.current = fetchPage;
  const pageRef = useRef(1);

  const load = useCallback(async (p: number) => {
    const mine = ++seq.current;
    setLoading(true);
    setError(null);
    try {
      const result = await fetchRef.current(p, pageSize);
      if (mine !== seq.current) return;
      pageRef.current = p;
      setPage(p);
      setRows(result.items);
      setTotal(result.totalCount);
    } catch (e) {
      if (mine === seq.current) setError(e instanceof Error ? e.message : String(e));
    } finally {
      if (mine === seq.current) setLoading(false);
    }
  }, [pageSize]);

  const reload = useCallback(() => load(pageRef.current), [load]);
  return { rows, setRows, total, page, pageSize, loading, error, load, reload };
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
