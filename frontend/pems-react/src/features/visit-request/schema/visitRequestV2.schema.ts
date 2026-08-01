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
 * "2000" reads as a year; "2.000" reads as a count. Grouped in the reader's own convention, since
 * Vietnamese and English disagree about which separator means which.
 */
const groupDigits = (n: number): string =>
  n.toLocaleString(i18n.language?.startsWith('en') ? 'en-US' : 'vi-VN');

/**
 * A bounded string whose over-limit message NAMES the field and says how far over it is
 * (plan §15). "Vượt quá giới hạn" on its own leaves the user hunting through a long form for
 * whichever box is too full, and pasted text is the usual way to get here — so the message states
 * the field, the ceiling and the current length, and nothing is silently truncated to fit.
 */
const bounded = (base: z.ZodString, max: number, fieldLabel: string, t: ValidationTranslator) =>
  base.superRefine((value, ctx) => {
    if (value.length <= max) return;
    ctx.addIssue({
      code: z.ZodIssueCode.custom,
      message: t('maxLengthFieldWithCurrent', {
        field: fieldLabel, max: groupDigits(max), current: groupDigits(value.length),
      }),
    });
  });

/** Shorthand for fields where the exact overflow count adds nothing (email, nationality…). */
const maxLenMessage = (t: ValidationTranslator, fieldLabel: string, max: number) =>
  t('maxLengthField', { field: fieldLabel, max: groupDigits(max) });

/**
 * Accepts "0912345678" as well as "+84912345678" — see `shared/utils/phoneNumber`, which mirrors
 * the backend rule. The value is normalized to E.164 on submit, not here, so the user keeps seeing
 * what they typed while the form is open.
 *
 * `fieldKey` names WHICH phone is wrong: a form carries three of them (registrant, primary contact,
 * per-campus operational contact) and "Số điện thoại không hợp lệ" beside any of them left the user
 * guessing. The message also states the accepted shapes, because "invalid" without an example is a
 * guessing game the user loses several times before finding the rule (plan §17).
 */
const buildPhoneSchema = (t: ValidationTranslator, fieldKey: string) => {
  const label = t(`fields.${fieldKey}`);
  return z
    .string()
    .nullish()
    .transform((val) => {
      const trimmed = val?.trim();
      return trimmed ? trimmed : null;
    })
    .refine((val) => {
      if (!val) return true;
      return isValidPhone(val);
    }, { message: t('phoneInvalidField', { field: label }) });
};

const buildEmailSchema = (t: ValidationTranslator, fieldKey: string) => {
  const label = t(`fields.${fieldKey}`);
  return z
    .string()
    .trim()
    .min(1, t('requiredField', { field: label }))
    .email(t('emailInvalidField', { field: label }))
    .max(150, maxLenMessage(t, label, 150));
};

const buildPersonSchema = (t: ValidationTranslator, role: 'visitor' | 'support') => {
  const label = (field: string) => t(`fields.${role}${field}`);
  return z.object({
    fullName: bounded(
      z.string().trim().min(1, t('requiredField', { field: label('FullName') })), 150, label('FullName'), t),
    jobTitle: bounded(
      z.string().trim().min(1, t('requiredField', { field: label('JobTitle') })), 150, label('JobTitle'), t),
    organization: bounded(
      z.string().trim().min(1, t('requiredField', { field: label('Organization') })), 200, label('Organization'), t),
    nationality: bounded(
      z.string().trim().min(1, t('requiredField', { field: label('Nationality') })), 100, label('Nationality'), t),
  });
};

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

    delegationName: bounded(
      z.string().trim().min(1, t('delegationNameRequired')), 200, t('fields.delegationName'), t),
    visitType: z.enum(['CAMPUS_TOUR', 'MEETING', 'WORKSHOP', 'SIGNING_CEREMONY', 'EXCHANGE', 'OTHER']),
    visitTypeOther: bounded(z.string(), 200, t('fields.visitTypeOther'), t).optional().default(''),
    purpose: bounded(z.string().trim().min(1, t('purposeRequired')), 2000, t('fields.purpose'), t),
    workingContent: bounded(
      z.string().trim().min(1, t('workingContentRequired')), 4000, t('fields.workingContent'), t),

    visitors: z
      .array(buildPersonSchema(t, 'visitor'))
      .min(1, t('atLeastOneVisitor'))
      .max(V2_MAX_MEMBERS_PER_CAMPUS, t('maxMembers', { max: V2_MAX_MEMBERS_PER_CAMPUS })),
    // The support list may be EMPTY, but a row that EXISTS must be complete — the underlying
    // visit_guest_members columns are NOT NULL, so a half-filled row would fail at insert rather
    // than as a form message.
    supportTeam: z
      .array(buildPersonSchema(t, 'support'))
      .max(V2_MAX_MEMBERS_PER_CAMPUS, t('maxMembers', { max: V2_MAX_MEMBERS_PER_CAMPUS })),

    // Every field is required: this is the person a campus actually calls on the day, and all four
    // columns are relied on downstream.
    operationalContact: z.object({
      fullName: bounded(
        z.string().trim().min(1, t('requiredField', { field: t('fields.operationalFullName') })),
        150, t('fields.operationalFullName'), t),
      organization: bounded(
        z.string().trim().min(1, t('requiredField', { field: t('fields.operationalOrganization') })),
        200, t('fields.operationalOrganization'), t),
      phone: buildPhoneSchema(t, 'operationalPhone'),
      email: buildEmailSchema(t, 'operationalEmail'),
    }),

    workingLanguage: z.enum(['EN', 'VI']),
    transportationNote: bounded(z.string(), 2000, t('fields.transportationNote'), t)
      .refine(noHtml(t), { message: t('noHtmlChars') })
      .optional()
      .default(''),
    mediaConsentStatus: z.enum(['AGREED', 'DECLINED']),
    mediaConsentNote: bounded(z.string(), 2000, t('fields.mediaConsentNote'), t).optional().default(''),
    notes: bounded(z.string(), 2000, t('fields.notes'), t).optional().default(''),
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
      fullName: bounded(
        z.string().trim().min(1, t('fullNameRequired')), 150, t('fields.registrantFullName'), t),
      organization: bounded(
        z.string().trim().min(1, t('organizationRequired')), 200, t('fields.registrantOrganization'), t),
      jobTitle: bounded(
        z.string().trim().min(1, t('jobTitleOrDeptRequired')), 150, t('fields.registrantJobTitle'), t),
      phone: buildPhoneSchema(t, 'registrantPhone'),
      email: buildEmailSchema(t, 'registrantEmail'),
      nationality: bounded(
        z.string().trim().min(1, t('nationalityRequired')), 100, t('fields.registrantNationality'), t),
    }),
    contactPoint: z.object({
      fullName: bounded(
        z.string().trim().min(1, t('requiredField', { field: t('fields.contactFullName') })),
        150, t('fields.contactFullName'), t),
      organization: bounded(
        z.string().trim().min(1, t('requiredField', { field: t('fields.contactOrganization') })),
        200, t('fields.contactOrganization'), t),
      phone: buildPhoneSchema(t, 'contactPhone'),
      email: buildEmailSchema(t, 'contactEmail'),
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
