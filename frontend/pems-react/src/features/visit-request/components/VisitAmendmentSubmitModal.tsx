import { useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Plus, Trash2, X } from 'lucide-react';
import {
  submitAmendment,
  type AmendmentProposalPayload,
  type ResolvedCampusVisit,
  type ResolvedMember,
} from '../api/visitRequestV2Api';
import { AmendmentErrorCode, errorCodeOf } from '../utils/visitV2Actions';

interface Props {
  visitRequestId: number;
  campus: ResolvedCampusVisit;
  onClose: () => void;
  onSubmitted: () => void;
}

/** ISO (+07:00) → "YYYY-MM-DDTHH:mm" wall-clock for a datetime-local input (no timezone shift). */
const toLocalInput = (iso: string): string => (iso ? iso.slice(0, 16) : '');

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
 * guest/support member lists — the current content stays active until a Staff Leader approves. Reason is
 * required. Member edits are scoped to THIS instance (deep-cloned, stable keys). Stable backend codes map
 * to steady messages.
 */
export default function VisitAmendmentSubmitModal({ visitRequestId, campus, onClose, onSubmitted }: Props) {
  const { t } = useTranslation(['visitRequestV2', 'visitRequest']);

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
        return t('visitRequestV2:amend.errConflict');
      default:
        return t('visitRequestV2:amend.errGeneric');
    }
  };

  const submit = async () => {
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
      onSubmitted();
    } catch (err) {
      setError(mapError(err));
    } finally {
      setBusy(false);
    }
  };

  const field = 'w-full rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-800 p-2 text-sm';
  const cell = 'rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-800 p-2 text-sm';

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
          <h2 className="text-base font-extrabold text-[#004c91]">
            {t('visitRequestV2:amend.title', { campus: campus.campusName })}
          </h2>
          <button type="button" onClick={onClose} className="rounded p-1 text-slate-500 hover:bg-slate-100" aria-label={t('visitRequestV2:common.cancel')}>
            <X className="h-5 w-5" />
          </button>
        </div>
        <p className="mb-4 rounded-lg bg-amber-50 px-3 py-2 text-xs text-amber-800">{t('visitRequestV2:amend.activeStaysNote')}</p>

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <label className="text-sm sm:col-span-2">
            <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:summary.delegationName')}</span>
            <input className={field} value={delegationName} onChange={e => setDelegationName(e.target.value)} />
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
              <input className={field} value={visitTypeOther} onChange={e => setVisitTypeOther(e.target.value)} />
            </label>
          )}
          <label className="text-sm">
            <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:summary.schedule')} ({t('visitRequestV2:card.startAt')})</span>
            <input type="datetime-local" className={field} value={start} onChange={e => setStart(e.target.value)} />
          </label>
          <label className="text-sm">
            <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:card.endAt')}</span>
            <input type="datetime-local" className={field} value={end} onChange={e => setEnd(e.target.value)} />
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
            <textarea className={field} rows={2} value={purpose} onChange={e => setPurpose(e.target.value)} />
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
              <input className={field} placeholder={t('visitRequestV2:card.phone')} value={opContact.phone} onChange={e => setOpContact({ ...opContact, phone: e.target.value })} />
            </div>
          </label>
          <label className="text-sm sm:col-span-2">
            <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:amend.reason')} <span className="text-red-500">*</span></span>
            <textarea className={field} rows={2} value={reason} onChange={e => setReason(e.target.value)} required />
          </label>
        </div>

        {error && <p className="mt-3 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700" role="alert">{error}</p>}

        <div className="mt-4 flex justify-end gap-2">
          <button type="button" onClick={onClose} className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700">
            {t('visitRequestV2:common.cancel')}
          </button>
          <button type="button" disabled={busy || !reasonValid || !hasVisitor} onClick={() => void submit()}
            className="rounded-lg bg-[#f37021] px-4 py-2 text-sm font-bold text-white hover:bg-orange-600 disabled:opacity-50">
            {t('visitRequestV2:amend.submit')}
          </button>
        </div>
      </div>
    </div>
  );
}
