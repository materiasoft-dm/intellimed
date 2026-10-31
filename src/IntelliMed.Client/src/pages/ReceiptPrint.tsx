import { useState } from 'react';
import { useParams } from 'react-router';
import { ReceiptApi } from '@/api/billing';
import { AllocationType, type ReceiptAllocationDto, type ReceiptDto } from '@/api/types.gen';
import { ClinicLetterhead, PrintPage } from '@/components/PrintLayout';
import { formatDate } from '@/lib/dates';
import { money } from '@/lib/format';
import { useMountEffect, usePageTitle } from '@/lib/hooks';

function allocationLabel(a: ReceiptAllocationDto): string {
  if (a.allocationType === AllocationType.Credit) return 'Held as credit';
  if (a.allocationType === AllocationType.Refund) return a.invoiceId == null ? 'Refunded from account credit' : `Refund — Invoice ${a.invoiceNumber}`;
  return `Invoice ${a.invoiceNumber}`;
}

export default function ReceiptPrint() {
  usePageTitle('Receipt - IntelliMed');
  const { receiptId } = useParams();
  const [receipt, setReceipt] = useState<ReceiptDto | null>(null);
  const [loading, setLoading] = useState(true);

  useMountEffect(async (alive) => {
    const r = await ReceiptApi.getById(Number(receiptId));
    if (!alive()) return;
    setReceipt(r);
    setLoading(false);
  });

  return (
    <PrintPage loading={loading} loadingText="Loading receipt..." missing={!receipt} missingText="Receipt not found." maxWidth={700}>
      {receipt && (
        <>
          <div className="d-flex justify-content-between align-items-start mb-4">
            <ClinicLetterhead clinic={receipt} />
            <div className="text-end">
              <h3 className="mb-1">RECEIPT</h3>
              <div><strong>Receipt No. {receipt.id}</strong></div>
            </div>
          </div>
          <div className="row g-1 small mb-4">
            <div className="col-6 text-muted">Received From</div><div className="col-6">{receipt.payerName}</div>
            <div className="col-6 text-muted">Receipt Date</div><div className="col-6">{formatDate(receipt.receiptDate, 'dd/MM/yyyy')}</div>
            {receipt.notes?.trim() && <><div className="col-6 text-muted">Notes</div><div className="col-6">{receipt.notes}</div></>}
          </div>
          <table className="table table-sm">
            <thead><tr><th>Payment Method</th><th>Reference</th><th className="text-end">Amount</th></tr></thead>
            <tbody>
              {receipt.payments.map((t, i) => (
                <tr key={i}><td>{t.methodName}</td><td>{t.reference ?? '-'}</td><td className="text-end">{money(t.amount)}</td></tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="fw-bold"><td colSpan={2} className="text-end">Total Received</td><td className="text-end">{money(receipt.totalTendered)}</td></tr>
            </tfoot>
          </table>
          <table className="table table-sm mb-0">
            <thead><tr><th>Applied To</th><th className="text-end">Amount</th></tr></thead>
            <tbody>
              {receipt.allocations.map((a, i) => (
                <tr key={i}><td>{allocationLabel(a)}</td><td className="text-end">{money(a.amount)}</td></tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </PrintPage>
  );
}
