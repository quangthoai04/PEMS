import React, { useEffect, useState } from 'react';
import { Loader2, Plus, Save, Edit2, Trash2, DollarSign, AlertTriangle } from 'lucide-react';
import toast from 'react-hot-toast';
import visitExpenseService, { VisitExpenseReport, SaveExpenseReportCommand, SaveExpenseItemDto } from '../../../services/visit-expense.service';

interface Props {
  logisticsItemId: number;
}

const ORIGIN_LABELS: Record<string, string> = {
  REQUEST_ITEM: 'Hạng mục yêu cầu',
  MANUAL: 'Nhập tay',
  ADDITIONAL: 'Phát sinh',
  DAMAGE_LOSS: 'Đền bù hư hỏng/mất mát',
  OTHER: 'Khác',
};

export function LogisticsExpensePanel({ logisticsItemId }: Props) {
  const [report, setReport] = useState<VisitExpenseReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [items, setItems] = useState<SaveExpenseItemDto[]>([]);
  const [reportNote, setReportNote] = useState('');

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
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Lỗi khi tải thông tin chi phí.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchReport();
    // eslint-disable-next-line
  }, [logisticsItemId]);

  const handleAddItem = () => {
    setItems([
      ...items,
      {
        itemOrigin: 'ADDITIONAL',
        itemName: '',
        quantity: 1,
        unitName: '',
        unitPrice: 0,
        displayOrder: items.length + 1,
      }
    ]);
  };

  const handleUpdateItem = (index: number, field: keyof SaveExpenseItemDto, value: any) => {
    const newItems = [...items];
    (newItems[index] as any)[field] = value;
    setItems(newItems);
  };

  const handleRemoveItem = (index: number) => {
    setItems(items.filter((_, i) => i !== index));
  };

  const handleSave = async () => {
    if (!report) return;
    
    // validate
    if (items.some(i => !i.itemName.trim())) {
      toast.error('Vui lòng nhập tên cho tất cả các hạng mục chi phí.');
      return;
    }

    try {
      setSaving(true);
      const cmd: SaveExpenseReportCommand = {
        rowVersion: report.rowVersion,
        reportNote,
        items: items.map((it, idx) => ({ ...it, displayOrder: idx + 1 })),
      };
      await visitExpenseService.saveExpenseReport(report.expenseReportId, cmd);
      toast.success('Đã lưu thông tin chi phí thành công.');
      await fetchReport();
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Lỗi khi lưu chi phí.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="p-8 flex justify-center items-center text-slate-500">
        <Loader2 className="w-6 h-6 animate-spin mr-2 text-[#004c91]" />
        Đang tải bảng chi phí...
      </div>
    );
  }

  if (!report) {
    return null;
  }

  const isReadOnly = report.status === 'FINALIZED' || report.status === 'CANCELLED';

  const totalCalculated = items.reduce((sum, it) => sum + (it.quantity * it.unitPrice), 0);

  return (
    <div className="bg-white rounded-[2rem] shadow-[0_8px_30px_-4px_rgba(0,0,0,0.05)] border border-gray-100 flex flex-col hover:shadow-[0_12px_40px_-4px_rgba(0,76,145,0.08)] transition-shadow duration-500 overflow-hidden animate-in slide-in-from-right-8 fade-in relative duration-500 font-sans mt-8">
      <div className="flex items-center justify-between bg-blue-50/50 px-8 py-5 border-b border-blue-100">
        <div className="flex items-center gap-4">
          <div className="p-3 bg-white text-[#004c91] rounded-2xl shadow-sm border border-blue-200 shrink-0">
            <DollarSign className="w-6 h-6" />
          </div>
          <div>
            <h2 className="text-xl font-black text-[#004c91] tracking-tight uppercase">Ghi chú chi phí</h2>
            <p className="text-xs font-bold text-slate-500 mt-0.5">Bảng kê khai chi phí phục vụ hạng mục hậu cần</p>
          </div>
        </div>
        <div>
          {isReadOnly && (
            <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold bg-slate-100 text-slate-600 border border-slate-200">
              <AlertTriangle className="w-3.5 h-3.5" />
              Đã chốt (Không thể sửa)
            </span>
          )}
        </div>
      </div>

      <div className="p-8">
        <div className="overflow-x-auto rounded-xl border border-slate-200 mb-6">
          <table className="w-full text-left border-collapse whitespace-nowrap">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-4 py-3 text-xs font-bold text-slate-500 uppercase">Phân loại</th>
                <th className="px-4 py-3 text-xs font-bold text-slate-500 uppercase min-w-[200px]">Tên hạng mục</th>
                <th className="px-4 py-3 text-xs font-bold text-slate-500 uppercase text-right w-24">Số lượng</th>
                <th className="px-4 py-3 text-xs font-bold text-slate-500 uppercase w-32">Đơn vị</th>
                <th className="px-4 py-3 text-xs font-bold text-slate-500 uppercase text-right w-40">Đơn giá (₫)</th>
                <th className="px-4 py-3 text-xs font-bold text-slate-500 uppercase text-right w-40">Thành tiền</th>
                {!isReadOnly && <th className="px-4 py-3 w-12"></th>}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {items.length === 0 ? (
                <tr>
                  <td colSpan={isReadOnly ? 6 : 7} className="px-4 py-8 text-center text-sm text-slate-400 italic">
                    Chưa có hạng mục chi phí nào.
                  </td>
                </tr>
              ) : (
                items.map((it, idx) => (
                  <tr key={idx} className="hover:bg-slate-50/50 transition-colors">
                    <td className="px-4 py-2">
                      <select
                        value={it.itemOrigin}
                        onChange={(e) => handleUpdateItem(idx, 'itemOrigin', e.target.value)}
                        disabled={isReadOnly || it.itemOrigin === 'REQUEST_ITEM'}
                        className="w-full bg-transparent border-none text-sm font-medium text-slate-700 outline-none focus:ring-0 disabled:opacity-70 disabled:bg-transparent"
                      >
                        {Object.entries(ORIGIN_LABELS).map(([k, v]) => (
                          <option key={k} value={k}>{v}</option>
                        ))}
                      </select>
                    </td>
                    <td className="px-4 py-2">
                      <input
                        type="text"
                        value={it.itemName}
                        onChange={(e) => handleUpdateItem(idx, 'itemName', e.target.value)}
                        disabled={isReadOnly || it.itemOrigin === 'REQUEST_ITEM'}
                        placeholder="Nhập tên..."
                        className="w-full bg-transparent border-none text-sm font-semibold text-slate-800 placeholder-slate-300 outline-none focus:ring-0 px-0 disabled:bg-transparent"
                      />
                    </td>
                    <td className="px-4 py-2">
                      <input
                        type="number"
                        min="0"
                        step="0.01"
                        value={it.quantity}
                        onChange={(e) => handleUpdateItem(idx, 'quantity', Number(e.target.value) || 0)}
                        disabled={isReadOnly}
                        className="w-full bg-transparent border-none text-sm font-bold text-slate-700 text-right outline-none focus:ring-0 px-0 disabled:bg-transparent"
                      />
                    </td>
                    <td className="px-4 py-2">
                      <input
                        type="text"
                        value={it.unitName || ''}
                        onChange={(e) => handleUpdateItem(idx, 'unitName', e.target.value)}
                        disabled={isReadOnly}
                        placeholder="VD: Cái, Chuyến..."
                        className="w-full bg-transparent border-none text-sm text-slate-600 placeholder-slate-300 outline-none focus:ring-0 px-0 disabled:bg-transparent"
                      />
                    </td>
                    <td className="px-4 py-2">
                      <input
                        type="number"
                        min="0"
                        value={it.unitPrice}
                        onChange={(e) => handleUpdateItem(idx, 'unitPrice', Number(e.target.value) || 0)}
                        disabled={isReadOnly}
                        className="w-full bg-transparent border-none text-sm font-bold text-slate-700 text-right outline-none focus:ring-0 px-0 disabled:bg-transparent"
                      />
                    </td>
                    <td className="px-4 py-2 text-right font-black text-[#004c91]">
                      {((it.quantity || 0) * (it.unitPrice || 0)).toLocaleString('vi-VN')} ₫
                    </td>
                    {!isReadOnly && (
                      <td className="px-4 py-2 text-center">
                        {it.itemOrigin !== 'REQUEST_ITEM' && (
                          <button
                            type="button"
                            onClick={() => handleRemoveItem(idx)}
                            className="p-1.5 text-slate-400 hover:text-rose-500 hover:bg-rose-50 rounded-lg transition-colors outline-none cursor-pointer"
                            title="Xóa"
                          >
                            <Trash2 className="w-4 h-4" />
                          </button>
                        )}
                      </td>
                    )}
                  </tr>
                ))
              )}
            </tbody>
            <tfoot>
              <tr className="bg-[#004c91]/5 border-t border-[#004c91]/10">
                <td colSpan={5} className="px-4 py-3 text-right text-sm font-black text-[#004c91] uppercase">Tổng chi phí dự kiến</td>
                <td className="px-4 py-3 text-right text-base font-black text-[#f37021]">
                  {totalCalculated.toLocaleString('vi-VN')} ₫
                </td>
                {!isReadOnly && <td></td>}
              </tr>
            </tfoot>
          </table>
        </div>

        {!isReadOnly && (
          <div className="flex justify-between items-start gap-6">
            <button
              type="button"
              onClick={handleAddItem}
              className="inline-flex items-center gap-1.5 px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-sm font-bold rounded-xl transition-colors cursor-pointer outline-none shrink-0"
            >
              <Plus className="w-4 h-4" /> Thêm hạng mục
            </button>
            <div className="flex-1 flex flex-col items-end gap-3">
              <textarea
                value={reportNote}
                onChange={(e) => setReportNote(e.target.value)}
                placeholder="Ghi chú tổng thể cho báo cáo chi phí này (không bắt buộc)..."
                rows={2}
                className="w-full max-w-lg px-4 py-3 text-sm rounded-xl border border-slate-200 outline-none focus:border-[#004c91] focus:ring-1 focus:ring-blue-100 transition-shadow resize-none placeholder-slate-400"
              />
              <button
                type="button"
                onClick={handleSave}
                disabled={saving}
                className="inline-flex items-center gap-2 px-6 py-2.5 bg-[#004c91] hover:bg-[#003b73] text-white text-sm font-black rounded-xl transition-all shadow-md shadow-[#004c91]/20 cursor-pointer outline-none disabled:opacity-50"
              >
                {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                Lưu bảng chi phí
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
