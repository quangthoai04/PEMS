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
import i18n from '../i18n/config';

/** Các mã HTTP có thông điệp riêng trong `toast:http.*`. */
const KNOWN_HTTP_STATUSES = new Set([400, 401, 403, 404, 409, 422, 429, 500]);

/**
 * Đọc message theo ngôn ngữ đang chọn. Phải resolve tại thời điểm gọi (không phải
 * lúc import module) để toast đổi theo ngôn ngữ user vừa chuyển.
 */
const tr = (key: string): string => i18n.t(key, { ns: 'toast' });

/** Thông điệp mặc định theo mã HTTP khi backend không trả message rõ ràng. */
function httpStatusMessage(status: number): string | undefined {
  return KNOWN_HTTP_STATUSES.has(status) ? tr(`http.${status}`) : undefined;
}

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
    tr('mask.secret'),
  );
  // Cặp key/value nhạy cảm trong JSON hoặc query string.
  out = out.replace(
    /("?(?:private_key|private_key_id|client_secret|client_email|api[_-]?key|access_token|refresh_token|secret|token)"?\s*[:=]\s*)("?)[^",}\s]+\2/gi,
    (_m, prefix) => `${prefix}${tr('mask.value')}`,
  );
  // Bearer token.
  out = out.replace(/Bearer\s+[A-Za-z0-9._-]+/g, `Bearer ${tr('mask.value')}`);
  // Chuỗi JWT-like.
  out = out.replace(/eyJ[A-Za-z0-9._-]{10,}/g, tr('mask.token'));
  return out;
}

function pickString(...values: unknown[]): string | undefined {
  for (const value of values) {
    if (typeof value === 'string' && value.trim()) return value.trim();
  }
  return undefined;
}

/** Chữ tiếng Việt có dấu — dùng để phát hiện message backend chưa được dịch. */
const VIETNAMESE_CHARS = /[àáâãèéêìíòóôõùúăđĩũơưạ-ỹ]/i;

/**
 * Map `errorCode` của backend sang message i18n (`errors:api.<CODE>`) nếu có key.
 * Chỉ dùng key đã tồn tại — không bịa code.
 */
function translateErrorCode(errorCode: unknown): string | undefined {
  if (typeof errorCode !== 'string' || !errorCode.trim()) return undefined;
  const key = `errors:api.${errorCode.trim()}`;
  return i18n.exists(key) ? i18n.t(key) : undefined;
}

/**
 * Trích message an toàn từ lỗi (axios) theo thứ tự ưu tiên:
 *   errorCode đã dịch → response.data.message → .error → .title → data(string)
 *   → message theo HTTP status → error.message → fallback. Kết quả luôn được mask secret.
 *
 * Ở chế độ EN, message thô tiếng Việt của backend không được hiển thị thẳng
 * (yêu cầu i18n §8.1) — thay bằng fallback/thông điệp chung theo HTTP status.
 */
export function getApiErrorMessage(error: unknown, fallback?: string): string {
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
    return tr('common.networkError');
  }

  const data = err?.response?.data as
    | { message?: unknown; error?: unknown; title?: unknown; errorCode?: unknown }
    | string
    | undefined;

  const status = err?.response?.status;

  const translated = typeof data === 'object' ? translateErrorCode(data?.errorCode) : undefined;
  if (translated) return translated;

  const raw = pickString(
    typeof data === 'string' ? data : undefined,
    typeof data === 'object' ? data?.message : undefined,
    typeof data === 'object' ? data?.error : undefined,
    typeof data === 'object' ? data?.title : undefined,
  );
  // Backend hiện trả message tiếng Việt và chưa có errorCode chuẩn hoá; ở EN mode
  // ta bỏ qua message thô để không trộn Anh/Việt trên UI public.
  const isUntranslatedVietnamese =
    i18n.language?.startsWith('en') && !!raw && VIETNAMESE_CHARS.test(raw);
  if (raw && !isUntranslatedVietnamese) return maskSecrets(raw);

  if (status !== undefined) {
    const byStatus = httpStatusMessage(status);
    if (byStatus) return byStatus;
  }

  // error.message của axios thường là "Request failed with status code 500" — bỏ qua chuỗi
  // kỹ thuật này để ưu tiên fallback nghiệp vụ.
  if (err?.message && !/status code/i.test(err.message) && err.message !== 'Network Error') {
    return maskSecrets(err.message);
  }
  return fallback ?? tr('common.defaultError');
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
