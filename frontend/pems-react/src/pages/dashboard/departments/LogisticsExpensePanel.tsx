/**
 * LogisticsExpensePanel — Ghi chú chi phí (phía Phòng ban) dạng compact.
 *
 * Hiện bên dưới biên bản bàn giao & nghiệm thu SAU KHI ký nghiệm thu xong.
 * - Không chọn phân loại: mọi dòng của phòng ban khi đồng bộ sang Host đều là "Hạng mục yêu cầu".
 * - Nút "Không có chi phí": xác nhận đơn không phát sinh chi phí (giá về 0, lưu noExpense).
 * - Nằm trong vùng in của biên bản → Tải PDF biên bản sẽ kèm bảng chi phí (nút bấm bị ẩn khi in).
 * - Fetch lỗi (không thuộc phòng ban / chưa đủ điều kiện) → ẩn im lặng, không toast.
 */
import React, { useEffect, useState } from 'react';
import { Loader2, Plus, Save, Trash2, DollarSign, CheckCircle2, Ban } from 'lucide-react';
import toast from 'react-hot-toast';
import visitExpenseService, { VisitExpenseReport, SaveExpenseReportCommand, SaveExpenseItemDto } from '../../../services/visit-expense.service';
import { ConfirmModal } from '../../../components/modals/ConfirmModal';

interface Props {
  logisticsItemId: number;
  readOnly?: boolean;
}

export function LogisticsExpensePanel({ logisticsItemId, readOnly = false }: Props) {
  const [report, setReport] = useState<VisitExpenseReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [items, setItems] = useState<SaveExpenseItemDto[]>([]);
  const [reportNote, setReportNote] = useState('');
  const [confirmNoExpensePopup, setConfirmNoExpensePopup] = useState(false);

  const fetchReport = async () => {
    try {
      setLoading(true);
      const data = await visitExpenseService.getLogisticsExpenseReport(logisticsItemId);
      setReport(data);
      setReportNote(data.reportNote || '');
      setItems(data.items.map(it => ({
        expenseItemId: it.expenseItemId,
        itemOrigin: it.itemOrigin,
        itemName: it.itemName,
        description: it.description,
        quantity: it.quantity,
        unitName: it.unitName,
        unitPrice: it.unitPrice,
        itemNote: it.itemNote,
        displayOrder: it.displayOrder,
      })));
    } catch {
      // Người xem không thuộc phòng ban / chưa đủ điều kiện tạo báo cáo → ẩn panel.
      setReport(null);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchReport();
    // eslint-disable-next-line
  }, [logisticsItemId]);

  if (loading) {
    return (
      <div className="mt-4 flex items-center gap-2 text-xs text-slate-400 print:hidden">
        <Loader2 className="w-4 h-4 animate-spin" /> Đang tải ghi chú chi phí...
      </div>
    );
  }

  if (!report) return null;

  const locked = readOnly || report.status === 'FINALIZED' || report.status === 'CANCELLED';
  const total = items.reduce((sum, it) => sum + (it.quantity || 0) * (it.unitPrice || 0), 0);

  const handleAddItem = () => {
    setItems([...items, { itemOrigin: 'MANUAL', itemName: '', quantity: 1, unitName: '', unitPrice: 0, displayOrder: items.length + 1 }]);
  };

  const handleUpdateItem = (index: number, field: keyof SaveExpenseItemDto, value: any) => {
    const next = [...items];
    (next[index] as any)[field] = value;
    setItems(next);
  };

  const handleRemoveItem = (index: number) => setItems(items.filter((_, i) => i !== index));

  const doSave = async (noExpense: boolean, itemsToSave: SaveExpenseItemDto[]) => {
    if (!report) return;
    try {
      setSaving(true);
      const cmd: SaveExpenseReportCommand = {
        rowVersion: report.rowVersion,
        reportNote,
        noExpense,
        items: itemsToSave.map((it, idx) => ({ ...it, displayOrder: idx + 1 })),
      };
      await visitExpenseService.saveExpenseReport(report.expenseReportId, cmd);
      toast.success(noExpense ? 'Đã xác nhận không có chi phí.' : 'Đã lưu chi phí và đồng bộ sang Host.');
      await fetchReport();
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Lỗi khi lưu chi phí.');
    } finally {
      setSaving(false);
    }
  };

  const handleSave = () => {
    if (items.some(i => !i.itemName.trim())) {
      toast.error('Vui lòng nhập tên cho tất cả các hạng mục chi phí.');
      return;
    }
    void doSave(false, items);
  };

  const handleNoExpenseClick = () => {
    setConfirmNoExpensePopup(true);
  };

  const confirmNoExpense = () => {
    setConfirmNoExpensePopup(false);
    const zeroed = items
      .filter(i => i.itemOrigin === 'REQUEST_ITEM' || i.itemName.trim())
      .map(i => ({ ...i, unitPrice: 0 }));
    setItems(zeroed);
    void doSave(true, zeroed);
  };

  return (
    <div className="mt-6 border border-slate-200 rounded-xl overflow-hidden bg-white">
      {/* Header nhỏ gọn */}
      <div className="flex items-center justify-between px-4 py-2.5 bg-slate-50 border-b border-slate-200">
        <div className="flex items-center gap-2">
          <div className="p-1 bg-blue-100 rounded-md text-[#004c91]"><DollarSign className="w-3.5 h-3.5" /></div>
          <span className="text-[12px] font-black text-[#004c91] uppercase tracking-wide">Ghi chú chi phí</span>
        </div>
        <div className="flex items-center gap-2 print:hidden">
          {report.noExpense ? (
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-50 text-emerald-700 border border-emerald-200">
              <CheckCircle2 className="w-3 h-3" /> Không có chi phí
            </span>
          ) : report.status === 'SAVED' ? (
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-50 text-emerald-700 border border-emerald-200">
              <CheckCircle2 className="w-3 h-3" /> Đã lưu & đồng bộ Host
            </span>
          ) : (
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold bg-amber-50 text-amber-700 border border-amber-200">
              Chưa kê khai
            </span>
          )}
        </div>
      </div>

      <div className="p-3">
        {/* Bảng compact — không có cột Phân loại (mặc định Hạng mục yêu cầu khi sang Host) */}
        <div className="overflow-x-auto rounded-lg border border-slate-200">
          <table className="w-full text-left border-collapse whitespace-nowrap text-xs">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-2.5 py-1.5 text-[10px] font-bold text-slate-500 uppercase min-w-[150px]">Tên hạng mục</th>
                <th className="px-2.5 py-1.5 text-[10px] font-bold text-slate-500 uppercase text-right w-16">SL</th>
                <th className="px-2.5 py-1.5 text-[10px] font-bold text-slate-500 uppercase w-24">Đơn vị</th>
                <th className="px-2.5 py-1.5 text-[10px] font-bold text-slate-500 uppercase text-right w-28">Đơn giá (₫)</th>
                <th className="px-2.5 py-1.5 text-[10px] font-bold text-slate-500 uppercase text-right w-28">Thành tiền</th>
                {!locked && <th className="px-1 py-1.5 w-8 print:hidden"></th>}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {items.length === 0 ? (
                <tr>
                  <td colSpan={locked ? 5 : 6} className="px-3 py-4 text-center text-[11px] text-slate-400 italic">Chưa có hạng mục chi phí nào.</td>
                </tr>
              ) : items.map((it, idx) => (
                <tr key={idx} className="hover:bg-slate-50/50">
                  <td className="px-2.5 py-1">
                    <input
                      type="text"
                      value={it.itemName}
                      onChange={(e) => handleUpdateItem(idx, 'itemName', e.target.value)}
                      disabled={locked || it.itemOrigin === 'REQUEST_ITEM'}
                      placeholder="Nhập tên..."
                      className="w-full bg-transparent border-none text-xs font-semibold text-slate-800 placeholder-slate-300 outline-none focus:ring-0 px-0 disabled:bg-transparent"
                    />
                  </td>
                  <td className="px-2.5 py-1">
                    <input
                      type="number" min="0" step="0.01"
                      value={it.quantity}
                      onChange={(e) => handleUpdateItem(idx, 'quantity', Number(e.target.value) || 0)}
                      disabled={locked}
                      className="w-full bg-transparent border-none text-xs font-bold text-slate-700 text-right outline-none focus:ring-0 px-0 disabled:bg-transparent"
                    />
                  </td>
                  <td className="px-2.5 py-1">
                    <input
                      type="text"
                      value={it.unitName || ''}
                      onChange={(e) => handleUpdateItem(idx, 'unitName', e.target.value)}
                      disabled={locked}
                      placeholder="Cái, Chuyến..."
                      className="w-full bg-transparent border-none text-xs text-slate-600 placeholder-slate-300 outline-none focus:ring-0 px-0 disabled:bg-transparent"
                    />
                  </td>
                  <td className="px-2.5 py-1">
                    <input
                      type="number" min="0"
                      value={it.unitPrice}
                      onChange={(e) => handleUpdateItem(idx, 'unitPrice', Number(e.target.value) || 0)}
                      disabled={locked}
                      className="w-full bg-transparent border-none text-xs font-bold text-slate-700 text-right outline-none focus:ring-0 px-0 disabled:bg-transparent"
                    />
                  </td>
                  <td className="px-2.5 py-1 text-right font-black text-[#004c91]">
                    {((it.quantity || 0) * (it.unitPrice || 0)).toLocaleString('vi-VN')} ₫
                  </td>
                  {!locked && (
                    <td className="px-1 py-1 text-center print:hidden">
                      {it.itemOrigin !== 'REQUEST_ITEM' && (
                        <button type="button" onClick={() => handleRemoveItem(idx)} title="Xóa"
                          className="p-1 text-slate-400 hover:text-rose-500 hover:bg-rose-50 rounded-md transition-colors outline-none cursor-pointer">
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      )}
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="bg-[#004c91]/5 border-t border-[#004c91]/10">
                <td colSpan={4} className="px-2.5 py-1.5 text-right text-[10px] font-black text-[#004c91] uppercase">Tổng chi phí</td>
                <td className="px-2.5 py-1.5 text-right text-xs font-black text-[#f37021]">
                  {report.noExpense ? 'Không có chi phí' : `${total.toLocaleString('vi-VN')} ₫`}
                </td>
                {!locked && <td className="print:hidden"></td>}
              </tr>
            </tfoot>
          </table>
        </div>

        {/* Ghi chú in được (chỉ hiện khi có nội dung lúc in) */}
        {locked && reportNote && (
          <p className="mt-2 text-[11px] text-slate-500 italic">Ghi chú: {reportNote}</p>
        )}

        {!locked && (
          <div className="mt-2.5 flex flex-wrap items-center gap-2 print:hidden">
            <button type="button" onClick={handleAddItem}
              className="inline-flex items-center gap-1 px-2.5 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 text-[11px] font-bold rounded-lg transition-colors cursor-pointer outline-none">
              <Plus className="w-3.5 h-3.5" /> Thêm hạng mục
            </button>
            <input
              type="text"
              value={reportNote}
              onChange={(e) => setReportNote(e.target.value)}
              placeholder="Ghi chú (không bắt buộc)..."
              className="flex-1 min-w-[140px] px-2.5 py-1.5 text-[11px] rounded-lg border border-slate-200 outline-none focus:border-[#004c91] transition-shadow placeholder-slate-400"
            />
            <button type="button" onClick={handleNoExpenseClick} disabled={saving}
              className="inline-flex items-center gap-1 px-2.5 py-1.5 bg-white hover:bg-slate-50 text-slate-600 text-[11px] font-bold rounded-lg border border-slate-300 transition-colors cursor-pointer outline-none disabled:opacity-50">
              <Ban className="w-3.5 h-3.5" /> Không có chi phí
            </button>
            <button type="button" onClick={handleSave} disabled={saving}
              className="inline-flex items-center gap-1 px-3 py-1.5 bg-[#004c91] hover:bg-[#003b73] text-white text-[11px] font-black rounded-lg transition-all shadow-sm cursor-pointer outline-none disabled:opacity-50">
              {saving ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Save className="w-3.5 h-3.5" />} Lưu chi phí
            </button>
          </div>
        )}
      </div>

      <ConfirmModal
        isOpen={confirmNoExpensePopup}
        onClose={() => setConfirmNoExpensePopup(false)}
        onConfirm={confirmNoExpense}
        title="Xác nhận Không có chi phí"
        message="Xác nhận đơn yêu cầu này KHÔNG phát sinh chi phí? Đơn giá các dòng sẽ được đưa về 0."
        variant="warning"
        confirmText="Xác nhận"
      />
    </div>
  );
}
