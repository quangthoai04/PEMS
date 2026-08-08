import React, { useState } from 'react';
import { CheckCircle2, ExternalLink, FilePlus2, Info, List, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { VisitRequestV2SubmittedSummary } from './VisitRequestV2SubmittedSummary';
import { formatVietnamDateTime } from '../../../../shared/utils/vietnamTime';
import { useRegistrationCampuses } from '../../hooks/useRegistrationCampuses';
import type { V2CreateResponse } from '../../api/visitRequestV2Api';
import type { VisitRequestV2Schema } from '../../schema/visitRequestV2.schema';

interface Props {
  response: V2CreateResponse;
  /**
   * The IMMUTABLE snapshot of what was submitted, deep-cloned before the request left the browser.
   * "Xem lại thông tin đã gửi" renders from THIS and never re-reads the form or asks the server:
   * the public flow has no session, so there is no detail endpoint it could legitimately call.
   */
  values: VisitRequestV2Schema;
  /**
   * Session-dependent actions. Each is optional because not every surface can honour it: an
   * anonymous visitor has no session, so sending them to a dashboard route would only bounce them
   * to a login screen. Offering an action that cannot work is worse than not offering it.
   */
  onViewRequest?: () => void;
  onGoToList?: () => void;
  onCreateAnother?: () => void;
  /** Dismisses the receipt. The modal shell supplies it; the standalone route uses `footer`. */
  onClose?: () => void;
  /** Rendered under the actions — a link home on the public route. */
  footer?: React.ReactNode;
}

const statusKey = (status: string) => `visitRequestV2:success.status.${status}`;

/**
 * Post-submit receipt for a v2 create, shared by the standalone route and the modal shell so the
 * confirmation a user sees never depends on which surface they started from. Only the ACTIONS
 * differ, driven by what the caller can actually reach.
 *
 * This is deliberately a SCREEN and not a toast. The request code is the only handle the user has
 * on what they just filed — a notification that disappears after four seconds is not somewhere to
 * put it. The toast is a companion, not the record.
 */
export const VisitRequestV2SuccessPanel: React.FC<Props> = ({
  response, values, onViewRequest, onGoToList, onCreateAnother, onClose, footer,
}) => {
  const { t } = useTranslation(['visitRequestV2']);
  const [showSubmitted, setShowSubmitted] = useState(false);
  const { campuses } = useRegistrationCampuses();

  // The lookup-recovered receipt (an uncertain result that turned out COMPLETED) knows the request
  // exists but not its campus breakdown — it answered an anonymous caller. Show what is known.
  const recovered = response.recoveredByLookup === true;
  const code = response.requestCode;

  // Campuses whose own operational contact has not answered yet. Counted per campus by the server —
  // there is no request-level contact to be "pending" on.
  const contactsPending = response.pendingContactConfirmations;

  // Emails of operational contacts that differ from the registrant — these are the ones awaiting
  // confirmation. Deduplicated because two campuses may share the same contact.
  const registrantEmailNorm = values.registerInfo.email.trim().toLowerCase();

  // Per-campus "TênCơSở (email)" pairs for contacts that still need to confirm.
  const pendingCampusContacts = values.campusVisits
    .filter(cv => (cv.operationalContact?.email?.trim() ?? '').toLowerCase() !== registrantEmailNorm)
    .map(cv => {
      const name = campuses.find(c => c.campusCode === cv.campus)?.campusName ?? cv.campus;
      const email = cv.operationalContact?.email?.trim() ?? '';
      return email ? `${name} (${email})` : name;
    })
    .join(', ');

  // Names, not a count — "Campuses: 1" tells the reader nothing they can act on; "Hà Nội" does.
  // Read from the submitted snapshot (always present, even on a lookup-recovered receipt) rather
  // than the response, which carries campus IDs but not a name.
  const campusNames = values.campusVisits
    .map(cv => campuses.find(c => c.campusCode === cv.campus)?.campusName ?? cv.campus)
    .join(', ');

  const actionBtn = 'inline-flex items-center gap-2 rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-bold text-slate-700 hover:bg-slate-50';

  return (
    <>
      <div className="rounded-2xl border border-green-200 bg-green-50 p-6">
        <div className="flex items-center gap-3">
          <CheckCircle2 className="h-8 w-8 shrink-0 text-green-600" />
          <div className="min-w-0">
            <h2 className="text-lg font-extrabold text-green-900">{t('visitRequestV2:success.title')}</h2>
            <p data-testid="v2-success-code" className="text-sm text-green-800">
              {t('visitRequestV2:success.requestCode', { code })}
            </p>
          </div>
        </div>

        <dl className="mt-4 flex flex-wrap items-baseline gap-x-6 gap-y-1 text-sm text-green-900">
          {response.status && (
            <div className="flex gap-2">
              <dt className="font-semibold">{t('visitRequestV2:success.statusLabel')}</dt>
              <dd data-testid="v2-success-status">
                {t([statusKey(response.status), 'visitRequestV2:success.status.UNKNOWN'], { status: response.status })}
              </dd>
            </div>
          )}
          <div className="flex gap-2">
            <dt className="font-semibold">{t('visitRequestV2:success.campusCountLabel')}</dt>
            <dd data-testid="v2-success-campuses">
              {campusNames}
            </dd>
          </div>
          {response.submittedAt && (
            <div className="flex gap-2">
              <dt className="font-semibold">{t('visitRequestV2:success.submittedAtLabel')}</dt>
              <dd data-testid="v2-success-submitted-at">{formatVietnamDateTime(response.submittedAt)}</dd>
            </div>
          )}
        </dl>

        {response.idempotent && (
          <p className="mt-4 text-sm text-green-800">{t('visitRequestV2:success.idempotentReplay')}</p>
        )}

        {/* Always shown — "how do I track this?" is a question on every receipt, not only when a
            campus is still waiting on its operational contact. The pending bullet is prepended when
            it applies; the track-status bullet's own wording adapts ("Đồng thời…" only makes sense
            after a first bullet) rather than always assuming something precedes it.

            The pending bullet counts CAMPUSES, not a single request-level contact: each campus has
            its own operational contact, and the confirmation gate stays shut until every one of
            them has answered. */}
        <div className="mt-4 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800" role="status">
          <Info className="mt-0.5 h-4 w-4 shrink-0" />
          <ul className="list-disc space-y-1 pl-4">
            {contactsPending > 0 && (
              <li>{t('visitRequestV2:success.claimPending', { campusContacts: pendingCampusContacts })}</li>
            )}
            <li>
              {t(
                contactsPending > 0
                  ? 'visitRequestV2:success.trackStatusAlso'
                  : 'visitRequestV2:success.trackStatus',
                { email: values.registerInfo.email },
              )}
            </li>
          </ul>
        </div>

        <div className="mt-6 flex flex-wrap gap-2">
          {onViewRequest && (
            <button
              type="button"
              data-testid="v2-success-view"
              onClick={onViewRequest}
              className="inline-flex items-center gap-2 rounded-xl bg-[#004c91] px-4 py-2.5 text-sm font-bold text-white hover:bg-[#003a6f]"
            >
              <ExternalLink className="h-4 w-4" /> {t('visitRequestV2:success.viewRequest')}
            </button>
          )}
          {onGoToList && (
            <button type="button" data-testid="v2-success-list" onClick={onGoToList} className={actionBtn}>
              <List className="h-4 w-4" /> {t('visitRequestV2:success.goToList')}
            </button>
          )}
          {/* Available on EVERY surface, because it needs nothing but the snapshot already in
              memory — this is how an anonymous visitor reads back what they sent. */}
          {!recovered && (
            <button
              type="button"
              data-testid="v2-success-review"
              aria-expanded={showSubmitted}
              aria-controls="v2-success-submitted"
              onClick={() => setShowSubmitted(v => !v)}
              className={onViewRequest ? actionBtn : 'inline-flex items-center gap-2 rounded-xl bg-[#004c91] px-4 py-2.5 text-sm font-bold text-white hover:bg-[#003a6f]'}
            >
              <List className="h-4 w-4" />
              {showSubmitted
                ? t('visitRequestV2:success.hideSubmitted')
                : t('visitRequestV2:success.reviewSubmitted')}
            </button>
          )}
          {/* No "copy the code" action: the code is on the receipt above (and in the summary and
              the confirmation email), where it can be selected like any other text. */}
          {onCreateAnother && (
            <button type="button" data-testid="v2-success-new" onClick={onCreateAnother} className={actionBtn}>
              <FilePlus2 className="h-4 w-4" /> {t('visitRequestV2:success.createAnother')}
            </button>
          )}
          {onClose && (
            <button type="button" data-testid="v2-success-close" onClick={onClose} className={actionBtn}>
              <X className="h-4 w-4" /> {t('visitRequestV2:success.close')}
            </button>
          )}
        </div>

        {footer && <div className="mt-6">{footer}</div>}
      </div>

      {/* Full per-campus summary from the immutable submitted snapshot — revealed on request rather
          than always on, so the receipt leads with the one thing the user must not lose (the code).
          Skipped entirely for a lookup-recovered receipt: that path never received the per-campus
          response, and an empty summary would read as "no campuses" rather than "not returned here". */}
      {!recovered && showSubmitted && (
        <div id="v2-success-submitted" className="mt-6">
          <VisitRequestV2SubmittedSummary response={response} values={values} />
        </div>
      )}
    </>
  );
};
