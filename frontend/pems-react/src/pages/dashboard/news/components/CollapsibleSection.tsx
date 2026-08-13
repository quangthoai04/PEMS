import React, { useState } from 'react';
import { ChevronDown } from 'lucide-react';

/**
 * News form section wrapper — dark blue header bar (matches the existing "1. THÔNG TIN CƠ BẢN"
 * style) that can collapse its body away, since content sections with long text can grow tall.
 * `headerExtra` renders on the right side of the header bar (e.g. the bilingual toggle icon) and
 * never triggers the collapse toggle itself.
 */
export function CollapsibleSection({
  title,
  headerExtra,
  defaultOpen = true,
  disabled,
  children,
}: {
  title: React.ReactNode;
  headerExtra?: React.ReactNode;
  defaultOpen?: boolean;
  disabled?: boolean;
  children: React.ReactNode;
}) {
  const [open, setOpen] = useState(defaultOpen);

  return (
    <section className={`bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden transition-opacity ${disabled ? 'opacity-50 pointer-events-none' : ''}`}>
      <div className="bg-[#004c91] px-6 py-3.5 flex items-center justify-between gap-3">
        <button
          type="button"
          onClick={() => setOpen(v => !v)}
          className="flex items-center gap-2 text-[15px] font-bold text-white uppercase tracking-wide min-w-0"
        >
          <ChevronDown className={`w-4 h-4 shrink-0 transition-transform ${open ? '' : '-rotate-90'}`} />
          <span className="truncate">{title}</span>
        </button>
        {headerExtra && <div className="flex items-center gap-2 shrink-0">{headerExtra}</div>}
      </div>
      {open && <div className="p-6">{children}</div>}
    </section>
  );
}
