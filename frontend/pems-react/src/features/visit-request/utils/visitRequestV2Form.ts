import { normalizePhone } from '../../../shared/utils/phoneNumber';

import type { CampusVisitSchema, VisitRequestV2Schema } from '../schema/visitRequestV2.schema';
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
 * the "thêm khách" buttons, an Excel import, "thêm đầu mối vào đoàn" — goes through this, so there is
 * no way to create a row the contact picker cannot name.</p>
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
 */
export const withMemberKeys = (cv: CampusVisitSchema): CampusVisitSchema => {
  const keyed = (rows: MemberRow[] | undefined): MemberRow[] =>
    (rows ?? []).map(row => (row?.clientMemberKey ? row : { ...row, clientMemberKey: newClientKey() }));

  const visitors = keyed(cv.visitors);
  const supportTeam = keyed(cv.supportTeam);

  const legacyIndex = (cv as { operationalContactVisitorIndex?: unknown }).operationalContactVisitorIndex;
  const fromLegacyIndex =
    typeof legacyIndex === 'number' && legacyIndex >= 0 && legacyIndex < visitors.length
      ? visitors[legacyIndex].clientMemberKey ?? null
      : null;

  const picked = cv.operationalContactClientMemberKey ?? fromLegacyIndex;
  const stillPresent = [...visitors, ...supportTeam].some(m => m.clientMemberKey === picked);

  return {
    ...cv,
    visitors,
    supportTeam,
    operationalContactClientMemberKey: picked && stillPresent ? picked : null,
  };
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
): V2CampusVisitForm => ({
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
  // Only sent when it still names a row that is actually in this payload. A key naming nobody is
  // REFUSED by the backend — deleting the person who is the contact has to be told, not absorbed —
  // so sending a stale one would turn a form the user has already fixed into a failed submit. Both
  // lists are searched: support staff travelling with the delegation may hold the role (NP-03).
  operationalContactClientMemberKey:
    cv.operationalContactClientMemberKey
      && [...(cv.visitors ?? []), ...(cv.supportTeam ?? [])]
        .some(m => m.clientMemberKey === cv.operationalContactClientMemberKey)
      ? cv.operationalContactClientMemberKey
      : null,
  // The same pick, named by its PERSISTENT id when the row that holds it has one (plan
  // CanhIter3FixBug). Derived from the SAME source of truth as the key above — there is no separate
  // "relation" state to keep in sync — so null here means either "not in the delegation" or "the
  // picked row is itself brand new this session"; the backend tells the two apart from its own
  // contentChanged, never from which of these two fields is null.
  operationalContactGuestMemberId:
    cv.operationalContactClientMemberKey
      ? [...(cv.visitors ?? []), ...(cv.supportTeam ?? [])]
        .find(m => m.clientMemberKey === cv.operationalContactClientMemberKey)?.guestMemberId ?? null
      : null,
  workingLanguage: cv.workingLanguage,
  transportationNote: trimOrNull(cv.transportationNote),
  mediaConsentStatus: cv.mediaConsentStatus,
  notes: trimOrNull(cv.notes),
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
});

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
      toApiCampusVisit(cv, hostByCampus.get((cv.campus ?? '').trim().toUpperCase()))),
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
