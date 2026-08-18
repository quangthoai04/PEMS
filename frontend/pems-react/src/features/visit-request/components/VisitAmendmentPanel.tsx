import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  approveAmendment,
  getActiveAmendment,
  rejectAmendment,
  withdrawAmendment,
  type AmendmentDto,
} from '../api/visitRequestV2Api';
import { showErrorToast, showSuccessToast } from '../../../shared/utils/toast';
import { formatLocalizedDateTime, type UiLanguage } from '../../../shared/utils/vietnamTime';

interface Props {
  visitRequestId: number;
  visitInstanceId: number;
  /** True when the current user is the CURRENT Staff Leader of THIS campus (decision rights). */
  canDecide: boolean;
  /** True when the current user is the requester side (registrant/ACTIVE contact) — may withdraw. */
  canWithdraw: boolean;
  onChanged?: () => void;
}

/** Maps a backend fieldPath to a dot-free i18n key segment under `detail.amendment.fields.*`
 * (fieldPath itself contains dots, which i18next would otherwise parse as nesting). */
const FIELD_LABEL_KEYS: Record<string, string> = {
  'instance.delegationName': 'delegationName',
  'instance.visitType': 'visitType',
  'instance.visitTypeOther': 'visitTypeOther',
  'instance.purpose': 'purpose',
  'instance.workingContent': 'workingContent',
  'instance.workingLanguage': 'workingLanguage',
  'instance.operationalContact.fullName': 'operationalContactFullName',
  'instance.operationalContact.organization': 'operationalContactOrganization',
  'instance.operationalContact.phone': 'operationalContactPhone',
  'instance.operationalContact.email': 'operationalContactEmail',
  'instance.members.visitors': 'membersVisitors',
  'instance.members.externalSupport': 'membersExternalSupport',
  'instance.plannedStartAt': 'plannedStartAt',
  'instance.plannedEndAt': 'plannedEndAt',
};

const pretty = (json: string | null): string => {
  if (json == null) return '—';
  try {
    const value = JSON.parse(json) as unknown;
    if (Array.isArray(value)) {
      return value
        .map(v => (typeof v === 'object' && v !== null ? Object.values(v as Record<string, unknown>).filter(Boolean).join(' · ') : String(v)))
        .join('\n');
    }
    return typeof value === 'string' ? value : JSON.stringify(value);
  } catch {
    return json;
  }
};

/**
 * Pending-amendment panel (plan §9.5): shows the PROPOSED old→new diff clearly separated from the
 * active content; the CURRENT campus Staff Leader approves/rejects (reject requires a reason); the
 * requester may withdraw. Nothing here ever presents the proposal as applied.
 */
export default function VisitAmendmentPanel({
  visitRequestId,
  visitInstanceId,
  canDecide,
  canWithdraw,
  onChanged,
}: Props) {
  const { t, i18n } = useTranslation('visitRequestV2');
  const language = i18n.language as UiLanguage;
  const [amendment, setAmendment] = useState<AmendmentDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [rejectMode, setRejectMode] = useState(false);
  const [note, setNote] = useState('');

  const refresh = useCallback(async () => {
    try {
      setAmendment(await getActiveAmendment(visitRequestId, visitInstanceId));
    } catch {
      setAmendment(null);
    }
  }, [visitRequestId, visitInstanceId]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  if (!amendment) return null;

  const emptyValue = t('detail.amendment.emptyValue');
  const pretty = (json: string | null): string => {
    if (json == null) return emptyValue;
    try {
      const value = JSON.parse(json) as unknown;
      if (Array.isArray(value)) {
        return value
          .map(v => (typeof v === 'object' && v !== null ? Object.values(v as Record<string, unknown>).filter(Boolean).join(' · ') : String(v)))
          .join('\n');
      }
      return typeof value === 'string' ? value : JSON.stringify(value);
    } catch {
      return json;
    }
  };

  // The backend's own success message is discarded — it's fixed Vietnamese prose, not a stable
  // code, and would leak untranslated text into English mode. A fixed localized string keyed by
  // which action just ran is used instead.
  const run = async (fn: () => Promise<{ message: string }>, successMessage: string) => {
    setBusy(true);
    setMessage(null);
    try {
      await fn();
      // Deciding an amendment makes this panel disappear (the proposal is no longer active), so an
      // inline confirmation would be unmounted before it could be read.
      showSuccessToast(successMessage);
      setRejectMode(false);
      setNote('');
      await refresh();
      onChanged?.();
    } catch (err: unknown) {
      showErrorToast(err, t('detail.amendment.processError'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section
      data-testid={`amendment-panel-${visitInstanceId}`}
      aria-label={t('detail.amendment.sectionAria')}
      className="rounded-xl border border-amber-300 dark:border-amber-700 bg-amber-50/60 dark:bg-amber-900/20 p-4"
    >
      <div className="flex flex-wrap items-center gap-2">
        <h3 className="text-sm font-semibold text-amber-900 dark:text-amber-100">
          {t('detail.amendment.heading', { no: amendment.amendmentNo })}
        </h3>
        <span className="rounded bg-amber-200/70 dark:bg-amber-800/60 px-1.5 py-0.5 text-[11px] text-amber-900 dark:text-amber-100">
          {t('detail.amendment.activeUnchangedBadge')}
        </span>
      </div>
      <p className="mt-1 text-xs text-amber-800 dark:text-amber-200">
        {t('detail.amendment.requestedBy', { name: amendment.requestedByName ?? emptyValue })} · {formatLocalizedDateTime(amendment.requestedAt, language)}
        {amendment.reason ? ` · ${t('detail.amendment.reasonSuffix', { reason: amendment.reason })}` : ''}
      </p>

      <div className="mt-3 overflow-x-auto">
        <table className="w-full min-w-[480px] text-left text-xs">
          <caption className="sr-only">{t('detail.amendment.tableCaption')}</caption>
          <thead>
            <tr className="border-b border-amber-200 dark:border-amber-800 text-amber-900 dark:text-amber-100">
              <th scope="col" className="py-1 pr-2 font-medium">{t('detail.amendment.columnField')}</th>
              <th scope="col" className="py-1 pr-2 font-medium">{t('detail.amendment.columnCurrent')}</th>
              <th scope="col" className="py-1 font-medium">{t('detail.amendment.columnProposed')}</th>
            </tr>
          </thead>
          <tbody>
            {amendment.changes.map(c => {
              const labelKey = FIELD_LABEL_KEYS[c.fieldPath];
              return (
                <tr key={c.fieldPath} className="border-b border-amber-100 dark:border-amber-900 align-top">
                  <th scope="row" className="py-1.5 pr-2 font-medium text-gray-800 dark:text-gray-100">
                    {labelKey ? t(`detail.amendment.fields.${labelKey}`) : c.fieldPath}
                  </th>
                  <td className="py-1.5 pr-2 whitespace-pre-wrap text-gray-600 dark:text-gray-300">{pretty(c.oldValueJson)}</td>
                  <td className="py-1.5 whitespace-pre-wrap font-normal text-gray-900 dark:text-gray-50">{pretty(c.newValueJson)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="mt-3 flex flex-wrap gap-2">
        {canDecide && !rejectMode && (
          <>
            <button
              type="button"
              data-testid={`amendment-approve-${amendment.amendmentId}`}
              disabled={busy}
              className="rounded-lg bg-green-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50"
              onClick={() => void run(
                () => approveAmendment(visitInstanceId, amendment.amendmentId, note || undefined),
                t('detail.amendment.approve'),
              )}
            >
              {t('detail.amendment.approve')}
            </button>
            <button
              type="button"
              data-testid={`amendment-reject-${amendment.amendmentId}`}
              disabled={busy}
              className="rounded-lg border border-red-300 px-3 py-1.5 text-sm text-red-600 hover:bg-red-50 dark:border-red-700 dark:text-red-300"
              onClick={() => setRejectMode(true)}
            >
              {t('detail.amendment.reject')}
            </button>
          </>
        )}
        {canDecide && rejectMode && (
          <div className="w-full space-y-2">
            <label htmlFor="amendment-reject-note" className="block text-xs text-amber-900 dark:text-amber-100">
              {t('detail.amendment.rejectReasonLabel')} <span className="text-red-500">*</span>
            </label>
            <textarea
              id="amendment-reject-note"
              required
              rows={2}
              maxLength={500}
              className="w-full rounded-lg border border-amber-300 dark:border-amber-700 bg-white dark:bg-gray-800 p-2 text-sm"
              value={note}
              onChange={e => setNote(e.target.value)}
            />
            <div className="flex gap-2">
              <button
                type="button"
                data-testid={`amendment-reject-confirm-${amendment.amendmentId}`}
                disabled={busy || note.trim().length === 0}
                className="rounded-lg bg-red-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
                onClick={() => void run(
                  () => rejectAmendment(visitInstanceId, amendment.amendmentId, note.trim()),
                  t('detail.amendment.reject'),
                )}
              >
                {t('detail.amendment.rejectConfirm')}
              </button>
              <button
                type="button"
                className="rounded-lg border border-gray-300 dark:border-gray-600 px-3 py-1.5 text-sm"
                onClick={() => setRejectMode(false)}
              >
                {t('detail.amendment.back')}
              </button>
            </div>
          </div>
        )}
        {canWithdraw && !canDecide && (
          <button
            type="button"
            data-testid={`amendment-withdraw-${amendment.amendmentId}`}
            disabled={busy}
            className="rounded-lg border border-gray-300 dark:border-gray-600 px-3 py-1.5 text-sm"
            onClick={() => void run(
              () => withdrawAmendment(visitRequestId, visitInstanceId, amendment.amendmentId),
              t('detail.amendment.withdraw'),
            )}
          >
            {t('detail.amendment.withdraw')}
          </button>
        )}
      </div>
      {message && (
        <p className="mt-2 text-sm text-gray-800 dark:text-gray-100" role="status">
          {message}
        </p>
      )}
    </section>
  );
}
