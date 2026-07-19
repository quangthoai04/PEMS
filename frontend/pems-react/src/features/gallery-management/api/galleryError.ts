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
  GALLERY_FILES_REQUIRED: 'Vui lòng chọn ít nhất một ảnh hoặc thêm một video YouTube.',
  GALLERY_TOO_MANY_FILES: 'Chỉ được tải lên tối đa 20 media mỗi lần.',
  GALLERY_INVALID_MEDIA_FILE: 'Tệp không phải ảnh được hỗ trợ.',
  GALLERY_VIDEO_UPLOAD_NOT_ALLOWED: 'Khi tải từ máy chỉ chấp nhận ảnh. Vui lòng thêm video qua liên kết YouTube.',
  GALLERY_MEDIA_REQUIRED: 'Gallery item phải có ít nhất một file media.',
  GALLERY_INVALID_STATUS: 'Trạng thái không hợp lệ.',
  GALLERY_PRIMARY_MEDIA_INVALID: 'Media chính được chọn không thuộc gallery item này.',
  GALLERY_NO_ACTIVE_MEDIA: 'Không thể hiển thị gallery item khi chưa có media khả dụng.',
  // Area/location management (UC-LOC-01..09).
  GALLERY_LOCATION_MANAGE_FORBIDDEN: 'Bạn không có quyền thao tác vị trí này.',
  GALLERY_AREA_NOT_FOUND: 'Không tìm thấy khu vực.',
  GALLERY_AREA_REQUIRED: 'Vui lòng chọn khu vực/tòa.',
  GALLERY_NEW_AREA_NAME_REQUIRED: 'Vui lòng nhập tên khu vực/tòa mới.',
  GALLERY_LOCATION_NAME_REQUIRED: 'Vui lòng nhập vị trí cụ thể.',
  GALLERY_AREA_DUPLICATE: 'Khu vực này đã tồn tại. Vui lòng chọn từ danh sách khu vực có sẵn.',
  GALLERY_LOCATION_DUPLICATE: 'Vị trí này đã tồn tại trong khu vực đã chọn.',
  GALLERY_AREA_INACTIVE: 'Khu vực này đang ngừng hoạt động.',
  GALLERY_INVALID_MODE: 'Chế độ thao tác không hợp lệ.',
  GALLERY_ITEM_PUBLISH_BLOCKED_LOCATION_INACTIVE: 'Không thể hiển thị bài đăng vì vị trí đang ngừng hoạt động.',
  GALLERY_ITEM_PUBLISH_BLOCKED_AREA_INACTIVE: 'Không thể hiển thị bài đăng vì khu vực đang ngừng hoạt động.',
  // Area/location cover image + gallery item type.
  GALLERY_AREA_COVER_REQUIRED: 'Vui lòng upload ảnh đại diện khu vực.',
  GALLERY_LOCATION_COVER_REQUIRED: 'Vui lòng upload ảnh đại diện vị trí.',
  GALLERY_AREA_COVER_INVALID: 'Ảnh đại diện khu vực không đúng định dạng.',
  GALLERY_LOCATION_COVER_INVALID: 'Ảnh đại diện vị trí không đúng định dạng.',
  GALLERY_ITEM_TYPE_REQUIRED: 'Vui lòng chọn loại nội dung.',
  GALLERY_ITEM_TYPE_INVALID: 'Vui lòng chọn Media hoặc Đoàn khách.',
  // Bilingual content (VI/EN descriptions + manually uploaded audio).
  GALLERY_DESCRIPTION_TOO_LONG: 'Mô tả không được vượt quá 1000 ký tự.',
  GALLERY_DESCRIPTION_VI_REQUIRED: 'Vui lòng nhập mô tả tiếng Việt.',
  GALLERY_AUDIO_VI_REQUIRED: 'Vui lòng chọn bản ghi âm tiếng Việt.',
  GALLERY_DESCRIPTION_EN_REQUIRED: 'Vui lòng nhập mô tả tiếng Anh.',
  GALLERY_AUDIO_EN_REQUIRED: 'Vui lòng chọn bản ghi âm tiếng Anh.',
  GALLERY_AUDIO_VI_INVALID: 'Bản ghi âm tiếng Việt không đúng định dạng (chỉ MP3/WAV).',
  GALLERY_AUDIO_EN_INVALID: 'Bản ghi âm tiếng Anh không đúng định dạng (chỉ MP3/WAV).',
  GALLERY_AUDIO_TOO_LARGE: 'Bản ghi âm vượt quá dung lượng cho phép (tối đa 20 MB).',
  GALLERY_CONTENT_MISSING: 'Gallery item chưa có nội dung song ngữ hợp lệ.',
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
