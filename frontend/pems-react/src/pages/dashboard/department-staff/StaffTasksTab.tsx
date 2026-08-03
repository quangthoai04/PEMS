/**
 * StaffTasksTab – Tab nhiệm vụ được giao cho Department Staff
 * - Section "Đơn/thư chưa xử lý": list compact đầy đủ, action Nhận/Từ chối/Đề xuất trực tiếp trên dòng.
 * - Bảng lịch sử nhiệm vụ: đã chấp nhận, từ chối, đang đề xuất, đã hủy...
 */
import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, ChevronLeft, ChevronRight, Eye, CheckCircle2, XCircle, FileSignature, FileText } from 'lucide-react';
import toast from 'react-hot-toast';
import { SubmittedVisitRequestDetailModal } from '../../../components/modals/SubmittedVisitRequestDetailModal';
import { departmentReceptionTasksApi } from '../../../features/department-reception-tasks/api/departmentReceptionTasksApi';
import type { AssignedTask, TaskStatusFilter } from './useDeptStaffData';
import { StaffLeaderTaskModal, type StaffLeaderTaskModalItem } from './StaffLeaderTaskModal';
import { AssignedTaskList, taskKey } from './AssignedTaskList';
import { DeclineReasonModal } from './DeclineReasonModal';
import { ProposalModal } from './ProposalModal';
import { toVietnamDateTimeLocalInput, toVietnamCalendarDate } from '../../../shared/utils/vietnamTime';

interface Props {
  user: any;
  tasks: AssignedTask[];
  totalTasks: number;
  tasksLoading: boolean;
  attentionItems: AssignedTask[];
  filter: {
    search: string; itemType: string; status: TaskStatusFilter;
    fromDate: string; toDate: string; sortDirection: 'ASC' | 'DESC'; page: number; pageSize: number;
  };
  onFilterChange: (patch: Partial<Props['filter']>) => void;
  onRefresh: () => void;
}



const STATUS_OPTIONS: { value: string; label: string }[] = [
  { value: 'ALL', label: 'Tất cả trạng thái' },
  { value: 'ACCEPTED', label: 'Đã chấp nhận' },
  { value: 'REJECTED', label: 'Đã từ chối' },
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
  CHANGE_PROPOSED: 'Đang đề xuất', REJECTED: 'Đã từ chối',
};

function fmt(iso?: string) {
  if (!iso) return '—';
  const local = toVietnamDateTimeLocalInput(iso); // "YYYY-MM-DDTHH:mm" giờ VN
  if (!local) return '—';
  return `${local.slice(11, 16)} ${local.slice(8, 10)}/${local.slice(5, 7)}/${local.slice(0, 4)}`;
}

function toTaskModalItem(item: AssignedTask): StaffLeaderTaskModalItem {
  // Re-based: các getter local bên dưới trả đúng phần giờ Việt Nam.
  const start = item.startAt ? toVietnamCalendarDate(item.startAt) : null;
  const end = item.endAt ? toVietnamCalendarDate(item.endAt) : null;
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
    // The assignments feed DOES carry the real description, so there is no reason to fall back to the
    // title — doing so turned "no description yet" into a line that read like one, and the modal this
    // feeds then had nothing to tell the two cases apart.
    purpose: item.description || '',
    canAccept: item.canAccept,
    canDecline: item.canDecline,
    canProposeChange: item.canProposeChange,
    canSignBorrow: item.canSignBorrow,
    canSignReturn: item.canSignReturn,
  };
}

export function StaffTasksTab({ user, tasks, totalTasks, tasksLoading, attentionItems, filter, onFilterChange, onRefresh }: Props) {
  const PAGE_SIZE = filter.pageSize || 10;
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

  const [decliningItem, setDecliningItem] = useState<AssignedTask | null>(null);
  const [proposingItem, setProposingItem] = useState<AssignedTask | null>(null);
  const [actionLoadingId, setActionLoadingId] = useState<string | null>(null);
  const [submittedVisitRequestId, setSubmittedVisitRequestId] = useState<number | null>(null);
  const [selectedTask, setSelectedTask] = useState<AssignedTask | null>(null);

  // "Nhận" xử lý trực tiếp trên dòng, không cần mở modal chi tiết
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

  const openDetail = (item: AssignedTask) => {
    setSelectedTask(item);
  };

  return (
    <div className="space-y-0">
      {/* Đơn/thư chưa xử lý – list compact, list hết mọi item */}
      {attentionItems.length > 0 && (
        <div className="p-4 md:p-5 pb-4">
          <AssignedTaskList
            items={attentionItems}
            busyKey={actionLoadingId}
            onAccept={handleAcceptItem}
            onDecline={setDecliningItem}
            onPropose={setProposingItem}
            onOpenDetail={openDetail}
          />
        </div>
      )}

      {/* Main tasks panel */}
      <div className="bg-white border-t border-slate-200 overflow-hidden">
        {/* Filters bar */}
        <div className="bg-[#005594] px-4 py-3 flex flex-wrap items-center gap-2">
          <div className="relative flex-1 min-w-[200px]">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-white/60" />
            <input
              value={filter.search}
              onChange={e => onFilterChange({ search: e.target.value, page: 1 })}
              placeholder="Tìm kiếm nhiệm vụ, đoàn khách..."
              className="w-full pl-9 pr-4 py-2 bg-white/10 border border-white/20 rounded-xl text-sm font-semibold text-white placeholder:text-white/60 outline-none focus:bg-white/20"
            />
          </div>
          <select value={filter.itemType} onChange={e => onFilterChange({ itemType: e.target.value, page: 1 })}
            className="px-3 py-2 bg-white border border-white/20 rounded-xl text-sm font-bold text-slate-800">
            <option value="ALL">Tất cả loại</option>
            <option value="INVITATION">Thư mời</option>
            <option value="REQUEST">Đơn yêu cầu</option>
          </select>
          <select value={filter.status} onChange={e => onFilterChange({ status: e.target.value as TaskStatusFilter, page: 1 })}
            className="px-3 py-2 bg-white border border-white/20 rounded-xl text-sm font-bold text-slate-800">
            {STATUS_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
          </select>
          <input type="date" value={filter.fromDate} onChange={e => onFilterChange({ fromDate: e.target.value, page: 1 })}
            className="px-3 py-2 bg-white border border-white/20 rounded-xl text-sm font-bold text-slate-800" />
          <span className="text-white font-black">-</span>
          <input type="date" value={filter.toDate} onChange={e => onFilterChange({ toDate: e.target.value, page: 1 })}
            className="px-3 py-2 bg-white border border-white/20 rounded-xl text-sm font-bold text-slate-800" />
          <button onClick={() => onFilterChange({ sortDirection: filter.sortDirection === 'ASC' ? 'DESC' : 'ASC' })}
            className="px-3 py-2 bg-white border border-white/20 rounded-xl text-sm font-black text-[#004c91] hover:bg-blue-50">
            {filter.sortDirection === 'DESC' ? '↓ Mới nhất' : '↑ Cũ nhất'}
          </button>
        </div>

        {/* Table */}
        <div className="overflow-x-auto">
          <table className="w-full min-w-[900px] text-left">
            <thead className="bg-[#005594] text-white text-[11px] uppercase font-black">
              <tr>
                <th className="px-4 py-3">STT</th>
                <th className="px-4 py-3">Thông tin đoàn</th>
                <th className="px-4 py-3">Lịch tiếp</th>
                <th className="px-4 py-3">Trạng thái</th>
                <th className="px-4 py-3 text-center">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {tasksLoading && (
                <tr><td colSpan={5} className="py-10 text-center text-sm text-slate-400 font-semibold">Đang tải dữ liệu...</td></tr>
              )}
              {!tasksLoading && tasks.length === 0 && (
                <tr><td colSpan={5} className="py-10 text-center text-sm text-slate-400 font-semibold">Không có dữ liệu phù hợp</td></tr>
              )}
              {!tasksLoading && tasks.map((item, i) => (
                <tr key={`${item.itemType}_${item.itemId}`} className="hover:bg-slate-50/80 transition-colors">
                  <td className="px-4 py-3 text-sm font-black text-slate-500">{(filter.page - 1) * PAGE_SIZE + i + 1}</td>
                  <td className="px-4 py-3">
                    <p className="text-sm font-black text-slate-900 line-clamp-2">{item.title}</p>
                    <p className="text-[11px] text-slate-500 font-semibold mt-0.5">
                      {item.delegationName} · {item.itemType === 'INVITATION' ? 'Thư mời' : 'Đơn yêu cầu'}
                      {item.requestCode ? ` · ${item.requestCode}` : ''}
                      {item.organizationName ? ` · ${item.organizationName}` : ''}
                    </p>
                  </td>
                  <td className="px-4 py-3 text-xs text-slate-600 font-semibold whitespace-nowrap">
                    <span className="block">Từ: {fmt(item.startAt)}</span>
                    <span className="block text-slate-400 mt-0.5">Đến: {fmt(item.endAt)}</span>
                  </td>
                  <td className="px-4 py-3 whitespace-nowrap">
                    <span className={`inline-flex whitespace-nowrap px-2.5 py-1 rounded-full border text-[11px] font-black ${STATUS_BADGE[item.uiStatus] || 'bg-slate-100 text-slate-600 border-slate-200'}`}>
                      {item.uiStatus === 'DECLINED' || item.uiStatus === 'REJECTED' ? 'Từ chối' : (item.statusLabel || STATUS_LABEL[item.uiStatus] || item.uiStatus)}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <div className="flex items-center justify-center gap-1.5">
                      {/* Propose change – mở thẳng form đề xuất */}
                      {item.canProposeChange && item.itemType === 'REQUEST' && (
                        <button onClick={() => setProposingItem(item)}
                          className="px-3 py-1.5 text-[11px] font-black text-amber-700 bg-amber-50 border border-amber-200 rounded-lg hover:bg-amber-100 transition-colors">
                          Đề xuất
                        </button>
                      )}
                      {/* Ký biên bản – thực hiện trong modal chi tiết (biên bản bàn giao/nghiệm thu) */}
                      {item.canSignBorrow && item.itemType === 'REQUEST' && (
                        <button onClick={() => openDetail(item)}
                          className="px-3 py-1.5 text-[11px] font-black text-orange-700 bg-orange-50 border border-orange-200 rounded-lg hover:bg-orange-100 transition-colors"
                          title="Ký bàn giao">
                          <FileSignature className="w-3.5 h-3.5 inline mr-1" />BG
                        </button>
                      )}
                      {item.canSignReturn && item.itemType === 'REQUEST' && (
                        <button onClick={() => openDetail(item)}
                          className="px-3 py-1.5 text-[11px] font-black text-blue-700 bg-blue-50 border border-blue-200 rounded-lg hover:bg-blue-100 transition-colors"
                          title="Ký nghiệm thu">
                          <FileSignature className="w-3.5 h-3.5 inline mr-1" />NT
                        </button>
                      )}
                      {/* Nhận – xử lý trực tiếp */}
                      {item.canAccept && (
                        <button onClick={() => handleAcceptItem(item)} disabled={actionLoadingId === taskKey(item, 'accept')}
                          className="w-8 h-8 rounded-full text-emerald-600 hover:bg-emerald-50 flex items-center justify-center transition-colors disabled:opacity-50"
                          title="Chấp nhận">
                          <CheckCircle2 className="w-5 h-5" />
                        </button>
                      )}
                      {/* Từ chối – mở modal nhập lý do */}
                      {item.canDecline && (
                        <button onClick={() => setDecliningItem(item)} disabled={actionLoadingId === taskKey(item, 'decline')}
                          className="w-8 h-8 rounded-full text-rose-500 hover:bg-rose-50 flex items-center justify-center transition-colors disabled:opacity-50"
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
                          className="w-8 h-8 rounded-full text-[#f37021] hover:bg-orange-50 flex items-center justify-center transition-colors">
                          <FileText className="w-5 h-5" />
                        </button>
                      )}
                      {/* Giữ nguyên icon mắt hiện tại */}
                      <button onClick={() => openDetail(item)}
                        className="w-8 h-8 rounded-full text-slate-400 hover:text-[#004c91] hover:bg-blue-50 flex items-center justify-center transition-colors"
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

        {/* Pagination & Page Size */}
        <div className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 border-t border-slate-100 text-xs font-bold text-slate-500">
          <div className="flex items-center gap-3">
            <span className="text-sm text-slate-500 font-semibold">Hiển thị:</span>
            <select
              value={filter.pageSize}
              onChange={e => onFilterChange({ pageSize: Number(e.target.value), page: 1 })}
              className="px-2 py-1 bg-slate-50 border border-slate-200 rounded text-sm font-bold text-slate-700 outline-none hover:bg-slate-100"
            >
              <option value={5}>5</option>
              <option value={10}>10</option>
              <option value={20}>20</option>
              <option value={50}>50</option>
            </select>
            <span>{tasksLoading ? 'Đang tải...' : `${totalTasks} nhiệm vụ`}</span>
          </div>
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

      {/* Từ chối – modal nhập lý do, mở trực tiếp từ dòng item */}
      <DeclineReasonModal
        item={decliningItem}
        onClose={() => setDecliningItem(null)}
        onSuccess={onRefresh}
      />
      {/* Đề xuất – mở thẳng form, không qua modal chi tiết */}
      <ProposalModal
        item={proposingItem}
        onClose={() => setProposingItem(null)}
        onSuccess={onRefresh}
      />
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
