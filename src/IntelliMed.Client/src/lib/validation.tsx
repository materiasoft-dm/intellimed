// The messages ASP.NET DataAnnotations produced on the Blazor forms, so the wording users (and the UI
// tests) know is unchanged. Rules return null when the value is valid.

export type Rule = (value: string) => string | null;

const EMAIL = /^[^@\s]+@[^@\s]+$/;

export const rules = {
  required: (field: string): Rule => (v) => (v && v.trim() ? null : `The ${field} field is required.`),
  email: (field: string): Rule => (v) => (!v || EMAIL.test(v.trim()) ? null : `The ${field} field is not a valid e-mail address.`),
  minLength: (min: number, message: string): Rule => (v) => (!v || v.length >= min ? null : message),
  range: (field: string, min: number, max: number): Rule => (v) => {
    const n = Number(v);
    return v !== '' && !Number.isNaN(n) && n >= min && n <= max ? null : `The field ${field} must be between ${min} and ${max}.`;
  },
  maxLength: (field: string, max: number): Rule => (v) =>
    (!v || v.length <= max ? null : `The field ${field} must be a string or array type with a maximum length of '${max}'.`),
};

/** Runs each field's rules; returns field → messages (only fields with problems). */
export function validate<T extends Record<string, unknown>>(model: T, spec: Partial<Record<keyof T, Rule[]>>): Partial<Record<keyof T, string[]>> {
  const errors: Partial<Record<keyof T, string[]>> = {};
  for (const key of Object.keys(spec) as (keyof T)[]) {
    const value = model[key];
    const messages = (spec[key] ?? []).map((r) => r(value == null ? '' : String(value))).filter((m): m is string => !!m);
    if (messages.length) errors[key] = messages;
  }
  return errors;
}

export const hasErrors = (errors: Record<string, string[] | undefined>) => Object.values(errors).some((m) => m && m.length);

/** Blazor's <ValidationSummary>: every message, as a list. */
export function ValidationSummary({ errors, className = '' }: { errors: Record<string, string[] | undefined>; className?: string }) {
  const all = Object.values(errors).flatMap((m) => m ?? []);
  if (!all.length) return null;
  return (
    <ul className={`validation-errors ${className}`}>
      {all.map((m) => <li key={m} className="validation-message">{m}</li>)}
    </ul>
  );
}

/** Blazor's <ValidationMessage For=...>: the messages for one field. */
export function FieldMessage({ messages }: { messages?: string[] }) {
  if (!messages?.length) return null;
  return <>{messages.map((m) => <div key={m} className="validation-message">{m}</div>)}</>;
}
