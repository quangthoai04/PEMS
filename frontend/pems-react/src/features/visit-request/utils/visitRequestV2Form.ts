import type { VisitRequestSchema } from '../schema/visitRequest.schema';
import type { CampusVisitSchema, VisitRequestV2Schema } from '../schema/visitRequestV2.schema';
import type {
  V2CampusVisitForm,
  V2CampusVisitEdit,
  V2CreatePayload,
  V2EditPayload,
} from '../api/visitRequestV2Api';
import type { CampusProcessingChoice } from '../api/visitRequestApi';

/**
 * Pure helpers for the per-campus form v2. Everything here is side-effect free so the
 * copy/apply-all/migration/payload rules are unit-testable without React.
 */

export const newClientKey = (): string =>
  typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : `ck-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;

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
  visitors: [{ fullName: '', jobTitle: '', organization: '', nationality: '' }],
  supportTeam: [],
  operationalContact: { fullName: '', organization: '', phone: '', email: '' },
  workingLanguage: 'VI',
  transportationNote: '',
  mediaConsentStatus: 'DECLINED',
  mediaConsentNote: '',
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
): CampusVisitSchema => ({
  ...deepClone(source),
  clientKey: target.clientKey,
  visitInstanceId: target.visitInstanceId ?? null,
  expectedRowVersion: target.expectedRowVersion ?? null,
  campus: target.campus,
  startDatetime: target.startDatetime,
  endDatetime: target.endDatetime,
});

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
  processing: CampusProcessingChoice | undefined,
): V2CampusVisitForm => ({
  campusId: cv.campus.trim().toUpperCase(),
  plannedStartAt: cv.startDatetime,
  plannedEndAt: cv.endDatetime,
  delegationName: cv.delegationName.trim(),
  visitType: cv.visitType,
  visitTypeOther: cv.visitType === 'OTHER' ? trimOrNull(cv.visitTypeOther) : null,
  purpose: cv.purpose.trim(),
  workingContent: trimOrNull(cv.workingContent),
  visitors: cv.visitors.map(v => ({
    fullName: v.fullName.trim(),
    jobTitle: v.jobTitle.trim(),
    organization: v.organization.trim(),
    nationality: v.nationality.trim(),
  })),
  externalSupportMembers: cv.supportTeam.map(s => ({
    fullName: s.fullName.trim(),
    jobTitle: (s.jobTitle ?? '').trim(),
    organization: (s.organization ?? '').trim(),
    nationality: (s.nationality ?? '').trim(),
  })),
  operationalContact: {
    fullName: cv.operationalContact.fullName.trim(),
    organization: (cv.operationalContact.organization ?? '').trim(),
    phone: cv.operationalContact.phone.trim(),
    email: (cv.operationalContact.email ?? '').trim(),
  },
  workingLanguage: cv.workingLanguage,
  transportationNote: trimOrNull(cv.transportationNote),
  mediaConsentStatus: cv.mediaConsentStatus,
  mediaConsentNote: trimOrNull(cv.mediaConsentNote),
  notes: trimOrNull(cv.notes),
  processing: processing
    ? { mode: processing.mode, hostUserId: processing.hostUserId ?? null }
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
  campusProcessing: CampusProcessingChoice[] = [],
): V2CreatePayload => {
  const processingByCampus = new Map(
    campusProcessing.map(p => [p.campusId.trim().toUpperCase(), p]),
  );
  return {
    submissionId,
    registrant: {
      fullName: values.registerInfo.fullName.trim(),
      nationality: values.registerInfo.nationality.trim(),
      organization: values.registerInfo.organization.trim(),
      jobTitle: values.registerInfo.jobTitle.trim(),
      phone: values.registerInfo.phone.trim(),
      email: values.registerInfo.email.trim(),
    },
    primaryContact: {
      fullName: values.contactPoint.fullName.trim(),
      organization: (values.contactPoint.organization ?? '').trim(),
      phone: values.contactPoint.phone.trim(),
      email: values.contactPoint.email.trim(),
    },
    partnerId: values.partnerSelectionMode === 'EXISTING_PARTNER' ? values.partnerId ?? null : null,
    campusVisits: values.campusVisits.map(cv =>
      toApiCampusVisit(cv, processingByCampus.get(cv.campus.trim().toUpperCase()))),
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
    fullName: values.registerInfo.fullName.trim(),
    nationality: values.registerInfo.nationality.trim(),
    organization: values.registerInfo.organization.trim(),
    jobTitle: values.registerInfo.jobTitle.trim(),
    phone: values.registerInfo.phone.trim(),
    email: values.registerInfo.email.trim(),
  },
  primaryContact: {
    fullName: values.contactPoint.fullName.trim(),
    organization: (values.contactPoint.organization ?? '').trim(),
    phone: values.contactPoint.phone.trim(),
    email: values.contactPoint.email.trim(),
  },
  partnerId: values.partnerSelectionMode === 'EXISTING_PARTNER' ? values.partnerId ?? null : null,
  campusVisits: values.campusVisits.map((cv): V2CampusVisitEdit => ({
    ...toApiCampusVisit(cv, undefined),
    visitInstanceId: cv.visitInstanceId ?? null,
    expectedRowVersion: cv.visitInstanceId != null ? cv.expectedRowVersion ?? null : null,
  })),
});

/**
 * v1-shaped projection used ONLY to mint the public OTP challenge: the public v2 flow
 * reuses the v1 `/visit-requests/initiate` endpoint (there is no v2 initiate yet — see
 * FINAL_IMPLEMENTATION_REPORT §6), and that endpoint validates the v1 form shape. The
 * ACTUAL create always sends the full v2 contract to `/v2/visit-requests/verify`; this
 * projection never becomes business content.
 *
 * Times are NEVER adjusted here: a campus shorter than the v1 3-hour minimum will be
 * rejected by initiate and the backend message is surfaced honestly.
 */
export const projectV2ToV1FormValues = (values: VisitRequestV2Schema): VisitRequestSchema => {
  const first = values.campusVisits[0] ?? createEmptyCampusVisit('projection');
  const personKey = (p: { fullName: string; jobTitle?: string; organization?: string; nationality?: string }) =>
    [p.fullName, p.jobTitle ?? '', p.organization ?? '', p.nationality ?? '']
      .map(s => s.trim().replace(/\s+/g, ' ').toLowerCase())
      .join('|');

  const mergedVisitors = new Map<string, VisitRequestSchema['visitors'][number]>();
  const mergedSupport = new Map<string, VisitRequestSchema['supportTeam'][number]>();
  for (const cv of values.campusVisits) {
    for (const v of cv.visitors) {
      if (v.fullName.trim()) mergedVisitors.set(personKey(v), { ...v });
    }
    for (const s of cv.supportTeam) {
      if (s.fullName.trim()) {
        mergedSupport.set(personKey(s), {
          fullName: s.fullName,
          jobTitle: s.jobTitle ?? '',
          organization: s.organization ?? '',
          nationality: s.nationality ?? '',
        });
      }
    }
  }

  return {
    registerInfo: { ...values.registerInfo },
    delegationName: first.delegationName,
    visitMode: values.campusVisits.length > 1 ? 'multiple' : 'single',
    visitType: first.visitType,
    visitTypeOther: first.visitTypeOther ?? '',
    visits: values.campusVisits.map(cv => ({
      campus: cv.campus,
      startDatetime: cv.startDatetime,
      endDatetime: cv.endDatetime,
    })),
    purpose: first.purpose,
    workingContent: first.workingContent ?? '',
    visitors: [...mergedVisitors.values()],
    supportTeam: [...mergedSupport.values()],
    contactPoint: { ...values.contactPoint, organization: values.contactPoint.organization ?? '' },
    workingLanguage: first.workingLanguage,
    transportationNote: first.transportationNote ?? '',
    mediaConsentStatus: first.mediaConsentStatus,
    mediaConsentNote: first.mediaConsentNote ?? '',
    partnerSelectionMode: values.partnerSelectionMode,
    partnerId: values.partnerId ?? null,
    notes: first.notes ?? '',
    timeOverlapConfirmed: false,
  };
};

/**
 * Migrates a GLOBAL (v1-shaped) draft into the per-campus v2 shape by duplicating the
 * global form/people/contact/additional snapshot into EVERY campus the user had selected.
 * Fresh clientKeys are generated (v1 drafts have none); the caller must never overwrite
 * an existing, newer v2 draft with this result.
 */
export const migrateV1DraftToV2 = (
  v1: Partial<VisitRequestSchema>,
): Partial<VisitRequestV2Schema> => {
  const slots = (v1.visits ?? []).length > 0 ? v1.visits! : [{ campus: '', startDatetime: '', endDatetime: '' }];

  const campusVisits: CampusVisitSchema[] = slots.map(slot => ({
    ...createEmptyCampusVisit(),
    campus: slot.campus ?? '',
    startDatetime: slot.startDatetime ?? '',
    endDatetime: slot.endDatetime ?? '',
    delegationName: v1.delegationName ?? '',
    visitType: (v1.visitType as CampusVisitSchema['visitType']) ?? 'CAMPUS_TOUR',
    visitTypeOther: v1.visitTypeOther ?? '',
    purpose: v1.purpose ?? '',
    workingContent: v1.workingContent ?? '',
    visitors: deepClone(
      (v1.visitors ?? []).length > 0
        ? v1.visitors!.map(v => ({
            fullName: v.fullName ?? '',
            jobTitle: v.jobTitle ?? '',
            organization: v.organization ?? '',
            nationality: v.nationality ?? '',
          }))
        : [{ fullName: '', jobTitle: '', organization: '', nationality: '' }],
    ),
    supportTeam: deepClone(
      (v1.supportTeam ?? []).map(s => ({
        fullName: s.fullName ?? '',
        jobTitle: s.jobTitle ?? '',
        organization: s.organization ?? '',
        nationality: s.nationality ?? '',
      })),
    ),
    operationalContact: {
      fullName: v1.contactPoint?.fullName ?? '',
      organization: v1.contactPoint?.organization ?? '',
      phone: v1.contactPoint?.phone ?? '',
      email: v1.contactPoint?.email ?? '',
    },
    workingLanguage: (v1.workingLanguage as 'VI' | 'EN') ?? 'VI',
    transportationNote: v1.transportationNote ?? '',
    mediaConsentStatus: (v1.mediaConsentStatus as 'AGREED' | 'DECLINED') ?? 'DECLINED',
    mediaConsentNote: v1.mediaConsentNote ?? '',
    notes: v1.notes ?? '',
  }));

  return {
    registerInfo: v1.registerInfo
      ? { ...v1.registerInfo }
      : { fullName: '', organization: '', jobTitle: '', phone: '', email: '', nationality: '' },
    contactPoint: v1.contactPoint
      ? { ...v1.contactPoint }
      : { fullName: '', organization: '', phone: '', email: '' },
    partnerSelectionMode: v1.partnerSelectionMode ?? 'NEW_ORGANIZATION',
    partnerId: v1.partnerId ?? null,
    campusVisits,
  };
};

/**
 * Maps a backend (FluentValidation) property path to the RHF field path, so a server
 * error lands on the exact campus card + nested field: `Form.CampusVisits[2].Visitors[0].FullName`
 * → `campusVisits.2.visitors.0.fullName`. Returns null for paths that have no stable form
 * mapping (those stay on the generic submit banner).
 */
export const mapServerFieldPathToFormPath = (serverPath: string): string | null => {
  if (!serverPath) return null;
  let path = serverPath.replace(/^(Form|Edit)\./, '');
  if (!/^(CampusVisits|Registrant|PrimaryContact)/.test(path)) return null;

  path = path
    .replace(/\[(\d+)\]/g, '.$1')
    .split('.')
    .map(seg => (/^\d+$/.test(seg) ? seg : seg.charAt(0).toLowerCase() + seg.slice(1)))
    .join('.');

  const renames: Array<[RegExp, string]> = [
    [/^registrant\./, 'registerInfo.'],
    [/^primaryContact\./, 'contactPoint.'],
    [/\.externalSupportMembers\./, '.supportTeam.'],
    [/\.campusId$/, '.campus'],
    [/\.plannedStartAt$/, '.startDatetime'],
    [/\.plannedEndAt$/, '.endDatetime'],
  ];
  for (const [from, to] of renames) path = path.replace(from, to);
  return path;
};

/** Applies imported Excel rows to ONE campus card only — never a global member list. */
export const applyImportedMembersToCampus = (
  campusVisits: CampusVisitSchema[],
  campusIndex: number,
  kind: 'visitors' | 'supportTeam',
  rows: Array<{ fullName: string; jobTitle: string; organization: string; nationality: string }>,
): CampusVisitSchema[] => {
  const target = campusVisits[campusIndex];
  if (!target) return campusVisits;
  const clipped = rows.slice(0, 200).map(r => deepClone(r));
  return campusVisits.map((cv, i) => (i === campusIndex ? { ...cv, [kind]: clipped } : cv));
};
