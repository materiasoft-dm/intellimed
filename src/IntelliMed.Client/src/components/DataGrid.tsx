import type { CSSProperties, ReactNode } from 'react';
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
 * The app's data grid (replaces RadzenDataGrid): a Bootstrap table, server-paged, with a skeleton while the
 * first page loads, a dimmed body while later pages load, an empty message, and a pager.
 * Rows carry `imed-data-row` so tests and styles can find them.
 */
export function DataGrid<T>({ columns, rows, rowKey, total, page, pageSize, loading, emptyText, emptyIcon = 'bi-inbox', onPageChange, rowClassName, onRowClick, style }: {
  columns: Column<T>[];
  rows: T[];
  rowKey: (row: T) => string | number;
  total: number;
  /** 1-based. */
  page: number;
  pageSize: number;
  loading: boolean;
  emptyText: string;
  emptyIcon?: string;
  onPageChange: (page: number) => void;
  rowClassName?: (row: T) => string;
  onRowClick?: (row: T) => void;
  style?: CSSProperties;
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  const firstLoad = loading && rows.length === 0;
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, total);

  return (
    <div className="imed-grid" style={style}>
      <div className={`table-responsive imed-grid-body ${loading && !firstLoad ? 'is-refreshing' : ''}`} aria-busy={loading}>
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
      </div>
      {total > 0 && (
        <div className="imed-pager">
          <span className="imed-pager-summary">{from}–{to} of {total.toLocaleString('en-AU')}</span>
          {pages > 1 && <Pager page={page} pages={pages} onPageChange={onPageChange} disabled={loading} />}
        </div>
      )}
    </div>
  );
}

/** First / previous / numbered window / next / last. */
export function Pager({ page, pages, onPageChange, disabled }: { page: number; pages: number; onPageChange: (p: number) => void; disabled?: boolean }) {
  const start = Math.max(1, Math.min(page - 2, pages - 4));
  const window = Array.from({ length: Math.min(5, pages) }, (_, i) => start + i);
  const go = (p: number) => { if (!disabled && p >= 1 && p <= pages && p !== page) onPageChange(p); };
  const item = (label: ReactNode, target: number, isDisabled: boolean, aria: string, active = false) => (
    <li className={`page-item ${isDisabled ? 'disabled' : ''} ${active ? 'active' : ''}`}>
      <button type="button" className="page-link" aria-label={aria} aria-current={active ? 'page' : undefined}
              disabled={isDisabled} onClick={() => go(target)}>{label}</button>
    </li>
  );
  return (
    <nav aria-label="Pages">
      <ul className="pagination pagination-sm mb-0">
        {item(<i className="bi bi-chevron-double-left" />, 1, page <= 1, 'First page')}
        {item(<i className="bi bi-chevron-left" />, page - 1, page <= 1, 'Previous page')}
        {window.map((p) => <span key={p} style={{ display: 'contents' }}>{item(p, p, false, `Page ${p}`, p === page)}</span>)}
        {item(<i className="bi bi-chevron-right" />, page + 1, page >= pages, 'Next page')}
        {item(<i className="bi bi-chevron-double-right" />, pages, page >= pages, 'Last page')}
      </ul>
    </nav>
  );
}
