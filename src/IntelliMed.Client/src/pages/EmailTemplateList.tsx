import { useState } from 'react';
import { useNavigate } from 'react-router';
import { EmailTemplateApi } from '@/api/settings';
import { EmailEventKeys, type EmailTemplateDto } from '@/api/types.gen';
import { EmptyState, LoadingState } from '@/components/States';
import { useToast } from '@/components/Toast';
import { formatUtc } from '@/lib/dates';
import { useMountEffect, usePageTitle } from '@/lib/hooks';

export function eventDisplayName(eventKey: string): string {
  switch (eventKey) {
    case EmailEventKeys.InviteEmail: return 'Invite Email';
    case EmailEventKeys.ForgotPassword: return 'Forgot Password';
    default: return eventKey;
  }
}

export default function EmailTemplateList() {
  usePageTitle('Email Templates - IntelliMed');
  const navigate = useNavigate();
  const toast = useToast();
  const [templates, setTemplates] = useState<EmailTemplateDto[] | null>(null);
  const [loading, setLoading] = useState(true);

  const load = async () => {
    setLoading(true);
    setTemplates(await EmailTemplateApi.list());
    setLoading(false);
  };
  useMountEffect(load);

  const archive = async (t: EmailTemplateDto) => {
    const ok = await EmailTemplateApi.archive(t.id);
    toast.show(ok ? `'${t.name}' archived.` : 'Failed to archive template.', !ok);
    if (ok) await load();
  };

  return (
    <>
      <h4 className="mb-4">Email Templates</h4>
      <div className="card mb-4">
        <div className="card-body d-flex align-items-center justify-content-between">
          <span className="d-flex align-items-center"><i className="bi bi-envelope-paper me-2" />Total Templates: {templates?.length ?? 0}</span>
          <button className="btn btn-primary" onClick={() => navigate('/admin/email-templates/edit')}><i className="bi bi-plus-lg me-1" /> New Template</button>
        </div>
      </div>

      {loading ? (
        <div className="card"><div className="card-body"><LoadingState text="Loading templates..." compact={false} /></div></div>
      ) : templates ? (
        <div className="card">
          {templates.length === 0 ? (
            <div className="card-body">
              <EmptyState message='No email templates yet. Click "New Template" to create one.' icon="bi-envelope-paper" compact={false} />
            </div>
          ) : (
            <div className="table-responsive">
              <table className="table table-hover table-striped table-sm mb-0">
                <thead className="table-light">
                  <tr><th>Name</th><th>Subject</th><th>Event</th><th>Status</th><th>Updated</th><th className="text-end">Actions</th></tr>
                </thead>
                <tbody>
                  {templates.map((t) => (
                    <tr key={t.id}>
                      <td><a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); navigate(`/admin/email-templates/edit/${t.id}`); }}>{t.name}</a></td>
                      <td className="text-truncate" style={{ maxWidth: 300 }}>{t.subject}</td>
                      <td>{t.eventKey ? <span className="badge bg-info text-dark">{eventDisplayName(t.eventKey)}</span> : <span className="text-muted small">Unassigned</span>}</td>
                      <td>{t.isArchived ? <span className="badge bg-secondary">Archived</span> : <span className="badge bg-success">Active</span>}</td>
                      <td>{formatUtc(t.updatedAt, 'dd/MM/yyyy HH:mm')}</td>
                      <td className="text-end">
                        {!t.isArchived && (
                          <button className="btn btn-sm btn-outline-danger" title="Archive" onClick={() => archive(t)}><i className="bi bi-archive" /></button>
                        )}
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
          <i className="bi bi-exclamation-triangle me-1" /> Failed to load email templates.
          <button className="btn btn-outline-danger btn-sm ms-auto" onClick={load}>Retry</button>
        </div>
      )}
    </>
  );
}
