import { useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router';
import { PractitionerApi } from '@/api/appointments';
import { FeeScheduleApi, InvoiceApi } from '@/api/billing';
import { ClientApi } from '@/api/clients';
import {
  AccountTypeEnum, defaultCreateInvoiceDto, defaultCreatePaymentDto, defaultResolveLineRequest, PaymentMethod, PlaceOfServiceEnum,
  type BillingItemDto, type ClientDto, type CreateInvoiceDto, type FeeScheduleDto, type PractitionerDto,
} from '@/api/types.gen';
import { BillingItemPicker } from '@/components/BillingItemPicker';
import { FindClientModal } from '@/components/FindClientModal';
import { DateInput, EnumOptions, NumberInput, NumberSelect, TextArea } from '@/components/Inputs';
import { Modal } from '@/components/Modal';
import { EmptyState } from '@/components/States';
import { addDays, todayIso, toIsoLocal } from '@/lib/dates';
import { money } from '@/lib/format';
import { useMountEffect, usePageTitle } from '@/lib/hooks';

function accountTypeLabel(type: AccountTypeEnum): string {
  switch (type) {
    case AccountTypeEnum.BulkBill: return 'BB';
    case AccountTypeEnum.PrivatePatient: return 'Private Patient';
    case AccountTypeEnum.Veteran: return 'DVA';
    case AccountTypeEnum.WorkCover: return 'WorkCover';
    case AccountTypeEnum.Tac: return 'TAC';
    case AccountTypeEnum.Imc: return 'IMC';
    case AccountTypeEnum.Ovs: return 'OVS';
    case AccountTypeEnum.ChildImm: return 'Immunisation';
    default: return AccountTypeEnum[type];
  }
}

const ACCOUNT_TYPES = Object.values(AccountTypeEnum).filter((v): v is AccountTypeEnum => typeof v === 'number');
/** WorkCover/TAC/Other are the only account types where none of the consent checkboxes apply. */
const anyConsent = (t: AccountTypeEnum) => ![AccountTypeEnum.WorkCover, AccountTypeEnum.Tac, AccountTypeEnum.Other].includes(t);

interface Line {
  key: number;
  billingItemId: number | null;
  /** Per-line fee schedule override; null uses the account type's mapped schedule. */
  feeScheduleId: number | null;
  itemNumberDisplay: string;
  description: string;
  serviceDate: string | null;
  quantity: number;
  unitPrice: number;
  rebatePerUnit: number;
  discount: number;
  derivedQuantity: number | null;
  note: string | null;
}
const lineRebate = (l: Line) => l.rebatePerUnit * l.quantity;
const lineTotal = (l: Line) => l.unitPrice * l.quantity - l.discount;
const lineGap = (l: Line) => lineTotal(l) - lineRebate(l);

export default function AddInvoice() {
  usePageTitle('New Invoice - IntelliMed');
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const [model, setModel] = useState<CreateInvoiceDto>(() => ({ ...defaultCreateInvoiceDto(), dueDate: toIsoLocal(addDays(new Date(new Date().toDateString()), 14)) }));
  const [lines, setLines] = useState<Line[]>([]);
  const [clientName, setClientName] = useState<string | null>(null);
  const [healthFundId, setHealthFundId] = useState<number | null>(null);
  const [accountTypeManuallySet, setAccountTypeManuallySet] = useState(false);
  const [practitioners, setPractitioners] = useState<PractitionerDto[]>([]);
  const [feeSchedules, setFeeSchedules] = useState<FeeScheduleDto[]>([]);
  const [clientPickerOpen, setClientPickerOpen] = useState(false);
  const [itemPicker, setItemPicker] = useState<{ editingKey: number | null } | null>(null);
  const [noteEditor, setNoteEditor] = useState<{ key: number; draft: string } | null>(null);
  const [payNow, setPayNow] = useState(false);
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>(PaymentMethod.Cash);
  const [saving, setSaving] = useState(false);
  const [status, setStatus] = useState<{ message: string; error: boolean } | null>(null);
  const nextKey = useRef(1);
  // the latest context, for re-resolving lines from async callbacks
  const ctx = useRef({ model, healthFundId });
  ctx.current = { model, healthFundId };

  const total = lines.reduce((s, l) => s + lineTotal(l), 0);
  const totalRebate = lines.reduce((s, l) => s + lineRebate(l), 0);
  const totalGap = lines.reduce((s, l) => s + lineGap(l), 0);

  const updateLine = (key: number, patch: Partial<Line>) => setLines((all) => all.map((l) => (l.key === key ? { ...l, ...patch } : l)));

  /** Resolve a catalogue line's fee + rebate from the server for the current billing context. */
  const resolveLine = async (line: Line, context = ctx.current) => {
    if (line.billingItemId == null) return;
    const result = await InvoiceApi.resolveLine({
      ...defaultResolveLineRequest(),
      accountType: context.model.accountType,
      practitionerId: context.model.practitionerId,
      placeOfService: context.model.placeOfService,
      billingItemId: line.billingItemId,
      serviceDate: line.serviceDate,
      healthFundId: context.healthFundId,
      feeScheduleId: line.feeScheduleId,
    });
    if (result) {
      setLines((all) => all.map((l) => (l.key === line.key
        ? { ...l, unitPrice: result.fee, rebatePerUnit: result.rebatePerUnit, description: l.description.trim() ? l.description : result.description ?? '' }
        : l)));
    }
  };

  /** Re-resolve every catalogue line when the account type, provider or place of service changes. */
  const recalcAll = (nextModel: CreateInvoiceDto, nextFund = healthFundId) => {
    for (const l of lines) if (l.billingItemId != null) resolveLine(l, { model: nextModel, healthFundId: nextFund });
  };

  useMountEffect(async (alive) => {
    const [p, s] = await Promise.all([PractitionerApi.active(), FeeScheduleApi.active()]);
    if (!alive()) return;
    setPractitioners(p);
    setFeeSchedules(s);
    const appointmentId = Number(params.get('appointmentId'));
    const clientId = Number(params.get('clientId'));
    let next = { ...model, appointmentId: appointmentId || null };
    if (clientId) {
      const c = await ClientApi.getById(clientId);
      if (c && alive()) {
        next = { ...next, clientId: c.id, accountType: c.accountType };
        setAccountTypeManuallySet(true);
        setClientName(c.fullName);
        setHealthFundId(c.healthFundId);
      }
    }
    if (alive()) setModel(next);
  });

  const onClientSelected = (c: ClientDto) => {
    // prefill the account type from the client's default, unless the user has already chosen one
    const next = { ...model, clientId: c.id, accountType: accountTypeManuallySet ? model.accountType : c.accountType };
    setModel(next);
    setClientName(c.fullName);
    setHealthFundId(c.healthFundId);
    recalcAll(next, c.healthFundId);
  };

  const selectAccountType = (type: AccountTypeEnum) => {
    const next = { ...model, accountType: type };
    setModel(next);
    setAccountTypeManuallySet(true);
    recalcAll(next);
  };

  const setAndRecalc = (patch: Partial<CreateInvoiceDto>) => {
    const next = { ...model, ...patch };
    setModel(next);
    recalcAll(next);
  };

  const selectBillingItem = (candidate: BillingItemDto) => {
    const editingKey = itemPicker?.editingKey ?? null;
    let line: Line;
    if (editingKey != null) {
      const existing = lines.find((l) => l.key === editingKey)!;
      line = { ...existing, billingItemId: candidate.id, itemNumberDisplay: candidate.itemNumber, description: candidate.description, unitPrice: candidate.scheduleFee };
      setLines((all) => all.map((l) => (l.key === editingKey ? line : l)));
    } else {
      line = {
        key: nextKey.current++, billingItemId: candidate.id, feeScheduleId: null, itemNumberDisplay: candidate.itemNumber, description: candidate.description,
        serviceDate: todayIso(), quantity: 1, unitPrice: candidate.scheduleFee, rebatePerUnit: 0, discount: 0, derivedQuantity: null, note: null,
      };
      setLines((all) => [...all, line]);
    }
    setItemPicker(null);
    resolveLine(line);
  };

  const addCustomLine = () => setLines((all) => [...all, {
    key: nextKey.current++, billingItemId: null, feeScheduleId: null, itemNumberDisplay: '-', description: '', serviceDate: todayIso(),
    quantity: 1, unitPrice: 0, rebatePerUnit: 0, discount: 0, derivedQuantity: null, note: null,
  }]);

  const save = async () => {
    if (model.clientId === 0) { setStatus({ message: 'Select a client first.', error: true }); return; }
    if (lines.length === 0) { setStatus({ message: 'Add at least one line item.', error: true }); return; }
    setSaving(true);
    try {
      const id = await InvoiceApi.create({
        ...model,
        items: lines.map((l) => ({
          billingItemId: l.billingItemId, feeScheduleId: l.feeScheduleId, description: l.description, serviceDate: l.serviceDate,
          quantity: l.quantity, unitPrice: l.unitPrice, discount: l.discount, derivedQuantity: l.derivedQuantity, note: l.note,
        })),
      });
      if (id == null) { setStatus({ message: 'Failed to save invoice.', error: true }); return; }
      if (payNow) await InvoiceApi.addPayment(id, { ...defaultCreatePaymentDto(), amount: total, method: paymentMethod, paymentDate: todayIso() });
      navigate(`/invoices/edit/${id}`);
    } finally {
      setSaving(false);
    }
  };

  const t = model.accountType;

  return (
    <>
      <div className="d-flex align-items-center justify-content-between mb-1">
        <h5 className="mb-0 small">🧾 New Invoice</h5>
        {status && <span className={`small ${status.error ? 'text-danger' : 'text-success'}`}>{status.message}</span>}
      </div>

      <div className="card mb-2">
        <div className="card-header py-1"><strong>Account Type</strong></div>
        <div className="card-body py-2">
          <ul className="nav nav-pills gap-1 flex-wrap">
            {ACCOUNT_TYPES.map((type) => (
              <li key={type} className="nav-item">
                <a className={`nav-link py-1 px-2 small ${t === type ? 'active' : ''}`} href="#" aria-current={t === type ? 'true' : undefined}
                   onClick={(e) => { e.preventDefault(); selectAccountType(type); }}>{accountTypeLabel(type)}</a>
              </li>
            ))}
          </ul>
          <div className="small text-muted mt-2">Fees and the Medicare rebate are resolved from the account type's mapped fee schedule (configure in Clinic Settings). Fee is editable inline.</div>
        </div>
      </div>

      <div className="row g-2 mb-2">
        <div className="col-lg-6">
          <div className="card h-100">
            <div className="card-header py-1"><strong>Details</strong></div>
            <div className="card-body py-2">
              <div className="row g-2">
                <div className="col-12">
                  <label className="form-label small mb-0">Client</label>
                  <div className="input-group input-group-sm">
                    <input className="form-control form-control-sm" value={clientName ?? ''} readOnly placeholder="No client selected" />
                    <button type="button" className="btn btn-outline-secondary" onClick={() => setClientPickerOpen(true)}>Find</button>
                  </div>
                </div>
                <div className="col-6 col-md-3">
                  <label className="form-label small mb-0">Provider</label>
                  <NumberSelect className="form-select form-select-sm" value={model.practitionerId} onChange={(v) => setAndRecalc({ practitionerId: v })}>
                    <option value="">-- None --</option>
                    {practitioners.map((p) => <option key={p.id} value={p.id}>{p.fullName}</option>)}
                  </NumberSelect>
                </div>
                <div className="col-6 col-md-3">
                  <label className="form-label small mb-0" title="Who receives the Medicare/DVA/fund payment, if different from the servicing Provider">Payee</label>
                  <NumberSelect className="form-select form-select-sm" value={model.payeePractitionerId} onChange={(v) => setModel({ ...model, payeePractitionerId: v })}>
                    <option value="">-- Same as Provider --</option>
                    {practitioners.map((p) => <option key={p.id} value={p.id}>{p.fullName}</option>)}
                  </NumberSelect>
                </div>
                <div className="col-6 col-md-3">
                  <label className="form-label small mb-0">Place of Service</label>
                  <NumberSelect enumType={PlaceOfServiceEnum} className="form-select form-select-sm" nullable={false} value={model.placeOfService}
                                onChange={(v) => setAndRecalc({ placeOfService: (v ?? 0) as PlaceOfServiceEnum })}>
                    <EnumOptions enumType={PlaceOfServiceEnum} />
                  </NumberSelect>
                </div>
                <div className="col-6 col-md-3">
                  <label className="form-label small mb-0">Due Date</label>
                  <DateInput className="form-control form-control-sm" value={model.dueDate} onChange={(v) => v && setModel({ ...model, dueDate: v })} />
                </div>
                <div className="col-12">
                  <label className="form-label small mb-0">Notes</label>
                  <TextArea className="form-control form-control-sm" rows={2} value={model.notes} onChange={(v) => setModel({ ...model, notes: v })} />
                </div>
              </div>
            </div>
          </div>
        </div>
        <div className="col-lg-6">
          <div className="card h-100">
            <div className="card-header py-1"><strong>Payment</strong></div>
            <div className="card-body py-2">
              <div className="form-check mb-2">
                <input className="form-check-input" type="checkbox" id="chkPayNow" checked={payNow} onChange={(e) => setPayNow(e.target.checked)} />
                <label className="form-check-label small" htmlFor="chkPayNow">Record full payment now</label>
              </div>
              {payNow ? (
                <div className="row g-2">
                  <div className="col-6">
                    <label className="form-label small mb-0">Method</label>
                    <NumberSelect enumType={PaymentMethod} className="form-select form-select-sm" nullable={false} value={paymentMethod} onChange={(v) => setPaymentMethod((v ?? 0) as PaymentMethod)}>
                      <EnumOptions enumType={PaymentMethod} />
                    </NumberSelect>
                  </div>
                  <div className="col-6">
                    <label className="form-label small mb-0">Amount</label>
                    <input className="form-control form-control-sm" value={money(total)} readOnly />
                  </div>
                </div>
              ) : (
                <div className="small text-muted">Invoice will be saved as Draft with no payment recorded.</div>
              )}
            </div>
          </div>
        </div>
      </div>

      {anyConsent(t) && (
        <div className="card mb-2">
          <div className="card-header py-1"><strong>Consent &amp; Claiming</strong></div>
          <div className="card-body py-2">
            <div className="row g-2">
              {anyConsent(t) && (
                <div className="col-auto form-check">
                  <input className="form-check-input" type="checkbox" id="chkClaimSubmissionAuthorised" checked={model.claimSubmissionAuthorised}
                         onChange={(e) => setModel({ ...model, claimSubmissionAuthorised: e.target.checked })} />
                  <label className="form-check-label small" htmlFor="chkClaimSubmissionAuthorised">Online Submission Authorised</label>
                </div>
              )}
              {(t === AccountTypeEnum.Imc || t === AccountTypeEnum.Ovs) && (
                <>
                  <div className="col-auto form-check">
                    <input className="form-check-input" type="checkbox" id="chkFinancialInterestDisclosed" checked={model.financialInterestDisclosed}
                           onChange={(e) => setModel({ ...model, financialInterestDisclosed: e.target.checked })} />
                    <label className="form-check-label small" htmlFor="chkFinancialInterestDisclosed">Financial Interest Disclosed</label>
                  </div>
                  <div className="col-auto form-check">
                    <input className="form-check-input" type="checkbox" id="chkCompensationClaim" checked={model.compensationClaim}
                           onChange={(e) => setModel({ ...model, compensationClaim: e.target.checked })} />
                    <label className="form-check-label small" htmlFor="chkCompensationClaim">Compensation Claim</label>
                  </div>
                </>
              )}
              {t === AccountTypeEnum.BulkBill && (
                <div className="col-auto form-check">
                  <input className="form-check-input" type="checkbox" id="chkSubmissionAuthorityReceived" checked={model.submissionAuthorityReceived}
                         onChange={(e) => setModel({ ...model, submissionAuthorityReceived: e.target.checked })} />
                  <label className="form-check-label small" htmlFor="chkSubmissionAuthorityReceived">Submission Authority Form Received (DB4)</label>
                </div>
              )}
              {t === AccountTypeEnum.Imc && (
                <div className="col-auto form-check">
                  <input className="form-check-input" type="checkbox" id="chkBenefitAssignmentRequested" checked={model.benefitAssignmentRequested}
                         onChange={(e) => setModel({ ...model, benefitAssignmentRequested: e.target.checked })} />
                  <label className="form-check-label small" htmlFor="chkBenefitAssignmentRequested" title="Unticked = implied assignment (default). Ticked = requested assignment.">Assignment Requested</label>
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      <div className="card mb-2">
        <div className="card-header py-1 d-flex justify-content-between align-items-center">
          <strong>Line Items</strong>
          <div>
            <button type="button" className="btn btn-sm btn-outline-primary me-1" onClick={() => setItemPicker({ editingKey: null })}>+ Add Item</button>
            <button type="button" className="btn btn-sm btn-outline-secondary" onClick={addCustomLine}>+ Add Custom Line</button>
          </div>
        </div>
        <div className="card-body py-2">
          {lines.length === 0 ? (
            <EmptyState message='No line items yet. Use "Add Item" to pick from the billing catalog, or "Add Custom Line" for a free-text charge.' icon="bi-list-ul" />
          ) : (
            <div className="table-responsive">
              <table className="table table-sm mb-0 align-middle imed-invoice-lines">
                <thead>
                  <tr>
                    <th style={{ width: 60 }}>Item#</th><th>Description</th><th style={{ width: 130 }}>Service Date</th>
                    <th style={{ width: 140 }} title="Overrides the invoice's account-type-mapped schedule for this line only">Schedule</th>
                    <th style={{ width: 65 }}>Qty</th><th style={{ width: 100 }}>Fee</th><th style={{ width: 90 }} className="text-end">Rebate</th>
                    <th style={{ width: 90 }}>Discount</th>
                    <th style={{ width: 80 }} title="For derived-item rules (patients seen, minutes, fields, units) — see Derived Item Rules">D.Qty</th>
                    <th style={{ width: 90 }} className="text-end">Gap</th><th style={{ width: 95 }} className="text-end">Total</th><th style={{ width: 36 }} />
                  </tr>
                </thead>
                <tbody>
                  {lines.map((l) => (
                    <tr key={l.key}>
                      <td>
                        <a href="#" className="link-primary" title="Change billing item" onClick={(e) => { e.preventDefault(); setItemPicker({ editingKey: l.key }); }}>{l.itemNumberDisplay}</a>
                      </td>
                      <td>
                        <div className="d-flex align-items-center gap-1">
                          {/* no type attribute on purpose — the UI tests find the description as input:not([type]) */}
                          <input className="form-control form-control-sm" aria-label="Description" value={l.description} onChange={(e) => updateLine(l.key, { description: e.target.value })} />
                          <button type="button" className={`btn btn-sm p-0 border-0 ${l.note?.trim() ? 'text-primary' : 'text-muted'}`}
                                  title={l.note?.trim() ? l.note : 'No note'} aria-label="Line note"
                                  onClick={() => setNoteEditor({ key: l.key, draft: l.note ?? '' })}>
                            <i className="bi bi-info-circle" />
                          </button>
                        </div>
                      </td>
                      <td><DateInput className="form-control form-control-sm" aria-label="Service date" value={l.serviceDate} onChange={(v) => updateLine(l.key, { serviceDate: v })} /></td>
                      <td>
                        <NumberSelect className="form-select form-select-sm" aria-label="Fee schedule" value={l.feeScheduleId}
                                      onChange={(v) => { const next = { ...l, feeScheduleId: v }; updateLine(l.key, { feeScheduleId: v }); resolveLine(next); }}>
                          <option value="">-- Default --</option>
                          {feeSchedules.map((s) => <option key={s.id} value={s.id}>{s.code} - {s.description}</option>)}
                        </NumberSelect>
                      </td>
                      <td><NumberInput min={1} className="form-control form-control-sm" aria-label="Quantity" value={l.quantity} onChange={(v) => updateLine(l.key, { quantity: v ?? 0 })} /></td>
                      <td><NumberInput step="0.01" className="form-control form-control-sm" aria-label="Fee" value={l.unitPrice} onChange={(v) => updateLine(l.key, { unitPrice: v ?? 0 })} /></td>
                      <td className="text-end">{money(lineRebate(l))}</td>
                      <td><NumberInput step="0.01" className="form-control form-control-sm" aria-label="Discount" value={l.discount} onChange={(v) => updateLine(l.key, { discount: v ?? 0 })} /></td>
                      <td><NumberInput step="0.01" nullable className="form-control form-control-sm" aria-label="Derived quantity" value={l.derivedQuantity} onChange={(v) => updateLine(l.key, { derivedQuantity: v })} /></td>
                      <td className={`text-end ${lineGap(l) > 0 ? 'text-danger' : ''}`}>{money(lineGap(l))}</td>
                      <td className="text-end">{money(lineTotal(l))}</td>
                      <td><button type="button" className="btn btn-sm btn-outline-danger" aria-label="Remove line" onClick={() => setLines((all) => all.filter((x) => x.key !== l.key))}>x</button></td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr className="fw-semibold">
                    <td colSpan={6} className="text-end">Totals</td>
                    <td className="text-end">{money(totalRebate)}</td>
                    <td /><td />
                    <td className="text-end">{money(totalGap)}</td>
                    <td className="text-end">{money(total)}</td>
                    <td />
                  </tr>
                </tfoot>
              </table>
            </div>
          )}
        </div>
      </div>

      <div className="d-flex justify-content-end gap-2">
        <button type="button" className="btn btn-secondary" onClick={() => navigate('/invoices')}>Cancel</button>
        <button type="button" className="btn btn-primary" disabled={saving} onClick={save}>
          {saving && <span className="spinner-border spinner-border-sm me-1" />} 💾 Save
        </button>
      </div>

      {clientPickerOpen && <FindClientModal onSelect={onClientSelected} onClose={() => setClientPickerOpen(false)} />}
      {itemPicker && <BillingItemPicker onSelect={selectBillingItem} onClose={() => setItemPicker(null)} />}
      {noteEditor && (
        <Modal title="Line Note" scrollable={false} onClose={() => setNoteEditor(null)}
               footer={<>
                 <button type="button" className="btn btn-sm btn-secondary" onClick={() => setNoteEditor(null)}>Cancel</button>
                 <button type="button" className="btn btn-sm btn-primary"
                         onClick={() => { updateLine(noteEditor.key, { note: noteEditor.draft.trim() ? noteEditor.draft : null }); setNoteEditor(null); }}>Save</button>
               </>}>
          <textarea className="form-control form-control-sm" rows={4} placeholder="Add a note for this line item..." value={noteEditor.draft}
                    onChange={(e) => setNoteEditor({ ...noteEditor, draft: e.target.value })} />
        </Modal>
      )}
    </>
  );
}
