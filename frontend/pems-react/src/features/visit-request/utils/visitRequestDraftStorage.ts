import type { VisitRequestSchema } from '../schema/visitRequest.schema';

const VISIT_FORM_DRAFT_KEY = 'pems_public_visit_registration_draft';

export type VisitRequestDraft = {
  version: 2;
  savedAt: number;
  expiresAt: number;
  data: Partial<VisitRequestSchema>;
};

export type SaveDraftResult =
  | { success: true; savedAt: number; expiresAt: number }
  | { success: false; error: string };


export function hasMeaningfulVisitRequestData(values: Partial<VisitRequestSchema> | undefined | null): boolean {
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
    values.visitors?.some(x => x.fullName?.trim() || x.organization?.trim()) ||
    values.supportTeam?.some(x => x.fullName?.trim() || x.organization?.trim()) ||
    cp?.fullName?.trim() || cp?.email?.trim() || cp?.phone?.trim() ||
    values.notes?.trim()
  );
}

export function saveVisitRequestDraft(data: Partial<VisitRequestSchema>, expiresInMs: number = 30 * 60 * 1000): SaveDraftResult {
  if (!hasMeaningfulVisitRequestData(data)) {
    return { success: false, error: 'No meaningful data to save' };
  }

  try {
    const sanitizedData = sanitizeVisitRequestDraft(data);

    const payload: VisitRequestDraft = {
      version: 2,
      savedAt: Date.now(),
      expiresAt: Date.now() + expiresInMs,
      data: sanitizedData,
    };

    localStorage.setItem(VISIT_FORM_DRAFT_KEY, JSON.stringify(payload));
    return { success: true, savedAt: payload.savedAt, expiresAt: payload.expiresAt };
  } catch (error) {
    console.warn('Failed to save UC-17 visit request draft', error);
    return { success: false, error: error instanceof Error ? error.message : 'Unknown storage error' };
  }
}

export function loadVisitRequestDraft(): VisitRequestDraft | null {
  try {
    const raw = localStorage.getItem(VISIT_FORM_DRAFT_KEY);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as VisitRequestDraft;

    if (!parsed?.data || !parsed?.expiresAt || Date.now() > parsed.expiresAt || parsed.version < 2) {
      localStorage.removeItem(VISIT_FORM_DRAFT_KEY);
      return null;
    }

    if (!parsed.data.partnerSelectionMode) {
      if (parsed.data.partnerId !== null && parsed.data.partnerId !== undefined) {
        parsed.data.partnerSelectionMode = 'EXISTING_PARTNER';
      } else {
        parsed.data.partnerSelectionMode = 'NEW_ORGANIZATION';
      }
    }

    return parsed;
  } catch (error) {
    console.warn('Failed to load UC-17 visit request draft', error);
    localStorage.removeItem(VISIT_FORM_DRAFT_KEY);
    return null;
  }
}

export function isVisitRequestDraftExpired(draft: VisitRequestDraft | null): boolean {
  if (!draft) return true;
  return Date.now() > draft.expiresAt;
}

export function getVisitRequestDraftRemainingTime(draft: VisitRequestDraft | null): number {
  if (!draft) return 0;
  const remaining = draft.expiresAt - Date.now();
  return remaining > 0 ? remaining : 0;
}

export function clearVisitRequestDraft() {
  try {
    localStorage.removeItem(VISIT_FORM_DRAFT_KEY);
    sessionStorage.removeItem('pems.uc17.visitRequestDraft.v1'); // clear legacy draft
  } catch (error) {
    console.warn('Failed to clear UC-17 visit request draft', error);
  }
}

export function sanitizeVisitRequestDraft(data: Partial<VisitRequestSchema>): Partial<VisitRequestSchema> {
  const cloned = JSON.parse(JSON.stringify(data));

  // Không lưu OTP/session nhạy cảm nếu sau này có đưa vào form state.
  delete cloned.otpCode;
  delete cloned.sessionToken;
  delete cloned.maskedEmail;

  // Không lưu file binary/base64.
  delete cloned.uploadedFile;
  delete cloned.uploadedFiles;
  delete cloned.excelFile;

  // Xóa các trường cũ đã bị loại bỏ theo v8.4
  delete (cloned as any).expectedGuestCount;
  delete (cloned as any).interpreterNote;
  
  if (cloned.visitors) {
    cloned.visitors.forEach((v: any) => {
      delete v.email;
      delete v.phone;
      delete v.isRepresentative;
      delete v.note;
    });
  }

  if (cloned.supportTeam) {
    cloned.supportTeam.forEach((s: any) => {
      delete s.email;
      delete s.phone;
      delete s.isRepresentative;
      delete s.note;
    });
  }

  return cloned;
}
