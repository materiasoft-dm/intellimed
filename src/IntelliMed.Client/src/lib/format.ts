const currency = new Intl.NumberFormat('en-AU', { style: 'currency', currency: 'AUD' });

/** .NET "C": $1,234.50 (and -$12.00). */
export function money(value: number | null | undefined): string {
  return value === null || value === undefined ? '' : currency.format(value);
}

/** .NET "D5" and friends: zero-padded integer. */
export function padNumber(value: number | null | undefined, width: number): string {
  return value === null || value === undefined ? '' : String(value).padStart(width, '0');
}

/** "1 contact" / "3 contacts". */
export function plural(count: number, singular: string, pluralForm = `${singular}s`): string {
  return `${count} ${count === 1 ? singular : pluralForm}`;
}

/** Join the non-blank parts: join(" · ", profession, speciality). */
export function joinNonBlank(separator: string, ...parts: (string | null | undefined)[]): string {
  return parts.filter((p) => p && p.trim()).join(separator);
}

/** First letters of the first and last name parts (or of the email local part). */
export function initials(name: string): string {
  const parts = name.split(/[ .@]+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0][0].toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

/** Splits PascalCase enum names for display: "BulkBill" → "Bulk Bill". */
export function humanize(name: string): string {
  return name.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/([A-Z]+)([A-Z][a-z])/g, '$1 $2');
}

/** Saves a Blob as a download (what download-interop.js did for Blazor). */
export function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
