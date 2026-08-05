/**
 * VisitRequestManagement — màn quản lý đơn tiếp khách theo vai trò.
 *
 * Dữ liệu được backend lọc theo role/scope (UC-20) và trả kèm `allowedActions`;
 * frontend chỉ render UI + nút theo danh sách đó, mọi thao tác đều được backend
 * validate lại. Tab theo role (actor relation):
 *   - Visitor: "Tôi là đầu mối" (responsible/owner) + "Tôi là người đăng ký" (registered, read-only).
 *   - IC Staff: "Đơn phụ trách" (host) + "Lời mời tham dự" (attending) + "Đơn tôi đăng ký" (registered).
 *   - Staff Leader: "Yêu cầu tại cơ sở" (campus review) + "Tôi là host" (hosted) + "Đơn tôi đăng ký".
 * Visitor/IC Staff/Staff Leader có nút "Tạo đoàn khách" mở shared form (authenticated mode).
 * Admin không tham gia luồng này.
 */

import React, { Fragment, useEffect, useMemo, useState } from 'react';
import {
  Search, Plus, Eye, AlertCircle, Users, MapPin, Calendar,
  ChevronLeft, ChevronRight, ChevronDown, Check, X, XCircle, Mail,
  FileText, ArrowRightCircle, Info, ClipboardList, Star, CheckCircle2,
  PencilLine, MailOpen, RefreshCw, FileX, FileMinus, UserCog, History, Bell,
} from 'lucide-react';
import { motion } from 'motion/react';
import { useNavigate, useSearchParams, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { SubmittedVisitRequestDetailModal } from '../../../components/modals/SubmittedVisitRequestDetailModal';
import SearchMatchContexts from '../../../features/visit-request/components/SearchMatchContexts';
import { VisitRequestV2Modal } from '../../../features/visit-request/components/v2/VisitRequestV2Modal';
import { usePerCampusV2Capability } from '../../../shared/features/perCampusV2Capability';
import { V2_AUTHENTICATED_CREATE_PATH } from '../../../shared/features/perCampusV2Entry';
import {
  resolveVisitEntryOutcome,
  notifyCapabilityError,
  notifyCapabilityLoading,
  notifyCapabilityDisabled,
  dismissCapabilityToasts,
} from '../../../shared/features/useVisitEntryCta';
import { resolveVisitRowRoutes } from '../../../features/visit-request/utils/visitVersionRouting';
import { visitDraftNamespace } from '../../../features/visit-request/utils/visitRequestV2DraftStorage';
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
  type VisitActionCapability,
} from '../../../features/delegations/types/delegations.types';
import { useAuthContext } from '../../../shared/auth/AuthContext';
import { getVisitRequestFilterConfig } from '../../../features/delegations/config/visitRequestFilterConfig';
import { useCampusFilterOptions } from '../../../features/campus-management/hooks/useCampusManagement';
import { authenticationApi } from '../../../features/authentication/api/authenticationApi';
import type { CampusOption } from '../../../features/authentication/types/authentication.types';
import { visitFeedbackApi } from '../../../features/feedbacks/api/visitFeedbackApi';
import { VisitFeedbackModal } from '../../../features/feedbacks/components/VisitFeedbackModal';
import type { PendingFeedbackItem } from '../../../features/feedbacks/types/visitFeedback.types';
import { VisitChangeBadges } from '../../../features/delegations/components/VisitChangeBadges';
import { VisitRowActionMenu, type VisitRowMenuItem } from '../../../features/delegations/components/VisitRowActionMenu';

import VisitHostTransferModal, { type HostTransferTarget } from '../../../features/visit-request/components/VisitHostTransferModal';
import { formatVietnamDateTime, formatVietnamDate } from '../../../shared/utils/vietnamTime';
import { getApiErrorMessage, showErrorToast, showSuccessToast } from '../../../shared/utils/toast';
type Tab = 'responsible' | 'attending' | 'registered' | 'hosted' | 'all';

/** Which of the two layouts an element belongs to — both are in the DOM, CSS picks one. */
type RowVariant = 'desktop' | 'mobile';

type ActionTone = 'blue' | 'green' | 'red' | 'gray' | 'orange';

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
// Fallback only: the backend now sends `statusLabel` per row — this covers rows that predate it
// (the attending tab builds its own rows) and keeps the module testable in isolation.
const getVietnameseStatus = (reqStatus?: string | null, campStatus?: string | null) => {
  if (campStatus === 'CANCELLED' || reqStatus === 'CANCELLED') return 'Đã hủy';
  if (campStatus === 'REJECTED') return 'Từ chối';
  if (campStatus === 'WAITING_REQUEST_APPROVAL') return 'Chờ duyệt';
  if (campStatus === 'ASSIGNED') return 'Đã duyệt';
  if (campStatus === 'BEFORE_VISIT') return 'Đang chuẩn bị';
  if (campStatus === 'DURING_VISIT') return 'Đang diễn ra';
  if (campStatus === 'AFTER_VISIT') return 'Chờ đóng đoàn';
  if (campStatus === 'CLOSED') return 'Đã hoàn tất';
  // Request-level rows (không có campusStatus): dùng aggregate. Không còn "Duyệt một phần" —
  // đơn liên cơ sở còn cơ sở nào chưa xong thì vẫn hiện "Chờ duyệt" (xem VisitRowLabels.Status).
  if (reqStatus === 'REJECTED') return 'Từ chối';
  if (reqStatus === 'PENDING_APPROVAL') return 'Chờ duyệt';
  if (reqStatus === 'PARTIALLY_APPROVED') return 'Chờ duyệt';
  if (reqStatus === 'APPROVED') return 'Đã duyệt';
  return reqStatus ?? '-';
};

// Map campus instanceStatus CODE → nhãn tiếng Việt + class badge (dùng cho accordion liên cơ sở).
// Chỉ để render hiển thị; KHÔNG dùng để gate action (action lấy từ boolean backend trả về).
const CAMPUS_STATUS_LABELS: Record<string, string> = {
  WAITING_REQUEST_APPROVAL: 'Chờ duyệt',
  ASSIGNED: 'Đã duyệt',
  BEFORE_VISIT: 'Đang chuẩn bị',
  DURING_VISIT: 'Đang diễn ra',
  AFTER_VISIT: 'Chờ đóng đoàn',
  CLOSED: 'Đã hoàn tất',
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

  const canReceiveParticipantInvitations = isRegularStaff || isStaffLeader || isDept || isStudent;
  const canUseAttendingTab = canReceiveParticipantInvitations;
  const canUseResponsibleTab = !isStudent && !isDept && !isAdmin && !isStaffLeader;
  // Actor relation: tab "Đơn tôi đăng ký / Tôi là người đăng ký" (registrant, read-only)
  // cho các role được tạo đoàn khách; tab "Tôi là host" riêng cho Staff Leader.
  const canUseRegisteredTab = isVisitor || isStaff;
  const canUseHostedTab = isStaffLeader;
  // "Tất cả các loại đơn" — gộp mọi tab quan hệ thành 1 danh sách (backend QueryAllMergedAsync).
  // Chỉ role có ≥2 tab mới cần (HO/Dept/Student chỉ có đúng 1 tab, "tất cả" sẽ trùng y hệt tab đó):
  //   - Staff Leader: THAY THẾ "Yêu cầu tại cơ sở" (theo yêu cầu — responsible cũ đã ẩn ở trên).
  //   - Visitor / Staff thường: THÊM làm 1 lựa chọn mới, giữ nguyên các tab hiện có.
  const canUseAllTab = isStaffLeader || isVisitor || isRegularStaff;
  // Các role được tạo đoàn khách (Visitor / IC Staff / Staff Leader) — backend revalidate.
  const canCreateVisitRequest = isVisitor || isRegularStaff || isStaffLeader;
  const showTabs = [canUseAttendingTab, canUseResponsibleTab, canUseRegisteredTab, canUseHostedTab, canUseAllTab].filter(Boolean).length > 1
    || canUseAttendingTab || (canUseResponsibleTab && canUseRegisteredTab);

  const responsibleTabLabel = isHO ? 'Theo dõi đơn tiếp khách'
    : isVisitor ? 'Tôi là đầu mối'
      : 'Đơn phụ trách';
  const allTabLabel = 'Tất cả các loại đơn';
  const attendingTabLabel = (isDept && subRole === 'STAFF') ? 'Nhiệm vụ được giao' : 'Lời mời tham dự';
  const registeredTabLabel = isVisitor ? 'Tôi là người đăng ký' : 'Đơn tôi đăng ký';
  const hostedTabLabel = 'Đoàn tôi phụ trách';
  const tabOptions = ([
    { key: 'all' as Tab, label: allTabLabel, show: canUseAllTab },
    { key: 'responsible' as Tab, label: responsibleTabLabel, show: canUseResponsibleTab },
    { key: 'hosted' as Tab, label: hostedTabLabel, show: canUseHostedTab },
    { key: 'attending' as Tab, label: attendingTabLabel, show: canUseAttendingTab },
    { key: 'registered' as Tab, label: registeredTabLabel, show: canUseRegisteredTab },
  ]).filter((t) => t.show);

  const [searchParams, setSearchParams] = useSearchParams();
  const location = useLocation();
  const { t } = useTranslation(['visitRequestV2']);

  /**
   * A one-shot "you just created VR-…" handed over in navigation state (plan §9, §16.20).
   *
   * It is consumed by REPLACING the history entry without the state, so a refresh or a Back does
   * not announce a request that was filed minutes ago as if it had just happened. Keyed by request
   * code so React's StrictMode double-effect cannot fire the same toast twice.
   */
  const consumedFlashRef = React.useRef<string | null>(null);
  useEffect(() => {
    const flash = (location.state as { flash?: { kind?: string; requestCode?: string } } | null)?.flash;
    if (flash?.kind !== 'v2-created' || !flash.requestCode) return;
    if (consumedFlashRef.current === flash.requestCode) return;
    consumedFlashRef.current = flash.requestCode;
    showSuccessToast(
      t('visitRequestV2:success.toast', { code: flash.requestCode }),
      `v2-created-${flash.requestCode}`,
    );
    navigate(location.pathname + location.search, { replace: true, state: null });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [location.state]);

  const isTabAllowed = (tab: Tab | null): tab is Tab => {
    if (tab === 'responsible') return canUseResponsibleTab;
    if (tab === 'attending') return canUseAttendingTab;
    if (tab === 'registered') return canUseRegisteredTab;
    if (tab === 'hosted') return canUseHostedTab;
    if (tab === 'all') return canUseAllTab;
    return false;
  };
  const urlTab = searchParams.get('tab') as Tab | null;
  const defaultTab: Tab = isTabAllowed(urlTab)
    ? urlTab
    : (isStudent || isDept) ? 'attending' : canUseAllTab ? 'all' : 'responsible';
  const [activeTab, setActiveTab] = useState<Tab>(defaultTab);

  // UC-27: pending participation invitations for invitee roles. This banner is the entry
  // point to the invitation-detail screen, where Accept/Decline happens — never in the
  // attending tab (which only lists already-ACCEPTED invitations and is read-only).
  const [pendingInvitations, setPendingInvitations] = useState<VisitInvitation[]>([]);

  // Shared create form (authenticated mode): Visitor / IC Staff / Staff Leader.
  const [showV2Modal, setShowV2Modal] = useState(false);

  // Default create-entry cutover: route to the v2 create page when the capability is enabled.
  const { status: v2Status, enabled: v2Enabled, retry: retryCapability } = usePerCampusV2Capability();
  const handleCreateVisitRequest = () => {
    const outcome = resolveVisitEntryOutcome(v2Status, v2Enabled);
    if (outcome === 'error') { notifyCapabilityError(retryCapability); return; }
    if (outcome === 'loading') { notifyCapabilityLoading(); return; }
    dismissCapabilityToasts();
    
    if (outcome === 'v2-modal') {
      setShowV2Modal(true);
    } else {
      notifyCapabilityDisabled();
    }
  };

  const filterConfig = getVisitRequestFilterConfig({
    roleCode,
    subRole,
    activeTab,
    isVisitor,
  });

  // Campus filter options — never hardcoded, always from the database. The filter sends
  // campusId, so value = campusId; label = campus name. Best-effort: if the options fail to
  // load the dropdown still renders the "Tất cả cơ sở" default.
  // HO uses the campus-management dataset (HO/ADMIN-only endpoint) which includes INACTIVE
  // campuses so historical/cancelled visits at a disabled campus stay filterable. Visitor has
  // no access to that endpoint (403) — it falls back to the public "active campuses" list
  // instead (same one the login page uses), active-only being an acceptable trade-off here.
  const campusFilterOptions = useCampusFilterOptions();
  const [visitorActiveCampuses, setVisitorActiveCampuses] = useState<CampusOption[]>([]);
  useEffect(() => {
    if (!isVisitor) return;
    let active = true;
    authenticationApi.getActiveCampuses()
      .then((list) => { if (active) setVisitorActiveCampuses(list); })
      .catch(() => { /* best-effort, same as the HO campus filter options */ });
    return () => { active = false; };
  }, [isVisitor]);
  const campusOptions = useMemo(
    () => [
      { value: '', label: 'Tất cả cơ sở' },
      ...(isVisitor
        ? visitorActiveCampuses.map((c) => ({ value: String(c.campusId), label: c.campusName }))
        : (campusFilterOptions?.campuses ?? []).map((c) => ({
          value: String(c.campusId),
          label: c.name,
        }))),
    ],
    [isVisitor, visitorActiveCampuses, campusFilterOptions],
  );

  const showTabFilter = showTabs && !isEmbedded;

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
  const [isTabFilterOpen, setIsTabFilterOpen] = useState(false);

  // Đến từ 1 thông báo cụ thể (?visitRequestId=...): chỉ hiển thị đúng đơn đó thay vì cả
  // danh sách, để người dùng không phải tự tìm. "Reset" (nút có sẵn) xoá filter này để xem
  // lại toàn bộ. Độc lập với draftFilters/appliedFilters (không hiện trên thanh filter UI).
  const [notificationVisitRequestId, setNotificationVisitRequestId] = useState(searchParams.get('visitRequestId') || '');

  const [rows, setRows] = useState<Row[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [total, setTotal] = useState(0);
  const [currentPage, setCurrentPage] = useState(Number(searchParams.get('page')) || 1);
  const [pageSize, setPageSize] = useState(Number(searchParams.get('pageSize')) || 10);
  const [sortOrder, setSortOrder] = useState<'desc' | 'asc'>((searchParams.get('sortOrder') as 'desc' | 'asc') || 'desc');
  const [debouncedKeyword, setDebouncedKeyword] = useState(draftFilters.keyword);
  const [summaryStats, setSummaryStats] = useState<any>(null);

  // keepNotificationFilter=false (mặc định): mọi thay đổi filter thường thoát khỏi chế độ
  // "xem 1 đơn từ thông báo" — chỉ Reset và lần load ban đầu mới cần giữ/xoá tường minh.
  const updateUrlParams = (tab: Tab, page: number, size: number, filters: typeof appliedFilters, sort: string, keepNotificationFilter = false) => {
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
    if (!keepNotificationFilter) params.delete('visitRequestId');

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
    setNotificationVisitRequestId('');
    updateUrlParams(activeTab, 1, pageSize, newFilters, sortOrder);
    loadDelegations(activeTab, 1, pageSize, newFilters, sortOrder, '');
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
  // Pure V2: every request opens the per-campus v2 detail route. The flat modal cannot represent
  // per-campus content and has no runtime left, so it is never the target here.
  const openRequestForm = (row: Row) => {
    navTo(resolveVisitRowRoutes(row.visitRequestId).detailRoute);
  };
  // "Xem đơn đăng ký tham quan trước khi duyệt" — read-only review of a PENDING_APPROVAL row.
  const [review, setReview] = useState<{ open: boolean; row: Row | null }>({ open: false, row: null });
  const [reason, setReason] = useState<{ open: boolean; row: Row | null }>({ open: false, row: null });
  // UC-136: read-only popup of the cancellation reason (Host / Visitor / Staff Leader / HO).
  const [cancelReason, setCancelReason] = useState<{ open: boolean; row: Row | null }>({ open: false, row: null });
  const [reject, setReject] = useState<{ open: boolean; row: Row | null; action: AllowedAction | null; text: string; submitting: boolean; error: string | null }>({ open: false, row: null, action: null, text: '', submitting: false, error: null });
  const [cancel, setCancel] = useState<{ open: boolean; row: Row | null; mode: 'visitor' | 'host' | null; instanceId?: number | null; text: string; submitting: boolean; error: string | null; confirmed: boolean }>({ open: false, row: null, mode: null, instanceId: null, text: '', submitting: false, error: null, confirmed: false });
  const [assign, setAssign] = useState<{ open: boolean; row: Row | null; mode: 'approve' }>({ open: false, row: null, mode: 'approve' });
  // Hand the reception owner over without leaving the list. Only ever opened from an INSTANCE-scoped
  // TRANSFER_HOST verdict — the row's own for a single campus, the accordion's for a multi-campus one.
  const [hostTransfer, setHostTransfer] = useState<HostTransferTarget | null>(null);

  // Mutation feedback goes through the ONE global top-right toaster mounted in App.tsx
  // (shared/utils/toast). This page used to run its own bottom-right viewport, which is why a
  // cancellation appeared in the opposite corner from every other notification in the product.

  // Feedback rule mới: map visitInstanceId → trạng thái đánh giá của user hiện tại.
  // Backend trả các instance user là Visitor/Host và đã kết thúc tiếp khách (AFTER_VISIT/CLOSED);
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

  useEffect(() => {
    const fbParam = searchParams.get('feedbackVisitInstanceId');
    if (fbParam) {
      const instId = Number(fbParam);
      if (!isNaN(instId) && instId > 0) {
        setFeedbackModalInstanceId(instId);
      }
    }
  }, [searchParams]);

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
    return formatVietnamDate(dateStr, { fallback: '' });
  };
  const formatDateTimeShort = (value?: string | null) => {
    if (!value) return '-';
    return formatVietnamDateTime(value);
  };

  /**
   * Hiển thị lịch tiếp:
   * - Cùng ngày: "DD/MM/YYYY HH:mm - HH:mm"
   * - Khác ngày: trả về null (caller sẽ dùng layout Từ/Đến 2 dòng)
   */
  const formatSameDayRange = (start?: string | null, end?: string | null): string | null => {
    if (!start || !end) return null;
    const startDT = formatVietnamDateTime(start);
    const endDT = formatVietnamDateTime(end);
    if (startDT === '-' || endDT === '-') return null;
    // So sánh phần ngày (10 ký tự đầu "DD/MM/YYYY")
    if (startDT.slice(0, 10) === endDT.slice(0, 10)) {
      // Cùng ngày: hiện "DD/MM/YYYY HH:mm - HH:mm"
      return `${startDT} - ${endDT.slice(11)}`;
    }
    return null; // khác ngày
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
    setNotificationVisitRequestId('');
    updateUrlParams(activeTab, 1, pageSize, empty, sortOrder);
    loadDelegations(activeTab, 1, pageSize, empty, sortOrder, '');
  };

  const loadDelegations = async (
    targetTab: Tab,
    targetPage: number,
    targetSize: number,
    targetFilters: typeof appliedFilters,
    targetSort: string = sortOrder,
    notifFilterOverride?: string,
  ) => {
    if (isAdmin) return;
    const notifFilter = notifFilterOverride !== undefined ? notifFilterOverride : notificationVisitRequestId;
    try {
      setIsLoading(true);
      setListError(null);
      // Students/Depts only have the attending view; Visitors have owner + registered.
      const effectiveTab = (isStudent || isDept)
        ? 'attending'
        : isVisitor
          ? (targetTab === 'registered' ? 'registered' : 'responsible')
          : targetTab;
      const params: Record<string, unknown> = {
        tab: effectiveTab,
        page: notifFilter ? 1 : targetPage,
        pageSize: notifFilter ? 1000 : targetSize,
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

      if (targetFilters.relation) {
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
        const filtered = notifFilter
          ? mapped.filter((r) => String(r.visitRequestId) === notifFilter)
          : mapped;
        setRows(filtered);
        setTotal(notifFilter ? filtered.length : (response.totalItems || 0));
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
        const filtered = notifFilter
          ? mapped.filter((r) => String(r.visitRequestId) === notifFilter)
          : mapped;
        setRows(filtered);
        setTotal(notifFilter ? filtered.length : (response.totalItems || 0));
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

  const loadPendingInvitations = async () => {
    if (!showTabs) return;
    try {
      const data = await delegationsApi.getMyInvitations(false);
      setPendingInvitations(data || []);
    } catch {
      setPendingInvitations([]);
    }
  };

  useEffect(() => {
    loadPendingInvitations();
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

    if (rowTab(row) === 'attending') return true;

    if (isVisitor) {
      const approvedish = row.requestStatus === 'APPROVED' || row.requestStatus === 'PARTIALLY_APPROVED';
      // MULTI_CAMPUS: no separate icon — it only ever toggled the same per-campus accordion the
      // "Xem N cơ sở" link right under the name already opens, so the icon was a pure duplicate.
      if (row.visitScope === 'MULTI_CAMPUS') {
        return false;
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

    if (rowTab(row) === 'attending') {
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

    if (actions.includes('OPEN_CONTRIBUTION')) {
      navTo(`/dashboard/visit/contribution/${row.visitInstanceId}`);
      return;
    }

    if (actions.includes('VIEW_RECEPTION_DETAIL')) {
      if (row.visitInstanceId) {
        navTo(`/dashboard/visit/reception-detail/${row.visitInstanceId}`);
        return;
      }
      if (row.visitScope === 'MULTI_CAMPUS') {
        toggleExpanded(row.visitRequestId);
        return;
      }
      return;
    }

    // OPEN_HOST_PROCESS được ưu tiên trước OPEN_PROCESS_SUMMARY: Staff Leader có thể ĐỒNG
    // THỜI là Host của chính instance này (backend thêm cả 2 action) — khi đó phải vào trang
    // Setup (có thể thao tác) thay vì bị ép về Báo cáo tổng hợp read-only. OPEN_PROCESS_SUMMARY
    // chỉ còn là fallback khi user không phải Host (HO thuần, hoặc theo dõi đơn Staff khác).
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
    } else if (actions.includes('OPEN_PROCESS_SUMMARY')) {
      navTo(`/dashboard/visit/process-summary/${row.visitInstanceId}`);
      return;
    }

    const idForRoute = row.id;

    // Dedicated "Lời mời tham dự" tab: rows come from the invitations API and carry a real
    // participantId. On the merged "all" tab an attending-origin row is the generic
    // VisitRequestManagementItemDto shape instead (no participantId) — it falls through to
    // the request detail route below, same as any other read-only row.
    if (activeTab === 'attending') {
      const partId = (row as any).participantId;
      if (isDept && subRole === 'STAFF') {
        navTo(`/dashboard/visit/department-tasks/${partId}`);
      } else {
        navTo(`/dashboard/visit/invitations/${partId}`);
      }
      return;
    }
    if (rowTab(row) === 'attending') {
      navTo(resolveVisitRowRoutes(row.visitRequestId).detailRoute);
      return;
    }

    if (isVisitor) {
      if (row.visitScope === 'MULTI_CAMPUS') {
        toggleExpanded(row.visitRequestId);
        return;
      }
      if (row.host && row.requestStatus === 'APPROVED') {
        if (row.visitInstanceId) {
          navTo(`/dashboard/visit/reception-detail/${row.visitInstanceId}`);
        }
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
      showSuccessToast(wasDecline ? 'Từ chối lời mời thành công.' : 'Đã từ chối tiếp nhận tại cơ sở này.');
      await loadDelegations(activeTab, currentPage, pageSize, appliedFilters, sortOrder);
      if (wasDecline) {
        await loadPendingInvitations();
      }
    } catch (e: any) {
      const msg = getApiErrorMessage(e, 'Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.');
      setReject((s) => ({ ...s, submitting: false, error: `Không thể từ chối. ${msg}` }));
    }
  };

  const submitAcceptInvitation = async (row: Row) => {
    try {
      await delegationsApi.visitInvitations.acceptInvitation((row as any).participantId);
      showSuccessToast('Đã chấp nhận lời mời.');
      await loadDelegations(activeTab, currentPage, pageSize, appliedFilters, sortOrder);
      await loadPendingInvitations();
    } catch (e: any) {
      showErrorToast(e, 'Không thể chấp nhận lời mời. Vui lòng thử lại sau.');
    }
  };

  const submitAssignDeptStaff = async (row: Row) => {
    const staffIdStr = window.prompt('Nhập ID của Department Staff để giao việc:');
    if (!staffIdStr) return;
    const note = window.prompt('Nhập ghi chú/nhiệm vụ:');
    try {
      await delegationsApi.visitInvitations.assignDepartmentStaff((row as any).participantId, parseInt(staffIdStr, 10), note || '');
      showSuccessToast('Đã giao việc cho nhân sự.');
      await loadDelegations(activeTab, currentPage, pageSize, appliedFilters, sortOrder);
    } catch (e: any) {
      showErrorToast(e, 'Không thể giao việc. Vui lòng thử lại sau.');
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
      showSuccessToast('Đã hủy lịch thăm thành công.');
      await loadDelegations(activeTab, currentPage, pageSize, appliedFilters, sortOrder);
    } catch (e: any) {
      // Surface the backend's real business message (clean Vietnamese sentence such as
      // "Không thể hủy lịch thăm. Đơn đang chờ duyệt..."); apiErrorMessage walks
      // message → error → errors → title and only then a generic safe fallback.
      setCancel((s) => ({ ...s, submitting: false, error: getApiErrorMessage(e, 'Không thể hủy lịch thăm. Vui lòng thử lại sau.') }));
    }
  };

  // Campus-independent approval: không còn bước "chờ gán host" riêng — Staff Leader duyệt là
  // gán host luôn. Instance của campus mình đang chờ chính là hàng cần xử lý.
  const isAwaitingMyDecision = (row: Row) =>
    isStaffLeader && row.campusStatus === 'WAITING_REQUEST_APPROVAL'
    && (row.allowedActions || []).includes('APPROVE_AND_ASSIGN_HOST');

  /**
   * On every OTHER tab, activeTab already describes every row on screen. On the merged "all" tab
   * (Staff Leader) rows come from 3 different sources mixed into one list, so code that used to
   * branch on activeTab to mean "this row is an invitation / a registered-only row" must branch
   * on the ROW's own origin instead — backend-tagged via tabType.
   */
  const rowTab = (row: Row): Tab => {
    if (activeTab !== 'all') return activeTab;
    switch (row.tabType) {
      case 'INVITED': return 'attending';
      case 'REGISTERED': return 'registered';
      case 'HOSTED': return 'hosted';
      default: return 'responsible';
    }
  };

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
    if (rowTab(row) === 'attending') {
      badges.push(chip('att', attendingTabLabel, 'bg-purple-50 text-purple-700 border-purple-200'));
      if (row.participantRole) {
        badges.push(chip('prole', PARTICIPANT_ROLE_LABELS[row.participantRole] ?? 'Tham dự', 'bg-slate-50 text-slate-700 border-slate-200'));
      }
      // visitRequestStatus/campusVisitStatus only exist on the dedicated invitations API shape —
      // a merged-origin row already gets its own status badge from getStatusBadge, so this extra
      // inline chip is scoped to the literal "attending" tab only.
      if (activeTab === 'attending') {
        const visitStatusText = getVietnameseStatus((row as any).visitRequestStatus, (row as any).campusVisitStatus);
        if (visitStatusText && visitStatusText !== '-' && visitStatusText !== 'Không xác định') {
          badges.push(chip('v-status', visitStatusText, 'bg-slate-100 text-slate-600 border-slate-300'));
        }
      }
    } else if (row.visitScope) {
      const single = row.visitScope === 'SINGLE_CAMPUS';
      badges.push(chip('scope', VISIT_SCOPE_LABELS[row.visitScope] + (row.campusCount > 1 ? ` (${row.campusCount})` : ''),
        single ? 'bg-sky-50 text-sky-700 border-sky-200' : 'bg-indigo-50 text-indigo-700 border-indigo-200'));
    }

    // ── Layer 2: what the reader IS to this row. One badge, from the backend's relationLabel —
    //    kept separate from the status badge (layer 1) and from "việc cần làm" (layer 3), because a
    //    single chip that tried to be all three is what made "Chờ xử lý tại cơ sở" read as an
    //    instruction to the visitor who could do nothing about it. ──
    if (rowTab(row) === 'registered') {
      badges.push(chip('registered', row.relationLabel || 'Bạn là người đăng ký', 'bg-slate-50 text-slate-600 border-slate-200'));
      if (row.isAlsoHost) {
        badges.push(chip('also-host', 'Đồng thời phụ trách tiếp đón', 'bg-emerald-50 text-emerald-700 border-emerald-200'));
      }
    } else if (row.relationLabel && rowTab(row) !== 'attending') {
      const emphasised = row.currentUserIsHost || isAwaitingMyDecision(row);
      badges.push(chip(
        'relation',
        row.relationLabel,
        emphasised && !isCancelledOrRejected(row)
          ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
          : 'bg-slate-50 text-slate-600 border-slate-200',
      ));
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
    // visit_request_campuses.status; request status chỉ dùng cho quyết định tổng.
    //
    // Vocabulary chung mọi role: Chờ duyệt · Đã duyệt · Đang chuẩn bị ·
    // Đang diễn ra · Chờ đóng đoàn · Đã hoàn tất · Từ chối · Đã hủy — khớp
    // VisitRowLabels.Status backend. Đơn liên cơ sở còn cơ sở chưa xong (trước là "Duyệt một
    // phần") giờ gộp chung "pending_request" (Chờ duyệt) — riêng biệt bằng ChangeSummary/
    // campus indicator, không phải bằng tên trạng thái. `kind` chỉ chọn MÀU badge.
    type StatusKind = 'pending' | 'pending_request' | 'rejected' | 'cancelled' | 'assigned'
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
    else if (row.requestStatus === 'APPROVED') kind = 'approved';
    else kind = 'pending_request'; // gồm cả PENDING_APPROVAL và PARTIALLY_APPROVED

    let cancelledText = 'Đã hủy';
    if (kind === 'cancelled') {
      const actor = (row as any).cancellationActorType;
      if (actor === 'VISITOR') cancelledText = 'Đã hủy bởi khách';
      else if (actor === 'HOST') cancelledText = 'Đã hủy bởi người phụ trách';
      else if (actor === 'SYSTEM') cancelledText = 'Hệ thống đã hủy';
    }

    const labelByKind: Record<StatusKind, string> = {
      pending: 'Chờ duyệt', pending_request: 'Chờ duyệt', rejected: 'Từ chối', cancelled: cancelledText,
      assigned: 'Đã duyệt',
      before: 'Đang chuẩn bị', during: 'Đang diễn ra', after: 'Chờ đóng đoàn',
      closed: 'Đã hoàn tất', approved: 'Đã duyệt',
    };

    const clsByKind: Record<StatusKind, string> = {
      pending: 'bg-yellow-50 text-yellow-700 border-yellow-200',
      pending_request: 'bg-yellow-50 text-yellow-700 border-yellow-200',
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
      pending: 'Cơ sở đang chờ Staff Leader duyệt và phân công người phụ trách tiếp đón',
      pending_request: 'Đơn đang chờ xử lý tại các cơ sở',
      rejected: 'Đã bị từ chối tiếp nhận',
      cancelled: 'Đơn/cơ sở đã bị hủy',
      assigned: 'Đã duyệt và có người phụ trách tiếp đón, chờ triển khai',
      before: 'Đang trong giai đoạn chuẩn bị đón tiếp',
      during: 'Đoàn đang được tiếp khách tại cơ sở',
      after: 'Đoàn đã kết thúc, chờ đóng đoàn/hoàn tất hồ sơ',
      closed: 'Đoàn đã hoàn tất toàn bộ quy trình',
      approved: 'Tất cả cơ sở đã xử lý xong và có cơ sở tiếp nhận',
    };

    // Layer 1 — the process status, and ONLY that. The backend's statusLabel wins where it exists so
    // one vocabulary serves the list, the detail screen and the notification text; the local map stays
    // as the fallback for rows built client-side (the attending tab) and for tests.
    statusText = row.statusLabel || labelByKind[kind];
    return (
      <span className="inline-flex flex-col items-start gap-1">
        <span title={titleByKind[kind]} className={`${base} ${clsByKind[kind]}`}>{statusText}</span>
        {/* BESIDE the status, never replacing it: the status is what people filter and sort by, and
            a row that was edited is still at whatever stage it was at. */}
        <VisitChangeBadges
          summary={row.changeSummary}
          data-testid={`change-badges-${row.visitRequestId}`}
        />
      </span>
    );
  };

  /**
   * The row's SECONDARY actions ("Thao tác khác"), built purely from what the backend granted.
   *
   * An action with a verdict (today: the handover) is offered even when the verdict is NO, carrying
   * the reason — "chỉ được chuyển ít nhất 6 giờ trước" teaches the rule, whereas a button that
   * quietly vanishes teaches nothing. An action with no grant at all is simply absent.
   */
  const buildRowMenuItems = (row: Row): VisitRowMenuItem[] => {
    const actions = row.allowedActions || [];
    const can = (a: AllowedAction) => actions.includes(a);
    const items: VisitRowMenuItem[] = [];

    // Instance-scoped handover. A multi-campus SUMMARY row never carries this verdict — the backend
    // puts it on each campus instead, so the accordion is the only place it can be acted on (§11.2).
    const transfer = (row.capabilities || []).find(c => c.code === 'TRANSFER_HOST');
    if (transfer) {
      items.push({
        key: 'transfer-host',
        label: 'Chuyển người phụ trách',
        icon: <UserCog className="h-4 w-4" />,
        disabled: !transfer.enabled,
        disabledReason: transfer.disabledReason,
        onSelect: () => openHostTransfer(row, transfer),
      });
    }

    if (can('EDIT_PENDING_REQUEST')) {
      items.push({
        key: 'edit-pending',
        label: 'Sửa đơn',
        icon: <PencilLine className="h-4 w-4" />,
        onSelect: () => navTo(resolveVisitRowRoutes(row.visitRequestId).edit),
      });
    }
    if (can('RESUBMIT_REJECTED_REQUEST')) {
      items.push({
        key: 'resubmit',
        label: 'Sửa & gửi lại đơn',
        icon: <RefreshCw className="h-4 w-4" />,
        onSelect: () => navTo(resolveVisitRowRoutes(row.visitRequestId).resubmit),
      });
    }
    if (can('CAMPUS_REJECT')) {
      items.push({
        key: 'campus-reject',
        label: 'Từ chối cơ sở này',
        icon: <X className="h-4 w-4" />,
        tone: 'danger',
        onSelect: () => setReject({ open: true, row, action: 'CAMPUS_REJECT', text: '', submitting: false, error: null }),
      });
    }
    if (can('DECLINE_INVITATION')) {
      items.push({
        key: 'decline-invitation',
        label: 'Từ chối lời mời',
        icon: <X className="h-4 w-4" />,
        tone: 'danger',
        onSelect: () => setReject({ open: true, row, action: 'DECLINE_INVITATION' as any, text: '', submitting: false, error: null }),
      });
    }
    if (can('CANCEL_BY_VISITOR') || can('CANCEL_BY_HOST')) {
      items.push({
        key: 'cancel',
        label: 'Hủy lịch thăm',
        icon: <XCircle className="h-4 w-4" />,
        tone: 'danger',
        onSelect: () => setCancel({
          open: true, row, mode: can('CANCEL_BY_HOST') ? 'host' : 'visitor',
          instanceId: null, text: '', submitting: false, error: null, confirmed: false,
        }),
      });
    }

    // Read-only explanations of how a row ended.
    if (row.requestStatus === 'REJECTED' && !!row.decisionNote) {
      items.push({
        key: 'reject-reason',
        label: 'Xem lý do từ chối',
        icon: <FileX className="h-4 w-4" />,
        onSelect: () => setReason({ open: true, row }),
      });
    }
    if (rowTab(row) !== 'attending'
      && (row.isCancelled === true || row.requestStatus === 'CANCELLED' || row.campusStatus === 'CANCELLED')) {
      items.push({
        key: 'cancel-reason',
        label: 'Xem lý do hủy',
        icon: <FileMinus className="h-4 w-4" />,
        onSelect: () => setCancelReason({ open: true, row }),
      });
    }

    // The full before/after diff is a detail-screen job (§10) — the menu only points at it.
    if (row.canViewRequestDetail !== false && rowTab(row) !== 'attending') {
      items.push({
        key: 'history',
        label: 'Xem lịch sử thay đổi',
        icon: <History className="h-4 w-4" />,
        onSelect: () => navTo(resolveVisitRowRoutes(row.visitRequestId).detailRoute),
      });
    }

    const fb = row.visitInstanceId ? feedbackByInstance[row.visitInstanceId] : undefined;
    if (fb && !fb.alreadySubmitted) {
      items.push({
        key: 'feedback',
        label: 'Đánh giá chuyến thăm',
        icon: <Star className="h-4 w-4" />,
        onSelect: () => setFeedbackModalInstanceId(row.visitInstanceId!),
      });
    }

    return items;
  };

  /**
   * The row's PRIMARY action — the one thing this reader is most likely here to do. Driven off the
   * backend's next task where there is one, so the button and the "Việc cần làm" line can never
   * disagree; everything else moved into the ⋯ menu, which is what stopped every row being a toolbar.
   */
  const renderRowActions = (row: Row, variant: RowVariant) => {
    const actions = row.allowedActions || [];
    const can = (a: AllowedAction) => actions.includes(a);
    const fb = (row.visitInstanceId && feedbackByInstance[row.visitInstanceId])
      || ((row as any).campusProgress && ((row as any).campusProgress as any[]).map((c: any) => feedbackByInstance[c.visitInstanceId]).find((f: any) => f && !f.alreadySubmitted));
    const menuItems = buildRowMenuItems(row);

    type Primary = { title: string; short: string; tone: ActionTone; icon: React.ReactNode; onClick: () => void };
    const primary: Primary | null =
      can('APPROVE_AND_ASSIGN_HOST')
        ? {
          title: 'Duyệt & phân công người phụ trách', short: 'Duyệt & phân công',
          tone: 'green', icon: <Check className="h-5 w-5" />,
          onClick: () => setAssign({ open: true, row, mode: 'approve' }),
        }
        : row.nextTask?.code === 'REVIEW_AMENDMENT'
          ? {
            title: 'Duyệt đề xuất thay đổi', short: 'Duyệt thay đổi',
            tone: 'orange', icon: <ClipboardList className="h-5 w-5" />,
            onClick: () => navTo(resolveVisitRowRoutes(row.visitRequestId).detailRoute),
          }
          : can('ACCEPT_INVITATION')
            ? {
              title: 'Xác nhận tham gia', short: 'Nhận lời',
              tone: 'green', icon: <Check className="h-5 w-5" />,
              onClick: () => submitAcceptInvitation(row),
            }
            : can('ASSIGN_TO_DEPARTMENT_STAFF')
              ? {
                title: 'Giao việc cho Staff', short: 'Giao việc',
                tone: 'blue', icon: <Users className="h-5 w-5" />,
                onClick: () => submitAssignDeptStaff(row),
              }
              : can('EDIT_PENDING_REQUEST')
                ? {
                  title: 'Sửa đơn đăng ký tham quan', short: 'Sửa đơn',
                  tone: 'blue', icon: <PencilLine className="h-5 w-5" />,
                  onClick: () => navTo(resolveVisitRowRoutes(row.visitRequestId).edit),
                }
                : can('RESUBMIT_REJECTED_REQUEST')
                  ? {
                    title: 'Sửa & gửi lại đơn', short: 'Gửi lại',
                    tone: 'orange', icon: <RefreshCw className="h-5 w-5" />,
                    onClick: () => navTo(resolveVisitRowRoutes(row.visitRequestId).resubmit),
                  }
                  : null;

    return (
      <div className="mx-auto flex w-[184px] items-center justify-center gap-2">
        {/* Xem form / xem chi tiết — always first, always in the same place. */}
        {row.visitRequestId && (activeTab !== 'attending' || can('VIEW_REQUEST_FORM')) ? (
          <ActionIconButton title="Xem form đăng ký tham quan" tone="blue" label="Xem form" icon={<ClipboardList className="h-5 w-5" />} onClick={(e) => { e.stopPropagation(); openRequestForm(row); }} />
        ) : (
          <span className="h-9 w-9" aria-hidden="true" />
        )}

        {/* Mở quy trình / theo dõi. */}
        {canOpenProcess(row) ? (
          <ActionIconButton
            title={getProcessActionTitle(row)}
            label="Mở quy trình"
            tone={can('OPEN_CONTRIBUTION') || can('OPEN_PROCESS_SUMMARY') ? 'orange' : 'blue'}
            icon={
              can('OPEN_CONTRIBUTION')
                ? <PencilLine className="h-5 w-5" />
                : can('OPEN_PROCESS_SUMMARY')
                  ? <FileText className="h-5 w-5" />
                  : can('VIEW_RECEPTION_DETAIL')
                    ? <Eye className="h-5 w-5" />
                    : (rowTab(row) === 'attending' && !can('OPEN_HOST_PROCESS'))
                      ? <MailOpen className="h-5 w-5" />
                      : <ArrowRightCircle className="h-5 w-5" />
            }
            onClick={(e) => { e.stopPropagation(); handleProcess(row); }}
          />
        ) : (
          <span className="h-9 w-9" aria-hidden="true" />
        )}

        {/* The one next action, if there is one. */}
        {primary ? (
          <ActionIconButton
            title={primary.title}
            label={primary.short}
            tone={primary.tone}
            icon={primary.icon}
            onClick={(e) => { e.stopPropagation(); primary.onClick(); }}
          />
        ) : fb && !fb.alreadySubmitted ? (
          <ActionIconButton
            title="Đánh giá chuyến thăm"
            label="Đánh giá"
            tone="orange"
            icon={<Star className="h-5 w-5 fill-amber-400 text-amber-500" />}
            onClick={(e) => {
              e.stopPropagation();
              const instId = fb.visitInstanceId || row.visitInstanceId;
              if (instId) setFeedbackModalInstanceId(instId);
            }}
          />
        ) : fb?.alreadySubmitted ? (
          <span title="Đã đánh giá" className="flex h-9 w-9 items-center justify-center text-emerald-500">
            <CheckCircle2 className="h-5 w-5" />
          </span>
        ) : (
          <span className="h-9 w-9" aria-hidden="true" />
        )}

        {/* Everything else. */}
        {menuItems.length > 0
          ? <VisitRowActionMenu items={menuItems} testId={`row-menu-${variant}-${row.id}`} />
          : <span className="h-9 w-9" aria-hidden="true" />}
      </div>
    );
  };

  /**
   * Open the handover for ONE campus instance.
   *
   * The instance id comes from the verdict, not from the row: on a multi-campus request the row is a
   * summary and has no single instance, which is exactly why the backend refuses to put this verdict
   * there. Passing the verdict through also carries the cutoff into the modal, so the deadline is on
   * the form rather than only in the error you get for missing it.
   */
  const openHostTransfer = (row: Row, capability: VisitActionCapability, item?: CampusProgressItem) => {
    const visitInstanceId = capability.visitInstanceId ?? item?.visitInstanceId ?? row.visitInstanceId;
    if (!visitInstanceId) return;
    setHostTransfer({
      visitInstanceId,
      campusName: item?.campusName || capability.campusName || row.campus || '',
      currentHostUserId: item?.hostUserId ?? row.currentHostUserId,
      currentHostName: item?.hostName ?? row.hostName,
      plannedStartAt: item?.plannedStartAt ?? capability.plannedStartAt ?? row.plannedStartAt,
      rowVersion: item?.rowVersion ?? row.rowVersion ?? 0,
      cutoffAt: capability.cutoffAt ?? null,
      requiredLeadHours: capability.requiredLeadHours,
    });
  };

  /** A campus row's own "⋯" menu. Instance-scoped by construction — see {@link openHostTransfer}. */
  const buildCampusMenuItems = (row: Row, item: CampusProgressItem): VisitRowMenuItem[] => {
    const items: VisitRowMenuItem[] = [];
    const transfer = (item.capabilities || []).find(c => c.code === 'TRANSFER_HOST');
    if (transfer) {
      items.push({
        key: `transfer-host-${item.visitInstanceId}`,
        label: 'Chuyển người phụ trách',
        icon: <UserCog className="h-4 w-4" />,
        disabled: !transfer.enabled,
        disabledReason: transfer.disabledReason,
        onSelect: () => openHostTransfer(row, transfer, item),
      });
    }
    if (item.canViewRejectReason) {
      items.push({
        key: `reject-reason-${item.visitInstanceId}`,
        label: 'Xem lý do từ chối cơ sở',
        icon: <FileX className="h-4 w-4" />,
        onSelect: () => openCampusRejectReason(row, item),
      });
    }
    return items;
  };

  // ── Multi-campus accordion (Phương án A): per-campus progress + actions ──
  const openCampusRequestForm = (row: Row, _item?: CampusProgressItem) => {
    // Pure V2: the flat modal cannot represent per-campus content — always open the scoped v2 detail.
    navTo(resolveVisitRowRoutes(row.visitRequestId).detailRoute);
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
        // Staff Leader CÓ THỂ là chính Host của cơ sở này (tự nhận, không chỉ gán cho Staff
        // thường) — chỉ khóa read-only khi họ không phải Host thật của instance đang xem.
        const isStaffLeaderNotHost = isStaffLeader && item.hostUserId != null && String(item.hostUserId) !== user?.userId;
        navTo(`/dashboard/visit/process/${item.visitInstanceId}`, {
          state: {
            defaultTab:
              item.instanceStatus === 'DURING_VISIT'
                ? 'during'
                : item.instanceStatus === 'AFTER_VISIT'
                  ? 'after'
                  : 'before',
            status: getCampusStatusLabel(item.instanceStatus),
            isReadOnly: isHO || isStaffLeaderNotHost || item.instanceStatus === 'CLOSED',
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

  const renderCampusAccordion = (row: Row, variant: RowVariant) => {
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
                      <><span className="text-slate-400">Người phụ trách tiếp đón:</span> <span className="font-semibold text-slate-700">{item.hostName || (item.instanceStatus === 'WAITING_REQUEST_APPROVAL' ? 'Chưa được phân công' : '-')}</span></>
                    )}
                  </div>
                </div>

                {/* Time Column */}
                <div className="lg:py-1 lg:px-3 text-[11px] text-slate-600 flex flex-wrap items-center gap-1 mt-1 lg:mt-0">
                  {(() => {
                    const sameDay = formatSameDayRange(item.plannedStartAt, item.plannedEndAt);
                    if (sameDay) {
                      return <span className="truncate">{sameDay}</span>;
                    }
                    return (
                      <>
                        <span className="truncate">{formatDateTimeShort(item.plannedStartAt)}</span>
                        <span className="text-slate-300">→</span>
                        <span className="truncate">{formatDateTimeShort(item.plannedEndAt)}</span>
                      </>
                    );
                  })()}
                </div>

                {/* Status Column */}
                <div className="lg:py-1 lg:px-3 lg:flex lg:justify-center mt-2 lg:mt-0">
                  <span className={`inline-flex justify-center whitespace-nowrap rounded-full border px-2 py-0.5 text-[10px] font-bold ${getCampusStatusBadgeClass(item.instanceStatus)}`}>
                    {getCampusStatusLabel(item.instanceStatus)}
                  </span>
                </div>

                {/* Actions Column — four slots, none of them wasted (§12):
                    1 change indicator · 2 view detail · 3 the campus's own ⋯ menu · 4 cancel/reason/feedback. */}
                <div className="lg:py-1 lg:px-2 flex items-center mt-2 lg:mt-0 lg:justify-center w-full">
                  <div className="mx-auto flex w-[184px] items-center justify-center gap-2">
                    {/* Slot 1: what moved at THIS campus since the reader last looked. */}
                    {(() => {
                      const indicator = row.changeSummary?.campusIndicators
                        ?.find(ci => ci.visitInstanceId === item.visitInstanceId);
                      return indicator ? (
                        <span
                          data-testid={`campus-change-dot-${variant}-${item.visitInstanceId}`}
                          title={`Có thay đổi tại cơ sở này${indicator.requiresAction ? ' — cần bạn xử lý' : ''}`}
                          className={`flex h-9 w-9 items-center justify-center ${indicator.requiresAction ? 'text-[#f37021]' : 'text-slate-400'}`}
                        >
                          <span className="h-2 w-2 rounded-full bg-current" />
                        </span>
                      ) : <span className="h-9 w-9" aria-hidden="true" />;
                    })()}

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

                    {/* Slot 3: this campus's own secondary actions. A handover lives HERE on a
                        multi-campus request — never on the summary row, which cannot say which
                        campus it would mean. */}
                    {(() => {
                      const campusMenu = buildCampusMenuItems(row, item);
                      return campusMenu.length > 0
                        ? <VisitRowActionMenu items={campusMenu} testId={`campus-menu-${variant}-${item.visitInstanceId}`} />
                        : <span className="h-9 w-9" aria-hidden="true" />;
                    })()}

                    {/* Slot 4: Cancel / Cancel Reason / Feedback */}
                    {item.instanceStatus === 'REJECTED' ? (
                      <ActionIconButton title="Xem lý do từ chối cơ sở" tone="orange" icon={<FileX className="h-5 w-5" />} onClick={() => openCampusRejectReason(row, item)} />
                    ) : item.canViewCancelReason ? (
                      <ActionIconButton title="Xem lý do hủy" tone="gray" icon={<FileMinus className="h-5 w-5" />} onClick={() => openCampusCancelReason(row, item)} />
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

  const hasActiveFilter = !!(appliedFilters.keyword || appliedFilters.status || appliedFilters.visitScope || appliedFilters.relation || appliedFilters.fromDate || appliedFilters.toDate);
  const emptyText = hasActiveFilter
    ? 'Không tìm thấy đơn phù hợp với bộ lọc.'
    : activeTab === 'attending'
      ? 'Bạn chưa có đơn mời tham dự nào.'
      : activeTab === 'registered'
        ? 'Bạn chưa đăng ký đoàn khách nào cho người khác.'
        : activeTab === 'hosted'
          ? 'Bạn chưa phụ trách tiếp đón đoàn khách nào.'
          : activeTab === 'all'
            ? isStaffLeader
              ? 'Chưa có đơn tiếp khách nào tại campus của bạn.'
              : 'Bạn chưa có đơn tiếp khách nào.'
            : isVisitor
              ? 'Bạn chưa là đầu mối của đơn tiếp khách nào.'
              : 'Bạn chưa có đơn phụ trách nào.';

  return (
    <div className="w-full flex flex-col space-y-4 pb-12 animate-in fade-in duration-300">
      {/* Header */}
      {!isEmbedded && (
        <>
          <div className="mb-1 flex items-center text-sm font-medium text-gray-500">
            <button onClick={() => navTo('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Dashboard</button>
            <span className="mx-2">/</span>
            <span className="text-[#004c91]">Quản lý tiếp khách</span>
          </div>
          {/* Page header: title group + action group each own their space (flex wrap) — the
              action button never sits under the layout notification bell (which reserves its
              own row in DashboardLayout, no fixed overlay). */}
          <div className="border-b border-gray-100 pb-4 flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
            <h1 className="min-w-0 text-3xl font-bold text-[#004c91]">{isVisitor ? 'Đơn của tôi' : 'Quản lý tiếp khách'}</h1>
            <div className="flex shrink-0 flex-wrap items-center gap-3 w-full md:w-auto">
              {canCreateVisitRequest && (
                <button
                  onClick={handleCreateVisitRequest}
                  disabled={v2Status === 'loading'}
                  aria-busy={v2Status === 'loading'}
                  className="flex items-center justify-center gap-2 bg-[#F37021] hover:bg-orange-600 outline-none focus-visible:ring-2 focus-visible:ring-[#F37021]/50 text-white px-4 py-2 rounded-lg font-bold shadow-sm transition-colors whitespace-nowrap w-full md:w-auto disabled:opacity-70 disabled:cursor-wait"
                >
                  <Plus className="w-5 h-5" /> Tạo đoàn khách
                </button>
              )}
              {isHO && (
                <button onClick={() => navTo('/dashboard/visit/agenda-templates')} className="flex items-center justify-center gap-2 bg-[#F37021] hover:bg-orange-600 outline-none text-white px-4 py-2 rounded-lg font-bold shadow-sm transition-colors whitespace-nowrap w-full md:w-auto">
                  <Plus className="w-5 h-5" /> Quản lý mẫu Agenda
                </button>
              )}
            </div>
          </div>
        </>
      )}

      {/* UC-27: pending invitations entry point (removed per user request to declutter) */}

      {/* Filters */}
      <div className="w-full rounded-2xl border border-slate-200 bg-white p-4 shadow-sm overflow-visible">
        <div className="flex flex-wrap gap-3 xl:items-end">
          {/* Search — first, grows to take leftover space */}
          <div className="min-w-[160px] flex-1 basis-40">
            <div className="relative w-full">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 w-5 h-5 shrink-0" />
              <input type="text" data-testid="visit-search-input" placeholder="Tìm tên đoàn, người phụ trách, đối tác..." value={draftFilters.keyword}
                onChange={(e) => {
                  const val = e.target.value;
                  setDraftFilters({ ...draftFilters, keyword: val });
                }}
                className="w-full pl-10 pr-4 h-11 bg-white border border-slate-300 rounded-xl text-sm font-semibold text-slate-700 outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10 transition-colors min-w-0" />
            </div>
          </div>

          {/* Loại đơn (filter button thay cho tabs) — width fits its own label */}
          {showTabFilter && (
            <div className="relative shrink-0">
              <button onClick={() => setIsTabFilterOpen(!isTabFilterOpen)} className="inline-flex h-11 items-center gap-2 whitespace-nowrap rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none transition-colors focus:border-[#004c91]">
                <span className="truncate">{tabOptions.find((t) => t.key === activeTab)?.label ?? 'Chọn loại đơn'}</span>
                <ChevronDown className="w-4 h-4 text-gray-500 flex-shrink-0 pointer-events-none" />
              </button>
              {isTabFilterOpen && (
                <>
                  <div className="fixed inset-0 z-10" onClick={() => setIsTabFilterOpen(false)} />
                  <div className="absolute left-0 top-full z-30 mt-1 min-w-[220px] w-max rounded-xl border border-slate-200 bg-white py-1 shadow-lg">
                    {tabOptions.map((t) => (
                      <div
                        key={t.key}
                        className={`px-3 py-2 text-sm cursor-pointer hover:bg-slate-50 flex items-center gap-4 ${activeTab === t.key ? 'bg-blue-50 text-[#004c91] font-bold' : 'text-gray-700 font-medium'}`}
                        onClick={() => {
                          setIsTabFilterOpen(false);
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
                      >
                        {t.label}
                        {activeTab === t.key && <Check className="w-4 h-4 ml-auto text-[#004c91]" />}
                      </div>
                    ))}
                  </div>
                </>
              )}
            </div>
          )}

          {/* Status — width fits its own label */}
          {filterConfig.showStatus && (
            <div className="relative shrink-0">
              <button onClick={() => setIsStatusFilterOpen(!isStatusFilterOpen)} className="inline-flex h-11 items-center gap-2 whitespace-nowrap rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none transition-colors focus:border-[#004c91]">
                <span className="truncate">{filterConfig.statusOptions.find((o) => o.value === draftFilters.status)?.label ?? 'Tất cả trạng thái'}</span>
                <ChevronDown className="w-4 h-4 text-gray-500 flex-shrink-0 pointer-events-none" />
              </button>
              {isStatusFilterOpen && (
                <>
                  <div className="fixed inset-0 z-10" onClick={() => setIsStatusFilterOpen(false)} />
                  <div className="absolute left-0 top-full z-30 mt-1 min-w-[220px] w-max rounded-xl border border-slate-200 bg-white py-1 shadow-lg max-h-72 overflow-y-auto">
                    {filterConfig.statusOptions.map((option) => (
                      <div key={option.value} title={option.description} className={`px-3 py-2 text-sm cursor-pointer hover:bg-slate-50 flex items-center gap-4 ${draftFilters.status === option.value ? 'bg-blue-50 text-[#004c91] font-bold' : 'text-gray-700 font-medium'}`}
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

          {/* Scope — width fits its own label */}
          {filterConfig.showScope && (
            <div className="relative shrink-0">
              <button onClick={() => setIsTypeFilterOpen(!isTypeFilterOpen)} className="inline-flex h-11 items-center gap-2 whitespace-nowrap rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none transition-colors focus:border-[#004c91]">
                <span className="truncate">{filterConfig.scopeOptions?.find((o) => o.value === draftFilters.visitScope)?.label ?? 'Tất cả phạm vi'}</span>
                <ChevronDown className="w-4 h-4 text-gray-500 flex-shrink-0 pointer-events-none" />
              </button>
              {isTypeFilterOpen && (
                <>
                  <div className="fixed inset-0 z-10" onClick={() => setIsTypeFilterOpen(false)} />
                  <div className="absolute left-0 top-full z-30 mt-1 min-w-[220px] w-max rounded-xl border border-slate-200 bg-white py-1 shadow-lg max-h-72 overflow-y-auto">
                    {filterConfig.scopeOptions?.map((option) => (
                      <div key={option.value} className={`px-3 py-2 text-sm cursor-pointer hover:bg-slate-50 flex items-center gap-4 ${draftFilters.visitScope === option.value ? 'bg-blue-50 text-[#004c91] font-bold' : 'text-gray-700 font-medium'}`}
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
            <div className="relative w-[190px] shrink-0">
              <button onClick={() => setIsCampusFilterOpen(!isCampusFilterOpen)} className="flex h-11 w-full min-w-0 items-center justify-between rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none transition-colors focus:border-[#004c91]">
                <span className="min-w-0 truncate">{campusOptions.find((o) => o.value === draftFilters.campusId)?.label ?? 'Tất cả cơ sở'}</span>
                <ChevronDown className="w-4 h-4 text-gray-500 flex-shrink-0 ml-2 pointer-events-none" />
              </button>
              {isCampusFilterOpen && (
                <>
                  <div className="fixed inset-0 z-10" onClick={() => setIsCampusFilterOpen(false)} />
                  <div className="absolute left-0 top-full z-30 mt-1 w-full min-w-[210px] rounded-xl border border-slate-200 bg-white py-1 shadow-lg max-h-72 overflow-y-auto">
                    {campusOptions.map((option) => (
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
            <div className="relative w-[170px] shrink-0">
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

          {/* Date range — width fits its own label */}
          <div className="relative shrink-0">
            <button onClick={() => setIsDateFilterOpen(!isDateFilterOpen)} className="inline-flex h-11 items-center gap-2 whitespace-nowrap rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none transition-colors focus:border-[#004c91]">
              <span className="truncate">
                {!draftFilters.fromDate && !draftFilters.toDate ? 'Chọn khoảng ngày'
                  : draftFilters.fromDate && !draftFilters.toDate ? `Từ ${formatDateOnly(draftFilters.fromDate)}`
                    : !draftFilters.fromDate && draftFilters.toDate ? `Đến ${formatDateOnly(draftFilters.toDate)}`
                      : `${formatDateOnly(draftFilters.fromDate)} - ${formatDateOnly(draftFilters.toDate)}`}
              </span>
              <Calendar className="w-4 h-4 text-gray-500 flex-shrink-0 pointer-events-none" />
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

          {/* Reset — pushed to the far right */}
          <div className="ml-auto flex shrink-0 items-end">
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
                !draftFilters.toDate &&
                !notificationVisitRequestId
              }
              className="h-11 rounded-xl border border-slate-200 bg-white px-4 text-sm font-bold text-slate-600 hover:bg-slate-50 disabled:text-slate-400 disabled:cursor-not-allowed outline-none transition-colors"
            >
              Reset
            </button>
          </div>
        </div>
        {filterError && <div className="text-red-500 text-sm font-medium mt-2"><AlertCircle className="w-4 h-4 inline-block mr-1" />{filterError}</div>}
      </div>

      {notificationVisitRequestId && (
        <div className="flex items-center justify-between gap-3 rounded-xl border border-blue-200 bg-blue-50 px-4 py-3 text-sm">
          <span className="font-medium text-blue-800">
            Đang hiển thị đúng đơn từ thông báo bạn vừa bấm.
          </span>
          <button
            onClick={handleResetFilters}
            className="shrink-0 rounded-lg border border-blue-300 bg-white px-3 py-1.5 text-xs font-bold text-blue-700 hover:bg-blue-100 transition-colors"
          >
            Xem tất cả
          </button>
        </div>
      )}

      {/* List */}
      <div className="w-full overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm flex flex-col">
        {/* Desktop */}
        <div data-testid="visit-list-desktop" className="hidden lg:block w-full">
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
                    <div className="py-3 px-3 flex flex-col items-center justify-center gap-0.5 text-center font-bold text-[#004c91] text-sm">
                      {row.changeSummary?.hasUnreadChanges && (
                        <span
                          data-testid={`stt-change-indicator-${row.id}`}
                          title={`Có thay đổi mới${row.changeSummary?.requiresViewerAction ? ' — cần bạn xử lý' : ''}`}
                          className={row.changeSummary?.requiresViewerAction ? 'text-[#f37021]' : 'text-blue-500'}
                        >
                          <Bell className="h-3.5 w-3.5" fill="currentColor" />
                        </span>
                      )}
                      <span>{(currentPage - 1) * pageSize + index + 1}</span>
                    </div>
                    <div className="py-3 px-3 min-w-0 flex flex-col justify-center pr-4">
                      <p className="text-sm font-bold text-[#004c91] line-clamp-2 break-words" title={row.name}>{row.name}</p>
                      <p className="text-xs font-medium text-slate-500 truncate" title={row.org}>{row.org}</p>
                      {!isHO && row.visitScope !== 'MULTI_CAMPUS' && rowTab(row) !== 'attending' && (
                        <p className="text-xs font-medium text-slate-600 mt-0.5 truncate">
                          <span className="text-slate-400">Người phụ trách tiếp đón:</span> {row.host || (row.campusStatus === 'WAITING_REQUEST_APPROVAL' ? 'Chưa được phân công' : '-')}
                          <span className="mx-1 text-slate-300">|</span>
                          <span className="text-slate-400">Cơ sở:</span> {row.campus || '-'}
                        </p>
                      )}
                      {renderBadges(row)}
                      <SearchMatchContexts contexts={row.matchedContexts} />
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
                      {(() => {
                        const sameDay = formatSameDayRange(row.plannedStartAt, row.plannedEndAt);
                        if (sameDay) {
                          return <div className="font-semibold text-slate-800 whitespace-nowrap">{sameDay}</div>;
                        }
                        return (
                          <>
                            <div className="flex items-center gap-2 whitespace-nowrap"><span className="w-9 text-slate-400 font-medium">Từ:</span><span className="font-semibold text-slate-800">{formatDateTimeShort(row.plannedStartAt)}</span></div>
                            <div className="flex items-center gap-2 whitespace-nowrap"><span className="w-9 text-slate-400 font-medium">Đến:</span><span className="font-semibold text-slate-800">{formatDateTimeShort(row.plannedEndAt)}</span></div>
                          </>
                        );
                      })()}
                    </div>
                    <div className="py-3 px-3 flex flex-col items-center justify-center gap-1">{getStatusBadge(row)}</div>
                    <div className="py-3 px-2 flex items-center justify-center" onClick={(e) => e.stopPropagation()}>{renderRowActions(row, 'desktop')}</div>
                  </div>
                  {isExpanded && row.canExpandCampuses && renderCampusAccordion(row, 'desktop')}
                </Fragment>
              );
            }) : (
              <div className="py-12 text-center text-slate-500 font-medium flex flex-col items-center justify-center"><Users className="w-12 h-12 text-slate-300 mb-3" /><p>{emptyText}</p></div>
            )}
          </div>
        </div>

        {/* Mobile / tablet */}
        <div data-testid="visit-list-mobile" className="lg:hidden w-full p-4 space-y-4 bg-slate-50/50">
          {isLoading ? (
            <div className="py-10 text-center text-slate-500 font-medium">Đang tải danh sách...</div>
          ) : rows.length > 0 ? rows.map((row) => {
            const isExpanded = expandedRequestId === row.visitRequestId;
            return (
              <Fragment key={row.id}>
                <div className={`rounded-2xl border bg-white p-4 shadow-sm transition-colors ${isExpanded ? 'border-[#004c91]/40' : 'border-slate-200 hover:border-[#004c91]/30'}`}>
                  <div className="flex items-start justify-between gap-3 mb-2">
                    <div className="min-w-0 flex-1">
                      <p className="font-bold text-[#004c91] text-sm line-clamp-2 leading-snug flex items-center gap-1.5">
                        {row.changeSummary?.hasUnreadChanges && (
                          <span
                            data-testid={`stt-change-indicator-mobile-${row.id}`}
                            title={`Có thay đổi mới${row.changeSummary?.requiresViewerAction ? ' — cần bạn xử lý' : ''}`}
                            className={`flex-shrink-0 ${row.changeSummary?.requiresViewerAction ? 'text-[#f37021]' : 'text-blue-500'}`}
                          >
                            <Bell className="h-3.5 w-3.5" fill="currentColor" />
                          </span>
                        )}
                        <span className="truncate">{row.name}</span>
                      </p>
                      <p className="text-xs text-slate-500 truncate">{row.org}</p>
                    </div>
                    <div className="flex-shrink-0">{getStatusBadge(row)}</div>
                  </div>
                  {renderBadges(row)}
                  <div className="grid grid-cols-1 gap-1.5 text-xs text-slate-600 bg-slate-50 p-3 rounded-xl border border-slate-100 mt-3">
                    <div className="flex items-center gap-2">
                      <Calendar className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" />
                      <span className="truncate">
                        {(() => {
                          const sameDay = formatSameDayRange(row.plannedStartAt, row.plannedEndAt);
                          if (sameDay) return sameDay;
                          return <>{formatDateTimeShort(row.plannedStartAt)} <span className="text-slate-400 mx-1">→</span> {formatDateTimeShort(row.plannedEndAt)}</>;
                        })()}
                      </span>
                    </div>
                    {!isHO && row.visitScope !== 'MULTI_CAMPUS' && rowTab(row) !== 'attending' && (
                      <>
                        <div className="flex items-center gap-2 mt-0.5"><Users className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" /><span className="truncate"><span className="text-slate-400">Người phụ trách tiếp đón:</span> {row.host || (row.requestStatus === 'APPROVED' && isVisitor ? 'Đang phân công' : 'Chưa được phân công')}</span></div>
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
                  <div className="mt-3 flex items-center justify-end border-t border-slate-100 pt-3" onClick={(e) => e.stopPropagation()}>{renderRowActions(row, 'mobile')}</div>
                </div>
                {isExpanded && row.canExpandCampuses && (
                  <div className="overflow-hidden rounded-2xl border border-slate-200">{renderCampusAccordion(row, 'mobile')}</div>
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

      {/* Tạo đoàn khách (shared form core, authenticated mode — không OTP). Đóng modal
          sau khi gửi thành công thì reload danh sách để thấy đơn mới ngay. */}
      {canCreateVisitRequest && (
        <>
          <VisitRequestV2Modal
            isOpen={showV2Modal}
            mode="authenticated"
            draftNamespace={visitDraftNamespace(user?.userId)}
            onClose={() => {
              setShowV2Modal(false);
              loadDelegations(activeTab, currentPage, pageSize, appliedFilters, sortOrder);
            }}
            onSuccess={() => {
              loadDelegations(activeTab, currentPage, pageSize, appliedFilters, sortOrder);
            }}
            onViewRequest={visitRequestId => navigate(`/dashboard/visit/v2/${visitRequestId}`)}
          />
        </>
      )}

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
                  Trường hợp người phụ trách tiếp đón hủy là do khách đã xác nhận hủy ngoài hệ thống.
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
            showSuccessToast('Đã duyệt cơ sở và phân công người phụ trách tiếp đón.');
            loadDelegations(activeTab, currentPage, pageSize, appliedFilters);
          }}
        />
      )}

      {/* Chuyển người phụ trách — opened from a row's ⋯ menu (single campus) or from a campus row
          inside the accordion (multi-campus). Same modal as the detail screen. */}
      {hostTransfer && (
        <VisitHostTransferModal
          campus={hostTransfer}
          onClose={() => setHostTransfer(null)}
          onTransferred={() => {
            setHostTransfer(null);
            loadDelegations(activeTab, currentPage, pageSize, appliedFilters, sortOrder);
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

    </div>
  );
}
