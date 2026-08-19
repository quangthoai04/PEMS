import { useCallback, useLayoutEffect, useRef, useState } from 'react';

export interface AnchoredPanelRect {
  top: number;
  left: number;
  maxWidth: number;
}

/**
 * Viewport-aware placement for a dropdown/popover panel anchored to a trigger element.
 *
 * Extracted from the placement logic already proven in VisitRowActionMenu.tsx (the row "..." menu):
 * flip above the trigger when there isn't enough room below, and clamp the left edge so the panel
 * never runs past either side of the viewport. Generalizes it so other trigger-anchored panels
 * (filter dropdowns, date popovers) don't each hand-roll their own `absolute left-0` positioning,
 * which is what let RC-07's filter popovers run off-screen from a trigger near the viewport edge --
 * width alone (`max-w-[calc(100vw-2rem)]`) does not fix that, the ANCHOR POINT has to move too.
 *
 * `preferredWidth` is either a fixed pixel target, or the literal `'trigger'` to make the panel
 * match the trigger's own rendered width (e.g. a filter dropdown whose panel used to be `w-full`
 * inside the trigger's own relative wrapper) -- clamped to the viewport either way.
 *
 * Returns `null` while closed/unmeasured; the caller should render the panel with
 * `position: fixed` at the returned `top`/`left`, `width`/`max-width: rect.maxWidth`.
 */
export function useAnchoredPanelPosition(open: boolean, preferredWidth: number | 'trigger', gutter = 8) {
  const triggerRef = useRef<HTMLElement | null>(null);
  const panelRef = useRef<HTMLElement | null>(null);
  const [rect, setRect] = useState<AnchoredPanelRect | null>(null);

  const place = useCallback(() => {
    const el = triggerRef.current;
    if (!el) return;
    const r = el.getBoundingClientRect();
    const vw = window.innerWidth;
    const vh = window.innerHeight;
    const wanted = preferredWidth === 'trigger' ? r.width : preferredWidth;
    const maxWidth = Math.min(wanted, vw - gutter * 2);
    // Prefer aligning the panel's left edge with the trigger's left edge; clamp so neither edge
    // of the panel ever leaves the viewport (the fix a plain `left-0` can't provide from a trigger
    // sitting near the right edge of a wrapping filter bar).
    const left = Math.max(gutter, Math.min(r.left, vw - maxWidth - gutter));
    // Panel height isn't known ahead of render; a generous 320px estimate for the flip decision
    // matches the max-h these filter/date panels already cap themselves to (max-h-72 = 288px +
    // header/footer padding). Flip above only when there is truly more room there.
    const estimatedPanelHeight = 320;
    const openUpwards = r.bottom + estimatedPanelHeight > vh && r.top > estimatedPanelHeight;
    const top = openUpwards ? Math.max(gutter, r.top - estimatedPanelHeight) : r.bottom + 4;
    setRect({ top, left, maxWidth });
  }, [preferredWidth, gutter]);

  useLayoutEffect(() => {
    if (!open) { setRect(null); return; }
    place();
    const onMove = () => place();
    window.addEventListener('scroll', onMove, true);
    window.addEventListener('resize', onMove);
    return () => {
      window.removeEventListener('scroll', onMove, true);
      window.removeEventListener('resize', onMove);
    };
  }, [open, place]);

  return { triggerRef, panelRef, rect };
}
