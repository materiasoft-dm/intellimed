import { useCallback, useState } from 'react';
import { BillingItemApi, DerivedItemConfigApi } from '@/api/billing';
import { DerivedCalculationType as D, type BillingItemDto, type DerivedItemConfigDto } from '@/api/types.gen';
import { NumberInput, NumberSelect, TextArea, TextInput } from '@/components/Inputs';
import { Modal } from '@/components/Modal';
import { EmptyState, LoadingState } from '@/components/States';
import { useToast } from '@/components/Toast';
import { money } from '@/lib/format';
import { useLatestSearch, useMountEffect, usePageTitle } from '@/lib/hooks';

type AssociationMode = 'item' | 'group' | 'list';

interface RuleForm {
  billingItemId: number;
  calculationType: D;
  associatedBillingItemId: number | null;
  associatedGroup: string | null;
  associatedItemNumbers: string | null;
  groupAAssociatedItems: string | null;
  groupBAssociatedItems: string | null;
  groupCAssociatedItems: string | null;
  groupDAssociatedItems: string | null;
  percentage: number | null;
  percentageFromInvoiceTotal: number | null;
  calculateFromFee: number | null;
  numOfLimitPatient: number | null;
  underNumOfLimitPatientPlusTotal: number | null;
  overNumOfLimitPatientPlus: number | null;
  numberOfLimitQuantity: number | null;
  overLimitQuantityPlus: number | null;
  isActive: boolean;
  notes: string | null;
}

const blankForm = (): RuleForm => ({
  billingItemId: 0, calculationType: D.PercentageOfAssociatedItem, associatedBillingItemId: null, associatedGroup: null, associatedItemNumbers: null,
  groupAAssociatedItems: null, groupBAssociatedItems: null, groupCAssociatedItems: null, groupDAssociatedItems: null, percentage: null,
  percentageFromInvoiceTotal: null, calculateFromFee: null, numOfLimitPatient: null, underNumOfLimitPatientPlusTotal: null,
  overNumOfLimitPatientPlus: null, numberOfLimitQuantity: null, overLimitQuantityPlus: null, isActive: true, notes: null,
});

const usesAssociationSelector = (t: D) => t === D.PercentageOfAssociatedItem || t === D.ExcisionMalignantTumour;

function calculationTypeLabel(t: D): string {
  switch (t) {
    case D.PercentageOfAssociatedItem: return '% of Associated Item';
    case D.ExcisionMalignantTumour: return 'Excision Malignant Tumour';
    case D.ProcedureDiscontinued: return 'Procedure Discontinued';
    case D.CombinationOperations: return 'Combination Operations';
    case D.AssistanceAnaesthesia: return 'Assistance / Anaesthesia';
    case D.BasicUnits: return 'Basic Units';
    case D.FieldQuantity: return 'Field Quantity';
    case D.TimeDuration: return 'Time Duration';
    case D.NumberOfPatientsSeen: return 'Patients Seen';
    default: return D[t];
  }
}

const pct = (n: number | null) => `${Number((n ?? 0).toFixed(2))}%`;
function valueSummary(c: DerivedItemConfigDto): string {
  switch (c.calculationType) {
    case D.CombinationOperations: return pct(c.percentageFromInvoiceTotal);
    case D.BasicUnits: return `${money(c.calculateFromFee ?? 0)} base`;
    case D.NumberOfPatientsSeen: return c.calculateFromFee != null ? `${money(c.calculateFromFee)} base` : 'assoc. item fee';
    case D.FieldQuantity:
    case D.TimeDuration: return `${money(c.overLimitQuantityPlus ?? 0)}/unit`;
    default: return pct(c.percentage);
  }
}
const associatedDisplay = (c: DerivedItemConfigDto) =>
  c.associatedGroup ? `Any MBS Group ${c.associatedGroup}` : c.associatedItemNumbers ? c.associatedItemNumbers : c.associatedItemNumber ?? '—';

/** Search-as-you-type over the billing catalogue, with the chosen item shown underneath. */
function ItemSearch({ label, selectedId, selectedLabel, onSelect, maxHeight = 150 }: {
  label: string; selectedId: number | null; selectedLabel: string | null; onSelect: (item: BillingItemDto) => void; maxHeight?: number;
}) {
  const [term, setTerm] = useState('');
  const search = useCallback((q: string) => BillingItemApi.search(q), []);
  const { results, searching, run, clear } = useLatestSearch<BillingItemDto[]>(search, []);
  return (
    <div className="mb-3">
      <label className="form-label">{label}</label>
      <input className="form-control form-control-sm mb-2" placeholder="Search by item number or description..." value={term}
             onChange={(e) => { setTerm(e.target.value); run(e.target.value); }} />
      {searching && <LoadingState text="Searching..." />}
      {!searching && results.length > 0 && (
        <ul className="list-group" style={{ maxHeight, overflowY: 'auto' }}>
          {results.map((c) => (
            <li key={c.id} className={`list-group-item list-group-item-action ${selectedId === c.id ? 'active' : ''}`} style={{ cursor: 'pointer' }}
                onClick={() => { onSelect(c); clear(); }}>
              <strong>{c.itemNumber}</strong> — {c.description}
            </li>
          ))}
        </ul>
      )}
      {selectedLabel && <div className="small text-success mt-1"><i className="bi bi-check-circle me-1" />{selectedLabel}</div>}
    </div>
  );
}

export default function DerivedItemConfigs() {
  usePageTitle('Derived Item Rules - IntelliMed');
  const toast = useToast();
  const [configs, setConfigs] = useState<DerivedItemConfigDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [dialog, setDialog] = useState<{ editingId: number | null; form: RuleForm; billingLabel: string | null; associatedLabel: string | null; mode: AssociationMode } | null>(null);
  const [saving, setSaving] = useState(false);

  const load = async () => {
    setLoading(true);
    try {
      setConfigs(await DerivedItemConfigApi.list());
    } finally {
      setLoading(false);
    }
  };
  useMountEffect(load);

  const openCreate = () => setDialog({ editingId: null, form: blankForm(), billingLabel: null, associatedLabel: null, mode: 'item' });
  const openEdit = (c: DerivedItemConfigDto) => setDialog({
    editingId: c.id,
    form: {
      billingItemId: c.billingItemId, calculationType: c.calculationType, associatedBillingItemId: c.associatedBillingItemId, associatedGroup: c.associatedGroup,
      associatedItemNumbers: c.associatedItemNumbers, groupAAssociatedItems: c.groupAAssociatedItems, groupBAssociatedItems: c.groupBAssociatedItems,
      groupCAssociatedItems: c.groupCAssociatedItems, groupDAssociatedItems: c.groupDAssociatedItems, percentage: c.percentage,
      percentageFromInvoiceTotal: c.percentageFromInvoiceTotal, calculateFromFee: c.calculateFromFee, numOfLimitPatient: c.numOfLimitPatient,
      underNumOfLimitPatientPlusTotal: c.underNumOfLimitPatientPlusTotal, overNumOfLimitPatientPlus: c.overNumOfLimitPatientPlus,
      numberOfLimitQuantity: c.numberOfLimitQuantity, overLimitQuantityPlus: c.overLimitQuantityPlus, isActive: c.isActive, notes: c.notes,
    },
    billingLabel: `${c.itemNumber} — ${c.description}`,
    associatedLabel: c.associatedItemNumber != null ? `${c.associatedItemNumber} — ${c.associatedDescription}` : null,
    mode: c.associatedGroup ? 'group' : c.associatedItemNumbers ? 'list' : 'item',
  });

  const set = (patch: Partial<RuleForm>) => setDialog((d) => (d ? { ...d, form: { ...d.form, ...patch } } : d));

  // only one of the three association fields may be set — clear the others so a stale value isn't saved alongside
  const setMode = (mode: AssociationMode) => setDialog((d) => d && {
    ...d, mode,
    associatedLabel: mode === 'item' ? d.associatedLabel : null,
    form: {
      ...d.form,
      associatedGroup: mode === 'group' ? d.form.associatedGroup : null,
      associatedItemNumbers: mode === 'list' ? d.form.associatedItemNumbers : null,
      associatedBillingItemId: mode === 'item' ? d.form.associatedBillingItemId : null,
    },
  });

  const save = async () => {
    if (!dialog) return;
    setSaving(true);
    try {
      const { billingItemId, isActive, ...rest } = dialog.form;
      const ok = dialog.editingId != null
        ? await DerivedItemConfigApi.update(dialog.editingId, { ...rest, isActive })
        : (await DerivedItemConfigApi.create({ ...rest, billingItemId })) != null;
      if (ok) {
        toast.success(dialog.editingId != null ? 'Rule updated.' : 'Rule created.');
        setDialog(null);
        await load();
      } else {
        toast.error('Failed to save rule.');
      }
    } finally {
      setSaving(false);
    }
  };

  const remove = async (c: DerivedItemConfigDto) => {
    if (!window.confirm(`Delete the derived-item rule for item ${c.itemNumber}?`)) return;
    const ok = await DerivedItemConfigApi.remove(c.id);
    toast.show(ok ? 'Rule deleted.' : 'Failed to delete rule.', !ok);
    if (ok) await load();
  };

  const f = dialog?.form;
  const t = f?.calculationType;
  const num = (key: keyof RuleForm, label: string, opts: { step?: string; small?: boolean; help?: string; col?: string } = {}) => (
    <div className={opts.col ?? 'mb-3'}>
      <label className={`form-label ${opts.small ? 'small mb-0' : ''}`}>{label}</label>
      <NumberInput step={opts.step ?? '0.01'} nullable className={`form-control ${opts.small ? 'form-control-sm' : ''}`}
                   value={f?.[key] as number | null} onChange={(v) => set({ [key]: v } as Partial<RuleForm>)} />
      {opts.help && <div className="small text-muted mt-1">{opts.help}</div>}
    </div>
  );
  const txt = (key: keyof RuleForm, label: string, col: string) => (
    <div className={col}>
      <label className="form-label small mb-0">{label}</label>
      <TextInput className="form-control form-control-sm" value={f?.[key] as string | null} onChange={(v) => set({ [key]: v } as Partial<RuleForm>)} />
    </div>
  );
  const associatedSearch = (label: string) => dialog && (
    <ItemSearch label={label} selectedId={dialog.form.associatedBillingItemId} selectedLabel={dialog.associatedLabel}
                onSelect={(c) => setDialog({ ...dialog, form: { ...dialog.form, associatedBillingItemId: c.id }, associatedLabel: `${c.itemNumber} — ${c.description}` })} />
  );

  return (
    <>
      <h4 className="mb-2">Derived Item Rules</h4>
      <p className="text-muted small">
        Configures MBS items whose fee is computed from other items on the same invoice (assistant-at-surgery, after-hours anaesthesia-assistance
        modifiers, per-patient-seen billing, discontinued procedures, etc.) instead of a flat fee-schedule lookup. Ported field-for-field from
        legacy Pracnet's DerivedFeeCalculator. There's no bulk-import source for this data — add rules one at a time as you identify them.
      </p>

      <div className="card">
        <div className="card-header d-flex align-items-center justify-content-between py-2">
          <span className="small text-muted"><i className="bi bi-calculator me-1" /> Total: <strong>{configs.length}</strong></span>
          <button className="btn btn-sm btn-primary" onClick={openCreate}><i className="bi bi-plus-lg me-1" /> Add Rule</button>
        </div>
        {loading ? (
          <div className="card-body"><LoadingState text="Loading rules..." /></div>
        ) : configs.length === 0 ? (
          <div className="card-body"><EmptyState message='No derived-item rules yet. Click "Add Rule" to create one.' icon="bi-calculator" /></div>
        ) : (
          <div className="table-responsive">
            <table className="table table-sm table-hover mb-0">
              <thead><tr><th>Item#</th><th>Description</th><th>Calculation</th><th>Associated Item</th><th>Value</th><th>Active</th><th /></tr></thead>
              <tbody>
                {configs.map((c) => (
                  <tr key={c.id}>
                    <td><a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); openEdit(c); }}>{c.itemNumber}</a></td>
                    <td>{c.description}</td>
                    <td>{calculationTypeLabel(c.calculationType)}</td>
                    <td>{associatedDisplay(c)}</td>
                    <td>{valueSummary(c)}</td>
                    <td>{c.isActive ? <span className="badge bg-success">Active</span> : <span className="badge bg-secondary">Inactive</span>}</td>
                    <td className="text-end"><button className="btn btn-sm btn-outline-danger" title="Delete rule" aria-label={`Delete rule for ${c.itemNumber}`} onClick={() => remove(c)}><i className="bi bi-trash" /></button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {dialog && f && t != null && (
        <Modal title={dialog.editingId != null ? 'Edit Derived Item Rule' : 'New Derived Item Rule'} titleTag="h5" onClose={() => setDialog(null)}
               footer={<>
                 <button type="button" className="btn btn-secondary" onClick={() => setDialog(null)}>Cancel</button>
                 <button type="button" className="btn btn-primary" onClick={save} disabled={saving || f.billingItemId === 0}>
                   {saving && <span className="spinner-border spinner-border-sm me-2" />} Save
                 </button>
               </>}>
          {dialog.editingId != null ? (
            <div className="mb-3">
              <label className="form-label">Billing Item <span className="text-danger">*</span></label>
              <input className="form-control" value={dialog.billingLabel ?? ''} readOnly />
            </div>
          ) : (
            <ItemSearch label="Billing Item *" selectedId={f.billingItemId || null} selectedLabel={dialog.billingLabel} maxHeight={180}
                        onSelect={(c) => setDialog({ ...dialog, form: { ...f, billingItemId: c.id }, billingLabel: `${c.itemNumber} — ${c.description}` })} />
          )}

          <div className="mb-3">
            <label className="form-label">Calculation Type</label>
            <NumberSelect enumType={D} className="form-select" nullable={false} value={t} onChange={(v) => set({ calculationType: (v ?? 0) as D })}>
              <option value={D.PercentageOfAssociatedItem}>Percentage of Associated Item</option>
              <option value={D.ExcisionMalignantTumour}>Excision Malignant Tumour (% of associated item)</option>
              <option value={D.ProcedureDiscontinued}>Procedure Discontinued (% of every other line)</option>
              <option value={D.CombinationOperations}>Combination Operations (% of invoice total)</option>
              <option value={D.AssistanceAnaesthesia}>Assistance / Anaesthesia (gated 4-group)</option>
              <option value={D.BasicUnits}>Basic Units (gated group sum)</option>
              <option value={D.NumberOfPatientsSeen}>Number of Patients Seen</option>
              <option value={D.FieldQuantity}>Field Quantity</option>
              <option value={D.TimeDuration}>Time Duration</option>
            </NumberSelect>
          </div>

          {usesAssociationSelector(t) && (
            <>
              <div className="mb-3">
                <label className="form-label d-block">Match Associated Item By</label>
                <div className="btn-group btn-group-sm" role="group">
                  {([['item', 'assocModeItem', 'Specific Item'], ['group', 'assocModeGroup', 'MBS Group'], ['list', 'assocModeList', 'Item Number List']] as const).map(([mode, id, label]) => (
                    <span key={mode} style={{ display: 'contents' }}>
                      <input type="radio" className="btn-check" name="assocMode" id={id} checked={dialog.mode === mode} onChange={() => setMode(mode)} />
                      <label className="btn btn-outline-primary" htmlFor={id}>{label}</label>
                    </span>
                  ))}
                </div>
              </div>
              {dialog.mode === 'item' ? associatedSearch('Associated Billing Item') : dialog.mode === 'group' ? (
                <div className="mb-3">
                  <label className="form-label">MBS Group Code</label>
                  <TextInput className="form-control form-control-sm" placeholder="e.g. T8" value={f.associatedGroup} onChange={(v) => set({ associatedGroup: v })} />
                  <div className="small text-muted mt-1">Sums every sibling line whose MBS Group equals this code.</div>
                </div>
              ) : (
                <div className="mb-3">
                  <label className="form-label">Item Numbers</label>
                  <TextInput className="form-control form-control-sm" placeholder="e.g. 25200-25205, 23010-24136" value={f.associatedItemNumbers} onChange={(v) => set({ associatedItemNumbers: v })} />
                  <div className="small text-muted mt-1">Comma/space-separated item numbers; "X-Y" expands to every number in that range.</div>
                </div>
              )}
              {num('percentage', 'Percentage (%)')}
            </>
          )}

          {t === D.ProcedureDiscontinued && num('percentage', 'Percentage (%)', { help: 'Applied to every OTHER line on the invoice (no association filter).' })}
          {t === D.CombinationOperations && num('percentageFromInvoiceTotal', 'Percentage of Invoice Total (%)', { help: 'Applied to every OTHER line on the invoice.' })}

          {t === D.AssistanceAnaesthesia && (
            <>
              <div className="small text-muted mb-2">Fires only when both Group A and Group B have a matching sibling line; sums every sibling matching any of the 4 groups.</div>
              <div className="row g-2 mb-3">
                {txt('groupAAssociatedItems', 'Group A Item Numbers', 'col-md-6')}
                {txt('groupBAssociatedItems', 'Group B Item Numbers', 'col-md-6')}
                {txt('groupCAssociatedItems', 'Group C Item Numbers (optional)', 'col-md-6')}
                {txt('groupDAssociatedItems', 'Group D Item Numbers (optional)', 'col-md-6')}
              </div>
              {num('percentage', 'Percentage (%)')}
            </>
          )}

          {t === D.BasicUnits && (
            <>
              <div className="small text-muted mb-2">Fires only when Group A has a matching sibling line; fee = base + raw sum of sibling fees matching Group A/B/C.</div>
              <div className="row g-2 mb-3">
                {txt('groupAAssociatedItems', 'Group A Item Numbers', 'col-md-4')}
                {txt('groupBAssociatedItems', 'Group B Item Numbers (optional)', 'col-md-4')}
                {txt('groupCAssociatedItems', 'Group C Item Numbers (optional)', 'col-md-4')}
              </div>
              {num('calculateFromFee', 'Base Fee ($)')}
            </>
          )}

          {t === D.NumberOfPatientsSeen && (
            <>
              {num('calculateFromFee', "Base Fee ($) — leave blank to use the associated item's fee instead")}
              {associatedSearch('Associated Billing Item (used when Base Fee is blank)')}
              <div className="row g-2 mb-3">
                {num('numOfLimitPatient', 'Patient Limit', { step: '1', small: true, col: 'col-md-4' })}
                {num('underNumOfLimitPatientPlusTotal', 'Under-Limit Total ($)', { small: true, col: 'col-md-4', help: 'Divided by patient count when at/under the limit.' })}
                {num('overNumOfLimitPatientPlus', 'Over-Limit Plus ($)', { small: true, col: 'col-md-4' })}
              </div>
            </>
          )}

          {(t === D.FieldQuantity || t === D.TimeDuration) && (
            <>
              {associatedSearch('Associated Billing Item')}
              <div className="row g-2 mb-3">
                {t === D.FieldQuantity && num('numberOfLimitQuantity', 'Quantity Limit', { small: true, col: 'col-md-6' })}
                {num('overLimitQuantityPlus', 'Over-Limit Plus ($/unit)', {
                  small: true, col: 'col-md-6', help: t === D.TimeDuration ? 'Multiplied by the full quantity, not just the over-limit portion.' : undefined,
                })}
              </div>
            </>
          )}

          <div className="mb-3">
            <label className="form-label">Notes</label>
            <TextArea className="form-control" rows={2} value={f.notes} onChange={(v) => set({ notes: v })} />
          </div>
          {dialog.editingId != null && (
            <div className="form-check form-switch mb-2">
              <input className="form-check-input" type="checkbox" role="switch" id="activeSwitch" checked={f.isActive} onChange={(e) => set({ isActive: e.target.checked })} />
              <label className="form-check-label" htmlFor="activeSwitch">Active</label>
            </div>
          )}
        </Modal>
      )}
    </>
  );
}
