/**
 * GalleryManagementStaffLeader
 * Server-driven VisitFPTU Gallery management for Staff Leader (role STAFF, sub_role LEADER).
 * Implements UC-GAL-01..07 (list / search / filter / detail / add / enable / disable / edit), all
 * scoped to the caller's campus by the backend. Kept in its own component so the legacy mock page
 * (other roles) stays untouched.
 */

import React, { useEffect, useMemo, useRef, useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import {
  Search, X, Upload, Trash2, ChevronRight, ChevronLeft, ChevronDown, ChevronUp,
  Plus, Image as ImageIcon, ImageOff, Film, Eye, MapPin, Loader2, AlertCircle, CheckCircle2, Star,
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuthenticatedMedia } from '../../../shared/hooks/useAuthenticatedImage';
import { validateFile } from '../../../shared/utils/fileValidation';
import { galleryManagementApi } from '../../../features/gallery-management/api/galleryManagementApi';
import { getGalleryErrorMessage } from '../../../features/gallery-management/api/galleryError';
import { useGalleryFilterOptions, useGalleryList } from '../../../features/gallery-management/hooks/useGalleryManagement';
import { GalleryDetailModal } from './GalleryDetailModal';
import { GalleryUpsertModal } from './GalleryUpsertModal';
import type {
  GalleryItemDetail,
  GalleryItemType,
  GalleryListItem,
  GalleryMediaKind,
  GalleryStatus,
} from '../../../features/gallery-management/types/galleryManagement.types';
import { formatVietnamDate } from '../../../shared/utils/vietnamTime';

const BRAND = '#004c91';
const ACCENT = '#f37021';
const MAX_FILES = 5;

const MEDIA_KIND_LABEL: Record<GalleryMediaKind, string> = {
  IMAGE: 'Hình ảnh',
  VIDEO: 'Video',
  MIXED: 'Hỗn hợp',
};

function formatDate(iso: string): string {
  if (!iso) return '';
  return formatVietnamDate(iso, { fallback: iso });
}

/** Renders an authenticated image/video (file served behind a Bearer-protected backend route). */
function AuthMedia({
  fileUrl,
  mediaType,
  alt,
  className,
  controls = false,
}: {
  fileUrl: string;
  mediaType: 'IMAGE' | 'VIDEO';
  alt?: string;
  className?: string;
  controls?: boolean;
}) {
  const { url, status } = useAuthenticatedMedia(fileUrl);

  if (status === 'error') {
    return (
      <div className={`flex items-center justify-center bg-slate-100 text-slate-300 ${className ?? ''}`} title="Media chưa có file khả dụng">
        {mediaType === 'VIDEO' ? <Film className="w-5 h-5" /> : <ImageOff className="w-5 h-5" />}
      </div>
    );
  }

  if (!url) {
    return (
      <div className={`flex items-center justify-center bg-slate-100 ${className ?? ''}`}>
        <Loader2 className="w-5 h-5 text-slate-300 animate-spin" />
      </div>
    );
  }

  if (mediaType === 'VIDEO') {
    return <video src={url} className={className} controls={controls} muted playsInline />;
  }
  return <img src={url} alt={alt} className={className} />;
}

function StatusBadge({ status }: { status: GalleryStatus }) {
  return (
    <span
      className={`inline-flex items-center px-2.5 py-1 rounded-full text-[11px] font-bold ${
        status === 'PUBLISHED'
          ? 'bg-green-100 text-green-700 border border-green-200'
          : 'bg-slate-100 text-slate-700 border border-slate-200'
      }`}
    >
      {status === 'PUBLISHED' ? 'Hiển thị' : 'Đã ẩn'}
    </span>
  );
}

function ItemTypeBadge({ itemType, label }: { itemType: GalleryItemType; label?: string }) {
  const text = label || (itemType === 'VISIT_DELEGATION' ? 'Đoàn khách' : 'Media');
  return (
    <span
      className={`inline-flex items-center px-2.5 py-1 rounded-full text-[11px] font-bold ${
        itemType === 'VISIT_DELEGATION'
          ? 'bg-orange-100 text-orange-700 border border-orange-200'
          : 'bg-blue-100 text-[#004c91] border border-blue-200'
      }`}
    >
      {text}
    </span>
  );
}

function KindBadge({ kind }: { kind: GalleryMediaKind }) {
  const icon = kind === 'VIDEO' ? <Film className="w-4 h-4 text-blue-500" /> : <ImageIcon className="w-4 h-4 text-orange-500" />;
  return (
    <div className="flex items-center justify-center gap-1.5 text-[11px] font-bold text-slate-700 bg-slate-100 px-2.5 py-1.5 rounded-lg w-max mx-auto">
      {icon} {MEDIA_KIND_LABEL[kind]}
    </div>
  );
}

interface ToastState {
  /** warning = the save SUCCEEDED but the EN auto-translation failed (never rendered as an error). */
  type: 'success' | 'error' | 'warning';
  message: string;
}

export function GalleryManagementStaffLeader() {
  const navigate = useNavigate();

  // ── Filters / paging ──
  const [searchInput, setSearchInput] = useState('');
  const [keyword, setKeyword] = useState('');
  const [areaId, setAreaId] = useState<number | ''>('');
  const [locationId, setLocationId] = useState<number | ''>('');
  const [mediaKind, setMediaKind] = useState<GalleryMediaKind | ''>('');
  const [itemType, setItemType] = useState<GalleryItemType | ''>('');
  const [status, setStatus] = useState<GalleryStatus | ''>('');
  // Default add-order: earliest-added item first, latest last (asc). Users can toggle to newest-first.
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  // Debounce the search box (UC-GAL-02: 300–500ms).
  useEffect(() => {
    const t = setTimeout(() => {
      setKeyword(searchInput.trim());
      setPage(1);
    }, 400);
    return () => clearTimeout(t);
  }, [searchInput]);

  const { areas } = useGalleryFilterOptions();

  const params = useMemo(
    () => ({
      page,
      pageSize,
      keyword: keyword || undefined,
      areaId: areaId === '' ? undefined : areaId,
      locationId: locationId === '' ? undefined : locationId,
      mediaKind: mediaKind || undefined,
      itemType: itemType || undefined,
      status: status || undefined,
      sortBy: 'createdAt',
      sortDirection,
    }),
    [page, pageSize, keyword, areaId, locationId, mediaKind, itemType, status, sortDirection],
  );

  const { data, items, loading, error, refetch } = useGalleryList(params);

  const totalPages = data?.totalPages ?? 0;
  const hasAnyFilter = !!(keyword || areaId !== '' || locationId !== '' || mediaKind || itemType || status);

  // Locations available for the location filter (scoped to the selected area when one is chosen).
  const filterLocations = useMemo(() => {
    if (areaId === '') return areas.flatMap((a) => a.locations);
    return areas.find((a) => a.areaId === areaId)?.locations ?? [];
  }, [areas, areaId]);

  // ── Modals & toast ──
  const [toast, setToast] = useState<ToastState | null>(null);
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [isEditOpen, setIsEditOpen] = useState(false);
  const [detail, setDetail] = useState<GalleryItemDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3500);
    return () => clearTimeout(t);
  }, [toast]);

  // ── Status toggle (UC-GAL-05/06) ──
  const [togglingId, setTogglingId] = useState<number | null>(null);
  const handleToggle = async (item: GalleryListItem) => {
    const next: GalleryStatus = item.status === 'PUBLISHED' ? 'HIDDEN' : 'PUBLISHED';
    setTogglingId(item.galleryItemId);
    try {
      await galleryManagementApi.changeStatus({ galleryItemId: item.galleryItemId, status: next });
      setToast({ type: 'success', message: next === 'PUBLISHED' ? 'Đã hiển thị gallery item.' : 'Đã ẩn gallery item.' });
      refetch();
    } catch (err) {
      setToast({ type: 'error', message: getGalleryErrorMessage(err) });
    } finally {
      setTogglingId(null);
    }
  };

  // ── Delete (soft delete — permanent from the user's point of view, unlike Hide) ──
  const [deleteTarget, setDeleteTarget] = useState<GalleryListItem | null>(null);
  const [deleting, setDeleting] = useState(false);
  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await galleryManagementApi.deleteGalleryItem({ galleryItemId: deleteTarget.galleryItemId });
      setDeleteTarget(null);
      setToast({ type: 'success', message: 'Xóa nội dung Gallery thành công.' });
      refetch();
    } catch (err) {
      // The row stays put — the record is never hidden on a failed delete.
      setToast({ type: 'error', message: getGalleryErrorMessage(err) });
    } finally {
      setDeleting(false);
    }
  };

  // ── Detail (UC-GAL-03) ──
  const openDetail = async (galleryItemId: number) => {
    setDetailLoading(true);
    setDetail(null);
    try {
      const result = await galleryManagementApi.getGalleryItemDetails(galleryItemId);
      setDetail(result);
    } catch (err) {
      setToast({ type: 'error', message: getGalleryErrorMessage(err) });
    } finally {
      setDetailLoading(false);
    }
  };

  const onCreated = (translationWarning?: string | null) => {
    setIsCreateOpen(false);
    // Translation failure is a WARNING on top of a successful save — never shown as an error.
    setToast(translationWarning
      ? { type: 'warning', message: translationWarning }
      : { type: 'success', message: 'Đã tạo Gallery Item với đầy đủ nội dung song ngữ.' });
    setPage(1);
    refetch();
  };

  const onUpdated = (updated: GalleryItemDetail) => {
    setIsEditOpen(false);
    setDetail(updated);
    setToast(updated.translationWarning
      ? { type: 'warning', message: updated.translationWarning }
      : { type: 'success', message: 'Đã cập nhật Gallery Item.' });
    refetch();
  };

  return (
    <div className="w-full pb-12 animate-in fade-in duration-500 font-sans">
      {/* Breadcrumbs */}
      <div className="flex items-center gap-2 text-sm font-normal text-slate-500 mb-6">
        <span>Dashboard</span>
        <span>/</span>
        <span className="text-[#004c91] font-bold">Quản lý Gallery</span>
      </div>

      <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8">
        <div>
          <h1 className="text-3xl font-bold text-[#004c91] tracking-tight">VisitFPTU Gallery</h1>
          <p className="text-gray-500 mt-1 font-normal">Quản lý tài nguyên hình ảnh và video</p>
        </div>
        <div className="flex items-center gap-3">
          <button
            onClick={() => navigate('/dashboard/gallery/locations')}
            className="flex items-center gap-2 bg-white hover:bg-slate-50 text-[#004c91] border border-[#004c91] px-5 py-2.5 rounded-xl text-sm font-bold shadow-sm transition-all hover:shadow-md outline-none"
          >
            <MapPin className="w-5 h-5" /> Quản lý khu vực
          </button>
          <button
            onClick={() => setIsCreateOpen(true)}
            className="flex items-center gap-2 bg-[#f37021] hover:bg-[#e85c0d] text-white px-5 py-2.5 rounded-xl text-sm font-bold shadow-sm transition-all hover:shadow-md outline-none"
          >
            <Plus className="w-5 h-5" /> Tải lên Media
          </button>
        </div>
      </div>

      {/* Toolbar / Search & Filters */}
      <div className="bg-[#004c91] rounded-t-2xl p-4 shadow-sm flex flex-col gap-4">
        <div className="flex flex-col xl:flex-row gap-4">
          <div className="relative w-full xl:w-80 shrink-0">
            <Search className="w-5 h-5 absolute left-4 top-1/2 -translate-y-1/2 text-slate-400" />
            <input
              type="text"
              placeholder="Tìm kiếm theo khu vực, vị trí, tiêu đề..."
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              className="w-full pl-11 pr-4 py-2.5 rounded-xl border border-white/20 focus:border-white focus:ring-1 focus:ring-white outline-none text-sm transition-all font-normal bg-white/10 text-white placeholder:text-white/60"
            />
          </div>

          <div className="grid grid-cols-2 xl:grid-cols-6 gap-3 w-full">
            <ScrollableDropdown
              value={areaId === '' ? '' : String(areaId)}
              options={[
                { value: '', label: 'Tất cả khu vực' },
                ...areas.map((a) => ({ value: String(a.areaId), label: a.areaName })),
              ]}
              onChange={(v) => {
                setAreaId(v === '' ? '' : Number(v));
                setLocationId('');
                setPage(1);
              }}
            />

            <ScrollableDropdown
              value={locationId === '' ? '' : String(locationId)}
              options={[
                { value: '', label: 'Tất cả vị trí' },
                ...filterLocations.map((l) => ({ value: String(l.locationId), label: l.locationName })),
              ]}
              onChange={(v) => {
                setLocationId(v === '' ? '' : Number(v));
                setPage(1);
              }}
            />

            <FilterSelect
              value={mediaKind}
              onChange={(v) => {
                setMediaKind(v as GalleryMediaKind | '');
                setPage(1);
              }}
            >
              <option value="" className="text-slate-800">Tất cả các loại</option>
              <option value="IMAGE" className="text-slate-800">Hình ảnh</option>
              <option value="VIDEO" className="text-slate-800">Video</option>
              <option value="MIXED" className="text-slate-800">Hỗn hợp</option>
            </FilterSelect>

            <FilterSelect
              value={itemType}
              onChange={(v) => {
                setItemType(v as GalleryItemType | '');
                setPage(1);
              }}
            >
              <option value="" className="text-slate-800">Tất cả nội dung</option>
              <option value="MEDIA" className="text-slate-800">Media</option>
              <option value="VISIT_DELEGATION" className="text-slate-800">Đoàn khách</option>
            </FilterSelect>

            <FilterSelect
              value={status}
              onChange={(v) => {
                setStatus(v as GalleryStatus | '');
                setPage(1);
              }}
            >
              <option value="" className="text-slate-800">Tất cả trạng thái</option>
              <option value="PUBLISHED" className="text-slate-800">Hiển thị</option>
              <option value="HIDDEN" className="text-slate-800">Đã ẩn</option>
            </FilterSelect>
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
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Khu vực</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Vị trí cụ thể</th>
                <th className="p-4 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Loại nội dung</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap min-w-[220px]">Tiêu đề</th>
                <th className="p-4 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Định dạng</th>
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
                  <td colSpan={9} className="px-6 py-16 text-center text-slate-500">
                    <Loader2 className="w-8 h-8 text-[#004c91] mx-auto mb-3 animate-spin" />
                    <p className="font-normal text-slate-600">Đang tải dữ liệu...</p>
                  </td>
                </tr>
              ) : error ? (
                <tr>
                  <td colSpan={9} className="px-6 py-16 text-center text-red-500">
                    <AlertCircle className="w-10 h-10 mx-auto mb-3" />
                    <p className="font-normal mb-3">{error}</p>
                    <button onClick={refetch} className="px-4 py-2 rounded-lg bg-[#004c91] text-white text-sm font-bold">Thử lại</button>
                  </td>
                </tr>
              ) : items.length > 0 ? (
                items.map((item, index) => (
                  <tr key={item.galleryItemId} className="hover:bg-blue-50/50 transition-colors group">
                    <td className="p-4 font-bold text-slate-500 text-center whitespace-nowrap">
                      {(page - 1) * pageSize + index + 1}
                    </td>
                    <td className="p-4 font-normal text-slate-800 whitespace-nowrap">{item.areaName}</td>
                    <td className="p-4 font-normal text-slate-600 whitespace-nowrap">{item.locationName}</td>
                    <td className="p-4 text-center"><ItemTypeBadge itemType={item.itemType} label={item.itemTypeLabel} /></td>
                    <td className="p-4">
                      <div className="text-sm font-semibold text-[#004c91] group-hover:text-blue-800 transition-colors line-clamp-2 max-w-[260px]">
                        {item.title}
                      </div>
                    </td>
                    <td className="p-4 text-center"><KindBadge kind={item.mediaKind} /></td>
                    <td className="p-4 text-center"><StatusBadge status={item.status} /></td>
                    <td className="p-4 text-slate-600 font-normal whitespace-nowrap text-center">{formatDate(item.createdAt)}</td>
                    <td className="p-4">
                      <div className="flex items-center justify-center gap-2">
                        <button
                          onClick={() => openDetail(item.galleryItemId)}
                          className="w-8 h-8 rounded-lg bg-slate-50 text-slate-400 hover:text-[#004c91] hover:bg-blue-50 flex items-center justify-center transition-colors outline-none"
                          title="Xem chi tiết"
                        >
                          <Eye className="w-4 h-4" />
                        </button>
                        <button
                          onClick={() => handleToggle(item)}
                          disabled={togglingId === item.galleryItemId}
                          className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer items-center justify-center rounded-full transition-colors duration-200 ease-in-out outline-none disabled:opacity-60 ${item.status === 'PUBLISHED' ? 'bg-[#004c91]' : 'bg-gray-300'}`}
                          title={item.status === 'PUBLISHED' ? 'Ẩn' : 'Hiện'}
                        >
                          <span className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${item.status === 'PUBLISHED' ? 'translate-x-2' : '-translate-x-2'}`} />
                        </button>
                        <button
                          onClick={() => setDeleteTarget(item)}
                          className="w-8 h-8 rounded-lg bg-slate-50 text-slate-400 hover:text-red-600 hover:bg-red-50 flex items-center justify-center transition-colors outline-none"
                          title="Xóa"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              ) : (
                <tr className="bg-slate-50/50">
                  <td colSpan={9} className="px-6 py-16 text-center text-slate-500">
                    <ImageIcon className="w-12 h-12 text-slate-300 mx-auto mb-3" />
                    <p className="font-normal text-slate-600 mb-1">
                      {hasAnyFilter ? 'Không tìm thấy media phù hợp.' : 'Chưa có media nào trong cơ sở này.'}
                    </p>
                    <p className="text-xs">
                      {hasAnyFilter ? 'Vui lòng thử từ khoá tìm kiếm hoặc đổi bộ lọc.' : 'Bấm "Tải lên Media" để thêm gallery item đầu tiên.'}
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
                  className="px-3 py-1.5 pr-8 rounded-lg border border-gray-200 text-sm font-normal text-gray-700 bg-white focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 appearance-none min-w-[70px] text-left"
                >
                  <option value={5}>5</option>
                  <option value={10}>10</option>
                  <option value={20}>20</option>
                  <option value={50}>50</option>
                </select>
                <ChevronDown className="w-4 h-4 absolute right-2 top-1/2 -translate-y-1/2 text-gray-500 pointer-events-none" />
              </div>
              <span className="text-sm font-normal text-gray-500">/ {data.totalItems} bản ghi</span>
            </div>

            <div className="flex items-center gap-2">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
                className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all disabled:opacity-50 disabled:cursor-not-allowed outline-none"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              <span className="text-sm font-normal text-gray-600 px-2">Trang {page} / {Math.max(1, totalPages)}</span>
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

      {/* Create modal (UC-GAL-04) */}
      <AnimatePresence>
        {isCreateOpen && (
          <GalleryUpsertModal
            mode="create"
            areas={areas}
            onClose={() => setIsCreateOpen(false)}
            onCreated={onCreated}
            onUpdated={onUpdated}
            onError={(m) => setToast({ type: 'error', message: m })}
          />
        )}
      </AnimatePresence>

      {/* Detail modal (UC-GAL-03) */}
      <AnimatePresence>
        {(detail || detailLoading) && !isEditOpen && (
          <GalleryDetailModal
            detail={detail}
            loading={detailLoading}
            onClose={() => { setDetail(null); }}
            onEdit={() => setIsEditOpen(true)}
          />
        )}
      </AnimatePresence>

      {/* Edit modal (UC-GAL-07) */}
      <AnimatePresence>
        {isEditOpen && detail && (
          <GalleryUpsertModal
            mode="edit"
            areas={areas}
            existing={detail}
            onClose={() => setIsEditOpen(false)}
            onCreated={onCreated}
            onUpdated={onUpdated}
            onError={(m) => setToast({ type: 'error', message: m })}
          />
        )}
      </AnimatePresence>

      {/* Delete confirmation — never delete straight from the icon click. */}
      <AnimatePresence>
        {deleteTarget && (
          <div className="fixed inset-0 z-[55] flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 font-sans">
            <motion.div
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              exit={{ opacity: 0, scale: 0.95 }}
              role="dialog"
              aria-modal="true"
              aria-labelledby="gallery-delete-title"
              className="bg-white rounded-3xl w-full max-w-md overflow-hidden shadow-2xl"
            >
              <div className="p-6">
                <div className="flex items-start gap-4">
                  <div className="w-12 h-12 shrink-0 rounded-2xl bg-red-50 text-red-600 flex items-center justify-center">
                    <Trash2 className="w-6 h-6" />
                  </div>
                  <div className="min-w-0">
                    <h3 id="gallery-delete-title" className="text-lg font-black text-slate-800">Xóa nội dung Gallery?</h3>
                    <p className="mt-1 text-sm font-semibold text-[#004c91] line-clamp-2">{deleteTarget.title}</p>
                  </div>
                </div>

                <div className="mt-4 space-y-2 text-sm text-slate-600 font-normal">
                  <p>Nội dung này sẽ không còn xuất hiện trong Quản lý Gallery và VisitFPTU.</p>
                  <p>Xóa khác với Ẩn nội dung và không thể bật lại bằng nút Hiện/Ẩn.</p>
                  <p className="text-red-600 font-normal">Bạn sẽ không thể khôi phục nội dung này từ giao diện hiện tại.</p>
                </div>

                <div className="mt-6 flex items-center justify-end gap-3">
                  <button
                    type="button"
                    onClick={() => setDeleteTarget(null)}
                    disabled={deleting}
                    className="px-4 py-2.5 rounded-xl border border-slate-200 text-slate-600 text-sm font-bold hover:bg-slate-50 transition-colors outline-none disabled:opacity-60"
                  >
                    Hủy
                  </button>
                  <button
                    type="button"
                    onClick={() => void confirmDelete()}
                    disabled={deleting}
                    className="px-4 py-2.5 rounded-xl bg-red-600 hover:bg-red-700 text-white text-sm font-bold shadow-sm transition-colors outline-none disabled:opacity-60 inline-flex items-center gap-2"
                  >
                    {deleting && <Loader2 className="w-4 h-4 animate-spin" />} Xóa
                  </button>
                </div>
              </div>
            </motion.div>
          </div>
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

/** Custom filter dropdown with a scrollable panel (max-height) for long area / location lists. */
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

function FilterSelect({
  value,
  onChange,
  children,
}: {
  value: string;
  onChange: (v: string) => void;
  children: React.ReactNode;
}) {
  return (
    <div className="relative">
      <select
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-full px-3 py-2.5 pr-8 rounded-xl border border-white/20 bg-white/10 text-white outline-none text-sm font-normal appearance-none"
      >
        {children}
      </select>
      <ChevronDown className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 text-white pointer-events-none" />
    </div>
  );
}

export default GalleryManagementStaffLeader;
