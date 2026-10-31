import { useRef, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { ClinicContext } from '@/api/admin';
import { AppointmentApi, PractitionerApi } from '@/api/appointments';
import { AuthApi } from '@/api/auth';
import { ClientApi, ClientHealthIssueApi, EpisodeApi } from '@/api/clients';
import { ProgressNoteApi } from '@/api/clinical';
import {
  AppointmentStatus, AppointmentType, defaultCreateAppointmentDto, ProgressNoteAddendumSource, ProgressNoteStatus,
  type ClientDto, type ClientHealthIssueDto, type EpisodeDto, type PractitionerDto, type ProgressNoteDto,
} from '@/api/types.gen';
import { ClientSafetyBanner } from '@/components/ClientSafetyBanner';
import { DateInput, NumberSelect, TextArea, TextInput } from '@/components/Inputs';
import { LoadingState } from '@/components/States';
import { formatDate, minutesToSpan, todayIso } from '@/lib/dates';
import { useMountEffect, usePageTitle } from '@/lib/hooks';

interface NoteForm {
  practitionerId: number;
  episodeId: number | null;
  encounterDate: string;
  reasonForVisit: string | null;
  subjective: string | null;
  objective: string | null;
  assessment: string | null;
  plan: string | null;
  reviewDate: string | null;
}

const practitionerName = (p: PractitionerDto) => [p.title, p.firstName, p.lastName].filter(Boolean).join(' ');

function NoteSection({ title, text }: { title: string; text: string | null }) {
  if (!text?.trim()) return null;
  return <div className="mb-2"><strong className="small text-muted d-block">{title}</strong><div style={{ whiteSpace: 'pre-wrap' }}>{text}</div></div>;
}

export default function ProgressNoteEditor() {
  const params = useParams();
  const clientId = Number(params.clientId);
  const noteId = params.noteId ? Number(params.noteId) : null;
  const [query] = useSearchParams();
  const appointmentIdParam = query.get('appointmentId') ? Number(query.get('appointmentId')) : null;
  const navigate = useNavigate();
  usePageTitle(`${noteId != null ? 'Progress Note' : 'New Progress Note'} - IntelliMed`);

  const [client, setClient] = useState<ClientDto | null>(null);
  const [practitioners, setPractitioners] = useState<PractitionerDto[]>([]);
  const [episodes, setEpisodes] = useState<EpisodeDto[]>([]);
  const [summaryIssues, setSummaryIssues] = useState<ClientHealthIssueDto[]>([]);
  const [note, setNote] = useState<ProgressNoteDto | null>(null);
  const [form, setForm] = useState<NoteForm>({ practitionerId: 0, episodeId: null, encounterDate: todayIso(), reasonForVisit: null, subjective: null, objective: null, assessment: null, plan: null, reviewDate: null });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [markCompleted, setMarkCompleted] = useState(true);
  const [newEpisode, setNewEpisode] = useState<string | null>(null);
  const [addendumDraft, setAddendumDraft] = useState('');
  const [addendumPractitionerId, setAddendumPractitionerId] = useState(0);
  // resolved once and cached: re-resolving on each save would book duplicate walk-in appointments
  const resolvedAppointmentId = useRef<number | null>(null);

  const loadNote = async (id: number) => {
    const n = await ProgressNoteApi.getById(clientId, id);
    setNote(n);
    if (n) {
      setForm({
        practitionerId: n.practitionerId, episodeId: n.episodeId, encounterDate: n.encounterDate, reasonForVisit: n.reasonForVisit,
        subjective: n.subjective, objective: n.objective, assessment: n.assessment, plan: n.plan, reviewDate: n.reviewDate,
      });
      setAddendumPractitionerId(n.practitionerId);
    }
    return n;
  };

  useMountEffect(async (alive) => {
    const [c, pracs, eps, summary] = await Promise.all([
      ClientApi.getById(clientId), PractitionerApi.active(), EpisodeApi.list(clientId), ClientHealthIssueApi.summary(clientId),
    ]);
    if (!alive()) return;
    setClient(c);
    setPractitioners(pracs);
    setEpisodes(eps);
    setSummaryIssues(summary);
    let addendumAuthor = 0;
    if (noteId != null) {
      const n = await loadNote(noteId);
      addendumAuthor = n?.practitionerId ?? 0;
    } else {
      // pre-fill (not lock) the author for a signed-in Doctor/Nurse with a linked practitioner record
      const me = await AuthApi.me();
      if (me.practitionerId != null && (me.roles?.includes('Doctor') || me.roles?.includes('Nurse'))) {
        setForm((f) => ({ ...f, practitionerId: me.practitionerId! }));
        addendumAuthor = me.practitionerId;
      }
    }
    if (!alive()) return;
    setAddendumPractitionerId(addendumAuthor || pracs[0]?.id || 0);
    setLoading(false);
  });

  const hasLinkedAppointment = (noteId != null ? note?.appointmentId : appointmentIdParam) != null;
  const canSave = form.practitionerId > 0;
  const set = (patch: Partial<NoteForm>) => setForm((f) => ({ ...f, ...patch }));

  /**
   * Which appointment a new note belongs to: the ?appointmentId it was opened with (e.g. from the Waiting
   * Room), or — for a walk-in — a same-day appointment booked on the spot and marked In Progress.
   */
  const resolveAppointmentId = async (): Promise<number | null> => {
    if (resolvedAppointmentId.current != null) return resolvedAppointmentId.current;
    if (appointmentIdParam != null) return appointmentIdParam;
    const clinicId = ClinicContext.get();
    if (clinicId == null || form.practitionerId <= 0) return null;
    const now = new Date();
    const start = now.getHours() * 60 + now.getMinutes();
    const id = await AppointmentApi.create({
      ...defaultCreateAppointmentDto(), clinicId, clientId, practitionerId: form.practitionerId, appointmentDate: todayIso(),
      startTime: minutesToSpan(start), endTime: minutesToSpan(start + 15), type: AppointmentType.Standard, notes: 'Auto-created for a walk-in progress note',
    });
    if (id != null) {
      resolvedAppointmentId.current = id;
      await AppointmentApi.updateStatus(id, AppointmentStatus.InProgress);
    }
    return id;
  };

  const createNote = async () => {
    const appointmentId = await resolveAppointmentId();
    return ProgressNoteApi.create({ clientId, appointmentId: appointmentId ?? appointmentIdParam, ...form });
  };

  const saveDraft = async () => {
    setSaving(true);
    setSaveError(null);
    try {
      if (noteId == null) {
        const id = await createNote();
        if (id == null) { setSaveError('Save failed — check your permissions.'); return; }
        navigate(`/clients/${clientId}/progress-notes/${id}`, { replace: true });
      } else {
        if (!(await ProgressNoteApi.update(clientId, noteId, form))) { setSaveError('Save failed.'); return; }
        await loadNote(noteId);
      }
    } finally {
      setSaving(false);
    }
  };

  const finalize = async () => {
    setSaving(true);
    setSaveError(null);
    try {
      let id = noteId;
      if (id == null) {
        id = await createNote();
        if (id == null) { setSaveError('Save failed — check your permissions.'); return; }
      } else if (!(await ProgressNoteApi.update(clientId, id, form))) {
        setSaveError('Save failed.');
        return;
      }
      if (!(await ProgressNoteApi.finalize(clientId, id, { markAppointmentCompleted: markCompleted }))) {
        setSaveError('Finalize failed.');
        if (noteId == null) navigate(`/clients/${clientId}/progress-notes/${id}`, { replace: true });
        return;
      }
      // navigate last: the page is remounted for the new URL and must load the finalized note
      if (noteId == null) navigate(`/clients/${clientId}/progress-notes/${id}`, { replace: true });
      else await loadNote(id);
    } finally {
      setSaving(false);
    }
  };

  const deleteDraft = async () => {
    if (noteId == null) return;
    if (await ProgressNoteApi.remove(clientId, noteId)) navigate(`/clients/edit/${clientId}`);
  };

  const addAddendum = async () => {
    if (!addendumDraft.trim() || noteId == null || addendumPractitionerId === 0) return;
    setSaving(true);
    try {
      if (await ProgressNoteApi.addAddendum(clientId, noteId, { content: addendumDraft, createdByPractitionerId: addendumPractitionerId })) {
        setAddendumDraft('');
        await loadNote(noteId);
      }
    } finally {
      setSaving(false);
    }
  };

  const createEpisode = async () => {
    if (!newEpisode?.trim()) return;
    const id = await EpisodeApi.create({ clientId, title: newEpisode, description: null });
    if (id != null) {
      setEpisodes(await EpisodeApi.list(clientId));
      set({ episodeId: id });
      setNewEpisode(null);
    }
  };

  const practitionerSelect = (value: number, onChange: (v: number) => void, label?: string, style?: React.CSSProperties, className = 'form-select form-select-sm') => (
    <NumberSelect className={className} style={style} nullable={false} aria-label={label} value={value} onChange={(v) => onChange(v ?? 0)}>
      {label === undefined && <option value={0}>Select...</option>}
      {practitioners.map((p) => <option key={p.id} value={p.id}>{practitionerName(p)}</option>)}
    </NumberSelect>
  );

  return (
    <>
      <h4 className="mb-1">{noteId != null ? 'Progress Note' : 'New Progress Note'}</h4>
      <p className="text-muted small mb-2">{client ? `for ${client.firstName} ${client.lastName}` : ''}</p>
      {client && (
        <ClientSafetyBanner deceased={client.deceased} warnings={client.warnings} interpreterRequired={client.interpreterRequired}
                            interpreterLanguage={client.interpreterLanguage} age={client.age} nextOfKinName={client.nextOfKinName} nextOfKinPhone={client.nextOfKinPhone} />
      )}

      {loading ? (
        <div className="card"><div className="card-body"><LoadingState text="Loading..." compact={false} /></div></div>
      ) : note?.status === ProgressNoteStatus.Finalized ? (
        <>
          <div className="card mb-3">
            <div className="card-header d-flex flex-wrap justify-content-between align-items-center gap-1">
              <strong>{note.reasonForVisit ?? 'Progress Note'} <span className="badge bg-success ms-1">Finalized</span></strong>
              <span className="small text-muted">{formatDate(note.encounterDate, 'd MMM yyyy')} — {note.practitionerName}</span>
            </div>
            <div className="card-body">
              {note.episodeTitle && <div className="mb-2"><span className="badge bg-info text-dark"><i className="bi bi-diagram-3 me-1" />{note.episodeTitle}</span></div>}
              <NoteSection title="Subjective" text={note.subjective} />
              <NoteSection title="Objective" text={note.objective} />
              <NoteSection title="Assessment" text={note.assessment} />
              <NoteSection title="Plan" text={note.plan} />
              {note.reviewDate && <div className="small text-muted"><i className="bi bi-calendar-check me-1" />Review: {formatDate(note.reviewDate, 'd MMM yyyy')}</div>}
            </div>
          </div>
          <div className="card mb-3">
            <div className="card-header"><strong>Addenda</strong></div>
            <div className="card-body">
              {note.addenda.length === 0 ? <p className="text-muted small mb-0">No addenda yet.</p> : note.addenda.map((a) => (
                <div key={a.id} className={`border-start border-3 ps-2 mb-2 ${a.source === ProgressNoteAddendumSource.System ? 'border-secondary' : 'border-primary'}`}>
                  <div className="small text-muted">
                    {formatDate(a.createdAt, 'd MMM yyyy HH:mm')} — {a.createdByPractitionerName}
                    {a.source === ProgressNoteAddendumSource.System && (
                      <><span className="badge bg-secondary ms-1">Auto</span>{a.sourceReferenceType && <span className="ms-1">(via {a.sourceReferenceType})</span>}</>
                    )}
                  </div>
                  <div style={{ whiteSpace: 'pre-wrap' }}>{a.content}</div>
                </div>
              ))}
              <hr />
              <label className="form-label small mb-0">Add addendum</label>
              <textarea className="form-control form-control-sm mb-2" rows={2} value={addendumDraft} onChange={(e) => setAddendumDraft(e.target.value)} />
              {practitionerSelect(addendumPractitionerId, setAddendumPractitionerId, 'Addendum author', { maxWidth: 280 }, 'form-select form-select-sm mb-2')}
              <button type="button" className="btn btn-sm btn-outline-primary" disabled={!addendumDraft.trim() || saving} onClick={addAddendum}>Add Addendum</button>
            </div>
          </div>
          <Link to={`/clients/edit/${clientId}`} className="btn btn-outline-secondary">Back to Client</Link>
        </>
      ) : (
        <>
          {/* summary-flagged problems, read-only, so the active problem list is in view while writing */}
          {summaryIssues.length > 0 && (
            <div className="alert alert-light border small py-2 mb-3">
              <strong className="me-1"><i className="bi bi-clipboard-pulse me-1" />Health issues:</strong>
              {summaryIssues.map((h) => (
                <span key={h.id} className="badge bg-light text-dark border me-1">{h.condition}{h.onsetDisplay ? ` (${h.onsetDisplay})` : ''}</span>
              ))}
            </div>
          )}
          <div className="card mb-3">
            <div className="card-body">
              <div className="row g-2 mb-3">
                <div className="col-md-6"><label className="form-label small mb-0">Reason for Visit</label><TextInput className="form-control form-control-sm" placeholder="e.g. Headache" value={form.reasonForVisit} onChange={(v) => set({ reasonForVisit: v })} /></div>
                <div className="col-md-3"><label className="form-label small mb-0">Encounter Date</label><DateInput className="form-control form-control-sm" value={form.encounterDate} onChange={(v) => v && set({ encounterDate: v })} /></div>
                <div className="col-md-3"><label className="form-label small mb-0">Review Date</label><DateInput className="form-control form-control-sm" value={form.reviewDate} onChange={(v) => set({ reviewDate: v })} /></div>
              </div>
              <div className="mb-2"><label className="form-label small mb-0">Subjective</label><TextArea className="form-control form-control-sm" rows={3} placeholder="History, symptoms as reported by the patient..." value={form.subjective} onChange={(v) => set({ subjective: v })} /></div>
              <div className="mb-2"><label className="form-label small mb-0">Objective</label><TextArea className="form-control form-control-sm" rows={3} placeholder="Examination findings, observations..." value={form.objective} onChange={(v) => set({ objective: v })} /></div>
              <div className="mb-2"><label className="form-label small mb-0">Assessment</label><TextArea className="form-control form-control-sm" rows={3} placeholder="Diagnosis / clinical impression..." value={form.assessment} onChange={(v) => set({ assessment: v })} /></div>
              <div className="mb-3"><label className="form-label small mb-0">Plan</label><TextArea className="form-control form-control-sm" rows={3} placeholder="Management, follow-up..." value={form.plan} onChange={(v) => set({ plan: v })} /></div>
              <div className="row g-2 mb-3">
                <div className="col-md-6">
                  <label className="form-label small mb-0">Episode</label>
                  {newEpisode !== null ? (
                    <div className="input-group input-group-sm">
                      <input className="form-control" placeholder="New episode title" autoFocus value={newEpisode} onChange={(e) => setNewEpisode(e.target.value)} />
                      <button type="button" className="btn btn-outline-primary" onClick={createEpisode}>Add</button>
                      <button type="button" className="btn btn-outline-secondary" onClick={() => setNewEpisode(null)}>Cancel</button>
                    </div>
                  ) : (
                    <div className="d-flex">
                      <NumberSelect className="form-select form-select-sm" value={form.episodeId} onChange={(v) => set({ episodeId: v })}>
                        <option value="">-- None (single visit) --</option>
                        {episodes.map((ep) => <option key={ep.id} value={ep.id}>{ep.title}</option>)}
                      </NumberSelect>
                      <button type="button" className="btn btn-sm btn-link" onClick={() => setNewEpisode('')}>+ New</button>
                    </div>
                  )}
                </div>
                <div className="col-md-6">
                  <label className="form-label small mb-0">Practitioner</label>
                  {practitionerSelect(form.practitionerId, (v) => set({ practitionerId: v }))}
                </div>
              </div>
              {note && note.addenda.length > 0 && (
                <div className="mb-3">
                  <label className="form-label small mb-0">Logged during this visit</label>
                  {note.addenda.map((a) => (
                    <div key={a.id} className="small text-muted border-start border-3 border-secondary ps-2 mb-1">
                      {formatDate(a.createdAt, 'HH:mm')} — {a.content}
                      {a.source === ProgressNoteAddendumSource.System && <span className="badge bg-secondary ms-1">Auto</span>}
                    </div>
                  ))}
                </div>
              )}
              {hasLinkedAppointment && (
                <div className="form-check mb-3">
                  <input className="form-check-input" type="checkbox" id="chkMarkCompleted" checked={markCompleted} onChange={(e) => setMarkCompleted(e.target.checked)} />
                  <label className="form-check-label small" htmlFor="chkMarkCompleted">Also mark the appointment as Completed</label>
                </div>
              )}
              {saveError && <div className="small text-danger mb-2">{saveError}</div>}
              <div className="d-flex flex-wrap gap-1">
                <button type="button" className="btn btn-outline-primary" disabled={!canSave || saving} onClick={saveDraft}>
                  {saving && <span className="spinner-border spinner-border-sm me-1" />} Save Draft
                </button>
                <button type="button" className="btn btn-primary" disabled={!canSave || saving} onClick={finalize}><i className="bi bi-check-circle me-1" /> Finalize &amp; Sign</button>
                {noteId != null && <button type="button" className="btn btn-outline-danger" disabled={saving} onClick={deleteDraft}>Delete Draft</button>}
                <Link to={`/clients/edit/${clientId}`} className="btn btn-outline-secondary">Cancel</Link>
              </div>
            </div>
          </div>
        </>
      )}
    </>
  );
}
