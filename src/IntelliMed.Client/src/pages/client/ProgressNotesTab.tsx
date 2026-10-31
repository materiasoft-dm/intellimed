import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { AppointmentApi } from '@/api/appointments';
import { EpisodeApi } from '@/api/clients';
import { ProgressNoteApi } from '@/api/clinical';
import {
  AppointmentStatus, EpisodeStatus, ProgressNoteAddendumSource, ProgressNoteStatus, type AppointmentDto, type EpisodeDto, type ProgressNoteDto,
} from '@/api/types.gen';
import { EmptyState, LoadingState } from '@/components/States';
import { formatDate, toDate } from '@/lib/dates';
import { useTabData } from './shared';

export function ProgressNotesTab({ clientId }: { clientId: number }) {
  const navigate = useNavigate();
  const { data, loading, reload } = useTabData(async () => {
    const [notes, episodes] = await Promise.all([ProgressNoteApi.listForClient(clientId), EpisodeApi.list(clientId, true)]);
    return { notes, episodes };
  }, { notes: [] as ProgressNoteDto[], episodes: [] as EpisodeDto[] });
  const [showEpisodes, setShowEpisodes] = useState(false);
  const [newEpisode, setNewEpisode] = useState<string | null>(null);

  const createEpisode = async () => {
    if (!newEpisode?.trim()) return;
    await EpisodeApi.create({ clientId, title: newEpisode, description: null });
    setNewEpisode(null);
    reload();
  };

  return (
    <div>
      <div className="d-flex justify-content-between mb-1">
        <button type="button" className="btn btn-sm btn-outline-secondary" aria-expanded={showEpisodes} onClick={() => setShowEpisodes((s) => !s)}>
          {showEpisodes ? 'Hide' : 'Manage'} Episodes
        </button>
        <Link to={`/clients/${clientId}/progress-notes/new`} className="btn btn-sm btn-outline-primary">+ New</Link>
      </div>

      {showEpisodes && (
        <div className="border rounded p-2 mb-2 bg-light">
          {data.episodes.length === 0 ? <p className="text-muted small mb-0">No episodes yet.</p> : data.episodes.map((ep) => (
            <div key={ep.id} className="d-flex justify-content-between align-items-center small mb-1">
              <span>{ep.title} <span className={`badge ${ep.status === EpisodeStatus.Active ? 'bg-success' : 'bg-secondary'} ms-1`}>{EpisodeStatus[ep.status]}</span></span>
              {ep.status === EpisodeStatus.Active
                ? <button type="button" className="btn btn-sm btn-outline-warning" onClick={async () => { await EpisodeApi.resolve(clientId, ep.id); reload(); }}>Resolve</button>
                : <button type="button" className="btn btn-sm btn-outline-secondary" onClick={async () => { await EpisodeApi.reopen(clientId, ep.id); reload(); }}>Reopen</button>}
            </div>
          ))}
          {newEpisode !== null ? (
            <div className="input-group input-group-sm mt-2">
              <input className="form-control" placeholder="New episode title" autoFocus value={newEpisode} onChange={(e) => setNewEpisode(e.target.value)}
                     onKeyDown={(e) => { if (e.key === 'Enter') createEpisode(); }} />
              <button type="button" className="btn btn-outline-primary" onClick={createEpisode}>Add</button>
              <button type="button" className="btn btn-outline-secondary" onClick={() => setNewEpisode(null)}>Cancel</button>
            </div>
          ) : (
            <button type="button" className="btn btn-sm btn-link px-0 mt-1" onClick={() => setNewEpisode('')}>+ New Episode</button>
          )}
        </div>
      )}

      {loading && data.notes.length === 0 ? <LoadingState text="Loading progress notes..." /> : data.notes.length === 0 ? (
        <EmptyState message="No progress notes recorded." icon="bi-journal-text" />
      ) : (
        <div className="table-responsive">
          <table className="table table-sm table-hover">
            <thead><tr><th>Date</th><th>Reason</th><th>Practitioner</th><th>Episode</th><th>Status</th></tr></thead>
            <tbody>
              {data.notes.map((n) => (
                <tr key={n.id} className="imed-row-clickable" onClick={() => navigate(`/clients/${clientId}/progress-notes/${n.id}`)}>
                  <td className="text-nowrap">{formatDate(n.encounterDate, 'd MMM yyyy')}</td>
                  <td>{n.reasonForVisit}</td>
                  <td>{n.practitionerName}</td>
                  <td>{n.episodeTitle}</td>
                  <td><span className={`badge ${n.status === ProgressNoteStatus.Finalized ? 'bg-success' : 'bg-secondary'}`}>{ProgressNoteStatus[n.status]}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

interface HistoryEntry { date: Date; kind: string; title: string; detail: string | null; borderClass: string; linkUrl: string | null }

/**
 * Unified timeline: progress notes (+ addenda), episodes opened/resolved and completed appointments, newest
 * first. No storage of its own — merged client-side from the same endpoints the other tabs use.
 */
export function PatientHistoryTab({ clientId }: { clientId: number }) {
  const { data, loading } = useTabData(async () => {
    const [notes, episodes, appts] = await Promise.all([
      ProgressNoteApi.listForClient(clientId),
      EpisodeApi.list(clientId, true),
      AppointmentApi.search({ clientId, status: AppointmentStatus.Completed, pageSize: 100 }),
    ]);
    return { notes, episodes, appointments: appts.items };
  }, { notes: [] as ProgressNoteDto[], episodes: [] as EpisodeDto[], appointments: [] as AppointmentDto[] });

  const entries: HistoryEntry[] = [];
  for (const note of data.notes) {
    const url = `/clients/${clientId}/progress-notes/${note.id}`;
    entries.push({
      date: toDate(note.encounterDate)!, kind: 'Progress Note', title: note.reasonForVisit ?? '(no reason recorded)',
      detail: note.episodeTitle != null ? `${note.practitionerName} — ${note.episodeTitle}` : note.practitionerName,
      borderClass: note.status === ProgressNoteStatus.Finalized ? 'border-success' : 'border-secondary', linkUrl: url,
    });
    for (const a of note.addenda) {
      const system = a.source === ProgressNoteAddendumSource.System;
      entries.push({ date: toDate(a.createdAt)!, kind: system ? 'Auto-logged' : 'Addendum', title: a.content, detail: a.createdByPractitionerName, borderClass: system ? 'border-secondary' : 'border-primary', linkUrl: url });
    }
  }
  for (const ep of data.episodes) {
    entries.push({ date: toDate(ep.openedDate)!, kind: 'Episode Opened', title: ep.title, detail: ep.description, borderClass: 'border-info', linkUrl: null });
    if (ep.closedDate) entries.push({ date: toDate(ep.closedDate)!, kind: 'Episode Resolved', title: ep.title, detail: null, borderClass: 'border-success', linkUrl: null });
  }
  for (const a of data.appointments) {
    entries.push({ date: toDate(a.appointmentDate)!, kind: 'Appointment Completed', title: a.typeName, detail: a.practitionerName, borderClass: 'border-success', linkUrl: null });
  }
  entries.sort((a, b) => b.date.getTime() - a.date.getTime());

  if (loading) return <LoadingState text="Loading history..." />;
  if (entries.length === 0) return <EmptyState message="No recorded history yet." icon="bi-clock-history" />;
  return (
    <div className="imed-timeline">
      {entries.map((e, i) => (
        <div key={i} className={`border-start border-3 ${e.borderClass} ps-2 mb-2`}>
          <div className="small text-muted">{formatDate(e.date, 'd MMM yyyy')} — <strong>{e.kind}</strong>{e.detail ? ` · ${e.detail}` : ''}</div>
          <div>{e.linkUrl ? <Link to={e.linkUrl}>{e.title}</Link> : e.title}</div>
        </div>
      ))}
    </div>
  );
}
