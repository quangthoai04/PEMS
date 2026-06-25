import { AxiosError } from 'axios';

interface ApiErrorBody {
  message?: string;
  errorCode?: string;
  errors?: Record<string, string[]>;
}

/**
 * Localized messages for department-management error codes (UC-101..UC-105).
 * Must stay in sync with backend DepartmentErrorCodes.
 */
export const DEPARTMENT_ERROR_MESSAGES: Record<string, string> = {
  DEPARTMENT_MANAGEMENT_FORBIDDEN: 'Bạn không có quyền quản lý phòng ban.',
  DEPARTMENT_NO_CAMPUS_ASSIGNED: 'Tài khoản chưa được gán cơ sở nên không thể quản lý phòng ban.',
  DEPARTMENT_CAMPUS_NOT_FOUND: 'Cơ sở hiện tại không tồn tại.',
  DEPARTMENT_CAMPUS_INACTIVE: 'Không thể tạo phòng ban trong cơ sở đã ngừng hoạt động.',
  DEPARTMENT_NAME_ALREADY_EXISTS: 'Tên phòng ban đã tồn tại trong cơ sở này.',
  DEPARTMENT_NOT_FOUND: 'Không tìm thấy phòng ban.',
  DEPARTMENT_TYPE_NOT_MANAGEABLE: 'Không thể thay đổi trạng thái của phòng ban mặc định (IC).',
  DEPARTMENT_SCOPE_FORBIDDEN: 'Bạn không có quyền xem hoặc cập nhật phòng ban này.',
  DEPARTMENT_IC_NOT_EDITABLE: 'Không thể chỉnh sửa phòng Hợp tác quốc tế mặc định.',
};

/** Extracts a safe, user-facing Vietnamese message from a department API error. */
export function getDepartmentErrorMessage(
  error: unknown,
  fallback = 'Đã có lỗi xảy ra. Vui lòng thử lại.',
): string {
  const axiosError = error as AxiosError<ApiErrorBody>;
  const status = axiosError?.response?.status;
  const body = axiosError?.response?.data;

  if (body?.errorCode && DEPARTMENT_ERROR_MESSAGES[body.errorCode]) {
    return DEPARTMENT_ERROR_MESSAGES[body.errorCode];
  }

  if (status === 401) return 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';
  if (status === 403) return 'Bạn không có quyền thực hiện thao tác này với phòng ban.';

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
