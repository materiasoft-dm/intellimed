import { useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';

export interface ContextMenuItem {
  label: string;
  icon?: string;
  onSelect: () => void;
}

/** A small menu at a screen position (right-click menus); closes on outside click, Escape or scroll. */
export function ContextMenu({ x, y, items, onClose }: { x: number; y: number; items: ContextMenuItem[]; onClose: () => void }) {
  const ref = useRef<HTMLUListElement>(null);

  useEffect(() => {
    const close = (e: Event) => {
      if (e instanceof MouseEvent && ref.current?.contains(e.target as Node)) return;
      onClose();
    };
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    document.addEventListener('mousedown', close);
    document.addEventListener('keydown', onKey);
    window.addEventListener('scroll', close, true);
    window.addEventListener('resize', close);
    ref.current?.querySelector<HTMLButtonElement>('button')?.focus();
    return () => {
      document.removeEventListener('mousedown', close);
      document.removeEventListener('keydown', onKey);
      window.removeEventListener('scroll', close, true);
      window.removeEventListener('resize', close);
    };
  }, [onClose]);

  // keep it on screen
  const left = Math.min(x, window.innerWidth - 230);
  const top = Math.min(y, window.innerHeight - 16 - items.length * 38);

  return createPortal(
    <ul ref={ref} className="dropdown-menu show imed-context-menu" role="menu" style={{ position: 'fixed', left, top, zIndex: 1080 }}>
      {items.map((item) => (
        <li key={item.label}>
          <button type="button" role="menuitem" className="dropdown-item d-flex align-items-center gap-2"
                  onClick={() => { onClose(); item.onSelect(); }}>
            {item.icon && <i className={`bi ${item.icon}`} />} {item.label}
          </button>
        </li>
      ))}
    </ul>,
    document.body,
  );
}
