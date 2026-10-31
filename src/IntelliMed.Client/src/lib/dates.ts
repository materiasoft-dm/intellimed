// Dates cross the API as local wall-clock strings ("2026-10-31T09:30:00", no zone) — the server stores
// clinic-local times. Parse them as local, never as UTC, or every time shifts by the browser's offset.

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

const pad = (n: number, width = 2) => String(n).padStart(width, '0');

/** A Date from an API date string (local unless it carries Z/offset), a Date, or null. */
export function toDate(value: string | Date | null | undefined): Date | null {
  if (value === null || value === undefined || value === '') return null;
  if (value instanceof Date) return isNaN(value.getTime()) ? null : value;
  const m = /^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2})(?::(\d{2})(?:\.(\d+))?)?)?(Z|[+-]\d{2}:?\d{2})?$/.exec(value.trim());
  if (!m) {
    const d = new Date(value);
    return isNaN(d.getTime()) ? null : d;
  }
  if (m[8]) return new Date(value);
  const ms = m[7] ? Number(m[7].slice(0, 3).padEnd(3, '0')) : 0;
  return new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]), Number(m[4] ?? 0), Number(m[5] ?? 0), Number(m[6] ?? 0), ms);
}

/** "0001-01-01T00:00:00" is C#'s default(DateTime) — treat it as "no date". */
export function isMinDate(value: string | null | undefined): boolean {
  return !value || value.startsWith('0001-01-01');
}

/** Local wall-clock ISO string the API expects: yyyy-MM-ddTHH:mm:ss. */
export function toIsoLocal(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
}

/** yyyy-MM-dd for query strings and <input type="date">. */
export function dateParam(value: string | Date | null | undefined): string {
  const d = toDate(value);
  return d ? `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}` : '';
}

/** Today at midnight, as the API's date string. */
export function todayIso(): string {
  const d = new Date();
  return toIsoLocal(new Date(d.getFullYear(), d.getMonth(), d.getDate()));
}

/** <input type="date"> value → API date string (midnight), or null when blank. */
export function fromDateInput(value: string): string | null {
  return value ? `${value}T00:00:00` : null;
}

/** TimeSpan "HH:mm:ss" ↔ <input type="time"> "HH:mm". */
export function timeInput(span: string | null | undefined): string {
  return span ? span.slice(0, 5) : '';
}
export function fromTimeInput(value: string): string {
  return value ? (value.length === 5 ? `${value}:00` : value) : '00:00:00';
}

/** Minutes since midnight for a TimeSpan string. */
export function spanMinutes(span: string | null | undefined): number {
  if (!span) return 0;
  const [h, m] = span.split(':').map(Number);
  return (h || 0) * 60 + (m || 0);
}
export function minutesToSpan(total: number): string {
  const t = Math.max(0, Math.round(total));
  return `${pad(Math.floor(t / 60) % 24)}:${pad(t % 60)}:00`;
}

/**
 * .NET custom date format, for the patterns the Blazor pages used: dd/MM/yyyy, d MMM yyyy, h:mm tt,
 * dddd, d MMMM yyyy, HH:mm, … Also "g" (short date + short time) and "d" (short date), in the AU form.
 */
export function formatDate(value: string | Date | null | undefined, pattern = 'dd/MM/yyyy'): string {
  const d = toDate(value);
  if (!d) return '';
  if (pattern === 'g') pattern = 'dd/MM/yyyy h:mm tt';
  else if (pattern === 'd') pattern = 'dd/MM/yyyy';
  else if (pattern === 't') pattern = 'h:mm tt';
  else if (pattern === 'O') return d.toISOString();

  const tokens: Record<string, () => string> = {
    yyyy: () => String(d.getFullYear()),
    yy: () => pad(d.getFullYear() % 100),
    MMMM: () => MONTHS[d.getMonth()],
    MMM: () => MONTHS[d.getMonth()].slice(0, 3),
    MM: () => pad(d.getMonth() + 1),
    M: () => String(d.getMonth() + 1),
    dddd: () => DAYS[d.getDay()],
    ddd: () => DAYS[d.getDay()].slice(0, 3),
    dd: () => pad(d.getDate()),
    d: () => String(d.getDate()),
    HH: () => pad(d.getHours()),
    H: () => String(d.getHours()),
    hh: () => pad(d.getHours() % 12 || 12),
    h: () => String(d.getHours() % 12 || 12),
    mm: () => pad(d.getMinutes()),
    ss: () => pad(d.getSeconds()),
    tt: () => (d.getHours() < 12 ? 'AM' : 'PM'),
  };
  return pattern.replace(/yyyy|yy|MMMM|MMM|MM|M|dddd|ddd|dd|d|HH|H|hh|h|mm|ss|tt|'[^']*'/g, (tok) =>
    tok.startsWith("'") ? tok.slice(1, -1) : tokens[tok]());
}

/**
 * A server UTC timestamp (audit fields: CreatedAt/UpdatedAt/AcknowledgedAt…) shown in local time — C#'s
 * value.ToLocalTime().ToString(pattern). A zone-less string is taken as UTC, exactly as ToLocalTime() did.
 */
export function formatUtc(value: string | null | undefined, pattern = 'dd/MM/yyyy HH:mm'): string {
  const d = toUtcDate(value);
  return d ? formatDate(d, pattern) : '';
}

/** A server UTC timestamp as a Date (zone-less strings are UTC). */
export function toUtcDate(value: string | null | undefined): Date | null {
  if (!value) return null;
  const d = new Date(/(Z|[+-]\d{2}:?\d{2})$/.test(value) ? value : `${value}Z`);
  return Number.isNaN(d.getTime()) ? null : d;
}

/** A TimeSpan ("14:30:00") as "2:30 PM" (or any pattern formatDate understands). */
export function formatTime(span: string | null | undefined, pattern = 'h:mm tt'): string {
  if (!span) return '';
  const [h, m, s] = span.split(':').map(Number);
  return formatDate(new Date(2000, 0, 1, h || 0, m || 0, s || 0), pattern);
}

/** Whole years between a date of birth and today. */
export function ageFrom(dob: string | null | undefined): number | null {
  const d = toDate(dob);
  if (!d || isMinDate(dob ?? null)) return null;
  const now = new Date();
  let age = now.getFullYear() - d.getFullYear();
  if (now.getMonth() < d.getMonth() || (now.getMonth() === d.getMonth() && now.getDate() < d.getDate())) age--;
  return age;
}

export function addDays(date: Date, days: number): Date {
  const d = new Date(date);
  d.setDate(d.getDate() + days);
  return d;
}

export function startOfDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}
