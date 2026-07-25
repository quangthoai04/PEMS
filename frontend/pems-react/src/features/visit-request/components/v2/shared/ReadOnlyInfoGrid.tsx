import React from 'react';

export interface InfoRow {
  label: string;
  value: React.ReactNode;
  /** Long prose (purpose, working content) spans the full width and wraps instead of being clipped. */
  multiline?: boolean;
}

interface Props {
  rows: InfoRow[];
  /** Shown in place of an empty value. */
  emptyText?: string;
  className?: string;
}

const isEmpty = (value: React.ReactNode): boolean =>
  value === null || value === undefined || (typeof value === 'string' && value.trim() === '');

/**
 * Label/value read-out for the v2 detail screens: two columns on desktop, one on mobile, with long
 * prose spanning the full width.
 *
 * An absent value is rendered as an explicit placeholder, never omitted — a missing row reads as
 * "this field does not apply", while a dash reads as "nobody filled this in", and on a visit request
 * those mean very different things to whoever has to act on it.
 */
export const ReadOnlyInfoGrid: React.FC<Props> = ({ rows, emptyText = '—', className = '' }) => (
  <dl className={`grid grid-cols-1 gap-x-8 gap-y-3 sm:grid-cols-2 ${className}`}>
    {rows.map(({ label, value, multiline }) => (
      <div key={label} className={multiline ? 'sm:col-span-2' : undefined}>
        <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</dt>
        <dd
          className={`mt-0.5 break-words text-sm text-slate-800 ${multiline ? 'whitespace-pre-wrap' : ''} ${
            isEmpty(value) ? 'italic text-slate-400' : 'font-medium'
          }`}
        >
          {isEmpty(value) ? emptyText : value}
        </dd>
      </div>
    ))}
  </dl>
);
