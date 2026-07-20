import React, { useCallback, useEffect, useRef } from 'react';

interface Props extends Omit<React.TextareaHTMLAttributes<HTMLTextAreaElement>, 'value' | 'onChange'> {
  value: string;
  onChange: (value: string) => void;
  hasError?: boolean;
  /** Shows a "typed / limit" counter under the field. Omit for no counter. */
  maxLength?: number;
  /** Never shrink below this many rows. */
  minRows?: number;
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
  value, onChange, hasError, maxLength, minRows = 3, className = '', ...rest
}) => {
  const ref = useRef<HTMLTextAreaElement>(null);

  const resize = useCallback(() => {
    const el = ref.current;
    if (!el) return;
    el.style.height = 'auto';           // collapse first, or the box can only ever grow
    el.style.height = `${el.scrollHeight}px`;
  }, []);

  useEffect(() => { resize(); }, [value, resize]);

  const over = maxLength !== undefined && value.length > maxLength;

  return (
    <div>
      <textarea
        ref={ref}
        rows={minRows}
        value={value}
        onChange={e => { onChange(e.target.value); resize(); }}
        className={`w-full resize-none overflow-hidden rounded-lg border px-3 py-2 text-sm outline-none transition-colors focus:ring-1 ${
          hasError
            ? 'border-red-400 focus:border-red-400 focus:ring-red-400'
            : 'border-slate-300 focus:border-[#004c91] focus:ring-[#004c91]'
        } ${className}`}
        {...rest}
      />
      {maxLength !== undefined && (
        <p className={`mt-1 text-right text-xs ${over ? 'font-bold text-red-600' : 'text-slate-400'}`}>
          {value.length}/{maxLength}
        </p>
      )}
    </div>
  );
};
