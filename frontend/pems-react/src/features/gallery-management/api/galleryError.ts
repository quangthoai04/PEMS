import { AxiosError } from 'axios';

interface ApiErrorBody {
  message?: string;
  errorCode?: string;
  errors?: Record<string, string[]>;
}

/**
 * Localized messages for gallery-management error codes (UC-GAL-01..07).
 * Must stay in sync with backend GalleryErrorCodes + the shared FILE_* upload codes.
 */
export const GALLERY_ERROR_MESSAGES: Record<string, string> = {
  GALLERY_MANAGEMENT_FORBIDDEN: 'Bạn không có quyền quản lý gallery.',
  GALLERY_NO_CAMPUS_ASSIGNED: 'Tài khoản chưa được gán cơ sở nên không thể quản lý gallery.',
  GALLERY_ITEM_NOT_FOUND: 'Không tìm thấy gallery item.',
  GALLERY_SCOPE_FORBIDDEN: 'Bạn không có quyền thao tác với gallery item này.',
  GALLERY_LOCATION_NOT_FOUND: 'Không tìm thấy vị trí.',
  GALLERY_LOCATION_SCOPE_FORBIDDEN: 'Bạn không có quyền thêm media vào vị trí này.',
  GALLERY_LOCATION_INACTIVE: 'Vị trí này đang ngừng hoạt động, không thể upload media mới.',
  GALLERY_LOCATION_ALREADY_USED: 'Vị trí này đã có gallery item. Mỗi vị trí chỉ được tạo một gallery item.',
  GALLERY_FILES_REQUIRED: 'Vui lòng chọn ít nhất một tệp media.',
  GALLERY_TOO_MANY_FILES: 'Chỉ được tải lên tối đa 5 tệp mỗi lần.',
  GALLERY_INVALID_MEDIA_FILE: 'Tệp không phải ảnh hoặc video được hỗ trợ.',
  GALLERY_MEDIA_REQUIRED: 'Gallery item phải có ít nhất một file media.',
  GALLERY_INVALID_STATUS: 'Trạng thái không hợp lệ.',
  GALLERY_PRIMARY_MEDIA_INVALID: 'Media chính được chọn không thuộc gallery item này.',
  GALLERY_NO_ACTIVE_MEDIA: 'Không thể hiển thị gallery item khi chưa có media khả dụng.',
  // Shared file-upload foundation codes.
  FILE_EMPTY: 'Tệp rỗng hoặc không hợp lệ.',
  FILE_TOO_LARGE: 'Tệp vượt quá kích thước cho phép.',
  FILE_INVALID_EXTENSION: 'Định dạng tệp không được hỗ trợ.',
  FILE_INVALID_TYPE: 'Kiểu nội dung tệp không được hỗ trợ.',
  FILE_MAGIC_BYTES_MISMATCH: 'Nội dung tệp không khớp định dạng (ảnh giả mạo / SVG).',
  GOOGLE_DRIVE_FOLDER_NOT_CONFIGURED: 'Hệ thống lưu trữ chưa được cấu hình. Vui lòng liên hệ quản trị.',
};

/** Extracts a safe, user-facing Vietnamese message from a gallery API error. */
export function getGalleryErrorMessage(
  error: unknown,
  fallback = 'Đã có lỗi xảy ra. Vui lòng thử lại.',
): string {
  const axiosError = error as AxiosError<ApiErrorBody>;
  const status = axiosError?.response?.status;
  const body = axiosError?.response?.data;

  if (body?.errorCode && GALLERY_ERROR_MESSAGES[body.errorCode]) {
    return GALLERY_ERROR_MESSAGES[body.errorCode];
  }

  if (status === 401) return 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';
  if (status === 403) return 'Bạn không có quyền thực hiện thao tác này với gallery.';

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
