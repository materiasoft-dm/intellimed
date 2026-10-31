import { http, getOrNull, getOr, ok, readJson, sendResult } from './http';
import { storage, StorageKeys } from './storage';
import type {
  CurrentUserResponse, LoginResponse, ProviderScheduleDayDto, SetProviderScheduleRequest, UpdateProfileRequest,
  UserManagementResponse, UserProfileDto, VersionDto,
} from './types.gen';

const connectionError = (message = 'Connection error. Please try again.'): UserManagementResponse =>
  ({ success: false, message }) as UserManagementResponse;

export const AuthApi = {
  async login(email: string, password: string): Promise<LoginResponse> {
    try {
      const res = await http('POST', 'api/auth/login', { email, password, rememberMe: true });
      const result = await readJson<LoginResponse>(res);
      if (res.ok && result?.success && result.token) {
        storage.set(StorageKeys.token, result.token);
        storage.set(StorageKeys.userEmail, result.email ?? '');
        storage.set(StorageKeys.userName, result.fullName ?? '');
        return result;
      }
      return result ?? ({ success: false, errorMessage: 'Login failed' } as LoginResponse);
    } catch {
      return { success: false, errorMessage: 'Connection error. Please try again.' } as LoginResponse;
    }
  },

  async logout(): Promise<void> {
    try {
      await http('POST', 'api/auth/logout');
    } catch {
      // clear local state regardless
    } finally {
      storage.set(StorageKeys.token, '');
      storage.set(StorageKeys.userEmail, '');
      storage.set(StorageKeys.userName, '');
    }
  },

  isAuthenticated: () => !!storage.get(StorageKeys.token),
  currentUserEmail: () => storage.get(StorageKeys.userEmail),
  currentUserName: () => storage.get(StorageKeys.userName),

  async me(): Promise<CurrentUserResponse> {
    return (await getOrNull<CurrentUserResponse>('api/auth/me')) ?? ({ isAuthenticated: false } as CurrentUserResponse);
  },

  getProfile: () => getOrNull<UserProfileDto>('api/auth/me/profile'),
  updateProfile: (request: UpdateProfileRequest) => ok('PUT', 'api/auth/me/profile', request),
  getSchedule: () => getOr<ProviderScheduleDayDto[]>('api/auth/me/schedule', []),
  setSchedule: (request: SetProviderScheduleRequest) => ok('PUT', 'api/auth/me/schedule', request),

  forgotPassword: (email: string) =>
    sendResult('POST', 'api/auth/me/forgot-password', { email }, connectionError()),
  setPassword: (userId: string, token: string, newPassword: string) =>
    sendResult('POST', 'api/auth/me/set-password', { userId, token, newPassword }, connectionError()),
};

export const VersionApi = {
  async get(): Promise<string> {
    return (await getOrNull<VersionDto>('api/version'))?.version ?? 'unknown';
  },
};

/** Best-effort: errors are logged to the console and, if possible, to the API's log. */
export const LogApi = {
  error(error: unknown, context?: string) {
    const e = error instanceof Error ? error : new Error(String(error));
    console.error(context ? `[${context}]` : '', e);
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 3000);
    http('POST', 'api/log/frontend-error', {
      message: e.message,
      stackTrace: e.stack ?? null,
      source: 'IntelliMed.Client',
      context: context ?? null,
      timestamp: new Date().toISOString(),
      userAgent: navigator.userAgent,
      url: window.location.href,
    }, { signal: controller.signal }).catch(() => undefined).finally(() => clearTimeout(timer));
  },
};
