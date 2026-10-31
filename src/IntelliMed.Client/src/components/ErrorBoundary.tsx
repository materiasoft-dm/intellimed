import { Component, type ErrorInfo, type ReactNode } from 'react';
import { isRouteErrorResponse, useRouteError } from 'react-router';
import { LogApi } from '@/api/auth';

function ErrorCard({ error, onDismiss }: { error: unknown; onDismiss?: () => void }) {
  const e = error instanceof Error ? error : null;
  const message = isRouteErrorResponse(error)
    ? `${error.status} ${error.statusText}`
    : e?.message ?? String(error);
  // A lazily-loaded page whose chunk vanished after a deploy: a reload fetches the new build.
  const staleBuild = /dynamically imported module|Failed to fetch|Importing a module script failed/i.test(message);
  return (
    <div className="error-boundary-overlay visible">
      <div className="error-boundary-card bg-white p-4 rounded shadow-lg">
        <div className="d-flex align-items-center mb-4">
          <i className="bi bi-exclamation-triangle-fill text-danger me-3" style={{ fontSize: '2rem' }} aria-hidden="true" />
          <h5 className="text-danger mb-0">{staleBuild ? 'IntelliMed was updated' : 'Something went wrong'}</h5>
        </div>
        <p className="fw-bold mb-2">{staleBuild ? 'A newer version is available. Reload the page to continue.' : message}</p>
        {!staleBuild && e?.stack && (
          <div className="error-stacktrace p-3 mb-4 border rounded">
            <pre className="error-stacktrace-text">{e.stack}</pre>
          </div>
        )}
        <div className="d-flex gap-2">
          <button className="btn btn-primary" onClick={() => window.location.reload()}>
            <i className="bi bi-arrow-clockwise me-1" aria-hidden="true" /> Reload Page
          </button>
          <button className="btn btn-outline-secondary" onClick={onDismiss ?? (() => window.location.assign('/'))}>
            <i className="bi bi-x-lg me-1" aria-hidden="true" /> Dismiss
          </button>
        </div>
      </div>
    </div>
  );
}

/** React Router's errorElement: anything thrown while rendering or loading a route. */
export function RouteErrorDisplay() {
  const error = useRouteError();
  LogApi.error(error, 'RouteError');
  return <ErrorCard error={error} />;
}

/** Catches render errors below it (the Blazor <ErrorBoundary> + ErrorDisplay). */
export class ErrorBoundary extends Component<{ children: ReactNode }, { error: unknown }> {
  state = { error: null as unknown };

  static getDerivedStateFromError(error: unknown) {
    return { error };
  }

  componentDidCatch(error: unknown, info: ErrorInfo) {
    LogApi.error(error, `AppErrorBoundary ${info.componentStack ?? ''}`.trim());
  }

  render() {
    if (this.state.error) return <ErrorCard error={this.state.error} onDismiss={() => { this.setState({ error: null }); window.location.assign('/'); }} />;
    return this.props.children;
  }
}
