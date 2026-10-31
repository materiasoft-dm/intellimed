import { useEffect, useRef, type ReactNode } from 'react';
import { createPortal } from 'react-dom';

/**
 * A Bootstrap modal (same .modal / .modal-content / .modal-title / .modal-footer markup the Blazor pages
 * rendered inline, so the UI tests' selectors still match), with what those lacked: Escape closes it,
 * focus moves into it and back out, the page behind does not scroll, and it fades in.
 */
export function Modal({ title, onClose, children, footer, size, scrollable = true, centered = true, closeOnBackdrop = false, headerClassName = 'py-2', titleTag = 'h6', staticBackdrop = false, closeLabel = 'Close' }: {
  title: ReactNode;
  onClose?: () => void;
  children: ReactNode;
  footer?: ReactNode;
  size?: 'sm' | 'lg' | 'xl';
  scrollable?: boolean;
  centered?: boolean;
  /** Clicking the dimmed area closes it. Off by default: half-filled forms should not vanish on a stray click. */
  closeOnBackdrop?: boolean;
  headerClassName?: string;
  titleTag?: 'h5' | 'h6';
  /** No close button and Escape does nothing (a choice is mandatory, e.g. picking a clinic). */
  staticBackdrop?: boolean;
  /** Accessible name of the header's X button — change it when the footer has its own "Close" button. */
  closeLabel?: string;
}) {
  const dialogRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const previouslyFocused = document.activeElement as HTMLElement | null;
    document.body.classList.add('modal-open');
    // focus the first field (or the dialog) so keyboard users land inside it
    const first = dialogRef.current?.querySelector<HTMLElement>('input:not([type=hidden]):not([disabled]), select, textarea, [autofocus]');
    (first ?? dialogRef.current)?.focus({ preventScroll: true });

    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !staticBackdrop && onClose) {
        // only the top-most modal reacts
        const all = document.querySelectorAll('.imed-dialog');
        if (all[all.length - 1] === dialogRef.current?.closest('.imed-dialog')) onClose();
      }
    };
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('keydown', onKey);
      // React has removed this modal's DOM by the time passive cleanups run
      if (!document.querySelector('.imed-dialog')) document.body.classList.remove('modal-open');
      previouslyFocused?.focus?.({ preventScroll: true });
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const TitleTag = titleTag;
  return createPortal(
    <div className="modal d-block imed-dialog" tabIndex={-1} role="dialog" aria-modal="true"
         onMouseDown={(e) => {
           if (closeOnBackdrop && !staticBackdrop && e.target === e.currentTarget) onClose?.();
         }}>
      <div className={`modal-dialog ${centered ? 'modal-dialog-centered' : ''} ${scrollable ? 'modal-dialog-scrollable' : ''} ${size ? `modal-${size}` : ''}`}>
        <div className="modal-content" ref={dialogRef} tabIndex={-1}>
          <div className={`modal-header ${headerClassName}`}>
            <TitleTag className="modal-title">{title}</TitleTag>
            {!staticBackdrop && onClose && <button type="button" className="btn-close" aria-label={closeLabel} onClick={onClose} />}
          </div>
          <div className="modal-body">{children}</div>
          {footer && <div className="modal-footer py-2">{footer}</div>}
        </div>
      </div>
    </div>,
    document.body,
  );
}

/** Yes/no question in a modal. */
export function ConfirmModal({ title, message, confirmText = 'Confirm', confirmClass = 'btn-danger', busy, onConfirm, onCancel }: {
  title: string;
  message: ReactNode;
  confirmText?: string;
  confirmClass?: string;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <Modal title={title} onClose={onCancel} titleTag="h5" headerClassName=""
           footer={<>
             <button type="button" className="btn btn-secondary" onClick={onCancel}>Cancel</button>
             <button type="button" className={`btn ${confirmClass}`} disabled={busy} onClick={onConfirm}>
               {busy && <span className="spinner-border spinner-border-sm me-1" aria-hidden="true" />}{confirmText}
             </button>
           </>}>
      {typeof message === 'string' ? <p className="mb-0">{message}</p> : message}
    </Modal>
  );
}
