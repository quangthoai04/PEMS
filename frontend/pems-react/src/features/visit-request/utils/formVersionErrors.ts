import axios from 'axios';

/**
 * Backend error code raised when a v1 write flow touches a per-campus v2 request
 * (`VisitFormV2ErrorCodes.FormVersionUpgradeRequired`). The UI must NEVER show this as a
 * raw technical error: it routes the user to the v2 experience instead.
 */
export const FORM_VERSION_UPGRADE_REQUIRED = 'FORM_VERSION_UPGRADE_REQUIRED';

export const isFormVersionUpgradeRequired = (error: unknown): boolean => {
  if (!axios.isAxiosError(error)) return false;
  const data = error.response?.data as { errorCode?: unknown } | undefined;
  return data?.errorCode === FORM_VERSION_UPGRADE_REQUIRED;
};

/** Route target for the guidance UX (the per-campus detail screen). */
export const v2DetailPath = (visitRequestId: number | string): string =>
  `/dashboard/visit/v2/${visitRequestId}`;
