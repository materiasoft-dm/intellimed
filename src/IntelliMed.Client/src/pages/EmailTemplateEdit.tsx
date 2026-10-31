import { useState, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router';
import { EmailTemplateApi } from '@/api/settings';
import { EmailEventKeys } from '@/api/types.gen';
import { RichTextEditor } from '@/components/RichTextEditor';
import { LoadingState } from '@/components/States';
import { useToast } from '@/components/Toast';
import { useMountEffect, usePageTitle } from '@/lib/hooks';
import { FieldMessage, hasErrors, rules, validate, ValidationSummary } from '@/lib/validation';
import { eventDisplayName } from './EmailTemplateList';

const ALL_EVENTS = [EmailEventKeys.InviteEmail, EmailEventKeys.ForgotPassword];

function tokensFor(eventKey: string): string[] {
  switch (eventKey) {
    case EmailEventKeys.InviteEmail: return ['FirstName', 'LastName', 'Email', 'RoleNames', 'ClinicNames', 'InviteLink', 'GeneratedPassword'];
    case EmailEventKeys.ForgotPassword: return ['FirstName', 'LastName', 'Email', 'ResetLink'];
    default: return [];
  }
}

export default function EmailTemplateEdit() {
  const params = useParams();
  const id = params.id ? Number(params.id) : null;
  usePageTitle(`${id != null ? 'Edit' : 'New'} Email Template - IntelliMed`);
  const navigate = useNavigate();
  const toast = useToast();
  const [model, setModel] = useState({ name: '', subject: '', bodyHtml: '', eventKey: '' });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [tried, setTried] = useState(false);
  const [copied, setCopied] = useState<string | null>(null);

  useMountEffect(async (alive) => {
    if (id != null) {
      const existing = await EmailTemplateApi.getById(id);
      if (existing && alive()) {
        setModel({ name: existing.name, subject: existing.subject, bodyHtml: existing.bodyHtml, eventKey: existing.eventKey ?? '' });
      }
    }
    if (alive()) setLoading(false);
  });

  const spec = { name: [rules.required('Name')], subject: [rules.required('Subject')], bodyHtml: [rules.required('BodyHtml')] };
  const errors = tried ? validate(model, spec) : {};

  const save = async (e: FormEvent) => {
    e.preventDefault();
    setTried(true);
    if (hasErrors(validate(model, spec))) return;
    setSaving(true);
    try {
      const request = { name: model.name, subject: model.subject, bodyHtml: model.bodyHtml, eventKey: model.eventKey || null };
      const ok = id != null ? await EmailTemplateApi.update(id, request) : await EmailTemplateApi.create(request);
      if (ok) navigate('/admin/email-templates');
      else toast.error('Failed to save template.');
    } finally {
      setSaving(false);
    }
  };

  const copyToken = async (token: string) => {
    try {
      await navigator.clipboard.writeText(`{{${token}}}`);
      setCopied(token);
      setTimeout(() => setCopied((c) => (c === token ? null : c)), 1500);
    } catch {
      // clipboard blocked: the token is still visible to copy by hand
    }
  };

  return (
    <>
      <h4 className="mb-4">{id != null ? 'Edit Email Template' : 'New Email Template'}</h4>
      {loading ? (
        <div className="card"><div className="card-body"><LoadingState text="Loading template..." compact={false} /></div></div>
      ) : (
        <div className="row g-3">
          <div className="col-lg-8">
            <div className="card">
              <div className="card-body">
                <form onSubmit={save} noValidate>
                  <ValidationSummary errors={errors} className="text-danger small mb-3" />
                  <div className="row g-3 mb-3">
                    <div className="col-md-8">
                      <label className="form-label">Template Name <span className="text-danger">*</span></label>
                      <input className="form-control" value={model.name} onChange={(e) => setModel({ ...model, name: e.target.value })} />
                      <FieldMessage messages={errors.name} />
                    </div>
                    <div className="col-md-4">
                      <label className="form-label">System Event</label>
                      <select className="form-select" value={model.eventKey} onChange={(e) => setModel({ ...model, eventKey: e.target.value })}>
                        <option value="">-- Unassigned --</option>
                        {ALL_EVENTS.map((key) => <option key={key} value={key}>{eventDisplayName(key)}</option>)}
                      </select>
                      <div className="form-text">Assigning this event unassigns any other template currently holding it.</div>
                    </div>
                  </div>
                  <div className="mb-3">
                    <label className="form-label">Subject <span className="text-danger">*</span></label>
                    <input className="form-control" value={model.subject} onChange={(e) => setModel({ ...model, subject: e.target.value })} />
                    <FieldMessage messages={errors.subject} />
                  </div>
                  <div className="mb-3">
                    <label className="form-label">Body <span className="text-danger">*</span></label>
                    <RichTextEditor value={model.bodyHtml} onChange={(html) => setModel((m) => ({ ...m, bodyHtml: html }))}
                                    uploadUrl="api/email-templates/upload-image" />
                    <FieldMessage messages={errors.bodyHtml} />
                  </div>
                  <div className="d-flex gap-2 mt-4">
                    <button type="submit" className="btn btn-primary" disabled={saving}>
                      {saving ? <><span className="spinner-border spinner-border-sm me-2" /><span>Saving...</span></> : <span><i className="bi bi-save me-1" />Save</span>}
                    </button>
                    <button type="button" className="btn btn-secondary" onClick={() => navigate('/admin/email-templates')}>Cancel</button>
                  </div>
                </form>
              </div>
            </div>
          </div>
          <div className="col-lg-4">
            <div className="card">
              <div className="card-header"><strong>Mail Merge Tokens</strong></div>
              <div className="card-body">
                <p className="small text-muted">Insert any of these into the subject or body — they'll be replaced when the email is sent. Click one to copy it.</p>
                {!model.eventKey ? (
                  <p className="small text-muted fst-italic">Assign a system event to see its tokens.</p>
                ) : (
                  <ul className="small mb-0 list-unstyled imed-token-list">
                    {tokensFor(model.eventKey).map((token) => (
                      <li key={token}>
                        <button type="button" className="btn btn-link btn-sm p-0 text-decoration-none" onClick={() => copyToken(token)} title="Copy to clipboard">
                          <code>{`{{${token}}}`}</code>
                        </button>
                        {copied === token && <span className="text-success ms-2"><i className="bi bi-check2" /> Copied</span>}
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
