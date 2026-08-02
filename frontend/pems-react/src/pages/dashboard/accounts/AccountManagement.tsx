/**
 * Trang AccountManagement
 * Giao diện quản trị để quản lý tài khoản người dùng.
 * Bao gồm các chức năng phân trang, tìm kiếm, chỉnh sửa và tạo mới tài khoản.
 */

import React, { useState, useEffect, useRef, useMemo, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Users, UserCheck, UserX, Clock, Search,
  Shield, CheckCircle, XCircle, MoreVertical, Eye,
  Edit, Key, RefreshCw, Plus, X, UserCog, Briefcase, GraduationCap,
  ChevronLeft, ChevronRight, ChevronDown, ChevronUp, UserCircle, Mail, Lock
} from 'lucide-react';
import { useDebounce } from '../../../shared/hooks/useDebounce';
import { useAccountList } from '../../../features/account-management/hooks/useAccountList';
import { useAccountManagement } from '../../../features/account-management/hooks/useAccountManagement';
import { accountManagementApi } from '../../../features/account-management/api/accountManagementApi';
import type { AxiosError } from 'axios';
import {
  ACCOUNT_ERROR_MESSAGES,
  getAccountErrorMessage,
  getAccountRoleChangeBlockers,
} from '../../../features/account-management/api/accountError';
import type {
  AccountListItem,
  AccountListQueryParams,
  AccountStatistics,
  ActiveCampusOption,
  CampusDepartmentOption,
  CreateAccountRequest,
  HoCampusCheck,
  RoleAssignmentOptions,
  StaffLeaderAvailability,
} from '../../../features/account-management/types/accountManagement.types';
import {
  ACCOUNT_EMAIL_MAX_LENGTH,
  ACCOUNT_FULL_NAME_MAX_LENGTH,
  normalizeAccountEmail,
  normalizeFullName,
  validateAccountEmail,
  validateFullName,
} from '../../../features/account-management/validation/accountIdentityValidation';
import type { AccountIdentityFieldErrors } from '../../../features/account-management/validation/accountIdentityValidation';
import { resolveAccountStatusMeta } from '../../../features/account-management/adapters/accountStatusMeta';
import {
  canResendEmailConfirmation,
  resendDeliveryFeedback,
  resendResultSummary,
} from '../../../features/account-management/adapters/accountResendConfirmation';
import {
  isPendingEmailConfirmation as isPendingEmailConfirmationStatus,
  pendingEmailEditFeedback,
  shouldUsePendingEmailEdit,
} from '../../../features/account-management/adapters/accountPendingEmailEdit';
import { accountRoleUpdateFeedback } from '../../../features/account-management/adapters/accountRoleUpdateFeedback';
import { AccountStatusConfirmModal } from '../../../features/account-management/components/AccountStatusConfirmModal';
import { LoginEmailChangeConfirmModal } from '../../../features/account-management/components/LoginEmailChangeConfirmModal';
import { PendingEmailEditConfirmModal } from '../../../features/account-management/components/PendingEmailEditConfirmModal';
import { ReplaceStaffLeaderModal } from '../../../features/account-management/components/ReplaceStaffLeaderModal';
import { RelatedVisitorsTab } from '../../../features/account-management/components/RelatedVisitorsTab';

const CAMPUSES = ["Hà Nội", "Hồ Chí Minh", "Đà Nẵng", "Cần Thơ", "Quy Nhơn"];
const ROLES = ["ADMIN", "HO", "STAFF", "DEPARTMENT", "STUDENT", "VISITOR"];

// Status dropdown value → users.status. One map for both the server query and the client-side
// narrowing below, so a filter can never mean two different things depending on which one runs.
const STATUS_FILTER_TO_DB: Record<string, string> = {
  Active: 'ACTIVE',
  Inactive: 'INACTIVE',
  Locked: 'LOCKED',
  PendingEmail: 'PENDING_EMAIL_CONFIRMATION',
};

// UC-100-SL — isolated role-edit state (never mutates the account-detail snapshot).
// departmentId/studentCode are the dependent fields for DEPARTMENT / STUDENT respectively.
// fullName/email are the identity fields, editable only for eligible targets (see canEditIdentity).
interface RoleEditForm {
  roleCode: string;
  departmentId: string;
  studentCode: string;
  fullName: string;
  email: string;
  /** Successor for the department the target heads — only used when the change vacates that seat. */
  replacementHeadUserId: string;
}

// Whether Họ tên / Email may be edited for a target, derived from its ORIGINAL role/sub-role
// snapshot (never the role being chosen in the dropdown). Only a Staff Leader may edit identity,
// and only for STAFF/STAFF, DEPARTMENT/LEADER or STUDENT targets (spec §4.2 / §4.9).
const computeCanEditIdentity = (isStaffLeader: boolean, role?: string, rawSubRole?: unknown): boolean => {
  if (!isStaffLeader) return false;
  const r = String(role ?? '').toUpperCase();
  const sr = String(rawSubRole ?? '').toUpperCase();
  return (r === 'STAFF' && sr === 'STAFF') || (r === 'DEPARTMENT' && sr === 'LEADER') || r === 'STUDENT';
};

// Maps gender to its Vietnamese label. The backend exposes gender as the C# enum
// Gender { Male=0, Female=1, Other=2 } and serializes it as a NUMBER, so we map both the
// numeric codes (0/1/2) and the string ENUM names (MALE/FEMALE/OTHER/UNKNOWN).
const GENDER_LABELS: Record<string, string> = {
  '0': 'Nam', '1': 'Nữ', '2': 'Khác',
  MALE: 'Nam', FEMALE: 'Nữ', OTHER: 'Khác', UNKNOWN: 'Không xác định',
};
// Case-insensitive + idempotent: 'MALE'/'male' → 'Nam', and an already-localized 'Nam' stays 'Nam'.
// Tolerant of non-string inputs (the API/list may hand us null, numbers, etc.).
const genderLabel = (g?: unknown): string => {
  if (g === null || g === undefined || g === '') return '';
  const raw = String(g);
  return GENDER_LABELS[raw.trim().toUpperCase()] ?? raw;
};

// Position label for STAFF/DEPARTMENT accounts: sub_role LEADER → Trưởng phòng, STAFF → Nhân viên.
// Idempotent (an already-localized value stays put) so it is safe over either the raw sub_role
// or the backend's displayPosition.
const SUBROLE_LABELS: Record<string, string> = {
  LEADER: 'Trưởng phòng', STAFF: 'Nhân viên',
  'TRƯỞNG PHÒNG': 'Trưởng phòng', 'NHÂN VIÊN': 'Nhân viên',
};
const subRoleLabel = (s?: unknown): string => {
  if (s === null || s === undefined || s === '') return '';
  const raw = String(s).trim();
  return SUBROLE_LABELS[raw.toUpperCase()] ?? raw;
};

// Friendly role display name for the create-confirmation summary (spec §9). The SAME role code can
// mean different things depending on WHO creates it and via which flow — HO creating STAFF makes a
// Staff Leader (Trưởng phòng IC), a Staff Leader creating STAFF makes an IC Staff — so it is
// resolved from the actor context, never from roleCode alone. Create-flow only: existing
// list/detail rows keep the backend-provided roleName.
const resolveCreateRoleDisplayName = (
  role: string,
  ctx: { isHO: boolean; isStaffLeader: boolean },
): string => {
  const r = String(role ?? '').toUpperCase();
  if (ctx.isHO) {
    if (r === 'HO') return 'Head Office';
    if (r === 'STAFF') return 'Staff Leader — Trưởng phòng IC';
  }
  if (ctx.isStaffLeader) {
    if (r === 'STAFF') return 'IC Staff';
    if (r === 'DEPARTMENT') return 'Department Leader — Trưởng phòng ban';
    if (r === 'STUDENT') return 'Student';
  }
  // Fallback (ADMIN flow — outside the spec's HO/SL scope, but the summary must stay readable).
  switch (r) {
    case 'ADMIN': return 'Quản trị viên';
    case 'HO': return 'Head Office';
    case 'STAFF': return 'Staff';
    case 'DEPARTMENT': return 'Department Leader — Trưởng phòng ban';
    case 'STUDENT': return 'Student';
    case 'VISITOR': return 'Khách';
    default: return r || '—';
  }
};

// Human-readable projection of the create payload, shown on the confirmation screen (spec §10.2).
// Built together with the payload snapshot so the screen can never drift from what is submitted.
interface PendingCreateSummary {
  fullName: string;
  email: string;
  roleDisplayName: string;
  campusDisplayName?: string | null;
  departmentDisplayName?: string | null;
  studentCode?: string | null;
  phone?: string | null;
}

export function AccountManagement() {
  const navigate = useNavigate();
  const userStr = localStorage.getItem("currentUser");
  const user = userStr ? JSON.parse(userStr) : null;
  const isHO = user?.role?.toUpperCase() === 'HO';
  const isStaffLeader = user?.role?.toUpperCase() === 'STAFF' && user?.subRole?.toUpperCase() === 'LEADER';
  // Tách bạch ADMIN thật và Staff Leader — mỗi nơi dùng đúng cờ của mình,
  // KHÔNG gộp isAdmin = ADMIN || StaffLeader như trước.
  const isRealAdmin = user?.role?.toUpperCase() === 'ADMIN';
  const isStaff = user?.role?.toUpperCase() === 'STAFF';

  // ADMIN/HO xem toàn quốc mặc định; các role campus-scoped mặc định campus của mình.
  const defaultCampus = (isHO || isRealAdmin) ? "" : (user?.campus || "Hà Nội");
  const [allFilters, setAllFilters] = useState({ search: "", campus: defaultCampus, role: "", status: "", accountType: "INTERNAL" });
  const [pendingFilters, setPendingFilters] = useState({ search: "", campus: defaultCampus, role: "", status: "", accountType: "INTERNAL" });
  
  const [activeTab, setActiveTab] = useState<'all' | 'pending'>('all');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');

  const currentFilters = activeTab === 'all' ? allFilters : pendingFilters;
  const setCurrentFilters = activeTab === 'all' ? setAllFilters : setPendingFilters;

  const searchQuery = currentFilters.search;
  const campusFilter = currentFilters.campus;
  const roleFilter = currentFilters.role;
  const statusFilter = currentFilters.status;
  const accountTypeFilter = currentFilters.accountType;

  const setSearchQuery = (val: string) => setCurrentFilters(prev => ({ ...prev, search: val }));
  const setCampusFilter = (val: string) => setCurrentFilters(prev => ({ ...prev, campus: val }));
  const setStatusFilter = (val: string) => setCurrentFilters(prev => ({ ...prev, status: val }));
  const setAccountTypeFilter = (val: string) => setCurrentFilters(prev => ({
    ...prev,
    accountType: val,
    // Leaving VISITOR (to INTERNAL or ALL) drops the now-meaningless role=VISITOR filter,
    // otherwise "Tất cả tài khoản" would still silently show only Visitor rows.
    role: val !== 'VISITOR' && prev.role === 'VISITOR' ? '' : prev.role,
  }));

  // A Staff Leader's Visitor mode is a different screen, not a filtered view of the internal one:
  // different endpoint, different table, read-only. Everything internal (list request, stat cards,
  // create button, role filter) is switched off while it is on.
  const isVisitorMode = isStaffLeader && accountTypeFilter === 'VISITOR';

  // Says WHOSE accounts the current mode lists, so the two very different lists are not mistaken
  // for one another. Copy is the project owner's — do not reword without asking.
  const accountManagementSubtitle = isVisitorMode
    ? 'Danh sách tài khoản khách có yêu cầu tham quan liên quan đến cơ sở'
    : 'Quản lý tài khoản của nhân sự phòng IC, trưởng phòng của các phòng ban khác và sinh viên trong cơ sở';
  const setRoleFilter = (val: string) => setCurrentFilters(prev => ({
    ...prev,
    role: val,
    accountType: val === 'VISITOR' ? 'VISITOR' : (prev.accountType === 'VISITOR' && val ? 'INTERNAL' : prev.accountType),
  }));

  // Pagination State
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const tableRef = useRef<HTMLDivElement>(null);

  const scrollToTable = () => {
    setTimeout(() => {
      tableRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }, 100);
  };

  // Modal & Drawer State
  const [isViewDrawerOpen, setIsViewDrawerOpen] = useState(false);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isResetPasswordModalOpen, setIsResetPasswordModalOpen] = useState(false);
  // Replace Staff Leader modal (HO only): set to the campus of the Staff Leader being replaced.
  const [replaceLeaderTarget, setReplaceLeaderTarget] = useState<{ campusId: string; campusName: string } | null>(null);
  const [selectedAccount, setSelectedAccount] = useState<any>(null);
  // True only once the UC-98 detail request has come back. The resend button keys off the DETAIL
  // status, so while this is false the list row must not be allowed to stand in for it.
  const [detailLoaded, setDetailLoaded] = useState(false);

  // ── Resend email confirmation (pending accounts). Scoped to the open detail modal: every field
  //    is cleared when the drawer closes or another account is opened, so one account's cooldown /
  //    limit state can never be read as another's. ──
  const [isResendConfirmOpen, setIsResendConfirmOpen] = useState(false);
  const [resendSubmitting, setResendSubmitting] = useState(false);
  const [resendError, setResendError] = useState<string | null>(null);
  const [resendLimitReached, setResendLimitReached] = useState(false);
  const [lastResendCount, setLastResendCount] = useState<number | null>(null);
  const [lastDeliveryStatus, setLastDeliveryStatus] = useState<string | null>(null);

  const [isEditingProfile, setIsEditingProfile] = useState(false);
  const [editForm, setEditForm] = useState<any>(null);
  // UC-100-SL role editor: isolated form + backend-provided options (campus scoped).
  const [roleEditForm, setRoleEditForm] = useState<RoleEditForm | null>(null);
  const [roleOptions, setRoleOptions] = useState<RoleAssignmentOptions | null>(null);
  const [roleOptionsLoading, setRoleOptionsLoading] = useState(false);
  const [roleOptionsError, setRoleOptionsError] = useState<string | null>(null);

  const [createMethod, setCreateMethod] = useState<'AUTO' | 'MANUAL'>('AUTO');
  const [selectedDept, setSelectedDept] = useState("");
  const [selectedDeptStaffId, setSelectedDeptStaffId] = useState("");
  const [createCampus, setCreateCampus] = useState("");
  
  const [manualForm, setManualForm] = useState({
    role: "",
    name: "",
    email: "",
    phone: "",
    gender: "Nam",
    studentCode: "",
  });
  // Field-level error shown right under the MSSV input in the create modal (spec §5.5 / §7.2).
  const [createStudentCodeError, setCreateStudentCodeError] = useState<string | null>(null);
  // Identity (họ tên / email) errors rendered under their own input, not in the footer alert.
  // Shared rules with the backend — see validation/accountIdentityValidation.ts.
  const [createFieldErrors, setCreateFieldErrors] = useState<AccountIdentityFieldErrors>({});
  const [editFieldErrors, setEditFieldErrors] = useState<AccountIdentityFieldErrors>({});

  const mockDeptStaff = [
    { id: '1', department: 'Phòng Đào tạo', name: 'Nguyễn Văn A (Phó trưởng phòng)', email: 'nguyenvana.dt@fpt.edu.vn', phone: '0987654321' },
    { id: '2', department: 'Phòng Công tác sinh viên', name: 'Trần Thị B (Chuyên viên)', email: 'tranthib.ctsv@fpt.edu.vn', phone: '0912345678' },
    { id: '3', department: 'Phòng Đào tạo', name: 'Lê Văn C (Nhân viên)', email: 'levanc.dt@fpt.edu.vn', phone: '0933333333' },
  ];
  
  const currentSelectedStaff = mockDeptStaff.find(s => s.id === selectedDeptStaffId);

  // Resets every transient bit of the role editor without touching selectedAccount (the snapshot).
  const resetRoleEditor = () => {
    setIsEditingProfile(false);
    setEditForm(null);
    setRoleEditForm(null);
    setRoleOptions(null);
    setRoleOptionsError(null);
    setRoleError(null);
    setRoleSaving(false);
    setBasicInfoEmailConfirm(null);
    setPendingEmailEditConfirm(null);
    setEditFieldErrors({});
  };

  /** Clears every resend field so nothing leaks from one account's modal session into the next. */
  const resetResendState = () => {
    setIsResendConfirmOpen(false);
    setResendSubmitting(false);
    setResendError(null);
    setResendLimitReached(false);
    setLastResendCount(null);
    setLastDeliveryStatus(null);
  };

  const closeViewDrawer = () => {
    setIsViewDrawerOpen(false);
    setDetailLoaded(false);
    resetResendState();
    resetRoleEditor();
  };

  // UC-100-SL — role-assignment options (campus IC dept + active GENERAL depts), scoped server-side.
  const loadRoleOptions = useCallback(async (targetUserId: string | number) => {
    setRoleOptionsLoading(true);
    setRoleOptionsError(null);
    try {
      const opts = await accountManagementApi.getRoleAssignmentOptions(targetUserId);
      setRoleOptions(opts);
    } catch {
      setRoleOptions(null);
      setRoleOptionsError('Không thể tải danh sách phòng ban. Vui lòng thử lại.');
    } finally {
      setRoleOptionsLoading(false);
    }
  }, []);

  const handleEditClick = () => {
    if (!selectedAccount) return;
    // Role editing is intentionally isolated from the account-detail snapshot.
    // All other fields keep the exact values loaded by UC-98 and remain read-only.
    setRoleError(null);
    setEditFieldErrors({});
    const roleCode: string = selectedAccount.role;
    setRoleEditForm({
      roleCode,
      // Preserve the original dependent value on first open (spec §3.4 / §3.5.6).
      departmentId: roleCode === 'DEPARTMENT' && selectedAccount.departmentId != null
        ? String(selectedAccount.departmentId)
        : '',
      studentCode: roleCode === 'STUDENT' ? (selectedAccount.studentId ?? '') : '',
      // Identity fields seeded from the snapshot; only editable for eligible targets.
      fullName: selectedAccount.name ?? '',
      email: selectedAccount.email ?? '',
      replacementHeadUserId: '',
    });
    setEditForm({ role: roleCode });
    setIsEditingProfile(true);
    // Staff Leader flow needs the campus-scoped department options; ADMIN keeps the legacy path.
    if (isStaffLeader) loadRoleOptions(selectedAccount.userId ?? selectedAccount.id);
  };

  useEffect(() => {
    setCurrentPage(1);
  }, [activeTab, searchQuery, campusFilter, roleFilter, statusFilter, accountTypeFilter]);

  // ── UC-95 / UC-99: real account data from the API (replaces the old mock) ──
  const [accounts, setAccounts] = useState<any[]>([]);
  const [campusOptions, setCampusOptions] = useState<ActiveCampusOption[]>([]);

  // Active campuses — used to translate the HO/ADMIN campus-name filter into a campusId.
  useEffect(() => {
    if (!isHO && !isRealAdmin) return;
    let active = true;
    accountManagementApi
      .getActiveCampuses()
      .then((list) => { if (active) setCampusOptions(list); })
      .catch(() => { /* non-fatal: campus filter simply falls back to no filter */ });
    return () => { active = false; };
  }, [isHO, isRealAdmin]);

  // Debounce the keyword so we don't call the API on every keystroke.
  const debouncedSearch = useDebounce(searchQuery, 450);

  // Map the "all" tab UI filters → backend query params (server enforces scope/paging).
  const listParams = useMemo<AccountListQueryParams>(() => {
    const campusId = (isHO || isRealAdmin) && campusFilter
      ? campusOptions.find((c) => c.campusName.includes(campusFilter))?.campusId
      : undefined;

    const status = STATUS_FILTER_TO_DB[statusFilter];

    return {
      page: currentPage,
      pageSize,
      keyword: debouncedSearch.trim() || undefined,
      roleCode: roleFilter || undefined,
      status,
      campusId,
      // Server-side INTERNAL/VISITOR split (AccountListQueryExecutor already supports it). Without
      // this the split was only applied client-side to whatever 20 rows the current page happened
      // to contain, so switching the filter could show 0 rows (or a wrong total) depending on which
      // account types landed on that page.
      accountType: (accountTypeFilter || undefined) as AccountListQueryParams['accountType'],
      sortBy: 'createdAt',
      sortDirection: 'desc',
    };
  }, [isHO, isRealAdmin, campusFilter, campusOptions, statusFilter, currentPage, pageSize, debouncedSearch, roleFilter, accountTypeFilter]);

  const {
    data: accountsData,
    loading: accountsLoading,
    error: accountsError,
    refetch: refetchAccounts,
    // Visitor mode is served entirely by RelatedVisitorsTab's own endpoint — the internal account
    // list must not keep running underneath it (wasted request, and its totals could leak into the
    // Visitor pagination).
  } = useAccountList(listParams, activeTab === 'all' && !isVisitorMode);

  // UC-97 / UC-98 mutations + detail fetch (create/update-role call the API directly
  // so the modal can surface the backend's specific error message).
  const { manageAccountStatus, getAccountDetails } = useAccountManagement();

  // UC-96 create modal feedback.
  const [createError, setCreateError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  // Create-confirmation step (spec §4/§15): "Tiếp tục" builds a snapshot and opens this screen;
  // the API is only called from "Xác nhận tạo tài khoản". pendingCreatePayload is the EXACT object
  // sent to the backend; pendingCreateSummary is its display projection (spec §10.3 — no drift).
  const [isCreateConfirmOpen, setIsCreateConfirmOpen] = useState(false);
  const [pendingCreatePayload, setPendingCreatePayload] = useState<CreateAccountRequest | null>(null);
  const [pendingCreateSummary, setPendingCreateSummary] = useState<PendingCreateSummary | null>(null);
  const confirmCreateBtnRef = useRef<HTMLButtonElement>(null);

  // UC-96 Staff Leader availability pre-check (HO picks a campus for role STAFF). Drives the
  // warning panel + disabled submit so HO can't try to create a 2nd Trưởng phòng IC.
  const [slAvailability, setSlAvailability] = useState<StaffLeaderAvailability | null>(null);
  const [slAvailabilityLoading, setSlAvailabilityLoading] = useState(false);

  // UC-96 HO campus pre-check (HO picks a campus for role HO). Drives the warning panel +
  // disabled submit so HO can't try to create a 2nd HO for a campus that already has one.
  const [hoCampusCheck, setHoCampusCheck] = useState<HoCampusCheck | null>(null);
  const [hoCampusCheckLoading, setHoCampusCheckLoading] = useState(false);

  // UC-97 enable/disable confirmation dialog.
  const [statusTarget, setStatusTarget] = useState<any | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);
  const [statusSaving, setStatusSaving] = useState(false);

  // ADMIN LOCK/UNLOCK — flow riêng, tách khỏi toggle ACTIVE↔INACTIVE, có nhập lý do.
  const [lockTarget, setLockTarget] = useState<any | null>(null);
  const [lockReason, setLockReason] = useState('');
  const [lockError, setLockError] = useState<string | null>(null);
  const [lockSaving, setLockSaving] = useState(false);

  // UC-100 role-update feedback (detail drawer edit).
  const [roleSaving, setRoleSaving] = useState(false);
  // Carries the full refusal text — for a 409 blocker error that is the backend's per-blocker
  // breakdown, rendered as a single panel inside the drawer (the toast only summarises it).
  const [roleError, setRoleError] = useState<string | null>(null);
  // HO_BASIC_INFO — email-change confirmation (spec §10). Set to {oldEmail,newEmail} to prompt.
  const [basicInfoEmailConfirm, setBasicInfoEmailConfirm] = useState<{ oldEmail: string; newEmail: string } | null>(null);
  // Pending account: address correction that re-issues the activation link. A separate prompt from
  // the one above because the consequences it has to state are different ones.
  const [pendingEmailEditConfirm, setPendingEmailEditConfirm] =
    useState<{ oldEmail: string; newEmail: string } | null>(null);

  // Lightweight toast notifications (create + email outcome, status, role update).
  const [toasts, setToasts] = useState<{ id: number; type: 'success' | 'error' | 'warning'; msg: string }[]>([]);
  const toastSeq = useRef(0);
  const pushToast = useCallback((type: 'success' | 'error' | 'warning', msg: string) => {
    const id = ++toastSeq.current;
    setToasts((prev) => [...prev, { id, type, msg }]);
    setTimeout(() => setToasts((prev) => prev.filter((t) => t.id !== id)), 5000);
  }, []);

  // UC-95-SL statistics — SL: campus của mình; ADMIN: toàn hệ thống (backend tự scope).
  const [statistics, setStatistics] = useState<AccountStatistics | null>(null);
  const loadStatistics = useCallback(() => {
    if (!isStaffLeader && !isRealAdmin) return;
    accountManagementApi.getStatistics().then(setStatistics).catch(() => { /* non-fatal */ });
  }, [isStaffLeader, isRealAdmin]);
  useEffect(() => { loadStatistics(); }, [loadStatistics]);

  // Active GENERAL departments of the leader's campus (Department-Leader dropdown).
  const [campusDepartments, setCampusDepartments] = useState<CampusDepartmentOption[]>([]);
  useEffect(() => {
    if (!isStaffLeader) return;
    let active = true;
    accountManagementApi
      .getCampusDepartments()
      .then((d) => { if (active) setCampusDepartments(d); })
      .catch(() => { /* non-fatal: dropdown simply stays empty */ });
    return () => { active = false; };
  }, [isStaffLeader]);

  // UC-96 — when HO picks a campus for role STAFF (Trưởng phòng IC), pre-check whether a Staff
  // Leader can be created there. Mirrors the authoritative server check so the form can warn and
  // disable submit before HO ever clicks "Xác nhận tạo". (Spec §11.1 / §12.)
  useEffect(() => {
    const shouldCheck = isHO && isCreateModalOpen && manualForm.role === 'STAFF' && !!createCampus;
    if (!shouldCheck) { setSlAvailability(null); setSlAvailabilityLoading(false); return; }

    const campusId = campusOptions.find((c) => c.campusName.includes(createCampus))?.campusId;
    if (!campusId) { setSlAvailability(null); setSlAvailabilityLoading(false); return; }

    let active = true;
    setSlAvailabilityLoading(true);
    setSlAvailability(null);
    accountManagementApi
      .getStaffLeaderAvailability(campusId)
      .then((res) => { if (active) setSlAvailability(res); })
      .catch(() => { if (active) setSlAvailability(null); /* non-fatal: server still re-checks on submit */ })
      .finally(() => { if (active) setSlAvailabilityLoading(false); });
    return () => { active = false; };
  }, [isHO, isCreateModalOpen, manualForm.role, createCampus, campusOptions]);

  // UC-96 — when HO picks a campus for role HO, pre-check whether a new HO can be created there.
  // Mirrors the authoritative server check so the form can warn and disable submit before HO
  // clicks "Xác nhận tạo". (HO_CREATE_HO_ACCOUNT spec §11.1 / §12.)
  useEffect(() => {
    const shouldCheck = isHO && isCreateModalOpen && manualForm.role === 'HO' && !!createCampus;
    if (!shouldCheck) { setHoCampusCheck(null); setHoCampusCheckLoading(false); return; }

    const campusId = campusOptions.find((c) => c.campusName.includes(createCampus))?.campusId;
    if (!campusId) { setHoCampusCheck(null); setHoCampusCheckLoading(false); return; }

    let active = true;
    setHoCampusCheckLoading(true);
    setHoCampusCheck(null);
    accountManagementApi
      .getHoCampusCheck(campusId)
      .then((res) => { if (active) setHoCampusCheck(res); })
      .catch(() => { if (active) setHoCampusCheck(null); /* non-fatal: server still re-checks on submit */ })
      .finally(() => { if (active) setHoCampusCheckLoading(false); });
    return () => { active = false; };
  }, [isHO, isCreateModalOpen, manualForm.role, createCampus, campusOptions]);

  // Project API rows into the shape the existing table/drawer already expect.
  useEffect(() => {
    if (activeTab !== 'all') return;
    const items = accountsData?.items ?? [];
    setAccounts(items.map((a: AccountListItem) => ({
      id: a.userId,
      userId: a.userId,
      name: a.fullName,
      email: a.email,
      campus: a.campusName || '',
      campusId: a.campusId,
      role: a.roleCode,
      roleName: a.roleName,
      loginStatus: a.lastLoginAt ? 'Đã đăng nhập' : 'Chưa từng đăng nhập',
      status: a.status === 'ACTIVE' ? 'Active' : a.status === 'INACTIVE' ? 'Inactive' : a.status === 'PENDING_EMAIL_CONFIRMATION' ? 'Pending' : 'Locked',
      rawStatus: a.status,
      gender: genderLabel(a.gender),
      phone: a.phone,
      avatar: a.avatarUrl || `https://ui-avatars.com/api/?name=${encodeURIComponent(a.fullName)}&background=random`,
      createdAt: a.createdAt ? a.createdAt.substring(0, 10) : '',
      departmentId: a.departmentId,
      department: a.departmentName,
      subRole: a.subRole,
      rawSubRole: a.subRole,
      studentId: a.studentCode,
      major: null,
      nationality: a.nationality,
      organization: null,
      manageScope: null,
      canViewDetails: a.canViewDetails,
      canUpdateRole: a.canUpdateRole,
      canManageStatus: a.canManageStatus,
      canEditBasicInfo: a.canEditBasicInfo,
      isCurrentUser: a.isCurrentUser,
    })));
  }, [accountsData, activeTab]);

  const [pendingAccounts, setPendingAccounts] = useState(() => [
    {
      id: 101,
      name: `Nguyễn Văn Chờ Duyệt 1`,
      email: `pending1@fpt.edu.vn`,
      campus: CAMPUSES[0],
      role: 'STUDENT',
      loginStatus: "Chưa từng đăng nhập",
      status: "Pending Approved",
      gender: "Nam",
      phone: `0900000088`,
      avatar: `https://ui-avatars.com/api/?name=Nguyễn+Văn+C&background=random`,
      createdAt: "2023-11-01",
      department: null,
      subRole: null,
      studentId: `HE160001`,
      major: "Kỹ thuật phần mềm",
      nationality: null,
      organization: null,
      manageScope: null
    },
    {
      id: 102,
      name: `Trần Thị Chờ Duyệt 2`,
      email: `pending2@fpt.edu.vn`,
      campus: CAMPUSES[1],
      role: 'VISITOR',
      loginStatus: "Chưa từng đăng nhập",
      status: "Approved",
      gender: "Nữ",
      phone: `0900000099`,
      avatar: `https://ui-avatars.com/api/?name=Trần+Thị+C&background=random`,
      createdAt: "2023-11-02",
      department: null,
      subRole: null,
      studentId: null,
      major: null,
      nationality: "Mỹ",
      organization: "Đại học ABC",
      manageScope: null
    },
    {
      id: 103,
      name: `Lê Văn Chờ Duyệt 3`,
      email: `pending3@fpt.edu.vn`,
      campus: CAMPUSES[2],
      role: 'STAFF',
      loginStatus: "Chưa từng đăng nhập",
      status: "Rejected",
      gender: "Nam",
      phone: `0900000077`,
      avatar: `https://ui-avatars.com/api/?name=Lê+Văn+C&background=random`,
      createdAt: "2023-11-03",
      department: "Phòng Tuyển sinh",
      subRole: "Nhân viên",
      studentId: null,
      major: null,
      nationality: null,
      organization: null,
      manageScope: null
    }
  ]);

  // Mock data for top widgets
  const statsBase = [
    { label: "Tổng số tài khoản", value: (accountsData?.totalItems ?? accounts.length).toString(), icon: Users, color: "text-[#004c91]", bg: "bg-white border-gray-100 shadow-sm outline-none", iconBg: "bg-blue-50", onClick: () => { setActiveTab('all'); setAllFilters(prev => ({ ...prev, status: '', role: '', search: '' })); scrollToTable(); } },
    { label: "Tài khoản hoạt động", value: accounts.filter(a => a.status === 'Active').length.toString(), icon: UserCheck, color: "text-[#0aa14f]", bg: "bg-white border-gray-100 shadow-sm outline-none", iconBg: "bg-green-50", onClick: () => { setActiveTab('all'); setAllFilters(prev => ({ ...prev, status: 'Active' })); scrollToTable(); } },
    { label: "Tài khoản bị khóa", value: accounts.filter(a => a.status !== 'Active').length.toString(), icon: XCircle, color: "text-red-500", bg: "bg-white border-gray-100 shadow-sm outline-none", iconBg: "bg-red-50", onClick: () => { setActiveTab('all'); setAllFilters(prev => ({ ...prev, status: 'Inactive' })); scrollToTable(); } },
    { label: "Chưa từng đăng nhập", value: accounts.filter(a => a.loginStatus === 'Chưa từng đăng nhập').length.toString(), icon: UserX, color: "text-gray-500", bg: "bg-white border-gray-100 shadow-sm outline-none", iconBg: "bg-gray-100", onClick: () => { setActiveTab('all'); setAllFilters(prev => ({ ...prev, status: 'NoLogin' })); scrollToTable(); } },
  ];
  
  // UC-95-SL stat cards — campus-scoped totals from the statistics API.
  const slStats = [
    { label: "Tổng số tài khoản", value: (statistics?.totalAccounts ?? 0).toString(), icon: Users, color: "text-[#004c91]", bg: "bg-white border-gray-100 shadow-sm outline-none", iconBg: "bg-blue-50", onClick: () => { setActiveTab('all'); setAllFilters(prev => ({ ...prev, status: '', role: '', search: '' })); scrollToTable(); } },
    { label: "Tài khoản hoạt động", value: (statistics?.activeAccounts ?? 0).toString(), icon: UserCheck, color: "text-[#0aa14f]", bg: "bg-white border-gray-100 shadow-sm outline-none", iconBg: "bg-green-50", onClick: () => { setActiveTab('all'); setAllFilters(prev => ({ ...prev, status: 'Active' })); scrollToTable(); } },
    { label: "Tài khoản vô hiệu hóa", value: (statistics?.inactiveAccounts ?? 0).toString(), icon: UserX, color: "text-amber-500", bg: "bg-white border-gray-100 shadow-sm outline-none", iconBg: "bg-amber-50", onClick: () => { setActiveTab('all'); setAllFilters(prev => ({ ...prev, status: 'Inactive' })); scrollToTable(); } },
    { label: "Tài khoản bị khóa", value: (statistics?.lockedAccounts ?? 0).toString(), icon: XCircle, color: "text-red-500", bg: "bg-white border-gray-100 shadow-sm outline-none", iconBg: "bg-red-50", onClick: () => { setActiveTab('all'); setAllFilters(prev => ({ ...prev, status: 'Locked' })); scrollToTable(); } },
  ];

  // ADMIN dùng chung bộ card thống kê thật từ /accounts/statistics như Staff Leader
  // (backend trả số toàn hệ thống cho ADMIN) — không dùng số đếm từ trang hiện tại.
  // HO sees no stat cards at all — only the create button. The per-campus counters were removed
  // on purpose: HO works across every campus, so five per-campus totals said little and pushed the
  // table below the fold.
  const stats = isHO
    ? []
    : (isStaffLeader || isRealAdmin)
    ? slStats
    : [...statsBase, { label: "Yêu cầu chờ duyệt", value: pendingAccounts.length.toString(), icon: Clock, color: "text-[#f37021]", bg: "bg-white border-gray-100 shadow-sm outline-none", iconBg: "bg-orange-50", onClick: () => { setActiveTab('pending'); setPendingFilters(prev => ({ ...prev, status: '', role: '', search: '' })); scrollToTable(); } }];


  // The "all" tab is server-driven (UC-95/UC-99): the API already applied scope,
  // search, filters and paging. The "pending" tab stays client-side (out of scope).
  const isServerTab = activeTab === 'all';

  const filteredPending = pendingAccounts.filter(acc => {
    const matchesSearch = acc.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
                          acc.email.toLowerCase().includes(searchQuery.toLowerCase()) ||
                          (acc.studentId && acc.studentId.toLowerCase().includes(searchQuery.toLowerCase()));
    const matchesCampus = (!isHO || !campusFilter) ? true : acc.campus === campusFilter;
    const matchesRole = roleFilter === "" ? true : acc.role === roleFilter;
    const matchesStatus = statusFilter === "" ? true : acc.status === statusFilter;
    const matchesStaffLeaderRole = isStaffLeader ? ['STAFF', 'DEPARTMENT', 'STUDENT', 'VISITOR'].includes(acc.role) : true;
    return matchesSearch && matchesCampus && matchesRole && matchesStatus && matchesStaffLeaderRole;
  });

  const sortedPending = [...filteredPending].sort((a, b) => {
    const timeA = new Date(a.createdAt || 0).getTime();
    const timeB = new Date(b.createdAt || 0).getTime();
    return sortOrder === 'asc' ? timeA - timeB : timeB - timeA;
  });

  const paginatedAccounts = useMemo(() => {
    if (!isServerTab) {
      return sortedPending.slice((currentPage - 1) * pageSize, currentPage * pageSize);
    }
    let list = accounts;
    if (accountTypeFilter === 'INTERNAL') {
      list = list.filter(acc => String(acc.role).toUpperCase() !== 'VISITOR');
    }
    if (roleFilter) {
      list = list.filter(acc => String(acc.role).toUpperCase() === roleFilter.toUpperCase());
    }
    if (statusFilter) {
      const s = STATUS_FILTER_TO_DB[statusFilter] ?? statusFilter.toUpperCase();
      list = list.filter(acc => String(acc.status).toUpperCase() === s || String(acc.rawStatus || '').toUpperCase() === s);
    }
    if (searchQuery.trim()) {
      const q = searchQuery.trim().toLowerCase();
      list = list.filter(acc =>
        (acc.name && acc.name.toLowerCase().includes(q)) ||
        (acc.email && acc.email.toLowerCase().includes(q)) ||
        (acc.studentId && acc.studentId.toLowerCase().includes(q))
      );
    }
    return list;
  }, [isServerTab, accountTypeFilter, roleFilter, statusFilter, searchQuery, accounts, sortedPending, currentPage, pageSize]);

  const totalItems = isServerTab
    ? (roleFilter || statusFilter || searchQuery.trim() ? paginatedAccounts.length : (accountsData?.totalItems ?? 0))
    : sortedPending.length;

  const totalPages = isServerTab
    ? (roleFilter || statusFilter || searchQuery.trim() ? Math.ceil(paginatedAccounts.length / pageSize) || 1 : (accountsData?.totalPages ?? 0))
    : Math.ceil(sortedPending.length / pageSize) || 1;

  const getRoleStyle = (role: string) => {
    switch(role.toUpperCase()) {
      case 'ADMIN': return 'bg-red-50 text-red-700 border-red-200';
      case 'HO': return 'bg-blue-50 text-blue-700 border-blue-200';
      case 'DEPARTMENT': return 'bg-orange-50 text-orange-700 border-orange-200';
      case 'STAFF': return 'bg-emerald-50 text-emerald-700 border-emerald-200';
      case 'STUDENT': return 'bg-purple-50 text-purple-700 border-purple-200';
      case 'VISITOR': return 'bg-blue-50 text-blue-700 border-blue-200';
      default: return 'bg-gray-50 text-gray-700 border-gray-200';
    }
  };

  // UC-97 — open the enable/disable confirmation for an account (real API call on confirm).
  const requestToggleStatus = (acc: any) => {
    if (isServerTab && acc.canManageStatus === false) return; // out of scope / self / locked
    setStatusError(null);
    setStatusTarget(acc);
  };

  const confirmToggleStatus = async () => {
    if (!statusTarget) return;
    const nextStatus = statusTarget.status === 'Active' ? 'INACTIVE' : 'ACTIVE';
    setStatusSaving(true);
    setStatusError(null);
    const result = await manageAccountStatus({ userId: statusTarget.userId ?? statusTarget.id, status: nextStatus });
    setStatusSaving(false);
    if (!result) {
      setStatusError('Không thể cập nhật trạng thái tài khoản. Vui lòng thử lại.');
      return;
    }
    setStatusTarget(null);
    pushToast('success', nextStatus === 'INACTIVE' ? 'Đã vô hiệu hóa tài khoản.' : 'Đã kích hoạt tài khoản.');
    refetchAccounts();
    loadStatistics();
  };

  // ADMIN LOCK/UNLOCK — flow riêng: LOCKED ↔ ACTIVE với lý do; backend tự thu hồi
  // toàn bộ phiên khi tài khoản rời trạng thái ACTIVE và chặn khóa Admin cuối cùng.
  const confirmLockToggle = async () => {
    if (!lockTarget) return;
    const nextStatus = lockTarget.status === 'Locked' ? 'ACTIVE' : 'LOCKED';
    if (nextStatus === 'LOCKED' && !lockReason.trim()) {
      setLockError('Vui lòng nhập lý do khóa tài khoản.');
      return;
    }
    setLockSaving(true);
    setLockError(null);
    const result = await manageAccountStatus({
      userId: lockTarget.userId ?? lockTarget.id,
      status: nextStatus,
      reason: lockReason.trim() || null,
    });
    setLockSaving(false);
    if (!result) {
      setLockError('Không thể cập nhật trạng thái khóa. Vui lòng thử lại.');
      return;
    }
    setLockTarget(null);
    setLockReason('');
    pushToast('success', nextStatus === 'LOCKED'
      ? 'Đã khóa tài khoản và thu hồi toàn bộ phiên đăng nhập.'
      : 'Đã mở khóa tài khoản.');
    refetchAccounts();
    loadStatistics();
  };

  // UC-98 — open the detail drawer, fetching the safe detail projection from the API.
  const openViewDrawer = async (acc: any) => {
    // Reset BEFORE the request: opening account B must not show account A's detail-derived actions
    // (or its resend cooldown) during the gap while B's detail is still in flight.
    setDetailLoaded(false);
    resetResendState();
    setSelectedAccount(acc);
    setIsViewDrawerOpen(true);
    if (!isServerTab) return;
    const details = await getAccountDetails(acc.userId ?? acc.id);
    // Detail failed → detailLoaded stays false, so no detail-gated action is offered. The row's own
    // status is deliberately NOT used as a stand-in.
    if (!details) return;
    setSelectedAccount((prev: any) => ({
      ...prev,
      name: details.fullName,
      email: details.email,
      phone: details.phone,
      gender: genderLabel(details.gender),
      role: details.roleCode,
      roleName: details.roleName,
      subRole: details.displayPosition ?? details.subRole,
      // Raw sub_role (never the localized displayPosition) so identity-edit eligibility is exact.
      rawSubRole: details.subRole,
      campusId: details.campusId ?? prev?.campusId ?? null,
      campus: details.campusName || prev?.campus || '',
      departmentId: details.departmentId ?? null,
      department: details.departmentName,
      // UC-98 detail is the source of truth for MSSV (spec §6); fall back to the list value.
      studentId: details.studentCode ?? prev?.studentId ?? null,
      rawStatus: details.status,
      lastLoginAt: details.lastLoginAt,
      // HO_BASIC_INFO — detail is authoritative for the edit-basic-info permission.
      canEditBasicInfo: details.canEditBasicInfo ?? prev?.canEditBasicInfo ?? false,
      // Pending-account actions. Taken from the detail response and NOT carried over from `prev`: an
      // absent flag means "not permitted", and inheriting the previous account's answer would offer a
      // button on the strength of a different account's permissions.
      canResendEmailConfirmation: details.canResendEmailConfirmation === true,
      canEditPendingEmail: details.canEditPendingEmail === true,
    }));
    setDetailLoaded(true);
  };

  // ── Resend email confirmation ────────────────────────────────────────────────
  // Gate: the backend's own permission answer, and ONLY the status the detail endpoint returned. The
  // list row carries a display-mapped status and may be stale (the holder could have confirmed in
  // another tab), so it is never consulted here — while the detail is loading or failed, the button
  // simply stays away. The action is not HO's alone: a Staff Leader runs it for the pending accounts
  // of their own campus, which is why the permission comes from the query rather than a role check
  // re-derived here.
  const canResendConfirmation = canResendEmailConfirmation({
    detailLoaded,
    detailStatus: selectedAccount?.rawStatus,
    canResend: selectedAccount?.canResendEmailConfirmation,
  });

  // A pending account holds its seat but no authority yet — it has not proven it owns the address.
  // Actions that treat it as an operating account (replacing the Staff Leader) do not apply until
  // it confirms; the way forward from here is the resend above. Read from rawStatus, which carries
  // the raw DB status from the list projection as well as the detail, so the action is withheld
  // immediately rather than flickering while the detail loads.
  const isPendingEmailConfirmation = isPendingEmailConfirmationStatus(selectedAccount?.rawStatus);

  /** Re-reads the detail so a stale-status refusal corrects the modal instead of arguing with it. */
  const refreshDetailStatus = async () => {
    const id = selectedAccount?.userId ?? selectedAccount?.id;
    if (!id) return;
    const details = await getAccountDetails(id);
    if (!details) return;
    setSelectedAccount((prev: any) => ({ ...prev, rawStatus: details.status, email: details.email }));
  };

  const handleResendError = (error: unknown) => {
    const code = (error as AxiosError<{ errorCode?: string }>)?.response?.data?.errorCode;
    const status = (error as AxiosError)?.response?.status;

    // The cap is a property of the account, not of this click: keep the button disabled for the
    // rest of this modal session rather than inviting a retry that cannot succeed.
    if (code === 'RESEND_LIMIT_REACHED') setResendLimitReached(true);

    if (code === 'ACCOUNT_NOT_PENDING') {
      setResendError(ACCOUNT_ERROR_MESSAGES.ACCOUNT_NOT_PENDING);
      pushToast('warning', ACCOUNT_ERROR_MESSAGES.ACCOUNT_NOT_PENDING);
      setIsResendConfirmOpen(false);
      // Whoever confirmed it was right and we were stale — adopt their truth, which also retires
      // the button because canResendConfirmation stops matching.
      void refreshDetailStatus();
      return;
    }

    if (status === 403 && !code) {
      setResendError('Bạn không có quyền gửi lại email xác nhận cho tài khoản này.');
      return;
    }

    if (status === 404 && !code) {
      setResendError('Tài khoản không tồn tại hoặc bạn không còn quyền truy cập.');
      setIsResendConfirmOpen(false);
      refetchAccounts();
      return;
    }

    setResendError(getAccountErrorMessage(
      error, 'Không thể gửi lại email xác nhận. Vui lòng thử lại sau.'));
  };

  const confirmResendEmail = async () => {
    if (!selectedAccount || resendSubmitting) return;   // double-click guard (UX only — the backend
                                                        // cooldown is the real defence)
    const userId = selectedAccount.userId ?? selectedAccount.id;
    if (!userId) {
      setResendError('Không xác định được tài khoản cần gửi lại email xác nhận.');
      return;
    }

    setResendSubmitting(true);
    setResendError(null);
    try {
      const result = await accountManagementApi.resendEmailConfirmation({ userId });
      const deliveryStatus = String(result.emailNotificationStatus ?? '').trim().toUpperCase();

      setLastDeliveryStatus(deliveryStatus);
      setLastResendCount(result.resendCount);
      setIsResendConfirmOpen(false);

      // Report what actually happened to the message. `success: true` only means a new token was
      // issued — claiming "đã gửi" on a SKIPPED/FAILED delivery would send HO away believing the
      // holder has a link they never received. The account stays pending in every branch.
      const feedback = resendDeliveryFeedback(deliveryStatus, selectedAccount.email);
      pushToast(feedback.kind, feedback.message);
    } catch (error) {
      handleResendError(error);
    } finally {
      setResendSubmitting(false);
    }
  };

  // Escape closes the resend confirmation — but never mid-flight, where it would hide a request
  // that is still going to land and leave HO unsure whether a mail went out.
  useEffect(() => {
    if (!isResendConfirmOpen) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !resendSubmitting) setIsResendConfirmOpen(false);
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [isResendConfirmOpen, resendSubmitting]);

  // UC-96 — prepare the create-confirmation step (spec §16.1 "handleContinueCreateAccount").
  // Runs the SAME validation as before, normalizes the data, builds the payload snapshot + its
  // display summary, then opens the confirmation screen. It does NOT call the API (spec §4/§5.4).
  // HO → HO/Staff Leader; Staff Leader → STAFF/STAFF, DEPARTMENT/LEADER (optional department) or
  // STUDENT. The backend derives sub-role and forces campus; we only collect what the role needs.
  const handleContinueCreateAccount = () => {
    setCreateError(null);
    setCreateStudentCodeError(null);
    setCreateFieldErrors({});
    const role = manualForm.role;
    if (!role) { setCreateError('Vui lòng chọn vai trò.'); return; }
    if (isHO && !createCampus) { setCreateError('Vui lòng chọn cơ sở.'); return; }
    if (isRealAdmin && ['HO', 'STUDENT'].includes(role) && !createCampus) {
      setCreateError('Vui lòng chọn cơ sở cho vai trò này.');
      return;
    }

    // Identity: shared rules, normalized values, errors pinned to their own field. The backend
    // re-validates everything (a direct API call is rejected the same way).
    const fullName = normalizeFullName(manualForm.name);
    const email = normalizeAccountEmail(manualForm.email);
    const identityErrors: AccountIdentityFieldErrors = {};
    const fullNameError = validateFullName(fullName);
    if (fullNameError) identityErrors.fullName = fullNameError;
    const emailError = validateAccountEmail(email);
    if (emailError) identityErrors.email = emailError;
    if (identityErrors.fullName || identityErrors.email) {
      setCreateFieldErrors(identityErrors);
      return;
    }

    // PHẦN B — MSSV bắt buộc/hợp lệ khi tạo STUDENT (validate again here, not only via disabled btn).
    const studentCode = role === 'STUDENT' ? manualForm.studentCode.trim() : '';
    if (role === 'STUDENT') {
      if (!studentCode) { setCreateStudentCodeError('Vui lòng nhập mã số sinh viên.'); return; }
      if (studentCode.length > 30) { setCreateStudentCodeError('Mã số sinh viên không được vượt quá 30 ký tự.'); return; }
    }

    const primaryCampusId = (isHO || isRealAdmin) && createCampus
      ? campusOptions.find((c) => c.campusName.includes(createCampus))?.campusId
      : undefined;
    if ((isHO || (isRealAdmin && ['HO', 'STUDENT'].includes(role))) && !primaryCampusId) {
      setCreateError('Cơ sở được chọn không hợp lệ.');
      return;
    }

    // UC-96: HO creating a Staff Leader — don't submit if the pre-check says the campus already
    // has a leader / is in a blocking state. The backend re-checks regardless (BR-SL-22).
    if (isHO && role === 'STAFF') {
      if (slAvailabilityLoading) { setCreateError('Đang kiểm tra cơ sở, vui lòng đợi giây lát.'); return; }
      if (slAvailability && !slAvailability.canCreateStaffLeader) {
        setCreateError(slAvailability.message || 'Không thể tạo Staff Leader cho cơ sở này.');
        return;
      }
    }

    // UC-96: HO creating a new HO — don't submit if the pre-check says the campus already has an
    // HO (any status) or has inconsistent data. The backend re-checks regardless (spec §10).
    if (isHO && role === 'HO') {
      if (hoCampusCheckLoading) { setCreateError('Đang kiểm tra cơ sở, vui lòng đợi giây lát.'); return; }
      if (hoCampusCheck && !hoCampusCheck.canCreateHo) {
        setCreateError(hoCampusCheck.message || 'Không thể tạo tài khoản HO cho cơ sở này.');
        return;
      }
    }

    // A Department Leader must have a department (enforced by the DB constraint).
    if (isStaffLeader && role === 'DEPARTMENT' && !selectedDept) {
      setCreateError('Vui lòng chọn phòng ban cho vai trò Trưởng phòng ban.');
      return;
    }
    const departmentId = isStaffLeader && role === 'DEPARTMENT' ? selectedDept : null;

    // The EXACT object that will be POSTed on confirm (spec §10.1). Nothing here is recomputed in
    // confirmCreateAccount — the summary below is a projection of THIS payload, so they can't drift.
    const payload: CreateAccountRequest = {
      roleCode: role as CreateAccountRequest['roleCode'],
      fullName,
      email,
      phone: manualForm.phone || null,
      // The create form has no gender field; the column is an ENUM
      // (MALE/FEMALE/OTHER/UNKNOWN), so leave it null rather than sending a label.
      gender: null,
      primaryCampusId: primaryCampusId ?? null,
      departmentId,
      // MSSV only for STUDENT; null otherwise so no hidden code is ever sent (spec §5.6).
      studentCode: role === 'STUDENT' ? studentCode : null,
    };

    // Display projection (spec §8 — show only fields that apply to the role, friendly labels, no
    // raw ids / role codes / empty values). Campus/department come from resolved server data, never
    // an arbitrary client value (spec §8.3).
    const campusDisplayName = createCampus || (isStaffLeader ? (user?.campus || null) : null);
    let departmentDisplayName: string | null = null;
    if (isHO && role === 'STAFF') {
      departmentDisplayName = slAvailability?.icDepartmentName || 'Phòng Hợp tác Quốc tế (IC)';
    } else if (isStaffLeader && role === 'STAFF') {
      departmentDisplayName = 'Phòng Hợp tác Quốc tế (IC)';
    } else if (isStaffLeader && role === 'DEPARTMENT') {
      departmentDisplayName =
        campusDepartments.find((d) => String(d.departmentId) === String(departmentId))?.name || null;
    }

    const summary: PendingCreateSummary = {
      fullName,
      email,
      roleDisplayName: resolveCreateRoleDisplayName(role, { isHO, isStaffLeader }),
      campusDisplayName,
      departmentDisplayName,
      studentCode: role === 'STUDENT' ? studentCode : null,
      phone: manualForm.phone.trim() || null,
    };

    // Fresh snapshot each time (spec §10.3) — open the confirmation, do NOT call the API here.
    setPendingCreatePayload(payload);
    setPendingCreateSummary(summary);
    setIsCreateConfirmOpen(true);
  };

  // UC-96 — actually create the account (spec §16.2 "confirmCreateAccount"). Uses the snapshot only,
  // guards against double-submit, and keeps the create/email outcomes distinct (spec §13/§14).
  const confirmCreateAccount = async () => {
    if (!pendingCreatePayload) return;   // no snapshot → nothing to submit
    if (creating) return;                 // double-submit / double-click guard (spec §12.1)
    setCreating(true);
    setCreateError(null);
    try {
      const result = await accountManagementApi.createAccount(pendingCreatePayload);

      // P0 #1: the account is created PENDING email confirmation — never shown as "activated". The
      // email-notification result is reported truthfully (SENT / SKIPPED-in-dev / FAILED), and a non-SENT
      // outcome is a warning, NOT a create failure — the account exists and can be resent.
      if (result.emailNotificationStatus === 'SENT')
        pushToast('success', `Đã tạo tài khoản ${result.email}. Tài khoản đang chờ xác nhận email — đã gửi liên kết xác nhận tới địa chỉ này.`);
      else if (result.emailNotificationStatus === 'SKIPPED')
        pushToast('warning', `Đã tạo tài khoản ${result.email} và đang chờ xác nhận email. Email chưa được gửi trong môi trường hiện tại (SMTP tắt).`);
      else
        pushToast('warning', `Đã tạo tài khoản ${result.email} và đang chờ xác nhận email, nhưng hệ thống chưa gửi được email xác nhận. Vui lòng gửi lại email xác nhận.`);

      // Success: close both screens, reset the form + snapshot, refetch list + stats (spec §13.1/§15.3).
      setIsCreateConfirmOpen(false);
      setPendingCreatePayload(null);
      setPendingCreateSummary(null);
      setIsCreateModalOpen(false);
      setCreateError(null);
      setCreateStudentCodeError(null);
      setCreateFieldErrors({});
      setManualForm({ role: '', name: '', email: '', phone: '', gender: 'Nam', studentCode: '' });
      setCreateCampus('');
      setSelectedDept('');
      refetchAccounts();
      loadStatistics();
    } catch (err) {
      // Backend rejected the create (spec §13.3): close the confirmation, KEEP the form + its data,
      // map the error to its field, drop the failed snapshot. Never auto-retry / auto-create twice.
      const msg = getAccountErrorMessage(err, 'Không thể tạo tài khoản. Vui lòng thử lại.');
      const role = pendingCreatePayload.roleCode;
      if (role === 'STUDENT' && /mã số sinh viên/i.test(msg)) {
        setCreateStudentCodeError(msg);
      } else if (/email/i.test(msg)) {
        setCreateFieldErrors((prev) => ({ ...prev, email: msg }));
      } else {
        setCreateError(msg);
      }
      pushToast('error', msg);
      setIsCreateConfirmOpen(false);   // back to the still-populated form
      setPendingCreatePayload(null);   // discard the failed snapshot (spec §13.3/§15.4)
      setPendingCreateSummary(null);
    } finally {
      setCreating(false);
    }
  };

  // "Quay lại chỉnh sửa" (spec §11): close the confirmation, drop the stale snapshot, keep the form
  // exactly as the user left it. Blocked while a request is in flight (spec §12.1). The next
  // "Tiếp tục" rebuilds a fresh snapshot from the latest form data (spec §10.3).
  const backToEditFromConfirm = () => {
    if (creating) return;
    setIsCreateConfirmOpen(false);
    setPendingCreatePayload(null);
    setPendingCreateSummary(null);
  };

  // A11y (spec §18): move focus into the confirmation when it opens; Escape closes it, but only when
  // no create request is in flight (never while isCreating).
  useEffect(() => {
    if (!isCreateConfirmOpen) return;
    confirmCreateBtnRef.current?.focus();
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !creating) {
        setIsCreateConfirmOpen(false);
        setPendingCreatePayload(null);
        setPendingCreateSummary(null);
      }
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [isCreateConfirmOpen, creating]);

  // Red border for an invalid identity input in the create modal (spec §7.4).
  const createInputClass = (hasError: boolean) =>
    `w-full px-4 py-2.5 rounded-xl border outline-none transition-shadow text-sm bg-white disabled:bg-slate-100 disabled:text-slate-500 disabled:cursor-not-allowed ${
      hasError
        ? 'border-red-400 focus:border-red-500 focus:ring-1 focus:ring-red-400'
        : 'border-gray-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91]'
    }`;

  // "Xác nhận tạo" stays disabled until họ tên + email pass the shared rules (spec §6.1.5).
  const createIdentityInvalid =
    validateFullName(manualForm.name) !== null || validateAccountEmail(manualForm.email) !== null;

  // ── UC-100-SL role editor: derived flags (spec §3.6 / §8.1). ──
  // Whether the selected role actually differs from the snapshot (incl. its dependent field), so
  // the Update button can stay disabled on a no-op (avoids needless session revoke + email).
  // Identity edit eligibility for the currently-selected target (spec §4.2.1 — from ORIGINAL role).
  // Staff Leader: STAFF/STAFF, DEPARTMENT/LEADER, STUDENT. HO (HO_BASIC_INFO): any HO / Staff Leader
  // the backend flagged with canEditBasicInfo (never self / LOCKED / out of scope).
  const canEditIdentity = !!selectedAccount && (
    computeCanEditIdentity(isStaffLeader, selectedAccount.role, selectedAccount.rawSubRole)
    || (isHO && selectedAccount.canEditBasicInfo === true)
  );

  const roleIsDirty = !!(roleEditForm && selectedAccount) && (
    roleEditForm.roleCode !== selectedAccount.role ||
    (roleEditForm.roleCode === 'DEPARTMENT'
      && String(roleEditForm.departmentId || '') !== String(selectedAccount.departmentId ?? '')) ||
    (roleEditForm.roleCode === 'STUDENT'
      && roleEditForm.studentCode.trim() !== (selectedAccount.studentId ?? '')) ||
    // Identity changes count only for editable targets, and only after normalization — a pure
    // whitespace/casing edit is a no-op and must not trigger a session revoke (spec §6.2.3).
    (canEditIdentity && normalizeFullName(roleEditForm.fullName) !== normalizeFullName(selectedAccount.name)) ||
    (canEditIdentity && normalizeAccountEmail(roleEditForm.email) !== normalizeAccountEmail(selectedAccount.email))
  );

  // The target heads a department and this change takes them out of that seat, so the request has
  // to carry a successor. Staying DEPARTMENT/LEADER of the SAME department is not a handover.
  const headedDepartment = roleOptions?.headedDepartment ?? null;
  const needsHeadReplacement = !!(roleEditForm && headedDepartment) && !(
    roleEditForm.roleCode === 'DEPARTMENT'
    && String(roleEditForm.departmentId || '') === String(headedDepartment.departmentId)
  );

  // Staff-Leader submit gate: options must be loaded/valid and the role's required field present.
  const roleUpdateBlocked = !!roleEditForm && isStaffLeader && (
    roleOptionsLoading ||
    !!roleOptionsError ||
    (roleEditForm.roleCode === 'STAFF' && !roleOptions?.icDepartment) ||
    (roleEditForm.roleCode === 'DEPARTMENT' && !roleEditForm.departmentId) ||
    (roleEditForm.roleCode === 'STUDENT' && roleEditForm.studentCode.trim().length === 0) ||
    (needsHeadReplacement && !roleEditForm.replacementHeadUserId)
  );

  // Identity submit gate — the same shared rules as the create modal, so both flows accept and
  // reject exactly the same values (spec §6.2.1/§6.2.2). Applies to every editable-identity target.
  const identityInvalid = canEditIdentity && !!roleEditForm && (
    validateFullName(roleEditForm.fullName) !== null ||
    validateAccountEmail(roleEditForm.email) !== null
  );

  // Which endpoint this submit belongs to. A pending account that is getting a NEW address must go
  // through edit-pending-email: the generic basic-info call mails a change notice with no activation
  // link, which would leave an account that has never confirmed with no way to ever log in.
  // Status is read from rawStatus (the UC-98 detail), never from the display-mapped list row.
  const usePendingEmailEdit = !!(selectedAccount && roleEditForm) && shouldUsePendingEmailEdit({
    rawStatus: selectedAccount.rawStatus,
    oldEmail: selectedAccount.email,
    newEmail: roleEditForm.email,
  });

  // Switch the target role and reset the dependent fields (spec §3.3/§3.4/§3.5 — always fresh on a
  // genuine role change; the original values are only preserved by handleEditClick on first open).
  const changeRoleCode = (nextRole: string) => {
    setRoleError(null);
    // Reset the role-dependent fields, but keep the identity fields — their editability is fixed by
    // the ORIGINAL target role and does not change with the dropdown (spec §4.2.1 / §4.7).
    // The successor is kept too: it belongs to the department the target currently heads, which the
    // role dropdown does not change. Switching back to that same department simply stops sending it.
    setRoleEditForm((prev) => (prev
      ? {
        roleCode: nextRole,
        departmentId: '',
        studentCode: '',
        fullName: prev.fullName,
        email: prev.email,
        replacementHeadUserId: prev.replacementHeadUserId,
      }
      : prev));
  };

  // UC-100 — Staff Leader/ADMIN updates another account's role. For a Staff Leader the payload is
  // role-shaped (department for DEPARTMENT, MSSV for STUDENT); the backend re-validates and derives
  // campus/sub-role, revokes the target's sessions and emails them.
  // HO_BASIC_INFO — HO edits only full name + email. Validates, then either submits directly (email
  // unchanged) or opens the email-change confirmation (spec §10). The actual call is submitBasicInfo.
  const handleUpdateBasicInfo = () => {
    if (!selectedAccount || !roleEditForm) return;
    setRoleError(null);
    setEditFieldErrors({});

    // Same shared rules as create; each violation lands under its own field, not in the alert.
    const fullName = normalizeFullName(roleEditForm.fullName);
    const email = normalizeAccountEmail(roleEditForm.email);
    const identityErrors: AccountIdentityFieldErrors = {};
    const fullNameError = validateFullName(fullName);
    if (fullNameError) identityErrors.fullName = fullNameError;
    const emailError = validateAccountEmail(email);
    if (emailError) identityErrors.email = emailError;
    if (identityErrors.fullName || identityErrors.email) {
      setEditFieldErrors(identityErrors);
      return;
    }

    const oldEmail = String(selectedAccount.email ?? '');
    const emailChanged = email !== normalizeAccountEmail(oldEmail);

    // A pending account's address change is a different operation, not a variant of this one: it has
    // to re-issue the activation link, so it gets its own endpoint and its own confirmation copy.
    if (usePendingEmailEdit) {
      setPendingEmailEditConfirm({ oldEmail, newEmail: email });
      return;
    }

    if (emailChanged) {
      // Confirm before an email change (session revoke + SSO/FEID re-link).
      setBasicInfoEmailConfirm({ oldEmail, newEmail: email });
      return;
    }
    void submitBasicInfo();
  };

  const submitBasicInfo = async () => {
    if (!selectedAccount || !roleEditForm) return;
    setRoleSaving(true);
    setRoleError(null);
    try {
      const res = await accountManagementApi.updateBasicAccountInfo({
        userId: (selectedAccount.userId ?? selectedAccount.id) as any,
        fullName: normalizeFullName(roleEditForm.fullName),
        email: normalizeAccountEmail(roleEditForm.email),
      });
      setBasicInfoEmailConfirm(null);
      const emailNote = res.emailChanged
        ? (res.emailNotificationStatus === 'SENT' ? ' Đã gửi email thông báo.'
          : res.emailNotificationStatus === 'PARTIAL' ? ' Một số email thông báo chưa gửi được.'
          : res.emailNotificationStatus === 'FAILED' ? ' Không gửi được email thông báo.' : '')
        : '';
      pushToast('success', `Cập nhật thông tin tài khoản thành công.${emailNote}`);
      closeViewDrawer();
      refetchAccounts();
      loadStatistics();
    } catch (err) {
      const msg = getAccountErrorMessage(err, 'Không thể cập nhật thông tin tài khoản. Vui lòng thử lại.');
      // Backend identity rejections (duplicate/domain/format) belong under the email input.
      if (/email/i.test(msg)) setEditFieldErrors((prev) => ({ ...prev, email: msg }));
      else setRoleError(msg);
      pushToast('error', msg);
    } finally {
      setRoleSaving(false);
    }
  };

  /**
   * Corrects a pending account's address through the dedicated endpoint. Name and email travel in ONE
   * request so they cannot half-apply, and the toast reports the delivery outcome rather than the
   * request outcome — the address is saved in every branch, but only SENT means a link went out.
   */
  const submitPendingEmailEdit = async () => {
    if (!selectedAccount || !roleEditForm || roleSaving) return;   // double-click guard
    const newEmail = normalizeAccountEmail(roleEditForm.email);
    setRoleSaving(true);
    setRoleError(null);
    try {
      const res = await accountManagementApi.editPendingAccountEmail({
        userId: (selectedAccount.userId ?? selectedAccount.id) as any,
        newEmail,
        fullName: normalizeFullName(roleEditForm.fullName),
      });
      setPendingEmailEditConfirm(null);
      const feedback = pendingEmailEditFeedback(res.emailNotificationStatus, res.email || newEmail);
      pushToast(feedback.kind, feedback.message);
      closeViewDrawer();
      refetchAccounts();
      loadStatistics();
    } catch (err) {
      handlePendingEmailEditError(err);
    } finally {
      setRoleSaving(false);
    }
  };

  const handlePendingEmailEditError = (error: unknown) => {
    const code = (error as AxiosError<{ errorCode?: string }>)?.response?.data?.errorCode;
    const status = (error as AxiosError)?.response?.status;

    // Somebody confirmed (or cancelled) the account while this modal was open. Their truth wins:
    // adopt it, which also flips the submit back to the ordinary basic-info flow.
    if (code === 'ACCOUNT_NOT_PENDING') {
      const msg = ACCOUNT_ERROR_MESSAGES.ACCOUNT_NOT_PENDING;
      setPendingEmailEditConfirm(null);
      setRoleError(msg);
      pushToast('warning', msg);
      void refreshDetailStatus();
      refetchAccounts();
      return;
    }

    // Rejections about the address itself belong under the email input, where the value that caused
    // them still is.
    if (code === 'EMAIL_UNCHANGED' || code === 'EMAIL_ALREADY_EXISTS') {
      const msg = getAccountErrorMessage(error);
      setPendingEmailEditConfirm(null);
      setEditFieldErrors((prev) => ({ ...prev, email: msg }));
      pushToast('error', msg);
      return;
    }

    if (status === 403 && !code) {
      const msg = 'Bạn không có quyền chỉnh sửa email của tài khoản này.';
      setPendingEmailEditConfirm(null);
      setRoleError(msg);
      pushToast('error', msg);
      return;
    }

    if (status === 404 && !code) {
      const msg = 'Tài khoản không tồn tại hoặc không còn quyền truy cập.';
      setPendingEmailEditConfirm(null);
      setRoleError(msg);
      pushToast('error', msg);
      refetchAccounts();
      return;
    }

    const msg = getAccountErrorMessage(
      error, 'Không thể cập nhật email tài khoản. Vui lòng thử lại sau.');
    // A validation message from the shared identity rules is about the address too.
    if (/email/i.test(msg)) setEditFieldErrors((prev) => ({ ...prev, email: msg }));
    else setRoleError(msg);
    pushToast('error', msg);
  };

  const handleUpdateRole = async () => {
    if (!selectedAccount || !roleEditForm) return;
    // HO uses the dedicated basic-info endpoint (never role/campus/department).
    if (isHO) { handleUpdateBasicInfo(); return; }
    setRoleError(null);
    const { roleCode, departmentId, studentCode } = roleEditForm;

    // Identity validation (only for editable targets; backend re-checks).
    if (canEditIdentity) {
      const fullName = roleEditForm.fullName.trim();
      const email = roleEditForm.email.trim();
      if (!fullName) { setRoleError('Vui lòng nhập họ và tên.'); return; }
      if (fullName.length > 150) { setRoleError('Họ và tên không được vượt quá 150 ký tự.'); return; }
      if (!email) { setRoleError('Vui lòng nhập địa chỉ email.'); return; }
      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) { setRoleError('Địa chỉ email không hợp lệ.'); return; }
      if (email.length > 150) { setRoleError('Email không được vượt quá 150 ký tự.'); return; }
    }

    // Frontend validation (backend remains the source of truth).
    if (isStaffLeader) {
      if (roleCode === 'STAFF' && !roleOptions?.icDepartment) {
        setRoleError('Không tìm thấy Phòng Hợp tác Quốc tế đang hoạt động cho cơ sở của bạn.');
        return;
      }
      if (roleCode === 'DEPARTMENT' && !departmentId) {
        setRoleError('Vui lòng chọn phòng ban cho vai trò Trưởng phòng ban.');
        return;
      }
      if (roleCode === 'STUDENT') {
        const code = studentCode.trim();
        if (!code) { setRoleError('Vui lòng nhập mã số sinh viên.'); return; }
        if (code.length > 30) { setRoleError('Mã số sinh viên không được vượt quá 30 ký tự.'); return; }
      }
      if (needsHeadReplacement && !roleEditForm.replacementHeadUserId) {
        setRoleError('Vui lòng chọn Trưởng phòng thay thế trước khi thay đổi vai trò.');
        return;
      }
    }

    // Compared after normalization, so a pure casing/whitespace edit is not a change: it must not
    // prompt for a confirmation, revoke sessions or invalidate an activation link.
    const outgoingEmail = canEditIdentity ? normalizeAccountEmail(roleEditForm.email) : null;
    const emailIsChanging = outgoingEmail !== null
      && outgoingEmail.length > 0
      && outgoingEmail !== normalizeAccountEmail(selectedAccount.email ?? '');

    // Changing the login email is not a field edit like the others: it re-points the account's
    // authentication and, for an account still awaiting activation, invalidates the link already
    // mailed and issues a new one. Both deserve a look before they happen — so the submit stops here
    // and the confirmation dialog does the calling. Which dialog depends on whether the account has
    // ever proven an address; the wording for the two situations is not interchangeable.
    if (emailIsChanging) {
      const confirmPayload = { oldEmail: String(selectedAccount.email ?? ''), newEmail: outgoingEmail! };
      if (isPendingEmailConfirmation) setPendingEmailEditConfirm(confirmPayload);
      else setBasicInfoEmailConfirm(confirmPayload);
      return;
    }

    void submitRoleUpdate();
  };

  /**
   * The single request that carries a Staff Leader's whole edit — role, department, MSSV, name and
   * email together.
   *
   * One call on purpose. Splitting it into "change the role, then change the email" is what produces
   * the half-applied states this flow must not have: a role that moved while the only live activation
   * link still points at the old address, or an address that moved while the role did not. The backend
   * commits all of it in one transaction, and reports separately what became of the emails — which is
   * what the toast below is reading, rather than assuming a mail went out because the request
   * succeeded.
   */
  const submitRoleUpdate = async () => {
    if (!selectedAccount || !roleEditForm || roleSaving) return;   // double-click guard
    const { roleCode, departmentId, studentCode } = roleEditForm;

    // ADMIN keeps the legacy behaviour: role-only change, original department preserved.
    const outgoingDepartmentId = roleCode === 'DEPARTMENT'
      ? (isStaffLeader ? departmentId : (selectedAccount.departmentId ?? null))
      : null;
    const outgoingStudentCode = roleCode === 'STUDENT'
      ? (isStaffLeader ? studentCode.trim() : null)
      : null;
    const outgoingEmail = canEditIdentity ? normalizeAccountEmail(roleEditForm.email) : null;

    setRoleSaving(true);
    try {
      const res = await accountManagementApi.updateAccountRole({
        userId: (selectedAccount.userId ?? selectedAccount.id) as any,
        newRoleCode: roleCode as any,
        departmentId: outgoingDepartmentId as any,
        studentCode: outgoingStudentCode,
        // Identity is only sent for editable targets; otherwise leave null so the backend keeps it.
        fullName: canEditIdentity ? normalizeFullName(roleEditForm.fullName) : null,
        email: outgoingEmail,
        // Sent only when this change actually vacates a department head seat — the backend rejects
        // a successor it has no use for rather than ignoring it.
        replacementDepartmentHeadUserId: needsHeadReplacement
          ? roleEditForm.replacementHeadUserId
          : null,
      });
      setPendingEmailEditConfirm(null);
      setBasicInfoEmailConfirm(null);

      // Report what happened to the mail, not that the request returned 200. Up to two messages can be
      // due — the activation link and the role-changed notice — and the response reports each on its
      // own, so the whole object goes to the adapter rather than a status picked out of it here. A
      // pending account whose activation link failed to send is still unusable, and saying "đã gửi"
      // would hide the one thing the operator needs to act on.
      const feedback = accountRoleUpdateFeedback(res, outgoingEmail);
      pushToast(feedback.kind, feedback.message);

      closeViewDrawer();
      refetchAccounts();
      loadStatistics();
    } catch (err) {
      // The drawer deliberately stays open with the user's choices intact: the role change was
      // refused, not lost, and re-entering everything after handing over a delegation would be
      // punishing. Nothing here touches selectedAccount or refetches (spec §16.4).
      const msg = getAccountErrorMessage(err, 'Không thể cập nhật vai trò. Vui lòng kiểm tra lại dữ liệu và thử lại.');
      const code = (err as AxiosError<{ errorCode?: string }>)?.response?.data?.errorCode;

      // Rejections about the address belong under the email input, where the value that caused them
      // still is — and the confirmation dialog steps aside so that field is reachable again.
      if (code === 'EMAIL_ALREADY_EXISTS' || code === 'EMAIL_UNCHANGED') {
        setPendingEmailEditConfirm(null);
        setBasicInfoEmailConfirm(null);
        setEditFieldErrors((prev) => ({ ...prev, email: msg }));
        pushToast('error', msg);
        return;
      }

      // Somebody confirmed (or cancelled) the account while this modal was open. Their truth wins:
      // adopt it, which also switches the next submit onto the right branch.
      if (code === 'ACCOUNT_NOT_PENDING') {
        setPendingEmailEditConfirm(null);
        setRoleError(ACCOUNT_ERROR_MESSAGES.ACCOUNT_NOT_PENDING);
        pushToast('warning', ACCOUNT_ERROR_MESSAGES.ACCOUNT_NOT_PENDING);
        void refreshDetailStatus();
        refetchAccounts();
        return;
      }

      setRoleError(msg);
      // The blocker breakdown can run to several lines; it belongs in the drawer, next to the
      // fields it is about. The toast only says that something blocked and where to look.
      const blocked = getAccountRoleChangeBlockers(err);
      pushToast('error', blocked
        ? 'Không thể đổi vai trò — tài khoản còn trách nhiệm đang hoạt động. Xem chi tiết trong biểu mẫu.'
        : msg);
    } finally {
      setRoleSaving(false);
    }
  };

  return (
    <div className="w-full pb-12 animate-in fade-in duration-300">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm font-medium text-gray-500 mb-2">
        <span>Dashboard</span>
        <span>/</span>
        <span className="text-[#004c91]">Quản lý tài khoản</span>
      </div>

      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-3xl font-bold text-[#004c91]">Quản lý tài khoản</h1>
          {isStaffLeader && (
            <p className="mt-1 max-w-3xl text-sm text-gray-500" aria-live="polite">
              {accountManagementSubtitle}
            </p>
          )}
        </div>
      </div>

      {/* I. Top Widgets & Create Account Card. With no stat cards (HO) the button stands alone,
          right-aligned, instead of being squeezed into one column of an otherwise empty grid.
          Hidden entirely in Visitor mode: the counters are internal-account totals (showing them
          over a Visitor list would read as Visitor figures), and the tab is read-only, so there is
          no account to create from it. */}
      {!isVisitorMode && (
      <div className={stats.length === 0
        ? 'flex justify-end mb-8'
        : `grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 ${stats.length === 4 ? 'lg:grid-cols-5' : 'lg:grid-cols-6'} gap-4 mb-8 items-stretch`}>
        {stats.map((stat: any, idx) => {
          const Icon = stat.icon;

          return (
            <button 
              key={idx} 
              onClick={stat.onClick} 
              className={`rounded-2xl p-4 sm:p-4.5 border ${stat.bg} shadow-sm flex flex-col justify-between relative overflow-hidden group hover:shadow-md transition-all duration-200 text-left w-full focus:ring-2 focus:ring-[#004c91]/20 outline-none`}
            >
              {/* Dòng 1: Icon + Số liệu */}
              <div className="flex items-center gap-3 mb-2">
                <div className={`w-9 h-9 rounded-xl ${stat.iconBg} flex items-center justify-center shrink-0 group-hover:scale-105 transition-transform`}>
                  <Icon className={`w-5 h-5 ${stat.color}`} />
                </div>
                <h3 className={`text-2xl sm:text-2xl font-black ${stat.textColor || 'text-gray-900'} tracking-tight leading-none`}>
                  {stat.value}
                </h3>
              </div>

              {/* Dòng 2: Chú thích */}
              <div>
                <p className={`text-[10px] sm:text-[11px] font-bold ${stat.labelColor || 'text-gray-500'} uppercase tracking-wider leading-tight truncate`}>
                  {stat.label}
                </p>
              </div>
            </button>
          );
        })}

        {/* Card 5: Tạo tài khoản mới. Standing alone (HO) it is a normal action button, not a card
            the size of a stat tile — so it drops to a compact padding/icon/text scale. */}
        <button
          onClick={() => {
            setCreateError(null);
            setCreateStudentCodeError(null);
            setSelectedDept('');
            setManualForm({ role: '', name: '', email: '', phone: '', gender: 'Nam', studentCode: '' });
            setIsCreateModalOpen(true);
          }}
          className={`bg-[#f37021] hover:bg-[#e85c0d] text-white rounded-2xl border border-transparent shadow-sm shadow-orange-500/20 flex items-center justify-center gap-2.5 transition-all hover:shadow-md hover:shadow-orange-500/40 cursor-pointer outline-none focus:ring-2 focus:ring-orange-400 group ${stats.length === 0 ? 'w-full sm:w-auto px-4 py-2.5 gap-2' : 'w-full p-4 sm:p-4.5'}`}
        >
          <div className={`rounded-lg bg-white/20 flex items-center justify-center shrink-0 group-hover:scale-110 transition-transform ${stats.length === 0 ? 'w-6 h-6' : 'w-9 h-9 rounded-xl'}`}>
            <Plus className={`text-white stroke-[2.5] ${stats.length === 0 ? 'w-4 h-4' : 'w-5 h-5'}`} />
          </div>
          <h3 className={`font-bold text-white tracking-tight leading-none whitespace-nowrap ${stats.length === 0 ? 'text-sm' : 'text-base sm:text-lg'}`}>
            Tạo tài khoản mới
          </h3>
        </button>
      </div>
      )}

      {isServerTab && !isVisitorMode && accountsError && (
        <div className="mb-6 rounded-2xl border border-red-200 bg-red-50 px-5 py-4 text-sm font-bold text-red-700 flex items-center gap-3">
          <XCircle className="w-5 h-5 shrink-0" />
          <span>{accountsError}</span>
        </div>
      )}

      {/* "Related Visitor Accounts" tab is scoped server-side to a Staff Leader's own campus
          (UC_StaffLeader_Related_Visitor_Accounts_Tab) — it 403s for any other role. ADMIN (and
          anyone else who can reach the VISITOR account-type filter) must keep using the normal,
          unscoped account list below, which already supports roleCode=VISITOR system-wide. */}
      {isVisitorMode ? (
        <RelatedVisitorsTab
          accountTypeFilter="VISITOR"
          onAccountTypeChange={(val) => setAccountTypeFilter(val)}
        />
      ) : (
        <div ref={tableRef} className="bg-white rounded-[2rem] shadow-[0_8px_30px_-4px_rgba(0,0,0,0.05)] border border-[#004c91] overflow-hidden">
        {/* Tab Filters — tab "Chờ duyệt" là mock, ẩn với ADMIN/HO/Staff Leader */}
        {!isHO && !isStaffLeader && !isRealAdmin && (
          <div className="flex px-6 bg-[#004c91]">
            <button 
              onClick={() => setActiveTab('all')}
              className={`px-6 py-4 font-bold text-sm outline-none border-b-2 transition-colors ${activeTab === 'all' ? 'border-white text-white' : 'border-transparent text-blue-200 hover:text-white'}`}
            >
              Tất cả tài khoản
            </button>
            {!isStaffLeader && (
              <button 
                onClick={() => setActiveTab('pending')}
                className={`px-6 py-4 font-bold text-sm outline-none border-b-2 transition-colors flex items-center gap-2 ${activeTab === 'pending' ? 'border-white text-white' : 'border-transparent text-blue-200 hover:text-white'}`}
              >
                Chờ duyệt tác vụ
                <span className="bg-[#fef1e8] text-[#f37021] px-2 py-0.5 rounded-full text-xs font-black">{pendingAccounts.length}</span>
              </button>
            )}
          </div>
        )}

        {/* II. Filter Bar */}
        <div className="p-6 bg-[#004c91] flex flex-wrap items-center gap-4 border-b border-[#00386b]">
          <div className="relative flex-1 min-w-[250px]">
            <Search className="w-5 h-5 absolute left-4 top-1/2 -translate-y-1/2 text-gray-400" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Tìm theo Họ tên, MSSV, hoặc Email..."
              className="w-full pl-11 pr-4 py-3 rounded-2xl border-none focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all text-sm shadow-inner bg-white/10 text-white placeholder:text-blue-200"
            />
          </div>
          
          {(isHO || isRealAdmin) && (
            <div className="relative">
              <select
                value={campusFilter}
                onChange={(e) => setCampusFilter(e.target.value)}
                className="px-4 py-3 pr-10 rounded-2xl border-none text-sm font-medium focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all min-w-[140px] bg-white/10 text-white shadow-inner appearance-none custom-select"
              >
                <option className="text-gray-900" value="">Toàn quốc</option>
                {campusOptions.map(c => <option className="text-gray-900" key={c.campusId} value={c.campusName}>{c.campusName}</option>)}
              </select>
              <ChevronDown className="w-4 h-4 absolute right-4 top-1/2 -translate-y-1/2 text-white pointer-events-none opacity-70" />
            </div>
          )}

          {/* Lọc Loại Tài khoản. Ẩn với HO: HO chỉ làm việc với tài khoản nội bộ, nên bộ lọc luôn
              ở INTERNAL (giá trị mặc định) và không hiển thị.
              Staff Leader chỉ có 2 lựa chọn — "Tất cả tài khoản" bị bỏ vì nội bộ và khách là hai
              nguồn dữ liệu / quyền / bảng khác nhau, không gộp được vào một danh sách phân trang.
              ADMIN giữ nguyên 3 lựa chọn (danh sách không phân phạm vi campus, dùng logic riêng). */}
          {!isHO && (
          <div className="relative">
            <select
              value={accountTypeFilter}
              onChange={(e) => {
                const val = e.target.value;
                setAccountTypeFilter(val);
                // Staff Leader: the Visitor tab has no role filter at all, so nothing is carried
                // over. Only ADMIN's shared list needs roleCode=VISITOR to narrow itself.
                if (val === 'VISITOR' && !isStaffLeader) setRoleFilter('VISITOR');
              }}
              aria-label="Loại tài khoản"
              className="px-4 py-3 pr-10 rounded-2xl border-none text-sm font-medium focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all min-w-[170px] bg-white/10 text-white shadow-inner appearance-none custom-select"
            >
              {!isStaffLeader && <option className="text-gray-900" value="ALL">Tất cả tài khoản</option>}
              <option className="text-gray-900" value="INTERNAL">Tài khoản nội bộ</option>
              <option className="text-gray-900" value="VISITOR">Tài khoản khách</option>
            </select>
            <ChevronDown className="w-4 h-4 absolute right-4 top-1/2 -translate-y-1/2 text-white pointer-events-none opacity-70" />
          </div>
          )}

          <div className="relative">
            <select
              value={roleFilter}
              onChange={(e) => {
                const val = e.target.value;
                setRoleFilter(val);
                if (val === 'VISITOR') {
                  setAccountTypeFilter('VISITOR');
                } else if (val && val !== 'VISITOR' && accountTypeFilter === 'VISITOR') {
                  setAccountTypeFilter('INTERNAL');
                }
              }}
              className="px-4 py-3 pr-10 rounded-2xl border-none text-sm font-medium focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all min-w-[140px] bg-white/10 text-white shadow-inner appearance-none custom-select"
            >
              <option className="text-gray-900" value="">Tất cả Vai trò</option>
              {ROLES.filter(r => {
                if (accountTypeFilter === 'INTERNAL' && r === 'VISITOR') return false;
                if (accountTypeFilter === 'VISITOR' && r !== 'VISITOR') return false;
                if (isRealAdmin) return true; // ADMIN xem mọi role
                if (isHO) return ['HO', 'STAFF'].includes(r);
                if (isStaffLeader) return ['STAFF', 'DEPARTMENT', 'STUDENT', 'VISITOR'].includes(r);
                return r !== 'HO';
              }).map(r => (
                <option className="text-gray-900" key={r} value={r}>
                  {/* STAFF reads differently per viewer: the only STAFF accounts HO manages are the
                      campus IC leaders, while a Staff Leader is filtering their own IC members. */}
                  {r === 'STAFF' ? (isHO ? 'Trưởng phòng IC' : 'Nhân sự phòng IC') : r === 'DEPARTMENT' ? 'Trưởng phòng ban' : r === 'STUDENT' ? 'Sinh viên' : r === 'HO' ? 'Cán bộ HO' : r === 'ADMIN' ? 'Quản trị viên' : r}
                </option>
              ))}
            </select>
            <ChevronDown className="w-4 h-4 absolute right-4 top-1/2 -translate-y-1/2 text-white pointer-events-none opacity-70" />
          </div>

          {activeTab === 'all' && (
            <div className="relative">
              <select
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
                className="px-4 py-3 pr-10 rounded-2xl border-none text-sm font-medium focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all min-w-[140px] bg-white/10 text-white shadow-inner appearance-none custom-select"
              >
                <option className="text-gray-900" value="">Tất cả trạng thái</option>
                <option className="text-gray-900" value="Active">Hoạt động</option>
                <option className="text-gray-900" value="Inactive">Vô hiệu hóa</option>
                <option className="text-gray-900" value="Locked">Bị khóa</option>
                <option className="text-gray-900" value="PendingEmail">Chờ xác nhận email</option>
              </select>
              <ChevronDown className="w-4 h-4 absolute right-4 top-1/2 -translate-y-1/2 text-white pointer-events-none opacity-70" />
            </div>
          )}
          {activeTab === 'pending' && (
            <div className="relative">
              <select
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
                className="px-4 py-3 pr-10 rounded-2xl border-none text-sm font-medium focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all min-w-[140px] bg-white/10 text-white shadow-inner appearance-none custom-select"
              >
                <option className="text-gray-900" value="">Tất cả trạng thái</option>
                <option className="text-gray-900" value="Pending Approved">Chờ duyệt</option>
                <option className="text-gray-900" value="Approved">Đã duyệt</option>
                <option className="text-gray-900" value="Rejected">Từ chối</option>
              </select>
              <ChevronDown className="w-4 h-4 absolute right-4 top-1/2 -translate-y-1/2 text-white pointer-events-none opacity-70" />
            </div>
          )}
        </div>

        {/* III. Bảng danh sách */}
        <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse">
                <thead className="bg-[#f8fafc] text-gray-500 border-b border-gray-200">
                  <tr>
                    <th className="p-5 pl-8 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">STT</th>
                    <th className="p-5 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Họ và Tên</th>
                    <th className="p-5 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Tên đăng nhập (Email)</th>
                    {!isStaff && <th className="p-5 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Cơ sở</th>}
                    <th className="p-5 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Vai trò</th>
                    {!isHO && !isStaffLeader && (
                      <th
                        className={`p-5 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap ${activeTab === 'pending' ? 'cursor-pointer hover:bg-gray-50 transition-colors select-none group' : ''}`}
                        onClick={() => activeTab === 'pending' && setSortOrder(prev => prev === 'asc' ? 'desc' : 'asc')}
                      >
                        <div className="flex items-center justify-center gap-1.5">
                          {activeTab === 'pending' ? 'Thời gian gửi' : 'Tình trạng'}
                          {activeTab === 'pending' && (
                            <div className="flex flex-col text-gray-300 group-hover:text-[#004c91] transition-colors">
                              <ChevronUp className={`w-2.5 h-2.5 -mb-0.5 ${sortOrder === 'asc' ? 'text-[#004c91]' : ''}`} />
                              <ChevronDown className={`w-2.5 h-2.5 -mt-0.5 ${sortOrder === 'desc' ? 'text-[#004c91]' : ''}`} />
                            </div>
                          )}
                        </div>
                      </th>
                    )}
                    <th className="p-5 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Trạng thái</th>
                    <th className="p-5 pr-8 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Hành động</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {paginatedAccounts.length > 0 ? paginatedAccounts.map((acc, idx) => (
                    <tr key={acc.id} className="hover:bg-blue-50/30 transition-colors group">
                      <td className="p-5 pl-8 text-sm font-bold text-[#004c91] text-center">{(currentPage - 1) * pageSize + idx + 1}</td>
                      <td className="p-5 text-center">
                        <div>
                          <p className="text-[13px] font-bold text-[#004c91] leading-tight whitespace-nowrap">{acc.name}</p>
                        </div>
                      </td>
                      <td className="p-5 text-[13px] font-medium text-gray-600 truncate max-w-[200px] text-center">{acc.email}</td>
                      {!isStaff && <td className="p-5 text-[13px] font-bold text-gray-700 text-center">{acc.campus}</td>}
                      <td className="p-5 text-center">
                        <span className={`inline-flex px-3 py-1.5 rounded-lg border shadow-sm font-bold text-[10px] tracking-wider uppercase ${getRoleStyle(acc.role)}`}>
                          {acc.role}
                        </span>
                      </td>
                      {!isHO && !isStaffLeader && (
                        <td className="p-5 text-center">
                          {activeTab === 'pending' ? (
                            <span className="text-[13px] font-bold text-gray-600 whitespace-nowrap tracking-wide">{acc.createdAt?.split('-').reverse().join('-')}</span>
                          ) : (
                            <span className={`text-[11px] font-bold uppercase tracking-wider ${acc.loginStatus === 'Đã đăng nhập' ? 'text-[#0aa14f]' : 'text-gray-400'}`}>
                              {acc.loginStatus}
                            </span>
                          )}
                        </td>
                      )}
                      <td className="p-5 text-center">
                        {acc.status === 'Active' && <span className="inline-flex items-center gap-1.5 text-[#0aa14f] bg-[#eaffe4] px-3 py-1.5 rounded-full text-[11px] font-bold border border-[#0aa14f]/30"><div className="w-1.5 h-1.5 rounded-full bg-[#0aa14f]"></div> Hoạt động</span>}
                        {acc.status === 'Inactive' && <span className="inline-flex items-center gap-1.5 text-amber-600 bg-amber-50 px-3 py-1.5 rounded-full text-[11px] font-bold border border-amber-200"><div className="w-1.5 h-1.5 rounded-full bg-amber-500"></div> Vô hiệu hóa</span>}
                        {acc.status === 'Locked' && <span className="inline-flex items-center gap-1.5 text-red-600 bg-red-50 px-3 py-1.5 rounded-full text-[11px] font-bold border border-red-200"><div className="w-1.5 h-1.5 rounded-full bg-red-600"></div> Bị khóa</span>}
                        {acc.status === 'Pending' && <span className="inline-flex items-center gap-1.5 text-sky-700 bg-sky-50 px-3 py-1.5 rounded-full text-[11px] font-bold border border-sky-200"><div className="w-1.5 h-1.5 rounded-full bg-sky-500"></div> Chờ xác nhận email</span>}
                        {acc.status === 'Deactive' && <span className="inline-flex items-center gap-1.5 text-red-600 bg-red-50 px-3 py-1.5 rounded-full text-[11px] font-bold border border-red-200"><div className="w-1.5 h-1.5 rounded-full bg-red-600"></div> Khóa</span>}
                        {acc.status === 'Pending Approved' && <span className="inline-flex items-center gap-1.5 text-[#f37021] bg-[#fef1e8] px-3 py-1.5 rounded-full text-[11px] font-bold border border-[#f37021]/30"><div className="w-1.5 h-1.5 rounded-full bg-[#f37021]"></div> Chờ duyệt</span>}
                        {acc.status === 'Approved' && <span className="inline-flex items-center gap-1.5 text-[#0aa14f] bg-[#eaffe4] px-3 py-1.5 rounded-full text-[11px] font-bold border border-[#0aa14f]/30"><div className="w-1.5 h-1.5 rounded-full bg-[#0aa14f]"></div> Đã duyệt</span>}
                        {acc.status === 'Rejected' && <span className="inline-flex items-center gap-1.5 text-red-600 bg-red-50 px-3 py-1.5 rounded-full text-[11px] font-bold border border-red-200"><div className="w-1.5 h-1.5 rounded-full bg-red-600"></div> Từ chối</span>}
                      </td>
                      <td className="p-5 pr-8 text-center">
                        <div className="flex items-center justify-center gap-2 transition-opacity">
                          {(isRealAdmin || isStaffLeader) ? (
                            <>
                              <button 
                                onClick={() => openViewDrawer(acc)}
                                className="flex items-center justify-center p-2 text-gray-500 hover:text-[#004c91] hover:bg-blue-50/50 rounded-full transition-all outline-none"
                                title="Xem tài khoản"
                              >
                                <Eye className="w-5 h-5" />
                              </button>
                              {activeTab === 'pending' ? (
                                acc.status === 'Pending Approved' && (
                                  <>
                                    <button 
                                      className="flex items-center justify-center p-2 text-gray-500 hover:text-green-600 hover:bg-green-50 rounded-full transition-all outline-none"
                                      title="Duyệt"
                                    >
                                      <CheckCircle className="w-5 h-5" />
                                    </button>
                                    <button 
                                      className="flex items-center justify-center p-2 text-gray-500 hover:text-red-500 hover:bg-red-50 rounded-full transition-all outline-none"
                                      title="Từ chối"
                                    >
                                      <XCircle className="w-5 h-5" />
                                    </button>
                                  </>
                                )
                              ) : (
                                <>
                                  {((!isServerTab || acc.canManageStatus) && acc.status !== 'Pending') ? (
                                    <label className="relative flex items-center cursor-pointer ml-1" title={acc.status === 'Active' ? 'Vô hiệu hóa' : 'Kích hoạt'}>
                                      <input type="checkbox" className="sr-only peer" checked={acc.status === 'Active'} onChange={() => requestToggleStatus(acc)} />
                                      <div className="w-10 h-5 bg-gray-200 rounded-full peer-checked:bg-[#004c91] transition-colors relative">
                                        <div className={`absolute top-0.5 left-0.5 w-4 h-4 bg-white rounded-full transition-transform ${acc.status === 'Active' ? 'translate-x-5' : 'translate-x-0'} shadow-sm`}></div>
                                      </div>
                                    </label>
                                  ) : !(isRealAdmin && acc.status === 'Locked') && (
                                    <span className="text-gray-300 text-sm">—</span>
                                  )}
                                  {/* LOCK/UNLOCK — flow riêng của ADMIN (khác toggle ACTIVE↔INACTIVE),
                                      không tự khóa chính mình; backend re-check toàn bộ. */}
                                  {isRealAdmin && !acc.isCurrentUser && acc.status === 'Active' && (
                                    <button
                                      onClick={() => { setLockError(null); setLockReason(''); setLockTarget(acc); }}
                                      className="flex items-center justify-center p-2 text-gray-500 hover:text-red-500 hover:bg-red-50 rounded-full transition-all outline-none"
                                      title="Khóa tài khoản (bảo mật)"
                                    >
                                      <Key className="w-4.5 h-4.5" />
                                    </button>
                                  )}
                                  {isRealAdmin && !acc.isCurrentUser && acc.status === 'Locked' && (
                                    <button
                                      onClick={() => { setLockError(null); setLockReason(''); setLockTarget(acc); }}
                                      className="px-2.5 py-1.5 rounded-lg text-xs font-bold text-[#0aa14f] border border-[#0aa14f]/40 hover:bg-[#eaffe4] transition-colors outline-none"
                                      title="Mở khóa tài khoản"
                                    >
                                      Mở khóa
                                    </button>
                                  )}
                                </>
                              )}
                            </>
                          ) : isStaff ? (
                            <>
                              <button 
                                onClick={() => openViewDrawer(acc)}
                                className="w-9 h-9 rounded-full bg-white border border-gray-200 shadow-sm flex items-center justify-center text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all outline-none"
                                title="Xem tài khoản"
                              >
                                <Eye className="w-4 h-4" />
                              </button>
                            </>
                          ) : (
                            <>
                              <button 
                                onClick={() => openViewDrawer(acc)}
                                className="w-9 h-9 rounded-full bg-white border border-gray-200 shadow-sm flex items-center justify-center text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all outline-none"
                                title="Xem tài khoản"
                              >
                                <Eye className="w-4 h-4" />
                              </button>
                              {activeTab === 'pending' ? (
                                acc.status === 'Pending Approved' && (
                                  <>
                                    <button 
                                      className="w-9 h-9 rounded-full bg-white border border-gray-200 shadow-sm flex items-center justify-center text-gray-500 hover:text-green-600 hover:border-green-600 hover:bg-green-50 transition-all outline-none mx-1 relative z-10"
                                      title="Duyệt"
                                    >
                                      <CheckCircle className="w-4 h-4" />
                                    </button>
                                    <button 
                                      className="w-9 h-9 rounded-full bg-white border border-gray-200 shadow-sm flex items-center justify-center text-gray-500 hover:text-red-500 hover:border-red-500 hover:bg-red-50 transition-all outline-none mr-1 relative z-10"
                                      title="Từ chối"
                                    >
                                      <XCircle className="w-4 h-4" />
                                    </button>
                                  </>
                                )
                              ) : ((!isServerTab || acc.canManageStatus) && acc.status !== 'Pending') ? (
                                <>
                                  <label className="relative flex items-center cursor-pointer mx-1" title={acc.status === 'Active' ? 'Vô hiệu hóa' : 'Kích hoạt'}>
                                    <input type="checkbox" className="sr-only peer" checked={acc.status === 'Active'} onChange={() => requestToggleStatus(acc)} />
                                    <div className="w-10 h-5 bg-gray-200 rounded-full peer-checked:bg-[#004c91] transition-colors relative">
                                      <div className={`absolute top-0.5 left-0.5 w-4 h-4 bg-white rounded-full transition-transform ${acc.status === 'Active' ? 'translate-x-5' : 'translate-x-0'} shadow-sm`}></div>
                                    </div>
                                  </label>
                                </>
                              ) : (
                                <span className="text-gray-300 text-sm">—</span>
                              )}
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  )) : (
                    <tr>
                      <td colSpan={8} className="py-16 text-center text-gray-400 font-medium text-sm">
                        {isServerTab && accountsLoading ? 'Đang tải danh sách tài khoản...' : 'Không tìm thấy tài khoản nào phù hợp'}
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>

            {/* Pagination */}
            {totalItems > 0 && (
            <div className="p-6 border-t border-gray-100 flex items-center justify-between bg-gray-50/50">
              <div className="flex items-center gap-2">
                <span className="text-sm font-medium text-gray-500">Hiển thị</span>
                <div className="relative">
                  <select
                    value={pageSize}
                    onChange={(e) => {
                      setPageSize(Number(e.target.value));
                      setCurrentPage(1);
                    }}
                    className="px-3 py-1.5 pr-8 rounded-lg border border-gray-200 text-sm font-bold text-gray-700 bg-white focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 appearance-none min-w-[70px] text-left"
                  >
                    <option value={5}>5</option>
                    <option value={10}>10</option>
                    <option value={20}>20</option>
                    <option value={50}>50</option>
                  </select>
                  <ChevronDown className="w-4 h-4 absolute right-2 top-1/2 -translate-y-1/2 text-gray-500 pointer-events-none" />
                </div>
                <span className="text-sm font-medium text-gray-500">tài khoản / trang</span>
              </div>

              <div className="flex items-center gap-2">
                <button
                  onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                  disabled={currentPage === 1}
                  className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all disabled:opacity-50 disabled:cursor-not-allowed outline-none"
                >
                  <ChevronLeft className="w-4 h-4" />
                </button>
                <div className="flex items-center gap-1">
                  {Array.from({ length: totalPages }, (_, i) => i + 1).map(page => (
                    <button
                      key={page}
                      onClick={() => setCurrentPage(page)}
                      className={`w-8 h-8 rounded-lg text-sm font-bold transition-all outline-none ${currentPage === page ? 'bg-[#004c91] text-white shadow-sm' : 'text-gray-600 hover:bg-gray-100'}`}
                    >
                      {page}
                    </button>
                  ))}
                </div>
                <button
                  onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
                  disabled={currentPage === totalPages}
                  className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all disabled:opacity-50 disabled:cursor-not-allowed outline-none"
                >
                  <ChevronRight className="w-4 h-4" />
                </button>
              </div>
            </div>
            )}
        </div>
      )}

      {/* Modal: Chi tiết Tài khoản */}
      {isViewDrawerOpen && selectedAccount && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6">
          {/* Overlay */}
          <div 
            className="absolute inset-0 bg-black/40 backdrop-blur-sm animate-in fade-in duration-300"
            onClick={closeViewDrawer}
          />
          {/* Modal Content - Horizontal Layout */}
          <div className="relative w-full max-w-4xl bg-white rounded-2xl shadow-2xl flex flex-col md:flex-row h-auto max-h-[85vh] animate-in zoom-in-95 duration-300 overflow-hidden">
            {/* Modal Left Sidebar */}
            <div className="p-4 sm:p-6 md:p-8 bg-gradient-to-br from-[#004c91] to-[#00386b] shrink-0 md:w-1/3 flex flex-col items-center justify-center text-center relative border-r border-[#00386b]/50">
              <button 
                onClick={closeViewDrawer}
                className="absolute top-4 right-4 md:hidden w-8 h-8 rounded-full bg-white/10 flex items-center justify-center text-white hover:bg-white/20 transition-colors outline-none"
              >
                <X className="w-5 h-5" />
              </button>
              <img src={selectedAccount.avatar} alt="" className="w-24 h-24 rounded-2xl bg-white p-1 shadow-xl mb-6 ring-4 ring-white/10" />
              <span className={`inline-flex px-3 py-1 rounded-lg text-[11px] font-black tracking-widest uppercase mb-3 border shadow-sm ${
                selectedAccount.role === 'ADMIN' ? 'bg-red-500/20 text-red-100 border-red-500/30' :
                selectedAccount.role === 'HO' ? 'bg-blue-500/20 text-blue-100 border-blue-500/30' :
                selectedAccount.role === 'STAFF' ? 'bg-emerald-500/20 text-emerald-100 border-emerald-500/30' :
                'bg-white/20 text-white border-white/20'
              }`}>
                {selectedAccount.role}
              </span>
              <h2 className="text-2xl font-black text-white leading-tight mb-2 tracking-tight">{selectedAccount.name}</h2>
              <p className="text-blue-200 text-sm font-medium mb-8 bg-[#00386b]/40 px-3 py-1 rounded-full">{selectedAccount.email}</p>
              
              <div className="w-full space-y-3 mt-auto">

                {/* Resend the activation link — only for an account the DETAIL endpoint reports as
                    still pending. Sits alongside the other actions, never in place of them, and is
                    kept apart from the status controls: this re-sends a mail, it does not activate
                    anything. */}
                {canResendConfirmation && (
                  <div className="w-full">
                    <button
                      type="button"
                      onClick={() => {
                        setResendError(null);
                        setIsResendConfirmOpen(true);
                      }}
                      disabled={resendSubmitting || resendLimitReached}
                      className="w-full inline-flex items-center justify-center gap-2 rounded-xl border border-sky-300 bg-sky-50 px-4 py-3 text-sm font-bold text-sky-700 transition-colors hover:bg-sky-100 disabled:cursor-not-allowed disabled:opacity-60 outline-none"
                    >
                      {resendSubmitting
                        ? <RefreshCw className="h-4 w-4 animate-spin" />
                        : <Mail className="h-4 w-4" />}
                      {resendSubmitting ? 'Đang gửi...' : 'Gửi lại email xác nhận'}
                    </button>
                    <p className="mt-1.5 text-[11px] leading-snug text-blue-200/80">
                      Dùng khi người nhận chưa nhận được email kích hoạt tài khoản.
                    </p>
                    {resendError && (
                      <p className="mt-1.5 text-[11px] font-bold leading-snug text-red-200">{resendError}</p>
                    )}
                    {lastResendCount !== null && !resendError && (() => {
                      const summary = resendResultSummary(lastDeliveryStatus, lastResendCount);
                      const headlineClass =
                        summary.kind === 'success' ? 'text-emerald-200'
                        : summary.kind === 'error' ? 'text-red-200'
                        : 'text-amber-200';
                      return (
                        <div className="mt-2 rounded-lg bg-white/10 px-2.5 py-2 text-left">
                          <p className={`text-[11px] font-bold leading-snug ${headlineClass}`}>
                            {summary.headline}
                          </p>
                          {summary.detail && (
                            <p className="mt-0.5 text-[11px] leading-snug text-blue-100/80">{summary.detail}</p>
                          )}
                        </div>
                      );
                    })()}
                  </div>
                )}

                {/* Replace Staff Leader (HO only) — the HO list only shows HO + Staff Leaders, so a
                    STAFF row here is the campus IC Head. Hidden while the leader is still awaiting
                    email confirmation: there is no seated leader to replace yet, only one to
                    activate (or cancel), so the resend above is the action that applies. */}
                {isHO && selectedAccount.role === 'STAFF' && selectedAccount.campusId && !isPendingEmailConfirmation && (
                  <button
                    onClick={() => {
                      setReplaceLeaderTarget({ campusId: String(selectedAccount.campusId), campusName: selectedAccount.campus || '' });
                      closeViewDrawer();
                    }}
                    className="w-full flex items-center justify-center gap-2 py-3 rounded-xl bg-orange-500 text-white font-bold hover:bg-orange-600 hover:shadow-[0_0_20px_rgba(249,115,22,0.5)] transition-all duration-300 border border-orange-400 outline-none group"
                  >
                    <UserCog className="w-4 h-4 group-hover:scale-110 transition-transform duration-300" /> Thay thế Staff Leader
                  </button>
                )}

                {/*!isHO && activeTab === 'all' && (!isStaff || isStaffLeader) && (
                  <button 
                    onClick={() => setIsResetPasswordModalOpen(true)}
                    className="w-full flex items-center justify-center gap-2 py-3 rounded-xl bg-orange-500 text-white font-bold hover:bg-orange-600 hover:shadow-[0_0_20px_rgba(249,115,22,0.6)] transition-all duration-300 border border-orange-400 outline-none group"
                  >
                    <Key className="w-4 h-4 group-hover:scale-110 transition-transform duration-300" /> Đặt lại Mật khẩu
                  </button>
                )*/}
              </div>
            </div>

            {/* Modal Right Content */}
            <div className="flex-1 overflow-y-auto p-8 bg-[#f8fafc] relative">
              <div className="flex items-start justify-between mb-6">
                <h3 className="text-xl font-black text-[#004c91] flex items-center gap-2 tracking-tight mt-1">
                  <UserCog className="w-6 h-6" /> {isEditingProfile ? 'Chỉnh sửa thông tin tài khoản' : 'Thông tin chi tiết'}
                </h3>
                <div className="flex items-center gap-3">
                  {/* Chỉnh sửa: HO → basic info (canEditBasicInfo); Staff Leader/ADMIN → role (canUpdateRole). */}
                  {!isEditingProfile && !selectedAccount.isCurrentUser && (
                    (isHO && selectedAccount.canEditBasicInfo === true) ||
                    ((isStaffLeader || isRealAdmin) && selectedAccount.canUpdateRole !== false)
                  ) && (
                    <button
                      onClick={handleEditClick}
                      className="flex items-center gap-1.5 px-3.5 py-2 rounded-xl text-xs font-bold text-[#004c91] border border-[#004c91]/40 bg-white hover:bg-blue-50 transition-all outline-none"
                    >
                      <Edit className="w-3.5 h-3.5" /> {isHO ? 'Chỉnh sửa thông tin' : 'Chỉnh sửa tài khoản'}
                    </button>
                  )}
                  <button
                    onClick={closeViewDrawer}
                    className="hidden md:flex w-8 h-8 rounded-full bg-white border border-gray-200 shadow-sm items-center justify-center text-gray-500 hover:text-red-500 hover:border-red-200 hover:bg-red-50 transition-all outline-none"
                  >
                    <X className="w-4 h-4" />
                  </button>
                </div>
              </div>

              <div className="bg-white rounded-2xl p-6 shadow-sm border border-gray-100 flex flex-col gap-6 relative z-10 w-full animate-in fade-in duration-300">
                {(() => {
                  // The detail snapshot never changes while the role editor is open.
                  // Only roleValue comes from editForm; every other field renders the UC-98 data.
                  const data = selectedAccount;
                  const roleValue = isEditingProfile
                    ? (editForm?.role ?? selectedAccount.role)
                    : selectedAccount.role;
                  const isEdit = isEditingProfile;

                  const Input = ({ label, value, field, type="text", disabled=false }: any) => (
                    <div className="flex flex-col min-w-0">
                      <span className="block text-[10px] font-bold uppercase tracking-wider mb-1 text-gray-500">{label}</span>
                      {isEdit ? (
                        <input
                          type={type}
                          value={value || ''}
                          onChange={(e) => setEditForm({...editForm, [field]: e.target.value})}
                          disabled={disabled}
                          className={`px-3 py-2 border border-gray-200 rounded-lg text-sm font-medium text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#004c91] bg-gray-50 transition-all w-full ${disabled ? 'opacity-70 cursor-not-allowed' : 'focus:bg-white'}`}
                        />
                      ) : (
                        <span className="block text-sm font-bold text-gray-900 bg-gray-50/50 p-2.5 rounded-lg border border-gray-100 break-words">{value || '-'}</span>
                      )}
                    </div>
                  );

                  const Select = ({ label, value, field, options, disabled=false }: any) => (
                    <div className="flex flex-col min-w-0">
                      <span className="block text-[10px] font-bold uppercase tracking-wider mb-1 text-gray-500">{label}</span>
                      {isEdit ? (
                        <div className="relative">
                          <select 
                            value={value || ''}
                            onChange={(e) => setEditForm({...editForm, [field]: e.target.value})}
                            disabled={disabled}
                            className={`px-3 py-2 pr-8 border border-gray-200 rounded-lg text-sm font-medium text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#004c91] bg-gray-50 transition-all appearance-none w-full ${disabled ? 'opacity-70 cursor-not-allowed' : 'focus:bg-white'}`}
                          >
                            {value && !options.some((opt: any) => String(opt.value) === String(value)) && (
                              <option value={value}>{value}</option>
                            )}
                            {options.map((opt: any) => (
                              <option key={opt.value} value={opt.value} disabled={opt.disabled}>{opt.label}</option>
                            ))}
                          </select>
                          <ChevronDown className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none" />
                        </div>
                      ) : (
                        <span className="block text-sm font-bold text-gray-900 bg-gray-50/50 p-2.5 rounded-lg border border-gray-100 break-words">{value || '-'}</span>
                      )}
                    </div>
                  );

                  const HighlightInput = ({ label, value, field, colSpan, disabled=false }: any) => (
                    <div className={`flex flex-col min-w-0 ${colSpan ? 'md:col-span-2' : ''}`}>
                      <span className="block text-[10px] font-bold uppercase tracking-wider mb-1 text-[#004c91]/80">{label}</span>
                      {isEdit ? (
                       <input 
                          value={value || ''}
                          onChange={(e) => setEditForm({...editForm, [field]: e.target.value})}
                          disabled={disabled}
                          className={`px-3 py-2 border border-blue-200 rounded-lg text-sm font-black text-[#004c91] focus:outline-none focus:ring-2 focus:ring-[#004c91] bg-blue-50/30 transition-all w-full ${disabled ? 'opacity-70 cursor-not-allowed' : 'focus:bg-white'}`}
                        />
                      ) : (
                        <span className="block text-sm font-black text-[#004c91] bg-blue-50/30 p-2.5 rounded-lg border border-blue-100 break-words">{value || '-'}</span>
                      )}
                    </div>
                  );

                  // ── One visual language for the whole edit grid (spec §4.6) ──────────────────
                  // Which fields a caller may actually change varies by role, sub-role and campus
                  // scope, so the operator cannot work it out from the labels — the field itself has
                  // to say so. Editable is WHITE on a brand-tinted border; locked is FILLED slate.
                  // The previous styling made the two nearly indistinguishable (locked fields sat on
                  // `bg-gray-50/50` while editable selects sat on `bg-gray-50`, i.e. the locked ones
                  // were the LIGHTER of the pair) — which is how an operator ends up clicking a field,
                  // getting no caret, and assuming the page is broken.
                  //
                  // Colour is never the only signal: locked fields also carry a lock glyph, so the
                  // distinction survives for anyone who cannot separate white from pale slate.
                  // Chrome only — each call site adds its own text colour, so the MSSV field can stay
                  // brand-bold without two competing `text-*` classes racing in the stylesheet.
                  const EDITABLE_FIELD =
                    'bg-white border-[#004c91]/35 hover:border-[#004c91]/60 focus:ring-2 focus:ring-[#004c91]';
                  const LOCKED_FIELD = 'bg-slate-100 border-slate-200 text-slate-500';

                  // Read-only labelled field: renders the snapshot value, or "-" when empty
                  // (spec §3.1 — null/undefined/'' must show "-", never a fallback like "Nam").
                  //
                  // Always styled locked, because that is what it always is. `highlight` tints only the
                  // LABEL now: a field the operator cannot touch must not be dressed as an active input
                  // just because its value matters (read-only MSSV was the one case).
                  const DisplayField = ({ label, value, highlight = false, colSpan = false }: any) => (
                    <div className={`flex flex-col min-w-0 ${colSpan ? 'md:col-span-2' : ''}`}>
                      <span className={`block text-[10px] font-bold uppercase tracking-wider mb-1 ${highlight ? 'text-[#004c91]/80' : 'text-gray-500'}`}>{label}</span>
                      <div className={`flex items-center gap-2 rounded-lg border px-2.5 py-2.5 ${LOCKED_FIELD}`}>
                        <span className="min-w-0 flex-1 break-words text-sm font-semibold">
                          {value === null || value === undefined || value === '' ? '-' : value}
                        </span>
                        <Lock className="h-3.5 w-3.5 shrink-0 text-slate-400" aria-hidden="true" />
                      </div>
                    </div>
                  );

                  // UC-98 — account status badge. The value comes from the DETAIL response
                  // (openViewDrawer stores details.status in rawStatus); the list row is only the
                  // fallback for the instant before that request resolves. Read-only in BOTH modes
                  // (spec §11.6): status is changed through the enable/disable + lock actions on the
                  // list, never from this modal.
                  const statusMeta = resolveAccountStatusMeta(data.rawStatus, data.status);
                  const StatusField = () => (
                    <div className="flex flex-col min-w-0">
                      <span className="block text-[10px] font-bold uppercase tracking-wider mb-1 text-gray-500">
                        Trạng thái tài khoản
                      </span>
                      {/* No field-style box behind the badge — the pill IS the value here, and a gray
                          container around it read as an empty input. min-h keeps the row aligned with
                          the boxed fields beside it. */}
                      <div className="min-h-[42px] flex items-center">
                        <span className={`inline-flex items-center rounded-full border px-3 py-1 text-xs font-bold ${statusMeta.className}`}>
                          {statusMeta.label}
                        </span>
                      </div>
                    </div>
                  );

                  // The role currently selected in the editor (falls back to the snapshot role).
                  const editRoleCode: string = roleEditForm?.roleCode ?? data.role;
                  const roleSelectOptions = isHO
                    ? [{ value: 'HO', label: 'HO (Head Office)' }, { value: 'STAFF', label: 'Staff Leader (Trưởng phòng IC)' }]
                    : isStaffLeader
                      ? [{ value: 'STAFF', label: 'STAFF (Nhân sự IC)' }, { value: 'DEPARTMENT', label: 'Department (Trưởng phòng ban)' }, { value: 'STUDENT', label: 'STUDENT (Sinh viên)' }]
                      : [{ value: 'ADMIN', label: 'ADMIN' }, { value: 'HO', label: 'HO (Head Office)' }, { value: 'STAFF', label: 'STAFF' }, { value: 'DEPARTMENT', label: 'DEPARTMENT' }, { value: 'STUDENT', label: 'STUDENT' }, { value: 'VISITOR', label: 'VISITOR' }];

                  const selectClass = (disabled = false) =>
                    `px-3 py-2 pr-8 border rounded-lg text-sm font-medium focus:outline-none transition-all appearance-none w-full ${
                      disabled ? `${LOCKED_FIELD} cursor-not-allowed` : `${EDITABLE_FIELD} text-gray-900`
                    }`;

                  // The chevron follows its select: a brand-tinted arrow on a slate box would read as
                  // an affordance that is not there.
                  const chevronClass = (disabled = false) =>
                    `w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none ${
                      disabled ? 'text-slate-400' : 'text-[#004c91]/50'
                    }`;

                  // Red border while the field has a validation error (identity spec §7.4).
                  const identityInputClass = (disabled: boolean, hasError = false) =>
                    `px-3 py-2 border rounded-lg text-sm font-medium focus:outline-none transition-all w-full ${
                      disabled
                        ? `${LOCKED_FIELD} cursor-not-allowed`
                        : hasError
                          ? 'bg-white border-red-400 text-gray-900 focus:ring-2 focus:ring-red-400'
                          : `${EDITABLE_FIELD} text-gray-900`
                    }`;

                  // Locked-target identity display value (snapshot); editable value comes from the form.
                  const identityDisabled = !canEditIdentity || roleSaving;

                  // ── Edit mode (Chỉnh sửa thông tin tài khoản): identity editable for eligible targets,
                  //    org fields locked, layout follows editRoleCode. Identity inputs are inline JSX
                  //    (not a nested component) so typing never remounts them / loses focus. ──
                  const editGrid = (
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-5 w-full">
                      <div className="flex flex-col min-w-0">
                        <label htmlFor="edit-full-name" className="block text-[10px] font-bold uppercase tracking-wider mb-1 text-gray-500">
                          Họ và tên{canEditIdentity && <span className="ml-1 text-red-500">*</span>}
                        </label>
                        <input
                          id="edit-full-name"
                          type="text"
                          value={canEditIdentity ? (roleEditForm?.fullName ?? '') : (data.name ?? '')}
                          maxLength={ACCOUNT_FULL_NAME_MAX_LENGTH}
                          autoComplete="name"
                          disabled={identityDisabled}
                          aria-invalid={!!editFieldErrors.fullName}
                          aria-describedby={editFieldErrors.fullName ? 'edit-full-name-error' : undefined}
                          onChange={(e) => {
                            setRoleEditForm((prev) => (prev ? { ...prev, fullName: e.target.value } : prev));
                            setEditFieldErrors((prev) => ({ ...prev, fullName: undefined }));
                          }}
                          onBlur={(e) => canEditIdentity && setEditFieldErrors((prev) => ({
                            ...prev,
                            fullName: validateFullName(e.target.value) ?? undefined,
                          }))}
                          className={identityInputClass(identityDisabled, !!editFieldErrors.fullName)}
                        />
                        {editFieldErrors.fullName && (
                          <p id="edit-full-name-error" className="mt-1.5 text-sm text-red-600 font-medium">{editFieldErrors.fullName}</p>
                        )}
                      </div>
                      <div className="flex flex-col min-w-0">
                        <label htmlFor="edit-email" className="block text-[10px] font-bold uppercase tracking-wider mb-1 text-gray-500">
                          Email{canEditIdentity && <span className="ml-1 text-red-500">*</span>}
                        </label>
                        <input
                          id="edit-email"
                          type="email"
                          value={canEditIdentity ? (roleEditForm?.email ?? '') : (data.email ?? '')}
                          maxLength={ACCOUNT_EMAIL_MAX_LENGTH}
                          autoComplete="email"
                          inputMode="email"
                          disabled={identityDisabled}
                          aria-invalid={!!editFieldErrors.email}
                          aria-describedby={editFieldErrors.email ? 'edit-email-error' : undefined}
                          onChange={(e) => {
                            setRoleEditForm((prev) => (prev ? { ...prev, email: e.target.value } : prev));
                            setEditFieldErrors((prev) => ({ ...prev, email: undefined }));
                          }}
                          onBlur={(e) => canEditIdentity && setEditFieldErrors((prev) => ({
                            ...prev,
                            email: validateAccountEmail(e.target.value) ?? undefined,
                          }))}
                          className={identityInputClass(identityDisabled, !!editFieldErrors.email)}
                        />
                        {editFieldErrors.email && (
                          <p id="edit-email-error" className="mt-1.5 text-sm text-red-600 font-medium">{editFieldErrors.email}</p>
                        )}
                        {canEditIdentity && !editFieldErrors.email && (
                          <p className="mt-1.5 text-xs text-gray-500">Chỉ chấp nhận @gmail.com và @fpt.edu.vn.</p>
                        )}
                      </div>
                      <DisplayField label="Giới tính" value={genderLabel(data.gender)} />
                      <DisplayField label="Số điện thoại" value={data.phone} />

                      {/* Vai trò — editable for Staff Leader/ADMIN; DISABLED for HO (basic-info only). */}
                      <div className="flex flex-col min-w-0">
                        <span className="block text-[10px] font-bold uppercase tracking-wider mb-1 text-gray-500">Vai trò</span>
                        <div className="relative">
                          <select
                            value={editRoleCode}
                            disabled={isHO}
                            onChange={(e) => changeRoleCode(e.target.value)}
                            className={selectClass(isHO)}
                          >
                            {roleSelectOptions.map((o) => (
                              <option key={o.value} value={o.value}>{o.label}</option>
                            ))}
                          </select>
                          <ChevronDown className={chevronClass(isHO)} />
                        </div>
                      </div>

                      <StatusField />

                      <DisplayField label="Cơ sở trực thuộc" value={data.campus} />

                      {/* HO editing a Staff Leader: position + IC department are shown read-only
                          (spec §5.3 — HO never sees a control that can change them). */}
                      {isHO && data.role === 'STAFF' && (
                        <>
                          <DisplayField label="Chức vụ" value="Trưởng phòng" />
                          <DisplayField label="Phòng ban" value={data.department} />
                        </>
                      )}

                      {roleOptionsError && (
                        <div className="md:col-span-2 rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-bold text-red-700">
                          {roleOptionsError}
                        </div>
                      )}

                      {/* ── Staff Leader dynamic matrix (spec §3.3/§3.4/§3.5) ── */}
                      {isStaffLeader && editRoleCode === 'STAFF' && (
                        <>
                          <DisplayField label="Chức vụ" value="Nhân viên" />
                          {/* Auto-assigned from the campus IC department — a value, never a choice. */}
                          <DisplayField
                            label="Phòng ban"
                            value={roleOptionsLoading
                              ? 'Đang tải...'
                              : (roleOptions?.icDepartment?.name ?? 'Không có Phòng Hợp tác Quốc tế đang hoạt động')}
                          />
                        </>
                      )}

                      {isStaffLeader && editRoleCode === 'DEPARTMENT' && (
                        <>
                          <DisplayField label="Chức vụ" value="Trưởng phòng" />
                          <div className="flex flex-col min-w-0 md:col-span-2">
                            <span className="block text-[10px] font-bold uppercase tracking-wider mb-1 text-gray-500">Phòng ban <span className="text-red-500">*</span></span>
                            <div className="relative">
                              <select
                                value={roleEditForm?.departmentId ?? ''}
                                disabled={roleOptionsLoading}
                                onChange={(e) => setRoleEditForm((prev) => (prev ? { ...prev, departmentId: e.target.value } : prev))}
                                className={selectClass(roleOptionsLoading)}
                              >
                                <option value="">-- Chọn phòng ban --</option>
                                {(roleOptions?.generalDepartments ?? []).map((d) => (
                                  <option key={d.departmentId} value={d.departmentId} disabled={!d.selectable}>
                                    {d.name}{!d.selectable ? ' — Đã có trưởng phòng' : d.isCurrentTargetHead ? ' (Phòng ban hiện tại)' : ''}
                                  </option>
                                ))}
                              </select>
                              <ChevronDown className={chevronClass(roleOptionsLoading)} />
                            </div>
                            {roleOptionsLoading && <p className="mt-1.5 text-xs text-gray-500">Đang tải danh sách phòng ban...</p>}
                            {!roleOptionsLoading && !roleOptionsError && (roleOptions?.generalDepartments.length ?? 0) === 0 && (
                              <p className="mt-1.5 text-xs text-amber-600">Cơ sở của bạn hiện chưa có phòng ban phù hợp để gán trưởng phòng.</p>
                            )}
                          </div>
                        </>
                      )}

                      {isStaffLeader && editRoleCode === 'STUDENT' && (
                        <div className="flex flex-col min-w-0">
                          <span className="block text-[10px] font-bold uppercase tracking-wider mb-1 text-[#004c91]/80">Mã số sinh viên (MSSV) <span className="text-red-500">*</span></span>
                          <input
                            value={roleEditForm?.studentCode ?? ''}
                            maxLength={30}
                            onChange={(e) => setRoleEditForm((prev) => (prev ? { ...prev, studentCode: e.target.value } : prev))}
                            placeholder="Nhập mã số sinh viên"
                            // Same editable chrome as every other open field; the brand-bold value is
                            // what marks it as the key identifier, not a different background.
                            className={`px-3 py-2 border rounded-lg text-sm font-black text-[#004c91] focus:outline-none transition-all w-full ${EDITABLE_FIELD}`}
                          />
                        </div>
                      )}

                      {/* ── Department handover. Shown whenever this change takes the account out of
                          a head seat, whatever the new role is: the backend refuses to leave a
                          department headless, and handing over separately afterwards would demote
                          the account to DEPARTMENT/STAFF — a shape a Staff Leader cannot manage,
                          which would strand the role change entirely. ── */}
                      {isStaffLeader && needsHeadReplacement && headedDepartment && (
                        <div className="flex flex-col min-w-0 md:col-span-2">
                          <span className="block text-[10px] font-bold uppercase tracking-wider mb-1 text-gray-500">
                            Trưởng phòng thay thế cho {headedDepartment.name} <span className="text-red-500">*</span>
                          </span>
                          {headedDepartment.replacementCandidates.length > 0 ? (
                            <>
                              <div className="relative">
                                <select
                                  value={roleEditForm?.replacementHeadUserId ?? ''}
                                  disabled={roleOptionsLoading}
                                  onChange={(e) => setRoleEditForm((prev) => (prev ? { ...prev, replacementHeadUserId: e.target.value } : prev))}
                                  className={selectClass(roleOptionsLoading)}
                                >
                                  <option value="">-- Chọn người thay thế --</option>
                                  {headedDepartment.replacementCandidates.map((c) => (
                                    <option key={c.userId} value={c.userId}>{c.fullName} — {c.email}</option>
                                  ))}
                                </select>
                                <ChevronDown className={chevronClass(roleOptionsLoading)} />
                              </div>
                              <p className="mt-1.5 text-xs text-gray-500">
                                Tài khoản này đang là Trưởng phòng của {headedDepartment.name}. Người được chọn sẽ nhận vai trò Trưởng phòng ngay khi lưu thay đổi.
                              </p>
                            </>
                          ) : (
                            <p className="mt-1.5 text-xs text-amber-600">
                              {headedDepartment.name} hiện chưa có nhân viên nào đủ điều kiện làm Trưởng phòng thay thế. Vui lòng bổ sung nhân sự cho phòng ban trước khi thay đổi vai trò của tài khoản này.
                            </p>
                          )}
                        </div>
                      )}

                      {/* ADMIN keeps the legacy read-only snapshot of the current org fields. */}
                      {isRealAdmin && (data.role === 'STAFF' || data.role === 'DEPARTMENT') && (
                        <>
                          <DisplayField label="Chức vụ" value={subRoleLabel(data.subRole)} />
                          <DisplayField label="Phòng ban" value={data.department} />
                        </>
                      )}
                      {isRealAdmin && data.role === 'STUDENT' && (
                        <DisplayField label="Mã số sinh viên (MSSV)" value={data.studentId} highlight />
                      )}
                    </div>
                  );

                  // ── View mode (read-only detail): the original layout, keyed on the real role. ──
                  const viewGrid = (
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-5 w-full">
                      <Input label="Họ và tên" value={data.name} field="name" disabled={isEdit} />
                      <Input label="Email" value={data.email} field="email" type="email" disabled={isEdit} />
                      <Select label="Giới tính" value={genderLabel(data.gender)} field="gender" options={[{value: 'Nam', label:'Nam'}, {value:'Nữ', label:'Nữ'}, {value:'Khác', label:'Khác'}, {value:'Không xác định', label:'Không xác định'}]} disabled={isEdit} />
                      <Input label="Số điện thoại" value={data.phone} field="phone" disabled={isEdit} />
                      {(isHO || isRealAdmin || isStaffLeader) && (
                        <Select label="Vai trò" value={roleValue} field="role" disabled={true} options={roleSelectOptions} />
                      )}

                      <StatusField />

                      {data.role === 'STUDENT' && (
                        <>
                          <HighlightInput label="Mã số sinh viên (MSSV)" value={data.studentId} field="studentId" disabled={isEdit} />
                          <Select label="Cơ sở trực thuộc" value={data.campus} field="campus" options={CAMPUSES.map(c=>({value:c,label:c}))} disabled={isEdit} />
                        </>
                      )}

                      {(data.role === 'STAFF' || data.role === 'DEPARTMENT') && (
                        <>
                          <Select label="Cơ sở trực thuộc" value={data.campus} field="campus" options={CAMPUSES.map(c=>({value:c,label:c}))} disabled={isEdit} />
                          <Select label="Chức vụ" value={subRoleLabel(data.subRole)} field="subRole" options={[{value:'Trưởng phòng', label:'Trưởng phòng'}, {value:'Nhân viên', label:'Nhân viên'}]} disabled={isEdit} />
                          <Select label="Phòng ban" value={data.department} field="department" options={data.department ? [{value:data.department, label:data.department}] : []} disabled={isEdit} />
                        </>
                      )}

                      {(data.role === 'ADMIN' || data.role === 'HO') && (
                        <Select label="Cơ sở trực thuộc" value={data.campus} field="campus" options={CAMPUSES.map(c=>({value:c,label:c}))} disabled={isEdit} />
                      )}

                      {data.role === 'VISITOR' && (
                        <>
                          <Input label="Quốc tịch" value={data.nationality} field="nationality" disabled={isEdit} />
                          <HighlightInput label="Đơn vị công tác / Doanh nghiệp" value={data.organization} field="organization" colSpan={true} disabled={isEdit} />
                        </>
                      )}
                    </div>
                  );

                  return (
                    <>
                      {isEdit ? editGrid : viewGrid}

                      {isEditingProfile && (
                        <div className="mt-4 pt-6 border-t border-gray-100 animate-in fade-in slide-in-from-bottom-2 duration-300">
                          {roleError && (
                            <div className="mb-3 rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-bold text-red-700 whitespace-pre-line">
                              {roleError}
                            </div>
                          )}
                          <div className="flex items-center justify-end gap-3">
                            <button
                              type="button"
                              onClick={resetRoleEditor}
                              className="px-5 py-2.5 rounded-xl font-bold text-sm text-gray-500 hover:text-gray-700 hover:bg-gray-100 transition-colors outline-none"
                            >
                              Hủy
                            </button>
                            <button
                              type="button"
                              disabled={roleSaving || !roleIsDirty || roleUpdateBlocked || identityInvalid}
                              onClick={handleUpdateRole}
                              className="px-6 py-2.5 rounded-xl text-white font-bold text-sm bg-[#0aa14f] hover:bg-[#088c44] shadow-[0_4px_12px_rgba(10,161,79,0.2)] hover:shadow-[0_6px_16px_rgba(10,161,79,0.3)] transition-all outline-none disabled:opacity-60 disabled:cursor-not-allowed"
                            >
                              {roleSaving ? 'Đang lưu...' : 'Cập nhật'}
                            </button>
                          </div>
                        </div>
                      )}
                    </>
                  );
                })()}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Xác nhận gửi lại email xác nhận. Layers ABOVE the detail modal and never closes it — after
          the send, HO stays on the account they were looking at. */}
      {isResendConfirmOpen && selectedAccount && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 sm:p-6 animate-in fade-in duration-200">
          <div
            className="absolute inset-0 bg-black/50 backdrop-blur-sm"
            onClick={() => { if (!resendSubmitting) setIsResendConfirmOpen(false); }}
          />
          <div className="relative bg-white rounded-2xl w-full max-w-md shadow-xl overflow-hidden animate-in zoom-in-95 duration-300">
            <div className="px-6 py-4 border-b border-gray-100 flex items-center gap-2">
              <Mail className="w-5 h-5 text-sky-600" />
              <h2 className="text-lg font-black text-gray-800">Gửi lại email xác nhận</h2>
            </div>

            <div className="p-6 space-y-3 text-[15px] leading-relaxed text-gray-700">
              <p>Hệ thống sẽ phát hành một liên kết xác nhận mới và gửi đến:</p>
              {/* Read-only on purpose: correcting a wrong address is the edit-pending-email flow,
                  not something to slip into a resend. */}
              <p className="rounded-lg border border-gray-200 bg-gray-50 px-3 py-2 font-bold text-[#004c91] break-all">
                {selectedAccount.email}
              </p>
              <p>
                Liên kết xác nhận cũ sẽ không còn hiệu lực. Tài khoản vẫn ở trạng thái chờ xác nhận
                cho đến khi người nhận hoàn tất xác nhận email.
              </p>
              {resendError && (
                <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-bold text-red-700">
                  {resendError}
                </div>
              )}
            </div>

            <div className="px-6 py-4 border-t border-gray-100 bg-gray-50 flex items-center justify-end gap-3 rounded-b-2xl">
              <button
                type="button"
                onClick={() => setIsResendConfirmOpen(false)}
                disabled={resendSubmitting}
                className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-100 transition-colors outline-none disabled:cursor-not-allowed disabled:opacity-60"
              >
                Hủy
              </button>
              <button
                type="button"
                onClick={confirmResendEmail}
                disabled={resendSubmitting || resendLimitReached}
                className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl font-bold text-white bg-sky-600 hover:bg-sky-700 shadow-[0_4px_12px_rgba(2,132,199,0.2)] transition-all outline-none disabled:cursor-not-allowed disabled:opacity-60"
              >
                {resendSubmitting && <RefreshCw className="h-4 w-4 animate-spin" />}
                {resendSubmitting ? 'Đang gửi...' : 'Xác nhận gửi lại'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal Xác nhận Đặt lại Mật khẩu */}
      {isResetPasswordModalOpen && selectedAccount && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-[60] flex items-center justify-center p-4 sm:p-6 animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl w-full max-w-md shadow-xl overflow-hidden animate-in zoom-in-95 duration-300 relative">
            <div className="px-6 py-4 border-b border-gray-100 flex items-center gap-2">
              <h2 className="text-xl font-black text-gray-800">⚠️ Xác nhận đặt lại mật khẩu</h2>
              <button 
                onClick={() => setIsResetPasswordModalOpen(false)}
                className="absolute top-4 right-4 w-8 h-8 rounded-full hover:bg-gray-100 flex items-center justify-center text-gray-500 transition-colors outline-none"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            
            <div className="p-6 text-gray-700 leading-relaxed text-[15px]">
              Bạn có chắc chắn muốn đặt lại mật khẩu cho tài khoản <strong className="text-[#004c91] font-bold">{selectedAccount.email}</strong> không?  Mật khẩu cũ của người này sẽ lập tức bị hủy bỏ và hệ thống sẽ tự động sinh ra một mật khẩu tạm thời mới.
            </div>

            <div className="px-6 py-4 border-t border-gray-100 bg-gray-50 flex items-center justify-end gap-3 rounded-b-2xl">
              <button 
                onClick={() => setIsResetPasswordModalOpen(false)}
                className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-100 transition-colors outline-none"
              >
                Hủy
              </button>
              <button 
                onClick={() => {
                  setIsResetPasswordModalOpen(false);
                  // Optionally add a toast message here for success
                }}
                className="px-5 py-2.5 rounded-xl font-bold text-white bg-orange-500 hover:bg-orange-600 shadow-[0_4px_12px_rgba(249,115,22,0.2)] hover:shadow-[0_6px_16px_rgba(249,115,22,0.4)] transition-all outline-none transform"
              >
                Xác nhận đặt lại
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal Khởi tạo tài khoản mới */}
      {isCreateModalOpen && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl w-full max-w-2xl shadow-xl overflow-hidden animate-in zoom-in-95 duration-300">
            {/* Header */}
            <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between bg-[#004c91]">
              <h2 className="text-xl font-black text-white">Khởi tạo tài khoản mới</h2>
              <button 
                onClick={() => setIsCreateModalOpen(false)}
                className="w-8 h-8 rounded-full hover:bg-white/20 flex items-center justify-center text-white transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Body */}
            <div className="p-6 max-h-[70vh] overflow-y-auto">
                <div className="space-y-5">                  
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-2">Vai trò (Role) <span className="text-red-500">*</span></label>
                    <select
                      value={manualForm.role}
                      onChange={(e) => {
                        const nextRole = e.target.value;
                        // Clear MSSV (+ its error) whenever the role is not STUDENT, so a hidden code
                        // is never carried over (spec §5.4).
                        setManualForm((prev) => ({ ...prev, role: nextRole, studentCode: nextRole === 'STUDENT' ? prev.studentCode : '' }));
                        setCreateStudentCodeError(null);
                      }}
                      className="w-full px-4 py-2.5 rounded-xl border border-gray-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none transition-shadow text-sm bg-gray-50 hover:bg-gray-100 cursor-pointer"
                    >
                      <option value="">-- Chọn vai trò --</option>
                      {isHO ? (
                        <>
                          <option value="HO">HO (Head Office)</option>
                          <option value="STAFF">Staff Leader (Trưởng phòng IC)</option>
                        </>
                      ) : isStaffLeader ? (
                        <>
                          <option value="STAFF">STAFF (Nhân sự phòng IC)</option>
                          <option value="DEPARTMENT">Department (Trưởng phòng ban)</option>
                          <option value="STUDENT">STUDENT (Sinh viên)</option>
                        </>
                      ) : (
                        <>
                          <option value="ADMIN">ADMIN (Quản trị viên)</option>
                          <option value="HO">HO (Head Office)</option>
                          <option value="STUDENT">STUDENT (Sinh viên)</option>
                          <option value="VISITOR">VISITOR (Khách)</option>
                        </>
                      )}
                    </select>
                  </div>
                  
                  {/* HO luôn chọn campus; ADMIN chọn campus khi role cần (HO/STUDENT) */}
                  {(isHO || (isRealAdmin && ['HO', 'STUDENT'].includes(manualForm.role))) && (
                    <div>
                      <label className="block text-sm font-bold text-gray-700 mb-2">Cơ sở <span className="text-red-500">*</span></label>
                      <select
                        value={createCampus}
                        onChange={(e) => setCreateCampus(e.target.value)}
                        className="w-full px-4 py-2.5 rounded-xl border border-gray-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none transition-shadow text-sm bg-gray-50 hover:bg-gray-100 cursor-pointer"
                      >
                        <option value="">-- Chọn cơ sở --</option>
                        {campusOptions.map(c => <option key={c.campusId} value={c.campusName}>{c.campusName}</option>)}
                      </select>
                    </div>
                  )}

                  {/* UC-96: Staff Leader availability for the chosen campus (HO + role STAFF). */}
                  {isHO && manualForm.role === 'STAFF' && createCampus && (
                    <div>
                      {slAvailabilityLoading ? (
                        <div className="flex items-center gap-2 rounded-xl border border-gray-200 bg-gray-50 px-4 py-3 text-sm font-medium text-gray-500">
                          <RefreshCw className="w-4 h-4 animate-spin" />
                          Đang kiểm tra Trưởng phòng IC của cơ sở...
                        </div>
                      ) : slAvailability && slAvailability.canCreateStaffLeader ? (
                        <div className="flex items-start gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
                          <CheckCircle className="w-5 h-5 shrink-0 mt-0.5" />
                          <div>
                            <p className="font-bold">Cơ sở này chưa có Trưởng phòng IC.</p>
                            {slAvailability.icDepartmentName && (
                              <p className="text-emerald-700 mt-0.5">Phòng ban: {slAvailability.icDepartmentName} — tự động gán.</p>
                            )}
                          </div>
                        </div>
                      ) : slAvailability ? (
                        <div className="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
                          <XCircle className="w-5 h-5 shrink-0 mt-0.5 text-amber-600" />
                          <div>
                            <p className="font-bold leading-snug">{slAvailability.message}</p>
                            {slAvailability.existingLeader && (
                              <p className="mt-1 text-amber-700">
                                <span className="font-bold">{slAvailability.existingLeader.fullName}</span>
                                {' — '}{slAvailability.existingLeader.email}
                                <span className="ml-2 inline-flex items-center rounded-full border border-amber-300 bg-amber-100 px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide">
                                  {slAvailability.existingLeader.status === 'ACTIVE' ? 'Đang hoạt động'
                                    : slAvailability.existingLeader.status === 'INACTIVE' ? 'Vô hiệu hóa'
                                    : slAvailability.existingLeader.status === 'LOCKED' ? 'Bị khóa'
                                    : slAvailability.existingLeader.status}
                                </span>
                              </p>
                            )}
                          </div>
                        </div>
                      ) : null}
                    </div>
                  )}

                  {/* UC-96: HO campus availability for the chosen campus (HO + role HO). */}
                  {isHO && manualForm.role === 'HO' && createCampus && (
                    <div>
                      {hoCampusCheckLoading ? (
                        <div className="flex items-center gap-2 rounded-xl border border-gray-200 bg-gray-50 px-4 py-3 text-sm font-medium text-gray-500">
                          <RefreshCw className="w-4 h-4 animate-spin" />
                          Đang kiểm tra tài khoản HO của cơ sở...
                        </div>
                      ) : hoCampusCheck && hoCampusCheck.canCreateHo ? (
                        <div className="flex items-start gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
                          <CheckCircle className="w-5 h-5 shrink-0 mt-0.5" />
                          <p className="font-bold">Cơ sở này chưa có tài khoản HO. Có thể tiếp tục tạo.</p>
                        </div>
                      ) : hoCampusCheck ? (
                        <div className="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
                          <XCircle className="w-5 h-5 shrink-0 mt-0.5 text-amber-600" />
                          <div>
                            <p className="font-bold leading-snug">{hoCampusCheck.message}</p>
                            {hoCampusCheck.existingHo && (
                              <p className="mt-1 text-amber-700">
                                <span className="font-bold">{hoCampusCheck.existingHo.fullName}</span>
                                {' — '}{hoCampusCheck.existingHo.email}
                                <span className="ml-2 inline-flex items-center rounded-full border border-amber-300 bg-amber-100 px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide">
                                  {hoCampusCheck.existingHo.status === 'ACTIVE' ? 'Đang hoạt động'
                                    : hoCampusCheck.existingHo.status === 'INACTIVE' ? 'Vô hiệu hóa'
                                    : hoCampusCheck.existingHo.status === 'LOCKED' ? 'Bị khóa'
                                    : hoCampusCheck.existingHo.status}
                                </span>
                              </p>
                            )}
                          </div>
                        </div>
                      ) : null}
                    </div>
                  )}

                  {/* UC-96-SL: STAFF auto-assigned to the IC department (read-only hint). */}
                  {isStaffLeader && manualForm.role === 'STAFF' && (
                    <div>
                      <label className="block text-sm font-bold text-gray-700 mb-2">Phòng ban</label>
                      <input
                        readOnly
                        value="Phòng Hợp tác Quốc tế (IC) — tự động gán"
                        className="w-full px-4 py-2.5 rounded-xl border border-gray-200 bg-gray-100 text-sm text-gray-500 cursor-not-allowed outline-none"
                      />
                    </div>
                  )}

                  {/* UC-96-SL: Department Leader may optionally lead a department in the campus. */}
                  {isStaffLeader && manualForm.role === 'DEPARTMENT' && (
                    <div>
                      <label className="block text-sm font-bold text-gray-700 mb-2">Phòng ban (Trưởng phòng) <span className="text-red-500">*</span></label>
                      <select
                        value={selectedDept}
                        onChange={(e) => setSelectedDept(e.target.value)}
                        className="w-full px-4 py-2.5 rounded-xl border border-gray-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none transition-shadow text-sm bg-gray-50 hover:bg-gray-100 cursor-pointer"
                      >
                        <option value="">-- Chọn phòng ban --</option>
                        {campusDepartments.map((d) => (
                          <option key={d.departmentId} value={d.departmentId} disabled={d.hasHead}>
                            {d.name}{d.hasHead ? ' (đã có trưởng phòng)' : ''}
                          </option>
                        ))}
                      </select>
                      <p className="mt-1.5 text-xs text-gray-500">Tài khoản này sẽ được gán làm trưởng phòng của phòng ban đã chọn. {campusDepartments.length === 0 && 'Cơ sở của bạn hiện chưa có phòng ban phù hợp.'}</p>
                    </div>
                  )}

                  <div className="grid grid-cols-2 gap-5 mt-6">
                    <div className="col-span-2">
                      <div className="flex items-center justify-between mb-4">
                        <h4 className="text-sm font-bold text-[#004c91] flex items-center gap-2 uppercase tracking-wide">
                          <UserCircle className="w-5 h-5" />
                          Thông tin chung
                        </h4>
                        <div className="h-px bg-gray-200 flex-1 ml-4 hidden md:block"></div>
                      </div>
                    </div>
                    <div>
                      <label htmlFor="create-full-name" className="block text-sm font-bold text-gray-700 mb-2">Họ và tên <span className="text-red-500">*</span></label>
                      <input
                        id="create-full-name"
                        type="text"
                        value={manualForm.name}
                        maxLength={ACCOUNT_FULL_NAME_MAX_LENGTH}
                        autoComplete="name"
                        disabled={creating}
                        aria-invalid={!!createFieldErrors.fullName}
                        aria-describedby={createFieldErrors.fullName ? 'create-full-name-error' : undefined}
                        onChange={(e) => {
                          setManualForm({ ...manualForm, name: e.target.value });
                          // Clear as soon as the user starts fixing it; re-checked on blur/submit.
                          setCreateFieldErrors((prev) => ({ ...prev, fullName: undefined }));
                        }}
                        onBlur={(e) => setCreateFieldErrors((prev) => ({
                          ...prev,
                          fullName: validateFullName(e.target.value) ?? undefined,
                        }))}
                        placeholder="Trần Văn C"
                        className={createInputClass(!!createFieldErrors.fullName)}
                      />
                      {createFieldErrors.fullName && (
                        <p id="create-full-name-error" className="mt-1.5 text-sm text-red-600 font-medium">{createFieldErrors.fullName}</p>
                      )}
                    </div>
                    <div>
                      <label htmlFor="create-email" className="block text-sm font-bold text-gray-700 mb-2">Email (Tên đăng nhập) <span className="text-red-500">*</span></label>
                      <input
                        id="create-email"
                        type="email"
                        value={manualForm.email}
                        maxLength={ACCOUNT_EMAIL_MAX_LENGTH}
                        autoComplete="email"
                        inputMode="email"
                        disabled={creating}
                        aria-invalid={!!createFieldErrors.email}
                        aria-describedby={createFieldErrors.email ? 'create-email-error' : undefined}
                        onChange={(e) => {
                          setManualForm({ ...manualForm, email: e.target.value });
                          setCreateFieldErrors((prev) => ({ ...prev, email: undefined }));
                        }}
                        onBlur={(e) => setCreateFieldErrors((prev) => ({
                          ...prev,
                          email: validateAccountEmail(e.target.value) ?? undefined,
                        }))}
                        placeholder="example@gmail.com"
                        className={createInputClass(!!createFieldErrors.email)}
                      />
                      {createFieldErrors.email && (
                        <p id="create-email-error" className="mt-1.5 text-sm text-red-600 font-medium">{createFieldErrors.email}</p>
                      )}
                      <p className="mt-1.5 text-xs text-gray-500">Chỉ chấp nhận @gmail.com và @fpt.edu.vn.</p>
                    </div>

                    {/* PHẦN B — MSSV bắt buộc khi tạo tài khoản STUDENT (spec §5.2). */}
                    {manualForm.role === 'STUDENT' && (
                      <div className="col-span-2">
                        <label className="block text-sm font-bold text-gray-700 mb-2">
                          Mã số sinh viên (MSSV) <span className="text-red-500">*</span>
                        </label>
                        <input
                          type="text"
                          value={manualForm.studentCode}
                          maxLength={30}
                          placeholder="Ví dụ: SE123456"
                          disabled={creating}
                          onChange={(e) => { setManualForm({ ...manualForm, studentCode: e.target.value }); setCreateStudentCodeError(null); }}
                          className="w-full px-4 py-2.5 rounded-xl border border-gray-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none transition-shadow text-sm bg-white placeholder:text-slate-400 disabled:bg-slate-100 disabled:text-slate-500 disabled:border-slate-200 disabled:cursor-not-allowed"
                        />
                        {createStudentCodeError && (
                          <p className="mt-1.5 text-sm text-red-600 font-medium">{createStudentCodeError}</p>
                        )}
                      </div>
                    )}
                  </div>
                </div>
            </div>

            {/* Footer */}
            <div className="px-6 py-4 border-t border-gray-100 bg-gray-50 rounded-b-2xl">
              {createError && (
                <div className="mb-3 rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-bold text-red-700">
                  {createError}
                </div>
              )}
              <div className="flex items-center justify-end gap-3">
                <button
                  onClick={() => { setIsCreateModalOpen(false); setCreateError(null); setCreateStudentCodeError(null); setCreateFieldErrors({}); }}
                  className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-100 transition-colors outline-none"
                >
                  Hủy bỏ
                </button>
                <button
                  type="button"
                  onClick={handleContinueCreateAccount}
                  disabled={
                    creating ||
                    createIdentityInvalid ||
                    (isHO && manualForm.role === 'STAFF' && !!createCampus &&
                      (slAvailabilityLoading || (!!slAvailability && !slAvailability.canCreateStaffLeader))) ||
                    (isHO && manualForm.role === 'HO' && !!createCampus &&
                      (hoCampusCheckLoading || (!!hoCampusCheck && !hoCampusCheck.canCreateHo)))
                  }
                  className="px-5 py-2.5 rounded-xl font-bold text-white bg-orange-500 hover:bg-orange-600 shadow-[0_4px_12px_rgba(249,115,22,0.2)] hover:shadow-[0_6px_16px_rgba(249,115,22,0.4)] transition-all outline-none transform hover:-translate-y-0.5 disabled:opacity-60 disabled:cursor-not-allowed"
                >
                  Tiếp tục
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Create-confirmation step (spec §6/§7/§12/§17) — nested above the create form (z-[70] > z-50),
          overlay is NOT click-to-close so the review can't be dismissed by accident. */}
      {isCreateConfirmOpen && pendingCreateSummary && (
        <div
          className="fixed inset-0 bg-black/50 backdrop-blur-sm z-[70] flex items-center justify-center p-4 sm:p-6 animate-in fade-in duration-200"
          role="dialog"
          aria-modal="true"
          aria-labelledby="create-confirm-title"
        >
          <div className="bg-white rounded-2xl w-full max-w-lg shadow-xl overflow-hidden animate-in zoom-in-95 duration-300 flex flex-col max-h-[90vh]">
            {/* Header */}
            <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between bg-[#004c91] shrink-0">
              <h2 id="create-confirm-title" className="text-lg font-black text-white uppercase tracking-wide">
                Xác nhận thông tin tài khoản
              </h2>
              <button
                type="button"
                onClick={backToEditFromConfirm}
                disabled={creating}
                aria-label="Quay lại chỉnh sửa"
                className="w-8 h-8 rounded-full hover:bg-white/20 flex items-center justify-center text-white transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Body */}
            <div className="p-6 overflow-y-auto space-y-4">
              <dl className="space-y-3 text-sm">
                <div className="flex flex-col gap-0.5">
                  <dt className="text-xs font-bold uppercase tracking-wide text-slate-500">Họ và tên</dt>
                  <dd className="text-slate-900 font-semibold break-words">{pendingCreateSummary.fullName}</dd>
                </div>
                <div className="flex flex-col gap-0.5">
                  <dt className="text-xs font-bold uppercase tracking-wide text-slate-500">Vai trò</dt>
                  <dd className="text-slate-900 font-semibold break-words">{pendingCreateSummary.roleDisplayName}</dd>
                </div>
                {pendingCreateSummary.campusDisplayName && (
                  <div className="flex flex-col gap-0.5">
                    <dt className="text-xs font-bold uppercase tracking-wide text-slate-500">Cơ sở</dt>
                    <dd className="text-slate-900 font-semibold break-words">{pendingCreateSummary.campusDisplayName}</dd>
                  </div>
                )}
                {pendingCreateSummary.departmentDisplayName && (
                  <div className="flex flex-col gap-0.5">
                    <dt className="text-xs font-bold uppercase tracking-wide text-slate-500">Phòng ban</dt>
                    <dd className="text-slate-900 font-semibold break-words">{pendingCreateSummary.departmentDisplayName}</dd>
                  </div>
                )}
                {pendingCreateSummary.studentCode && (
                  <div className="flex flex-col gap-0.5">
                    <dt className="text-xs font-bold uppercase tracking-wide text-slate-500">Mã số sinh viên</dt>
                    <dd className="text-slate-900 font-semibold break-words">{pendingCreateSummary.studentCode}</dd>
                  </div>
                )}
                {pendingCreateSummary.phone && (
                  <div className="flex flex-col gap-0.5">
                    <dt className="text-xs font-bold uppercase tracking-wide text-slate-500">Số điện thoại</dt>
                    <dd className="text-slate-900 font-semibold break-words">{pendingCreateSummary.phone}</dd>
                  </div>
                )}
              </dl>

              {/* Highlighted email block (spec §7) — amber warning (not an error), never truncated,
                  wraps safely, and carries a check-your-email reminder (not color alone). */}
              <div className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3.5">
                <p className="text-xs font-black uppercase tracking-wide text-amber-700">Email đăng nhập</p>
                <p className="mt-1.5 text-base font-bold text-[#004c91] break-words">{pendingCreateSummary.email}</p>
                <p className="mt-2 text-xs text-slate-600 leading-relaxed">
                  Hãy kiểm tra kỹ địa chỉ email này. Thông báo tài khoản và quyền đăng nhập sẽ được gửi tới địa chỉ trên.
                </p>
              </div>
            </div>

            {/* Footer — exactly two actions (spec §6.3). Both locked while a request is in flight. */}
            <div className="px-6 py-4 border-t border-slate-200 bg-slate-50 rounded-b-2xl shrink-0 flex items-center justify-end gap-3">
              <button
                type="button"
                onClick={backToEditFromConfirm}
                disabled={creating}
                className="px-5 py-2.5 rounded-xl font-bold text-slate-600 bg-white border border-slate-200 hover:bg-slate-100 transition-colors outline-none disabled:opacity-50 disabled:cursor-not-allowed"
              >
                Quay lại chỉnh sửa
              </button>
              <button
                ref={confirmCreateBtnRef}
                type="button"
                onClick={confirmCreateAccount}
                disabled={creating}
                className="px-5 py-2.5 rounded-xl font-bold text-white bg-orange-500 hover:bg-orange-600 shadow-[0_4px_12px_rgba(249,115,22,0.2)] hover:shadow-[0_6px_16px_rgba(249,115,22,0.4)] transition-all outline-none disabled:opacity-60 disabled:cursor-not-allowed inline-flex items-center gap-2"
              >
                {creating && <RefreshCw className="w-4 h-4 animate-spin" />}
                {creating ? 'Đang tạo...' : 'Xác nhận tạo tài khoản'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* UC-97: Xác nhận kích hoạt / vô hiệu hóa tài khoản. Trạng thái hiện tại quyết định hướng đi —
          chỉ ACTIVE mới là "vô hiệu hóa", mọi trạng thái khác vào đây đều là "kích hoạt lại". */}
      {statusTarget && (
        <AccountStatusConfirmModal
          account={statusTarget}
          action={statusTarget.status === 'Active' ? 'disable' : 'enable'}
          submitting={statusSaving}
          error={statusError}
          onCancel={() => setStatusTarget(null)}
          onConfirm={() => void confirmToggleStatus()}
        />
      )}

      {/* HO_BASIC_INFO §10 — xác nhận đổi email đăng nhập (thu hồi phiên + liên kết lại SSO/FEID). */}
      {basicInfoEmailConfirm && (
        <LoginEmailChangeConfirmModal
          oldEmail={basicInfoEmailConfirm.oldEmail}
          newEmail={basicInfoEmailConfirm.newEmail}
          submitting={roleSaving}
          error={roleError}
          onCancel={() => setBasicInfoEmailConfirm(null)}
          // Whose submit this is follows from who opened it: HO edits basic info through its own
          // endpoint, everyone else sends role and identity together in one updateAccountRole call.
          onConfirm={() => void (isHO ? submitBasicInfo() : submitRoleUpdate())}
        />
      )}

      {/* Pending account — xác nhận đổi email + phát hành lại liên kết kích hoạt. Tách khỏi hộp
          thoại trên vì hệ quả khác hẳn: liên kết cũ chết, liên kết mới gửi tới địa chỉ mới, và tài
          khoản vẫn chờ xác nhận cho tới khi người nhận bấm vào đó. */}
      {pendingEmailEditConfirm && (
        <PendingEmailEditConfirmModal
          oldEmail={pendingEmailEditConfirm.oldEmail}
          newEmail={pendingEmailEditConfirm.newEmail}
          submitting={roleSaving}
          error={roleError}
          onCancel={() => setPendingEmailEditConfirm(null)}
          // Same split as above. A Staff Leader may be changing the role in the same breath, so their
          // path must stay the single atomic call — never edit-pending-email followed by a role update.
          onConfirm={() => void (isHO ? submitPendingEmailEdit() : submitRoleUpdate())}
        />
      )}

      {/* ADMIN LOCK/UNLOCK — flow riêng với lý do bắt buộc khi khóa */}
      {lockTarget && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-[60] flex items-center justify-center p-4 sm:p-6 animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl w-full max-w-md shadow-xl overflow-hidden animate-in zoom-in-95 duration-300 relative">
            <div className="px-6 py-4 border-b border-gray-100 flex items-center gap-2">
              <h2 className="text-xl font-black text-gray-800">
                {lockTarget.status === 'Locked' ? '🔓 Mở khóa tài khoản' : '🔒 Khóa tài khoản (bảo mật)'}
              </h2>
              <button
                onClick={() => setLockTarget(null)}
                className="absolute top-4 right-4 w-8 h-8 rounded-full hover:bg-gray-100 flex items-center justify-center text-gray-500 transition-colors outline-none"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-6 text-gray-700 leading-relaxed text-[15px]">
              {lockTarget.status === 'Locked' ? (
                <>Mở khóa tài khoản <strong className="text-[#004c91]">{lockTarget.email}</strong>? Tài khoản sẽ trở lại trạng thái <strong className="text-[#0aa14f]">ACTIVE</strong> và người dùng có thể đăng nhập lại.</>
              ) : (
                <>Khóa tài khoản <strong className="text-[#004c91]">{lockTarget.email}</strong> vì lý do bảo mật? Toàn bộ phiên đăng nhập sẽ bị thu hồi ngay lập tức và tài khoản không thể đăng nhập cho đến khi được mở khóa.</>
              )}
              <div className="mt-4">
                <label className="block text-[10px] font-bold uppercase tracking-wider mb-1 text-gray-500">
                  Lý do {lockTarget.status === 'Locked' ? '(tùy chọn)' : '(bắt buộc)'}
                </label>
                <textarea
                  rows={2}
                  value={lockReason}
                  onChange={(e) => setLockReason(e.target.value)}
                  placeholder="VD: Nghi ngờ lộ mật khẩu / đăng nhập bất thường..."
                  className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm font-medium text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#004c91] bg-gray-50 focus:bg-white transition-all resize-none"
                />
              </div>
              {lockError && (
                <div className="mt-4 rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-bold text-red-700">
                  {lockError}
                </div>
              )}
            </div>

            <div className="px-6 py-4 border-t border-gray-100 bg-gray-50 flex items-center justify-end gap-3 rounded-b-2xl">
              <button
                onClick={() => setLockTarget(null)}
                className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-100 transition-colors outline-none"
              >
                Hủy
              </button>
              <button
                onClick={confirmLockToggle}
                disabled={lockSaving}
                className={`px-5 py-2.5 rounded-xl font-bold text-white shadow-sm transition-all outline-none disabled:opacity-60 disabled:cursor-not-allowed ${lockTarget.status === 'Locked' ? 'bg-[#0aa14f] hover:bg-[#088c44]' : 'bg-red-500 hover:bg-red-600'}`}
              >
                {lockSaving ? 'Đang xử lý...' : lockTarget.status === 'Locked' ? 'Mở khóa' : 'Khóa tài khoản'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Toast notifications (create + email outcome, status, role update) */}
      {toasts.length > 0 && (
        <div className="fixed top-6 right-6 z-[100] flex flex-col gap-3 w-[360px] max-w-[calc(100vw-2rem)]">
          {toasts.map((t) => (
            <div
              key={t.id}
              className={`flex items-start gap-3 rounded-2xl border px-4 py-3 shadow-lg animate-in slide-in-from-right-4 duration-300 ${
                t.type === 'success' ? 'bg-emerald-50 border-emerald-200 text-emerald-800' :
                t.type === 'warning' ? 'bg-amber-50 border-amber-200 text-amber-800' :
                'bg-red-50 border-red-200 text-red-800'
              }`}
            >
              {t.type === 'success' ? <CheckCircle className="w-5 h-5 shrink-0 mt-0.5" /> : <XCircle className="w-5 h-5 shrink-0 mt-0.5" />}
              <span className="text-sm font-bold leading-snug">{t.msg}</span>
              <button
                onClick={() => setToasts((prev) => prev.filter((x) => x.id !== t.id))}
                className="ml-auto opacity-60 hover:opacity-100 transition-opacity outline-none"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
          ))}
        </div>
      )}

      {/* Replace Staff Leader modal (HO only) */}
      {replaceLeaderTarget && (
        <ReplaceStaffLeaderModal
          campusId={replaceLeaderTarget.campusId}
          campusName={replaceLeaderTarget.campusName}
          onClose={() => setReplaceLeaderTarget(null)}
          onReplaced={(result) => {
            setReplaceLeaderTarget(null);
            pushToast(
              result.emailNotificationStatus === 'FAILED' ? 'warning' : 'success',
              result.emailNotificationStatus === 'FAILED'
                ? 'Đã thay thế Staff Leader nhưng gửi email thông báo thất bại.'
                : 'Đã thay thế Staff Leader thành công.',
            );
            refetchAccounts();
            loadStatistics();
          }}
        />
      )}
    </div>
  );
}

