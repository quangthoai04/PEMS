import React, { useId, useState } from 'react';
import { ChevronDown, ChevronUp, Lock } from 'lucide-react';

interface Props {
  /** 1-based position shown in the orange circle. Omit for a section that is not part of the numbered run. */
  step?: number;
  title: string;
  /** Renders the "read only" lock chip in the header. */
  readOnlyLabel?: string;
  /** Extra header content (badges, counts) rendered before the collapse control. */
  headerExtra?: React.ReactNode;
  /** Collapsible sections start open; a non-collapsible one has no toggle at all. */
  collapsible?: boolean;
  defaultOpen?: boolean;
  children: React.ReactNode;
  'data-testid'?: string;
  /** Anchor id for a deep link (e.g. notification's `#history` target) to scroll straight to this section. */
  id?: string;
}

/**
 * The section shell shared by the v2 read screens — the same blue header / orange step number /
 * "chỉ đọc" chip the Xử lý đơn screen uses, so a user moving between the two reads one system
 * rather than two products.
 *
 * The header is a real <button> when collapsible, so the section is reachable and toggleable from
 * the keyboard; when it is not collapsible there is no interactive element to tab through.
 */
export const VisitSectionCard: React.FC<Props> = ({
  step, title, readOnlyLabel, headerExtra, collapsible = true, defaultOpen = true, children, id, ...rest
}) => {
  const [open, setOpen] = useState(defaultOpen);
  const bodyId = useId();
  const isOpen = collapsible ? open : true;

  const header = (
    <>
      <h3 className="flex min-w-0 items-center gap-2 text-sm font-bold uppercase tracking-tight text-white">
        {step !== undefined && (
          <span
            aria-hidden
            className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-[#f37021] text-xs font-bold text-white"
          >
            {step}
          </span>
        )}
        <span className="truncate">{title}</span>
      </h3>
      <div className="flex shrink-0 items-center gap-2">
        {headerExtra}
        {readOnlyLabel && (
          <span className="inline-flex items-center gap-1 rounded-md bg-white/15 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-white">
            <Lock className="h-3 w-3" aria-hidden /> {readOnlyLabel}
          </span>
        )}
        {collapsible && (
          <span aria-hidden className="flex h-6 w-6 items-center justify-center text-white/80">
            {isOpen ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
          </span>
        )}
      </div>
    </>
  );

  return (
    <section
      id={id}
      data-testid={rest['data-testid']}
      className="overflow-hidden rounded-xl border border-[#004c91]/20 bg-white shadow-sm"
    >
      {collapsible ? (
        <button
          type="button"
          aria-expanded={isOpen}
          aria-controls={bodyId}
          onClick={() => setOpen(o => !o)}
          className="flex w-full items-center justify-between gap-3 bg-[#004c91] px-4 py-2.5 text-left transition-colors hover:bg-[#013565]"
        >
          {header}
        </button>
      ) : (
        <div className="flex items-center justify-between gap-3 bg-[#004c91] px-4 py-2.5">{header}</div>
      )}
      <div id={bodyId} className={isOpen ? 'border-t border-slate-100 p-4 sm:p-5' : 'hidden'}>
        {children}
      </div>
    </section>
  );
};
