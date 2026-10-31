import { useRef, useState, type FormEvent } from 'react';
import { FeeScheduleApi } from '@/api/billing';
import { LookupApi } from '@/api/clients';
import {
  RoundingTypeEnum, type BillingItemDto, type FeeScheduleDto, type FeeScheduleItemDto, type FeeScheduleItemHistoryDto,
  type FeeScheduleSearchDto, type HealthFundDto, type UpdateFeeScheduleItemOverridesDto,
} from '@/api/types.gen';
import { BillingItemPicker } from '@/components/BillingItemPicker';
import { CommitNumberInput } from '@/components/CommitNumberInput';
import { DataGrid, type Column } from '@/components/DataGrid';
import { NumberInput, NumberSelect, TextInput } from '@/components/Inputs';
import { Modal } from '@/components/Modal';
import { EmptyState, LoadingState } from '@/components/States';
import { useToast } from '@/components/Toast';
import { formatUtc } from '@/lib/dates';
import { money } from '@/lib/format';
import { useMountEffect, usePageTitle, usePagedGrid } from '@/lib/hooks';
import { IntersectionSentinel } from '@/lib/infinite';
import { FieldMessage, hasErrors, rules, validate, ValidationSummary } from '@/lib/validation';

const ITEMS_PAGE = 50;

interface ScheduleForm extends Record<string, unknown> {
  code: string;
  description: string;
  note: string | null;
  healthFundId: number | null;
  feeTableId: number | null;
  roundingType: RoundingTypeEnum;
  isArchived: boolean;
  isHealthFund: boolean;
  sourceUrl: string | null;
}
const blankForm = (): ScheduleForm => ({ code: '', description: '', note: null, healthFundId: null, feeTableId: null, roundingType: RoundingTypeEnum.Exact, isArchived: false, isHealthFund: false, sourceUrl: null });

type Filter = Pick<FeeScheduleSearchDto, 'code' | 'description' | 'includeArchived'>;

export default function FeeSchedules() {
  usePageTitle('Fee Schedules - IntelliMed');
  const toast = useToast();
  const [filter, setFilter] = useState<Filter>({ code: null, description: null, includeArchived: false });
  const filterRef = useRef(filter);
  filterRef.current = filter;
  const grid = usePagedGrid<FeeScheduleDto>((page, pageSize) => FeeScheduleApi.search({ ...filterRef.current, page, pageSize }), 10);
  const [selected, setSelected] = useState<FeeScheduleDto | null>(null);
  const [healthFunds, setHealthFunds] = useState<HealthFundDto[]>([]);
  const [seeding, setSeeding] = useState<'bb' | 'dva' | 'wc' | null>(null);

  // dialog
  const [dialog, setDialog] = useState<{ editingId: number | null; form: ScheduleForm; tab: 'General' | 'Items'; feeTableOptions: FeeScheduleDto[] } | null>(null);
  const [dialogSaving, setDialogSaving] = useState(false);
  const [tried, setTried] = useState(false);
  const [lastFetch, setLastFetch] = useState<{ at: string | null; result: string | null }>({ at: null, result: null });

  // items (infinite scroll, 50 at a time)
  const [items, setItems] = useState<FeeScheduleItemDto[]>([]);
  const [itemsTotal, setItemsTotal] = useState(0);
  const [itemsLoading, setItemsLoading] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const itemsBusy = useRef(false);
  const [copyingFromMbs, setCopyingFromMbs] = useState(false);
  const [fetchingNow, setFetchingNow] = useState(false);
  const [importing, setImporting] = useState(false);
  const [importSource, setImportSource] = useState<number | null>(null);
  const [importPercent, setImportPercent] = useState(0);
  const [importFlat, setImportFlat] = useState(0);
  const [pickerOpen, setPickerOpen] = useState(false);

  // history / overrides
  const [history, setHistory] = useState<{ label: string; entries: FeeScheduleItemHistoryDto[] | null } | null>(null);
  const [overrides, setOverrides] = useState<{ item: FeeScheduleItemDto; model: UpdateFeeScheduleItemOverridesDto; orderNum: number; fee: number } | null>(null);
  const [overridesSaving, setOverridesSaving] = useState(false);

  useMountEffect(async () => {
    grid.load(1);
    setHealthFunds(await LookupApi.healthFunds());
  });

  const editingId = dialog?.editingId ?? null;
  const hasMoreItems = items.length < itemsTotal;

  const loadItems = async (scheduleId: number) => {
    setItemsLoading(true);
    try {
      const page = await FeeScheduleApi.itemsPaged(scheduleId, 0, ITEMS_PAGE);
      setItems(page.items);
      setItemsTotal(page.totalCount);
    } finally {
      setItemsLoading(false);
    }
  };

  const loadMoreItems = async () => {
    if (itemsBusy.current || !hasMoreItems || editingId == null) return;
    itemsBusy.current = true;
    setLoadingMore(true);
    try {
      const page = await FeeScheduleApi.itemsPaged(editingId, items.length, ITEMS_PAGE);
      setItems((all) => [...all, ...page.items]);
      setItemsTotal(page.totalCount);
    } finally {
      itemsBusy.current = false;
      setLoadingMore(false);
    }
  };

  const openCreate = async () => {
    setTried(false);
    setItems([]);
    setItemsTotal(0);
    setLastFetch({ at: null, result: null });
    setDialog({ editingId: null, form: blankForm(), tab: 'General', feeTableOptions: await FeeScheduleApi.active() });
  };

  const openEdit = async (s: FeeScheduleDto) => {
    setSelected(s);
    setTried(false);
    setImportSource(null);
    setImportPercent(0);
    setImportFlat(0);
    setLastFetch({ at: s.lastFetchedAt, result: s.lastFetchResult });
    // a schedule can't borrow rates from itself
    const options = (await FeeScheduleApi.active()).filter((x) => x.id !== s.id);
    setDialog({
      editingId: s.id, tab: 'General', feeTableOptions: options,
      form: {
        code: s.code, description: s.description, note: s.note, healthFundId: s.healthFundId, feeTableId: s.feeTableId,
        roundingType: s.roundingType, isArchived: s.isArchived, isHealthFund: s.isHealthFund, sourceUrl: s.sourceUrl,
      },
    });
    await loadItems(s.id);
  };

  const setForm = (patch: Partial<ScheduleForm>) => setDialog((d) => (d ? { ...d, form: { ...d.form, ...patch } } : d));
  const spec = { code: [rules.required('Code')], description: [rules.required('Description')] };
  const errors = tried && dialog ? validate(dialog.form, spec) : {};

  const saveSchedule = async (e: FormEvent) => {
    e.preventDefault();
    if (!dialog) return;
    setTried(true);
    if (hasErrors(validate(dialog.form, spec))) return;
    setDialogSaving(true);
    try {
      const { isArchived, ...common } = dialog.form;
      const ok = dialog.editingId != null
        ? await FeeScheduleApi.update(dialog.editingId, { ...common, isArchived })
        : (await FeeScheduleApi.create(common)) != null;
      if (ok) {
        toast.success(dialog.editingId != null ? 'Fee schedule updated.' : 'Fee schedule created.');
        setDialog(null);
        setSelected(null);
        grid.reload();
      } else {
        toast.error('Failed to save fee schedule.');
      }
    } finally {
      setDialogSaving(false);
    }
  };

  const archiveSelected = async () => {
    if (!selected) return;
    const ok = await FeeScheduleApi.archive(selected.id);
    toast.show(ok ? 'Fee schedule archived.' : 'Failed to archive fee schedule.', !ok);
    if (ok) { setSelected(null); grid.reload(); }
  };

  const deleteSelected = async () => {
    if (!selected) return;
    if (!window.confirm(`Delete fee schedule '${selected.code}'? This cannot be undone.`)) return;
    const ok = await FeeScheduleApi.remove(selected.id);
    toast.show(ok ? 'Fee schedule deleted.' : 'Failed to delete fee schedule.', !ok);
    if (ok) { setSelected(null); grid.reload(); }
  };

  const seed = async (kind: 'bb' | 'dva' | 'wc') => {
    setSeeding(kind);
    try {
      const result = kind === 'bb' ? await FeeScheduleApi.seedBulkBill() : kind === 'dva' ? await FeeScheduleApi.seedDva() : await FeeScheduleApi.seedWorkCoverQld();
      if (!result) {
        toast.error(kind === 'bb' ? 'Failed to seed bulk-bill schedules.' : kind === 'dva' ? 'Failed to seed DVA schedules.' : 'Failed to seed WorkCover QLD schedule.');
        return;
      }
      toast.success(kind === 'bb'
        ? `BBGP/BBO/BBI ready: ${result.schedulesCreated} schedule(s) created, ${result.itemsAffected} item fee(s) updated from the MBS catalog.`
        : kind === 'dva'
          ? `VAGP/VASO/VASI ready: ${result.schedulesCreated} schedule(s) created. Import DVA fees via CSV (Source URL/Fetch Now) or Add Item.`
          : `WorkCover QLD ready: ${result.schedulesCreated} schedule(s) created. Import fees via CSV (Source URL/Fetch Now) or Add Item.`);
      grid.reload();
    } finally {
      setSeeding(null);
    }
  };

  const addItem = async (candidate: BillingItemDto) => {
    if (editingId == null) return;
    const ok = await FeeScheduleApi.addItem(editingId, { billingItemId: candidate.id, fee: candidate.scheduleFee });
    if (ok) {
      setPickerOpen(false);
      await loadItems(editingId);
    } else {
      toast.error('Failed to add item (it may already be on this schedule).');
    }
  };

  const updateItemFee = async (item: FeeScheduleItemDto, fee: number) => {
    const ok = await FeeScheduleApi.updateItemFee(item.id, fee);
    if (ok) setItems((all) => all.map((i) => (i.id === item.id ? { ...i, fee } : i)));
    else toast.error('Failed to update item fee.');
  };

  const removeItem = async (item: FeeScheduleItemDto) => {
    const ok = await FeeScheduleApi.removeItem(item.id);
    if (ok) {
      setItems((all) => all.filter((i) => i.id !== item.id));
      setItemsTotal((t) => Math.max(0, t - 1));
    } else {
      toast.error('Failed to remove item.');
    }
  };

  const copyFromMbs = async () => {
    if (editingId == null) return;
    setCopyingFromMbs(true);
    try {
      const updated = await FeeScheduleApi.copyFromMbs(editingId);
      if (updated != null) {
        toast.success(`${updated} item fee(s) reset to the current MBS schedule fee.`);
        await loadItems(editingId);
      } else {
        toast.error('Failed to copy fees from MBS.');
      }
    } finally {
      setCopyingFromMbs(false);
    }
  };

  const importItems = async () => {
    if (editingId == null) return;
    setImporting(true);
    try {
      const affected = await FeeScheduleApi.importItems(editingId, { sourceFeeScheduleId: importSource, percent: importPercent, flatAmount: importFlat });
      if (affected != null) {
        toast.success(`${affected} item(s) imported.`);
        await loadItems(editingId);
      } else {
        toast.error('Failed to import items.');
      }
    } finally {
      setImporting(false);
    }
  };

  const fetchNow = async () => {
    if (editingId == null) return;
    setFetchingNow(true);
    try {
      const result = await FeeScheduleApi.fetchNow(editingId);
      if (result) {
        setLastFetch({ at: result.fetchedAt, result: result.message });
        toast.show(result.message, !result.success);
        if (result.success) await loadItems(editingId);
      } else {
        toast.error('Failed to fetch from source URL.');
      }
    } finally {
      setFetchingNow(false);
    }
  };

  const openHistory = async (item: FeeScheduleItemDto) => {
    setHistory({ label: `${item.itemNumber} — ${item.description}`, entries: null });
    const entries = await FeeScheduleApi.itemHistory(item.id);
    setHistory((h) => (h ? { ...h, entries } : h));
  };

  const saveOverrides = async () => {
    if (!overrides) return;
    setOverridesSaving(true);
    try {
      const ok = await FeeScheduleApi.updateItemOverrides(overrides.item.id, overrides.model);
      toast.show(ok ? 'Overrides saved.' : 'Failed to save overrides.', !ok);
      if (ok) {
        // update the loaded (possibly partial, paged) list in place rather than re-fetching it
        setItems((all) => all.map((i) => (i.id === overrides.item.id ? { ...i, ...overrides.model } : i)));
      }
    } finally {
      setOverridesSaving(false);
    }
  };

  const refreshOverridesItem = async (id: number) => {
    const fresh = await FeeScheduleApi.getItem(id);
    if (fresh) setOverrides((o) => (o ? { ...o, item: fresh } : o));
  };

  const addRate = async () => {
    if (!overrides) return;
    const ok = await FeeScheduleApi.addPrecalculatedRate(overrides.item.id, { orderNum: overrides.orderNum, fee: overrides.fee });
    if (ok) {
      // re-fetch just this item to pick up the new rate's id (so Delete on it works at once)
      await refreshOverridesItem(overrides.item.id);
      setOverrides((o) => (o ? { ...o, orderNum: 0, fee: 0 } : o));
    }
    toast.show(ok ? 'Rate added.' : 'Failed to add rate.', !ok);
  };

  const removeRate = async (rateId: number) => {
    if (!overrides) return;
    const ok = await FeeScheduleApi.removePrecalculatedRate(rateId);
    if (ok) await refreshOverridesItem(overrides.item.id);
    toast.show(ok ? 'Rate removed.' : 'Failed to remove rate.', !ok);
  };

  const columns: Column<FeeScheduleDto>[] = [
    {
      title: 'Code', width: 100, render: (s) => (
        <a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); e.stopPropagation(); openEdit(s); }}>{s.code}</a>
      ),
    },
    { title: 'Description', width: 220, render: (s) => s.description },
    { title: 'Note', width: 160, render: (s) => s.note },
    { title: 'Health Fund', width: 110, render: (s) => s.healthFundCode },
    { title: 'Fee Table', width: 100, render: (s) => s.feeTableCode },
    { title: 'Round Type', width: 120, render: (s) => s.roundingTypeName },
    { title: 'Status', width: 100, render: (s) => (s.isArchived ? <span className="badge bg-secondary">Archived</span> : <span className="badge bg-success">Active</span>) },
  ];

  const seedButton = (kind: 'bb' | 'dva' | 'wc', className: string, icon: string, label: string, title: string) => (
    <button className={`btn btn-sm ${className}`} onClick={() => seed(kind)} disabled={seeding !== null} title={title}>
      {seeding === kind ? <span className="spinner-border spinner-border-sm me-1" /> : <i className={`bi ${icon} me-1`} />}{label}
    </button>
  );

  return (
    <>
      <h4 className="mb-4">Fee Schedules</h4>
      <div className="row">
        <div className="col-12 col-md-3">
          <div className="card mb-4">
            <div className="card-header"><strong>Filters</strong></div>
            <div className="card-body" onKeyDown={(e) => { if (e.key === 'Enter') grid.load(1); }}>
              <div className="mb-2">
                <label className="form-label small mb-0">Code</label>
                <TextInput className="form-control form-control-sm" value={filter.code} onChange={(v) => setFilter({ ...filter, code: v })} />
              </div>
              <div className="mb-2">
                <label className="form-label small mb-0">Description</label>
                <TextInput className="form-control form-control-sm" value={filter.description} onChange={(v) => setFilter({ ...filter, description: v })} />
              </div>
              <div className="form-check">
                <input className="form-check-input" type="checkbox" id="chkIncludeArchived" checked={filter.includeArchived}
                       onChange={(e) => setFilter({ ...filter, includeArchived: e.target.checked })} />
                <label className="form-check-label small" htmlFor="chkIncludeArchived">Include archived</label>
              </div>
            </div>
            <div className="card-footer d-flex justify-content-between">
              <button type="button" className="btn btn-primary" onClick={() => grid.load(1)}>Find</button>
              <button type="button" className="btn btn-secondary" onClick={() => setFilter({ code: null, description: null, includeArchived: false })}>Clear</button>
            </div>
          </div>
        </div>
        <div className="col-12 col-md-9 mt-3 mt-md-0">
          <div className="card">
            <div className="card-header d-flex align-items-center justify-content-between py-2">
              <span className="small text-muted"><i className="bi bi-cash-stack me-1" /> Total: <strong>{grid.total}</strong></span>
              <button className="btn btn-sm btn-outline-primary" onClick={grid.reload} disabled={grid.loading}>
                <i className={`bi bi-arrow-clockwise me-1 ${grid.loading ? 'imed-spin' : ''}`} /> Refresh
              </button>
            </div>
            <DataGrid columns={columns} rows={grid.rows} rowKey={(s) => s.id} total={grid.total} page={grid.page} pageSize={grid.pageSize}
                      loading={grid.loading} emptyText="No fee schedules found." emptyIcon="bi-cash-stack" onPageChange={grid.load}
                      onRowClick={(s) => setSelected(s)} rowClassName={(s) => (s.id === selected?.id ? 'table-active imed-row-selected' : '')} />
          </div>
          <div className="d-flex flex-wrap gap-2 mt-3">
            <button className="btn btn-sm btn-primary" onClick={openCreate}><i className="bi bi-plus-lg" /> New</button>
            <button className="btn btn-sm btn-outline-secondary" onClick={archiveSelected} disabled={!selected || selected.isArchived}><i className="bi bi-archive" /> Archive</button>
            <button className="btn btn-sm btn-outline-danger" onClick={deleteSelected} disabled={!selected}><i className="bi bi-trash" /> Delete</button>
            <span className="ms-auto" />
            {seedButton('bb', 'btn-outline-success', 'bi-magic', 'Seed Bulk-Bill Schedules (BBGP/BBO/BBI)', "Creates/refreshes BBGP, BBO and BBI from the MBS catalog's 100% benefit — no external file needed.")}
            {seedButton('dva', 'btn-outline-secondary', 'bi-shield-plus', 'Seed DVA Schedules (VAGP/VASO/VASI)', 'Creates the VAGP/VASO/VASI DVA schedule shells — no fee auto-population, since DVA has no MBS-derived proxy. Import fees via CSV or Add Item.')}
            {seedButton('wc', 'btn-outline-secondary', 'bi-briefcase-fill', 'Seed WorkCover QLD Schedule', 'Creates the WorkCover QLD schedule shell — no fee auto-population, since the WorkCover QLD fee list is a PDF, not a machine-readable feed. Import fees via CSV or Add Item.')}
          </div>
        </div>
      </div>

      {dialog && (
        <Modal title={dialog.editingId != null ? 'Edit Fee Schedule' : 'New Fee Schedule'} titleTag="h5" size="lg" onClose={() => setDialog(null)}>
          <ul className="nav nav-tabs mb-3">
            {(['General', 'Items'] as const).map((tab) => (
              <li key={tab} className="nav-item">
                <a className={`nav-link ${dialog.tab === tab ? 'active' : ''}`} href="#" onClick={(e) => { e.preventDefault(); setDialog({ ...dialog, tab }); }}>{tab}</a>
              </li>
            ))}
          </ul>

          {dialog.tab === 'General' ? (
            <form onSubmit={saveSchedule} noValidate>
              <ValidationSummary errors={errors} className="text-danger small mb-3" />
              <div className="row g-3 mb-3">
                <div className="col-md-4">
                  <label className="form-label">Code <span className="text-danger">*</span></label>
                  <input className="form-control" value={dialog.form.code} onChange={(e) => setForm({ code: e.target.value })} />
                  <FieldMessage messages={errors.code} />
                </div>
                <div className="col-md-8">
                  <label className="form-label">Description <span className="text-danger">*</span></label>
                  <input className="form-control" value={dialog.form.description} onChange={(e) => setForm({ description: e.target.value })} />
                  <FieldMessage messages={errors.description} />
                </div>
              </div>
              <div className="form-check mb-3">
                <input className="form-check-input" type="checkbox" id="chkIsHealthFund" checked={dialog.form.isHealthFund} onChange={(e) => setForm({ isHealthFund: e.target.checked })} />
                <label className="form-check-label" htmlFor="chkIsHealthFund">Is a Health Fund</label>
              </div>
              <div className="row g-3 mb-3">
                <div className="col-md-12"><label className="form-label">Note</label><TextInput className="form-control" value={dialog.form.note} onChange={(v) => setForm({ note: v })} /></div>
              </div>
              <div className="row g-3 mb-3">
                <div className="col-md-6">
                  <label className="form-label">Health Fund</label>
                  <NumberSelect className="form-select" value={dialog.form.healthFundId} onChange={(v) => setForm({ healthFundId: v })}>
                    <option value="">-- None --</option>
                    {healthFunds.map((f) => <option key={f.id} value={f.id}>{f.code} - {f.name}</option>)}
                  </NumberSelect>
                </div>
                <div className="col-md-6">
                  <label className="form-label">Fee Table</label>
                  <NumberSelect className="form-select" value={dialog.form.feeTableId} onChange={(v) => setForm({ feeTableId: v })}>
                    <option value="">-- None --</option>
                    {dialog.feeTableOptions.map((s) => <option key={s.id} value={s.id}>{s.code} - {s.description}</option>)}
                  </NumberSelect>
                </div>
              </div>
              <div className="row g-3 mb-3">
                <div className="col-md-6">
                  <label className="form-label">Round Type</label>
                  <NumberSelect enumType={RoundingTypeEnum} className="form-select" nullable={false} value={dialog.form.roundingType} onChange={(v) => setForm({ roundingType: (v ?? 0) as RoundingTypeEnum })}>
                    <option value={RoundingTypeEnum.Exact}>Exact</option>
                    <option value={RoundingTypeEnum.ToNearest1c}>To Nearest 1c</option>
                    <option value={RoundingTypeEnum.ToNearest5c}>To Nearest 5c</option>
                  </NumberSelect>
                </div>
              </div>
              <div className="row g-3 mb-3">
                <div className="col-12">
                  <label className="form-label">Source URL <span className="text-muted small">(optional)</span></label>
                  <TextInput className="form-control" placeholder="https://... direct-download link to a CSV of item number + fee" value={dialog.form.sourceUrl} onChange={(v) => setForm({ sourceUrl: v })} />
                  <div className="form-text">When set, a background job re-downloads and re-imports this file daily. Use the "Fetch Now" button on the Items tab to run it immediately.</div>
                </div>
              </div>
              {dialog.editingId != null && (
                <div className="form-check form-switch mb-2">
                  <input className="form-check-input" type="checkbox" role="switch" id="feeScheduleActiveSwitch" checked={!dialog.form.isArchived}
                         onChange={(e) => setForm({ isArchived: !e.target.checked })} />
                  <label className="form-check-label" htmlFor="feeScheduleActiveSwitch">Active</label>
                </div>
              )}
              <div className="modal-footer px-0 pb-0">
                <button type="button" className="btn btn-secondary" onClick={() => setDialog(null)}>Cancel</button>
                <button type="submit" className="btn btn-primary" disabled={dialogSaving}>
                  {dialogSaving ? <><span className="spinner-border spinner-border-sm me-2" /><span>Saving...</span></> : <span>{dialog.editingId != null ? 'Save Changes' : 'Create'}</span>}
                </button>
              </div>
            </form>
          ) : (
            <>
              {dialog.editingId == null ? (
                <div className="text-muted small">Save the schedule first to manage its items.</div>
              ) : (
                <>
                  <div className="d-flex flex-wrap gap-2 mb-2">
                    <button type="button" className="btn btn-sm btn-outline-primary" onClick={() => setPickerOpen(true)}><i className="bi bi-plus-lg" /> Add Item</button>
                    <button type="button" className="btn btn-sm btn-outline-secondary" onClick={copyFromMbs} disabled={copyingFromMbs}>
                      {copyingFromMbs && <span className="spinner-border spinner-border-sm me-1" />} Copy Fees from MBS
                    </button>
                    {dialog.form.sourceUrl?.trim() && (
                      <button type="button" className="btn btn-sm btn-outline-success" onClick={fetchNow} disabled={fetchingNow}>
                        {fetchingNow ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-cloud-download me-1" />} Fetch Now
                      </button>
                    )}
                  </div>
                  {dialog.form.sourceUrl?.trim() && (
                    <div className="small text-muted mb-3">
                      {lastFetch.at ? <span>Last fetched {formatUtc(lastFetch.at, 'g')}: {lastFetch.result}</span>
                        : <span>Not yet fetched. The background job runs daily, or use "Fetch Now" above.</span>}
                    </div>
                  )}
                  <div className="card mb-3">
                    <div className="card-header py-1"><strong className="small">Import Items From</strong></div>
                    <div className="card-body py-2">
                      <div className="row g-2 align-items-end">
                        <div className="col-md-5">
                          <label className="form-label small mb-0">Source</label>
                          <NumberSelect className="form-select form-select-sm" value={importSource} onChange={setImportSource}>
                            <option value="">MBS Catalog (all active items)</option>
                            {dialog.feeTableOptions.map((s) => <option key={s.id} value={s.id}>{s.code} - {s.description}</option>)}
                          </NumberSelect>
                        </div>
                        <div className="col-md-3">
                          <label className="form-label small mb-0">Adjustment %</label>
                          <NumberInput step="0.01" className="form-control form-control-sm" value={importPercent} onChange={(v) => setImportPercent(v ?? 0)} />
                        </div>
                        <div className="col-md-3">
                          <label className="form-label small mb-0">Flat $ Adjustment</label>
                          <NumberInput step="0.01" className="form-control form-control-sm" value={importFlat} onChange={(v) => setImportFlat(v ?? 0)} />
                        </div>
                        <div className="col-md-1">
                          <button type="button" className="btn btn-sm btn-primary w-100" onClick={importItems} disabled={importing}>
                            {importing ? <span className="spinner-border spinner-border-sm" /> : <span>Apply</span>}
                          </button>
                        </div>
                      </div>
                      <div className="small text-muted mt-1">Fee = source fee &times; (1 + %/100) + flat $. Leave both at 0 for a plain copy.</div>
                    </div>
                  </div>

                  {itemsLoading ? (
                    <LoadingState text="Loading items..." />
                  ) : items.length === 0 ? (
                    <EmptyState message='No items yet. Use "Add Item" or "Import Items From" above.' icon="bi-list-ul" />
                  ) : (
                    <>
                      <div className="small text-muted mb-1">Showing {items.length} of {itemsTotal} item{itemsTotal === 1 ? '' : 's'}.</div>
                      <div id="feeScheduleItemsScroll" className="imed-table-scroll" style={{ maxHeight: '50vh' }}>
                        <table className="table table-sm table-hover mb-0 align-middle">
                          <thead><tr><th>Item#</th><th>Description</th><th style={{ width: 120 }}>Fee</th><th style={{ width: 110 }}>Last Updated</th><th style={{ width: 165 }} /></tr></thead>
                          <tbody>
                            {items.map((item) => (
                              <tr key={item.id}>
                                <td>{item.itemNumber}</td>
                                <td>{item.description}</td>
                                <td><CommitNumberInput step="0.01" className="form-control form-control-sm" aria-label={`Fee for ${item.itemNumber}`} value={item.fee}
                                                       onCommit={(fee) => updateItemFee(item, fee)} /></td>
                                <td className="text-end">
                                  <button type="button" className="btn btn-link btn-sm p-0 small" title="Price history" onClick={() => openHistory(item)}>
                                    {formatUtc(item.updatedAt ?? item.createdAt, 'd MMM yyyy')}
                                  </button>
                                </td>
                                <td className="text-end text-nowrap">
                                  <button type="button" className="btn btn-sm btn-outline-secondary me-1"
                                          onClick={() => setOverrides({ item, orderNum: 0, fee: 0, model: { medicalGapPercent: item.medicalGapPercent, percentageFromAssociatedItemFee: item.percentageFromAssociatedItemFee, overLimitQuantityPlus: item.overLimitQuantityPlus } })}>Derived</button>
                                  <button type="button" className="btn btn-sm btn-outline-danger" onClick={() => removeItem(item)}>Delete</button>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                        <IntersectionSentinel id="feeScheduleItemsSentinel" enabled={hasMoreItems && !loadingMore} onVisible={loadMoreItems} />
                        {loadingMore ? (
                          <div className="text-center text-muted small py-2"><span className="spinner-border spinner-border-sm me-1" />Loading more...</div>
                        ) : !hasMoreItems && (
                          <div className="text-center text-muted small py-2">All {itemsTotal} items loaded.</div>
                        )}
                      </div>
                    </>
                  )}
                </>
              )}
              <div className="modal-footer px-0 pb-0">
                <button type="button" className="btn btn-secondary" onClick={() => setDialog(null)}>Close</button>
              </div>
            </>
          )}
        </Modal>
      )}

      {pickerOpen && <BillingItemPicker onSelect={addItem} onClose={() => setPickerOpen(false)} />}

      {history && (
        <Modal title={`Price History — ${history.label}`} scrollable={false} onClose={() => setHistory(null)}
               footer={<button type="button" className="btn btn-sm btn-secondary" onClick={() => setHistory(null)}>Close</button>}>
          {history.entries === null ? <LoadingState text="Loading history..." /> : history.entries.length === 0 ? (
            <EmptyState message="No history recorded yet." icon="bi-clock-history" />
          ) : (
            <table className="table table-sm mb-0">
              <thead><tr><th>Fee</th><th>Changed</th></tr></thead>
              <tbody>
                {history.entries.map((h, i) => <tr key={i}><td>{money(h.fee)}</td><td>{formatUtc(h.changedAt, 'd MMM yyyy, h:mm tt')}</td></tr>)}
              </tbody>
            </table>
          )}
        </Modal>
      )}

      {overrides && (
        <Modal title={`Derived-Item Overrides — ${overrides.item.itemNumber}`} scrollable={false} onClose={() => setOverrides(null)}
               footer={<button type="button" className="btn btn-sm btn-secondary" onClick={() => setOverrides(null)}>Close</button>}>
          <div className="small text-muted mb-2">
            Per-schedule overrides for this item when it's used as a derived-item's associated item (see Derived Item Rules). Leave blank to use the rule's own configured values.
          </div>
          <div className="row g-2 mb-3">
            <div className="col-md-4">
              <label className="form-label small mb-0">Medical Gap %</label>
              <NumberInput step="0.01" nullable className="form-control form-control-sm" value={overrides.model.medicalGapPercent}
                           onChange={(v) => setOverrides({ ...overrides, model: { ...overrides.model, medicalGapPercent: v } })} />
            </div>
            <div className="col-md-4">
              <label className="form-label small mb-0">% From Assoc. Item</label>
              <NumberInput step="0.01" nullable className="form-control form-control-sm" value={overrides.model.percentageFromAssociatedItemFee}
                           onChange={(v) => setOverrides({ ...overrides, model: { ...overrides.model, percentageFromAssociatedItemFee: v } })} />
            </div>
            <div className="col-md-4">
              <label className="form-label small mb-0">Over-Limit Plus ($)</label>
              <NumberInput step="0.01" nullable className="form-control form-control-sm" value={overrides.model.overLimitQuantityPlus}
                           onChange={(v) => setOverrides({ ...overrides, model: { ...overrides.model, overLimitQuantityPlus: v } })} />
            </div>
          </div>
          <button type="button" className="btn btn-sm btn-primary mb-3" onClick={saveOverrides} disabled={overridesSaving}>
            {overridesSaving && <span className="spinner-border spinner-border-sm me-1" />} Save Overrides
          </button>
          <hr />
          <div className="small text-muted mb-2">Pre-calculated fee overrides, keyed by patient count / field quantity — takes priority over the formula when a match exists.</div>
          {overrides.item.derivedItemRateCalculateds.length > 0 && (
            <table className="table table-sm mb-2">
              <thead><tr><th>Order#</th><th>Fee</th><th /></tr></thead>
              <tbody>
                {overrides.item.derivedItemRateCalculateds.map((r) => (
                  <tr key={r.id}>
                    <td>{r.orderNum}</td>
                    <td>{money(r.fee)}</td>
                    <td className="text-end"><button type="button" className="btn btn-sm btn-outline-danger" onClick={() => removeRate(r.id)}>Delete</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <div className="row g-2 align-items-end">
            <div className="col-md-5">
              <label className="form-label small mb-0">Order# (count/quantity)</label>
              <NumberInput className="form-control form-control-sm" value={overrides.orderNum} onChange={(v) => setOverrides({ ...overrides, orderNum: v ?? 0 })} />
            </div>
            <div className="col-md-5">
              <label className="form-label small mb-0">Fee ($)</label>
              <NumberInput step="0.01" className="form-control form-control-sm" value={overrides.fee} onChange={(v) => setOverrides({ ...overrides, fee: v ?? 0 })} />
            </div>
            <div className="col-md-2">
              <button type="button" className="btn btn-sm btn-outline-primary w-100" onClick={addRate}>Add</button>
            </div>
          </div>
        </Modal>
      )}
    </>
  );
}
