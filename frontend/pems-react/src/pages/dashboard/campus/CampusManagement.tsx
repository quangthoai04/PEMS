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
import { CAMPUS_PROVINCES } from '../../../features/campus-management/constants';
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
} from '../../../features/campus-management/types/campusManagement.types';

type Toast = { id: number; type: 'success' | 'error'; msg: string };

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

  // ── Disable-confirm modal + toggle in-flight ──
  const [confirmTarget, setConfirmTarget] = useState<CampusListItem | null>(null);
  const [togglingId, setTogglingId] = useState<number | null>(null);

  // ── UC-81 Create modal ──
  const emptyCreate = { campusCode: '', name: '', city: CAMPUS_PROVINCES[0], address: '', phone: '', email: '' };
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [createForm, setCreateForm] = useState(emptyCreate);
  const [createErrors, setCreateErrors] = useState<Record<string, string>>({});
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
  // (e.g. 409 dependency / 422 missing master data) surface the backend's message; the
  // toggle stays unchanged because we never optimistically mutated the row.
  const applyStatusChange = async (campus: CampusListItem, next: CampusStatus) => {
    setTogglingId(campus.campusId);
    try {
      await campusManagementApi.manageCampusStatus({ campusId: campus.campusId, status: next });
      pushToast(
        'success',
        next === 'ACTIVE'
          ? `Đã kích hoạt campus "${campus.name}".`
          : `Đã ngừng hoạt động campus "${campus.name}".`,
      );
      refetch();
    } catch (err) {
      pushToast('error', getAuthErrorMessage(err, 'Không thể cập nhật trạng thái campus. Vui lòng thử lại.'));
    } finally {
      setTogglingId(null);
    }
  };

  const onToggle = (campus: CampusListItem) => {
    if (!campus.canManageStatus || togglingId !== null) return;
    if (campus.status === 'ACTIVE') {
      // Disabling affects new registration/assignment dropdowns → confirm first (UC-86 §7).
      setConfirmTarget(campus);
    } else {
      // Enabling: no confirm, but the backend may reject (missing master data / IC dept).
      applyStatusChange(campus, 'ACTIVE');
    }
  };

  const confirmDisable = async () => {
    const target = confirmTarget;
    setConfirmTarget(null);
    if (target) await applyStatusChange(target, 'INACTIVE');
  };

  const openCreate = () => {
    setCreateForm(emptyCreate);
    setCreateErrors({});
    setIsCreateOpen(true);
  };

  const setCreateField = (field: keyof typeof emptyCreate, value: string) => {
    setCreateForm((prev) => ({ ...prev, [field]: value }));
    setCreateErrors((prev) => {
      if (!prev[field]) return prev;
      const next = { ...prev };
      delete next[field];
      return next;
    });
  };

  // UC-81 §6 — basic required + format validation before calling the API (AF-01).
  const validateCreate = (): boolean => {
    const errs: Record<string, string> = {};
    if (!createForm.campusCode.trim()) errs.campusCode = 'Vui lòng nhập mã campus.';
    if (!createForm.name.trim()) errs.name = 'Vui lòng nhập tên campus.';
    if (!createForm.city.trim()) errs.city = 'Vui lòng chọn thành phố.';
    if (!createForm.address.trim()) errs.address = 'Vui lòng nhập địa chỉ.';
    if (!createForm.phone.trim()) errs.phone = 'Vui lòng nhập số điện thoại.';
    else if ((createForm.phone.match(/\d/g) ?? []).length < 8) errs.phone = 'Số điện thoại không đúng định dạng.';
    if (!createForm.email.trim()) errs.email = 'Vui lòng nhập email.';
    else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(createForm.email.trim())) errs.email = 'Email không đúng định dạng.';
    setCreateErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const handleCreate = async () => {
    if (!validateCreate()) return;
    setCreating(true);
    try {
      const res = await campusManagementApi.createCampus({
        campusCode: createForm.campusCode.trim(),
        name: createForm.name.trim(),
        city: createForm.city.trim(),
        address: createForm.address.trim(),
        phone: createForm.phone.trim(),
        email: createForm.email.trim(),
      });
      setIsCreateOpen(false);
      pushToast('success', `Đã tạo campus "${res.name}" và phòng ban IC mặc định.`);
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
    <div className="p-4 md:p-8 space-y-6 bg-gray-50/50 min-h-screen">
      {/* Breadcrumb & Tiêu đề */}
      <div className="flex items-center gap-2 text-sm text-gray-500 mb-6">
        <span className="hover:text-[#004c91] cursor-pointer transition-colors" onClick={() => navigate('/dashboard')}>Dashboard</span>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-medium">Quản lý campus</span>
      </div>
      <div className="border-b border-gray-100 pb-4 mb-6 text-left flex justify-start">
        <h1 className="text-3xl font-bold text-[#004c91]">Quản lý campus</h1>
      </div>

      {/* Filters */}
      <div className="flex flex-col md:flex-row gap-4 items-center justify-between relative z-20">
        <div className="flex flex-col md:flex-row items-center gap-4 w-full flex-1">
          <div className="relative w-full md:max-w-[400px]">
            <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input
              type="text"
              placeholder="Tìm kiếm campus..."
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              className="w-full pl-10 pr-4 py-2.5 bg-white border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all shadow-sm"
            />
          </div>

          <div className="flex items-center gap-3 w-full md:w-auto">
            <select
              value={city}
              onChange={(e) => { setCity(e.target.value); setPage(1); }}
              className="w-full md:w-[160px] px-3 py-2.5 bg-white border border-gray-200 rounded-xl text-sm font-medium focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-600 shadow-sm"
            >
              <option value="">Tất cả cơ sở</option>
              {(filterOptions?.cities ?? []).map((c) => (
                <option key={c} value={c}>{c}</option>
              ))}
            </select>

            <select
              value={status}
              onChange={(e) => { setStatus(e.target.value as '' | CampusStatus); setPage(1); }}
              className="w-full md:w-[200px] px-3 py-2.5 bg-white border border-gray-200 rounded-xl text-sm font-medium focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-600 shadow-sm"
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
          <table className="w-full min-w-[760px]">
            <thead className="bg-[#004c91] text-white">
              <tr>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-16">STT</th>
                <th className="px-4 py-3 text-left text-xs font-bold uppercase tracking-wider w-[12%]">Mã code</th>
                <th className="px-4 py-3 text-left text-xs font-bold uppercase tracking-wider w-[28%]">Tên Campus</th>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-[15%]">Cơ sở</th>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-[18%]">Trưởng phòng IC</th>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-[15%]">Trạng thái</th>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-24">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {loading && (
                <tr>
                  <td colSpan={7} className="px-4 py-12 text-center text-gray-500 font-medium">
                    <Loader2 className="w-5 h-5 animate-spin inline mr-2 text-[#004c91]" />
                    Đang tải danh sách campus...
                  </td>
                </tr>
              )}

              {!loading && error && (
                <tr>
                  <td colSpan={7} className="px-4 py-12 text-center">
                    <div className="flex flex-col items-center gap-2 text-red-600">
                      <AlertTriangle className="w-6 h-6" />
                      <span className="font-medium">{error}</span>
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
                    <div className="text-sm text-gray-600 font-medium">{item.city || '—'}</div>
                  </td>
                  <td className="px-4 py-3 text-center">
                    {item.icHeadName ? (
                      <div className="text-sm font-bold text-[#004c91]">{item.icHeadName}</div>
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
                  <td colSpan={7} className="px-4 py-12 text-center text-gray-500 font-medium">
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
              className="border border-gray-200 rounded-lg px-2 py-1 bg-white focus:outline-none focus:ring-1 focus:ring-[#004c91] font-medium"
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
              {([
                { key: 'campusCode', label: 'Mã code', placeholder: 'VD: HN, HCM...' },
                { key: 'name', label: 'Tên campus', placeholder: 'VD: FPT University Hà Nội' },
              ] as const).map((f) => (
                <div key={f.key} className="space-y-1.5">
                  <label className="text-sm font-bold text-gray-900 block">{f.label}<span className="text-red-500 ml-1">*</span></label>
                  <input
                    type="text"
                    className={`w-full px-3 py-2 border rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all ${createErrors[f.key] ? 'border-red-400' : 'border-gray-200'}`}
                    placeholder={f.placeholder}
                    value={createForm[f.key]}
                    onChange={(e) => setCreateField(f.key, e.target.value)}
                  />
                  {createErrors[f.key] && <p className="text-xs text-red-500 font-medium">{createErrors[f.key]}</p>}
                </div>
              ))}

              <div className="space-y-1.5">
                <label className="text-sm font-bold text-gray-900 block">Chọn vị trí<span className="text-red-500 ml-1">*</span></label>
                <select
                  className={`w-full px-3 py-2 border rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all bg-white ${createErrors.city ? 'border-red-400' : 'border-gray-200'}`}
                  value={createForm.city}
                  onChange={(e) => setCreateField('city', e.target.value)}
                >
                  {CAMPUS_PROVINCES.map((p) => <option key={p} value={p}>{p}</option>)}
                </select>
                {createErrors.city && <p className="text-xs text-red-500 font-medium">{createErrors.city}</p>}
              </div>

              <div className="space-y-1.5">
                <label className="text-sm font-bold text-gray-900 block">Địa chỉ<span className="text-red-500 ml-1">*</span></label>
                <input
                  type="text"
                  className={`w-full px-3 py-2 border rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all ${createErrors.address ? 'border-red-400' : 'border-gray-200'}`}
                  placeholder="Số nhà, đường, phường/xã..."
                  value={createForm.address}
                  onChange={(e) => setCreateField('address', e.target.value)}
                />
                {createErrors.address && <p className="text-xs text-red-500 font-medium">{createErrors.address}</p>}
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <label className="text-sm font-bold text-gray-900 block">Số điện thoại<span className="text-red-500 ml-1">*</span></label>
                  <input
                    type="text"
                    className={`w-full px-3 py-2 border rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all ${createErrors.phone ? 'border-red-400' : 'border-gray-200'}`}
                    placeholder="VD: 024 7300 5588"
                    value={createForm.phone}
                    onChange={(e) => setCreateField('phone', e.target.value)}
                  />
                  {createErrors.phone && <p className="text-xs text-red-500 font-medium">{createErrors.phone}</p>}
                </div>
                <div className="space-y-1.5">
                  <label className="text-sm font-bold text-gray-900 block">Email<span className="text-red-500 ml-1">*</span></label>
                  <input
                    type="email"
                    className={`w-full px-3 py-2 border rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all ${createErrors.email ? 'border-red-400' : 'border-gray-200'}`}
                    placeholder="VD: hn@fpt.edu.vn"
                    value={createForm.email}
                    onChange={(e) => setCreateField('email', e.target.value)}
                  />
                  {createErrors.email && <p className="text-xs text-red-500 font-medium">{createErrors.email}</p>}
                </div>
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
              <button
                onClick={handleCreate}
                disabled={creating}
                className="px-5 py-2 bg-[#f37021] text-white font-bold rounded-xl hover:bg-[#e85c0d] transition-colors shadow-sm disabled:opacity-50 flex items-center gap-2"
              >
                {creating && <Loader2 className="w-4 h-4 animate-spin" />}
                Tạo mới
              </button>
            </div>
          </div>
        </div>,
        document.body,
      )}

      {/* UC-86 — Disable confirmation modal */}
      {confirmTarget && createPortal(
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md overflow-hidden">
            <div className="p-5 border-b border-gray-100 flex items-center justify-between">
              <h3 className="text-lg font-bold text-[#004c91] flex items-center gap-2">
                <AlertTriangle className="w-5 h-5 text-[#f37021]" />
                Ngừng hoạt động campus
              </h3>
              <button onClick={() => setConfirmTarget(null)} className="p-1.5 text-gray-400 hover:text-gray-600 transition-colors bg-gray-50 hover:bg-gray-100 rounded-lg">
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="p-6 text-sm text-gray-700 leading-relaxed">
              Bạn có chắc muốn ngừng hoạt động campus{' '}
              <span className="font-bold text-gray-900">"{confirmTarget.name}"</span>? Campus sẽ không còn
              xuất hiện trong các lựa chọn đăng ký/phân công mới.
            </div>
            <div className="p-5 border-t border-gray-100 bg-gray-50 flex items-center justify-end gap-3">
              <button
                onClick={() => setConfirmTarget(null)}
                className="px-5 py-2 bg-white border border-gray-200 text-gray-600 font-bold rounded-xl hover:bg-gray-50 transition-colors shadow-sm"
              >
                Hủy
              </button>
              <button
                onClick={confirmDisable}
                className="px-5 py-2 bg-[#f37021] text-white font-bold rounded-xl hover:bg-[#e85c0d] transition-colors shadow-sm"
              >
                Xác nhận
              </button>
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
            className={`px-4 py-3 rounded-xl shadow-lg text-sm font-medium max-w-sm ${
              t.type === 'success'
                ? 'bg-[#0aa14f] text-white'
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
