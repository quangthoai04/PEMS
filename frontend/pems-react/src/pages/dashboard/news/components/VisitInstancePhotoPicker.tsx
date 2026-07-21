import React, { useEffect, useState } from 'react';
import { X, Check } from 'lucide-react';
import toast from 'react-hot-toast';
import { visitPhotosApi } from '../../../../features/delegations/api/visitPhotosApi';
import type { VisitInstancePhotoItem } from '../../../../features/delegations/types/visitPhotos.types';
import { useAuthenticatedImage } from '../../../../shared/hooks/useAuthenticatedImage';

function PhotoThumb({ url, alt }: { url: string; alt: string }) {
  const authSrc = useAuthenticatedImage(url);
  if (!authSrc) return <div className="w-full h-full bg-gray-100 animate-pulse" />;
  return <img src={authSrc} alt={alt} className="w-full h-full object-cover" />;
}

interface VisitInstancePhotoPickerProps {
  visitInstanceId: number;
  /** fileIds already attached to this section — shown as pre-checked / not re-added on pick. */
  alreadyPickedFileIds: number[];
  maxPickable: number;
  onClose: () => void;
  onPick: (photos: VisitInstancePhotoItem[]) => void;
}

/**
 * Lets a Staff/Student author reuse a photo the đoàn already has in its shared Drive folder
 * (e.g. one a Student uploaded via "Ảnh đoàn khách") instead of re-uploading a duplicate copy.
 * Picking here only references the existing fileId — no upload call is made.
 */
export function VisitInstancePhotoPicker({
  visitInstanceId, alreadyPickedFileIds, maxPickable, onClose, onPick,
}: VisitInstancePhotoPickerProps) {
  const [loading, setLoading] = useState(true);
  const [photos, setPhotos] = useState<VisitInstancePhotoItem[]>([]);
  const [selected, setSelected] = useState<Set<number>>(new Set());

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const data = await visitPhotosApi.byInstance(visitInstanceId);
        if (!cancelled) setPhotos(data.photos);
      } catch (err: any) {
        if (!cancelled) toast.error(err?.response?.data?.message ?? 'Không thể tải ảnh của đoàn.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [visitInstanceId]);

  function toggle(fileId: number) {
    if (alreadyPickedFileIds.includes(fileId)) return;
    setSelected(prev => {
      const next = new Set(prev);
      if (next.has(fileId)) {
        next.delete(fileId);
      } else {
        if (next.size >= maxPickable) {
          toast.error(`Chỉ có thể chọn thêm tối đa ${maxPickable} ảnh.`);
          return prev;
        }
        next.add(fileId);
      }
      return next;
    });
  }

  function confirm() {
    const picked = photos.filter(p => selected.has(p.fileId));
    onPick(picked);
    onClose();
  }

  return (
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 px-4 py-8">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-2xl max-h-[85vh] flex flex-col">
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <h3 className="text-lg font-bold text-gray-800">Chọn từ ảnh đoàn</h3>
          <button onClick={onClose} className="p-1.5 rounded-lg text-gray-400 hover:bg-gray-100 hover:text-gray-600 transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-6 overflow-y-auto flex-1">
          {loading ? (
            <div className="flex items-center justify-center py-12">
              <div className="w-6 h-6 border-2 border-[#004c91] border-t-transparent rounded-full animate-spin" />
            </div>
          ) : photos.length === 0 ? (
            <p className="text-sm text-gray-500 text-center py-12">Đoàn này chưa có ảnh nào được tải lên.</p>
          ) : (
            <div className="grid grid-cols-3 sm:grid-cols-4 gap-3">
              {photos.map(p => {
                const already = alreadyPickedFileIds.includes(p.fileId);
                const isSelected = selected.has(p.fileId);
                return (
                  <button
                    key={p.visitPhotoId}
                    type="button"
                    disabled={already}
                    onClick={() => toggle(p.fileId)}
                    className={`relative aspect-square rounded-xl overflow-hidden border-2 transition-colors ${
                      already ? 'border-gray-200 opacity-40 cursor-not-allowed'
                        : isSelected ? 'border-[#004c91]' : 'border-transparent hover:border-gray-300'
                    }`}
                    title={already ? 'Đã có trong mục này' : p.fileName}
                  >
                    <PhotoThumb url={p.url} alt={p.fileName} />
                    {(isSelected || already) && (
                      <div className="absolute inset-0 bg-[#004c91]/40 flex items-center justify-center">
                        <div className="w-6 h-6 bg-[#004c91] rounded-full flex items-center justify-center">
                          <Check className="w-4 h-4 text-white" />
                        </div>
                      </div>
                    )}
                  </button>
                );
              })}
            </div>
          )}
        </div>

        <div className="flex justify-end gap-3 px-6 py-4 border-t border-gray-100">
          <button
            onClick={onClose}
            className="px-5 py-2.5 border border-gray-300 text-gray-600 font-semibold rounded-xl hover:bg-gray-50 transition-colors text-sm"
          >
            Hủy
          </button>
          <button
            onClick={confirm}
            disabled={selected.size === 0}
            className="px-5 py-2.5 bg-[#004c91] text-white font-bold rounded-xl hover:bg-[#003a70] transition-colors text-sm disabled:opacity-50 disabled:cursor-not-allowed"
          >
            Thêm {selected.size > 0 ? `(${selected.size})` : ''}
          </button>
        </div>
      </div>
    </div>
  );
}
