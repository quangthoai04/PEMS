/** Create (UC-GAL-04) / Edit (UC-GAL-07) modal for a gallery item. Media can be uploaded images and/or
 * external YouTube videos added by URL. Every item requires bilingual content: a Vietnamese + English
 * description and a Staff-Leader-uploaded audio recording for each language (managed in the language
 * tabs). Audio can never be removed — only kept or replaced. */

import React, { useEffect, useMemo, useRef, useState } from 'react';
import { motion } from 'motion/react';
import { X, Upload, Trash2, Star, Loader2, CheckCircle2, AlertCircle, ImageOff, Youtube, Plus, Play, Music, RefreshCw } from 'lucide-react';
import { useAuthenticatedMedia } from '../../../shared/hooks/useAuthenticatedImage';
import { validateFile } from '../../../shared/utils/fileValidation';
import { parseYouTubeVideoId, youtubeWatchUrl, youtubeEmbedUrl, youtubeThumbnailUrl } from '../../../shared/utils/youtube';
import { galleryManagementApi } from '../../../features/gallery-management/api/galleryManagementApi';
import { getGalleryErrorMessage } from '../../../features/gallery-management/api/galleryError';
import {
  EnFieldHint,
  TranslateButton,
  fieldBlockingError,
  fieldPayload,
  normalizeVi,
  useBilingualField,
} from './locationModalShared';
import type {
  GalleryAreaOption,
  GalleryAudioInfo,
  GalleryItemDetail,
  GalleryItemType,
} from '../../../features/gallery-management/types/galleryManagement.types';

type Mode = 'create' | 'edit';
type Lang = 'vi' | 'en';
// Machine uploads accept images only — videos are added via a YouTube link, never uploaded as files.
const MEDIA_ACCEPT = 'image/jpeg,image/png,image/webp';
const AUDIO_ACCEPT = 'audio/mpeg,audio/mp3,audio/wav,audio/x-wav,.mp3,.wav';
const MAX_FILES = 20;
const MAX_AUDIO_BYTES = 20 * 1024 * 1024;

/** Which media is primary — stable across list reorders (existing = mediaId; new = a stable local id). */
type PrimarySel =
  | { kind: 'existing'; id: number }
  | { kind: 'upload'; id: string }
  | { kind: 'youtube'; id: string }
  | null;

interface NewFileEntry {
  id: string;
  file: File;
}
interface YoutubeEntry {
  id: string;
  url: string; // canonical watch URL sent to the backend
  videoId: string;
}
interface KeptMediaState {
  mediaId: number;
  mediaType: 'IMAGE' | 'VIDEO';
  sourceType: 'UPLOADED_FILE' | 'YOUTUBE';
  fileUrl?: string | null;
  thumbnailUrl?: string | null;
  youtubeVideoId?: string | null;
  kept: boolean;
}

let localIdSeq = 0;
const nextLocalId = () => `m${Date.now().toString(36)}_${localIdSeq++}`;

/** True when a picked file is a video (by MIME or extension), so previews/validation pick the right kind. */
function isVideoFile(file: File): boolean {
  if (file.type) return file.type.startsWith('video/');
  return /\.(mp4|webm)$/i.test(file.name);
}

/** A small round "set primary" star button shared by every media card. */
function PrimaryStar({ isPrimary, onClick }: { isPrimary: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      title="Đặt làm media chính"
      className={`p-1 rounded ${isPrimary ? 'bg-[#f37021] text-white' : 'bg-white/90 text-slate-500 hover:text-[#f37021]'}`}
    >
      <Star className="w-3.5 h-3.5" fill={isPrimary ? 'currentColor' : 'none'} />
    </button>
  );
}

/** Existing UPLOADED media thumbnail (served through the authenticated proxy) with keep/remove + primary. */
function ExistingUploadedCard({
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
        {kept && <PrimaryStar isPrimary={isPrimary} onClick={onSetPrimary} />}
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

/** A YouTube media card (existing or new): direct YouTube thumbnail (never /api/files) + badge + controls. */
function YoutubeCard({
  videoId,
  kept,
  isPrimary,
  onToggleKeep,
  onSetPrimary,
}: {
  videoId: string;
  kept: boolean;
  isPrimary: boolean;
  onToggleKeep: () => void;
  onSetPrimary: () => void;
}) {
  const [failed, setFailed] = useState(false);
  return (
    <div className={`relative rounded-xl overflow-hidden border-2 ${isPrimary ? 'border-[#f37021]' : 'border-slate-200'} ${kept ? '' : 'opacity-40'}`}>
      <div className="w-full h-24 bg-black relative">
        {failed ? (
          <div className="w-full h-full flex items-center justify-center text-white/50"><Play className="w-5 h-5" /></div>
        ) : (
          <img src={youtubeThumbnailUrl(videoId)} onError={() => setFailed(true)} className="w-full h-full object-cover" alt="" />
        )}
        <span className="absolute inset-0 flex items-center justify-center pointer-events-none">
          <span className="w-8 h-8 rounded-full bg-red-600/90 flex items-center justify-center text-white"><Play className="w-4 h-4 ml-0.5" fill="currentColor" /></span>
        </span>
      </div>
      <span className="absolute bottom-1 left-1 inline-flex items-center gap-1 px-1.5 py-0.5 rounded bg-red-600 text-white text-[9px] font-bold">
        <Youtube className="w-3 h-3" /> YouTube
      </span>
      <div className="absolute top-1 left-1 flex gap-1">
        {kept && <PrimaryStar isPrimary={isPrimary} onClick={onSetPrimary} />}
      </div>
      <button
        type="button"
        onClick={onToggleKeep}
        title={kept ? 'Bỏ video này' : 'Giữ lại video này'}
        className={`absolute top-1 right-1 p-1 rounded ${kept ? 'bg-white/90 text-red-500 hover:bg-red-50' : 'bg-green-600 text-white'}`}
      >
        {kept ? <Trash2 className="w-3.5 h-3.5" /> : <CheckCircle2 className="w-3.5 h-3.5" />}
      </button>
    </div>
  );
}

/** Plays an existing (already-uploaded) audio recording through the authenticated proxy. */
function ExistingAudioPlayer({ url }: { url: string }) {
  const { url: blobUrl, status } = useAuthenticatedMedia(url);
  if (status === 'error') {
    return <p className="text-[11px] text-amber-600 font-medium">Không tải được bản ghi âm hiện tại.</p>;
  }
  if (!blobUrl) {
    return <Loader2 className="w-4 h-4 text-slate-300 animate-spin" />;
  }
  return <audio controls src={blobUrl} className="w-full h-9" />;
}

function formatBytes(bytes?: number | null): string {
  if (!bytes || bytes <= 0) return '';
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/**
 * One language's audio picker. On create there is only the new-file flow. On edit an existing recording
 * is played inline and kept unless a replacement file is picked ("Thay file"). There is never a remove
 * button — a valid item always has both audio recordings.
 */
function AudioPicker({
  labelUpload,
  labelReplace,
  labelKeep,
  existingAudio,
  newFile,
  onPick,
  onClearNew,
}: {
  labelUpload: string;
  labelReplace: string;
  labelKeep: string;
  existingAudio?: GalleryAudioInfo | null;
  newFile: File | null;
  onPick: (file: File | null) => void;
  onClearNew: () => void;
}) {
  const inputRef = useRef<HTMLInputElement | null>(null);
  // Object URL for the newly picked file (revoked on change/unmount).
  const previewUrl = useMemo(() => (newFile ? URL.createObjectURL(newFile) : null), [newFile]);
  useEffect(() => () => { if (previewUrl) URL.revokeObjectURL(previewUrl); }, [previewUrl]);

  const handlePick = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0] ?? null;
    onPick(file);
    e.target.value = '';
  };

  return (
    <div className="space-y-2">
      {newFile ? (
        <div className="rounded-xl border border-[#f37021]/40 bg-orange-50/40 p-3 space-y-2">
          <div className="flex items-center justify-between gap-2">
            <span className="flex items-center gap-1.5 text-xs font-bold text-[#004c91] truncate">
              <Music className="w-4 h-4 shrink-0" /> <span className="truncate">{newFile.name}</span>
            </span>
            <span className="text-[11px] text-slate-400 font-medium shrink-0">{formatBytes(newFile.size)}</span>
          </div>
          {previewUrl && <audio controls src={previewUrl} className="w-full h-9" />}
          <div className="flex items-center gap-2">
            <button type="button" onClick={() => inputRef.current?.click()} className="inline-flex items-center gap-1 text-[11px] font-bold text-[#004c91] hover:underline">
              <RefreshCw className="w-3 h-3" /> {labelReplace}
            </button>
            <button type="button" onClick={onClearNew} className="inline-flex items-center gap-1 text-[11px] font-bold text-slate-400 hover:text-red-500">
              <Trash2 className="w-3 h-3" /> Hủy chọn
            </button>
          </div>
        </div>
      ) : existingAudio ? (
        <div className="rounded-xl border border-slate-200 bg-white p-3 space-y-2">
          <div className="flex items-center justify-between gap-2">
            <span className="flex items-center gap-1.5 text-xs font-bold text-slate-600 truncate">
              <Music className="w-4 h-4 shrink-0" /> <span className="truncate">{existingAudio.fileName}</span>
            </span>
            <span className="text-[11px] text-slate-400 font-medium shrink-0">{formatBytes(existingAudio.fileSize)}</span>
          </div>
          <ExistingAudioPlayer url={existingAudio.url} />
          <button type="button" onClick={() => inputRef.current?.click()} className="inline-flex items-center gap-1 text-[11px] font-bold text-[#004c91] hover:underline">
            <RefreshCw className="w-3 h-3" /> {labelReplace}
          </button>
          <p className="text-[10px] text-slate-400">{labelKeep}</p>
        </div>
      ) : (
        <button
          type="button"
          onClick={() => inputRef.current?.click()}
          className="w-full flex flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed border-[#004c91]/30 bg-white hover:bg-blue-50/50 hover:border-[#004c91]/50 p-5 transition-colors"
        >
          <Music className="w-6 h-6 text-[#004c91]" />
          <span className="text-xs font-bold text-gray-700">{labelUpload}</span>
          <span className="text-[11px] text-slate-400">MP3/WAV · tối đa 20MB</span>
        </button>
      )}
      <input ref={inputRef} type="file" accept={AUDIO_ACCEPT} className="hidden" onChange={handlePick} />
    </div>
  );
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
  onCreated: (translationWarning?: string | null) => void;
  onUpdated: (updated: GalleryItemDetail) => void;
  onError: (message: string) => void;
}) {
  const activeAreas = useMemo(() => areas.filter((a) => a.status === 'ACTIVE' || a.areaId === existing?.area.areaId), [areas, existing]);

  // Bilingual title (§4/§13 of the title-preview plan): VI + previewed/manual EN with stale tracking.
  // Google is NEVER called from here — "Dịch sang EN" goes through the backend preview endpoint.
  const titleField = useBilingualField(existing?.title ?? '', existing?.titleEn ?? '');
  // Create starts with ONLY the VI input; the EN block appears after the first translate attempt.
  // Edit always shows both (§13).
  const [titleEnRevealed, setTitleEnRevealed] = useState(mode === 'edit');
  const [translatingTitle, setTranslatingTitle] = useState(false);
  const titlePreviewRequestIdRef = useRef(0);
  // Mirror of the CURRENT VI so a late preview response is only applied when it still matches (§9).
  const titleViRef = useRef(titleField.vi);
  titleViRef.current = titleField.vi;

  const [activeLang, setActiveLang] = useState<Lang>('vi');
  const [descriptionVi, setDescriptionVi] = useState(existing?.content?.descriptionVi ?? '');
  const [descriptionEn, setDescriptionEn] = useState(existing?.content?.descriptionEn ?? '');
  const [audioVi, setAudioVi] = useState<File | null>(null);
  const [audioEn, setAudioEn] = useState<File | null>(null);
  const [itemType, setItemType] = useState<GalleryItemType>(existing?.itemType ?? 'MEDIA');
  const [areaId, setAreaId] = useState<number | ''>(existing?.area.areaId ?? '');
  const [locationId, setLocationId] = useState<number | ''>(existing?.location.locationId ?? '');
  const [newFiles, setNewFiles] = useState<NewFileEntry[]>([]);
  const [youtubeEntries, setYoutubeEntries] = useState<YoutubeEntry[]>([]);
  const [youtubeInput, setYoutubeInput] = useState('');
  const [youtubeError, setYoutubeError] = useState<string | null>(null);
  const [keptMedia, setKeptMedia] = useState<KeptMediaState[]>(
    (existing?.media ?? []).map((m) => ({
      mediaId: m.mediaId,
      mediaType: m.mediaType,
      sourceType: m.sourceType,
      fileUrl: m.fileUrl,
      thumbnailUrl: m.thumbnailUrl,
      youtubeVideoId: m.youtubeVideoId,
      kept: true,
    })),
  );
  const [primary, setPrimary] = useState<PrimarySel>(() => {
    const p = existing?.media.find((m) => m.isPrimary);
    return p ? { kind: 'existing', id: p.mediaId } : null;
  });
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  /** Shows an error both inline (in the modal) and as a toast so it's clearly noticed. */
  const flagError = (message: string) => {
    setFormError(message);
    onError(message);
  };

  const locations = useMemo(() => {
    const area = activeAreas.find((a) => a.areaId === areaId);
    if (!area) return [];
    return area.locations.filter((l) => l.status === 'ACTIVE' || l.locationId === existing?.location.locationId);
  }, [activeAreas, areaId, existing]);

  // Object URLs for new-file previews (revoked on change/unmount).
  const previews = useMemo(() => newFiles.map((n) => ({ entry: n, url: URL.createObjectURL(n.file) })), [newFiles]);
  useEffect(() => () => previews.forEach((p) => URL.revokeObjectURL(p.url)), [previews]);

  const keptCount = keptMedia.filter((m) => m.kept).length;
  const totalAfter = keptCount + newFiles.length + youtubeEntries.length;
  const slotsLeft = MAX_FILES - totalAfter;

  const isPrimary = (sel: PrimarySel): boolean => {
    if (!sel || !primary || primary.kind !== sel.kind) return false;
    return primary.id === sel.id;
  };

  const handleAddFiles = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!e.target.files) return;
    const incoming = Array.from(e.target.files) as File[];
    const accepted: NewFileEntry[] = [];
    for (const file of incoming) {
      // Machine uploads accept images only. A video picked here is rejected with a clear message that
      // points the user to the YouTube field (videos are added by link, never uploaded as a file).
      if (isVideoFile(file)) {
        flagError(`${file.name}: Chỉ chấp nhận ảnh khi tải từ máy. Vui lòng thêm video qua liên kết YouTube.`);
        continue;
      }
      const asImage = validateFile(file, 'GALLERY_ITEM_IMAGE');
      if (!asImage.ok) {
        flagError(`${file.name}: ${asImage.message}`);
        continue;
      }
      accepted.push({ id: nextLocalId(), file });
    }
    const room = MAX_FILES - totalAfter;
    if (accepted.length > room) {
      flagError(`Gallery item chỉ được có tối đa ${MAX_FILES} media.`);
    }
    setNewFiles((prev) => [...prev, ...accepted].slice(0, prev.length + Math.max(0, room)));
    e.target.value = '';
  };

  const removeNewFile = (id: string) => {
    setNewFiles((prev) => prev.filter((n) => n.id !== id));
    setPrimary((cur) => (cur && cur.kind === 'upload' && cur.id === id ? null : cur));
  };

  const addYoutube = () => {
    const videoId = parseYouTubeVideoId(youtubeInput);
    if (!videoId) {
      setYoutubeError('URL YouTube không hợp lệ. Hãy dán liên kết dạng youtube.com/watch?v=... hoặc youtu.be/...');
      return;
    }
    if (youtubeEntries.some((y) => y.videoId === videoId) ||
        keptMedia.some((m) => m.kept && m.sourceType === 'YOUTUBE' && m.youtubeVideoId === videoId)) {
      setYoutubeError('Video YouTube này đã được thêm.');
      return;
    }
    if (totalAfter >= MAX_FILES) {
      setYoutubeError(`Gallery item chỉ được có tối đa ${MAX_FILES} media.`);
      return;
    }
    setYoutubeEntries((prev) => [...prev, { id: nextLocalId(), url: youtubeWatchUrl(videoId), videoId }]);
    setYoutubeInput('');
    setYoutubeError(null);
  };

  const removeYoutube = (id: string) => {
    setYoutubeEntries((prev) => prev.filter((y) => y.id !== id));
    setPrimary((cur) => (cur && cur.kind === 'youtube' && cur.id === id ? null : cur));
  };

  const toggleKeep = (mediaId: number) => {
    setKeptMedia((prev) => prev.map((m) => (m.mediaId === mediaId ? { ...m, kept: !m.kept } : m)));
    setPrimary((cur) => (cur && cur.kind === 'existing' && cur.id === mediaId ? null : cur));
  };

  /** Translates the stable primary selection into the backend key (existing:{id} / upload:{i} / youtube:{i}). */
  const primaryMediaKey = (): string | undefined => {
    if (!primary) return undefined;
    if (primary.kind === 'existing') return `existing:${primary.id}`;
    if (primary.kind === 'upload') {
      const idx = newFiles.findIndex((n) => n.id === primary.id);
      return idx >= 0 ? `upload:${idx}` : undefined;
    }
    const idx = youtubeEntries.findIndex((y) => y.id === primary.id);
    return idx >= 0 ? `youtube:${idx}` : undefined;
  };

  const existingAudioVi = existing?.content?.audioVi ?? null;
  const existingAudioEn = existing?.content?.audioEn ?? null;

  /** Validates a picked audio file (size + type) before accepting it. */
  const pickAudio = (setter: (f: File | null) => void, langLabel: string) => (file: File | null) => {
    if (!file) { setter(null); return; }
    const okType = /\.(mp3|wav)$/i.test(file.name) || /^audio\//i.test(file.type);
    if (!okType) {
      flagError(`Bản ghi âm ${langLabel} không đúng định dạng (chỉ MP3/WAV).`);
      return;
    }
    if (file.size > MAX_AUDIO_BYTES) {
      flagError(`Bản ghi âm ${langLabel} vượt quá dung lượng cho phép (tối đa 20 MB).`);
      return;
    }
    setFormError(null);
    setter(file);
  };

  /** "Dịch sang EN" for the title — ONE backend request; the save later reuses the result (§7–§9). */
  const handleTranslateTitle = async () => {
    const vi = normalizeVi(titleField.vi);
    if (!vi) {
      flagError('Vui lòng nhập tiêu đề trước khi dịch.');
      return;
    }
    const requestId = ++titlePreviewRequestIdRef.current;
    setTranslatingTitle(true);
    titleField.beginTranslating();
    setTitleEnRevealed(true);
    try {
      const res = await galleryManagementApi.previewItemTranslation({
        entityType: 'GALLERY_ITEM',
        field: 'TITLE',
        entityId: mode === 'edit' ? existing?.galleryItemId ?? null : null,
        sourceText: vi,
      });
      if (requestId !== titlePreviewRequestIdRef.current) return; // an older response — never apply
      // Apply only while the VI is still the one that was translated; otherwise the EN is stale (§9).
      if (normalizeVi(titleViRef.current) === res.sourceText) {
        titleField.applyPreview(res.translatedText, res.sourceHash);
      } else {
        titleField.markStale();
      }
    } catch (err) {
      if (requestId !== titlePreviewRequestIdRef.current) return;
      titleField.markFailed();
      const msg = getGalleryErrorMessage(err);
      setFormError(msg);
      onError(msg);
    } finally {
      if (requestId === titlePreviewRequestIdRef.current) setTranslatingTitle(false);
    }
  };

  const validate = (): string | null => {
    if (!titleField.vi.trim()) return 'Vui lòng nhập tiêu đề.';
    // An auto-previewed EN whose VI changed afterwards must be re-translated or hand-edited (§6.1).
    const titleStale = fieldBlockingError(titleField, 'Tiêu đề');
    if (titleStale) return titleStale;
    if (!descriptionVi.trim()) return 'Vui lòng nhập mô tả tiếng Việt.';
    if (!descriptionEn.trim()) return 'Vui lòng nhập mô tả tiếng Anh.';
    // Audio: create requires both files; edit requires each to be kept (existing) or replaced (new).
    if (!audioVi && !(mode === 'edit' && existingAudioVi)) return 'Vui lòng chọn bản ghi âm tiếng Việt.';
    if (!audioEn && !(mode === 'edit' && existingAudioEn)) return 'Vui lòng chọn bản ghi âm tiếng Anh.';
    if (areaId === '') return 'Vui lòng chọn khu vực.';
    if (locationId === '') return 'Vui lòng chọn vị trí.';
    if (mode === 'create' && newFiles.length + youtubeEntries.length === 0)
      return 'Vui lòng chọn ít nhất một tệp media hoặc thêm một video YouTube.';
    if (mode === 'edit' && totalAfter === 0) return 'Gallery item phải có ít nhất một media.';
    return null;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const err = validate();
    if (err) {
      flagError(err);
      return;
    }
    setFormError(null);
    setSubmitting(true);
    // What the save carries for the EN title: AUTO_PREVIEW + hash (reused preview, 0 extra Google
    // calls), MANUAL (hand-typed), or NONE (backend keeps/auto-translates).
    const titlePayload = fieldPayload(titleField);
    try {
      if (mode === 'create') {
        const created = await galleryManagementApi.createGalleryItem({
          title: titleField.vi.trim(),
          titleEn: titlePayload.en,
          titleTranslationOrigin: titlePayload.origin,
          titleTranslationSourceHash: titlePayload.sourceHash,
          descriptionVi: descriptionVi.trim(),
          descriptionEn: descriptionEn.trim(),
          audioVi: audioVi!,
          audioEn: audioEn!,
          locationId: Number(locationId),
          itemType,
          status: 'PUBLISHED',
          files: newFiles.map((n) => n.file),
          youtubeUrls: youtubeEntries.map((y) => y.url),
          primaryMediaKey: primaryMediaKey(),
        });
        onCreated(created.translationWarning);
      } else if (existing) {
        const updated = await galleryManagementApi.updateGalleryItem({
          galleryItemId: existing.galleryItemId,
          title: titleField.vi.trim(),
          titleEn: titlePayload.en,
          titleTranslationOrigin: titlePayload.origin,
          titleTranslationSourceHash: titlePayload.sourceHash,
          descriptionVi: descriptionVi.trim(),
          descriptionEn: descriptionEn.trim(),
          newAudioVi: audioVi,
          newAudioEn: audioEn,
          locationId: Number(locationId),
          itemType,
          keepMediaIds: keptMedia.filter((m) => m.kept).map((m) => m.mediaId),
          newFiles: newFiles.map((n) => n.file),
          youtubeUrls: youtubeEntries.map((y) => y.url),
          primaryMediaKey: primaryMediaKey(),
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
              {slotsLeft > 0 ? 'Click để chọn ảnh' : `Đã đủ ${MAX_FILES} media`}
            </p>
            <p className="text-xs text-slate-400 mt-1 text-center">
              Chỉ ảnh (JPG/PNG/WEBP ≤20MB) · tối đa {MAX_FILES} media · video thêm qua YouTube bên dưới
            </p>
            <input type="file" multiple accept={MEDIA_ACCEPT} className="hidden" onChange={handleAddFiles} disabled={slotsLeft <= 0} />
          </label>

          {/* YouTube video source (added by URL — not downloaded to PEMS) */}
          <div className="mb-4 rounded-2xl border border-red-100 bg-white p-3">
            <p className="text-xs font-bold text-slate-600 uppercase tracking-wide mb-2 flex items-center gap-1.5">
              <Youtube className="w-4 h-4 text-red-600" /> Thêm video YouTube
            </p>
            <div className="flex items-center gap-2">
              <input
                type="url"
                value={youtubeInput}
                onChange={(e) => { setYoutubeInput(e.target.value); setYoutubeError(null); }}
                onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); addYoutube(); } }}
                placeholder="https://www.youtube.com/watch?v=..."
                className="flex-1 min-w-0 px-3 py-2 rounded-lg border border-slate-200 focus:border-red-400 focus:ring-1 focus:ring-red-400 outline-none text-xs font-normal"
              />
              <button
                type="button"
                onClick={addYoutube}
                disabled={slotsLeft <= 0}
                className="shrink-0 inline-flex items-center gap-1 px-3 py-2 rounded-lg bg-red-600 hover:bg-red-700 text-white text-xs font-bold disabled:opacity-50"
              >
                <Plus className="w-3.5 h-3.5" /> Thêm
              </button>
            </div>
            {youtubeError && <p className="mt-1.5 text-[11px] text-red-600 font-medium">{youtubeError}</p>}
          </div>

          {/* Existing media (edit) */}
          {mode === 'edit' && keptMedia.length > 0 && (
            <div className="mb-4">
              <p className="text-xs font-bold text-slate-500 uppercase tracking-wide mb-2">Media hiện có</p>
              <div className="grid grid-cols-3 gap-2">
                {keptMedia.map((m) =>
                  m.sourceType === 'YOUTUBE' && m.youtubeVideoId ? (
                    <YoutubeCard
                      key={m.mediaId}
                      videoId={m.youtubeVideoId}
                      kept={m.kept}
                      isPrimary={isPrimary({ kind: 'existing', id: m.mediaId })}
                      onToggleKeep={() => toggleKeep(m.mediaId)}
                      onSetPrimary={() => setPrimary({ kind: 'existing', id: m.mediaId })}
                    />
                  ) : (
                    <ExistingUploadedCard
                      key={m.mediaId}
                      fileUrl={m.thumbnailUrl || m.fileUrl || ''}
                      mediaType={m.mediaType}
                      kept={m.kept}
                      isPrimary={isPrimary({ kind: 'existing', id: m.mediaId })}
                      onToggleKeep={() => toggleKeep(m.mediaId)}
                      onSetPrimary={() => setPrimary({ kind: 'existing', id: m.mediaId })}
                    />
                  ),
                )}
              </div>
            </div>
          )}

          {/* New YouTube videos */}
          {youtubeEntries.length > 0 && (
            <div className="mb-4">
              <p className="text-xs font-bold text-slate-500 uppercase tracking-wide mb-2">Video YouTube mới ({youtubeEntries.length})</p>
              <div className="grid grid-cols-3 gap-2">
                {youtubeEntries.map((y) => (
                  <YoutubeCard
                    key={y.id}
                    videoId={y.videoId}
                    kept
                    isPrimary={isPrimary({ kind: 'youtube', id: y.id })}
                    onToggleKeep={() => removeYoutube(y.id)}
                    onSetPrimary={() => setPrimary({ kind: 'youtube', id: y.id })}
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
            {previews.map((p) => (
              <div key={p.entry.id} className={`flex items-center justify-between p-2 rounded-xl bg-white border ${isPrimary({ kind: 'upload', id: p.entry.id }) ? 'border-[#f37021]' : 'border-slate-200'} shadow-sm`}>
                <div className="flex items-center gap-3 min-w-0">
                  <div className="w-10 h-10 rounded-lg overflow-hidden shrink-0 bg-slate-100">
                    {isVideoFile(p.entry.file)
                      ? <video src={p.url} className="w-full h-full object-cover" muted />
                      : <img src={p.url} className="w-full h-full object-cover" alt="" />}
                  </div>
                  <p className="text-xs font-bold text-gray-700 truncate">{p.entry.file.name}</p>
                </div>
                <div className="flex items-center gap-1 shrink-0">
                  <PrimaryStar isPrimary={isPrimary({ kind: 'upload', id: p.entry.id })} onClick={() => setPrimary({ kind: 'upload', id: p.entry.id })} />
                  <button type="button" onClick={() => removeNewFile(p.entry.id)} className="p-2 text-slate-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors">
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
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
              <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Tiêu đề (VI) <span className="text-red-500">*</span></label>
              <input
                type="text"
                value={titleField.vi}
                maxLength={255}
                onChange={(e) => titleField.setVi(e.target.value)}
                className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-normal transition-all"
                placeholder="Nhập tiêu đề..."
              />
              <div>
                <TranslateButton
                  onClick={handleTranslateTitle}
                  disabled={!titleField.vi.trim() || submitting}
                  translating={translatingTitle}
                  label={titleField.en.trim() ? 'Dịch lại sang tiếng Anh' : 'Dịch sang tiếng Anh'}
                />
              </div>
            </div>

            {/* EN title: hidden on create until the first translate attempt; always shown on edit (§13). */}
            {titleEnRevealed && (
              <div className="space-y-1.5">
                <label className="text-xs font-bold text-slate-500 uppercase tracking-wide flex items-center gap-2">
                  <span>Tiêu đề (EN)</span>
                  {titleField.state === 'STALE' && !titleField.manuallyEdited && (
                    <span className="px-1.5 py-0.5 rounded bg-amber-100 text-amber-700 text-[10px] font-bold normal-case">Cần dịch lại</span>
                  )}
                </label>
                <input
                  type="text"
                  value={titleField.en}
                  maxLength={500}
                  onChange={(e) => titleField.setEnManual(e.target.value)}
                  disabled={translatingTitle}
                  className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-normal transition-all disabled:opacity-60"
                  placeholder="English title..."
                />
                <EnFieldHint field={titleField} />
              </div>
            )}

            <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
              <div className="space-y-1.5">
                <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Danh mục tòa/khu <span className="text-red-500">*</span></label>
                <select
                  value={areaId === '' ? '' : String(areaId)}
                  onChange={(e) => { setAreaId(e.target.value === '' ? '' : Number(e.target.value)); setLocationId(''); }}
                  className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-normal transition-all appearance-none bg-slate-50 focus:bg-white"
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
                  className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-normal transition-all appearance-none bg-slate-50 focus:bg-white disabled:opacity-50"
                >
                  <option value="">-- Chọn vị trí --</option>
                  {locations.map((l) => <option key={l.locationId} value={l.locationId}>{l.locationName}</option>)}
                </select>
              </div>

              <div className="space-y-1.5">
                <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Loại nội dung <span className="text-red-500">*</span></label>
                <select
                  value={itemType}
                  onChange={(e) => setItemType(e.target.value as GalleryItemType)}
                  className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-normal transition-all appearance-none bg-slate-50 focus:bg-white"
                >
                  <option value="MEDIA">Media</option>
                  <option value="VISIT_DELEGATION">Đoàn khách</option>
                </select>
              </div>

            </div>

            {/* Bilingual content — language tabs. Both VI and EN (description + audio) are mandatory. */}
            <div className="space-y-3 flex-1">
              <div className="flex items-center gap-2">
                {(['vi', 'en'] as Lang[]).map((lng) => {
                  const active = activeLang === lng;
                  const label = lng === 'vi' ? 'Tiếng Việt' : 'English';
                  return (
                    <button
                      key={lng}
                      type="button"
                      onClick={() => setActiveLang(lng)}
                      className={`px-4 py-2 rounded-xl text-sm font-bold transition-colors ${active ? 'bg-[#004c91] text-white shadow-sm' : 'bg-slate-100 text-slate-500 hover:bg-slate-200'}`}
                    >
                      {label}
                    </button>
                  );
                })}
              </div>

              {activeLang === 'vi' ? (
                <div className="space-y-3">
                  <div className="space-y-1.5">
                    <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Mô tả tiếng Việt <span className="text-red-500">*</span></label>
                    <textarea
                      rows={5}
                      value={descriptionVi}
                      onChange={(e) => setDescriptionVi(e.target.value)}
                      className="min-h-[120px] w-full px-4 py-3 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-normal transition-all resize-y"
                      placeholder="Nhập mô tả tiếng Việt..."
                    />
                    <span className="text-xs text-slate-400 font-medium">
                      {descriptionVi.length} ký tự
                    </span>
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Bản ghi âm tiếng Việt <span className="text-red-500">*</span></label>
                    <AudioPicker
                      labelUpload="Chọn bản ghi âm tiếng Việt (MP3/WAV)"
                      labelReplace="Thay file"
                      labelKeep="Giữ nguyên bản ghi âm hiện tại nếu không chọn file mới."
                      existingAudio={existingAudioVi}
                      newFile={audioVi}
                      onPick={pickAudio(setAudioVi, 'tiếng Việt')}
                      onClearNew={() => setAudioVi(null)}
                    />
                  </div>
                </div>
              ) : (
                <div className="space-y-3">
                  <div className="space-y-1.5">
                    <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">English description <span className="text-red-500">*</span></label>
                    <textarea
                      rows={5}
                      value={descriptionEn}
                      onChange={(e) => setDescriptionEn(e.target.value)}
                      className="min-h-[120px] w-full px-4 py-3 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-normal transition-all resize-y"
                      placeholder="Enter the English description..."
                    />
                    <span className="text-xs text-slate-400 font-medium">
                      {descriptionEn.length} characters
                    </span>
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">English audio <span className="text-red-500">*</span></label>
                    <AudioPicker
                      labelUpload="Select the English audio (MP3/WAV)"
                      labelReplace="Replace file"
                      labelKeep="The current recording is kept unless a new file is chosen."
                      existingAudio={existingAudioEn}
                      newFile={audioEn}
                      onPick={pickAudio(setAudioEn, 'tiếng Anh')}
                      onClearNew={() => setAudioEn(null)}
                    />
                  </div>
                </div>
              )}
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
