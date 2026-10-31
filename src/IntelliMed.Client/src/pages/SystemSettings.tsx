import { useState } from 'react';
import { Link } from 'react-router';
import { SystemSettingsApi } from '@/api/settings';
import type { UpdateSystemSettingsDto } from '@/api/types.gen';
import { NumberInput } from '@/components/Inputs';
import { LoadingState } from '@/components/States';
import { formatDate, formatUtc } from '@/lib/dates';
import { useMountEffect, usePageTitle } from '@/lib/hooks';

function NumberField({ label, value, onChange, help }: { label: string; value: number; onChange: (v: number) => void; help?: string }) {
  return (
    <div className="col-md-3">
      <label className="form-label small mb-0">{label}</label>
      <NumberInput className="form-control form-control-sm" value={value} onChange={(v) => { if (v != null) onChange(v); }} />
      {help && <div className="form-text small">{help}</div>}
    </div>
  );
}

function Check({ id, label, checked, onChange }: { id: string; label: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <div className="form-check">
      <input className="form-check-input" type="checkbox" id={id} checked={checked} onChange={(e) => onChange(e.target.checked)} />
      <label className="form-check-label small" htmlFor={id}>{label}</label>
    </div>
  );
}

export default function SystemSettings() {
  usePageTitle('System Settings - IntelliMed');
  const [form, setForm] = useState<UpdateSystemSettingsDto | null>(null);
  const [lastUpdated, setLastUpdated] = useState<string | Date | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useMountEffect(async (alive) => {
    const s = await SystemSettingsApi.get();
    if (!alive()) return;
    if (s) {
      setLastUpdated(s.updatedAt);
      setForm({
        passwordMinLength: s.passwordMinLength,
        passwordRequireDigit: s.passwordRequireDigit,
        passwordRequireLowercase: s.passwordRequireLowercase,
        passwordRequireUppercase: s.passwordRequireUppercase,
        passwordRequireNonAlphanumeric: s.passwordRequireNonAlphanumeric,
        lockoutMaxFailedAttempts: s.lockoutMaxFailedAttempts,
        lockoutDurationMinutes: s.lockoutDurationMinutes,
        sessionExpirationHours: s.sessionExpirationHours,
        defaultAppointmentSlotMinutes: s.defaultAppointmentSlotMinutes,
        newUserShowCommandPalette: s.newUserShowCommandPalette,
        backupRetentionDays: s.backupRetentionDays,
      });
    }
    setLoading(false);
  });

  const set = <K extends keyof UpdateSystemSettingsDto>(key: K) => (value: UpdateSystemSettingsDto[K]) => {
    setSaved(false);
    setForm((f) => (f ? { ...f, [key]: value } : f));
  };

  const save = async () => {
    if (!form) return;
    setSaving(true);
    setError(null);
    setSaved(false);
    try {
      const problem = await SystemSettingsApi.update(form);
      setError(problem);
      if (problem == null) {
        setSaved(true);
        setLastUpdated(new Date());
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <h4 className="mb-1">System Settings</h4>
      <p className="text-muted small mb-4">
        Application-wide settings. Practice details, the billing catalogue and email setup live on <Link to="/clinic-settings">Clinic Settings</Link>.
      </p>

      {loading ? (
        <LoadingState text="Loading..." />
      ) : !form ? (
        <div className="alert alert-secondary small py-2">Couldn't load system settings — you may not have permission to view this page.</div>
      ) : (
        <>
          <div className="card mb-3">
            <div className="card-header"><strong>Password policy</strong></div>
            <div className="card-body">
              <div className="alert alert-secondary small py-2 mb-3">
                <i className="bi bi-info-circle me-1" />
                Password and lockout changes apply the <strong>next time the application restarts</strong>.
                Existing passwords are never invalidated — the policy applies to new and changed passwords from that point on.
              </div>
              <div className="row g-2">
                <NumberField label="Minimum length" value={form.passwordMinLength} onChange={set('passwordMinLength')} />
              </div>
              <div className="d-flex flex-wrap gap-3 mt-2">
                <Check id="pwDigit" label="Require a digit" checked={form.passwordRequireDigit} onChange={set('passwordRequireDigit')} />
                <Check id="pwLower" label="Require lower case" checked={form.passwordRequireLowercase} onChange={set('passwordRequireLowercase')} />
                <Check id="pwUpper" label="Require upper case" checked={form.passwordRequireUppercase} onChange={set('passwordRequireUppercase')} />
                <Check id="pwSymbol" label="Require a symbol" checked={form.passwordRequireNonAlphanumeric} onChange={set('passwordRequireNonAlphanumeric')} />
              </div>
            </div>
          </div>

          <div className="card mb-3">
            <div className="card-header"><strong>Sign-in and sessions</strong></div>
            <div className="card-body">
              <div className="row g-2">
                <NumberField label="Failed attempts before lockout" value={form.lockoutMaxFailedAttempts} onChange={set('lockoutMaxFailedAttempts')} />
                <NumberField label="Lockout duration (minutes)" value={form.lockoutDurationMinutes} onChange={set('lockoutDurationMinutes')} />
                <NumberField label="Session length (hours)" value={form.sessionExpirationHours} onChange={set('sessionExpirationHours')} help="Applies at next sign-in." />
              </div>
            </div>
          </div>

          <div className="card mb-3">
            <div className="card-header"><strong>Defaults for new users</strong></div>
            <div className="card-body">
              <div className="row g-2">
                <NumberField label="Appointment slot (minutes)" value={form.defaultAppointmentSlotMinutes} onChange={set('defaultAppointmentSlotMinutes')}
                             help="Used when a user hasn't set their own." />
              </div>
              <div className="mt-2">
                <Check id="cmdPalette" label="Command palette (Ctrl+K) enabled for new users" checked={form.newUserShowCommandPalette} onChange={set('newUserShowCommandPalette')} />
              </div>
            </div>
          </div>

          <div className="card mb-3">
            <div className="card-header"><strong>Backups</strong></div>
            <div className="card-body">
              <div className="row g-2">
                <NumberField label="Keep backups for (days)" value={form.backupRetentionDays} onChange={set('backupRetentionDays')} help="Older backups are pruned automatically." />
              </div>
              <div className="small text-muted mt-2">Schedule and manual backups are on <Link to="/admin/database-backups">Database Backups</Link>.</div>
            </div>
          </div>

          {error && <div className="small text-danger mb-2">{error}</div>}
          {saved && <div className="small text-success mb-2"><i className="bi bi-check-circle me-1" />Settings saved.</div>}
          <button type="button" className="btn btn-primary" disabled={saving} onClick={save}>
            {saving && <span className="spinner-border spinner-border-sm me-1" />} Save Settings
          </button>
          {lastUpdated && (
            <span className="text-muted small ms-2">
              Last changed {lastUpdated instanceof Date ? formatDate(lastUpdated, 'd MMM yyyy, h:mm tt') : formatUtc(lastUpdated, 'd MMM yyyy, h:mm tt')}
            </span>
          )}
        </>
      )}
    </>
  );
}
