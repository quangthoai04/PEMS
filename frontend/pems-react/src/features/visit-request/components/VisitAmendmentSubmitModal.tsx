import { useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { HelpCircle, Plus, Trash2, X } from 'lucide-react';
import {
  submitAmendment,
  type AmendmentProposalPayload,
  type ResolvedCampusVisit,
  type ResolvedMember,
} from '../api/visitRequestV2Api';
import { AmendmentErrorCode, errorCodeOf } from '../utils/visitV2Actions';
import { showSuccessToast } from '../../../shared/utils/toast';
import { isValidPhone } from '../../../shared/utils/phoneNumber';

interface Props {
  visitRequestId: number;
  campus: ResolvedCampusVisit;
  onClose: () => void;
  onSubmitted: () => void;
}

/** ISO (+07:00) → "YYYY-MM-DDTHH:mm" wall-clock for a datetime-local input (no timezone shift). */
const toLocalInput = (iso: string): string => (iso ? iso.slice(0, 16) : '');

/** Mirrors VisitAmendmentService.MinDurationMinutes — the server stays the authority. */
const MIN_DURATION_MINUTES = 30;

/** Which fields a proposal can be wrong in. Keyed so each message renders under its own input. */
type FieldErrors = Partial<Record<
  'delegationName' | 'visitTypeOther' | 'purpose' | 'start' | 'end' | 'contactPhone' | 'visitors' | 'reason',
  string
>>;

/** A member row with a STABLE client key (never the array index) so add/remove keeps React identity. */
interface EditableMember {
  key: string;
  fullName: string;
  jobTitle: string;
  organization: string;
  nationality: string;
}

/** Deep-clone the read-model members into editable rows — no reference is shared with `campus`. */
const cloneMembers = (members: ResolvedMember[], prefix: string): EditableMember[] =>
  members.map((m, i) => ({
    key: `${prefix}-${i}`,
    fullName: m.fullName ?? '',
    jobTitle: m.jobTitle ?? '',
    organization: m.organization ?? '',
    nationality: m.nationality ?? '',
  }));

const normalize = (m: EditableMember): string =>
  JSON.stringify([m.fullName.trim(), m.jobTitle.trim(), m.organization.trim(), m.nationality.trim()]);

/** added = rows with a fresh key; removed = original keys now gone; modified = original key, changed value. */
function diffMembers(current: EditableMember[], originalByKey: Map<string, string>) {
  let added = 0;
  let modified = 0;
  for (const m of current) {
    const base = originalByKey.get(m.key);
    if (base === undefined) added += 1;
    else if (base !== normalize(m)) modified += 1;
  }
  const currentKeys = new Set(current.map(m => m.key));
  let removed = 0;
  for (const k of originalByKey.keys()) if (!currentKeys.has(k)) removed += 1;
  return { added, removed, modified };
}

/**
 * Amendment proposal for ONE campus (plan §9.5 / §16.6). Approval-sensitive fields — including the
 * guest/support member lists — the current content stays active until the campus's current HOST
 * approves. Reason is required. Member edits are scoped to THIS instance (deep-cloned, stable keys).
 * Stable backend codes map to steady messages.
 */
export default function VisitAmendmentSubmitModal({ visitRequestId, campus, onClose, onSubmitted }: Props) {
  const { t } = useTranslation(['visitRequestV2', 'visitRequest']);
  /**
   * Whether this submission is decided in the same call, straight from the backend's per-campus
   * verdict: the viewer is the requester side AND this campus's current Host, so there is nobody to
   * wait for. It was `user.roleCode === 'STAFF'` — a role, which is not the question. A staff account
   * that merely registered the request hosts nothing, saw "Cập nhật", and got a proposal that sat
   * waiting for somebody else.
   */
  const selfApproves = campus.amendmentSelfApproves === true;

  const [delegationName, setDelegationName] = useState(campus.delegationName);
  const [visitType, setVisitType] = useState(campus.visitType);
  const [visitTypeOther, setVisitTypeOther] = useState(campus.visitTypeOther ?? '');
  const [purpose, setPurpose] = useState(campus.purpose);
  const [workingContent, setWorkingContent] = useState(campus.workingContent ?? '');
  const [workingLanguage, setWorkingLanguage] = useState(campus.workingLanguage);
  const [opContact, setOpContact] = useState({ ...campus.operationalContact });
  const [start, setStart] = useState(toLocalInput(campus.plannedStartAt));
  const [end, setEnd] = useState(toLocalInput(campus.plannedEndAt));
  const [visitors, setVisitors] = useState<EditableMember[]>(() => cloneMembers(campus.visitors, 'v-orig'));
  const [support, setSupport] = useState<EditableMember[]>(() => cloneMembers(campus.supportMembers, 's-orig'));
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  // Stable keys for freshly-added rows; the original snapshots anchor the add/remove/modify diff.
  const keySeq = useRef(0);
  const nextKey = (p: string) => `${p}-new-${keySeq.current++}`;
  const visitorOriginals = useMemo(
    () => new Map(cloneMembers(campus.visitors, 'v-orig').map(m => [m.key, normalize(m)])),
    [campus.visitors],
  );
  const supportOriginals = useMemo(
    () => new Map(cloneMembers(campus.supportMembers, 's-orig').map(m => [m.key, normalize(m)])),
    [campus.supportMembers],
  );
  const visitorDiff = diffMembers(visitors, visitorOriginals);
  const supportDiff = diffMembers(support, supportOriginals);
  const memberChangeCount =
    visitorDiff.added + visitorDiff.removed + visitorDiff.modified +
    supportDiff.added + supportDiff.removed + supportDiff.modified;

  const reasonValid = reason.trim().length > 0;
  const hasVisitor = visitors.length > 0;
  const visitTypes = useMemo(
    () => ['CAMPUS_TOUR', 'MEETING', 'WORKSHOP', 'SIGNING_CEREMONY', 'EXCHANGE', 'OTHER'],
    [],
  );

  const patchMember = (
    setList: React.Dispatch<React.SetStateAction<EditableMember[]>>,
    key: string, field: keyof Omit<EditableMember, 'key'>, value: string,
  ) => setList(prev => prev.map(m => (m.key === key ? { ...m, [field]: value } : m)));

  const mapError = (err: unknown): string => {
    switch (errorCodeOf(err)) {
      case AmendmentErrorCode.AlreadyPending:
        return t('visitRequestV2:amend.errAlreadyPending');
      case AmendmentErrorCode.WindowExpired:
        return t('visitRequestV2:amend.errWindowExpired');
      case AmendmentErrorCode.NotEditable:
        return t('visitRequestV2:amend.errNotEditable');
      case AmendmentErrorCode.BaseRevisionConflict:
      case AmendmentErrorCode.ConcurrencyConflict:
      case AmendmentErrorCode.FormConcurrencyConflict:
        return t('visitRequestV2:amend.errConflict');
      case AmendmentErrorCode.ApproverScopeForbidden:
        return t('visitRequestV2:amend.errApproverScopeForbidden');
      case AmendmentErrorCode.NoChanges:
        return t('visitRequestV2:amend.errNoChanges');
      case AmendmentErrorCode.ContactEmailNotAmendable:
        return t('visitRequestV2:amend.errContactEmailNotAmendable');
      case AmendmentErrorCode.InvalidVisitTime:
        return t('visitRequestV2:amend.errInvalidVisitTime');
      case AmendmentErrorCode.ValidationError:
        return t('visitRequestV2:amend.errValidation');
      default:
        return t('visitRequestV2:amend.errGeneric');
    }
  };

  /**
   * Everything that can be judged wrong from here, judged here.
   *
   * The backend re-validates all of it — this is not a substitute for that. It exists because the
   * round trip came back with one sentence ("Không thể gửi đề xuất. Vui lòng thử lại.") for a phone
   * number with letters in it, an end time before its start, and a visitor row with no name, leaving
   * the user to guess which of a dozen inputs the server disliked.
   */
  const validate = (): FieldErrors => {
    const errors: FieldErrors = {};
    if (!delegationName.trim()) errors.delegationName = t('visitRequestV2:amend.errRequired');
    if (!purpose.trim()) errors.purpose = t('visitRequestV2:amend.errRequired');
    if (visitType === 'OTHER' && !visitTypeOther.trim()) errors.visitTypeOther = t('visitRequestV2:amend.errRequired');
    if (!reason.trim()) errors.reason = t('visitRequestV2:amend.errRequired');

    // Optional, but if given it must be a number somebody can actually ring.
    if (opContact.phone?.trim() && !isValidPhone(opContact.phone)) {
      errors.contactPhone = t('visitRequestV2:amend.errPhoneFormat');
    }

    if (!start) errors.start = t('visitRequestV2:amend.errRequired');
    if (!end) errors.end = t('visitRequestV2:amend.errRequired');
    if (start && end) {
      // Wall-clock strings, compared as wall clock: PEMS stores Vietnam local time, and putting these
      // through the browser's timezone is how a valid slot becomes an invalid one abroad.
      const startMs = new Date(start).getTime();
      const endMs = new Date(end).getTime();
      if (!Number.isNaN(startMs) && !Number.isNaN(endMs)) {
        if (endMs <= startMs) errors.end = t('visitRequestV2:amend.errEndBeforeStart');
        else if (endMs - startMs < MIN_DURATION_MINUTES * 60_000)
          errors.end = t('visitRequestV2:amend.errTooShort', { minutes: MIN_DURATION_MINUTES });
      }
    }

    if (visitors.length === 0) errors.visitors = t('visitRequestV2:amend.members.needOne');
    else if (visitors.some(v => !v.fullName.trim())) errors.visitors = t('visitRequestV2:amend.errVisitorName');
    else if (support.some(s => !s.fullName.trim())) errors.visitors = t('visitRequestV2:amend.errSupportName');

    return errors;
  };

  const submit = async () => {
    const errors = validate();
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      setError(t('visitRequestV2:amend.errFixFields'));
      return;
    }
    setBusy(true);
    setError(null);
    const payload: AmendmentProposalPayload = {
      expectedInstanceRowVersion: campus.rowVersion,
      baseFormRevision: campus.formRevision,
      baseApprovalRevision: campus.approvalRevision,
      reason: reason.trim(),
      delegationName: delegationName.trim(),
      visitType,
      visitTypeOther: visitType === 'OTHER' ? visitTypeOther.trim() : null,
      purpose: purpose.trim(),
      workingContent: workingContent.trim() || null,
      workingLanguage,
      operationalContact: opContact,
      // Member lists are part of the per-campus proposal (approval-sensitive) — the backend diffs and,
      // on approve, replaces this instance's members copy-on-write (siblings untouched).
      visitors: visitors.map(v => ({
        fullName: v.fullName.trim(), nationality: v.nationality.trim(),
        jobTitle: v.jobTitle.trim(), organization: v.organization.trim(),
      })),
      externalSupportMembers: support.map(v => ({
        fullName: v.fullName.trim(), jobTitle: v.jobTitle.trim(),
        organization: v.organization.trim(), nationality: v.nationality.trim(),
      })),
      plannedStartAt: start,
      plannedEndAt: end,
    };
    try {
      await submitAmendment(visitRequestId, campus.visitInstanceId, payload);
      // The modal closes on this callback, so the confirmation has to outlive it.
      showSuccessToast(t('visitRequestV2:amend.submitted', { campus: campus.campusName }));
      onSubmitted();
    } catch (err) {
      // mapError turns the stable error codes into wording the user can act on (already pending,
      // window expired, stale version) — those belong next to the form, not in a toast that vanishes.
      setError(mapError(err));
    } finally {
      setBusy(false);
    }
  };

  const field = 'w-full rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-800 p-2 text-sm';
  const cell = 'rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-800 p-2 text-sm';

  /** The message for one field, rendered directly under the input it belongs to. */
  const fieldError = (key: keyof FieldErrors) =>
    fieldErrors[key] ? (
      <p role="alert" data-testid={`amendment-error-${key}`} className="mt-1 text-xs font-semibold text-red-600">
        {fieldErrors[key]}
      </p>
    ) : null;

  const memberEditor = (
    kind: 'visitors' | 'support',
    list: EditableMember[],
    setList: React.Dispatch<React.SetStateAction<EditableMember[]>>,
    canEmpty: boolean,
  ) => (
    <fieldset className="sm:col-span-2">
      <legend className="mb-1 block text-sm font-semibold text-slate-700">
        {t(`visitRequestV2:amend.members.${kind === 'visitors' ? 'visitors' : 'support'}`)}
      </legend>
      <div className="space-y-2">
        {list.map(m => (
          <div key={m.key} className="grid grid-cols-1 gap-2 sm:grid-cols-[1fr_1fr_1fr_1fr_auto]">
            {(['fullName', 'jobTitle', 'organization', 'nationality'] as const).map(f => (
              <input
                key={f}
                data-testid={`amendment-${kind}-${f.toLowerCase()}`}
                className={cell}
                value={m[f]}
                placeholder={t(`visitRequestV2:person.${f}`)}
                aria-label={`${t(`visitRequestV2:amend.members.${kind === 'visitors' ? 'visitors' : 'support'}`)} — ${t(`visitRequestV2:person.${f}`)}`}
                onChange={e => patchMember(setList, m.key, f, e.target.value)}
              />
            ))}
            <button
              type="button"
              aria-label={t('visitRequestV2:amend.members.remove')}
              disabled={!canEmpty && list.length <= 1}
              className="rounded-lg p-2 text-slate-400 hover:bg-red-50 hover:text-red-600 disabled:opacity-30"
              onClick={() => setList(prev => prev.filter(x => x.key !== m.key))}
            >
              <Trash2 className="h-4 w-4" />
            </button>
          </div>
        ))}
      </div>
      <button
        type="button"
        data-testid={kind === 'visitors' ? 'amendment-add-visitor' : 'amendment-add-support'}
        className="mt-2 inline-flex items-center gap-1 rounded-lg border border-dashed border-slate-300 px-3 py-1.5 text-sm font-semibold text-[#004c91] hover:bg-slate-50"
        onClick={() => setList(prev => [
          ...prev,
          { key: nextKey(kind === 'visitors' ? 'v' : 's'), fullName: '', jobTitle: '', organization: '', nationality: '' },
        ])}
      >
        <Plus className="h-4 w-4" />
        {t(`visitRequestV2:amend.members.${kind === 'visitors' ? 'addVisitor' : 'addSupport'}`)}
      </button>
    </fieldset>
  );

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" role="dialog" aria-modal="true"
      aria-label={t('visitRequestV2:amend.title', { campus: campus.campusName })}>
      <div className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white dark:bg-slate-900 p-5 shadow-xl">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="flex items-center text-base font-extrabold text-[#004c91]">
            {selfApproves ? t('visitRequestV2:amend.titleUpdate', { campus: campus.campusName, defaultValue: `Cập nhật thông tin — ${campus.campusName}` }) : t('visitRequestV2:amend.title', { campus: campus.campusName })}
            <span title={t('visitRequestV2:amend.activeStaysNote')} className="ml-2 flex items-center">
              <HelpCircle className="h-4 w-4 text-slate-400" />
            </span>
          </h2>
          <button type="button" onClick={onClose} className="rounded p-1 text-slate-500 hover:bg-slate-100" aria-label={t('visitRequestV2:common.cancel')}>
            <X className="h-5 w-5" />
          </button>
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <label className="text-sm sm:col-span-2">
            <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:summary.delegationName')}</span>
            <input data-testid="amendment-delegation-input" className={field} value={delegationName} onChange={e => setDelegationName(e.target.value)} aria-invalid={fieldErrors.delegationName ? true : undefined} />
            {fieldError('delegationName')}
          </label>
          <label className="text-sm">
            <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:summary.visitType')}</span>
            <select className={field} value={visitType} onChange={e => setVisitType(e.target.value)}>
              {visitTypes.map(vt => <option key={vt} value={vt}>{t(`visitRequest:step2Info.visitTypes.${vt}`, vt)}</option>)}
            </select>
          </label>
          {visitType === 'OTHER' && (
            <label className="text-sm">
              <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:card.visitTypeOther')}</span>
              <input className={field} value={visitTypeOther} onChange={e => setVisitTypeOther(e.target.value)} aria-invalid={fieldErrors.visitTypeOther ? true : undefined} />
              {fieldError('visitTypeOther')}
            </label>
          )}
          <label className="text-sm">
            <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:summary.schedule')} ({t('visitRequestV2:card.startAt')})</span>
            <input type="datetime-local" className={field} value={start} onChange={e => setStart(e.target.value)} aria-invalid={fieldErrors.start ? true : undefined} />
            {fieldError('start')}
          </label>
          <label className="text-sm">
            <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:card.endAt')}</span>
            <input type="datetime-local" className={field} value={end} onChange={e => setEnd(e.target.value)} aria-invalid={fieldErrors.end ? true : undefined} />
            {fieldError('end')}
          </label>
          <label className="text-sm">
            <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:summary.workingLanguage')}</span>
            <select className={field} value={workingLanguage} onChange={e => setWorkingLanguage(e.target.value)}>
              <option value="EN">{t('visitRequestV2:summary.languageEN')}</option>
              <option value="VI">{t('visitRequestV2:summary.languageVI')}</option>
            </select>
          </label>
          <label className="text-sm sm:col-span-2">
            <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:summary.purpose')}</span>
            <textarea className={field} rows={2} value={purpose} onChange={e => setPurpose(e.target.value)} aria-invalid={fieldErrors.purpose ? true : undefined} />
            {fieldError('purpose')}
          </label>
          <label className="text-sm sm:col-span-2">
            <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:summary.workingContent')}</span>
            <textarea className={field} rows={2} value={workingContent} onChange={e => setWorkingContent(e.target.value)} />
          </label>

          {memberEditor('visitors', visitors, setVisitors, false)}
          {memberEditor('support', support, setSupport, true)}

          {/* Additions / removals / edits vs the active content — a proposal is never active content. */}
          <p className="text-xs font-semibold text-slate-500 sm:col-span-2" role="status">
            {memberChangeCount === 0
              ? t('visitRequestV2:amend.members.noChange')
              : t('visitRequestV2:amend.members.changeSummary', {
                  added: visitorDiff.added + supportDiff.added,
                  removed: visitorDiff.removed + supportDiff.removed,
                  modified: visitorDiff.modified + supportDiff.modified,
                })}
          </p>
          {!hasVisitor && (
            <p className="text-xs font-semibold text-red-600 sm:col-span-2">{t('visitRequestV2:amend.members.needOne')}</p>
          )}

          <label className="text-sm sm:col-span-2">
            <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:summary.operationalContact')}</span>
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              <input className={field} placeholder={t('visitRequestV2:person.fullName', 'Họ tên')} value={opContact.fullName} onChange={e => setOpContact({ ...opContact, fullName: e.target.value })} />
              <input className={field} placeholder={t('visitRequestV2:card.phone')} value={opContact.phone} onChange={e => setOpContact({ ...opContact, phone: e.target.value })} aria-invalid={fieldErrors.contactPhone ? true : undefined} />
            </div>
          </label>
          <label className="text-sm sm:col-span-2">
            <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:amend.reason')} <span className="text-red-500">*</span></span>
            <textarea data-testid="amendment-reason" className={field} rows={2} value={reason} onChange={e => setReason(e.target.value)} required aria-invalid={fieldErrors.reason ? true : undefined} />
            {fieldError('reason')}
          </label>
        </div>

        {error && <p className="mt-3 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700" role="alert">{error}</p>}

        <div className="mt-4 flex justify-end gap-2">
          <button type="button" onClick={onClose} className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700">
            {t('visitRequestV2:common.cancel')}
          </button>
          <button type="button" data-testid="amendment-submit" disabled={busy || !reasonValid || !hasVisitor} onClick={() => void submit()}
            className="rounded-lg bg-[#f37021] px-4 py-2 text-sm font-bold text-white hover:bg-orange-600 disabled:opacity-50">
            {selfApproves ? t('visitRequestV2:amend.submitUpdate', { defaultValue: 'Cập nhật' }) : t('visitRequestV2:amend.submit')}
          </button>
        </div>
      </div>
    </div>
  );
}
