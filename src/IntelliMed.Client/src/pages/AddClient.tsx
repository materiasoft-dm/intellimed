import { useState, type FormEvent, type ReactNode } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router';
import { PractitionerApi } from '@/api/appointments';
import { ClientAddressApi, ClientApi, LookupApi } from '@/api/clients';
import {
  AccountTypeEnum, AtsiStatusEnum, ClientAddressType, ClientTypeEnum, defaultCreateClientDto, defaultUpdateClientDto, DobAccuracyEnum,
  GenderEnum, MaritalStatusEnum, type ClientAddressDto, type ClientDto, type ClientProviderAccessDto, type CreateClientDto,
  type HealthFundDto, type PractitionerDto, type UpdateClientDto,
} from '@/api/types.gen';
import { ClientPicker } from '@/components/ClientPicker';
import { ClientSafetyBanner } from '@/components/ClientSafetyBanner';
import { DateInput, NumberInput, NumberSelect, TextArea, TextInput } from '@/components/Inputs';
import { Modal } from '@/components/Modal';
import { LoadingState } from '@/components/States';
import { addDays, ageFrom, formatDate, startOfDay, toIsoLocal } from '@/lib/dates';
import { useMountEffect, usePageTitle } from '@/lib/hooks';
import { AllergiesTab } from './client/AllergiesTab';
import { ClaimsTab, FamilyTab } from './client/ClaimsAndFamilyTabs';
import { HealthIssuesTab } from './client/HealthIssuesTab';
import { EHealthTab, OccupationsTab, UdfTab } from './client/OtherTabs';
import { PrescriptionsTab } from './client/PrescriptionsTab';
import { PatientHistoryTab, ProgressNotesTab } from './client/ProgressNotesTab';
import { ReferralsTab } from './client/ReferralsTab';

const TABS = ['Details', 'Referrals', 'Allergies', 'Health Issues', 'Prescriptions', 'Progress Notes', 'WC/TAC', 'Family', 'Occupations', 'eHealth', 'UDF', 'Patient History'] as const;
type Tab = typeof TABS[number];

/** Strings the DTO declares non-nullable: an empty box is "" for these, null for the rest. */
const REQUIRED_STRINGS = new Set<keyof UpdateClientDto>(['firstName', 'lastName', 'medicareNumber', 'address', 'suburb', 'state', 'postcode', 'email', 'phone']);

function toFormModel(dto: ClientDto): UpdateClientDto {
  const model = defaultUpdateClientDto() as unknown as Record<string, unknown>;
  for (const key of Object.keys(model)) {
    if (key in dto) model[key] = (dto as unknown as Record<string, unknown>)[key];
  }
  // the creator's own access is implicit; only the explicitly added providers are sent back
  model.providerIds = dto.providerAccess.filter((a) => !a.isCreator).map((a) => a.practitionerId);
  return model as unknown as UpdateClientDto;
}

function toCreateDto(m: UpdateClientDto): CreateClientDto {
  const dto = defaultCreateClientDto() as unknown as Record<string, unknown>;
  for (const key of Object.keys(dto)) {
    if (key in m) dto[key] = (m as unknown as Record<string, unknown>)[key];
  }
  // an organisation has no date of birth
  dto.dateOfBirth = m.type === ClientTypeEnum.Person ? m.dateOfBirth : null;
  return dto as unknown as CreateClientDto;
}

function Card({ title, header, children, col }: { title?: string; header?: ReactNode; children: ReactNode; col: string }) {
  return (
    <div className={col}>
      <div className="card h-100">
        <div className="card-header py-1">{header ?? <strong>{title}</strong>}</div>
        <div className="card-body py-1">{children}</div>
      </div>
    </div>
  );
}

export default function AddClient() {
  const params = useParams();
  const id = params.id ? Number(params.id) : null;
  const [query] = useSearchParams();
  const navigate = useNavigate();
  usePageTitle(`${id != null ? 'Edit Client' : 'Add Client'} - IntelliMed`);

  const [loading, setLoading] = useState(true);
  const [m, setM] = useState<UpdateClientDto>(defaultUpdateClientDto);
  const [createdAt, setCreatedAt] = useState<string | null>(null);
  const [providerAccess, setProviderAccess] = useState<ClientProviderAccessDto[]>([]);
  const [addresses, setAddresses] = useState<Partial<Record<ClientAddressType, ClientAddressDto>>>({});
  const [addressTab, setAddressTab] = useState<'Residential' | 'Postal' | 'Other'>('Residential');
  const [healthFunds, setHealthFunds] = useState<HealthFundDto[]>([]);
  const [practitioners, setPractitioners] = useState<PractitionerDto[]>([]);
  const [tab, setTab] = useState<Tab>('Details');
  const [saving, setSaving] = useState(false);
  const [status, setStatus] = useState<{ message: string; error: boolean } | null>(null);
  const [underConstruction, setUnderConstruction] = useState<string | null>(null);
  const [payerPickerOpen, setPayerPickerOpen] = useState(false);
  const [providerPickerOpen, setProviderPickerOpen] = useState(false);

  const loadAddresses = async (clientId: number) => {
    const list = await ClientAddressApi.list(clientId);
    setAddresses(Object.fromEntries(list.map((a) => [a.addressType, a])));
  };

  useMountEffect(async (alive) => {
    const [funds, pracs] = await Promise.all([LookupApi.healthFunds(), PractitionerApi.active()]);
    if (!alive()) return;
    setHealthFunds(funds);
    setPractitioners(pracs);
    if (id != null) {
      const client = await ClientApi.getById(id);
      if (client && alive()) {
        setM(toFormModel(client));
        setCreatedAt(client.createdAt);
        setProviderAccess(client.providerAccess);
        await loadAddresses(id);
      }
    } else {
      const type = query.get('type')?.toLowerCase() === 'organisation' ? ClientTypeEnum.Organisation : ClientTypeEnum.Person;
      setM({
        ...defaultUpdateClientDto(), type,
        dateOfBirth: type === ClientTypeEnum.Person ? toIsoLocal(startOfDay(addDays(new Date(new Date().setFullYear(new Date().getFullYear() - 30)), 0))) : null,
      });
    }
    if (alive()) setLoading(false);
  });

  const set = (patch: Partial<UpdateClientDto>) => setM((x) => ({ ...x, ...patch }));

  const save = async (e: FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setStatus(null);
    try {
      if (id != null) {
        const ok = await ClientApi.update(id, m);
        setStatus(ok ? { message: 'Saved.', error: false } : { message: 'Failed to save.', error: true });
      } else {
        const newId = await ClientApi.create(toCreateDto(m));
        if (newId != null) navigate(`/clients/edit/${newId}`);
        else setStatus({ message: 'Failed to create client.', error: true });
      }
    } finally {
      setSaving(false);
    }
  };

  // Postal / Other addresses are separate records, saved on their own
  const addrType = addressTab === 'Postal' ? ClientAddressType.Postal : ClientAddressType.Other;
  const addr: ClientAddressDto = addresses[addrType] ?? {
    id: 0, clientId: id ?? 0, addressType: addrType, addressLine1: '', addressLine2: null, suburb: '', postcode: '', state: '',
    addressSubType: 'Not Specified', community: null, sendToMedicare: false,
  };
  const setAddr = (patch: Partial<ClientAddressDto>) => setAddresses((all) => ({ ...all, [addrType]: { ...addr, ...patch } }));
  const saveAddress = async () => {
    if (id == null) return;
    const body = {
      addressType: addr.addressType, addressLine1: addr.addressLine1, addressLine2: addr.addressLine2, suburb: addr.suburb, postcode: addr.postcode,
      state: addr.state, addressSubType: addr.addressSubType, community: addr.community, sendToMedicare: addr.sendToMedicare,
    };
    let ok: boolean;
    if (addr.id === 0) {
      ok = await ClientAddressApi.create({ ...body, clientId: id });
      if (ok) await loadAddresses(id);
    } else {
      ok = await ClientAddressApi.update(id, addr.id, body);
    }
    setStatus(ok ? { message: 'Address saved.', error: false } : { message: 'Failed to save address.', error: true });
  };

  const selectProvider = (p: PractitionerDto) => {
    if (!m.providerIds.includes(p.id) && !providerAccess.some((a) => a.isCreator && a.practitionerId === p.id)) {
      set({ providerIds: [...m.providerIds, p.id] });
      setProviderAccess((all) => [...all, { practitionerId: p.id, practitionerName: [p.title, p.firstName, p.lastName].filter(Boolean).join(' '), isCreator: false }]);
    }
    setProviderPickerOpen(false);
  };
  const removeProvider = (practitionerId: number) => {
    set({ providerIds: m.providerIds.filter((x) => x !== practitionerId) });
    setProviderAccess((all) => all.filter((a) => a.isCreator || a.practitionerId !== practitionerId));
  };

  // ---- field helpers: one line per field, same label → input structure as the Blazor form ----
  const T = (key: keyof UpdateClientDto, label: string, col = 'col-6', opts: { disabled?: boolean; maxLength?: number } = {}) => (
    <div className={col}>
      <label className="form-label small mb-0">{label}</label>
      <TextInput className="form-control form-control-sm" nullable={!REQUIRED_STRINGS.has(key)} disabled={opts.disabled} maxLength={opts.maxLength}
                 value={m[key] as string | null} onChange={(v) => set({ [key]: REQUIRED_STRINGS.has(key) ? v ?? '' : v } as Partial<UpdateClientDto>)} />
    </div>
  );
  const D = (key: keyof UpdateClientDto, label: string, col = 'col-3') => (
    <div className={col}>
      <label className="form-label small mb-0">{label}</label>
      <DateInput className="form-control form-control-sm" value={m[key] as string | null} onChange={(v) => set({ [key]: v } as Partial<UpdateClientDto>)} />
    </div>
  );
  const C = (key: keyof UpdateClientDto, id: string, label: string) => (
    <div className="form-check">
      <input className="form-check-input" type="checkbox" id={id} checked={!!m[key]} onChange={(e) => set({ [key]: e.target.checked } as Partial<UpdateClientDto>)} />
      <label className="form-check-label small" htmlFor={id}>{label}</label>
    </div>
  );
  const A = (key: keyof UpdateClientDto, label: string, col = 'col-6') => (
    <div className={col}>
      <label className="form-label small mb-0">{label}</label>
      <TextArea className="form-control form-control-sm" rows={2} value={m[key] as string | null} onChange={(v) => set({ [key]: v } as Partial<UpdateClientDto>)} />
    </div>
  );

  if (loading) return <div className="card"><div className="card-body"><LoadingState text="Loading client..." /></div></div>;

  const isPerson = m.type === ClientTypeEnum.Person;
  const age = ageFrom(m.dateOfBirth);

  return (
    <div className="client-edit-page">
      <div className="d-flex align-items-center justify-content-between mb-1">
        <h5 className="mb-0 small">🩺 {id != null ? `Edit Client — ${m.firstName} ${m.lastName}` : 'New Client'}</h5>
        {status && <span className={`small ${status.error ? 'text-danger' : 'text-success'}`}>{status.message}</span>}
      </div>

      <div className="d-flex justify-content-between align-items-start mb-2">
        <div className="flex-grow-1 me-2">
          <ClientSafetyBanner deceased={m.deceased} warnings={m.warnings} interpreterRequired={m.interpreterRequired} interpreterLanguage={m.interpreterLanguage}
                              age={age} nextOfKinName={m.nextOfKinName} nextOfKinPhone={m.nextOfKinPhone} />
        </div>
        <button type="button" className="btn btn-sm btn-outline-secondary flex-shrink-0" onClick={() => navigate('/clients')}>Close</button>
      </div>

      <div className="card mb-1">
        {/* the twelve sections wrap onto a second line on narrow screens instead of scrolling sideways */}
        <div className="card-header client-tabs-bar">
          <ul className="nav client-tabs">
            {TABS.map((t) => (
              <li key={t} className="nav-item">
                <a className={`nav-link ${tab === t ? 'active' : ''}`} href="#" aria-current={tab === t ? 'page' : undefined} onClick={(e) => { e.preventDefault(); setTab(t); }}>{t}</a>
              </li>
            ))}
          </ul>
        </div>
        <div className="card-body py-2">
          {tab === 'Details' ? (
            <form onSubmit={save} noValidate>
              <div className="row g-1 mb-1">
                <Card col="col-lg-6" title="Personal">
                  <div className="row g-1">
                    <div className="col-6">
                      <label className="form-label small mb-0">Type</label>
                      <NumberSelect enumType={ClientTypeEnum} className="form-select form-select-sm" nullable={false} value={m.type} onChange={(v) => set({ type: (v ?? 0) as ClientTypeEnum })}>
                        <option value={ClientTypeEnum.Person}>Person</option>
                        <option value={ClientTypeEnum.Organisation}>Organisation</option>
                      </NumberSelect>
                    </div>
                    <div className="col-6" />
                    {T('lastName', isPerson ? 'Surname' : 'Family/Org')}
                    {T('firstName', 'Given')}
                    {T('middleName', 'Middle')}
                    {T('preferredName', 'Preferred')}
                    {T('maidenName', 'Maiden')}
                    {T('title', 'Title')}
                    <div className="col-6">
                      <label className="form-label small mb-0">Gender</label>
                      <NumberSelect enumType={GenderEnum} className="form-select form-select-sm" nullable={false} value={m.gender ?? GenderEnum.Unspecified} onChange={(v) => set({ gender: v as GenderEnum })}>
                        <option value={GenderEnum.Unspecified}>Unspecified</option>
                        <option value={GenderEnum.Male}>Male</option>
                        <option value={GenderEnum.Female}>Female</option>
                        <option value={GenderEnum.Other}>Other</option>
                      </NumberSelect>
                    </div>
                    {/* an organisation has no date of birth, so the D.O.B block only applies to a person */}
                    {isPerson && (
                      <>
                        {D('dateOfBirth', 'D.O.B', 'col-6')}
                        <div className="col-6">
                          <label className="form-label small mb-0">Accuracy</label>
                          <NumberSelect enumType={DobAccuracyEnum} className="form-select form-select-sm" nullable={false} value={m.dobAccuracy} onChange={(v) => set({ dobAccuracy: (v ?? 0) as DobAccuracyEnum })}>
                            <option value={DobAccuracyEnum.Day}>Day - Accurate</option>
                            <option value={DobAccuracyEnum.Month}>Month - Accurate</option>
                            <option value={DobAccuracyEnum.Year}>Year - Accurate</option>
                            <option value={DobAccuracyEnum.Estimated}>Estimated</option>
                          </NumberSelect>
                        </div>
                        <div className="col-6">
                          <label className="form-label small mb-0">Age</label>
                          <input className="form-control form-control-sm" value={age ?? ''} disabled />
                        </div>
                      </>
                    )}
                    {T('placeOfBirth', 'P.O.B')}
                    <div className="col-6 d-flex align-items-end">{C('interpreterRequired', 'chkInterpreter', 'Interpreter?')}</div>
                    {T('interpreterLanguage', 'Language', 'col-6', { disabled: !m.interpreterRequired })}
                    <div className="col-6">
                      <label className="form-label small mb-0">Marital Status</label>
                      <NumberSelect enumType={MaritalStatusEnum} className="form-select form-select-sm" value={m.maritalStatus} onChange={(v) => set({ maritalStatus: v as MaritalStatusEnum | null })}>
                        <option value="">-</option>
                        <option value={MaritalStatusEnum.Single}>Single</option>
                        <option value={MaritalStatusEnum.Married}>Married</option>
                        <option value={MaritalStatusEnum.DeFacto}>De Facto</option>
                        <option value={MaritalStatusEnum.Divorced}>Divorced</option>
                        <option value={MaritalStatusEnum.Widowed}>Widowed</option>
                        <option value={MaritalStatusEnum.Separated}>Separated</option>
                        <option value={MaritalStatusEnum.Unknown}>Unknown</option>
                      </NumberSelect>
                    </div>
                    {T('ethnicity', 'Ethnicity')}
                  </div>
                </Card>

                <Card col="col-lg-6" title="Entitlement">
                  <div className="row g-1">
                    {T('medicareNumber', 'Medicare#')}
                    {T('medicarePosition', 'Ref', 'col-3')}
                    {D('medicareExpiryDate', 'EXP')}
                    {T('dvaNumber', 'Veteran#', 'col-9')}
                    {D('dvaExpiryDate', 'EXP')}
                    {T('pensionNumber', 'Pension/HCC', 'col-9')}
                    {D('pensionExpiryDate', 'EXP')}
                    {T('entitlementStatus', 'Status')}
                    {T('safetyNetNumber', 'Safety Net')}
                    <div className="col-6">
                      <label className="form-label small mb-0">ATSI</label>
                      <NumberSelect enumType={AtsiStatusEnum} className="form-select form-select-sm" nullable={false} value={m.atsiStatus} onChange={(v) => set({ atsiStatus: (v ?? 0) as AtsiStatusEnum })}>
                        <option value={AtsiStatusEnum.NotAsked}>Not Asked</option>
                        <option value={AtsiStatusEnum.AboriginalOnly}>Aboriginal</option>
                        <option value={AtsiStatusEnum.TorresStraitIslanderOnly}>Torres Strait Islander</option>
                        <option value={AtsiStatusEnum.Both}>Both</option>
                        <option value={AtsiStatusEnum.NeitherAboriginalNorTorresStraitIslander}>Neither</option>
                      </NumberSelect>
                    </div>
                    <div className="col-6 d-flex flex-column justify-content-end">
                      {C('medicareIncentiveEligible', 'chkIncentive', 'Medicare+ Incentive')}
                      {C('ctgCoPaymentRelief', 'chkCtg', 'CTG CoPayment Relief')}
                    </div>
                  </div>
                </Card>
              </div>

              <div className="row g-1 mb-1">
                <Card col="col-lg-6" header={
                  <ul className="nav nav-tabs card-header-tabs">
                    {(['Residential', 'Postal', 'Other'] as const).map((t) => (
                      <li key={t} className="nav-item">
                        <a className={`nav-link ${addressTab === t ? 'active' : ''}`} href="#" onClick={(e) => { e.preventDefault(); setAddressTab(t); }}>{t === 'Other' ? 'Other Addresses' : t}</a>
                      </li>
                    ))}
                  </ul>
                }>
                  {addressTab === 'Residential' ? (
                    <div className="row g-1">
                      {T('address', 'Address', 'col-12')}
                      {T('suburb', 'Suburb')}
                      {T('postcode', 'Postcode', 'col-3')}
                      {T('state', 'State', 'col-3')}
                    </div>
                  ) : (
                    <div className="row g-1">
                      <div className="col-12"><label className="form-label small mb-0">Address</label><input className="form-control form-control-sm" value={addr.addressLine1} onChange={(e) => setAddr({ addressLine1: e.target.value })} /></div>
                      <div className="col-6"><label className="form-label small mb-0">Suburb</label><input className="form-control form-control-sm" value={addr.suburb} onChange={(e) => setAddr({ suburb: e.target.value })} /></div>
                      <div className="col-3"><label className="form-label small mb-0">Postcode</label><input className="form-control form-control-sm" value={addr.postcode} onChange={(e) => setAddr({ postcode: e.target.value })} /></div>
                      <div className="col-3"><label className="form-label small mb-0">State</label><input className="form-control form-control-sm" value={addr.state} onChange={(e) => setAddr({ state: e.target.value })} /></div>
                      <div className="col-6"><label className="form-label small mb-0">Type</label><input className="form-control form-control-sm" value={addr.addressSubType} onChange={(e) => setAddr({ addressSubType: e.target.value })} /></div>
                      <div className="col-6"><label className="form-label small mb-0">Community</label><TextInput className="form-control form-control-sm" value={addr.community} onChange={(v) => setAddr({ community: v })} /></div>
                      <div className="col-6 d-flex align-items-end">
                        <div className="form-check">
                          <input type="checkbox" className="form-check-input" id="chkSendMedicare" checked={addr.sendToMedicare} onChange={(e) => setAddr({ sendToMedicare: e.target.checked })} />
                          <label className="form-check-label small" htmlFor="chkSendMedicare">Send To Medicare</label>
                        </div>
                      </div>
                      <div className="col-6 text-end align-self-end">
                        <button type="button" className="btn btn-sm btn-outline-primary" onClick={saveAddress} disabled={id == null}>Save Address</button>
                      </div>
                      {id == null && <div className="col-12"><span className="text-muted small">Save the client first to add this address.</span></div>}
                    </div>
                  )}
                </Card>

                <Card col="col-lg-6" title="Contact Details">
                  <div className="row g-1">
                    {T('phone', 'Home Phone')}
                    {T('businessHoursPhone', 'B.H')}
                    {T('mobilePhone', 'Mobile Phone')}
                    {T('faxNumber', 'Fax')}
                    {T('email', 'Email', 'col-12')}
                    <div className="col-12 d-flex flex-wrap gap-3">
                      {C('acceptSms', 'chkSms', 'Accept SMS')}
                      {C('acceptEmail', 'chkEmail', 'Accept Email')}
                      {C('acceptOnlineAppointments', 'chkOnlineAppt', 'Accept Online Appt.')}
                    </div>
                    {A('notes', 'Note')}
                    {A('warnings', 'Warnings')}
                    {T('nextOfKinName', 'Next of Kin')}
                    {T('nextOfKinPhone', 'N.O.K Phone')}
                    {T('emergencyContactName', 'Emergency', 'col-6', { disabled: m.sameAsNextOfKin })}
                    {T('emergencyContactPhone', 'Emergency Phone', 'col-6', { disabled: m.sameAsNextOfKin })}
                    <div className="col-6 d-flex align-items-end">{C('sameAsNextOfKin', 'chkSameAsNok', 'Same as Next of Kin')}</div>
                    <div className="col-6 d-flex align-items-end">{C('acceptSmsMarketing', 'chkSmsMkt', 'Accept SMS Marketing')}</div>
                  </div>
                </Card>
              </div>

              <div className="row g-1 mb-1">
                <Card col="col-lg-4" title="Health Fund">
                  <div className="row g-1">
                    <div className="col-12">
                      <label className="form-label small mb-0">Fund Id</label>
                      <NumberSelect className="form-select form-select-sm" value={m.healthFundId} onChange={(v) => set({ healthFundId: v })}>
                        <option value="">-- None --</option>
                        {healthFunds.map((f) => <option key={f.id} value={f.id}>{f.code} - {f.name}</option>)}
                      </NumberSelect>
                    </div>
                    {T('healthFundNumber', 'Member#')}
                    {T('healthFundRef', 'Ref')}
                    {T('healthFundAliasFamily', 'Alias Family')}
                    {T('healthFundAliasFirst', 'Alias First')}
                    {D('healthFundJoinDate', 'Joined', 'col-6')}
                  </div>
                </Card>

                <Card col="col-lg-4" title="Account">
                  <div className="row g-1">
                    <div className="col-6">
                      <label className="form-label small mb-0">Acc. Type</label>
                      <NumberSelect enumType={AccountTypeEnum} className="form-select form-select-sm" nullable={false} value={m.accountType} onChange={(v) => set({ accountType: (v ?? 0) as AccountTypeEnum })}>
                        <option value={AccountTypeEnum.PrivatePatient}>Private Patient</option>
                        <option value={AccountTypeEnum.Concession}>Concession</option>
                        <option value={AccountTypeEnum.Pensioner}>Pensioner</option>
                        <option value={AccountTypeEnum.Veteran}>Veteran</option>
                        <option value={AccountTypeEnum.WorkCover}>WorkCover</option>
                        <option value={AccountTypeEnum.Tac}>TAC</option>
                        <option value={AccountTypeEnum.BulkBill}>Bulk Bill</option>
                        <option value={AccountTypeEnum.Other}>Other</option>
                        <option value={AccountTypeEnum.Imc}>IMC</option>
                        <option value={AccountTypeEnum.Ovs}>OVS</option>
                        <option value={AccountTypeEnum.ChildImm}>Immunisation</option>
                      </NumberSelect>
                    </div>
                    {T('feeRateCode', 'Fee Rate')}
                    <div className="col-12">
                      <label className="form-label small mb-0">Payer</label>
                      <div className="input-group input-group-sm">
                        <input className="form-control form-control-sm" value={m.payerName ?? ''} readOnly placeholder="No payer selected" />
                        <button type="button" className="btn btn-outline-secondary" onClick={() => setPayerPickerOpen(true)}>Find</button>
                        {m.payerClientId != null && <button type="button" className="btn btn-outline-secondary" aria-label="Clear payer" onClick={() => set({ payerClientId: null, payerName: null })}>x</button>}
                      </div>
                    </div>
                    {T('accountName', 'Acc. Name', 'col-12')}
                    {T('accountBsb', 'BSB')}
                    {T('accountNumber', 'Acc#')}
                    <div className="col-12">{C('useMedicareRegisteredBankAccount', 'chkBankMedicare', 'Use bank account registered with Medicare')}</div>
                  </div>
                </Card>

                <Card col="col-lg-4" title="File">
                  <div className="row g-1">
                    {T('fileNumber', 'File#')}
                    {T('urNumber', 'UR No')}
                    <div className="col-6"><label className="form-label small mb-0">Created</label><input className="form-control form-control-sm" value={createdAt ? formatDate(createdAt, 'dd/MM/yyyy') : '-'} disabled /></div>
                    <div className="col-6 d-flex align-items-end">{C('deceased', 'chkDeceased', 'Deceased')}</div>
                    <div className="col-6">
                      <label className="form-label small mb-0">Provider (Practitioner ID)</label>
                      <NumberInput nullable className="form-control form-control-sm" value={m.providerId} onChange={(v) => set({ providerId: v })} />
                    </div>
                    {D('lastSeenDate', 'Last Seen Date', 'col-6')}
                  </div>
                </Card>
              </div>

              <div className="row g-1 mb-1">
                <div className="col-12">
                  <div className="card">
                    <div className="card-header py-1 d-flex justify-content-between align-items-center">
                      <strong>Providers with Access</strong>
                      <button type="button" className="btn btn-sm btn-outline-primary" onClick={() => setProviderPickerOpen(true)}>+ Add Provider</button>
                    </div>
                    <div className="card-body py-2">
                      {m.visibleToAllProviders ? (
                        <>
                          <span className="badge bg-info text-dark me-2">All Providers</span>
                          <button type="button" className="btn btn-sm btn-link text-danger py-0 px-0" onClick={() => set({ visibleToAllProviders: false })}>Remove</button>
                          <div className="small text-muted mt-1">Visible to everyone. Remove to restrict to specific providers instead.</div>
                        </>
                      ) : providerAccess.length === 0 ? (
                        <span className="text-muted small">No providers assigned yet — this patient won't be visible to anyone until at least one is added.</span>
                      ) : (
                        <ul className="list-group list-group-flush">
                          {providerAccess.map((a) => (
                            <li key={a.practitionerId} className="list-group-item d-flex justify-content-between align-items-center py-1 px-0">
                              <span>{a.practitionerName}{a.isCreator && <span className="badge bg-secondary ms-1">Creator</span>}</span>
                              {!a.isCreator && <button type="button" className="btn btn-sm btn-link text-danger py-0" onClick={() => removeProvider(a.practitionerId)}>Remove</button>}
                            </li>
                          ))}
                        </ul>
                      )}
                    </div>
                  </div>
                </div>
              </div>

              <div className="d-flex justify-content-between align-items-center">
                <div className="d-flex gap-1">
                  <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => setUnderConstruction('Patient Verification')}>Patient Verification</button>
                  <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => setUnderConstruction('Print Label')}>Print Label</button>
                </div>
                <button type="submit" className="btn btn-sm btn-primary" disabled={saving}>
                  {saving && <span className="spinner-border spinner-border-sm me-1" />}💾 Save
                </button>
              </div>
            </form>
          ) : id == null ? (
            <div className="text-muted small">Save the client first to manage {tab}.</div>
          ) : tab === 'Referrals' ? <ReferralsTab clientId={id} />
            : tab === 'Health Issues' ? <HealthIssuesTab clientId={id} />
              : tab === 'Allergies' ? <AllergiesTab clientId={id} />
                : tab === 'Prescriptions' ? <PrescriptionsTab clientId={id} />
                  : tab === 'Progress Notes' ? <ProgressNotesTab clientId={id} />
                    : tab === 'WC/TAC' ? <ClaimsTab clientId={id} />
                      : tab === 'Family' ? <FamilyTab clientId={id} />
                        : tab === 'Occupations' ? <OccupationsTab clientId={id} />
                          : tab === 'eHealth' ? <EHealthTab model={m} onChange={set} onUnderConstruction={setUnderConstruction} />
                            : tab === 'UDF' ? <UdfTab clientId={id} />
                              : <PatientHistoryTab clientId={id} />}
        </div>
      </div>

      {underConstruction && (
        <Modal title={underConstruction} size="sm" scrollable={false} onClose={() => setUnderConstruction(null)}
               footer={<button type="button" className="btn btn-sm btn-primary" onClick={() => setUnderConstruction(null)}>OK</button>}>
          <div className="small text-muted">This feature is under construction and will be available in a future update.</div>
        </Modal>
      )}
      {payerPickerOpen && (
        <ClientPicker title="Find Payer" onClose={() => setPayerPickerOpen(false)}
                      onSelect={(c) => { set({ payerClientId: c.id, payerName: c.fullName }); setPayerPickerOpen(false); }} />
      )}
      {providerPickerOpen && (
        <Modal title="Add Provider" scrollable={false} onClose={() => setProviderPickerOpen(false)}
               footer={<button type="button" className="btn btn-sm btn-secondary" onClick={() => setProviderPickerOpen(false)}>Cancel</button>}>
          <div style={{ maxHeight: 320, overflowY: 'auto' }}>
            <ul className="list-group">
              <li className="list-group-item list-group-item-action" style={{ cursor: 'pointer' }} onClick={() => { set({ visibleToAllProviders: true }); setProviderPickerOpen(false); }}>
                <strong>All Providers</strong>
                <div className="small text-muted">Visible to everyone, not just specific providers.</div>
              </li>
              {[...practitioners].sort((a, b) => a.lastName.localeCompare(b.lastName)).map((p) => (
                <li key={p.id} className="list-group-item list-group-item-action" style={{ cursor: 'pointer' }} onClick={() => selectProvider(p)}>
                  {[p.title, p.firstName, p.lastName].filter(Boolean).join(' ')}
                </li>
              ))}
            </ul>
          </div>
        </Modal>
      )}
    </div>
  );
}
