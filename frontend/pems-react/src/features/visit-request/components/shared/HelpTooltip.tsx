import React from 'react';
import { Info } from 'lucide-react';
import { useTranslation } from 'react-i18next';

interface HelpTooltipProps {
  content: React.ReactNode;
  className?: string;
  /** Accessible name for the trigger — what the icon is ABOUT, not "help". */
  label?: string;
  /** Test hook for the trigger, so a test can open the tooltip the way a user does. */
  testId?: string;
  /**
   * Which side of the trigger the bubble opens on. Defaults to `'top'` (unchanged behavior for every
   * existing caller). A trigger sitting near the top of a scrollable panel — e.g. right under a modal's
   * fixed header — has no room to open upward: the bubble renders past the edge of the viewport and its
   * first line is clipped, which is illegible rather than merely ugly. Pass `'bottom'` for any trigger
   * in that position.
   */
  placement?: 'top' | 'bottom';
  /**
   * Horizontal alignment of the bubble relative to the trigger. Defaults to `'center'` (unchanged
   * behavior for every existing caller): the bubble is centered on the icon. A trigger sitting near the
   * LEFT edge of a narrow scrollable panel has no room to center — the bubble's own width extends
   * further left than the panel has, clipping its first/last words against the edge. Pass `'start'` to
   * anchor the bubble's left edge to the trigger instead (it grows rightward), or `'end'` to anchor its
   * right edge (it grows leftward).
   */
  align?: 'center' | 'start' | 'end';
}

let tooltipSeq = 0;

/**
 * The `i` next to a label, holding the explanation that would otherwise be a standing paragraph
 * under the field (UX-01).
 *
 * <p>This used to be a hover-only `<div>`: no keyboard could reach it, a tap did nothing, and the
 * text it hid was announced to nobody. Guidance that only exists for a mouse is guidance half the
 * users cannot have — and the field it explains ("Đầu mối là ai trong đoàn?") is one where not
 * reading it means picking the wrong person.</p>
 *
 * <p>Three ways in, because they are three different users: hover for a pointer, focus for a
 * keyboard, click for a touch screen where hover does not exist. <c>aria-describedby</c> is what
 * makes the text part of the field's description rather than decoration — the HTML <c>title</c>
 * attribute is deliberately not used, since it is unreachable by keyboard and unreliable on touch.</p>
 */
export const HelpTooltip: React.FC<HelpTooltipProps> = ({
  content, className = '', label, testId, placement = 'top', align = 'center',
}) => {
  const { t } = useTranslation('common');
  const [open, setOpen] = React.useState(false);
  // Stable across renders; `useId` is React 18+, and this file is imported by tests that render
  // the component many times, so a module counter keeps the ids readable and unique either way.
  const id = React.useRef(`help-tooltip-${++tooltipSeq}`).current;
  const bubblePositionCls = align === 'start' ? 'left-0' : align === 'end' ? 'right-0' : 'left-1/2 -translate-x-1/2';
  const arrowPositionCls = align === 'start' ? 'left-4' : align === 'end' ? 'right-4' : 'left-1/2 -translate-x-1/2';

  return (
    <div className={`relative inline-flex items-center align-middle ml-1.5 ${className}`}>
      <button
        type="button"
        data-testid={testId}
        aria-label={label ?? t('help')}
        aria-expanded={open}
        // Present only while the tooltip is on screen: pointing at a hidden element would have a
        // screen reader read out a description the sighted user is not being shown.
        aria-describedby={open ? id : undefined}
        className="inline-flex items-center rounded-full text-slate-400 transition-colors hover:text-[#004c91] focus:text-[#004c91] focus:outline-none focus-visible:ring-2 focus-visible:ring-[#004c91]"
        onMouseEnter={() => setOpen(true)}
        onMouseLeave={() => setOpen(false)}
        onFocus={() => setOpen(true)}
        onBlur={() => setOpen(false)}
        onClick={e => {
          // The trigger often sits inside a <label>/<legend>; without this a tap would also
          // activate the field the label points at.
          e.preventDefault();
          e.stopPropagation();
          setOpen(v => !v);
        }}
        onKeyDown={e => {
          if (e.key === 'Escape') setOpen(false);
        }}
      >
        <Info className="h-4 w-4" />
      </button>
      <div
        id={id}
        role="tooltip"
        hidden={!open}
        className={
          'pointer-events-none absolute z-50 w-max max-w-xs ' + bubblePositionCls + ' '
          + (placement === 'bottom' ? 'top-full mt-2' : 'bottom-full mb-2')
        }
      >
        <div
          className={
            'relative whitespace-pre-line rounded-lg bg-slate-800 px-3 py-2 text-[12px] font-normal leading-5 '
            + 'text-white shadow-xl '
            + (align === 'center' ? 'text-center' : 'text-left')
          }
        >
          {content}
          <div
            className={
              'absolute border-4 border-transparent ' + arrowPositionCls + ' '
              + (placement === 'bottom'
                ? 'bottom-full -mb-px border-b-slate-800'
                : 'top-full -mt-px border-t-slate-800')
            }
          />
        </div>
      </div>
    </div>
  );
};
