// ─────────────────────────────────────────────────────────────────────────────
// "May I write the news for this visit?" — the BACKEND's answer, rendered.
//
// The rule itself lives in exactly one place server-side (VisitNewsAccess.Evaluate), and every path
// that asks — the process screen's button, this page's preset, and the create command — reads that
// one verdict. So there is nothing to decide here: this module only turns a stable reason CODE into
// the one sentence that names the real cause.
//
// It exists because the page used to guess. On a preset visit that was missing from the eligible
// list it printed "chưa vào giai đoạn Sau tiếp khách, không yêu cầu tin tức, hoặc bạn không phải
// Host/người tham gia" — three causes at once, at least two of them wrong every time, and none of
// them actionable. Absence from a list is not a reason; a reason code is.
// ─────────────────────────────────────────────────────────────────────────────

/** The stable codes PEMS.Application.Delegations.News.VisitNewsAccess.ReasonCodes emits. */
export const VisitNewsReasonCode = {
  NotInScope: 'NEWS_VISIT_NOT_IN_SCOPE',
  ParticipantRoleNotAllowed: 'NEWS_VISIT_PARTICIPANT_ROLE_NOT_ALLOWED',
  NotInWritingWindow: 'NEWS_VISIT_NOT_IN_WRITING_WINDOW',
  NewsNotRequired: 'NEWS_VISIT_NOT_REQUIRED',
  MediaConsentDenied: 'NEWS_VISIT_MEDIA_CONSENT_DENIED',
  AlreadyExists: 'NEWS_ALREADY_EXISTS_FOR_VISIT_INSTANCE',
} as const;

export type VisitNewsReasonCodeValue =
  (typeof VisitNewsReasonCode)[keyof typeof VisitNewsReasonCode];

/**
 * The backend verdict for the ONE campus the page was opened for (`?visitInstanceId=`).
 * Mirrors `RequestedVisitInstanceEligibilityDto`.
 */
export interface RequestedVisitNewsEligibility {
  visitInstanceId: number;
  canCreate: boolean;
  /** Null exactly when `canCreate`. */
  reasonCode: string | null;
  hasNews: boolean;
  /** The author's existing post, so the page can offer it instead of a dead end. */
  existingNewsId: number | null;
  canEditExisting: boolean;
}

const REASON_KEYS: Record<string, string> = {
  [VisitNewsReasonCode.NotInScope]: 'news:visitEligibility.notInScope',
  [VisitNewsReasonCode.ParticipantRoleNotAllowed]: 'news:visitEligibility.participantRoleNotAllowed',
  [VisitNewsReasonCode.NotInWritingWindow]: 'news:visitEligibility.notInWritingWindow',
  [VisitNewsReasonCode.NewsNotRequired]: 'news:visitEligibility.newsNotRequired',
  [VisitNewsReasonCode.MediaConsentDenied]: 'news:visitEligibility.mediaConsentDenied',
  [VisitNewsReasonCode.AlreadyExists]: 'news:visitEligibility.alreadyExists',
};

/**
 * The i18n key for a reason code, or null when the code is not a visit-news eligibility reason.
 *
 * Use this wherever the code arrives from a channel that carries OTHER codes too — a failed POST can
 * answer VALIDATION_ERROR, and translating that into "chuyến này chưa thể viết tin" would replace a
 * true message with a false one.
 */
export const visitNewsReasonKeyOrNull = (reasonCode: string | null | undefined): string | null =>
  (reasonCode && REASON_KEYS[reasonCode]) || null;

/**
 * The i18n key for a reason code, falling back to a generic sentence.
 *
 * Only for the `requested` verdict, where the field is a reason code BY CONTRACT — so the fallback is
 * reached solely by a code added server-side after this build shipped. Everything the backend emits
 * today is named exactly, because a user told "chưa đủ điều kiện" cannot tell whether to wait, to ask
 * somebody, or to give up.
 */
export const visitNewsReasonKey = (reasonCode: string | null | undefined): string =>
  visitNewsReasonKeyOrNull(reasonCode) ?? 'news:visitEligibility.unknown';
