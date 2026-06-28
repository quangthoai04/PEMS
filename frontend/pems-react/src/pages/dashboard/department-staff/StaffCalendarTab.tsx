/**
 * StaffCalendarTab – Tab lịch của Department Staff
 * Chỉ hiển thị đơn/thư được Leader phân công cho staff đó.
 * Không có bộ lọc "Loại lịch" (lọc tôi / phòng ban).
 */
import React, { useState, useMemo, useEffect } from 'react';
import {
  ChevronLeft, ChevronRight, ChevronDown, X, Clock, MapPin, User, AlertCircle, FileText,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { SubmittedVisitRequestDetailModal } from '../../../components/modals/SubmittedVisitRequestDetailModal';
import { departmentReceptionTasksApi } from '../../../features/department-reception-tasks/api/departmentReceptionTasksApi';
import type { CalendarItem } from './useDeptStaffData';
import { StaffLeaderTaskModal } from './StaffLeaderTaskModal';

interface Props {
  year: number;
  onYearChange: (year: number) => void;
  calendarItems: CalendarItem[];
  calendarLoading: boolean;
  onRefresh: () => void;
}

const WEEKDAYS = ['Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy', 'Chủ Nhật'];
const MONTHS = ['Tháng 1','Tháng 2','Tháng 3','Tháng 4','Tháng 5','Tháng 6','Tháng 7','Tháng 8','Tháng 9','Tháng 10','Tháng 11','Tháng 12'];

function todayStr() {
  const t = new Date();
  return `${t.getFullYear()}-${String(t.getMonth()+1).padStart(2,'0')}-${String(t.getDate()).padStart(2,'0')}`;
}

function buildDaysGrid(year: number, month: number) {
  const firstRaw = new Date(year, month, 1).getDay();
  const firstIdx = firstRaw === 0 ? 6 : firstRaw - 1;
  const total = new Date(year, month + 1, 0).getDate();
  const prevTotal = new Date(year, month, 0).getDate();
  const days: { day: number; month: number; year: number; isCurrent: boolean; dateStr: string }[] = [];

  for (let i = 0; i < firstIdx; i++) {
    const d = prevTotal - firstIdx + 1 + i;
    const m = month === 0 ? 11 : month - 1;
    const y = month === 0 ? year - 1 : year;
    days.push({ day: d, month: m, year: y, isCurrent: false, dateStr: `${y}-${String(m+1).padStart(2,'0')}-${String(d).padStart(2,'0')}` });
  }
  for (let i = 1; i <= total; i++) {
    days.push({ day: i, month, year, isCurrent: true, dateStr: `${year}-${String(month+1).padStart(2,'0')}-${String(i).padStart(2,'0')}` });
  }
  const rem = 35 - days.length;
  for (let i = 1; i <= rem; i++) {
    const m = month === 11 ? 0 : month + 1;
    const y = month === 11 ? year + 1 : year;
    days.push({ day: i, month: m, year: y, isCurrent: false, dateStr: `${y}-${String(m+1).padStart(2,'0')}-${String(i).padStart(2,'0')}` });
  }
  return days;
}

export function StaffCalendarTab({ year, onYearChange, calendarItems, calendarLoading, onRefresh }: Props) {
  const today = todayStr();
  const now = new Date();
  const [month, setMonth] = useState(now.getMonth());
  const [selectedDate, setSelectedDate] = useState<string | null>(today);
  const [displayMode, setDisplayMode] = useState<'Ngày' | 'Tuần' | 'Tháng' | 'Năm'>('Tháng');
  const [showDisplayDd, setShowDisplayDd] = useState(false);
  const [activeEvent, setActiveEvent] = useState<CalendarItem | null>(null);
  const [eventDetail, setEventDetail] = useState<any>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [showRejectInput, setShowRejectInput] = useState(false);
  const [proposalNote, setProposalNote] = useState('');
  const [proposalStart, setProposalStart] = useState('');
  const [proposalEnd, setProposalEnd] = useState('');
  const [showProposalInput, setShowProposalInput] = useState(false);
  const [handoverNote, setHandoverNote] = useState('');
  const [submittedVisitRequestId, setSubmittedVisitRequestId] = useState<number | null>(null);

  const daysGrid = useMemo(() => buildDaysGrid(year, month), [year, month]);

  const shiftView = (dir: -1 | 1) => {
    const base = selectedDate ? new Date(selectedDate) : new Date(year, month, 1);
    if (displayMode === 'Ngày') {
      base.setDate(base.getDate() + dir);
      const next = `${base.getFullYear()}-${String(base.getMonth()+1).padStart(2,'0')}-${String(base.getDate()).padStart(2,'0')}`;
      setSelectedDate(next);
      setMonth(base.getMonth());
      onYearChange(base.getFullYear());
    } else if (displayMode === 'Tuần') {
      base.setDate(base.getDate() + dir * 7);
      const next = `${base.getFullYear()}-${String(base.getMonth()+1).padStart(2,'0')}-${String(base.getDate()).padStart(2,'0')}`;
      setSelectedDate(next);
      setMonth(base.getMonth());
      onYearChange(base.getFullYear());
    } else if (displayMode === 'Năm') {
      onYearChange(year + dir);
    } else if (month === 0 && dir === -1) {
      setMonth(11);
      onYearChange(year - 1);
    } else if (month === 11 && dir === 1) {
      setMonth(0);
      onYearChange(year + 1);
    } else {
      setMonth(m => m + dir);
    }
    setActiveEvent(null);
  };

  const prevMonth = () => shiftView(-1);
  const nextMonth = () => shiftView(1);

  const weekDays = useMemo(() => {
    const base = selectedDate ? new Date(selectedDate) : new Date();
    const day = base.getDay();
    const mondayOffset = day === 0 ? -6 : 1 - day;
    const monday = new Date(base);
    monday.setDate(base.getDate() + mondayOffset);
    return Array.from({ length: 7 }, (_, i) => {
      const d = new Date(monday);
      d.setDate(monday.getDate() + i);
      const dateStr = `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
      return { dateStr, label: WEEKDAYS[i], day: d.getDate(), month: d.getMonth(), year: d.getFullYear() };
    });
  }, [selectedDate]);

  const viewTitle = useMemo(() => {
    if (displayMode === 'Năm') return `Năm ${year}`;
    if (displayMode === 'Ngày' && selectedDate) return `Ngày ${selectedDate.split('-').reverse().join('/')}`;
    if (displayMode === 'Tuần') {
      const start = weekDays[0]?.dateStr.split('-').reverse().join('/');
      const end = weekDays[6]?.dateStr.split('-').reverse().join('/');
      return `Tuần ${start} - ${end}`;
    }
    return `${MONTHS[month]} ${year}`;
  }, [displayMode, month, selectedDate, weekDays, year]);

  const renderEventPill = (ev: CalendarItem, dateStr: string) => (
    <div key={ev.id} onClick={e => { e.stopPropagation(); setSelectedDate(dateStr); setActiveEvent(ev); }}
      className={`px-1.5 py-1 rounded-md border text-[9px] font-bold leading-tight cursor-pointer truncate ${ev.color} ${activeEvent?.id === ev.id ? 'ring-2 ring-[#f37021]/50' : ''}`}>
      <span className="inline-block w-1 h-1 rounded-full mr-1 bg-current" />{ev.title}
    </div>
  );

  const renderEmpty = (label = 'Không có lịch được giao') => (
    <div className="flex items-center justify-center min-h-[220px] text-sm text-slate-400 font-semibold">{label}</div>
  );

  const yearMonths = useMemo(() => Array.from({ length: 12 }, (_, m) => ({
    month: m,
    items: calendarItems.filter(e => {
      const d = new Date(e.date);
      return d.getFullYear() === year && d.getMonth() === m;
    }),
  })), [calendarItems, year]);

  // Fetch detail when event selected
  useEffect(() => {
    if (!activeEvent?.rawId) { setEventDetail(null); return; }
    (async () => {
      try {
        if (activeEvent.itemType === 'INVITATION') {
          const d = await departmentReceptionTasksApi.getInvitationDetail(activeEvent.rawId);
          setEventDetail(d);
        } else {
          const d = await departmentReceptionTasksApi.getRequestDetail(activeEvent.rawId);
          setEventDetail(d);
        }
      } catch { setEventDetail(null); }
    })();
    setRejectReason('');
    setShowRejectInput(false);
    setProposalNote('');
    setProposalStart('');
    setProposalEnd('');
    setShowProposalInput(false);
    setHandoverNote('');
  }, [activeEvent?.rawId, activeEvent?.itemType]);

  const handleAccept = async () => {
    if (!activeEvent) return;
    setActionLoading(true);
    try {
      if (activeEvent.itemType === 'INVITATION') {
        await departmentReceptionTasksApi.acceptInvitation(activeEvent.rawId);
        toast.success('Đã chấp nhận thư mời');
      } else {
        await departmentReceptionTasksApi.acceptAssignment(activeEvent.rawId);
        toast.success('Đã chấp nhận nhiệm vụ');
      }
      onRefresh();
      setActiveEvent(null);
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Thao tác thất bại');
    } finally { setActionLoading(false); }
  };

  const handleDecline = async () => {
    if (!activeEvent || !rejectReason.trim()) { toast.error('Vui lòng nhập lý do từ chối'); return; }
    setActionLoading(true);
    try {
      if (activeEvent.itemType === 'INVITATION') {
        await departmentReceptionTasksApi.declineInvitation(activeEvent.rawId, rejectReason.trim());
        toast.success('Đã từ chối thư mời');
      } else {
        await departmentReceptionTasksApi.declineAssignment(activeEvent.rawId, rejectReason.trim());
        toast.success('Đã từ chối nhiệm vụ');
      }
      onRefresh();
      setActiveEvent(null);
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Thao tác thất bại');
    } finally { setActionLoading(false); }
  };

  const handlePropose = async () => {
    if (!activeEvent || !proposalNote.trim()) { toast.error('Vui lòng nhập nội dung đề xuất'); return; }
    if (!proposalStart || !proposalEnd) { toast.error('Vui lòng chọn giờ bắt đầu và giờ kết thúc'); return; }
    if (proposalEnd <= proposalStart) {
      toast.error('Giờ kết thúc phải sau giờ bắt đầu');
      return;
    }
    setActionLoading(true);
    try {
      await departmentReceptionTasksApi.proposeChange(activeEvent.rawId, {
        proposedUsageStartAt: `${activeEvent.date}T${proposalStart}:00`,
        proposedUsageEndAt: `${activeEvent.date}T${proposalEnd}:00`,
        proposalNote: proposalNote.trim(),
        proposedDescription: proposalNote.trim(),
      });
      toast.success('Đã gửi đề xuất thay đổi');
      onRefresh();
      setActiveEvent(null);
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Gửi đề xuất thất bại');
    } finally { setActionLoading(false); }
  };

  const handleSignHandover = async (handoverType: 'BORROW' | 'RETURN') => {
    if (!activeEvent) return;
    setActionLoading(true);
    try {
      await departmentReceptionTasksApi.signHandover(
        activeEvent.rawId,
        handoverType,
        handoverType === 'BORROW' ? 'PROVIDER' : 'BORROWER',
        handoverNote.trim(),
      );
      toast.success(handoverType === 'BORROW' ? 'Đã ký bàn giao' : 'Đã ký nghiệm thu');
      onRefresh();
      setActiveEvent(null);
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Ký biên bản thất bại');
    } finally {
      setActionLoading(false);
    }
  };

  const openVisitDetail = () => {
    if (activeEvent?.visitRequestId) {
      setSubmittedVisitRequestId(Number(activeEvent.visitRequestId));
      return;
    }
    toast.error('Không tìm thấy lịch đoàn tiếp khách');
  };

  const getStatusBadge = (status: string) => {
    const map: Record<string, string> = {
      ASSIGNED: 'bg-orange-100 text-orange-700 border-orange-200',
      ACCEPTED: 'bg-emerald-100 text-emerald-700 border-emerald-200',
      DECLINED: 'bg-rose-100 text-rose-700 border-rose-200',
      IN_PROGRESS: 'bg-cyan-100 text-cyan-700 border-cyan-200',
      DONE: 'bg-slate-100 text-slate-600 border-slate-200',
      CANCELLED: 'bg-gray-100 text-gray-500 border-gray-200',
      CHANGE_PROPOSED: 'bg-amber-100 text-amber-700 border-amber-200',
    };
    return map[status] || 'bg-slate-100 text-slate-600 border-slate-200';
  };

  const statusLabel = (s: string) => {
    const m: Record<string, string> = {
      ASSIGNED: 'Mới được giao', ACCEPTED: 'Đã chấp nhận', DECLINED: 'Đã từ chối',
      IN_PROGRESS: 'Đang thực hiện', DONE: 'Hoàn thành', CANCELLED: 'Đã hủy',
      CHANGE_PROPOSED: 'Đang đề xuất', REQUESTED: 'Chờ phân công',
    };
    return m[s] || s;
  };

  const canAct = activeEvent && (activeEvent.status === 'ASSIGNED');
  const canProposeRequest = activeEvent?.itemType === 'REQUEST' && activeEvent.status !== 'CANCELLED' && activeEvent.status !== 'DECLINED' && activeEvent.status !== 'DONE' && activeEvent.status !== 'CHANGE_PROPOSED';
  const canSignBorrow = activeEvent?.itemType === 'REQUEST' && activeEvent.status === 'ACCEPTED';
  const canSignReturn = activeEvent?.itemType === 'REQUEST' && activeEvent.status === 'IN_PROGRESS';

  return (
    <div className="space-y-4">
      {/* Legend */}
      <div className="flex flex-wrap items-center gap-4 text-xs font-semibold text-slate-600">
        <span className="flex items-center gap-1.5"><span className="w-3 h-3 rounded-full bg-emerald-50 border-2 border-emerald-400 inline-block" />Thư mời chưa xử lý</span>
        <span className="flex items-center gap-1.5"><span className="w-3 h-3 rounded-full bg-orange-50 border-2 border-orange-400 inline-block" />Đơn yêu cầu chưa xử lý</span>
        <span className="flex items-center gap-1.5"><span className="w-3 h-3 rounded-full bg-blue-50 border-2 border-blue-400 inline-block" />Đã chấp nhận / Từ chối</span>
        <span className="flex items-center gap-1.5"><span className="w-3 h-3 rounded-full bg-slate-100 border-2 border-slate-400 inline-block" />Đã hủy / Hết hạn</span>
      </div>

      {/* Toolbar */}
      <div className="flex items-center gap-3 flex-wrap">
        <div className="bg-slate-100 p-0.5 rounded-xl border border-slate-200 flex items-center gap-1">
          <button onClick={() => { setMonth(now.getMonth()); onYearChange(now.getFullYear()); }} className="px-4 py-2 text-xs font-bold text-slate-700 bg-white shadow-sm hover:bg-slate-50 border border-slate-200/60 rounded-lg">Hôm nay</button>
          <div className="h-4 w-px bg-slate-200 mx-1" />
          <button onClick={prevMonth} className="p-2 text-slate-600 hover:bg-white rounded-lg transition-all"><ChevronLeft className="w-4 h-4" /></button>
          <button onClick={nextMonth} className="p-2 text-slate-600 hover:bg-white rounded-lg transition-all"><ChevronRight className="w-4 h-4" /></button>
        </div>
        <span className="text-base font-extrabold text-[#004c91]">{viewTitle}</span>
        <div className="relative ml-auto">
          <button onClick={() => setShowDisplayDd(v => !v)} className="flex items-center gap-2 px-4 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-slate-700 hover:bg-slate-50 shadow-sm">
            Hiển thị: {displayMode} <ChevronDown className="w-3.5 h-3.5 text-slate-400" />
          </button>
          {showDisplayDd && (
            <><div className="fixed inset-0 z-20" onClick={() => setShowDisplayDd(false)} />
            <div className="absolute right-0 top-full mt-2 w-32 bg-white border border-slate-200 rounded-xl shadow-xl z-30 py-1">
              {(['Ngày', 'Tuần', 'Tháng', 'Năm'] as const).map(m => (
                <button key={m} onClick={() => { setDisplayMode(m); setShowDisplayDd(false); }}
                  className={`w-full text-left px-4 py-2.5 text-xs font-bold transition-colors ${displayMode === m ? 'bg-blue-50 text-[#004c91]' : 'text-slate-700 hover:bg-slate-50'}`}>{m}</button>
              ))}
            </div></>
          )}
        </div>
      </div>

      <div className="flex gap-4">
        {/* Calendar grid */}
        <div className="flex-1 bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
          <div className="grid grid-cols-7 bg-[#004c91] text-center text-xs font-extrabold text-white uppercase tracking-wider py-4">
            {WEEKDAYS.map(d => <div key={d}>{d}</div>)}
          </div>
          {calendarLoading ? (
            <div className="flex items-center justify-center h-64 text-sm text-slate-400 font-semibold">Đang tải lịch...</div>
          ) : displayMode === 'Tháng' ? (
            <div className="grid grid-cols-7 grid-rows-5 min-h-[700px] divide-x divide-y divide-slate-200 bg-slate-50/20">
              {daysGrid.map((cell, idx) => {
                const dayEvs = calendarItems.filter(e => e.date === cell.dateStr);
                const isSelected = selectedDate === cell.dateStr;
                const isToday = cell.dateStr === today;
                return (
                  <div key={idx} onClick={() => { setSelectedDate(cell.dateStr); if (dayEvs.length === 0) setActiveEvent(null); }}
                    className={`h-[130px] max-h-[130px] overflow-hidden p-2 flex flex-col cursor-pointer transition-colors group ${
                      isSelected ? 'bg-orange-50 ring-2 ring-inset ring-[#f37021] z-10' : cell.isCurrent ? 'bg-white hover:bg-orange-50/40' : 'bg-slate-50/30 hover:bg-slate-50'
                    }`}>
                    {cell.isCurrent && (
                      <>
                        <span className={`self-start text-xs font-extrabold px-1.5 py-0.5 rounded-md mb-1 ${isToday ? 'bg-red-500 text-white' : isSelected ? 'bg-[#f37021] text-white' : 'text-slate-700'}`}>{cell.day}</span>
                        <div className="space-y-0.5 overflow-y-auto flex-1 no-scrollbar">
                          {dayEvs.map(ev => renderEventPill(ev, cell.dateStr))}
                        </div>
                      </>
                    )}
                  </div>
                );
              })}
            </div>
          ) : displayMode === 'Tuần' ? (
            <div className="grid grid-cols-7 min-h-[520px] divide-x divide-slate-200 bg-white">
              {weekDays.map(day => {
                const dayEvs = calendarItems.filter(e => e.date === day.dateStr);
                const isToday = day.dateStr === today;
                return (
                  <div key={day.dateStr} onClick={() => setSelectedDate(day.dateStr)} className={`p-3 ${selectedDate === day.dateStr ? 'bg-orange-50' : ''}`}>
                    <div className="text-center mb-3">
                      <p className="text-[10px] font-black uppercase text-slate-400">{day.label}</p>
                      <span className={`inline-flex mt-1 w-8 h-8 items-center justify-center rounded-full text-sm font-black ${isToday ? 'bg-red-500 text-white' : selectedDate === day.dateStr ? 'bg-[#f37021] text-white' : 'bg-slate-100 text-slate-700'}`}>{day.day}</span>
                    </div>
                    <div className="space-y-1">{dayEvs.length ? dayEvs.map(ev => renderEventPill(ev, day.dateStr)) : <p className="text-[11px] text-slate-300 text-center font-semibold pt-4">Trống</p>}</div>
                  </div>
                );
              })}
            </div>
          ) : displayMode === 'Ngày' ? (
            <div className="p-5 min-h-[520px] bg-white">
              {(() => {
                const dayEvs = calendarItems.filter(e => e.date === selectedDate);
                if (!dayEvs.length) return renderEmpty();
                return <div className="space-y-2 max-w-3xl mx-auto">{dayEvs.map(ev => (
                  <div key={ev.id} onClick={() => setActiveEvent(ev)} className={`px-4 py-3 rounded-xl border text-sm font-bold cursor-pointer ${ev.color}`}>
                    <div className="flex items-center justify-between gap-3">
                      <span className="truncate">{ev.title}</span>
                      <span className="text-xs shrink-0">{ev.time}</span>
                    </div>
                    {ev.delegationName && <p className="text-xs opacity-80 mt-1">{ev.delegationName}</p>}
                  </div>
                ))}</div>;
              })()}
            </div>
          ) : (
            <div className="p-5 grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4 bg-white max-h-[720px] overflow-y-auto">
              {yearMonths.map(({ month: m, items }) => (
                <button key={m} onClick={() => { setMonth(m); setDisplayMode('Tháng'); }}
                  className="text-left rounded-2xl border border-slate-200 bg-slate-50/60 p-4 hover:border-orange-200 hover:bg-orange-50/40 transition-colors min-h-[120px]">
                  <div className="flex items-center justify-between mb-3">
                    <h4 className="text-sm font-black text-[#004c91]">{MONTHS[m]}</h4>
                    <span className="text-[11px] font-black text-slate-400">{items.length} lịch</span>
                  </div>
                  <div className="space-y-1">
                    {items.slice(0, 3).map(ev => (
                      <div key={ev.id} className={`px-2 py-1 rounded-md border text-[10px] font-bold truncate ${ev.color}`}>{ev.title}</div>
                    ))}
                    {items.length > 3 && <p className="text-[11px] font-bold text-orange-600">...và {items.length - 3} lịch khác</p>}
                    {!items.length && <p className="text-[11px] font-semibold text-slate-300">Không có lịch</p>}
                  </div>
                </button>
              ))}
            </div>
          )}
        </div>

        <StaffLeaderTaskModal
          item={activeEvent?.itemType === 'PERSONAL' ? null : activeEvent}
          onClose={() => setActiveEvent(null)}
          onRefresh={onRefresh}
        />

        {/* Event detail modal */}
        {false && activeEvent && (
          <div className="fixed inset-0 z-50 bg-slate-900/45 flex items-center justify-center p-4" onClick={() => setActiveEvent(null)}>
          <div className="w-full max-w-4xl max-h-[92vh] bg-white rounded-2xl border border-slate-200 shadow-2xl flex flex-col overflow-hidden" onClick={e => e.stopPropagation()}>
            <div className="bg-[#004c91] px-6 py-5 text-white flex items-start justify-between gap-4 shadow-sm">
              <div className="flex-1 min-w-0">
                <span className="text-[11px] font-black uppercase tracking-wider text-white/75 inline-block mb-2">
                  {activeEvent.itemType === 'INVITATION' ? 'Chi tiết thư mời' : 'Thông tin đơn yêu cầu'}
                </span>
                <p className="text-xl md:text-3xl font-extrabold tracking-tight text-white leading-tight">{activeEvent.title}</p>
                {activeEvent.delegationName && <p className="text-sm text-white/80 font-medium mt-1">{activeEvent.delegationName}</p>}
              </div>
              <button onClick={() => setActiveEvent(null)} className="p-2 hover:bg-white/10 rounded-full text-white flex-shrink-0"><X className="w-5 h-5" /></button>
            </div>

            <div className="p-6 md:p-8 space-y-5 flex-1 overflow-y-auto bg-slate-50/50">
              {(activeEvent.visitRequestId || activeEvent.visitInstanceId) && (
                <button
                  onClick={openVisitDetail}
                  className="w-full md:w-auto py-3 px-5 rounded-xl border border-blue-100 bg-white text-[#004c91] text-sm font-black hover:bg-blue-50 flex items-center justify-center gap-2 shadow-sm"
                >
                  <FileText className="w-4 h-4" />
                  Xem chi tiết đoàn tiếp khách
                </button>
              )}

              <div className="bg-white rounded-2xl border border-slate-200/85 p-5 md:p-6 space-y-5">
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="p-4 bg-blue-50/60 rounded-2xl border border-blue-100">
                    <div className="flex items-center gap-2 text-[#004c91] mb-2">
                      <User className="w-4 h-4" />
                      <span className="text-[11px] font-bold uppercase tracking-wider">Người giao</span>
                    </div>
                    <p className="text-sm font-black text-[#004c91]">{activeEvent.host || 'Hệ thống'}</p>
                  </div>
                  <div className="p-4 bg-orange-50/60 rounded-2xl border border-orange-100">
                    <div className="flex items-center gap-2 text-[#f37021] mb-2">
                      <MapPin className="w-4 h-4" />
                      <span className="text-[11px] font-bold uppercase tracking-wider">Địa điểm</span>
                    </div>
                    <p className="text-sm font-black text-slate-800">{activeEvent.location || 'Chưa cập nhật'}</p>
                  </div>
                  <div className="sm:col-span-2 p-4 bg-slate-50 rounded-2xl border border-slate-100">
                    <div className="flex items-center gap-2 text-slate-500 mb-2">
                      <Clock className="w-4 h-4" />
                      <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian</span>
                    </div>
                    <div className="text-[15px] font-bold text-gray-800 flex items-center flex-wrap gap-2">
                      <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{activeEvent.time?.split('-')[0]?.trim()}</span>
                      <ChevronRight className="w-4 h-4 text-gray-400" />
                      <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{activeEvent.time?.split('-')[1]?.trim()}</span>
                      <span className="text-[#004c91] font-bold ml-1">{activeEvent.date?.split('-').reverse().join('-')}</span>
                    </div>
                  </div>
                </div>
                <div className="flex items-center justify-between gap-3">
                  <span className={`text-[11px] font-black px-3 py-1 rounded-full border ${getStatusBadge(activeEvent.status)}`}>{statusLabel(activeEvent.status)}</span>
                  <span className="text-[11px] font-bold text-slate-400">{activeEvent.itemType === 'INVITATION' ? 'Thư mời tham gia' : 'Đơn yêu cầu hỗ trợ'}</span>
                </div>
                {activeEvent.purpose && (
                  <div className="space-y-2">
                    <div className="flex items-center gap-2 text-slate-400">
                      <FileText className="w-4 h-4" />
                      <span className="text-[11px] font-bold uppercase tracking-wider">Nội dung chi tiết</span>
                    </div>
                    <div className="text-sm font-medium text-slate-700 leading-relaxed">
                      {activeEvent.purpose.split('\n').map((line, idx) => <p key={idx} className="mb-1.5">{line}</p>)}
                    </div>
                  </div>
                )}
              </div>

              {/* Decline reason from detail */}
              {eventDetail?.rejectReason && (
                <div className="bg-rose-50 rounded-xl p-3 border border-rose-100 flex gap-2">
                  <AlertCircle className="w-4 h-4 text-rose-500 flex-shrink-0 mt-0.5" />
                  <p className="text-[11px] text-rose-700 font-medium">{eventDetail.rejectReason}</p>
                </div>
              )}

              {/* Actions – only when status is ASSIGNED */}
              {canAct && (
                <div className="pt-2 space-y-2">
                  <button onClick={handleAccept} disabled={actionLoading}
                    className="w-full py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-black rounded-xl transition-colors disabled:opacity-50">
                    ✓ Chấp nhận
                  </button>
                  {!showRejectInput ? (
                    <button onClick={() => setShowRejectInput(true)} disabled={actionLoading}
                      className="w-full py-2.5 bg-white border border-rose-200 text-rose-600 text-sm font-black rounded-xl hover:bg-rose-50 transition-colors">
                      ✕ Từ chối
                    </button>
                  ) : (
                    <div className="space-y-2">
                      <textarea rows={2} value={rejectReason} onChange={e => setRejectReason(e.target.value)}
                        placeholder="Lý do từ chối..."
                        className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-rose-400 resize-none" />
                      <div className="flex gap-2">
                        <button onClick={handleDecline} disabled={actionLoading || !rejectReason.trim()}
                          className="flex-1 py-2 bg-rose-600 hover:bg-rose-700 text-white text-xs font-black rounded-xl disabled:opacity-50">Xác nhận từ chối</button>
                        <button onClick={() => setShowRejectInput(false)} className="px-3 py-2 text-xs font-bold text-slate-600 bg-slate-100 rounded-xl hover:bg-slate-200">Hủy</button>
                      </div>
                    </div>
                  )}
                  {activeEvent.itemType === 'REQUEST' && (
                    !showProposalInput ? (
                      <button onClick={() => setShowProposalInput(true)} disabled={actionLoading}
                        className="w-full py-2.5 bg-white border border-amber-200 text-amber-700 text-sm font-black rounded-xl hover:bg-amber-50 transition-colors">
                        Đề xuất thay đổi
                      </button>
                    ) : (
                      <div className="space-y-2">
                        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                          <div>
                            <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Thời gian bắt đầu mới</label>
                            <input type="time" value={proposalStart} onChange={e => setProposalStart(e.target.value)}
                              className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-amber-400" />
                          </div>
                          <div>
                            <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Thời gian kết thúc mới</label>
                            <input type="time" value={proposalEnd} onChange={e => setProposalEnd(e.target.value)}
                              className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-amber-400" />
                          </div>
                        </div>
                        <textarea rows={3} value={proposalNote} onChange={e => setProposalNote(e.target.value)}
                          placeholder="Nội dung/lý do đề xuất..."
                          className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-amber-400 resize-none" />
                        <div className="flex gap-2">
                          <button onClick={handlePropose} disabled={actionLoading || !proposalNote.trim() || !proposalStart || !proposalEnd}
                            className="flex-1 py-2 bg-amber-600 hover:bg-amber-700 text-white text-xs font-black rounded-xl disabled:opacity-50">Gửi đề xuất</button>
                          <button onClick={() => setShowProposalInput(false)} className="px-3 py-2 text-xs font-bold text-slate-600 bg-slate-100 rounded-xl hover:bg-slate-200">Hủy</button>
                        </div>
                      </div>
                    )
                  )}
                </div>
              )}
              {!canAct && canProposeRequest && (
                <div className="pt-2 space-y-2">
                  {!showProposalInput ? (
                    <button onClick={() => setShowProposalInput(true)} disabled={actionLoading}
                      className="w-full py-2.5 bg-white border border-amber-200 text-amber-700 text-sm font-black rounded-xl hover:bg-amber-50 transition-colors">
                      Đề xuất thay đổi
                    </button>
                  ) : (
                    <div className="space-y-2">
                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                        <div>
                          <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Thời gian bắt đầu mới</label>
                          <input type="time" value={proposalStart} onChange={e => setProposalStart(e.target.value)}
                            className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-amber-400" />
                        </div>
                        <div>
                          <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Thời gian kết thúc mới</label>
                          <input type="time" value={proposalEnd} onChange={e => setProposalEnd(e.target.value)}
                            className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-amber-400" />
                        </div>
                      </div>
                      <textarea rows={3} value={proposalNote} onChange={e => setProposalNote(e.target.value)}
                        placeholder="Nội dung/lý do đề xuất..."
                        className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-amber-400 resize-none" />
                      <div className="flex gap-2">
                        <button onClick={handlePropose} disabled={actionLoading || !proposalNote.trim() || !proposalStart || !proposalEnd}
                          className="flex-1 py-2 bg-amber-600 hover:bg-amber-700 text-white text-xs font-black rounded-xl disabled:opacity-50">Gửi đề xuất</button>
                        <button onClick={() => setShowProposalInput(false)} className="px-3 py-2 text-xs font-bold text-slate-600 bg-slate-100 rounded-xl hover:bg-slate-200">Hủy</button>
                      </div>
                    </div>
                  )}
                </div>
              )}
              {(canSignBorrow || canSignReturn) && (
                <div className="pt-3 space-y-2 border-t border-slate-100">
                  <textarea rows={2} value={handoverNote} onChange={e => setHandoverNote(e.target.value)}
                    placeholder={canSignBorrow ? 'Ghi chú bàn giao...' : 'Ghi chú nghiệm thu...'}
                    className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-blue-400 resize-none" />
                  {canSignBorrow && (
                    <button onClick={() => handleSignHandover('BORROW')} disabled={actionLoading}
                      className="w-full py-2.5 bg-orange-600 hover:bg-orange-700 text-white text-sm font-black rounded-xl transition-colors disabled:opacity-50">
                      Ký bàn giao
                    </button>
                  )}
                  {canSignReturn && (
                    <button onClick={() => handleSignHandover('RETURN')} disabled={actionLoading}
                      className="w-full py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-sm font-black rounded-xl transition-colors disabled:opacity-50">
                      Ký nghiệm thu
                    </button>
                  )}
                </div>
              )}
            </div>
          </div>
          </div>
        )}
      </div>
      <SubmittedVisitRequestDetailModal
        isOpen={submittedVisitRequestId != null}
        visitRequestId={submittedVisitRequestId}
        onClose={() => setSubmittedVisitRequestId(null)}
      />
    </div>
  );
}
