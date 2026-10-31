import { useState } from 'react';
import { DrugInteractionApi } from '@/api/clinical';
import { InteractionSeverity, type DrugInteractionDto, type DrugInteractionImportResultDto, type UpdateDrugInteractionDto } from '@/api/types.gen';
import { TextArea, TextInput } from '@/components/Inputs';
import { Modal } from '@/components/Modal';
import { EmptyState, SkeletonRows } from '@/components/States';
import { formatDate, formatUtc } from '@/lib/dates';
import { downloadBlob } from '@/lib/format';
import { useMountEffect, usePageTitle } from '@/lib/hooks';

const SEVERITIES = [InteractionSeverity.Minor, InteractionSeverity.Moderate, InteractionSeverity.Major];

const severityBadgeClass = (s: InteractionSeverity) =>
  s === InteractionSeverity.Major ? 'bg-danger' : s === InteractionSeverity.Moderate ? 'bg-warning text-dark' : 'bg-secondary';

export default function DrugInteractionsAdmin() {
  usePageTitle('Drug Interactions - IntelliMed');
  const [interactions, setInteractions] = useState<DrugInteractionDto[]>([]);
  const [loading, setLoading] = useState(true);

  const [file, setFile] = useState<File | null>(null);
  const [importing, setImporting] = useState(false);
  const [lastImport, setLastImport] = useState<DrugInteractionImportResultDto | null>(null);
  const [importError, setImportError] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  const [editingId, setEditingId] = useState<number | null | undefined>(undefined);   // undefined = closed, null = new
  const [ingredientA, setIngredientA] = useState('');
  const [ingredientB, setIngredientB] = useState('');
  const [form, setForm] = useState<UpdateDrugInteractionDto>({ severity: InteractionSeverity.Minor, description: null, sourceDataset: null, isActive: true });
  const [formError, setFormError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const set = (patch: Partial<UpdateDrugInteractionDto>) => setForm((f) => ({ ...f, ...patch }));

  const load = async () => {
    setLoading(true);
    setInteractions(await DrugInteractionApi.list());
    setLoading(false);
  };
  useMountEffect(() => { void load(); });

  const openNew = () => {
    setIngredientA('');
    setIngredientB('');
    setForm({ severity: InteractionSeverity.Minor, description: null, sourceDataset: null, isActive: true });
    setFormError(null);
    setEditingId(null);
  };

  const openEdit = (i: DrugInteractionDto) => {
    setIngredientA(i.ingredientAName);
    setIngredientB(i.ingredientBName);
    setForm({ severity: i.severity, description: i.description, sourceDataset: i.sourceDataset, isActive: i.isActive });
    setFormError(null);
    setEditingId(i.id);
  };

  const save = async () => {
    if (!ingredientA.trim() || !ingredientB.trim()) { setFormError('Both ingredients are required.'); return; }
    setSaving(true);
    try {
      const ok = editingId != null
        ? await DrugInteractionApi.update(editingId, form)
        : await DrugInteractionApi.create({
          ingredientAName: ingredientA, ingredientBName: ingredientB, severity: form.severity,
          description: form.description, sourceDataset: form.sourceDataset,
        });
      if (!ok) { setFormError('Save failed — check your permissions.'); return; }
      setEditingId(undefined);
      await load();
    } finally {
      setSaving(false);
    }
  };

  const importCsv = async () => {
    if (!file) return;
    setImporting(true);
    setImportError(null);
    try {
      if (file.size > 20_000_000) throw new Error('the file is larger than 20 MB.');
      const result = await DrugInteractionApi.importCsv(file);
      if (!result) setImportError('Import failed — check the file format and your permissions.');
      else { setLastImport(result); await load(); }
    } catch (e) {
      setImportError(`Import failed: ${e instanceof Error ? e.message : String(e)}`);
    } finally {
      setImporting(false);
    }
  };

  const exportCsv = async () => {
    setExporting(true);
    setExportError(null);
    try {
      const blob = await DrugInteractionApi.exportCsv();
      if (!blob) setExportError('Export failed — check your permissions.');
      else downloadBlob(blob, `drug-interactions-${formatDate(new Date(), 'yyyyMMdd-HHmmss')}.csv`);
    } finally {
      setExporting(false);
    }
  };

  const spinner = <span className="spinner-border spinner-border-sm me-1" />;
  const isEdit = editingId != null;

  return (
    <>
      <h4 className="mb-1">Drug Interaction Reference Data</h4>
      <p className="text-muted small mb-4">
        Known interactions between active ingredients, used by the prescribing safety check. Ships empty — there's no
        bundled dataset (the obvious open source, DDInter, is CC-BY-NC and needs explicit publisher permission before it
        can be redistributed here). Populate via CSV import below.
      </p>

      {!loading && interactions.length === 0 && (
        <div className="alert alert-warning small py-2 mb-3">
          <i className="bi bi-exclamation-triangle me-1" />
          <strong>No interaction data loaded.</strong> The prescribing safety check will run but has nothing to check
          candidate medicines against — every check will report "no interactions found," which is not the same as
          "confirmed safe." Import a reference dataset below.
        </div>
      )}

      <div className="card mb-3">
        <div className="card-header"><strong>Import / Export</strong></div>
        <div className="card-body">
          <div className="row g-2 align-items-end">
            <div className="col-auto">
              <label className="form-label small mb-0" htmlFor="interactionImportFile">CSV file</label>
              <input type="file" id="interactionImportFile" className="form-control form-control-sm"
                     onChange={(e) => { setFile(e.target.files?.[0] ?? null); setLastImport(null); setImportError(null); }} />
            </div>
            <div className="col-auto">
              <button type="button" className="btn btn-sm btn-primary" disabled={!file || importing} onClick={() => void importCsv()}>
                {importing && spinner}<i className="bi bi-upload me-1" /> Import
              </button>
            </div>
            <div className="col-auto ms-auto">
              <button type="button" className="btn btn-sm btn-outline-secondary" disabled={exporting} onClick={() => void exportCsv()}>
                {exporting && spinner}<i className="bi bi-download me-1" /> Download CSV
              </button>
            </div>
          </div>
          <p className="text-muted small mt-2 mb-0">
            Columns: IngredientA, IngredientB, Severity (Minor/Moderate/Major), Description, SourceDataset. Ingredient
            names are matched case-insensitively against the existing catalog, or created new.
          </p>
          {lastImport ? (
            <div className="small text-muted mt-3">
              Last import {formatUtc(lastImport.importedAt)}: {lastImport.added} added, {lastImport.updated} updated,{' '}
              {lastImport.ingredientsAutoCreated} new ingredient(s) auto-created.
            </div>
          ) : importError && <div className="small text-danger mt-3">{importError}</div>}
          {exportError && <div className="small text-danger mt-3">{exportError}</div>}
        </div>
      </div>

      <div className="card">
        <div className="card-header d-flex justify-content-between align-items-center py-2">
          <strong>Reference Data ({interactions.length})</strong>
          <button className="btn btn-sm btn-primary" onClick={openNew}><i className="bi bi-plus-lg" /> Add Interaction</button>
        </div>
        <div className="card-body">
          {!loading && interactions.length === 0 ? (
            <EmptyState message="No interactions recorded." icon="bi-shield-check" />
          ) : (
            <div className="table-responsive">
              <table className="table table-sm table-hover align-middle mb-0">
                <thead>
                  <tr>
                    <th>Ingredient A</th>
                    <th>Ingredient B</th>
                    <th>Severity</th>
                    <th>Description</th>
                    <th>Source</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {loading ? <SkeletonRows columns={6} rows={5} /> : interactions.map((i) => (
                    <tr key={i.id} className={i.isActive ? '' : 'text-muted'}>
                      <td>{i.ingredientAName}</td>
                      <td>{i.ingredientBName}</td>
                      <td><span className={`badge ${severityBadgeClass(i.severity)}`}>{InteractionSeverity[i.severity]}</span></td>
                      <td>{i.description}</td>
                      <td className="text-muted small">{i.sourceDataset}</td>
                      <td className="text-end">
                        <button className="btn btn-sm btn-outline-secondary" onClick={() => openEdit(i)}>Edit</button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {editingId !== undefined && (
        <Modal title={`${isEdit ? 'Edit' : 'New'} Interaction`} onClose={() => setEditingId(undefined)}
               footer={<>
                 <button type="button" className="btn btn-sm btn-secondary" onClick={() => setEditingId(undefined)}>Cancel</button>
                 <button type="button" className="btn btn-sm btn-primary" disabled={saving} onClick={() => void save()}>{saving && spinner}Save</button>
               </>}>
          <div className="row g-2">
            <div className="col">
              <label className="form-label small mb-0">Ingredient A</label>
              <input className="form-control form-control-sm" value={ingredientA} disabled={isEdit} onChange={(e) => setIngredientA(e.target.value)} />
            </div>
            <div className="col">
              <label className="form-label small mb-0">Ingredient B</label>
              <input className="form-control form-control-sm" value={ingredientB} disabled={isEdit} onChange={(e) => setIngredientB(e.target.value)} />
            </div>
          </div>
          <div className="mb-2 mt-2">
            <label className="form-label small mb-0">Severity</label>
            {/* option values are the enum names, as Blazor bound them */}
            <select className="form-select form-select-sm" value={InteractionSeverity[form.severity]}
                    onChange={(e) => set({ severity: InteractionSeverity[e.target.value as keyof typeof InteractionSeverity] })}>
              {SEVERITIES.map((s) => <option key={s} value={InteractionSeverity[s]}>{InteractionSeverity[s]}</option>)}
            </select>
          </div>
          <div className="mb-2">
            <label className="form-label small mb-0">Description</label>
            <TextArea className="form-control form-control-sm" value={form.description} onChange={(v) => set({ description: v })} />
          </div>
          <div className="mb-2">
            <label className="form-label small mb-0">Source dataset</label>
            <TextInput className="form-control form-control-sm" value={form.sourceDataset} onChange={(v) => set({ sourceDataset: v })}
                       placeholder="e.g. self-curated, dataset name/version" />
          </div>
          {isEdit && (
            <div className="form-check">
              <input className="form-check-input" type="checkbox" id="chkActive" checked={form.isActive} onChange={(e) => set({ isActive: e.target.checked })} />
              <label className="form-check-label small" htmlFor="chkActive">Active</label>
            </div>
          )}
          {formError && <div className="small text-danger mt-2" role="alert">{formError}</div>}
        </Modal>
      )}
    </>
  );
}
