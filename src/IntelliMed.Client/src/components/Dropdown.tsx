import { useEffect, useRef, useState, type ReactNode } from 'react';

/**
 * Bootstrap dropdown markup driven by React instead of bootstrap.bundle.js: click toggles, a click outside
 * or Escape closes, choosing an item closes. `data-bs-popper="static"` makes Bootstrap's CSS position the
 * menu under the toggle without Popper.
 */
export function Dropdown({ toggle, toggleClassName, title, align = 'end', menuClassName = '', menuStyle, className = '', onOpen, children }: {
  toggle: ReactNode;
  toggleClassName: string;
  title?: string;
  align?: 'start' | 'end';
  menuClassName?: string;
  menuStyle?: React.CSSProperties;
  className?: string;
  onOpen?: () => void;
  children: ReactNode | ((close: () => void) => ReactNode);
}) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    document.addEventListener('mousedown', onDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  const close = () => setOpen(false);

  return (
    <div className={`dropdown ${className}`} ref={ref}>
      <button type="button" className={`${toggleClassName} ${open ? 'show' : ''}`} aria-expanded={open} title={title}
              onClick={() => {
                const next = !open;
                setOpen(next);
                if (next) onOpen?.();
              }}>
        {toggle}
      </button>
      <ul className={`dropdown-menu ${align === 'end' ? 'dropdown-menu-end' : ''} ${open ? 'show' : ''} ${menuClassName}`}
          data-bs-popper="static" style={menuStyle}
          onClick={(e) => {
            // an item (button or link) closes the menu; clicks on padding/headers do not
            if ((e.target as HTMLElement).closest('.dropdown-item')) close();
          }}>
        {typeof children === 'function' ? children(close) : children}
      </ul>
    </div>
  );
}
