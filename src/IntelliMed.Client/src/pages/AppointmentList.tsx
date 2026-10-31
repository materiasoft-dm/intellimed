import { useRef, useState } from 'react';
import { useNavigate } from 'react-router';
import { AppointmentApi, PractitionerApi } from '@/api/appointments';
import { AppointmentStatus, type AppointmentDto, type AppointmentSearchDto, type PractitionerDto } from '@/api/types.gen';
import { AppointmentDetailsModal } from '@/components/AppointmentDetails';
import { ClientPicker } from '@/components/ClientPicker';
import { DataGrid, type Column } from '@/components/DataGrid';
import { DateInput, EnumOptions, NumberSelect } from '@/components/Inputs';
import { appointmentStatusClass } from '@/components/StatusBadges';
import { formatDate } from '@/lib/dates';
import { useMountEffect, usePageTitle, usePagedGrid } from '@/lib/hooks';

type Filter = Pick<AppointmentSearchDto, 'clientId' | 'practitionerId' | 'status' | 'fromDate' | 'toDate'>;
const emptyFilter = (): Filter => ({ clientId: null, practitionerId: null, status: null, fromDate: null, toDate: null });

export default function AppointmentList() {
  usePageTitle('Appointments - IntelliMed');
  const navigate = useNavigate();
  const [filter, setFilter] = useState<Filter>(emptyFilter);
  const [clientName, setClientName] = useState<string | null>(null);
  const [practitioners, setPractitioners] = useState<PractitionerDto[]>([]);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [selected, setSelected] = useState<number | null>(null);

  // Like the Blazor grid's LoadData, every load (Find, Refresh, paging) uses the filter as it stands now.
  const filterRef = useRef(filter);
  filterRef.current = filter;
  const grid = usePagedGrid<AppointmentDto>((page, pageSize) => AppointmentApi.search({ ...filterRef.current, page, pageSize }), 25);

  useMountEffect(async () => {
    setPractitioners(await PractitionerApi.active());
  });
  useMountEffect(() => grid.load(1));

  const find = () => grid.load(1);

  const columns: Column<AppointmentDto>[] = [
    {
      title: 'Date', width: 100, render: (a) => (
        <a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); setSelected(a.id); }}>{formatDate(a.appointmentDate, 'dd/MM/yyyy')}</a>
      ),
    },
    { title: 'Time', width: 130, render: (a) => a.timeRangeFormatted, className: 'text-nowrap' },
    { title: 'Client', width: 180, render: (a) => a.clientName },
    { title: 'Practitioner', width: 160, render: (a) => a.practitionerName },
    { title: 'Type', width: 100, render: (a) => a.typeName },
    { title: 'Status', width: 110, render: (a) => <span className={`badge ${appointmentStatusClass(a.status)}`}>{a.statusName}</span> },
  ];

  return (
    <>
      <h4 className="mb-4">Appointments</h4>

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
            <div className="card-body" onKeyDown={(e) => { if (e.key === 'Enter') find(); }}>
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
                <label className="form-label small mb-0">Practitioner</label>
                <NumberSelect className="form-select form-select-sm" value={filter.practitionerId} onChange={(v) => setFilter({ ...filter, practitionerId: v })}>
                  <option value="">Any</option>
                  {practitioners.map((p) => <option key={p.id} value={p.id}>{p.fullName}</option>)}
                </NumberSelect>
              </div>
              <div className="mb-2">
                <label className="form-label small mb-0">Status</label>
                <NumberSelect enumType={AppointmentStatus} className="form-select form-select-sm" value={filter.status} onChange={(v) => setFilter({ ...filter, status: v as AppointmentStatus | null })}>
                  <option value="">All</option>
                  <EnumOptions enumType={AppointmentStatus} />
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
              <button type="button" className="btn btn-primary" onClick={find}>Find</button>
              <button type="button" className="btn btn-secondary" onClick={() => { setFilter(emptyFilter()); setClientName(null); }}>Clear</button>
            </div>
          </div>
        </div>

        <div className="col-12 col-md-9 mt-3 mt-md-0">
          <div className="card">
            <div className="card-header d-flex align-items-center justify-content-between py-2">
              <span className="small text-muted"><i className="bi bi-calendar-week me-1" /> Total: <strong>{grid.total}</strong></span>
              <button className="btn btn-sm btn-outline-primary" onClick={grid.reload} disabled={grid.loading}>
                <i className={`bi bi-arrow-clockwise me-1 ${grid.loading ? 'imed-spin' : ''}`} /> Refresh
              </button>
            </div>
            <DataGrid columns={columns} rows={grid.rows} rowKey={(a) => a.id} total={grid.total} pageSize={grid.pageSize}
                      loading={grid.loading} emptyText="No appointments found." emptyIcon="bi-calendar-x" loadingMore={grid.loadingMore} onLoadMore={grid.loadMore} noun="appointments" />
          </div>
          <div className="d-flex justify-content-end gap-2 mt-3">
            <button className="btn btn-sm btn-outline-secondary" onClick={() => navigate('/appointments/calendar')}><i className="bi bi-calendar3" /> Calendar View</button>
            <button className="btn btn-sm btn-primary" onClick={() => navigate('/appointments/new')}><i className="bi bi-plus-lg" /> New Appointment</button>
          </div>
        </div>
      </div>

      <AppointmentDetailsModal appointmentId={selected} onClose={() => setSelected(null)} onChanged={grid.reload} />

      {pickerOpen && (
        <ClientPicker onClose={() => setPickerOpen(false)}
                      onSelect={(c) => { setFilter({ ...filter, clientId: c.id }); setClientName(c.fullName); setPickerOpen(false); }} />
      )}
    </>
  );
}
