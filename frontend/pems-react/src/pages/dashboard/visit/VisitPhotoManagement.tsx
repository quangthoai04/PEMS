/**
 * VisitPhotoManagement — Trang "Quản lý ảnh đoàn khách" dành cho Student, Staff, Host & Staff Leader/Admin.
 *
 * Hỗ trợ:
 * - Lọc theo khoảng thời gian (fromDate - toDate)
 * - Sắp xếp theo ngày (Mới nhất / Cũ nhất)
 * - Chuyển đổi linh hoạt giữa 2 chế độ xem: "📁 Thư mục đoàn" và "📷 Danh sách Bức ảnh"
 * - Tự động chuyển sang chế độ "Bức ảnh trực tiếp" khi người dùng nhập từ khóa tìm kiếm tên khách/đoàn
 * - Mở modal xem/quản lý/quét mặt từng đoàn khách
 */
/**
 * VisitPhotoManagement — Trang "Quản lý ảnh đoàn khách" dành cho Student, Staff, Host & Staff Leader/Admin.
 *
 * Hỗ trợ:
 * - Lọc theo khoảng thời gian (fromDate - toDate)
 * - Sắp xếp theo ngày (Mới nhất / Cũ nhất)
 * - Chuyển đổi linh hoạt giữa 2 chế độ xem: "📁 Thư mục đoàn" và "📷 Danh sách Bức ảnh"
 * - Tự động chuyển sang chế độ "Bức ảnh trực tiếp" khi người dùng nhập từ khóa tìm kiếm tên khách/đoàn
 * - Xem ảnh thư mục làm góc nhìn chính (có hiển thị nhãn danh tính khi xem phóng to)
 * - Nút "Quét mặt" riêng biệt mở modal quét & gán danh tính khuôn mặt
 */
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Camera, ChevronLeft, ChevronRight, Eye, Pencil, Search, X, Folder, FolderOpen, Upload, Image as ImageIcon, Calendar, Sparkles, RefreshCw, FileText } from 'lucide-react';
import toast from 'react-hot-toast';
import httpClient from '../../../shared/api/httpClient';
import { visitPhotosApi } from '../../../features/delegations/api/visitPhotosApi';
import { VisitPhotoPanel } from '../../../features/delegations/components/VisitPhotoPanel';
import { FaceScanPanel, type FaceScanPhoto, type FaceScanPanelHandle } from '../../../features/delegations/components/FaceScanPanel';
import type { MyVisitPhotoFolderItem, MyVisitPhotoFoldersPage, VisitInstancePhotos } from '../../../features/delegations/types/visitPhotos.types';
import { formatVietnamDate } from '../../../shared/utils/vietnamTime';
import { validateFile } from '../../../shared/utils/fileValidation';
import { useAuth } from '../../../shared/hooks/useAuth';

const PAGE_SIZE = 10;

const INSTANCE_STATUS_LABELS: Record<string, string> = {
  WAITING_REQUEST_APPROVAL: 'Chờ xử lý tại cơ sở',
  ASSIGNED: 'Đã duyệt và phân công',
  BEFORE_VISIT: 'Trước tiếp khách',
  DURING_VISIT: 'Đang tiếp khách',
  AFTER_VISIT: 'Chờ đóng đoàn',
  CLOSED: 'Đã đóng đoàn',
  CANCELLED: 'Đã hủy',
  REJECTED: 'Từ chối',
};

const errMsg = (e: any, fallback: string) => e?.response?.data?.message || fallback;

/** Modal Quét mặt riêng biệt dành cho Staff / Staff Leader */
function FaceScanModalContent({
  item,
  onClose,
}: {
  item: MyVisitPhotoFolderItem;
  onClose: (changed: boolean) => void;
}) {
  const [photosData, setPhotosData] = useState<VisitInstancePhotos | null>(null);
  const [loadingPhotos, setLoadingPhotos] = useState(true);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const faceScanPanelRef = useRef<FaceScanPanelHandle>(null);

  const loadPhotos = useCallback(async () => {
    setLoadingPhotos(true);
    try {
      const res = await visitPhotosApi.byInstance(item.visitInstanceId);
      setPhotosData(res);
    } catch {
      toast.error('Không thể tải danh sách ảnh đoàn khách.');
    } finally {
      setLoadingPhotos(false);
    }
  }, [item.visitInstanceId]);

  useEffect(() => {
    loadPhotos();
  }, [loadPhotos]);

  const handleUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!e.target.files || e.target.files.length === 0) return;
    const files = Array.from(e.target.files) as File[];
    if (fileInputRef.current) fileInputRef.current.value = '';

    for (const f of files) {
      const check = validateFile(f, 'VISIT_REQUEST_PHOTO');
      if (!check.ok) {
        toast.error(`${f.name}: ${check.message ?? 'Ảnh không hợp lệ.'}`);
        return;
      }
    }

    const toastId = toast.loading('Đang tải ảnh lên...');
    try {
      await visitPhotosApi.upload(item.visitInstanceId, files);
      toast.success('Đã tải ảnh đoàn khách lên.', { id: toastId });
      await loadPhotos();
    } catch (err: any) {
      toast.error(errMsg(err, 'Không thể tải ảnh lên.'), { id: toastId });
    }
  };

  const faceScanPhotos: FaceScanPhoto[] = useMemo(() => {
    if (!photosData?.photos) return [];
    return photosData.photos.map((p) => ({
      visitPhotoId: p.visitPhotoId,
      url: p.url,
      name: p.fileName,
      uploadedByName: p.uploadedByName,
      uploadedAt: p.uploadedAt,
    }));
  }, [photosData]);

  return (
    <div className="flex flex-col h-full max-h-[92vh]">
      {/* Modal Header */}
      <div className="px-6 py-3.5 border-b border-slate-100 flex items-center justify-between gap-3 bg-slate-50/50 shrink-0">
        <div className="min-w-0 flex items-center gap-3">
          <div className="w-9 h-9 rounded-xl bg-orange-50 text-[#f37021] flex items-center justify-center shrink-0 border border-orange-100 shadow-sm">
            <Sparkles className="w-5 h-5 text-[#f37021]" />
          </div>
          <div>
            <h3 className="text-base font-extrabold text-[#004c91] truncate flex items-center gap-2">
              Quét mặt & Gán danh tính khuôn mặt
            </h3>
            <p className="text-xs text-slate-500 font-medium truncate">
              {item.delegationName} · {item.campusName}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2 shrink-0">
          <input
            type="file"
            ref={fileInputRef}
            onChange={handleUpload}
            accept=".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp"
            multiple
            className="hidden"
          />
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            className="inline-flex items-center gap-1.5 px-3.5 py-1.5 bg-[#004c91] hover:bg-[#00386b] text-white rounded-lg text-xs font-extrabold shadow-sm transition-all active:scale-95 cursor-pointer"
          >
            <Upload className="w-3.5 h-3.5" /> Tải lên ảnh chụp thực tế
          </button>
          <button
            type="button"
            onClick={() => faceScanPanelRef.current?.openAlbumModal()}
            className="inline-flex items-center gap-1.5 px-3.5 py-1.5 bg-white text-[#004c91] border border-[#004c91] hover:bg-blue-50 rounded-lg text-xs font-extrabold shadow-sm transition-all active:scale-95 cursor-pointer"
          >
            <FolderOpen className="w-3.5 h-3.5" /> Chọn từ thư mục ảnh đoàn
          </button>
          <button
            type="button"
            onClick={() => onClose(true)}
            className="p-2 rounded-xl text-slate-400 hover:text-slate-700 hover:bg-slate-200/60 transition-colors cursor-pointer"
            aria-label="Đóng"
          >
            <X className="w-5 h-5" />
          </button>
        </div>
      </div>

      {/* Modal Body */}
      <div className="p-6 overflow-y-auto flex-1 bg-white">
        {loadingPhotos ? (
          <div className="py-20 text-center flex flex-col items-center justify-center gap-2">
            <RefreshCw className="w-6 h-6 animate-spin text-[#004c91]" />
            <span className="text-xs font-bold text-gray-600">Đang tải công cụ định danh khuôn mặt...</span>
          </div>
        ) : (
          <FaceScanPanel
            ref={faceScanPanelRef}
            visitInstanceId={item.visitInstanceId}
            photos={faceScanPhotos}
            isReadOnly={false}
            onUploadClick={() => fileInputRef.current?.click()}
            folderUrl={photosData?.folderWebViewUrl ?? undefined}
            onRefreshPhotos={loadPhotos}
          />
        )}
      </div>
    </div>
  );
}

interface RelatedNewsItem {
  newsId: number;
  title: string;
  description?: string;
  coverImageUrl?: string;
  campusName?: string;
  authorName: string;
  createdAt: string;
  status: string;
  statusLabel: string;
}

export function VisitPhotoManagement() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const isStudent = user?.roleCode?.toUpperCase() === 'STUDENT';

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [search, setSearch] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [sortDirection, setSortDirection] = useState('DESC');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [viewMode, setViewMode] = useState<'folders' | 'photos'>('folders');

  const [relatedNews, setRelatedNews] = useState<RelatedNewsItem[]>([]);
  const [loadingNews, setLoadingNews] = useState(false);

  const [data, setData] = useState<MyVisitPhotoFoldersPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Modal xem danh mục ảnh chính (Gallery View)
  const [photoModalItem, setPhotoModalItem] = useState<MyVisitPhotoFolderItem | null>(null);
  // Modal quét mặt (Face Scan View)
  const [faceScanModalItem, setFaceScanModalItem] = useState<MyVisitPhotoFolderItem | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(await visitPhotosApi.myFolders(page, pageSize, search || undefined, sortDirection, fromDate || undefined, toDate || undefined));
      setError(null);
    } catch (e: any) {
      if (e?.response?.status === 403) setError('Bạn không có quyền truy cập danh sách ảnh đoàn khách này.');
      else setError('Không thể tải danh sách ảnh đoàn khách. Vui lòng thử lại.');
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, search, sortDirection, fromDate, toDate]);

  useEffect(() => { load(); }, [load]);

  // Debounce tìm kiếm & Tự động đổi chế độ xem khi tìm tên khách
  useEffect(() => {
    const t = setTimeout(() => {
      const trimmed = searchInput.trim();
      setSearch(trimmed);
      setPage(1);
      if (trimmed) {
        setViewMode('photos');
      } else {
        setViewMode('folders');
      }
    }, 400);
    return () => clearTimeout(t);
  }, [searchInput]);

  // Fetch related news articles matching the search term (tagged partner/guest photos or news content)
  useEffect(() => {
    if (!search) {
      setRelatedNews([]);
      return;
    }
    let active = true;
    setLoadingNews(true);
    httpClient.get<{ items: RelatedNewsItem[] }>('/news', { params: { keyword: search, pageSize: 6 } })
      .then((res) => {
        if (active) setRelatedNews(res.data.items || []);
      })
      .catch(() => {
        if (active) setRelatedNews([]);
      })
      .finally(() => {
        if (active) setLoadingNews(false);
      });
    return () => { active = false; };
  }, [search]);

  const closePhotoModal = async (changed: boolean) => {
    setPhotoModalItem(null);
    if (changed) await load();
  };

  const closeFaceScanModal = async (changed: boolean) => {
    setFaceScanModalItem(null);
    if (changed) await load();
  };

  const totalPages = data?.totalPages ?? 0;
  const items = data?.items ?? [];

  return (
    <div className="w-full pb-16 animate-in fade-in duration-300">
      {/* Breadcrumb Header */}
      <div className="flex items-center gap-2 text-sm font-medium text-slate-500 mb-2">
        <span>Dashboard</span>
        <span>/</span>
        <span className="text-[#004c91] font-bold">Quản lý ảnh đoàn khách</span>
      </div>

      <div className="border-b border-gray-100 pb-3 mb-4 text-left flex justify-start">
        <h1 className="text-3xl font-bold text-[#004c91]">Quản lý ảnh đoàn khách</h1>
      </div>

      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
        {/* Top Control Bar — Compact Single Row Layout */}
        <div className="px-4 sm:px-6 py-3.5 border-b border-slate-100 flex items-center justify-between gap-3 bg-slate-50/50 flex-wrap lg:flex-nowrap">
          {/* Camera Icon */}
          <div className="w-10 h-10 rounded-xl bg-orange-50 text-[#f37021] flex items-center justify-center shrink-0 shadow-sm border border-orange-100">
            <Camera className="w-5 h-5" />
          </div>

          {/* Search Bar (Bên phải Icon) */}
          <div className="relative flex-1 min-w-[200px] max-w-[320px]">
            <Search className="w-3.5 h-3.5 text-gray-400 absolute left-3 top-1/2 -translate-y-1/2" />
            <input
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder="Tìm theo tên khách, tên đoàn..."
              className="w-full text-xs font-semibold rounded-xl border border-gray-300 pl-8 pr-7 py-2 outline-none focus:border-[#004c91] bg-white shadow-sm"
            />
            {searchInput && (
              <button
                onClick={() => setSearchInput('')}
                className="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 p-0.5"
              >
                <X className="w-3 h-3" />
              </button>
            )}
          </div>

          {/* Controls & Filters (Cùng 1 hàng với Icon & Search) */}
          <div className="flex items-center gap-2.5 flex-wrap lg:flex-nowrap shrink-0">
            {/* View Mode Toggle */}
            <div className="bg-gray-100 p-1 rounded-xl flex items-center gap-1 border border-gray-200 shrink-0">
              <button
                type="button"
                onClick={() => setViewMode('folders')}
                className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 ${viewMode === 'folders' ? 'bg-white text-[#004c91] shadow-sm' : 'text-gray-500 hover:text-gray-800'}`}
              >
                <Folder className="w-3.5 h-3.5" /> Thư mục đoàn
              </button>
              <button
                type="button"
                onClick={() => setViewMode('photos')}
                className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 ${viewMode === 'photos' ? 'bg-white text-[#004c91] shadow-sm' : 'text-gray-500 hover:text-gray-800'}`}
              >
                <ImageIcon className="w-3.5 h-3.5" /> Xem tất cả ảnh
              </button>
            </div>

            {/* Date Range Picker */}
            <div className="flex items-center gap-1.5 bg-white p-1 rounded-xl border border-gray-300 shadow-sm shrink-0">
              <Calendar className="w-3.5 h-3.5 text-gray-400 ml-2" />
              <input
                type="date"
                value={fromDate}
                title="Từ ngày"
                onChange={(e) => { setFromDate(e.target.value); setPage(1); }}
                className="text-xs font-semibold rounded-lg px-2 py-1 outline-none text-gray-700 bg-transparent"
              />
              <span className="text-gray-400 text-xs">-</span>
              <input
                type="date"
                value={toDate}
                title="Đến ngày"
                onChange={(e) => { setToDate(e.target.value); setPage(1); }}
                className="text-xs font-semibold rounded-lg px-2 py-1 outline-none text-gray-700 bg-transparent pr-2"
              />
              {(fromDate || toDate) && (
                <button
                  onClick={() => { setFromDate(''); setToDate(''); setPage(1); }}
                  className="text-gray-400 hover:text-gray-600 p-1"
                  title="Xóa bộ lọc ngày"
                >
                  <X className="w-3 h-3" />
                </button>
              )}
            </div>

            {/* Sort Direction */}
            <select
              value={sortDirection}
              onChange={(e) => { setSortDirection(e.target.value); setPage(1); }}
              className="text-xs font-bold rounded-xl border border-gray-300 px-3 py-2 outline-none focus:border-[#004c91] bg-white cursor-pointer shadow-sm shrink-0"
            >
              <option value="DESC">Mới nhất trước</option>
              <option value="ASC">Cũ nhất trước</option>
            </select>
          </div>
        </div>

        {/* Related News Section when searching */}
        {search && (
          <div className="mx-4 sm:mx-6 my-4 p-4 sm:p-5 rounded-2xl border border-blue-100 bg-gradient-to-r from-blue-50/60 to-indigo-50/40 space-y-3 shadow-xs">
            <div className="flex items-center justify-between flex-wrap gap-2">
              <div className="flex items-center gap-2.5">
                <div className="w-8 h-8 rounded-xl bg-[#004c91] text-white flex items-center justify-center font-bold text-sm shadow-sm shrink-0">
                  <FileText className="w-4 h-4 text-white" />
                </div>
                <div>
                  <h3 className="font-extrabold text-[#004c91] text-sm flex items-center gap-2">
                    Bài viết tin tức liên quan đến từ khóa "{search}"
                  </h3>
                  <p className="text-xs text-slate-500 font-medium">
                    Các bài tin tức có sử dụng ảnh hoặc liên quan đến đối tác/khách được gán tên
                  </p>
                </div>
              </div>
              {loadingNews && (
                <span className="text-xs text-[#004c91] font-semibold flex items-center gap-1.5 bg-white/80 px-2.5 py-1 rounded-lg border border-blue-100">
                  <RefreshCw className="w-3.5 h-3.5 animate-spin" /> Đang tìm bài viết...
                </span>
              )}
            </div>

            {relatedNews.length === 0 && !loadingNews ? (
              <p className="text-xs text-slate-500 italic bg-white/70 p-3 rounded-xl border border-blue-100">
                Không tìm thấy bài viết tin tức nào sử dụng ảnh của từ khóa "{search}".
              </p>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3 pt-1">
                {relatedNews.map((news) => (
                  <div
                    key={news.newsId}
                    onClick={() => navigate(`/dashboard/news/${news.newsId}`)}
                    className="bg-white border border-slate-200 hover:border-[#004c91] hover:shadow-md rounded-xl p-3.5 transition-all cursor-pointer flex flex-col justify-between group"
                  >
                    <div className="space-y-2">
                      <div className="flex items-center justify-between gap-2">
                        <span className="text-[10px] font-bold px-2 py-0.5 rounded-md bg-blue-50 text-[#004c91] border border-blue-100">
                          {news.statusLabel || news.status}
                        </span>
                        {news.campusName && (
                          <span className="text-[10px] text-slate-500 font-semibold truncate">
                            {news.campusName}
                          </span>
                        )}
                      </div>
                      <h4 className="font-bold text-slate-800 text-xs line-clamp-2 group-hover:text-[#004c91] transition-colors">
                        {news.title}
                      </h4>
                      {news.description && (
                        <p className="text-[11px] text-slate-500 line-clamp-2 font-medium">
                          {news.description}
                        </p>
                      )}
                    </div>
                    <div className="mt-3 pt-2 border-t border-slate-100 flex items-center justify-between text-[10px] text-slate-400 font-medium">
                      <span>Tác giả: {news.authorName}</span>
                      <span className="text-[#004c91] font-bold flex items-center gap-1 group-hover:underline">
                        Xem bài viết →
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* Content Body */}
        {loading ? (
          <div className="py-20 text-center text-slate-500 font-medium flex flex-col items-center justify-center gap-2">
            <RefreshCw className="w-6 h-6 animate-spin text-[#004c91]" />
            <span className="text-xs font-bold text-gray-600">Đang tải dữ liệu ảnh đoàn khách...</span>
          </div>
        ) : error ? (
          <div className="py-16 text-center text-slate-500 font-medium px-4">{error}</div>
        ) : items.length === 0 ? (
          <div className="py-20 text-center px-4">
            <Camera className="w-12 h-12 mx-auto text-slate-300 mb-3" />
            <p className="text-slate-700 font-bold text-sm">
              {search ? `Không tìm thấy kết quả phù hợp với từ khóa "${search}".` : 'Chưa có dữ liệu ảnh đoàn khách nào.'}
            </p>
            <p className="text-xs text-slate-400 mt-1">
              Thử thay đổi bộ lọc khoảng thời gian hoặc từ khóa tìm kiếm.
            </p>
          </div>
        ) : viewMode === 'folders' ? (
          /* Mode 1: Table Thư Mục Đoàn */
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-sm min-w-[760px]">
              <thead className="bg-[#004c91] text-white text-[12px] uppercase tracking-wider font-extrabold">
                <tr>
                  <th className="px-5 py-3.5 w-14 text-center">STT</th>
                  <th className="px-4 py-3.5">Tên đoàn khách / Chuyến thăm</th>
                  <th className="px-4 py-3.5 w-56">Tên thư mục Drive</th>
                  <th className="px-4 py-3.5 w-40 text-center">Số lượng ảnh</th>
                  <th className="px-5 py-3.5 w-48 text-right">Hành động</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {items.map((item, idx) => (
                  <tr key={item.visitInstanceId} className="hover:bg-slate-50/70 transition-colors group">
                    <td className="px-5 py-4 font-extrabold text-slate-400 text-center text-xs">
                      {(page - 1) * PAGE_SIZE + idx + 1}
                    </td>
                    <td className="px-4 py-4">
                      <p className="font-bold text-slate-900 group-hover:text-[#004c91] transition-colors">{item.delegationName || '—'}</p>
                      <div className="flex items-center gap-2 mt-1 flex-wrap">
                        <span className="text-[11px] font-bold text-[#004c91] bg-blue-50 px-2 py-0.5 rounded-md">
                          {INSTANCE_STATUS_LABELS[item.instanceStatus] || item.instanceStatus}
                        </span>
                        {item.plannedStartAt && (
                          <span className="text-[11px] text-gray-400 font-medium">
                            {formatVietnamDate(item.plannedStartAt)}
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-4 font-semibold text-slate-700">
                      <div className="flex items-center gap-1.5">
                        <Folder className="w-4 h-4 text-amber-500 shrink-0" />
                        <span className="truncate max-w-[200px]" title={item.folderName || ''}>
                          {item.folderName || <span className="text-slate-400 italic">Chưa tạo thư mục</span>}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-4 text-center">
                      <span className="inline-flex items-center gap-1 px-2.5 py-1 bg-gray-100 rounded-full text-xs font-bold text-gray-700">
                        <ImageIcon className="w-3.5 h-3.5 text-gray-500" />
                        {item.activePhotoCount} ảnh
                      </span>
                    </td>
                    <td className="px-5 py-4 text-right">
                      <div className="flex items-center justify-end gap-2">
                        {isStudent ? (
                          <button
                            type="button"
                            onClick={() => setPhotoModalItem(item)}
                            className="px-3.5 py-1.5 rounded-xl text-xs font-extrabold text-white bg-[#f37021] hover:bg-[#e0611d] inline-flex items-center gap-1.5 transition-all shadow-sm active:scale-[0.98] cursor-pointer"
                          >
                            <Pencil className="w-3.5 h-3.5" /> Chỉnh sửa
                          </button>
                        ) : (
                          <>
                            <button
                              type="button"
                              onClick={() => setPhotoModalItem(item)}
                              className="px-3.5 py-1.5 rounded-xl text-xs font-extrabold text-white bg-[#004c91] hover:bg-[#00386b] inline-flex items-center gap-1.5 transition-all shadow-sm active:scale-[0.98] cursor-pointer"
                            >
                              <Eye className="w-3.5 h-3.5" /> Xem ảnh
                            </button>
                            <button
                              type="button"
                              onClick={() => setFaceScanModalItem(item)}
                              title="Quét mặt & gán danh tính"
                              className="px-2.5 py-1.5 rounded-xl text-xs font-extrabold bg-orange-50 hover:bg-orange-100 text-[#f37021] border border-orange-200 inline-flex items-center gap-1 transition-all shadow-sm active:scale-[0.98] cursor-pointer"
                            >
                              <Sparkles className="w-3.5 h-3.5 text-[#f37021]" /> Quét mặt
                            </button>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          /* Mode 2: Lưới Bức Ảnh Trực Tiếp (Direct Photo Grid Mode) */
          <div className="p-6 space-y-6">
            {items.map((item) => (
              <div key={item.visitInstanceId} className="bg-slate-50/60 rounded-2xl border border-slate-200 p-5 space-y-4 shadow-sm text-left">
                <div className="flex items-center justify-between flex-wrap gap-2 border-b border-slate-200 pb-3">
                  <div className="flex items-center gap-3">
                    <div className="w-8 h-8 rounded-xl bg-[#004c91]/10 text-[#004c91] flex items-center justify-center font-bold shrink-0">
                      <Folder className="w-4 h-4 text-amber-500" />
                    </div>
                    <div>
                      <h4 className="font-extrabold text-slate-900 text-sm flex items-center gap-2 flex-wrap">
                        {item.delegationName}
                        <span className="text-[11px] font-bold text-[#004c91] bg-blue-50 px-2 py-0.5 rounded-md border border-blue-100 font-normal">
                          {INSTANCE_STATUS_LABELS[item.instanceStatus] || item.instanceStatus}
                        </span>
                      </h4>
                      <p className="text-xs text-slate-500 font-medium">
                        {item.campusName || 'Cơ sở'} · {formatVietnamDate(item.plannedStartAt)} · {item.activePhotoCount} bức ảnh
                      </p>
                    </div>
                  </div>

                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      onClick={() => setFaceScanModalItem(item)}
                      className="px-3 py-1.5 rounded-xl text-xs font-extrabold bg-orange-50 hover:bg-orange-100 text-[#f37021] border border-orange-200 inline-flex items-center gap-1.5 transition-all shadow-sm active:scale-95 cursor-pointer"
                    >
                      <Sparkles className="w-3.5 h-3.5 text-[#f37021]" /> Quét mặt & Gán tên
                    </button>
                    <button
                      type="button"
                      onClick={() => setPhotoModalItem(item)}
                      className="px-3 py-1.5 rounded-xl text-xs font-extrabold text-white bg-[#004c91] hover:bg-[#00386b] inline-flex items-center gap-1.5 transition-all shadow-sm active:scale-95 cursor-pointer"
                    >
                      <Eye className="w-3.5 h-3.5" /> Quản lý ảnh
                    </button>
                  </div>
                </div>

                {/* Direct photo grid showing photo thumbnails */}
                <div className="bg-white rounded-xl p-3 border border-slate-200 shadow-xs">
                  <VisitPhotoPanel
                    visitInstanceId={item.visitInstanceId}
                    mode={isStudent ? 'edit' : 'view'}
                    showFaceTags={true}
                    columns={4}
                    onOpenFaceScan={() => setFaceScanModalItem(item)}
                  />
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Pagination Footer */}
        {!loading && !error && (
          <div className="px-6 py-4 border-t border-slate-100 flex flex-wrap items-center justify-between gap-3 bg-slate-50/50">
            <div className="flex items-center gap-4 flex-wrap">
              <p className="text-xs font-bold text-slate-500">
                Trang {page}/{Math.max(1, totalPages)} · Tổng cộng {data?.totalCount ?? 0} đoàn khách
              </p>
              <div className="flex items-center gap-2">
                <span className="text-xs text-slate-500 font-bold">Hiển thị:</span>
                <select
                  value={pageSize}
                  onChange={(e) => { setPageSize(Number(e.target.value)); setPage(1); }}
                  className="text-xs font-bold rounded-lg border border-slate-300 px-2.5 py-1 bg-white outline-none focus:border-[#004c91] cursor-pointer shadow-sm"
                >
                  <option value={5}>5 / trang</option>
                  <option value={10}>10 / trang</option>
                  <option value={20}>20 / trang</option>
                  <option value={50}>50 / trang</option>
                </select>
              </div>
            </div>
            {totalPages > 1 && (
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  className="p-2 rounded-xl border border-slate-200 text-slate-600 bg-white hover:bg-slate-50 disabled:opacity-40 transition-colors shadow-sm cursor-pointer"
                >
                  <ChevronLeft className="w-4 h-4" />
                </button>
                <button
                  type="button"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  className="p-2 rounded-xl border border-slate-200 text-slate-600 bg-white hover:bg-slate-50 disabled:opacity-40 transition-colors shadow-sm cursor-pointer"
                >
                  <ChevronRight className="w-4 h-4" />
                </button>
              </div>
            )}
          </div>
        )}
      </div>

      {/* Modal 1: Xem tất cả ảnh (Gallery view, hỗ trợ hiển thị danh tính khi xem full cho Staff) */}
      {photoModalItem && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm flex items-center justify-center z-[110] p-4 sm:p-6">
          <div className="bg-white rounded-3xl shadow-2xl w-full max-w-6xl max-h-[92vh] flex flex-col overflow-hidden animate-in zoom-in-95">
            <div className="px-6 py-3.5 border-b border-slate-100 flex items-center justify-between gap-3 bg-slate-50/50 shrink-0">
              <div className="min-w-0 flex items-center gap-3">
                <div className="w-9 h-9 rounded-xl bg-orange-50 text-[#f37021] flex items-center justify-center shrink-0 border border-orange-100 shadow-sm">
                  <Camera className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-base font-extrabold text-[#004c91] truncate">
                    {photoModalItem.delegationName}
                  </h3>
                  <p className="text-xs text-slate-500 font-medium truncate">
                    {photoModalItem.campusName} · {photoModalItem.activePhotoCount} bức ảnh
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => closePhotoModal(true)}
                className="p-2 rounded-xl text-slate-400 hover:text-slate-700 hover:bg-slate-200/60 transition-colors cursor-pointer"
                aria-label="Đóng"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="p-6 overflow-y-auto flex-1 bg-white">
              <VisitPhotoPanel
                visitInstanceId={photoModalItem.visitInstanceId}
                mode="edit"
                showFaceTags={!isStudent}
                onOpenFaceScan={!isStudent ? () => {
                  const target = photoModalItem;
                  setPhotoModalItem(null);
                  setFaceScanModalItem(target);
                } : undefined}
                onForbidden={() => {
                  toast.error('Bạn không có quyền xem ảnh của đoàn khách này.');
                  setPhotoModalItem(null);
                }}
              />
            </div>
          </div>
        </div>
      )}

      {/* Modal 2: Modal Quét mặt & Gán tên khuôn mặt */}
      {faceScanModalItem && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm flex items-center justify-center z-[110] p-4 sm:p-6">
          <div className="bg-white rounded-3xl shadow-2xl w-full max-w-6xl max-h-[92vh] flex flex-col overflow-hidden animate-in zoom-in-95">
            <FaceScanModalContent
              item={faceScanModalItem}
              onClose={(changed) => closeFaceScanModal(changed)}
            />
          </div>
        </div>
      )}
    </div>
  );
}


