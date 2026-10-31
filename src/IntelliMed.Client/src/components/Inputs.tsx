import {
  Children, cloneElement, createContext, Fragment, isValidElement, useContext, useEffect, useRef, useState,
  type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes,
} from 'react';
import { dateParam, fromDateInput } from '@/lib/dates';

type BaseInput = Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange' | 'type'>;

/**
 * A number field bound like Blazor's @bind on <input type=number>: the text is the user's while they type,
 * and the bound value only changes when the text parses (or clears to null when `nullable`).
 */
export function NumberInput({ value, onChange, nullable = false, ...rest }: BaseInput & {
  value: number | null | undefined;
  onChange: (value: number | null) => void;
  nullable?: boolean;
}) {
  const [text, setText] = useState(value == null ? '' : String(value));
  useEffect(() => {
    // follow outside changes, but not while the text already means this number ("1." while typing "1.5")
    setText((t) => (t !== '' && Number(t) === value ? t : value == null ? '' : String(value)));
  }, [value]);
  return (
    <input {...rest} type="number" value={text}
           onChange={(e) => {
             const t = e.target.value;
             setText(t);
             if (t === '') { if (nullable) onChange(null); return; }
             const n = Number(t);
             if (!Number.isNaN(n)) onChange(n);
           }} />
  );
}

/** A text field for a nullable string: blank is null (as the DTOs expect). */
export function TextInput({ value, onChange, nullable = true, ...rest }: BaseInput & {
  value: string | null | undefined;
  onChange: (value: string | null) => void;
  nullable?: boolean;
  type?: string;
}) {
  return <input type="text" {...rest} value={value ?? ''} onChange={(e) => onChange(e.target.value === '' && nullable ? null : e.target.value)} />;
}

export function TextArea({ value, onChange, nullable = true, ...rest }: Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, 'value' | 'onChange'> & {
  value: string | null | undefined;
  onChange: (value: string | null) => void;
  nullable?: boolean;
}) {
  return <textarea {...rest} value={value ?? ''} onChange={(e) => onChange(e.target.value === '' && nullable ? null : e.target.value)} />;
}

/** <input type=date> bound to an API date string ("2026-10-31T00:00:00"); blank is null. */
export function DateInput({ value, onChange, ...rest }: BaseInput & {
  value: string | null | undefined;
  onChange: (value: string | null) => void;
}) {
  const v = value && !value.startsWith('0001-01-01') ? dateParam(value) : '';
  return <input {...rest} type="date" value={v} onChange={(e) => onChange(fromDateInput(e.target.value))} />;
}

/** <input type=time> bound to a TimeSpan string ("14:30:00"). */
export function TimeInput({ value, onChange, ...rest }: BaseInput & {
  value: string | null | undefined;
  onChange: (value: string | null) => void;
}) {
  return <input {...rest} type="time" value={value ? value.slice(0, 5) : ''}
                onChange={(e) => onChange(e.target.value ? `${e.target.value}:00` : null)} />;
}

/** A <select> over a numeric enum or id; `null` is the empty option. */
type EnumLike = Record<string, string | number>;
const EnumSelectContext = createContext<EnumLike | null>(null);

/**
 * A <select> over numbers. Give it `enumType` when the numbers are an enum's: the options then carry the member
 * *names* as their values, as Blazor rendered enum-bound selects (and as the UI tests select them), while the
 * page still reads and writes the numeric value. Plain `<option value={X.Member}>` children are converted too.
 */
export function NumberSelect({ value, onChange, children, nullable = true, enumType, ...rest }: Omit<SelectHTMLAttributes<HTMLSelectElement>, 'value' | 'onChange'> & {
  value: number | null | undefined;
  onChange: (value: number | null) => void;
  nullable?: boolean;
  enumType?: EnumLike;
}) {
  const toDom = (v: unknown) => (enumType && typeof v === 'number' && typeof enumType[v] === 'string' ? (enumType[v] as string) : v);
  const fromDom = (raw: string): number | null => {
    if (raw === '') return nullable ? null : 0;
    if (/^-?\d+$/.test(raw)) return Number(raw);
    const n = enumType?.[raw];
    return typeof n === 'number' ? n : (nullable ? null : 0);
  };
  const options = enumType ? convertOptions(children, toDom) : children;
  return (
    <EnumSelectContext.Provider value={enumType ?? null}>
      <select {...rest} value={(toDom(value) as string | number | undefined) ?? ''} onChange={(e) => onChange(fromDom(e.target.value))}>
        {options}
      </select>
    </EnumSelectContext.Provider>
  );
}

function convertOptions(children: ReactNode, toDom: (v: unknown) => unknown): ReactNode {
  return Children.map(children, (child) => {
    if (!isValidElement<{ value?: unknown; children?: ReactNode }>(child)) return child;
    if (child.type === 'option') return cloneElement(child, { value: toDom(child.props.value) as string });
    if (child.type === Fragment) return cloneElement(child, {}, convertOptions(child.props.children, toDom));
    return child;
  });
}

/** <option>s for every member of a numeric TS enum, with optional display labels. */
export function EnumOptions<E extends EnumLike>({ enumType, labels, exclude }: {
  enumType: E;
  labels?: Partial<Record<number, string>>;
  exclude?: number[];
}) {
  // inside a NumberSelect with this enum, option values are member names (see NumberSelect)
  const byName = useContext(EnumSelectContext) === enumType;
  const entries = Object.entries(enumType).filter(([, v]) => typeof v === 'number') as [string, number][];
  return (
    <>
      {entries.filter(([, v]) => !exclude?.includes(v)).map(([name, v]) => (
        <option key={v} value={byName ? name : v}>{labels?.[v] ?? name}</option>
      ))}
    </>
  );
}

/**
 * A dropdown for fields that have a usual set of values but must not refuse an unusual one: click the chevron
 * (or start typing) to pick from the list, filtered by what's typed — or just type something else.
 */
export function SuggestInput({ id, value, onChange, options, className, ...rest }: BaseInput & {
  id: string;
  value: string | null | undefined;
  onChange: (value: string | null) => void;
  options: { value: string; label?: string }[];
}) {
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(-1);
  const [filtering, setFiltering] = useState(false);   // only filter once the user types, not on open
  const wrap = useRef<HTMLDivElement>(null);
  const text = value ?? '';
  const q = text.trim().toLowerCase();
  const shown = filtering && q
    ? options.filter((o) => o.value.toLowerCase().includes(q) || o.label?.toLowerCase().includes(q))
    : options;

  useEffect(() => {
    if (!open) return;
    const away = (e: MouseEvent) => { if (!wrap.current?.contains(e.target as Node)) setOpen(false); };
    document.addEventListener('mousedown', away);
    return () => document.removeEventListener('mousedown', away);
  }, [open]);

  useEffect(() => {
    wrap.current?.querySelector('.active')?.scrollIntoView({ block: 'nearest' });
  }, [active]);

  const pick = (v: string) => { onChange(v); setOpen(false); setFiltering(false); };
  const show = () => { setOpen(true); setFiltering(false); setActive(options.findIndex((o) => o.value === text)); };

  return (
    <div className="imed-suggest" ref={wrap}>
      <input type="text" {...rest} id={id} className={className} autoComplete="off" value={text}
             role="combobox" aria-expanded={open} aria-controls={`${id}-list`} aria-autocomplete="list"
             onChange={(e) => { onChange(e.target.value === '' ? null : e.target.value); setOpen(true); setFiltering(true); setActive(-1); }}
             onKeyDown={(e) => {
               if (e.key === 'ArrowDown') { e.preventDefault(); if (!open) show(); else setActive((a) => Math.min(a + 1, shown.length - 1)); }
               else if (e.key === 'ArrowUp') { e.preventDefault(); setActive((a) => Math.max(a - 1, 0)); }
               else if (e.key === 'Enter' && open && active >= 0 && shown[active]) { e.preventDefault(); pick(shown[active].value); }
               else if (e.key === 'Escape' && open) { e.stopPropagation(); setOpen(false); }
               else if (e.key === 'Tab') setOpen(false);
             }} />
      <button type="button" className="imed-suggest-toggle" tabIndex={-1} aria-label="Show choices"
              onMouseDown={(e) => e.preventDefault()}
              onClick={() => { if (open) setOpen(false); else { show(); wrap.current?.querySelector('input')?.focus(); } }}>
        <i className="bi bi-chevron-down" />
      </button>
      {open && shown.length > 0 && (
        <ul className="dropdown-menu show imed-suggest-menu" id={`${id}-list`} role="listbox">
          {shown.map((o, i) => (
            <li key={o.value} role="option" aria-selected={o.value === text}>
              <button type="button" className={`dropdown-item ${i === active ? 'active' : ''}`}
                      onMouseDown={(e) => e.preventDefault()} onClick={() => pick(o.value)}>
                <span className="fw-medium">{o.value}</span>
                {o.label && <span className="imed-suggest-hint">{o.label}</span>}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

const OTHER = '__other__';

/**
 * A dropdown of set choices plus one "type your own" choice (Medinet's "Other"): picking it shows a text box,
 * and a saved value that isn't in the list reopens in that box. Stores the choice's value, or the typed text.
 */
export function ChoiceWithOther({ id, value, onChange, options, otherLabel = 'Other…', noneLabel = 'Select...', otherPlaceholder, className = 'form-select form-select-sm' }: {
  id: string;
  value: string | null | undefined;
  onChange: (value: string | null) => void;
  options: { value: string; label?: string }[];
  otherLabel?: string;
  noneLabel?: string;
  otherPlaceholder?: string;
  className?: string;
}) {
  const inList = !!value && options.some((o) => o.value === value);
  const [typing, setTyping] = useState(!!value && !inList);
  const selected = typing ? OTHER : inList ? value! : '';
  return (
    <>
      <select id={id} className={className} value={selected}
              onChange={(e) => {
                const v = e.target.value;
                setTyping(v === OTHER);
                onChange(v === OTHER || v === '' ? null : v);
              }}>
        <option value="">{noneLabel}</option>
        {options.map((o) => <option key={o.value} value={o.value}>{o.label ? `${o.value} — ${o.label}` : o.value}</option>)}
        <option value={OTHER}>{otherLabel}</option>
      </select>
      {typing && (
        <input type="text" className="form-control form-control-sm mt-1" autoFocus aria-label={otherLabel.replace('…', '')}
               placeholder={otherPlaceholder} value={value ?? ''} onChange={(e) => onChange(e.target.value === '' ? null : e.target.value)} />
      )}
    </>
  );
}
