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
    'Tài khoản của bạn không có quyền truy cập cơ sở đã chọn. Vui lòng chọn đúng cơ sở được phân quyền.',
  PORTAL_MISMATCH:
    'Tài khoản này không được phép đăng nhập tại cổng hiện tại. Vui lòng kiểm tra lại cổng đăng nhập phù hợp.',
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

  // Campus Management (UC-82/83/86)
  CAMPUS_MANAGEMENT_FORBIDDEN: 'Bạn không có quyền quản lý campus.',
  CAMPUS_NOT_FOUND: 'Không tìm thấy campus.',
  INVALID_CAMPUS_STATUS: 'Trạng thái campus không hợp lệ.',
  CAMPUS_ACTIVATION_MASTER_DATA_INCOMPLETE:
    'Không thể kích hoạt campus vì còn thiếu thông tin bắt buộc (mã, tên, cơ sở, địa chỉ, số điện thoại, email).',
  CAMPUS_ACTIVATION_MISSING_IC_DEPARTMENT:
    'Không thể kích hoạt campus vì chưa có phòng ban IC đang hoạt động.',
  CAMPUS_CODE_ALREADY_EXISTS: 'Mã campus đã tồn tại.',
  CAMPUS_NAME_ALREADY_EXISTS: 'Tên campus đã tồn tại.',
  CAMPUS_ADDRESS_ALREADY_EXISTS: 'Địa chỉ này đã được sử dụng cho campus khác.',
  CAMPUS_PHONE_ALREADY_EXISTS: 'Số điện thoại này đã được sử dụng cho campus khác.',
  CAMPUS_EMAIL_ALREADY_EXISTS: 'Email này đã được sử dụng cho campus khác.',
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
