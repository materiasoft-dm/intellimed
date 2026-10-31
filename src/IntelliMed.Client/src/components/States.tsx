import type { ReactNode } from 'react';

/**
 * The one "data is on its way" indicator for grids, lists and search results. Show it INSTEAD of the grid
 * while a request is in flight, so a page never flashes a false "no data" message before the rows arrive.
 */
export function LoadingState({ text = 'Loading...', compact = true }: { text?: string; compact?: boolean }) {
  return (
    <div className={`imed-loading ${compact ? 'imed-loading--compact' : ''}`} role="status" aria-live="polite">
      <span className="spinner-border spinner-border-sm" aria-hidden="true" />
      <span className="imed-loading-text">{text}</span>
    </div>
  );
}

/**
 * The one "nothing here" message for a grid or list that has finished loading with no rows. Show it
 * INSTEAD of an empty table. Say what is missing and, where it helps, how to get some (children).
 */
export function EmptyState({ message, icon = 'bi-inbox', compact = true, children }: {
  message: string;
  icon?: string;
  compact?: boolean;
  children?: ReactNode;
}) {
  return (
    <div className={`imed-empty-state ${compact ? 'imed-empty-state--compact' : ''}`} role="status">
      <i className={`bi ${icon}`} aria-hidden="true" />
      <span className="imed-empty-text">{message}</span>
      {children}
    </div>
  );
}

/** Placeholder rows that hold a table's shape while its first page loads. */
export function SkeletonRows({ rows = 5, columns }: { rows?: number; columns: number }) {
  return (
    <>
      {Array.from({ length: rows }, (_, r) => (
        <tr key={r} className="imed-skeleton-row" aria-hidden="true">
          {Array.from({ length: columns }, (_, c) => (
            <td key={c}><span className="imed-skeleton-bar" style={{ width: `${55 + ((r * 7 + c * 13) % 40)}%` }} /></td>
          ))}
        </tr>
      ))}
    </>
  );
}
