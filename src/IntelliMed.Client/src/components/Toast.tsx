import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react';

interface ToastItem {
  id: number;
  message: string;
  error: boolean;
  leaving: boolean;
}

interface ToastApi {
  /** Green confirmation; fades after a few seconds. */
  success(message: string): void;
  /** Red error; stays until closed so it cannot be missed. */
  error(message: string): void;
  /** success() or error() by flag — matches the Blazor pages' ShowToast(message, isError). */
  show(message: string, isError?: boolean): void;
}

const ToastContext = createContext<ToastApi | null>(null);

const SUCCESS_MS = 6000;

/** One toast stack for the whole app (bottom-right), replacing each Blazor page's own toast markup. */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([]);
  const nextId = useRef(1);

  const dismiss = useCallback((id: number) => {
    setItems((all) => all.map((t) => (t.id === id ? { ...t, leaving: true } : t)));
    setTimeout(() => setItems((all) => all.filter((t) => t.id !== id)), 200);
  }, []);

  const show = useCallback((message: string, isError = false) => {
    const id = nextId.current++;
    setItems((all) => [...all.slice(-3), { id, message, error: isError, leaving: false }]);
    if (!isError) setTimeout(() => dismiss(id), SUCCESS_MS);
  }, [dismiss]);

  const api = useMemo<ToastApi>(() => ({
    show,
    success: (m) => show(m, false),
    error: (m) => show(m, true),
  }), [show]);

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div className="imed-toast-stack position-fixed bottom-0 end-0 p-3" aria-live="polite">
        {items.map((t) => (
          <div key={t.id} role="alert"
               className={`toast show align-items-center border-0 imed-toast ${t.error ? 'text-bg-danger' : 'text-bg-success'} ${t.leaving ? 'imed-toast--leaving' : ''}`}>
            <div className="d-flex">
              <div className="toast-body">
                <i className={`bi ${t.error ? 'bi-exclamation-octagon' : 'bi-check-circle'} me-2`} aria-hidden="true" />
                {t.message}
              </div>
              <button type="button" className="btn-close btn-close-white me-2 m-auto" aria-label="Close" onClick={() => dismiss(t.id)} />
            </div>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast(): ToastApi {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error('useToast must be used inside <ToastProvider>');
  return ctx;
}
