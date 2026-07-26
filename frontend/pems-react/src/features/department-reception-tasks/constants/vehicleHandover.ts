/**
 * Checklist cố định trong biên bản bàn giao XE Ô TÔ ĐIỆN (đơn yêu cầu item_type = TRANSPORT).
 * Theo mẫu biên bản giấy: cột Số Lượng để trống dạng "___ cái" cho các mục đếm được,
 * 2 cột tình trạng để trống điền tay / khi ký.
 */
export const VEHICLE_HANDOVER_CHECKLIST: { name: string; qty: string }[] = [
  { name: 'Chìa khoá xe', qty: '___ cái' },
  { name: 'Các thiết bị điện của xe', qty: '' },
  { name: 'Gương chiếu hậu', qty: '___ cái' },
  { name: 'Ghế bọc da', qty: '' },
  { name: 'Thân vỏ xe', qty: '' },
  { name: 'Vô lăng', qty: '' },
  { name: 'Cần số', qty: '' },
  { name: 'Các cần gạt xi nhan, cần gạt mưa', qty: '' },
  { name: 'Bánh xe', qty: '___ cái' },
  { name: 'Bộ sạc (trong cốp)', qty: '___ cái' },
  { name: '', qty: '' }, // dòng trống bổ sung khi cần
];

/** Đơn yêu cầu mượn xe (điều phối di chuyển) dùng biên bản xe ô tô điện. */
export const isVehicleHandover = (itemType?: string | null): boolean =>
  String(itemType || '').toUpperCase() === 'TRANSPORT';

export type VehicleChecklistRow = { name: string; qty: string; giao: string; nhan: string };

export const buildDefaultVehicleChecklist = (): VehicleChecklistRow[] =>
  VEHICLE_HANDOVER_CHECKLIST.map(row => ({ name: row.name, qty: row.qty, giao: '', nhan: '' }));
