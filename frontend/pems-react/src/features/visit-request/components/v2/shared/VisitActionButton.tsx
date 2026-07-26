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
 *   refused, deadline  → disabled, with the deadline and the start time spelled out
 *   refused, otherwise → nothing at all
 *
 * A user who is thirty minutes past the window and a user who was never allowed need different
 * answers. Hiding both leaves the first one hunting for a button that was there this morning;
 * greying out both implies the second one could wait and try later. The distinction comes from the
 * backend's stable reason code, never from parsing its message.
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
    capability?.campusName
      ? t('visitRequestV2:mutationCutoff.forCampus', { campus: capability.campusName })
      : null,
    t('visitRequestV2:mutationCutoff.rule', { hours: capability?.requiredLeadHours ?? 6 }),
    capability?.cutoffAt
      ? t('visitRequestV2:mutationCutoff.deadline', { at: formatVietnamDateTime(capability.cutoffAt) })
      : null,
    capability?.plannedStartAt
      ? t('visitRequestV2:mutationCutoff.startsAt', { at: formatVietnamDateTime(capability.plannedStartAt) })
      : null,
  ].filter(Boolean).join('\n');

  return (
    <span className="inline-flex flex-col items-start gap-0.5">
      <button
        type="button"
        data-testid={testId ? `${testId}-disabled` : undefined}
        disabled
        title={explanation}
        aria-describedby={testId ? `${testId}-reason` : undefined}
        className={`${className ?? ''} cursor-not-allowed opacity-50`}
      >
        {icon}
        {children}
      </button>
      {/* The tooltip alone would be unreachable by keyboard and invisible on touch, and this is the
          only place the deadline is stated. */}
      <span
        id={testId ? `${testId}-reason` : undefined}
        data-testid={testId ? `${testId}-reason` : undefined}
        className="max-w-xs whitespace-pre-line text-[11px] leading-snug text-slate-500"
      >
        {explanation}
      </span>
    </span>
  );
}
