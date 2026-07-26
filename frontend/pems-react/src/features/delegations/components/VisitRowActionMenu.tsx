import React, { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { MoreHorizontal } from 'lucide-react';

export interface VisitRowMenuItem {
  key: string;
  label: string;
  icon?: React.ReactNode;
  tone?: 'default' | 'danger';
  /**
   * A CLOSED action, not a hidden one. Present it disabled with {@link disabledReason} when the
   * backend refused it and said why — "Chỉ được chuyển người phụ trách ít nhất 6 giờ trước giờ bắt
   * đầu" teaches the rule; an action that silently disappears teaches nothing. An action the caller
   * has no permission for at all should not be in the list at all.
   */
  disabled?: boolean;
  disabledReason?: string | null;
  onSelect: () => void;
}

interface Props {
  items: VisitRowMenuItem[];
  /** Accessible name of the trigger. */
  label?: string;
  testId?: string;
}

/**
 * The "Thao tác khác" (⋯) menu for a list row.
 *
 * A row can offer up to a dozen things, and putting them all on the row turns every line into a
 * toolbar nobody reads. So the row keeps the two that matter — open it, and do the next thing — and
 * everything else comes to live here, in a list with real text labels (which is also what makes it
 * usable on a phone, where an icon with no word is a guess).
 *
 * It renders through a portal because the list container clips its overflow; an absolutely positioned
 * dropdown inside the row would be cut off at the row boundary.
 */
export function VisitRowActionMenu({ items, label = 'Thao tác khác', testId }: Props) {
  const [open, setOpen] = useState(false);
  const [rect, setRect] = useState<{ top: number; left: number; width: number } | null>(null);
  const [activeIndex, setActiveIndex] = useState(-1);
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const menuRef = useRef<HTMLDivElement | null>(null);

  const enabledIndexes = items.map((item, i) => (item.disabled ? -1 : i)).filter(i => i >= 0);

  const place = useCallback(() => {
    const el = triggerRef.current;
    if (!el) return;
    const r = el.getBoundingClientRect();
    const width = 248;
    // Prefer opening below-right of the trigger; flip when either edge would leave the viewport.
    const left = Math.max(8, Math.min(r.right - width, window.innerWidth - width - 8));
    const openUpwards = r.bottom + 260 > window.innerHeight && r.top > 260;
    setRect({ top: openUpwards ? r.top - 6 : r.bottom + 6, left, width });
  }, []);

  useLayoutEffect(() => {
    if (!open) return;
    place();
    const onMove = () => place();
    window.addEventListener('scroll', onMove, true);
    window.addEventListener('resize', onMove);
    return () => {
      window.removeEventListener('scroll', onMove, true);
      window.removeEventListener('resize', onMove);
    };
  }, [open, place]);

  // Closing has to return focus to the trigger, or a keyboard user is dropped at the top of the page.
  const close = useCallback((restoreFocus = true) => {
    setOpen(false);
    setActiveIndex(-1);
    if (restoreFocus) triggerRef.current?.focus();
  }, []);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { e.stopPropagation(); close(); return; }
      if (e.key === 'Tab') { close(false); return; } // let focus leave naturally
      if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp' && e.key !== 'Home' && e.key !== 'End') return;
      e.preventDefault();
      if (enabledIndexes.length === 0) return;
      const at = enabledIndexes.indexOf(activeIndex);
      const next =
        e.key === 'Home' ? enabledIndexes[0]
          : e.key === 'End' ? enabledIndexes[enabledIndexes.length - 1]
            : e.key === 'ArrowDown' ? enabledIndexes[(at + 1 + enabledIndexes.length) % enabledIndexes.length]
              : enabledIndexes[(at - 1 + enabledIndexes.length) % enabledIndexes.length];
      setActiveIndex(next);
    };
    const onPointerDown = (e: MouseEvent) => {
      const target = e.target as Node;
      if (menuRef.current?.contains(target) || triggerRef.current?.contains(target)) return;
      close(false);
    };
    document.addEventListener('keydown', onKey, true);
    document.addEventListener('mousedown', onPointerDown);
    return () => {
      document.removeEventListener('keydown', onKey, true);
      document.removeEventListener('mousedown', onPointerDown);
    };
  }, [open, activeIndex, enabledIndexes, close]);

  useEffect(() => {
    if (!open || activeIndex < 0) return;
    menuRef.current?.querySelector<HTMLElement>(`[data-index="${activeIndex}"]`)?.focus();
  }, [open, activeIndex]);

  // Nothing to offer → no affordance. An empty menu that opens onto "no actions" is worse than absent.
  if (items.length === 0) return null;

  const run = (item: VisitRowMenuItem) => {
    if (item.disabled) return;
    close(false);
    item.onSelect();
  };

  return (
    <>
      <button
        ref={triggerRef}
        type="button"
        data-testid={testId}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={label}
        title={label}
        onClick={e => {
          e.stopPropagation();
          setOpen(v => !v);
          setActiveIndex(-1);
        }}
        className="inline-flex h-9 w-9 items-center justify-center rounded-lg border border-transparent text-slate-500 outline-none transition-colors cursor-pointer hover:border-slate-200 hover:bg-slate-100 hover:text-[#004c91] focus-visible:ring-2 focus-visible:ring-[#004c91]/40 aria-expanded:bg-slate-100"
      >
        <MoreHorizontal className="h-5 w-5" />
      </button>

      {open && rect && createPortal(
        <div
          ref={menuRef}
          role="menu"
          aria-label={label}
          data-testid={testId ? `${testId}-panel` : undefined}
          onClick={e => e.stopPropagation()}
          style={{ position: 'fixed', top: rect.top, left: rect.left, width: rect.width, zIndex: 120 }}
          className="overflow-hidden rounded-xl border border-slate-200 bg-white py-1 shadow-xl"
        >
          {items.map((item, index) => {
            const danger = item.tone === 'danger';
            return (
              <button
                key={item.key}
                type="button"
                role="menuitem"
                data-index={index}
                data-testid={`row-menu-item-${item.key}`}
                disabled={item.disabled}
                aria-disabled={item.disabled}
                // A disabled entry still needs its reason reachable by hover AND by screen reader,
                // so the sentence goes on title as well as into the visible sub-line below.
                title={item.disabled ? (item.disabledReason ?? item.label) : item.label}
                onClick={() => run(item)}
                className={`flex w-full items-start gap-2.5 px-3 py-2 text-left text-sm outline-none transition-colors ${
                  item.disabled
                    ? 'cursor-not-allowed text-slate-400'
                    : `cursor-pointer ${danger ? 'text-red-600 hover:bg-red-50' : 'text-slate-700 hover:bg-slate-100'} focus-visible:bg-slate-100`
                }`}
              >
                {item.icon && <span className="mt-0.5 shrink-0" aria-hidden>{item.icon}</span>}
                <span className="min-w-0">
                  <span className="block font-semibold leading-snug">{item.label}</span>
                  {item.disabled && item.disabledReason && (
                    <span className="mt-0.5 block text-[11px] font-normal leading-snug text-slate-400">
                      {item.disabledReason}
                    </span>
                  )}
                </span>
              </button>
            );
          })}
        </div>,
        document.body,
      )}
    </>
  );
}
