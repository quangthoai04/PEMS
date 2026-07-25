import React from 'react';
import { useTranslation } from 'react-i18next';
import {
  VISIT_STATUS_TONE_CLASSES,
  visitStatusI18nKey,
  visitStatusTone,
  type VisitStatusKind,
} from './visitStatus';

interface Props {
  /** Which enum this value comes from — the two are not interchangeable. */
  kind: VisitStatusKind;
  status: string | null | undefined;
  className?: string;
  'data-testid'?: string;
}

/**
 * The only way a visit status reaches the screen. It never renders the raw enum: an unrecognised
 * value shows the neutral "unknown" label instead, so a schema value the UI has not been taught
 * about degrades to plain language rather than shouting WAITING_REQUEST_APPROVAL at a visitor.
 */
export const VisitStatusBadge: React.FC<Props> = ({ kind, status, className = '', ...rest }) => {
  const { t } = useTranslation(['visitRequestV2']);
  const tone = visitStatusTone(kind, status);

  return (
    <span
      data-testid={rest['data-testid']}
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-bold ring-1 ring-inset ${VISIT_STATUS_TONE_CLASSES[tone]} ${className}`}
    >
      {t(visitStatusI18nKey(kind, status))}
    </span>
  );
};
