import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { InvoiceApi } from '@/api/billing';
import { InvoiceStatus, type InvoiceDto, type SplitInvoiceResultDto } from '@/api/types.gen';
import { DateInput } from '@/components/Inputs';
import { LoadingState } from '@/components/States';
import { money } from '@/lib/format';
import { useMountEffect, usePageTitle } from '@/lib/hooks';

interface SplitRow { itemId: number; description: string; itemNumber: string | null; totalPrice: number; groupIndex: number }

export default function SplitInvoice() {
  const { id: idParam } = useParams();
  const id = Number(idParam);
  const navigate = useNavigate();
  const [invoice, setInvoice] = useState<InvoiceDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [rows, setRows] = useState<SplitRow[]>([]);
  const [groupDueDates, setGroupDueDates] = useState<(string | null)[]>([]);
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<SplitInvoiceResultDto | null>(null);
  usePageTitle(`Split Invoice ${invoice?.invoiceNumber ?? ''} - IntelliMed`);

  useMountEffect(async (alive) => {
    const i = await InvoiceApi.getById(id);
    if (!alive()) return;
    setInvoice(i);
    setLoading(false);
    if (i) setRows(i.items.map((x) => ({ itemId: x.id, description: x.description, itemNumber: x.itemNumber, totalPrice: x.totalPrice, groupIndex: 0 })));
  });

  const groupCount = groupDueDates.length;
  const canSplit = !!invoice && invoice.status !== InvoiceStatus.Cancelled && invoice.amountPaid <= 0 && invoice.amountWrittenOff <= 0;
  const canSave = groupCount > 0 && rows.some((r) => r.groupIndex === 0) && !!reason.trim() && !saving;

  // "+ Add new invoice" (-1) mints the next group on the spot and moves this row into it
  const setGroup = (itemId: number, value: number) => {
    let group = value;
    if (value === -1) {
      group = groupCount + 1;
      setGroupDueDates((d) => [...d, null]);
    }
    setRows((all) => all.map((r) => (r.itemId === itemId ? { ...r, groupIndex: group } : r)));
  };

  const save = async () => {
    if (!canSave) return;
    setSaving(true);
    setError(null);
    const dto = {
      reason,
      newInvoices: groupDueDates
        .map((dueDate, g) => ({ invoiceItemIds: rows.filter((r) => r.groupIndex === g + 1).map((r) => r.itemId), dueDate }))
        .filter((g) => g.invoiceItemIds.length > 0),
    };
    const r = await InvoiceApi.split(id, dto);
    setSaving(false);
    setResult(r);
    if (!r) setError('Failed to split invoice. Check that every new invoice has at least one item.');
  };

  if (loading) return <div className="card"><div className="card-body"><LoadingState text="Loading invoice..." /></div></div>;
  if (!invoice) return <div className="alert alert-danger">Invoice not found.</div>;
  if (!canSplit) {
    return (
      <div className="alert alert-warning">
        Invoice {invoice.invoiceNumber} can't be split — only unpaid invoices with no write-offs can be divided. <Link to={`/invoices/edit/${id}`}>Back to invoice</Link>
      </div>
    );
  }
  if (result) {
    return (
      <div className="alert alert-success">
        <p className="mb-2">Invoice {invoice.invoiceNumber} was split into {result.newInvoiceIds.length + 1} invoices.</p>
        <ul className="mb-2">
          {result.newInvoiceIds.map((newId, i) => (
            <li key={newId}><a href={`/invoices/edit/${newId}`} target="_blank" rel="noopener">New Invoice {i + 1}</a></li>
          ))}
        </ul>
        <Link to={`/invoices/edit/${id}`} className="btn btn-sm btn-primary">Back to Invoice {invoice.invoiceNumber}</Link>
      </div>
    );
  }

  const remaining = rows.filter((r) => r.groupIndex === 0);

  return (
    <>
      <div className="d-flex align-items-center justify-content-between mb-2">
        <h5 className="mb-0 small">Split Invoice {invoice.invoiceNumber} — {invoice.clientName}</h5>
      </div>
      {error && <div className="alert alert-danger py-2">{error}</div>}

      <div className="card mb-2">
        <div className="card-header py-1"><strong>Assign Line Items</strong></div>
        <div className="card-body py-2">
          <table className="table table-sm mb-0 align-middle">
            <thead><tr><th style={{ width: 60 }}>Item#</th><th>Description</th><th style={{ width: 90 }} className="text-end">Total</th><th style={{ width: 220 }}>Goes To</th></tr></thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.itemId}>
                  <td>{r.itemNumber ?? '-'}</td>
                  <td>{r.description}</td>
                  <td className="text-end">{money(r.totalPrice)}</td>
                  <td>
                    <select className="form-select form-select-sm" value={r.groupIndex} aria-label={`Destination for ${r.description}`}
                            onChange={(e) => setGroup(r.itemId, Number(e.target.value))}>
                      <option value={0}>Stay on original</option>
                      {groupDueDates.map((_, g) => <option key={g + 1} value={g + 1}>New Invoice {g + 1}</option>)}
                      <option value={-1}>+ Add new invoice</option>
                    </select>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {groupCount > 0 && (
        <>
          <div className="card mb-2">
            <div className="card-header py-1"><strong>New Invoices</strong></div>
            <div className="card-body py-2">
              <div className="row g-2 small text-muted mb-1">
                <div className="col-3">Invoice</div><div className="col-3">Items</div><div className="col-3">Total</div><div className="col-3">Due Date</div>
              </div>
              {groupDueDates.map((due, g) => {
                const groupRows = rows.filter((r) => r.groupIndex === g + 1);
                return (
                  <div key={g} className="row g-2 small align-items-center mb-1">
                    <div className="col-3">New Invoice {g + 1}</div>
                    <div className="col-3">{groupRows.length} item(s)</div>
                    <div className="col-3">{money(groupRows.reduce((s, r) => s + r.totalPrice, 0))}</div>
                    <div className="col-3">
                      <DateInput className="form-control form-control-sm" aria-label={`Due date for new invoice ${g + 1}`} value={due}
                                 onChange={(v) => setGroupDueDates((d) => d.map((x, i) => (i === g ? v : x)))} />
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
          <div className="card mb-2">
            <div className="card-header py-1"><strong>Source Invoice (Remaining)</strong></div>
            <div className="card-body py-2 small">
              {remaining.length === 0
                ? <span className="text-danger">The source invoice must retain at least one item.</span>
                : <span>{remaining.length} item(s), {money(remaining.reduce((s, r) => s + r.totalPrice, 0))}</span>}
            </div>
          </div>
          <div className="card mb-2">
            <div className="card-body py-2">
              <label className="form-label small mb-0">Reason</label>
              <textarea className="form-control form-control-sm" rows={2} value={reason} onChange={(e) => setReason(e.target.value)} />
            </div>
          </div>
        </>
      )}

      <div className="d-flex justify-content-end gap-2">
        <button type="button" className="btn btn-secondary" onClick={() => navigate(`/invoices/edit/${id}`)}>Cancel</button>
        <button type="button" className="btn btn-primary" onClick={save} disabled={!canSave}>
          {saving ? <><span className="spinner-border spinner-border-sm me-1" /><span>Splitting...</span></> : <span>Split Invoice</span>}
        </button>
      </div>
    </>
  );
}
