import { getBlob, getOr, getOrNull, http, ok, readJson } from './http';
import type {
  ClinicSettingsDto, DatabaseBackupDto, DatabaseBackupSettingsDto, EmailTemplateDto, NotificationDto,
  SaveEmailTemplateRequest, SaveSearchActionRequest, SearchActionDto, SystemSettingsDto, TestEmailRequest,
  UnreadCountDto, UpdateClinicSettingsRequest, UpdateDatabaseBackupSettingsDto, UpdateSystemSettingsDto,
} from './types.gen';

export const ClinicSettingsApi = {
  get: () => getOrNull<ClinicSettingsDto>('api/clinic-settings'),
  update: (request: UpdateClinicSettingsRequest) => ok('PUT', 'api/clinic-settings', request),
  async sendTestEmail(request: TestEmailRequest): Promise<{ success: boolean; message: string | null }> {
    try {
      const res = await http('POST', 'api/clinic-settings/test-email', request);
      const body = await readJson<{ message?: string }>(res);
      return { success: res.ok, message: body?.message ?? null };
    } catch {
      return { success: false, message: 'Connection error.' };
    }
  },
};

export const SystemSettingsApi = {
  get: () => getOrNull<SystemSettingsDto>('api/system-settings'),
  /** null on success, otherwise the reason it failed. */
  async update(dto: UpdateSystemSettingsDto): Promise<string | null> {
    const res = await http('PUT', 'api/system-settings', dto);
    if (res.ok) return null;
    try {
      const problem = await readJson<{ message?: string }>(res);
      if (problem && typeof problem === 'object' && problem.message?.trim()) return problem.message;
    } catch {
      // not a JSON body (a 403 from the permission check, for instance)
    }
    return res.status === 403 ? "You don't have permission to change system settings." : 'Save failed. Please try again.';
  },
};

export const DatabaseBackupApi = {
  list: () => getOr<DatabaseBackupDto[]>('api/database-backups', []),
  getSettings: () => getOrNull<DatabaseBackupSettingsDto>('api/database-backups/settings'),
  updateSettings: (dto: UpdateDatabaseBackupSettingsDto) => ok('PUT', 'api/database-backups/settings', dto),
  backupNow: () => ok('POST', 'api/database-backups/backup-now'),
  download: (fileName: string) => getBlob(`api/database-backups/${encodeURIComponent(fileName)}/download`),
  remove: (fileName: string) => ok('DELETE', `api/database-backups/${encodeURIComponent(fileName)}`),
};

export const EmailTemplateApi = {
  list: () => getOrNull<EmailTemplateDto[]>('api/email-templates'),
  getById: (id: number) => getOrNull<EmailTemplateDto>(`api/email-templates/${id}`),
  create: (request: SaveEmailTemplateRequest) => ok('POST', 'api/email-templates', request),
  update: (id: number, request: SaveEmailTemplateRequest) => ok('PUT', `api/email-templates/${id}`, request),
  archive: (id: number) => ok('POST', `api/email-templates/${id}/archive`),
};

export const SearchActionApi = {
  list: () => getOrNull<SearchActionDto[]>('api/search-actions'),
  getById: (id: number) => getOrNull<SearchActionDto>(`api/search-actions/${id}`),
  create: (request: SaveSearchActionRequest) => ok('POST', 'api/search-actions', request),
  update: (id: number, request: SaveSearchActionRequest) => ok('PUT', `api/search-actions/${id}`, request),
  archive: (id: number) => ok('POST', `api/search-actions/${id}/archive`),
};

export const NotificationApi = {
  recent: (take = 20) => getOr<NotificationDto[]>(`api/notifications?take=${take}`, []),
  async unreadCount(): Promise<number> {
    return (await getOrNull<UnreadCountDto>('api/notifications/unread-count'))?.count ?? 0;
  },
  markRead: (id: number) => ok('PUT', `api/notifications/${id}/read`, {}),
  markAllRead: () => ok('PUT', 'api/notifications/read-all', {}),
};
