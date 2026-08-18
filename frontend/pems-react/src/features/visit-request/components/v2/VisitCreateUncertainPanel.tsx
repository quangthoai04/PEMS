import React from 'react';
import { AlertTriangle, Loader2, RefreshCw, Undo2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { VisitSubmissionLookup } from '../../api/visitRequestV2Api';

interface Props {
  isChecking: boolean;
  /** The last answer from the lookup, if one has been asked for. */
  lookup: VisitSubmissionLookup | null;
  /** A failure of the LOOKUP itself — distinct from the lookup saying "not found". */
  error: string | null;
  onCheck: () => void;
  onBackToForm: () => void;
}

/**
 * "We do not know yet" (plan §10).
 *
 * A verify that dies without a reply is genuinely ambiguous: the backend consumes the OTP in the
 * SAME transaction as the create, so the request may well exist. The dangerous thing to say here is
 * "failed" — that is what makes somebody fill the whole form in again and file a duplicate. So this
 * panel says exactly what is known, refuses to guess, and offers the one action that can settle it:
 * ask the server about the submit intent.
 *
 * Nothing on this panel can create a request. Checking is a read.
 */
export const VisitCreateUncertainPanel: React.FC<Props> = ({
  isChecking, lookup, error, onCheck, onBackToForm,
}) => {
  const { t } = useTranslation(['visitRequestV2']);

  return (
    <div
      role="alert"
      data-testid="v2-uncertain"
      className="rounded-2xl border border-amber-300 bg-amber-50 p-5"
    >
      <div className="flex items-start gap-3">
        <AlertTriangle className="mt-0.5 h-6 w-6 shrink-0 text-amber-600" />
        <div className="min-w-0">
          <h2 className="text-lg font-extrabold text-amber-900">
            {t('visitRequestV2:uncertain.title')}
          </h2>
          <p className="mt-1 text-sm text-amber-900">{t('visitRequestV2:uncertain.body')}</p>
          <p className="mt-1 text-sm font-normal text-amber-900">{t('visitRequestV2:uncertain.doNotResend')}</p>
        </div>
      </div>

      {/* What the lookup answered. COMPLETED never reaches here — the caller promotes straight to
          the success screen — so these are the states that still need the user to decide. */}
      {lookup?.state === 'PENDING' && (
        <p data-testid="v2-uncertain-pending" className="mt-4 rounded-xl bg-white/70 px-3 py-2.5 text-sm font-normal text-amber-900">
          {t('visitRequestV2:uncertain.statePending')}
        </p>
      )}
      {lookup?.state === 'FAILED' && (
        <p data-testid="v2-uncertain-failed" className="mt-4 rounded-xl bg-white/70 px-3 py-2.5 text-sm font-normal text-amber-900">
          {t('visitRequestV2:uncertain.stateFailed')}
        </p>
      )}
      {lookup?.state === 'NOT_FOUND' && (
        <p data-testid="v2-uncertain-notfound" className="mt-4 rounded-xl bg-white/70 px-3 py-2.5 text-sm font-normal text-amber-900">
          {t('visitRequestV2:uncertain.stateNotFound')}
        </p>
      )}
      {error && (
        <p data-testid="v2-uncertain-error" className="mt-4 rounded-xl bg-red-50 px-3 py-2.5 text-sm font-normal text-red-700">
          {error}
        </p>
      )}

      <div className="mt-5 flex flex-wrap gap-2">
        <button
          type="button"
          data-testid="v2-uncertain-check"
          disabled={isChecking}
          onClick={onCheck}
          className="inline-flex items-center gap-2 rounded-xl bg-[#004c91] px-4 py-2.5 text-sm font-bold text-white hover:bg-[#003a6f] disabled:opacity-60"
        >
          {isChecking
            ? <><Loader2 className="h-4 w-4 animate-spin" /> {t('visitRequestV2:uncertain.checking')}</>
            : <><RefreshCw className="h-4 w-4" /> {t('visitRequestV2:uncertain.check')}</>}
        </button>
        <button
          type="button"
          data-testid="v2-uncertain-back"
          disabled={isChecking}
          onClick={onBackToForm}
          className="inline-flex items-center gap-2 rounded-xl border border-amber-300 bg-white px-4 py-2.5 text-sm font-bold text-amber-900 hover:bg-amber-100 disabled:opacity-60"
        >
          <Undo2 className="h-4 w-4" /> {t('visitRequestV2:uncertain.backToForm')}
        </button>
      </div>
    </div>
  );
};
