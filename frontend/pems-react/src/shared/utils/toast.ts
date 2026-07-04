/**
 * Toast helper thống nhất cho toàn app.
 *
 * App đã mount <Toaster/> đúng 1 lần ở App.tsx (react-hot-toast) nên ở đây chỉ cần
 * dùng lại API `toast`, KHÔNG mount Toaster lần nữa. Helper chuẩn hoá:
 *   - loading → success/error (update cùng 1 toast id, không để loading treo).
 *   - trích message thật từ backend theo thứ tự ưu tiên.
 *   - phân loại lỗi HTTP để message rõ ràng hơn.
 *   - MASK mọi credential (private_key/token/secret) trước khi hiển thị.
 */
import toast from 'react-hot-toast';

/** Thông điệp mặc định theo mã HTTP khi backend không trả message rõ ràng. */
const HTTP_STATUS_MESSAGES: Record<number, string> = {
  400: 'Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.',
  401: 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',
  403: 'Bạn không có quyền thực hiện thao tác này.',
  404: 'Không tìm thấy dữ liệu cần xử lý.',
  409: 'Dữ liệu đang bị xung đột hoặc đã tồn tại.',
  422: 'Dữ liệu chưa đạt điều kiện xử lý.',
  429: 'Bạn thao tác quá nhanh. Vui lòng thử lại sau.',
  500: 'Hệ thống đang gặp lỗi. Vui lòng thử lại sau.',
};

const NETWORK_ERROR_MESSAGE =
  'Không thể kết nối tới máy chủ. Vui lòng kiểm tra mạng hoặc backend.';

const DEFAULT_ERROR_MESSAGE = 'Đã xảy ra lỗi. Vui lòng thử lại.';

/**
 * Che mọi thông tin nhạy cảm (private_key, token, service account JSON, Bearer, JWT…)
 * nếu lỡ backend trả về trong message lỗi. Chỉ che khi phát hiện dấu hiệu bí mật để
 * không đụng tới message tiếng Việt thông thường.
 */
export function maskSecrets(text: string): string {
  if (!text) return text;
  let out = text;
  // Khối PEM private key.
  out = out.replace(
    /-----BEGIN[^-]*PRIVATE KEY-----[\s\S]*?-----END[^-]*PRIVATE KEY-----/gi,
    '[đã ẩn thông tin bí mật]',
  );
  // Cặp key/value nhạy cảm trong JSON hoặc query string.
  out = out.replace(
    /("?(?:private_key|private_key_id|client_secret|client_email|api[_-]?key|access_token|refresh_token|secret|token)"?\s*[:=]\s*)("?)[^",}\s]+\2/gi,
    (_m, prefix) => `${prefix}[đã ẩn]`,
  );
  // Bearer token.
  out = out.replace(/Bearer\s+[A-Za-z0-9._-]+/g, 'Bearer [đã ẩn]');
  // Chuỗi JWT-like.
  out = out.replace(/eyJ[A-Za-z0-9._-]{10,}/g, '[đã ẩn token]');
  return out;
}

function pickString(...values: unknown[]): string | undefined {
  for (const value of values) {
    if (typeof value === 'string' && value.trim()) return value.trim();
  }
  return undefined;
}

/**
 * Trích message an toàn từ lỗi (axios) theo thứ tự ưu tiên:
 *   response.data.message → .error → .title → data(string) → message theo HTTP status
 *   → error.message → fallback. Kết quả luôn được mask secret.
 */
export function getApiErrorMessage(
  error: unknown,
  fallback: string = DEFAULT_ERROR_MESSAGE,
): string {
  const err = error as {
    response?: { status?: number; data?: unknown };
    request?: unknown;
    code?: string;
    message?: string;
  } | null | undefined;

  // Lỗi mạng / không có response (server down, mất mạng, CORS…).
  if (
    err &&
    err.response === undefined &&
    (err.request !== undefined || err.code === 'ERR_NETWORK' || err.message === 'Network Error')
  ) {
    return NETWORK_ERROR_MESSAGE;
  }

  const data = err?.response?.data as
    | { message?: unknown; error?: unknown; title?: unknown }
    | string
    | undefined;

  const raw = pickString(
    typeof data === 'string' ? data : undefined,
    typeof data === 'object' ? data?.message : undefined,
    typeof data === 'object' ? data?.error : undefined,
    typeof data === 'object' ? data?.title : undefined,
  );
  if (raw) return maskSecrets(raw);

  const status = err?.response?.status;
  if (status && HTTP_STATUS_MESSAGES[status]) return HTTP_STATUS_MESSAGES[status];

  // error.message của axios thường là "Request failed with status code 500" — bỏ qua chuỗi
  // kỹ thuật này để ưu tiên fallback nghiệp vụ.
  if (err?.message && !/status code/i.test(err.message) && err.message !== 'Network Error') {
    return maskSecrets(err.message);
  }
  return fallback;
}

/** Toast thành công (id tuỳ chọn để dedupe/cập nhật). */
export function showSuccessToast(message: string, id?: string): string {
  return toast.success(message, id ? { id } : undefined);
}

/** Toast lỗi từ một error bất kỳ (đã trích + mask message). */
export function showErrorToast(error: unknown, fallback?: string, id?: string): string {
  return toast.error(getApiErrorMessage(error, fallback), id ? { id } : undefined);
}

/** Toast lỗi với message đã dựng sẵn (không đi qua getApiErrorMessage). */
export function showMessageErrorToast(message: string, id?: string): string {
  return toast.error(maskSecrets(message), id ? { id } : undefined);
}

/** Toast loading — trả về id để update/dismiss sau khi request kết thúc. */
export function showLoadingToast(message: string, id?: string): string {
  return toast.loading(message, id ? { id } : undefined);
}

/** Update toast (loading) thành success theo id. */
export function updateToastSuccess(id: string, message: string): void {
  toast.success(message, { id });
}

/** Update toast (loading) thành error theo id, trích message từ error. */
export function updateToastError(id: string, error: unknown, fallback?: string): void {
  toast.error(getApiErrorMessage(error, fallback), { id });
}

/** Update toast (loading) thành error với message dựng sẵn theo id. */
export function updateToastMessageError(id: string, message: string): void {
  toast.error(maskSecrets(message), { id });
}

/** Dismiss một toast theo id (hoặc tất cả nếu bỏ trống). */
export function dismissToast(id?: string): void {
  toast.dismiss(id);
}
