import { useEffect, useRef, type InputHTMLAttributes } from 'react';

/**
 * A number input that commits on the native `change` event (blur or Enter), like Blazor's @onchange — not on
 * every keystroke. For inline edits that save to the server, e.g. a fee in a schedule's item list.
 */
export function CommitNumberInput({ value, onCommit, ...rest }: Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange' | 'type' | 'defaultValue'> & {
  value: number;
  onCommit: (value: number) => void;
}) {
  const ref = useRef<HTMLInputElement>(null);
  const commit = useRef(onCommit);
  commit.current = onCommit;

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const onChange = () => {
      const n = Number(el.value);
      if (el.value !== '' && !Number.isNaN(n)) commit.current(n);
    };
    el.addEventListener('change', onChange);
    return () => el.removeEventListener('change', onChange);
  }, []);

  // keep the box in step with saved values without fighting the user's typing
  useEffect(() => {
    const el = ref.current;
    if (el && document.activeElement !== el) el.value = String(value);
  }, [value]);

  return <input ref={ref} type="number" defaultValue={value} {...rest} />;
}
