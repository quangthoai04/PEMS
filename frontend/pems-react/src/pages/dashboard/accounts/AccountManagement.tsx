/**
 * Trang AccountManagement
 * Giao diện quản trị để quản lý tài khoản người dùng.
 * Bao gồm các chức năng phân trang, tìm kiếm, chỉnh sửa và tạo mới tài khoản.
 */

import React, { useState, useEffect, useRef, useMemo, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Users, UserCheck, UserX, Clock, Search, MapPin,
  Shield, CheckCircle, XCircle, MoreVertical, Eye,
  Edit, Key, RefreshCw, Plus, X, UserCog, Briefcase, GraduationCap,
  ChevronLeft, ChevronRight, ChevronDown, ChevronUp, UserCircle
} from 'lucide-react';
import { useDebounce } from '../../../shared/hooks/useDebounce';
import { useAccountList } from '../../../features/account-management/hooks/useAccountList';
import { useAccountManagement } from '../../../features/account-management/hooks/useAccountManagement';
import { accountManagementApi } from '../../../features/account-management/api/accountManagementApi';
import { getAccountErrorMessage } from '../../../features/account-management/api/accountError';
import type {
  AccountListItem,
  AccountListQueryParams,
  AccountStatistics,
  ActiveCampusOption,
  CampusDepartmentOption,
  HoCampusCheck,
  RoleAssignmentOptions,
  StaffLeaderAvailability,
} from '../../../features/account-management/types/accountManagement.types';
import { ReplaceStaffLeaderModal } from '../../../features/account-management/components/ReplaceStaffLeaderModal';
import { RelatedVisitorsTab } from '../../../features/account-management/components/RelatedVisitorsTab';

const CAMPUSES = ["Hà Nội", "Hồ Chí Minh", "Đà Nẵng", "Cần Thơ", "Quy Nhơn"];
const ROLES = ["ADMIN", "HO", "STAFF", "DEPARTMENT", "STUDENT", "VISITOR"];

// UC-100-SL — isolated role-edit state (never mutates the account-detail snapshot).
// departmentId/studentCode are the dependent fields for DEPARTMENT / STUDENT respectively.
// fullName/email are the identity fields, editable only for eligible targets (see canEditIdentity).
interface RoleEditForm {
  roleCode: string;
  departmentId: string;
  studentCode: string;
  fullName: string;
  email: string;
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
  const [allFilters, setAllFilters] = useState({ search: "", campus: defaultCampus, role: "", status: "" });
  const [pendingFilters, setPendingFilters] = useState({ search: "", campus: defaultCampus, role: "", status: "" });
  
  const [activeTab, setActiveTab] = useState<'all' | 'pending'>('all');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
  // Staff Leader top-level view switch: internal accounts vs the read-only "Visitor liên quan" tab.
  const [slView, setSlView] = useState<'internal' | 'visitors'>('internal');

  const currentFilters = activeTab === 'all' ? allFilters : pendingFilters;
  const setCurrentFilters = activeTab === 'all' ? setAllFilters : setPendingFilters;

  const searchQuery = currentFilters.search;
  const campusFilter = currentFilters.campus;
  const roleFilter = currentFilters.role;
  const statusFilter = currentFilters.status;

  const setSearchQuery = (val: string) => setCurrentFilters(prev => ({ ...prev, search: val }));
  const setCampusFilter = (val: string) => setCurrentFilters(prev => ({ ...prev, campus: val }));
  const setRoleFilter = (val: string) => setCurrentFilters(prev => ({ ...prev, role: val }));
  const setStatusFilter = (val: string) => setCurrentFilters(prev => ({ ...prev, status: val }));

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
  };

  const closeViewDrawer = () => {
    setIsViewDrawerOpen(false);
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
    });
    setEditForm({ role: roleCode });
    setIsEditingProfile(true);
    // Staff Leader flow needs the campus-scoped department options; ADMIN keeps the legacy path.
    if (isStaffLeader) loadRoleOptions(selectedAccount.userId ?? selectedAccount.id);
  };

  useEffect(() => {
    setCurrentPage(1);
  }, [activeTab, searchQuery, campusFilter, roleFilter, statusFilter]);

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

    const status = statusFilter === 'Active'
      ? 'ACTIVE'
      : statusFilter === 'Inactive'
        ? 'INACTIVE'
        : statusFilter === 'Locked'
          ? 'LOCKED'
          : undefined;

    return {
      page: currentPage,
      pageSize,
      keyword: debouncedSearch.trim() || undefined,
      roleCode: roleFilter || undefined,
      status,
      campusId,
      sortBy: 'createdAt',
      sortDirection: 'desc',
    };
  }, [isHO, isRealAdmin, campusFilter, campusOptions, statusFilter, currentPage, pageSize, debouncedSearch, roleFilter]);

  const {
    data: accountsData,
    loading: accountsLoading,
    error: accountsError,
    refetch: refetchAccounts,
  } = useAccountList(listParams, activeTab === 'all');

  // UC-97 / UC-98 mutations + detail fetch (create/update-role call the API directly
  // so the modal can surface the backend's specific error message).
  const { manageAccountStatus, getAccountDetails } = useAccountManagement();

  // UC-96 create modal feedback.
  const [createError, setCreateError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

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
  const [roleError, setRoleError] = useState<string | null>(null);
  // HO_BASIC_INFO — email-change confirmation (spec §10). Set to {oldEmail,newEmail} to prompt.
  const [basicInfoEmailConfirm, setBasicInfoEmailConfirm] = useState<{ oldEmail: string; newEmail: string } | null>(null);

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
      status: a.status === 'ACTIVE' ? 'Active' : a.status === 'INACTIVE' ? 'Inactive' : 'Locked',
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
  
  const campusThemes = [
    { borderTop: "border-t-[#004c91]", iconText: "text-[#004c91]", iconBg: "bg-blue-50", glow: "from-blue-50/50" },
    { borderTop: "border-t-[#f37021]", iconText: "text-[#f37021]", iconBg: "bg-orange-50", glow: "from-orange-50/50" },
    { borderTop: "border-t-[#0aa14f]", iconText: "text-[#0aa14f]", iconBg: "bg-emerald-50", glow: "from-emerald-50/50" },
    { borderTop: "border-t-[#b32d2e]", iconText: "text-[#b32d2e]", iconBg: "bg-red-50", glow: "from-red-50/50" },
    { borderTop: "border-t-[#6b21a8]", iconText: "text-[#6b21a8]", iconBg: "bg-purple-50", glow: "from-purple-50/50" },
  ];

  const hoStats = CAMPUSES.map((campus, index) => {
    const theme = campusThemes[index % campusThemes.length];
    return {
      isHOStyle: true,
      campus: campus,
      label: `Cơ sở ${campus}`, 
      value: accounts.filter(a => a.campus === campus).length.toString(), 
      icon: MapPin,
      theme: theme,
      onClick: () => { setActiveTab('all'); setAllFilters(prev => ({ ...prev, status: '', role: '', search: '', campus: campus })); scrollToTable(); } 
    };
  });

  // UC-95-SL stat cards — campus-scoped totals from the statistics API.
  const slStats = [
    { label: "Tổng số tài khoản", value: (statistics?.totalAccounts ?? 0).toString(), icon: Users, color: "text-[#004c91]", bg: "bg-white border-gray-100 shadow-sm outline-none", iconBg: "bg-blue-50", onClick: () => { setActiveTab('all'); setAllFilters(prev => ({ ...prev, status: '', role: '', search: '' })); scrollToTable(); } },
    { label: "Tài khoản hoạt động", value: (statistics?.activeAccounts ?? 0).toString(), icon: UserCheck, color: "text-[#0aa14f]", bg: "bg-white border-gray-100 shadow-sm outline-none", iconBg: "bg-green-50", onClick: () => { setActiveTab('all'); setAllFilters(prev => ({ ...prev, status: 'Active' })); scrollToTable(); } },
    { label: "Tài khoản vô hiệu hóa", value: (statistics?.inactiveAccounts ?? 0).toString(), icon: UserX, color: "text-amber-500", bg: "bg-white border-gray-100 shadow-sm outline-none", iconBg: "bg-amber-50", onClick: () => { setActiveTab('all'); setAllFilters(prev => ({ ...prev, status: 'Inactive' })); scrollToTable(); } },
    { label: "Tài khoản bị khóa", value: (statistics?.lockedAccounts ?? 0).toString(), icon: XCircle, color: "text-red-500", bg: "bg-white border-gray-100 shadow-sm outline-none", iconBg: "bg-red-50", onClick: () => { setActiveTab('all'); setAllFilters(prev => ({ ...prev, status: 'Locked' })); scrollToTable(); } },
  ];

  // ADMIN dùng chung bộ card thống kê thật từ /accounts/statistics như Staff Leader
  // (backend trả số toàn hệ thống cho ADMIN) — không dùng số đếm từ trang hiện tại.
  const stats = isHO
    ? hoStats
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

  const totalItems = isServerTab ? (accountsData?.totalItems ?? 0) : sortedPending.length;
  const totalPages = isServerTab
    ? (accountsData?.totalPages ?? 0)
    : Math.ceil(sortedPending.length / pageSize);
  const paginatedAccounts = isServerTab
    ? accounts
    : sortedPending.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  const getRoleStyle = (role: string) => {
    switch(role.toUpperCase()) {
      case 'ADMIN': return 'bg-red-50 text-red-700 border-red-200';
      case 'HO': return 'bg-blue-50 text-blue-700 border-blue-200';
      case 'DEPARTMENT': return 'bg-orange-50 text-orange-700 border-orange-200';
      case 'STAFF': return 'bg-emerald-50 text-emerald-700 border-emerald-200';
      case 'STUDENT': return 'bg-purple-50 text-purple-700 border-purple-200';
      case 'VISITOR': return 'bg-pink-50 text-pink-700 border-pink-200';
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
    setSelectedAccount(acc);
    setIsViewDrawerOpen(true);
    if (!isServerTab) return;
    const details = await getAccountDetails(acc.userId ?? acc.id);
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
    }));
  };

  // UC-96 — create a new account. HO → HO/Staff Leader; Staff Leader → STAFF/STAFF,
  // DEPARTMENT/LEADER (optional department) or STUDENT. The backend derives sub-role and
  // forces campus; we only collect what the role needs.
  const handleCreateAccount = async () => {
    setCreateError(null);
    setCreateStudentCodeError(null);
    const role = manualForm.role;
    if (!role) { setCreateError('Vui lòng chọn vai trò.'); return; }
    if (isHO && !createCampus) { setCreateError('Vui lòng chọn cơ sở.'); return; }
    if (isRealAdmin && ['HO', 'STUDENT'].includes(role) && !createCampus) {
      setCreateError('Vui lòng chọn cơ sở cho vai trò này.');
      return;
    }
    if (!manualForm.name.trim()) { setCreateError('Vui lòng nhập họ và tên.'); return; }
    const email = manualForm.email.trim();
    if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      setCreateError('Vui lòng nhập email hợp lệ.');
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

    setCreating(true);
    try {
      const result = await accountManagementApi.createAccount({
        roleCode: role as any,
        fullName: manualForm.name.trim(),
        email,
        phone: manualForm.phone || null,
        // The create form has no gender field; the column is an ENUM
        // (MALE/FEMALE/OTHER/UNKNOWN), so leave it null rather than sending a label.
        gender: null,
        primaryCampusId: primaryCampusId ?? null,
        departmentId,
        // MSSV only for STUDENT; null otherwise so no hidden code is ever sent (spec §5.6).
        studentCode: role === 'STUDENT' ? studentCode : null,
      });

      // UC-96 requirement: toast for the email-notification outcome.
      if (result.emailNotificationStatus === 'SENT')
        pushToast('success', `Đã tạo tài khoản ${result.email} và gửi email thông báo thành công.`);
      else
        pushToast('warning', `Đã tạo tài khoản ${result.email} nhưng gửi email thông báo thất bại.`);

      setIsCreateModalOpen(false);
      setManualForm({ role: '', name: '', email: '', phone: '', gender: 'Nam', studentCode: '' });
      setCreateCampus('');
      setSelectedDept('');
      refetchAccounts();
      loadStatistics();
    } catch (err) {
      const msg = getAccountErrorMessage(err, 'Không thể tạo tài khoản. Vui lòng thử lại.');
      // A duplicate MSSV maps to its own field (modal stays open, other inputs kept — spec §5.8).
      if (role === 'STUDENT' && /mã số sinh viên/i.test(msg)) {
        setCreateStudentCodeError(msg);
      } else {
        setCreateError(msg);
      }
      pushToast('error', msg);
    } finally {
      setCreating(false);
    }
  };

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
    // Identity changes (normalized) also count, but only for editable targets.
    (canEditIdentity && roleEditForm.fullName.trim() !== (selectedAccount.name ?? '')) ||
    (canEditIdentity && roleEditForm.email.trim().toLowerCase() !== String(selectedAccount.email ?? '').toLowerCase())
  );

  // Staff-Leader submit gate: options must be loaded/valid and the role's required field present.
  const roleUpdateBlocked = !!roleEditForm && isStaffLeader && (
    roleOptionsLoading ||
    !!roleOptionsError ||
    (roleEditForm.roleCode === 'STAFF' && !roleOptions?.icDepartment) ||
    (roleEditForm.roleCode === 'DEPARTMENT' && !roleEditForm.departmentId) ||
    (roleEditForm.roleCode === 'STUDENT' && roleEditForm.studentCode.trim().length === 0) ||
    // Identity fields (when editable) must be non-empty and the email well-formed.
    (canEditIdentity && roleEditForm.fullName.trim().length === 0) ||
    (canEditIdentity && roleEditForm.email.trim().length === 0) ||
    (canEditIdentity && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(roleEditForm.email.trim()))
  );

  // HO basic-info submit gate (spec §7.1/§8): identity required + well-formed email. Applies to any
  // editable-identity target (covers the HO flow where roleUpdateBlocked's isStaffLeader guard is off).
  const identityInvalid = canEditIdentity && !!roleEditForm && (
    roleEditForm.fullName.trim().length === 0 ||
    roleEditForm.fullName.trim().length > 150 ||
    roleEditForm.email.trim().length === 0 ||
    roleEditForm.email.trim().length > 150 ||
    !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(roleEditForm.email.trim())
  );

  // Switch the target role and reset the dependent fields (spec §3.3/§3.4/§3.5 — always fresh on a
  // genuine role change; the original values are only preserved by handleEditClick on first open).
  const changeRoleCode = (nextRole: string) => {
    setRoleError(null);
    // Reset the role-dependent fields, but keep the identity fields — their editability is fixed by
    // the ORIGINAL target role and does not change with the dropdown (spec §4.2.1 / §4.7).
    setRoleEditForm((prev) => (prev
      ? { roleCode: nextRole, departmentId: '', studentCode: '', fullName: prev.fullName, email: prev.email }
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
    const fullName = roleEditForm.fullName.trim();
    const email = roleEditForm.email.trim();
    if (!fullName) { setRoleError('Vui lòng nhập họ và tên.'); return; }
    if (fullName.length > 150) { setRoleError('Họ và tên không được vượt quá 150 ký tự.'); return; }
    if (!email) { setRoleError('Vui lòng nhập email.'); return; }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) { setRoleError('Email không đúng định dạng.'); return; }
    if (email.length > 150) { setRoleError('Email không được vượt quá 150 ký tự.'); return; }

    const oldEmail = String(selectedAccount.email ?? '');
    const emailChanged = email.toLowerCase() !== oldEmail.trim().toLowerCase();
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
        fullName: roleEditForm.fullName.trim(),
        email: roleEditForm.email.trim(),
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
      setRoleError(msg);
      pushToast('error', msg);
    } finally {
      setRoleSaving(false);
    }
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
    }

    // ADMIN keeps the legacy behaviour: role-only change, original department preserved.
    const outgoingDepartmentId = roleCode === 'DEPARTMENT'
      ? (isStaffLeader ? departmentId : (selectedAccount.departmentId ?? null))
      : null;
    const outgoingStudentCode = roleCode === 'STUDENT'
      ? (isStaffLeader ? studentCode.trim() : null)
      : null;

    setRoleSaving(true);
    try {
      await accountManagementApi.updateAccountRole({
        userId: (selectedAccount.userId ?? selectedAccount.id) as any,
        newRoleCode: roleCode as any,
        departmentId: outgoingDepartmentId as any,
        studentCode: outgoingStudentCode,
        // Identity is only sent for editable targets; otherwise leave null so the backend keeps it.
        fullName: canEditIdentity ? roleEditForm.fullName.trim() : null,
        email: canEditIdentity ? roleEditForm.email.trim() : null,
      });
      pushToast('success', 'Cập nhật tài khoản thành công. Đã gửi email thông báo cho người dùng.');
      closeViewDrawer();
      refetchAccounts();
      loadStatistics();
    } catch (err) {
      const msg = getAccountErrorMessage(err, 'Không thể cập nhật vai trò. Vui lòng kiểm tra lại dữ liệu và thử lại.');
      setRoleError(msg);
      pushToast('error', msg);
    } finally {
      setRoleSaving(false);
    }
  };

  // Staff-Leader-only top-level tabs: internal accounts vs the read-only "Visitor liên quan" tab.
  const staffLeaderTabs = isStaffLeader ? (
    <div className="flex gap-1 mb-8 bg-white rounded-2xl p-1.5 border border-gray-100 shadow-sm w-fit">
      <button
        onClick={() => setSlView('internal')}
        className={`px-5 py-2.5 rounded-xl font-bold text-sm transition-colors outline-none ${slView === 'internal' ? 'bg-[#004c91] text-white shadow-sm' : 'text-gray-500 hover:text-[#004c91]'}`}
      >
        Tài khoản nội bộ
      </button>
      <button
        onClick={() => setSlView('visitors')}
        className={`px-5 py-2.5 rounded-xl font-bold text-sm transition-colors outline-none ${slView === 'visitors' ? 'bg-[#004c91] text-white shadow-sm' : 'text-gray-500 hover:text-[#004c91]'}`}
      >
        Tài khoản khách
      </button>
    </div>
  ) : null;

  // Read-only related-visitors view: a self-contained tab, scope resolved server-side.
  if (isStaffLeader && slView === 'visitors') {
    return (
      <div className="w-full pb-12 animate-in fade-in duration-300">
        <div className="flex items-center gap-2 text-sm font-medium text-gray-500 mb-6">
          <span>Dashboard</span>
          <span>/</span>
          <span className="text-[#004c91]">Quản lý tài khoản</span>
        </div>
        <div className="flex items-center justify-between mb-8">
          <div>
            <h1 className="text-3xl font-bold text-[#004c91]">Quản lý tài khoản</h1>
            <p className="text-gray-500 mt-1 font-medium">Danh sách tài khoản khách liên quan đến cơ sở của bạn</p>
          </div>
        </div>
        {staffLeaderTabs}
        <RelatedVisitorsTab />
      </div>
    );
  }

  return (
    <div className="w-full pb-12 animate-in fade-in duration-300">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm font-medium text-gray-500 mb-6">
        <span>Dashboard</span>
        <span>/</span>
        <span className="text-[#004c91]">Quản lý tài khoản</span>
      </div>

      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold text-[#004c91]">Quản lý tài khoản</h1>
          <p className="text-gray-500 mt-1 font-medium">Tổng quan hệ thống và phân quyền truy cập</p>
        </div>
      </div>

      {staffLeaderTabs}

      {/* I. Top Widgets */}
      {!isHO && (
        <div className={`grid grid-cols-1 sm:grid-cols-2 ${(isStaffLeader || isRealAdmin) ? 'lg:grid-cols-4' : 'lg:grid-cols-5'} gap-6 mb-8`}>
          {stats.map((stat: any, idx) => {
            const Icon = stat.icon;
            if (stat.isHOStyle) {
              return (
                <button 
                  key={idx} 
                  onClick={stat.onClick} 
                  className={`relative bg-white border border-slate-100 ${stat.theme.borderTop} border-t-4 rounded-2xl p-6 shadow-sm flex flex-col justify-between overflow-hidden group text-left w-full hover:-translate-y-1.5 hover:shadow-xl hover:shadow-slate-200/80 transition-all duration-300 ease-in-out focus:ring-2 focus:ring-[#004c91]/20 outline-none`}
                >
                  {/* Subtle Radial Glow */}
                  <div className={`absolute inset-0 bg-gradient-to-br ${stat.theme.glow} to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-300 pointer-events-none`}></div>
                  
                  {/* Top Row */}
                  <div className="flex items-center justify-between w-full mb-6 relative z-10">
                    <span className={`text-sm font-black ${stat.theme.iconText} uppercase tracking-widest`}>{stat.campus}</span>
                    <div className={`w-10 h-10 rounded-xl ${stat.theme.iconBg} flex items-center justify-center shrink-0 group-hover:scale-105 transition-transform duration-300 ease-out`}>
                      <Icon className={`w-5 h-5 ${stat.theme.iconText}`} />
                    </div>
                  </div>

                  {/* Bottom Row */}
                  <div className="relative z-10 flex items-baseline gap-2 mt-auto">
                    <h3 className={`text-4xl font-extrabold ${stat.theme.iconText} tracking-tight`}>{stat.value}</h3>
                    <span className="text-sm font-medium text-slate-500">Tài khoản</span>
                  </div>
                </button>
              );
            }

            return (
              <button key={idx} onClick={stat.onClick} className={`rounded-[2rem] p-6 border ${stat.bg} shadow-sm flex flex-col justify-between relative overflow-hidden group hover:shadow-[0_8px_30px_-4px_rgba(0,0,0,0.1)] transition-all duration-300 text-left w-full focus:ring-2 focus:ring-[#004c91]/20`}>
                <div className="flex justify-between items-start mb-4">
                  <div className={`w-12 h-12 rounded-2xl ${stat.iconBg} shadow-sm flex items-center justify-center shrink-0`}>
                    <Icon className={`w-6 h-6 ${stat.color}`} />
                  </div>
                </div>
                <div>
                  <h3 className={`text-3xl font-black ${stat.textColor || 'text-gray-900'} tracking-tight leading-none mb-2`}>{stat.value}</h3>
                  <p className={`text-[10px] sm:text-[11px] font-bold ${stat.labelColor || 'text-gray-500'} uppercase tracking-wide leading-tight`}>{stat.label}</p>
                </div>
              </button>
            );
          })}
        </div>
      )}

      <div className="flex justify-end mb-8">
        <button
          onClick={() => { setCreateError(null); setCreateStudentCodeError(null); setSelectedDept(''); setManualForm({ role: '', name: '', email: '', phone: '', gender: 'Nam', studentCode: '' }); setIsCreateModalOpen(true); }}
          className="bg-[#f37021] hover:bg-[#e85c0d] text-white px-6 py-3.5 rounded-2xl font-bold flex items-center gap-2 shadow-sm shadow-orange-500/20 transition-all hover:shadow-md hover:shadow-orange-500/40 outline-none"
        >
          + Tạo tài khoản mới
        </button>
      </div>

      {isServerTab && accountsError && (
        <div className="mb-6 rounded-2xl border border-red-200 bg-red-50 px-5 py-4 text-sm font-bold text-red-700 flex items-center gap-3">
          <XCircle className="w-5 h-5 shrink-0" />
          <span>{accountsError}</span>
        </div>
      )}

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

          <div className="relative">
            <select
              value={roleFilter}
              onChange={(e) => setRoleFilter(e.target.value)}
              className="px-4 py-3 pr-10 rounded-2xl border-none text-sm font-medium focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all min-w-[140px] bg-white/10 text-white shadow-inner appearance-none custom-select"
            >
              <option className="text-gray-900" value="">Tất cả Vai trò</option>
              {ROLES.filter(r => {
                if (isRealAdmin) return true; // ADMIN xem mọi role
                if (isHO) return ['HO', 'STAFF'].includes(r);
                if (isStaffLeader) return ['STAFF', 'DEPARTMENT', 'STUDENT'].includes(r);
                return r !== 'HO';
              }).map(r => <option className="text-gray-900" key={r} value={r}>{r}</option>)}
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
                              {(!isServerTab || acc.canManageStatus) ? (
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
                          ) : (!isServerTab || acc.canManageStatus) ? (
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
                  className={`w-8 h-8 rounded-lg text-sm font-bold transition-all outline-none ${currentPage === page ? 'bg-[#004c91] text-white shadow-sm' : 'text-gray-600 hover:bg-gray-200'}`}
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

                {/* Replace Staff Leader (HO only) — the HO list only shows HO + Staff Leaders, so a
                    STAFF row here is the campus IC Head. */}
                {isHO && selectedAccount.role === 'STAFF' && selectedAccount.campusId && (
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

                  // Read-only labelled field: renders the snapshot value, or "-" when empty
                  // (spec §3.1 — null/undefined/'' must show "-", never a fallback like "Nam").
                  const DisplayField = ({ label, value, highlight = false, colSpan = false }: any) => (
                    <div className={`flex flex-col min-w-0 ${colSpan ? 'md:col-span-2' : ''}`}>
                      <span className={`block text-[10px] font-bold uppercase tracking-wider mb-1 ${highlight ? 'text-[#004c91]/80' : 'text-gray-500'}`}>{label}</span>
                      <span className={`block text-sm p-2.5 rounded-lg border break-words ${highlight ? 'font-black text-[#004c91] bg-blue-50/30 border-blue-100' : 'font-bold text-gray-900 bg-gray-50/50 border-gray-100'}`}>
                        {value === null || value === undefined || value === '' ? '-' : value}
                      </span>
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
                    `px-3 py-2 pr-8 border rounded-lg text-sm font-medium text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#004c91] bg-gray-50 transition-all appearance-none w-full ${disabled ? 'opacity-70 cursor-not-allowed border-gray-200' : 'border-gray-200 focus:bg-white'}`;

                  // Gray, clearly-disabled styling for locked fields (spec §4.6).
                  const identityInputClass = (disabled: boolean) =>
                    `px-3 py-2 border rounded-lg text-sm font-medium text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#004c91] transition-all w-full ${
                      disabled
                        ? 'bg-slate-100 text-slate-500 border-slate-200 cursor-not-allowed'
                        : 'bg-white border-gray-200 focus:bg-white'
                    }`;

                  // Locked-target identity display value (snapshot); editable value comes from the form.
                  const identityDisabled = !canEditIdentity || roleSaving;

                  // ── Edit mode (Chỉnh sửa thông tin tài khoản): identity editable for eligible targets,
                  //    org fields locked, layout follows editRoleCode. Identity inputs are inline JSX
                  //    (not a nested component) so typing never remounts them / loses focus. ──
                  const editGrid = (
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-5 w-full">
                      <div className="flex flex-col min-w-0">
                        <span className="block text-[10px] font-bold uppercase tracking-wider mb-1 text-gray-500">
                          Họ và tên{canEditIdentity && <span className="ml-1 text-red-500">*</span>}
                        </span>
                        <input
                          type="text"
                          value={canEditIdentity ? (roleEditForm?.fullName ?? '') : (data.name ?? '')}
                          maxLength={150}
                          disabled={identityDisabled}
                          onChange={(e) => setRoleEditForm((prev) => (prev ? { ...prev, fullName: e.target.value } : prev))}
                          className={identityInputClass(identityDisabled)}
                        />
                      </div>
                      <div className="flex flex-col min-w-0">
                        <span className="block text-[10px] font-bold uppercase tracking-wider mb-1 text-gray-500">
                          Email{canEditIdentity && <span className="ml-1 text-red-500">*</span>}
                        </span>
                        <input
                          type="email"
                          value={canEditIdentity ? (roleEditForm?.email ?? '') : (data.email ?? '')}
                          maxLength={150}
                          disabled={identityDisabled}
                          onChange={(e) => setRoleEditForm((prev) => (prev ? { ...prev, email: e.target.value } : prev))}
                          className={identityInputClass(identityDisabled)}
                        />
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
                          <ChevronDown className={`w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none ${isHO ? 'text-slate-400' : 'text-gray-400'}`} />
                        </div>
                      </div>

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
                          <div className="flex flex-col min-w-0">
                            <span className="block text-[10px] font-bold uppercase tracking-wider mb-1 text-gray-500">Phòng ban</span>
                            <span className="block text-sm font-bold text-gray-900 bg-gray-50/50 p-2.5 rounded-lg border border-gray-100 break-words">
                              {roleOptionsLoading ? 'Đang tải...' : (roleOptions?.icDepartment?.name ?? 'Không có Phòng Hợp tác Quốc tế đang hoạt động')}
                            </span>
                          </div>
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
                              <ChevronDown className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none" />
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
                            className="px-3 py-2 border border-blue-200 rounded-lg text-sm font-black text-[#004c91] focus:outline-none focus:ring-2 focus:ring-[#004c91] bg-blue-50/30 focus:bg-white transition-all w-full"
                          />
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
                            <div className="mb-3 rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-bold text-red-700">
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
                      <label className="block text-sm font-bold text-gray-700 mb-2">Họ và tên <span className="text-red-500">*</span></label>
                      <input 
                        type="text" 
                        value={manualForm.name}
                        onChange={(e) => setManualForm({...manualForm, name: e.target.value})}
                        placeholder="Trần Văn C" 
                        className="w-full px-4 py-2.5 rounded-xl border border-gray-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none transition-shadow text-sm bg-white" 
                      />
                    </div>
                    <div>
                      <label className="block text-sm font-bold text-gray-700 mb-2">Email (Tên đăng nhập) <span className="text-red-500">*</span></label>
                      <input
                        type="email"
                        value={manualForm.email}
                        onChange={(e) => setManualForm({...manualForm, email: e.target.value})}
                        placeholder="example@domain.com"
                        className="w-full px-4 py-2.5 rounded-xl border border-gray-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none transition-shadow text-sm bg-white"
                      />
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
                  onClick={() => { setIsCreateModalOpen(false); setCreateError(null); setCreateStudentCodeError(null); }}
                  className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-100 transition-colors outline-none"
                >
                  Hủy bỏ
                </button>
                <button
                  onClick={handleCreateAccount}
                  disabled={
                    creating ||
                    (isHO && manualForm.role === 'STAFF' && !!createCampus &&
                      (slAvailabilityLoading || (!!slAvailability && !slAvailability.canCreateStaffLeader))) ||
                    (isHO && manualForm.role === 'HO' && !!createCampus &&
                      (hoCampusCheckLoading || (!!hoCampusCheck && !hoCampusCheck.canCreateHo)))
                  }
                  className="px-5 py-2.5 rounded-xl font-bold text-white bg-orange-500 hover:bg-orange-600 shadow-[0_4px_12px_rgba(249,115,22,0.2)] hover:shadow-[0_6px_16px_rgba(249,115,22,0.4)] transition-all outline-none transform hover:-translate-y-0.5 disabled:opacity-60 disabled:cursor-not-allowed"
                >
                  {creating ? 'Đang tạo...' : 'Xác nhận tạo'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* UC-97: Xác nhận kích hoạt / vô hiệu hóa tài khoản */}
      {statusTarget && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-[60] flex items-center justify-center p-4 sm:p-6 animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl w-full max-w-md shadow-xl overflow-hidden animate-in zoom-in-95 duration-300 relative">
            <div className="px-6 py-4 border-b border-gray-100 flex items-center gap-2">
              <h2 className="text-xl font-black text-gray-800">
                {statusTarget.status === 'Active' ? '⚠️ Xác nhận vô hiệu hóa' : '✅ Xác nhận kích hoạt'}
              </h2>
              <button
                onClick={() => setStatusTarget(null)}
                className="absolute top-4 right-4 w-8 h-8 rounded-full hover:bg-gray-100 flex items-center justify-center text-gray-500 transition-colors outline-none"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-6 text-gray-700 leading-relaxed text-[15px]">
              {statusTarget.status === 'Active' ? (
                <>Bạn có chắc muốn <strong className="text-red-600">vô hiệu hóa</strong> tài khoản <strong className="text-[#004c91]">{statusTarget.email}</strong>? Tất cả phiên đăng nhập của tài khoản này sẽ bị thu hồi ngay lập tức.</>
              ) : (
                <>Bạn có chắc muốn <strong className="text-[#0aa14f]">kích hoạt</strong> lại tài khoản <strong className="text-[#004c91]">{statusTarget.email}</strong>?</>
              )}
              {statusError && (
                <div className="mt-4 rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-bold text-red-700">
                  {statusError}
                </div>
              )}
            </div>

            <div className="px-6 py-4 border-t border-gray-100 bg-gray-50 flex items-center justify-end gap-3 rounded-b-2xl">
              <button
                onClick={() => setStatusTarget(null)}
                className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-100 transition-colors outline-none"
              >
                Hủy
              </button>
              <button
                onClick={confirmToggleStatus}
                disabled={statusSaving}
                className={`px-5 py-2.5 rounded-xl font-bold text-white shadow-sm transition-all outline-none disabled:opacity-60 disabled:cursor-not-allowed ${statusTarget.status === 'Active' ? 'bg-red-500 hover:bg-red-600' : 'bg-[#0aa14f] hover:bg-[#088c44]'}`}
              >
                {statusSaving ? 'Đang xử lý...' : 'Xác nhận'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* HO_BASIC_INFO §10 — xác nhận đổi email đăng nhập (thu hồi phiên + liên kết lại SSO/FEID). */}
      {basicInfoEmailConfirm && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-[70] flex items-center justify-center p-4 sm:p-6 animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl w-full max-w-md shadow-xl overflow-hidden animate-in zoom-in-95 duration-300 relative">
            <div className="px-6 py-4 border-b border-gray-100 flex items-center gap-2">
              <h2 className="text-xl font-black text-gray-800">✉️ Xác nhận thay đổi email đăng nhập</h2>
              <button
                onClick={() => setBasicInfoEmailConfirm(null)}
                disabled={roleSaving}
                className="absolute top-4 right-4 w-8 h-8 rounded-full hover:bg-gray-100 flex items-center justify-center text-gray-500 transition-colors outline-none disabled:opacity-50"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-6 text-gray-700 leading-relaxed text-[15px]">
              Bạn đang thay đổi email đăng nhập từ <strong className="text-[#004c91]">{basicInfoEmailConfirm.oldEmail || '-'}</strong> sang <strong className="text-[#004c91]">{basicInfoEmailConfirm.newEmail}</strong>.
              <div className="mt-3 text-sm text-gray-600">
                Tài khoản sẽ bị đăng xuất khỏi các phiên hiện tại và phải liên kết lại SSO/FEID khi đăng nhập lần tiếp theo.
              </div>
              {roleError && (
                <div className="mt-4 rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-bold text-red-700">
                  {roleError}
                </div>
              )}
            </div>

            <div className="px-6 py-4 border-t border-gray-100 bg-gray-50 flex items-center justify-end gap-3 rounded-b-2xl">
              <button
                onClick={() => setBasicInfoEmailConfirm(null)}
                disabled={roleSaving}
                className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-100 transition-colors outline-none disabled:opacity-60"
              >
                Hủy
              </button>
              <button
                onClick={submitBasicInfo}
                disabled={roleSaving}
                className="px-5 py-2.5 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#00386b] shadow-sm transition-all outline-none disabled:opacity-60 disabled:cursor-not-allowed"
              >
                {roleSaving ? 'Đang lưu...' : 'Xác nhận thay đổi'}
              </button>
            </div>
          </div>
        </div>
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

