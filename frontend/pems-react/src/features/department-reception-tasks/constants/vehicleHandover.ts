/**
 * Checklist cố định trong biên bản bàn giao XE Ô TÔ ĐIỆN (đơn yêu cầu item_type = TRANSPORT).
 * Theo mẫu biên bản giấy: mọi ô Số Lượng/tình trạng để trống, input tự có placeholder "Nhập..."
 * làm hint — không seed sẵn text giả như "___ cái" (dễ nhầm là giá trị thật đã điền).
 */
export const VEHICLE_HANDOVER_CHECKLIST: { name: string; qty: string }[] = [
  { name: 'Chìa khoá xe', qty: '' },
  { name: 'Các thiết bị điện của xe', qty: '' },
  { name: 'Gương chiếu hậu', qty: '' },
  { name: 'Ghế bọc da', qty: '' },
  { name: 'Thân vỏ xe', qty: '' },
  { name: 'Vô lăng', qty: '' },
  { name: 'Cần số', qty: '' },
  { name: 'Các cần gạt xi nhan, cần gạt mưa', qty: '' },
  { name: 'Bánh xe', qty: '' },
  { name: 'Bộ sạc (trong cốp)', qty: '' },
  { name: '', qty: '' }, // dòng trống bổ sung khi cần
];

/** Đơn yêu cầu mượn xe (điều phối di chuyển) dùng biên bản xe ô tô điện. */
export const isVehicleHandover = (itemType?: string | null): boolean =>
  String(itemType || '').toUpperCase() === 'TRANSPORT';

export type VehicleChecklistRow = { name: string; qty: string; giao: string; nhan: string };

export const buildDefaultVehicleChecklist = (): VehicleChecklistRow[] =>
  VEHICLE_HANDOVER_CHECKLIST.map(row => ({ name: row.name, qty: row.qty, giao: '', nhan: '' }));

/**
 * Checklist mặc định cho hạng mục KHÔNG phải xe điện (Teabreak, phòng họp, thiết bị...) — bảng
 * bàn giao dùng chung layout với xe điện (thêm/xoá dòng, ô nhập, hàng ghi chú cuối) nhưng không có
 * danh sách item cố định sẵn: chỉ 1 dòng khởi điểm lấy tên/số lượng từ chính đơn yêu cầu.
 */
export const buildDefaultGenericChecklist = (title?: string | null, quantity?: number | null): VehicleChecklistRow[] => [
  { name: title || '', qty: quantity ? String(quantity) : '', giao: '', nhan: '' },
];
