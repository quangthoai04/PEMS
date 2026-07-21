import React from 'react';
import { CheckCircle2, Info } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { VisitRequestV2SubmittedSummary } from './VisitRequestV2SubmittedSummary';
import type { V2CreateResponse } from '../../api/visitRequestV2Api';
import type { VisitRequestV2Schema } from '../../schema/visitRequestV2.schema';

interface Props {
  response: V2CreateResponse;
  values: VisitRequestV2Schema;
  /** Rendered under the receipt — a link home on the route, a Close button in the modal. */
  footer?: React.ReactNode;
}

/**
 * Post-submit receipt for a v2 create, shared by the standalone route and the modal shell so the
 * confirmation a user sees never depends on which surface they started from.
 */
export const VisitRequestV2SuccessPanel: React.FC<Props> = ({ response, values, footer }) => {
  const { t } = useTranslation(['visitRequestV2']);

  return (
    <>
      <div className="rounded-2xl border border-green-200 bg-green-50 p-6">
        <div className="flex items-center gap-3">
          <CheckCircle2 className="h-8 w-8 shrink-0 text-green-600" />
          <div>
            <h2 className="text-lg font-extrabold text-green-900">{t('visitRequestV2:success.title')}</h2>
            <p className="text-sm text-green-800">
              {t('visitRequestV2:success.requestCode', { code: response.requestCode })}
            </p>
          </div>
        </div>
        <ul className="mt-4 space-y-1 text-sm text-green-900">
          <li>
            {t('visitRequestV2:success.campusCount', { count: response.instances.length })}
            {response.hasMixedCampusDetails ? ` — ${t('visitRequestV2:success.mixedNote')}` : ''}
          </li>
          {response.idempotent && <li>{t('visitRequestV2:success.idempotentReplay')}</li>}
        </ul>
        {response.contactClaimPending && (
          <div className="mt-4 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800" role="status">
            <Info className="mt-0.5 h-4 w-4 shrink-0" />
            <p>{t('visitRequestV2:success.claimPending')}</p>
          </div>
        )}
        {footer && <div className="mt-6">{footer}</div>}
      </div>

      {/* Full per-campus summary from the immutable submitted snapshot. */}
      <div className="mt-6">
        <VisitRequestV2SubmittedSummary response={response} values={values} />
      </div>
    </>
  );
};
