import { useCallback, useEffect, useRef, useState } from 'react';
import type { PagedResult } from '@/api/types.gen';

/**
 * A server-paged list that loads its next page as the user scrolls to the bottom. `reset()` starts over
 * (new search text, a filter toggled); a slower, older request can never overwrite a newer one.
 */
export function useInfiniteList<T>(fetchPage: (page: number, pageSize: number) => Promise<PagedResult<T>>, pageSize = 50) {
  const [items, setItems] = useState<T[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);       // first page in flight
  const [loadingMore, setLoadingMore] = useState(false);
  const page = useRef(0);
  const generation = useRef(0);
  const busy = useRef(false);
  const fetchRef = useRef(fetchPage);
  fetchRef.current = fetchPage;

  const reset = useCallback(async () => {
    const gen = ++generation.current;
    busy.current = true;
    setLoading(true);
    try {
      const result = await fetchRef.current(1, pageSize);
      if (gen !== generation.current) return;
      page.current = 1;
      setItems(result.items);
      setTotal(result.totalCount);
    } finally {
      if (gen === generation.current) {
        busy.current = false;
        setLoading(false);
      }
    }
  }, [pageSize]);

  const hasMore = items.length < total;

  const loadMore = useCallback(async () => {
    if (busy.current || !hasMore) return;
    const gen = generation.current;
    busy.current = true;
    setLoadingMore(true);
    try {
      const result = await fetchRef.current(page.current + 1, pageSize);
      if (gen !== generation.current) return;
      page.current += 1;
      setItems((all) => [...all, ...result.items]);
      setTotal(result.totalCount);
    } finally {
      if (gen === generation.current) {
        busy.current = false;
        setLoadingMore(false);
      }
    }
  }, [hasMore, pageSize]);

  return { items, setItems, total, loading, loadingMore, hasMore, reset, loadMore };
}

/** Bare marker: calls `onVisible` whenever it scrolls into view while `enabled`. */
export function IntersectionSentinel({ onVisible, enabled, id }: { onVisible: () => void; enabled: boolean; id?: string }) {
  const ref = useRef<HTMLDivElement>(null);
  const cb = useRef(onVisible);
  cb.current = onVisible;
  useEffect(() => {
    if (!enabled || !ref.current) return;
    const io = new IntersectionObserver((entries) => {
      if (entries.some((e) => e.isIntersecting)) cb.current();
    });
    io.observe(ref.current);
    return () => io.disconnect();
  }, [enabled]);
  return <div ref={ref} id={id} style={{ height: 1 }} aria-hidden="true" />;
}

/** Invisible marker at the end of a list; when it scrolls into view, `onVisible` loads the next page. */
export function LoadMoreSentinel({ onVisible, loading, hasMore, shown, total, noun = 'items', totalKnown = true }: {
  onVisible: () => void;
  loading: boolean;
  hasMore: boolean;
  shown: number;
  total: number;
  noun?: string;
  /** False when the source only says whether more rows exist, not how many — then no total is printed. */
  totalKnown?: boolean;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const cb = useRef(onVisible);
  cb.current = onVisible;

  useEffect(() => {
    if (!hasMore || !ref.current) return;
    const io = new IntersectionObserver((entries) => {
      if (entries.some((e) => e.isIntersecting)) cb.current();
    }, { rootMargin: '200px' });
    io.observe(ref.current);
    return () => io.disconnect();
  }, [hasMore, shown]);

  return (
    <div ref={ref} className="imed-load-more" aria-live="polite">
      {loading
        ? <><span className="spinner-border spinner-border-sm" aria-hidden="true" /> Loading more {noun}…</>
        : hasMore
          ? <button type="button" className="btn btn-link btn-sm p-0" onClick={() => cb.current()}>Showing {shown}{totalKnown ? ` of ${total}` : ''} — load more</button>
          : shown > 0 ? <span>All {shown} {noun} shown</span> : null}
    </div>
  );
}
