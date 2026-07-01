/**
 * StaffTasksTab – Tab nhiệm vụ được giao cho Department Staff
 * Chỉ hiển thị nhiệm vụ đã chấp nhận, từ chối, đang đề xuất, đã hủy
 * (KHÔNG có "Mới được giao" vì chúng được hiển thị ở banner cam phía trên)
 */
import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, ChevronLeft, ChevronRight, Eye, AlertCircle, X, CheckCircle2, XCircle, FileSignature, FileText } from 'lucide-react';
import toast from 'react-hot-toast';
import { SubmittedVisitRequestDetailModal } from '../../../components/modals/SubmittedVisitRequestDetailModal';
import { departmentReceptionTasksApi } from '../../../features/department-reception-tasks/api/departmentReceptionTasksApi';
import type { AssignedTask, TaskStatusFilter } from './useDeptStaffData';
import { StaffLeaderTaskModal, type StaffLeaderTaskModalItem } from './StaffLeaderTaskModal';

interface Props {
  user: any;
  tasks: AssignedTask[];
  totalTasks: number;
  tasksLoading: boolean;
  attentionItems: AssignedTask[];
  filter: {
    search: string; itemType: string; status: TaskStatusFilter;
    fromDate: string; toDate: string; sortDirection: 'ASC' | 'DESC'; page: number;
  };
  onFilterChange: (patch: Partial<Props['filter']>) => void;
  onRefresh: () => void;
}

const PAGE_SIZE = 8;

const STATUS_OPTIONS: { value: string; label: string }[] = [
  { value: 'ALL', label: 'Tất cả trạng thái' },
  { value: 'ACCEPTED', label: 'Đã chấp nhận' },
  { value: 'DECLINED', label: 'Đã từ chối' },
  { value: 'CHANGE_PROPOSED', label: 'Đang đề xuất' },
  { value: 'IN_PROGRESS', label: 'Trong tiến trình' },
  { value: 'DONE', label: 'Hoàn thành' },
  { value: 'CANCELLED', label: 'Đã hủy' },
];

const STATUS_BADGE: Record<string, string> = {
  ASSIGNED: 'bg-orange-100 text-orange-700 border-orange-200',
  ACCEPTED: 'bg-emerald-100 text-emerald-700 border-emerald-200',
  DECLINED: 'bg-slate-100 text-slate-600 border-slate-200',
  IN_PROGRESS: 'bg-cyan-100 text-cyan-700 border-cyan-200',
  DONE: 'bg-slate-100 text-slate-600 border-slate-200',
  CANCELLED: 'bg-gray-100 text-gray-500 border-gray-200',
  CHANGE_PROPOSED: 'bg-amber-100 text-amber-700 border-amber-200',
  REJECTED: 'bg-rose-100 text-rose-700 border-rose-200',
};

const STATUS_LABEL: Record<string, string> = {
  ASSIGNED: 'Mới được giao', ACCEPTED: 'Đã chấp nhận', DECLINED: 'Đã từ chối',
  IN_PROGRESS: 'Đang thực hiện', DONE: 'Hoàn thành', CANCELLED: 'Đã hủy',
  CHANGE_PROPOSED: 'Đang đề xuất', REJECTED: 'Đã từ chối (HO)',
};

function fmt(iso?: string) {
  if (!iso) return '—';
  const d = new Date(iso);
  return `${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')} ${String(d.getDate()).padStart(2,'0')}/${String(d.getMonth()+1).padStart(2,'0')}/${d.getFullYear()}`;
}

function toTaskModalItem(item: AssignedTask): StaffLeaderTaskModalItem {
  const start = item.startAt ? new Date(item.startAt) : null;
  const end = item.endAt ? new Date(item.endAt) : null;
  const hasStart = !!start && !Number.isNaN(start.getTime());
  const hasEnd = !!end && !Number.isNaN(end.getTime());
  const date = hasStart
    ? `${start!.getFullYear()}-${String(start!.getMonth() + 1).padStart(2, '0')}-${String(start!.getDate()).padStart(2, '0')}`
    : '';
  const time = hasStart && hasEnd
    ? `${String(start!.getHours()).padStart(2, '0')}:${String(start!.getMinutes()).padStart(2, '0')} - ${String(end!.getHours()).padStart(2, '0')}:${String(end!.getMinutes()).padStart(2, '0')}`
    : '';

  return {
    itemType: item.itemType,
    rawId: item.itemType === 'INVITATION'
      ? (item.participantId || item.itemId)
      : (item.logisticsItemId || item.itemId),
    visitRequestId: item.visitRequestId,
    visitInstanceId: item.visitInstanceId,
    status: item.uiStatus || item.rawStatus,
    title: item.title,
    delegationName: item.delegationName,
    date,
    time,
    location: item.organizationName || 'Hòa Lạc',
    host: item.currentResponsibleName || 'Hệ thống',
    purpose: item.description || item.title,
    canAccept: item.canAccept,
    canDecline: item.canDecline,
    canProposeChange: item.canProposeChange,
    canSignBorrow: item.canSignBorrow,
    canSignReturn: item.canSignReturn,
  };
}

export function StaffTasksTab({ user, tasks, totalTasks, tasksLoading, attentionItems, filter, onFilterChange, onRefresh }: Props) {
  const totalPages = Math.max(1, Math.ceil(totalTasks / PAGE_SIZE));
  const navigate = useNavigate();

  // TEMP DEV TEST: Department contribution shortcut.
  // Shows an extra action (next to the unchanged eye icon) that opens the Contribution Page,
  // only for Department roles on rows that carry a visitInstanceId, and only in dev builds.
  // Does NOT touch the eye icon's onClick/detail flow, nor backend allowedActions.
  // Remove this shortcut when the OPEN_CONTRIBUTION allowedAction is implemented by backend.
  const roleCode = (user?.role || user?.roleCode || '').toUpperCase();
  const isDepartmentRole = roleCode === 'DEPARTMENT' || roleCode === 'DEPT';
  const canOpenContribution = (item: AssignedTask) =>
    import.meta.env.DEV && isDepartmentRole && !!item.visitInstanceId;

  // Propose change modal state
  const [proposingItem, setProposingItem] = useState<AssignedTask | null>(null);
  const [proposalNote, setProposalNote] = useState('');
  const [proposalStart, setProposalStart] = useState('');
  const [proposalEnd, setProposalEnd] = useState('');
  const [proposalLoading, setProposalLoading] = useState(false);
  const [actionLoadingId, setActionLoadingId] = useState<string | null>(null);
  const [submittedVisitRequestId, setSubmittedVisitRequestId] = useState<number | null>(null);
  const [selectedTask, setSelectedTask] = useState<AssignedTask | null>(null);

  const taskKey = (item: AssignedTask, action: string) => `${item.itemType}_${item.itemId}_${action}`;

  const handleAcceptItem = async (item: AssignedTask) => {
    const key = taskKey(item, 'accept');
    setActionLoadingId(key);
    try {
      if (item.itemType === 'INVITATION') {
        await departmentReceptionTasksApi.acceptInvitation(item.participantId || item.itemId);
        toast.success('Đã chấp nhận thư mời');
      } else {
        await departmentReceptionTasksApi.acceptAssignment(item.logisticsItemId || item.itemId);
        toast.success('Đã xác nhận nhận việc');
      }
      onRefresh();
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Thao tác thất bại');
    } finally {
      setActionLoadingId(null);
    }
  };

  const handleDeclineItem = async (item: AssignedTask) => {
    openDetail(item);
    return;
    const reason = '';
    if (!reason?.trim()) return;
    const key = taskKey(item, 'decline');
    setActionLoadingId(key);
    try {
      if (item.itemType === 'INVITATION') {
        await departmentReceptionTasksApi.declineInvitation(item.participantId || item.itemId, reason.trim());
        toast.success('Đã từ chối thư mời');
      } else {
        await departmentReceptionTasksApi.declineAssignment(item.logisticsItemId || item.itemId, reason.trim());
        toast.success('Đã từ chối nhiệm vụ');
      }
      onRefresh();
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Từ chối thất bại');
    } finally {
      setActionLoadingId(null);
    }
  };

  const handleSignItem = async (item: AssignedTask, handoverType: 'BORROW' | 'RETURN') => {
    openDetail(item);
    return;
    const note = '';
    const key = taskKey(item, handoverType);
    setActionLoadingId(key);
    try {
      await departmentReceptionTasksApi.signHandover(
        item.logisticsItemId || item.itemId,
        handoverType,
        handoverType === 'BORROW' ? 'PROVIDER' : 'BORROWER',
        note.trim(),
      );
      toast.success(handoverType === 'BORROW' ? 'Đã ký bàn giao' : 'Đã ký nghiệm thu');
      onRefresh();
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Ký biên bản thất bại');
    } finally {
      setActionLoadingId(null);
    }
  };

  const handlePropose = async () => {
    if (!proposingItem || !proposalNote.trim()) { toast.error('Vui lòng nhập ghi chú đề xuất'); return; }
    if (!proposalStart || !proposalEnd) { toast.error('Vui lòng chọn giờ bắt đầu và giờ kết thúc'); return; }
    if (proposalEnd <= proposalStart) {
      toast.error('Giờ kết thúc phải sau giờ bắt đầu');
      return;
    }
    const baseDate = (proposingItem.startAt || '').slice(0, 10);
    setProposalLoading(true);
    try {
      await departmentReceptionTasksApi.proposeChange(proposingItem.logisticsItemId || proposingItem.itemId, {
        proposedUsageStartAt: `${baseDate}T${proposalStart}:00`,
        proposedUsageEndAt: `${baseDate}T${proposalEnd}:00`,
        proposalNote: proposalNote.trim(),
        proposedDescription: proposalNote.trim(),
      });
      toast.success('Đã gửi đề xuất thay đổi');
      setProposingItem(null);
      onRefresh();
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Gửi đề xuất thất bại');
    } finally { setProposalLoading(false); }
  };

  const openDetail = (item: AssignedTask) => {
    setSelectedTask(item);
  };

  return (
    <div className="space-y-5">
      {/* Attention banner */}
      {attentionItems.length > 0 && (
        <div className="rounded-2xl border border-orange-200 bg-orange-50 p-4 shadow-sm">
          <div className="flex items-center gap-2 text-[#f37021] font-black text-sm mb-3">
            <AlertCircle className="w-4 h-4" />
            <span>Đơn/thư chưa xử lý ({attentionItems.length})</span>
          </div>
          <div className="space-y-2">
            {attentionItems.slice(0, 5).map(item => (
              <div key={`${item.itemType}_${item.itemId}`} className="flex items-center justify-between gap-3 bg-white/80 border border-orange-100 rounded-xl px-3 py-2">
                <div className="min-w-0">
                  <p className="text-xs font-black text-slate-800 truncate">{item.delegationName} - {item.title}</p>
                  <p className="text-[11px] text-orange-700 font-semibold">{item.attentionReason || item.statusLabel}</p>
                </div>
                <div className="flex items-center gap-1.5 shrink-0">
                  {item.canAccept && (
                    <button onClick={() => openDetail(item)} disabled={actionLoadingId === taskKey(item, 'accept')}
                      className="px-2.5 py-1.5 rounded-lg bg-emerald-600 text-white text-[11px] font-black hover:bg-emerald-700 disabled:opacity-50">
                      Nhận
                    </button>
                  )}
                  {item.canDecline && (
                    <button onClick={() => openDetail(item)} disabled={actionLoadingId === taskKey(item, 'decline')}
                      className="px-2.5 py-1.5 rounded-lg border border-rose-200 bg-white text-rose-600 text-[11px] font-black hover:bg-rose-50 disabled:opacity-50">
                      Từ chối
                    </button>
                  )}
                  {item.itemType === 'REQUEST' && item.canProposeChange && (
                    <button onClick={() => openDetail(item)}
                      className="px-2.5 py-1.5 rounded-lg border border-amber-200 bg-white text-amber-700 text-[11px] font-black hover:bg-amber-50">
                      Đề xuất
                    </button>
                  )}
                </div>
                <button onClick={() => openDetail(item)} className="px-3 py-1.5 rounded-lg border border-orange-200 bg-white text-[#f37021] text-[11px] font-black hover:bg-orange-50 shrink-0">
                  Xem chi tiết
                </button>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Main tasks panel */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
        {/* Filters bar */}
        <div className="bg-[#005594] px-6 py-4 flex flex-wrap items-center gap-3">
          <div className="relative flex-1 min-w-[200px]">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-white/60" />
            <input
              value={filter.search}
              onChange={e => onFilterChange({ search: e.target.value, page: 1 })}
              placeholder="Tìm kiếm nhiệm vụ, đoàn khách..."
              className="w-full pl-9 pr-4 py-2.5 bg-white/10 border border-white/20 rounded-xl text-sm font-semibold text-white placeholder:text-white/60 outline-none focus:bg-white/20"
            />
          </div>
          <select value={filter.itemType} onChange={e => onFilterChange({ itemType: e.target.value, page: 1 })}
            className="px-3 py-2.5 bg-white border border-white/20 rounded-xl text-sm font-bold text-slate-800">
            <option value="ALL">Tất cả loại</option>
            <option value="INVITATION">Thư mời</option>
            <option value="REQUEST">Đơn yêu cầu</option>
          </select>
          <select value={filter.status} onChange={e => onFilterChange({ status: e.target.value as TaskStatusFilter, page: 1 })}
            className="px-3 py-2.5 bg-white border border-white/20 rounded-xl text-sm font-bold text-slate-800">
            {STATUS_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
          </select>
          <input type="date" value={filter.fromDate} onChange={e => onFilterChange({ fromDate: e.target.value, page: 1 })}
            className="px-3 py-2.5 bg-white border border-white/20 rounded-xl text-sm font-bold text-slate-800" />
          <span className="text-white font-black">-</span>
          <input type="date" value={filter.toDate} onChange={e => onFilterChange({ toDate: e.target.value, page: 1 })}
            className="px-3 py-2.5 bg-white border border-white/20 rounded-xl text-sm font-bold text-slate-800" />
          <button onClick={() => onFilterChange({ sortDirection: filter.sortDirection === 'ASC' ? 'DESC' : 'ASC' })}
            className="px-3 py-2.5 bg-white border border-white/20 rounded-xl text-sm font-black text-[#004c91] hover:bg-blue-50">
            {filter.sortDirection === 'DESC' ? '↓ Mới nhất' : '↑ Cũ nhất'}
          </button>
        </div>

        {/* Table */}
        <div className="overflow-x-auto">
          <table className="w-full min-w-[900px] text-left">
            <thead className="bg-[#005594] text-white text-[11px] uppercase font-black">
              <tr>
                <th className="px-6 py-4">STT</th>
                <th className="px-5 py-4">Thông tin đoàn</th>
                <th className="px-5 py-4">Lịch tiếp</th>
                <th className="px-5 py-4">Trạng thái</th>
                <th className="px-5 py-4 text-center">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {tasksLoading && (
                <tr><td colSpan={5} className="py-12 text-center text-sm text-slate-400 font-semibold">Đang tải dữ liệu...</td></tr>
              )}
              {!tasksLoading && tasks.length === 0 && (
                <tr><td colSpan={5} className="py-12 text-center text-sm text-slate-400 font-semibold">Không có dữ liệu phù hợp</td></tr>
              )}
              {!tasksLoading && tasks.map((item, i) => (
                <tr key={`${item.itemType}_${item.itemId}`} className="hover:bg-slate-50/80 transition-colors">
                  <td className="px-6 py-5 text-sm font-black text-slate-500">{(filter.page - 1) * PAGE_SIZE + i + 1}</td>
                  <td className="px-5 py-5">
                    <p className="text-sm font-black text-slate-900 line-clamp-2">{item.title}</p>
                    <p className="text-[11px] text-slate-500 font-semibold mt-0.5">
                      {item.delegationName} · {item.itemType === 'INVITATION' ? 'Thư mời' : 'Đơn yêu cầu'}
                      {item.requestCode ? ` · ${item.requestCode}` : ''}
                    </p>
                    <div className="flex flex-wrap gap-1 mt-1.5">
                      {item.organizationName && <span className="text-[10px] bg-blue-50 text-blue-700 px-2 py-0.5 rounded-full border border-blue-100 font-semibold">{item.organizationName}</span>}
                    </div>
                  </td>
                  <td className="px-5 py-5 text-xs text-slate-600 font-semibold whitespace-nowrap">
                    <span className="block">Từ: {fmt(item.startAt)}</span>
                    <span className="block text-slate-400 mt-0.5">Đến: {fmt(item.endAt)}</span>
                  </td>
                  <td className="px-5 py-5 whitespace-nowrap">
                    <span className={`inline-flex whitespace-nowrap px-2.5 py-1 rounded-full border text-[11px] font-black ${STATUS_BADGE[item.uiStatus] || 'bg-slate-100 text-slate-600 border-slate-200'}`}>
                      {item.statusLabel || STATUS_LABEL[item.uiStatus] || item.uiStatus}
                    </span>
                  </td>
                  <td className="px-5 py-5 text-center">
                    <div className="flex items-center justify-center gap-2">
                      {/* Propose change – for REQUEST items */}
                      {item.canProposeChange && item.itemType === 'REQUEST' && (
                        <button onClick={() => openDetail(item)}
                          className="px-3 py-1.5 text-[11px] font-black text-amber-700 bg-amber-50 border border-amber-200 rounded-lg hover:bg-amber-100 transition-colors">
                          Đề xuất
                        </button>
                      )}
                      {item.canSignBorrow && item.itemType === 'REQUEST' && (
                        <button onClick={() => openDetail(item)} disabled={actionLoadingId === taskKey(item, 'BORROW')}
                          className="px-3 py-1.5 text-[11px] font-black text-orange-700 bg-orange-50 border border-orange-200 rounded-lg hover:bg-orange-100 transition-colors disabled:opacity-50"
                          title="Ký bàn giao">
                          <FileSignature className="w-3.5 h-3.5 inline mr-1" />BG
                        </button>
                      )}
                      {item.canSignReturn && item.itemType === 'REQUEST' && (
                        <button onClick={() => openDetail(item)} disabled={actionLoadingId === taskKey(item, 'RETURN')}
                          className="px-3 py-1.5 text-[11px] font-black text-blue-700 bg-blue-50 border border-blue-200 rounded-lg hover:bg-blue-100 transition-colors disabled:opacity-50"
                          title="Ký nghiệm thu">
                          <FileSignature className="w-3.5 h-3.5 inline mr-1" />NT
                        </button>
                      )}
                      {item.canAccept && (
                        <button onClick={() => openDetail(item)} disabled={actionLoadingId === taskKey(item, 'accept')}
                          className="w-9 h-9 rounded-full text-emerald-600 hover:bg-emerald-50 flex items-center justify-center transition-colors disabled:opacity-50"
                          title="Chấp nhận">
                          <CheckCircle2 className="w-5 h-5" />
                        </button>
                      )}
                      {item.canDecline && (
                        <button onClick={() => openDetail(item)} disabled={actionLoadingId === taskKey(item, 'decline')}
                          className="w-9 h-9 rounded-full text-rose-500 hover:bg-rose-50 flex items-center justify-center transition-colors disabled:opacity-50"
                          title="Từ chối">
                          <XCircle className="w-5 h-5" />
                        </button>
                      )}
                      {/* TEMP DEV TEST: Department contribution shortcut.
                          Keep the eye icon unchanged.
                          Remove this shortcut when OPEN_CONTRIBUTION allowedAction is implemented by backend. */}
                      {canOpenContribution(item) && (
                        <button
                          type="button"
                          title="Đóng góp kết quả chuyến thăm"
                          aria-label="Đóng góp kết quả chuyến thăm"
                          onClick={(e) => {
                            e.stopPropagation();
                            navigate(`/dashboard/visit/contribution/${item.visitInstanceId}`);
                          }}
                          className="w-9 h-9 rounded-full text-[#f37021] hover:bg-orange-50 flex items-center justify-center transition-colors">
                          <FileText className="w-5 h-5" />
                        </button>
                      )}
                      {/* Giữ nguyên icon mắt hiện tại */}
                      <button onClick={() => openDetail(item)}
                        className="w-9 h-9 rounded-full text-slate-400 hover:text-[#004c91] hover:bg-blue-50 flex items-center justify-center transition-colors"
                        title="Xem chi tiết">
                        <Eye className="w-5 h-5" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div className="flex flex-wrap items-center justify-between gap-3 px-6 py-4 border-t border-slate-100 text-xs font-bold text-slate-500">
          <span>{tasksLoading ? 'Đang tải...' : `${totalTasks} nhiệm vụ`}</span>
          <div className="flex items-center gap-2">
            <button onClick={() => onFilterChange({ page: Math.max(1, filter.page - 1) })} disabled={filter.page <= 1}
              className="px-3 py-2 bg-white border border-slate-200 rounded-xl text-xs font-black text-[#004c91] hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed">
              <ChevronLeft className="w-4 h-4" />
            </button>
            <span className="px-3 py-2 rounded-xl bg-[#004c91] text-white">{filter.page} / {totalPages}</span>
            <button onClick={() => onFilterChange({ page: Math.min(totalPages, filter.page + 1) })} disabled={filter.page >= totalPages}
              className="px-3 py-2 bg-white border border-slate-200 rounded-xl text-xs font-black text-[#004c91] hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed">
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      </div>

      {/* Propose change modal */}
      {proposingItem && (
        <div className="fixed inset-0 z-50 bg-slate-900/40 flex items-center justify-center p-4">
          <div className="w-full max-w-md bg-white rounded-2xl shadow-2xl border border-slate-200 overflow-hidden">
            <div className="px-5 py-4 border-b border-slate-100 flex items-center justify-between">
              <div>
                <h3 className="text-base font-black text-[#004c91]">Đề xuất thay đổi</h3>
                <p className="text-xs text-slate-500 mt-0.5 line-clamp-1">{proposingItem.title}</p>
              </div>
              <button onClick={() => setProposingItem(null)} className="w-8 h-8 rounded-full hover:bg-slate-100 flex items-center justify-center text-slate-400"><X className="w-4 h-4" /></button>
            </div>
            <div className="p-5 space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Thời gian bắt đầu mới</label>
                  <input type="time" value={proposalStart} onChange={e => setProposalStart(e.target.value)}
                    className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-blue-400" />
                </div>
                <div>
                  <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Thời gian kết thúc mới</label>
                  <input type="time" value={proposalEnd} onChange={e => setProposalEnd(e.target.value)}
                    className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-blue-400" />
                </div>
              </div>
              <div>
                <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Ghi chú đề xuất *</label>
                <textarea rows={3} value={proposalNote} onChange={e => setProposalNote(e.target.value)}
                  placeholder="Mô tả lý do và nội dung thay đổi đề xuất..."
                  className="w-full px-3 py-2 text-sm border border-slate-200 rounded-xl outline-none focus:border-blue-400 resize-none" />
              </div>
              <div className="flex gap-2 pt-1">
                <button onClick={handlePropose} disabled={proposalLoading || !proposalNote.trim() || !proposalStart || !proposalEnd}
                  className="flex-1 py-2.5 bg-amber-600 hover:bg-amber-700 text-white text-sm font-black rounded-xl transition-colors disabled:opacity-50">
                  {proposalLoading ? 'Đang gửi...' : 'Gửi đề xuất'}
                </button>
                <button onClick={() => setProposingItem(null)} className="px-4 py-2.5 text-sm font-bold text-slate-600 bg-slate-100 rounded-xl hover:bg-slate-200">Hủy</button>
              </div>
            </div>
          </div>
        </div>
      )}
      <StaffLeaderTaskModal
        item={selectedTask ? toTaskModalItem(selectedTask) : null}
        onClose={() => setSelectedTask(null)}
        onRefresh={onRefresh}
      />
      <SubmittedVisitRequestDetailModal
        isOpen={submittedVisitRequestId != null}
        visitRequestId={submittedVisitRequestId}
        onClose={() => setSubmittedVisitRequestId(null)}
      />
    </div>
  );
}

