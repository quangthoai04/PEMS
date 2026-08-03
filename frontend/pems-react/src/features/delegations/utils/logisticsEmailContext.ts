/**
 * The variable context for LOGISTICS_REQUEST_TO_DEPARTMENT, as the compose-screen preview must supply
 * it.
 *
 * <p>
 * This is a MIRROR of what PrepareVisitLogisticsCommandHandler passes at send time, and it is pulled
 * out into its own module so that fact is testable. The preview shares the send's renderer, which
 * rejects an undeclared or missing key rather than substituting a placeholder — so every display value
 * for an empty field is decided here, by the caller, using the same wording the server uses.
 * </p>
 * <p>
 * Getting this wrong is not hypothetical. The context used to pass
 * <code>coordinationNote: payload.offlineCoordinationNote</code>, which is ALWAYS undefined on a
 * SYSTEM_REQUEST — only the "đã trao đổi bên ngoài" form sets that field, and that form sends no email
 * at all. So the preview reliably printed "Không có ghi chú phối hợp." while the send put the Host's
 * description into the very same slot: two different emails from one screen.
 * </p>
 */

/** Wording the server uses for a field the Host left empty. Must match the handler exactly. */
export const EMPTY_DESCRIPTION = 'Chưa có mô tả chi tiết.';
export const EMPTY_QUANTITY = 'Chưa nhập';
export const EMPTY_TIME = 'Chưa chọn thời gian';
export const FALLBACK_LEADER_NAME = 'Trưởng phòng';

export interface LogisticsEmailContextInput {
  title: string;
  itemType: string;
  description?: string | null;
  quantity?: number | null;
  usageStartAt?: string | null;
  usageEndAt?: string | null;
}

/**
 * Exactly the eight keys the template declares — no more, no fewer.
 *
 * The index signature is what lets this be handed to the preview API, which takes an open
 * `Record<string, string>`. The named keys still carry the contract: adding one here without adding it
 * to the registry is what the renderer refuses at send time.
 */
export interface LogisticsEmailContext extends Record<string, string> {
  departmentLeaderName: string;
  requesterName: string;
  logisticsTitle: string;
  logisticsItemType: string;
  quantity: string;
  usageStartAt: string;
  usageEndAt: string;
  logisticsDescription: string;
}

export function buildLogisticsEmailContext(
  payload: LogisticsEmailContextInput,
  options: {
    leaderName?: string | null;
    requesterName: string;
    /** Code → business label, the same mapping the request screen shows. */
    itemTypeLabel: (itemType: string) => string;
    /** "yyyy-MM-ddTHH:mm" → "HH:mm dd/MM/yyyy"; returns '' or '—' when there is nothing to format. */
    formatDateTime: (value?: string | null) => string;
  },
): LogisticsEmailContext {
  const formatted = (value?: string | null) => {
    const text = options.formatDateTime(value);
    // The formatter yields an em dash for a missing value; the server says "Chưa chọn thời gian".
    return !value || !text || text === '—' ? EMPTY_TIME : text;
  };

  return {
    departmentLeaderName: options.leaderName || FALLBACK_LEADER_NAME,
    requesterName: options.requesterName,
    logisticsTitle: payload.title,
    logisticsItemType: options.itemTypeLabel(payload.itemType),
    quantity: payload.quantity != null ? String(payload.quantity) : EMPTY_QUANTITY,
    usageStartAt: formatted(payload.usageStartAt),
    usageEndAt: formatted(payload.usageEndAt),
    logisticsDescription: payload.description?.trim() || EMPTY_DESCRIPTION,
  };
}
