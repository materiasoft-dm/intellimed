import { useRef, useState } from 'react';
import { MedicineApi, SyncLostError } from '@/api/clinical';
import {
  MedicineSchedule, MedicineSource, PbsRestrictionLevel, type ArtgEnrichmentResultDto, type IngredientRefDto,
  type MedicineDto, type MedicineImportResultDto, type SyncProgressDto, type UpdateMedicineDto,
} from '@/api/types.gen';
import { EnumOptions, NumberSelect, TextInput } from '@/components/Inputs';
import { Modal } from '@/components/Modal';
import { EmptyState, LoadingState } from '@/components/States';
import { formatDate, formatUtc } from '@/lib/dates';
import { downloadBlob } from '@/lib/format';
import { LoadMoreSentinel, useInfiniteList } from '@/lib/infinite';
import { useMountEffect, usePageTitle } from '@/lib/hooks';
import { restrictionBadgeClass, restrictionLabel, scheduleBadgeClass } from '@/lib/medicines';

const PAGE_SIZE = 50;
const SYNC_PHASES = ['gather', 'import', 'enrich'] as const;

const ingredientLabel = (i: IngredientRefDto) => (i.atcCode == null ? i.name : `${i.name} (${i.atcCode})`);
const blankForm = (): UpdateMedicineDto => ({
  name: '', genericName: null, strength: null, form: null, packSize: null, manufacturer: null, isPbsListed: false,
  isRpbsListed: false, restrictionLevel: null, schedule: null, activeIngredientNames: null, isActive: true,
} as UpdateMedicineDto);

const RESTRICTION_LABELS = { [PbsRestrictionLevel.AuthorityRequired]: 'Authority Required' };

/**
 * Which Sync phase we're in, inferred from the whole accumulated log rather than the latest line — the last line
 * of a phase (e.g. the import tally) doesn't necessarily repeat a phase keyword.
 */
function syncPhase(log: string[]): (typeof SYNC_PHASES)[number] {
  if (log.some((m) => /enrich/i.test(m))) return 'enrich';
  if (log.some((m) => /importing into catalogue/i.test(m))) return 'import';
  return 'gather';
}

export default function MedicinesAdmin() {
  usePageTitle('Medicine Catalog - IntelliMed');

  // ---- catalogue list (infinite scroll) ----
  const [query, setQuery] = useState('');
  const [source, setSource] = useState<MedicineSource | null>(null);
  // read by the page fetcher, which runs before React re-renders with the new filter
  const filters = useRef({ query: '', source: null as MedicineSource | null });
  const list = useInfiniteList<MedicineDto>(async (page, pageSize) => {
    // the search endpoint returns a plain list, so ask for one extra row to learn whether another page exists
    const rows = await MedicineApi.searchPage(filters.current.query, filters.current.source, (page - 1) * pageSize, pageSize + 1);
    const items = rows.slice(0, pageSize);
    const more = rows.length > pageSize;
    return {
      items, totalCount: (page - 1) * pageSize + items.length + (more ? 1 : 0), page, pageSize,
      totalPages: more ? page + 1 : page, hasPreviousPage: page > 1, hasNextPage: more,
    };
  }, PAGE_SIZE);
  const search = (patch: Partial<typeof filters.current>) => {
    filters.current = { ...filters.current, ...patch };
    void list.reset();
  };
  useMountEffect(() => { void list.reset(); });

  // ---- sync ----
  const [syncing, setSyncing] = useState(false);
  const [syncError, setSyncError] = useState<string | null>(null);
  const [progressStatus, setProgressStatus] = useState('');
  const [progressLog, setProgressLog] = useState<string[]>([]);
  const [syncJobId, setSyncJobId] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState(false);
  const [lastSync, setLastSync] = useState<SyncProgressDto | null>(null);

  // ---- import / export / ARTG ----
  const [importFile, setImportFile] = useState<File | null>(null);
  const [importing, setImporting] = useState(false);
  const [lastImport, setLastImport] = useState<MedicineImportResultDto | null>(null);
  const [importError, setImportError] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [artgFile, setArtgFile] = useState<File | null>(null);
  const [enriching, setEnriching] = useState(false);
  const [artgResult, setArtgResult] = useState<ArtgEnrichmentResultDto | null>(null);
  const [artgError, setArtgError] = useState<string | null>(null);

  // ---- create / view / edit ----
  const [modal, setModal] = useState<{ id: number | null; readOnly: boolean } | null>(null);
  const [form, setForm] = useState<UpdateMedicineDto>(blankForm);
  const [formError, setFormError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const set = (patch: Partial<UpdateMedicineDto>) => setForm((f) => ({ ...f, ...patch }));

  const openNew = () => {
    setForm(blankForm());
    setFormError(null);
    setModal({ id: null, readOnly: false });
  };

  // Manual entries are editable; synced ones open read-only (they're only changed by re-importing).
  const openRecord = (m: MedicineDto) => {
    setForm({
      name: m.name, genericName: m.genericName, strength: m.strength, form: m.form, packSize: m.packSize,
      manufacturer: m.manufacturer, isPbsListed: m.isPbsListed, isRpbsListed: m.isRpbsListed,
      restrictionLevel: m.restrictionLevel, schedule: m.schedule, isActive: m.isActive,
      activeIngredientNames: m.activeIngredients.map((i) => i.name).join(';'),
    } as UpdateMedicineDto);
    setFormError(null);
    setModal({ id: m.id, readOnly: m.source !== MedicineSource.Manual });
  };

  const save = async () => {
    if (!modal) return;
    if (!form.name.trim()) { setFormError('Name is required.'); return; }
    setSaving(true);
    try {
      let ok: boolean;
      if (modal.id != null) {
        ok = await MedicineApi.update(modal.id, form);
      } else {
        const { isActive: _ignored, ...create } = form;
        ok = (await MedicineApi.create(create)) != null;
      }
      if (!ok) { setFormError('Save failed — check your permissions.'); return; }
      setModal(null);
      void list.reset();
    } finally {
      setSaving(false);
    }
  };

  const deactivate = async (m: MedicineDto) => {
    await MedicineApi.deactivate(m.id);
    void list.reset();
  };

  const sync = async () => {
    setSyncing(true);
    setSyncError(null);
    setLastSync(null);
    setProgressStatus('Starting...');
    setProgressLog([]);
    setSyncJobId(null);
    try {
      // each line already carries the server's "[HH:mm:ss]" (UTC) stamp, so no second client-side one here
      const result = await MedicineApi.sync(
        (line) => { setProgressLog((log) => [...log, line]); setProgressStatus(line); },
        (jobId) => setSyncJobId(jobId),
      );
      if (!result) {
        setSyncError('Sync failed — check your permissions.');
      } else {
        setLastSync(result);
        if (result.error != null) setSyncError(`Sync failed: ${result.error}`);
        if (result.importResult) void list.reset();
      }
    } catch (e) {
      const message = e instanceof SyncLostError ? e.message : e instanceof Error ? e.message : String(e);
      setSyncError(`Sync failed: ${message}`);
      setProgressLog((log) => [...log, `[${formatDate(new Date(), 'HH:mm:ss')}] ERROR: ${message}`]);
    }
    // a brief pause so the final status is seen before the dialog closes
    await new Promise((r) => setTimeout(r, 800));
    setSyncJobId(null);
    setSyncing(false);
  };

  const cancelSync = async () => {
    if (!syncJobId) return;
    setCancelling(true);
    await MedicineApi.cancelSync(syncJobId);
    setCancelling(false);
  };

  const exportCsv = async () => {
    setExporting(true);
    setExportError(null);
    try {
      const blob = await MedicineApi.exportCsv();
      if (!blob) setExportError('Export failed — check your permissions.');
      else downloadBlob(blob, `medicine-catalogue-${formatDate(new Date(), 'yyyyMMdd-HHmmss')}.csv`);
    } finally {
      setExporting(false);
    }
  };

  const importCsv = async () => {
    if (!importFile) return;
    setImporting(true);
    setImportError(null);
    try {
      if (importFile.size > 20_000_000) throw new Error('the file is larger than 20 MB.');
      const result = await MedicineApi.importCsv(importFile);
      if (!result) setImportError('Import failed — check the file format and your permissions.');
      else { setLastImport(result); void list.reset(); }
    } catch (e) {
      setImportError(`Import failed: ${e instanceof Error ? e.message : String(e)}`);
    } finally {
      setImporting(false);
    }
  };

  const enrichFromArtg = async () => {
    if (!artgFile) return;
    setEnriching(true);
    setArtgError(null);
    try {
      // the full ARTG Medicines export is ~95 MB; the cap is generous so a slightly larger export still reaches the API
      if (artgFile.size > 200_000_000) throw new Error('the file is larger than 200 MB.');
      const result = await MedicineApi.enrichFromArtg(artgFile);
      if (!result) setArtgError('Backfill failed — check the file is a Medicines-filtered ARTG CSV export, and your permissions.');
      else { setArtgResult(result); void list.reset(); }   // refresh so the new schedule badges show
    } catch (e) {
      setArtgError(`Backfill failed: ${e instanceof Error ? e.message : String(e)}`);
    } finally {
      setEnriching(false);
    }
  };

  const phase = syncPhase(progressLog);
  const stepClass = (step: (typeof SYNC_PHASES)[number]) => {
    const current = SYNC_PHASES.indexOf(phase);
    const target = SYNC_PHASES.indexOf(step);
    return target < current ? 'bg-success' : target === current ? 'bg-primary' : 'bg-secondary';
  };
  const readOnly = modal?.readOnly ?? false;
  const spinner = <span className="spinner-border spinner-border-sm me-1" />;

  return (
    <>
      <h4 className="mb-1">Medicine Catalog</h4>
      <p className="text-muted small mb-4">
        A self-hosted medicines reference list — built without a MIMS subscription. Today this is seeded from the PBS
        API Public v3 (Australian Pharmaceutical Benefits Scheme), Wikidata (CC0-licensed generic names with ATC codes),
        and CSV import. See the README for the licensing status of each source.
      </p>

      <div className="card mb-3">
        <div className="card-header"><strong>Sync from Sources</strong></div>
        <div className="card-body">
          <p className="small text-muted mb-2">
            Fetches candidate rows from every registered data source (PBS API Public v3 — Australian Pharmaceutical
            Benefits Scheme, and Wikidata — CC0-licensed generic names with ATC codes), imports them directly into the
            catalog below, then automatically looks up each newly-added ingredient on Wikidata for an ATC code. Takes
            10-15 minutes — almost all of it spent waiting out the PBS API's rate limit, not local work.
          </p>
          <button type="button" className="btn btn-sm btn-outline-primary" disabled={syncing} onClick={() => void sync()}>
            {syncing && spinner}<i className="bi bi-arrow-repeat me-1" /> Sync from Sources
          </button>
          {syncError && <div className="small text-danger mt-3">{syncError}</div>}
          {lastSync?.importResult && (
            <div className="small text-muted mt-3">
              Last sync: {lastSync.importResult.added} added, {lastSync.importResult.updated} updated,{' '}
              {lastSync.importResult.deactivated} deactivated (no longer PBS-listed),{' '}
              {lastSync.importResult.unmatched} new ingredient(s) auto-created.
              {lastSync.enrichmentResult && (
                <> Then enriched {lastSync.enrichmentResult.enriched} of {lastSync.enrichmentResult.totalProcessed} new
                  ingredient(s) ({lastSync.enrichmentResult.notFound} not found on Wikidata).</>
              )}
            </div>
          )}
        </div>
      </div>

      {syncing && (
        <Modal title={<><i className="bi bi-arrow-repeat me-1" /> Syncing Medicine Catalog</>} size="lg" staticBackdrop
               footer={
                 <button type="button" className="btn btn-sm btn-outline-danger" disabled={syncJobId == null || cancelling} onClick={() => void cancelSync()}>
                   {cancelling && spinner}Cancel Sync
                 </button>
               }>
          <div className="d-flex gap-2 mb-3">
            <span className={`badge ${stepClass('gather')}`}>1. Gather from sources</span>
            <span className={`badge ${stepClass('import')}`}>2. Import</span>
            <span className={`badge ${stepClass('enrich')}`}>3. Enrich ingredients</span>
          </div>
          <div className="mb-3">
            <div className="small text-muted mb-1">{progressStatus}</div>
            <div className="progress" style={{ height: 8 }}>
              <div className="progress-bar progress-bar-striped progress-bar-animated bg-primary" role="progressbar" style={{ width: '100%' }} />
            </div>
          </div>
          <div className="border rounded bg-light p-2" style={{ maxHeight: 300, overflowY: 'auto' }}>
            <div className="small font-monospace">
              {progressLog.map((entry, i) => (
                <div key={i} className={`text-${/failed|error/i.test(entry) ? 'danger' : 'muted'} mb-1`}>{entry}</div>
              ))}
            </div>
          </div>
        </Modal>
      )}

      <div className="card mb-3">
        <div className="card-header"><strong>Import / Export</strong></div>
        <div className="card-body">
          <div className="row g-2 align-items-end">
            <div className="col-auto">
              <label className="form-label small mb-0" htmlFor="medicineImportFile">CSV file</label>
              <input type="file" id="medicineImportFile" className="form-control form-control-sm"
                     onChange={(e) => { setImportFile(e.target.files?.[0] ?? null); setLastImport(null); setImportError(null); }} />
            </div>
            <div className="col-auto">
              <button type="button" className="btn btn-sm btn-primary" disabled={!importFile || importing} onClick={() => void importCsv()}>
                {importing && spinner}<i className="bi bi-upload me-1" /> Import
              </button>
            </div>
            <div className="col-auto ms-auto">
              <button type="button" className="btn btn-sm btn-outline-secondary" disabled={exporting} onClick={() => void exportCsv()}>
                {exporting && spinner}<i className="bi bi-download me-1" /> Download Catalogue CSV
              </button>
            </div>
          </div>
          {lastImport ? (
            <div className="small text-muted mt-3">
              Last import {formatUtc(lastImport.importedAt)}: {lastImport.added} added, {lastImport.updated} updated,{' '}
              {lastImport.deactivated} deactivated (no longer PBS-listed), {lastImport.unmatched} new ingredient(s)
              auto-created (not AMT-confirmed).
            </div>
          ) : importError && <div className="small text-danger mt-3">{importError}</div>}
          {exportError && <div className="small text-danger mt-3">{exportError}</div>}
        </div>
      </div>

      <div className="card mb-3">
        <div className="card-header"><strong>Poisons Scheduling (TGA ARTG)</strong></div>
        <div className="card-body">
          <p className="text-muted small">
            The PBS feed publishes no poisons scheduling, so synced medicines have no S2/S3/S4/S8 value and the
            prescribing wizard's Schedule&nbsp;8 check can't fire for them. Upload a TGA ARTG export to backfill it,
            along with ARTG ID, route and dosage form.
          </p>
          <div className="alert alert-secondary small py-2">
            <i className="bi bi-info-circle me-1" />
            Export from the{' '}
            <a href="https://compliance.health.gov.au/artg/" target="_blank" rel="noopener">ARTG Search Visualisation Tool</a>{' '}
            with the <strong>Medicines</strong> filter applied, then save as CSV. The unfiltered export has only 8 columns
            and carries no scheduling. Existing values are never overwritten — only empty fields are filled.
          </div>
          <div className="row g-2 align-items-end">
            <div className="col-auto">
              <label className="form-label small mb-0" htmlFor="artgEnrichFile">ARTG Medicines CSV</label>
              <input type="file" id="artgEnrichFile" className="form-control form-control-sm"
                     onChange={(e) => { setArtgFile(e.target.files?.[0] ?? null); setArtgResult(null); setArtgError(null); }} />
            </div>
            <div className="col-auto">
              <button type="button" className="btn btn-sm btn-primary" disabled={!artgFile || enriching} onClick={() => void enrichFromArtg()}>
                {enriching && spinner}<i className="bi bi-shield-plus me-1" /> Backfill Scheduling
              </button>
            </div>
          </div>
          {artgResult ? (
            <div className="small mt-3">
              <div className="text-success"><i className="bi bi-check-circle me-1" />{artgResult.message}</div>
              <div className="text-muted mt-1">
                {artgResult.artgRows} ARTG row(s) read · {artgResult.unmatched} catalogue row(s) had no brand match ·{' '}
                {artgResult.alreadyComplete} already complete.
              </div>
              {Object.keys(artgResult.scheduleBreakdown).length > 0 && (
                <div className="mt-1">
                  Catalogue now scheduled:{' '}
                  {Object.entries(artgResult.scheduleBreakdown).sort(([a], [b]) => a.localeCompare(b)).map(([k, v]) => (
                    <span key={k} className="badge bg-light text-dark border me-1">{k}: {v}</span>
                  ))}
                </div>
              )}
            </div>
          ) : artgError && <div className="small text-danger mt-3">{artgError}</div>}
        </div>
      </div>

      <div className="card">
        <div className="card-header d-flex justify-content-between align-items-center py-2">
          <strong>Search</strong>
          <button className="btn btn-sm btn-primary" onClick={openNew}><i className="bi bi-plus-lg" /> Add Medicine</button>
        </div>
        <div className="card-body">
          <div className="row g-2 mb-3">
            <div className="col-auto">
              <input type="text" className="form-control form-control-sm" style={{ width: 280 }} placeholder="Search by name..."
                     value={query} onChange={(e) => { setQuery(e.target.value); search({ query: e.target.value }); }} />
            </div>
            <div className="col-auto">
              <NumberSelect enumType={MedicineSource} className="form-select form-select-sm" style={{ width: 180 }} value={source}
                            onChange={(v) => { setSource(v as MedicineSource | null); search({ source: v as MedicineSource | null }); }}>
                <option value="">All sources</option>
                <option value={MedicineSource.Manual}>Manually created</option>
                <option value={MedicineSource.Synced}>Synced / imported</option>
              </NumberSelect>
            </div>
          </div>
          {list.loading && list.items.length === 0 ? (
            <LoadingState text="Searching..." />
          ) : list.items.length === 0 ? (
            <EmptyState message="No medicines found. Import a CSV above to get started." icon="bi-capsule" />
          ) : (
            <>
              <div className={`table-responsive imed-grid-body ${list.loading ? 'is-refreshing' : ''}`} aria-busy={list.loading}>
                <table className="table table-sm table-hover align-middle mb-0">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Generic Name</th>
                      <th>Strength</th>
                      <th>Form</th>
                      <th>Pack Size</th>
                      <th>Active Ingredient(s)</th>
                      <th>Schedule</th>
                      <th>Subsidy</th>
                      <th>Restriction</th>
                      <th>Source</th>
                      <th>Last Updated</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {list.items.map((m) => (
                      <tr key={m.id}>
                        <td>
                          <a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); openRecord(m); }}>{m.name}</a>
                        </td>
                        <td>{m.genericName}</td>
                        <td>{m.strength}</td>
                        <td>{m.form}</td>
                        <td>{m.packSize}</td>
                        <td>{m.activeIngredients.map(ingredientLabel).join(', ')}</td>
                        <td>
                          {m.schedule != null ? (
                            <span className={`badge ${scheduleBadgeClass(m.schedule)}`}>{MedicineSchedule[m.schedule]}</span>
                          ) : (
                            // blank would read as "unscheduled"; PBS publishes no poisons scheduling, so it is genuinely unknown
                            <span className="badge bg-light text-muted border" title="PBS does not publish poisons scheduling — verify independently">unknown</span>
                          )}
                        </td>
                        <td>
                          <span className="d-inline-flex gap-1">
                            {m.isPbsListed && <span className="badge bg-info text-dark">PBS</span>}
                            {m.isRpbsListed && <span className="badge bg-info text-dark">RPBS</span>}
                          </span>
                        </td>
                        <td>
                          {m.restrictionLevel != null && (
                            <span className={`badge ${restrictionBadgeClass(m.restrictionLevel)}`}>{restrictionLabel(m.restrictionLevel)}</span>
                          )}
                        </td>
                        <td>
                          <span className={`badge ${m.source === MedicineSource.Manual ? 'bg-secondary' : 'bg-light text-dark border'}`}>
                            {m.source === MedicineSource.Manual ? 'Manual' : 'Synced'}
                          </span>
                        </td>
                        <td className="text-muted small text-nowrap">{formatUtc(m.lastSyncedAt) || '—'}</td>
                        <td className="text-end">
                          {m.source === MedicineSource.Manual && (
                            <button className="btn btn-sm btn-outline-danger" onClick={() => void deactivate(m)}>Deactivate</button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <LoadMoreSentinel onVisible={() => void list.loadMore()} loading={list.loadingMore} hasMore={list.hasMore}
                                shown={list.items.length} total={list.total} noun="medicines" totalKnown={false} />
            </>
          )}
        </div>
      </div>

      <div className="small text-muted mt-4 border-top pt-3">
        <strong>PBS data copyright:</strong> Copyright (c) 2024 Department of Health, Commonwealth of Australia.
        Permission is granted to use and redistribute this content, as long as all copyright statements are retained.
        Permission is not granted to modify this content.{' '}
        <a href="https://www.health.gov.au/using-our-websites/copyright" target="_blank" rel="noopener">More information</a>.
      </div>

      {modal && (
        <Modal title={`${readOnly ? 'View' : modal.id != null ? 'Edit' : 'New'} Medicine`} onClose={() => setModal(null)}
               // the footer has its own "Close" button for read-only rows; keep the X's name distinct from it
               closeLabel="Dismiss"
               footer={readOnly ? (
                 <button type="button" className="btn btn-sm btn-secondary" onClick={() => setModal(null)}>Close</button>
               ) : (
                 <>
                   <button type="button" className="btn btn-sm btn-secondary" onClick={() => setModal(null)}>Cancel</button>
                   <button type="button" className="btn btn-sm btn-primary" disabled={saving} onClick={() => void save()}>
                     {saving && spinner}Save
                   </button>
                 </>
               )}>
          {readOnly && (
            <div className="alert alert-secondary small py-2 mb-3">
              This medicine was synced/imported, not manually created — it can be viewed here but only edited by
              re-importing an updated CSV.
            </div>
          )}
          <fieldset disabled={readOnly} className="imed-fieldset">
            <div className="mb-2">
              <label className="form-label small mb-0">Name</label>
              <TextInput className="form-control form-control-sm" nullable={false} value={form.name} onChange={(v) => set({ name: v ?? '' })} />
            </div>
            <div className="row g-2">
              <div className="col">
                <label className="form-label small mb-0">Generic Name</label>
                <TextInput className="form-control form-control-sm" value={form.genericName} onChange={(v) => set({ genericName: v })} />
              </div>
              <div className="col">
                <label className="form-label small mb-0">Strength</label>
                <TextInput className="form-control form-control-sm" value={form.strength} onChange={(v) => set({ strength: v })} />
              </div>
            </div>
            <div className="row g-2 mt-1">
              <div className="col">
                <label className="form-label small mb-0">Form</label>
                <TextInput className="form-control form-control-sm" value={form.form} onChange={(v) => set({ form: v })} />
              </div>
              <div className="col">
                <label className="form-label small mb-0">Pack Size</label>
                <TextInput className="form-control form-control-sm" value={form.packSize} onChange={(v) => set({ packSize: v })} />
              </div>
            </div>
            <div className="mb-2 mt-1">
              <label className="form-label small mb-0">Manufacturer</label>
              <TextInput className="form-control form-control-sm" value={form.manufacturer} onChange={(v) => set({ manufacturer: v })} />
            </div>
            <div className="mb-2 mt-2">
              <label className="form-label small mb-0">Active Ingredient(s)</label>
              <TextInput className="form-control form-control-sm" value={form.activeIngredientNames} onChange={(v) => set({ activeIngredientNames: v })}
                         placeholder="Semicolon-separated, e.g. Paracetamol;Codeine" />
            </div>
            <div className="row g-2 mt-1">
              <div className="col">
                <label className="form-label small mb-0">Schedule</label>
                <NumberSelect enumType={MedicineSchedule} className="form-select form-select-sm" value={form.schedule} onChange={(v) => set({ schedule: v as MedicineSchedule | null })}>
                  <option value="">Unknown</option>
                  <EnumOptions enumType={MedicineSchedule} />
                </NumberSelect>
              </div>
              <div className="col">
                <label className="form-label small mb-0">Restriction</label>
                <NumberSelect enumType={PbsRestrictionLevel} className="form-select form-select-sm" value={form.restrictionLevel} onChange={(v) => set({ restrictionLevel: v as PbsRestrictionLevel | null })}>
                  <option value="">N/A</option>
                  <EnumOptions enumType={PbsRestrictionLevel} labels={RESTRICTION_LABELS} />
                </NumberSelect>
              </div>
            </div>
            <div className="row g-2 mt-2">
              <div className="col d-flex align-items-center">
                <div className="form-check">
                  <input className="form-check-input" type="checkbox" id="chkPbs" checked={form.isPbsListed} onChange={(e) => set({ isPbsListed: e.target.checked })} />
                  <label className="form-check-label small" htmlFor="chkPbs">PBS listed</label>
                </div>
              </div>
              <div className="col d-flex align-items-center">
                <div className="form-check">
                  <input className="form-check-input" type="checkbox" id="chkRpbs" checked={form.isRpbsListed} onChange={(e) => set({ isRpbsListed: e.target.checked })} />
                  <label className="form-check-label small" htmlFor="chkRpbs">RPBS listed</label>
                </div>
              </div>
            </div>
            {modal.id != null && (
              <div className="form-check mt-2">
                <input className="form-check-input" type="checkbox" id="chkActive" checked={form.isActive} onChange={(e) => set({ isActive: e.target.checked })} />
                <label className="form-check-label small" htmlFor="chkActive">Active</label>
              </div>
            )}
          </fieldset>
          {formError && <div className="small text-danger mt-2" role="alert">{formError}</div>}
        </Modal>
      )}
    </>
  );
}
