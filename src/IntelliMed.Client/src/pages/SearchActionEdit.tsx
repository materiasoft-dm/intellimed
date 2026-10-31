import { useState, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router';
import { SearchActionApi } from '@/api/settings';
import { SearchActionType } from '@/api/types.gen';
import { LoadingState } from '@/components/States';
import { useToast } from '@/components/Toast';
import { useMountEffect, usePageTitle } from '@/lib/hooks';
import { FieldMessage, hasErrors, rules, validate, ValidationSummary } from '@/lib/validation';

interface ActionForm extends Record<string, unknown> {
  title: string;
  keywords: string;
  description: string;
  category: string;
  actionType: SearchActionType;
  target: string;
  pageKey: string;
  sortOrder: number;
}

const blankToNull = (v: string) => (v.trim() ? v : null);

export default function SearchActionEdit() {
  const params = useParams();
  const id = params.id ? Number(params.id) : null;
  usePageTitle(`${id != null ? 'Edit' : 'New'} Command Palette Action - IntelliMed`);
  const navigate = useNavigate();
  const toast = useToast();
  const [model, setModel] = useState<ActionForm>({
    title: '', keywords: '', description: '', category: '', actionType: SearchActionType.Navigate, target: '', pageKey: '', sortOrder: 0,
  });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [tried, setTried] = useState(false);

  useMountEffect(async (alive) => {
    if (id != null) {
      const a = await SearchActionApi.getById(id);
      if (a && alive()) {
        setModel({
          title: a.title, keywords: a.keywords ?? '', description: a.description ?? '', category: a.category,
          actionType: a.actionType, target: a.target, pageKey: a.pageKey ?? '', sortOrder: a.sortOrder,
        });
      }
    }
    if (alive()) setLoading(false);
  });

  const spec = { title: [rules.required('Title')], category: [rules.required('Category')], target: [rules.required('Target')] };
  const errors = tried ? validate(model, spec) : {};
  const set = <K extends keyof ActionForm>(key: K, value: ActionForm[K]) => setModel((m) => ({ ...m, [key]: value }));

  const save = async (e: FormEvent) => {
    e.preventDefault();
    setTried(true);
    if (hasErrors(validate(model, spec))) return;
    setSaving(true);
    try {
      const request = {
        title: model.title, keywords: blankToNull(model.keywords), description: blankToNull(model.description),
        category: model.category, actionType: model.actionType, target: model.target, pageKey: blankToNull(model.pageKey),
        sortOrder: model.sortOrder,
      };
      const ok = id != null ? await SearchActionApi.update(id, request) : await SearchActionApi.create(request);
      if (ok) navigate('/admin/search-actions');
      else toast.error('Failed to save action.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <h4 className="mb-4">{id != null ? 'Edit Action' : 'New Action'}</h4>
      {loading ? (
        <div className="card"><div className="card-body"><LoadingState text="Loading action..." compact={false} /></div></div>
      ) : (
        <div className="row g-3">
          <div className="col-lg-7">
            <div className="card">
              <div className="card-body">
                <form onSubmit={save} noValidate>
                  <ValidationSummary errors={errors} className="text-danger small mb-3" />
                  <div className="mb-3">
                    <label className="form-label">Title <span className="text-danger">*</span></label>
                    <input className="form-control" placeholder="e.g. Add Client" value={model.title} onChange={(e) => set('title', e.target.value)} />
                    <FieldMessage messages={errors.title} />
                  </div>
                  <div className="mb-3">
                    <label className="form-label">Keywords</label>
                    <input className="form-control" placeholder="e.g. patient create new register" value={model.keywords} onChange={(e) => set('keywords', e.target.value)} />
                    <div className="form-text">Space-separated synonym terms that should also match this action.</div>
                  </div>
                  <div className="mb-3">
                    <label className="form-label">Description</label>
                    <input className="form-control" placeholder="Subtitle shown under the title in results" value={model.description} onChange={(e) => set('description', e.target.value)} />
                  </div>
                  <div className="row g-3 mb-3">
                    <div className="col-md-6">
                      <label className="form-label">Category <span className="text-danger">*</span></label>
                      <input className="form-control" placeholder="e.g. Clinical" value={model.category} onChange={(e) => set('category', e.target.value)} />
                      <FieldMessage messages={errors.category} />
                    </div>
                    <div className="col-md-6">
                      <label className="form-label">Action Type</label>
                      <select className="form-select" value={model.actionType} onChange={(e) => set('actionType', Number(e.target.value) as SearchActionType)}>
                        <option value={SearchActionType.Navigate}>Navigate</option>
                        <option value={SearchActionType.Modal}>Modal</option>
                        <option value={SearchActionType.Info}>Info</option>
                      </select>
                    </div>
                  </div>
                  <div className="mb-3">
                    <label className="form-label">Target <span className="text-danger">*</span></label>
                    <input className="form-control" placeholder="e.g. /clients/add" value={model.target} onChange={(e) => set('target', e.target.value)} />
                    <div className="form-text">Route path for Navigate actions, or a modal identifier for Modal actions.</div>
                    <FieldMessage messages={errors.target} />
                  </div>
                  <div className="mb-3">
                    <label className="form-label">Page Key (optional)</label>
                    <input className="form-control" placeholder="e.g. clients/create" value={model.pageKey} onChange={(e) => set('pageKey', e.target.value)} />
                    <div className="form-text">Ties this action to a Role Configuration page key so it only shows for users with access. Leave blank to show to everyone.</div>
                  </div>
                  <div className="mb-3">
                    <label className="form-label">Sort Order</label>
                    <input type="number" className="form-control" style={{ maxWidth: 120 }} value={model.sortOrder} onChange={(e) => set('sortOrder', Number(e.target.value) || 0)} />
                  </div>
                  <div className="d-flex gap-2 mt-4">
                    <button type="submit" className="btn btn-primary" disabled={saving}>
                      {saving ? <><span className="spinner-border spinner-border-sm me-2" /><span>Saving...</span></> : <span><i className="bi bi-save me-1" />Save</span>}
                    </button>
                    <button type="button" className="btn btn-secondary" onClick={() => navigate('/admin/search-actions')}>Cancel</button>
                  </div>
                </form>
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
