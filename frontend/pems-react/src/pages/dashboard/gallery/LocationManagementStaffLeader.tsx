/**
 * LocationManagementStaffLeader
 * Server-driven "Quản lý khu vực" (area/location management) for Staff Leader (role STAFF, sub_role LEADER).
 * Implements UC-LOC-01..09 (list / search / filter / add / edit / enable / disable), all scoped to the
 * caller's campus by the backend. Kept separate so the legacy mock page (other roles) stays untouched.
 */

import React, { useEffect, useMemo, useRef, useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import {
  Plus, Edit, Search, X, ChevronRight, ChevronLeft, ChevronDown, ChevronUp, ArrowLeft,
  Loader2, AlertCircle, CheckCircle2, MapPin, Upload, ImageOff,
} from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuthenticatedMedia } from '../../../shared/hooks/useAuthenticatedImage';
import { validateFile } from '../../../shared/utils/fileValidation';
import { galleryManagementApi } from '../../../features/gallery-management/api/galleryManagementApi';
import { getGalleryErrorMessage } from '../../../features/gallery-management/api/galleryError';
import {
  useGalleryFilterOptions,
  useGalleryLocationList,
} from '../../../features/gallery-management/hooks/useGalleryManagement';
import type {
  GalleryLocationListItem,
  GalleryLocationMode,
  GalleryLocationStatus,
} from '../../../features/gallery-management/types/galleryManagement.types';
import { formatVietnamDate } from '../../../shared/utils/vietnamTime';

function formatDate(iso: string): string {
  if (!iso) return '';
  return formatVietnamDate(iso, { fallback: iso });
}

interface ToastState {
  type: 'success' | 'error';
  message: string;
}

interface ModalState {
  mode: 'create' | 'edit';
  /** The row being edited (edit mode only). */
  target: GalleryLocationListItem | null;
}

export function LocationManagementStaffLeader() {
  const navigate = useNavigate();

  // ── Filters / paging ──
  const [searchInput, setSearchInput] = useState('');
  const [keyword, setKeyword] = useState('');
  const [areaId, setAreaId] = useState<number | ''>('');
  const [status, setStatus] = useState<GalleryLocationStatus | ''>('');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('desc');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(5);

  // Debounce the search box (UC-LOC-02: 300–500ms).
  useEffect(() => {
    const t = setTimeout(() => {
      setKeyword(searchInput.trim());
      setPage(1);
    }, 400);
    return () => clearTimeout(t);
  }, [searchInput]);

  const { areas, loading: optionsLoading } = useGalleryFilterOptions();
  const activeAreas = useMemo(() => areas.filter((a) => a.status === 'ACTIVE'), [areas]);

  const params = useMemo(
    () => ({
      page,
      pageSize,
      keyword: keyword || undefined,
      areaId: areaId === '' ? undefined : areaId,
      status: status || undefined,
      sortBy: 'createdAt',
      sortDirection,
    }),
    [page, pageSize, keyword, areaId, status, sortDirection],
  );

  const { data, items, loading, error, refetch } = useGalleryLocationList(params);

  const totalPages = data?.totalPages ?? 0;
  const hasAnyFilter = !!(keyword || areaId !== '' || status);

  // ── Toast ──
  const [toast, setToast] = useState<ToastState | null>(null);
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3500);
    return () => clearTimeout(t);
  }, [toast]);

  // ── Status toggle (UC-LOC-08/09) ──
  const [togglingId, setTogglingId] = useState<number | null>(null);
  const handleToggle = async (item: GalleryLocationListItem) => {
    const next: GalleryLocationStatus = item.status === 'ACTIVE' ? 'INACTIVE' : 'ACTIVE';
    setTogglingId(item.locationId);
    try {
      const res = await galleryManagementApi.changeLocationStatus({ locationId: item.locationId, status: next });
      setToast({ type: 'success', message: res.message || (next === 'ACTIVE' ? 'Đã kích hoạt vị trí.' : 'Đã ngừng hoạt động vị trí.') });
      refetch();
    } catch (err) {
      setToast({ type: 'error', message: getGalleryErrorMessage(err) });
    } finally {
      setTogglingId(null);
    }
  };

  // ── Create / Edit modal ──
  const [modal, setModal] = useState<ModalState | null>(null);

  const onSaved = (message: string) => {
    setModal(null);
    setToast({ type: 'success', message });
    refetch();
  };

  return (
    <div className="w-full pb-12 animate-in fade-in duration-500 font-sans">
      {/* Breadcrumbs */}
      <div className="flex items-center gap-2 text-sm font-medium text-slate-500 mb-6">
        <span>Dashboard</span>
        <span>/</span>
        <Link to="/dashboard/gallery" className="hover:text-[#004c91] transition-colors">Quản lý Gallery</Link>
        <span>/</span>
        <span className="text-[#004c91] font-bold">Quản lý khu vực</span>
      </div>

      <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8">
        <div className="flex items-center gap-4">
          <button
            onClick={() => navigate(-1)}
            className="w-10 h-10 rounded-full bg-slate-100 flex items-center justify-center text-slate-500 hover:bg-[#004c91] hover:text-white transition-colors outline-none"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <div>
            <h1 className="text-3xl font-bold text-[#004c91]">Quản lý khu vực</h1>
            <p className="text-gray-500 mt-1 font-medium">Danh sách khu vực/tòa và vị trí cụ thể dùng cho Gallery</p>
          </div>
        </div>
        <button
          onClick={() => setModal({ mode: 'create', target: null })}
          className="flex items-center gap-2 bg-[#f37021] hover:bg-[#e85c0d] text-white px-5 py-2.5 rounded-xl text-sm font-bold shadow-sm transition-all hover:shadow-md outline-none"
        >
          <Plus className="w-5 h-5" /> Thêm khu vực mới
        </button>
      </div>

      {/* Toolbar / Search & Filters */}
      <div className="bg-[#004c91] rounded-t-2xl p-4 shadow-sm flex flex-col gap-4">
        <div className="flex flex-col md:flex-row items-center justify-between gap-4">
          <div className="relative w-full md:w-80 shrink-0">
            <Search className="w-5 h-5 absolute left-4 top-1/2 -translate-y-1/2 text-slate-400" />
            <input
              type="text"
              placeholder="Tìm kiếm khu vực, vị trí..."
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              className="w-full pl-11 pr-4 py-2.5 rounded-xl border border-white/20 focus:border-white focus:ring-1 focus:ring-white outline-none text-sm transition-all font-medium bg-white/10 text-white placeholder:text-white/60"
            />
          </div>

          <div className="flex flex-wrap items-center gap-2 w-full md:w-auto">
            <div className="relative min-w-[160px] flex-1 md:flex-none">
              <ScrollableDropdown
                value={areaId === '' ? '' : String(areaId)}
                options={[
                  { value: '', label: 'Tất cả khu vực' },
                  ...areas.map((a) => ({ value: String(a.areaId), label: a.areaName })),
                ]}
                onChange={(v) => {
                  setAreaId(v === '' ? '' : Number(v));
                  setPage(1);
                }}
              />
            </div>

            <div className="relative min-w-[150px] flex-1 md:flex-none">
              <select
                value={status}
                onChange={(e) => {
                  setStatus(e.target.value as GalleryLocationStatus | '');
                  setPage(1);
                }}
                className="w-full px-3 py-2.5 pr-8 rounded-xl border border-white/20 bg-white/10 text-white outline-none text-sm font-medium appearance-none"
              >
                <option value="" className="text-slate-800">Trạng thái</option>
                <option value="ACTIVE" className="text-slate-800">Hoạt động</option>
                <option value="INACTIVE" className="text-slate-800">Ngừng hoạt động</option>
              </select>
              <ChevronDown className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 text-white pointer-events-none" />
            </div>
          </div>
        </div>
      </div>

      {/* Table */}
      <div className="bg-white rounded-b-2xl shadow-sm border border-slate-200 overflow-hidden text-sm">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead className="bg-[#f8fafc] text-gray-500 border-b border-gray-200">
              <tr>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap text-center">STT</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Khu vực (Tòa/Khu)</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Vị trí cụ thể</th>
                <th className="p-4 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Trạng thái</th>
                <th
                  className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap cursor-pointer hover:bg-gray-50 transition-colors select-none group text-center"
                  onClick={() => setSortDirection((p) => (p === 'asc' ? 'desc' : 'asc'))}
                >
                  <div className="flex items-center justify-center gap-1.5">
                    Ngày tạo
                    <div className="flex flex-col text-gray-300 group-hover:text-[#004c91] transition-colors">
                      <ChevronUp className={`w-2.5 h-2.5 -mb-0.5 ${sortDirection === 'asc' ? 'text-[#004c91]' : ''}`} />
                      <ChevronDown className={`w-2.5 h-2.5 -mt-0.5 ${sortDirection === 'desc' ? 'text-[#004c91]' : ''}`} />
                    </div>
                  </div>
                </th>
                <th className="p-4 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {loading ? (
                <tr>
                  <td colSpan={6} className="px-6 py-16 text-center text-slate-500">
                    <Loader2 className="w-8 h-8 text-[#004c91] mx-auto mb-3 animate-spin" />
                    <p className="font-medium text-slate-600">Đang tải dữ liệu...</p>
                  </td>
                </tr>
              ) : error ? (
                <tr>
                  <td colSpan={6} className="px-6 py-16 text-center text-red-500">
                    <AlertCircle className="w-10 h-10 mx-auto mb-3" />
                    <p className="font-semibold mb-3">{error}</p>
                    <button onClick={refetch} className="px-4 py-2 rounded-lg bg-[#004c91] text-white text-sm font-bold">Thử lại</button>
                  </td>
                </tr>
              ) : items.length > 0 ? (
                items.map((item, index) => (
                  <tr key={item.locationId} className="hover:bg-blue-50/50 transition-colors group">
                    <td className="p-4 font-bold text-slate-500 text-center whitespace-nowrap">
                      {(page - 1) * pageSize + index + 1}
                    </td>
                    <td className="p-4 font-semibold text-slate-800 whitespace-nowrap">{item.areaName}</td>
                    <td className="p-4 font-medium text-slate-600 whitespace-nowrap">{item.locationName}</td>
                    <td className="p-4 text-center">
                      <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-[11px] font-bold ${
                        item.status === 'ACTIVE'
                          ? 'bg-green-100 text-green-700 border border-green-200'
                          : 'bg-slate-100 text-slate-700 border border-slate-200'
                      }`}>
                        {item.status === 'ACTIVE' ? 'Hoạt động' : 'Ngừng hoạt động'}
                      </span>
                    </td>
                    <td className="p-4 text-slate-600 font-medium whitespace-nowrap text-center">{formatDate(item.createdAt)}</td>
                    <td className="p-4">
                      <div className="flex items-center justify-center gap-2">
                        <button
                          onClick={() => setModal({ mode: 'edit', target: item })}
                          className="w-8 h-8 rounded-lg bg-slate-50 text-slate-400 hover:text-orange-500 hover:bg-orange-50 flex items-center justify-center transition-colors outline-none"
                          title="Chỉnh sửa"
                        >
                          <Edit className="w-4 h-4" />
                        </button>
                        <button
                          onClick={() => handleToggle(item)}
                          disabled={togglingId === item.locationId}
                          className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer items-center justify-center rounded-full transition-colors duration-200 ease-in-out outline-none ml-2 disabled:opacity-60 ${item.status === 'ACTIVE' ? 'bg-[#004c91]' : 'bg-gray-300'}`}
                          title={item.status === 'ACTIVE' ? 'Ngừng hoạt động' : 'Kích hoạt'}
                        >
                          <span className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${item.status === 'ACTIVE' ? 'translate-x-2' : '-translate-x-2'}`} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              ) : (
                <tr className="bg-slate-50/50">
                  <td colSpan={6} className="px-6 py-16 text-center text-slate-500">
                    <MapPin className="w-12 h-12 text-slate-300 mx-auto mb-3" />
                    <p className="font-medium text-slate-600 mb-1">
                      {hasAnyFilter ? 'Không tìm thấy khu vực phù hợp.' : 'Chưa có khu vực/vị trí nào.'}
                    </p>
                    <p className="text-xs">
                      {hasAnyFilter ? 'Vui lòng thử từ khoá tìm kiếm hoặc đổi bộ lọc.' : 'Bấm "Thêm khu vực mới" để tạo vị trí đầu tiên.'}
                    </p>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {data && data.totalItems > 0 && (
          <div className="p-6 border-t border-gray-100 flex flex-col sm:flex-row items-center justify-between gap-4 bg-gray-50/50">
            <div className="flex items-center gap-2">
              <span className="text-sm font-medium text-gray-500">Hiển thị</span>
              <div className="relative">
                <select
                  value={pageSize}
                  onChange={(e) => { setPageSize(Number(e.target.value)); setPage(1); }}
                  className="px-3 py-1.5 pr-8 rounded-lg border border-gray-200 text-sm font-bold text-gray-700 bg-white focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 appearance-none min-w-[70px] text-left"
                >
                  <option value={5}>5</option>
                  <option value={10}>10</option>
                  <option value={20}>20</option>
                  <option value={50}>50</option>
                </select>
                <ChevronDown className="w-4 h-4 absolute right-2 top-1/2 -translate-y-1/2 text-gray-500 pointer-events-none" />
              </div>
              <span className="text-sm font-medium text-gray-500">/ {data.totalItems} bản ghi</span>
            </div>

            <div className="flex items-center gap-2">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
                className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all disabled:opacity-50 disabled:cursor-not-allowed outline-none"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              <span className="text-sm font-bold text-gray-600 px-2">Trang {page} / {Math.max(1, totalPages)}</span>
              <button
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages}
                className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all disabled:opacity-50 disabled:cursor-not-allowed outline-none"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Create / Edit modal (UC-LOC-04..07) */}
      <AnimatePresence>
        {modal && (
          <LocationUpsertModal
            mode={modal.mode}
            target={modal.target}
            activeAreas={activeAreas.map((a) => ({ areaId: a.areaId, areaName: a.areaName, coverUrl: a.coverUrl }))}
            areasLoading={optionsLoading}
            onClose={() => setModal(null)}
            onSaved={onSaved}
            onError={(m) => setToast({ type: 'error', message: m })}
          />
        )}
      </AnimatePresence>

      {/* Toast */}
      <AnimatePresence>
        {toast && (
          <motion.div
            initial={{ opacity: 0, y: -20 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -20 }}
            className={`fixed top-6 right-6 z-[60] flex items-center gap-3 px-5 py-3 rounded-xl shadow-2xl text-sm font-bold text-white ${toast.type === 'success' ? 'bg-green-600' : 'bg-red-600'}`}
          >
            {toast.type === 'success' ? <CheckCircle2 className="w-5 h-5" /> : <AlertCircle className="w-5 h-5" />}
            {toast.message}
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

interface AreaOption {
  areaId: number;
  areaName: string;
  coverUrl?: string | null;
}

/** Single mandatory cover-image picker: preview of the picked file, or the existing (authenticated) cover. */
function CoverImageField({
  label,
  required,
  file,
  onPick,
  existingUrl,
  hint,
}: {
  label: string;
  required?: boolean;
  file: File | null;
  onPick: (f: File | null) => void;
  existingUrl?: string | null;
  hint?: string;
}) {
  const preview = useMemo(() => (file ? URL.createObjectURL(file) : null), [file]);
  useEffect(() => () => { if (preview) URL.revokeObjectURL(preview); }, [preview]);
  // Only fetch the stored cover when no new file has been picked.
  const existing = useAuthenticatedMedia(!file && existingUrl ? existingUrl : null);
  const shown = preview ?? existing.url;

  return (
    <div className="space-y-1.5">
      <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">
        {label} {required && <span className="text-red-500">*</span>}
      </label>
      <div className="flex items-center gap-3">
        <div className="w-20 h-20 rounded-xl overflow-hidden bg-slate-100 border border-slate-200 flex items-center justify-center shrink-0">
          {shown
            ? <img src={shown} className="w-full h-full object-cover" alt="" />
            : <ImageOff className="w-5 h-5 text-slate-300" />}
        </div>
        <label className="flex-1 cursor-pointer border-2 border-dashed border-[#004c91]/30 rounded-xl px-4 py-3 flex items-center gap-2 text-sm font-bold text-[#004c91] hover:bg-blue-50/50 transition-colors">
          <Upload className="w-4 h-4" />
          {file ? 'Đổi ảnh' : 'Chọn 1 ảnh'}
          <input
            type="file"
            accept="image/jpeg,image/png,image/webp"
            className="hidden"
            onChange={(e) => { onPick(e.target.files?.[0] ?? null); e.target.value = ''; }}
          />
        </label>
      </div>
      <p className="text-[11px] text-slate-400">{hint ?? 'Chỉ upload 1 ảnh (JPG/PNG/WEBP ≤5MB).'}</p>
    </div>
  );
}

/** Create/edit "khu vực" modal — radio between an existing area and a brand-new one (UC §28.4/28.5). */
function LocationUpsertModal({
  mode,
  target,
  activeAreas,
  areasLoading,
  onClose,
  onSaved,
  onError,
}: {
  mode: 'create' | 'edit';
  target: GalleryLocationListItem | null;
  activeAreas: AreaOption[];
  areasLoading: boolean;
  onClose: () => void;
  onSaved: (message: string) => void;
  onError: (message: string) => void;
}) {
  // On edit, default to the existing area (if it is still ACTIVE / present in the list); otherwise the first.
  const initialAreaId = useMemo(() => {
    if (mode === 'edit' && target && activeAreas.some((a) => a.areaId === target.areaId)) return target.areaId;
    return activeAreas[0]?.areaId ?? '';
  }, [mode, target, activeAreas]);

  const [areaMode, setAreaMode] = useState<GalleryLocationMode>('EXISTING_AREA');
  const [areaId, setAreaId] = useState<number | ''>(initialAreaId);
  const [newAreaName, setNewAreaName] = useState('');
  const [locationName, setLocationName] = useState(target?.locationName ?? '');
  const [areaCover, setAreaCover] = useState<File | null>(null);
  const [locationCover, setLocationCover] = useState<File | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    setAreaId(initialAreaId);
  }, [initialAreaId]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmedLocation = locationName.trim();
    if (!trimmedLocation) {
      onError('Vui lòng nhập vị trí cụ thể.');
      return;
    }
    if (areaMode === 'EXISTING_AREA' && areaId === '') {
      onError('Vui lòng chọn khu vực/tòa.');
      return;
    }
    if (areaMode === 'NEW_AREA' && !newAreaName.trim()) {
      onError('Vui lòng nhập tên khu vực/tòa mới.');
      return;
    }
    // A new area always needs its own cover image (BR-AREA-COVER-01 / BR-LOCATION-COVER-05).
    if (areaMode === 'NEW_AREA' && !areaCover) {
      onError('Vui lòng upload ảnh đại diện khu vực.');
      return;
    }
    // Location cover: mandatory on create, optional on edit (kept when omitted).
    if (mode === 'create' && !locationCover) {
      onError('Vui lòng upload ảnh đại diện vị trí.');
      return;
    }
    // Client-side image sanity check (backend re-validates).
    for (const [f, msg] of [
      [areaCover, 'Ảnh đại diện khu vực không đúng định dạng.'],
      [locationCover, 'Ảnh đại diện vị trí không đúng định dạng.'],
    ] as [File | null, string][]) {
      if (f && !validateFile(f, 'GALLERY_IMAGE').ok) {
        onError(msg);
        return;
      }
    }

    const payload = {
      mode: areaMode,
      areaId: areaMode === 'EXISTING_AREA' ? Number(areaId) : null,
      newAreaName: areaMode === 'NEW_AREA' ? newAreaName.trim() : null,
      locationName: trimmedLocation,
      // NEW_AREA: required area cover. EXISTING_AREA + edit: optional replacement of that area's cover.
      areaCoverImage: areaCover,
      locationCoverImage: locationCover,
    };

    setSubmitting(true);
    try {
      if (mode === 'create') {
        await galleryManagementApi.createLocation(payload);
        onSaved('Đã tạo vị trí mới.');
      } else {
        await galleryManagementApi.updateLocation({ ...payload, locationId: target!.locationId });
        onSaved('Đã cập nhật vị trí.');
      }
    } catch (err) {
      onError(getGalleryErrorMessage(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 font-sans">
      <motion.div
        initial={{ opacity: 0, scale: 0.95 }}
        animate={{ opacity: 1, scale: 1 }}
        exit={{ opacity: 0, scale: 0.95 }}
        className="bg-white rounded-3xl w-full max-w-lg overflow-hidden shadow-2xl relative"
      >
        <div className="px-6 py-4 border-b border-slate-100 flex items-center justify-between bg-slate-50">
          <h3 className="text-xl font-black text-[#004c91]">
            {mode === 'edit' ? 'Chỉnh sửa khu vực' : 'Thêm khu vực mới'}
          </h3>
          <button onClick={onClose} className="w-8 h-8 bg-white border border-slate-200 text-slate-500 rounded-full flex items-center justify-center hover:text-red-500 outline-none">
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="p-6">
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-3">
              <label className="text-xs font-bold text-slate-500 uppercase tracking-wide border-b border-slate-100 pb-2 block">Tên Khu Vực / Tòa <span className="text-red-500">*</span></label>
              <div className="flex items-center gap-4">
                <label className="flex items-center gap-2 cursor-pointer text-sm font-medium text-slate-700">
                  <input
                    type="radio"
                    name="areaMode"
                    value="EXISTING_AREA"
                    checked={areaMode === 'EXISTING_AREA'}
                    onChange={() => setAreaMode('EXISTING_AREA')}
                    className="w-4 h-4 text-[#004c91] border-gray-300 focus:ring-[#004c91]"
                  />
                  Khu vực có sẵn
                </label>
                <label className="flex items-center gap-2 cursor-pointer text-sm font-medium text-slate-700">
                  <input
                    type="radio"
                    name="areaMode"
                    value="NEW_AREA"
                    checked={areaMode === 'NEW_AREA'}
                    onChange={() => setAreaMode('NEW_AREA')}
                    className="w-4 h-4 text-[#004c91] border-gray-300 focus:ring-[#004c91]"
                  />
                  Khu vực mới
                </label>
              </div>

              {areaMode === 'EXISTING_AREA' ? (
                <div className="relative">
                  <select
                    value={areaId === '' ? '' : String(areaId)}
                    onChange={(e) => setAreaId(e.target.value === '' ? '' : Number(e.target.value))}
                    disabled={areasLoading || activeAreas.length === 0}
                    className="w-full px-4 py-2.5 pr-10 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium appearance-none bg-white disabled:bg-slate-50 disabled:text-slate-400"
                  >
                    {activeAreas.length === 0 ? (
                      <option value="">Chưa có khu vực hoạt động</option>
                    ) : (
                      activeAreas.map((a) => (
                        <option key={a.areaId} value={a.areaId}>{a.areaName}</option>
                      ))
                    )}
                  </select>
                  <ChevronDown className="w-4 h-4 text-slate-400 absolute right-4 top-1/2 -translate-y-1/2 pointer-events-none" />
                </div>
              ) : (
                <input
                  type="text"
                  value={newAreaName}
                  onChange={(e) => setNewAreaName(e.target.value)}
                  className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium"
                  placeholder="Nhập tên khu vực mới (VD: TÒA DELTA)"
                />
              )}

              {areaMode === 'NEW_AREA' ? (
                <CoverImageField
                  label="Ảnh đại diện khu vực"
                  required
                  file={areaCover}
                  onPick={setAreaCover}
                  hint="Ảnh tổng quan tòa/khu — chỉ 1 ảnh (JPG/PNG/WEBP ≤5MB)."
                />
              ) : mode === 'edit' ? (
                <CoverImageField
                  label="Ảnh đại diện khu vực"
                  file={areaCover}
                  onPick={setAreaCover}
                  existingUrl={activeAreas.find((a) => a.areaId === areaId)?.coverUrl}
                  hint="Để trống nếu muốn giữ ảnh khu vực hiện tại. Chỉ 1 ảnh (JPG/PNG/WEBP ≤5MB)."
                />
              ) : null}
            </div>

            <div className="space-y-1.5">
              <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Vị trí cụ thể <span className="text-red-500">*</span></label>
              <input
                type="text"
                value={locationName}
                onChange={(e) => setLocationName(e.target.value)}
                className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium"
                placeholder="VD: Sảnh chính"
              />
            </div>

            <CoverImageField
              label="Ảnh đại diện vị trí"
              required={mode === 'create'}
              file={locationCover}
              onPick={setLocationCover}
              existingUrl={target?.locationCoverUrl}
              hint={mode === 'edit'
                ? 'Để trống nếu muốn giữ ảnh hiện tại. Chỉ 1 ảnh (JPG/PNG/WEBP ≤5MB).'
                : 'Ảnh mặt trước/không gian vị trí — chỉ 1 ảnh (JPG/PNG/WEBP ≤5MB).'}
            />

            <div className="flex justify-end gap-3 pt-4 border-t border-slate-100 mt-4">
              <button type="button" onClick={onClose} className="px-5 py-2.5 rounded-xl text-sm font-bold text-slate-600 bg-slate-100 hover:bg-slate-200 outline-none">
                Hủy
              </button>
              <button
                type="submit"
                disabled={submitting}
                className="px-5 py-2.5 rounded-xl text-sm font-bold text-white bg-[#f37021] hover:bg-[#e85c0d] disabled:opacity-60 flex items-center gap-2 outline-none"
              >
                {submitting && <Loader2 className="w-4 h-4 animate-spin" />}
                {mode === 'edit' ? 'Cập nhật' : 'Tạo mới'}
              </button>
            </div>
          </form>
        </div>
      </motion.div>
    </div>
  );
}

/** Filter dropdown with a scrollable panel for long area lists (mirrors the gallery list page). */
function ScrollableDropdown({
  value,
  options,
  onChange,
}: {
  value: string;
  options: { value: string; label: string }[];
  onChange: (v: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onDoc = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, [open]);

  const selected = options.find((o) => o.value === value) ?? options[0];

  return (
    <div className="relative" ref={ref}>
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="w-full flex items-center justify-between gap-2 px-3 py-2.5 pr-8 rounded-xl border border-white/20 bg-white/10 text-white outline-none text-sm font-medium text-left"
      >
        <span className="truncate">{selected?.label}</span>
        <ChevronDown className={`w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 text-white pointer-events-none transition-transform ${open ? 'rotate-180' : ''}`} />
      </button>
      {open && (
        <div className="absolute z-30 mt-1 w-full max-h-60 overflow-y-auto rounded-xl bg-white shadow-xl border border-slate-200 py-1">
          {options.map((o) => (
            <button
              key={o.value}
              type="button"
              onClick={() => { onChange(o.value); setOpen(false); }}
              className={`w-full text-left px-3 py-2 text-sm font-medium transition-colors hover:bg-blue-50 ${o.value === value ? 'bg-blue-50 text-[#004c91] font-bold' : 'text-slate-700'}`}
            >
              {o.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

export default LocationManagementStaffLeader;
