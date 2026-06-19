import { AxiosError } from 'axios';

interface ApiErrorBody {
  message?: string;
  errorCode?: string;
  errors?: Record<string, string[]>;
}

/**
 * Maps backend auth error codes (the "errorCode" field) to localized, user-facing
 * messages. Must stay in sync with backend AuthErrorCodes.
 */
export const AUTH_ERROR_MESSAGES: Record<string, string> = {
  CAMPUS_REQUIRED: 'Vui lòng chọn cơ sở trước khi đăng nhập.',
  CAMPUS_MISMATCH:
    'Tài khoản của bạn không thuộc cơ sở đã chọn. Vui lòng chọn đúng cơ sở hoặc liên hệ quản trị viên.',
  WRONG_PORTAL_VISITOR_ACCOUNT:
    'Tài khoản của bạn hiện là Visitor nên không phù hợp với cổng nội bộ. Vui lòng liên hệ Staff Leader của cơ sở để được cập nhật vai trò.',
  WRONG_PORTAL_INTERNAL_ACCOUNT:
    'Tài khoản của bạn thuộc cổng nội bộ. Vui lòng đăng nhập tại cổng nội bộ và chọn đúng cơ sở.',
  INTERNAL_ACCOUNT_NOT_FOUND:
    'Tài khoản của bạn chưa được tạo trong hệ thống nội bộ. Vui lòng liên hệ Staff Leader hoặc quản trị viên của cơ sở để được cấp quyền đăng nhập.',
  PASSWORD_LOGIN_DISABLED: 'Đăng nhập bằng mật khẩu đã bị tắt. Vui lòng sử dụng SSO/FEID.',
  INVALID_CREDENTIALS: 'Email hoặc mật khẩu không đúng.',
  ACCOUNT_INACTIVE: 'Tài khoản của bạn đã bị vô hiệu hóa.',
  ACCOUNT_LOCKED: 'Tài khoản của bạn đang bị khóa. Vui lòng thử lại sau hoặc liên hệ quản trị viên.',
  SSO_DISABLED: 'Đăng nhập bằng Google hiện đang bị tắt.',
  FEID_DISABLED: 'Đăng nhập bằng FEID hiện đang bị tắt.',
  FEID_NOT_CONFIGURED:
    'Đăng nhập bằng FEID hiện chưa khả dụng. Vui lòng dùng phương thức khác hoặc liên hệ quản trị viên.',
  FEID_NOT_ELIGIBLE:
    'Tài khoản FEID của bạn chưa đủ điều kiện đăng nhập. Vui lòng liên hệ quản trị viên.',
  EXTERNAL_AUTH_FAILED: 'Không thể đăng nhập bằng tài khoản này. Vui lòng thử lại.',
  VISITOR_PROVISION_DISABLED: 'Hệ thống chưa cho phép tạo tài khoản Visitor tự động.',
  SESSION_REVOKED: 'Phiên đăng nhập đã bị thu hồi. Vui lòng đăng nhập lại.',
  TOKEN_EXPIRED: 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',
  UNAUTHORIZED: 'Bạn cần đăng nhập để tiếp tục.',
  INTERNAL_SERVER_ERROR: 'Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.',
};

/**
 * Extracts a safe, user-facing message from an API error. Prefers the localized
 * message for a known errorCode, then the backend message, then field errors, then
 * a network/generic fallback. Never surfaces stack traces or internal details.
 */
export function getAuthErrorMessage(error: unknown, fallback = 'Đã có lỗi xảy ra. Vui lòng thử lại.'): string {
  const axiosError = error as AxiosError<ApiErrorBody>;
  const body = axiosError?.response?.data;

  if (body?.errorCode && AUTH_ERROR_MESSAGES[body.errorCode]) {
    return AUTH_ERROR_MESSAGES[body.errorCode];
  }

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
