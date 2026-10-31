import { useState } from 'react';
import { useParams } from 'react-router';
import { InvoiceApi } from '@/api/billing';
import type { InvoiceDto } from '@/api/types.gen';
import { ClinicLetterhead, PrintPage } from '@/components/PrintLayout';
import { formatDate } from '@/lib/dates';
import { money } from '@/lib/format';
import { useMountEffect, usePageTitle } from '@/lib/hooks';

export default function InvoicePrint() {
  const { id } = useParams();
  const [invoice, setInvoice] = useState<InvoiceDto | null>(null);
  const [loading, setLoading] = useState(true);
  usePageTitle(`Invoice ${invoice?.invoiceNumber ?? ''} - IntelliMed`);

  useMountEffect(async (alive) => {
    const i = await InvoiceApi.getById(Number(id));
    if (!alive()) return;
    setInvoice(i);
    setLoading(false);
  });

  const totalGst = invoice?.items.reduce((s, i) => s + i.gstAmount, 0) ?? 0;

  return (
    <PrintPage loading={loading} loadingText="Loading invoice..." missing={!invoice} missingText="Invoice not found.">
      {invoice && (
        <>
          <div className="d-flex justify-content-between align-items-start mb-4">
            <ClinicLetterhead clinic={invoice} />
            <div className="text-end">
              <h3 className="mb-1">INVOICE</h3>
              <div><strong>{invoice.invoiceNumber}</strong></div>
            </div>
          </div>
          <div className="row mb-4">
            <div className="col-6">
              <div className="text-muted small">Bill To</div>
              <div className="fw-semibold">{invoice.clientName}</div>
            </div>
            <div className="col-6">
              <div className="row g-1 small">
                <div className="col-6 text-muted">Invoice Date</div><div className="col-6">{formatDate(invoice.invoiceDate, 'dd/MM/yyyy')}</div>
                <div className="col-6 text-muted">Due Date</div><div className="col-6">{formatDate(invoice.dueDate, 'dd/MM/yyyy')}</div>
                <div className="col-6 text-muted">Provider</div><div className="col-6">{invoice.practitionerName ?? '-'}</div>
                {invoice.payeePractitionerId != null && invoice.payeePractitionerId !== invoice.practitionerId && (
                  <><div className="col-6 text-muted">Payee</div><div className="col-6">{invoice.payeePractitionerName}</div></>
                )}
                <div className="col-6 text-muted">Account Type</div><div className="col-6">{invoice.accountTypeName}</div>
              </div>
            </div>
          </div>
          <table className="table table-sm table-bordered">
            <thead>
              <tr>
                <th style={{ width: 100 }}>Service Date</th>
                <th>Description</th>
                <th style={{ width: 50 }} className="text-end">Qty</th>
                <th style={{ width: 90 }} className="text-end">Fee</th>
                <th style={{ width: 90 }} className="text-end">Rebate</th>
                <th style={{ width: 90 }} className="text-end">Gap</th>
                <th style={{ width: 90 }} className="text-end">Total</th>
              </tr>
            </thead>
            <tbody>
              {invoice.items.map((item) => (
                <tr key={item.id}>
                  <td>{item.serviceDate ? formatDate(item.serviceDate, 'dd/MM/yyyy') : '-'}</td>
                  <td>{item.description}</td>
                  <td className="text-end">{item.quantity}</td>
                  <td className="text-end">{money(item.unitPrice)}</td>
                  <td className="text-end">{money(item.lineRebate)}</td>
                  <td className="text-end">{money(item.gap)}</td>
                  <td className="text-end">{money(item.totalPrice)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="row">
            <div className="col-6">
              {invoice.notes?.trim() && <><div className="text-muted small">Notes</div><div>{invoice.notes}</div></>}
            </div>
            <div className="col-6">
              <table className="table table-sm mb-0">
                <tbody>
                  <tr><td>Total Fee</td><td className="text-end">{money(invoice.totalAmount)}</td></tr>
                  {totalGst !== 0 && <tr><td>GST</td><td className="text-end">{money(totalGst)}</td></tr>}
                  <tr><td>Total Rebate</td><td className="text-end">{money(invoice.totalRebate)}</td></tr>
                  <tr><td>Total Gap</td><td className="text-end">{money(invoice.totalGap)}</td></tr>
                  <tr><td>Amount Paid</td><td className="text-end">{money(invoice.amountPaid)}</td></tr>
                  <tr className="fw-bold border-top"><td>Balance Due</td><td className="text-end">{money(invoice.amountOwing)}</td></tr>
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}
    </PrintPage>
  );
}
