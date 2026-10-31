import { storage, StorageKeys } from './storage';

/**
 * Every API call goes through here. Like the Blazor AuthHeaderHandler it reads the token and the current
 * clinic fresh from storage on each request (a page's first fetch can run before the shell has restored
 * the session), and turns a 401 anywhere but the login call into "token is dead": clear it and go to /login.
 */
export async function http(method: string, url: string, body?: unknown, init?: RequestInit): Promise<Response> {
  const headers = new Headers(init?.headers);
  const token = storage.get(StorageKeys.token);
  if (token) headers.set('Authorization', `Bearer ${token}`);
  const clinicId = storage.get(StorageKeys.clinicId);
  if (clinicId) headers.set('X-Clinic-Id', clinicId);

  let payload: BodyInit | undefined;
  if (body instanceof FormData) {
    payload = body;
  } else if (body !== undefined) {
    headers.set('Content-Type', 'application/json');
    payload = JSON.stringify(body);
  }

  const res = await fetch(resolve(url), { ...init, method, headers, body: payload });

  if (res.status === 401 && !url.toLowerCase().split('?')[0].endsWith('api/auth/login')) {
    storage.set(StorageKeys.token, '');
    if (window.location.pathname !== '/login') window.location.assign('/login');
  }
  return res;
}

function resolve(url: string): string {
  return /^(https?:)?\//.test(url) ? url : `/${url}`;
}

export class HttpError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message || `Request failed (${status})`);
  }
}

/** Parses a JSON body; an empty body (204, or an action returning nothing) is null. */
export async function readJson<T>(res: Response): Promise<T | null> {
  const text = await res.text();
  if (!text) return null;
  try {
    return JSON.parse(text) as T;
  } catch {
    return text as unknown as T;
  }
}

/** The human-readable reason in an error response: a ProblemDetails title/detail, a { message }, or the text. */
export async function readError(res: Response, fallback = 'Something went wrong. Please try again.'): Promise<string> {
  try {
    const text = (await res.text()).trim();
    if (!text) return fallback;
    try {
      const json = JSON.parse(text);
      if (typeof json === 'string') return json || fallback;
      const errors = json?.errors && typeof json.errors === 'object'
        ? Object.values(json.errors as Record<string, string[]>).flat().join(' ')
        : '';
      return json?.message || json?.Message || json?.detail || errors || json?.title || fallback;
    } catch {
      return text.replace(/^"|"$/g, '') || fallback;
    }
  } catch {
    return fallback;
  }
}

// ---- the five shapes the Blazor services used, so pages keep the same failure behaviour ----

/** GET that throws on failure (GetFromJsonAsync). */
export async function getJson<T>(url: string): Promise<T> {
  const res = await http('GET', url);
  if (!res.ok) throw new HttpError(res.status, await readError(res));
  return (await readJson<T>(res)) as T;
}

/** GET, falling back to a default on any failure (the services' try/catch → empty list). */
export async function getOr<T>(url: string, fallback: T): Promise<T> {
  try {
    return (await getJson<T>(url)) ?? fallback;
  } catch (e) {
    logFailure('GET', url, e);
    return fallback;
  }
}

/** GET, null when the request fails or the record is missing. */
export async function getOrNull<T>(url: string): Promise<T | null> {
  try {
    const res = await http('GET', url);
    if (!res.ok || res.status === 204) return null;
    return await readJson<T>(res);
  } catch (e) {
    logFailure('GET', url, e);
    return null;
  }
}

/** A write whose only outcome is success/failure. */
export async function ok(method: 'POST' | 'PUT' | 'DELETE', url: string, body?: unknown): Promise<boolean> {
  try {
    return (await http(method, url, body)).ok;
  } catch (e) {
    logFailure(method, url, e);
    return false;
  }
}

/** A write that returns a body on success, null on failure. */
export async function sendFor<T>(method: 'POST' | 'PUT' | 'DELETE', url: string, body?: unknown): Promise<T | null> {
  try {
    const res = await http(method, url, body);
    if (!res.ok) return null;
    return await readJson<T>(res);
  } catch (e) {
    logFailure(method, url, e);
    return null;
  }
}

/** A write whose body describes the outcome either way (UserManagementResponse and friends). */
export async function sendResult<T>(method: 'POST' | 'PUT' | 'DELETE', url: string, body: unknown, fallback: T): Promise<T> {
  try {
    const res = await http(method, url, body);
    return (await readJson<T>(res)) ?? fallback;
  } catch (e) {
    logFailure(method, url, e);
    return fallback;
  }
}

/** A write that reports why it failed: { ok, error }. */
export async function sendWithError(method: 'POST' | 'PUT' | 'DELETE', url: string, body?: unknown, fallback?: string): Promise<{ ok: boolean; error: string | null }> {
  try {
    const res = await http(method, url, body);
    if (res.ok) return { ok: true, error: null };
    return { ok: false, error: await readError(res, fallback) };
  } catch (e) {
    logFailure(method, url, e);
    return { ok: false, error: fallback ?? 'Connection error. Please try again.' };
  }
}

/** A file download (CSV export, backup, attachment). */
export async function getBlob(url: string): Promise<Blob | null> {
  try {
    const res = await http('GET', url);
    return res.ok ? await res.blob() : null;
  } catch (e) {
    logFailure('GET', url, e);
    return null;
  }
}

/** A query value the way the Blazor services sent it: skipped when blank, otherwise as typed. */
export const nb = (value: string | null | undefined): string | undefined =>
  value && value.trim() ? value : undefined;

/** Query string from an object: skips null/undefined/'' and repeats the key for arrays. */
export function qs(params: Record<string, unknown>): string {
  const parts: string[] = [];
  for (const [key, value] of Object.entries(params)) {
    if (value === null || value === undefined || value === '') continue;
    const values = Array.isArray(value) ? value : [value];
    for (const v of values) {
      if (v === null || v === undefined || v === '') continue;
      parts.push(`${encodeURIComponent(key)}=${encodeURIComponent(String(v))}`);
    }
  }
  return parts.length ? `?${parts.join('&')}` : '';
}

function logFailure(method: string, url: string, e: unknown) {
  console.error(`[api] ${method} ${url} failed`, e);
}
