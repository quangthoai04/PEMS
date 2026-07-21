import React, { useEffect, useRef } from 'react';

interface AutoGrowInputProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  maxLength?: number;
  disabled?: boolean;
  className?: string;
}

/**
 * Single-line-looking text field that wraps and grows vertically as the user types long content,
 * instead of a native <input> which only scrolls horizontally within a fixed height. Backed by a
 * <textarea rows={1}> whose height is resized on every change; Enter is suppressed so it still
 * behaves like a one-line field (title/section-title), just one that never clips text.
 */
export function AutoGrowInput({ value, onChange, placeholder, maxLength, disabled, className }: AutoGrowInputProps) {
  const ref = useRef<HTMLTextAreaElement>(null);

  function resize(el: HTMLTextAreaElement) {
    el.style.height = 'auto';
    el.style.height = `${el.scrollHeight}px`;
  }

  useEffect(() => {
    if (ref.current) resize(ref.current);
  }, [value]);

  return (
    <textarea
      ref={ref}
      rows={1}
      value={value}
      placeholder={placeholder}
      maxLength={maxLength}
      disabled={disabled}
      onChange={e => { onChange(e.target.value); resize(e.target); }}
      onKeyDown={e => { if (e.key === 'Enter') e.preventDefault(); }}
      className={`resize-none overflow-hidden leading-normal ${className ?? ''}`}
    />
  );
}

interface AutoGrowTextareaProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  maxLength?: number;
  disabled?: boolean;
  className?: string;
  minRows?: number;
}

/**
 * Multi-line textarea (summary/description fields) that grows taller as content wraps past its
 * minimum row count, instead of clipping into an internal scrollbar at a fixed height. Enter is
 * allowed (unlike AutoGrowInput) since these are genuinely multi-line fields.
 */
export function AutoGrowTextarea({ value, onChange, placeholder, maxLength, disabled, className, minRows = 3 }: AutoGrowTextareaProps) {
  const ref = useRef<HTMLTextAreaElement>(null);

  function resize(el: HTMLTextAreaElement) {
    el.style.height = 'auto';
    el.style.height = `${el.scrollHeight}px`;
  }

  useEffect(() => {
    if (ref.current) resize(ref.current);
  }, [value]);

  return (
    <textarea
      ref={ref}
      rows={minRows}
      value={value}
      placeholder={placeholder}
      maxLength={maxLength}
      disabled={disabled}
      onChange={e => { onChange(e.target.value); resize(e.target); }}
      className={`resize-none overflow-hidden ${className ?? ''}`}
    />
  );
}
