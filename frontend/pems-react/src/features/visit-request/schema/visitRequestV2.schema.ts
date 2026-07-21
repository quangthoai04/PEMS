import { z } from 'zod';
import { isValidPhone } from '../../../shared/utils/phoneNumber';
import i18n from '../../../shared/i18n/config';
import { parseApiDate } from '../../../shared/utils/vietnamTime';
export type ValidationTranslator = (key: string, options?: Record<string, unknown>) => string;
export type CreatorRole = 'VISITOR' | 'STAFF' | 'STAFF_LEADER';
/**
 * Per-campus form v2 schema (plan §5): the form is `registrant` + `primaryContact`
 * request-level, plus `campusVisits[]` where EVERY element is a complete, independent
 * snapshot (schedule + content + people + operational contact + requirements).
 * "Same for all campuses" is a one-time UI copy — never a shared/inherited state.
 *
 * The backend is the final validator (FluentValidation + service revalidation inside
 * the transaction); everything here is presentation-side UX that mirrors those rules.
 */

const parseVietnamWallClock = (value: string): Date => parseApiDate(value) ?? new Date(NaN);

/** Mirrors CampusVisitFormDtoValidator.MinDurationMinutes — enforced in MINUTES, not hours. */
export const V2_MIN_DURATION_MINUTES = 30;
/**
 * Absolute ceiling mirroring VisitRequestFormDataV2Validator.MaxCampuses — a backstop, NOT the
 * number shown to the user. The real limit is however many campuses are open for registration,
 * which the backend reports at run time; pass it as `maxCampuses` so adding or retiring a campus
 * changes the form without a code change.
 */
export const V2_MAX_CAMPUSES = 10;
/** Mirrors CampusVisitFormDtoValidator.MaxMembers (per campus, visitors and support separately). */
export const V2_MAX_MEMBERS_PER_CAMPUS = 200;
/** Public submit needs 72h advance; Visitor edit/resubmit only 24h (same as v1). */
export const V2_MIN_ADVANCE_HOURS_CREATE = 72;
export const V2_MIN_ADVANCE_HOURS_EDIT = 24;

const defaultT: ValidationTranslator = (key, options) =>
  i18n.t(key, { ns: 'validation', ...options }) as string;

/**
 * Accepts "0912345678" as well as "+84912345678" — see `shared/utils/phoneNumber`, which mirrors
 * the backend rule. The value is normalized to E.164 on submit, not here, so the user keeps seeing
 * what they typed while the form is open.
 */
const buildPhoneSchema = (t: ValidationTranslator) => z
  .string()
  .trim()
  .min(1, t('phoneRequired'))
  .refine(isValidPhone, { message: t('phoneInvalid') });

const buildEmailSchema = (t: ValidationTranslator) => z
  .string()
  .trim()
  .min(1, t('emailRequired'))
  .email(t('emailInvalid'))
  .max(150, t('maxLength', { max: 150 }));

const buildPersonSchema = (t: ValidationTranslator) => z.object({
  fullName: z.string().trim().min(1, t('fullNameRequired')).max(150, t('maxLength', { max: 150 })),
  jobTitle: z.string().trim().min(1, t('jobTitleRequired')).max(150, t('maxLength', { max: 150 })),
  organization: z.string().trim().min(1, t('organizationRequired')).max(200, t('maxLength', { max: 200 })),
  nationality: z.string().trim().min(1, t('nationalityRequired')).max(100, t('maxLength', { max: 100 })),
});

/**
 * The support list may be EMPTY, but a row that EXISTS must be complete — the underlying
 * visit_guest_members columns are NOT NULL, so a half-filled row would fail at insert rather than
 * as a form message. Same shape as a guest.
 */
const buildSupportPersonSchema = buildPersonSchema;

const noHtml = (t: ValidationTranslator) => (v: string | undefined) =>
  !v || (!v.includes('<') && !v.includes('>'));

/**
 * One campus card. `clientKey` is a STABLE client-generated identity used as the React
 * key and preserved in drafts — array index is never used as identity, so removing or
 * reordering campuses cannot re-associate typed data with the wrong card.
 */
export const buildCampusVisitSchema = (minAdvanceHours: number, t: ValidationTranslator) => z
  .object({
    clientKey: z.string().min(1),
    /** Stable instance id when editing an existing campus (null/undefined = new campus). */
    visitInstanceId: z.number().nullable().optional(),
    expectedRowVersion: z.number().nullable().optional(),
    campus: z.string().min(1, t('campusRequired')),
    startDatetime: z.string().min(1, t('startTimeRequired')),
    endDatetime: z.string().min(1, t('endTimeRequired')),

    delegationName: z.string().trim().min(1, t('delegationNameRequired')).max(200),
    visitType: z.enum(['CAMPUS_TOUR', 'MEETING', 'WORKSHOP', 'SIGNING_CEREMONY', 'EXCHANGE', 'OTHER']),
    visitTypeOther: z.string().max(200).optional().default(''),
    purpose: z.string().trim().min(1, t('purposeRequired')).max(2000, t('maxLength', { max: 2000 })),
    workingContent: z.string().trim().min(1, t('workingContentRequired')).max(4000, t('maxLength', { max: 4000 })),

    visitors: z
      .array(buildPersonSchema(t))
      .min(1, t('atLeastOneVisitor'))
      .max(V2_MAX_MEMBERS_PER_CAMPUS, t('maxLength', { max: V2_MAX_MEMBERS_PER_CAMPUS })),
    supportTeam: z
      .array(buildSupportPersonSchema(t))
      .max(V2_MAX_MEMBERS_PER_CAMPUS, t('maxLength', { max: V2_MAX_MEMBERS_PER_CAMPUS })),

    // Every field is required: this is the person a campus actually calls on the day, and all four
    // columns are relied on downstream.
    operationalContact: z.object({
      fullName: z.string().trim().min(1, t('fullNameRequired')).max(150, t('maxLength', { max: 150 })),
      organization: z.string().trim().min(1, t('organizationRequired')).max(200, t('maxLength', { max: 200 })),
      phone: buildPhoneSchema(t),
      email: buildEmailSchema(t),
    }),

    workingLanguage: z.enum(['EN', 'VI']),
    transportationNote: z
      .string()
      .max(2000, t('transportationNoteMaxLength', { max: 2000 }))
      .refine(noHtml(t), { message: t('noHtmlChars') })
      .optional()
      .default(''),
    mediaConsentStatus: z.enum(['AGREED', 'DECLINED']),
    mediaConsentNote: z.string().max(2000).optional().default(''),
    notes: z.string().max(2000).optional().default(''),
  })
  .superRefine((data, ctx) => {
    if (data.visitType === 'OTHER' && (!data.visitTypeOther || data.visitTypeOther.trim() === '')) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['visitTypeOther'],
        message: t('visitTypeOtherRequired'),
      });
    }

    if (!data.startDatetime || !data.endDatetime) return;

    const start = parseVietnamWallClock(data.startDatetime);
    const end = parseVietnamWallClock(data.endDatetime);
    if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return;

    const minStart = new Date(Date.now() + minAdvanceHours * 60 * 60 * 1000);
    if (start < minStart) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: t('startTimeMinAdvance', { hours: minAdvanceHours }),
        path: ['startDatetime'],
      });
    }

    if (end.getTime() <= start.getTime()) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: t('endTimeAfterStart'),
        path: ['endDatetime'],
      });
      return;
    }

    // v2 minimum is 30 MINUTES (millisecond math — 29m59s fails, 30m00s passes).
    // The end time the user typed is never auto-adjusted; only the message is shown.
    const durationMs = end.getTime() - start.getTime();
    if (durationMs < V2_MIN_DURATION_MINUTES * 60 * 1000) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: t('minDurationMinutes', { minutes: V2_MIN_DURATION_MINUTES }),
        path: ['endDatetime'],
      });
    }
  });

export const buildVisitRequestV2Schema = (
  minAdvanceHours: number = V2_MIN_ADVANCE_HOURS_CREATE,
  t: ValidationTranslator = defaultT,
  /** Campuses currently open for registration; falls back to the hard ceiling before they load. */
  maxCampuses: number = V2_MAX_CAMPUSES,
) => z
  .object({
    registerInfo: z.object({
      fullName: z.string().trim().min(1, t('fullNameRequired')).max(150, t('maxLength', { max: 150 })),
      organization: z.string().trim().min(1, t('organizationRequired')).max(200, t('maxLength', { max: 200 })),
      jobTitle: z.string().trim().min(1, t('jobTitleOrDeptRequired')).max(150, t('maxLength', { max: 150 })),
      phone: buildPhoneSchema(t),
      email: buildEmailSchema(t),
      nationality: z.string().trim().min(1, t('nationalityRequired')).max(100, t('maxLength', { max: 100 })),
    }),
    contactPoint: z.object({
      fullName: z.string().trim().min(1, t('fullNameRequired')).max(150, t('maxLength', { max: 150 })),
      organization: z.string().trim().min(1, t('organizationRequired')).max(200, t('maxLength', { max: 200 })),
      phone: buildPhoneSchema(t),
      email: buildEmailSchema(t),
    }),
    partnerSelectionMode: z.enum(['EXISTING_PARTNER', 'NEW_ORGANIZATION']).default('NEW_ORGANIZATION'),
    partnerId: z.number().nullable().optional(),
    campusVisits: z
      .array(buildCampusVisitSchema(minAdvanceHours, t))
      .min(1, t('campusRequired'))
      .max(
        Math.min(maxCampuses, V2_MAX_CAMPUSES),
        t('maxCampuses', { max: Math.min(maxCampuses, V2_MAX_CAMPUSES) }),
      ),
  })
  .superRefine((data, ctx) => {
    if (data.partnerSelectionMode === 'EXISTING_PARTNER' && (data.partnerId === null || data.partnerId === undefined)) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['partnerId'],
        message: t('partnerRequired'),
      });
    }

    // Duplicate campuses: flag EVERY later occurrence on its own card so the error
    // expands/focuses the right accordion, not a generic form-level banner.
    const seen = new Map<string, number>();
    data.campusVisits.forEach((cv, index) => {
      const code = cv.campus?.trim().toUpperCase();
      if (!code) return;
      if (seen.has(code)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ['campusVisits', index, 'campus'],
          message: t('duplicateCampus'),
        });
      } else {
        seen.set(code, index);
      }
    });
  });

export type CampusVisitSchema = z.infer<ReturnType<typeof buildCampusVisitSchema>>;
export type VisitRequestV2Schema = z.infer<ReturnType<typeof buildVisitRequestV2Schema>>;
