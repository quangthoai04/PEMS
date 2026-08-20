import axios from 'axios';

/**
 * Backend từ chối một QUYẾT ĐỊNH (duyệt / từ chối campus) vì nội dung đã đổi sau khi người duyệt
 * mở màn hình. Mã ổn định dùng chung với pending-edit / safe-edit / amendment — client chỉ có một
 * hợp đồng xung đột để xử lý thay vì mỗi endpoint một kiểu.
 */
export const VISIT_INSTANCE_VERSION_CONFLICT = 'VISIT_INSTANCE_VERSION_CONFLICT';

/**
 * True khi lỗi là xung đột phiên bản campus.
 *
 * Kiểm tra CẢ mã lỗi lẫn HTTP 409: mã lỗi là nguồn chính xác, còn 409 là lưới an toàn cho các
 * proxy/handler không truyền `errorCode` — nhưng chỉ 409 thôi thì chưa đủ, nên mã lỗi được ưu tiên.
 */
export const isInstanceVersionConflict = (error: unknown): boolean => {
  if (!axios.isAxiosError(error)) return false;
  const data = error.response?.data as { errorCode?: unknown } | undefined;
  if (data?.errorCode === VISIT_INSTANCE_VERSION_CONFLICT) return true;
  return error.response?.status === 409 && data?.errorCode === undefined;
};

/**
 * Câu thông báo chặn khi gặp xung đột. Cố ý KHÔNG nói "thử lại": thử lại là chính xác điều không
 * được làm — nội dung đã khác, nên người duyệt phải đọc lại bản mới rồi mới quyết định.
 */
export const INSTANCE_VERSION_CONFLICT_MESSAGE =
  'Thông tin đơn đã được cập nhật sau khi bạn mở màn hình. ' +
  'Vui lòng tải phiên bản mới nhất và xem lại trước khi duyệt.';

/**
 * Giới hạn ký tự DUY NHẤT cho ghi chú quyết định (approve/reject/"Lưu và duyệt") — mirrors
 * `VisitMutationPolicy.DecisionNoteMaxLength` ở backend. Cả ba flow ghi cùng một cột
 * `visit_request_campuses.decision_note`; trước hằng số này mỗi validator backend tự chọn một số
 * (2000/2000/500) và UI đi theo từng số riêng, tạo drift trên cùng một cột với cùng một ý nghĩa.
 */
export const DECISION_NOTE_MAX_LENGTH = 2000;
