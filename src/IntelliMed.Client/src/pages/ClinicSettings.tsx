import { useState, type FormEvent } from 'react';
import { BillingItemApi, FeeScheduleApi, InvoiceApi } from '@/api/billing';
import { ClinicSettingsApi } from '@/api/settings';
import {
  AccountTypeEnum, type AccountTypeFeeScheduleMappingDto, type BillingItemSyncResultDto, type FeeScheduleDto,
  type UpdateClinicSettingsRequest,
} from '@/api/types.gen';
import { NumberInput, TextInput } from '@/components/Inputs';
import { Modal } from '@/components/Modal';
import { LoadingState } from '@/components/States';
import { useToast } from '@/components/Toast';
import { formatUtc } from '@/lib/dates';
import { useMountEffect, usePageTitle } from '@/lib/hooks';
import { FieldMessage, hasErrors, rules, validate, ValidationSummary } from '@/lib/validation';

function accountTypeLabel(type: AccountTypeEnum): string {
  switch (type) {
    case AccountTypeEnum.BulkBill: return 'Bulk Bill';
    case AccountTypeEnum.PrivatePatient: return 'Private Patient';
    case AccountTypeEnum.Veteran: return 'DVA (Veteran)';
    case AccountTypeEnum.WorkCover: return 'WorkCover';
    case AccountTypeEnum.Tac: return 'TAC';
    case AccountTypeEnum.Imc: return 'IMC';
    case AccountTypeEnum.Ovs: return 'OVS';
    case AccountTypeEnum.ChildImm: return 'Immunisation';
    default: return AccountTypeEnum[type];
  }
}

type Form = UpdateClinicSettingsRequest & Record<string, unknown>;

export default function ClinicSettings() {
  usePageTitle('Clinic Settings - IntelliMed');
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [forbidden, setForbidden] = useState(false);
  const [form, setForm] = useState<Form | null>(null);
  const [saving, setSaving] = useState(false);
  const [tried, setTried] = useState(false);
  const [syncing, setSyncing] = useState(false);
  const [lastSync, setLastSync] = useState<BillingItemSyncResultDto | null>(null);
  const [mappings, setMappings] = useState<AccountTypeFeeScheduleMappingDto[]>([]);
  const [feeSchedules, setFeeSchedules] = useState<FeeScheduleDto[]>([]);
  const [testEmail, setTestEmail] = useState<{ toEmail: string; message: string } | null>(null);
  const [sendingTest, setSendingTest] = useState(false);

  useMountEffect(async (alive) => {
    const s = await ClinicSettingsApi.get();
    if (!alive()) return;
    if (!s) {
      setForbidden(true);
    } else {
      setForm({
        practiceName: s.practiceName, abn: s.abn, phone: s.phone, fax: s.fax, email: s.email, website: s.website,
        address: s.address, suburb: s.suburb, postcode: s.postcode, state: s.state, minimumTimeslotMinutes: s.minimumTimeslotMinutes,
        smtpEnabled: s.smtpEnabled, smtpHost: s.smtpHost, smtpPort: s.smtpPort, smtpUsername: s.smtpUsername,
        smtpPassword: s.smtpPassword, smtpFromEmail: s.smtpFromEmail, smtpFromName: s.smtpFromName, smtpUseSsl: s.smtpUseSsl,
      } as Form);
      const [schedules, maps] = await Promise.all([FeeScheduleApi.active(), InvoiceApi.feeScheduleMappings()]);
      if (!alive()) return;
      setFeeSchedules(schedules);
      setMappings(maps);
    }
    setLoading(false);
  });

  const spec = {
    practiceName: [rules.required('PracticeName')],
    minimumTimeslotMinutes: [rules.range('MinimumTimeslotMinutes', 1, 240)],
    smtpPort: [rules.range('SmtpPort', 1, 65535)],
  };
  const errors = tried && form ? validate(form, spec) : {};
  const set = <K extends keyof Form>(key: K) => (value: Form[K]) => setForm((f) => (f ? { ...f, [key]: value } : f));

  const save = async (e: FormEvent) => {
    e.preventDefault();
    if (!form) return;
    setTried(true);
    if (hasErrors(validate(form, spec))) return;
    setSaving(true);
    try {
      const ok = await ClinicSettingsApi.update(form);
      toast.show(ok ? 'Clinic settings updated successfully.' : 'Failed to update clinic settings.', !ok);
    } finally {
      setSaving(false);
    }
  };

  const syncBillingItems = async () => {
    setSyncing(true);
    try {
      const result = await BillingItemApi.sync();
      if (result) {
        setLastSync(result);
        toast.success(`Billing items updated: ${result.added} added, ${result.updated} updated, ${result.deactivated} deactivated.`);
      } else {
        toast.error('Failed to update billing items.');
      }
    } finally {
      setSyncing(false);
    }
  };

  const saveMapping = async (mapping: AccountTypeFeeScheduleMappingDto, value: string) => {
    const scheduleId = value ? Number(value) : null;
    setMappings((all) => all.map((m) => (m.accountType === mapping.accountType ? { ...m, feeScheduleId: scheduleId } : m)));
    const ok = await InvoiceApi.saveFeeScheduleMapping({ accountType: mapping.accountType, feeScheduleId: scheduleId });
    toast.show(ok ? `${accountTypeLabel(mapping.accountType)} mapping saved.` : 'Failed to save mapping.', !ok);
  };

  const sendTest = async () => {
    if (!testEmail) return;
    setSendingTest(true);
    try {
      const { success, message } = await ClinicSettingsApi.sendTestEmail(testEmail);
      toast.show(message ?? (success ? 'Test email sent.' : 'Failed to send test email.'), !success);
      if (success) setTestEmail(null);
    } finally {
      setSendingTest(false);
    }
  };

  if (loading) return <><h4 className="mb-4">Clinic Settings</h4><div className="card"><div className="card-body"><LoadingState text="Loading clinic settings..." compact={false} /></div></div></>;
  if (forbidden) return <><h4 className="mb-4">Clinic Settings</h4><div className="alert alert-danger">You don't have access to this page.</div></>;
  if (!form) return <><h4 className="mb-4">Clinic Settings</h4><div className="alert alert-danger">Failed to load clinic settings. Please try reloading the page.</div></>;

  return (
    <>
      <h4 className="mb-4">Clinic Settings</h4>
      <div className="row g-3">
        <div className="col-lg-8">
          <div className="card">
            <div className="card-header"><strong>Practice Information</strong></div>
            <div className="card-body">
              <form onSubmit={save} noValidate>
                <ValidationSummary errors={errors} className="text-danger small mb-3" />
                <div className="row g-3 mb-3">
                  <div className="col-md-8">
                    <label className="form-label">Practice Name <span className="text-danger">*</span></label>
                    <input className="form-control" value={form.practiceName} onChange={(e) => set('practiceName')(e.target.value)} />
                    <FieldMessage messages={errors.practiceName} />
                  </div>
                  <div className="col-md-4"><label className="form-label">ABN</label><TextInput className="form-control" value={form.abn} onChange={set('abn')} /></div>
                </div>
                <div className="row g-3 mb-3">
                  <div className="col-md-4"><label className="form-label">Phone</label><TextInput className="form-control" value={form.phone} onChange={set('phone')} /></div>
                  <div className="col-md-4"><label className="form-label">Fax</label><TextInput className="form-control" value={form.fax} onChange={set('fax')} /></div>
                  <div className="col-md-4"><label className="form-label">Email</label><TextInput className="form-control" value={form.email} onChange={set('email')} /></div>
                </div>
                <div className="row g-3 mb-3">
                  <div className="col-md-12"><label className="form-label">Website</label><TextInput className="form-control" value={form.website} onChange={set('website')} /></div>
                </div>

                <hr className="my-4" />
                <h6 className="mb-3">Scheduling</h6>
                <div className="row g-3 mb-4">
                  <div className="col-md-4">
                    <label className="form-label">Minimum Appointment Timeslot (minutes)</label>
                    <NumberInput className="form-control" value={form.minimumTimeslotMinutes} onChange={(v) => v != null && set('minimumTimeslotMinutes')(v)} />
                    <div className="form-text">Granularity of the appointment calendar's time grid. Staff can override this for themselves in Profile Settings.</div>
                    <FieldMessage messages={errors.minimumTimeslotMinutes} />
                  </div>
                </div>

                <hr className="my-4" />
                <h6 className="mb-3">Address</h6>
                <div className="row g-3 mb-2">
                  <div className="col-md-12"><label className="form-label small mb-0">Address</label><TextInput className="form-control form-control-sm" value={form.address} onChange={set('address')} /></div>
                </div>
                <div className="row g-3 mb-4">
                  <div className="col-md-6"><label className="form-label small mb-0">Suburb</label><TextInput className="form-control form-control-sm" value={form.suburb} onChange={set('suburb')} /></div>
                  <div className="col-md-3"><label className="form-label small mb-0">Postcode</label><TextInput className="form-control form-control-sm" value={form.postcode} onChange={set('postcode')} /></div>
                  <div className="col-md-3"><label className="form-label small mb-0">State</label><TextInput className="form-control form-control-sm" value={form.state} onChange={set('state')} /></div>
                </div>

                <hr className="my-4" />
                <h6 className="mb-3">Email (SMTP)</h6>
                <div className="row g-3 mb-2">
                  <div className="col-md-6">
                    <div className="form-check form-switch">
                      <input className="form-check-input" type="checkbox" role="switch" id="smtpEnabled" checked={form.smtpEnabled} onChange={(e) => set('smtpEnabled')(e.target.checked)} />
                      <label className="form-check-label" htmlFor="smtpEnabled">Enable email sending</label>
                    </div>
                  </div>
                </div>
                <fieldset className={`imed-fieldset ${form.smtpEnabled ? '' : 'is-muted'}`}>
                  <div className="row g-3 mb-3">
                    <div className="col-md-8"><label className="form-label">SMTP Host</label><TextInput className="form-control" placeholder="smtp.example.com" value={form.smtpHost} onChange={set('smtpHost')} /></div>
                    <div className="col-md-4">
                      <label className="form-label">Port</label>
                      <NumberInput className="form-control" value={form.smtpPort} onChange={(v) => v != null && set('smtpPort')(v)} />
                      <FieldMessage messages={errors.smtpPort} />
                    </div>
                  </div>
                  <div className="row g-3 mb-3">
                    <div className="col-md-6"><label className="form-label">Username</label><TextInput className="form-control" autoComplete="off" value={form.smtpUsername} onChange={set('smtpUsername')} /></div>
                    <div className="col-md-6"><label className="form-label">Password</label><TextInput type="password" autoComplete="new-password" className="form-control" value={form.smtpPassword} onChange={set('smtpPassword')} /></div>
                  </div>
                  <div className="row g-3 mb-3">
                    <div className="col-md-6"><label className="form-label">From Email</label><TextInput className="form-control" placeholder="no-reply@yourclinic.com" value={form.smtpFromEmail} onChange={set('smtpFromEmail')} /></div>
                    <div className="col-md-6"><label className="form-label">From Name</label><TextInput className="form-control" placeholder={form.practiceName} value={form.smtpFromName} onChange={set('smtpFromName')} /></div>
                  </div>
                  <div className="row g-3 mb-4">
                    <div className="col-md-6">
                      <div className="form-check form-switch">
                        <input className="form-check-input" type="checkbox" role="switch" id="smtpUseSsl" checked={form.smtpUseSsl} onChange={(e) => set('smtpUseSsl')(e.target.checked)} />
                        <label className="form-check-label" htmlFor="smtpUseSsl">Use SSL/TLS</label>
                      </div>
                    </div>
                    <div className="col-md-6 text-md-end">
                      <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => setTestEmail({ toEmail: '', message: 'This is a test email' })}>
                        <i className="bi bi-send me-1" />Send Test Email
                      </button>
                    </div>
                  </div>
                </fieldset>

                <button type="submit" className="btn btn-primary" disabled={saving}>
                  {saving ? <><span className="spinner-border spinner-border-sm me-2" /><span>Saving...</span></> : <span><i className="bi bi-save me-1" />Save Changes</span>}
                </button>
              </form>
            </div>
          </div>
        </div>

        <div className="col-lg-4">
          <div className="card">
            <div className="card-header"><strong>Billing Item Catalog</strong></div>
            <div className="card-body">
              <p className="small text-muted">
                Refreshes the invoice line-item picker from the free, official Medicare Benefits Schedule (MBS) published quarterly by the Department of Health.
              </p>
              <button type="button" className="btn btn-outline-primary" disabled={syncing} onClick={syncBillingItems}>
                {syncing ? <><span className="spinner-border spinner-border-sm me-2" /><span>Updating...</span></> : <span><i className="bi bi-cloud-download me-1" />Update Fees &amp; Bill Items</span>}
              </button>
              {lastSync && (
                <div className="small text-muted mt-3">
                  Last sync {formatUtc(lastSync.syncedAt, 'dd/MM/yyyy HH:mm')}: {lastSync.added} added, {lastSync.updated} updated, {lastSync.deactivated} deactivated.
                </div>
              )}
            </div>
          </div>
          <div className="card mt-3">
            <div className="card-header"><strong>Account Type → Fee Schedule</strong></div>
            <div className="card-body">
              <p className="small text-muted">
                Maps each account type to the fee schedule that prices its invoice lines. For the Medicare rebate to resolve, also create
                schedules coded <code>BBGP</code>, <code>BBO</code> and <code>BBI</code> and populate them via Fee Schedules → Items.
              </p>
              {mappings.map((m) => (
                <div key={m.accountType} className="row g-2 align-items-center mb-2">
                  <div className="col-5 small">{accountTypeLabel(m.accountType)}</div>
                  <div className="col-7">
                    <select className="form-select form-select-sm" value={m.feeScheduleId ?? ''} aria-label={`Fee schedule for ${accountTypeLabel(m.accountType)}`}
                            onChange={(e) => saveMapping(m, e.target.value)}>
                      <option value="">-- None --</option>
                      {feeSchedules.map((s) => <option key={s.id} value={s.id}>{s.code} - {s.description}</option>)}
                    </select>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>

      {testEmail && (
        <Modal title="Send Test Email" titleTag="h5" headerClassName="" scrollable={false} onClose={() => setTestEmail(null)}
               footer={<>
                 <button type="button" className="btn btn-secondary" onClick={() => setTestEmail(null)}>Cancel</button>
                 <button type="button" className="btn btn-primary" onClick={sendTest} disabled={!testEmail.toEmail.trim() || sendingTest}>
                   {sendingTest ? <><span className="spinner-border spinner-border-sm me-2" /><span>Sending...</span></> : <span><i className="bi bi-send me-1" />Send</span>}
                 </button>
               </>}>
          <div className="mb-3">
            <label className="form-label">Email Address <span className="text-danger">*</span></label>
            <input type="email" className="form-control" placeholder="you@example.com" value={testEmail.toEmail}
                   onChange={(e) => setTestEmail({ ...testEmail, toEmail: e.target.value })} />
          </div>
          <div className="mb-3">
            <label className="form-label">Message</label>
            <textarea className="form-control" rows={4} value={testEmail.message} onChange={(e) => setTestEmail({ ...testEmail, message: e.target.value })} />
          </div>
        </Modal>
      )}
    </>
  );
}
