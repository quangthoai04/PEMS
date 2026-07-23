/**
 * LocationManagementStaffLeader
 * Server-driven "Quản lý khu vực" (area/location management) for Staff Leader (role STAFF, sub_role LEADER).
 * Implements UC-LOC-01..09 (list / search / filter / add / edit / enable / disable), all scoped to the
 * caller's campus by the backend. Kept separate so the legacy mock page (other roles) stays untouched.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import {
  Plus, Edit, Search, ChevronRight, ChevronLeft, ChevronDown, ChevronUp, ArrowLeft,
  Loader2, AlertCircle, CheckCircle2, MapPin,
} from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import { galleryManagementApi } from '../../../features/gallery-management/api/galleryManagementApi';
import { getGalleryErrorMessage } from '../../../features/gallery-management/api/galleryError';
import {
  useGalleryFilterOptions,
  useGalleryLocationList,
} from '../../../features/gallery-management/hooks/useGalleryManagement';
import type {
  GalleryLocationDetail,
  GalleryLocationListItem,
  GalleryLocationStatus,
} from '../../../features/gallery-management/types/galleryManagement.types';
import { formatVietnamDate } from '../../../shared/utils/vietnamTime';
import { LocationCreateModal } from './LocationCreateModal';
import { LocationEditModal } from './LocationEditModal';

function formatDate(iso: string): string {
  if (!iso) return '';
  return formatVietnamDate(iso, { fallback: iso });
}

interface ToastState {
  /** warning = the save SUCCEEDED but the EN auto-translation failed (never rendered as an error). */
  type: 'success' | 'error' | 'warning';
  message: string;
}

type ModalState =
  | { mode: 'create' }
  /** Edit loads its own authoritative detail from the backend — only the id is passed. */
  | { mode: 'edit'; locationId: number };

export function LocationManagementStaffLeader() {
  const navigate = useNavigate();

  // ── Filters / paging ──
  const [searchInput, setSearchInput] = useState('');
  const [keyword, setKeyword] = useState('');
  const [areaId, setAreaId] = useState<number | ''>('');
  const [status, setStatus] = useState<GalleryLocationStatus | ''>('');
  // Default add-order: earliest-added location first, latest last (asc). Users can toggle to newest-first.
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');
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

  const {
    areas,
    loading: optionsLoading,
    refetch: refetchFilterOptions,
    upsertArea,
  } = useGalleryFilterOptions();
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

  const onSaved = (res: GalleryLocationDetail) => {
    setModal(null);
    // Translation failure is a WARNING on top of a successful save — never shown as an error.
    setToast(res.translationWarning
      ? { type: 'warning', message: res.translationWarning }
      : { type: 'success', message: res.message || 'Đã lưu khu vực và vị trí.' });
    // Optimistic dropdown update, then refresh BOTH the table and the area options so a brand-new /
    // renamed area shows up immediately in every dropdown — no F5 needed (§4.3).
    if (res.area) upsertArea(res.area);
    void Promise.all([refetch(), refetchFilterOptions()]);
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
          onClick={() => setModal({ mode: 'create' })}
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
              placeholder="Tìm kiếm theo khu vực, vị trí..."
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
                          onClick={() => setModal({ mode: 'edit', locationId: item.locationId })}
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

      {/* Create modal (UC-LOC-04/05) + direct edit modal — both with VI → EN translation preview. */}
      <AnimatePresence>
        {modal?.mode === 'create' && (
          <LocationCreateModal
            activeAreas={activeAreas.map((a) => ({ areaId: a.areaId, areaName: a.areaName, coverUrl: a.coverUrl, coverMediaType: a.coverMediaType }))}
            areasLoading={optionsLoading}
            onClose={() => setModal(null)}
            onSaved={onSaved}
            onError={(m) => setToast({ type: 'error', message: m })}
          />
        )}
        {modal?.mode === 'edit' && (
          <LocationEditModal
            locationId={modal.locationId}
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
            className={`fixed top-6 right-6 z-[60] flex items-center gap-3 px-5 py-3 rounded-xl shadow-2xl text-sm font-bold text-white ${
              toast.type === 'success' ? 'bg-green-600' : toast.type === 'warning' ? 'bg-amber-500' : 'bg-red-600'
            }`}
          >
            {toast.type === 'success' ? <CheckCircle2 className="w-5 h-5" /> : <AlertCircle className="w-5 h-5" />}
            {toast.message}
          </motion.div>
        )}
      </AnimatePresence>
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
