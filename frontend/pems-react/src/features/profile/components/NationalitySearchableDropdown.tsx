import React, { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { ChevronDown, Search, Check } from 'lucide-react';
import { NATIONALITY_OPTIONS, nationalityLabel } from '../constants/nationalities';

interface Props {
  value: string | null;
  onChange: (value: string) => void;
  disabled?: boolean;
}

interface MenuPos {
  left: number;
  top: number;
  width: number;
  openUp: boolean;
}

const MENU_HEIGHT = 290; // search box (~46) + list (max 240) + padding

/** Searchable nationality dropdown (UC-15 §6) — VN/EN search, scrollable, no free text.
 *  The menu is portaled to <body> so it is never clipped by an ancestor's overflow-hidden. */
export function NationalitySearchableDropdown({ value, onChange, disabled }: Props) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [pos, setPos] = useState<MenuPos | null>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);

  const computePosition = () => {
    const btn = buttonRef.current;
    if (!btn) return;
    const rect = btn.getBoundingClientRect();
    const spaceBelow = window.innerHeight - rect.bottom;
    const openUp = spaceBelow < MENU_HEIGHT && rect.top > spaceBelow;
    setPos({
      left: rect.left,
      top: openUp ? rect.top - MENU_HEIGHT - 4 : rect.bottom + 4,
      width: rect.width,
      openUp,
    });
  };

  useLayoutEffect(() => {
    if (!open) return;
    computePosition();
    const onScrollOrResize = () => computePosition();
    window.addEventListener('scroll', onScrollOrResize, true);
    window.addEventListener('resize', onScrollOrResize);
    return () => {
      window.removeEventListener('scroll', onScrollOrResize, true);
      window.removeEventListener('resize', onScrollOrResize);
    };
  }, [open]);

  useEffect(() => {
    if (!open) return;
    function onClickOutside(e: MouseEvent) {
      const target = e.target as Node;
      if (buttonRef.current?.contains(target)) return;
      if (menuRef.current?.contains(target)) return;
      setOpen(false);
    }
    document.addEventListener('mousedown', onClickOutside);
    return () => document.removeEventListener('mousedown', onClickOutside);
  }, [open]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return NATIONALITY_OPTIONS;
    return NATIONALITY_OPTIONS.filter((o) => {
      if (o.label.toLowerCase().includes(q)) return true;
      if (o.value.toLowerCase().includes(q)) return true;
      return o.aliases.some((a) => a.toLowerCase().includes(q));
    });
  }, [query]);

  const toggle = () => {
    if (disabled) return;
    setQuery('');
    setOpen((o) => !o);
  };

  return (
    <>
      <button
        ref={buttonRef}
        type="button"
        disabled={disabled}
        onClick={toggle}
        className="flex w-full items-center justify-between rounded-lg border border-[#b6d4f0] bg-white px-3 py-1.5 text-left font-medium text-gray-900 focus:border-[#004c91] focus:outline-none focus:ring-1 focus:ring-[#004c91] disabled:cursor-not-allowed disabled:bg-gray-50"
      >
        <span className={value ? '' : 'text-gray-400'}>
          {value ? nationalityLabel(value) : 'Chọn quốc tịch'}
        </span>
        <ChevronDown className={`h-4 w-4 flex-shrink-0 text-gray-400 transition-transform ${open ? 'rotate-180' : ''}`} />
      </button>

      {open && pos &&
        createPortal(
          <div
            ref={menuRef}
            style={{ position: 'fixed', left: pos.left, top: pos.top, width: pos.width, zIndex: 1000 }}
            className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-lg"
          >
            <div className="flex items-center gap-2 border-b border-slate-100 px-3 py-2">
              <Search className="h-4 w-4 text-gray-400" />
              <input
                autoFocus
                type="text"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Tìm quốc tịch..."
                className="w-full text-sm text-gray-900 focus:outline-none"
              />
            </div>
            <ul className="max-h-[240px] overflow-y-auto py-1">
              {filtered.length === 0 ? (
                <li className="px-3 py-2 text-sm text-gray-400">Không tìm thấy quốc tịch phù hợp.</li>
              ) : (
                filtered.map((o) => (
                  <li key={o.value}>
                    <button
                      type="button"
                      onClick={() => {
                        onChange(o.value);
                        setOpen(false);
                        setQuery('');
                      }}
                      className="flex w-full items-center justify-between px-3 py-2 text-left text-sm text-gray-700 hover:bg-[#f0f7fc]"
                    >
                      <span>{o.label}</span>
                      {value === o.value && <Check className="h-4 w-4 text-[#004c91]" />}
                    </button>
                  </li>
                ))
              )}
            </ul>
          </div>,
          document.body,
        )}
    </>
  );
}
