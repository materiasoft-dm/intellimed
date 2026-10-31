import { useState } from 'react';
import { DatabaseBackupApi } from '@/api/settings';
import { BackupIntervalUnit, DatabaseBackupTrigger, type DatabaseBackupDto, type DatabaseBackupSettingsDto, type UpdateDatabaseBackupSettingsDto } from '@/api/types.gen';
import { NumberSelect } from '@/components/Inputs';
import { EmptyState, LoadingState } from '@/components/States';
import { useToast } from '@/components/Toast';
import { formatUtc } from '@/lib/dates';
import { downloadBlob } from '@/lib/format';
import { useMountEffect, usePageTitle } from '@/lib/hooks';

function formatSize(bytes: number): string {
  const trim = (n: number) => n.toFixed(2).replace(/\.?0+$/, '');
  if (bytes >= 1024 * 1024) return `${trim(bytes / (1024 * 1024))} MB`;
  if (bytes >= 1024) return `${trim(bytes / 1024)} KB`;
  return `${bytes} B`;
}

export default function DatabaseBackups() {
  usePageTitle('Database Backups - IntelliMed');
  const toast = useToast();
  const [backups, setBackups] = useState<DatabaseBackupDto[]>([]);
  const [settings, setSettings] = useState<DatabaseBackupSettingsDto | null>(null);
  const [form, setForm] = useState<UpdateDatabaseBackupSettingsDto>({ intervalValue: 1, intervalUnit: BackupIntervalUnit.Days });
  const [loading, setLoading] = useState(true);
  const [backingUp, setBackingUp] = useState(false);
  const [settingsSaved, setSettingsSaved] = useState(false);
  const [downloading, setDownloading] = useState<string | null>(null);

  const loadSettings = async () => {
    const s = await DatabaseBackupApi.getSettings();
    setSettings(s);
    if (s) setForm({ intervalValue: s.intervalValue, intervalUnit: s.intervalUnit });
  };
  const loadBackups = async () => {
    setLoading(true);
    setBackups(await DatabaseBackupApi.list());
    setLoading(false);
  };
  useMountEffect(async () => {
    await loadSettings();
    await loadBackups();
  });

  const saveSettings = async () => {
    setSettingsSaved(await DatabaseBackupApi.updateSettings(form));
    await loadSettings();
  };

  const backupNow = async () => {
    setBackingUp(true);
    const ok = await DatabaseBackupApi.backupNow();
    setBackingUp(false);
    if (!ok) toast.error('The backup failed.');
    await loadBackups();
  };

  const download = async (b: DatabaseBackupDto) => {
    setDownloading(b.fileName);
    const blob = await DatabaseBackupApi.download(b.fileName);
    setDownloading(null);
    if (blob) downloadBlob(blob, b.fileName);
    else toast.error(`Couldn't download ${b.fileName}.`);
  };

  const remove = async (b: DatabaseBackupDto) => {
    if (!window.confirm(`Permanently delete ${b.fileName}? This cannot be undone.`)) return;
    await DatabaseBackupApi.remove(b.fileName);
    await loadBackups();
  };

  return (
    <>
      <h4 className="mb-4">Database Backups</h4>

      <div className="card mb-3">
        <div className="card-header"><strong>Schedule</strong></div>
        <div className="card-body">
          <div className="row g-2 align-items-end">
            <div className="col-auto">
              <label className="form-label small mb-0">Back up every</label>
              <input type="number" min={1} className="form-control form-control-sm" style={{ width: 100 }} value={form.intervalValue}
                     onChange={(e) => { setSettingsSaved(false); setForm({ ...form, intervalValue: Number(e.target.value) || 0 }); }} />
            </div>
            <div className="col-auto">
              <label className="form-label small mb-0">&nbsp;</label>
              <NumberSelect enumType={BackupIntervalUnit} nullable={false} className="form-select form-select-sm" value={form.intervalUnit} aria-label="Interval unit"
                            onChange={(v) => { setSettingsSaved(false); setForm({ ...form, intervalUnit: (v ?? 0) as BackupIntervalUnit }); }}>
                <option value={BackupIntervalUnit.Minutes}>Minutes</option>
                <option value={BackupIntervalUnit.Hours}>Hours</option>
                <option value={BackupIntervalUnit.Days}>Days</option>
                <option value={BackupIntervalUnit.Weeks}>Weeks</option>
                <option value={BackupIntervalUnit.Months}>Months</option>
              </NumberSelect>
            </div>
            <div className="col-auto"><button className="btn btn-sm btn-primary" onClick={saveSettings}>Save</button></div>
            {settings?.lastRunAt && (
              <div className="col-auto ms-auto text-muted small">Last scheduled backup: {formatUtc(settings.lastRunAt, 'g')}</div>
            )}
          </div>
          {settingsSaved && <div className="text-success small mt-2"><i className="bi bi-check-circle me-1" />Saved.</div>}
        </div>
      </div>

      <div className="card">
        <div className="card-header d-flex justify-content-between align-items-center py-2">
          <strong>Backups</strong>
          <button className="btn btn-sm btn-primary" onClick={backupNow} disabled={backingUp}>
            {backingUp && <span className="spinner-border spinner-border-sm me-1" />}
            <i className="bi bi-cloud-arrow-down-fill me-1" /> Backup Now
          </button>
        </div>
        <div className="card-body py-2">
          <div className="text-muted small mb-2"><i className="bi bi-info-circle me-1" />Backups older than 2 months are deleted automatically.</div>
          {loading ? (
            <LoadingState text="Loading backups..." />
          ) : backups.length === 0 ? (
            <EmptyState message="No backups yet." icon="bi-hdd-stack" />
          ) : (
            <table className="table table-sm align-middle mb-0">
              <thead><tr><th>File</th><th>Size</th><th>Trigger</th><th>Created</th><th /></tr></thead>
              <tbody>
                {backups.map((b) => (
                  <tr key={b.fileName}>
                    <td className="text-break">{b.fileName}</td>
                    <td className="text-nowrap">{formatSize(b.fileSizeBytes)}</td>
                    <td><span className={`badge ${b.trigger === DatabaseBackupTrigger.Manual ? 'bg-secondary' : 'bg-info text-dark'}`}>{b.triggerName}</span></td>
                    <td className="text-nowrap">{formatUtc(b.createdAt, 'g')}</td>
                    <td className="text-end text-nowrap">
                      <button className="btn btn-sm btn-outline-secondary me-1" disabled={downloading === b.fileName} onClick={() => download(b)}>
                        {downloading === b.fileName ? <span className="spinner-border spinner-border-sm" /> : <i className="bi bi-download" />} Download
                      </button>
                      <button className="btn btn-sm btn-outline-danger" onClick={() => remove(b)}><i className="bi bi-trash" /> Delete</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </>
  );
}
