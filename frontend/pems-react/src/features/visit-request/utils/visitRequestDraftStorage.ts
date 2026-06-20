import type { VisitRequestSchema } from '../schema/visitRequest.schema';

const DRAFT_KEY = 'pems.uc17.visitRequestDraft.v1';

export type VisitRequestDraft = {
  savedAt: string;
  data: Partial<VisitRequestSchema>;
};

export function saveVisitRequestDraft(data: Partial<VisitRequestSchema>) {
  try {
    const sanitizedData = sanitizeDraftData(data);

    const payload: VisitRequestDraft = {
      savedAt: new Date().toISOString(),
      data: sanitizedData,
    };

    sessionStorage.setItem(DRAFT_KEY, JSON.stringify(payload));
  } catch (error) {
    console.warn('Failed to save UC-17 visit request draft', error);
  }
}

export function loadVisitRequestDraft(): VisitRequestDraft | null {
  try {
    const raw = sessionStorage.getItem(DRAFT_KEY);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as VisitRequestDraft;

    if (!parsed?.data || !parsed?.savedAt) {
      sessionStorage.removeItem(DRAFT_KEY);
      return null;
    }

    return parsed;
  } catch (error) {
    console.warn('Failed to load UC-17 visit request draft', error);
    sessionStorage.removeItem(DRAFT_KEY);
    return null;
  }
}

export function clearVisitRequestDraft() {
  try {
    sessionStorage.removeItem(DRAFT_KEY);
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
