import { useRef, useState } from 'react';
import { useNavigate } from 'react-router';
import { InvoiceApi } from '@/api/billing';
import { InvoiceStatus, type InvoiceDto, type InvoiceSearchDto } from '@/api/types.gen';
import { ClientPicker } from '@/components/ClientPicker';
import { DataGrid, type Column } from '@/components/DataGrid';
import { DateInput, EnumOptions, NumberSelect } from '@/components/Inputs';
import { invoiceStatusClass } from '@/components/StatusBadges';
import { formatDate } from '@/lib/dates';
import { money } from '@/lib/format';
import { useMountEffect, usePageTitle, usePagedGrid } from '@/lib/hooks';

type Filter = Pick<InvoiceSearchDto, 'clientId' | 'status' | 'fromDate' | 'toDate'>;
const emptyFilter = (): Filter => ({ clientId: null, status: null, fromDate: null, toDate: null });

export default function InvoiceSearch() {
  usePageTitle('Invoice Search - IntelliMed');
  const navigate = useNavigate();
  const [filter, setFilter] = useState<Filter>(emptyFilter);
  const [clientName, setClientName] = useState<string | null>(null);
  const [pickerOpen, setPickerOpen] = useState(false);
  const filterRef = useRef(filter);
  filterRef.current = filter;

  const grid = usePagedGrid<InvoiceDto>((page, pageSize) => InvoiceApi.search({ ...filterRef.current, page, pageSize }), 25);
  useMountEffect(() => grid.load(1));

  const columns: Column<InvoiceDto>[] = [
    {
      title: 'Invoice #', width: 120, render: (i) => (
        <a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); navigate(`/invoices/edit/${i.id}`); }}>{i.invoiceNumber}</a>
      ),
    },
    { title: 'Client', width: 180, render: (i) => i.clientName },
    { title: 'Date', width: 100, render: (i) => formatDate(i.invoiceDate, 'dd/MM/yyyy') },
    { title: 'Due', width: 100, render: (i) => formatDate(i.dueDate, 'dd/MM/yyyy') },
    { title: 'Status', width: 110, render: (i) => <span className={`badge ${invoiceStatusClass(i.status)} imed-status-pill`}>{i.statusName}</span> },
    { title: 'Total', width: 100, render: (i) => money(i.totalAmount), className: 'text-end', headerClassName: 'text-end' },
    { title: 'Owing', width: 100, render: (i) => <span className={i.amountOwing > 0 ? 'fw-semibold' : 'text-muted'}>{money(i.amountOwing)}</span>, className: 'text-end', headerClassName: 'text-end' },
  ];

  return (
    <>
      <h4 className="mb-4">Invoice Search</h4>
      {grid.error && (
        <div className="alert alert-danger d-flex align-items-center mb-4">
          <i className="bi bi-exclamation-triangle me-2" /> {grid.error}
          <button className="btn btn-outline-danger btn-sm ms-auto" onClick={grid.reload}>Retry</button>
        </div>
      )}
      <div className="row">
        <div className="col-12 col-md-3">
          <div className="card mb-4">
            <div className="card-header"><strong>Filters</strong></div>
            <div className="card-body" onKeyDown={(e) => { if (e.key === 'Enter') grid.load(1); }}>
              <div className="mb-2">
                <label className="form-label small mb-0">Client</label>
                <div className="input-group input-group-sm">
                  <input className="form-control form-control-sm" value={clientName ?? ''} readOnly placeholder="Any" />
                  <button type="button" className="btn btn-outline-secondary" onClick={() => setPickerOpen(true)}>Find</button>
                  {filter.clientId != null && (
                    <button type="button" className="btn btn-outline-secondary" title="Clear client" aria-label="Clear client"
                            onClick={() => { setFilter({ ...filter, clientId: null }); setClientName(null); }}>x</button>
                  )}
                </div>
              </div>
              <div className="mb-2">
                <label className="form-label small mb-0">Status</label>
                <NumberSelect enumType={InvoiceStatus} className="form-select form-select-sm" value={filter.status} onChange={(v) => setFilter({ ...filter, status: v as InvoiceStatus | null })}>
                  <option value="">All</option>
                  <EnumOptions enumType={InvoiceStatus} />
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
              <button type="button" className="btn btn-secondary" onClick={() => { setFilter(emptyFilter()); setClientName(null); }}>Clear</button>
            </div>
          </div>
        </div>
        <div className="col-12 col-md-9 mt-3 mt-md-0">
          <div className="card">
            <div className="card-header d-flex align-items-center justify-content-between py-2">
              <span className="small text-muted"><i className="bi bi-receipt me-1" /> Total Invoices: <strong>{grid.total}</strong></span>
              <button className="btn btn-sm btn-outline-primary" onClick={grid.reload} disabled={grid.loading}>
                <i className={`bi bi-arrow-clockwise me-1 ${grid.loading ? 'imed-spin' : ''}`} /> Refresh
              </button>
            </div>
            <DataGrid columns={columns} rows={grid.rows} rowKey={(i) => i.id} total={grid.total} pageSize={grid.pageSize}
                      loading={grid.loading} emptyText="No invoices found." emptyIcon="bi-receipt" loadingMore={grid.loadingMore} onLoadMore={grid.loadMore} noun="invoices" />
          </div>
          <div className="d-flex justify-content-end mt-3">
            <button className="btn btn-sm btn-primary" onClick={() => navigate('/invoices/new')}><i className="bi bi-plus-lg" /> New Invoice</button>
          </div>
        </div>
      </div>

      {pickerOpen && (
        <ClientPicker onClose={() => setPickerOpen(false)}
                      onSelect={(c) => { setFilter({ ...filter, clientId: c.id }); setClientName(c.fullName); setPickerOpen(false); }} />
      )}
    </>
  );
}
