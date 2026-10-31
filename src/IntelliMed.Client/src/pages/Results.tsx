import { useCallback, useRef, useState } from 'react';
import { Link } from 'react-router';
import { ResultApi } from '@/api/clinical';
import {
  ResultReportKind, ResultStatus, type ClientDto, type CreatedResultIngestKeyDto, type ResultAttachmentDto,
  type ResultCountsDto, type ResultDetailDto, type ResultIngestKeyDto, type ResultSummaryDto,
} from '@/api/types.gen';
import { Drawer, PageHeader } from '@/components/Drawer';
import { FindClientModal } from '@/components/FindClientModal';
import { Modal } from '@/components/Modal';
import { EmptyState, SkeletonRows } from '@/components/States';
import { formatDate, formatUtc } from '@/lib/dates';
import { downloadBlob } from '@/lib/format';
import { useMountEffect, usePageTitle } from '@/lib/hooks';
import { useSession } from '@/session';

const TABS: { status: ResultStatus; label: string }[] = [
  { status: ResultStatus.Inbox, label: 'Inbox' },
  { status: ResultStatus.Unmatched, label: 'Unmatched' },
  { status: ResultStatus.Held, label: 'Held' },
  { status: ResultStatus.Archived, label: 'Archived' },
];

const PAGE_SIZE = 25;
const MAX_UPLOAD_BYTES = 25 * 1024 * 1024;

const EMPTY_MESSAGES: Record<ResultStatus, string> = {
  [ResultStatus.Inbox]: 'Nothing waiting for review.',
  [ResultStatus.Unmatched]: 'No reports are waiting to be matched to a patient.',
  [ResultStatus.Held]: 'No held reports.',
  [ResultStatus.Archived]: 'No acknowledged reports yet.',
};

interface Filters { status: ResultStatus; query: string; abnormalOnly: boolean; mine: boolean; page: number }

export default function Results() {
  usePageTitle('Results - IntelliMed');
  const session = useSession();
  const isAdmin = session.can('admin/settings');

  const [filters, setFilters] = useState<Filters>({ status: ResultStatus.Inbox, query: '', abnormalOnly: false, mine: true, page: 1 });
  // the latest filters, so a load never runs with a stale tab/search (and a slow response can't overwrite a newer one)
  const live = useRef(filters);
  const seq = useRef(0);
  const [items, setItems] = useState<ResultSummaryDto[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [counts, setCounts] = useState<ResultCountsDto>({ inbox: 0, unmatched: 0, held: 0, inboxAbnormal: 0 });
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<{ text: string; isError: boolean } | null>(null);

  const [detail, setDetail] = useState<ResultDetailDto | null>(null);
  const [comment, setComment] = useState('');
  const [findOpen, setFindOpen] = useState(false);
  const [pendingMatch, setPendingMatch] = useState<ClientDto | null>(null);
  const [rememberLab, setRememberLab] = useState(true);

  const [connectionsOpen, setConnectionsOpen] = useState(false);
  const [keys, setKeys] = useState<ResultIngestKeyDto[]>([]);
  const [keyLabel, setKeyLabel] = useState('');
  const [newKey, setNewKey] = useState<CreatedResultIngestKeyDto | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);

  const load = useCallback(async (patch: Partial<Filters> = {}) => {
    const f = { ...live.current, ...patch };
    live.current = f;
    setFilters(f);
    const mine = ++seq.current;
    setLoading(true);
    const result = await ResultApi.search({ status: f.status, query: f.query, abnormalOnly: f.abnormalOnly, mine: f.mine, page: f.page, pageSize: PAGE_SIZE });
    if (mine !== seq.current) return;
    setItems(result.items);
    setTotal(result.totalCount);
    setLoading(false);
  }, []);

  const reloadAll = useCallback(async (patch: Partial<Filters> = {}) => {
    const mine = patch.mine ?? live.current.mine;
    const [c] = await Promise.all([ResultApi.counts(mine), load({ ...patch, page: 1 })]);
    setCounts(c);
    session.refreshResultsBadge();
  }, [load, session]);

  useMountEffect(() => { void reloadAll(); });

  const countFor = (s: ResultStatus) =>
    s === ResultStatus.Inbox ? counts.inbox : s === ResultStatus.Unmatched ? counts.unmatched : s === ResultStatus.Held ? counts.held : 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const say = (text: string, isError = false) => setNotice({ text, isError });

  const open = async (id: number) => {
    setComment('');
    setDetail(await ResultApi.getById(id));
  };
  const closeDetail = useCallback(() => setDetail(null), []);

  // ---- upload ----
  const upload = async (file: File | undefined) => {
    if (!file) return;
    setBusy(true);
    try {
      if (file.size > MAX_UPLOAD_BYTES) {
        say('The file could not be read: it is larger than the 25 MB limit.', true);
        return;
      }
      const { outcome, error } = await ResultApi.upload(file);
      if (!outcome) say(error ?? 'The file could not be uploaded.', true);
      else if (outcome.duplicate) say('That report has already been received — nothing was added.');
      else say(outcome.status === ResultStatus.Unmatched
        ? "Report received, but it couldn't be matched to a patient. Open the Unmatched tab to file it."
        : 'Report received and filed in the inbox.');
      await reloadAll();
    } finally {
      setBusy(false);
      if (fileInput.current) fileInput.current.value = '';
    }
  };

  // ---- review actions ----
  const review = async (action: () => Promise<boolean>, failure: string, success: string) => {
    if (!detail) return;
    setBusy(true);
    const ok = await action();
    setBusy(false);
    if (!ok) { say(failure, true); return; }
    say(success);
    setDetail(null);
    await reloadAll();
  };
  const note = comment.trim() ? comment : null;
  const acknowledge = () => review(() => ResultApi.acknowledge(detail!.id, note), 'The report could not be acknowledged.', 'Report acknowledged.');
  const hold = () => review(() => ResultApi.hold(detail!.id, note), 'The report could not be held.', 'Report held for later.');
  const reopen = () => review(() => ResultApi.reopen(detail!.id), 'The report could not be moved back to the inbox.', 'Report moved back to the inbox.');

  // ---- matching ----
  const confirmMatch = async () => {
    if (!detail || !pendingMatch) return;
    setBusy(true);
    const id = detail.id;
    const ok = await ResultApi.match(id, pendingMatch.id, rememberLab);
    setBusy(false);
    setPendingMatch(null);
    if (!ok) { say('The report could not be matched to that patient.', true); return; }
    say('Report filed under the patient and moved to the inbox.');
    await reloadAll();
    setDetail(await ResultApi.getById(id));
  };

  const download = async (a: ResultAttachmentDto) => {
    if (!detail) return;
    const blob = await ResultApi.attachment(detail.id, a.id);
    if (!blob) { say('The attachment could not be downloaded.', true); return; }
    downloadBlob(blob, a.fileName);
  };

  // ---- connections ----
  const openConnections = async () => {
    setNewKey(null);
    setKeyLabel('');
    setKeys(await ResultApi.ingestKeys());
    setConnectionsOpen(true);
  };
  const createKey = async () => {
    setBusy(true);
    const created = await ResultApi.createIngestKey(keyLabel);
    setBusy(false);
    setNewKey(created);
    if (!created) { say('The key could not be created.', true); return; }
    setKeyLabel('');
    setKeys(await ResultApi.ingestKeys());
  };
  const revokeKey = async (id: number) => {
    if (await ResultApi.revokeIngestKey(id)) setKeys(await ResultApi.ingestKeys());
  };
  const closeConnections = useCallback(() => setConnectionsOpen(false), []);
  const ingestUrl = new URL('api/results/ingest', document.baseURI).toString();

  const reviewable = detail && (detail.status === ResultStatus.Inbox || detail.status === ResultStatus.Held);
  const firstLoad = loading && items.length === 0;

  return (
    <>
      <PageHeader title="Results"
                  subtitle="Pathology and imaging reports received from labs. Nothing leaves the inbox until a doctor acknowledges it."
                  actions={<>
                    {isAdmin && (
                      <button className="btn btn-outline-secondary btn-sm" id="btnConnections" onClick={() => void openConnections()}>
                        <i className="bi bi-plug me-1" /> Connections
                      </button>
                    )}
                    <label className={`btn btn-primary btn-sm mb-0 ${busy ? 'disabled' : ''}`} htmlFor="uploadResult">
                      {busy ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-upload me-1" />} Upload HL7 file
                    </label>
                    <input ref={fileInput} type="file" id="uploadResult" className="d-none" accept=".hl7,.txt,.oru,.msg"
                           onChange={(e) => void upload(e.target.files?.[0])} />
                  </>} />

      {notice && (
        <div className={`alert ${notice.isError ? 'alert-danger' : 'alert-success'} py-2 small d-flex align-items-start`} role="alert">
          <span className="flex-grow-1">{notice.text}</span>
          <button type="button" className="btn-close btn-close-sm ms-2" aria-label="Dismiss" onClick={() => setNotice(null)} />
        </div>
      )}

      <ul className="nav nav-tabs mb-3" id="resultTabs">
        {TABS.map((tab) => (
          <li key={tab.status} className="nav-item">
            <button className={`nav-link ${filters.status === tab.status ? 'active' : ''}`} id={`tab-${ResultStatus[tab.status].toLowerCase()}`}
                    onClick={() => void load({ status: tab.status, page: 1 })}>
              {tab.label}
              {countFor(tab.status) > 0 && (
                <span className={`badge rounded-pill ms-1 ${tab.status === ResultStatus.Inbox && counts.inboxAbnormal > 0 ? 'bg-danger' : 'bg-secondary'}`}>
                  {countFor(tab.status)}
                </span>
              )}
            </button>
          </li>
        ))}
      </ul>

      <div className="card">
        <div className="card-header d-flex flex-wrap align-items-center gap-3 py-2">
          <div className="input-group input-group-sm" style={{ maxWidth: 320 }}>
            <span className="input-group-text bg-white"><i className="bi bi-search" /></span>
            <input className="form-control" placeholder="Search patient, report or lab..." value={filters.query}
                   onChange={(e) => void load({ query: e.target.value, page: 1 })} />
          </div>
          <div className="form-check form-check-inline small mb-0">
            <input className="form-check-input" type="checkbox" id="chkAbnormal" checked={filters.abnormalOnly}
                   onChange={(e) => void load({ abnormalOnly: e.target.checked, page: 1 })} />
            <label className="form-check-label" htmlFor="chkAbnormal">Abnormal only</label>
          </div>
          <div className="form-check form-check-inline small mb-0">
            <input className="form-check-input" type="checkbox" id="chkMine" checked={filters.mine}
                   onChange={(e) => void reloadAll({ mine: e.target.checked })} />
            <label className="form-check-label" htmlFor="chkMine">My results and unassigned</label>
          </div>
          <span className="small text-muted ms-auto">{total} report{total === 1 ? '' : 's'}</span>
        </div>

        {!firstLoad && items.length === 0 ? (
          <div className="card-body"><EmptyState message={EMPTY_MESSAGES[filters.status]} icon="bi-inbox" /></div>
        ) : (
          <div className={`table-responsive imed-grid-body ${loading && !firstLoad ? 'is-refreshing' : ''}`} aria-busy={loading}>
            <table className="table table-hover mb-0" id="resultsTable">
              <thead>
                <tr>
                  <th style={{ width: 28 }} />
                  <th>Patient</th>
                  <th>Report</th>
                  <th>Lab</th>
                  <th>Collected</th>
                  <th>Ordered by</th>
                  <th>Received</th>
                </tr>
              </thead>
              <tbody>
                {firstLoad ? <SkeletonRows columns={7} rows={6} /> : items.map((r) => (
                  <tr key={r.id} style={{ cursor: 'pointer' }} onClick={() => void open(r.id)}
                      className={`result-row ${detail?.id === r.id ? 'table-active' : ''}`}>
                    <td>{r.hasAbnormal && <i className="bi bi-exclamation-triangle-fill text-danger" title="Abnormal result" />}</td>
                    <td>
                      <div className="fw-semibold">{r.clientName ?? (r.patientNameAsReceived?.trim() ? r.patientNameAsReceived : '(no name)')}</div>
                      <div className="small text-muted">
                        {formatDate(r.patientDateOfBirth)}
                        {r.status === ResultStatus.Unmatched && <span className="badge bg-warning text-dark ms-1">Needs matching</span>}
                      </div>
                    </td>
                    <td>
                      {r.reportName ?? 'Report'}
                      {r.kind === ResultReportKind.Imaging && <span className="badge bg-secondary ms-1">Imaging</span>}
                    </td>
                    <td>{r.senderName}</td>
                    <td>{formatDate(r.collectedAt)}</td>
                    <td>{r.assignedPractitionerName ?? r.orderingProviderName}</td>
                    <td>{formatUtc(r.receivedAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {total > PAGE_SIZE && (
          <div className="card-footer d-flex align-items-center justify-content-between py-2">
            <button className="btn btn-sm btn-outline-secondary" disabled={filters.page <= 1 || loading} onClick={() => void load({ page: filters.page - 1 })}>
              <i className="bi bi-chevron-left" /> Previous
            </button>
            <span className="small text-muted">Page {filters.page} of {totalPages}</span>
            <button className="btn btn-sm btn-outline-secondary" disabled={filters.page >= totalPages || loading} onClick={() => void load({ page: filters.page + 1 })}>
              Next <i className="bi bi-chevron-right" />
            </button>
          </div>
        )}
      </div>

      {/* ---- Report viewer ---- */}
      {detail && (
        <Drawer title={detail.reportName ?? 'Report'} wide onClose={closeDetail}
                footer={<>
                  {reviewable && (
                    <button className="btn btn-primary" id="btnAcknowledge" disabled={busy} onClick={() => void acknowledge()}>
                      <i className="bi bi-check2-circle me-1" /> Acknowledge
                    </button>
                  )}
                  {detail.status === ResultStatus.Inbox && (
                    <button className="btn btn-outline-secondary" id="btnHold" disabled={busy} onClick={() => void hold()}>Hold for later</button>
                  )}
                  {(detail.status === ResultStatus.Held || detail.status === ResultStatus.Archived) && (
                    <button className="btn btn-outline-secondary" id="btnReopen" disabled={busy} onClick={() => void reopen()}>Move back to inbox</button>
                  )}
                  <button className="btn btn-outline-secondary ms-auto" onClick={closeDetail}>Close</button>
                </>}>
          <div className="d-flex align-items-start justify-content-between mb-2">
            <div>
              <div className="fw-semibold fs-6">{detail.clientName ?? detail.patientNameAsReceived}</div>
              <div className="small text-muted">
                DOB {formatDate(detail.patientDateOfBirth) || '—'}
                {detail.patientSex && <span> · {detail.patientSex}</span>}
                {detail.patientMedicareNumber && <span> · Medicare {detail.patientMedicareNumber}</span>}
              </div>
            </div>
            {detail.clientId != null && (
              <Link className="btn btn-sm btn-outline-secondary" to={`/clients/edit/${detail.clientId}`}>
                <i className="bi bi-person me-1" /> Patient record
              </Link>
            )}
          </div>

          <div className="imed-section-label">Report</div>
          <div className="row g-2 mb-2">
            <Field label="Lab" value={detail.senderName} />
            <Field label="Ordered by" value={detail.orderingProviderName} />
            <Field label="Collected" value={formatDate(detail.collectedAt, 'dd/MM/yyyy HH:mm')} />
            <Field label="Reported" value={formatDate(detail.reportedAt, 'dd/MM/yyyy HH:mm')} />
            <Field label="Lab accession" value={detail.fillerOrderNumber} />
            <Field label="Our order number" value={detail.placerOrderNumber} />
          </div>

          {detail.status === ResultStatus.Unmatched && (
            <div className="alert alert-warning small">
              <strong>This report isn't filed under a patient.</strong>{' '}
              Check the name, date of birth and Medicare number above against the patient you pick — it is only filed once you confirm.
              <div className="mt-2">
                <button className="btn btn-sm btn-primary" id="btnFindPatient" onClick={() => setFindOpen(true)}>
                  <i className="bi bi-search me-1" /> Find patient
                </button>
              </div>
            </div>
          )}

          <div className="imed-section-label">Results</div>
          <table className="table table-sm" id="resultItems">
            <thead>
              <tr><th>Test</th><th className="text-end">Result</th><th>Units</th><th>Reference</th><th /></tr>
            </thead>
            <tbody>
              {detail.items.map((item, i) => (
                <tr key={i} className={item.isAbnormal ? 'table-danger' : ''}>
                  <td>
                    {item.name}
                    {item.comment?.trim() && <div className="small text-muted" style={{ whiteSpace: 'pre-line' }}>{item.comment}</div>}
                  </td>
                  <td className={`text-end ${item.isAbnormal ? 'fw-bold' : ''}`} style={{ whiteSpace: 'pre-line' }}>{item.value}</td>
                  <td>{item.units}</td>
                  <td className="text-muted">{item.referenceRange}</td>
                  <td>{item.isAbnormal && <span className="badge bg-danger">{item.abnormalFlag?.trim() ? item.abnormalFlag : 'Abn'}</span>}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {detail.attachments.length > 0 && (
            <>
              <div className="imed-section-label">Attachments</div>
              {detail.attachments.map((a) => (
                <button key={a.id} className="btn btn-sm btn-outline-secondary me-2 mb-2" onClick={() => void download(a)}>
                  <i className="bi bi-file-earmark-arrow-down me-1" /> {a.fileName}
                </button>
              ))}
            </>
          )}

          {detail.status === ResultStatus.Archived ? (
            <div className="alert alert-secondary small mt-3 mb-0">
              Acknowledged{detail.reviewedByName != null ? ` by ${detail.reviewedByName}` : ''} {formatUtc(detail.reviewedAt)}
              {detail.reviewComment?.trim() && <div className="mt-1">“{detail.reviewComment}”</div>}
            </div>
          ) : detail.status === ResultStatus.Held && detail.holdNote?.trim() ? (
            <div className="alert alert-info small mt-3 mb-0">Held: {detail.holdNote}</div>
          ) : null}

          {reviewable && (
            <>
              <div className="imed-section-label">Your review</div>
              <textarea className="form-control form-control-sm" rows={2} id="reviewComment" placeholder="Comment or action taken (optional)"
                        value={comment} onChange={(e) => setComment(e.target.value)} />
            </>
          )}
        </Drawer>
      )}

      {findOpen && (
        <FindClientModal title="Match to patient" onClose={() => setFindOpen(false)}
                         onSelect={(c) => { setFindOpen(false); setPendingMatch(c); setRememberLab(true); }} />
      )}

      {pendingMatch && (
        <Modal title="File this report under this patient?" onClose={() => setPendingMatch(null)}
               footer={<>
                 <button className="btn btn-sm btn-outline-secondary" onClick={() => setPendingMatch(null)}>Cancel</button>
                 <button className="btn btn-sm btn-primary" id="btnConfirmMatch" disabled={busy} onClick={() => void confirmMatch()}>Yes, file it</button>
               </>}>
          <div className="small">
            <div className="row">
              <div className="col-6">
                <div className="imed-section-label mt-0">Lab sent</div>
                <div className="fw-semibold">{detail?.patientNameAsReceived}</div>
                <div>DOB {formatDate(detail?.patientDateOfBirth) || '—'}</div>
                <div>Medicare {detail?.patientMedicareNumber ?? '—'}</div>
              </div>
              <div className="col-6">
                <div className="imed-section-label mt-0">IntelliMed patient</div>
                <div className="fw-semibold">{pendingMatch.fullName}</div>
                <div>DOB {formatDate(pendingMatch.dateOfBirth) || '—'}</div>
                <div>Medicare {pendingMatch.medicareNumber?.trim() ? pendingMatch.medicareNumber : '—'}</div>
              </div>
            </div>
            {detail?.patientIdentifier?.trim() && (
              <div className="form-check mt-3">
                <input className="form-check-input" type="checkbox" id="chkRemember" checked={rememberLab} onChange={(e) => setRememberLab(e.target.checked)} />
                <label className="form-check-label" htmlFor="chkRemember">
                  Remember the lab's patient number ({detail.patientIdentifier}) so its next report files itself
                </label>
              </div>
            )}
          </div>
        </Modal>
      )}

      {/* ---- Ingest connections (admins) ---- */}
      {connectionsOpen && (
        <Drawer title="Result connections" onClose={closeConnections}>
          <p className="small text-muted">
            A lab feed or relay delivers HL7 results by POSTing them to this clinic's ingest endpoint with a key.
            Each key only delivers into the clinic it was created in.
          </p>
          <div className="imed-section-label">Endpoint</div>
          <code className="small d-block mb-3">POST {ingestUrl}<br />X-Api-Key: &lt;key&gt;</code>
          {newKey && (
            <div className="alert alert-warning small" id="newKeyAlert">
              <strong>Copy this key now.</strong> It is shown once and can't be recovered.
              <div className="d-flex align-items-center gap-2 mt-1">
                <code className="d-block user-select-all flex-grow-1" id="newKeyValue">{newKey.key}</code>
                <button type="button" className="btn btn-sm btn-outline-secondary" aria-label="Copy key"
                        onClick={() => void navigator.clipboard?.writeText(newKey.key)}>
                  <i className="bi bi-clipboard" />
                </button>
              </div>
            </div>
          )}
          <div className="imed-section-label">New key</div>
          <div className="input-group input-group-sm mb-3">
            <input className="form-control" id="keyLabel" placeholder="Label, e.g. Acme Pathology relay" value={keyLabel}
                   onChange={(e) => setKeyLabel(e.target.value)} />
            <button className="btn btn-primary" id="btnCreateKey" disabled={!keyLabel.trim() || busy} onClick={() => void createKey()}>Create</button>
          </div>
          <div className="imed-section-label">Keys</div>
          {keys.length === 0 ? (
            <div className="small text-muted">No keys yet.</div>
          ) : (
            <table className="table table-sm small">
              <thead><tr><th>Label</th><th>Key</th><th>Last used</th><th /></tr></thead>
              <tbody>
                {keys.map((k) => (
                  <tr key={k.id} className={k.isActive ? '' : 'text-muted'}>
                    <td>{k.label}</td>
                    <td><code>{k.keyPrefix}…</code></td>
                    <td>{formatUtc(k.lastUsedAt) || 'never'}</td>
                    <td className="text-end">
                      {k.isActive
                        ? <button className="btn btn-link btn-sm text-danger p-0" onClick={() => void revokeKey(k.id)}>Revoke</button>
                        : <span>revoked</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Drawer>
      )}
    </>
  );
}

function Field({ label, value }: { label: string; value: string | null | undefined }) {
  return (
    <div className="col-6">
      <div className="imed-field-label">{label}</div>
      <div className="imed-field-value">{value?.trim() ? value : '—'}</div>
    </div>
  );
}
