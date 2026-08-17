import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { isOverCharacterLimit, shouldShowCharacterCount } from './characterCount';

interface Props {
  value: string;
  onChange: (value: string) => void;
  onBlur?: () => void;
  hasError?: boolean;
  /** The schema limit. Drives the counter; never truncates. */
  maxLength?: number;
  placeholder?: string;
  ariaLabel?: string;
  disabled?: boolean;
  /** Stops growing past this many lines and scrolls instead. */
  maxRows?: number;
  /** Borderless variant for use inside a table cell. */
  isCell?: boolean;
  className?: string;
  testId?: string;
}

/**
 * A single-line field that WRAPS instead of scrolling sideways (plan §21.2).
 *
 * "Trường Đại học Khoa học Tự nhiên — Đại học Quốc gia Thành phố Hồ Chí Minh" is a real
 * organization name and a perfectly ordinary answer, but in a 200px table cell an <input>
 * shows about a fifth of it and hides the rest behind a caret the user has to drag. This is a
 * textarea underneath — so the text wraps and the row grows — while behaving like an input:
 * Enter does not insert a line break, and pasted newlines collapse to spaces. The stored value
 * therefore stays exactly what an <input> would have stored.
 *
 * Not for email, phone, date, time or any select — those have a canonical single-line form and
 * a wrapped one would only look broken.
 */
export const AutoGrowTextField: React.FC<Props> = ({
  value, onChange, onBlur, hasError, maxLength, placeholder, ariaLabel,
  disabled, maxRows = 4, isCell, className = '', testId,
}) => {
  const { t } = useTranslation(['validation']);
  const ref = useRef<HTMLTextAreaElement>(null);
  const [focused, setFocused] = useState(false);

  const resize = useCallback(() => {
    const el = ref.current;
    if (!el || el.scrollHeight === 0) return; // hidden (collapsed card) — resized when shown

    const style = window.getComputedStyle(el);
    const lineHeight = parseFloat(style.lineHeight) || 20;
    const padding = parseFloat(style.paddingTop || '0') + parseFloat(style.paddingBottom || '0');
    const border = parseFloat(style.borderTopWidth || '0') + parseFloat(style.borderBottomWidth || '0');
    const max = lineHeight * maxRows + padding;

    el.style.height = 'auto';
    const next = Math.min(el.scrollHeight, max);
    el.style.height = `${next + border}px`;
    el.style.overflowY = el.scrollHeight > max ? 'auto' : 'hidden';
  }, [maxRows]);

  // Runs on every value change — typing, pasting, a restored draft and "copy from another
  // campus" all have to end up at the right height, not just keystrokes.
  useEffect(() => { resize(); }, [value, resize]);

  useEffect(() => {
    const el = ref.current;
    if (!el || typeof ResizeObserver === 'undefined') return;
    const observer = new ResizeObserver(() => { if (el.scrollHeight > 0) resize(); });
    observer.observe(el);
    return () => observer.disconnect();
  }, [resize]);

  const over = isOverCharacterLimit(value, maxLength);
  // A counter on every name field would be noise; it appears while the field is being worked in,
  // or once the limit comes into view (plan §14).
  const showCounter = shouldShowCharacterCount({ value, maxLength, focused, hasError });

  const base = isCell
    ? `w-full resize-none border-0 bg-transparent px-3 py-2.5 text-sm outline-none focus:ring-1 focus:ring-[#004c91] ${
        hasError ? 'bg-red-50/40' : ''
      }`
    : `w-full resize-none rounded-xl border bg-white px-4 py-2.5 text-sm font-normal text-slate-800 outline-none transition-colors ${
        hasError
          ? 'border-red-400 focus:border-red-500 focus:ring-2 focus:ring-red-500/10'
          : 'border-slate-300 focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10'
      }`;

  return (
    <div>
      <textarea
        ref={ref}
        rows={1}
        value={value}
        disabled={disabled}
        placeholder={placeholder}
        aria-label={ariaLabel}
        data-testid={testId}
        // Names, delegations and job titles are Vietnamese proper nouns the browser's dictionary
        // does not know: every one of them came back underlined in red, which on a form that marks
        // its real errors in red reads as "the system rejected this".
        spellCheck={false}
        onChange={e => { onChange(e.target.value.replace(/[\r\n]+/g, ' ')); resize(); }}
        onKeyDown={e => { if (e.key === 'Enter') e.preventDefault(); }}
        onPaste={e => {
          const text = e.clipboardData.getData('text');
          if (!/[\r\n]/.test(text)) return;
          // Multi-line paste into a one-line field: flatten rather than let the box explode.
          e.preventDefault();
          const el = e.currentTarget;
          const next = value.slice(0, el.selectionStart ?? value.length)
            + text.replace(/[\r\n]+/g, ' ').trim()
            + value.slice(el.selectionEnd ?? value.length);
          onChange(next);
        }}
        onBlur={() => { setFocused(false); onBlur?.(); }}
        onFocus={() => { setFocused(true); resize(); }}
        className={`${base} ${className}`}
      />
      {showCounter && (
        <p
          data-testid={testId ? `${testId}-counter` : undefined}
          className={`mt-0.5 text-right text-[11px] ${over ? 'font-bold text-red-600' : 'text-slate-400'}`}
        >
          {t('validation:characterCount', { current: value.length, max: maxLength })}
        </p>
      )}
    </div>
  );
};
