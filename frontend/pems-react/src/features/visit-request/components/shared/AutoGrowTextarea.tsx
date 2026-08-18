import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { isOverCharacterLimit, shouldShowCharacterCount } from './characterCount';

interface Props extends Omit<React.TextareaHTMLAttributes<HTMLTextAreaElement>, 'value' | 'onChange'> {
  value: string;
  onChange: (value: string) => void;
  hasError?: boolean;
  /** The schema limit. Drives the counter; never truncates. */
  maxLength?: number;
  /** Never shrink below this many rows. */
  minRows?: number;
  /** Test id for the counter, so a test can assert it is absent as well as present. */
  counterTestId?: string;
}

/**
 * A textarea that grows with its content instead of scrolling internally. A long "purpose" or
 * "working content" is normal here, and a nested scrollbar inside an already-scrolling modal body
 * hides text the user just typed.
 *
 * Height is recomputed from scrollHeight on every value change — not only on keystrokes — so
 * pasting, restoring a draft and copying another campus all resize correctly. `maxLength` renders a
 * counter but is deliberately NOT passed to the DOM: the browser would silently truncate at the
 * limit, whereas the schema should tell the user they are over it.
 */
export const AutoGrowTextarea: React.FC<Props> = ({
  value, onChange, hasError, maxLength, minRows = 3, className = '', onFocus, onBlur,
  counterTestId, ...rest
}) => {
  const { t } = useTranslation(['validation']);
  const ref = useRef<HTMLTextAreaElement>(null);
  const [focused, setFocused] = useState(false);

  const resize = useCallback(() => {
    const el = ref.current;
    if (!el) return;
    
    // If element is hidden (e.g. inside a collapsed accordion), scrollHeight is 0.
    // Setting height to 0 will squash the textarea to just its borders/padding.
    // We skip resizing here; it will resize when it becomes visible.
    if (el.scrollHeight === 0) return;

    // Tailwind borders add to the element's total height in border-box.
    // scrollHeight only measures content + padding.
    const computed = window.getComputedStyle(el);
    const borderY = parseInt(computed.borderTopWidth || '0') + parseInt(computed.borderBottomWidth || '0');

    el.style.height = 'auto';           // collapse first, or the box can only ever grow
    el.style.height = `${el.scrollHeight + borderY}px`;
  }, []);

  useEffect(() => { resize(); }, [value, resize]);

  // Handle visibility changes (like opening a collapsed campus card)
  useEffect(() => {
    const el = ref.current;
    if (!el || typeof ResizeObserver === 'undefined') return;
    
    const observer = new ResizeObserver(() => {
      if (el.scrollHeight > 0) resize();
    });
    
    observer.observe(el);
    return () => observer.disconnect();
  }, [resize]);

  const over = isOverCharacterLimit(value, maxLength);
  const showCounter = shouldShowCharacterCount({ value, maxLength, focused, hasError });

  return (
    <div>
      <textarea
        ref={ref}
        rows={minRows}
        value={value}
        // Free Vietnamese prose ("Mục đích", "Nội dung làm việc"): the browser spell checker has no
        // Vietnamese dictionary here and red-underlines nearly every word, which is indistinguishable
        // from the form saying the value is invalid. Before `...rest`, so a caller can still opt in.
        spellCheck={false}
        onChange={e => { onChange(e.target.value); resize(); }}
        onFocus={e => { setFocused(true); resize(); if (onFocus) onFocus(e); }}
        onBlur={e => { setFocused(false); if (onBlur) onBlur(e); }}
        className={`w-full resize-none overflow-hidden rounded-lg border bg-white px-3 py-2 text-sm outline-none transition-colors focus:ring-1 ${
          hasError
            ? 'border-red-400 focus:border-red-400 focus:ring-red-400'
            : 'border-slate-300 focus:border-[#004c91] focus:ring-[#004c91]'
        } ${className}`}
        {...rest}
      />
      {showCounter && (
        <p
          data-testid={counterTestId}
          className={`mt-1 text-right text-xs ${over ? 'font-normal text-red-600' : 'text-slate-400'}`}
        >
          {t('validation:characterCount', { current: value.length, max: maxLength })}
        </p>
      )}
    </div>
  );
};
