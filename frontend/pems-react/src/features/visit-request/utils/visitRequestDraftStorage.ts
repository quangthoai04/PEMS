import type { VisitRequestSchema } from '../schema/visitRequest.schema';

const VISIT_FORM_DRAFT_KEY = 'pems_public_visit_registration_draft';

export type VisitRequestDraft = {
  version: 1;
  savedAt: number;
  expiresAt: number;
  data: Partial<VisitRequestSchema>;
};

export function hasAnyUserInput(values: Partial<VisitRequestSchema> | undefined | null): boolean {
  if (!values) return false;
  const reg = values.registerInfo;
  const cp = values.contactPoint;

  return Boolean(
    reg?.fullName?.trim() ||
    reg?.organization?.trim() ||
    reg?.jobTitle?.trim() ||
    reg?.phone?.trim() ||
    reg?.email?.trim() ||
    reg?.nationality?.trim() ||
    (values.partnerId !== undefined && values.partnerId !== null) ||
    values.delegationName?.trim() ||
    values.purpose?.trim() ||
    values.workingContent?.trim() ||
    values.visits?.some(x => x.startDatetime || x.endDatetime) ||
    values.visitors?.some(x => x.fullName?.trim() || x.email?.trim() || x.organization?.trim()) ||
    values.supportTeam?.some(x => x.fullName?.trim() || x.organization?.trim()) ||
    cp?.fullName?.trim() || cp?.email?.trim() || cp?.phone?.trim() ||
    values.notes?.trim()
  );
}

export function saveVisitRequestDraft(data: Partial<VisitRequestSchema>, expiresInMs: number = 30 * 60 * 1000) {
  if (!hasAnyUserInput(data)) return;

  try {
    const sanitizedData = sanitizeDraftData(data);

    const payload: VisitRequestDraft = {
      version: 1,
      savedAt: Date.now(),
      expiresAt: Date.now() + expiresInMs,
      data: sanitizedData,
    };

    localStorage.setItem(VISIT_FORM_DRAFT_KEY, JSON.stringify(payload));
  } catch (error) {
    console.warn('Failed to save UC-17 visit request draft', error);
  }
}

export function loadVisitRequestDraft(): VisitRequestDraft | null {
  try {
    const raw = localStorage.getItem(VISIT_FORM_DRAFT_KEY);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as VisitRequestDraft;

    if (!parsed?.data || !parsed?.expiresAt || Date.now() > parsed.expiresAt) {
      localStorage.removeItem(VISIT_FORM_DRAFT_KEY);
      return null;
    }

    return parsed;
  } catch (error) {
    console.warn('Failed to load UC-17 visit request draft', error);
    localStorage.removeItem(VISIT_FORM_DRAFT_KEY);
    return null;
  }
}

export function clearVisitRequestDraft() {
  try {
    localStorage.removeItem(VISIT_FORM_DRAFT_KEY);
    sessionStorage.removeItem('pems.uc17.visitRequestDraft.v1'); // clear legacy draft
  } catch (error) {
    console.warn('Failed to clear UC-17 visit request draft', error);
  }
}

function sanitizeDraftData(data: Partial<VisitRequestSchema>): Partial<VisitRequestSchema> {
  const cloned = JSON.parse(JSON.stringify(data));

  // Không lưu OTP/session nhạy cảm nếu sau này có đưa vào form state.
  delete cloned.otpCode;
  delete cloned.sessionToken;
  delete cloned.maskedEmail;

  // Không lưu file binary/base64.
  delete cloned.uploadedFile;
  delete cloned.uploadedFiles;
  delete cloned.excelFile;

  return cloned;
}
