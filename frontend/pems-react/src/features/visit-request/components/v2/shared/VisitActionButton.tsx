import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { formatVietnamDateTime } from '../../../../../shared/utils/vietnamTime';
import { shouldShowDisabled } from '../../../utils/visitV2Actions';
import type { VisitActionCapability } from '../../../api/visitRequestV2Api';

interface Props {
  capability: VisitActionCapability | undefined;
  /** True when the flat allowedActions list grants this action. */
  granted: boolean;
  onClick: () => void;
  children: ReactNode;
  icon?: ReactNode;
  className?: string;
  'data-testid'?: string;
}

/**
 * Renders ONE backend capability. Three outcomes, and the middle one is the reason this exists:
 *
 *   granted            → an ordinary button
 *   refused, deadline  → disabled, with the deadline in a short tooltip
 *   refused, otherwise → nothing at all
 *
 * A user who is thirty minutes past the window and a user who was never allowed need different
 * answers. Hiding both leaves the first one hunting for a button that was there this morning;
 * greying out both implies the second one could wait and try later. The distinction comes from the
 * backend's stable reason code, never from parsing its message.
 *
 * <p>The disabled reason is a TOOLTIP, not a paragraph printed under every button — a card with
 * several cutoff-gated actions used to grow a full sentence (campus name, rule, deadline, start
 * time) under each one, all repeating what the card above already says. It carries only the two
 * facts that actually change per-action (the rule and the deadline); the campus is implied by the
 * card it sits in, and the start time is elsewhere on the same card.</p>
 *
 * <p>The button stays a REAL, focusable element (`aria-disabled`, not the `disabled` attribute) so
 * the tooltip reaches keyboard focus and a tap on mobile — a native `disabled` button accepts
 * neither. Nothing is wired to fire on click either way, since `granted` is false here.</p>
 */
export function VisitActionButton({
  capability, granted, onClick, children, icon, className, 'data-testid': testId,
}: Props) {
  const { t } = useTranslation(['visitRequestV2']);

  if (granted) {
    return (
      <button type="button" data-testid={testId} onClick={onClick} className={className}>
        {icon}
        {children}
      </button>
    );
  }

  if (!shouldShowDisabled(capability)) return null;

  const explanation = [
    t('visitRequestV2:mutationCutoff.rule', { hours: capability?.requiredLeadHours ?? 6 }),
    capability?.cutoffAt
      ? t('visitRequestV2:mutationCutoff.deadline', { at: formatVietnamDateTime(capability.cutoffAt) })
      : null,
  ].filter(Boolean).join(' ');
  const reasonId = testId ? `${testId}-reason` : undefined;

  return (
    <span className="group/tip relative inline-flex">
      <button
        type="button"
        data-testid={testId ? `${testId}-disabled` : undefined}
        aria-disabled="true"
        aria-describedby={reasonId}
        onClick={(e) => e.preventDefault()}
        className={`${className ?? ''} cursor-not-allowed opacity-50`}
      >
        {icon}
        {children}
      </button>
      <span
        id={reasonId}
        data-testid={reasonId}
        role="tooltip"
        className="pointer-events-none absolute left-1/2 top-full z-20 mt-1.5 w-max max-w-[16rem] -translate-x-1/2 rounded-lg bg-slate-800 px-2.5 py-1.5 text-[11px] font-normal leading-snug text-white opacity-0 shadow-lg transition-opacity duration-150 group-hover/tip:opacity-100 group-focus-within/tip:opacity-100"
      >
        {explanation}
      </span>
    </span>
  );
}
