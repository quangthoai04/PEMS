import { normalizePhone } from '../../../shared/utils/phoneNumber';

import type {
  CampusVisitSchema,
  OperationalContactSource,
  VisitRequestV2Schema,
} from '../schema/visitRequestV2.schema';
import type {
  V2CampusVisitForm,
  V2CampusVisitEdit,
  V2CreatePayload,
  V2EditPayload,
  ResolvedVisitForm,
} from '../api/visitRequestV2Api';
import type { CampusHostSelectionChoice } from '../api/visitRequestApi';

/**
 * Pure helpers for the per-campus form v2. Everything here is side-effect free so the
 * copy/apply-all/migration/payload rules are unit-testable without React.
 */

export const newClientKey = (): string =>
  typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : `ck-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;

/** One person row in a campus card — the shape both `visitors` and `supportTeam` hold. */
type MemberRow = CampusVisitSchema['visitors'][number];

/**
 * A blank member row WITH its stable identity already on it (NP-03).
 *
 * <p>Minted here, at the one moment a row comes into existence, and never again: a key regenerated on
 * re-render would be exactly as useless as the array index it replaced. Every place that adds a row —
 * the "thêm khách" buttons, an Excel import — goes through this, so there is no way to create a row
 * the contact picker cannot name.</p>
 */
export const createEmptyMember = (): MemberRow => ({
  clientMemberKey: newClientKey(),
  guestMemberId: null,
  fullName: '',
  jobTitle: '',
  organization: '',
  organizationPartnerId: null,
  nationality: '',
});

export const createEmptyCampusVisit = (clientKey: string = newClientKey()): CampusVisitSchema => ({
  clientKey,
  visitInstanceId: null,
  expectedRowVersion: null,
  campus: '',
  startDatetime: '',
  endDatetime: '',
  delegationName: '',
  visitType: 'CAMPUS_TOUR',
  visitTypeOther: '',
  purpose: '',
  workingContent: '',
  visitors: [createEmptyMember()],
  supportTeam: [],
  operationalContact: { fullName: '', organization: '', jobTitle: '', phone: '', email: '' },
  // Nobody has been picked from the delegation list yet (NP-03).
  operationalContactClientMemberKey: null,
  // Not decided yet — MEMBER vs EXTERNAL is an explicit choice the user has to make, never a guess
  // this form starts with (plan CanhIter3FixBug).
  operationalContactSource: null,
  workingLanguage: 'VI',
  transportationNote: '',
  /**
   * The value a NEW campus card is born with — "Đồng ý", the answer nearly every delegation gives,
   * so the common case is not a box everyone has to change by hand. It is a visible select with both
   * options side by side and a tooltip saying what is being agreed to, not a hidden assumption, and
   * the value the user leaves it on is exactly what the payload carries.
   *
   * This is the ONE place the born value is written down: `visitRequestV2DraftStorage` reads it from
   * here to decide whether the consent field has been touched, rather than repeating the literal.
   * The two used to be separate constants and drifted apart, which made answering the question read
   * as "nothing changed" and silently cost the user their draft.
   *
   * A campus loaded from the server keeps whatever it was saved with (`resolvedFormToV2Schema`), and
   * a restored draft keeps the user's own answer — neither is overwritten by this default.
   */
  mediaConsentStatus: 'AGREED',
  notes: '',
});

/**
 * The request-level "Yêu cầu bổ sung" a fresh CREATE form is born with — same born values as a
 * fresh campus card (`createEmptyCampusVisit`), because the two used to be the same fields and the
 * defaults must not drift apart. Read from there rather than repeated as literals, for the same
 * reason `visitRequestV2DraftStorage`'s `UNTOUCHED_*` constants are.
 */
export const createEmptyAdditionalRequirements = (): NonNullable<VisitRequestV2Schema['additionalRequirements']> => {
  const born = createEmptyCampusVisit('untouched-sentinel');
  return {
    workingLanguage: born.workingLanguage,
    mediaConsentStatus: born.mediaConsentStatus,
    transportationNote: born.transportationNote,
    notes: born.notes,
  };
};

/** JSON-safe deep clone (the form state is string/number/boolean/null/array only). */
const deepClone = <T>(value: T): T =>
  typeof structuredClone === 'function'
    ? structuredClone(value)
    : (JSON.parse(JSON.stringify(value)) as T);

/**
 * Copies the CONTENT of one campus card into another as a one-time deep clone.
 * The target keeps its own identity and schedule (clientKey, visitInstanceId, campus,
 * start/end): campuses are visited at different times, and campus selection must never
 * be silently overwritten. Editing the copy afterwards never mutates the source.
 */
export const cloneCampusVisitContent = (
  source: CampusVisitSchema,
  target: CampusVisitSchema,
): CampusVisitSchema => {
  // `withMemberKeys` first, so a card that arrived without identities (an old draft) is healed
  // before it is copied — otherwise the copy inherits the same gap and its members can never be
  // named as the contact.
  const cloned = withMemberKeys(deepClone(source));
  // The copy is a DIFFERENT set of people — each campus keeps its own independent member rows, and
  // the backend inserts a distinct guest_member_id per campus. Carrying the source's member keys over
  // would make one identity name two rows, so every row is re-minted and the contact pick is
  // re-pointed at the copy of the person it named (NP-03).
  const remapped = remintMemberKeys(cloned);
  return {
    ...remapped,
    clientKey: target.clientKey,
    visitInstanceId: target.visitInstanceId ?? null,
    expectedRowVersion: target.expectedRowVersion ?? null,
    campus: target.campus,
    startDatetime: target.startDatetime,
    endDatetime: target.endDatetime,
  };
};

/** Fresh member identities for a copied card, with the contact pick following its person. */
const remintMemberKeys = (cv: CampusVisitSchema): CampusVisitSchema => {
  const mapping = new Map<string, string>();
  const remint = (rows: MemberRow[]): MemberRow[] =>
    rows.map(row => {
      const next = newClientKey();
      if (row.clientMemberKey) mapping.set(row.clientMemberKey, next);
      // The copy is a row on a DIFFERENT campus's delegation — the source's own guestMemberId names
      // a VisitGuestMember that belongs to the SOURCE instance and cannot be carried over; the copy
      // has no persisted identity of its own yet, same as any other freshly added row.
      return { ...row, clientMemberKey: next, guestMemberId: null };
    });

  const visitors = remint(cv.visitors ?? []);
  const supportTeam = remint(cv.supportTeam ?? []);
  const picked = cv.operationalContactClientMemberKey;
  return {
    ...cv,
    visitors,
    supportTeam,
    operationalContactClientMemberKey: picked ? mapping.get(picked) ?? null : null,
  };
};

/**
 * The one, shared definition of "does this key name exactly one row" (plan CanhIter3FixBug §5/§7/
 * §19/§21). Zero matches (nothing picked, or the pick went stale) and more than one match (a
 * duplicate key — should not be reachable, but must not be trusted) are both treated as "no valid
 * identity" — callers must never fall back to `.some()` (which cannot tell 1 match from many) or
 * `.find()` (which silently accepts the first of many).
 */
export const resolveExactlyOne = <T extends { clientMemberKey?: string | null }>(
  rows: T[],
  key: string | null | undefined,
): T | null => {
  if (!key) return null;
  const matches = rows.filter(r => !!r.clientMemberKey && r.clientMemberKey === key);
  return matches.length === 1 ? matches[0] : null;
};

/**
 * Whether a currently-linked contact's persisted member would survive a bulk member-list replacement
 * (Excel Replace/Replace Both, Copy From Campus, Apply-To-All — operational-contact consistency fix).
 * `currentContactGuestMemberId == null` (nothing linked) always survives trivially — but that fact
 * alone does NOT mean the replacement is otherwise safe: see {@link preserveTargetContact} for the
 * separate rule a persisted target also needs. Callers resolve the id themselves first, typically via
 * `resolveExactlyOne(currentMembers, currentOperationalContactClientMemberKey)?.guestMemberId ?? null`
 * — the write-side schema has no `operationalContact.guestMemberId` field of its own to read directly.
 */
export const contactSurvivesReplacement = (
  currentContactGuestMemberId: number | null | undefined,
  incomingRows: { guestMemberId?: number | null }[],
): boolean => {
  if (currentContactGuestMemberId == null) return true;
  return incomingRows.filter(r => r.guestMemberId === currentContactGuestMemberId).length === 1;
};

/**
 * For a PERSISTED target campus being overwritten by another campus's content (Apply-To-All, Copy
 * From Campus), re-applies the target's OWN Operational Contact relation and snapshot on top of the
 * proposed next state. Business/member content copies from the source; the target's contact relation
 * and snapshot never do, whether the target was linked or unlinked.
 *
 * <p>Without this, {@link cloneCampusVisitContent} silently carries the SOURCE campus's
 * `operationalContact`/`operationalContactClientMemberKey`/`operationalContactSource` onto the target
 * (it re-mints member keys but copies the contact fields verbatim) — for an unlinked target that means
 * "Target B, relation null" silently becomes "Target B, relation Kim (copied from Campus A)": the
 * frontend's own version of the backend's RelationIntroduced gap. Call this AFTER
 * {@link cloneCampusVisitContent} and BEFORE checking {@link contactSurvivesReplacement}, so the
 * survival check runs against the target's own (preserved) relation, not the source's.</p>
 */
export const preserveTargetContact = (
  target: CampusVisitSchema,
  proposed: CampusVisitSchema,
): CampusVisitSchema => ({
  ...proposed,
  operationalContact: target.operationalContact,
  operationalContactClientMemberKey: target.operationalContactClientMemberKey,
  operationalContactSource: target.operationalContactSource,
});

/**
 * Gives every member row of a campus card an identity, and repairs a pick that has lost its meaning.
 *
 * <p>Called when a card arrives from somewhere that did not mint keys: a draft written before this
 * field existed, or one written by an older build. Without it those rows would submit with no key at
 * all and the contact could not be named — the user would reopen a resumed draft to find the pick
 * quietly gone.</p>
 *
 * <p>A draft from the ARRAY-INDEX era carries `operationalContactVisitorIndex` instead. It is read
 * once, here, and translated into the key of whichever row it happens to point at now — the last time
 * that number is trusted anywhere.</p>
 *
 * <p>This function ONLY mints/repairs keys — it never touches `operationalContactSource`. Inferring
 * that field for a legacy draft is `restoreCampusVisitFromDraft`'s job, not this one's: by the time a
 * `CampusVisitSchema` reaches here it may already carry a real (possibly explicitly `null`) source
 * value that must not be reinterpreted.</p>
 */
export const withMemberKeys = (cv: CampusVisitSchema): CampusVisitSchema => {
  const keyed = (rows: MemberRow[] | undefined): MemberRow[] =>
    (rows ?? []).map(row => (row?.clientMemberKey ? row : { ...row, clientMemberKey: newClientKey() }));

  const visitors = keyed(cv.visitors);
  const supportTeam = keyed(cv.supportTeam);

  const legacyIndex = (cv as { operationalContactVisitorIndex?: unknown }).operationalContactVisitorIndex;
  // Number.isInteger guards against a fractional index passing the range check and then indexing the
  // array at a position that does not exist (`visitors[1.5]` reads back `undefined`).
  const fromLegacyIndex =
    typeof legacyIndex === 'number' && Number.isInteger(legacyIndex)
      && legacyIndex >= 0 && legacyIndex < visitors.length
      ? visitors[legacyIndex].clientMemberKey ?? null
      : null;

  const picked = cv.operationalContactClientMemberKey ?? fromLegacyIndex;
  const stillPresent = !!resolveExactlyOne([...visitors, ...supportTeam], picked);

  return {
    ...cv,
    visitors,
    supportTeam,
    operationalContactClientMemberKey: picked && stillPresent ? picked : null,
  };
};

/**
 * Restores ONE campus card from a stored draft, migrating the pre-`operationalContactSource` shape
 * safely (plan CanhIter3FixBug §12-§15).
 *
 * <p>The subtlety this exists for: `withMemberKeys({ ...createEmptyCampusVisit(), ...rawCv })` would
 * merge the "not decided yet" default (`operationalContactSource: null`) into a LEGACY draft — one
 * saved before this field existed — before anything downstream could tell "never had the field" apart
 * from "the user explicitly left it undecided". Inference has to run on the RAW object, before that
 * merge, which is why this checks `hasOwnProperty` rather than `=== undefined` on the merged result.</p>
 *
 * <p>Evidence a legacy draft once had a member picked — a real key string, or an in-range integer
 * legacy index — infers `'MEMBER'` even if that key no longer resolves to anyone after repair: a stale
 * key is still evidence of the user's original choice and must not be silently reread as EXTERNAL.</p>
 */
export const restoreCampusVisitFromDraft = (
  rawCv: Partial<CampusVisitSchema> & Record<string, unknown>,
): CampusVisitSchema => {
  const hadSourceField = Object.prototype.hasOwnProperty.call(rawCv, 'operationalContactSource');
  const defaults = createEmptyCampusVisit((rawCv.clientKey as string) || newClientKey());
  const repaired = withMemberKeys({ ...defaults, ...rawCv } as CampusVisitSchema);
  // New-format draft: whatever it recorded — including an explicit `null` — stands as the user's own
  // answer, not evidence to be reinterpreted.
  if (hadSourceField) return repaired;

  const rawKey = rawCv.operationalContactClientMemberKey;
  const rawKeyPresent = typeof rawKey === 'string' && rawKey.trim().length > 0;
  const legacyIndex = (rawCv as { operationalContactVisitorIndex?: unknown }).operationalContactVisitorIndex;
  const rawVisitors = Array.isArray(rawCv.visitors) ? rawCv.visitors : [];
  const legacyIndexValid =
    typeof legacyIndex === 'number' && Number.isInteger(legacyIndex)
      && legacyIndex >= 0 && legacyIndex < rawVisitors.length;
  const rawMemberEvidence = rawKeyPresent || legacyIndexValid;

  const rawContact = rawCv.operationalContact as Record<string, unknown> | undefined;
  const contactHasData = !!rawContact && (['fullName', 'organization', 'jobTitle', 'phone', 'email'] as const)
    .some(f => typeof rawContact[f] === 'string' && (rawContact[f] as string).trim().length > 0);

  const inferred: OperationalContactSource = rawMemberEvidence ? 'MEMBER' : contactHasData ? 'EXTERNAL' : null;
  return { ...repaired, operationalContactSource: inferred };
};

/**
 * "Apply to the remaining campuses": returns a NEW array where every campus except the
 * source receives a deep copy of the source content (identity + schedule preserved).
 * Callers MUST confirm with the user first — `listOverwrittenCampuses` names the cards
 * whose typed content this will replace.
 */
export const applyContentToAllCampuses = (
  campusVisits: CampusVisitSchema[],
  sourceIndex: number,
): CampusVisitSchema[] => {
  const source = campusVisits[sourceIndex];
  if (!source) return campusVisits;
  return campusVisits.map((cv, i) => (i === sourceIndex ? cv : cloneCampusVisitContent(source, cv)));
};

/** True when the user has typed meaningful CONTENT into the card (used for destructive confirms). */
export const campusVisitHasUserContent = (cv: CampusVisitSchema): boolean =>
  Boolean(
    cv.delegationName?.trim() ||
    cv.purpose?.trim() ||
    cv.workingContent?.trim() ||
    cv.notes?.trim() ||
    cv.transportationNote?.trim() ||
    cv.operationalContact?.fullName?.trim() ||
    cv.operationalContact?.phone?.trim() ||
    cv.operationalContact?.email?.trim() ||
    cv.visitors?.some(v => v.fullName?.trim() || v.organization?.trim()) ||
    cv.supportTeam?.some(s => s.fullName?.trim() || s.organization?.trim()),
  );

/** Campuses (labels) whose non-empty content an apply-to-all from `sourceIndex` would overwrite. */
export const listOverwrittenCampuses = (
  campusVisits: CampusVisitSchema[],
  sourceIndex: number,
  labelOf: (cv: CampusVisitSchema, index: number) => string,
): string[] =>
  campusVisits
    .map((cv, index) => ({ cv, index }))
    .filter(({ cv, index }) => index !== sourceIndex && campusVisitHasUserContent(cv))
    .map(({ cv, index }) => labelOf(cv, index));

const trimOrNull = (v: string | undefined | null): string | null => {
  const s = (v ?? '').trim();
  return s.length > 0 ? s : null;
};

const toApiCampusVisit = (
  cv: CampusVisitSchema,
  hostChoice: CampusHostSelectionChoice | undefined,
  /**
   * The CREATE form's request-level "Yêu cầu bổ sung", when the caller has one to apply. Present
   * only from `buildV2CreatePayload` — when given, its 4 fields override `cv`'s own (which on
   * CREATE never left their born defaults, since the UI no longer writes to them per campus).
   * Absent from `buildV2EditPayload`, which keeps sourcing these fields from `cv` itself: an
   * existing campus's own copy is what the per-campus EDIT screens let the user change.
   */
  additionalRequirements?: VisitRequestV2Schema['additionalRequirements'],
): V2CampusVisitForm => {
  // Exact-one, resolved ONCE and shared by both relation fields below — never `.some()` (cannot tell
  // one match from several) or `.find()` (silently accepts the first of several).
  const exactMember = resolveExactlyOne(
    [...(cv.visitors ?? []), ...(cv.supportTeam ?? [])],
    cv.operationalContactClientMemberKey,
  );
  // Fresh campus (no visitInstanceId): the relation may only serialize when the user explicitly chose
  // MEMBER, even if a stray valid key happens to sit in form state under EXTERNAL/null — that state
  // should not be reachable once the schema validates, but this builder must not trust that and leak
  // a member link into a payload the user declared EXTERNAL (plan CanhIter3FixBug §16/§18).
  //
  // Existing campus (visitInstanceId set): never renders the new selector at all, so `source` carries
  // no meaning there — the relation keeps deriving from the exact-one key match alone, exactly as
  // before this change, so the edit path never loses a legitimate relation.
  const isNewCampus = cv.visitInstanceId == null;
  const allowRelation = isNewCampus ? cv.operationalContactSource === 'MEMBER' : true;
  const relationKey = allowRelation && exactMember ? exactMember.clientMemberKey ?? null : null;
  const relationGuestMemberId = allowRelation && exactMember ? exactMember.guestMemberId ?? null : null;

  return {
    campusId: (cv.campus ?? '').trim().toUpperCase(),
    plannedStartAt: cv.startDatetime,
    plannedEndAt: cv.endDatetime,
    delegationName: (cv.delegationName ?? '').trim(),
    visitType: cv.visitType,
    visitTypeOther: cv.visitType === 'OTHER' ? trimOrNull(cv.visitTypeOther) : null,
    purpose: (cv.purpose ?? '').trim(),
    workingContent: trimOrNull(cv.workingContent),
    // `organizationPartnerId` rides along with the organization text: the text is what the request
    // will display, the id is which partner profile it actually IS (PART-01).
    visitors: (cv.visitors ?? []).map(v => ({
      fullName: (v.fullName ?? '').trim(),
      jobTitle: (v.jobTitle ?? '').trim(),
      organization: (v.organization ?? '').trim(),
      organizationPartnerId: v.organizationPartnerId ?? null,
      nationality: (v.nationality ?? '').trim(),
      clientMemberKey: v.clientMemberKey ?? null,
      guestMemberId: v.guestMemberId ?? null,
    })),
    externalSupportMembers: (cv.supportTeam ?? []).map(s => ({
      fullName: (s.fullName ?? '').trim(),
      jobTitle: (s.jobTitle ?? '').trim(),
      organization: (s.organization ?? '').trim(),
      organizationPartnerId: s.organizationPartnerId ?? null,
      nationality: (s.nationality ?? '').trim(),
      clientMemberKey: s.clientMemberKey ?? null,
      guestMemberId: s.guestMemberId ?? null,
    })),
    operationalContact: {
      fullName: (cv.operationalContact?.fullName ?? '').trim(),
      organization: (cv.operationalContact?.organization ?? '').trim(),
      phone: normalizePhone(cv.operationalContact?.phone) ?? (cv.operationalContact?.phone ?? '').trim(),
      jobTitle: (cv.operationalContact?.jobTitle ?? '').trim(),
      email: (cv.operationalContact?.email ?? '').trim(),
    },
    // Only sent when it still names a row that is actually in this payload AND (for a fresh campus)
    // the user explicitly chose MEMBER. A key naming nobody is REFUSED by the backend — deleting the
    // person who is the contact has to be told, not absorbed — so sending a stale one would turn a
    // form the user has already fixed into a failed submit. Both lists are searched: support staff
    // travelling with the delegation may hold the role (NP-03). `operationalContactSource` is read
    // above to decide this, but — like every other field on `cv` this function reads without echoing
    // verbatim — it is never itself a property of the object this function returns.
    operationalContactClientMemberKey: relationKey,
    // The same pick, named by its PERSISTENT id when the row that holds it has one (plan
    // CanhIter3FixBug). Derived from the SAME source of truth as the key above — there is no separate
    // "relation" state to keep in sync — so null here means either "not in the delegation" or "the
    // picked row is itself brand new this session"; the backend tells the two apart from its own
    // contentChanged, never from which of these two fields is null.
    operationalContactGuestMemberId: relationGuestMemberId,
    workingLanguage: additionalRequirements?.workingLanguage ?? cv.workingLanguage,
    transportationNote: trimOrNull(additionalRequirements?.transportationNote ?? cv.transportationNote),
    mediaConsentStatus: additionalRequirements?.mediaConsentStatus ?? cv.mediaConsentStatus,
    notes: trimOrNull(additionalRequirements?.notes ?? cv.notes),
    // Omitted entirely when the caller has no host rights: the backend REFUSES a payload from an
    // external submit that names anybody, so sending a placeholder would fail the whole request.
    hostSelection: hostChoice
      ? {
        mode: hostChoice.mode,
        proposedHostUserId:
          hostChoice.mode === 'SELECTED' ? hostChoice.proposedHostUserId ?? null : null,
        confirmedHostConflict: hostChoice.confirmedHostConflict ?? false,
      }
      : null,
  };
};

/**
 * Builds the REAL v2 create contract (`VisitRequestFormDataV2`): every campus is a fully
 * resolved snapshot. No `sameForAll`, no client-sent visitScope/hasMixedCampusDetails —
 * the backend derives those. `campusProcessing` (authenticated Staff/Leader only) is
 * matched to campuses by campus CODE.
 */
export const buildV2CreatePayload = (
  values: VisitRequestV2Schema,
  submissionId: string,
  campusHostSelections: CampusHostSelectionChoice[] = [],
): V2CreatePayload => {
  const hostByCampus = new Map(
    (campusHostSelections ?? []).map(h => [(h.campusId ?? '').trim().toUpperCase(), h]),
  );
  return {
    submissionId,
    registrant: {
      fullName: (values.registerInfo?.fullName ?? '').trim(),
      nationality: (values.registerInfo?.nationality ?? '').trim(),
      organization: (values.registerInfo?.organization ?? '').trim(),
      jobTitle: (values.registerInfo?.jobTitle ?? '').trim(),
      phone: normalizePhone(values.registerInfo?.phone) ?? (values.registerInfo?.phone ?? '').trim(),
      email: (values.registerInfo?.email ?? '').trim(),
    },
    partnerId: values.partnerSelectionMode === 'EXISTING_PARTNER' ? values.partnerId ?? null : null,
    campusVisits: (values.campusVisits ?? []).map(cv =>
      toApiCampusVisit(cv, hostByCampus.get((cv.campus ?? '').trim().toUpperCase()), values.additionalRequirements)),
  };
};

/**
 * Builds the v2 EDIT contract (`VisitRequestEditV2Dto`) for pending-edit/resubmit: the
 * request row version plus, per campus, the stable visitInstanceId + row version for
 * existing instances (null id = campus being added).
 */
export const buildV2EditPayload = (
  values: VisitRequestV2Schema,
  expectedRequestRowVersion: number,
): V2EditPayload => ({
  expectedRequestRowVersion,
  registrant: {
    fullName: (values.registerInfo?.fullName ?? '').trim(),
    nationality: (values.registerInfo?.nationality ?? '').trim(),
    organization: (values.registerInfo?.organization ?? '').trim(),
    jobTitle: (values.registerInfo?.jobTitle ?? '').trim(),
    phone: normalizePhone(values.registerInfo?.phone) ?? (values.registerInfo?.phone ?? '').trim(),
    email: (values.registerInfo?.email ?? '').trim(),
  },
  partnerId: values.partnerSelectionMode === 'EXISTING_PARTNER' ? values.partnerId ?? null : null,
  campusVisits: (values.campusVisits ?? []).map((cv): V2CampusVisitEdit => ({
    ...toApiCampusVisit(cv, undefined),
    visitInstanceId: cv.visitInstanceId ?? null,
    expectedRowVersion: cv.visitInstanceId != null ? cv.expectedRowVersion ?? null : null,
  })),
});



/**
 * Maps a backend (FluentValidation) property path to the RHF field path, so a server
 * error lands on the exact campus card + nested field: `Form.CampusVisits[2].Visitors[0].FullName`
 * → `campusVisits.2.visitors.0.fullName`. Returns null for paths that have no stable form
 * mapping (those stay on the generic submit banner).
 */
export const mapServerFieldPathToFormPath = (serverPath: string): string | null => {
  if (!serverPath) return null;
  let path = serverPath.replace(/^(Form|Edit)\./i, '');
  if (!/^(CampusVisits|Registrant)/i.test(path)) return null;

  path = path
    .replace(/\[(\d+)\]/g, '.$1')
    .split('.')
    .map(seg => (/^\d+$/.test(seg) ? seg : seg.charAt(0).toLowerCase() + seg.slice(1)))
    .join('.');

  const renames: Array<[RegExp, string]> = [
    [/^registrant\./, 'registerInfo.'],
    [/\.externalSupportMembers\./, '.supportTeam.'],
    [/\.campusId$/, '.campus'],
    [/\.plannedStartAt$/, '.startDatetime'],
    [/\.plannedEndAt$/, '.endDatetime'],
  ];
  for (const [from, to] of renames) path = path.replace(from, to);
  return path;
};

/** "2026-08-01T09:00:00" (wall-clock) → "2026-08-01T09:00" for a datetime-local input. */
const toLocalInputValue = (value: string | null | undefined): string => (value ? value.slice(0, 16) : '');

/**
 * Hydrates the per-campus v2 EDIT form from the scoped read model (`ResolvedVisitForm`). Every campus card
 * carries its STABLE `visitInstanceId` and its `expectedRowVersion` (from the instance's `rowVersion`) so the
 * edit payload can enforce per-instance optimistic concurrency; the request-level `rowVersion` is returned
 * separately for `expectedRequestRowVersion`. Fresh clientKeys are minted (the read model has none). The
 * component renders ONLY the campuses the backend scoped into `form.campusVisits` — hidden campuses never appear.
 */
export const resolvedFormToV2Schema = (
  form: ResolvedVisitForm,
): { values: VisitRequestV2Schema; expectedRequestRowVersion: number } => ({
  expectedRequestRowVersion: form.rowVersion,
  values: {
    registerInfo: {
      fullName: form.registrant.fullName,
      organization: form.registrant.organization,
      jobTitle: form.registrant.jobTitle,
      phone: form.registrant.phone,
      email: form.registrant.email,
      nationality: form.registrant.nationality,
    },
    partnerSelectionMode: form.partnerId != null ? 'EXISTING_PARTNER' : 'NEW_ORGANIZATION',
    partnerId: form.partnerId ?? null,
    campusVisits: form.campusVisits.map((cv): CampusVisitSchema => {
      // Fresh member identities for this editing session, remembered per stored guest_member_id so
      // the contact's link can be translated back into a pick below. The KEY is client-side and
      // per-session; the guest_member_id is the server's, and this map is the only place the two
      // meet (NP-03).
      const keyByGuestMemberId = new Map<number, string>();
      const hydrateMember = (m: {
        guestMemberId: number; fullName: string; jobTitle: string;
        organization: string; organizationPartnerId?: number | null; nationality: string;
      }): MemberRow => {
        const clientMemberKey = newClientKey();
        keyByGuestMemberId.set(m.guestMemberId, clientMemberKey);
        return {
          clientMemberKey,
          // The row's own PERSISTENT id, restored alongside the ephemeral key (plan CanhIter3FixBug)
          // — never regenerated, unaffected by editing this row's own fields.
          guestMemberId: m.guestMemberId,
          fullName: m.fullName, jobTitle: m.jobTitle, organization: m.organization,
          organizationPartnerId: m.organizationPartnerId ?? null, nationality: m.nationality,
        };
      };

      const visitors = cv.visitors.map(hydrateMember);
      const supportTeam = cv.supportMembers.map(hydrateMember);

      return {
      clientKey: newClientKey(),
      visitInstanceId: cv.visitInstanceId,
      expectedRowVersion: cv.rowVersion,
      campus: cv.campusCode,
      startDatetime: toLocalInputValue(cv.plannedStartAt),
      endDatetime: toLocalInputValue(cv.plannedEndAt),
      delegationName: cv.delegationName,
      visitType: cv.visitType as CampusVisitSchema['visitType'],
      visitTypeOther: cv.visitTypeOther ?? '',
      purpose: cv.purpose,
      workingContent: cv.workingContent ?? '',
      visitors: visitors.length ? visitors : [createEmptyMember()],
      supportTeam,
      operationalContact: {
        fullName: cv.operationalContact.fullName,
        organization: cv.operationalContact.organization,
        jobTitle: cv.operationalContact.jobTitle,
        phone: cv.operationalContact.phone,
        email: cv.operationalContact.email,
      },
      // Restore "Đầu mối là ai trong đoàn?" from the stored link (NP-03). Both lists were fed into
      // the same map above, so a contact who is one of the SUPPORT staff restores just as a guest
      // does — the previous version searched `visitors` only and silently showed those picks as
      // "chưa chọn". A link naming somebody no longer in either list restores as null, which is the
      // honest rendering of a member who has since been removed.
      operationalContactClientMemberKey:
        cv.operationalContact.guestMemberId == null
          ? null
          : keyByGuestMemberId.get(cv.operationalContact.guestMemberId) ?? null,
      // An EXISTING campus never renders the MEMBER/EXTERNAL selector, so this has no meaning for it
      // — `toApiCampusVisit` treats a campus with a `visitInstanceId` as always allowed to carry its
      // relation regardless of this value (plan CanhIter3FixBug §18/§20).
      operationalContactSource: null,
      workingLanguage: cv.workingLanguage === 'VI' ? 'VI' : 'EN',
      transportationNote: cv.transportationNote ?? '',
      mediaConsentStatus: cv.mediaConsentStatus === 'AGREED' ? 'AGREED' : 'DECLINED',
      // Hydrated from the server, not blanked. Seeding '' here is what made an edit silently erase
      // whatever the guest had written: the form loaded empty and saved that emptiness back.
      notes: cv.notes ?? '',
      };
    }),
  },
});

/** Applies imported Excel rows to ONE campus card only — never a global member list. */
export const applyImportedMembersToCampus = (
  campusVisits: CampusVisitSchema[],
  campusIndex: number,
  kind: 'visitors' | 'supportTeam',
  rows: Array<{ fullName: string; jobTitle: string; organization: string; nationality: string }>,
): CampusVisitSchema[] => {
  const target = campusVisits[campusIndex];
  if (!target) return campusVisits;
  // An imported row is a new person, so it is born with an identity like any other — a spreadsheet
  // carries names, never keys, and a row with no key is a row the contact picker cannot name.
  const clipped = rows.slice(0, 200).map(r => ({ ...createEmptyMember(), ...deepClone(r) }));
  return campusVisits.map((cv, i) => (i === campusIndex ? { ...cv, [kind]: clipped } : cv));
};
