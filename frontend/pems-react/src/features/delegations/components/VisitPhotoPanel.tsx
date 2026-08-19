import { useCallback, useEffect, useRef, useState } from 'react';
import { ExternalLink, Image as ImageIcon, Video, Loader2, Trash2, UploadCloud, X, Sparkles } from 'lucide-react';
import toast from 'react-hot-toast';
import { visitPhotosApi } from '../api/visitPhotosApi';
import type { VisitInstancePhotoItem, VisitInstancePhotos, VisitPhotoFaceDetection } from '../types/visitPhotos.types';
import { validateFile } from '../../../shared/utils/fileValidation';
import { useAuthenticatedImage } from '../../../shared/hooks/useAuthenticatedImage';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';

const errMsg = (e: any, fallback: string) => e?.response?.data?.message || fallback;

function PhotoTile({ photo, canManage, onPreview, selected, onToggleSelect }: {
  photo: VisitInstancePhotoItem;
  canManage: boolean;
  selected: boolean;
  onPreview: (photo: VisitInstancePhotoItem, imgUrl: string | null) => void;
  onToggleSelect: (photo: VisitInstancePhotoItem, selected: boolean) => void;
}) {
  const imgUrl = useAuthenticatedImage(`/api${photo.url.replace(/^\/api/, '')}`);
  const isVideo = photo.fileName.toLowerCase().endsWith('.mp4') || photo.fileName.toLowerCase().endsWith('.webm');
  return (
    <div 
      className={`relative group rounded-xl overflow-hidden border aspect-square bg-slate-50 cursor-pointer transition-all ${selected ? 'border-[#004c91] ring-2 ring-[#004c91] shadow-md' : 'border-slate-200 hover:border-slate-300'}`}
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
        <div
          className={`absolute top-2 right-2 ${selected ? 'opacity-100' : 'opacity-0 group-hover:opacity-100'} transition-opacity p-1.5`}
          onClick={(e) => { e.stopPropagation(); onToggleSelect(photo, !selected); }}
        >
          <div className={`w-5 h-5 rounded flex items-center justify-center border shadow-sm ${selected ? 'bg-[#004c91] border-[#004c91]' : 'bg-white/90 border-slate-300 hover:bg-white'}`}>
            {selected && <svg className="w-3.5 h-3.5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={3} d="M5 13l4 4L19 7" /></svg>}
          </div>
        </div>
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
  /** Nếu true (dành cho Staff/Staff Leader), khi click xem ảnh full sẽ tự động tải & hiển thị danh tính đã gán */
  showFaceTags?: boolean;
  /** Nút mở modal quét mặt (nếu được truyền vào) */
  onOpenFaceScan?: () => void;
}

export function VisitPhotoPanel({ 
  visitInstanceId, 
  mode = 'edit', 
  onForbidden,
  columns = 4,
  maxInitialItems = 24,
  showFaceTags = false,
  onOpenFaceScan,
}: Props) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [showAll, setShowAll] = useState(false);
  const [data, setData] = useState<VisitInstancePhotos | null>(null);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [showRemovePrompt, setShowRemovePrompt] = useState(false);
  const [removeReason, setRemoveReason] = useState('');
  const [removing, setRemoving] = useState(false);
  const [previewData, setPreviewData] = useState<{ photo: VisitInstancePhotoItem, url: string | null } | null>(null);

  const [previewFaceDetections, setPreviewFaceDetections] = useState<VisitPhotoFaceDetection[]>([]);

  // Tải thông tin gán khuôn mặt khi mở ảnh xem full nếu showFaceTags = true
  useEffect(() => {
    if (!previewData || !showFaceTags) {
      setPreviewFaceDetections([]);
      return;
    }
    let cancelled = false;
    visitPhotosApi.getFaceScans(previewData.photo.visitPhotoId)
      .then((scans) => {
        if (cancelled) return;
        const latest = scans[0];
        if (latest && (latest.status === 'SUCCEEDED' || latest.status === 'CONFIRMED')) {
          setPreviewFaceDetections(latest.detections ?? []);
        } else {
          setPreviewFaceDetections([]);
        }
      })
      .catch(() => {
        if (!cancelled) setPreviewFaceDetections([]);
      });
    return () => { cancelled = true; };
  }, [previewData, showFaceTags]);

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
      toast.success('Đã tải ảnh đoàn khách lên.', { id: toastId });
      await load();
    } catch (e: any) {
      toast.error(errMsg(e, 'Không thể tải lên. Vui lòng thử lại.'), { id: toastId });
    } finally {
      setUploading(false);
    }
  };

  const handleRemove = async () => {
    if (selectedIds.size === 0 || !removeReason.trim()) return;
    setRemoving(true);
    const toastId = toast.loading(`Đang xóa ${selectedIds.size} ảnh...`);
    try {
      const promises = Array.from(selectedIds).map(id => visitPhotosApi.remove(id, removeReason.trim()));
      await Promise.all(promises);
      toast.success(`Đã xóa ${selectedIds.size} ảnh.`, { id: toastId });
      setSelectedIds(new Set());
      setShowRemovePrompt(false);
      setRemoveReason('');
      await load();
    } catch (e: any) {
      toast.error(errMsg(e, 'Không thể xóa ảnh. Vui lòng thử lại.'), { id: toastId });
    } finally {
      setRemoving(false);
    }
  };

  const toggleSelect = (photo: VisitInstancePhotoItem, isSelected: boolean) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (isSelected) next.add(photo.visitPhotoId);
      else next.delete(photo.visitPhotoId);
      return next;
    });
  };

  if (loading) {
    return (
      <div className="py-6 flex items-center justify-center gap-2 text-slate-400 text-sm font-normal">
        <Loader2 className="w-4 h-4 animate-spin" /> Đang tải ảnh đoàn khách...
      </div>
    );
  }
  if (!data) return null;

  const canManage = mode === 'edit';

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-xs font-normal text-slate-500">
          {data.folderName
            ? <>Thư mục: <span className="text-slate-700">{data.folderName}</span> · {data.photos.length} ảnh</>
            : <>{data.photos.length} ảnh</>}
        </p>
        <div className="flex items-center gap-3">
          {onOpenFaceScan && (
            <button
              type="button"
              onClick={onOpenFaceScan}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-orange-50 hover:bg-orange-100 text-[#f37021] border border-orange-200 rounded-xl text-xs font-extrabold transition-all shadow-sm active:scale-95 cursor-pointer"
            >
              <Sparkles className="w-3.5 h-3.5" /> Quét mặt
            </button>
          )}
          {data.folderWebViewUrl && (
            <a href={data.folderWebViewUrl} target="_blank" rel="noreferrer"
              className="inline-flex items-center gap-1.5 text-xs font-bold text-[#004c91] hover:underline">
              <ExternalLink className="w-3.5 h-3.5" /> Mở thư mục Drive
            </a>
          )}
        </div>
      </div>

      {data.photos.length === 0 ? (
        <p className="text-sm font-normal text-slate-400">Chưa có ảnh đoàn khách nào được tải lên.</p>
      ) : (
        <div className="space-y-4">
          <div className={columns === 6 ? "grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-6 gap-3" : "grid grid-cols-2 sm:grid-cols-4 gap-3"}>
            {(showAll ? data.photos : data.photos.slice(0, maxInitialItems)).map((p) => (
              <PhotoTile key={p.visitPhotoId} photo={p} canManage={canManage}
                selected={selectedIds.has(p.visitPhotoId)}
                onToggleSelect={toggleSelect}
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

      {canManage && (data.canUpload || selectedIds.size > 0) && (
        <div className="pt-1 flex flex-wrap items-center gap-3">
          {data.canUpload && (
            <div>
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
            </div>
          )}
          {selectedIds.size > 0 && (
            <button
              type="button"
              onClick={() => setShowRemovePrompt(true)}
              className="inline-flex items-center gap-2 px-4 py-2 bg-rose-50 text-rose-600 hover:bg-rose-100 rounded-lg text-sm font-bold transition-colors"
            >
              <Trash2 className="w-4 h-4" />
              Xóa {selectedIds.size} ảnh đã chọn
            </button>
          )}
        </div>
      )}
      {canManage && data.canUpload && (
        <p className="mt-0.5 text-[11px] font-normal text-slate-400">
          JPG/JPEG/PNG/WEBP, tối đa 5MB/file, 10 file mỗi lần.
        </p>
      )}

      {/* Popup xóa mềm — bắt buộc nhập lý do (removal_reason) */}
      {showRemovePrompt && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-[120] p-4">
          {/* Overlay gutter is p-4 (2rem) on every side now (was px-4 only, so a short landscape
              viewport had zero vertical margin); max-h + overflow-y-auto is the same safety net
              every other modal in this pass uses so the confirm button stays reachable. */}
          <div className="bg-white rounded-2xl shadow-xl p-8 max-w-sm w-full max-h-[calc(100dvh-2rem)] overflow-y-auto">
            <div className="flex justify-center mb-4"><Trash2 className="w-12 h-12 text-red-500" /></div>
            <h3 className="text-lg font-bold text-gray-800 text-center mb-2">Xóa ảnh đoàn khách</h3>
            <p className="text-gray-500 text-center text-sm mb-4">
              Bạn đang chọn xóa {selectedIds.size} ảnh. Vui lòng nhập lý do xóa.
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
              <button onClick={() => setShowRemovePrompt(false)} disabled={removing}
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

      {/* Popup xem ảnh phóng to (Có hiển thị nhãn danh tính nếu showFaceTags = true) */}
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
              <div className="relative max-w-full max-h-[calc(100dvh-2rem)] flex items-center justify-center select-none" onClick={(e) => e.stopPropagation()}>
                <img
                  src={previewData.url}
                  alt={previewData.photo.fileName}
                  className="max-w-full max-h-[calc(100dvh-2rem)] object-contain rounded-lg shadow-2xl"
                />
                {showFaceTags && previewFaceDetections.length > 0 && (
                  previewFaceDetections.map((detection, idx) => {
                    const name = detection.guestFullName;
                    // Bỏ hoàn toàn nhãn cho khuôn mặt chưa được gán tên
                    if (!name) return null;

                    const isTopEdge = detection.boundingBoxY < 0.2;
                    // Stem height 24px, 44px, 64px vươn cao hẳn lên khỏi vị trí mặt người đằng sau
                    const stemHeight = 24 + (idx % 3) * 20;

                    return (
                      <div
                        key={detection.faceDetectionId}
                        style={{
                          left: `${detection.boundingBoxX * 100}%`,
                          top: `${detection.boundingBoxY * 100}%`,
                          width: `${detection.boundingBoxWidth * 100}%`,
                          height: `${detection.boundingBoxHeight * 100}%`,
                        }}
                        className="absolute border-[1.5px] border-emerald-400 bg-emerald-400/10 pointer-events-none rounded-sm z-20"
                      >
                        {/* Chấm ghim ở viền khung mặt */}
                        <div className={`absolute left-1/2 -translate-x-1/2 w-1 h-1 rounded-full bg-emerald-400 ${isTopEdge ? '-bottom-0.5' : '-top-0.5'}`} />

                        {/* Đường kẻ nối siêu mảnh 1px vươn cao */}
                        <div
                          style={{ height: `${stemHeight}px` }}
                          className={`absolute left-1/2 -translate-x-1/2 w-[1px] bg-emerald-400/90 pointer-events-none ${
                            isTopEdge ? 'top-full' : 'bottom-full'
                          }`}
                        />

                        {/* Thẻ tên siêu nhỏ gọn, phong cách pill tối màu mỏng nhẹ. Anchor flips near
                            the image edges (same boundingBoxX heuristic as FaceScanPanel) so the
                            label doesn't render past the viewport edge for an edge-detected face. */}
                        <div
                          style={isTopEdge ? { top: `calc(100% + ${stemHeight}px)` } : { bottom: `calc(100% + ${stemHeight}px)` }}
                          className={`absolute whitespace-nowrap z-30 ${
                            detection.boundingBoxX < 0.25
                              ? 'left-0'
                              : detection.boundingBoxX > 0.75
                                ? 'right-0'
                                : 'left-1/2 -translate-x-1/2'
                          }`}
                        >
                          <span
                            className="px-1.5 py-0.5 rounded-full text-[7px] sm:text-[8px] font-semibold leading-none tracking-tight block max-w-[80px] sm:max-w-[95px] truncate bg-emerald-950/90 text-emerald-200 border border-emerald-400/50 shadow-md backdrop-blur-xs"
                            title={name}
                          >
                            {name}
                          </span>
                        </div>
                      </div>
                    );
                  })
                )}
              </div>
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

