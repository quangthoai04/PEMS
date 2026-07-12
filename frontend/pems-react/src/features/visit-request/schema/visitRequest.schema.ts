import { z } from 'zod';
import { isValidPhoneNumber } from 'libphonenumber-js';
import i18n from '../../../shared/i18n/config';
import { parseApiDate } from '../../../shared/utils/vietnamTime';

/**
 * Form datetime-local values are Vietnam wall-clock strings; parse them as +07:00
 * so the 72h/duration checks give the same verdict on every browser timezone.
 * (The backend re-validates with VietnamNow — this is presentation-side UX only.)
 */
const parseVietnamWallClock = (value: string): Date => parseApiDate(value) ?? new Date(NaN);

const MIN_ADVANCE_HOURS = 72;
const MIN_DURATION_HOURS = 3;

/**
 * Translator for validation messages, scoped to the `validation` namespace.
 *
 * The schema must be REBUILT when the language changes — a Zod schema bakes its
 * messages in at construction time, so a schema created at module scope would keep
 * whatever language was active on first import. Callers build it inside a
 * `useMemo(..., [t, i18n.language])`; see `useVisitRequestForm`.
 */
export type ValidationTranslator = (key: string, options?: Record<string, unknown>) => string;

/** Fallback translator used by the type-only schema instance below. */
const defaultT: ValidationTranslator = (key, options) =>
  i18n.t(key, { ns: 'validation', ...options }) as string;

export type VisitCampusRow = {
  campus: string;
  startDatetime: string;
  endDatetime: string;
};

export function isTimeOverlap(a: VisitCampusRow, b: VisitCampusRow): boolean {
  if (!a.startDatetime || !a.endDatetime || !b.startDatetime || !b.endDatetime) return false;
  const startA = parseVietnamWallClock(a.startDatetime).getTime();
  const endA = parseVietnamWallClock(a.endDatetime).getTime();
  const startB = parseVietnamWallClock(b.startDatetime).getTime();
  const endB = parseVietnamWallClock(b.endDatetime).getTime();
  return startA < endB && startB < endA;
}

export function findCampusTimeOverlaps(visits: VisitCampusRow[]) {
  const conflicts: Array<{ firstIndex: number; secondIndex: number; campusId: string }> = [];
  for (let i = 0; i < visits.length; i++) {
    for (let j = i + 1; j < visits.length; j++) {
      const a = visits[i];
      const b = visits[j];
      if (!a.campus || !b.campus) continue;
      // Duplicate campus is a hard error handled separately. Track overlap for DIFFERENT campuses.
      if (a.campus === b.campus) continue;
      if (isTimeOverlap(a, b)) {
        conflicts.push({ firstIndex: i, secondIndex: j, campusId: a.campus });
      }
    }
  }
  return conflicts;
}

const buildPhoneSchema = (t: ValidationTranslator) => z
  .string()
  .min(1, t('phoneRequired'))
  .refine(
    (val) => {
      try {
        return isValidPhoneNumber(val);
      } catch {
        return false;
      }
    },
    { message: t('phoneInvalid') }
  );

const buildEmailSchema = (t: ValidationTranslator) => z
  .string()
  .min(1, t('emailRequired'))
  .email(t('emailInvalid'));

const buildVisitorSchema = (t: ValidationTranslator) => z.object({
  fullName: z.string().trim().min(1, t('fullNameRequired')).max(100, t('maxLength', { max: 100 })),
  jobTitle: z.string().trim().min(1, t('jobTitleRequired')),
  organization: z.string().trim().min(1, t('organizationRequired')),
  nationality: z.string().trim().min(1, t('nationalityRequired')),
});

const buildSupportTeamSchema = (t: ValidationTranslator) => z.object({
  fullName: z.string().trim().min(1, t('fullNameRequired')).max(100),
  jobTitle: z.string().trim().min(1, t('jobTitleRequired')),
  organization: z.string().trim().min(1, t('organizationRequired')),
  nationality: z.string().trim().min(1, t('nationalityRequired')),
  isAutoFilledFromRegistrant: z.boolean().optional(),
});

// Slot schema factory: the public submit requires 72h advance; the Visitor edit/resubmit
// flow only requires 24h (spec "sửa đơn / gửi lại / hủy trước 24h").
const buildVisitSlotSchema = (minAdvanceHours: number, t: ValidationTranslator) => z
  .object({
    campus: z.string().min(1, t('campusRequired')),
    startDatetime: z.string().min(1, t('startTimeRequired')),
    endDatetime: z.string().min(1, t('endTimeRequired')),
  })
  .superRefine((data, ctx) => {
    if (!data.startDatetime || !data.endDatetime) return;

    const start = parseVietnamWallClock(data.startDatetime);
    const end = parseVietnamWallClock(data.endDatetime);
    const minStart = new Date(Date.now() + minAdvanceHours * 60 * 60 * 1000);

    if (start < minStart) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: t('startTimeMinAdvance', { hours: minAdvanceHours }),
        path: ['startDatetime'],
      });
    }

    if (end <= start) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: t('endTimeAfterStart'),
        path: ['endDatetime'],
      });
      return;
    }

    const durationHours = (end.getTime() - start.getTime()) / (1000 * 60 * 60);
    if (durationHours < MIN_DURATION_HOURS) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: t('minDuration', { hours: MIN_DURATION_HOURS }),
        path: ['endDatetime'],
      });
    }
  });

export const buildVisitRequestSchema = (
  minAdvanceHours: number = MIN_ADVANCE_HOURS,
  t: ValidationTranslator = defaultT,
) => z.object({
  registerInfo: z.object({
    fullName: z.string().min(1, t('fullNameRequired')).max(100),
    organization: z.string().min(1, t('organizationRequired')),
    jobTitle: z.string().min(1, t('jobTitleOrDeptRequired')),
    phone: buildPhoneSchema(t),
    email: buildEmailSchema(t),
    nationality: z.string().min(1, t('nationalityRequired')),
  }),
  delegationName: z.string().min(1, t('delegationNameRequired')),
  visitMode: z.enum(['single', 'multiple']),
  visitType: z.enum(['CAMPUS_TOUR', 'MEETING', 'WORKSHOP', 'SIGNING_CEREMONY', 'EXCHANGE', 'OTHER']),
  visitTypeOther: z.string().optional().default(''),
  visits: z.array(buildVisitSlotSchema(minAdvanceHours, t)).min(1),
  purpose: z.string().min(1, t('purposeRequired')),
  workingContent: z.string().min(1, t('workingContentRequired')),
  visitors: z.array(buildVisitorSchema(t)).min(1, t('atLeastOneVisitor')),
  supportTeam: z.array(buildSupportTeamSchema(t)).min(1, t('atLeastOneSupport')),
  contactPoint: z.object({
    fullName: z.string().trim().min(1, t('fullNameRequired')),
    organization: z.string().trim().min(1, t('organizationShortRequired')),
    phone: buildPhoneSchema(t),
    email: buildEmailSchema(t),
  }),
  workingLanguage: z.enum(['EN', 'VI']),
  // Free text identifying the transportation to FPTU — optional, bounded, no HTML/script.
  transportationNote: z
    .string()
    .max(2000, t('transportationNoteMaxLength', { max: 2000 }))
    .refine((v) => !v.includes('<') && !v.includes('>'), {
      message: t('noHtmlChars'),
    })
    .optional()
    .default(''),
  mediaConsentStatus: z.enum(['AGREED', 'DECLINED']),
  mediaConsentNote: z.string().optional().default(''),
  partnerId: z.number().nullable().optional(),
  partnerSelectionMode: z.enum(['EXISTING_PARTNER', 'NEW_ORGANIZATION']).default('NEW_ORGANIZATION'),
  notes: z.string().optional().default(''),
  timeOverlapConfirmed: z.boolean().optional().default(false),
}).superRefine((data, ctx) => {
  if (data.partnerSelectionMode === 'NEW_ORGANIZATION' && (!data.registerInfo.organization || data.registerInfo.organization.trim().length === 0)) {
    ctx.addIssue({
      code: z.ZodIssueCode.custom,
      path: ['registerInfo', 'organization'],
      message: t('organizationNameRequired'),
    });
  } else if (data.partnerSelectionMode === 'EXISTING_PARTNER') {
    if (data.partnerId === null || data.partnerId === undefined) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['partnerId'],
        message: t('partnerRequired'),
      });
    } else if (!data.registerInfo.organization || data.registerInfo.organization.trim().length === 0) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['partnerId'],
        message: t('partnerNameUnresolved'),
      });
    }
  }

  if (data.visitType === 'OTHER' && (!data.visitTypeOther || data.visitTypeOther.trim() === '')) {
    ctx.addIssue({
      code: z.ZodIssueCode.custom,
      path: ['visitTypeOther'],
      message: t('visitTypeOtherRequired'),
    });
  }

  // Campus count must match the chosen scope. MULTI_CAMPUS never auto-downgrades —
  // it stays "Liên cơ sở" and the user is told to add a second campus.
  const codes = data.visits.map((v) => v.campus?.trim()).filter(Boolean);
  const distinct = new Set(codes);

  if (data.visitMode === 'multiple') {
    const hasDuplicateCampus = codes.length !== distinct.size;

    if (hasDuplicateCampus) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['visits'],
        message: t('duplicateCampus'),
      });
    } else if (distinct.size < 2) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['visits'],
        message: t('multiCampusNeedsTwo'),
      });
    }
  }

  if (data.visitMode === 'single' && distinct.size !== 1) {
    ctx.addIssue({
      code: z.ZodIssueCode.custom,
      path: ['visits'],
      message: t('singleCampusExactlyOne'),
    });
  }

});

/** Advance-notice thresholds: public submit needs 72h, Visitor edit/resubmit only 24h. */
export const VISIT_REQUEST_MIN_ADVANCE_HOURS = MIN_ADVANCE_HOURS;
export const VISIT_REQUEST_EDIT_MIN_ADVANCE_HOURS = 24;

export type VisitRequestSchema = z.infer<ReturnType<typeof buildVisitRequestSchema>>;
