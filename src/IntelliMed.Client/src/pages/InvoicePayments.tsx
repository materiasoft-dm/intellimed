import { useRef, useState } from 'react';
import { useNavigate } from 'react-router';
import { InvoiceApi } from '@/api/billing';
import { PaymentMethod, type PaymentDto, type PaymentSearchDto } from '@/api/types.gen';
import { DataGrid, type Column } from '@/components/DataGrid';
import { DateInput, EnumOptions, NumberSelect } from '@/components/Inputs';
import { formatDate } from '@/lib/dates';
import { money } from '@/lib/format';
import { useMountEffect, usePageTitle, usePagedGrid } from '@/lib/hooks';

type Filter = Pick<PaymentSearchDto, 'method' | 'fromDate' | 'toDate'>;
const emptyFilter = (): Filter => ({ method: null, fromDate: null, toDate: null });

export default function InvoicePayments() {
  usePageTitle('Payments - IntelliMed');
  const navigate = useNavigate();
  const [filter, setFilter] = useState<Filter>(emptyFilter);
  const filterRef = useRef(filter);
  filterRef.current = filter;

  const grid = usePagedGrid<PaymentDto>((page, pageSize) => InvoiceApi.payments({ ...filterRef.current, page, pageSize }), 25);
  useMountEffect(() => grid.load(1));
  const pageSum = grid.rows.reduce((sum, p) => sum + p.amount, 0);

  const columns: Column<PaymentDto>[] = [
    { title: 'Date', width: 100, render: (p) => formatDate(p.paymentDate, 'dd/MM/yyyy') },
    { title: 'Client', width: 180, render: (p) => p.clientName },
    {
      title: 'Invoice #', width: 120, render: (p) => (
        <a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); navigate(`/invoices/edit/${p.invoiceId}`); }}>{p.invoiceNumber}</a>
      ),
    },
    { title: 'Method', width: 120, render: (p) => p.methodName },
    { title: 'Reference', width: 140, render: (p) => p.reference },
    { title: 'Amount', width: 100, render: (p) => money(p.amount), className: 'text-end', headerClassName: 'text-end' },
    {
      title: '', width: 60, className: 'text-end', render: (p) => (
        <a href={`/invoices/receipt/${p.receiptId}`} target="_blank" rel="noopener" className="btn btn-sm btn-outline-secondary" title="Print Receipt">
          <i className="bi bi-printer" />
        </a>
      ),
    },
  ];

  return (
    <>
      <h4 className="mb-4">Payments</h4>
      <div className="row">
        <div className="col-12 col-md-3">
          <div className="card mb-4">
            <div className="card-header"><strong>Filters</strong></div>
            <div className="card-body" onKeyDown={(e) => { if (e.key === 'Enter') grid.load(1); }}>
              <div className="mb-2">
                <label className="form-label small mb-0">Method</label>
                <NumberSelect enumType={PaymentMethod} className="form-select form-select-sm" value={filter.method} onChange={(v) => setFilter({ ...filter, method: v as PaymentMethod | null })}>
                  <option value="">All</option>
                  <EnumOptions enumType={PaymentMethod} />
                </NumberSelect>
              </div>
              <div className="mb-2">
                <label className="form-label small mb-0">From</label>
                <DateInput className="form-control form-control-sm" value={filter.fromDate} onChange={(v) => setFilter({ ...filter, fromDate: v })} />
              </div>
              <div className="mb-3">
                <label className="form-label small mb-0">To</label>
                <DateInput className="form-control form-control-sm" value={filter.toDate} onChange={(v) => setFilter({ ...filter, toDate: v })} />
              </div>
            </div>
            <div className="card-footer d-flex justify-content-between">
              <button type="button" className="btn btn-primary" onClick={() => grid.load(1)}>Find</button>
              <button type="button" className="btn btn-secondary" onClick={() => setFilter(emptyFilter())}>Clear</button>
            </div>
          </div>
        </div>
        <div className="col-12 col-md-9 mt-3 mt-md-0">
          <div className="card">
            <div className="card-header d-flex align-items-center justify-content-between py-2">
              <span className="small text-muted">
                <i className="bi bi-cash-coin me-1" /> Total: <strong>{grid.total}</strong> &middot; Sum: <strong>{money(pageSum)}</strong>
              </span>
              <button className="btn btn-sm btn-outline-primary" onClick={grid.reload} disabled={grid.loading}>
                <i className={`bi bi-arrow-clockwise me-1 ${grid.loading ? 'imed-spin' : ''}`} /> Refresh
              </button>
            </div>
            <DataGrid columns={columns} rows={grid.rows} rowKey={(p) => p.id} total={grid.total} pageSize={grid.pageSize}
                      loading={grid.loading} emptyText="No payments found." emptyIcon="bi-cash-coin" loadingMore={grid.loadingMore} onLoadMore={grid.loadMore} noun="payments" />
          </div>
        </div>
      </div>
    </>
  );
}
