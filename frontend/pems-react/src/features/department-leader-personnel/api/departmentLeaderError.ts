import { AxiosError } from 'axios';
import type { StatusImpactBlocker } from '../types/departmentLeaderPersonnel.types';

interface ApiErrorBody {
  message?: string;
  errorCode?: string;
  errors?: Record<string, string[]>;
  data?: { blockers?: StatusImpactBlocker[] };
}

/**
 * Localized messages keyed by the backend's stable error codes. Must stay in sync with
 * `DepartmentLeaderErrorCodes`. The frontend renders from the CODE, never by matching on message
 * text, so backend wording can change without breaking the UI.
 */
export const DEPARTMENT_LEADER_ERROR_MESSAGES: Record<string, string> = {
  DEPARTMENT_LEADER_REQUIRED: 'Chỉ Trưởng phòng ban mới được quản lý nhân sự phòng ban.',
  DEPARTMENT_CONTEXT_MISSING: 'Tài khoản của bạn chưa được gán phòng ban.',
  DEPARTMENT_NOT_ACTIVE: 'Phòng ban hoặc cơ sở đã ngừng hoạt động.',
  DEPARTMENT_SCOPE_FORBIDDEN: 'Bạn không còn là Trưởng phòng của phòng ban này. Vui lòng đăng nhập lại.',

  PERSONNEL_NOT_FOUND: 'Không tìm thấy nhân sự trong phòng ban của bạn.',
  PERSONNEL_SCOPE_FORBIDDEN: 'Bạn không có quyền thao tác với nhân sự này.',
  PERSONNEL_INVALID_STATUS: 'Trạng thái yêu cầu không hợp lệ.',
  PERSONNEL_SELF_DISABLE_FORBIDDEN: 'Bạn không thể vô hiệu hóa tài khoản của chính mình.',
  CURRENT_LEADER_DISABLE_FORBIDDEN:
    'Không thể vô hiệu hóa Trưởng phòng đương nhiệm. Vui lòng đổi trưởng phòng trước.',
  PERSONNEL_HAS_ACTIVE_RESPONSIBILITIES:
    'Nhân sự đang có nhiệm vụ chưa hoàn thành nên chưa thể vô hiệu hóa.',
  PERSONNEL_EMAIL_CONFIRMATION_PENDING:
    'Tài khoản chỉ được kích hoạt bằng cách xác nhận email. Vui lòng gửi lại email xác nhận.',
  PERSONNEL_SECURITY_LOCKED:
    'Tài khoản đang bị khóa vì lý do bảo mật và phải được mở khóa qua quy trình riêng.',
  PERSONNEL_NOT_PENDING: 'Tài khoản không ở trạng thái chờ xác nhận email.',

  EMAIL_UNCHANGED: 'Email mới trùng với email hiện tại.',
  INVALID_EMAIL: 'Email không đúng định dạng.',
  ACCOUNT_EMAIL_ALREADY_EXISTS: 'Email này đã được sử dụng bởi một tài khoản khác.',
  AUTH_IDENTITY_CONFLICT:
    'Email này đang được liên kết với phương thức đăng nhập của một tài khoản khác.',

  LEADER_CANDIDATE_INVALID: 'Nhân sự được chọn không hợp lệ để làm Trưởng phòng.',
  LEADER_CANDIDATE_NOT_ACTIVE: 'Trưởng phòng mới phải là tài khoản đang hoạt động.',
  LEADER_CANDIDATE_WRONG_DEPARTMENT: 'Nhân sự được chọn không thuộc phòng ban của bạn.',
  LEADERSHIP_ALREADY_CHANGED:
    'Trưởng phòng của phòng ban vừa thay đổi. Vui lòng tải lại trang và thử lại.',
  LEADERSHIP_TRANSFER_CONFLICT: 'Không thể hoàn tất đổi trưởng phòng. Vui lòng thử lại.',

  RESEND_TOO_SOON: 'Vui lòng đợi một lát trước khi gửi lại email xác nhận.',
  RESEND_LIMIT_REACHED:
    'Đã đạt số lần gửi lại tối đa. Vui lòng chỉnh sửa email của nhân sự hoặc liên hệ quản trị hệ thống.',
};

/** Extracts a safe, user-facing Vietnamese message from a department-leader API error. */
export function getDepartmentLeaderErrorMessage(
  error: unknown,
  fallback = 'Đã có lỗi xảy ra. Vui lòng thử lại.',
): string {
  const axiosError = error as AxiosError<ApiErrorBody>;
  const status = axiosError?.response?.status;
  const body = axiosError?.response?.data;

  if (body?.errorCode && DEPARTMENT_LEADER_ERROR_MESSAGES[body.errorCode]) {
    return DEPARTMENT_LEADER_ERROR_MESSAGES[body.errorCode];
  }

  // Field-level validation (400) — surface the first specific message rather than a generic one.
  if (body?.errors) {
    const first = Object.values(body.errors).flat()[0];
    if (first) return first;
  }

  if (body?.message) return body.message;

  if (status === 401) return 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';
  if (status === 403) return 'Bạn không có quyền thực hiện thao tác này.';
  if (status === 404) return 'Không tìm thấy dữ liệu yêu cầu.';

  if (axiosError?.code === 'ERR_NETWORK') {
    return 'Không thể kết nối tới máy chủ. Vui lòng kiểm tra kết nối và thử lại.';
  }

  return fallback;
}

/** The stable error code, when the backend supplied one. */
export function getDepartmentLeaderErrorCode(error: unknown): string | undefined {
  return (error as AxiosError<ApiErrorBody>)?.response?.data?.errorCode;
}

/**
 * Blocker breakdown attached to a 409 refusal (e.g. disabling someone who still owns open tasks),
 * so the modal can list the specific reasons instead of one flat sentence.
 */
export function getDepartmentLeaderErrorBlockers(error: unknown): StatusImpactBlocker[] {
  return (error as AxiosError<ApiErrorBody>)?.response?.data?.data?.blockers ?? [];
}

/**
 * True when the caller lost their Department Leader authority mid-session (demoted, department
 * disabled, or leadership transferred away). The page uses this to stop retrying and send the user
 * back to sign in, instead of looping on a screen they can no longer load.
 */
export function isDepartmentLeaderScopeLost(error: unknown): boolean {
  const code = getDepartmentLeaderErrorCode(error);
  return code === 'DEPARTMENT_SCOPE_FORBIDDEN' || code === 'DEPARTMENT_LEADER_REQUIRED';
}
