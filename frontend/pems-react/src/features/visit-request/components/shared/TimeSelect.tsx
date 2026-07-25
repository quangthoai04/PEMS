import React, { useCallback, useEffect, useId, useLayoutEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Clock } from 'lucide-react';
import { normalizeTimeInput } from './visitDateTime';

export interface TimeOption {
  /** "HH:mm" */
  value: string;
  /** Optional trailing note, e.g. the resulting duration ("1 giờ"). */
  hint?: string;
}

interface Props {
  /** Committed "HH:mm", or '' when nothing is chosen yet. */
  value: string;
  onChange: (next: string) => void;
  options: TimeOption[];
  hasError?: boolean;
  disabled?: boolean;
  id?: string;
  ariaLabel: string;
  /** Rendered inside the listbox when `options` is empty. */
  emptyHint?: string;
  testId?: string;
}

/**
 * A time combobox: type it, or pick it (plan §11).
 *
 * The listbox is PORTALLED to <body> and positioned from the trigger's viewport rect, because
 * this control lives inside a modal whose body scrolls with `overflow-y: auto` — an absolutely
 * positioned dropdown would be clipped by that container and the last options would be
 * unreachable. Note that a portal keeps the REACT tree: this component still sits inside the
 * visit-request <form>, so every button is type="button" and Enter is always defaultPrevented,
 * or choosing a time would submit the whole registration.
 */
export const TimeSelect: React.FC<Props> = ({
  value, onChange, options, hasError, disabled, id, ariaLabel, emptyHint, testId,
}) => {
  const autoId = useId();
  const listId = `${id ?? autoId}-listbox`;
  const [open, setOpen] = useState(false);
  const [raw, setRaw] = useState(value);
  const [activeIndex, setActiveIndex] = useState(-1);
  const [rect, setRect] = useState<{ top: number; left: number; width: number } | null>(null);

  const wrapRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLUListElement>(null);
  /**
   * Whether the highlight was moved by the ARROW KEYS, as opposed to merely marking where the
   * current value sits when the list opened. Enter must commit what was TYPED unless the user
   * actually navigated — otherwise typing "08:00" over a 09:00 field and pressing Enter would
   * quietly re-select 09:00, the value they were replacing.
   */
  const navigated = useRef(false);

  // The committed value is the source of truth; local `raw` only holds in-progress typing.
  useEffect(() => { setRaw(value); }, [value]);

  const place = useCallback(() => {
    const el = wrapRef.current;
    if (!el) return;
    const r = el.getBoundingClientRect();
    const below = window.innerHeight - r.bottom;
    const height = Math.min(288, Math.max(options.length, 1) * 36 + 8);
    // Flip above when there is not enough room below, so the list is never half off-screen.
    const top = below < height + 8 && r.top > height + 8 ? r.top - height - 4 : r.bottom + 4;
    setRect({ top, left: r.left, width: r.width });
  }, [options.length]);

  useLayoutEffect(() => { if (open) place(); }, [open, place]);

  useEffect(() => {
    if (!open) return;
    const onScrollOrResize = () => place();
    // `true` → capture, so an ancestor's scroll (the modal body) repositions the list too.
    window.addEventListener('scroll', onScrollOrResize, true);
    window.addEventListener('resize', onScrollOrResize);
    return () => {
      window.removeEventListener('scroll', onScrollOrResize, true);
      window.removeEventListener('resize', onScrollOrResize);
    };
  }, [open, place]);

  useEffect(() => {
    if (!open) return;
    const onDocPointerDown = (e: MouseEvent | TouchEvent) => {
      const target = e.target as Node;
      if (wrapRef.current?.contains(target) || listRef.current?.contains(target)) return;
      setOpen(false);
    };
    document.addEventListener('mousedown', onDocPointerDown);
    document.addEventListener('touchstart', onDocPointerDown);
    return () => {
      document.removeEventListener('mousedown', onDocPointerDown);
      document.removeEventListener('touchstart', onDocPointerDown);
    };
  }, [open]);

  // Keep the highlighted option in view while arrowing through a 96-slot list.
  useEffect(() => {
    if (!open || activeIndex < 0) return;
    const el = listRef.current?.querySelector<HTMLElement>(`[data-index="${activeIndex}"]`);
    // jsdom has no layout, so it does not implement scrollIntoView.
    if (typeof el?.scrollIntoView === 'function') el.scrollIntoView({ block: 'nearest' });
  }, [open, activeIndex]);

  const openList = () => {
    if (disabled) return;
    const idx = options.findIndex(o => o.value === value);
    setActiveIndex(idx);
    navigated.current = false;
    setOpen(true);
  };

  const commit = (next: string) => {
    onChange(next);
    setRaw(next);
    setOpen(false);
    setActiveIndex(-1);
    navigated.current = false;
  };

  /**
   * Reads what was typed; an unreadable entry restores the last committed value.
   *
   * Takes the text from the DOM rather than from `raw` state: the keystroke that commits (Enter,
   * or the blur) can arrive in the same batch as the change that produced the text, and a
   * handler closed over the previous render would then commit the PREVIOUS value.
   */
  const commitRaw = (typed?: string) => {
    const parsed = normalizeTimeInput(typed ?? inputRef.current?.value ?? raw);
    if (parsed) commit(parsed);
    else setRaw(value);
  };

  const onKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      if (!open) { openList(); return; }
      navigated.current = true;
      const dir = e.key === 'ArrowDown' ? 1 : -1;
      setActiveIndex(i => {
        if (options.length === 0) return -1;
        const next = i + dir;
        if (next < 0) return options.length - 1;
        if (next >= options.length) return 0;
        return next;
      });
      return;
    }
    if (e.key === 'Home' && open) { e.preventDefault(); navigated.current = true; setActiveIndex(0); return; }
    if (e.key === 'End' && open) { e.preventDefault(); navigated.current = true; setActiveIndex(options.length - 1); return; }
    if (e.key === 'Escape') {
      if (open) {
        // Only swallowed while the list is open, so Esc still closes the surrounding modal.
        e.preventDefault();
        setOpen(false);
        setActiveIndex(-1);
        navigated.current = false;
      }
      return;
    }
    if (e.key === 'Enter') {
      // ALWAYS swallowed: without this, Enter here submits the visit request.
      e.preventDefault();
      if (open && navigated.current && activeIndex >= 0 && options[activeIndex]) {
        commit(options[activeIndex].value);
      } else {
        commitRaw(e.currentTarget.value);
      }
      return;
    }
    if (e.key === 'Tab' && open) {
      setOpen(false);
      setActiveIndex(-1);
    }
  };

  const list = open && rect ? createPortal(
    <ul
      ref={listRef}
      id={listId}
      role="listbox"
      data-testid={testId ? `${testId}-listbox` : undefined}
      aria-label={ariaLabel}
      style={{ position: 'fixed', top: rect.top, left: rect.left, width: rect.width, zIndex: 1000 }}
      className="max-h-72 overflow-y-auto rounded-xl border border-slate-200 bg-white py-1 shadow-lg"
    >
      {options.length === 0 && (
        <li className="px-3 py-2 text-xs font-semibold text-slate-500">{emptyHint}</li>
      )}
      {options.map((o, i) => (
        <li key={o.value} data-index={i} role="none">
          <button
            type="button"
            role="option"
            aria-selected={o.value === value}
            tabIndex={-1}
            // mousedown would blur the input first and close the list before the click lands.
            onMouseDown={e => e.preventDefault()}
            onClick={() => commit(o.value)}
            onMouseEnter={() => setActiveIndex(i)}
            className={`flex w-full items-baseline justify-between gap-3 px-3 py-2 text-left text-sm ${
              i === activeIndex ? 'bg-[#004c91]/10' : ''
            } ${o.value === value ? 'font-bold text-[#004c91]' : 'font-semibold text-slate-700'}`}
          >
            <span>{o.value}</span>
            {o.hint && <span className="text-xs font-medium text-slate-500">{o.hint}</span>}
          </button>
        </li>
      ))}
    </ul>,
    document.body,
  ) : null;

  return (
    <div ref={wrapRef} className="relative">
      <input
        ref={inputRef}
        id={id}
        type="text"
        inputMode="numeric"
        autoComplete="off"
        role="combobox"
        aria-expanded={open}
        aria-controls={open ? listId : undefined}
        aria-autocomplete="none"
        aria-label={ariaLabel}
        data-testid={testId}
        disabled={disabled}
        value={raw}
        placeholder="--:--"
        onChange={e => setRaw(e.target.value)}
        onFocus={openList}
        onClick={openList}
        onBlur={e => commitRaw(e.currentTarget.value)}
        onKeyDown={onKeyDown}
        className={`h-11 w-full min-w-0 rounded-xl border bg-white pl-4 pr-9 text-sm font-semibold text-slate-800 outline-none transition-colors disabled:bg-slate-100 ${
          hasError
            ? 'border-red-400 focus:border-red-500 focus:ring-2 focus:ring-red-500/10'
            : 'border-slate-300 focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10'
        }`}
      />
      <Clock className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
      {list}
    </div>
  );
};
