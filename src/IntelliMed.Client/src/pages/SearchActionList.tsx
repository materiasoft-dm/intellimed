import { useState } from 'react';
import { useNavigate } from 'react-router';
import { SearchActionApi } from '@/api/settings';
import { SearchActionType, type SearchActionDto } from '@/api/types.gen';
import { EmptyState, LoadingState } from '@/components/States';
import { useToast } from '@/components/Toast';
import { useMountEffect, usePageTitle } from '@/lib/hooks';

export default function SearchActionList() {
  usePageTitle('Command Palette Actions - IntelliMed');
  const navigate = useNavigate();
  const toast = useToast();
  const [actions, setActions] = useState<SearchActionDto[] | null>(null);
  const [loading, setLoading] = useState(true);

  const load = async () => {
    setLoading(true);
    setActions(await SearchActionApi.list());
    setLoading(false);
  };
  useMountEffect(load);

  const archive = async (a: SearchActionDto) => {
    const ok = await SearchActionApi.archive(a.id);
    toast.show(ok ? `'${a.title}' archived.` : 'Failed to archive action.', !ok);
    if (ok) await load();
  };

  const sorted = [...(actions ?? [])].sort((a, b) => a.category.localeCompare(b.category) || a.sortOrder - b.sortOrder);

  return (
    <>
      <h4 className="mb-4">Command Palette Actions</h4>
      <p className="text-muted">Entries searchable from the floating command palette (bottom-right button, or Ctrl+K) available on every page.</p>
      <div className="card mb-4">
        <div className="card-body d-flex align-items-center justify-content-between">
          <span className="d-flex align-items-center"><i className="bi bi-command me-2" />Total Actions: {actions?.length ?? 0}</span>
          <button className="btn btn-primary" onClick={() => navigate('/admin/search-actions/edit')}><i className="bi bi-plus-lg me-1" /> New Action</button>
        </div>
      </div>

      {loading ? (
        <div className="card"><div className="card-body"><LoadingState text="Loading actions..." compact={false} /></div></div>
      ) : actions ? (
        <div className="card">
          {actions.length === 0 ? (
            <div className="card-body"><EmptyState message='No actions yet. Click "New Action" to add one.' icon="bi-command" compact={false} /></div>
          ) : (
            <div className="table-responsive">
              <table className="table table-hover table-striped table-sm mb-0">
                <thead className="table-light">
                  <tr><th>Title</th><th>Category</th><th>Type</th><th>Target</th><th className="text-end">Actions</th></tr>
                </thead>
                <tbody>
                  {sorted.map((a) => (
                    <tr key={a.id}>
                      <td><a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); navigate(`/admin/search-actions/edit/${a.id}`); }}>{a.title}</a></td>
                      <td><span className="badge bg-secondary">{a.category}</span></td>
                      <td>{SearchActionType[a.actionType]}</td>
                      <td className="text-truncate" style={{ maxWidth: 220 }}><code>{a.target}</code></td>
                      <td className="text-end">
                        <button className="btn btn-sm btn-outline-danger" title="Archive" onClick={() => archive(a)}><i className="bi bi-archive" /></button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      ) : (
        <div className="alert alert-danger d-flex align-items-center">
          <i className="bi bi-exclamation-triangle me-1" /> Failed to load actions.
          <button className="btn btn-outline-danger btn-sm ms-auto" onClick={load}>Retry</button>
        </div>
      )}
    </>
  );
}
