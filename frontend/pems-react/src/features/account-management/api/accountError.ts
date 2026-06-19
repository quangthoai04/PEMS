import { AxiosError } from 'axios';

interface ApiErrorBody {
  message?: string;
  errorCode?: string;
  errors?: Record<string, string[]>;
}

/**
 * Localized messages for account-management error codes (UC-95..UC-100).
 * Must stay in sync with backend AccountErrorCodes.
 */
export const ACCOUNT_ERROR_MESSAGES: Record<string, string> = {
  ACCOUNT_LIST_FORBIDDEN: 'Bạn không có quyền xem danh sách tài khoản.',
  CAMPUS_SCOPE_FORBIDDEN: 'Bạn không có quyền xem tài khoản ở cơ sở này.',
  UNSUPPORTED_SORT_COLUMN: 'Cột sắp xếp không hợp lệ.',
  INVALID_ACCOUNT_FILTER: 'Bộ lọc không hợp lệ.',
  RATE_LIMIT_EXCEEDED: 'Bạn thao tác quá nhanh. Vui lòng thử lại sau.',
};

/**
 * Extracts a safe, user-facing message from an account API error. Prefers the localized
 * message for a known errorCode, then the backend message, then field errors, then a
 * status/network fallback. Never surfaces stack traces or internal details.
 */
export function getAccountErrorMessage(
  error: unknown,
  fallback = 'Đã có lỗi xảy ra khi tải danh sách tài khoản. Vui lòng thử lại.',
): string {
  const axiosError = error as AxiosError<ApiErrorBody>;
  const status = axiosError?.response?.status;
  const body = axiosError?.response?.data;

  if (body?.errorCode && ACCOUNT_ERROR_MESSAGES[body.errorCode]) {
    return ACCOUNT_ERROR_MESSAGES[body.errorCode];
  }

  if (status === 401) return 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';
  if (status === 403) return 'Bạn không có quyền xem danh sách tài khoản.';

  if (body?.message) return body.message;

  if (body?.errors) {
    const first = Object.values(body.errors).flat()[0];
    if (first) return first;
  }

  if (axiosError?.code === 'ERR_NETWORK') {
    return 'Không thể kết nối tới máy chủ. Vui lòng kiểm tra kết nối và thử lại.';
  }

  return fallback;
}
