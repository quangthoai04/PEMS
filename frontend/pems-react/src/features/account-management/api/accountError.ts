import { AxiosError } from 'axios';

/** One active-responsibility group blocking a role change (backend AccountRoleChangeBlocker). */
export interface AccountRoleChangeBlocker {
  type: string;
  count: number;
  affectedVisitCount: number;
  sampleVisitInstanceIds?: number[];
  message: string;
}

export interface AccountRoleChangeBlockedData {
  affectedVisitCount: number;
  blockers: AccountRoleChangeBlocker[];
}

interface ApiErrorBody {
  message?: string;
  errorCode?: string;
  errors?: Record<string, string[]>;
  /** Structured payload for codes that carry one (see backend ConflictException.Data). */
  data?: AccountRoleChangeBlockedData;
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

  // UC-96 Create Staff Leader (Trưởng phòng IC) — HO_CREATE_STAFF_LEADER spec §8/§9/§10.
  CAMPUS_NOT_FOUND: 'Cơ sở được chọn không tồn tại.',
  CAMPUS_INACTIVE: 'Cơ sở được chọn đang không hoạt động. Vui lòng kích hoạt cơ sở trước.',
  IC_DEPARTMENT_MISSING: 'Cơ sở chưa có Phòng Hợp tác Quốc tế đang hoạt động. Vui lòng tạo/kích hoạt phòng IC trước.',
  IC_DEPARTMENT_MULTIPLE: 'Dữ liệu cơ sở không hợp lệ: có nhiều hơn một Phòng Hợp tác Quốc tế đang hoạt động.',
  EMAIL_ALREADY_EXISTS: 'Email này đã tồn tại trong hệ thống. Vui lòng dùng email khác.',
  STAFF_LEADER_ALREADY_EXISTS_ACTIVE: 'Cơ sở này đã có Trưởng phòng IC đang hoạt động. Vui lòng dùng chức năng thay thế Staff Leader nếu muốn đổi người phụ trách.',
  STAFF_LEADER_EXISTS_INACTIVE: 'Cơ sở này đang có Trưởng phòng IC bị vô hiệu hóa. Vui lòng khôi phục tài khoản cũ hoặc dùng chức năng thay thế Staff Leader.',
  STAFF_LEADER_EXISTS_LOCKED: 'Cơ sở này đang có Trưởng phòng IC bị khóa. Vui lòng xử lý trạng thái khóa hoặc dùng chức năng thay thế Staff Leader sau khi xác nhận.',
  IC_LEADER_REFERENCE_MISMATCH: 'Dữ liệu Trưởng phòng IC của cơ sở không nhất quán giữa campus và phòng IC. Vui lòng kiểm tra và sửa dữ liệu trước khi tạo mới.',
  IC_LEADER_PARTIAL_REFERENCE: 'Dữ liệu Trưởng phòng IC của cơ sở chưa đồng bộ. Vui lòng đồng bộ campus và phòng IC trước khi tạo mới.',
  UNLINKED_STAFF_LEADER_EXISTS: 'Đã tồn tại tài khoản Staff Leader trong cơ sở nhưng chưa được liên kết đúng với campus/phòng IC. Vui lòng kiểm tra dữ liệu trước khi tạo mới.',

  // UC-96 Create HO account — HO_CREATE_HO_ACCOUNT spec §6.
  CAMPUS_HO_ALREADY_ACTIVE: 'Cơ sở này đã có tài khoản HO đang hoạt động. Không thể tạo thêm HO cho cùng một cơ sở. Vui lòng dùng chức năng thay thế HO cơ sở nếu muốn đổi người phụ trách.',
  CAMPUS_HO_INACTIVE_EXISTS: 'Cơ sở này đang có tài khoản HO bị vô hiệu hóa. Vui lòng khôi phục tài khoản hiện tại hoặc dùng chức năng thay thế HO cơ sở.',
  CAMPUS_HO_LOCKED_EXISTS: 'Cơ sở này đang có tài khoản HO bị khóa. Vui lòng xử lý trạng thái khóa hoặc thực hiện luồng thay thế HO sau khi xác minh.',
  CAMPUS_HO_DATA_INCONSISTENT: 'Dữ liệu không nhất quán: cơ sở này đang có nhiều hơn một tài khoản HO. Vui lòng xử lý dữ liệu trước khi tạo tài khoản mới.',
  HO_STATUS_CHANGE_REQUIRES_SPECIAL_FLOW: 'Không thể thay đổi trạng thái tài khoản HO bằng thao tác thông thường. Vui lòng sử dụng luồng xử lý HO chuyên biệt.',

  // Replace Staff Leader — REPLACE_STAFF_LEADER spec §15.
  CAMPUS_HAS_NO_STAFF_LEADER: 'Cơ sở này chưa có Staff Leader. Vui lòng dùng chức năng tạo Staff Leader.',
  INVALID_REPLACEMENT_CANDIDATE: 'Nhân sự được chọn không hợp lệ để làm Staff Leader mới.',

  // Pending-account email confirmation (resend / edit email / cancel).
  ACCOUNT_NOT_PENDING: 'Tài khoản không còn ở trạng thái chờ xác nhận email.',
  RESEND_TOO_SOON: 'Vui lòng đợi một lát trước khi gửi lại email xác nhận.',
  RESEND_LIMIT_REACHED:
    'Đã đạt số lần gửi lại tối đa. Vui lòng chỉnh sửa email hoặc liên hệ quản trị.',
  EMAIL_UNCHANGED: 'Email mới trùng với email hiện tại.',

  // Related Visitor Accounts tab (Staff Leader) — UC_StaffLeader_Related_Visitor_Accounts_Tab.
  RELATED_VISITOR_FORBIDDEN: 'Chỉ Trưởng phòng (Staff Leader) mới được xem danh sách Visitor liên quan đến cơ sở.',
  VISITOR_SCOPE_FORBIDDEN: 'Bạn không có quyền xem tài khoản khách này.',

  // Safe account role change (active responsibilities) — spec §11/§16.
  ACCOUNT_ROLE_TARGET_NOT_MANAGEABLE: 'Tài khoản này nằm ngoài phạm vi thay đổi vai trò của bạn.',
  ACCOUNT_ROLE_CHANGE_BLOCKED_BY_ACTIVE_RESPONSIBILITIES:
    'Không thể thay đổi vai trò vì tài khoản còn trách nhiệm đang hoạt động. Vui lòng chuyển giao, hoàn tất hoặc hủy hợp lệ các trách nhiệm đó trước khi thử lại.',
};

/**
 * Codes whose backend message is more informative than any static string can be, because it is
 * assembled from live data (how many visits, which department). The static entry above stays as a
 * fallback for the rare case the body arrives without a message — it must never REPLACE the
 * backend's per-blocker breakdown, which is the whole point of the error (spec §16.3).
 */
const PREFER_BACKEND_MESSAGE = new Set<string>([
  'ACCOUNT_ROLE_CHANGE_BLOCKED_BY_ACTIVE_RESPONSIBILITIES',
]);

/** The blocker breakdown from a 409, or null when this error is not a blocker error. */
export function getAccountRoleChangeBlockers(error: unknown): AccountRoleChangeBlockedData | null {
  const body = (error as AxiosError<ApiErrorBody>)?.response?.data;
  if (body?.errorCode !== 'ACCOUNT_ROLE_CHANGE_BLOCKED_BY_ACTIVE_RESPONSIBILITIES') return null;
  const data = body.data;
  if (!data || !Array.isArray(data.blockers)) return null;
  return data;
}

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

  // Data-driven codes speak for themselves; a static string here would silently discard the
  // per-blocker counts the user needs in order to act.
  if (body?.errorCode && PREFER_BACKEND_MESSAGE.has(body.errorCode) && body.message) {
    return body.message;
  }

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
