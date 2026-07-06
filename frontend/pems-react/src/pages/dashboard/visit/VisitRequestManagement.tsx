/**
 * VisitRequestManagement — màn quản lý đơn tiếp khách theo vai trò.
 *
 * Dữ liệu được backend lọc theo role/scope (UC-20) và trả kèm `allowedActions`;
 * frontend chỉ render UI + nút theo danh sách đó, mọi thao tác đều được backend
 * validate lại. Hai tab: "Đơn phụ trách" (responsible) và "Đơn mời tham dự"
 * (attending). Visitor chỉ thấy "Đơn của tôi"; Admin không tham gia luồng này.
 */

import React, { Fragment, useEffect, useState } from 'react';
import {
  Search, Plus, Eye, AlertCircle, Users, MapPin, Calendar,
  ChevronLeft, ChevronRight, ChevronDown, Check, X, XCircle, Mail,
  FileText, ArrowRightCircle, Info, ClipboardList, Star, CheckCircle2,
  PencilLine, MailOpen, RefreshCw,
} from 'lucide-react';
import { motion } from 'motion/react';
import { useNavigate, useSearchParams, useLocation } from 'react-router-dom';
import { SubmittedVisitRequestDetailModal } from '../../../components/modals/SubmittedVisitRequestDetailModal';
import { AssignHostModal } from '../../../components/modals/AssignHostModal';
import { CancellationReasonModal } from '../../../features/delegations/components/CancellationReasonModal';
import { RejectedReasonModal } from '../../../features/delegations/components/RejectedReasonModal';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';
import {
  VISIT_SCOPE_LABELS,
  PARTICIPANT_ROLE_LABELS,
  type AllowedAction,
  type VisitRequestManagementItem,
  type VisitInvitation,
  type CampusProgressItem,
} from '../../../features/delegations/types/delegations.types';
import { useAuthContext } from '../../../shared/auth/AuthContext';
import { getVisitRequestFilterConfig } from '../../../features/delegations/config/visitRequestFilterConfig';
import { visitFeedbackApi } from '../../../features/feedbacks/api/visitFeedbackApi';
import { VisitFeedbackModal } from '../../../features/feedbacks/components/VisitFeedbackModal';
import type { PendingFeedbackItem } from '../../../features/feedbacks/types/visitFeedback.types';

type Tab = 'responsible' | 'attending';

type ActionTone = 'blue' | 'green' | 'red' | 'gray' | 'orange';

// Lightweight in-page toast (cùng pattern với CampusManagement — không thêm thư viện mới).
type Toast = { id: number; type: 'success' | 'error'; msg: string };

const ActionIconButton = ({
  title, icon, tone = 'blue', onClick, label,
}: {
  title: string;
  icon: React.ReactNode;
  tone?: ActionTone;
  onClick: (e: React.MouseEvent<HTMLButtonElement>) => void;
  label?: string;
}) => {
  const toneClassMap: Record<ActionTone, string> = {
    blue: 'text-slate-500 hover:text-[#004c91] hover:bg-blue-50 border-transparent hover:border-blue-100 lg:border-none lg:bg-transparent',
    green: 'text-green-500 hover:text-green-600 hover:bg-green-50 border-transparent hover:border-green-100 lg:border-none lg:bg-transparent',
    red: 'text-red-500 hover:text-red-600 hover:bg-red-50 border-transparent hover:border-red-100 lg:border-none lg:bg-transparent',
    gray: 'text-slate-400 hover:text-slate-600 hover:bg-slate-100 border-transparent hover:border-slate-200 lg:border-none lg:bg-transparent',
    orange: 'text-orange-500 hover:text-orange-600 hover:bg-orange-50 border-transparent hover:border-orange-100 lg:border-none lg:bg-transparent',
  };
  return (
    <button
      type="button"
      title={title}
      aria-label={title}
      onClick={onClick}
      className={`inline-flex h-9 ${label ? 'px-2 w-auto lg:w-9 lg:px-0 bg-white border border-slate-200 shadow-sm lg:shadow-none lg:border-transparent' : 'w-9'} items-center justify-center gap-1.5 rounded-lg transition-colors outline-none cursor-pointer ${toneClassMap[tone]}`}
    >
      {icon}
      {label && <span className="text-[11px] font-bold lg:hidden whitespace-nowrap">{label}</span>}
    </button>
  );
};

// Map (requestStatus, campusStatus) → nhãn tiếng Việt (campus-independent approval:
// mỗi campus có trạng thái/quyết định riêng; requestStatus chỉ là aggregate).
const getVietnameseStatus = (reqStatus?: string | null, campStatus?: string | null) => {
  if (campStatus === 'CANCELLED' || reqStatus === 'CANCELLED') return 'Đã hủy';
  if (campStatus === 'REJECTED') return 'Từ chối';
  if (campStatus === 'WAITING_REQUEST_APPROVAL') return 'Chờ xử lý tại cơ sở';
  if (campStatus === 'ASSIGNED') return 'Đã duyệt & gán Host';
  if (campStatus === 'BEFORE_VISIT') return 'Trước tiếp khách';
  if (campStatus === 'DURING_VISIT') return 'Trong tiếp khách';
  if (campStatus === 'AFTER_VISIT') return 'Chờ đóng đoàn';
  if (campStatus === 'CLOSED') return 'Đã đóng đoàn';
  // Request-level rows (không có campusStatus): dùng aggregate.
  if (reqStatus === 'REJECTED') return 'Từ chối';
  if (reqStatus === 'PENDING_APPROVAL') return 'Chờ xử lý';
  if (reqStatus === 'PARTIALLY_APPROVED') return 'Duyệt một phần';
  if (reqStatus === 'APPROVED') return 'Đã duyệt';
  return reqStatus ?? '-';
};

// Map campus instanceStatus CODE → nhãn tiếng Việt + class badge (dùng cho accordion liên cơ sở).
// Chỉ để render hiển thị; KHÔNG dùng để gate action (action lấy từ boolean backend trả về).
const CAMPUS_STATUS_LABELS: Record<string, string> = {
  WAITING_REQUEST_APPROVAL: 'Chờ xử lý tại cơ sở',
  ASSIGNED: 'Đã duyệt & gán Host',
  BEFORE_VISIT: 'Trước tiếp khách',
  DURING_VISIT: 'Đang tiếp khách',
  AFTER_VISIT: 'Chờ đóng đoàn',
  CLOSED: 'Đã đóng đoàn',
  CANCELLED: 'Đã hủy',
  REJECTED: 'Từ chối',
};
const getCampusStatusLabel = (status?: string | null) => (status && CAMPUS_STATUS_LABELS[status]) || status || '-';
const getCampusStatusBadgeClass = (status?: string | null) => {
  switch (status) {
    case 'WAITING_REQUEST_APPROVAL': return 'bg-yellow-50 text-yellow-700 border-yellow-200';
    case 'ASSIGNED': return 'bg-cyan-50 text-cyan-700 border-cyan-200';
    case 'BEFORE_VISIT': return 'bg-blue-50 text-blue-700 border-blue-200';
    case 'DURING_VISIT': return 'bg-green-50 text-green-700 border-green-200';
    case 'AFTER_VISIT': return 'bg-orange-50 text-orange-700 border-orange-200';
    case 'CLOSED': return 'bg-slate-100 text-slate-700 border-slate-300';
    case 'CANCELLED': return 'bg-gray-100 text-gray-600 border-gray-200';
    case 'REJECTED': return 'bg-red-50 text-red-700 border-red-200';
    default: return 'bg-gray-100 text-gray-700 border-gray-200';
  }
};

// STATUS_FILTER_OPTIONS and VISIT_SCOPE_OPTIONS are dynamically generated by getVisitRequestFilterConfig

type Row = VisitRequestManagementItem & {
  id: number;
  name: string;
  org: string;
  campus: string;
  host: string;
  sender: string;
  time: string;
  statusText: string;
};

export function VisitRequestManagement({ isEmbedded = false }: { isEmbedded?: boolean } = {}) {
  const navigate = useNavigate();
  const navTo = (path: string, options?: any) => {
    navigate(path, {
      ...options,
      state: {
        ...options?.state,
        returnTo: location.pathname + location.search,
      }
    });
  };
  const { user } = useAuthContext();

  const roleCode = (user?.roleCode || '').toUpperCase();
  const subRole = (user?.subRole || '').toUpperCase();
  const isAdmin = roleCode === 'ADMIN';
  const isHO = roleCode === 'HO';
  const isStaff = roleCode === 'STAFF';
  const isStaffLeader = isStaff && subRole === 'LEADER';
  const isRegularStaff = isStaff && subRole === 'STAFF';
  const isVisitor = roleCode === 'VISITOR';
  const isDept = roleCode === 'DEPARTMENT' || roleCode === 'DEPT';
  const isStudent = roleCode === 'STUDENT';

  // The "Đơn mời tham dự" (attending) tab is only for users who can be invited as a
  const canUseAttendingTab = isRegularStaff || isDept || isStudent;
  const canUseResponsibleTab = !isStudent && !isDept && !isAdmin && !isVisitor;
  const showTabs = canUseAttendingTab || canUseResponsibleTab;
  
  const responsibleTabLabel = isHO ? 'Theo dõi đơn tiếp khách' : 'Đơn phụ trách';
  const attendingTabLabel = (isDept && subRole === 'STAFF') ? 'Nhiệm vụ được giao' : 'Lời mời tham dự';
  
  const [searchParams, setSearchParams] = useSearchParams();
  const location = useLocation();

  const defaultTab: Tab = (searchParams.get('tab') as Tab) || ((isStudent || isDept) ? 'attending' : 'responsible');
  const [activeTab, setActiveTab] = useState<Tab>(defaultTab);

  // UC-27: pending participation invitations for invitee roles. This banner is the entry
  // point to the invitation-detail screen, where Accept/Decline happens — never in the
  // attending tab (which only lists already-ACCEPTED invitations and is read-only).
  const [pendingInvitations, setPendingInvitations] = useState<VisitInvitation[]>([]);

  const filterConfig = getVisitRequestFilterConfig({
    roleCode,
    subRole,
    activeTab,
    isVisitor,
  });

  // Desktop (xl) single-row grid template — column count MUST match the rendered filter
  // children, which varies by role: showScope (Visitor/HO/Staff Leader) and showCampus (HO
  // only) toggle extra columns. Static literal strings so Tailwind JIT picks them up. Search
  // uses minmax(0,1fr) so it absorbs leftover space yet shrinks (never forces horizontal
  // scroll) on the tight 1366px + 290px-sidebar layout; long values truncate via span/truncate.
  const filterGridClass = filterConfig.showCampus
    ? 'xl:grid-cols-[minmax(0,1fr)_200px_150px_160px_185px_112px_44px]' // search·status·scope·campus·date·apply·reset
    : filterConfig.showScope
      ? 'xl:grid-cols-[minmax(0,1fr)_210px_160px_200px_112px_44px]'     // search·status·scope·date·apply·reset
      : 'xl:grid-cols-[minmax(0,1fr)_220px_200px_112px_44px]';          // search·status·date·apply·reset

  const getUrlFilters = () => ({
    keyword: searchParams.get('keyword') || '',
    status: searchParams.get('status') || '',
    visitScope: searchParams.get('visitScope') || '',
    relation: searchParams.get('relation') || '',
    fromDate: searchParams.get('fromDate') || '',
    toDate: searchParams.get('toDate') || '',
    campusId: searchParams.get('campusId') || ''
  });

  const createEmptyFilters = () => ({ keyword: '', status: '', visitScope: '', relation: '', fromDate: '', toDate: '', campusId: '' });
  const [draftFilters, setDraftFilters] = useState(getUrlFilters());
  const [appliedFilters, setAppliedFilters] = useState(getUrlFilters());
  const [filterError, setFilterError] = useState<string | null>(null);
  const [listError, setListError] = useState<string | null>(null);

  const [isTypeFilterOpen, setIsTypeFilterOpen] = useState(false);
  const [isRelationFilterOpen, setIsRelationFilterOpen] = useState(false);
  const [isStatusFilterOpen, setIsStatusFilterOpen] = useState(false);
  const [isCampusFilterOpen, setIsCampusFilterOpen] = useState(false);
  const [isDateFilterOpen, setIsDateFilterOpen] = useState(false);

  const [rows, setRows] = useState<Row[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [total, setTotal] = useState(0);
  const [currentPage, setCurrentPage] = useState(Number(searchParams.get('page')) || 1);
  const [pageSize, setPageSize] = useState(Number(searchParams.get('pageSize')) || 10);
  const [sortOrder, setSortOrder] = useState<'desc' | 'asc'>((searchParams.get('sortOrder') as 'desc' | 'asc') || 'desc');
  const [debouncedKeyword, setDebouncedKeyword] = useState(draftFilters.keyword);
  const [summaryStats, setSummaryStats] = useState<any>(null);

  const updateUrlParams = (tab: Tab, page: number, size: number, filters: typeof appliedFilters, sort: string) => {
    const params = new URLSearchParams(searchParams);
    if (tab) params.set('tab', tab);
    if (page > 1) params.set('page', page.toString()); else params.delete('page');
    if (size !== 10) params.set('pageSize', size.toString()); else params.delete('pageSize');
    if (sort !== 'desc') params.set('sortOrder', sort); else params.delete('sortOrder');
    
    if (filters.keyword) params.set('keyword', filters.keyword); else params.delete('keyword');
    if (filters.status) params.set('status', filters.status); else params.delete('status');
    if (filters.visitScope) params.set('visitScope', filters.visitScope); else params.delete('visitScope');
    if (filters.relation) params.set('relation', filters.relation); else params.delete('relation');
    if (filters.fromDate) params.set('fromDate', filters.fromDate); else params.delete('fromDate');
    if (filters.toDate) params.set('toDate', filters.toDate); else params.delete('toDate');
    if (filters.campusId) params.set('campusId', filters.campusId); else params.delete('campusId');
    
    setSearchParams(params, { replace: true });
  };

  const applyFilterChange = (updates: Partial<typeof draftFilters>) => {
    const newFilters = { ...draftFilters, ...updates };
    if (newFilters.fromDate && newFilters.toDate && newFilters.fromDate > newFilters.toDate) {
      setFilterError('Từ ngày không được lớn hơn Đến ngày.');
      setDraftFilters(newFilters);
      return;
    }
    setFilterError(null);
    setDraftFilters(newFilters);
    setAppliedFilters(newFilters);
    setCurrentPage(1);
    updateUrlParams(activeTab, 1, pageSize, newFilters, sortOrder);
    loadDelegations(activeTab, 1, pageSize, newFilters, sortOrder);
  };

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedKeyword(draftFilters.keyword.trim());
    }, 400);
    return () => clearTimeout(timer);
  }, [draftFilters.keyword]);

  useEffect(() => {
    if (debouncedKeyword !== appliedFilters.keyword) {
      applyFilterChange({ keyword: debouncedKeyword });
    }
  }, [debouncedKeyword]);

  // Modals
  const [requestForm, setRequestForm] = useState<{ open: boolean; row: Row | null }>({ open: false, row: null });
  const openRequestForm = (row: Row) => setRequestForm({ open: true, row });
  // "Xem đơn đăng ký tham quan trước khi duyệt" — read-only review of a PENDING_APPROVAL row.
  const [review, setReview] = useState<{ open: boolean; row: Row | null }>({ open: false, row: null });
  const [reason, setReason] = useState<{ open: boolean; row: Row | null }>({ open: false, row: null });
  // UC-136: read-only popup of the cancellation reason (Host / Visitor / Staff Leader / HO).
  const [cancelReason, setCancelReason] = useState<{ open: boolean; row: Row | null }>({ open: false, row: null });
  const [reject, setReject] = useState<{ open: boolean; row: Row | null; action: AllowedAction | null; text: string; submitting: boolean; error: string | null }>({ open: false, row: null, action: null, text: '', submitting: false, error: null });
  const [cancel, setCancel] = useState<{ open: boolean; row: Row | null; mode: 'visitor' | 'host' | null; instanceId?: number | null; text: string; submitting: boolean; error: string | null; confirmed: boolean }>({ open: false, row: null, mode: null, instanceId: null, text: '', submitting: false, error: null, confirmed: false });
  const [assign, setAssign] = useState<{ open: boolean; row: Row | null; mode: 'approve' }>({ open: false, row: null, mode: 'approve' });

  // ── Toasts (success/failure notification cho approve/reject/cancel/assign host) ──
  const [toasts, setToasts] = useState<Toast[]>([]);
  const pushToast = (type: Toast['type'], msg: string) => {
    const id = Date.now() + Math.floor(Math.random() * 1000);
    setToasts((prev) => [...prev, { id, type, msg }]);
    setTimeout(() => setToasts((prev) => prev.filter((t) => t.id !== id)), 4500);
  };
  // Lấy message lỗi nghiệp vụ thật từ backend (400/403/404/409/422). Ưu tiên message → error →
  // errors (mảng/đối tượng từ FluentValidation) → title; chỉ fallback chung khi không có gì.
  const apiErrorMessage = (e: any, fallback: string): string => {
    const data = e?.response?.data;
    if (!data) return fallback;
    if (typeof data === 'string' && data.trim()) return data;
    if (data.message) return data.message;
    if (data.error) return data.error;
    if (data.errors) {
      const flat = Array.isArray(data.errors) ? data.errors : Object.values(data.errors).flat();
      const first = (flat as any[]).find((x) => typeof x === 'string' && x.trim());
      if (first) return first;
    }
    if (data.title) return data.title;
    return fallback;
  };

  // Feedback rule mới: map visitInstanceId → trạng thái đánh giá của user hiện tại.
  // Backend trả các instance user là Visitor/Host và đang DURING_VISIT/AFTER_VISIT/CLOSED;
  // dùng để hiện nút "Đánh giá" hoặc badge "Đã đánh giá" ở cột hành động.
  const [feedbackByInstance, setFeedbackByInstance] = useState<Record<number, PendingFeedbackItem>>({});
  useEffect(() => {
    if (isAdmin || isHO || isDept || isStudent) return; // chỉ Visitor & Staff (host) có mục đánh giá
    let cancelled = false;
    (async () => {
      try {
        const res = await visitFeedbackApi.getMyPending();
        if (cancelled) return;
        const map: Record<number, PendingFeedbackItem> = {};
        for (const it of res.items || []) map[it.visitInstanceId] = it;
        setFeedbackByInstance(map);
      } catch { /* im lặng — không chặn danh sách */ }
    })();
    return () => { cancelled = true; };
  }, [isAdmin, isHO, isDept, isStudent]);

  // Modal đánh giá mở ngay trên danh sách (không chuyển route). Sau khi gửi thành công,
  // đổi row sang "Đã đánh giá" tại chỗ.
  const [feedbackModalInstanceId, setFeedbackModalInstanceId] = useState<number | null>(null);
  const handleFeedbackSubmitted = (instanceId: number) => {
    setFeedbackByInstance((prev) =>
      prev[instanceId] ? { ...prev, [instanceId]: { ...prev[instanceId], alreadySubmitted: true } } : prev,
    );
  };

  // Phương án A: đơn liên cơ sở mở rộng để xem tiến trình từng campus. Mở tối đa 1 row tại 1 thời điểm.
  const [expandedRequestId, setExpandedRequestId] = useState<number | null>(null);
  const toggleExpanded = (visitRequestId: number) =>
    setExpandedRequestId((current) => (current === visitRequestId ? null : visitRequestId));

  const formatDateOnly = (dateStr: string) => {
    if (!dateStr) return '';
    const d = new Date(dateStr);
    return `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`;
  };
  const formatDateTimeShort = (value?: string | null) => {
    if (!value) return '-';
    return new Date(value).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
  };

  const handleApplyFilters = () => {
    applyFilterChange({});
  };
  const handleResetFilters = () => {
    const empty = createEmptyFilters();
    setDraftFilters(empty);
    setAppliedFilters(empty);
    setFilterError(null);
    setCurrentPage(1);
    setDebouncedKeyword('');
    updateUrlParams(activeTab, 1, pageSize, empty, sortOrder);
    loadDelegations(activeTab, 1, pageSize, empty, sortOrder);
  };

  const loadDelegations = async (targetTab: Tab, targetPage: number, targetSize: number, targetFilters: typeof appliedFilters, targetSort: string = sortOrder) => {
    if (isAdmin) return;
    try {
      setIsLoading(true);
      setListError(null);
      const effectiveTab = isVisitor ? 'responsible' : (isStudent || isDept) ? 'attending' : targetTab;
      const params: Record<string, unknown> = {
        tab: effectiveTab,
        page: targetPage,
        pageSize: targetSize,
        sortBy: 'plannedStartAt',
        sortOrder: targetSort,
      };
      const keyword = targetFilters.keyword.trim();
      if (keyword) params.keyword = keyword;
      
      if (targetFilters.status) {
        const option = filterConfig.statusOptions.find((o) => o.value === targetFilters.status);
        if (option?.cancelledOnly) params.cancelledOnly = true;
        if (option?.requestStatus) params.requestStatus = option.requestStatus;
        if (option?.campusStatus) params.campusStatus = option.campusStatus;
        if (option?.visitScope) params.visitScope = option.visitScope;
        if (option?.readOnlyOnly) params.readOnlyOnly = true;
        if (option?.actionableOnly) params.actionableOnly = true;
        if (option?.timing) params.timing = option.timing;
        if (option?.relation) params.relation = option.relation;
      }
      
      if (filterConfig.showScope && targetFilters.visitScope) {
        if (!params.visitScope) params.visitScope = targetFilters.visitScope;
      }
      
      if (filterConfig.showRelation && targetFilters.relation) {
        if (targetFilters.relation === 'READ_ONLY') params.readOnlyOnly = true;
        else if (targetFilters.relation === 'ACTION_REQUIRED') params.actionableOnly = true;
        else params.relation = targetFilters.relation;
      }
      
      if (targetFilters.fromDate) params.fromDate = targetFilters.fromDate;
      if (targetFilters.toDate) params.toDate = targetFilters.toDate;
      if (targetFilters.campusId) params.campusId = targetFilters.campusId;

      if (effectiveTab === 'attending') {
        const invParams: Record<string, unknown> = {
          page: targetPage,
          pageSize: targetSize,
          includeResponded: true,
        };
        const keyword = targetFilters.keyword.trim();
        if (keyword) invParams.keyword = keyword;
        if (targetFilters.status) invParams.invitationStatus = targetFilters.status;
        if (targetFilters.fromDate) invParams.fromDate = targetFilters.fromDate;
        if (targetFilters.toDate) invParams.toDate = targetFilters.toDate;

        const response = await delegationsApi.visitInvitations.getMyInvitations(invParams);
        
        if (response.summary) {
           setSummaryStats(response.summary);
        } else {
           const items = response.items || [];
           const sum = { total: 0, pending: 0, accepted: 0, declined: 0, cancelledOrExpired: 0 };
           items.forEach((it: any) => {
              if (it.invitationStatus === 'REMOVED') return;
              sum.total++;
              if (it.invitationStatus === 'INVITED') sum.pending++;
              if (it.invitationStatus === 'ACCEPTED' || it.invitationStatus === 'ASSIGNED') sum.accepted++;
              if (it.invitationStatus === 'DECLINED') sum.declined++;
              if (it.requestStatus === 'CANCELLED' || it.campusStatus === 'CANCELLED' || it.requestStatus === 'REJECTED') sum.cancelledOrExpired++;
           });
           setSummaryStats({ ...sum, _isLocal: true });
        }

        const isDepartmentLeader = isDept && subRole === 'LEADER';
        
        const items: any[] = (response.items || []).filter((it: any) => {
          if (it.invitationStatus === 'REMOVED') return false;
          if (it.invitationStatus === 'ASSIGNED') {
            const isInvalidAssignedInvitation = isStudent || isRegularStaff || isDepartmentLeader;
            if (isInvalidAssignedInvitation) {
              console.warn(`[Invalid Data] Role cannot have ASSIGNED status. Row ignored: ${it.visitInstanceId || it.visitRequestId}`);
              return false;
            }
          }
          return true;
        });
        const mapped: Row[] = items.map((item) => {
          let statusText = item.invitationStatus;
          if (statusText === 'INVITED') statusText = 'Chờ phản hồi';
          else if (statusText === 'ACCEPTED') statusText = 'Đã nhận lời';
          else if (statusText === 'ASSIGNED') statusText = 'Được giao nhiệm vụ';
          else if (statusText === 'DECLINED') statusText = 'Đã từ chối';

          return {
            ...item,
            id: item.visitInstanceId || item.visitRequestId,
            name: item.delegationName || 'Không có tên',
            org: item.invitedByName ? `Mời bởi: ${item.invitedByName} · ${PARTICIPANT_ROLE_LABELS[item.participantRole] ?? item.participantRole} · ${item.campusName || '-'}` : 'Người mời: -',
            campus: item.campusName || '-',
            host: '-',
            sender: '-',
            time: formatDateTimeShort(item.plannedStartAt),
            statusText,
          };
        });
        setRows(mapped);
        setTotal(response.totalItems || 0);
      } else {
        const response = await delegationsApi.getVisitRequestManagementList(params);
        
        if (response.summary) {
           setSummaryStats(response.summary);
        } else {
           const items = response.items || [];
           // Campus-independent approval: đếm theo trạng thái từng campus instance,
           // không gate theo requestStatus === 'APPROVED' (PARTIALLY_APPROVED vẫn có instance sống).
           const sum = {
             total: items.length,
             pendingApproval: items.filter((x: any) => x.campusStatus === 'WAITING_REQUEST_APPROVAL'
               || (!x.campusStatus && x.requestStatus === 'PENDING_APPROVAL')).length,
             waitingHost: 0, // không còn trạng thái WAITING_HOST_ASSIGNMENT
             assigned: items.filter((x: any) => x.campusStatus === 'ASSIGNED').length,
             before: items.filter((x: any) => x.campusStatus === 'BEFORE_VISIT').length,
             during: items.filter((x: any) => x.campusStatus === 'DURING_VISIT').length,
             after: items.filter((x: any) => x.campusStatus === 'AFTER_VISIT').length,
             closed: items.filter((x: any) => x.campusStatus === 'CLOSED').length,
             cancelled: items.filter((x: any) => x.requestStatus === 'CANCELLED' || x.campusStatus === 'CANCELLED').length,
             rejected: items.filter((x: any) => x.campusStatus === 'REJECTED'
               || (!x.campusStatus && x.requestStatus === 'REJECTED')).length,
             interCampusPending: items.filter((x: any) => x.visitScope === 'MULTI_CAMPUS'
               && (x.requestStatus === 'PENDING_APPROVAL' || x.requestStatus === 'PARTIALLY_APPROVED')).length,
           };
           setSummaryStats({ ...sum, _isLocal: true });
        }

        const items: VisitRequestManagementItem[] = response.items || [];
        const mapped: Row[] = items.map((item) => ({
          ...item,
          id: item.visitInstanceId || item.visitRequestId,
          name: item.delegationName || 'Không có tên',
          org: item.partnerName || '-',
          campus: item.campusName || '-',
          host: item.hostName || '',
          sender: item.visitorName || '',
          time: formatDateTimeShort(item.plannedStartAt),
          statusText: getVietnameseStatus(item.requestStatus, item.campusStatus),
        }));
        setRows(mapped);
        setTotal(response.totalItems || 0);
      }
    } catch (e) {
      console.error('Failed to fetch visit requests', e);
      setListError('Không thể tải danh sách tiếp khách. Vui lòng thử lại.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadDelegations(activeTab, currentPage, pageSize, appliedFilters, sortOrder);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Load pending invitations (invitee roles only). Non-blocking: a failure just hides the banner.
  useEffect(() => {
    if (!showTabs) return;
    let active = true;
    delegationsApi.getMyInvitations(false)
      .then((data) => { if (active) setPendingInvitations(data || []); })
      .catch(() => { if (active) setPendingInvitations([]); });
    return () => { active = false; };
  }, [showTabs]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  // Navigation when opening a row's detail (keeps existing detail routes).
  // Whether the icon-mắt should open the shared submitted-form detail modal for this row, and
  // the current user is in scope for it. Covers pre-approval review, rejected detail, and the
  // approved multi-campus waiting-host detail. Backend re-enforces scope (403 on direct URL).
  const hasSetupProcess = (row: Row) => {
    return !!row.visitInstanceId && (!!row.currentHostUserId || !!row.host);
  };

  const canOpenProcess = (row: Row) => {
    const actions = row.allowedActions || [];
    if (actions.includes('OPEN_HOST_PROCESS') || 
        actions.includes('OPEN_PROCESS_SUMMARY') || 
        actions.includes('VIEW_RECEPTION_DETAIL') || 
        actions.includes('OPEN_CONTRIBUTION')) {
      return true;
    }

    if (activeTab === 'attending') return true;

    if (isVisitor) {
      const approvedish = row.requestStatus === 'APPROVED' || row.requestStatus === 'PARTIALLY_APPROVED';
      if (row.visitScope === 'MULTI_CAMPUS') {
        return approvedish;
      }
      return !!row.host && approvedish;
    }

    if (isHO && row.visitScope === 'MULTI_CAMPUS') {
      const rs = row.requestStatus;
      if (rs === 'PENDING_APPROVAL' || rs === 'REJECTED') return false;
      return true;
    }

    if (!row.visitInstanceId) return false;
    if (row.requestStatus === 'REJECTED') return false;

    const isCancelled = row.requestStatus === 'CANCELLED' || row.campusStatus === 'CANCELLED';

    // Hủy trước khi duyệt/chưa có setup thì không vào process
    if (isCancelled && !hasSetupProcess(row)) {
      return false;
    }

    // Đã từng có host/setup thì vẫn cho xem lại quy trình ở chế độ read-only dù đã hủy
    if (isCancelled && hasSetupProcess(row)) {
      return true;
    }

    return (
      row.campusStatus === 'ASSIGNED' ||
      row.campusStatus === 'BEFORE_VISIT' ||
      row.campusStatus === 'DURING_VISIT' ||
      row.campusStatus === 'AFTER_VISIT' ||
      row.campusStatus === 'CLOSED'
    );
  };

  const getProcessActionTitle = (row: Row) => {
    const actions = row.allowedActions || [];
    if (actions.includes('OPEN_HOST_PROCESS')) {
      if (row.campusStatus === 'DURING_VISIT') return 'Tiếp tục xử lý đang tiếp khách';
      if (row.campusStatus === 'AFTER_VISIT') return 'Hoàn tất sau tiếp khách';
      if (row.campusStatus === 'CLOSED') return 'Xem quy trình đã đóng';
      return 'Xử lý quy trình tiếp khách';
    }
    if (actions.includes('OPEN_PROCESS_SUMMARY')) return 'Xem báo cáo tổng hợp';
    if (actions.includes('VIEW_RECEPTION_DETAIL')) return 'Xem thông tin tiếp khách';
    if (actions.includes('OPEN_CONTRIBUTION')) return 'Vào trang đóng góp nội dung';

    const isCancelled = row.requestStatus === 'CANCELLED' || row.campusStatus === 'CANCELLED';

    if (activeTab === 'attending') {
      return isDept && subRole === 'STAFF' ? 'Xem nhiệm vụ' : 'Xem lời mời';
    }

    if (isVisitor) {
      return row.visitScope === 'MULTI_CAMPUS' ? 'Xem tiến trình các cơ sở' : 'Xem thông tin tiếp đón';
    }
    if (isHO && row.visitScope === 'MULTI_CAMPUS') return 'Theo dõi đơn liên cơ sở';

    if (isCancelled && hasSetupProcess(row)) return 'Xem quy trình đã hủy';

    if (row.campusStatus === 'DURING_VISIT') return 'Tiếp tục xử lý đang tiếp khách';
    if (row.campusStatus === 'AFTER_VISIT') return 'Hoàn tất sau tiếp khách';
    if (row.campusStatus === 'CLOSED') return 'Xem quy trình đã đóng';

    return 'Xử lý quy trình tiếp khách';
  };

  const handleProcess = (row: Row) => {
    const actions = row.allowedActions || [];
    const isCancelled = row.requestStatus === 'CANCELLED' || row.campusStatus === 'CANCELLED';
    const displayStatus = row.statusText;

    if (actions.includes('OPEN_PROCESS_SUMMARY')) {
      navTo(`/dashboard/visit/process-summary/${row.visitInstanceId}`);
      return;
    }

    if (actions.includes('OPEN_CONTRIBUTION')) {
      navTo(`/dashboard/visit/contribution/${row.visitInstanceId}`);
      return;
    }

    if (actions.includes('VIEW_RECEPTION_DETAIL')) {
      navTo(`/dashboard/visit/reception-detail/${row.id}`);
      return;
    }

    if (actions.includes('OPEN_HOST_PROCESS')) {
      if (row.campusStatus === 'ASSIGNED' || row.campusStatus === 'BEFORE_VISIT') {
        navTo(`/dashboard/visit/process/${row.visitInstanceId}`, {
          state: { isPrep: true, status: displayStatus, isReadOnly: false }
        });
        return;
      }

      if (row.campusStatus === 'DURING_VISIT') {
        navTo(`/dashboard/visit/process/${row.visitInstanceId}`, {
          state: { defaultTab: 'during', status: displayStatus, isReadOnly: false }
        });
        return;
      }

      if (row.campusStatus === 'AFTER_VISIT') {
        navTo(`/dashboard/visit/process/${row.visitInstanceId}`, {
          state: { defaultTab: 'after', status: displayStatus, isReadOnly: false }
        });
        return;
      }

      if (row.campusStatus === 'CLOSED') {
        navTo(`/dashboard/visit/process/${row.visitInstanceId}`, {
          state: { defaultTab: 'before', status: displayStatus, isReadOnly: true }
        });
        return;
      }
    }

    const idForRoute = row.id;

    if (activeTab === 'attending') {
      const partId = (row as any).participantId;
      if (isDept && subRole === 'STAFF') {
        navTo(`/dashboard/visit/department-tasks/${partId}`);
      } else {
        navTo(`/dashboard/visit/invitations/${partId}`);
      }
      return;
    }

    if (isVisitor) {
      if (row.visitScope === 'MULTI_CAMPUS') {
        toggleExpanded(row.visitRequestId);
        return;
      }
      if (row.host && row.requestStatus === 'APPROVED') {
        navTo(`/dashboard/visit/reception-detail/${row.visitInstanceId || idForRoute}`);
      }
      return;
    }

    if (isHO && row.visitScope === 'MULTI_CAMPUS') {
      navTo(`/dashboard/visit/ho-detail/${idForRoute}`, { state: { guestData: row } });
      return;
    }

    if (isCancelled && hasSetupProcess(row)) {
      navTo(`/dashboard/visit/process/${row.visitInstanceId}`, {
        state: { isReadOnly: true, cancelled: true, status: 'Đã hủy' }
      });
      return;
    }

    if (row.campusStatus === 'ASSIGNED' || row.campusStatus === 'BEFORE_VISIT') {
      navTo(`/dashboard/visit/process/${row.visitInstanceId}`, {
        state: { isPrep: true, status: displayStatus, isReadOnly: isStaffLeader }
      });
      return;
    }

    if (row.campusStatus === 'DURING_VISIT') {
      navTo(`/dashboard/visit/process/${row.visitInstanceId}`, {
        state: { defaultTab: 'during', status: displayStatus, isReadOnly: isStaffLeader }
      });
      return;
    }

    if (row.campusStatus === 'AFTER_VISIT') {
      navTo(`/dashboard/visit/process/${row.visitInstanceId}`, {
        state: { defaultTab: 'after', status: displayStatus, isReadOnly: isStaffLeader }
      });
      return;
    }

    if (row.campusStatus === 'CLOSED') {
      navTo(`/dashboard/visit/process/${row.visitInstanceId}`, {
        state: { defaultTab: 'before', status: displayStatus, isReadOnly: true }
      });
      return;
    }
  };

  // ── Pre-approval review modal → reuse the existing approve/reject flows ──
  // Campus-independent approval: chỉ Staff Leader của campus xử lý. Duyệt LUÔN mở modal chọn
  // host ("Duyệt & gán host"); từ chối là per campus instance với lý do bắt buộc.
  const handleReviewApprove = (row: Row) => {
    setReview({ open: false, row: null });
    setAssign({ open: true, row, mode: 'approve' });
  };
  const handleReviewReject = (row: Row) => {
    setReview({ open: false, row: null });
    setReject({ open: true, row, action: 'CAMPUS_REJECT', text: '', submitting: false, error: null });
  };
  const handleReviewAssignHost = (row: Row) => {
    setReview({ open: false, row: null });
    setAssign({ open: true, row, mode: 'approve' });
  };

  // ── Action handlers ──
  const submitReject = async () => {
    if (!reject.row || !reject.action) return;
    const text = reject.text.trim();
    if (!text) { setReject((s) => ({ ...s, error: 'Vui lòng nhập lý do từ chối.' })); return; }
    setReject((s) => ({ ...s, submitting: true, error: null }));
    try {
      if (reject.action === 'DECLINE_INVITATION' as any) {
        await delegationsApi.visitInvitations.declineInvitation((reject.row as any).participantId, text);
      } else {
        if (!reject.row.visitInstanceId) throw new Error('Thiếu thông tin cơ sở cần từ chối.');
        await delegationsApi.rejectCampusInstance(reject.row.visitRequestId, reject.row.visitInstanceId, text);
      }
      const wasDecline = reject.action === ('DECLINE_INVITATION' as any);
      setReject({ open: false, row: null, action: null, text: '', submitting: false, error: null });
      pushToast('success', wasDecline ? 'Từ chối lời mời thành công.' : 'Đã từ chối tiếp nhận tại cơ sở này.');
      await loadDelegations(activeTab, currentPage, pageSize, appliedFilters, sortOrder);
    } catch (e: any) {
      const msg = apiErrorMessage(e, 'Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.');
      setReject((s) => ({ ...s, submitting: false, error: `Không thể từ chối. ${msg}` }));
    }
  };

  const submitAcceptInvitation = async (row: Row) => {
    try {
      await delegationsApi.visitInvitations.acceptInvitation((row as any).participantId);
      pushToast('success', 'Đã chấp nhận lời mời.');
      await loadDelegations(activeTab, currentPage, pageSize, appliedFilters, sortOrder);
    } catch (e: any) {
      pushToast('error', apiErrorMessage(e, 'Không thể chấp nhận lời mời. Vui lòng thử lại sau.'));
    }
  };

  const submitAssignDeptStaff = async (row: Row) => {
    const staffIdStr = window.prompt('Nhập ID của Department Staff để giao việc:');
    if (!staffIdStr) return;
    const note = window.prompt('Nhập ghi chú/nhiệm vụ:');
    try {
      await delegationsApi.visitInvitations.assignDepartmentStaff((row as any).participantId, parseInt(staffIdStr, 10), note || '');
      pushToast('success', 'Đã giao việc cho nhân sự.');
      await loadDelegations(activeTab, currentPage, pageSize, appliedFilters, sortOrder);
    } catch (e: any) {
      pushToast('error', apiErrorMessage(e, 'Không thể giao việc. Vui lòng thử lại sau.'));
    }
  };

  const submitCancel = async () => {
    if (!cancel.row || !cancel.mode) return;
    const text = cancel.text.trim();
    if (!text) { setCancel((s) => ({ ...s, error: 'Vui lòng nhập lý do hủy.' })); return; }
    if (!cancel.confirmed) { setCancel((s) => ({ ...s, error: 'Vui lòng xác nhận rằng bạn hiểu thao tác hủy không thể hoàn tác.' })); return; }
    setCancel((s) => ({ ...s, submitting: true, error: null }));
    try {
      const payload = { cancellationReason: text };
      // Per-campus cancel from the accordion (instanceId set) → campus endpoint; Host cancel →
      // its own campus instance; otherwise Visitor cancels the whole request.
      const campusInstanceId = cancel.instanceId ?? (cancel.mode === 'host' ? cancel.row.visitInstanceId : null);
      if (campusInstanceId) {
        await delegationsApi.cancelVisitRequestCampus(cancel.row.visitRequestId, campusInstanceId, payload);
      } else {
        await delegationsApi.cancelVisitRequest(cancel.row.visitRequestId, payload);
      }
      setCancel({ open: false, row: null, mode: null, instanceId: null, text: '', submitting: false, error: null, confirmed: false });
      pushToast('success', 'Đã hủy lịch thăm thành công.');
      await loadDelegations(activeTab, currentPage, pageSize, appliedFilters, sortOrder);
    } catch (e: any) {
      // Surface the backend's real business message (clean Vietnamese sentence such as
      // "Không thể hủy lịch thăm. Đơn đang chờ duyệt..."); apiErrorMessage walks
      // message → error → errors → title and only then a generic safe fallback.
      setCancel((s) => ({ ...s, submitting: false, error: apiErrorMessage(e, 'Không thể hủy lịch thăm. Vui lòng thử lại sau.') }));
    }
  };

  // Campus-independent approval: không còn bước "chờ gán host" riêng — Staff Leader duyệt là
  // gán host luôn. Instance của campus mình đang chờ chính là hàng cần xử lý.
  const isAwaitingMyDecision = (row: Row) =>
    isStaffLeader && row.campusStatus === 'WAITING_REQUEST_APPROVAL'
    && (row.allowedActions || []).includes('APPROVE_AND_ASSIGN_HOST');

  const isCancelledOrRejected = (row: Row) => {
    return row.requestStatus === 'CANCELLED' ||
      row.campusStatus === 'CANCELLED' ||
      row.campusStatus === 'REJECTED' ||
      row.requestStatus === 'REJECTED';
  };

  // ── Badges ──
  const renderBadges = (row: Row) => {
    const badges: React.ReactNode[] = [];
    const chip = (key: string, text: string, cls: string) => (
      <span key={key} className={`inline-flex items-center rounded-full border px-2 py-0.5 text-[10px] font-bold whitespace-nowrap ${cls}`}>{text}</span>
    );
    if (activeTab === 'attending') {
      badges.push(chip('att', attendingTabLabel, 'bg-purple-50 text-purple-700 border-purple-200'));
      if (row.participantRole) {
        badges.push(chip('prole', PARTICIPANT_ROLE_LABELS[row.participantRole] ?? 'Tham dự', 'bg-slate-50 text-slate-700 border-slate-200'));
      }
      const visitStatusText = getVietnameseStatus((row as any).visitRequestStatus, (row as any).campusVisitStatus);
      if (visitStatusText && visitStatusText !== '-' && visitStatusText !== 'Không xác định') {
         badges.push(chip('v-status', visitStatusText, 'bg-slate-100 text-slate-600 border-slate-300'));
      }
    } else if (row.visitScope) {
      const single = row.visitScope === 'SINGLE_CAMPUS';
      badges.push(chip('scope', VISIT_SCOPE_LABELS[row.visitScope] + (row.campusCount > 1 ? ` (${row.campusCount})` : ''),
        single ? 'bg-sky-50 text-sky-700 border-sky-200' : 'bg-indigo-50 text-indigo-700 border-indigo-200'));
    }

    if (isAwaitingMyDecision(row) && !isCancelledOrRejected(row)) {
      badges.push(chip('pending-decision', 'Cần duyệt & gán host', 'bg-orange-50 text-orange-700 border-orange-200'));
    } else if (row.currentUserIsHost && activeTab !== 'attending') {
      badges.push(chip('host', 'Được giao làm host', 'bg-emerald-50 text-emerald-700 border-emerald-200'));
    }
    return badges.length ? <div className="flex flex-wrap gap-1 mt-1">{badges}</div> : null;
  };

  const getStatusBadge = (row: Row) => {
    let statusText = 'Không xác định';
    const base = 'inline-flex min-w-[96px] max-w-[150px] justify-center whitespace-nowrap rounded-full border px-2.5 py-1 text-xs font-semibold';
    
    if (activeTab === 'attending') {
      const status = (row as any).invitationStatus;
      
      const isReqCancelled = row.requestStatus === 'CANCELLED';
      const isCampCancelled = row.campusStatus === 'CANCELLED';
      const isRejected = row.requestStatus === 'REJECTED';
      const isClosed = row.campusStatus === 'CLOSED';

      if (isReqCancelled || isCampCancelled) {
        return <span title="Lịch thăm đã bị hủy, lời mời không còn hiệu lực" className={`${base} bg-gray-100 text-gray-600 border-gray-200`}>Lời mời hết hiệu lực</span>;
      }
      if (isRejected) {
        return <span title="Đơn đã bị từ chối" className={`${base} bg-red-50 text-red-700 border-red-200`}>Đơn đã bị từ chối</span>;
      }
      if (isClosed) {
        return <span title="Chuyến thăm đã hoàn tất" className={`${base} bg-slate-100 text-slate-700 border-slate-300`}>Đã đóng đoàn</span>;
      }

      let text = row.statusText;
      if (isDept && subRole === 'STAFF' && status === 'ASSIGNED') {
        text = 'Được giao nhiệm vụ';
      }

      let cls = 'bg-gray-100 text-gray-700 border-gray-200';
      if (status === 'INVITED') cls = 'bg-yellow-50 text-yellow-700 border-yellow-200';
      else if (status === 'ACCEPTED') cls = 'bg-green-50 text-green-700 border-green-200';
      else if (status === 'ASSIGNED') cls = 'bg-blue-50 text-blue-700 border-blue-200';
      else if (status === 'DECLINED') cls = 'bg-red-50 text-red-700 border-red-200';
      return <span title={text} className={`${base} ${cls}`}>{text}</span>;
    }

    // Chuẩn hóa trạng thái hiển thị (AC-04): KHÔNG ghép request status với campus status
    // (bỏ kiểu "Đã duyệt · Đã phân công Host"). Trong màn vận hành theo campus/role ưu tiên
    // visit_request_campuses.status; request status chỉ dùng cho quyết định tổng. `kind` chọn
    // màu badge, nhãn theo vai trò (Visitor xem ngôn ngữ thân thiện hơn nội bộ).
    type StatusKind = 'pending' | 'pending_request' | 'rejected' | 'cancelled' | 'partially' | 'assigned'
      | 'before' | 'during' | 'after' | 'closed' | 'approved';
    let kind: StatusKind;
    if (row.requestStatus === 'CANCELLED' || row.campusStatus === 'CANCELLED') kind = 'cancelled';
    else if (row.campusStatus === 'REJECTED') kind = 'rejected';
    else if (row.campusStatus === 'WAITING_REQUEST_APPROVAL') kind = 'pending';
    else if (row.campusStatus === 'ASSIGNED') kind = 'assigned';
    else if (row.campusStatus === 'BEFORE_VISIT') kind = 'before';
    else if (row.campusStatus === 'DURING_VISIT') kind = 'during';
    else if (row.campusStatus === 'AFTER_VISIT') kind = 'after';
    else if (row.campusStatus === 'CLOSED') kind = 'closed';
    // Request-level rows (không có campusStatus): aggregate.
    else if (row.requestStatus === 'REJECTED') kind = 'rejected';
    else if (row.requestStatus === 'PARTIALLY_APPROVED') kind = 'partially';
    else if (row.requestStatus === 'APPROVED') kind = 'approved';
    else kind = 'pending_request';

    let cancelledText = 'Đã hủy';
    if (kind === 'cancelled') {
      const actor = (row as any).cancellationActorType;
      if (actor === 'VISITOR') cancelledText = 'Đã hủy bởi khách';
      else if (actor === 'HOST') cancelledText = 'Đã hủy bởi Host';
      else if (actor === 'SYSTEM') cancelledText = 'Hệ thống đã hủy';
    }

    const assignedText = isStaffLeader ? 'Đã duyệt & gán Host' : 'Đã được phân công';

    const labelByKind: Record<StatusKind, string> = isVisitor ? {
      pending: 'Chờ xử lý', pending_request: 'Chờ xử lý', rejected: 'Đã bị từ chối', cancelled: cancelledText,
      partially: 'Duyệt một phần', assigned: 'Đã phân công người phụ trách',
      before: 'Sắp diễn ra', during: 'Đang diễn ra', after: 'Đã diễn ra',
      closed: 'Đã hoàn tất', approved: 'Đã được duyệt',
    } : {
      pending: 'Chờ xử lý tại cơ sở', pending_request: 'Chờ xử lý', rejected: 'Đã bị từ chối', cancelled: cancelledText,
      partially: 'Duyệt một phần', assigned: assignedText,
      before: 'Đang chuẩn bị', during: 'Đang tiếp khách', after: 'Chờ đóng đoàn',
      closed: 'Đã đóng đoàn', approved: 'Đã duyệt',
    };

    const clsByKind: Record<StatusKind, string> = {
      pending: 'bg-yellow-50 text-yellow-700 border-yellow-200',
      pending_request: 'bg-yellow-50 text-yellow-700 border-yellow-200',
      partially: 'bg-amber-50 text-amber-700 border-amber-200',
      assigned: 'bg-cyan-50 text-cyan-700 border-cyan-200',
      approved: 'bg-cyan-50 text-cyan-700 border-cyan-200',
      before: 'bg-blue-50 text-blue-700 border-blue-200',
      during: 'bg-green-50 text-green-700 border-green-200',
      after: 'bg-orange-50 text-orange-700 border-orange-200',
      closed: 'bg-slate-100 text-slate-700 border-slate-300',
      rejected: 'bg-red-50 text-red-700 border-red-200',
      cancelled: 'bg-gray-100 text-gray-600 border-gray-200',
    };

    const titleByKind: Record<StatusKind, string> = {
      pending: 'Cơ sở đang chờ Staff Leader duyệt & gán host',
      pending_request: 'Đơn đang chờ xử lý tại các cơ sở',
      rejected: 'Đã bị từ chối tiếp nhận',
      cancelled: 'Đơn/cơ sở đã bị hủy',
      partially: 'Một số cơ sở đã tiếp nhận, một số còn chờ xử lý hoặc bị từ chối',
      assigned: 'Đã duyệt và có host phụ trách, chờ triển khai',
      before: 'Đang trong giai đoạn chuẩn bị đón tiếp',
      during: 'Đoàn đang được tiếp khách tại cơ sở',
      after: 'Đoàn đã kết thúc, chờ đóng đoàn/hoàn tất hồ sơ',
      closed: 'Đoàn đã hoàn tất toàn bộ quy trình',
      approved: 'Tất cả cơ sở đã xử lý xong và có cơ sở tiếp nhận',
    };

    statusText = labelByKind[kind];
    return <span title={titleByKind[kind]} className={`${base} ${clsByKind[kind]}`}>{statusText}</span>;
  };

  const renderRowActions = (row: Row) => {
    const actions = row.allowedActions || [];
    const can = (a: AllowedAction) => actions.includes(a);
    // Gate on the real status CODE (never the display label). statusText is for UI only,
    // so the button survives label changes ("Từ chối" → "Đã từ chối"/"Rejected"/i18n).
    const showReason = row.requestStatus === 'REJECTED' && !!row.decisionNote;
    // UC-136: any in-scope user (Host/Visitor/Staff Leader/HO) may review the cancel reason.
    // The list is already scoped server-side, so a cancelled row here is always one the user may see.
    const isCancelledRow = activeTab !== 'attending'
      && (row.isCancelled === true || row.requestStatus === 'CANCELLED' || row.campusStatus === 'CANCELLED');
    
    const isMultiCampusParentRow = row.visitScope === 'MULTI_CAMPUS' && row.canExpandCampuses === true && !row.visitInstanceId;
    const shouldHideParentCancel = isVisitor && isMultiCampusParentRow && row.hasStartedCampus === true;
    const canRenderCancelAction = (can('CANCEL_BY_VISITOR') || can('CANCEL_BY_HOST')) && !shouldHideParentCancel;

    // Feedback rule mới: nút "Đánh giá" khi instance đang tiếp khách / đã hoàn tất và user
    // đủ điều kiện (Visitor của đơn hoặc Host phụ trách); đã gửi rồi → badge check "Đã đánh giá".
    const fb = row.visitInstanceId ? feedbackByInstance[row.visitInstanceId] : undefined;

    return (
      <div className="mx-auto grid w-[184px] grid-cols-4 gap-2 place-items-center">
        {/* Slot 1: Xem form yêu cầu */}
        {row.visitRequestId && (activeTab !== 'attending' || can('VIEW_REQUEST_FORM')) ? (
          <ActionIconButton title="Xem form đăng ký tham quan" tone="blue" icon={<ClipboardList className="h-5 w-5" />} onClick={(e) => { e.stopPropagation(); openRequestForm(row); }} />
        ) : (
          <span className="h-9 w-9" aria-hidden="true" />
        )}

        {/* Slot 2: Xử lý / Theo dõi quy trình */}
        {canOpenProcess(row) ? (
          <ActionIconButton 
            title={getProcessActionTitle(row)}
            tone={
              can('OPEN_CONTRIBUTION') || can('OPEN_PROCESS_SUMMARY')
                ? 'orange'
                : 'blue'
            }
            icon={
              can('OPEN_CONTRIBUTION')
                ? <PencilLine className="h-5 w-5" />
                : can('OPEN_PROCESS_SUMMARY')
                  ? <FileText className="h-5 w-5" />
                  : can('VIEW_RECEPTION_DETAIL')
                    ? <Eye className="h-5 w-5" />
                    : (activeTab === 'attending' && !can('OPEN_HOST_PROCESS'))
                      ? <MailOpen className="h-5 w-5" />
                      : <ArrowRightCircle className="h-5 w-5" />
            }
            onClick={(e) => {
              e.stopPropagation();
              handleProcess(row);
            }}
          />
        ) : (
          <span className="h-9 w-9" aria-hidden="true" />
        )}

        {/* Slot 3: Approve / Accept / Edit / Resubmit (campus-independent approval: duyệt LUÔN kèm gán host) */}
        {can('APPROVE_AND_ASSIGN_HOST') ? (
          <ActionIconButton title="Duyệt & gán host" tone="green" icon={<Check className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); setAssign({ open: true, row, mode: 'approve' }); }} />
        ) : can('EDIT_PENDING_REQUEST') ? (
          <ActionIconButton title="Sửa đơn đăng ký tham quan" tone="blue" icon={<PencilLine className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); navTo(`/dashboard/visit/edit/${row.visitRequestId}`); }} />
        ) : can('RESUBMIT_REJECTED_REQUEST') ? (
          <ActionIconButton title="Sửa & gửi lại đơn" tone="orange" icon={<RefreshCw className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); navTo(`/dashboard/visit/resubmit/${row.visitRequestId}`); }} />
        ) : can('ACCEPT_INVITATION') ? (
          <ActionIconButton title="Xác nhận tham gia" tone="green" icon={<Check className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); submitAcceptInvitation(row); }} />
        ) : can('ASSIGN_TO_DEPARTMENT_STAFF') ? (
          <ActionIconButton title="Giao việc cho Staff" tone="blue" icon={<Users className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); submitAssignDeptStaff(row); }} />
        ) : (
          <span className="h-9 w-9" aria-hidden="true" />
        )}

        {/* Slot 4: Reject / Cancel / Reason / Feedback (Các hành động tiêu cực / Cảnh báo / Đánh giá) */}
        {can('CAMPUS_REJECT') ? (
          <ActionIconButton title="Từ chối cơ sở này" tone="red" icon={<X className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); setReject({ open: true, row, action: 'CAMPUS_REJECT', text: '', submitting: false, error: null }); }} />
        ) : can('DECLINE_INVITATION') ? (
          <ActionIconButton title="Từ chối lời mời" tone="red" icon={<X className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); setReject({ open: true, row, action: 'DECLINE_INVITATION' as any, text: '', submitting: false, error: null }); }} />
        ) : canRenderCancelAction ? (
          <ActionIconButton title="Hủy lịch thăm" tone="red" icon={<XCircle className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); setCancel({ open: true, row, mode: can('CANCEL_BY_HOST') ? 'host' : 'visitor', instanceId: null, text: '', submitting: false, error: null, confirmed: false }); }} />
        ) : showReason ? (
          <ActionIconButton title="Xem lý do từ chối" tone="orange" icon={<AlertCircle className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); setReason({ open: true, row }); }} />
        ) : isCancelledRow ? (
          <ActionIconButton title="Xem lý do hủy" tone="gray" icon={<Info className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); setCancelReason({ open: true, row }); }} />
        ) : fb && !fb.alreadySubmitted ? (
          <ActionIconButton title="Đánh giá chuyến thăm" tone="orange" icon={<Star className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); setFeedbackModalInstanceId(row.visitInstanceId!); }} />
        ) : fb?.alreadySubmitted ? (
          <span title="Đã đánh giá" className="flex h-9 w-9 items-center justify-center text-emerald-500">
            <CheckCircle2 className="h-5 w-5" />
          </span>
        ) : (
          <span className="h-9 w-9" aria-hidden="true" />
        )}
      </div>
    );
  };

  // ── Multi-campus accordion (Phương án A): per-campus progress + actions ──
  const openCampusRequestForm = (row: Row, item?: CampusProgressItem) => {
    const campusRow = {
      ...row,
      campus: item?.campusName || row.campus || '-',
      campusStatus: item?.instanceStatus || row.campusStatus,
      visitInstanceId: item?.visitInstanceId || row.visitInstanceId,
    } as Row;
    setRequestForm({ open: true, row: campusRow });
  };

  const openCampusDetail = (row: Row, item: CampusProgressItem) => {
    if (item.instanceStatus === 'REJECTED') {
      openCampusRejectReason(row, item);
      return;
    }

    if (item.instanceStatus === 'CANCELLED') {
      if (!isVisitor && item.hostUserId != null && item.visitInstanceId) {
        navTo(`/dashboard/visit/process/${item.visitInstanceId}`, {
          state: { isReadOnly: true, cancelled: true, status: 'Đã hủy' },
        });
        return;
      }
      if (item.canViewCancelReason) {
        openCampusCancelReason(row, item);
        return;
      }
      openCampusRequestForm(row, item);
      return;
    }

    if (isHO && item.visitInstanceId && item.hostUserId != null && item.instanceStatus !== 'WAITING_REQUEST_APPROVAL') {
      navTo(`/dashboard/visit/process-summary/${item.visitInstanceId}`);
      return;
    }

    if (
      isVisitor &&
      (row.requestStatus === 'APPROVED' || row.requestStatus === 'PARTIALLY_APPROVED') &&
      item.hostUserId != null &&
      item.visitInstanceId
    ) {
      navTo(`/dashboard/visit/reception-detail/${item.visitInstanceId}`);
      return;
    }

    if (
      item.instanceStatus === 'ASSIGNED' ||
      item.instanceStatus === 'BEFORE_VISIT' ||
      item.instanceStatus === 'DURING_VISIT' ||
      item.instanceStatus === 'AFTER_VISIT' ||
      item.instanceStatus === 'CLOSED'
    ) {
      if (item.visitInstanceId) {
        navTo(`/dashboard/visit/process/${item.visitInstanceId}`, {
          state: {
            defaultTab:
              item.instanceStatus === 'DURING_VISIT'
                ? 'during'
                : item.instanceStatus === 'AFTER_VISIT'
                  ? 'after'
                  : 'before',
            status: getCampusStatusLabel(item.instanceStatus),
            isReadOnly: isHO || isStaffLeader || item.instanceStatus === 'CLOSED',
          },
        });
        return;
      }
    }

    openCampusRequestForm(row, item);
  };

  const openCampusCancel = (row: Row, item: CampusProgressItem) =>
    setCancel({ open: true, row, mode: 'visitor', instanceId: item.visitInstanceId, text: '', submitting: false, error: null, confirmed: false });

  const openCampusRejectReason = (row: Row, item: CampusProgressItem) => {
    const campusRow = {
      ...row,
      campus: item.campusName || '-',
      campusStatus: item.instanceStatus,
      visitInstanceId: item.visitInstanceId,
      decisionActorRole: item.decisionActorRole || 'STAFF_LEADER',
      decidedByName: item.decidedByName,
      decidedBy: item.decidedBy,
      decidedAt: item.decidedAt,
      decisionNote: item.decisionNote,
    } as Row;
    setReason({ open: true, row: campusRow });
  };

  const openCampusCancelReason = (row: Row, item: CampusProgressItem) => {
    const campusRow = {
      ...row,
      campus: item.campusName || '-',
      cancellationLevel: 'CAMPUS_INSTANCE',
      cancelledByName: item.cancelledByName,
      cancelledBy: item.cancelledBy,
      cancelledAt: item.cancelledAt,
      cancellationActorType: item.cancellationActorType,
      cancellationSource: item.cancellationSource,
      cancellationReason: item.cancellationReason,
    } as Row;
    setCancelReason({ open: true, row: campusRow });
  };

  const renderCampusAccordion = (row: Row) => {
    const items = row.campusProgressItems || [];
    return (
      <div className="bg-[#f8fafc] border-b border-slate-200/70 shadow-inner" onClick={(e) => e.stopPropagation()}>
        {items.length === 0 ? (
          <p className="py-2 pl-14 text-xs text-slate-500">Chưa có dữ liệu cơ sở để hiển thị.</p>
        ) : (
          <div className="divide-y divide-slate-200/60">
            {items.map((item) => (
              <div key={item.visitInstanceId} className="flex flex-col lg:grid lg:grid-cols-[52px_minmax(0,1fr)_210px_150px_246px] items-start lg:items-center py-2 px-3 lg:p-0 hover:bg-[#f1f5f9] transition-colors min-h-[44px]">
                
                {/* Spacer / STT col for desktop */}
                <div className="hidden lg:block w-full"></div>
                
                {/* Info Column */}
                <div className="lg:py-1 lg:pl-10 lg:pr-4 w-full flex flex-col sm:flex-row sm:items-center gap-1 sm:gap-4 min-w-0">
                  <div className="text-xs font-bold text-[#004c91] truncate sm:min-w-[160px]">
                    {item.campusName || '-'}
                    {item.campusCode ? <span className="ml-1 text-[10px] font-medium text-slate-400">({item.campusCode})</span> : null}
                  </div>
                  <div className="text-[11px] text-slate-500 truncate">
                    {item.instanceStatus === 'REJECTED' ? (
                      <><span className="text-slate-400">Lý do từ chối:</span> <span className="font-semibold text-red-700">{item.decisionNote || '-'}</span></>
                    ) : (
                      <><span className="text-slate-400">Host:</span> <span className="font-semibold text-slate-700">{item.hostName || (item.instanceStatus === 'WAITING_REQUEST_APPROVAL' ? 'Chờ Staff Leader xử lý' : '-')}</span></>
                    )}
                  </div>
                </div>

                {/* Time Column */}
                <div className="lg:py-1 lg:px-3 text-[11px] text-slate-600 flex flex-wrap items-center gap-1 mt-1 lg:mt-0">
                  <span className="truncate">{formatDateTimeShort(item.plannedStartAt)}</span>
                  <span className="text-slate-300">→</span>
                  <span className="truncate">{formatDateTimeShort(item.plannedEndAt)}</span>
                </div>

                {/* Status Column */}
                <div className="lg:py-1 lg:px-3 lg:flex lg:justify-center mt-2 lg:mt-0">
                  <span className={`inline-flex justify-center whitespace-nowrap rounded-full border px-2 py-0.5 text-[10px] font-bold ${getCampusStatusBadgeClass(item.instanceStatus)}`}>
                    {getCampusStatusLabel(item.instanceStatus)}
                  </span>
                </div>

                {/* Actions Column */}
                <div className="lg:py-1 lg:px-2 flex items-center mt-2 lg:mt-0 lg:justify-center w-full">
                  <div className="mx-auto grid w-[184px] grid-cols-4 gap-2 place-items-center">
                    {/* Slot 1: Empty */}
                    <span className="h-9 w-9" aria-hidden="true" />
                    
                    {/* Slot 2: View Detail */}
                    {item.canViewCampusDetail && item.instanceStatus !== 'REJECTED' ? (
                      <ActionIconButton
                        title={
                          item.instanceStatus === 'WAITING_REQUEST_APPROVAL'
                            ? 'Xem form đăng ký tham quan'
                            : isHO && item.visitInstanceId && item.hostUserId != null
                              ? 'Xem báo cáo tổng hợp'
                              : 'Xem chi tiết cơ sở'
                        }
                        tone={
                          isHO && item.visitInstanceId && item.hostUserId != null && item.instanceStatus !== 'WAITING_REQUEST_APPROVAL'
                            ? 'orange'
                            : 'blue'
                        }
                        icon={
                          item.instanceStatus === 'WAITING_REQUEST_APPROVAL'
                            ? <ClipboardList className="h-5 w-5" />
                            : isHO && item.visitInstanceId && item.hostUserId != null
                              ? <FileText className="h-5 w-5" />
                              : <Eye className="h-5 w-5" />
                        }
                        onClick={() => openCampusDetail(row, item)}
                      />
                    ) : <span className="h-9 w-9" aria-hidden="true" />}

                    {/* Slot 3: Empty */}
                    <span className="h-9 w-9" aria-hidden="true" />

                    {/* Slot 4: Cancel / Cancel Reason / Feedback */}
                    {item.instanceStatus === 'REJECTED' ? (
                      <ActionIconButton title="Xem lý do từ chối cơ sở" tone="orange" icon={<AlertCircle className="h-5 w-5" />} onClick={() => openCampusRejectReason(row, item)} />
                    ) : item.canViewCancelReason ? (
                      <ActionIconButton title="Xem lý do hủy" tone="gray" icon={<Info className="h-5 w-5" />} onClick={() => openCampusCancelReason(row, item)} />
                    ) : item.canCancelCampusVisit ? (
                      <ActionIconButton title="Hủy lịch thăm cơ sở" tone="red" icon={<XCircle className="h-5 w-5" />} onClick={() => openCampusCancel(row, item)} />
                    ) : item.visitInstanceId && feedbackByInstance[item.visitInstanceId] ? (
                      feedbackByInstance[item.visitInstanceId].alreadySubmitted ? (
                        <span title="Đã đánh giá" className="flex h-9 w-9 items-center justify-center text-emerald-500">
                          <CheckCircle2 className="h-5 w-5" />
                        </span>
                      ) : (
                        <ActionIconButton title="Đánh giá chuyến thăm" tone="orange" icon={<Star className="h-5 w-5" />}
                          onClick={() => setFeedbackModalInstanceId(item.visitInstanceId!)} />
                      )
                    ) : <span className="h-9 w-9" aria-hidden="true" />}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    );
  };

  // ── Admin: no business screen ──
  if (isAdmin) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[50vh] animate-in fade-in duration-300">
        <AlertCircle className="w-16 h-16 text-slate-300 mb-4" />
        <h2 className="text-2xl font-bold text-slate-700 mb-2">Không tham gia luồng tiếp khách</h2>
        <p className="text-slate-500 text-center max-w-md">
          Tài khoản Admin không tham gia vào luồng xử lý đơn tiếp khách. Vui lòng dùng các chức năng quản trị tài khoản, vai trò và cấu hình hệ thống.
        </p>
      </div>
    );
  }

  const hasActiveFilter = !!(appliedFilters.keyword || appliedFilters.status || appliedFilters.visitScope || appliedFilters.fromDate || appliedFilters.toDate);
  const emptyText = hasActiveFilter
    ? 'Không tìm thấy đơn phù hợp với bộ lọc.'
    : activeTab === 'attending'
      ? 'Bạn chưa có đơn mời tham dự nào.'
      : isVisitor
        ? 'Bạn chưa gửi đơn tiếp khách nào.'
        : 'Bạn chưa có đơn phụ trách nào.';

  return (
    <div className="w-full flex flex-col space-y-6 pb-12 animate-in fade-in duration-300">
      {/* Header */}
      {!isEmbedded && (
        <>
          <div className="mb-2 flex items-center text-sm font-medium text-gray-500">
            <button onClick={() => navTo('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Dashboard</button>
            <span className="mx-2">/</span>
            <span className="text-[#004c91]">Quản lý tiếp khách</span>
          </div>
          <div className="border-b border-gray-100 pb-4 flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
            <h1 className="text-3xl font-bold text-[#004c91]">{isVisitor ? 'Đơn của tôi' : 'Quản lý tiếp khách'}</h1>
            {isRegularStaff ? (
              <button onClick={() => navTo('/dashboard/visit/create')} className="flex items-center justify-center gap-2 bg-[#F37021] hover:bg-orange-600 outline-none text-white px-4 py-2 rounded-lg font-bold shadow-sm transition-colors whitespace-nowrap w-full md:w-auto">
                <Plus className="w-5 h-5" /> Tạo đoàn khách
              </button>
            ) : isHO ? (
              <button onClick={() => navTo('/dashboard/visit/agenda-templates')} className="flex items-center justify-center gap-2 bg-[#F37021] hover:bg-orange-600 outline-none text-white px-4 py-2 rounded-lg font-bold shadow-sm transition-colors whitespace-nowrap w-full md:w-auto">
                <Plus className="w-5 h-5" /> Quản lý mẫu Agenda
              </button>
            ) : null}
          </div>
        </>
      )}

      {/* UC-27: pending invitations entry point (accept/decline lives on the detail screen) */}
      {showTabs && pendingInvitations.length > 0 && (
        <div className="rounded-2xl border border-orange-200 bg-orange-50/70 p-4">
          <div className="flex items-center gap-2 mb-3">
            <Mail className="w-5 h-5 text-[#F37021]" />
            <h3 className="text-sm font-bold text-slate-800">Lời mời tham gia chờ phản hồi ({pendingInvitations.length})</h3>
          </div>
          <div className="space-y-2">
            {pendingInvitations.map((inv) => (
              <button
                key={inv.participantId}
                onClick={() => navTo(`/dashboard/visit/invitations/${inv.participantId}`)}
                className="w-full flex items-center justify-between gap-3 rounded-xl border border-orange-100 bg-white px-4 py-3 text-left hover:border-[#F37021] hover:shadow-sm transition-all outline-none cursor-pointer"
              >
                <div className="min-w-0">
                  <p className="text-sm font-bold text-[#004c91] truncate">{inv.delegationName || 'Đoàn khách'}</p>
                  <p className="text-xs text-slate-500 truncate">
                    {PARTICIPANT_ROLE_LABELS[inv.participantRole] ?? inv.participantRole}
                    {inv.campusName ? ` · ${inv.campusName}` : ''}
                    {inv.invitedByName ? ` · Mời bởi ${inv.invitedByName}` : ''}
                  </p>
                </div>
                <span className="flex-shrink-0 inline-flex items-center gap-1 text-xs font-bold text-[#F37021]">
                  Phản hồi <ChevronRight className="w-4 h-4" />
                </span>
              </button>
            ))}
          </div>
        </div>
      )}

      {/* Tabs */}
      {showTabs && !isEmbedded && (
        <div className="flex w-full sm:w-max items-center gap-1 rounded-xl bg-slate-100 p-1">
          {([
            { key: 'responsible' as Tab, label: responsibleTabLabel, show: canUseResponsibleTab },
            { key: 'attending' as Tab, label: attendingTabLabel, show: canUseAttendingTab },
          ]).filter(t => t.show).map((t) => (
            <button
              key={t.key}
              onClick={() => { 
                if (activeTab !== t.key) { 
                  const nextEmptyFilters = createEmptyFilters();
                  setActiveTab(t.key); 
                  setDraftFilters(nextEmptyFilters);
                  setAppliedFilters(nextEmptyFilters);
                  setCurrentPage(1); 
                  updateUrlParams(t.key, 1, pageSize, nextEmptyFilters, sortOrder);
                  loadDelegations(t.key, 1, pageSize, nextEmptyFilters, sortOrder); 
                } 
              }}
              className={`flex-1 sm:flex-none px-5 py-2 rounded-lg text-sm font-bold transition-colors outline-none cursor-pointer ${activeTab === t.key ? 'bg-white text-[#004c91] shadow-sm' : 'text-slate-500 hover:text-slate-700'}`}
            >
              {t.label}
            </button>
          ))}
        </div>
      )}

      {/* Summary Chips */}
      {summaryStats && (
        <div className="flex flex-wrap items-center mb-4 text-sm font-medium text-slate-700">
          {activeTab === 'attending' ? (
             <span>{[
               { label: 'Tổng', count: summaryStats.total },
               { label: 'Chờ phản hồi', count: summaryStats.pending },
               { label: 'Đã nhận lời', count: summaryStats.accepted },
               { label: 'Đã từ chối', count: summaryStats.declined },
               { label: 'Hết hiệu lực / Đã hủy', count: summaryStats.cancelledOrExpired }
             ].filter(it => it.count > 0 || it.label === 'Tổng').map(it => `${it.label} ${it.count}`).join(' · ')}</span>
          ) : isHO ? (
             <span>{[
               { label: 'Tổng', count: summaryStats.total },
               { label: 'Còn cơ sở chờ xử lý', count: summaryStats.pendingApproval },
               { label: 'Đã tiếp nhận', count: summaryStats.assigned + summaryStats.before + summaryStats.during + summaryStats.after + summaryStats.closed },
               { label: 'Đã từ chối', count: summaryStats.rejected },
               { label: 'Đã hủy', count: summaryStats.cancelled }
             ].filter(it => it.count > 0 || it.label === 'Tổng').map(it => `${it.label} ${it.count}`).join(' · ')}</span>
          ) : isVisitor ? (
             <span>{[
               { label: 'Tổng', count: summaryStats.total },
               { label: 'Chờ xử lý', count: summaryStats.pendingApproval },
               { label: 'Đã tiếp nhận', count: summaryStats.assigned + summaryStats.before + summaryStats.during + summaryStats.after },
               { label: 'Đã từ chối', count: summaryStats.rejected },
               { label: 'Đã hoàn tất', count: summaryStats.closed },
               { label: 'Đã hủy', count: summaryStats.cancelled }
             ].filter(it => it.count > 0 || it.label === 'Tổng').map(it => `${it.label} ${it.count}`).join(' · ')}</span>
          ) : isStaffLeader ? (
             <span>{[
               { label: 'Tổng', count: summaryStats.total },
               { label: 'Chờ duyệt & gán host', count: summaryStats.pendingApproval },
               { label: 'Đã duyệt & gán Host', count: summaryStats.assigned },
               { label: 'Trước tiếp khách', count: summaryStats.before },
               { label: 'Trong tiếp khách', count: summaryStats.during },
               { label: 'Chờ đóng đoàn', count: summaryStats.after },
               { label: 'Đã đóng đoàn', count: summaryStats.closed },
               { label: 'Đã từ chối', count: summaryStats.rejected },
               { label: 'Đã hủy', count: summaryStats.cancelled }
             ].filter(it => it.count > 0 || it.label === 'Tổng').map(it => `${it.label} ${it.count}`).join(' · ')}</span>
          ) : (
             <span>{[
               { label: 'Tổng', count: summaryStats.total },
               { label: 'Đã phân công Host', count: summaryStats.assigned },
               { label: 'Trước tiếp khách', count: summaryStats.before },
               { label: 'Trong tiếp khách', count: summaryStats.during },
               { label: 'Chờ đóng đoàn', count: summaryStats.after },
               { label: 'Đã đóng đoàn', count: summaryStats.closed },
               { label: 'Đã hủy', count: summaryStats.cancelled }
             ].filter(it => it.count > 0 || it.label === 'Tổng').map(it => `${it.label} ${it.count}`).join(' · ')}</span>
          )}
          {summaryStats._isLocal && <span className="text-xs font-medium text-slate-400 ml-auto">Số liệu theo trang hiện tại</span>}
        </div>
      )}

      {/* Filters */}
      <div className="w-full rounded-2xl border border-slate-200 bg-white p-4 shadow-sm overflow-visible">
        <div className={`grid grid-cols-1 gap-3 md:grid-cols-2 ${filterGridClass} xl:items-end`}>
          <div className="min-w-0 md:col-span-2 xl:col-span-1">
            <label className="block h-5 mb-1 truncate text-xs font-bold text-slate-500">Tìm kiếm</label>
            <div className="relative w-full">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 w-5 h-5 shrink-0" />
              <input type="text" placeholder="Tìm tên đoàn, host, đối tác..." value={draftFilters.keyword}
                onChange={(e) => {
                  const val = e.target.value;
                  setDraftFilters({ ...draftFilters, keyword: val });
                }}
                className="w-full pl-10 pr-4 h-11 bg-white border border-slate-300 rounded-xl text-sm font-semibold text-slate-700 outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10 transition-colors min-w-0" />
            </div>
          </div>

          {/* Status */}
          {filterConfig.showStatus && (
            <div className="relative min-w-0">
              <label className="block h-5 mb-1 truncate text-xs font-bold text-slate-500">{filterConfig.statusLabel || 'Trạng thái'}</label>
              <button onClick={() => setIsStatusFilterOpen(!isStatusFilterOpen)} className="flex h-11 w-full min-w-0 items-center justify-between rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none transition-colors focus:border-[#004c91]">
                <span className="min-w-0 truncate">{filterConfig.statusOptions.find((o) => o.value === draftFilters.status)?.label ?? 'Tất cả trạng thái'}</span>
                <ChevronDown className="w-4 h-4 text-gray-500 flex-shrink-0 ml-2 pointer-events-none" />
              </button>
              {isStatusFilterOpen && (
                <>
                  <div className="fixed inset-0 z-10" onClick={() => setIsStatusFilterOpen(false)} />
                  <div className="absolute left-0 top-full z-30 mt-1 w-full min-w-[210px] rounded-xl border border-slate-200 bg-white py-1 shadow-lg max-h-72 overflow-y-auto">
                    {filterConfig.statusOptions.map((option) => (
                      <div key={option.value} title={option.description} className={`px-3 py-2 text-sm cursor-pointer hover:bg-slate-50 flex items-center ${draftFilters.status === option.value ? 'bg-blue-50 text-[#004c91] font-bold' : 'text-gray-700 font-medium'}`}
                        onClick={() => { applyFilterChange({ status: option.value }); setIsStatusFilterOpen(false); }}>
                        {option.label}
                        {draftFilters.status === option.value && <Check className="w-4 h-4 ml-auto text-[#004c91]" />}
                      </div>
                    ))}
                  </div>
                </>
              )}
            </div>
          )}

          {/* Scope */}
          {filterConfig.showScope && (
            <div className="relative min-w-0">
              <label className="block h-5 mb-1 truncate text-xs font-bold text-slate-500">{filterConfig.scopeLabel || 'Phạm vi đơn'}</label>
              <button onClick={() => setIsTypeFilterOpen(!isTypeFilterOpen)} className="flex h-11 w-full min-w-0 items-center justify-between rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none transition-colors focus:border-[#004c91]">
                <span className="min-w-0 truncate">{filterConfig.scopeOptions?.find((o) => o.value === draftFilters.visitScope)?.label ?? 'Tất cả phạm vi'}</span>
                <ChevronDown className="w-4 h-4 text-gray-500 flex-shrink-0 ml-2 pointer-events-none" />
              </button>
              {isTypeFilterOpen && (
                <>
                  <div className="fixed inset-0 z-10" onClick={() => setIsTypeFilterOpen(false)} />
                  <div className="absolute left-0 top-full z-30 mt-1 w-full min-w-[210px] rounded-xl border border-slate-200 bg-white py-1 shadow-lg max-h-72 overflow-y-auto">
                    {filterConfig.scopeOptions?.map((option) => (
                      <div key={option.value} className={`px-3 py-2 text-sm cursor-pointer hover:bg-slate-50 flex items-center ${draftFilters.visitScope === option.value ? 'bg-blue-50 text-[#004c91] font-bold' : 'text-gray-700 font-medium'}`}
                        onClick={() => { applyFilterChange({ visitScope: option.value }); setIsTypeFilterOpen(false); }}>
                        {option.label}
                        {draftFilters.visitScope === option.value && <Check className="w-4 h-4 ml-auto text-[#004c91]" />}
                      </div>
                    ))}
                  </div>
                </>
              )}
            </div>
          )}

          {/* Campus */}
          {filterConfig.showCampus && (
            <div className="relative min-w-0">
              <label className="block h-5 mb-1 truncate text-xs font-bold text-slate-500">Cơ sở</label>
              <button onClick={() => setIsCampusFilterOpen(!isCampusFilterOpen)} className="flex h-11 w-full min-w-0 items-center justify-between rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none transition-colors focus:border-[#004c91]">
                <span className="min-w-0 truncate">{[
                    { value: '', label: 'Tất cả cơ sở' },
                    { value: '1', label: 'Hà Nội' },
                    { value: '2', label: 'Hồ Chí Minh' },
                    { value: '3', label: 'Đà Nẵng' },
                    { value: '4', label: 'Cần Thơ' },
                    { value: '5', label: 'Quy Nhơn' },
                  ].find((o) => o.value === draftFilters.campusId)?.label ?? 'Tất cả cơ sở'}</span>
                <ChevronDown className="w-4 h-4 text-gray-500 flex-shrink-0 ml-2 pointer-events-none" />
              </button>
              {isCampusFilterOpen && (
                <>
                  <div className="fixed inset-0 z-10" onClick={() => setIsCampusFilterOpen(false)} />
                  <div className="absolute left-0 top-full z-30 mt-1 w-full min-w-[210px] rounded-xl border border-slate-200 bg-white py-1 shadow-lg max-h-72 overflow-y-auto">
                    {[
                      { value: '', label: 'Tất cả cơ sở' },
                      { value: '1', label: 'Hà Nội' },
                      { value: '2', label: 'Hồ Chí Minh' },
                      { value: '3', label: 'Đà Nẵng' },
                      { value: '4', label: 'Cần Thơ' },
                      { value: '5', label: 'Quy Nhơn' },
                    ].map((option) => (
                      <div key={option.value} className={`px-3 py-2 text-sm cursor-pointer hover:bg-slate-50 flex items-center ${draftFilters.campusId === option.value ? 'bg-blue-50 text-[#004c91] font-bold' : 'text-gray-700 font-medium'}`}
                        onClick={() => { applyFilterChange({ campusId: option.value }); setIsCampusFilterOpen(false); }}>
                        {option.label}
                        {draftFilters.campusId === option.value && <Check className="w-4 h-4 ml-auto text-[#004c91]" />}
                      </div>
                    ))}
                  </div>
                </>
              )}
            </div>
          )}

          {/* Relation */}
          {filterConfig.showRelation && (
            <div className="relative min-w-0">
              <label className="block h-5 mb-1 truncate text-xs font-bold text-slate-500">{filterConfig.relationLabel || 'Loại xử lý'}</label>
              <button onClick={() => setIsRelationFilterOpen(!isRelationFilterOpen)} className="flex h-11 w-full min-w-0 items-center justify-between rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none transition-colors focus:border-[#004c91]">
                <span className="min-w-0 truncate">{filterConfig.relationOptions.find((o) => o.value === draftFilters.relation)?.label ?? 'Tất cả'}</span>
                <ChevronDown className="w-4 h-4 text-gray-500 flex-shrink-0 ml-2 pointer-events-none" />
              </button>
              {isRelationFilterOpen && (
                <>
                  <div className="fixed inset-0 z-10" onClick={() => setIsRelationFilterOpen(false)} />
                  <div className="absolute left-0 top-full z-30 mt-1 w-full min-w-[210px] rounded-xl border border-slate-200 bg-white py-1 shadow-lg max-h-72 overflow-y-auto">
                    {filterConfig.relationOptions.map((option) => (
                      <div key={option.value} className={`px-3 py-2 text-sm cursor-pointer hover:bg-slate-50 flex items-center ${draftFilters.relation === option.value ? 'bg-blue-50 text-[#004c91] font-bold' : 'text-gray-700 font-medium'}`}
                        onClick={() => { applyFilterChange({ relation: option.value }); setIsRelationFilterOpen(false); }}>
                        {option.label}
                        {draftFilters.relation === option.value && <Check className="w-4 h-4 ml-auto text-[#004c91]" />}
                      </div>
                    ))}
                  </div>
                </>
              )}
            </div>
          )}

          {/* Date range */}
          <div className="relative min-w-0">
            <label className="block h-5 mb-1 truncate text-xs font-bold text-slate-500">Khoảng ngày</label>
            <button onClick={() => setIsDateFilterOpen(!isDateFilterOpen)} className="flex h-11 w-full min-w-0 items-center justify-between rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none transition-colors focus:border-[#004c91]">
              <span className="min-w-0 truncate">
                {!draftFilters.fromDate && !draftFilters.toDate ? 'Chọn khoảng ngày'
                  : draftFilters.fromDate && !draftFilters.toDate ? `Từ ${formatDateOnly(draftFilters.fromDate)}`
                  : !draftFilters.fromDate && draftFilters.toDate ? `Đến ${formatDateOnly(draftFilters.toDate)}`
                  : `${formatDateOnly(draftFilters.fromDate)} - ${formatDateOnly(draftFilters.toDate)}`}
              </span>
              <Calendar className="w-4 h-4 text-gray-500 flex-shrink-0 ml-2 pointer-events-none" />
            </button>
            {isDateFilterOpen && (
              <>
                <div className="fixed inset-0 z-20" onClick={() => setIsDateFilterOpen(false)} />
                <div className="absolute left-0 top-full z-30 mt-2 w-[280px] bg-white border border-slate-200 rounded-2xl shadow-lg p-4">
                  <div className="flex flex-col gap-3">
                    <div className="w-full space-y-1">
                      <label className="block text-xs font-bold text-slate-500">Từ ngày</label>
                      <input type="date" value={draftFilters.fromDate} onChange={(e) => setDraftFilters({ ...draftFilters, fromDate: e.target.value })} className="h-11 w-full rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none focus:border-[#004c91]" />
                    </div>
                    <div className="w-full space-y-1">
                      <label className="block text-xs font-bold text-slate-500">Đến ngày</label>
                      <input type="date" value={draftFilters.toDate} onChange={(e) => setDraftFilters({ ...draftFilters, toDate: e.target.value })} className="h-11 w-full rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none focus:border-[#004c91]" />
                    </div>
                    <button onClick={() => { setIsDateFilterOpen(false); applyFilterChange({}); }} className="mt-2 h-9 w-full rounded-lg bg-slate-100 text-sm font-semibold text-slate-700 hover:bg-slate-200 transition-colors">Đóng</button>
                  </div>
                </div>
              </>
            )}
          </div>

          <div className="flex md:col-span-2 xl:contents">
            <button 
              onClick={handleResetFilters} 
              disabled={
                !draftFilters.keyword.trim() && 
                !debouncedKeyword.trim() &&
                !draftFilters.status && 
                !draftFilters.visitScope && 
                !draftFilters.campusId && 
                !draftFilters.relation && 
                !draftFilters.fromDate && 
                !draftFilters.toDate
              }
              className="h-11 rounded-xl border border-slate-200 bg-white px-4 text-sm font-bold text-slate-600 hover:bg-slate-50 disabled:text-slate-400 disabled:cursor-not-allowed outline-none w-full xl:w-auto transition-colors"
            >
              Reset
            </button>
          </div>
        </div>
        {filterError && <div className="text-red-500 text-sm font-medium mt-2"><AlertCircle className="w-4 h-4 inline-block mr-1" />{filterError}</div>}
      </div>

      {/* List */}
      <div className="w-full overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm flex flex-col">
        {/* Desktop */}
        <div className="hidden lg:block w-full">
          <div className="grid grid-cols-[52px_minmax(0,1fr)_210px_150px_246px] bg-[#004c91] text-white">
            <div className="p-3 text-[12px] font-bold text-center uppercase tracking-wider">STT</div>
            <div className="p-3 text-[12px] font-bold text-left uppercase tracking-wider">Thông tin đoàn</div>
            <div 
              className="p-3 text-[12px] font-bold text-left uppercase tracking-wider cursor-pointer hover:bg-[#003b70] transition-colors group flex items-center gap-1"
              onClick={() => {
                const nextSort = sortOrder === 'desc' ? 'asc' : 'desc';
                setSortOrder(nextSort);
                setCurrentPage(1);
                updateUrlParams(activeTab, 1, pageSize, appliedFilters, nextSort);
                loadDelegations(activeTab, 1, pageSize, appliedFilters, nextSort);
              }}
              title="Nhấn để sắp xếp theo thời gian tiếp khách"
            >
              Lịch tiếp
              <span className="text-[10px] text-blue-200 opacity-0 group-hover:opacity-100 transition-opacity">
                {sortOrder === 'desc' ? '▼' : '▲'}
              </span>
            </div>
            <div className="p-3 text-[12px] font-bold text-center uppercase tracking-wider">Trạng thái</div>
            <div className="p-3 text-[12px] font-bold text-center uppercase tracking-wider">Hành động</div>
          </div>
          <div className="flex flex-col">
            {isLoading ? (
              <div className="py-12 text-center text-slate-500 font-medium">Đang tải danh sách...</div>
            ) : listError ? (
              <div className="py-12 text-center text-red-500 font-medium"><AlertCircle className="w-8 h-8 mx-auto mb-2 text-red-400" /><p>{listError}</p></div>
            ) : rows.length > 0 ? rows.map((row, index) => {
              const isExpanded = expandedRequestId === row.visitRequestId;
              return (
              <Fragment key={row.id}>
              <div className={`grid grid-cols-[52px_minmax(0,1fr)_210px_150px_246px] items-center min-h-[78px] border-b border-slate-200/70 transition-colors duration-150 ${isExpanded ? 'bg-blue-50' : index % 2 === 0 ? 'bg-white' : 'bg-slate-50'} hover:bg-blue-50 group`}>
                <div className="py-3 px-3 text-center font-bold text-[#004c91] text-sm">{(currentPage - 1) * pageSize + index + 1}</div>
                <div className="py-3 px-3 min-w-0 flex flex-col justify-center pr-4">
                  <p className="text-sm font-bold text-[#004c91] line-clamp-2 break-words" title={row.name}>{row.name}</p>
                  <p className="text-xs font-medium text-slate-500 truncate" title={row.org}>{row.org}</p>
                  {!isHO && activeTab !== 'attending' && (
                    <p className="text-xs font-medium text-slate-600 mt-0.5 truncate">
                      <span className="text-slate-400">Host:</span> {row.host || (row.campusStatus === 'WAITING_REQUEST_APPROVAL' ? 'Chờ duyệt & gán host' : '-')}
                      <span className="mx-1 text-slate-300">|</span>
                      <span className="text-slate-400">Cơ sở:</span> {row.campus || '-'}
                    </p>
                  )}
                  {renderBadges(row)}
                  {row.canExpandCampuses && (
                    <button
                      type="button"
                      aria-expanded={isExpanded}
                      aria-label="Xem tiến trình theo từng cơ sở"
                      title="Xem tiến trình theo từng cơ sở"
                      onClick={(e) => { e.stopPropagation(); toggleExpanded(row.visitRequestId); }}
                      className="mt-1.5 inline-flex w-max items-center gap-1 rounded-md text-xs font-bold text-[#004c91] outline-none hover:underline cursor-pointer"
                    >
                      {isExpanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
                      {isExpanded ? 'Thu gọn cơ sở' : `Xem ${row.campusCount} cơ sở`}
                    </button>
                  )}
                </div>
                <div className="py-3 px-3 text-sm leading-6 text-slate-700">
                  <div className="flex items-center gap-2 whitespace-nowrap"><span className="w-9 text-slate-400 font-medium">Từ:</span><span className="font-semibold text-slate-800">{formatDateTimeShort(row.plannedStartAt)}</span></div>
                  <div className="flex items-center gap-2 whitespace-nowrap"><span className="w-9 text-slate-400 font-medium">Đến:</span><span className="font-semibold text-slate-800">{formatDateTimeShort(row.plannedEndAt)}</span></div>
                </div>
                <div className="py-3 px-3 flex flex-col items-center justify-center gap-1">{getStatusBadge(row)}</div>
                <div className="py-3 px-2 flex items-center justify-center" onClick={(e) => e.stopPropagation()}>{renderRowActions(row)}</div>
              </div>
              {isExpanded && row.canExpandCampuses && renderCampusAccordion(row)}
              </Fragment>
              );
            }) : (
              <div className="py-12 text-center text-slate-500 font-medium flex flex-col items-center justify-center"><Users className="w-12 h-12 text-slate-300 mb-3" /><p>{emptyText}</p></div>
            )}
          </div>
        </div>

        {/* Mobile / tablet */}
        <div className="lg:hidden w-full p-4 space-y-4 bg-slate-50/50">
          {isLoading ? (
            <div className="py-10 text-center text-slate-500 font-medium">Đang tải danh sách...</div>
          ) : rows.length > 0 ? rows.map((row) => {
            const isExpanded = expandedRequestId === row.visitRequestId;
            return (
            <Fragment key={row.id}>
            <div className={`rounded-2xl border bg-white p-4 shadow-sm transition-colors ${isExpanded ? 'border-[#004c91]/40' : 'border-slate-200 hover:border-[#004c91]/30'}`}>
              <div className="flex items-start justify-between gap-3 mb-2">
                <div className="min-w-0 flex-1">
                  <p className="font-bold text-[#004c91] text-sm line-clamp-2 leading-snug">{row.name}</p>
                  <p className="text-xs text-slate-500 truncate">{row.org}</p>
                </div>
                <div className="flex-shrink-0">{getStatusBadge(row)}</div>
              </div>
              {renderBadges(row)}
              <div className="grid grid-cols-1 gap-1.5 text-xs text-slate-600 bg-slate-50 p-3 rounded-xl border border-slate-100 mt-3">
                <div className="flex items-center gap-2"><Calendar className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" /><span className="truncate">{formatDateTimeShort(row.plannedStartAt)} <span className="text-slate-400 mx-1">→</span> {formatDateTimeShort(row.plannedEndAt)}</span></div>
                {!isHO && activeTab !== 'attending' && (
                  <>
                    <div className="flex items-center gap-2 mt-0.5"><Users className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" /><span className="truncate"><span className="text-slate-400">Host:</span> {row.host || (row.requestStatus === 'APPROVED' && isVisitor ? 'Đang phân công' : '-')}</span></div>
                    <div className="flex items-center gap-2 mt-0.5"><MapPin className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" /><span className="truncate"><span className="text-slate-400">Cơ sở:</span> {row.campus || '-'}</span></div>
                  </>
                )}
              </div>
              {row.canExpandCampuses && (
                <button
                  type="button"
                  aria-expanded={isExpanded}
                  aria-label="Xem tiến trình theo từng cơ sở"
                  title="Xem tiến trình theo từng cơ sở"
                  onClick={(e) => { e.stopPropagation(); toggleExpanded(row.visitRequestId); }}
                  className="mt-3 inline-flex w-full items-center justify-center gap-1 rounded-lg border border-slate-200 py-2 text-xs font-bold text-[#004c91] outline-none cursor-pointer"
                >
                  {isExpanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
                  {isExpanded ? 'Thu gọn cơ sở' : `Xem ${row.campusCount} cơ sở`}
                </button>
              )}
              <div className="mt-3 flex items-center justify-end border-t border-slate-100 pt-3" onClick={(e) => e.stopPropagation()}>{renderRowActions(row)}</div>
            </div>
            {isExpanded && row.canExpandCampuses && (
              <div className="overflow-hidden rounded-2xl border border-slate-200">{renderCampusAccordion(row)}</div>
            )}
            </Fragment>
            );
          }) : (
            <div className="py-10 text-center text-slate-500 font-medium flex flex-col items-center justify-center"><Users className="w-12 h-12 text-slate-300 mb-3" /><p>{emptyText}</p></div>
          )}
        </div>

        {/* Pagination */}
        {total > 0 && (
          <div className="p-6 border-t border-gray-100 flex flex-col sm:flex-row items-center justify-between gap-4 bg-gray-50/50">
            <div className="flex items-center gap-2">
              <span className="text-sm font-medium text-gray-500">Hiển thị</span>
              <div className="relative">
                <select value={pageSize} onChange={(e) => { const newSize = Number(e.target.value); setPageSize(newSize); setCurrentPage(1); updateUrlParams(activeTab, 1, newSize, appliedFilters, sortOrder); loadDelegations(activeTab, 1, newSize, appliedFilters, sortOrder); }} className="px-3 py-1.5 pr-8 rounded-lg border border-gray-200 text-sm font-bold text-gray-700 bg-white focus:outline-none appearance-none min-w-[70px] text-left">
                  <option value={5}>5</option><option value={10}>10</option><option value={20}>20</option><option value={50}>50</option>
                </select>
                <ChevronDown className="w-4 h-4 absolute right-2 top-1/2 -translate-y-1/2 text-gray-500 pointer-events-none" />
              </div>
              <span className="text-sm font-medium text-gray-500">bản ghi / trang</span>
            </div>
            <div className="flex items-center gap-2">
              <button onClick={() => { const p = Math.max(1, currentPage - 1); setCurrentPage(p); updateUrlParams(activeTab, p, pageSize, appliedFilters, sortOrder); loadDelegations(activeTab, p, pageSize, appliedFilters, sortOrder); }} disabled={currentPage === 1} className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all disabled:opacity-50 disabled:cursor-not-allowed outline-none"><ChevronLeft className="w-4 h-4" /></button>
              <div className="flex items-center gap-1">
                {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                  <button key={page} onClick={() => { setCurrentPage(page); updateUrlParams(activeTab, page, pageSize, appliedFilters, sortOrder); loadDelegations(activeTab, page, pageSize, appliedFilters, sortOrder); }} className={`w-8 h-8 rounded-lg text-sm font-bold transition-all outline-none ${currentPage === page ? 'bg-[#004c91] text-white shadow-sm' : 'text-gray-600 hover:bg-gray-200'}`}>{page}</button>
                ))}
              </div>
              <button onClick={() => { const p = Math.min(totalPages, currentPage + 1); setCurrentPage(p); updateUrlParams(activeTab, p, pageSize, appliedFilters, sortOrder); loadDelegations(activeTab, p, pageSize, appliedFilters, sortOrder); }} disabled={currentPage === totalPages} className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all disabled:opacity-50 disabled:cursor-not-allowed outline-none"><ChevronRight className="w-4 h-4" /></button>
            </div>
          </div>
        )}
      </div>

      {/* Xem form yêu cầu (readonly view of original request form) */}
      <SubmittedVisitRequestDetailModal
        isOpen={requestForm.open}
        visitRequestId={requestForm.row?.visitRequestId ?? null}
        onClose={() => setRequestForm({ open: false, row: null })}
      />

      {/* Shared submitted-form detail modal (with actions): pre-approval / rejected / waiting-host */}
      <SubmittedVisitRequestDetailModal
        isOpen={review.open}
        visitRequestId={review.row?.visitRequestId ?? null}
        onClose={() => setReview({ open: false, row: null })}
        onApprove={() => review.row && handleReviewApprove(review.row)}
        onReject={() => review.row && handleReviewReject(review.row)}
        onAssignHost={() => review.row && handleReviewAssignHost(review.row)}
      />

      {/* Reason (rejection) popup — full metadata: vai trò / người xử lý / thời gian / nội dung. */}
      <RejectedReasonModal
        isOpen={reason.open && !!reason.row}
        onClose={() => setReason({ open: false, row: null })}
        delegationName={reason.row?.name}
        requestCode={reason.row?.requestCode}
        visitScope={reason.row?.visitScope}
        decisionActorRole={reason.row?.decisionActorRole}
        decidedByName={reason.row?.decidedByName}
        decidedByUserId={reason.row?.decidedBy}
        decidedAt={reason.row?.decidedAt}
        decisionNote={reason.row?.decisionNote}
        contextLabel={reason.row?.campus || null}
      />

      {/* Campus-independent approval: không còn modal HO duyệt liên cơ sở — mọi quyết định
          thuộc Staff Leader từng campus (modal "Duyệt & gán host" bên dưới). */}

      {/* Reject modal */}
      {reject.open && reject.row && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
          <motion.div initial={{ opacity: 0, scale: 0.95, y: 10 }} animate={{ opacity: 1, scale: 1, y: 0 }} className="bg-white rounded-3xl shadow-2xl w-full max-w-md overflow-hidden relative">
            <div className="px-6 py-4 bg-red-600 flex items-center justify-between">
              <h3 className="text-lg font-bold text-white flex items-center gap-2"><AlertCircle className="w-5 h-5 bg-white/20 rounded-full p-0.5" /> {reject.action === ('DECLINE_INVITATION' as any) ? 'Từ chối lời mời' : 'Từ chối cơ sở này'}</h3>
              <button type="button" disabled={reject.submitting} onClick={() => setReject({ open: false, row: null, action: null, text: '', submitting: false, error: null })} className="text-white/80 hover:text-white hover:bg-white/10 rounded-full p-1.5"><X className="w-5 h-5" /></button>
            </div>
            <div className="p-6">
              <p className="text-sm text-gray-700 mb-3">
                {reject.action === ('DECLINE_INVITATION' as any)
                  ? <>Vui lòng nhập lý do từ chối lời mời của đoàn <span className="font-bold text-[#004c91]">{reject.row.name}</span>:</>
                  : <>Vui lòng nhập lý do từ chối tiếp nhận đoàn <span className="font-bold text-[#004c91]">{reject.row.name}</span> tại cơ sở <span className="font-bold text-[#004c91]">{reject.row.campus || 'của bạn'}</span>. Các cơ sở khác (nếu có) không bị ảnh hưởng:</>}
              </p>
              <textarea value={reject.text} onChange={(e) => setReject((s) => ({ ...s, text: e.target.value }))} placeholder="Nhập lý do chi tiết..." className="w-full px-4 py-3 rounded-2xl border border-gray-200 focus:border-red-500 focus:ring-4 focus:ring-red-500/10 outline-none transition-all text-sm min-h-[120px] resize-none bg-gray-50/50 focus:bg-white" disabled={reject.submitting} />
              {reject.error && <p className="text-red-500 text-sm mt-2">{reject.error}</p>}
            </div>
            <div className="px-6 py-4 bg-gray-50 flex items-center justify-end gap-3 border-t border-gray-100">
              <button type="button" disabled={reject.submitting} onClick={() => setReject({ open: false, row: null, action: null, text: '', submitting: false, error: null })} className="px-6 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-50 transition-all outline-none text-sm">Hủy bỏ</button>
              <button type="button" disabled={!reject.text.trim() || reject.submitting} onClick={submitReject} className="px-6 py-2.5 rounded-xl font-bold text-white bg-red-600 hover:bg-red-700 shadow-sm transition-all outline-none text-sm disabled:opacity-50 disabled:cursor-not-allowed">{reject.submitting ? 'Đang xử lý...' : 'Xác nhận từ chối'}</button>
            </div>
          </motion.div>
        </div>
      )}

      {/* Cancel modal */}
      {cancel.open && cancel.row && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
          <motion.div initial={{ opacity: 0, scale: 0.95, y: 10 }} animate={{ opacity: 1, scale: 1, y: 0 }} className="bg-white rounded-3xl shadow-2xl w-full max-w-md overflow-hidden relative border border-gray-100">
            <div className="px-6 py-4 bg-red-600 flex items-center justify-between">
              <h3 className="text-lg font-bold text-white flex items-center gap-2">
                <AlertCircle className="w-5 h-5 bg-white/20 rounded-full p-0.5" />
                {cancel.row.requestStatus === 'PENDING_APPROVAL' ? 'Hủy đơn đăng ký tham quan'
                  : cancel.row.visitScope === 'SINGLE_CAMPUS' ? 'Hủy lịch thăm'
                    : cancel.instanceId ? `Hủy lịch thăm tại cơ sở ${cancel.row.campus}`
                      : 'Hủy toàn bộ lịch thăm liên cơ sở'}
              </h3>
              <button type="button" disabled={cancel.submitting} onClick={() => setCancel({ open: false, row: null, mode: null, text: '', submitting: false, error: null, confirmed: false })} className="text-white/85 hover:text-white hover:bg-white/10 rounded-full p-1.5 cursor-pointer"><X className="w-5 h-5" /></button>
            </div>
            <div className="p-6 space-y-4">
              <div>
                <p className="text-sm font-semibold text-gray-800 mb-1">
                  {cancel.row.requestStatus === 'PENDING_APPROVAL' ? 'Bạn đang hủy đơn đăng ký tham quan này. Sau khi hủy, đơn sẽ không tiếp tục được phê duyệt và bạn sẽ không thể khôi phục lại đơn này.'
                    : cancel.row.visitScope === 'SINGLE_CAMPUS' ? 'Bạn đang hủy lịch thăm đã được duyệt. Sau khi hủy, lịch tiếp khách tại cơ sở này sẽ bị hủy và không thể khôi phục.'
                      : cancel.instanceId ? `Bạn đang hủy lịch thăm tại cơ sở ${cancel.row.campus}. Các cơ sở khác trong đơn liên cơ sở sẽ không bị ảnh hưởng.`
                        : 'Bạn đang hủy toàn bộ lịch thăm liên cơ sở. Tất cả cơ sở trong đơn này sẽ bị hủy và không thể khôi phục.'}
                </p>
                <p className="text-sm text-gray-500">
                  {cancel.row.requestStatus === 'PENDING_APPROVAL' ? 'Nếu bạn vẫn muốn tham quan vào thời gian khác, vui lòng tạo đơn đăng ký mới.'
                    : cancel.row.visitScope === 'SINGLE_CAMPUS' ? 'Nhà trường có thể đã chuẩn bị nhân sự, phòng họp hoặc hậu cần cho lịch thăm này. Vui lòng nhập lý do hủy rõ ràng.'
                      : cancel.instanceId ? 'Chỉ lịch thăm tại cơ sở này bị hủy. Những cơ sở đã diễn ra, đang diễn ra hoặc đã hoàn tất sẽ không thể hủy.'
                        : 'Hành động này sẽ ảnh hưởng đến toàn bộ lịch tiếp khách tại các cơ sở đã được sắp xếp. Nếu bạn chỉ muốn thay đổi một phần lịch trình, vui lòng liên hệ người phụ trách trước khi hủy.'}
                </p>
              </div>

              {cancel.mode === 'host' && (
                <p className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs font-medium text-red-700">
                  Trường hợp Host hủy là do Visitor đã xác nhận hủy ngoài hệ thống.
                </p>
              )}

              <div>
                <label className="block text-sm font-bold text-gray-700 mb-2">Lý do hủy <span className="text-red-500">*</span></label>
                <textarea value={cancel.text} onChange={(e) => setCancel((s) => ({ ...s, text: e.target.value, error: null }))} maxLength={2000} placeholder="Nhập lý do hủy..." className="w-full px-4 py-3 rounded-2xl border border-gray-200 focus:border-red-500 focus:ring-4 focus:ring-red-500/10 outline-none transition-all text-sm min-h-[100px] resize-none bg-gray-50/50 focus:bg-white" disabled={cancel.submitting} />
              </div>
              
              <label className="flex items-start gap-3 cursor-pointer group p-1">
                <div className="flex items-center h-5">
                  <input type="checkbox" checked={cancel.confirmed} onChange={(e) => setCancel((s) => ({ ...s, confirmed: e.target.checked, error: null }))} disabled={cancel.submitting} className="w-4 h-4 rounded border-gray-300 text-red-600 focus:ring-red-600/20 cursor-pointer" />
                </div>
                <div className="flex flex-col">
                  <span className="text-sm font-semibold text-gray-700 group-hover:text-red-600 transition-colors">Tôi hiểu rằng thao tác hủy không thể hoàn tác.</span>
                </div>
              </label>

              {cancel.error && <p className="text-red-500 text-sm mt-2">{cancel.error}</p>}
            </div>
            <div className="px-6 py-4 bg-gray-50 flex items-center justify-end gap-3 border-t border-gray-100">
              <button type="button" disabled={cancel.submitting} onClick={() => setCancel({ open: false, row: null, mode: null, text: '', submitting: false, error: null, confirmed: false })} className="px-5 py-2 rounded-xl font-bold text-gray-600 hover:bg-gray-200 transition-colors outline-none text-sm cursor-pointer">Quay lại</button>
              <button type="button" disabled={!cancel.text.trim() || !cancel.confirmed || cancel.submitting} onClick={submitCancel} className="px-6 py-2 rounded-xl font-bold text-white bg-red-600 hover:bg-red-700 shadow-sm transition-all outline-none text-sm cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed">
                {cancel.submitting ? 'Đang xử lý...' : 
                 cancel.row.requestStatus === 'PENDING_APPROVAL' ? 'Xác nhận hủy đơn'
                 : cancel.row.visitScope === 'SINGLE_CAMPUS' ? 'Xác nhận hủy lịch thăm'
                 : cancel.instanceId ? 'Xác nhận hủy cơ sở này'
                 : 'Xác nhận hủy toàn bộ'}
              </button>
            </div>
          </motion.div>
        </div>
      )}

      {/* Cancellation reason popup (read-only) */}
      <CancellationReasonModal
        isOpen={cancelReason.open}
        onClose={() => setCancelReason({ open: false, row: null })}
        delegationName={cancelReason.row?.name}
        cancellationLevel={cancelReason.row?.cancellationLevel}
        cancelledByName={cancelReason.row?.cancelledByName}
        cancelledByUserId={cancelReason.row?.cancelledBy}
        cancelledAt={cancelReason.row?.cancelledAt}
        cancellationActorType={cancelReason.row?.cancellationActorType}
        cancellationSource={cancelReason.row?.cancellationSource}
        cancellationReason={cancelReason.row?.cancellationReason}
        contextLabel={cancelReason.row?.cancellationLevel === 'CAMPUS_INSTANCE' ? (cancelReason.row?.campus || null) : null}
      />

      {/* Assign / transfer host modal */}
      {assign.open && assign.row && (
        <AssignHostModal
          isOpen={assign.open}
          mode={assign.mode}
          visitRequestId={assign.row.visitRequestId}
          visitInstanceId={assign.row.visitInstanceId}
          delegationName={assign.row.name}
          currentHostUserId={assign.row.currentHostUserId}
          onClose={() => setAssign({ open: false, row: null, mode: 'approve' })}
          onConfirmed={() => {
            setAssign({ open: false, row: null, mode: 'approve' });
            pushToast('success', 'Đã duyệt cơ sở và gán host phụ trách.');
            loadDelegations(activeTab, currentPage, pageSize, appliedFilters);
          }}
        />
      )}

      {/* Modal đánh giá chuyến thăm — mở ngay trên danh sách, không chuyển route */}
      <VisitFeedbackModal
        open={feedbackModalInstanceId !== null}
        visitInstanceId={feedbackModalInstanceId}
        onClose={() => setFeedbackModalInstanceId(null)}
        onSubmitted={handleFeedbackSubmitted}
      />

      {/* Toast viewport (success/failure notifications) */}
      {toasts.length > 0 && (
        <div className="fixed bottom-5 right-5 z-[200] flex flex-col gap-2 w-[min(92vw,360px)]">
          {toasts.map((t) => (
            <motion.div
              key={t.id}
              initial={{ opacity: 0, x: 24 }}
              animate={{ opacity: 1, x: 0 }}
              role="status"
              className={`flex items-start gap-2 rounded-xl border px-4 py-3 text-sm font-semibold shadow-lg ${
                t.type === 'success'
                  ? 'bg-emerald-50 border-emerald-200 text-emerald-800'
                  : 'bg-red-50 border-red-200 text-red-700'
              }`}
            >
              {t.type === 'success' ? <Check className="mt-0.5 h-4 w-4 flex-shrink-0" /> : <AlertCircle className="mt-0.5 h-4 w-4 flex-shrink-0" />}
              <span className="flex-1">{t.msg}</span>
              <button type="button" aria-label="Đóng" onClick={() => setToasts((prev) => prev.filter((x) => x.id !== t.id))} className="text-current/70 hover:text-current">
                <X className="h-4 w-4" />
              </button>
            </motion.div>
          ))}
        </div>
      )}
    </div>
  );
}
