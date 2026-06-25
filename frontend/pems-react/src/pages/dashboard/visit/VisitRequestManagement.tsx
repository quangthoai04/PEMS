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
} from 'lucide-react';
import { motion } from 'motion/react';
import { useNavigate } from 'react-router-dom';
import { VisitDetailsModal } from '../../../components/modals/VisitDetailsModal';
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

type Tab = 'responsible' | 'attending';

type ActionTone = 'blue' | 'green' | 'red' | 'gray' | 'orange';

// Lightweight in-page toast (cùng pattern với CampusManagement — không thêm thư viện mới).
type Toast = { id: number; type: 'success' | 'error'; msg: string };

const ActionIconButton = ({
  title, icon, tone = 'blue', onClick,
}: {
  title: string;
  icon: React.ReactNode;
  tone?: ActionTone;
  onClick: (e: React.MouseEvent<HTMLButtonElement>) => void;
}) => {
  const toneClassMap: Record<ActionTone, string> = {
    blue: 'text-slate-500 hover:text-[#004c91] hover:bg-blue-50',
    green: 'text-green-500 hover:text-green-600 hover:bg-green-50',
    red: 'text-red-500 hover:text-red-600 hover:bg-red-50',
    gray: 'text-slate-400 hover:text-slate-600 hover:bg-slate-100',
    orange: 'text-orange-500 hover:text-orange-600 hover:bg-orange-50',
  };
  return (
    <button
      type="button"
      title={title}
      aria-label={title}
      onClick={onClick}
      className={`inline-flex h-9 w-9 items-center justify-center rounded-lg transition-colors outline-none cursor-pointer ${toneClassMap[tone]}`}
    >
      {icon}
    </button>
  );
};

// Map (requestStatus, campusStatus) → nhãn tiếng Việt. Request status quyết định trước.
const getVietnameseStatus = (reqStatus?: string | null, campStatus?: string | null) => {
  if (campStatus === 'CANCELLED' || reqStatus === 'CANCELLED') return 'Đã hủy';
  if (reqStatus === 'REJECTED') return 'Từ chối';
  if (reqStatus === 'PENDING_APPROVAL') return 'Chờ duyệt';
  if (reqStatus === 'APPROVED') {
    if (campStatus === 'WAITING_HOST_ASSIGNMENT') return 'Chờ phân công Host';
    if (campStatus === 'ASSIGNED') return 'Đã phân công Host';
    if (campStatus === 'BEFORE_VISIT') return 'Trước tiếp khách';
    if (campStatus === 'DURING_VISIT') return 'Trong tiếp khách';
    if (campStatus === 'AFTER_VISIT') return 'Chờ đóng đoàn';
    if (campStatus === 'CLOSED') return 'Đã đóng đoàn';
    return 'Đã duyệt';
  }
  return reqStatus ?? '-';
};

// Map campus instanceStatus CODE → nhãn tiếng Việt + class badge (dùng cho accordion liên cơ sở).
// Chỉ để render hiển thị; KHÔNG dùng để gate action (action lấy từ boolean backend trả về).
const CAMPUS_STATUS_LABELS: Record<string, string> = {
  WAITING_REQUEST_APPROVAL: 'Chờ duyệt đơn',
  WAITING_HOST_ASSIGNMENT: 'Chờ phân công Host',
  ASSIGNED: 'Đã phân công Host',
  BEFORE_VISIT: 'Trước tiếp khách',
  DURING_VISIT: 'Đang tiếp khách',
  AFTER_VISIT: 'Chờ đóng đoàn',
  CLOSED: 'Đã đóng đoàn',
  CANCELLED: 'Đã hủy',
};
const getCampusStatusLabel = (status?: string | null) => (status && CAMPUS_STATUS_LABELS[status]) || status || '-';
const getCampusStatusBadgeClass = (status?: string | null) => {
  switch (status) {
    case 'WAITING_REQUEST_APPROVAL':
    case 'WAITING_HOST_ASSIGNMENT': return 'bg-yellow-50 text-yellow-700 border-yellow-200';
    case 'ASSIGNED': return 'bg-cyan-50 text-cyan-700 border-cyan-200';
    case 'BEFORE_VISIT': return 'bg-blue-50 text-blue-700 border-blue-200';
    case 'DURING_VISIT': return 'bg-green-50 text-green-700 border-green-200';
    case 'AFTER_VISIT': return 'bg-orange-50 text-orange-700 border-orange-200';
    case 'CLOSED': return 'bg-slate-100 text-slate-700 border-slate-300';
    case 'CANCELLED': return 'bg-gray-100 text-gray-600 border-gray-200';
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
  
  const defaultTab: Tab = (isStudent || isDept) ? 'attending' : 'responsible';
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

  const createEmptyFilters = () => ({ keyword: '', status: '', visitScopes: [] as string[], relation: '', fromDate: '', toDate: '', campusId: '' });
  const [draftFilters, setDraftFilters] = useState(createEmptyFilters());
  const [appliedFilters, setAppliedFilters] = useState(createEmptyFilters());
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
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [sortOrder, setSortOrder] = useState<'desc' | 'asc'>('desc');
  const [debouncedKeyword, setDebouncedKeyword] = useState(draftFilters.keyword);

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedKeyword(draftFilters.keyword.trim());
    }, 400);
    return () => clearTimeout(timer);
  }, [draftFilters.keyword]);

  useEffect(() => {
    if (debouncedKeyword !== appliedFilters.keyword) {
      const nextFilters = { ...appliedFilters, keyword: debouncedKeyword };
      setAppliedFilters(nextFilters);
      setCurrentPage(1);
      loadDelegations(activeTab, 1, pageSize, nextFilters, sortOrder);
    }
  }, [debouncedKeyword]);

  // Modals
  const [view, setView] = useState<{ open: boolean; row: Row | null }>({ open: false, row: null });
  // "Xem đơn đăng ký tham quan trước khi duyệt" — read-only review of a PENDING_APPROVAL row.
  const [review, setReview] = useState<{ open: boolean; row: Row | null }>({ open: false, row: null });
  const [reason, setReason] = useState<{ open: boolean; row: Row | null }>({ open: false, row: null });
  // UC-136: read-only popup of the cancellation reason (Host / Visitor / Staff Leader / HO).
  const [cancelReason, setCancelReason] = useState<{ open: boolean; row: Row | null }>({ open: false, row: null });
  const [approveConfirm, setApproveConfirm] = useState<{ open: boolean; row: Row | null; submitting: boolean; error: string | null }>({ open: false, row: null, submitting: false, error: null });
  const [reject, setReject] = useState<{ open: boolean; row: Row | null; action: AllowedAction | null; text: string; submitting: boolean; error: string | null }>({ open: false, row: null, action: null, text: '', submitting: false, error: null });
  const [cancel, setCancel] = useState<{ open: boolean; row: Row | null; mode: 'visitor' | 'host' | null; instanceId?: number | null; text: string; submitting: boolean; error: string | null }>({ open: false, row: null, mode: null, instanceId: null, text: '', submitting: false, error: null });
  const [assign, setAssign] = useState<{ open: boolean; row: Row | null; mode: 'approve' | 'transfer' }>({ open: false, row: null, mode: 'approve' });

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
    if (draftFilters.fromDate && draftFilters.toDate && draftFilters.fromDate > draftFilters.toDate) {
      setFilterError('Từ ngày không được lớn hơn Đến ngày.');
      return;
    }
    setFilterError(null);
    const nextFilters = { ...draftFilters };
    setAppliedFilters(nextFilters);
    setCurrentPage(1);
    loadDelegations(activeTab, 1, pageSize, nextFilters, sortOrder);
  };
  const handleResetFilters = () => {
    const empty = createEmptyFilters();
    setDraftFilters(empty);
    setAppliedFilters(empty);
    setFilterError(null);
    setCurrentPage(1);
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
        if (option?.visitScopes?.length) params.visitScopes = option.visitScopes.join(',');
        if (option?.readOnlyOnly) params.readOnlyOnly = true;
        if (option?.actionableOnly) params.actionableOnly = true;
        if (option?.timing) params.timing = option.timing;
        if (option?.relation) params.relation = option.relation;
      }
      
      if (filterConfig.showScope && targetFilters.visitScopes.length > 0) {
        // Only merge with option if option didn't already set it, or let option override
        if (!params.visitScopes) params.visitScopes = targetFilters.visitScopes.join(',');
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
        };
        const keyword = targetFilters.keyword.trim();
        if (keyword) invParams.keyword = keyword;
        if (targetFilters.status) invParams.invitationStatus = targetFilters.status;
        if (targetFilters.fromDate) invParams.fromDate = targetFilters.fromDate;
        if (targetFilters.toDate) invParams.toDate = targetFilters.toDate;

        const response = await delegationsApi.visitInvitations.getMyInvitations(invParams);
        const items: any[] = response.items || [];
        const mapped: Row[] = items.map((item) => {
          let statusText = item.invitationStatus;
          if (statusText === 'INVITED') statusText = 'Chờ phản hồi';
          else if (statusText === 'ACCEPTED') statusText = (isDept && subRole === 'STAFF') ? 'Đã nhận' : 'Đã nhận lời';
          else if (statusText === 'ASSIGNED') statusText = 'Mới được giao';
          else if (statusText === 'DECLINED') statusText = 'Đã từ chối';

          return {
            ...item,
            id: item.visitInstanceId || item.visitRequestId,
            name: item.delegationName || 'Không có tên',
            org: item.invitedByName ? `Người mời: ${item.invitedByName}` : '-',
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
  const opensSubmittedDetail = (row: Row) => {
    if (activeTab === 'attending') return false;
    const st = row.requestStatus;
    // Pre-approval review
    if (st === 'PENDING_APPROVAL'
      && ((isHO && row.visitScope === 'MULTI_CAMPUS')
        || (isStaffLeader && row.visitScope === 'SINGLE_CAMPUS' && row.campusStatus === 'WAITING_REQUEST_APPROVAL'))) {
      return true;
    }
    // Rejected detail — single (Staff Leader own campus) / multi (HO) / visitor's own
    if (st === 'REJECTED'
      && ((isHO && row.visitScope === 'MULTI_CAMPUS')
        || (isStaffLeader && row.visitScope === 'SINGLE_CAMPUS')
        || isVisitor)) {
      return true;
    }
    // Approved multi-campus waiting host (Staff Leader picks the official host)
    if (st === 'APPROVED' && isStaffLeader && row.visitScope === 'MULTI_CAMPUS'
      && row.campusStatus === 'WAITING_HOST_ASSIGNMENT') {
      return true;
    }
    return false;
  };

  const handleView = (row: Row) => {
    const idForRoute = row.id;
    // Shared submitted-form detail (read-only) takes priority for in-scope reviewers — it must
    // run BEFORE the rejected/cancelled fallback so rejected rows show the real form + reason.
    if (opensSubmittedDetail(row)) {
      setReview({ open: true, row });
      return;
    }
    // Instance-level cancel (campus CANCELLED while the request is still APPROVED): internal roles
    // (not Visitor) review the prepared setup in a READ-ONLY VisitProcess when a host had been
    // assigned. Whole-request CANCELLED / REJECTED still fall through to the read-only preview modal.
    if (activeTab !== 'attending' && row.campusStatus === 'CANCELLED' && row.requestStatus !== 'CANCELLED'
        && !isVisitor && row.currentHostUserId && row.visitInstanceId) {
      navigate(`/dashboard/visit/process/${row.visitInstanceId}`, { state: { isReadOnly: true, cancelled: true, status: 'Đã hủy' } });
      return;
    }
    if (row.requestStatus === 'CANCELLED' || row.campusStatus === 'CANCELLED' || row.requestStatus === 'REJECTED') {
      setView({ open: true, row });
      return;
    }
    if (activeTab === 'attending') {
      const partId = (row as any).participantId;
      if (isDept && subRole === 'STAFF') {
        navigate(`/dashboard/visit/department-tasks/${partId}`);
      } else {
        navigate(`/dashboard/visit/invitations/${partId}`);
      }
      return;
    }
    if (isVisitor) {
      if (row.host && row.requestStatus === 'APPROVED') navigate(`/dashboard/visit/reception-detail/${idForRoute}`);
      else setView({ open: true, row });
      return;
    }
    if (isHO) {
      // HO processes MULTI_CAMPUS on the dedicated HO detail page; SINGLE_CAMPUS is
      // read-only monitoring (chốt 2026-06) → show the read-only details modal instead.
      if (row.visitScope === 'SINGLE_CAMPUS') setView({ open: true, row });
      else navigate(`/dashboard/visit/ho-detail/${idForRoute}`, { state: { guestData: row } });
      return;
    }
    // Campus actors (Staff Leader / Staff host): điều hướng theo STATUS CODE thật, không dùng
    // statusText. statusText chỉ truyền xuống trang process để hiển thị. Giữ đúng outcome cũ.
    const rs = row.requestStatus;
    const cs = row.campusStatus;
    const isApprovedGeneric = rs === 'APPROVED' && cs == null; // tương đương nhãn cũ "Đã duyệt"
    const displayStatus = row.statusText;
    if (rs === 'PENDING_APPROVAL' || (isStaffLeader && isApprovedGeneric)) {
      setView({ open: true, row });
    } else if (cs === 'ASSIGNED' || cs === 'BEFORE_VISIT' || isApprovedGeneric) {
      // Staff Leader theo dõi tiến độ Host chuẩn bị (read-only) ngay từ khi ASSIGNED; Host vào để thao tác.
      navigate(`/dashboard/visit/process/${idForRoute}`, { state: { isPrep: true, status: displayStatus, isReadOnly: isStaffLeader } });
    } else if (cs === 'DURING_VISIT') {
      navigate(`/dashboard/visit/process/${idForRoute}`, { state: { defaultTab: 'during', status: displayStatus, isReadOnly: isStaffLeader } });
    } else if (cs === 'AFTER_VISIT') {
      navigate(`/dashboard/visit/process/${idForRoute}`, { state: { defaultTab: 'after', status: displayStatus, isReadOnly: isStaffLeader } });
    } else if (cs === 'CLOSED') {
      navigate(`/dashboard/visit/process/${idForRoute}`, { state: { defaultTab: 'before', status: displayStatus, isReadOnly: isStaffLeader } });
    } else {
      setView({ open: true, row });
    }
  };

  // ── Pre-approval review modal → reuse the existing approve/reject flows ──
  // The review modal never calls approve/reject itself; it just routes to the same
  // commands used by the row action buttons (HO: ho-approve / ho-reject, Staff Leader:
  // approve-and-assign-host / campus-reject).
  const handleReviewApprove = (row: Row) => {
    setReview({ open: false, row: null });
    if (isHO) setApproveConfirm({ open: true, row, submitting: false, error: null });
    else setAssign({ open: true, row, mode: 'approve' });
  };
  const handleReviewReject = (row: Row) => {
    setReview({ open: false, row: null });
    setReject({ open: true, row, action: isHO ? 'HO_REJECT' : 'CAMPUS_REJECT', text: '', submitting: false, error: null });
  };
  const handleReviewAssignHost = (row: Row) => {
    setReview({ open: false, row: null });
    setAssign({ open: true, row, mode: 'approve' });
  };

  // ── Action handlers ──
  const submitApprove = async () => {
    if (!approveConfirm.row) return;
    setApproveConfirm((s) => ({ ...s, submitting: true, error: null }));
    try {
      await delegationsApi.hoApprove(approveConfirm.row.visitRequestId);
      setApproveConfirm({ open: false, row: null, submitting: false, error: null });
      pushToast('success', 'Duyệt đơn thành công.');
      await loadDelegations(activeTab, currentPage, pageSize, appliedFilters, sortOrder);
    } catch (e: any) {
      const msg = apiErrorMessage(e, 'Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.');
      setApproveConfirm((s) => ({ ...s, submitting: false, error: `Không thể duyệt đơn. ${msg}` }));
    }
  };

  const submitReject = async () => {
    if (!reject.row || !reject.action) return;
    const text = reject.text.trim();
    if (!text) { setReject((s) => ({ ...s, error: 'Vui lòng nhập lý do từ chối.' })); return; }
    setReject((s) => ({ ...s, submitting: true, error: null }));
    try {
      if (reject.action === 'HO_REJECT') await delegationsApi.hoReject(reject.row.visitRequestId, text);
      else if (reject.action === 'DECLINE_INVITATION' as any) await delegationsApi.visitInvitations.declineInvitation((reject.row as any).participantId, text);
      else await delegationsApi.campusReject(reject.row.visitRequestId, text);
      const wasDecline = reject.action === ('DECLINE_INVITATION' as any);
      setReject({ open: false, row: null, action: null, text: '', submitting: false, error: null });
      pushToast('success', wasDecline ? 'Từ chối lời mời thành công.' : 'Từ chối đơn thành công.');
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
      setCancel({ open: false, row: null, mode: null, instanceId: null, text: '', submitting: false, error: null });
      pushToast('success', 'Đã hủy lịch thăm thành công.');
      await loadDelegations(activeTab, currentPage, pageSize, appliedFilters, sortOrder);
    } catch (e: any) {
      // Surface the backend's real business message (clean Vietnamese sentence such as
      // "Không thể hủy lịch thăm. Đơn đang chờ duyệt..."); apiErrorMessage walks
      // message → error → errors → title and only then a generic safe fallback.
      setCancel((s) => ({ ...s, submitting: false, error: apiErrorMessage(e, 'Không thể hủy lịch thăm. Vui lòng thử lại sau.') }));
    }
  };

  const isPendingHostAssignment = (row: Row) => {
    return isStaffLeader &&
      row.visitScope === 'MULTI_CAMPUS' &&
      row.requestStatus === 'APPROVED' &&
      (row.currentUserRelation === 'PENDING_HOST_ASSIGNMENT' || row.currentUserRelation === 'TEMP_CAMPUS_RESPONSIBLE') &&
      (row.campusStatus === 'WAITING_HOST_ASSIGNMENT' || row.campusStatus === 'ASSIGNED' || row.campusStatus === 'BEFORE_VISIT');
  };

  const isOperationalOrFinished = (row: Row) => {
    return row.campusStatus === 'DURING_VISIT' ||
      row.campusStatus === 'AFTER_VISIT' ||
      row.campusStatus === 'CLOSED';
  };

  const isCancelledOrRejected = (row: Row) => {
    return row.requestStatus === 'CANCELLED' ||
      row.campusStatus === 'CANCELLED' ||
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
    } else if (row.visitScope === 'MULTI_CAMPUS' && row.requestStatus === 'APPROVED') {
      badges.push(chip('multi-approved', 'Liên cơ sở · HO đã duyệt', 'bg-indigo-50 text-indigo-700 border-indigo-200'));
    } else if (row.visitScope) {
      const single = row.visitScope === 'SINGLE_CAMPUS';
      badges.push(chip('scope', VISIT_SCOPE_LABELS[row.visitScope] + (row.campusCount > 1 ? ` (${row.campusCount})` : ''),
        single ? 'bg-sky-50 text-sky-700 border-sky-200' : 'bg-indigo-50 text-indigo-700 border-indigo-200'));
    }

    if (isPendingHostAssignment(row) && !isCancelledOrRejected(row) && !isOperationalOrFinished(row)) {
      badges.push(chip('pending-host', 'Cần chọn Host chính thức', 'bg-orange-50 text-orange-700 border-orange-200'));
    } else if (!isStaffLeader && row.currentUserIsHost && activeTab !== 'attending') {
      badges.push(chip('host', 'Được giao làm host', 'bg-emerald-50 text-emerald-700 border-emerald-200'));
    }
    return badges.length ? <div className="flex flex-wrap gap-1 mt-1">{badges}</div> : null;
  };

  const getStatusBadge = (row: Row) => {
    let statusText = 'Không xác định';
    const base = 'inline-flex min-w-[96px] max-w-[150px] justify-center whitespace-nowrap rounded-full border px-2.5 py-1 text-xs font-semibold';
    
    if (activeTab === 'attending') {
      const text = row.statusText;
      let cls = 'bg-gray-100 text-gray-700 border-gray-200';
      if (text === 'Chờ phản hồi') cls = 'bg-yellow-50 text-yellow-700 border-yellow-200';
      else if (text === 'Đã nhận lời' || text === 'Đã nhận') cls = 'bg-green-50 text-green-700 border-green-200';
      else if (text === 'Mới được giao') cls = 'bg-blue-50 text-blue-700 border-blue-200';
      else if (text === 'Đã từ chối') cls = 'bg-red-50 text-red-700 border-red-200';
      return <span title={text} className={`${base} ${cls}`}>{text}</span>;
    }

    // Chuẩn hóa trạng thái hiển thị (AC-04): KHÔNG ghép request status với campus status
    // (bỏ kiểu "Đã duyệt · Đã phân công Host"). Trong màn vận hành theo campus/role ưu tiên
    // visit_request_campuses.status; request status chỉ dùng cho quyết định tổng. `kind` chọn
    // màu badge, nhãn theo vai trò (Visitor xem ngôn ngữ thân thiện hơn nội bộ).
    type StatusKind = 'pending' | 'rejected' | 'cancelled' | 'waiting_host' | 'assigned'
      | 'before' | 'during' | 'after' | 'closed' | 'approved';
    let kind: StatusKind;
    if (row.requestStatus === 'CANCELLED' || row.campusStatus === 'CANCELLED') kind = 'cancelled';
    else if (row.requestStatus === 'REJECTED') kind = 'rejected';
    else if (row.requestStatus === 'PENDING_APPROVAL') kind = 'pending';
    else if (row.requestStatus === 'APPROVED') {
      if (row.campusStatus === 'WAITING_HOST_ASSIGNMENT' || isPendingHostAssignment(row)) kind = 'waiting_host';
      else if (row.campusStatus === 'ASSIGNED') kind = 'assigned';
      else if (row.campusStatus === 'BEFORE_VISIT') kind = 'before';
      else if (row.campusStatus === 'DURING_VISIT') kind = 'during';
      else if (row.campusStatus === 'AFTER_VISIT') kind = 'after';
      else if (row.campusStatus === 'CLOSED') kind = 'closed';
      else kind = 'approved';
    } else kind = 'pending';

    let cancelledText = 'Đã hủy';
    if (kind === 'cancelled') {
      const actor = (row as any).cancellationActorType;
      if (actor === 'VISITOR') cancelledText = 'Khách đã hủy';
      else if (actor === 'HOST') cancelledText = 'Host đã hủy';
      else if (actor === 'SYSTEM') cancelledText = 'Hệ thống đã hủy';
    }

    const labelByKind: Record<StatusKind, string> = isVisitor ? {
      pending: 'Chờ duyệt', rejected: 'Đã bị từ chối', cancelled: cancelledText,
      waiting_host: 'Đang sắp xếp người phụ trách', assigned: 'Đã phân công người phụ trách',
      before: 'Sắp diễn ra', during: 'Đang diễn ra', after: 'Đã diễn ra',
      closed: 'Đã hoàn tất', approved: 'Đã được duyệt',
    } : {
      pending: 'Chờ duyệt', rejected: 'Từ chối', cancelled: cancelledText,
      waiting_host: 'Chờ gán host', assigned: 'Đã phân công host',
      before: 'Đang chuẩn bị', during: 'Trong tiếp khách', after: 'Chờ hoàn tất',
      closed: 'Đã đóng đoàn', approved: 'Đã duyệt',
    };

    const clsByKind: Record<StatusKind, string> = {
      pending: 'bg-yellow-50 text-yellow-700 border-yellow-200',
      waiting_host: 'bg-yellow-50 text-yellow-700 border-yellow-200',
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
      pending: 'Đơn đang chờ được phê duyệt',
      rejected: 'Đơn đã bị từ chối',
      cancelled: 'Đơn/cơ sở đã bị hủy',
      waiting_host: 'Đơn đã duyệt, chờ phân công người phụ trách',
      assigned: 'Đã phân công người phụ trách, chờ triển khai',
      before: 'Đang trong giai đoạn chuẩn bị đón tiếp',
      during: 'Đoàn đang diễn ra tại cơ sở',
      after: 'Đoàn đã kết thúc, chờ hoàn tất thủ tục',
      closed: 'Đoàn đã hoàn tất toàn bộ quy trình',
      approved: 'Đơn đã duyệt (chờ triển khai)',
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
    
    return (
      <div className="mx-auto grid w-[108px] grid-cols-3 gap-2 place-items-center">
        {/* Slot 1: View */}
        <ActionIconButton title="Xem chi tiết" tone="blue" icon={<Eye className="h-5 w-5" />} onClick={(e) => { e.stopPropagation(); handleView(row); }} />

        {/* Slot 2: Approve & assign host (one-time) / Accept / Reason. Host được gán MỘT lần — không có chuyển/đổi host. */}
        {can('HO_APPROVE') ? (
          <ActionIconButton title="Duyệt đơn liên cơ sở" tone="green" icon={<Check className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); setApproveConfirm({ open: true, row, submitting: false, error: null }); }} />
        ) : can('APPROVE_AND_ASSIGN_HOST') ? (
          <ActionIconButton title={isPendingHostAssignment(row) ? 'Chọn Host chính thức' : 'Duyệt & chọn host'} tone="green" icon={<Check className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); setAssign({ open: true, row, mode: 'approve' }); }} />
        ) : can('ACCEPT_INVITATION') ? (
          <ActionIconButton title="Chấp nhận lời mời" tone="green" icon={<Check className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); submitAcceptInvitation(row); }} />
        ) : can('ASSIGN_TO_DEPARTMENT_STAFF') ? (
          <ActionIconButton title="Giao việc cho Staff" tone="blue" icon={<Users className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); submitAssignDeptStaff(row); }} />
        ) : showReason ? (
          <ActionIconButton title="Xem lý do từ chối" tone="orange" icon={<AlertCircle className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); setReason({ open: true, row }); }} />
        ) : isCancelledRow ? (
          <ActionIconButton title="Xem lý do hủy" tone="gray" icon={<XCircle className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); setCancelReason({ open: true, row }); }} />
        ) : (
          <span className="h-9 w-9" aria-hidden="true" />
        )}

        {/* Slot 3: Reject / Cancel / Decline */}
        {(can('HO_REJECT') || can('CAMPUS_REJECT')) ? (
          <ActionIconButton title="Từ chối" tone="red" icon={<X className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); setReject({ open: true, row, action: can('HO_REJECT') ? 'HO_REJECT' : 'CAMPUS_REJECT', text: '', submitting: false, error: null }); }} />
        ) : can('DECLINE_INVITATION') ? (
          <ActionIconButton title="Từ chối lời mời" tone="red" icon={<X className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); setReject({ open: true, row, action: 'DECLINE_INVITATION' as any, text: '', submitting: false, error: null }); }} />
        ) : (can('CANCEL_BY_VISITOR') || can('CANCEL_BY_HOST')) ? (
          <ActionIconButton title="Hủy lịch thăm" tone="red" icon={<XCircle className="h-5 w-5" />}
            onClick={(e) => { e.stopPropagation(); setCancel({ open: true, row, mode: can('CANCEL_BY_HOST') ? 'host' : 'visitor', text: '', submitting: false, error: null }); }} />
        ) : (
          <span className="h-9 w-9" aria-hidden="true" />
        )}
      </div>
    );
  };

  // ── Multi-campus accordion (Phương án A): per-campus progress + actions ──
  const openCampusDetail = (row: Row, item: CampusProgressItem) => {
    // Campus đã hủy: nội bộ (không phải Visitor) xem lại setup cũ ở VisitProcess READ-ONLY nếu đã từng
    // có Host/setup; Visitor luôn dùng modal public-safe (không thấy setup/logistics/minutes nội bộ).
    if (item.instanceStatus === 'CANCELLED' && !isVisitor && item.hostUserId != null && item.visitInstanceId) {
      navigate(`/dashboard/visit/process/${item.visitInstanceId}`, { state: { isReadOnly: true, cancelled: true, status: 'Đã hủy' } });
      return;
    }
    const startTime = item.plannedStartAt ? new Date(item.plannedStartAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) : '';
    const endTime = item.plannedEndAt ? new Date(item.plannedEndAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) : '';
    const campusRow = {
      ...row,
      campus: item.campusName || '-',
      host: item.hostName || '',
      time: startTime && endTime ? `${startTime} - ${endTime}` : '',
      date: item.plannedStartAt ? formatDateOnly(item.plannedStartAt) : '',
      campusStatus: item.instanceStatus,
      visitInstanceId: item.visitInstanceId,
      isCancelled: item.instanceStatus === 'CANCELLED',
      cancellationLevel: 'CAMPUS_INSTANCE',
      cancelledByName: item.cancelledByName,
      cancelledBy: item.cancelledBy,
      cancelledAt: item.cancelledAt,
      cancellationActorType: item.cancellationActorType,
      cancellationSource: item.cancellationSource,
      cancellationReason: item.cancellationReason,
    } as Row;
    setView({ open: true, row: campusRow });
  };

  const openCampusCancel = (row: Row, item: CampusProgressItem) =>
    setCancel({ open: true, row, mode: 'visitor', instanceId: item.visitInstanceId, text: '', submitting: false, error: null });

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
      <div className="border-b border-slate-200 bg-slate-50 px-4 sm:px-6 py-4" onClick={(e) => e.stopPropagation()}>
        <div className="mb-3 flex items-center justify-between">
          <h4 className="text-sm font-bold text-[#004c91]">Tiến trình theo từng cơ sở</h4>
          <span className="text-xs font-semibold text-slate-500">{items.length} cơ sở</span>
        </div>
        {items.length === 0 ? (
          <p className="py-2 text-sm text-slate-500">Chưa có thông tin cơ sở cho đơn này.</p>
        ) : (
          <div className="divide-y divide-slate-100 overflow-hidden rounded-xl border border-slate-200 bg-white">
            {items.map((item) => (
              <div key={item.visitInstanceId} className="flex flex-col gap-2 p-3 sm:flex-row sm:items-center sm:gap-4">
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-bold text-[#004c91]">
                    {item.campusName || '-'}
                    {item.campusCode ? <span className="ml-1 text-xs font-medium text-slate-400">({item.campusCode})</span> : null}
                  </p>
                  <p className="truncate text-xs text-slate-500">
                    <span className="text-slate-400">Host:</span> {item.hostName || 'Đang phân công'}
                  </p>
                </div>
                <div className="text-xs text-slate-600 sm:w-[190px]">
                  <div className="whitespace-nowrap"><span className="w-9 text-slate-400">Từ:</span> {formatDateTimeShort(item.plannedStartAt)}</div>
                  <div className="whitespace-nowrap"><span className="w-9 text-slate-400">Đến:</span> {formatDateTimeShort(item.plannedEndAt)}</div>
                </div>
                <div className="sm:w-[150px]">
                  <span className={`inline-flex justify-center whitespace-nowrap rounded-full border px-2.5 py-1 text-xs font-semibold ${getCampusStatusBadgeClass(item.instanceStatus)}`}>
                    {getCampusStatusLabel(item.instanceStatus)}
                  </span>
                </div>
                <div className="flex items-center gap-1 sm:w-[100px] sm:justify-end">
                  {item.canViewCampusDetail && (
                    <ActionIconButton title="Xem chi tiết cơ sở" tone="blue" icon={<Eye className="h-5 w-5" />} onClick={() => openCampusDetail(row, item)} />
                  )}
                  {item.canViewCancelReason ? (
                    <ActionIconButton title="Xem lý do hủy" tone="gray" icon={<XCircle className="h-5 w-5" />} onClick={() => openCampusCancelReason(row, item)} />
                  ) : item.canCancelCampusVisit ? (
                    <ActionIconButton title="Hủy lịch thăm cơ sở" tone="red" icon={<XCircle className="h-5 w-5" />} onClick={() => openCampusCancel(row, item)} />
                  ) : null}
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

  const hasActiveFilter = !!(appliedFilters.keyword || appliedFilters.status || appliedFilters.visitScopes.length > 0 || appliedFilters.fromDate || appliedFilters.toDate);
  const emptyText = hasActiveFilter
    ? 'Không tìm thấy đơn phù hợp với bộ lọc.'
    : activeTab === 'attending'
      ? 'Bạn chưa có đơn mời tham dự nào.'
      : isVisitor
        ? 'Bạn chưa gửi đơn tiếp khách nào.'
        : 'Bạn chưa có đơn phụ trách nào.';

  return (
    <div className="w-full max-w-[1320px] mx-auto p-4 sm:p-6 lg:p-8 flex flex-col space-y-6 pb-12 animate-in fade-in duration-300">
      {/* Header */}
      {!isEmbedded && (
        <>
          <div className="mb-2 flex items-center text-sm font-medium text-gray-500">
            <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Dashboard</button>
            <span className="mx-2">/</span>
            <span className="text-[#004c91]">Quản lý tiếp khách</span>
          </div>
          <div className="border-b border-gray-100 pb-4 flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
            <h1 className="text-3xl font-bold text-[#004c91]">{isVisitor ? 'Đơn của tôi' : 'Quản lý tiếp khách'}</h1>
            {isRegularStaff ? (
              <button onClick={() => navigate('/dashboard/visit/create')} className="flex items-center justify-center gap-2 bg-[#F37021] hover:bg-orange-600 outline-none text-white px-4 py-2 rounded-lg font-bold shadow-sm transition-colors whitespace-nowrap w-full md:w-auto">
                <Plus className="w-5 h-5" /> Tạo đoàn khách
              </button>
            ) : isHO ? (
              <button onClick={() => navigate('/dashboard/visit/agenda-templates')} className="flex items-center justify-center gap-2 bg-[#F37021] hover:bg-orange-600 outline-none text-white px-4 py-2 rounded-lg font-bold shadow-sm transition-colors whitespace-nowrap w-full md:w-auto">
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
                onClick={() => navigate(`/dashboard/visit/invitations/${inv.participantId}`)}
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

      {/* Filters */}
      <div className="w-full rounded-2xl border border-slate-200 bg-white p-4 shadow-sm overflow-visible">
        <div className="flex flex-wrap items-end gap-3 w-full">
          <div className="min-w-[200px] flex-1">
            <label className="block text-xs font-bold text-slate-500 mb-1">Tìm kiếm</label>
            <div className="relative w-full">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 w-5 h-5" />
              <input type="text" placeholder="Tìm tên đoàn, host, đối tác..." value={draftFilters.keyword}
                onChange={(e) => {
                  const val = e.target.value;
                  setDraftFilters({ ...draftFilters, keyword: val });
                }}
                onKeyDown={(e) => { if (e.key === 'Enter') handleApplyFilters(); }}
                className="w-full pl-10 pr-4 h-11 bg-white border border-slate-300 rounded-xl text-sm font-semibold text-slate-700 outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10 transition-colors min-w-0" />
            </div>
          </div>

          {/* Status */}
          {filterConfig.showStatus && (
            <div className="relative min-w-[170px] flex-1 xl:flex-none">
              <label className="block text-xs font-bold text-slate-500 mb-1">{filterConfig.statusLabel || 'Trạng thái'}</label>
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
                        onClick={() => { setDraftFilters({ ...draftFilters, status: option.value }); setIsStatusFilterOpen(false); }}>
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
            <div className="relative min-w-[170px] flex-1 xl:flex-none">
              <label className="block text-xs font-bold text-slate-500 mb-1">{filterConfig.scopeLabel || 'Phạm vi'}</label>
              <button onClick={() => setIsTypeFilterOpen(!isTypeFilterOpen)} className="flex h-11 w-full min-w-0 items-center justify-between rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none transition-colors focus:border-[#004c91]">
                <span className="min-w-0 truncate">{draftFilters.visitScopes.length === 0 ? filterConfig.scopeOptions[0]?.label || 'Tất cả phạm vi' : draftFilters.visitScopes.length === 1 ? (filterConfig.scopeOptions.find((x) => x.value === draftFilters.visitScopes[0])?.label ?? '1 phạm vi') : `${draftFilters.visitScopes.length} phạm vi`}</span>
                <ChevronDown className="w-4 h-4 text-gray-500 flex-shrink-0 ml-2 pointer-events-none" />
              </button>
              {isTypeFilterOpen && (
                <>
                  <div className="fixed inset-0 z-10" onClick={() => setIsTypeFilterOpen(false)} />
                  <div className="absolute left-0 top-full z-30 mt-1 w-full min-w-[210px] rounded-xl border border-slate-200 bg-white py-1 shadow-lg">
                    {filterConfig.scopeOptions.map((scope) => (
                      <label key={scope.value} className="flex items-center px-3 py-2 hover:bg-slate-50 cursor-pointer">
                        {scope.value === '' ? (
                          <input type="checkbox" className="mr-2 rounded border-gray-300 text-[#004c91] focus:ring-[#004c91]" checked={draftFilters.visitScopes.length === 0}
                            onChange={(e) => {
                              if (e.target.checked) setDraftFilters({ ...draftFilters, visitScopes: [] });
                            }} />
                        ) : (
                          <input type="checkbox" className="mr-2 rounded border-gray-300 text-[#004c91] focus:ring-[#004c91]" checked={draftFilters.visitScopes.includes(scope.value)}
                            onChange={(e) => {
                              if (e.target.checked) setDraftFilters({ ...draftFilters, visitScopes: [...draftFilters.visitScopes, scope.value] });
                              else setDraftFilters({ ...draftFilters, visitScopes: draftFilters.visitScopes.filter((t) => t !== scope.value) });
                            }} />
                        )}
                        <span className="text-sm font-medium text-gray-700">{scope.label}</span>
                      </label>
                    ))}
                  </div>
                </>
              )}
            </div>
          )}

          {/* Campus */}
          {filterConfig.showCampus && (
            <div className="relative min-w-[170px] flex-1 xl:flex-none">
              <label className="block text-xs font-bold text-slate-500 mb-1">Cơ sở</label>
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
                        onClick={() => { setDraftFilters({ ...draftFilters, campusId: option.value }); setIsCampusFilterOpen(false); }}>
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
            <div className="relative min-w-[170px] flex-1 xl:flex-none">
              <label className="block text-xs font-bold text-slate-500 mb-1">{filterConfig.relationLabel || 'Loại xử lý'}</label>
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
                        onClick={() => { setDraftFilters({ ...draftFilters, relation: option.value }); setIsRelationFilterOpen(false); }}>
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
          <div className="relative min-w-[180px] flex-1 xl:flex-none">
            <label className="block text-xs font-bold text-slate-500 mb-1">Khoảng ngày</label>
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
                    <button onClick={() => setIsDateFilterOpen(false)} className="mt-2 h-9 w-full rounded-lg bg-slate-100 text-sm font-semibold text-slate-700 hover:bg-slate-200 transition-colors">Đóng</button>
                  </div>
                </div>
              </>
            )}
          </div>

          <button onClick={handleApplyFilters} className="inline-flex h-11 w-full xl:w-[112px] items-center justify-center rounded-xl bg-[#004c91] px-4 text-sm font-bold text-white transition-colors hover:bg-[#003b70] whitespace-nowrap flex-1 xl:flex-none">Áp dụng</button>
          <button onClick={handleResetFilters} title="Xóa bộ lọc" aria-label="Xóa bộ lọc" className="inline-flex h-11 w-11 items-center justify-center rounded-xl border border-slate-300 bg-white text-slate-500 transition-colors hover:bg-slate-50 hover:text-red-500 flex-shrink-0"><X className="h-5 w-5" /></button>
        </div>
        {filterError && <div className="text-red-500 text-sm font-medium mt-2"><AlertCircle className="w-4 h-4 inline-block mr-1" />{filterError}</div>}
      </div>

      {/* List */}
      <div className="w-full overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm flex flex-col">
        {/* Desktop */}
        <div className="hidden lg:block w-full">
          <div className="grid grid-cols-[52px_minmax(0,1fr)_210px_150px_140px] bg-[#004c91] text-white">
            <div className="p-3 text-[12px] font-bold text-center uppercase tracking-wider">STT</div>
            <div className="p-3 text-[12px] font-bold text-left uppercase tracking-wider">Thông tin đoàn</div>
            <div 
              className="p-3 text-[12px] font-bold text-left uppercase tracking-wider cursor-pointer hover:bg-[#003b70] transition-colors group flex items-center gap-1"
              onClick={() => {
                const nextSort = sortOrder === 'desc' ? 'asc' : 'desc';
                setSortOrder(nextSort);
                setCurrentPage(1);
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
              <div className={`grid grid-cols-[52px_minmax(0,1fr)_210px_150px_140px] items-center min-h-[78px] border-b border-slate-200/70 transition-colors duration-150 cursor-pointer ${isExpanded ? 'bg-blue-50' : index % 2 === 0 ? 'bg-white' : 'bg-slate-50'} hover:bg-blue-50 group`} onClick={() => handleView(row)}>
                <div className="py-3 px-3 text-center font-bold text-[#004c91] text-sm">{(currentPage - 1) * pageSize + index + 1}</div>
                <div className="py-3 px-3 min-w-0 flex flex-col justify-center pr-4">
                  <p className="text-sm font-bold text-[#004c91] line-clamp-2 break-words" title={row.name}>{row.name}</p>
                  <p className="text-xs font-medium text-slate-500 truncate" title={row.org}>{row.org}</p>
                  {!isHO && (
                    <p className="text-xs font-medium text-slate-600 mt-0.5 truncate">
                      <span className="text-slate-400">{isPendingHostAssignment(row) ? 'Người tiếp nhận tạm:' : 'Host:'}</span> {row.host || (row.requestStatus === 'APPROVED' && isVisitor ? 'Đang phân công' : '-')}
                      <span className="mx-1 text-slate-300">|</span>
                      <span className="text-slate-400">Cơ sở:</span> {row.campus || '-'}
                    </p>
                  )}
                  {renderBadges(row)}
                  {row.canExpandCampuses && row.requestStatus === 'APPROVED' && (
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
              {isExpanded && row.canExpandCampuses && row.requestStatus === 'APPROVED' && renderCampusAccordion(row)}
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
            <div className={`rounded-2xl border bg-white p-4 shadow-sm transition-colors cursor-pointer ${isExpanded ? 'border-[#004c91]/40' : 'border-slate-200 hover:border-[#004c91]/30'}`} onClick={() => handleView(row)}>
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
                {!isHO && (
                  <>
                    <div className="flex items-center gap-2 mt-0.5"><Users className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" /><span className="truncate"><span className="text-slate-400">Host:</span> {row.host || (row.requestStatus === 'APPROVED' && isVisitor ? 'Đang phân công' : '-')}</span></div>
                    <div className="flex items-center gap-2 mt-0.5"><MapPin className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" /><span className="truncate"><span className="text-slate-400">Cơ sở:</span> {row.campus || '-'}</span></div>
                  </>
                )}
              </div>
              {row.canExpandCampuses && row.requestStatus === 'APPROVED' && (
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
            {isExpanded && row.canExpandCampuses && row.requestStatus === 'APPROVED' && (
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
                <select value={pageSize} onChange={(e) => { const newSize = Number(e.target.value); setPageSize(newSize); setCurrentPage(1); loadDelegations(activeTab, 1, newSize, appliedFilters); }} className="px-3 py-1.5 pr-8 rounded-lg border border-gray-200 text-sm font-bold text-gray-700 bg-white focus:outline-none appearance-none min-w-[70px] text-left">
                  <option value={5}>5</option><option value={10}>10</option><option value={20}>20</option><option value={50}>50</option>
                </select>
                <ChevronDown className="w-4 h-4 absolute right-2 top-1/2 -translate-y-1/2 text-gray-500 pointer-events-none" />
              </div>
              <span className="text-sm font-medium text-gray-500">bản ghi / trang</span>
            </div>
            <div className="flex items-center gap-2">
              <button onClick={() => { const p = Math.max(1, currentPage - 1); setCurrentPage(p); loadDelegations(activeTab, p, pageSize, appliedFilters); }} disabled={currentPage === 1} className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all disabled:opacity-50 disabled:cursor-not-allowed outline-none"><ChevronLeft className="w-4 h-4" /></button>
              <div className="flex items-center gap-1">
                {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                  <button key={page} onClick={() => { setCurrentPage(page); loadDelegations(activeTab, page, pageSize, appliedFilters); }} className={`w-8 h-8 rounded-lg text-sm font-bold transition-all outline-none ${currentPage === page ? 'bg-[#004c91] text-white shadow-sm' : 'text-gray-600 hover:bg-gray-200'}`}>{page}</button>
                ))}
              </div>
              <button onClick={() => { const p = Math.min(totalPages, currentPage + 1); setCurrentPage(p); loadDelegations(activeTab, p, pageSize, appliedFilters); }} disabled={currentPage === totalPages} className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all disabled:opacity-50 disabled:cursor-not-allowed outline-none"><ChevronRight className="w-4 h-4" /></button>
            </div>
          </div>
        )}
      </div>

      {/* View modal */}
      <VisitDetailsModal isOpen={view.open} onClose={() => setView({ open: false, row: null })} guest={view.row} />

      {/* Shared submitted-form detail modal (read-only): pre-approval / rejected / waiting-host */}
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
      />

      {/* Approve confirm modal (HO) */}
      {approveConfirm.open && approveConfirm.row && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
          <motion.div initial={{ opacity: 0, scale: 0.95, y: 10 }} animate={{ opacity: 1, scale: 1, y: 0 }} className="bg-white rounded-3xl shadow-2xl w-full max-w-md overflow-hidden">
            <div className="px-6 py-4 bg-green-600 flex items-center justify-between">
              <h3 className="text-lg font-bold text-white flex items-center gap-2"><Check className="w-5 h-5 bg-white/20 rounded-full p-0.5" /> Duyệt đơn liên cơ sở</h3>
              <button type="button" disabled={approveConfirm.submitting} onClick={() => setApproveConfirm({ open: false, row: null, submitting: false, error: null })} className="text-white/85 hover:text-white hover:bg-white/10 rounded-full p-1.5 cursor-pointer"><X className="w-5 h-5" /></button>
            </div>
            <div className="p-6 space-y-3">
              <p className="text-sm text-gray-700">Bạn xác nhận <span className="font-bold text-green-700">duyệt</span> đơn liên cơ sở của đoàn <span className="font-bold text-[#004c91]">{approveConfirm.row.name}</span>?</p>
              <p className="text-xs text-slate-500">Sau khi duyệt, mỗi cơ sở sẽ được tạm gán host là Trưởng IC; Staff Leader từng cơ sở có thể chuyển host cho nhân sự phụ trách.</p>
              {approveConfirm.error && <p className="text-red-500 text-sm">{approveConfirm.error}</p>}
            </div>
            <div className="px-6 py-4 bg-gray-50 flex items-center justify-end gap-3 border-t border-gray-100">
              <button type="button" disabled={approveConfirm.submitting} onClick={() => setApproveConfirm({ open: false, row: null, submitting: false, error: null })} className="px-5 py-2 rounded-xl font-bold text-gray-600 hover:bg-gray-200 transition-colors outline-none text-sm cursor-pointer">Hủy bỏ</button>
              <button type="button" disabled={approveConfirm.submitting} onClick={submitApprove} className="px-6 py-2 rounded-xl font-bold text-white bg-green-600 hover:bg-green-700 shadow-sm transition-all outline-none text-sm cursor-pointer disabled:opacity-50">{approveConfirm.submitting ? 'Đang xử lý...' : 'Xác nhận duyệt'}</button>
            </div>
          </motion.div>
        </div>
      )}

      {/* Reject modal */}
      {reject.open && reject.row && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
          <motion.div initial={{ opacity: 0, scale: 0.95, y: 10 }} animate={{ opacity: 1, scale: 1, y: 0 }} className="bg-white rounded-3xl shadow-2xl w-full max-w-md overflow-hidden relative">
            <div className="px-6 py-4 bg-red-600 flex items-center justify-between">
              <h3 className="text-lg font-bold text-white flex items-center gap-2"><AlertCircle className="w-5 h-5 bg-white/20 rounded-full p-0.5" /> Từ chối đơn</h3>
              <button type="button" disabled={reject.submitting} onClick={() => setReject({ open: false, row: null, action: null, text: '', submitting: false, error: null })} className="text-white/80 hover:text-white hover:bg-white/10 rounded-full p-1.5"><X className="w-5 h-5" /></button>
            </div>
            <div className="p-6">
              <p className="text-sm text-gray-700 mb-3">Vui lòng nhập lý do từ chối đơn của đoàn <span className="font-bold text-[#004c91]">{reject.row.name}</span>:</p>
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
              <h3 className="text-lg font-bold text-white flex items-center gap-2"><AlertCircle className="w-5 h-5 bg-white/20 rounded-full p-0.5" /> Hủy lịch thăm</h3>
              <button type="button" disabled={cancel.submitting} onClick={() => setCancel({ open: false, row: null, mode: null, text: '', submitting: false, error: null })} className="text-white/85 hover:text-white hover:bg-white/10 rounded-full p-1.5 cursor-pointer"><X className="w-5 h-5" /></button>
            </div>
            <div className="p-6 space-y-3">
              <p className="text-sm text-gray-700">Bạn đang hủy lịch thăm của đoàn <span className="font-bold text-[#004c91]">{cancel.row.name}</span>. Hành động này không thể hoàn tác.</p>
              {cancel.row.campus && cancel.row.campus !== '-' && (
                <p className="text-sm text-gray-600"><span className="text-slate-400">Cơ sở:</span> <span className="font-semibold text-slate-700">{cancel.row.campus}</span></p>
              )}
              {cancel.mode === 'host' && (
                <p className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs font-medium text-red-700">
                  Trường hợp Host hủy là do Visitor đã xác nhận hủy ngoài hệ thống.
                </p>
              )}
              <div>
                <label className="block text-sm font-bold text-gray-700 mb-2">Lý do hủy <span className="text-red-500">*</span></label>
                <textarea value={cancel.text} onChange={(e) => setCancel((s) => ({ ...s, text: e.target.value }))} maxLength={2000} placeholder="Nhập lý do hủy..." className="w-full px-4 py-3 rounded-2xl border border-gray-200 focus:border-red-500 focus:ring-4 focus:ring-red-500/10 outline-none transition-all text-sm min-h-[100px] resize-none bg-gray-50/50 focus:bg-white" disabled={cancel.submitting} />
                {cancel.error && <p className="text-red-500 text-sm mt-2">{cancel.error}</p>}
              </div>
            </div>
            <div className="px-6 py-4 bg-gray-50 flex items-center justify-end gap-3 border-t border-gray-100">
              <button type="button" disabled={cancel.submitting} onClick={() => setCancel({ open: false, row: null, mode: null, text: '', submitting: false, error: null })} className="px-5 py-2 rounded-xl font-bold text-gray-600 hover:bg-gray-200 transition-colors outline-none text-sm cursor-pointer">Đóng</button>
              <button type="button" disabled={!cancel.text.trim() || cancel.submitting} onClick={submitCancel} className="px-6 py-2 rounded-xl font-bold text-white bg-red-600 hover:bg-red-700 shadow-sm transition-all outline-none text-sm cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed">{cancel.submitting ? 'Đang xử lý...' : 'Xác nhận hủy'}</button>
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
          customTitle={isPendingHostAssignment(assign.row) ? "Chọn Host chính thức" : undefined}
          onClose={() => setAssign({ open: false, row: null, mode: 'approve' })}
          onConfirmed={() => {
            const successMsg = isPendingHostAssignment(assign.row!)
              ? 'Gán host thành công.'
              : assign.mode === 'transfer'
                ? 'Chuyển host thành công.'
                : 'Duyệt đơn và gán host thành công.';
            setAssign({ open: false, row: null, mode: 'approve' });
            pushToast('success', successMsg);
            loadDelegations(activeTab, currentPage, pageSize, appliedFilters);
          }}
        />
      )}

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
