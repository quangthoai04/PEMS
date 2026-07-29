import React, { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { FileText, Download, X, Loader2, PenLine, Plus, Trash2 } from 'lucide-react';
import { departmentReceptionTasksApi } from '../../../features/department-reception-tasks/api/departmentReceptionTasksApi';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';
import { isVehicleHandover, buildDefaultVehicleChecklist, buildDefaultGenericChecklist, vehicleItemQuantity, type VehicleChecklistRow } from '../../../features/department-reception-tasks/constants/vehicleHandover';
import { LogisticsExpensePanel } from './LogisticsExpensePanel';
import toast from 'react-hot-toast';

function fmtDateTime(value?: string | null): string {
  if (!value) return '—';
  const [d, t] = value.replace(' ', 'T').split('T');
  if (!d) return value;
  const [y, m, day] = d.split('-');
  const hm = (t || '').slice(0, 5);
  if (!y || !m || !day) return value;
  return hm ? `${hm} ${day}/${m}/${y}` : `${day}/${m}/${y}`;
}

interface TaskHandoverModalProps {
  isOpen?: boolean;
  onClose?: () => void;
  detailData: any; // RequestDetailDto
  onSuccess?: () => void;
  inline?: boolean;
  readOnly?: boolean;
}

export function TaskHandoverModal({ isOpen, onClose, detailData, onSuccess, inline, readOnly }: TaskHandoverModalProps) {
  const [busy, setBusy] = useState(false);
  const [borrowNote, setBorrowNote] = useState('');
  const [returnNote, setReturnNote] = useState('');
  const [checklistRows, setChecklistRows] = useState<VehicleChecklistRow[]>(() => {
    if (detailData?.ChecklistJson) {
      try {
        const parsed = JSON.parse(detailData.ChecklistJson);
        if (Array.isArray(parsed) && parsed.length > 0) return parsed;
      } catch { /* rơi về mặc định nếu JSON hỏng */ }
    }
    // Xe điện: checklist mẫu giấy có sẵn danh mục. Hạng mục khác: chỉ 1 dòng khởi điểm lấy tên/số
    // lượng từ chính đơn yêu cầu — cùng logic bảng, không list sẵn.
    return isVehicleHandover(detailData?.ItemType)
      ? buildDefaultVehicleChecklist(detailData?.Quantity)
      : buildDefaultGenericChecklist(detailData?.Title, detailData?.Quantity);
  });

  // Chỉ seed 1 lần lúc mount (useState initializer) — nếu Quantity "chốt" đổi sau đó (vd Host vừa
  // chấp nhận đề xuất số lượng mới trong khi biên bản này đã mở), cột Số Lượng của dòng mặc định
  // tự sinh (chưa có checklistJson đã lưu) bị kẹt ở giá trị cũ. Đồng bộ lại riêng cột qty, không
  // đụng Giao/Nhận người dùng có thể đã gõ.
  useEffect(() => {
    if (detailData?.ChecklistJson) return; // đã có checklist lưu sẵn, không tự ý sửa
    if (isVehicleHandover(detailData?.ItemType)) {
      setChecklistRows((rows) => rows.map((r) => (r.name ? { ...r, qty: vehicleItemQuantity(r.name, detailData?.Quantity) } : r)));
      return;
    }
    const qty = detailData?.Quantity ? String(detailData.Quantity) : '';
    setChecklistRows((rows) => rows.map((r) => (r.name === (detailData?.Title || '') ? { ...r, qty } : r)));
  }, [detailData?.Quantity, detailData?.Title, detailData?.ChecklistJson, detailData?.ItemType]);

  if ((!isOpen && !inline) || !detailData) return null;

  const bg1 = detailData.BorrowProviderSignature?.Name ? `${detailData.BorrowProviderSignature.Name}` : null;
  const bg2 = detailData.BorrowBorrowerSignature?.Name ? `${detailData.BorrowBorrowerSignature.Name}` : null;
  const nt1 = detailData.ReturnBorrowerSignature?.Name ? `${detailData.ReturnBorrowerSignature.Name}` : null;
  const nt2 = detailData.ReturnProviderSignature?.Name ? `${detailData.ReturnProviderSignature.Name}` : null;

  const canSignBG1 = !bg1 && !readOnly; // Department is PROVIDER
  const isBorrowDone = bg1 && bg2;
  const canSignNT2 = isBorrowDone && nt1 && !nt2 && !readOnly; // Department is PROVIDER

  const isReturnStarted = nt1 || nt2;
  // Cột "bàn giao" do phòng ban (Dept) điền TRƯỚC khi ký "Ký Giao", khoá lại ngay sau đó. Cột
  // "Tình trạng nghiệm thu" (cuối bảng) là việc của Host — chỉ Host điền khi ký nghiệm thu (NT1)
  // trong LogisticsHandoverSection; Dept không sửa cột này ở modal này, kể cả lúc ký NT2.
  const canEditChecklist = !bg1 && !readOnly;

  // parse date for top section
  let handoverTime = "....";
  let handoverDate = "..../..../20.....";
  if (detailData.BorrowProviderSignature?.SignedAt) {
    const [d, t] = detailData.BorrowProviderSignature.SignedAt.replace(' ', 'T').split('T');
    if (d && t) {
      const hm = t.slice(0, 5);
      const [y, m, day] = d.split('-');
      handoverTime = `${hm}`;
      handoverDate = `${day}/${m}/${y}`;
    }
  }

  const hostName = detailData.BorrowBorrowerSignature?.Name || detailData.ReturnBorrowerSignature?.Name || detailData.SenderName || 'Đại diện Host đón tiếp';
  const providerName = detailData.BorrowProviderSignature?.Name || detailData.ReturnProviderSignature?.Name || detailData.AssigneeName || 'Đại diện Phòng ban';

  // Đơn mượn xe (TRANSPORT): biên bản có checklist cố định theo mẫu giấy;
  // đơn yêu cầu chung chung giữ nguyên biên bản hiện tại.
  const isVehicle = isVehicleHandover(detailData.ItemType);
  const vehicleReturnTime = detailData.UsageEndTime && detailData.UsageDate
    ? `${detailData.UsageEndTime} ngày ${detailData.UsageDate}`
    : '............................................';

  const [savingDocType, setSavingDocType] = useState<'BORROW' | 'RETURN' | null>(null);
  const handleSaveDocument = async (type: 'BORROW' | 'RETURN') => {
    if (!detailData.VisitInstanceId || !detailData.LogisticsItemId) return;
    setSavingDocType(type);
    try {
      await delegationsApi.saveLogisticsHandoverDocument(detailData.VisitInstanceId, detailData.LogisticsItemId, type);
      toast.success('Đã lưu biên bản vào hệ thống.');
    } catch (e: any) {
      toast.error(e.response?.data?.message || 'Không thể lưu biên bản vào hệ thống.');
    } finally {
      setSavingDocType(null);
    }
  };

  const handleSign = async (type: 'BORROW' | 'RETURN') => {
    setBusy(true);
    try {
      const note = (type === 'BORROW' ? borrowNote : returnNote).trim();
      // Checklist (cột "bàn giao") chỉ Dept gửi lúc ký BORROW — cột "Tình trạng nghiệm thu" là việc
      // của Host, ký ở LogisticsHandoverSection, không gửi lại từ đây để tránh đè dữ liệu Host vừa điền.
      const checklistJson = type === 'BORROW' ? JSON.stringify(checklistRows) : undefined;
      await departmentReceptionTasksApi.signHandover(detailData.LogisticsItemId, type, 'PROVIDER', note || undefined, checklistJson);
      toast.success(`Đã ký ${type === 'BORROW' ? 'bàn giao' : 'nhận lại'}`);
      onSuccess?.();
    } catch (e: any) {
      toast.error(e.response?.data?.message || 'Không thể ký biên bản. Vui lòng thử lại.');
    } finally {
      setBusy(false);
    }
  };

  const content = (
    <>
      <style type="text/css" media="print">
        {`
          body * {
            visibility: hidden;
          }
          #task-handover-modal, #task-handover-modal * {
            visibility: visible;
          }
          #task-handover-modal {
            position: absolute;
            left: 0;
            top: 0;
            width: 100%;
            margin: 0;
            padding: 0;
          }
          /* flex/overflow-hidden trên khung modal chặn nội dung tràn nhiều trang khi in —
             ép về block + overflow visible cho toàn bộ cây con để trình duyệt tự chia trang. */
          #task-handover-modal, #task-handover-modal * {
            overflow: visible !important;
            max-height: none !important;
          }
          #task-handover-modal > div {
            display: block !important;
          }
        `}
      </style>
      <div id="task-handover-modal" className={inline ? "bg-white rounded-2xl p-6 md:p-10 font-sans w-full space-y-6 relative overflow-hidden print:max-w-none mt-6 border border-slate-200" : "fixed inset-0 z-[80] flex items-center justify-center p-4 print:static print:inset-auto print:p-0"}>
        {!inline && <div className="absolute inset-0 bg-black/60 backdrop-blur-sm print:hidden" onClick={onClose} />}
        <div className={inline ? "w-full" : "relative w-full max-w-4xl max-h-[90vh] flex flex-col rounded-2xl bg-white shadow-2xl overflow-hidden font-sans border border-slate-200 print:max-w-none print:max-h-none print:shadow-none print:border-none print:rounded-none"}>
          
          {/* Header/Download Button */}
          {inline ? (
            <button 
              type="button"
              onClick={() => window.print()}
              className="absolute top-6 right-6 z-20 flex items-center gap-1.5 text-xs font-bold text-[#f37021] bg-orange-50 hover:bg-orange-100 px-3 py-1.5 rounded-lg transition-colors outline-none print:hidden"
            >
              <Download className="w-4 h-4" /> Tải PDF
            </button>
          ) : (
            <div className="bg-[#f37021] text-white px-6 py-4 flex items-center justify-between shrink-0 print:hidden">
              <h3 className="font-bold text-sm uppercase tracking-wide flex items-center gap-2">
                <FileText className="w-5 h-5 opacity-80" />
                BIÊN BẢN BÀN GIAO VÀ NGHIỆM THU
              </h3>
              <div className="flex items-center gap-4">
                <button 
                  type="button" 
                  onClick={() => window.print()}
                  className="flex items-center gap-1.5 text-xs font-bold bg-white/20 hover:bg-white/30 px-3 py-1.5 rounded-lg transition-colors outline-none"
                >
                  <Download className="w-4 h-4" /> Tải PDF
                </button>
                <button type="button" onClick={onClose} disabled={busy}
                  className="text-white/70 hover:text-white outline-none transition-colors">
                  <X className="w-5 h-5" />
                </button>
              </div>
            </div>
          )}

          {/* Modal body (scrollable) */}
          <div className={inline ? "bg-white flex-1 text-slate-900" : "p-6 md:p-12 overflow-y-auto bg-white flex-1 text-slate-900 shadow-[inset_0_0_20px_rgba(0,0,0,0.02)] print:overflow-visible print:shadow-none"}>
            
            <div className="text-center space-y-1 mb-8">
              <h4 className="text-xl sm:text-2xl font-bold uppercase tracking-wide">
                BIÊN BẢN BÀN GIAO VÀ NGHIỆM THU
              </h4>
              <p className="text-lg font-bold uppercase">
                TÀI SẢN / TRANG THIẾT BỊ
              </p>
            </div>

            <div className="space-y-3 text-[15px] leading-relaxed mb-6">
              <p>
                Hôm nay, lúc: <b>{handoverTime}</b> giờ, ngày <b>{handoverDate}</b>, tại: <b>Trường Đại học FPT Hòa Lạc</b>.
              </p>
              <p>Chúng tôi gồm:</p>
              <div className="space-y-2 pl-4">
                <div className="flex flex-wrap gap-x-8 gap-y-2">
                  <p className="flex-1 min-w-[250px]">Người bàn giao: <b>{providerName}</b></p>
                  <p className="flex-1 min-w-[200px]">Bộ phận: <b>Phòng ban hỗ trợ</b></p>
                </div>
                <div className="flex flex-wrap gap-x-8 gap-y-2">
                  <p className="flex-1 min-w-[250px]">Người nhận bàn giao: <b>{hostName}</b></p>
                  <p className="flex-1 min-w-[200px]">Bộ phận: <b>Host (Tiếp đón)</b></p>
                </div>
                {detailData.Description && (
                  <p>Mô tả: <b>{detailData.Description}</b></p>
                )}
                <p>Lý do bàn giao: <b>Phục vụ công tác đón tiếp đoàn khách</b></p>
                <p>Đoàn khách: <b>{detailData.DelegationName}</b></p>
                {isVehicle ? (
                  <p>Thời gian hẹn trả xe: <b>{vehicleReturnTime}</b></p>
                ) : (
                  <p>Thời gian hẹn trả tài sản: <b>Sau khi kết thúc chuyến thăm</b></p>
                )}
              </div>
            </div>

            <p className="font-bold text-[15px] mb-2">
              {isVehicle
                ? `Cùng bàn giao xe ${detailData.Quantity || 1} ô tô điện với tình trạng sau:`
                : `Cùng bàn giao ${detailData.Quantity || 1} ${detailData.Title || 'hạng mục'} với tình trạng sau:`}
            </p>
            <div className="overflow-x-auto mb-6">
              <table className="w-full border-collapse border border-slate-500 text-[14px]">
                <thead>
                  <tr className="bg-slate-50">
                    <th className="border border-slate-500 p-2 text-center w-14">STT</th>
                    <th className="border border-slate-500 p-2 text-center min-w-[160px]">Nội dung</th>
                    <th className="border border-slate-500 p-2 text-center w-24">Số Lượng</th>
                    <th className="border border-slate-500 p-2 text-center min-w-[140px]">Tình Trạng bàn giao</th>
                    <th className="border border-slate-500 p-2 text-center min-w-[140px]">Tình trạng nghiệm thu</th>
                  </tr>
                </thead>
                <tbody>
                  {checklistRows.map((row, i) => (
                    <tr key={i}>
                      <td className="border border-slate-500 p-1 text-center">
                        <div className="flex items-center justify-center gap-1">
                          <span>{i + 1}</span>
                          {canEditChecklist && (
                            <button type="button" onClick={() => setChecklistRows(prev => prev.filter((_, idx) => idx !== i))}
                              className="text-slate-400 hover:text-rose-500 transition-colors print:hidden cursor-pointer" title="Xoá dòng">
                              <Trash2 className="w-3 h-3" />
                            </button>
                          )}
                        </div>
                      </td>
                      <td className="border border-slate-500 p-0 min-w-[160px]">
                        <input type="text" className="w-full min-h-[36px] bg-transparent outline-none px-2" value={row.name} disabled={!canEditChecklist}
                          onChange={e => setChecklistRows(prev => prev.map((r, idx) => idx === i ? { ...r, name: e.target.value } : r))} />
                      </td>
                      <td className="border border-slate-500 p-0">
                        <input type="text" placeholder="Nhập..." className="w-full min-h-[36px] bg-transparent outline-none text-center px-1 placeholder:text-slate-300" value={row.qty} disabled={!canEditChecklist}
                          onChange={e => setChecklistRows(prev => prev.map((r, idx) => idx === i ? { ...r, qty: e.target.value } : r))} />
                      </td>
                      <td className="border border-slate-500 p-0 min-w-[140px]">
                        <input type="text" placeholder="Nhập..." className="w-full min-h-[36px] bg-transparent outline-none px-2 placeholder:text-slate-300" value={row.giao} disabled={!canEditChecklist}
                          onChange={e => setChecklistRows(prev => prev.map((r, idx) => idx === i ? { ...r, giao: e.target.value } : r))} />
                      </td>
                      <td className="border border-slate-500 p-0 min-w-[140px] bg-slate-50">
                        <input type="text" placeholder="Host điền khi nghiệm thu" className="w-full min-h-[36px] bg-transparent outline-none px-2 placeholder:text-slate-300 text-slate-600" value={row.nhan} disabled
                          readOnly />
                      </td>
                    </tr>
                  ))}
                  {canEditChecklist && (
                    <tr className="print:hidden">
                      <td colSpan={5} className="border border-slate-500 p-0">
                        <button type="button" onClick={() => setChecklistRows(prev => [...prev, { name: '', qty: '', giao: '', nhan: '' }])} className="w-full py-2 flex items-center justify-center gap-1 text-sm font-bold text-[#004c91] bg-blue-50/50 hover:bg-blue-100/50 transition-colors">
                          <Plus className="w-4 h-4" /> Thêm dòng
                        </button>
                      </td>
                    </tr>
                  )}
                  {/* Hàng ghi chú tổng — gộp ý kiến Bên Giao/Bên Nhận mỗi giai đoạn, cùng format
                      "+ Nhãn: nội dung" (MergeNote), đặt cuối bảng. */}
                  <tr>
                    <td className="border border-slate-500 p-2 text-center font-semibold">{checklistRows.length + 1}</td>
                    <td className="border border-slate-500 p-2 font-semibold">Ghi chú</td>
                    <td className="border border-slate-500 p-2"></td>
                    <td className="border border-slate-500 p-2 whitespace-pre-line align-top">{detailData.BorrowNote || ''}</td>
                    <td className="border border-slate-500 p-2 whitespace-pre-line align-top">{detailData.ReturnNote || ''}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div className="space-y-1 text-[14px] mb-8">
              <p className="font-bold">{isVehicle ? 'Quy định khi sử dụng xe ô tô điện:' : 'Quy định khi sử dụng tài sản:'}</p>
              {isVehicle ? (
                <ul className="list-disc pl-8 space-y-1">
                  <li>Người mượn xe phải tuân thủ đúng mục đích sử dụng, không tự ý chuyển giao xe cho người khác.</li>
                  <li>Khi có vấn đề xảy ra (xe bị hỏng hoặc không nguyên hiện trạng ban đầu), <b>người mượn xe</b> sẽ phải chịu hoàn toàn trách nhiệm chi trả chi phí sửa chữa/đền bù.</li>
                  <li>An toàn trong quá trình sử dụng xe sẽ do <b>người mượn xe</b> chịu hoàn toàn trách nhiệm.</li>
                  <li>Ghi chú khác: ....................................................................................................................</li>
                </ul>
              ) : (
                <ul className="list-disc pl-8 space-y-1">
                  <li>Người mượn tài sản phải tuân thủ đúng mục đích sử dụng, không tự ý chuyển giao cho người khác.</li>
                  <li>Khi có vấn đề xảy ra (bị hỏng hoặc không nguyên hiện trạng ban đầu), <b>người mượn tài sản</b> sẽ phải chịu hoàn toàn trách nhiệm chi trả chi phí sửa chữa/đền bù.</li>
                  <li>An toàn trong quá trình sử dụng tài sản sẽ do <b>người mượn tài sản</b> chịu hoàn toàn trách nhiệm.</li>
                  <li>Ghi chú khác: ....................................................................................................................</li>
                </ul>
              )}
              <p className="mt-4">
                Tôi là <b>{hostName}</b>, đã đọc hiểu và cam kết thực hiện đúng quy định sử dụng.
              </p>
            </div>

            {/* 4 NÚT KÝ */}
            <div className="space-y-8">
              {/* KHỐI BÀN GIAO */}
              <div className="relative my-7">
                <div className="absolute inset-0 flex items-center" aria-hidden="true"><div className="w-full border-t border-slate-300"></div></div>
                <div className="relative flex justify-center"><span className="bg-white px-4 py-1.5 text-[10px] font-black text-slate-900 uppercase tracking-widest border border-slate-200 rounded-full shadow-sm">BÀN GIAO</span></div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-5 bg-slate-50/70 p-4 rounded-2xl border border-slate-200">
                {/* Bên Giao (Department) */}
                <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col justify-between gap-4">
                  <div>
                    <label className="block text-[10px] font-black text-[#004c91] uppercase tracking-wider mb-2">Ghi chú Bên Giao</label>
                    {canSignBG1 ? (
                      <textarea rows={2} value={borrowNote} onChange={e => setBorrowNote(e.target.value)} disabled={busy} className="w-full text-xs p-2.5 border border-slate-250 rounded-xl focus:border-[#004c91] focus:ring-1 focus:ring-blue-200 outline-none resize-none bg-slate-50/30" placeholder="Ghi nhận tình trạng trước khi giao..." />
                    ) : (
                      <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-600 italic">
                        {detailData.BorrowNote || 'Tốt. Đã bàn giao.'}
                      </div>
                    )}
                  </div>
                  <div className={`border-2 rounded-xl p-3 relative ${bg1 ? 'border-emerald-500 bg-emerald-50/20' : canSignBG1 ? 'border-dashed border-[#004c91]/50 bg-blue-50/20' : 'border-dashed border-slate-250 bg-slate-50'}`}>
                    {bg1 ? (
                      <div className="flex items-center gap-2.5">
                        <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs shrink-0 shadow-sm border border-emerald-100">✓</div>
                        <div>
                          <span className="text-[9px] font-black uppercase text-emerald-800 block leading-none mb-0.5">ĐÃ KÝ DUYỆT BÀN GIAO</span>
                          <p className="text-[11px] font-extrabold text-slate-800 leading-snug truncate">{bg1}</p>
                          <p className="text-[9px] text-slate-500">{fmtDateTime(detailData.BorrowProviderSignature?.SignedAt)}</p>
                        </div>
                      </div>
                    ) : (
                      <div className="flex flex-row items-center justify-between gap-3 w-full">
                        <div className="flex items-center gap-2">
                          <FileText className={`w-4 h-4 shrink-0 ${canSignBG1 ? 'text-[#004c91]' : 'text-slate-400'}`} />
                          <div className="text-left">
                            <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Giao</span>
                            <span className="text-[9px] text-slate-450">{canSignBG1 ? 'Nhấp để xác nhận' : (bg1 ? 'Đã ký' : (readOnly ? 'Chỉ xem (Staff thực hiện)' : 'Chưa thể ký'))}</span>
                          </div>
                        </div>
                        {canSignBG1 && (
                          <button type="button" onClick={() => handleSign('BORROW')} disabled={busy} className="py-2 px-3 bg-blue-50 hover:bg-blue-100 text-[#004c91] font-extrabold text-[11px] rounded-xl border border-blue-200 transition-all flex items-center gap-1.5 cursor-pointer shadow-sm print:hidden">
                            {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <FileText className="w-3.5 h-3.5" />} Ký Giao
                          </button>
                        )}
                      </div>
                    )}
                  </div>
                </div>

                {/* Bên Nhận (Host) */}
                <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col justify-between gap-4">
                  <div>
                    <label className="block text-[10px] font-black text-[#f37021] uppercase tracking-wider mb-2">Ghi chú Bên Nhận</label>
                    <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-600 italic">
                      {bg2 ? 'Đã xác nhận nhận tài sản.' : 'Chưa ký nhận.'}
                    </div>
                  </div>
                  <div className={`border-2 rounded-xl p-3 relative ${bg2 ? 'border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-250 bg-slate-50'}`}>
                    {bg2 ? (
                      <div className="flex items-center gap-2.5">
                        <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs shrink-0 shadow-sm border border-emerald-100">✓</div>
                        <div>
                          <span className="text-[9px] font-black uppercase text-emerald-800 block leading-none mb-0.5">ĐÃ KÝ XÁC NHẬN</span>
                          <p className="text-[11px] font-extrabold text-slate-800 leading-snug truncate">{bg2}</p>
                          <p className="text-[9px] text-slate-500">{fmtDateTime(detailData.BorrowBorrowerSignature?.SignedAt)}</p>
                        </div>
                      </div>
                    ) : (
                      <div className="flex flex-row items-center justify-between gap-3 w-full opacity-60">
                        <div className="flex items-center gap-2">
                          <FileText className="w-4 h-4 text-slate-400 shrink-0" />
                          <div className="text-left">
                            <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Nhận</span>
                            <span className="text-[9px] text-slate-450">Chờ Host ký</span>
                          </div>
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              </div>

              {/* KHỐI NGHIỆM THU */}
              <div className={`transition-opacity pt-4 ${!isBorrowDone ? 'opacity-40 pointer-events-none' : ''}`}>
                <div className="relative my-7">
                  <div className="absolute inset-0 flex items-center" aria-hidden="true"><div className="w-full border-t border-slate-300"></div></div>
                  <div className="relative flex justify-center"><span className="bg-white px-4 py-1.5 text-[10px] font-black text-slate-900 uppercase tracking-widest border border-slate-200 rounded-full shadow-sm">NGHIỆM THU</span></div>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-5 bg-[#f8fbfe] p-4 rounded-2xl border border-blue-200/50">
                  {/* Bên Giao (Host trả) */}
                  <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col justify-between gap-4">
                    <div>
                      <label className="block text-[10px] font-black text-[#004c91] uppercase tracking-wider mb-2">Ghi chú Nghiệm thu (Bên Giao)</label>
                      <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-600 italic">
                        {detailData.ReturnNote || (nt1 ? 'Đã bàn giao trả tài sản.' : 'Chưa bàn giao trả')}
                      </div>
                    </div>
                    <div className={`border-2 rounded-xl p-3 relative ${nt1 ? 'border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-250 bg-slate-50'}`}>
                      {nt1 ? (
                        <div className="flex items-center gap-2.5">
                          <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs shrink-0 shadow-sm border border-emerald-100">✓</div>
                          <div>
                            <span className="text-[9px] font-black uppercase text-emerald-800 block leading-none mb-0.5">ĐÃ KÝ DUYỆT NGHIỆM THU</span>
                            <p className="text-[11px] font-extrabold text-slate-800 leading-snug truncate">{nt1}</p>
                            <p className="text-[9px] text-slate-500">{fmtDateTime(detailData.ReturnBorrowerSignature?.SignedAt)}</p>
                          </div>
                        </div>
                      ) : (
                        <div className="flex flex-row items-center justify-between gap-3 w-full opacity-60">
                          <div className="flex items-center gap-2">
                            <FileText className="w-4 h-4 text-slate-400 shrink-0" />
                            <div className="text-left">
                              <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Giao (Trả)</span>
                              <span className="text-[9px] text-slate-450">Chờ Host ký trả</span>
                            </div>
                          </div>
                        </div>
                      )}
                    </div>
                  </div>

                  {/* Bên Nhận (Department) */}
                  <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col justify-between gap-4">
                    <div>
                      <label className="block text-[10px] font-black text-[#f37021] uppercase tracking-wider mb-2">Ghi chú Nghiệm thu (Bên Nhận)</label>
                      {canSignNT2 ? (
                        <textarea rows={2} value={returnNote} onChange={e => setReturnNote(e.target.value)} disabled={busy} className="w-full text-xs p-2.5 border border-slate-250 rounded-xl focus:border-[#f37021] focus:ring-1 focus:ring-orange-200 outline-none resize-none bg-slate-50/30" placeholder="Ghi nhận hiện trạng lúc nhận lại..." />
                      ) : (
                        <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-600 italic">
                          {nt2 ? 'Đã nghiệm thu nhận lại tài sản.' : 'Chờ Host ký trả trước.'}
                        </div>
                      )}
                    </div>
                    <div className={`border-2 rounded-xl p-3 relative ${nt2 ? 'border-emerald-500 bg-emerald-50/20' : canSignNT2 ? 'border-dashed border-[#f37021]/50 bg-orange-50/20' : 'border-dashed border-slate-250 bg-slate-50'}`}>
                      {nt2 ? (
                        <div className="flex items-center gap-2.5">
                          <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs shrink-0 shadow-sm border border-emerald-100">✓</div>
                          <div>
                            <span className="text-[9px] font-black uppercase text-emerald-800 block leading-none mb-0.5">ĐÃ KÝ NGHIỆM THU LẠI</span>
                            <p className="text-[11px] font-extrabold text-slate-800 leading-snug truncate">{nt2}</p>
                            <p className="text-[9px] text-slate-500">{fmtDateTime(detailData.ReturnProviderSignature?.SignedAt)}</p>
                          </div>
                        </div>
                      ) : (
                        <div className="flex flex-row items-center justify-between gap-3 w-full">
                          <div className="flex items-center gap-2">
                            <FileText className={`w-4 h-4 shrink-0 ${canSignNT2 ? 'text-[#f37021]' : 'text-slate-400'}`} />
                            <div className="text-left">
                              <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Nhận (Lại)</span>
                              <span className="text-[9px] text-slate-450">{canSignNT2 ? 'Nhấp để xác nhận' : (nt2 ? 'Đã ký' : (readOnly && isBorrowDone && nt1 ? 'Chỉ xem (Staff thực hiện)' : 'Chờ Host trả'))}</span>
                            </div>
                          </div>
                          {canSignNT2 && (
                            <button type="button" onClick={() => handleSign('RETURN')} disabled={busy} className="py-2 px-3 bg-orange-50 hover:bg-orange-100 text-[#f37021] font-extrabold text-[11px] rounded-xl border border-orange-200 transition-all flex items-center gap-1.5 cursor-pointer shadow-sm print:hidden">
                              {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <FileText className="w-3.5 h-3.5" />} Ký Nhận
                            </button>
                          )}
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              </div>

              {/* "Lưu vào hệ thống" — sinh PDF thật phía backend (khác nút Tải PDF ở trên, chỉ in
                  trang hiện tại) và lưu vào Quản lý tài liệu (loại Hậu cần). Chỉ bật khi đủ 2 chữ ký. */}
              <div className="flex flex-wrap gap-3 print:hidden">
                {!readOnly && isBorrowDone && (
                  <button
                    type="button"
                    disabled={savingDocType !== null}
                    onClick={() => handleSaveDocument('BORROW')}
                    className="flex items-center gap-1.5 px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-bold rounded-lg transition-colors cursor-pointer disabled:opacity-50"
                  >
                    {savingDocType === 'BORROW' ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <FileText className="w-3.5 h-3.5" />}
                    Lưu biên bản bàn giao vào hệ thống
                  </button>
                )}
                {!readOnly && nt1 && nt2 && (
                  <button
                    type="button"
                    disabled={savingDocType !== null}
                    onClick={() => handleSaveDocument('RETURN')}
                    className="flex items-center gap-1.5 px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-bold rounded-lg transition-colors cursor-pointer disabled:opacity-50"
                  >
                    {savingDocType === 'RETURN' ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <FileText className="w-3.5 h-3.5" />}
                    Lưu biên bản nghiệm thu vào hệ thống
                  </button>
                )}
              </div>

              {/* Ghi chú chi phí — hiện khi biên bản đã ký nghiệm thu đủ 2 bên; nằm trong vùng in
                  nên Tải PDF biên bản sẽ kèm bảng chi phí. Người xem không thuộc phòng ban → panel tự ẩn. */}
              {isBorrowDone && nt1 && nt2 && detailData.LogisticsItemId && (
                <LogisticsExpensePanel logisticsItemId={detailData.LogisticsItemId} readOnly={readOnly} />
              )}
            </div>
          </div>
        </div>
      </div>
    </>
  );

  if (inline) return content;
  return createPortal(content, document.body);
}
