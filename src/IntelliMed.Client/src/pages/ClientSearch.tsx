import { useRef, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router';
import { ClientApi, LookupApi } from '@/api/clients';
import { AccountTypeEnum, AtsiStatusEnum, ClientTypeEnum, GenderEnum, type ClientDto, type ClientSearchDto, type HealthFundDto } from '@/api/types.gen';
import { ClientPicker } from '@/components/ClientPicker';
import { DataGrid, type Column } from '@/components/DataGrid';
import { Dropdown } from '@/components/Dropdown';
import { DateInput, NumberSelect, TextInput } from '@/components/Inputs';
import { Modal } from '@/components/Modal';
import { EmptyState, LoadingState } from '@/components/States';
import { storage } from '@/api/storage';
import { formatDate } from '@/lib/dates';
import { downloadBlob, padNumber } from '@/lib/format';
import { useMountEffect, usePageTitle, usePagedGrid } from '@/lib/hooks';

type Filter = Partial<ClientSearchDto>;
const PAGE_SIZE = 10;
const COLUMNS_KEY = 'intellimed_client_search_columns';

const ALL_COLUMNS: (Column<ClientDto> & { key: string; label: string })[] = [
  { key: 'name', label: 'Client Name', title: 'Client Name', width: 180, render: () => null },
  { key: 'preferred', label: 'Preferred', title: 'Preferred', width: 120, render: (c) => c.firstName },
  { key: 'dob', label: 'D.O.B.', title: 'D.O.B.', width: 110, render: (c) => formatDate(c.dateOfBirth, 'dd/MM/yyyy') },
  { key: 'file', label: 'File#', title: 'File#', width: 80, render: (c) => padNumber(c.id, 5) },
  { key: 'address', label: 'Street Address', title: 'Street Address', width: 220, render: (c) => c.fullAddress },
  { key: 'home', label: 'Home Phone', title: 'Home Phone', width: 120, render: (c) => c.phone },
  { key: 'bh', label: 'B.H. Phone', title: 'B.H. Phone', width: 120, render: (c) => c.businessHoursPhone },
  { key: 'mobile', label: 'Mobile Phone', title: 'Mobile Phone', width: 120, render: (c) => c.mobilePhone },
  { key: 'gender', label: 'Gender', title: 'Gender', width: 100, render: (c) => (c.gender != null ? GenderEnum[c.gender] : '') },
  { key: 'account', label: 'DF Acc. Type', title: 'DF Acc. Type', width: 130, render: (c) => AccountTypeEnum[c.accountType] },
  { key: 'medicare', label: 'Medicare#', title: 'Medicare#', width: 130, render: (c) => c.medicareNumber },
  { key: 'dva', label: 'Veteran#', title: 'Veteran#', width: 110, render: (c) => c.dvaNumber },
  { key: 'fund', label: 'Fund ID', title: 'Fund ID', width: 90, render: (c) => c.healthFundCode },
  { key: 'email', label: 'Email', title: 'Email', width: 180, render: (c) => c.email },
];

function Section({ title, open, onToggle, children }: { title: string; open: boolean; onToggle: () => void; children: ReactNode }) {
  return (
    <>
      <div className="filter-section-header d-flex justify-content-between align-items-center" role="button" tabIndex={0} aria-expanded={open}
           onClick={onToggle} onKeyDown={(e) => { if (e.key === ' ') { e.preventDefault(); onToggle(); } }}>
        <span className="small fw-semibold text-uppercase text-muted">{title}</span>
        <i className={`bi ${open ? 'bi-chevron-up' : 'bi-chevron-down'} small text-muted`} />
      </div>
      {open && children}
    </>
  );
}

export default function ClientSearch() {
  usePageTitle('Client Search - IntelliMed');
  const navigate = useNavigate();
  const [filter, setFilter] = useState<Filter>({});
  const [accountTypes, setAccountTypes] = useState<Set<AccountTypeEnum>>(new Set());
  const filterRef = useRef({ filter, accountTypes });
  filterRef.current = { filter, accountTypes };
  const grid = usePagedGrid<ClientDto>((page, pageSize) => ClientApi.search({
    ...filterRef.current.filter, page, pageSize,
    accountTypes: filterRef.current.accountTypes.size ? [...filterRef.current.accountTypes] : null,
  }), PAGE_SIZE);

  const [selected, setSelected] = useState<ClientDto | null>(null);
  const [healthFunds, setHealthFunds] = useState<HealthFundDto[]>([]);
  const [tab, setTab] = useState<'Basic' | 'Advance'>('Basic');
  // phones only (CSS ignores this from md up): filters start open, then fold away after a search
  const [mobileFiltersOpen, setMobileFiltersOpen] = useState(true);
  const [sections, setSections] = useState({ identifiers: true, address: false, contact: false, dates: false, account: false, flags: false });
  const [payerName, setPayerName] = useState<string | null>(null);
  const [payerPickerOpen, setPayerPickerOpen] = useState(false);
  const [underConstruction, setUnderConstruction] = useState<string | null>(null);
  const [hiddenColumns, setHiddenColumns] = useState<Set<string>>(() => {
    try { return new Set(JSON.parse(storage.get(COLUMNS_KEY) ?? '[]')); } catch { return new Set(); }
  });

  useMountEffect(async () => {
    grid.load(1);
    setHealthFunds(await LookupApi.healthFunds());
  });

  const find = () => {
    grid.load(1);
    setMobileFiltersOpen(false);
  };
  const set = (patch: Filter) => setFilter((f) => ({ ...f, ...patch }));
  const toggleSection = (key: keyof typeof sections) => setSections((s) => ({ ...s, [key]: !s[key] }));
  const toggleColumn = (key: string) => setHiddenColumns((h) => {
    const next = new Set(h);
    if (!next.delete(key)) next.add(key);
    storage.set(COLUMNS_KEY, JSON.stringify([...next]));
    return next;
  });

  const archiveSelected = async () => {
    if (!selected) return;
    await ClientApi.archive(selected.id);
    setSelected(null);
    grid.reload();
  };

  const exportCsv = () => {
    const q = (s: string | null | undefined) => `"${(s ?? '').replace(/"/g, '""')}"`;
    const lines = ['FullName,DateOfBirth,Email,PhoneNumber,IsActive',
      ...grid.rows.map((c) => `${q(c.fullName)},${c.dateOfBirth ? formatDate(c.dateOfBirth, 'yyyy-MM-dd') : ''},${q(c.email)},${q(c.phone)},${c.isActive ? 'True' : 'False'}`)];
    downloadBlob(new Blob([lines.join('\r\n') + '\r\n'], { type: 'text/csv' }), 'clients.csv');
  };

  const columns: Column<ClientDto>[] = ALL_COLUMNS.filter((c) => !hiddenColumns.has(c.key)).map((c) => c.key === 'name' ? {
    ...c, render: (row: ClientDto) => (
      <a href="#" className="link-primary" onClick={(e) => { e.preventDefault(); e.stopPropagation(); navigate(`/clients/edit/${row.id}`); }}>{row.fullName}</a>
    ),
  } : c);

  const text = (key: keyof ClientSearchDto, label: string, narrow = true) => (
    <div className={narrow ? 'col-6 d-flex align-items-center gap-1 flex-wrap' : 'd-flex align-items-center gap-2 mb-1 flex-wrap'}>
      <label className={`form-label small ${narrow ? 'filter-label-sm' : 'filter-label'}`}>{label}</label>
      <TextInput className="form-control form-control-sm" value={filter[key] as string | null | undefined} onChange={(v) => set({ [key]: v })} />
    </div>
  );
  const range = (from: keyof ClientSearchDto, to: keyof ClientSearchDto, label: string) => (
    <div className="d-flex align-items-center gap-2 mb-1 flex-wrap">
      <label className="form-label small filter-label">{label}</label>
      <DateInput className="form-control form-control-sm" aria-label={`${label} from`} value={filter[from] as string | null | undefined} onChange={(v) => set({ [from]: v })} />
      <span className="small text-muted">-</span>
      <DateInput className="form-control form-control-sm" aria-label={`${label} to`} value={filter[to] as string | null | undefined} onChange={(v) => set({ [to]: v })} />
    </div>
  );
  const triState = (key: 'acceptEmail' | 'acceptSms' | 'acceptSmsMarketing', label: string, last = false) => (
    <div className={`d-flex align-items-center gap-2 ${last ? 'mb-3' : 'mb-1'} flex-wrap`}>
      <label className="form-label small filter-label">{label}</label>
      <select className="form-select form-select-sm" value={filter[key] == null ? '' : String(filter[key])}
              onChange={(e) => set({ [key]: e.target.value === '' ? null : e.target.value === 'true' })}>
        <option value="">Any</option><option value="true">Yes</option><option value="false">No</option>
      </select>
    </div>
  );

  return (
    <>
      <h4 className="mb-4">Client Search</h4>
      {grid.error && (
        <div className="alert alert-danger d-flex align-items-center mb-4">
          <i className="bi bi-exclamation-triangle me-2" /> {grid.error}
          <button className="btn btn-outline-danger btn-sm ms-auto" onClick={grid.reload}>Retry</button>
        </div>
      )}

      <div className="row">
        <div className="col-12 col-md-3">
          <div className="card mb-4 filter-panel-card">
            <div className="card-header d-flex align-items-center justify-content-between">
              <ul className="nav nav-tabs card-header-tabs flex-grow-1 mb-0">
                {(['Basic', 'Advance'] as const).map((t) => (
                  <li key={t} className="nav-item"><a className={`nav-link ${tab === t ? 'active' : ''}`} href="#" onClick={(e) => { e.preventDefault(); setTab(t); }}>{t}</a></li>
                ))}
              </ul>
              <button type="button" className="btn btn-sm btn-link text-decoration-none d-md-none p-0 ms-2" title="Show/hide filters" onClick={() => setMobileFiltersOpen((o) => !o)}>
                <i className={`bi ${mobileFiltersOpen ? 'bi-chevron-up' : 'bi-chevron-down'}`} />
              </button>
            </div>
            <div className={`card-body filter-panel-body ${mobileFiltersOpen ? '' : 'd-none d-md-block'}`} onKeyDown={(e) => { if (e.key === 'Enter') find(); }}>
              {tab === 'Basic' ? (
                <>
                  <Section title="Identifiers" open={sections.identifiers} onToggle={() => toggleSection('identifiers')}>
                    <div className="row g-2 mb-1">{text('surname', 'Family/Org')}{text('givenName', 'First')}</div>
                    <div className="row g-2 mb-1">
                      {text('medicareNumber', 'Medicare#')}
                      <div className="col-6 d-flex align-items-center gap-1 flex-wrap">
                        <label className="form-label small filter-label-sm">Gender</label>
                        <NumberSelect enumType={GenderEnum} className="form-select form-select-sm" value={filter.gender} onChange={(v) => set({ gender: v as GenderEnum | null })}>
                          <option value="">All</option>
                          <option value={GenderEnum.Unspecified}>Unspecified</option>
                          <option value={GenderEnum.Male}>Male</option>
                          <option value={GenderEnum.Female}>Female</option>
                          <option value={GenderEnum.Other}>Other</option>
                        </NumberSelect>
                      </div>
                    </div>
                    <div className="row g-2 mb-1">{text('dvaNumber', 'DVA#')}{text('fileNumber', 'File#')}</div>
                    <div className="row g-2 mb-1">{text('pensionNumber', 'Pension')}{text('healthFundNumber', 'Fund#')}</div>
                    {range('dobFrom', 'dobTo', 'D.O.B.')}
                  </Section>
                  <hr className="my-2" />
                  <Section title="Address" open={sections.address} onToggle={() => toggleSection('address')}>
                    {text('address', 'Address', false)}
                    <div className="d-flex align-items-center gap-2 mb-1 flex-wrap">
                      <span className="filter-label" />
                      <TextInput className="form-control form-control-sm" placeholder="Suburb" value={filter.suburb} onChange={(v) => set({ suburb: v })} />
                      <TextInput className="form-control form-control-sm" placeholder="Postcode" value={filter.postcode} onChange={(v) => set({ postcode: v })} />
                      <TextInput className="form-control form-control-sm" placeholder="State" value={filter.state} onChange={(v) => set({ state: v })} />
                    </div>
                    {text('postalAddress', 'Postal Addr', false)}
                    <div className="d-flex align-items-center gap-2 mb-1 flex-wrap">
                      <span className="filter-label" />
                      <TextInput className="form-control form-control-sm" placeholder="Suburb" value={filter.postalSuburb} onChange={(v) => set({ postalSuburb: v })} />
                      <TextInput className="form-control form-control-sm" placeholder="Postcode" value={filter.postalPostcode} onChange={(v) => set({ postalPostcode: v })} />
                      <TextInput className="form-control form-control-sm" placeholder="State" value={filter.postalState} onChange={(v) => set({ postalState: v })} />
                    </div>
                  </Section>
                  <hr className="my-2" />
                  <Section title="Contact" open={sections.contact} onToggle={() => toggleSection('contact')}>
                    <div className="row g-2 mb-1">{text('homePhone', 'Home Ph.')}{text('businessHoursPhone', 'B.H.')}</div>
                    {text('mobilePhone', 'Mobile Phone', false)}
                    {text('email', 'Email', false)}
                    <div className="d-flex align-items-center gap-2 mb-1 flex-wrap">
                      <label className="form-label small filter-label">ATSI</label>
                      <NumberSelect enumType={AtsiStatusEnum} className="form-select form-select-sm" value={filter.atsiStatus} onChange={(v) => set({ atsiStatus: v as AtsiStatusEnum | null })}>
                        <option value="">All</option>
                        <option value={AtsiStatusEnum.NotAsked}>Not Asked</option>
                        <option value={AtsiStatusEnum.AboriginalOnly}>Aboriginal Only</option>
                        <option value={AtsiStatusEnum.TorresStraitIslanderOnly}>TSI Only</option>
                        <option value={AtsiStatusEnum.Both}>Both</option>
                        <option value={AtsiStatusEnum.NeitherAboriginalNorTorresStraitIslander}>Neither</option>
                      </NumberSelect>
                    </div>
                  </Section>
                  <hr className="my-2" />
                  <Section title="Date Ranges" open={sections.dates} onToggle={() => toggleSection('dates')}>
                    {range('createdFrom', 'createdTo', 'Created')}
                    {range('medicareExpiryFrom', 'medicareExpiryTo', 'Medicare EXP')}
                    {range('healthFundJoinFrom', 'healthFundJoinTo', 'PHI Joined')}
                  </Section>
                  <hr className="my-2" />
                  <Section title="Account / Notes" open={sections.account} onToggle={() => toggleSection('account')}>
                    {text('warnings', 'Warnings', false)}
                    {text('notes', 'Note', false)}
                    <div className="d-flex align-items-center gap-2 mb-1 flex-wrap">
                      <label className="form-label small filter-label">Referred by</label>
                      <div className="input-group input-group-sm">
                        <TextInput className="form-control form-control-sm" value={filter.referredBy} onChange={(v) => set({ referredBy: v })} />
                        {filter.referredBy && <button type="button" className="btn btn-outline-secondary" aria-label="Clear referred by" onClick={() => set({ referredBy: null })}>x</button>}
                      </div>
                    </div>
                    <div className="d-flex align-items-center gap-2 mb-1 flex-wrap">
                      <label className="form-label small filter-label">Client Type</label>
                      <NumberSelect enumType={ClientTypeEnum} className="form-select form-select-sm" value={filter.clientType} onChange={(v) => set({ clientType: v as ClientTypeEnum | null })}>
                        <option value="">All</option>
                        <option value={ClientTypeEnum.Person}>Person</option>
                        <option value={ClientTypeEnum.Organisation}>Organisation</option>
                      </NumberSelect>
                    </div>
                    {text('urNumber', 'UR No', false)}
                    <div className="d-flex align-items-center gap-2 mb-1 flex-wrap">
                      <label className="form-label small filter-label">Fund Id</label>
                      <NumberSelect className="form-select form-select-sm" value={filter.healthFundId} onChange={(v) => set({ healthFundId: v })}>
                        <option value="">All</option>
                        {healthFunds.map((f) => <option key={f.id} value={f.id}>{f.code} - {f.name}</option>)}
                      </NumberSelect>
                    </div>
                    <div className="d-flex align-items-center gap-2 mb-1 flex-wrap">
                      <label className="form-label small filter-label">DF Payer</label>
                      <div className="input-group input-group-sm">
                        <input className="form-control form-control-sm" value={payerName ?? ''} readOnly placeholder="Any" />
                        <button type="button" className="btn btn-outline-secondary" onClick={() => setPayerPickerOpen(true)}>Find</button>
                        {filter.payerClientId != null && (
                          <button type="button" className="btn btn-outline-secondary" aria-label="Clear payer" onClick={() => { set({ payerClientId: null }); setPayerName(null); }}>x</button>
                        )}
                      </div>
                    </div>
                    <div className="d-flex align-items-start gap-2 mb-1">
                      <label className="form-label small filter-label mt-1">DF Acc. Type</label>
                      <div className="border rounded p-2 flex-grow-1" style={{ maxHeight: 140, overflowY: 'auto' }}>
                        {Object.values(AccountTypeEnum).filter((v): v is AccountTypeEnum => typeof v === 'number').map((type) => (
                          <div key={type} className="form-check form-check-sm">
                            <input className="form-check-input" type="checkbox" id={`acctype_${AccountTypeEnum[type]}`} checked={accountTypes.has(type)}
                                   onChange={(e) => setAccountTypes((s) => { const n = new Set(s); if (e.target.checked) n.add(type); else n.delete(type); return n; })} />
                            <label className="form-check-label small" htmlFor={`acctype_${AccountTypeEnum[type]}`}>{AccountTypeEnum[type]}</label>
                          </div>
                        ))}
                      </div>
                    </div>
                  </Section>
                  <hr className="my-2" />
                  <Section title="Flags" open={sections.flags} onToggle={() => toggleSection('flags')}>
                    <div className="row g-2 mb-2">
                      <div className="col-6">
                        <div className="form-check">
                          <input className="form-check-input" type="checkbox" id="chkDeceased" checked={!!filter.deceased} onChange={(e) => set({ deceased: e.target.checked || null })} />
                          <label className="form-check-label small" htmlFor="chkDeceased">Deceased</label>
                        </div>
                      </div>
                      <div className="col-6">
                        <div className="form-check">
                          <input className="form-check-input" type="checkbox" id="chkArchived" checked={!!filter.includeArchived} onChange={(e) => set({ includeArchived: e.target.checked })} />
                          <label className="form-check-label small" htmlFor="chkArchived">Archived</label>
                        </div>
                      </div>
                    </div>
                    {triState('acceptEmail', 'Accept Email')}
                    {triState('acceptSms', 'Accept SMS')}
                    {triState('acceptSmsMarketing', 'SMS Mktg', true)}
                  </Section>
                </>
              ) : (
                <div className="text-muted small">Advanced filters coming soon.</div>
              )}
            </div>
            <div className={`card-footer d-flex justify-content-between filter-panel-footer ${mobileFiltersOpen ? '' : 'd-none d-md-flex'}`}>
              <button type="button" className="btn btn-primary" onClick={find}>Find</button>
              <button type="button" className="btn btn-secondary" onClick={() => { setFilter({}); setAccountTypes(new Set()); setPayerName(null); }}>Clear</button>
            </div>
          </div>
        </div>

        <div className="col-12 col-md-9 mt-3 mt-md-0">
          <div className="card">
            <div className="card-header d-flex align-items-center justify-content-between py-2 gap-2">
              <span className="small text-muted"><i className="bi bi-heart-pulse me-1" /> Total Clients: <strong>{grid.total}</strong></span>
              <div className="d-flex gap-2">
                <Dropdown className="d-none d-md-block" toggleClassName="btn btn-sm btn-outline-secondary dropdown-toggle" title="Choose columns"
                          toggle={<><i className="bi bi-layout-three-columns me-1" />Columns</>} menuStyle={{ maxHeight: 360, overflowY: 'auto' }}>
                  {ALL_COLUMNS.map((c) => (
                    <li key={c.key} className="px-2">
                      <div className="form-check small">
                        <input className="form-check-input" type="checkbox" id={`col_${c.key}`} checked={!hiddenColumns.has(c.key)}
                               disabled={c.key === 'name'} onChange={() => toggleColumn(c.key)} />
                        <label className="form-check-label" htmlFor={`col_${c.key}`}>{c.label}</label>
                      </div>
                    </li>
                  ))}
                </Dropdown>
                <button className="btn btn-sm btn-outline-primary" onClick={grid.reload} disabled={grid.loading}>
                  <i className={`bi bi-arrow-clockwise me-1 ${grid.loading ? 'imed-spin' : ''}`} /> Refresh
                </button>
              </div>
            </div>
            <div className="d-none d-md-block">
              <DataGrid columns={columns} rows={grid.rows} rowKey={(c) => c.id} total={grid.total} page={grid.page} pageSize={grid.pageSize}
                        loading={grid.loading} emptyText="No clients found." emptyIcon="bi-people" onPageChange={grid.load}
                        onRowClick={setSelected} rowClassName={(c) => (c.id === selected?.id ? 'table-active imed-row-selected' : '')} />
            </div>
            <div className="d-md-none p-2">
              {grid.loading ? <LoadingState text="Loading..." compact={false} /> : grid.rows.length === 0 ? (
                <EmptyState message="No clients found." icon="bi-people" compact={false} />
              ) : (
                <>
                  {grid.rows.map((c) => (
                    <div key={c.id} className={`card mb-2 client-mobile-card ${c.id === selected?.id ? 'border-primary' : ''}`} onClick={() => setSelected(c)}>
                      <div className="card-body py-2 px-3">
                        <div className="d-flex justify-content-between align-items-start">
                          <a href="#" className="fw-semibold link-primary" onClick={(e) => { e.preventDefault(); e.stopPropagation(); navigate(`/clients/edit/${c.id}`); }}>{c.fullName}</a>
                          {c.id === selected?.id && <i className="bi bi-check-circle-fill text-primary" />}
                        </div>
                        <div className="small text-muted">File# {padNumber(c.id, 5)} &middot; {formatDate(c.dateOfBirth, 'dd/MM/yyyy')}</div>
                        {c.fullAddress?.trim() && <div className="small mt-1"><i className="bi bi-geo-alt text-muted me-1" />{c.fullAddress}</div>}
                        {c.mobilePhone?.trim() && <div className="small"><i className="bi bi-telephone text-muted me-1" />{c.mobilePhone}</div>}
                        {c.email?.trim() && <div className="small"><i className="bi bi-envelope text-muted me-1" />{c.email}</div>}
                      </div>
                    </div>
                  ))}
                  <div className="d-flex justify-content-between align-items-center mt-2 mb-1">
                    <button className="btn btn-sm btn-outline-secondary" disabled={grid.page <= 1} onClick={() => grid.load(grid.page - 1)}><i className="bi bi-chevron-left" /> Prev</button>
                    <span className="small text-muted">Page {grid.page} of {Math.max(1, Math.ceil(grid.total / PAGE_SIZE))}</span>
                    <button className="btn btn-sm btn-outline-secondary" disabled={grid.page >= Math.ceil(grid.total / PAGE_SIZE)} onClick={() => grid.load(grid.page + 1)}>Next <i className="bi bi-chevron-right" /></button>
                  </div>
                </>
              )}
            </div>
          </div>

          <div className="d-flex flex-wrap justify-content-between align-items-center mt-3 gap-2">
            <div className="d-flex flex-wrap gap-2">
              <button className="btn btn-sm btn-primary" onClick={() => navigate('/clients/add')}><i className="bi bi-person-plus" /> <span className="d-none d-sm-inline">New Person</span></button>
              <button className="btn btn-sm btn-outline-primary" onClick={() => navigate('/clients/add?type=Organisation')}><i className="bi bi-building-add" /> <span className="d-none d-sm-inline">New Org</span></button>
              <button className="btn btn-sm btn-outline-danger" onClick={archiveSelected} disabled={!selected}><i className="bi bi-archive" /> <span className="d-none d-sm-inline">Archive</span></button>
            </div>
            <Dropdown toggleClassName="btn btn-sm btn-outline-secondary dropdown-toggle" toggle={<><i className="bi bi-three-dots" /> <span className="d-none d-sm-inline">More</span></>}>
              <li><h6 className="dropdown-header">Reports</h6></li>
              <li><a className="dropdown-item disabled" href="#" aria-disabled="true" onClick={(e) => e.preventDefault()}><i className="bi bi-bar-chart me-2" />Graph</a></li>
              <li><a className="dropdown-item" href="#" onClick={(e) => { e.preventDefault(); exportCsv(); }}><i className="bi bi-download me-2" />Export</a></li>
              <li><hr className="dropdown-divider" /></li>
              <li><h6 className="dropdown-header">Communication</h6></li>
              <li><a className="dropdown-item disabled" href="#" aria-disabled="true" onClick={(e) => e.preventDefault()}><i className="bi bi-chat-dots me-2" />Send SMS</a></li>
              <li><a className="dropdown-item disabled" href="#" aria-disabled="true" onClick={(e) => e.preventDefault()}><i className="bi bi-envelope me-2" />Send Email</a></li>
              <li><hr className="dropdown-divider" /></li>
              <li><h6 className="dropdown-header">Verification</h6></li>
              <li><a className="dropdown-item disabled" href="#" aria-disabled="true" onClick={(e) => e.preventDefault()}><i className="bi bi-shield-check me-2" />Enterprise Verification</a></li>
              <li><a className="dropdown-item" href="#" onClick={(e) => { e.preventDefault(); setUnderConstruction('Under construction'); }}><i className="bi bi-patch-check me-2" />Patient Verification</a></li>
            </Dropdown>
          </div>
        </div>
      </div>

      {payerPickerOpen && (
        <ClientPicker title="Find Payer" onClose={() => setPayerPickerOpen(false)}
                      onSelect={(c) => { set({ payerClientId: c.id }); setPayerName(c.fullName); setPayerPickerOpen(false); }} />
      )}
      {underConstruction && (
        <Modal title="Patient Verification" size="sm" scrollable={false} onClose={() => setUnderConstruction(null)}
               footer={<button className="btn btn-primary btn-sm" onClick={() => setUnderConstruction(null)}>OK</button>}>
          <p className="mb-0 text-center">{underConstruction}</p>
        </Modal>
      )}
    </>
  );
}
