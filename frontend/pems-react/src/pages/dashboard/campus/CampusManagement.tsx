/**
 * Trang CampusManagement (HO)
 * UC-82 View Campus List, UC-83 Search/Filter Campus, UC-86 Manage Campus Status.
 * Dữ liệu lấy trực tiếp từ API (CampusesController), không còn mock.
 */

import React, { useEffect, useMemo, useState } from 'react';
import {
  Search, Plus, Eye, ChevronLeft, ChevronRight, AlertTriangle, Loader2, X, Building2,
} from 'lucide-react';
import { createPortal } from 'react-dom';
import { useNavigate } from 'react-router-dom';
import { CAMPUS_PROVINCES, campusReadinessReasons } from '../../../features/campus-management/constants';
import {
  CAMPUS_ADDRESS_MAX_LENGTH,
  CAMPUS_CODE_MAX_LENGTH,
  CAMPUS_EMAIL_MAX_LENGTH,
  CAMPUS_FIELD_VALIDATORS,
  CAMPUS_NAME_MAX_LENGTH,
  CAMPUS_PHONE_MAX_LENGTH,
  isCampusMasterFormValid,
  normalizeCampusMasterForm,
  validateCampusMasterForm,
} from '../../../features/campus-management/validation/campusMasterValidation';
import type {
  CampusMasterFieldErrors,
  CampusMasterForm,
} from '../../../features/campus-management/validation/campusMasterValidation';
import {
  useCampusFilterOptions,
  useCampusList,
} from '../../../features/campus-management/hooks/useCampusManagement';
import { campusManagementApi } from '../../../features/campus-management/api/campusManagementApi';
import { getAuthErrorMessage } from '../../../features/authentication/api/authError';
import type {
  CampusListItem,
  CampusListQueryParams,
  CampusStatus,
  CampusStatusImpact,
} from '../../../features/campus-management/types/campusManagement.types';

type Toast = { id: number; type: 'success' | 'error' | 'warning'; msg: string };

/** UC-86 §22.3 — groups the preview's blockersByStatus into the 3 confirmation-modal lines. */
function summarizeBlockers(byStatus: Record<string, number>): string[] {
  const count = (...statuses: string[]) =>
    statuses.reduce((sum, s) => sum + (byStatus[s] ?? 0), 0);

  const known = ['WAITING_REQUEST_APPROVAL', 'ASSIGNED', 'BEFORE_VISIT', 'DURING_VISIT', 'AFTER_VISIT'];
  const waiting = count('WAITING_REQUEST_APPROVAL');
  const prepared = count('ASSIGNED', 'BEFORE_VISIT');
  const receiving = count('DURING_VISIT', 'AFTER_VISIT');
  // Any unknown non-terminal status still blocks server-side — surface it verbatim.
  const other = Object.entries(byStatus)
    .filter(([status]) => !known.includes(status))
    .reduce((sum, [, n]) => sum + n, 0);

  const lines: string[] = [];
  if (waiting > 0) lines.push(`${waiting} đơn đang chờ xử lý`);
  if (prepared > 0) lines.push(`${prepared} chuyến đã được phân công/chuẩn bị`);
  if (receiving > 0) lines.push(`${receiving} chuyến đang hoặc đã tiếp khách nhưng chưa đóng`);
  if (other > 0) lines.push(`${other} chuyến ở trạng thái khác chưa kết thúc`);
  return lines;
}

export function CampusManagement() {
  const navigate = useNavigate();

  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase();

  // ── Filters / paging state ──
  const [searchInput, setSearchInput] = useState('');
  const [keyword, setKeyword] = useState('');
  const [city, setCity] = useState('');
  const [status, setStatus] = useState<'' | CampusStatus>('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  // ── Toasts ──
  const [toasts, setToasts] = useState<Toast[]>([]);
  const pushToast = (type: Toast['type'], msg: string) => {
    const id = Date.now() + Math.random();
    setToasts((prev) => [...prev, { id, type, msg }]);
    setTimeout(() => setToasts((prev) => prev.filter((t) => t.id !== id)), 4500);
  };

  // ── Disable-confirm modal (with UC-86 §18 impact preview) + toggle in-flight ──
  const [confirmTarget, setConfirmTarget] = useState<CampusListItem | null>(null);
  const [impact, setImpact] = useState<CampusStatusImpact | null>(null);
  const [impactLoading, setImpactLoading] = useState(false);
  const [impactError, setImpactError] = useState<string | null>(null);
  const [togglingId, setTogglingId] = useState<number | null>(null);

  // ── UC-81 Create modal ──
  const emptyCreate: CampusMasterForm = { campusCode: '', name: '', city: CAMPUS_PROVINCES[0], address: '', phone: '', email: '' };
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [createForm, setCreateForm] = useState<CampusMasterForm>(emptyCreate);
  const [createErrors, setCreateErrors] = useState<CampusMasterFieldErrors>({});
  const [creating, setCreating] = useState(false);

  // Debounce the search box (UC-83 §4 step 2). Reset to page 1 on change.
  useEffect(() => {
    const t = setTimeout(() => {
      setKeyword(searchInput.trim());
      setPage(1);
    }, 400);
    return () => clearTimeout(t);
  }, [searchInput]);

  const params: CampusListQueryParams = useMemo(
    () => ({
      keyword: keyword || undefined,
      city: city || undefined,
      status: status || undefined,
      page,
      pageSize,
      sortBy: 'name',
      sortOrder: 'asc',
    }),
    [keyword, city, status, page, pageSize],
  );

  const { data, loading, error, refetch } = useCampusList(params);
  const filterOptions = useCampusFilterOptions();

  const campuses = data?.items ?? [];
  const totalPages = data?.totalPages ?? 0;
  const totalItems = data?.totalItems ?? 0;
  const hasActiveFilter = !!keyword || !!city || !!status;

  if (userRole !== 'HO' && userRole !== 'ADMIN') {
    return (
      <div className="p-4 sm:p-6 md:p-8 h-full flex items-center justify-center">
        <div className="text-center">
          <h2 className="text-2xl font-bold text-gray-900 mb-2">Không có quyền truy cập</h2>
          <p className="text-gray-500">Trang này chỉ dành cho tài khoản HO.</p>
        </div>
      </div>
    );
  }

  // UC-86 — apply a status change, then refetch the list to reflect the DB. On failure
  // (e.g. 409 blockers due to a race / 422 missing master data) surface the backend's message;
  // the toggle stays unchanged because we never optimistically mutated the row, and the list
  // is refreshed so the row/blocker state matches the server again (§22.4).
  const applyStatusChange = async (campus: CampusListItem, next: CampusStatus) => {
    setTogglingId(campus.campusId);
    try {
      const res = await campusManagementApi.manageCampusStatus({ campusId: campus.campusId, status: next });
      if (next === 'ACTIVE') {
        if (res.readiness && !res.readiness.isAvailableForVisitRegistration) {
          // §22.5 — enable succeeded but the campus is not ready: warning, NOT an error, and
          // never a "đã sẵn sàng nhận đăng ký" success message.
          const reasons = campusReadinessReasons(res.readiness);
          pushToast(
            'warning',
            `Đã kích hoạt campus "${campus.name}". Campus chưa xuất hiện trên form đăng ký tham quan: ${
              reasons[0] ?? 'chưa đủ điều kiện tiếp nhận.'
            }`,
          );
        } else {
          pushToast('success', `Đã kích hoạt campus "${campus.name}". Campus đã sẵn sàng nhận đăng ký.`);
        }
      } else {
        // Disable also revokes sessions of the campus's STAFF/DEPARTMENT accounts (UC-86).
        pushToast(
          'success',
          res.affectedAccountCount > 0
            ? `Đã ngừng hoạt động campus "${campus.name}". ${res.affectedAccountCount} tài khoản thuộc cơ sở đã bị đăng xuất.`
            : `Đã ngừng hoạt động campus "${campus.name}".`,
        );
      }
      refetch();
    } catch (err) {
      pushToast('error', getAuthErrorMessage(err, 'Không thể cập nhật trạng thái campus. Vui lòng thử lại.'));
      // Refresh so the row (and any stale preview data) reflects the server state again.
      refetch();
    } finally {
      setTogglingId(null);
    }
  };

  // UC-86 §18/§22.3 — opening the disable modal fetches the impact preview. The preview is UX
  // only: even when canChange=true the backend rechecks in its own transaction.
  const openDisableConfirm = async (campus: CampusListItem) => {
    setConfirmTarget(campus);
    setImpact(null);
    setImpactError(null);
    setImpactLoading(true);
    try {
      const preview = await campusManagementApi.getCampusStatusImpact(campus.campusId, 'INACTIVE');
      setImpact(preview);
    } catch (err) {
      // Preview failure must not silently allow the change — show the error and keep confirm disabled.
      setImpactError(getAuthErrorMessage(err, 'Không thể kiểm tra ảnh hưởng. Vui lòng thử lại.'));
    } finally {
      setImpactLoading(false);
    }
  };

  const closeConfirm = () => {
    setConfirmTarget(null);
    setImpact(null);
    setImpactError(null);
    setImpactLoading(false);
  };

  const onToggle = (campus: CampusListItem) => {
    if (!campus.canManageStatus || togglingId !== null) return;
    if (campus.status === 'ACTIVE') {
      // Disabling affects new registration/assignment dropdowns → preview + confirm first.
      openDisableConfirm(campus);
    } else {
      // Enabling: no confirm, but the backend may reject (missing master data / IC dept).
      applyStatusChange(campus, 'ACTIVE');
    }
  };

  const confirmDisable = async () => {
    const target = confirmTarget;
    if (!target || impactLoading || !impact?.canChange) return;
    closeConfirm();
    await applyStatusChange(target, 'INACTIVE');
  };

  const openCreate = () => {
    setCreateForm(emptyCreate);
    setCreateErrors({});
    setIsCreateOpen(true);
  };

  // Typing clears the field's error; it is re-evaluated on blur (spec §11.3).
  const setCreateField = (field: keyof CampusMasterForm, value: string) => {
    setCreateForm((prev) => ({ ...prev, [field]: value }));
    setCreateErrors((prev) => {
      if (!prev[field]) return prev;
      const next = { ...prev };
      delete next[field];
      return next;
    });
  };

  const blurCreateField = (field: keyof CampusMasterForm) => {
    const message = CAMPUS_FIELD_VALIDATORS[field](createForm[field]);
    setCreateErrors((prev) => {
      const next = { ...prev };
      if (message) next[field] = message;
      else delete next[field];
      return next;
    });
  };

  // The submit button is already disabled while the form is invalid; this is the belt-and-braces
  // pass so a keyboard submit can never skip validation (spec §11.2/§11.3).
  const createFormIsValid = isCampusMasterFormValid(createForm);

  const handleCreate = async () => {
    // Guard against a double click / Enter landing a second request (spec §11.6).
    if (creating) return;

    const errs = validateCampusMasterForm(createForm);
    if (Object.keys(errs).length > 0) {
      setCreateErrors(errs);
      return;
    }

    setCreating(true);
    try {
      // Send the SAME normalized values the validation ran against — the code reaches the API
      // uppercased, spaces collapsed, email lowercased (spec §3).
      const res = await campusManagementApi.createCampus(normalizeCampusMasterForm(createForm));
      setIsCreateOpen(false);
      // §22.2 — create succeeds without a Staff Leader; explain why it is not on the form yet.
      pushToast('success', `Đã tạo campus "${res.name}" và phòng ban IC mặc định.`);
      pushToast(
        'warning',
        'Campus hiện đang hoạt động nhưng chưa xuất hiện trên form đăng ký tham quan vì chưa có Staff Leader đang hoạt động.',
      );
      setPage(1);
      refetch();
    } catch (err) {
      // 409/422 → keep modal open + retain data (UC-81 §12), surface backend message.
      pushToast('error', getAuthErrorMessage(err, 'Không thể tạo campus. Vui lòng thử lại.'));
    } finally {
      setCreating(false);
    }
  };

  const pageNumbers = Array.from({ length: totalPages }, (_, i) => i + 1);

  return (
    <div className="w-full space-y-4 pb-12">
      {/* Breadcrumb & Tiêu đề */}
      <div className="flex items-center gap-2 text-sm text-gray-500 mb-2">
        <span className="hover:text-[#004c91] cursor-pointer transition-colors" onClick={() => navigate('/dashboard')}>Dashboard</span>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-medium">Quản lý campus</span>
      </div>
      <div className="border-b border-gray-100 pb-3 mb-4 text-left flex justify-start">
        <h1 className="text-3xl font-bold text-[#004c91]">Quản lý campus</h1>
      </div>

      {/* Filters */}
      <div className="flex flex-col md:flex-row gap-4 items-center justify-between relative z-20">
        <div className="flex flex-col md:flex-row items-center gap-4 w-full flex-1">
          <div className="relative w-full md:max-w-[400px]">
            <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input
              type="text"
              placeholder="Tìm kiếm theo mã, tên campus, trưởng phòng IC..."
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              className="w-full pl-10 pr-4 py-2.5 bg-white border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all shadow-sm"
            />
          </div>

          <div className="flex items-center gap-3 w-full md:w-auto">
            <select
              value={city}
              onChange={(e) => { setCity(e.target.value); setPage(1); }}
              className="w-full md:w-[160px] px-3 py-2.5 bg-white border border-gray-200 rounded-xl text-sm font-normal focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-600 shadow-sm"
            >
              <option value="">Tất cả cơ sở</option>
              {(filterOptions?.cities ?? []).map((c) => (
                <option key={c} value={c}>{c}</option>
              ))}
            </select>

            <select
              value={status}
              onChange={(e) => { setStatus(e.target.value as '' | CampusStatus); setPage(1); }}
              className="w-full md:w-[200px] px-3 py-2.5 bg-white border border-gray-200 rounded-xl text-sm font-normal focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-600 shadow-sm"
            >
              <option value="">Tất cả trạng thái</option>
              <option value="ACTIVE">Hoạt động</option>
              <option value="INACTIVE">Ngừng hoạt động</option>
            </select>
          </div>
        </div>

        <button
          onClick={openCreate}
          className="flex items-center justify-center gap-2 bg-[#f37021] hover:bg-[#e06218] text-white px-5 py-2.5 rounded-xl text-sm font-bold shadow-sm transition-all shadow-[#f37021]/20 hover:shadow-[#f37021]/40 shrink-0"
        >
          <Plus className="w-4 h-4" /> Thêm mới campus
        </button>
      </div>

      {/* Table */}
      <div className="bg-white border rounded-2xl shadow-sm border-gray-100 relative z-10 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[960px]">
            <thead className="bg-[#004c91] text-white">
              <tr>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-14">STT</th>
                <th className="px-4 py-3 text-left text-xs font-bold uppercase tracking-wider w-[10%]">Mã code</th>
                <th className="px-4 py-3 text-left text-xs font-bold uppercase tracking-wider w-[22%]">Tên Campus</th>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-[12%]">Cơ sở</th>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-[15%]">Trưởng phòng IC</th>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-[12%]">Trạng thái</th>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-[18%]">Khả năng tiếp nhận</th>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-24">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {loading && (
                <tr>
                  <td colSpan={8} className="px-4 py-12 text-center text-gray-500 font-normal">
                    <Loader2 className="w-5 h-5 animate-spin inline mr-2 text-[#004c91]" />
                    Đang tải danh sách campus...
                  </td>
                </tr>
              )}

              {!loading && error && (
                <tr>
                  <td colSpan={8} className="px-4 py-12 text-center">
                    <div className="flex flex-col items-center gap-2 text-red-600">
                      <AlertTriangle className="w-6 h-6" />
                      <span className="font-normal">{error}</span>
                      <button
                        onClick={() => refetch()}
                        className="mt-2 px-4 py-1.5 text-sm font-bold text-[#004c91] border border-[#004c91]/30 rounded-lg hover:bg-[#e6eff7] transition-colors"
                      >
                        Thử lại
                      </button>
                    </div>
                  </td>
                </tr>
              )}

              {!loading && !error && campuses.map((item, index) => (
                <tr key={item.campusId} className="hover:bg-gray-50/50 transition-colors">
                  <td className="px-4 py-3 text-sm text-gray-500 font-medium text-center">
                    {(page - 1) * pageSize + index + 1}
                  </td>
                  <td className="px-4 py-3">
                    <span className="inline-flex px-2.5 py-1 text-xs font-bold rounded-md bg-[#e6eff7] text-[#004c91] uppercase tracking-wide">
                      {item.campusCode}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="text-sm font-bold text-gray-900">{item.name}</div>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <div className="text-sm text-gray-600 font-normal">{item.city || '—'}</div>
                  </td>
                  <td className="px-4 py-3 text-center">
                    {item.icHeadName ? (
                      <div className="text-sm font-normal text-[#004c91]">{item.icHeadName}</div>
                    ) : (
                      <span className="text-sm text-gray-400 italic">Chưa phân công</span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span className={`inline-flex px-3 py-1 text-xs font-bold rounded-full ${
                      item.status === 'ACTIVE'
                        ? 'bg-[#eaffe4] text-[#0aa14f] border border-[#ceefda]'
                        : 'bg-gray-100 text-gray-600 border border-gray-200'
                    }`}>
                      {item.status === 'ACTIVE' ? 'Hoạt động' : 'Ngừng hoạt động'}
                    </span>
                  </td>
                  {/* UC-86 §22.1 — readiness is a SEPARATE signal from the administrative status. */}
                  <td className="px-4 py-3 text-center">
                    {item.status !== 'ACTIVE' ? (
                      <span className="inline-flex px-3 py-1 text-xs font-bold rounded-full bg-gray-100 text-gray-500 border border-gray-200">
                        Không nhận tiếp đón
                      </span>
                    ) : item.readiness?.isAvailableForVisitRegistration ? (
                      <span className="inline-flex px-3 py-1 text-xs font-bold rounded-full bg-[#eaffe4] text-[#0aa14f] border border-[#ceefda]">
                        Sẵn sàng nhận tiếp đón
                      </span>
                    ) : (
                      <div className="flex flex-col items-center gap-1">
                        <span className="inline-flex items-center gap-1 px-3 py-1 text-xs font-bold rounded-full bg-amber-50 text-amber-700 border border-amber-200">
                          <AlertTriangle className="w-3 h-3" aria-hidden="true" /> Chưa sẵn sàng
                        </span>
                        {campusReadinessReasons(item.readiness).slice(0, 1).map((reason) => (
                          <span key={reason} className="text-[11px] text-amber-700/90 leading-tight max-w-[200px]">
                            {reason}
                          </span>
                        ))}
                      </div>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1 justify-center">
                      <button
                        onClick={() => navigate(`/dashboard/campus/${item.campusId}`)}
                        className="p-1.5 rounded-lg transition-colors hover:bg-[#e6eff7] hover:text-[#004c91] text-gray-400"
                        title="Xem chi tiết"
                      >
                        <Eye className="w-[16px] h-[16px]" />
                      </button>
                      {item.canManageStatus ? (
                        <button
                          onClick={() => onToggle(item)}
                          disabled={togglingId === item.campusId}
                          className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ml-2 disabled:opacity-50 disabled:cursor-not-allowed ${item.status === 'ACTIVE' ? 'bg-[#004c91]' : 'bg-gray-300'}`}
                          title={item.status === 'ACTIVE' ? 'Ngừng hoạt động' : 'Kích hoạt'}
                        >
                          <span className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${item.status === 'ACTIVE' ? 'translate-x-4' : 'translate-x-0'}`} />
                        </button>
                      ) : (
                        <span className="ml-2 text-gray-300 text-sm">—</span>
                      )}
                    </div>
                  </td>
                </tr>
              ))}

              {!loading && !error && campuses.length === 0 && (
                <tr>
                  <td colSpan={8} className="px-4 py-12 text-center text-gray-500 font-normal">
                    {hasActiveFilter ? 'Không tìm thấy campus phù hợp.' : 'Chưa có campus nào.'}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div className="px-4 py-3 border-t border-gray-100 bg-gray-50/50 flex flex-col md:flex-row items-center justify-between gap-4">
          <div className="flex items-center gap-2 text-sm text-gray-600">
            <span>Hiển thị</span>
            <select
              className="border border-gray-200 rounded-lg px-2 py-1 bg-white focus:outline-none focus:ring-1 focus:ring-[#004c91] font-normal"
              value={pageSize}
              onChange={(e) => { setPageSize(Number(e.target.value)); setPage(1); }}
            >
              <option value={5}>5</option>
              <option value={10}>10</option>
              <option value={20}>20</option>
            </select>
            <span>bản ghi / trang</span>
            <span className="ml-2 text-gray-400">· Tổng {totalItems}</span>
          </div>

          <div className="flex items-center gap-1.5">
            <button
              className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-100 text-gray-500 disabled:opacity-50 disabled:cursor-not-allowed bg-white"
              disabled={page <= 1}
              onClick={() => setPage(page - 1)}
            >
              <ChevronLeft className="w-5 h-5" />
            </button>
            {pageNumbers.map((p) => (
              <button
                key={p}
                className={`w-8 h-8 rounded-lg flex items-center justify-center text-sm font-bold transition-colors ${
                  page === p ? 'bg-[#004c91] text-white border border-[#004c91]' : 'bg-white border border-gray-200 text-gray-600 hover:bg-gray-50'
                }`}
                onClick={() => setPage(p)}
              >
                {p}
              </button>
            ))}
            <button
              className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-100 text-gray-500 disabled:opacity-50 disabled:cursor-not-allowed bg-white"
              disabled={totalPages === 0 || page >= totalPages}
              onClick={() => setPage(page + 1)}
            >
              <ChevronRight className="w-5 h-5" />
            </button>
          </div>
        </div>
      </div>

      {/* UC-81 — Create campus modal */}
      {isCreateOpen && createPortal(
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg overflow-hidden max-h-[90vh] flex flex-col">
            <div className="p-5 border-b border-gray-100 flex items-center justify-between shrink-0">
              <h3 className="text-xl font-bold text-[#004c91] flex items-center gap-2">
                <Building2 className="w-5 h-5" /> Thêm mới campus
              </h3>
              <button onClick={() => setIsCreateOpen(false)} className="p-1.5 text-gray-400 hover:text-gray-600 transition-colors bg-gray-50 hover:bg-gray-100 rounded-lg">
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-6 space-y-4 overflow-y-auto">
              <CreateField label="Mã code" field="campusCode" error={createErrors.campusCode}>
                <input
                  id="create-campusCode"
                  type="text"
                  maxLength={CAMPUS_CODE_MAX_LENGTH}
                  autoCapitalize="characters"
                  className={createInputCls(!!createErrors.campusCode)}
                  placeholder="VD: HN, HCM..."
                  value={createForm.campusCode}
                  onChange={(e) => setCreateField('campusCode', e.target.value)}
                  onBlur={() => blurCreateField('campusCode')}
                  aria-invalid={!!createErrors.campusCode}
                  aria-describedby={createErrors.campusCode ? 'create-campusCode-error' : undefined}
                />
              </CreateField>

              <CreateField label="Tên campus" field="name" error={createErrors.name}>
                <input
                  id="create-name"
                  type="text"
                  maxLength={CAMPUS_NAME_MAX_LENGTH}
                  className={createInputCls(!!createErrors.name)}
                  placeholder="VD: FPT University Hà Nội"
                  value={createForm.name}
                  onChange={(e) => setCreateField('name', e.target.value)}
                  onBlur={() => blurCreateField('name')}
                  aria-invalid={!!createErrors.name}
                  aria-describedby={createErrors.name ? 'create-name-error' : undefined}
                />
              </CreateField>

              {/* §6.1 — the field holds a province, not a free-form "vị trí". */}
              <CreateField label="Tỉnh/Thành phố" field="city" error={createErrors.city}>
                <select
                  id="create-city"
                  className={`${createInputCls(!!createErrors.city)} bg-white`}
                  value={createForm.city}
                  onChange={(e) => setCreateField('city', e.target.value)}
                  onBlur={() => blurCreateField('city')}
                  aria-invalid={!!createErrors.city}
                  aria-describedby={createErrors.city ? 'create-city-error' : undefined}
                >
                  {CAMPUS_PROVINCES.map((p) => <option key={p} value={p}>{p}</option>)}
                </select>
              </CreateField>

              <CreateField label="Địa chỉ" field="address" error={createErrors.address}>
                <input
                  id="create-address"
                  type="text"
                  maxLength={CAMPUS_ADDRESS_MAX_LENGTH}
                  className={createInputCls(!!createErrors.address)}
                  placeholder="Số nhà, đường, phường/xã..."
                  value={createForm.address}
                  onChange={(e) => setCreateField('address', e.target.value)}
                  onBlur={() => blurCreateField('address')}
                  aria-invalid={!!createErrors.address}
                  aria-describedby={createErrors.address ? 'create-address-error' : undefined}
                />
              </CreateField>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <CreateField label="Số điện thoại" field="phone" error={createErrors.phone}>
                  <input
                    id="create-phone"
                    type="text"
                    maxLength={CAMPUS_PHONE_MAX_LENGTH}
                    inputMode="tel"
                    autoComplete="tel"
                    className={createInputCls(!!createErrors.phone)}
                    placeholder="VD: 024 7300 5588"
                    value={createForm.phone}
                    onChange={(e) => setCreateField('phone', e.target.value)}
                    onBlur={() => blurCreateField('phone')}
                    aria-invalid={!!createErrors.phone}
                    aria-describedby={createErrors.phone ? 'create-phone-error' : undefined}
                  />
                </CreateField>
                <CreateField label="Email" field="email" error={createErrors.email}>
                  <input
                    id="create-email"
                    type="email"
                    maxLength={CAMPUS_EMAIL_MAX_LENGTH}
                    inputMode="email"
                    autoComplete="email"
                    className={createInputCls(!!createErrors.email)}
                    placeholder="VD: hn@fpt.edu.vn"
                    value={createForm.email}
                    onChange={(e) => setCreateField('email', e.target.value)}
                    onBlur={() => blurCreateField('email')}
                    aria-invalid={!!createErrors.email}
                    aria-describedby={createErrors.email ? 'create-email-error' : undefined}
                  />
                </CreateField>
              </div>
            </div>

            <div className="p-5 border-t border-gray-100 bg-gray-50 flex items-center justify-end gap-3 shrink-0">
              <button
                onClick={() => setIsCreateOpen(false)}
                disabled={creating}
                className="px-5 py-2 bg-white border border-gray-200 text-gray-600 font-bold rounded-xl hover:bg-gray-50 transition-colors shadow-sm disabled:opacity-50"
              >
                Hủy
              </button>
              {/* §11.2 — disabled while the form is invalid, not only while submitting. */}
              <button
                onClick={handleCreate}
                disabled={creating || !createFormIsValid}
                className="px-5 py-2 bg-[#f37021] text-white font-bold rounded-xl hover:bg-[#e85c0d] transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
              >
                {creating && <Loader2 className="w-4 h-4 animate-spin" />}
                Tạo mới
              </button>
            </div>
          </div>
        </div>,
        document.body,
      )}

      {/* UC-86 — Disable confirmation modal with §18 impact preview */}
      {confirmTarget && createPortal(
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/50 backdrop-blur-sm p-4" role="dialog" aria-modal="true" aria-label="Ngừng hoạt động campus">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md overflow-hidden max-h-[90vh] flex flex-col">
            <div className="p-5 border-b border-gray-100 flex items-center justify-between shrink-0">
              <h3 className="text-lg font-bold text-[#004c91] flex items-center gap-2">
                <AlertTriangle className="w-5 h-5 text-[#f37021]" />
                Ngừng hoạt động campus
              </h3>
              <button onClick={closeConfirm} aria-label="Đóng" className="p-1.5 text-gray-400 hover:text-gray-600 transition-colors bg-gray-50 hover:bg-gray-100 rounded-lg">
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-6 text-sm text-gray-700 leading-relaxed space-y-3 overflow-y-auto">
              {impactLoading && (
                <div className="flex items-center gap-2 text-gray-500 font-normal">
                  <Loader2 className="w-4 h-4 animate-spin text-[#004c91]" />
                  Đang kiểm tra các chuyến thăm liên quan...
                </div>
              )}

              {!impactLoading && impactError && (
                <div className="text-red-600 font-normal">{impactError}</div>
              )}

              {!impactLoading && !impactError && impact && impact.canChange && (
                <>
                  <p>
                    Bạn có chắc muốn ngừng hoạt động campus{' '}
                    <span className="font-bold text-gray-900">"{confirmTarget.name}"</span>?
                  </p>
                  <ul className="list-disc pl-5 space-y-1 text-gray-600">
                    <li>Campus sẽ không còn xuất hiện trong các lựa chọn đăng ký/phân công mới.</li>
                    <li>Các tài khoản Staff/Phòng ban thuộc cơ sở sẽ bị đăng xuất và không đăng nhập lại được cho đến khi cơ sở hoạt động trở lại.</li>
                    <li>Dữ liệu lịch sử, phòng ban và bản thân tài khoản vẫn được giữ nguyên (không bị xóa).</li>
                  </ul>
                </>
              )}

              {!impactLoading && !impactError && impact && !impact.canChange && (
                <>
                  <p className="font-bold text-gray-900">Không thể ngừng hoạt động campus.</p>
                  <p>Campus hiện còn:</p>
                  <ul className="list-disc pl-5 space-y-1 text-gray-700">
                    {summarizeBlockers(impact.blockersByStatus).map((line) => (
                      <li key={line}>{line}</li>
                    ))}
                  </ul>
                  {impact.blockerExamples.length > 0 && (
                    <div className="mt-2 border border-gray-100 rounded-xl divide-y divide-gray-100">
                      {impact.blockerExamples.map((ex) => (
                        <div key={ex.visitInstanceId} className="px-3 py-2 flex items-center justify-between gap-2">
                          <div className="min-w-0">
                            <div className="text-xs font-bold text-gray-900 truncate">
                              {ex.delegationName || ex.requestCode || `Chuyến #${ex.visitInstanceId}`}
                            </div>
                            {ex.plannedStartAt && (
                              <div className="text-[11px] text-gray-500">
                                {new Date(ex.plannedStartAt).toLocaleString('vi-VN')}
                              </div>
                            )}
                          </div>
                          <span className="shrink-0 inline-flex px-2 py-0.5 text-[10px] font-bold rounded-md bg-amber-50 text-amber-700 border border-amber-200">
                            {ex.status}
                          </span>
                        </div>
                      ))}
                      {impact.blockerCount > impact.blockerExamples.length && (
                        <div className="px-3 py-2 text-[11px] text-gray-500">
                          ... và {impact.blockerCount - impact.blockerExamples.length} chuyến khác.
                        </div>
                      )}
                    </div>
                  )}
                  <p className="text-gray-500 text-xs">
                    Vui lòng xử lý/đóng các chuyến thăm này trước khi ngừng hoạt động campus.
                  </p>
                </>
              )}
            </div>

            <div className="p-5 border-t border-gray-100 bg-gray-50 flex items-center justify-end gap-3 shrink-0">
              <button
                onClick={closeConfirm}
                className="px-5 py-2 bg-white border border-gray-200 text-gray-600 font-bold rounded-xl hover:bg-gray-50 transition-colors shadow-sm"
              >
                {impact && !impact.canChange ? 'Đóng' : 'Hủy'}
              </button>
              {/* Confirm is only actionable when the preview allows the change (§22.3). */}
              {(impactLoading || impactError !== null || (impact?.canChange ?? false)) && (
                <button
                  onClick={confirmDisable}
                  disabled={impactLoading || !impact?.canChange}
                  aria-label={`Xác nhận ngừng hoạt động campus ${confirmTarget.name}`}
                  className="px-5 py-2 bg-[#f37021] text-white font-bold rounded-xl hover:bg-[#e85c0d] transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  Xác nhận
                </button>
              )}
            </div>
          </div>
        </div>,
        document.body,
      )}

      {/* Toasts */}
      <div className="fixed top-6 right-6 z-[110] flex flex-col gap-2">
        {toasts.map((t) => (
          <div
            key={t.id}
            className={`px-4 py-3 rounded-xl shadow-lg text-sm font-normal max-w-sm ${
              t.type === 'success'
                ? 'bg-[#0aa14f] text-white'
                : t.type === 'warning'
                  ? 'bg-amber-500 text-white'
                  : 'bg-red-600 text-white'
            }`}
          >
            {t.msg}
          </div>
        ))}
      </div>
    </div>
  );
}

const createInputCls = (hasError: boolean) =>
  `w-full px-3 py-2 border rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all ${hasError ? 'border-red-400' : 'border-gray-200'}`;

/**
 * One labelled field of the create modal. The error node carries the id referenced by the input's
 * aria-describedby, so a screen reader announces the message with the field (spec §11.3).
 */
function CreateField({ label, field, error, children }: {
  label: string;
  field: keyof CampusMasterForm;
  error?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-1.5">
      <label htmlFor={`create-${field}`} className="text-sm font-bold text-gray-900 block">
        {label}<span className="text-red-500 ml-1">*</span>
      </label>
      {children}
      {error && (
        <p id={`create-${field}-error`} className="text-xs text-red-500 font-normal">{error}</p>
      )}
    </div>
  );
}
