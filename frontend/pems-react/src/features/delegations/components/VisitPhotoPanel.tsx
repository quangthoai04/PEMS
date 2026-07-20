/**
 * VisitPhotoPanel — khối "Ảnh đoàn khách" của Student (visit_photos, độc lập Gallery).
 *
 * Dùng CHUNG cho trang Đóng góp kết quả (phần Ảnh / Media) và tab "Quản lý ảnh đoàn khách"
 * (modal Xem chi tiết / Chỉnh sửa) để không nhân đôi logic upload/xóa. Backend là nguồn quyền
 * duy nhất (ACTIVE Student + ACCEPTED participant, chống IDOR): panel chỉ ẩn/hiện nút theo cờ
 * canUpload/canRemove trả về; 403/404 → gọi onForbidden (nơi nhúng tự quyết định ẩn khối).
 * Ảnh hiển thị qua proxy /api/files/{fileId}/content (JWT header — cần fetch blob).
 */
import { useCallback, useEffect, useRef, useState } from 'react';
import { ExternalLink, Image as ImageIcon, Video, Loader2, Trash2, UploadCloud, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { visitPhotosApi } from '../api/visitPhotosApi';
import type { VisitInstancePhotoItem, VisitInstancePhotos } from '../types/visitPhotos.types';
import { validateFile } from '../../../shared/utils/fileValidation';
import { useAuthenticatedImage } from '../../../shared/hooks/useAuthenticatedImage';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';

const errMsg = (e: any, fallback: string) => e?.response?.data?.message || fallback;

function PhotoTile({ photo, canManage, onRemove, onPreview }: {
  photo: VisitInstancePhotoItem;
  canManage: boolean;
  onRemove: (photo: VisitInstancePhotoItem) => void;
  onPreview: (photo: VisitInstancePhotoItem, imgUrl: string | null) => void;
}) {
  const imgUrl = useAuthenticatedImage(`/api${photo.url.replace(/^\/api/, '')}`);
  const isVideo = photo.fileName.toLowerCase().endsWith('.mp4') || photo.fileName.toLowerCase().endsWith('.webm');
  return (
    <div 
      className="relative group rounded-xl overflow-hidden border border-slate-200 aspect-square bg-slate-50 cursor-pointer"
      onClick={() => onPreview(photo, imgUrl)}
    >
      {imgUrl ? (
        isVideo ? (
          <video src={imgUrl} className="w-full h-full object-cover" />
        ) : (
          <img src={imgUrl} alt={photo.fileName} className="w-full h-full object-cover" />
        )
      ) : (
        <div className="w-full h-full flex items-center justify-center text-slate-300">
          {isVideo ? <Video className="w-8 h-8" /> : <ImageIcon className="w-8 h-8" />}
        </div>
      )}
      <div className="absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/70 to-transparent px-2 pt-6 pb-1.5 opacity-0 group-hover:opacity-100 transition-opacity">
        <p className="text-[11px] font-semibold text-white truncate" title={photo.fileName}>{photo.fileName}</p>
        <p className="text-[10px] text-white/80 truncate">
          {photo.uploadedByName} · {formatVietnamDateTime(photo.uploadedAt)}
        </p>
      </div>
      {canManage && photo.canRemove && (
        <button
          type="button"
          onClick={(e) => { e.stopPropagation(); onRemove(photo); }}
          title="Xóa ảnh"
          className="absolute top-1.5 right-1.5 p-1.5 rounded-lg bg-white/90 text-slate-500 hover:text-red-600 hover:bg-white shadow-sm opacity-0 group-hover:opacity-100 transition-all"
        >
          <Trash2 className="w-4 h-4" />
        </button>
      )}
    </div>
  );
}

interface Props {
  visitInstanceId: string | number;
  /** 'edit' hiện nút upload/xóa (theo cờ backend); 'view' chỉ liệt kê ảnh. */
  mode?: 'view' | 'edit';
  /** Gọi khi backend trả 403/404 — Student không thuộc scope; nơi nhúng tự ẩn khối. */
  onForbidden?: () => void;
  columns?: 4 | 6;
  maxInitialItems?: number;
}

export function VisitPhotoPanel({ 
  visitInstanceId, 
  mode = 'edit', 
  onForbidden,
  columns = 4,
  maxInitialItems = 24
}: Props) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [showAll, setShowAll] = useState(false);
  const [data, setData] = useState<VisitInstancePhotos | null>(null);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [removeTarget, setRemoveTarget] = useState<VisitInstancePhotoItem | null>(null);
  const [removeReason, setRemoveReason] = useState('');
  const [removing, setRemoving] = useState(false);
  const [previewData, setPreviewData] = useState<{ photo: VisitInstancePhotoItem, url: string | null } | null>(null);

  // Giữ callback qua ref để một inline arrow từ cha không làm `load` đổi identity mỗi render
  // (tránh useEffect refetch vô hạn).
  const onForbiddenRef = useRef(onForbidden);
  onForbiddenRef.current = onForbidden;

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(await visitPhotosApi.byInstance(visitInstanceId));
    } catch (e: any) {
      const status = e?.response?.status;
      setData(null);
      if (status === 403 || status === 404) onForbiddenRef.current?.();
      else toast.error(errMsg(e, 'Không thể tải ảnh đoàn khách.'));
    } finally {
      setLoading(false);
    }
  }, [visitInstanceId]);

  useEffect(() => { load(); }, [load]);

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
    if (files.length > 10) {
      toast.error('Chỉ được tải lên tối đa 10 ảnh mỗi lần.');
      return;
    }

    setUploading(true);
    const toastId = toast.loading('Đang tải lên...');
    try {
      await visitPhotosApi.upload(visitInstanceId, files);
      toast.success('Đã tải ảnh/video đoàn khách lên.', { id: toastId });
      await load();
    } catch (e: any) {
      toast.error(errMsg(e, 'Không thể tải lên. Vui lòng thử lại.'), { id: toastId });
    } finally {
      setUploading(false);
    }
  };

  const handleRemove = async () => {
    if (!removeTarget || !removeReason.trim()) return;
    setRemoving(true);
    const toastId = toast.loading('Đang xóa ảnh...');
    try {
      await visitPhotosApi.remove(removeTarget.visitPhotoId, removeReason.trim());
      toast.success('Đã xóa ảnh.', { id: toastId });
      setRemoveTarget(null);
      setRemoveReason('');
      await load();
    } catch (e: any) {
      toast.error(errMsg(e, 'Không thể xóa ảnh. Vui lòng thử lại.'), { id: toastId });
    } finally {
      setRemoving(false);
    }
  };

  if (loading) {
    return (
      <div className="py-6 flex items-center justify-center gap-2 text-slate-400 text-sm font-semibold">
        <Loader2 className="w-4 h-4 animate-spin" /> Đang tải ảnh đoàn khách...
      </div>
    );
  }
  if (!data) return null;

  const canManage = mode === 'edit';

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-xs font-bold text-slate-500">
          {data.folderName
            ? <>Thư mục: <span className="text-slate-700">{data.folderName}</span> · {data.photos.length} ảnh</>
            : <>{data.photos.length} ảnh</>}
        </p>
        {data.folderWebViewUrl && (
          <a href={data.folderWebViewUrl} target="_blank" rel="noreferrer"
            className="inline-flex items-center gap-1.5 text-xs font-bold text-[#004c91] hover:underline">
            <ExternalLink className="w-3.5 h-3.5" /> Mở thư mục Drive
          </a>
        )}
      </div>

      {data.photos.length === 0 ? (
        <p className="text-sm font-semibold text-slate-400">Chưa có ảnh đoàn khách nào được tải lên.</p>
      ) : (
        <div className="space-y-4">
          <div className={columns === 6 ? "grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-6 gap-3" : "grid grid-cols-2 sm:grid-cols-4 gap-3"}>
            {(showAll ? data.photos : data.photos.slice(0, maxInitialItems)).map((p) => (
              <PhotoTile key={p.visitPhotoId} photo={p} canManage={canManage}
                onRemove={(photo) => { setRemoveTarget(photo); setRemoveReason(''); }}
                onPreview={(photo, url) => setPreviewData({ photo, url })} />
            ))}
          </div>
          {!showAll && data.photos.length > maxInitialItems && (
            <div className="flex justify-center pt-2">
              <button
                type="button"
                onClick={() => setShowAll(true)}
                className="px-5 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-sm font-bold rounded-xl transition-colors"
              >
                Hiển thị thêm ({data.photos.length - maxInitialItems} ảnh)
              </button>
            </div>
          )}
          {showAll && data.photos.length > maxInitialItems && (
            <div className="flex justify-center pt-2">
              <button
                type="button"
                onClick={() => setShowAll(false)}
                className="px-5 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-sm font-bold rounded-xl transition-colors"
              >
                Thu gọn
              </button>
            </div>
          )}
        </div>
      )}

      {canManage && data.canUpload && (
        <div className="pt-1">
          <input
            type="file"
            multiple
            accept=".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp"
            className="hidden"
            ref={fileInputRef}
            onChange={handleUpload}
          />
          <button
            type="button"
            disabled={uploading}
            onClick={() => fileInputRef.current?.click()}
            className="inline-flex items-center gap-2 px-4 py-2 bg-blue-50 text-[#004c91] hover:bg-blue-100 rounded-lg text-sm font-bold transition-colors disabled:opacity-50"
          >
            <UploadCloud className="w-4 h-4" />
            {uploading ? 'Đang tải lên...' : 'Upload ảnh'}
          </button>
          <p className="mt-1.5 text-[11px] font-medium text-slate-400">
            JPG/JPEG/PNG/WEBP, tối đa 5MB/file, 10 file mỗi lần.
          </p>
        </div>
      )}

      {/* Popup xóa mềm — bắt buộc nhập lý do (removal_reason) */}
      {removeTarget && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-[120] px-4">
          <div className="bg-white rounded-2xl shadow-xl p-8 max-w-sm w-full">
            <div className="flex justify-center mb-4"><Trash2 className="w-12 h-12 text-red-500" /></div>
            <h3 className="text-lg font-bold text-gray-800 text-center mb-2">Xóa ảnh đoàn khách</h3>
            <p className="text-gray-500 text-center text-sm mb-4 break-all">
              Xóa ảnh "{removeTarget.fileName}"? Vui lòng nhập lý do xóa.
            </p>
            <textarea
              value={removeReason}
              onChange={(e) => setRemoveReason(e.target.value)}
              placeholder="Nhập lý do xóa..."
              rows={3}
              maxLength={500}
              className="w-full border border-gray-300 rounded-xl px-4 py-3 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-red-300 mb-1"
            />
            <p className="text-right text-xs text-gray-400 mb-5">{removeReason.length}/500</p>
            <div className="flex gap-3">
              <button onClick={() => setRemoveTarget(null)} disabled={removing}
                className="flex-1 border border-gray-300 text-gray-600 font-semibold py-2.5 rounded-xl hover:bg-gray-50 transition-colors inline-flex items-center justify-center gap-2">
                <X className="w-4 h-4" /> Hủy
              </button>
              <button onClick={handleRemove} disabled={removing || !removeReason.trim()}
                className="flex-1 bg-red-500 text-white font-semibold py-2.5 rounded-xl hover:bg-red-600 transition-colors flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed">
                {removing && <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />}
                Xác nhận xóa
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Popup xem ảnh phóng to */}
      {previewData && (
        <div 
          className="fixed inset-0 bg-black/90 flex items-center justify-center z-[130] p-4 sm:p-8"
          onClick={() => setPreviewData(null)}
        >
          <button 
            className="absolute top-4 right-4 p-2 text-white/70 hover:text-white bg-black/50 rounded-full transition-colors z-[140]"
            onClick={(e) => { e.stopPropagation(); setPreviewData(null); }}
          >
            <X className="w-6 h-6" />
          </button>
          {previewData.url ? (
            previewData.photo.fileName.toLowerCase().endsWith('.mp4') || previewData.photo.fileName.toLowerCase().endsWith('.webm') ? (
              <video 
                src={previewData.url} 
                controls
                autoPlay
                className="max-w-full max-h-full rounded-lg shadow-2xl"
                onClick={(e) => e.stopPropagation()}
              />
            ) : (
              <img 
                src={previewData.url} 
                alt={previewData.photo.fileName} 
                className="max-w-full max-h-full object-contain rounded-lg shadow-2xl"
                onClick={(e) => e.stopPropagation()}
              />
            )
          ) : (
            <div className="flex flex-col items-center text-white/70" onClick={(e) => e.stopPropagation()}>
              <ImageIcon className="w-16 h-16 mb-4 opacity-50" />
              <p>Không thể tải file</p>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
