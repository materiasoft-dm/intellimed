import { getJson, getOr, getOrNull, http, ok, qs, readJson, sendFor, sendWithError } from './http';
import { emptyPage } from './clients';
import { dateParam } from '@/lib/dates';
import type {
  AppointmentDto, AppointmentSearchDto, AppointmentStatus, AppointmentTypeSettingDto, CreateAppointmentDto,
  CreateAppointmentSeriesDto, CreateAppointmentTypeSettingDto, PagedResult, PractitionerDto, ProviderScheduleDayDto,
  RescheduleAppointmentDto, UpdateAppointmentDto, UpdateAppointmentTypeSettingDto,
} from './types.gen';

type IdResult = { id: number };

export const AppointmentApi = {
  search(search: Partial<AppointmentSearchDto>): Promise<PagedResult<AppointmentDto>> {
    const query = qs({
      page: search.page ?? 1,
      pageSize: search.pageSize ?? 25,
      clientId: search.clientId,
      practitionerId: search.practitionerId,
      status: search.status,
      fromDate: dateParam(search.fromDate),
      toDate: dateParam(search.toDate),
    });
    return getOr(`api/appointments/search${query}`, emptyPage<AppointmentDto>());
  },
  waitingRoom: (date?: string | Date | null) =>
    getOr<AppointmentDto[]>(`api/appointments/waiting-room${qs({ date: dateParam(date) })}`, []),
  getById: (id: number) => getOrNull<AppointmentDto>(`api/appointments/${id}`),
  series: (seriesId: string) => getOr<AppointmentDto[]>(`api/appointments/series/${seriesId}`, []),

  /** Fails open, like Blazor: a transient check failure must not block booking. */
  async checkAvailability(practitionerId: number, date: string | Date, startTime: string, endTime: string, excludeAppointmentId?: number | null): Promise<boolean> {
    try {
      return await getJson<boolean>(`api/appointments/availability${qs({
        practitionerId, date: dateParam(date), startTime, endTime, excludeAppointmentId,
      })}`);
    } catch {
      return true;
    }
  },

  practitionerSchedule: (practitionerId: number, date: string | Date) =>
    getOrNull<ProviderScheduleDayDto>(`api/appointments/practitioner-schedule${qs({ practitionerId, date: dateParam(date) })}`),

  async create(dto: CreateAppointmentDto): Promise<number | null> {
    return (await sendFor<IdResult>('POST', 'api/appointments', dto))?.id ?? null;
  },
  createSeries: (dto: CreateAppointmentSeriesDto) => sendFor<number[]>('POST', 'api/appointments/series', dto),
  update: (id: number, dto: UpdateAppointmentDto) => ok('PUT', `api/appointments/${id}`, dto),
  updateStatus: (id: number, status: AppointmentStatus) => ok('PUT', `api/appointments/${id}/status`, { status }),
  reschedule: (id: number, dto: RescheduleAppointmentDto) => sendWithError('PUT', `api/appointments/${id}/reschedule`, dto),
  remove: (id: number) => ok('DELETE', `api/appointments/${id}`),
  async removeSeries(seriesId: string, fromDate?: string | Date | null): Promise<number> {
    try {
      const res = await http('DELETE', `api/appointments/series/${seriesId}${qs({ fromDate: dateParam(fromDate) })}`);
      return res.ok ? ((await readJson<number>(res)) ?? 0) : 0;
    } catch {
      return 0;
    }
  },
};

export const AppointmentTypeApi = {
  list: () => getOr<AppointmentTypeSettingDto[]>('api/appointment-type-settings', []),
  async create(dto: CreateAppointmentTypeSettingDto): Promise<number | null> {
    return (await sendFor<IdResult>('POST', 'api/appointment-type-settings', dto))?.id ?? null;
  },
  update: (id: number, dto: UpdateAppointmentTypeSettingDto) => ok('PUT', `api/appointment-type-settings/${id}`, dto),
  archive: (id: number) => ok('POST', `api/appointment-type-settings/${id}/archive`),
};

export const PractitionerApi = {
  active: () => getOr<PractitionerDto[]>('api/practitioners/active', []),
};
