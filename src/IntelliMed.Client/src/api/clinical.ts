import { getBlob, getOr, getOrNull, http, ok, qs, readError, readJson, sendFor } from './http';
import type {
  AddProgressNoteAddendumDto, ArtgEnrichmentResultDto, CheckPrescriptionSafetyDto, CreatedResultIngestKeyDto,
  CreateDrugInteractionDto, CreateMedicineDto, CreatePrescriptionDto, CreateProgressNoteDto, DrugInteractionDto,
  DrugInteractionImportResultDto, FinalizeProgressNoteDto, IngestResultOutcomeDto, IssuePrescriptionDto,
  MedicationSafetyCheckResultDto, MedicineDto, MedicineImportResultDto, MedicineSource, PagedResult, PrescriptionDto,
  PrescriptionIssueDto, ProgressNoteDto, ResultCountsDto, ResultDetailDto, ResultIngestKeyDto, ResultStatus,
  ResultSummaryDto, SyncProgressDto, UpdateDrugInteractionDto, UpdateMedicineDto, UpdatePrescriptionDto,
  UpdateProgressNoteDto,
} from './types.gen';
import { emptyPage } from './clients';

const upload = (file: File, contentType?: string) => {
  const form = new FormData();
  form.append('file', contentType ? new File([file], file.name, { type: contentType }) : file, file.name);
  return form;
};

export const MedicineApi = {
  search: (query: string, source?: MedicineSource | null) =>
    getOr<MedicineDto[]>(`api/medicines/search${qs({ query: query ?? '', source })}`, []),
  /** One page of the catalogue list (the plain search returns only the first 20). */
  searchPage: (query: string, source: MedicineSource | null, skip: number, take: number) =>
    getOr<MedicineDto[]>(`api/medicines/search${qs({ query: query ?? '', source, skip, take })}`, []),
  enrichFromArtg: (file: File) => sendFor<ArtgEnrichmentResultDto>('POST', 'api/medicines/enrich-artg', upload(file)),
  importCsv: (file: File) => sendFor<MedicineImportResultDto>('POST', 'api/medicines/import', upload(file)),
  exportCsv: () => getBlob('api/medicines/export'),
  create: (dto: CreateMedicineDto) => sendFor<MedicineDto>('POST', 'api/medicines', dto),
  update: (id: number, dto: UpdateMedicineDto) => ok('PUT', `api/medicines/${id}`, dto),
  deactivate: (id: number) => ok('POST', `api/medicines/${id}/deactivate`),
  cancelSync: (jobId: string) => ok('POST', `api/medicines/sync/${jobId}/cancel`),

  /**
   * Starts a catalogue sync and polls it every 500 ms, reporting each new log line. Resolves with the final
   * state, or null on failure. A 404 while polling means the API restarted and lost the (in-memory) job —
   * that throws, so the page can say exactly that instead of a generic permissions message.
   */
  async sync(onLog: (line: string) => void, onJobStarted: (jobId: string) => void): Promise<SyncProgressDto | null> {
    let lastLogCount = 0;
    try {
      const start = await http('POST', 'api/medicines/sync');
      if (!start.ok) return null;
      const jobId = (await readJson<{ jobId: string }>(start))?.jobId;
      if (!jobId) return null;
      onJobStarted(jobId);
      for (;;) {
        await new Promise((r) => setTimeout(r, 500));
        const poll = await http('GET', `api/medicines/sync/${jobId}/progress`);
        if (poll.status === 404) {
          throw new SyncLostError('The sync job was lost — the server may have restarted while it was running. Please try again.');
        }
        if (!poll.ok) return null;
        const state = await readJson<SyncProgressDto>(poll);
        if (!state) return null;
        for (let i = lastLogCount; i < state.log.length; i++) onLog(state.log[i]);
        lastLogCount = state.log.length;
        if (state.isComplete) return state;
      }
    } catch (e) {
      if (e instanceof SyncLostError) throw e;
      console.error('Sync medicines error', e);
      return null;
    }
  },
};

export class SyncLostError extends Error {}

export const DrugInteractionApi = {
  list: () => getOr<DrugInteractionDto[]>('api/drug-interactions', []),
  create: (dto: CreateDrugInteractionDto) => ok('POST', 'api/drug-interactions', dto),
  update: (id: number, dto: UpdateDrugInteractionDto) => ok('PUT', `api/drug-interactions/${id}`, dto),
  importCsv: (file: File) => sendFor<DrugInteractionImportResultDto>('POST', 'api/drug-interactions/import', upload(file)),
  exportCsv: () => getBlob('api/drug-interactions/export'),
};

export const PrescriptionApi = {
  listForClient: (clientId: number, includeInactive = false) =>
    getOr<PrescriptionDto[]>(`api/clients/${clientId}/prescriptions${qs({ includeInactive })}`, []),
  checkSafety: (clientId: number, dto: CheckPrescriptionSafetyDto) =>
    sendFor<MedicationSafetyCheckResultDto>('POST', `api/clients/${clientId}/prescriptions/check-safety`, dto),
  async create(dto: CreatePrescriptionDto): Promise<number | null> {
    return (await sendFor<{ id: number }>('POST', `api/clients/${dto.clientId}/prescriptions`, dto))?.id ?? null;
  },
  update: (clientId: number, id: number, dto: UpdatePrescriptionDto) => ok('PUT', `api/clients/${clientId}/prescriptions/${id}`, dto),
  cease: (clientId: number, id: number) => ok('POST', `api/clients/${clientId}/prescriptions/${id}/cease`),
  cancel: (clientId: number, id: number) => ok('POST', `api/clients/${clientId}/prescriptions/${id}/cancel`),
  issues: (clientId: number, id: number) => getOr<PrescriptionIssueDto[]>(`api/clients/${clientId}/prescriptions/${id}/issues`, []),
  async issue(clientId: number, id: number, dto: IssuePrescriptionDto): Promise<number | null> {
    return (await sendFor<{ issueId: number }>('POST', `api/clients/${clientId}/prescriptions/${id}/issue`, dto))?.issueId ?? null;
  },
  getIssue: (issueId: number) => getOrNull<PrescriptionIssueDto>(`api/prescription-issues/${issueId}`),
};

export const ProgressNoteApi = {
  listForClient: (clientId: number) => getOr<ProgressNoteDto[]>(`api/clients/${clientId}/progress-notes`, []),
  getById: (clientId: number, id: number) => getOrNull<ProgressNoteDto>(`api/clients/${clientId}/progress-notes/${id}`),
  async create(dto: CreateProgressNoteDto): Promise<number | null> {
    return (await sendFor<{ id: number }>('POST', `api/clients/${dto.clientId}/progress-notes`, dto))?.id ?? null;
  },
  update: (clientId: number, id: number, dto: UpdateProgressNoteDto) => ok('PUT', `api/clients/${clientId}/progress-notes/${id}`, dto),
  finalize: (clientId: number, id: number, dto: FinalizeProgressNoteDto) => ok('POST', `api/clients/${clientId}/progress-notes/${id}/finalize`, dto),
  addAddendum: (clientId: number, id: number, dto: AddProgressNoteAddendumDto) => ok('POST', `api/clients/${clientId}/progress-notes/${id}/addenda`, dto),
  remove: (clientId: number, id: number) => ok('DELETE', `api/clients/${clientId}/progress-notes/${id}`),
};

export interface ResultSearch {
  status?: ResultStatus | null;
  query?: string | null;
  abnormalOnly?: boolean;
  mine?: boolean;
  clientId?: number | null;
  page?: number;
  pageSize?: number;
}

export const ResultApi = {
  search(s: ResultSearch): Promise<PagedResult<ResultSummaryDto>> {
    const query = qs({
      page: s.page ?? 1,
      pageSize: s.pageSize ?? 25,
      status: s.status,
      query: s.query && s.query.trim() ? s.query : undefined,
      abnormalOnly: s.abnormalOnly ? true : undefined,
      mine: s.mine ? true : undefined,
      clientId: s.clientId,
    });
    return getOr(`api/results${query}`, emptyPage<ResultSummaryDto>());
  },
  counts: (mine: boolean) =>
    getOr<ResultCountsDto>(`api/results/counts${qs({ mine })}`, { inbox: 0, unmatched: 0, held: 0, inboxAbnormal: 0 }),
  getById: (id: number) => getOrNull<ResultDetailDto>(`api/results/${id}`),
  async upload(file: File): Promise<{ outcome: IngestResultOutcomeDto | null; error: string | null }> {
    try {
      const res = await http('POST', 'api/results/upload', upload(file, 'text/plain'));
      if (res.ok) return { outcome: await readJson<IngestResultOutcomeDto>(res), error: null };
      return { outcome: null, error: await readError(res, 'The file could not be uploaded.') };
    } catch {
      return { outcome: null, error: 'The file could not be uploaded.' };
    }
  },
  match: (id: number, clientId: number, rememberLabIdentifier: boolean) =>
    ok('POST', `api/results/${id}/match`, { clientId, rememberLabIdentifier }),
  acknowledge: (id: number, comment: string | null) => ok('POST', `api/results/${id}/acknowledge`, { comment }),
  hold: (id: number, note: string | null) => ok('POST', `api/results/${id}/hold`, { note }),
  reopen: (id: number) => ok('POST', `api/results/${id}/reopen`),
  attachment: (reportId: number, attachmentId: number) => getBlob(`api/results/${reportId}/attachments/${attachmentId}`),
  ingestKeys: () => getOr<ResultIngestKeyDto[]>('api/results/ingest-keys', []),
  createIngestKey: (label: string) => sendFor<CreatedResultIngestKeyDto>('POST', 'api/results/ingest-keys', { label }),
  revokeIngestKey: (id: number) => ok('DELETE', `api/results/ingest-keys/${id}`),
};
