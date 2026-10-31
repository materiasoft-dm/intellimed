import type { CSSProperties, ReactNode } from 'react';
import { LoadMoreSentinel } from '@/lib/infinite';
import { EmptyState, SkeletonRows } from './States';

export interface Column<T> {
  title: ReactNode;
  /** Cell content; defaults to nothing. */
  render: (row: T) => ReactNode;
  width?: string | number;
  className?: string;
  headerClassName?: string;
}

/**
 * The app's data grid (replaces RadzenDataGrid): a Bootstrap table that scrolls infinitely — the next page is
 * fetched as the end of the list comes into view — with a skeleton while the first page loads, a dimmed body
 * while a new search runs, and an empty message.
 * Rows carry `imed-data-row` so tests and styles can find them.
 */
export function DataGrid<T>({ columns, rows, rowKey, total, pageSize, loading, loadingMore = false, emptyText, emptyIcon = 'bi-inbox', onLoadMore, noun = 'rows', scrollHeight, rowClassName, onRowClick, style }: {
  columns: Column<T>[];
  rows: T[];
  rowKey: (row: T) => string | number;
  total: number;
  pageSize: number;
  /** A new search (page 1) is in flight. */
  loading: boolean;
  /** The next page is being appended. */
  loadingMore?: boolean;
  emptyText: string;
  emptyIcon?: string;
  onLoadMore: () => void;
  /** What the rows are, for the "All 162 clients shown" footer. */
  noun?: string;
  /** Height cap of the scrolling section (CSS length); defaults to most of the window. */
  scrollHeight?: string;
  rowClassName?: (row: T) => string;
  onRowClick?: (row: T) => void;
  style?: CSSProperties;
}) {
  const firstLoad = loading && rows.length === 0;

  return (
    <div className="imed-grid" style={style}>
      <div className={`imed-grid-body imed-scroll-section ${loading && !firstLoad ? 'is-refreshing' : ''}`} aria-busy={loading}
           style={scrollHeight ? ({ '--imed-scroll-max': scrollHeight } as CSSProperties) : undefined}>
        {/* columns keep their widths and the grid scrolls sideways, rather than squeezing every column (as Radzen did) */}
        <table className="table table-hover table-sm align-middle mb-0"
               style={{ minWidth: columns.reduce((sum, c) => sum + (typeof c.width === 'number' ? c.width : 0), 0) || undefined }}>
          <thead>
            <tr>
              {columns.map((c, i) => (
                <th key={i} className={c.headerClassName} style={c.width ? { width: c.width } : undefined}>{c.title}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {firstLoad ? (
              <SkeletonRows columns={columns.length} rows={Math.min(pageSize, 8)} />
            ) : rows.length === 0 ? (
              <tr className="imed-grid-empty">
                <td colSpan={columns.length}><EmptyState message={emptyText} icon={emptyIcon} /></td>
              </tr>
            ) : rows.map((row) => (
              <tr key={rowKey(row)} className={`imed-data-row ${onRowClick ? 'imed-row-clickable' : ''} ${rowClassName?.(row) ?? ''}`}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}>
                {columns.map((c, i) => <td key={i} className={c.className}>{c.render(row)}</td>)}
              </tr>
            ))}
          </tbody>
        </table>
        {total > 0 && (
          <LoadMoreSentinel onVisible={onLoadMore} loading={loadingMore} hasMore={rows.length < total}
                            shown={rows.length} total={total} noun={noun} />
        )}
      </div>
    </div>
  );
}
