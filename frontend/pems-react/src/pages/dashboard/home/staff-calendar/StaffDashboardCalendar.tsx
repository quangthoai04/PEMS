/**
 * StaffDashboardCalendar — bảng lịch dashboard cho Staff Leader (STAFF+LEADER) và
 * Staff thường (STAFF+STAFF), data thật từ GET /api/dashboard/staff/calendar.
 *
 * Hai loại lịch:
 *   • Lịch văn phòng — toàn bộ yêu cầu đến thăm thuộc campus của user.
 *   • Lịch của tôi   — chỉ yêu cầu tham quan mà user là host.
 *
 * Click event → modal chi tiết (StaffVisitDetailModal). Staff Leader có thể
 * Chấp nhận & gán host / Từ chối theo allowedActions backend trả; flow gán host
 * gồm bước chọn host (AssignHostModal) + chọn/sửa mẫu email mời host
 * (EmailPreviewModal, template HOST_ASSIGNMENT) trước khi submit.
 */
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Calendar as CalendarIcon, ChevronLeft, ChevronRight, ChevronDown,
  Loader2, AlertCircle, RefreshCw, Briefcase, User as UserIcon, X,
} from 'lucide-react';
import toast, { Toaster } from 'react-hot-toast';
import {
  staffCalendarApi,
  type StaffCalendarItem,
  type StaffCalendarDetail,
} from '../../../../features/dashboard/api/staffCalendarApi';
import { delegationsApi } from '../../../../features/delegations/api/delegationsApi';
import { AssignHostModal } from '../../../../components/modals/AssignHostModal';
import type { HostCandidate } from '../../../../features/delegations/types/delegations.types';
import {
  EmailPreviewModal,
  type EmailPreviewSendPayload,
} from '../../../../features/delegations/components/EmailPreviewModal';
import { StaffVisitDetailModal } from './StaffVisitDetailModal';

type DisplayMode = 'Tháng' | 'Tuần' | 'Ngày';
type CalendarType = 'office' | 'mine';

const WEEKDAYS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
const MONTH_NAMES = [
  'Tháng 1', 'Tháng 2', 'Tháng 3', 'Tháng 4', 'Tháng 5', 'Tháng 6',
  'Tháng 7', 'Tháng 8', 'Tháng 9', 'Tháng 10', 'Tháng 11', 'Tháng 12',
];

/** Legend đúng nhóm nghiệp vụ yêu cầu đến thăm (không dùng "thư mời"/"đơn yêu cầu"). */
const LEGEND: { key: string; label: string; dot: string }[] = [
  { key: 'NEW', label: 'Mới / Chờ xử lý', dot: 'bg-sky-400' },
  { key: 'NEEDS_ACTION', label: 'Cần xử lý', dot: 'bg-amber-400' },
  { key: 'PROCESSED', label: 'Đã xử lý', dot: 'bg-emerald-400' },
  { key: 'CANCELLED_OR_EXPIRED', label: 'Bị hủy / Đã hết hạn', dot: 'bg-slate-300' },
  { key: 'MINE', label: 'Tôi là host', dot: 'bg-[#004c91]' },
];

const PILL_CLASS: Record<string, string> = {
  NEW: 'bg-sky-50 text-sky-700 border-sky-200 hover:bg-sky-100',
  NEEDS_ACTION: 'bg-amber-50 text-amber-800 border-amber-300 hover:bg-amber-100',
  PROCESSED: 'bg-emerald-50 text-emerald-700 border-emerald-200 hover:bg-emerald-100',
  CANCELLED_OR_EXPIRED: 'bg-slate-100 text-slate-500 border-slate-200 hover:bg-slate-200',
  MINE: 'bg-blue-50 text-[#004c91] border-blue-300 hover:bg-blue-100',
};

const pad2 = (n: number) => String(n).padStart(2, '0');
const toDateKey = (d: Date) => `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
const addDays = (d: Date, days: number) => {
  const r = new Date(d);
  r.setDate(r.getDate() + days);
  return r;
};
/** Thứ Hai đầu tuần của một ngày bất kỳ. */
const startOfWeek = (d: Date) => {
  const r = new Date(d.getFullYear(), d.getMonth(), d.getDate());
  const day = r.getDay(); // 0=CN
  return addDays(r, day === 0 ? -6 : 1 - day);
};
const fmtTime = (value: string) => {
  const d = new Date(value);
  return `${pad2(d.getHours())}:${pad2(d.getMinutes())}`;
};

export function StaffDashboardCalendar({ isStaffLeader }: { user?: any; isStaffLeader?: boolean }) {
  const today = new Date();
  const todayKey = toDateKey(today);

  // ── Điều hướng lịch ──
  const [displayMode, setDisplayMode] = useState<DisplayMode>('Tháng');
  const [anchorDate, setAnchorDate] = useState<Date>(new Date(today.getFullYear(), today.getMonth(), today.getDate()));
  const [calendarType, setCalendarType] = useState<CalendarType>('office');
  const [showModeDropdown, setShowModeDropdown] = useState(false);

  // ── Data ──
  const [items, setItems] = useState<StaffCalendarItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // ── Modal chi tiết + action ──
  const [detailInstanceId, setDetailInstanceId] = useState<number | null>(null);
  const [detailRefreshKey, setDetailRefreshKey] = useState(0);

  // Từ chối yêu cầu (Staff Leader — campus-reject).
  const [reject, setReject] = useState<{
    open: boolean; detail: StaffCalendarDetail | null; text: string; submitting: boolean; error: string | null;
  }>({ open: false, detail: null, text: '', submitting: false, error: null });

  // Gán host: bước 1 chọn host, bước 2 chọn/sửa mẫu email mời host.
  const [assign, setAssign] = useState<{ open: boolean; detail: StaffCalendarDetail | null }>({ open: false, detail: null });
  const [pendingHost, setPendingHost] = useState<HostCandidate | null>(null);
  const [emailPreview, setEmailPreview] = useState({
    open: false, loading: false, sending: false, error: null as string | null,
    subject: '', body: '',
  });

  // ── Khoảng ngày hiển thị (và fetch) theo chế độ xem ──
  const { gridStart, gridEnd, monthCells } = useMemo(() => {
    if (displayMode === 'Ngày') {
      const d = new Date(anchorDate);
      return { gridStart: d, gridEnd: d, monthCells: [] as Date[] };
    }
    if (displayMode === 'Tuần') {
      const start = startOfWeek(anchorDate);
      return { gridStart: start, gridEnd: addDays(start, 6), monthCells: [] as Date[] };
    }
    // Tháng: lưới Monday-first phủ trọn tháng của anchorDate.
    const firstOfMonth = new Date(anchorDate.getFullYear(), anchorDate.getMonth(), 1);
    const lastOfMonth = new Date(anchorDate.getFullYear(), anchorDate.getMonth() + 1, 0);
    const start = startOfWeek(firstOfMonth);
    const totalDays = Math.ceil(((lastOfMonth.getTime() - start.getTime()) / 86400000 + 1) / 7) * 7;
    const cells: Date[] = [];
    for (let i = 0; i < totalDays; i++) cells.push(addDays(start, i));
    return { gridStart: start, gridEnd: cells[cells.length - 1], monthCells: cells };
  }, [displayMode, anchorDate]);

  const fromStr = toDateKey(gridStart);
  const toStr = toDateKey(gridEnd);

  const fetchCalendar = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await staffCalendarApi.getCalendar({ viewMode: calendarType, from: fromStr, to: toStr });
      setItems(res?.items || []);
    } catch (e: any) {
      setError('Không thể tải lịch yêu cầu đến thăm. Vui lòng thử lại.');
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [calendarType, fromStr, toStr]);

  useEffect(() => {
    fetchCalendar();
  }, [fetchCalendar]);

  // ── Gom event theo ngày (một yêu cầu kéo dài nhiều ngày sẽ hiện ở mọi ngày nó phủ) ──
  const eventsByDay = useMemo(() => {
    const map: Record<string, StaffCalendarItem[]> = {};
    const rangeStart = new Date(gridStart.getFullYear(), gridStart.getMonth(), gridStart.getDate());
    const rangeEnd = new Date(gridEnd.getFullYear(), gridEnd.getMonth(), gridEnd.getDate());
    for (const item of items) {
      const s = new Date(item.plannedStartAt);
      const e = new Date(item.plannedEndAt);
      let d = new Date(Math.max(rangeStart.getTime(), new Date(s.getFullYear(), s.getMonth(), s.getDate()).getTime()));
      const end = new Date(Math.min(rangeEnd.getTime(), new Date(e.getFullYear(), e.getMonth(), e.getDate()).getTime()));
      while (d <= end) {
        const key = toDateKey(d);
        (map[key] = map[key] || []).push(item);
        d = addDays(d, 1);
      }
    }
    Object.values(map).forEach((list) =>
      list.sort((a, b) => new Date(a.plannedStartAt).getTime() - new Date(b.plannedStartAt).getTime()));
    return map;
  }, [items, gridStart, gridEnd]);

  // ── Điều hướng ──
  const goPrev = () => {
    if (displayMode === 'Tháng') setAnchorDate((d) => new Date(d.getFullYear(), d.getMonth() - 1, 1));
    else setAnchorDate((d) => addDays(d, displayMode === 'Tuần' ? -7 : -1));
  };
  const goNext = () => {
    if (displayMode === 'Tháng') setAnchorDate((d) => new Date(d.getFullYear(), d.getMonth() + 1, 1));
    else setAnchorDate((d) => addDays(d, displayMode === 'Tuần' ? 7 : 1));
  };
  const goToday = () => setAnchorDate(new Date(today.getFullYear(), today.getMonth(), today.getDate()));

  const headerLabel = displayMode === 'Tháng'
    ? `${MONTH_NAMES[anchorDate.getMonth()]} ${anchorDate.getFullYear()}`
    : displayMode === 'Tuần'
      ? `${pad2(gridStart.getDate())}/${pad2(gridStart.getMonth() + 1)} – ${pad2(gridEnd.getDate())}/${pad2(gridEnd.getMonth() + 1)}/${gridEnd.getFullYear()}`
      : `${pad2(anchorDate.getDate())}/${pad2(anchorDate.getMonth() + 1)}/${anchorDate.getFullYear()}`;

  // ── Refresh sau action (không reload trang) ──
  const refreshAfterAction = useCallback(async () => {
    setDetailRefreshKey((k) => k + 1);
    await fetchCalendar();
  }, [fetchCalendar]);

  // ── Flow từ chối (Staff Leader) ──
  const submitReject = async () => {
    if (!reject.detail || !reject.text.trim()) return;
    setReject((s) => ({ ...s, submitting: true, error: null }));
    try {
      await delegationsApi.campusReject(reject.detail.visitRequestId, reject.text.trim());
      toast.success('Đã từ chối yêu cầu đến thăm.');
      setReject({ open: false, detail: null, text: '', submitting: false, error: null });
      await refreshAfterAction();
    } catch (e: any) {
      setReject((s) => ({
        ...s, submitting: false,
        error: e?.response?.data?.message || e?.response?.data?.title || 'Từ chối thất bại. Vui lòng thử lại.',
      }));
    }
  };

  // ── Flow gán host: chọn host → chọn/sửa mẫu email mời host → submit ──
  const openHostEmailPreview = async (host: HostCandidate, detail: StaffCalendarDetail) => {
    setPendingHost(host);
    setEmailPreview((s) => ({ ...s, open: true, loading: true, error: null }));
    try {
      const res = await delegationsApi.previewEmailTemplate({
        templateCode: 'HOST_ASSIGNMENT',
        context: {
          DelegationName: detail.delegationName ?? 'đoàn khách',
          CampusName: detail.campusName,
          PlannedStartAt: `${fmtTime(detail.plannedStartAt)} ${new Date(detail.plannedStartAt).toLocaleDateString('vi-VN')}`,
          HostName: host.fullName,
          RequestCode: detail.requestCode ?? '',
        },
      });
      setEmailPreview({
        open: true, loading: false, sending: false, error: null,
        subject: res.subject, body: res.bodyHtml,
      });
    } catch (e: any) {
      setEmailPreview((s) => ({
        ...s, loading: false,
        error: e?.response?.data?.message || 'Không thể tải bản xem trước email.',
      }));
    }
  };

  const submitAssignHost = async (payload: EmailPreviewSendPayload) => {
    if (!assign.detail || !pendingHost) return;
    if (!payload.subject.trim()) { toast.error('Tiêu đề email không được để trống.'); return; }
    if (!payload.bodyHtml.trim()) { toast.error('Nội dung email không được để trống.'); return; }
    setEmailPreview((s) => ({ ...s, sending: true }));
    try {
      await delegationsApi.assignHost(
        assign.detail.visitRequestId,
        assign.detail.visitInstanceId,
        pendingHost.userId,
        { useEditedContent: true, subject: payload.subject.trim(), bodyHtml: payload.bodyHtml, attachments: payload.attachments },
      );
      toast.success('Đã gán host và gửi email mời host.');
      setEmailPreview((s) => ({ ...s, open: false, sending: false }));
      setAssign({ open: false, detail: null });
      setPendingHost(null);
      await refreshAfterAction();
    } catch (e: any) {
      setEmailPreview((s) => ({ ...s, sending: false }));
      toast.error(e?.response?.data?.message || e?.response?.data?.title || 'Gán host thất bại.');
    }
  };

  // ── Render 1 event pill ── (`key` khai báo trong props type vì project thiếu @types/react
  // đầy đủ nên JSX không tự loại trừ key khỏi props check)
  const EventPill = ({ item, full = false }: { item: StaffCalendarItem; full?: boolean; key?: string | number }) => (
    <button
      type="button"
      onClick={() => setDetailInstanceId(item.visitInstanceId)}
      title={`${item.title} — ${item.displayStatus}`}
      className={`w-full text-left border rounded-lg transition-colors cursor-pointer ${PILL_CLASS[item.colorType] || PILL_CLASS.NEW} ${full ? 'px-3 py-2' : 'px-1.5 py-0.5'}`}
    >
      {full ? (
        <>
          <p className="text-xs font-bold truncate">{item.title}</p>
          <p className="text-[11px] font-medium opacity-80 mt-0.5">
            {fmtTime(item.plannedStartAt)} – {fmtTime(item.plannedEndAt)} · {item.displayStatus}
            {item.currentHostName ? ` · Host: ${item.currentHostName}` : ''}
          </p>
        </>
      ) : (
        <p className="text-[10px] font-bold truncate leading-4">
          {fmtTime(item.plannedStartAt)} {item.title}
        </p>
      )}
    </button>
  );

  const renderDayColumn = (date: Date, compactHeader = false) => {
    const key = toDateKey(date);
    const dayEvents = eventsByDay[key] || [];
    const isToday = key === todayKey;
    const isPast = key < todayKey;
    return (
      <div key={key} className={`flex-1 min-w-0 rounded-xl border p-2 ${isToday ? 'border-[#004c91]/50 bg-blue-50/40' : 'border-slate-200 bg-white'} ${isPast ? 'opacity-70' : ''}`}>
        <p className={`text-xs font-bold mb-2 ${isToday ? 'text-[#004c91]' : 'text-slate-500'}`}>
          {compactHeader
            ? `${WEEKDAYS[(date.getDay() + 6) % 7]} ${pad2(date.getDate())}/${pad2(date.getMonth() + 1)}`
            : `${pad2(date.getDate())}/${pad2(date.getMonth() + 1)}`}
        </p>
        <div className="space-y-1.5">
          {dayEvents.length === 0 ? (
            <p className="text-[11px] text-slate-400 italic">Trống</p>
          ) : (
            dayEvents.map((ev) => <EventPill key={`${ev.visitInstanceId}_${key}`} item={ev} full />)
          )}
        </div>
      </div>
    );
  };

  return (
    <div className="space-y-4">
      <Toaster position="top-right" />

      {/* ── Toolbar ── */}
      <div className="bg-white border border-slate-200 rounded-2xl shadow-sm px-4 py-3 flex flex-wrap items-center gap-3">
        <h2 className="text-base font-extrabold text-slate-800 flex items-center gap-2 mr-1">
          <CalendarIcon className="w-5 h-5 text-[#004c91]" />
          Lịch yêu cầu đến thăm
        </h2>

        <div className="flex items-center gap-1">
          <button onClick={goPrev} className="w-8 h-8 rounded-lg hover:bg-slate-100 flex items-center justify-center text-slate-500 hover:text-[#004c91] transition-colors cursor-pointer">
            <ChevronLeft className="w-4 h-4" />
          </button>
          <span className="text-sm font-extrabold text-slate-700 min-w-[150px] text-center">{headerLabel}</span>
          <button onClick={goNext} className="w-8 h-8 rounded-lg hover:bg-slate-100 flex items-center justify-center text-slate-500 hover:text-[#004c91] transition-colors cursor-pointer">
            <ChevronRight className="w-4 h-4" />
          </button>
          <button onClick={goToday} className="ml-1 text-xs font-bold text-[#004c91] bg-blue-50 hover:bg-blue-100 px-3 py-1.5 rounded-lg transition-colors cursor-pointer">
            Hôm nay
          </button>
        </div>

        <div className="flex items-center gap-2 ml-auto">
          {/* Loại lịch */}
          <div className="flex rounded-xl border border-slate-200 overflow-hidden">
            <button
              onClick={() => setCalendarType('office')}
              className={`px-3 py-1.5 text-xs font-bold flex items-center gap-1.5 transition-colors cursor-pointer ${calendarType === 'office' ? 'bg-[#004c91] text-white' : 'bg-white text-slate-600 hover:bg-slate-50'}`}
            >
              <Briefcase className="w-3.5 h-3.5" /> Lịch văn phòng
            </button>
            <button
              onClick={() => setCalendarType('mine')}
              className={`px-3 py-1.5 text-xs font-bold flex items-center gap-1.5 transition-colors cursor-pointer ${calendarType === 'mine' ? 'bg-[#004c91] text-white' : 'bg-white text-slate-600 hover:bg-slate-50'}`}
            >
              <UserIcon className="w-3.5 h-3.5" /> Lịch của tôi
            </button>
          </div>

          {/* Chế độ hiển thị */}
          <div className="relative">
            <button
              onClick={() => setShowModeDropdown((v) => !v)}
              className="px-3 py-1.5 text-xs font-bold text-slate-600 bg-white border border-slate-200 rounded-xl hover:bg-slate-50 flex items-center gap-1.5 transition-colors cursor-pointer"
            >
              {displayMode} <ChevronDown className="w-3.5 h-3.5" />
            </button>
            {showModeDropdown && (
              <div className="absolute right-0 top-full mt-1 z-20 bg-white border border-slate-200 rounded-xl shadow-lg overflow-hidden min-w-[110px]">
                {(['Tháng', 'Tuần', 'Ngày'] as DisplayMode[]).map((m) => (
                  <button
                    key={m}
                    onClick={() => { setDisplayMode(m); setShowModeDropdown(false); }}
                    className={`w-full text-left px-3.5 py-2 text-xs font-bold transition-colors cursor-pointer ${displayMode === m ? 'bg-blue-50 text-[#004c91]' : 'text-slate-600 hover:bg-slate-50'}`}
                  >
                    {m}
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ── Bảng lịch ── */}
      <div className="bg-white border border-slate-200 rounded-2xl shadow-sm overflow-hidden">
        {/* Legend */}
        <div className="px-4 py-2.5 border-b border-slate-100 flex flex-wrap items-center gap-x-4 gap-y-1.5">
          {LEGEND.map((l) => (
            <span key={l.key} className="flex items-center gap-1.5 text-[11px] font-semibold text-slate-500">
              <span className={`w-2.5 h-2.5 rounded-full ${l.dot}`} />
              {l.label}
            </span>
          ))}
          {loading && <Loader2 className="w-4 h-4 animate-spin text-[#004c91] ml-auto" />}
        </div>

        {error ? (
          <div className="py-14 text-center">
            <AlertCircle className="w-8 h-8 mx-auto mb-2 text-red-400" />
            <p className="text-sm font-medium text-red-600 mb-3">{error}</p>
            <button
              onClick={fetchCalendar}
              className="text-xs font-bold text-[#004c91] bg-blue-50 hover:bg-blue-100 px-4 py-2 rounded-xl inline-flex items-center gap-1.5 transition-colors cursor-pointer"
            >
              <RefreshCw className="w-3.5 h-3.5" /> Thử lại
            </button>
          </div>
        ) : displayMode === 'Tháng' ? (
          <div className="p-3">
            <div className="grid grid-cols-7 mb-1">
              {WEEKDAYS.map((wd) => (
                <div key={wd} className="text-center text-[10px] font-extrabold text-slate-400 uppercase tracking-widest py-1.5">{wd}</div>
              ))}
            </div>
            <div className="grid grid-cols-7 gap-1">
              {monthCells.map((date) => {
                const key = toDateKey(date);
                const inMonth = date.getMonth() === anchorDate.getMonth();
                const isToday = key === todayKey;
                const isPast = key < todayKey;
                const dayEvents = eventsByDay[key] || [];
                const visible = dayEvents.slice(0, 3);
                const more = dayEvents.length - visible.length;
                return (
                  <div
                    key={key}
                    className={`min-h-[92px] rounded-lg border p-1 flex flex-col gap-0.5 transition-colors
                      ${isToday ? 'border-[#004c91]/60 bg-blue-50/50' : 'border-slate-100'}
                      ${!inMonth ? 'bg-slate-50/60' : isPast && !isToday ? 'bg-slate-50/40' : 'bg-white'}`}
                  >
                    <span className={`text-[11px] font-bold self-end px-1
                      ${isToday ? 'text-white bg-[#004c91] rounded-md px-1.5 py-0.5' : !inMonth ? 'text-slate-300' : isPast ? 'text-slate-400' : 'text-slate-600'}`}>
                      {date.getDate()}
                    </span>
                    {visible.map((ev) => <EventPill key={`${ev.visitInstanceId}_${key}`} item={ev} />)}
                    {more > 0 && (
                      <button
                        type="button"
                        onClick={() => { setAnchorDate(new Date(date)); setDisplayMode('Ngày'); }}
                        className="text-[10px] font-bold text-[#004c91] hover:underline text-left px-1 cursor-pointer"
                      >
                        +{more} khác
                      </button>
                    )}
                  </div>
                );
              })}
            </div>
            {!loading && items.length === 0 && (
              <p className="text-center text-sm text-slate-400 font-medium py-4">
                Không có yêu cầu đến thăm trong khoảng thời gian này.
              </p>
            )}
          </div>
        ) : displayMode === 'Tuần' ? (
          <div className="p-3">
            <div className="grid grid-cols-2 md:grid-cols-4 xl:grid-cols-7 gap-2">
              {Array.from({ length: 7 }, (_, i) => addDays(gridStart, i)).map((d) => renderDayColumn(d, true))}
            </div>
            {!loading && items.length === 0 && (
              <p className="text-center text-sm text-slate-400 font-medium py-4">
                Không có yêu cầu đến thăm trong khoảng thời gian này.
              </p>
            )}
          </div>
        ) : (
          <div className="p-4">
            {(eventsByDay[toDateKey(anchorDate)] || []).length === 0 && !loading ? (
              <p className="text-center text-sm text-slate-400 font-medium py-8">
                Không có yêu cầu đến thăm trong khoảng thời gian này.
              </p>
            ) : (
              <div className="space-y-2 max-w-2xl mx-auto">
                {(eventsByDay[toDateKey(anchorDate)] || []).map((ev) => (
                  <EventPill key={ev.visitInstanceId} item={ev} full />
                ))}
              </div>
            )}
          </div>
        )}
      </div>

      {/* ── Modal chi tiết yêu cầu tham quan ── */}
      <StaffVisitDetailModal
        isOpen={detailInstanceId !== null}
        visitInstanceId={detailInstanceId}
        refreshKey={detailRefreshKey}
        onClose={() => setDetailInstanceId(null)}
        onAssignHost={isStaffLeader ? (d) => setAssign({ open: true, detail: d }) : undefined}
        onReject={isStaffLeader ? (d) => setReject({ open: true, detail: d, text: '', submitting: false, error: null }) : undefined}
      />

      {/* ── Bước 1: chọn host (pick-only, tiếp tục sang bước email) ── */}
      {assign.open && assign.detail && (
        <AssignHostModal
          isOpen={assign.open}
          mode="approve"
          visitRequestId={assign.detail.visitRequestId}
          visitInstanceId={assign.detail.visitInstanceId}
          delegationName={assign.detail.delegationName}
          currentHostUserId={assign.detail.currentHostUserId}
          customTitle={assign.detail.allowedActions.canApprove ? 'Chấp nhận yêu cầu & chọn host' : 'Chọn host phụ trách'}
          onHostPicked={(host) => { void openHostEmailPreview(host, assign.detail!); }}
          onClose={() => setAssign({ open: false, detail: null })}
          onConfirmed={() => { /* không dùng ở pick-only mode */ }}
        />
      )}

      {/* ── Bước 2: chọn/sửa mẫu email mời host rồi submit gán host ── */}
      <EmailPreviewModal
        open={emailPreview.open}
        loading={emailPreview.loading}
        sending={emailPreview.sending}
        error={emailPreview.error}
        subject={emailPreview.subject}
        body={emailPreview.body}
        isActionTemplate={false}
        recipient={pendingHost ? {
          name: pendingHost.fullName,
          email: pendingHost.email,
          roleLabel: 'Host phụ trách',
          departmentName: pendingHost.departmentName,
          campusName: assign.detail?.campusName,
        } : null}
        canSend
        sendLabel="Gán host & gửi email"
        onSubjectChange={(v) => setEmailPreview((s) => ({ ...s, subject: v }))}
        onBodyChange={(v) => setEmailPreview((s) => ({ ...s, body: v }))}
        onClose={() => setEmailPreview((s) => ({ ...s, open: false }))}
        onRestore={() => { if (pendingHost && assign.detail) void openHostEmailPreview(pendingHost, assign.detail); }}
        onSend={(payload) => { void submitAssignHost(payload); }}
      />

      {/* ── Modal từ chối yêu cầu ── */}
      {reject.open && reject.detail && (
        <div className="fixed inset-0 z-[110] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
          <div className="bg-white rounded-3xl shadow-2xl w-full max-w-md overflow-hidden">
            <div className="px-6 py-4 bg-red-600 flex items-center justify-between">
              <h3 className="text-lg font-bold text-white flex items-center gap-2">
                <AlertCircle className="w-5 h-5 bg-white/20 rounded-full p-0.5" /> Từ chối yêu cầu đến thăm
              </h3>
              <button
                type="button"
                disabled={reject.submitting}
                onClick={() => setReject({ open: false, detail: null, text: '', submitting: false, error: null })}
                className="text-white/85 hover:text-white hover:bg-white/10 rounded-full p-1.5 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="p-6">
              <p className="text-sm text-gray-700 mb-3">
                Vui lòng nhập lý do từ chối yêu cầu của đoàn{' '}
                <span className="font-bold text-[#004c91]">{reject.detail.delegationName || reject.detail.requestCode}</span>:
              </p>
              <textarea
                value={reject.text}
                onChange={(e) => setReject((s) => ({ ...s, text: e.target.value }))}
                placeholder="Nhập lý do chi tiết..."
                disabled={reject.submitting}
                className="w-full px-4 py-3 rounded-2xl border border-gray-200 focus:border-red-500 focus:ring-4 focus:ring-red-500/10 outline-none transition-all text-sm min-h-[110px] resize-none bg-gray-50/50 focus:bg-white"
              />
              {reject.error && <p className="text-red-500 text-sm mt-2">{reject.error}</p>}
            </div>
            <div className="px-6 py-4 bg-gray-50 flex items-center justify-end gap-3 border-t border-gray-100">
              <button
                type="button"
                disabled={reject.submitting}
                onClick={() => setReject({ open: false, detail: null, text: '', submitting: false, error: null })}
                className="px-5 py-2 rounded-xl font-bold text-gray-600 hover:bg-gray-200 transition-colors outline-none text-sm cursor-pointer"
              >
                Hủy bỏ
              </button>
              <button
                type="button"
                disabled={!reject.text.trim() || reject.submitting}
                onClick={submitReject}
                className="px-6 py-2 rounded-xl font-bold text-white bg-red-600 hover:bg-red-700 shadow-sm transition-all outline-none text-sm cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {reject.submitting ? 'Đang xử lý...' : 'Xác nhận từ chối'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
