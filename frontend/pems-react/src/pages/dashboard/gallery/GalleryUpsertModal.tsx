/** Create (UC-GAL-04) / Edit (UC-GAL-07) modal for a gallery item. */

import React, { useEffect, useMemo, useState } from 'react';
import { motion } from 'motion/react';
import { X, Upload, Trash2, Star, Loader2, CheckCircle2, AlertCircle, ImageOff } from 'lucide-react';
import { useAuthenticatedMedia } from '../../../shared/hooks/useAuthenticatedImage';
import { validateFile } from '../../../shared/utils/fileValidation';
import { galleryManagementApi } from '../../../features/gallery-management/api/galleryManagementApi';
import { getGalleryErrorMessage } from '../../../features/gallery-management/api/galleryError';
import type {
  GalleryAreaOption,
  GalleryItemDetail,
} from '../../../features/gallery-management/types/galleryManagement.types';

type Mode = 'create' | 'edit';
const MEDIA_ACCEPT = 'image/jpeg,image/png,image/webp,video/mp4,video/webm';
const MAX_FILES = 20;

/** True when a picked file is a video (by MIME or extension), so previews/validation pick the right kind. */
function isVideoFile(file: File): boolean {
  if (file.type) return file.type.startsWith('video/');
  return /\.(mp4|webm)$/i.test(file.name);
}

/** Existing (server-stored) media thumbnail with keep/remove + set-primary controls. */
function ExistingMediaCard({
  fileUrl,
  mediaType,
  kept,
  isPrimary,
  onToggleKeep,
  onSetPrimary,
}: {
  fileUrl: string;
  mediaType: 'IMAGE' | 'VIDEO';
  kept: boolean;
  isPrimary: boolean;
  onToggleKeep: () => void;
  onSetPrimary: () => void;
}) {
  const { url, status } = useAuthenticatedMedia(fileUrl);
  return (
    <div className={`relative rounded-xl overflow-hidden border-2 ${isPrimary ? 'border-[#f37021]' : 'border-slate-200'} ${kept ? '' : 'opacity-40'}`}>
      <div className="w-full h-24 bg-slate-100">
        {url ? (
          mediaType === 'VIDEO'
            ? <video src={url} className="w-full h-full object-cover" muted />
            : <img src={url} className="w-full h-full object-cover" alt="" />
        ) : (
          <div className="w-full h-full flex items-center justify-center">
            {status === 'error'
              ? <ImageOff className="w-4 h-4 text-slate-300" />
              : <Loader2 className="w-4 h-4 text-slate-300 animate-spin" />}
          </div>
        )}
      </div>
      <div className="absolute top-1 left-1 flex gap-1">
        {kept && (
          <button
            type="button"
            onClick={onSetPrimary}
            title="Đặt làm ảnh chính"
            className={`p-1 rounded ${isPrimary ? 'bg-[#f37021] text-white' : 'bg-white/90 text-slate-500 hover:text-[#f37021]'}`}
          >
            <Star className="w-3.5 h-3.5" fill={isPrimary ? 'currentColor' : 'none'} />
          </button>
        )}
      </div>
      <button
        type="button"
        onClick={onToggleKeep}
        title={kept ? 'Bỏ media này' : 'Giữ lại media này'}
        className={`absolute top-1 right-1 p-1 rounded ${kept ? 'bg-white/90 text-red-500 hover:bg-red-50' : 'bg-green-600 text-white'}`}
      >
        {kept ? <Trash2 className="w-3.5 h-3.5" /> : <CheckCircle2 className="w-3.5 h-3.5" />}
      </button>
    </div>
  );
}

interface KeptMediaState {
  mediaId: number;
  mediaType: 'IMAGE' | 'VIDEO';
  fileUrl: string;
  thumbnailUrl?: string | null;
  kept: boolean;
}

export function GalleryUpsertModal({
  mode,
  areas,
  existing,
  onClose,
  onCreated,
  onUpdated,
  onError,
}: {
  mode: Mode;
  areas: GalleryAreaOption[];
  existing?: GalleryItemDetail;
  onClose: () => void;
  onCreated: () => void;
  onUpdated: (updated: GalleryItemDetail) => void;
  onError: (message: string) => void;
}) {
  const activeAreas = useMemo(() => areas.filter((a) => a.status === 'ACTIVE' || a.areaId === existing?.area.areaId), [areas, existing]);

  const [title, setTitle] = useState(existing?.title ?? '');
  const [description, setDescription] = useState(existing?.description ?? '');
  const [areaId, setAreaId] = useState<number | ''>(existing?.area.areaId ?? '');
  const [locationId, setLocationId] = useState<number | ''>(existing?.location.locationId ?? '');
  const [newFiles, setNewFiles] = useState<File[]>([]);
  const [keptMedia, setKeptMedia] = useState<KeptMediaState[]>(
    (existing?.media ?? []).map((m) => ({
      mediaId: m.mediaId,
      mediaType: m.mediaType,
      fileUrl: m.fileUrl,
      thumbnailUrl: m.thumbnailUrl,
      kept: true,
    })),
  );
  const [primaryMediaId, setPrimaryMediaId] = useState<number | null>(
    existing?.media.find((m) => m.isPrimary)?.mediaId ?? null,
  );
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const locations = useMemo(() => {
    const area = activeAreas.find((a) => a.areaId === areaId);
    if (!area) return [];
    return area.locations.filter((l) => l.status === 'ACTIVE' || l.locationId === existing?.location.locationId);
  }, [activeAreas, areaId, existing]);

  // Object URLs for new-file previews (revoked on change/unmount).
  const previews = useMemo(() => newFiles.map((f) => ({ file: f, url: URL.createObjectURL(f) })), [newFiles]);
  useEffect(() => () => previews.forEach((p) => URL.revokeObjectURL(p.url)), [previews]);

  const keptCount = keptMedia.filter((m) => m.kept).length;
  const totalAfter = keptCount + newFiles.length;
  const slotsLeft = MAX_FILES - totalAfter;

  const handleAddFiles = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!e.target.files) return;
    const incoming = Array.from(e.target.files) as File[];
    const accepted: File[] = [];
    for (const file of incoming) {
      // A gallery may mix images and videos — accept a file if it passes EITHER rule.
      const asImage = validateFile(file, 'GALLERY_IMAGE');
      const asVideo = validateFile(file, 'GALLERY_VIDEO');
      if (!asImage.ok && !asVideo.ok) {
        setFormError(`${file.name}: ${isVideoFile(file) ? asVideo.message : asImage.message}`);
        continue;
      }
      accepted.push(file);
    }
    const room = MAX_FILES - keptCount - newFiles.length;
    if (accepted.length > room) {
      setFormError(`Gallery item chỉ được có tối đa ${MAX_FILES} tệp.`);
    }
    setNewFiles((prev) => [...prev, ...accepted].slice(0, MAX_FILES - keptCount));
    e.target.value = '';
  };

  const removeNewFile = (idx: number) => setNewFiles((prev) => prev.filter((_, i) => i !== idx));

  const toggleKeep = (mediaId: number) => {
    setKeptMedia((prev) => prev.map((m) => (m.mediaId === mediaId ? { ...m, kept: !m.kept } : m)));
    setPrimaryMediaId((cur) => (cur === mediaId ? null : cur));
  };

  const validate = (): string | null => {
    if (!title.trim()) return 'Vui lòng nhập tiêu đề.';
    if (!description.trim()) return 'Vui lòng nhập mô tả.';
    if (areaId === '') return 'Vui lòng chọn khu vực.';
    if (locationId === '') return 'Vui lòng chọn vị trí.';
    if (mode === 'create' && newFiles.length === 0) return 'Vui lòng chọn ít nhất một tệp media.';
    if (mode === 'edit' && totalAfter === 0) return 'Gallery item phải có ít nhất một file media.';
    return null;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const err = validate();
    if (err) {
      setFormError(err);
      return;
    }
    setFormError(null);
    setSubmitting(true);
    try {
      if (mode === 'create') {
        await galleryManagementApi.createGalleryItem({
          title: title.trim(),
          description: description.trim(),
          locationId: Number(locationId),
          status: 'PUBLISHED',
          files: newFiles,
        });
        onCreated();
      } else if (existing) {
        const updated = await galleryManagementApi.updateGalleryItem({
          galleryItemId: existing.galleryItemId,
          title: title.trim(),
          description: description.trim(),
          locationId: Number(locationId),
          keepMediaIds: keptMedia.filter((m) => m.kept).map((m) => m.mediaId),
          newFiles,
          primaryMediaId: primaryMediaId ?? undefined,
        });
        onUpdated(updated);
      }
    } catch (error) {
      const msg = getGalleryErrorMessage(error);
      setFormError(msg);
      onError(msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-auto bg-black/60 backdrop-blur-sm p-4 font-sans">
      <motion.div
        initial={{ opacity: 0, scale: 0.95 }}
        animate={{ opacity: 1, scale: 1 }}
        exit={{ opacity: 0, scale: 0.95 }}
        className="bg-white rounded-3xl w-full max-w-4xl overflow-hidden shadow-2xl relative focus:outline-none flex flex-col md:flex-row max-h-[92vh]"
      >
        {/* Left: media upload */}
        <div className="w-full md:w-5/12 p-6 bg-slate-50 border-r border-slate-200 flex flex-col overflow-y-auto">
          <h3 className="text-xl font-black text-[#004c91] mb-4">{mode === 'edit' ? 'Sửa media' : 'Upload media'}</h3>

          <label className="border-2 border-dashed border-[#004c91]/30 rounded-2xl p-6 flex flex-col items-center justify-center bg-white hover:bg-blue-50/50 hover:border-[#004c91]/50 transition-colors cursor-pointer group mb-4 min-h-[140px]">
            <div className="w-14 h-14 bg-[#ebf5ff] text-[#004c91] rounded-2xl flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
              <Upload className="w-6 h-6" />
            </div>
            <p className="text-sm font-bold text-gray-700 text-center">
              {slotsLeft > 0 ? 'Click để chọn files' : `Đã đủ ${MAX_FILES} tệp`}
            </p>
            <p className="text-xs text-slate-400 mt-1 text-center">
              Ảnh (JPG/PNG/WEBP ≤5MB) hoặc Video (MP4/WEBM ≤100MB) · tối đa {MAX_FILES} tệp
            </p>
            <input type="file" multiple accept={MEDIA_ACCEPT} className="hidden" onChange={handleAddFiles} disabled={slotsLeft <= 0} />
          </label>

          {/* Existing media (edit) */}
          {mode === 'edit' && keptMedia.length > 0 && (
            <div className="mb-4">
              <p className="text-xs font-bold text-slate-500 uppercase tracking-wide mb-2">Media hiện có</p>
              <div className="grid grid-cols-3 gap-2">
                {keptMedia.map((m) => (
                  <ExistingMediaCard
                    key={m.mediaId}
                    fileUrl={m.thumbnailUrl || m.fileUrl}
                    mediaType={m.mediaType}
                    kept={m.kept}
                    isPrimary={primaryMediaId === m.mediaId}
                    onToggleKeep={() => toggleKeep(m.mediaId)}
                    onSetPrimary={() => setPrimaryMediaId(m.mediaId)}
                  />
                ))}
              </div>
            </div>
          )}

          {/* New files */}
          <div className="space-y-2">
            <p className="text-xs font-bold text-slate-500 uppercase tracking-wide">
              Tệp mới ({newFiles.length}) · Tổng {totalAfter}/{MAX_FILES}
            </p>
            {previews.map((p, idx) => (
              <div key={idx} className="flex items-center justify-between p-2 rounded-xl bg-white border border-slate-200 shadow-sm">
                <div className="flex items-center gap-3 min-w-0">
                  <div className="w-10 h-10 rounded-lg overflow-hidden shrink-0 bg-slate-100">
                    {isVideoFile(p.file)
                      ? <video src={p.url} className="w-full h-full object-cover" muted />
                      : <img src={p.url} className="w-full h-full object-cover" alt="" />}
                  </div>
                  <p className="text-xs font-bold text-gray-700 truncate">{p.file.name}</p>
                </div>
                <button type="button" onClick={() => removeNewFile(idx)} className="p-2 text-slate-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors shrink-0">
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>
            ))}
          </div>
        </div>

        {/* Right: metadata form */}
        <div className="w-full md:w-7/12 p-6 md:p-8 flex flex-col overflow-y-auto">
          <div className="flex justify-between items-center mb-6 border-b border-slate-100 pb-4">
            <h3 className="text-xl font-black text-gray-800">{mode === 'edit' ? 'Chỉnh sửa gallery item' : 'Thông tin chi tiết'}</h3>
            <button onClick={onClose} className="w-8 h-8 rounded-full flex items-center justify-center text-slate-500 bg-white hover:text-red-500 hover:bg-red-50 transition-colors border border-slate-200 shadow-sm">
              <X className="w-4 h-4" />
            </button>
          </div>

          <form onSubmit={handleSubmit} className="space-y-5 flex-1 flex flex-col">
            {formError && (
              <div className="flex items-start gap-2 p-3 rounded-xl bg-red-50 border border-red-200 text-red-700 text-sm font-medium">
                <AlertCircle className="w-4 h-4 mt-0.5 shrink-0" />
                <span>{formError}</span>
              </div>
            )}

            <div className="space-y-1.5">
              <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Tiêu đề <span className="text-red-500">*</span></label>
              <input
                type="text"
                value={title}
                maxLength={255}
                onChange={(e) => setTitle(e.target.value)}
                className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium transition-all"
                placeholder="Nhập tiêu đề..."
              />
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
              <div className="space-y-1.5">
                <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Danh mục tòa/khu <span className="text-red-500">*</span></label>
                <select
                  value={areaId === '' ? '' : String(areaId)}
                  onChange={(e) => { setAreaId(e.target.value === '' ? '' : Number(e.target.value)); setLocationId(''); }}
                  className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium transition-all appearance-none bg-slate-50 focus:bg-white"
                >
                  <option value="">-- Chọn khu vực --</option>
                  {activeAreas.map((a) => <option key={a.areaId} value={a.areaId}>{a.areaName}</option>)}
                </select>
              </div>

              <div className="space-y-1.5">
                <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Vị trí thực tế <span className="text-red-500">*</span></label>
                <select
                  value={locationId === '' ? '' : String(locationId)}
                  onChange={(e) => setLocationId(e.target.value === '' ? '' : Number(e.target.value))}
                  disabled={areaId === ''}
                  className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium transition-all appearance-none bg-slate-50 focus:bg-white disabled:opacity-50"
                >
                  <option value="">-- Chọn vị trí --</option>
                  {locations.map((l) => <option key={l.locationId} value={l.locationId}>{l.locationName}</option>)}
                </select>
              </div>

            </div>

            <div className="space-y-1.5 flex-1">
              <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Mô tả <span className="text-red-500">*</span></label>
              <textarea
                rows={3}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium transition-all resize-none"
                placeholder="Nhập mô tả về tài nguyên..."
              />
            </div>

            <div className="flex items-center justify-end gap-3 pt-4 border-t border-slate-100 mt-auto">
              <button
                type="button"
                onClick={onClose}
                disabled={submitting}
                className="px-6 py-2.5 rounded-xl font-bold text-slate-500 bg-slate-100 hover:bg-slate-200 transition-colors disabled:opacity-60"
              >
                Hủy bỏ
              </button>
              <button
                type="submit"
                disabled={submitting}
                className="px-6 py-2.5 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#00386b] transition-colors shadow-sm flex items-center gap-2 disabled:opacity-60"
              >
                {submitting && <Loader2 className="w-4 h-4 animate-spin" />}
                {mode === 'edit' ? 'Lưu thay đổi' : 'Hoàn tất upload'}
              </button>
            </div>
          </form>
        </div>
      </motion.div>
    </div>
  );
}

export default GalleryUpsertModal;
