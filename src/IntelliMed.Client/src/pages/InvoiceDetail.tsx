import { useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import { InvoiceApi, ReceiptApi } from '@/api/billing';
import {
  ClaimStatus,
  AccountTypeEnum, defaultCreateReceiptAllocationDto, defaultCreateReceiptDto, defaultCreateReceiptTenderDto, defaultCreateRefundDto,
  InvoiceStatus, PaymentMethod, type InvoiceDto,
} from '@/api/types.gen';
import { DateInput, EnumOptions, NumberInput, NumberSelect, TextInput } from '@/components/Inputs';
import { Modal } from '@/components/Modal';
import { EmptyState, LoadingState } from '@/components/States';
import { invoiceStatusClass } from '@/components/StatusBadges';
import { formatDate, todayIso } from '@/lib/dates';
import { money } from '@/lib/format';
import { useMountEffect, usePageTitle } from '@/lib/hooks';
import { useSession } from '@/session';

interface AllocationRow {
  invoiceId: number;
  invoiceNumber: string;
  clientName: string;
  maxAmount: number;
  isLocked: boolean;
  isSelected: boolean;
  amount: number;
}

const round2 = (n: number) => Math.round(n * 100) / 100;

/** Same account-type gating as New Invoice's Consent & Claiming card: only the boxes that applied. */
function consentFlags(i: InvoiceDto): { label: string; value: boolean }[] {
  const t = i.accountType;
  const list: { label: string; value: boolean }[] = [];
  if (![AccountTypeEnum.WorkCover, AccountTypeEnum.Tac, AccountTypeEnum.Other].includes(t)) list.push({ label: 'Online Submission Authorised', value: i.claimSubmissionAuthorised });
  if (t === AccountTypeEnum.Imc || t === AccountTypeEnum.Ovs) {
    list.push({ label: 'Financial Interest Disclosed', value: i.financialInterestDisclosed });
    list.push({ label: 'Compensation Claim', value: i.compensationClaim });
  }
  if (t === AccountTypeEnum.BulkBill) list.push({ label: 'Submission Authority Received', value: i.submissionAuthorityReceived });
  if (t === AccountTypeEnum.Imc) list.push({ label: 'Assignment Requested', value: i.benefitAssignmentRequested });
  return list;
}

export default function InvoiceDetail() {
  const { id: idParam } = useParams();
  const id = Number(idParam);
  const navigate = useNavigate();
  const session = useSession();
  const [invoice, setInvoice] = useState<InvoiceDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [status, setStatus] = useState<{ message: string; error: boolean } | null>(null);
  const [availableCredit, setAvailableCredit] = useState(0);
  usePageTitle(`Invoice ${invoice?.invoiceNumber ?? ''} - IntelliMed`);

  // receipt
  const [receipt, setReceipt] = useState<{ rows: AllocationRow[]; amount: number; method: PaymentMethod; date: string; reference: string | null } | null>(null);
  const [receiptSaving, setReceiptSaving] = useState(false);
  // refund
  const [refund, setRefund] = useState<{ fromCredit: boolean; amount: number; method: PaymentMethod; date: string; reference: string | null; reason: string } | null>(null);
  const [refundSaving, setRefundSaving] = useState(false);
  // write-off / cancel
  const [writeOff, setWriteOff] = useState<{ amount: number; reason: string } | null>(null);
  const [writeOffSaving, setWriteOffSaving] = useState(false);
  const [cancel, setCancel] = useState<{ reason: string } | null>(null);
  const [cancelSaving, setCancelSaving] = useState(false);

  const isAdmin = session.roles.some((r) => r === 'SuperAdmin' || r === 'Admin');

  const load = async () => {
    setLoading(true);
    const i = await InvoiceApi.getById(id);
    setInvoice(i);
    setLoading(false);
    return i;
  };
  const loadCredit = async (i: InvoiceDto | null) => {
    if (i) setAvailableCredit(await ReceiptApi.availableCredit(i.payerClientId ?? i.clientId));
  };
  useMountEffect(async () => {
    const i = await load();
    await loadCredit(i);
  });

  // AmountPaid is already net of every prior refund, so it IS the refundable cap (see ReceiptRepository.CreateRefundAsync)
  const refundableFromInvoice = invoice?.amountPaid ?? 0;
  const refundCap = (fromCredit: boolean) => (fromCredit ? availableCredit : refundableFromInvoice);
  const canCancel = !!invoice && invoice.status !== InvoiceStatus.Cancelled && invoice.amountPaid <= 0;
  const cancelDisabledReason = !invoice || canCancel ? undefined
    : invoice.status === InvoiceStatus.Cancelled ? 'This invoice is already cancelled' : 'Refund this invoice before cancelling it';
  const canSplit = !!invoice && invoice.status !== InvoiceStatus.Cancelled && invoice.amountPaid <= 0 && invoice.amountWrittenOff <= 0;

  const totalAllocated = (rows: AllocationRow[]) => round2(rows.filter((r) => r.isSelected).reduce((s, r) => s + r.amount, 0));

  const openReceipt = async () => {
    if (!invoice) return;
    const rows: AllocationRow[] = [{
      invoiceId: invoice.id, invoiceNumber: invoice.invoiceNumber, clientName: invoice.clientName, maxAmount: invoice.amountOwing,
      isLocked: true, isSelected: true, amount: invoice.amountOwing,
    }];
    const outstanding = await ReceiptApi.outstandingForPayer(invoice.payerClientId ?? invoice.clientId, invoice.id);
    for (const o of outstanding) {
      rows.push({ invoiceId: o.invoiceId, invoiceNumber: o.invoiceNumber, clientName: o.clientName, maxAmount: o.amountOwing, isLocked: false, isSelected: false, amount: o.amountOwing });
    }
    setReceipt({ rows, amount: totalAllocated(rows), method: PaymentMethod.Cash, date: todayIso(), reference: null });
  };

  // changing an allocation re-totals what is tendered, as the Blazor page did
  const setRow = (index: number, patch: Partial<AllocationRow>) => setReceipt((r) => {
    if (!r) return r;
    const rows = r.rows.map((row, i) => (i === index ? { ...row, ...patch } : row));
    return { ...r, rows, amount: totalAllocated(rows) };
  });

  const saveReceipt = async () => {
    if (!invoice || !receipt) return;
    const selected = receipt.rows.filter((r) => r.isSelected);
    if (selected.length === 0) return;
    setReceiptSaving(true);
    const receiptId = await ReceiptApi.create({
      ...defaultCreateReceiptDto(),
      payerClientId: invoice.payerClientId ?? invoice.clientId,
      receiptDate: receipt.date,
      payments: [{ ...defaultCreateReceiptTenderDto(), amount: receipt.amount, method: receipt.method, reference: receipt.reference }],
      allocations: selected.map((r) => ({ ...defaultCreateReceiptAllocationDto(), invoiceId: r.invoiceId, amount: r.amount })),
    });
    setReceiptSaving(false);
    setReceipt(null);
    if (receiptId != null) {
      setStatus({ message: 'Receipt recorded.', error: false });
      await load();
    } else {
      setStatus({ message: 'Failed to record receipt.', error: true });
    }
  };

  const openRefund = () => {
    const fromCredit = refundableFromInvoice <= 0 && availableCredit > 0;
    setRefund({ fromCredit, amount: refundCap(fromCredit), method: PaymentMethod.Cash, date: todayIso(), reference: null, reason: '' });
  };

  const saveRefund = async () => {
    if (!invoice || !refund) return;
    const cap = refundCap(refund.fromCredit);
    if (refund.amount <= 0 || refund.amount > cap || !refund.reason.trim()) return;
    setRefundSaving(true);
    const result = await ReceiptApi.refund({
      ...defaultCreateRefundDto(),
      payerClientId: invoice.payerClientId ?? invoice.clientId,
      invoiceId: refund.fromCredit ? null : invoice.id,
      amount: refund.amount, method: refund.method, reference: refund.reference, refundDate: refund.date, reason: refund.reason,
    });
    setRefundSaving(false);
    setRefund(null);
    if (result != null) {
      setStatus({ message: 'Refund recorded.', error: false });
      const i = await load();
      await loadCredit(i);
    } else {
      setStatus({ message: 'Failed to record refund.', error: true });
    }
  };

  const saveWriteOff = async () => {
    if (!invoice || !writeOff || writeOff.amount <= 0 || writeOff.amount > invoice.amountOwing || !writeOff.reason.trim()) return;
    setWriteOffSaving(true);
    const ok = await InvoiceApi.writeOff(invoice.id, { amount: writeOff.amount, reason: writeOff.reason });
    setWriteOffSaving(false);
    setWriteOff(null);
    setStatus(ok ? { message: 'Invoice written off.', error: false } : { message: 'Failed to write off invoice.', error: true });
    if (ok) await load();
  };

  const saveCancel = async () => {
    if (!invoice || !cancel?.reason.trim()) return;
    setCancelSaving(true);
    const ok = await InvoiceApi.cancel(invoice.id, { reason: cancel.reason });
    setCancelSaving(false);
    setCancel(null);
    setStatus(ok ? { message: 'Invoice cancelled.', error: false } : { message: 'Failed to cancel invoice.', error: true });
    if (ok) await load();
  };

  if (loading && !invoice) return <div className="card"><div className="card-body"><LoadingState text="Loading invoice..." /></div></div>;
  if (!invoice) return <div className="alert alert-danger">Invoice not found.</div>;

  const flags = consentFlags(invoice);
  const field = (label: string, value: React.ReactNode, strong = false) => (
    <>
      <div className={`col-6 text-muted ${strong ? 'fw-semibold' : ''}`}>{label}</div>
      <div className={`col-6 ${strong ? 'fw-semibold' : ''}`}>{value}</div>
    </>
  );

  return (
    <>
      <div className="d-flex align-items-center justify-content-between mb-1">
        <h5 className="mb-0 small">🧾 Invoice {invoice.invoiceNumber} — {invoice.clientName}</h5>
        {status && <span className={`small ${status.error ? 'text-danger' : 'text-success'}`}>{status.message}</span>}
      </div>

      <div className="row g-2 mb-2">
        <div className="col-lg-6">
          <div className="card h-100">
            <div className="card-header py-1"><strong>Details</strong></div>
            <div className="card-body py-2">
              <div className="row g-1 small">
                {field('Client', invoice.clientName)}
                {field('Provider', invoice.practitionerName ?? '-')}
                {field('Payee', invoice.payeePractitionerName ?? 'Same as Provider')}
                {field('Account Type', invoice.accountTypeName)}
                {field('Invoice Date', formatDate(invoice.invoiceDate, 'dd/MM/yyyy'))}
                {field('Due Date', formatDate(invoice.dueDate, 'dd/MM/yyyy'))}
                {invoice.notes?.trim() && field('Notes', invoice.notes)}
              </div>
            </div>
          </div>
        </div>
        <div className="col-lg-6">
          <div className="card h-100">
            <div className="card-header py-1"><strong>Status</strong></div>
            <div className="card-body py-2">
              <div className="row g-1 small">
                {field('Status', <span className={`badge ${invoiceStatusClass(invoice.status)}`}>{invoice.statusName}</span>)}
                {/* Read-only on purpose: it reflects where the claim actually is, so it will be driven by the claim
                    itself once electronic claiming exists — never typed in by hand. */}
                {field('Claim Status', <span className="badge bg-secondary" title="Set automatically from the claim's progress once electronic claiming is built">{ClaimStatus[invoice.claimStatus]}</span>)}
                {field('Total', money(invoice.totalAmount))}
                {field('Paid', money(invoice.amountPaid))}
                {field('Owing', money(invoice.amountOwing), true)}
                {invoice.amountWrittenOff > 0 && field('Written Off', money(invoice.amountWrittenOff))}
                {availableCredit > 0 && field('Payer Credit Available', <span className="text-success">{money(availableCredit)}</span>)}
                {invoice.cancelReason?.trim() && field('Cancel Reason', invoice.cancelReason)}
                {flags.length > 0 && (
                  <>
                    <div className="col-12 text-muted mt-1">Consent &amp; Claiming</div>
                    <div className="col-12">
                      {flags.map((f) => <span key={f.label} className={`badge ${f.value ? 'bg-success' : 'bg-secondary'} me-1`}>{f.value ? '✓' : '✗'} {f.label}</span>)}
                    </div>
                  </>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="card mb-2">
        <div className="card-header py-1"><strong>Line Items</strong></div>
        <div className="card-body py-2 table-responsive">
          <table className="table table-sm mb-0">
            <thead>
              <tr>
                <th style={{ width: 60 }}>Item#</th><th>Description</th><th style={{ width: 100 }}>Service Date</th><th style={{ width: 110 }}>Schedule</th>
                <th style={{ width: 50 }}>Qty</th><th style={{ width: 90 }} className="text-end">Fee</th><th style={{ width: 90 }} className="text-end">Rebate</th>
                <th style={{ width: 80 }} className="text-end">Disc.</th><th style={{ width: 90 }} className="text-end">Gap</th><th style={{ width: 90 }} className="text-end">Total</th>
              </tr>
            </thead>
            <tbody>
              {invoice.items.map((item) => (
                <tr key={item.id}>
                  <td>{item.itemNumber ?? '-'}</td>
                  <td>
                    {item.description}
                    {item.note?.trim() && <i className="bi bi-info-circle text-primary ms-1" title={item.note} style={{ cursor: 'help' }} />}
                  </td>
                  <td>{item.serviceDate ? formatDate(item.serviceDate, 'dd/MM/yyyy') : '-'}</td>
                  <td>{item.feeScheduleCode ?? 'Default'}</td>
                  <td>{item.quantity}</td>
                  <td className="text-end">{money(item.unitPrice)}</td>
                  <td className="text-end">{money(item.lineRebate)}</td>
                  <td className="text-end">{money(item.discount)}</td>
                  <td className={`text-end ${item.gap > 0 ? 'text-danger' : ''}`}>{money(item.gap)}</td>
                  <td className="text-end">{money(item.totalPrice)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="fw-semibold">
                <td colSpan={5} className="text-end">Totals</td>
                <td className="text-end" />
                <td className="text-end">{money(invoice.totalRebate)}</td>
                <td />
                <td className="text-end">{money(invoice.totalGap)}</td>
                <td className="text-end">{money(invoice.totalAmount)}</td>
              </tr>
            </tfoot>
          </table>
        </div>
      </div>

      <div className="card mb-2">
        <div className="card-header py-1 d-flex justify-content-between align-items-center">
          <strong>Payments</strong>
          <div className="d-flex gap-1">
            {isAdmin && (
              <>
                <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => setWriteOff({ amount: invoice.amountOwing, reason: '' })}
                        disabled={invoice.status === InvoiceStatus.Cancelled || invoice.amountOwing <= 0}>Write Off</button>
                <button type="button" className="btn btn-sm btn-outline-secondary" onClick={openRefund}
                        disabled={refundableFromInvoice <= 0 && availableCredit <= 0}>Refund</button>
              </>
            )}
            <button type="button" className="btn btn-sm btn-outline-primary" onClick={openReceipt} disabled={invoice.amountOwing <= 0}>+ Record Receipt</button>
          </div>
        </div>
        <div className="card-body py-2">
          {invoice.payments.length === 0 ? (
            <EmptyState message="No payments recorded yet." icon="bi-cash-coin" />
          ) : (
            <table className="table table-sm mb-0">
              <thead><tr><th style={{ width: 110 }}>Date</th><th style={{ width: 120 }}>Method</th><th>Reference</th><th style={{ width: 100 }}>Amount</th><th style={{ width: 40 }} /></tr></thead>
              <tbody>
                {invoice.payments.map((p) => (
                  <tr key={p.id}>
                    <td>{formatDate(p.paymentDate, 'dd/MM/yyyy')}</td>
                    <td>{p.methodName}</td>
                    <td>{p.reference ?? '-'}</td>
                    <td>{money(p.amount)}</td>
                    <td>
                      <a href={`/invoices/receipt/${p.receiptId}`} target="_blank" rel="noopener" className="btn btn-sm btn-outline-secondary" title="Print Receipt">
                        <i className="bi bi-printer" />
                      </a>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      <div className="d-flex flex-wrap justify-content-end gap-2">
        {isAdmin && (
          <>
            <button type="button" className="btn btn-outline-secondary" onClick={() => navigate(`/invoices/split/${id}`)} disabled={!canSplit}
                    title={canSplit ? undefined : 'Only unpaid invoices with no write-offs can be split'}>Split Invoice</button>
            <button type="button" className="btn btn-outline-danger" onClick={() => setCancel({ reason: '' })} disabled={!canCancel} title={cancelDisabledReason}>Cancel Invoice</button>
          </>
        )}
        <a href={`/invoices/print/${id}`} target="_blank" rel="noopener" className="btn btn-outline-primary"><i className="bi bi-printer" /> Print Invoice</a>
        <button type="button" className="btn btn-secondary" onClick={() => navigate('/invoices')}>Close</button>
      </div>

      {receipt && (
        <Modal size="lg" scrollable={false} onClose={() => setReceipt(null)}
               title={<>Record Receipt {invoice.payerClientId != null && <span className="text-muted small fw-normal">— Payer: {invoice.payerName}</span>}</>}
               footer={<>
                 <button type="button" className="btn btn-sm btn-secondary" onClick={() => setReceipt(null)}>Cancel</button>
                 <button type="button" className="btn btn-sm btn-primary" onClick={saveReceipt} disabled={receiptSaving || receipt.amount < totalAllocated(receipt.rows)}>
                   {receiptSaving && <span className="spinner-border spinner-border-sm me-1" />}Save
                 </button>
               </>}>
          <label className="form-label small mb-1 fw-semibold">Allocate To</label>
          <table className="table table-sm mb-0">
            <thead><tr><th style={{ width: 30 }} /><th>Invoice #</th><th>Client</th><th style={{ width: 100 }} className="text-end">Owing</th><th style={{ width: 120 }} className="text-end">Amount</th></tr></thead>
            <tbody>
              {receipt.rows.map((row, i) => (
                <tr key={row.invoiceId}>
                  <td><input type="checkbox" className="form-check-input" checked={row.isSelected} disabled={row.isLocked} aria-label={`Allocate to ${row.invoiceNumber}`}
                             onChange={(e) => setRow(i, { isSelected: e.target.checked })} /></td>
                  <td>{row.invoiceNumber}</td>
                  <td>{row.clientName}</td>
                  <td className="text-end">{money(row.maxAmount)}</td>
                  <td className="text-end">
                    <NumberInput step="0.01" className="form-control form-control-sm text-end" disabled={!row.isSelected} value={row.amount}
                                 onChange={(v) => setRow(i, { amount: v ?? 0 })} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {receipt.rows.length <= 1 && <div className="text-muted small mt-1">No other outstanding invoices for this payer.</div>}
          <hr />
          <div className="row g-2">
            <div className="col-6">
              <label className="form-label small mb-0">Amount Tendered</label>
              <NumberInput step="0.01" className="form-control form-control-sm" value={receipt.amount} onChange={(v) => setReceipt({ ...receipt, amount: v ?? 0 })} />
            </div>
            <div className="col-6">
              <label className="form-label small mb-0">Method</label>
              <NumberSelect enumType={PaymentMethod} className="form-select form-select-sm" nullable={false} value={receipt.method} onChange={(v) => setReceipt({ ...receipt, method: (v ?? 0) as PaymentMethod })}>
                <EnumOptions enumType={PaymentMethod} />
              </NumberSelect>
            </div>
            <div className="col-6">
              <label className="form-label small mb-0">Date</label>
              <DateInput className="form-control form-control-sm" value={receipt.date} onChange={(v) => v && setReceipt({ ...receipt, date: v })} />
            </div>
            <div className="col-6">
              <label className="form-label small mb-0">Reference</label>
              <input className="form-control form-control-sm" value={receipt.reference ?? ''} onChange={(e) => setReceipt({ ...receipt, reference: e.target.value || null })} />
            </div>
          </div>
          <div className={`small mt-2 ${receipt.amount < totalAllocated(receipt.rows) ? 'text-danger' : 'text-muted'}`}>
            Total Allocated: {money(totalAllocated(receipt.rows))} &middot; Total Tendered: {money(receipt.amount)}
            {receipt.amount > totalAllocated(receipt.rows) && <span> &middot; Excess of {money(round2(receipt.amount - totalAllocated(receipt.rows)))} will be kept as credit.</span>}
          </div>
        </Modal>
      )}

      {refund && (
        <Modal title="Refund" scrollable={false} onClose={() => setRefund(null)}
               footer={<>
                 <button type="button" className="btn btn-sm btn-secondary" onClick={() => setRefund(null)}>Cancel</button>
                 <button type="button" className="btn btn-sm btn-primary" onClick={saveRefund}
                         disabled={refundSaving || refund.amount <= 0 || refund.amount > refundCap(refund.fromCredit) || !refund.reason.trim()}>
                   {refundSaving ? <><span className="spinner-border spinner-border-sm me-1" /><span>Refunding...</span></> : <span>Refund</span>}
                 </button>
               </>}>
          {refundableFromInvoice > 0 && availableCredit > 0 && (
            <div className="mb-2">
              <div className="form-check form-check-inline">
                <input className="form-check-input" type="radio" name="refundSource" id="refundSourceInvoice" checked={!refund.fromCredit}
                       onChange={() => setRefund({ ...refund, fromCredit: false, amount: refundCap(false) })} />
                <label className="form-check-label small" htmlFor="refundSourceInvoice">This invoice ({money(refundableFromInvoice)})</label>
              </div>
              <div className="form-check form-check-inline">
                <input className="form-check-input" type="radio" name="refundSource" id="refundSourceCredit" checked={refund.fromCredit}
                       onChange={() => setRefund({ ...refund, fromCredit: true, amount: refundCap(true) })} />
                <label className="form-check-label small" htmlFor="refundSourceCredit">Account credit ({money(availableCredit)})</label>
              </div>
            </div>
          )}
          <div className="row g-2">
            <div className="col-6">
              <label className="form-label small mb-0">Amount (max {money(refundCap(refund.fromCredit))})</label>
              <NumberInput step="0.01" max={refundCap(refund.fromCredit)} className="form-control form-control-sm" value={refund.amount} onChange={(v) => setRefund({ ...refund, amount: v ?? 0 })} />
            </div>
            <div className="col-6">
              <label className="form-label small mb-0">Method</label>
              <NumberSelect enumType={PaymentMethod} className="form-select form-select-sm" nullable={false} value={refund.method} onChange={(v) => setRefund({ ...refund, method: (v ?? 0) as PaymentMethod })}>
                <EnumOptions enumType={PaymentMethod} />
              </NumberSelect>
            </div>
            <div className="col-6">
              <label className="form-label small mb-0">Date</label>
              <DateInput className="form-control form-control-sm" value={refund.date} onChange={(v) => v && setRefund({ ...refund, date: v })} />
            </div>
            <div className="col-6">
              <label className="form-label small mb-0">Reference</label>
              <TextInput className="form-control form-control-sm" value={refund.reference} onChange={(v) => setRefund({ ...refund, reference: v })} />
            </div>
            <div className="col-12">
              <label className="form-label small mb-0">Reason</label>
              <textarea className="form-control form-control-sm" rows={2} value={refund.reason} onChange={(e) => setRefund({ ...refund, reason: e.target.value })} />
            </div>
          </div>
        </Modal>
      )}

      {writeOff && (
        <Modal title="Write Off Invoice" titleTag="h5" headerClassName="" scrollable={false} onClose={() => setWriteOff(null)}
               footer={<>
                 <button type="button" className="btn btn-secondary" onClick={() => setWriteOff(null)}>Cancel</button>
                 <button type="button" className="btn btn-danger" onClick={saveWriteOff}
                         disabled={writeOffSaving || writeOff.amount <= 0 || writeOff.amount > invoice.amountOwing || !writeOff.reason.trim()}>
                   {writeOffSaving ? <><span className="spinner-border spinner-border-sm me-2" /><span>Writing Off...</span></> : <span>Write Off Invoice</span>}
                 </button>
               </>}>
          <div className="alert alert-warning d-flex align-items-center mb-3">
            <i className="bi bi-exclamation-triangle me-1" /> This forgives part or all of the outstanding balance. No money changes hands, and this cannot be undone.
          </div>
          <div className="row g-2">
            <div className="col-6">
              <label className="form-label small mb-0">Amount (max {money(invoice.amountOwing)})</label>
              <NumberInput step="0.01" max={invoice.amountOwing} className="form-control form-control-sm" value={writeOff.amount} onChange={(v) => setWriteOff({ ...writeOff, amount: v ?? 0 })} />
            </div>
            <div className="col-12">
              <label className="form-label small mb-0">Reason</label>
              <textarea className="form-control form-control-sm" rows={2} value={writeOff.reason} onChange={(e) => setWriteOff({ ...writeOff, reason: e.target.value })} />
            </div>
          </div>
        </Modal>
      )}

      {cancel && (
        <Modal title="Cancel Invoice" titleTag="h5" headerClassName="" scrollable={false} onClose={() => setCancel(null)}
               footer={<>
                 <button type="button" className="btn btn-secondary" onClick={() => setCancel(null)}>Keep Invoice</button>
                 <button type="button" className="btn btn-danger" onClick={saveCancel} disabled={cancelSaving || !cancel.reason.trim()}>
                   {cancelSaving ? <><span className="spinner-border spinner-border-sm me-2" /><span>Cancelling...</span></> : <span>Cancel Invoice</span>}
                 </button>
               </>}>
          <div className="alert alert-warning d-flex align-items-center mb-3">
            <i className="bi bi-exclamation-triangle me-1" /> This will cancel invoice {invoice.invoiceNumber}. It can no longer be paid or split.
          </div>
          <label className="form-label small mb-0">Reason</label>
          <textarea className="form-control form-control-sm" rows={2} value={cancel.reason} onChange={(e) => setCancel({ reason: e.target.value })} />
        </Modal>
      )}
    </>
  );
}
