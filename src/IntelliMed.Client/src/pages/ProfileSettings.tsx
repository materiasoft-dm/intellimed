import { useState, type FormEvent } from 'react';
import { AuthApi } from '@/api/auth';
import { LookupApi } from '@/api/clients';
import { DayOfWeek, type ProviderGroupDto, type ProviderScheduleDayDto, type UpdateProfileRequest, type UserProfileDto } from '@/api/types.gen';
import { NumberInput, NumberSelect, TextArea, TextInput, TimeInput } from '@/components/Inputs';
import { LoadingState } from '@/components/States';
import { useToast } from '@/components/Toast';
import { useMountEffect, usePageTitle } from '@/lib/hooks';
import { FieldMessage, hasErrors, rules, validate, ValidationSummary } from '@/lib/validation';

type Form = UpdateProfileRequest & Record<string, unknown>;

export default function ProfileSettings() {
  usePageTitle('Profile Settings - IntelliMed');
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [profile, setProfile] = useState<UserProfileDto | null>(null);
  const [form, setForm] = useState<Form | null>(null);
  const [groups, setGroups] = useState<ProviderGroupDto[]>([]);
  const [saving, setSaving] = useState(false);
  const [tried, setTried] = useState(false);
  const [days, setDays] = useState<ProviderScheduleDayDto[]>([]);
  const [scheduleLoading, setScheduleLoading] = useState(true);
  const [scheduleSaving, setScheduleSaving] = useState(false);

  useMountEffect(async (alive) => {
    const [g, p, schedule] = await Promise.all([LookupApi.providerGroups(), AuthApi.getProfile(), AuthApi.getSchedule()]);
    if (!alive()) return;
    setGroups(g);
    // Monday first, whatever the DayOfWeek enum's Sunday-first storage order
    setDays([...schedule].sort((a, b) => ((a.dayOfWeek + 6) % 7) - ((b.dayOfWeek + 6) % 7)));
    setScheduleLoading(false);
    setProfile(p);
    if (p) {
      setForm({
        firstName: p.firstName, lastName: p.lastName, title: p.title, middleName: p.middleName, mobilePhone: p.mobilePhone,
        businessHoursPhone: p.businessHoursPhone, fax: p.fax, qualifications: p.qualifications, specialty: p.specialty,
        providerNumber: p.providerNumber, ahpraNumber: p.ahpraNumber, hpiiNumber: p.hpiiNumber, note: p.note,
        vocationallyRegistered: p.vocationallyRegistered, internalProvider: p.internalProvider, ePrescribingEnabled: p.ePrescribingEnabled,
        defaultAppointmentTimeslotMinutes: p.defaultAppointmentTimeslotMinutes, showCommandPalette: p.showCommandPalette,
        groupId: p.groupId, residentialAddress: p.residentialAddress, residentialSuburb: p.residentialSuburb,
        residentialPostcode: p.residentialPostcode, residentialState: p.residentialState, postalSameAsResidential: p.postalSameAsResidential,
        postalAddress: p.postalAddress, postalSuburb: p.postalSuburb, postalPostcode: p.postalPostcode, postalState: p.postalState,
      } as Form);
    }
    setLoading(false);
  });

  const check = (f: Form) => {
    const errors = validate(f, { firstName: [rules.required('FirstName')], lastName: [rules.required('LastName')] });
    if (f.defaultAppointmentTimeslotMinutes != null && (f.defaultAppointmentTimeslotMinutes < 1 || f.defaultAppointmentTimeslotMinutes > 240)) {
      errors.defaultAppointmentTimeslotMinutes = ['The field DefaultAppointmentTimeslotMinutes must be between 1 and 240.'];
    }
    return errors;
  };
  const errors = tried && form ? check(form) : {};
  const set = <K extends keyof Form>(key: K) => (value: Form[K]) => setForm((f) => (f ? { ...f, [key]: value } : f));

  const save = async (e: FormEvent) => {
    e.preventDefault();
    if (!form) return;
    setTried(true);
    if (hasErrors(check(form))) return;
    setSaving(true);
    try {
      const ok = await AuthApi.updateProfile(form);
      toast.show(ok ? 'Profile updated successfully.' : 'Failed to update profile.', !ok);
    } finally {
      setSaving(false);
    }
  };

  const setDay = (index: number, patch: Partial<ProviderScheduleDayDto>) =>
    setDays((all) => all.map((d, i) => (i === index ? { ...d, ...patch } : d)));

  const saveSchedule = async () => {
    setScheduleSaving(true);
    const ok = await AuthApi.setSchedule({ days });
    setScheduleSaving(false);
    toast.show(ok ? 'Schedule saved.' : 'Failed to save schedule.', !ok);
  };

  const text = (key: keyof Form, label: string, col: string, opts: { placeholder?: string; small?: boolean; disabled?: boolean } = {}) => (
    <div className={col}>
      <label className={`form-label ${opts.small ? 'small mb-0' : ''}`}>{label}</label>
      <TextInput className={`form-control ${opts.small ? 'form-control-sm' : ''}`} placeholder={opts.placeholder} disabled={opts.disabled}
                 value={form?.[key] as string | null} onChange={(v) => set(key)(v as never)} />
    </div>
  );
  const check2 = (key: keyof Form, id: string, label: string) => (
    <div className="form-check">
      <input className="form-check-input" type="checkbox" id={id} checked={!!form?.[key]} onChange={(e) => set(key)(e.target.checked as never)} />
      <label className="form-check-label" htmlFor={id}>{label}</label>
    </div>
  );

  return (
    <>
      <h4 className="mb-4">Profile Settings</h4>
      {loading ? (
        <div className="card"><div className="card-body"><LoadingState text="Loading profile..." compact={false} /></div></div>
      ) : !profile || !form ? (
        <div className="alert alert-danger">Failed to load your profile. Please try reloading the page.</div>
      ) : (
        <>
          <div className="row g-3">
            <div className="col-lg-10">
              <div className="card">
                <div className="card-header"><strong>Your Details</strong></div>
                <div className="card-body">
                  <form onSubmit={save} noValidate>
                    <ValidationSummary errors={errors} className="text-danger small mb-3" />
                    <div className="row g-3 mb-3">
                      <div className="col-md-4"><label className="form-label">Email</label><input className="form-control" value={profile.email} disabled /></div>
                      <div className="col-md-8">
                        <label className="form-label">Roles</label>
                        <div>{profile.roles.map((r) => <span key={r} className="badge bg-secondary me-1">{r}</span>)}</div>
                      </div>
                    </div>
                    <div className="row g-3 mb-3">
                      {text('title', 'Title', 'col-md-2', { placeholder: 'Dr, Mr, Ms...' })}
                      <div className="col-md-4">
                        <label className="form-label">First Name <span className="text-danger">*</span></label>
                        <input className="form-control" value={form.firstName} onChange={(e) => set('firstName')(e.target.value)} />
                        <FieldMessage messages={errors.firstName} />
                      </div>
                      {text('middleName', 'Middle', 'col-md-2')}
                      <div className="col-md-4">
                        <label className="form-label">Last Name <span className="text-danger">*</span></label>
                        <input className="form-control" value={form.lastName} onChange={(e) => set('lastName')(e.target.value)} />
                        <FieldMessage messages={errors.lastName} />
                      </div>
                    </div>
                    <div className="row g-3 mb-3">
                      {text('mobilePhone', 'Mobile Phone', 'col-md-3')}
                      {text('businessHoursPhone', 'B.H. (Business Hours)', 'col-md-3')}
                      {text('fax', 'Fax', 'col-md-3')}
                    </div>

                    <hr className="my-4" />
                    <h6 className="mb-3">Professional Details</h6>
                    <p className="text-muted small mb-3">Only relevant for clinicians — leave blank if not applicable.</p>
                    <div className="row g-3 mb-3">
                      <div className="col-md-6">
                        <label className="form-label">Group</label>
                        <NumberSelect className="form-select" value={form.groupId} onChange={set('groupId')}>
                          <option value="">-- Select --</option>
                          {groups.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
                        </NumberSelect>
                      </div>
                      {text('specialty', 'Speciality', 'col-md-6', { placeholder: 'e.g. General Practice' })}
                    </div>
                    <div className="row g-3 mb-3">
                      {text('qualifications', 'Qualifications', 'col-md-6', { placeholder: 'e.g. MBBS, FRACGP' })}
                      {text('providerNumber', 'Prescriber# (Provider Number)', 'col-md-6')}
                    </div>
                    <div className="row g-3 mb-3">
                      {text('ahpraNumber', 'AHPRA Number', 'col-md-6')}
                      {text('hpiiNumber', 'HPI-I Number', 'col-md-6')}
                    </div>
                    <div className="row g-3 mb-4">
                      <div className="col-md-4">{check2('vocationallyRegistered', 'chkVocReg', 'Vocationally Registered')}</div>
                      <div className="col-md-4">{check2('internalProvider', 'chkInternal', 'Internal Provider')}</div>
                      <div className="col-md-4">{check2('ePrescribingEnabled', 'chkEPrescribing', 'ePrescribing')}</div>
                    </div>
                    <div className="row g-3 mb-4">
                      <div className="col-md-4">
                        <label className="form-label">My Appointment Timeslot (minutes)</label>
                        <NumberInput className="form-control" nullable placeholder="Clinic default" value={form.defaultAppointmentTimeslotMinutes}
                                     onChange={set('defaultAppointmentTimeslotMinutes')} />
                        <div className="form-text">Overrides the clinic's default calendar slot size just for your bookings. Leave blank to use the clinic setting.</div>
                        <FieldMessage messages={errors.defaultAppointmentTimeslotMinutes} />
                      </div>
                    </div>

                    <hr className="my-4" />
                    <h6 className="mb-3">Preferences</h6>
                    <div className="row g-3 mb-4">
                      <div className="col-md-6">
                        {check2('showCommandPalette', 'chkCommandPalette', 'Show Command Palette (Ctrl+K)')}
                        <div className="form-text">Turns off the search button and the Ctrl+K shortcut across the app.</div>
                      </div>
                    </div>

                    <hr className="my-4" />
                    <h6 className="mb-3">Residential Address</h6>
                    <div className="row g-3 mb-2">{text('residentialAddress', 'Address', 'col-md-12', { small: true })}</div>
                    <div className="row g-3 mb-3">
                      {text('residentialSuburb', 'Suburb', 'col-md-6', { small: true })}
                      {text('residentialPostcode', 'Postcode', 'col-md-3', { small: true })}
                      {text('residentialState', 'State', 'col-md-3', { small: true })}
                    </div>
                    <h6 className="mb-2">Postal Address</h6>
                    <div className="mb-3">{check2('postalSameAsResidential', 'chkPostalSame', 'Same as Residential Address')}</div>
                    <div className="row g-3 mb-2">{text('postalAddress', 'Address', 'col-md-12', { small: true, disabled: form.postalSameAsResidential })}</div>
                    <div className="row g-3 mb-4">
                      {text('postalSuburb', 'Suburb', 'col-md-6', { small: true, disabled: form.postalSameAsResidential })}
                      {text('postalPostcode', 'Postcode', 'col-md-3', { small: true, disabled: form.postalSameAsResidential })}
                      {text('postalState', 'State', 'col-md-3', { small: true, disabled: form.postalSameAsResidential })}
                    </div>

                    <hr className="my-4" />
                    <div className="mb-3">
                      <label className="form-label">Note</label>
                      <TextArea className="form-control" rows={3} value={form.note} onChange={set('note')} />
                    </div>
                    <button type="submit" className="btn btn-primary" disabled={saving}>
                      {saving ? <><span className="spinner-border spinner-border-sm me-2" /><span>Saving...</span></> : <span><i className="bi bi-save me-1" />Save Changes</span>}
                    </button>
                  </form>
                </div>
              </div>
            </div>
          </div>

          <div className="row g-3 mt-1">
            <div className="col-lg-10">
              <div className="card">
                <div className="card-header"><strong>Weekly Schedule</strong></div>
                <div className="card-body">
                  <p className="text-muted small">Your usual working hours — used to flag bookings outside your normal hours on the appointment calendar.</p>
                  {scheduleLoading ? <LoadingState text="Loading schedule..." /> : (
                    <>
                      <table className="table table-sm align-middle mb-2">
                        <thead><tr><th style={{ width: 40 }} /><th>Day</th><th>Start</th><th>End</th></tr></thead>
                        <tbody>
                          {days.map((d, i) => (
                            <tr key={d.dayOfWeek} className={d.isActive ? '' : 'text-muted'}>
                              <td><input type="checkbox" className="form-check-input" aria-label={`Works on ${DayOfWeek[d.dayOfWeek]}`} checked={d.isActive}
                                         onChange={(e) => setDay(i, { isActive: e.target.checked })} /></td>
                              <td>{DayOfWeek[d.dayOfWeek]}</td>
                              <td><TimeInput className="form-control form-control-sm" disabled={!d.isActive} value={d.startTime} onChange={(v) => v && setDay(i, { startTime: v })} /></td>
                              <td><TimeInput className="form-control form-control-sm" disabled={!d.isActive} value={d.endTime} onChange={(v) => v && setDay(i, { endTime: v })} /></td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                      <button type="button" className="btn btn-primary btn-sm" disabled={scheduleSaving} onClick={saveSchedule}>
                        {scheduleSaving && <span className="spinner-border spinner-border-sm me-1" />} Save Schedule
                      </button>
                    </>
                  )}
                </div>
              </div>
            </div>
          </div>
        </>
      )}
    </>
  );
}
