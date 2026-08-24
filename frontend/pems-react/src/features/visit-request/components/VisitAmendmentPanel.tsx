import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  approveAmendment,
  getActiveAmendment,
  rejectAmendment,
  withdrawAmendment,
  type AmendmentDto,
  type ResolvedMember,
} from '../api/visitRequestV2Api';
import { showErrorToast, showSuccessToast } from '../../../shared/utils/toast';
import { formatLocalizedDateTime, type UiLanguage } from '../../../shared/utils/vietnamTime';
import { AutoGrowTextarea } from './shared/AutoGrowTextarea';

interface Props {
  visitRequestId: number;
  visitInstanceId: number;
  /** True when the current user is the CURRENT Staff Leader of THIS campus (decision rights). */
  canDecide: boolean;
  /** True when the current user is the requester side (registrant/ACTIVE contact) — may withdraw. */
  canWithdraw: boolean;
  onChanged?: () => void;
  /**
   * This campus's CURRENT delegation (visitors + support), for resolving a relationship-only change's
   * OLD/NEW `guestMemberId` values to names (plan CanhIter3FixBug "Đầu mối hiện tại có nằm trong danh
   * sách đoàn không?", §15). Safe to use for both sides of that specific field path: the member SET is
   * provably unchanged for a relationship-only amendment (BuildChangeRows never writes this path in the
   * same amendment as a Visitors/SupportMembers change), so whoever either id names is still on the
   * current roster — never a guess reconstructed from a stale or partial snapshot.
   */
  members?: ResolvedMember[];
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
  // No raw UUID may ever reach the DOM (plan CanhIter3FixBug FIX-K) — an entry here means this path is
  // never left to fall through to the bare fieldPath string, and renderMemberContactKey below resolves
  // the value to a name instead of printing the key.
  'instance.operationalContact.clientMemberKey': 'operationalContactMemberKey',
  // Same no-raw-value guarantee for the PERSISTENT counterpart (plan CanhIter3FixBug) — a bare
  // GuestMemberId is exactly as unreadable to a reviewer as a UUID, just a shorter number.
  'instance.operationalContact.guestMemberId': 'operationalContactMemberKey',
  'instance.plannedStartAt': 'plannedStartAt',
  'instance.plannedEndAt': 'plannedEndAt',
};

/** Business-facing fields of one delegation member — the ONLY shape ever rendered for a member-list
 * change row. The backend's change JSON is the FULL VisitorDto/SupportTeamMemberDto, which also carries
 * `organizationPartnerId` (internal numeric id) and `clientMemberKey` (a per-submission UUID) — an
 * `Object.values(...)`-style render used to print both into the review table (plan FIX-K). */
interface MemberPresentation {
  fullName?: unknown;
  jobTitle?: unknown;
  organization?: unknown;
  nationality?: unknown;
  clientMemberKey?: unknown;
}

const isMemberListPath = (fieldPath: string): boolean =>
  fieldPath === 'instance.members.visitors' || fieldPath === 'instance.members.externalSupport';

/** Allow-list rendering for a Visitors/SupportMembers change row — never the raw DTO. */
const renderMemberList = (json: string | null, emptyValue: string): string => {
  if (json == null) return emptyValue;
  try {
    const value = JSON.parse(json) as unknown;
    if (!Array.isArray(value)) return emptyValue;
    if (value.length === 0) return emptyValue;
    return value
      .map(entry => {
        const m = entry as MemberPresentation;
        return [m.fullName, m.jobTitle, m.organization, m.nationality]
          .filter((f): f is string => typeof f === 'string' && f.length > 0)
          .join(' · ');
      })
      .join('\n');
  } catch {
    return emptyValue;
  }
};

/** Every member row named across the amendment's OWN Visitors/SupportMembers change rows, keyed by the
 * per-submission `clientMemberKey` the proposal minted for it — built from the NEW (proposed) values
 * only, since the ACTIVE snapshot never carries a client key of its own (NP-03: a saved row has none).
 * This is what lets `instance.operationalContact.clientMemberKey`'s value resolve to a name instead of
 * a bare UUID, without ever guessing an identity the backend did not already name. */
const buildMemberNamesByKey = (changes: AmendmentDto['changes']): Map<string, string> => {
  const byKey = new Map<string, string>();
  for (const c of changes) {
    if (!isMemberListPath(c.fieldPath) || c.newValueJson == null) continue;
    try {
      const value = JSON.parse(c.newValueJson) as unknown;
      if (!Array.isArray(value)) continue;
      for (const entry of value) {
        const m = entry as MemberPresentation;
        if (typeof m.clientMemberKey === 'string' && m.clientMemberKey && typeof m.fullName === 'string') {
          byKey.set(m.clientMemberKey, m.fullName);
        }
      }
    } catch {
      // Malformed JSON on this row is reported as emptyValue by renderMemberList; nothing to index here.
    }
  }
  return byKey;
};

/** Resolves the operational-contact member-key change row to a NAME, never the raw key. The machine
 * identity (the key itself) is used only to look up a name here — it never reaches the DOM on its own,
 * and an unresolvable key (a legacy/cross-submission value this amendment's own rows cannot explain)
 * gets a generic label rather than a guess. */
const renderMemberContactKey = (
  json: string | null,
  namesByKey: Map<string, string>,
  emptyValue: string,
  notInDelegationLabel: string,
  changedLabel: string,
): string => {
  if (json == null) return notInDelegationLabel;
  let key: unknown;
  try {
    key = JSON.parse(json) as unknown;
  } catch {
    return emptyValue;
  }
  if (typeof key !== 'string' || key.length === 0) return notInDelegationLabel;
  return namesByKey.get(key) ?? changedLabel;
};

/** Resolves the PERSISTENT contact-relationship change row (`instance.operationalContact.guestMemberId`,
 * plan CanhIter3FixBug) to a NAME from the campus's CURRENT roster — never the raw numeric id. Safe for
 * both the OLD and NEW value: this field path is only ever written when the member list itself is
 * unchanged, so the current roster already contains whoever either side names. A value the current
 * roster cannot explain (the campus's membership moved AFTER this snapshot, through some other path)
 * falls back to the same generic label the ephemeral-key resolver uses — never a guess. */
const renderGuestMemberId = (
  json: string | null,
  namesById: Map<number, string>,
  notInDelegationLabel: string,
  changedLabel: string,
): string => {
  if (json == null) return notInDelegationLabel;
  let id: unknown;
  try {
    id = JSON.parse(json) as unknown;
  } catch {
    return changedLabel;
  }
  if (id === null) return notInDelegationLabel;
  if (typeof id !== 'number') return changedLabel;
  return namesById.get(id) ?? changedLabel;
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
  members,
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
  const notInDelegationLabel = t('detail.amendment.contactKeyNotInDelegation');
  const changedLabel = t('detail.amendment.contactKeyChanged');
  // Built once per render from THIS amendment's own member-list rows — see buildMemberNamesByKey.
  const memberNamesByKey = buildMemberNamesByKey(amendment.changes);
  // The campus's CURRENT roster, keyed by GuestMemberId — see renderGuestMemberId and the `members`
  // prop doc comment for why this is safe for a relationship-only change's old AND new value alike.
  const memberNamesById = new Map((members ?? []).map(m => [m.guestMemberId, m.fullName]));

  /** Per-fieldPath rendering — member lists and the contact member-key never go through a generic
   * "stringify whatever's there" path (plan CanhIter3FixBug FIX-K), because that is exactly how a
   * UUID or a numeric partner id used to leak into this table. Every other field is plain business
   * data (dates, free text, enums) with no internal identity to protect. */
  const pretty = (fieldPath: string, json: string | null): string => {
    if (isMemberListPath(fieldPath)) return renderMemberList(json, emptyValue);
    if (fieldPath === 'instance.operationalContact.clientMemberKey') {
      return renderMemberContactKey(json, memberNamesByKey, emptyValue, notInDelegationLabel, changedLabel);
    }
    if (fieldPath === 'instance.operationalContact.guestMemberId') {
      return renderGuestMemberId(json, memberNamesById, notInDelegationLabel, changedLabel);
    }
    if (json == null) return emptyValue;
    try {
      const value = JSON.parse(json) as unknown;
      // Every remaining known path is a scalar (string/number/bool) in this table — an unexpected
      // object here is an unhandled shape, not something to print field-by-field.
      if (value !== null && typeof value === 'object') return emptyValue;
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
                    {/* An unknown path never renders the raw internal fieldPath string (plan FIX-K
                        §13.4) — a generic label, so a backend field the frontend hasn't mapped yet
                        degrades to "something changed" rather than leaking implementation detail. */}
                    {labelKey ? t(`detail.amendment.fields.${labelKey}`) : t('detail.amendment.fields.unknown')}
                  </th>
                  <td className="py-1.5 pr-2 whitespace-pre-wrap text-gray-600 dark:text-gray-300">{pretty(c.fieldPath, c.oldValueJson)}</td>
                  <td className="py-1.5 whitespace-pre-wrap font-normal text-gray-900 dark:text-gray-50">{pretty(c.fieldPath, c.newValueJson)}</td>
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
            <AutoGrowTextarea
              id="amendment-reject-note"
              required
              minRows={2}
              maxLength={500}
              value={note}
              onChange={setNote}
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
