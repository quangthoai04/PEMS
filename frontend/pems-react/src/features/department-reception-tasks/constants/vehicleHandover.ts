/**
 * Checklist cố định trong biên bản bàn giao XE Ô TÔ ĐIỆN (đơn yêu cầu item_type = TRANSPORT).
 * Tên mục cố định theo mẫu biên bản giấy; Số Lượng/Tình trạng bàn giao được seed sẵn (nhân theo số
 * xe mượn, tình trạng mặc định "Tốt") nhưng vẫn là input thường — người dùng sửa thoải mái.
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

/** Tình trạng bàn giao mặc định — seed sẵn cho mọi dòng có tên, người dùng vẫn sửa được. */
const DEFAULT_CONDITION = 'Tốt';

/** Số lượng mỗi mục nhân theo số xe mượn — mặc định 1:1, trừ các mục có nhiều đơn vị/xe. */
const VEHICLE_ITEM_MULTIPLIER: Record<string, number> = {
  'Gương chiếu hậu': 2,
  'Ghế bọc da': 4,
  'Bánh xe': 4,
};

/** Số lượng seed sẵn cho 1 mục checklist xe = đơn giá mục đó x số xe mượn. Trống nếu chưa rõ số xe. */
export const vehicleItemQuantity = (name: string, vehicleCount?: number | null): string => {
  if (!name || !vehicleCount || vehicleCount <= 0) return '';
  const multiplier = VEHICLE_ITEM_MULTIPLIER[name] ?? 1;
  return String(multiplier * vehicleCount);
};

export const buildDefaultVehicleChecklist = (vehicleCount?: number | null): VehicleChecklistRow[] =>
  VEHICLE_HANDOVER_CHECKLIST.map(row => ({
    name: row.name,
    qty: row.name ? vehicleItemQuantity(row.name, vehicleCount) : row.qty,
    giao: row.name ? DEFAULT_CONDITION : '',
    nhan: '',
  }));

/**
 * Checklist mặc định cho hạng mục KHÔNG phải xe điện (Teabreak, phòng họp, thiết bị...) — bảng
 * bàn giao dùng chung layout với xe điện (thêm/xoá dòng, ô nhập, hàng ghi chú cuối) nhưng không có
 * danh sách item cố định sẵn: chỉ 1 dòng khởi điểm lấy tên/số lượng từ chính đơn yêu cầu.
 */
export const buildDefaultGenericChecklist = (title?: string | null, quantity?: number | null): VehicleChecklistRow[] => [
  { name: title || '', qty: quantity ? String(quantity) : '', giao: title ? DEFAULT_CONDITION : '', nhan: '' },
];
