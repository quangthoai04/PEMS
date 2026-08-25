import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { HelpCircle, Plus, Trash2, X } from 'lucide-react';
import {
  submitAmendment,
  type AmendmentProposalPayload,
  type ResolvedCampusVisit,
  type ResolvedMember,
} from '../api/visitRequestV2Api';
import { AmendmentErrorCode, errorCodeOf } from '../utils/visitV2Actions';
import { showSuccessToast, showMessageErrorToast } from '../../../shared/utils/toast';
import { OrganizationCombobox } from './shared/OrganizationCombobox';
import { CountrySelect } from './shared/CountrySelect';
import { AutoGrowTextarea } from './shared/AutoGrowTextarea';
import { newClientKey } from '../utils/visitRequestV2Form';
import { focusFirstInvalidField } from '../utils/formErrorNavigation';
import { V2_MIN_DURATION_MINUTES as MIN_DURATION_MINUTES } from '../schema/visitRequestV2.schema';
import { wallClockToMinutes } from './shared/visitDateTime';

interface Props {
  visitRequestId: number;
  campus: ResolvedCampusVisit;
  onClose: () => void;
  onSubmitted: () => void;
}

/** ISO (+07:00) → "YYYY-MM-DDTHH:mm" wall-clock for a datetime-local input (no timezone shift). */
const toLocalInput = (iso: string): string => (iso ? iso.slice(0, 16) : '');

/** Which fields a proposal can be wrong in. Keyed so each message renders under its own input. */
type FieldErrors = Partial<Record<
  'delegationName' | 'visitTypeOther' | 'purpose' | 'workingContent' | 'start' | 'end' | 'visitors' | 'reason',
  string
>>;

/**
 * Per-MEMBER, per-FIELD errors (plan PEMS_AMENDMENT_VALIDATION_HIGHLIGHT) — mirrors the required set
 * Create/Edit already enforce (`buildPersonSchema` in the v2 schema): a guest/support row is not valid
 * on a name alone.
 *
 * A single `errors.visitors` string used to cover the WHOLE list ("some guest has no name"), which
 * blocked Submit and printed a banner but highlighted nothing — the user had no way to tell which of
 * possibly several rows, or which of four fields on that row, was the problem. Keyed by the row's
 * STABLE `key` (never the array index), so add/remove/reorder cannot move an error onto the wrong
 * person.
 */
type MemberFieldKey = 'fullName' | 'jobTitle' | 'organization' | 'nationality';
type MemberRowErrors = Partial<Record<MemberFieldKey, string>>;
type MemberErrorsMap = Record<string, MemberRowErrors>;

/**
 * A member row with a STABLE client key (never the array index) so add/remove keeps React identity.
 *
 * `clientMemberKey` is EPHEMERAL — minted fresh every time this modal opens, the same convention
 * Create/Edit use (NP-03): a saved member row carries no client key of its own, so one is minted for
 * this editing session and sent to the backend, which uses it to re-point the operational-contact
 * link at the right row after a member replace instead of guessing by name/org/title.
 */
interface EditableMember {
  key: string;
  clientMemberKey: string;
  /**
   * The row's STABLE database identity, or null for a row that does not exist yet (freshly added in
   * this session). Existing rows keep the id the read model resolved them with regardless of what the
   * user edits on them (per NP-03, editing a member never changes WHICH person they are) — this is
   * what lets the operational-contact picker send a PERSISTENT target
   * (`operationalContactGuestMemberId`) instead of only the ephemeral `clientMemberKey` when the
   * member list itself is unchanged (plan CanhIter3FixBug "Đầu mối hiện tại có nằm trong danh sách
   * đoàn không?").
   */
  guestMemberId: number | null;
  fullName: string;
  jobTitle: string;
  organization: string;
  organizationPartnerId: number | null;
  nationality: string;
}

/** Deep-clone the read-model members into editable rows — no reference is shared with `campus`. */
const cloneMembers = (members: ResolvedMember[], prefix: string): EditableMember[] =>
  members.map((m, i) => ({
    key: `${prefix}-${i}`,
    clientMemberKey: newClientKey(),
    guestMemberId: m.guestMemberId,
    fullName: m.fullName ?? '',
    jobTitle: m.jobTitle ?? '',
    organization: m.organization ?? '',
    organizationPartnerId: m.organizationPartnerId ?? null,
    nationality: m.nationality ?? '',
  }));

const normalize = (m: EditableMember): string =>
  JSON.stringify([
    m.fullName.trim(), m.jobTitle.trim(), m.organization.trim(), m.organizationPartnerId, m.nationality.trim(),
  ]);

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
 * Clears ONE field's error the moment it becomes valid again — never waits for the next Submit, and
 * never touches a sibling field's error in the same row even when it clears the row down to empty.
 */
const clearMemberFieldError = (
  setErrors: React.Dispatch<React.SetStateAction<MemberErrorsMap>>,
  rowKey: string,
  field: MemberFieldKey,
  value: string,
) => {
  if (!value.trim()) return; // still invalid — leave the red state in place
  setErrors(prev => {
    const row = prev[rowKey];
    if (!row?.[field]) return prev;
    const nextRow = { ...row, [field]: undefined };
    const rowStillHasErrors = Object.values(nextRow).some(Boolean);
    const next = { ...prev };
    if (rowStillHasErrors) next[rowKey] = nextRow;
    else delete next[rowKey];
    return next;
  });
};

/** Drops one row's errors entirely — called on remove, so a deleted row's problem cannot linger. */
const dropMemberErrors = (setErrors: React.Dispatch<React.SetStateAction<MemberErrorsMap>>, rowKey: string) =>
  setErrors(prev => {
    if (!prev[rowKey]) return prev;
    const next = { ...prev };
    delete next[rowKey];
    return next;
  });

/** Desktop: index + 4 fields + delete, one row. Tablet: 2-up pairs. Mobile: stacked. */
const MEMBER_GRID =
  'grid-cols-1 gap-2 sm:grid-cols-2 ' +
  'xl:grid-cols-[28px_minmax(150px,1fr)_minmax(150px,1fr)_minmax(260px,1.6fr)_minmax(140px,.9fr)_40px] ' +
  'xl:items-center xl:gap-3';

/**
 * Amendment proposal for ONE campus (plan §9.5 / §16.6). Approval-sensitive fields — including the
 * guest/support member lists — the current content stays active until the campus's current HOST
 * approves. Reason is required. Member edits are scoped to THIS instance (deep-cloned, stable keys).
 * Stable backend codes map to steady messages.
 *
 * The Operational Contact — its profile AND which delegation member it corresponds to — has NO surface
 * on this modal at all any more (plan CanhIter3FixBug §3/§27): a general "Đề xuất thay đổi" is for
 * visit content (delegation info, schedule, purpose, members, ...), and showing the contact here —
 * even read-only, even just the relation picker — read as "this is where you change the contact",
 * which is a different, actual-holder-changing workflow that lives on the Operational Contact
 * Management surface instead. The relation is still tracked and preserved INTERNALLY, silently: the
 * `contactMemberKey` this modal opened on (read straight off `campus.operationalContact.guestMemberId`,
 * never guessed) travels through every member edit exactly as NP-03 always required, and the delete
 * guard below still refuses to remove whoever currently holds the role — none of that logic changed,
 * only whether the user can see or touch it from here.
 */
export default function VisitAmendmentSubmitModal({ visitRequestId, campus, onClose, onSubmitted }: Props) {
  const { t } = useTranslation(['visitRequestV2', 'visitRequest', 'validation']);
  const dialogRef = useRef<HTMLDivElement>(null);
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
  const [start, setStart] = useState(toLocalInput(campus.plannedStartAt));
  const [end, setEnd] = useState(toLocalInput(campus.plannedEndAt));
  const [visitors, setVisitors] = useState<EditableMember[]>(() => cloneMembers(campus.visitors, 'v-orig'));
  const [support, setSupport] = useState<EditableMember[]>(() => cloneMembers(campus.supportMembers, 's-orig'));
  // Durable contact-member reference (NP-03), read straight off the resolved snapshot rather than
  // guessed by name/org/title: `operationalContact.guestMemberId` already names which delegation row
  // (if any) IS the contact, so the freshly-minted key of that same row is the correct starting pick.
  const [contactMemberKey, setContactMemberKey] = useState<string | null>(() => {
    const contactId = campus.operationalContact.guestMemberId;
    if (contactId == null) return null;
    const vIdx = campus.visitors.findIndex(m => m.guestMemberId === contactId);
    if (vIdx >= 0) return visitors[vIdx]?.clientMemberKey ?? null;
    const sIdx = campus.supportMembers.findIndex(m => m.guestMemberId === contactId);
    if (sIdx >= 0) return support[sIdx]?.clientMemberKey ?? null;
    return null;
  });
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [visitorErrors, setVisitorErrors] = useState<MemberErrorsMap>({});
  const [supportErrors, setSupportErrors] = useState<MemberErrorsMap>({});
  // Bumped on every failed Submit; the effect below reacts to the CHANGE, not the value, so a second
  // failed attempt in a row (same field still invalid) still re-focuses instead of doing nothing.
  const [focusToken, setFocusToken] = useState(0);

  // …then the caret lands ON the first invalid field, in the order it appears on screen (plan §19 /
  // PEMS_AMENDMENT_VALIDATION_HIGHLIGHT §7). Deferred a tick: the error state above has to reach the
  // DOM (`data-field-error="true"`) before there is anything to find.
  useEffect(() => {
    if (focusToken === 0) return;
    const timer = window.setTimeout(() => {
      focusFirstInvalidField(dialogRef.current ?? document);
    }, 60);
    return () => window.clearTimeout(timer);
  }, [focusToken]);

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

  const patchMember = <K extends keyof Omit<EditableMember, 'key' | 'clientMemberKey'>>(
    setList: React.Dispatch<React.SetStateAction<EditableMember[]>>,
    key: string, field: K, value: EditableMember[K],
  ) => setList(prev => prev.map(m => (m.key === key ? { ...m, [field]: value } : m)));

  // ── "Đầu mối là ai trong đoàn?" (NP-03), same durable-key picker as create/edit ──
  type MemberKind = 'visitors' | 'support';
  interface EligibleMember {
    kind: MemberKind;
    index: number;
    clientMemberKey: string;
    fullName: string;
    jobTitle: string;
    organization: string;
    complete: boolean;
  }
  const buildEligible = (rows: EditableMember[], kind: MemberKind): EligibleMember[] =>
    rows.map((m, index) => {
      const fullName = m.fullName.trim();
      const jobTitle = m.jobTitle.trim();
      const organization = m.organization.trim();
      return {
        kind, index, clientMemberKey: m.clientMemberKey, fullName, jobTitle, organization,
        complete: !!fullName && !!jobTitle && !!organization,
      };
    });
  const eligibleMembers: EligibleMember[] = [...buildEligible(visitors, 'visitors'), ...buildEligible(support, 'support')];
  const eligibleKeys = eligibleMembers.map(m => m.clientMemberKey).join('|');

  // The pick must not silently survive removing the row it names — clearing it here (rather than
  // falling back to a fingerprint match) is what keeps this mechanism honest per plan §16.
  //
  // DEFENSIVE FALLBACK ONLY (plan CanhIter3FixBug FIX-C): the normal delete flow can no longer reach
  // this branch at all — the Trash2 button above refuses to remove the contact's own row in the first
  // place. This still runs for whatever this invariant did not anticipate (e.g. a future code path
  // that mutates `visitors`/`support` some other way), so it stays as the last line of defence rather
  // than being deleted.
  useEffect(() => {
    if (!contactMemberKey) return;
    if (eligibleKeys.split('|').includes(contactMemberKey)) return;
    setContactMemberKey(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [contactMemberKey, eligibleKeys]);

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
      case AmendmentErrorCode.ContactProfileNotAmendable:
        return t('visitRequestV2:amend.errContactProfileNotAmendable');
      case AmendmentErrorCode.InvalidVisitTime:
        return t('visitRequestV2:amend.errInvalidVisitTime');
      case AmendmentErrorCode.ValidationError:
        return t('visitRequestV2:amend.errValidation');
      case AmendmentErrorCode.PartnerNotSelectable:
        return t('visitRequestV2:amend.errPartnerNotSelectable');
      default:
        return t('visitRequestV2:amend.errGeneric');
    }
  };

  /**
   * Same required set Create/Edit already enforce per row (`buildPersonSchema` in the v2 zod schema):
   * fullName, jobTitle, organization, nationality — never just the name. Anything the backend leaves
   * optional (there is nothing optional on a member row) stays optional here too.
   */
  const validateMembers = (list: EditableMember[]): MemberErrorsMap => {
    const map: MemberErrorsMap = {};
    for (const m of list) {
      const row: MemberRowErrors = {};
      if (!m.fullName.trim()) row.fullName = t('validation:requiredField', { field: t('visitRequestV2:person.fullName') });
      if (!m.jobTitle.trim()) row.jobTitle = t('validation:requiredField', { field: t('visitRequestV2:person.jobTitle') });
      if (!m.organization.trim()) row.organization = t('validation:requiredField', { field: t('visitRequestV2:person.organization') });
      if (!m.nationality.trim()) row.nationality = t('validation:requiredField', { field: t('visitRequestV2:person.nationality') });
      if (Object.keys(row).length > 0) map[m.key] = row;
    }
    return map;
  };

  /**
   * Everything that can be judged wrong from here, judged here.
   *
   * The backend re-validates all of it — this is not a substitute for that. It exists because the
   * round trip came back with one sentence ("Không thể gửi đề xuất. Vui lòng thử lại.") for a phone
   * number with letters in it, an end time before its start, and a visitor row with no name, leaving
   * the user to guess which of a dozen inputs the server disliked.
   */
  const validate = (): { errors: FieldErrors; vErrors: MemberErrorsMap; sErrors: MemberErrorsMap } => {
    const errors: FieldErrors = {};
    if (!delegationName.trim()) errors.delegationName = t('visitRequestV2:amend.errRequired');
    if (!purpose.trim()) errors.purpose = t('visitRequestV2:amend.errRequired');
    if (visitType === 'OTHER' && !visitTypeOther.trim()) errors.visitTypeOther = t('visitRequestV2:amend.errRequired');
    // WorkingContent parity with the create/edit schema (visitRequestV2.schema.ts: required, ≤4000
    // chars) — the amendment modal previously enforced nothing here at all (plan FIX-D).
    if (!workingContent.trim()) errors.workingContent = t('validation:workingContentRequired');
    if (!reason.trim()) errors.reason = t('visitRequestV2:amend.errRequired');

    if (!start) errors.start = t('visitRequestV2:amend.errRequired');
    if (!end) errors.end = t('visitRequestV2:amend.errRequired');
    if (start && end) {
      // Pure wall-clock minute arithmetic (plan FIX-I) — never new Date(start).getTime(), which parses
      // a bare "YYYY-MM-DDTHH:mm" string as LOCAL time in the BROWSER's own timezone. The subtraction
      // below happens to cancel a fixed offset out for duration, but not across a DST boundary, and the
      // pattern is exactly the one this fix removes everywhere else in this feature.
      const startMin = wallClockToMinutes(start);
      const endMin = wallClockToMinutes(end);
      if (startMin !== null && endMin !== null) {
        if (endMin <= startMin) errors.end = t('visitRequestV2:amend.errEndBeforeStart');
        else if (endMin - startMin < MIN_DURATION_MINUTES)
          errors.end = t('visitRequestV2:amend.errTooShort', { minutes: MIN_DURATION_MINUTES });
      }
    }

    // The "no rows at all" case has nowhere on a row to point at, so it stays a list-level message
    // (rendered unconditionally below from `hasVisitor`, not through `fieldError`). Anything ELSE
    // wrong with a row — missing name, job title, organization or nationality — is now the per-row,
    // per-field errors below, never folded back into this one string.
    if (visitors.length === 0) errors.visitors = t('visitRequestV2:amend.members.needOne');

    const vErrors = validateMembers(visitors);
    const sErrors = validateMembers(support);
    return { errors, vErrors, sErrors };
  };

  const submit = async () => {
    const { errors, vErrors, sErrors } = validate();
    setFieldErrors(errors);
    setVisitorErrors(vErrors);
    setSupportErrors(sErrors);
    const hasErrors = Object.keys(errors).length > 0
      || Object.keys(vErrors).length > 0
      || Object.keys(sErrors).length > 0;
    if (hasErrors) {
      setError(t('visitRequestV2:amend.errFixFields'));
      setFocusToken(v => v + 1);
      return;
    }
    setBusy(true);
    setError(null);
    // Both durable-reference forms travel together (plan CanhIter3FixBug "Đầu mối hiện tại có nằm
    // trong danh sách đoàn không?"): the backend picks whichever one actually applies — the EPHEMERAL
    // key when the member list changed (a replaced row has no persistent id yet), the PERSISTENT id
    // when it did not (so a relationship-only pick still registers as a real change instead of being
    // silently dropped as "no changes"). The frontend does not need to replicate that decision; it
    // just resolves the CURRENT pick's own id from whichever row it belongs to.
    const pickedMember = contactMemberKey
      ? [...visitors, ...support].find(m => m.clientMemberKey === contactMemberKey)
      : undefined;
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
      // Read-only here (plan PEMS_CONTACT_ONE_DOOR) — sent back exactly as the server resolved it, so
      // the backend's own unchanged-profile check always passes for a proposal built by this modal.
      operationalContact: {
        fullName: campus.operationalContact.fullName,
        organization: campus.operationalContact.organization,
        jobTitle: campus.operationalContact.jobTitle,
        phone: campus.operationalContact.phone,
        email: campus.operationalContact.email,
      },
      // Member lists are part of the per-campus proposal (approval-sensitive) — the backend diffs and,
      // on approve, replaces this instance's members copy-on-write (siblings untouched).
      // guestMemberId travels alongside clientMemberKey for every EXISTING row (null for a row added
      // this session) — the backend's continuity proof needs the real persisted id as evidence that a
      // proposal's member-list rewrite still contains the SAME logical person the active Operational
      // Contact is linked to; the ephemeral key alone is not sufficient evidence any more
      // (operational-contact consistency fix). Omitting this would make every member-list-changing
      // amendment from THIS modal look identical to a stale pre-upgrade client to the new backend check.
      visitors: visitors.map(v => ({
        fullName: v.fullName.trim(), nationality: v.nationality.trim(),
        jobTitle: v.jobTitle.trim(), organization: v.organization.trim(),
        organizationPartnerId: v.organizationPartnerId, clientMemberKey: v.clientMemberKey,
        guestMemberId: v.guestMemberId,
      })),
      externalSupportMembers: support.map(v => ({
        fullName: v.fullName.trim(), jobTitle: v.jobTitle.trim(),
        organization: v.organization.trim(), nationality: v.nationality.trim(),
        organizationPartnerId: v.organizationPartnerId, clientMemberKey: v.clientMemberKey,
        guestMemberId: v.guestMemberId,
      })),
      plannedStartAt: start,
      plannedEndAt: end,
      // Durable contact-member reference (NP-03) — null/absent means "outside the delegation",
      // never a fallback the backend has to guess from names.
      operationalContactClientMemberKey: contactMemberKey,
      // Same pick, named by its STABLE database id when it has one (an existing member always does).
      // Null for "not in the delegation" AND for a picked row that is itself brand new this session —
      // in the latter case the ephemeral key above is what carries the pick; a new row has no
      // persistent id to send yet.
      operationalContactGuestMemberId: pickedMember?.guestMemberId ?? null,
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

  /** Red border + ring on an invalid control — same palette FormField/CampusVisitCard already use. */
  const borderCls = (hasError?: boolean) => (hasError
    ? 'border-red-500 focus:border-red-500 focus:ring-2 focus:ring-red-200'
    : 'border-slate-300 dark:border-slate-600 focus:border-[#004c91]');
  const fieldCls = (hasError?: boolean) =>
    `w-full rounded-lg border ${borderCls(hasError)} bg-white dark:bg-slate-800 p-2 text-sm`;
  const cellCls = (hasError?: boolean) =>
    `rounded-lg border ${borderCls(hasError)} bg-white dark:bg-slate-800 p-2 text-sm min-w-0`;

  /** The message for one top-level field, rendered directly under the input it belongs to. */
  const fieldError = (key: keyof FieldErrors) =>
    fieldErrors[key] ? (
      <p role="alert" data-testid={`amendment-error-${key}`} className="mt-1 text-xs font-normal text-red-600">
        {fieldErrors[key]}
      </p>
    ) : null;

  /** Same, for one FIELD of one MEMBER ROW — `data-field-error` is what `focusFirstInvalidField` scans for. */
  const memberFieldError = (kind: 'visitors' | 'support', rowKey: string, field: MemberFieldKey, msg: string | undefined) =>
    msg ? (
      <p role="alert" data-testid={`amendment-${kind}-${field}-error-${rowKey}`} className="mt-1 text-xs font-normal text-red-600">
        {msg}
      </p>
    ) : null;

  const memberEditor = (
    kind: 'visitors' | 'support',
    list: EditableMember[],
    setList: React.Dispatch<React.SetStateAction<EditableMember[]>>,
    canEmpty: boolean,
    errors: MemberErrorsMap,
    setErrors: React.Dispatch<React.SetStateAction<MemberErrorsMap>>,
  ) => {
    const sectionLabel = t(`visitRequestV2:amend.members.${kind === 'visitors' ? 'visitors' : 'support'}`);
    return (
      <fieldset className="min-w-0 sm:col-span-2">
        <legend className="mb-1 block text-sm font-semibold text-slate-700">{sectionLabel}</legend>
        {/* Column headers — desktop only. At sm/mobile each field carries its own placeholder + aria-label. */}
        <div
          aria-hidden="true"
          className={`hidden xl:grid ${MEMBER_GRID} px-0.5 pb-1 text-xs font-semibold uppercase tracking-wide text-slate-400`}
        >
          <span />
          <span>{t('visitRequestV2:person.fullName')}</span>
          <span>{t('visitRequestV2:person.jobTitle')}</span>
          <span>{t('visitRequestV2:person.organization')}</span>
          <span>{t('visitRequestV2:person.nationality')}</span>
          <span />
        </div>
        <div className="space-y-2">
          {list.map((m, i) => {
            const rowErr = errors[m.key] ?? {};
            return (
              <div key={m.key} className={`grid ${MEMBER_GRID}`}>
                <span className="hidden items-center pt-2 text-xs font-semibold text-slate-400 xl:flex">{i + 1}</span>
                <div className="min-w-0" data-field-error={rowErr.fullName ? 'true' : undefined}>
                  <input
                    data-testid={`amendment-${kind}-fullname`}
                    className={cellCls(!!rowErr.fullName)}
                    value={m.fullName}
                    placeholder={t('visitRequestV2:person.fullName')}
                    aria-label={`${sectionLabel} — ${t('visitRequestV2:person.fullName')}`}
                    aria-invalid={rowErr.fullName ? true : undefined}
                    onChange={e => {
                      patchMember(setList, m.key, 'fullName', e.target.value);
                      clearMemberFieldError(setErrors, m.key, 'fullName', e.target.value);
                    }}
                  />
                  {memberFieldError(kind, m.key, 'fullName', rowErr.fullName)}
                </div>
                <div className="min-w-0" data-field-error={rowErr.jobTitle ? 'true' : undefined}>
                  <input
                    data-testid={`amendment-${kind}-jobtitle`}
                    className={cellCls(!!rowErr.jobTitle)}
                    value={m.jobTitle}
                    placeholder={t('visitRequestV2:person.jobTitle')}
                    aria-label={`${sectionLabel} — ${t('visitRequestV2:person.jobTitle')}`}
                    aria-invalid={rowErr.jobTitle ? true : undefined}
                    onChange={e => {
                      patchMember(setList, m.key, 'jobTitle', e.target.value);
                      clearMemberFieldError(setErrors, m.key, 'jobTitle', e.target.value);
                    }}
                  />
                  {memberFieldError(kind, m.key, 'jobTitle', rowErr.jobTitle)}
                </div>
                <div
                  className="min-w-0 sm:col-span-2 xl:col-span-1"
                  data-field-error={rowErr.organization ? 'true' : undefined}
                >
                  <OrganizationCombobox
                    testId={`amendment-${kind}-organization-${m.key}`}
                    ariaLabel={`${sectionLabel} — ${t('visitRequestV2:person.organization')}`}
                    value={m.organization}
                    partnerId={m.organizationPartnerId}
                    searchMode="REQUEST_FORM"
                    hasError={!!rowErr.organization}
                    placeholder={t('visitRequestV2:person.organization')}
                    onChange={(value, pickedPartnerId) => {
                      setList(prev => prev.map(x => (
                        x.key === m.key ? { ...x, organization: value, organizationPartnerId: pickedPartnerId } : x
                      )));
                      clearMemberFieldError(setErrors, m.key, 'organization', value);
                    }}
                  />
                  {memberFieldError(kind, m.key, 'organization', rowErr.organization)}
                </div>
                <div
                  className="min-w-0 sm:col-span-2 xl:col-span-1"
                  data-field-error={rowErr.nationality ? 'true' : undefined}
                >
                  <CountrySelect
                    strict
                    value={m.nationality}
                    hasError={!!rowErr.nationality}
                    ariaLabel={`${sectionLabel} — ${t('visitRequestV2:person.nationality')}`}
                    placeholder={t('visitRequestV2:person.nationality')}
                    onChange={value => {
                      patchMember(setList, m.key, 'nationality', value);
                      clearMemberFieldError(setErrors, m.key, 'nationality', value);
                    }}
                  />
                  {memberFieldError(kind, m.key, 'nationality', rowErr.nationality)}
                </div>
                <div className="flex items-start justify-end sm:col-span-2 xl:col-span-1 xl:justify-center xl:pt-1">
                  <button
                    type="button"
                    aria-label={t('visitRequestV2:amend.members.remove')}
                    disabled={!canEmpty && list.length <= 1}
                    className="rounded-lg p-2 text-slate-400 hover:bg-red-50 hover:text-red-600 disabled:opacity-30"
                    onClick={() => {
                      // The Operational Contact link (NP-03) must never be silently orphaned by a
                      // delete — the user has to actively re-point it (pick someone else, or "not in
                      // the delegation") before this row can go (plan CanhIter3FixBug FIX-C). The
                      // defensive useEffect above still exists as a last-resort invariant repair, but
                      // this guard means the normal delete flow never has to fall back on it.
                      if (m.clientMemberKey === contactMemberKey) {
                        showMessageErrorToast(t('visitRequestV2:amend.members.cannotRemoveContact'));
                        return;
                      }
                      setList(prev => prev.filter(x => x.key !== m.key));
                      dropMemberErrors(setErrors, m.key);
                    }}
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              </div>
            );
          })}
        </div>
        <button
          type="button"
          data-testid={kind === 'visitors' ? 'amendment-add-visitor' : 'amendment-add-support'}
          className="mt-2 inline-flex items-center gap-1 rounded-lg border border-dashed border-slate-300 px-3 py-1.5 text-sm font-semibold text-[#004c91] hover:bg-slate-50"
          onClick={() => setList(prev => [
            ...prev,
            {
              key: nextKey(kind === 'visitors' ? 'v' : 's'), clientMemberKey: newClientKey(), guestMemberId: null,
              fullName: '', jobTitle: '', organization: '', organizationPartnerId: null, nationality: '',
            },
          ])}
        >
          <Plus className="h-4 w-4" />
          {t(`visitRequestV2:amend.members.${kind === 'visitors' ? 'addVisitor' : 'addSupport'}`)}
        </button>
      </fieldset>
    );
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" role="dialog" aria-modal="true"
      aria-label={t('visitRequestV2:amend.title', { campus: campus.campusName })}>
      {/* Overlay is a flat p-4 (2rem vertical gutter). */}
      <div ref={dialogRef} className="flex max-h-[calc(100dvh-2rem)] w-[min(1180px,96vw)] max-w-none flex-col overflow-hidden rounded-2xl bg-white dark:bg-slate-900 shadow-xl">
        <header className="flex shrink-0 items-center justify-between border-b border-slate-200 px-5 py-4 dark:border-slate-700">
          <h2 className="flex items-center text-base font-extrabold text-[#004c91]">
            {selfApproves ? t('visitRequestV2:amend.titleUpdate', { campus: campus.campusName, defaultValue: `Cập nhật thông tin — ${campus.campusName}` }) : t('visitRequestV2:amend.title', { campus: campus.campusName })}
            <span title={t('visitRequestV2:amend.activeStaysNote')} className="ml-2 flex items-center">
              <HelpCircle className="h-4 w-4 text-slate-400" />
            </span>
          </h2>
          <button type="button" onClick={onClose} className="rounded p-1 text-slate-500 hover:bg-slate-100" aria-label={t('visitRequestV2:common.cancel')}>
            <X className="h-5 w-5" />
          </button>
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto overflow-x-hidden p-5">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <label className="text-sm sm:col-span-2" data-field-error={fieldErrors.delegationName ? 'true' : undefined}>
              <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:summary.delegationName')}</span>
              <input data-testid="amendment-delegation-input" className={fieldCls(!!fieldErrors.delegationName)} value={delegationName}
                onChange={e => {
                  setDelegationName(e.target.value);
                  if (fieldErrors.delegationName && e.target.value.trim())
                    setFieldErrors(prev => ({ ...prev, delegationName: undefined }));
                }}
                aria-invalid={fieldErrors.delegationName ? true : undefined} />
              {fieldError('delegationName')}
            </label>
            <label className="text-sm">
              <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:summary.visitType')}</span>
              <select className={fieldCls()} value={visitType} onChange={e => setVisitType(e.target.value)}>
                {visitTypes.map(vt => <option key={vt} value={vt}>{t(`visitRequest:step2Info.visitTypes.${vt}`, vt)}</option>)}
              </select>
            </label>
            {visitType === 'OTHER' && (
              <label className="text-sm" data-field-error={fieldErrors.visitTypeOther ? 'true' : undefined}>
                <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:card.visitTypeOther')}</span>
                <input className={fieldCls(!!fieldErrors.visitTypeOther)} value={visitTypeOther}
                  onChange={e => {
                    setVisitTypeOther(e.target.value);
                    if (fieldErrors.visitTypeOther && e.target.value.trim())
                      setFieldErrors(prev => ({ ...prev, visitTypeOther: undefined }));
                  }}
                  aria-invalid={fieldErrors.visitTypeOther ? true : undefined} />
                {fieldError('visitTypeOther')}
              </label>
            )}
            <label className="text-sm" data-field-error={fieldErrors.start ? 'true' : undefined}>
              <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:summary.schedule')} ({t('visitRequestV2:card.startAt')})</span>
              <input type="datetime-local" className={fieldCls(!!fieldErrors.start)} value={start}
                onChange={e => {
                  const next = e.target.value;
                  setStart(next);
                  // The pair is interdependent (end must be after start by at least 30 minutes), so
                  // both sides are re-checked together rather than only ever clearing on their own input.
                  setFieldErrors(prev => {
                    if (!prev.start && !prev.end) return prev;
                    const startMin = wallClockToMinutes(next);
                    const endMin = wallClockToMinutes(end);
                    if (!next || startMin === null) return prev;
                    if (!end || endMin === null) return { ...prev, start: undefined };
                    if (endMin <= startMin || endMin - startMin < MIN_DURATION_MINUTES) return prev;
                    return { ...prev, start: undefined, end: undefined };
                  });
                }}
                aria-invalid={fieldErrors.start ? true : undefined} />
              {fieldError('start')}
            </label>
            <label className="text-sm" data-field-error={fieldErrors.end ? 'true' : undefined}>
              <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:card.endAt')}</span>
              <input type="datetime-local" className={fieldCls(!!fieldErrors.end)} value={end}
                onChange={e => {
                  const next = e.target.value;
                  setEnd(next);
                  setFieldErrors(prev => {
                    if (!prev.start && !prev.end) return prev;
                    const startMin = wallClockToMinutes(start);
                    const endMin = wallClockToMinutes(next);
                    if (!next || endMin === null) return prev;
                    if (!start || startMin === null) return { ...prev, end: undefined };
                    if (endMin <= startMin || endMin - startMin < MIN_DURATION_MINUTES) return prev;
                    return { ...prev, start: undefined, end: undefined };
                  });
                }}
                aria-invalid={fieldErrors.end ? true : undefined} />
              {fieldError('end')}
            </label>
            <label className="text-sm">
              <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:summary.workingLanguage')}</span>
              <select className={fieldCls()} value={workingLanguage} onChange={e => setWorkingLanguage(e.target.value)}>
                <option value="EN">{t('visitRequestV2:summary.languageEN')}</option>
                <option value="VI">{t('visitRequestV2:summary.languageVI')}</option>
              </select>
            </label>
            <label className="text-sm sm:col-span-2" data-field-error={fieldErrors.purpose ? 'true' : undefined}>
              <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:summary.purpose')}</span>
              <AutoGrowTextarea
                value={purpose}
                minRows={3}
                maxLength={2000}
                hasError={!!fieldErrors.purpose}
                counterTestId="amendment-purpose-counter"
                onChange={value => {
                  setPurpose(value);
                  if (fieldErrors.purpose && value.trim())
                    setFieldErrors(prev => ({ ...prev, purpose: undefined }));
                }}
                aria-invalid={fieldErrors.purpose ? true : undefined}
              />
              {fieldError('purpose')}
            </label>
            <label className="text-sm sm:col-span-2" data-field-error={fieldErrors.workingContent ? 'true' : undefined}>
              <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:summary.workingContent')}</span>
              <AutoGrowTextarea
                value={workingContent}
                minRows={3}
                maxLength={4000}
                hasError={!!fieldErrors.workingContent}
                counterTestId="amendment-workingcontent-counter"
                onChange={value => {
                  setWorkingContent(value);
                  if (fieldErrors.workingContent && value.trim())
                    setFieldErrors(prev => ({ ...prev, workingContent: undefined }));
                }}
                aria-invalid={fieldErrors.workingContent ? true : undefined}
              />
              {fieldError('workingContent')}
            </label>

            {memberEditor('visitors', visitors, setVisitors, false, visitorErrors, setVisitorErrors)}
            {memberEditor('support', support, setSupport, true, supportErrors, setSupportErrors)}

            {/* Additions / removals / edits vs the active content — a proposal is never active content. */}
            <p className="text-xs font-normal text-slate-500 sm:col-span-2" role="status">
              {memberChangeCount === 0
                ? t('visitRequestV2:amend.members.noChange')
                : t('visitRequestV2:amend.members.changeSummary', {
                    added: visitorDiff.added + supportDiff.added,
                    removed: visitorDiff.removed + supportDiff.removed,
                    modified: visitorDiff.modified + supportDiff.modified,
                  })}
            </p>
            {!hasVisitor && (
              <p className="text-xs font-normal text-red-600 sm:col-span-2">{t('visitRequestV2:amend.members.needOne')}</p>
            )}

            <label className="text-sm sm:col-span-2" data-field-error={fieldErrors.reason ? 'true' : undefined}>
              <span className="mb-1 block font-semibold text-slate-700">{t('visitRequestV2:amend.reason')} <span className="text-red-500">*</span></span>
              <textarea data-testid="amendment-reason" className={fieldCls(!!fieldErrors.reason)} rows={2} value={reason}
                onChange={e => {
                  setReason(e.target.value);
                  if (fieldErrors.reason && e.target.value.trim())
                    setFieldErrors(prev => ({ ...prev, reason: undefined }));
                }}
                required aria-invalid={fieldErrors.reason ? true : undefined} />
              {fieldError('reason')}
            </label>
          </div>
        </div>

        <footer className="shrink-0 border-t border-slate-200 px-5 py-4 dark:border-slate-700">
          {error && <p className="mb-3 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700" role="alert">{error}</p>}
          <div className="flex justify-end gap-2">
            <button type="button" onClick={onClose} className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700">
              {t('visitRequestV2:common.cancel')}
            </button>
            <button type="button" data-testid="amendment-submit" disabled={busy || !reasonValid || !hasVisitor} onClick={() => void submit()}
              className="rounded-lg bg-[#f37021] px-4 py-2 text-sm font-bold text-white hover:bg-orange-600 disabled:opacity-50">
              {selfApproves ? t('visitRequestV2:amend.submitUpdate', { defaultValue: 'Cập nhật' }) : t('visitRequestV2:amend.submit')}
            </button>
          </div>
        </footer>
      </div>
    </div>
  );
}
