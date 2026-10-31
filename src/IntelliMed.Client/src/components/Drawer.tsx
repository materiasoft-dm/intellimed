import { useEffect, type ReactNode } from 'react';
import { createPortal } from 'react-dom';

/**
 * Right-hand slide-over (same .imed-drawer markup as Blazor's IMedDrawer). Clicking the dimmed area or pressing
 * Escape closes it — unless a modal opened from inside it is on top, which gets the Escape instead.
 */
export function Drawer({ title, onClose, wide, footer, children }: {
  title: ReactNode;
  onClose: () => void;
  wide?: boolean;
  footer?: ReactNode;
  children: ReactNode;
}) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !document.querySelector('.imed-dialog')) onClose();
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onClose]);

  return createPortal(
    <div className="imed-drawer-overlay" onMouseDown={(e) => { if (e.target === e.currentTarget) onClose(); }}>
      <aside className={`imed-drawer ${wide ? 'imed-drawer--wide' : ''}`} role="dialog" aria-label={typeof title === 'string' ? title : undefined}>
        <div className="imed-drawer-header">
          <h5 className="imed-drawer-title">{title}</h5>
          <button type="button" className="imed-modal-close" onClick={onClose} aria-label="Close">
            <i className="bi bi-x-lg" />
          </button>
        </div>
        <div className="imed-drawer-body">{children}</div>
        {footer && <div className="imed-drawer-footer">{footer}</div>}
      </aside>
    </div>,
    document.body,
  );
}

/** Page title + subtitle with actions on the right (Blazor's IMedPageHeader). */
export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: string; actions?: ReactNode }) {
  return (
    <div className="imed-page-header">
      <div>
        <h3 className="imed-page-title">{title}</h3>
        {subtitle?.trim() && <p className="imed-page-subtitle">{subtitle}</p>}
      </div>
      {actions && <div className="d-flex align-items-center gap-2">{actions}</div>}
    </div>
  );
}
