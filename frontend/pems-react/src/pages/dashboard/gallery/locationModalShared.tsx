/**
 * Shared building blocks for the "Quản lý khu vực" create/edit modals:
 *  - cover pickers (area MP4 video / location image) with client-side validation,
 *  - the bilingual-name field state machine (VI + EN + translation origin/state per §12–15 of the
 *    translation-preview plan) and the "Dịch sang EN" button.
 * Google is NEVER called from here — translation always goes through the backend preview endpoint.
 */

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Film, ImageOff, Languages, Loader2, Trash2, Upload } from 'lucide-react';
import { useAuthenticatedMedia } from '../../../shared/hooks/useAuthenticatedImage';
import type { TranslationOrigin } from '../../../features/gallery-management/types/galleryManagement.types';

// ── Area cover VIDEO rules (mirrors the backend FileValidationPolicy; duration is frontend-only) ──
const MAX_AREA_VIDEO_BYTES = 100 * 1024 * 1024;
const MAX_AREA_VIDEO_DURATION_SECONDS = 120;

/** Reads a video file's duration (seconds) via an off-DOM <video>. Rejects on a broken/undecodable file. */
function readVideoDuration(file: File): Promise<number> {
  return new Promise((resolve, reject) => {
    const video = document.createElement('video');
    const url = URL.createObjectURL(file);
    video.preload = 'metadata';

    const cleanup = () => {
      video.removeAttribute('src');
      video.load();
      URL.revokeObjectURL(url);
    };

    video.onloadedmetadata = () => {
      const duration = video.duration;
      cleanup();
      if (!Number.isFinite(duration) || duration <= 0) {
        reject(new Error('File video không hợp lệ hoặc đã bị hỏng.'));
        return;
      }
      resolve(duration);
    };
    video.onerror = () => {
      cleanup();
      reject(new Error('File video không hợp lệ hoặc đã bị hỏng.'));
    };
    video.src = url;
  });
}

/** Validates one MP4 area cover video (≤100MB, ≤120s). Returns a VN error message, or null when valid. */
export async function validateAreaVideo(file: File): Promise<string | null> {
  const name = file.name.toLowerCase();
  if (file.size <= 0) return 'File video không hợp lệ hoặc đã bị hỏng.';
  const mime = (file.type || '').toLowerCase();
  if (!name.endsWith('.mp4') || (mime && mime !== 'video/mp4'))
    return 'Video đại diện khu vực chỉ hỗ trợ định dạng MP4.';
  if (file.size > MAX_AREA_VIDEO_BYTES) return 'Video đại diện khu vực không được vượt quá 100 MB.';
  let duration: number;
  try {
    duration = await readVideoDuration(file);
  } catch {
    return 'File video không hợp lệ hoặc đã bị hỏng.';
  }
  if (duration > MAX_AREA_VIDEO_DURATION_SECONDS + 0.5)
    return 'Video đại diện khu vực không được dài quá 120 giây (2 phút).';
  return null;
}

function formatBytes(bytes: number): string {
  if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  if (bytes >= 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${bytes} B`;
}

/**
 * Area cover VIDEO picker (MP4). Shows an inline muted/looping preview of the picked clip, or the
 * existing cover (video or legacy image) when editing. The object URL is revoked on change/unmount.
 */
export function CoverVideoField({
  label,
  required,
  file,
  onPick,
  existingUrl,
  existingMediaType,
  hint,
}: {
  label: string;
  required?: boolean;
  file: File | null;
  onPick: (f: File | null) => void;
  existingUrl?: string | null;
  existingMediaType?: 'IMAGE' | 'VIDEO';
  hint?: string;
}) {
  const preview = useMemo(() => (file ? URL.createObjectURL(file) : null), [file]);
  useEffect(() => () => { if (preview) URL.revokeObjectURL(preview); }, [preview]);
  // Only fetch the stored cover when no new file has been picked.
  const existing = useAuthenticatedMedia(!file && existingUrl ? existingUrl : null);

  return (
    <div className="space-y-1.5">
      <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">
        {label} {required && <span className="text-red-500">*</span>}
      </label>
      <div className="flex items-start gap-3">
        <div className="w-28 h-20 rounded-xl overflow-hidden bg-slate-900 border border-slate-200 flex items-center justify-center shrink-0">
          {preview ? (
            <video src={preview} autoPlay muted loop playsInline className="w-full h-full object-cover" />
          ) : existing.url ? (
            existingMediaType === 'IMAGE' ? (
              <img src={existing.url} className="w-full h-full object-cover" alt="" />
            ) : (
              <video src={existing.url} autoPlay muted loop playsInline className="w-full h-full object-cover" />
            )
          ) : (
            <Film className="w-5 h-5 text-slate-400" />
          )}
        </div>
        <div className="flex-1 space-y-1.5">
          <label className="cursor-pointer border-2 border-dashed border-[#004c91]/30 rounded-xl px-4 py-3 flex items-center gap-2 text-sm font-bold text-[#004c91] hover:bg-blue-50/50 transition-colors">
            <Upload className="w-4 h-4" />
            {file ? 'Đổi video' : 'Chọn 1 video MP4'}
            <input
              type="file"
              accept="video/mp4"
              className="hidden"
              onChange={(e) => { onPick(e.target.files?.[0] ?? null); e.target.value = ''; }}
            />
          </label>
          {file && (
            <div className="flex items-center justify-between gap-2 text-[11px] text-slate-500">
              <span className="truncate" title={file.name}>{file.name} · {formatBytes(file.size)}</span>
              <button
                type="button"
                onClick={() => onPick(null)}
                className="inline-flex items-center gap-1 text-red-500 hover:text-red-600 font-semibold shrink-0"
              >
                <Trash2 className="w-3.5 h-3.5" /> Xóa
              </button>
            </div>
          )}
        </div>
      </div>
      <p className="text-[11px] text-slate-400">{hint ?? 'Chỉ chấp nhận MP4, tối đa 100 MB và tối đa 120 giây (2 phút). Khuyến nghị video ngang, H.264, Full HD hoặc thấp hơn.'}</p>
    </div>
  );
}

/** Single mandatory cover-image picker: preview of the picked file, or the existing (authenticated) cover. */
export function CoverImageField({
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

// ── Bilingual name field state machine (§12–15) ──

export type TranslationUiState = 'IDLE' | 'TRANSLATING' | 'READY' | 'STALE' | 'FAILED';

/** Frontend twin of the backend TranslationSourceNormalizer: trim + collapse whitespace. */
export function normalizeVi(input: string): string {
  return input.trim().replace(/\s+/g, ' ');
}

export interface BilingualFieldValue {
  vi: string;
  en: string;
  origin: TranslationOrigin;
  sourceHash: string | null;
  manuallyEdited: boolean;
  state: TranslationUiState;
  /** An existing EN (manual edit OR the stored EN prefilled by the edit modal) while the VI changed
   *  afterwards — non-blocking, but show the "kiểm tra lại bản tiếng Anh" review warning (§15.2). */
  manualNeedsReview: boolean;
}

export interface BilingualField extends BilingualFieldValue {
  setVi: (value: string) => void;
  setEnManual: (value: string) => void;
  /** Marks the field as waiting for the preview response. */
  beginTranslating: () => void;
  /** Applies a successful preview (already stale-guarded by the caller). */
  applyPreview: (en: string, sourceHash: string) => void;
  markFailed: () => void;
  /** Marks a saved-but-stale preview (backend PREVIEW_STALE). */
  markStale: () => void;
  /** Re-initializes the whole field (edit modal prefill). */
  reset: (vi: string, en: string) => void;
}

interface BilingualInternalState extends BilingualFieldValue {
  /** The normalized VI the current EN was produced FROM (preview source / VI at manual-edit time). */
  basisVi: string | null;
}

function initialFieldState(vi: string, en: string): BilingualInternalState {
  return {
    vi, en,
    origin: 'NONE',
    sourceHash: null,
    manuallyEdited: false,
    state: 'IDLE',
    manualNeedsReview: false,
    // A prefilled stored EN (edit modal) records which VI it belongs to, so a later VI edit can
    // raise the "kiểm tra lại bản tiếng Anh" review warning.
    basisVi: en.trim() ? normalizeVi(vi) : null,
  };
}

/**
 * One bilingual name (area or location). Tracks where the EN came from and whether the VI drifted
 * after it was produced:
 *  - auto-previewed EN + VI changed  → STALE (blocks save until re-translate / manual edit);
 *  - manual EN + VI changed          → kept, but flagged for review (warning, non-blocking);
 *  - typing EN by hand               → origin MANUAL.
 * All transitions are single atomic state updates (no cross-setState coupling).
 */
export function useBilingualField(initialVi = '', initialEn = ''): BilingualField {
  const [f, setF] = useState<BilingualInternalState>(() => initialFieldState(initialVi, initialEn));

  const setVi = useCallback((value: string) => {
    setF((prev) => {
      const next = { ...prev, vi: value };
      if (prev.basisVi === null) return next;
      const drifted = normalizeVi(value) !== prev.basisVi;
      if (prev.manuallyEdited) {
        // Manual EN: keepable but flagged for review while drifted (§15.2).
        next.manualNeedsReview = drifted;
      } else if (prev.state === 'READY' || prev.state === 'STALE') {
        // Auto-previewed EN: VI drift makes it STALE; typing the original back restores READY (§15.1).
        next.state = drifted ? 'STALE' : 'READY';
      } else if (prev.en.trim().length > 0) {
        // Prefilled stored EN (edit modal, untouched): VI drift → non-blocking review warning —
        // the save will auto re-translate, but the Staff Leader must be told the EN shown is stale.
        next.manualNeedsReview = drifted;
      }
      return next;
    });
  }, []);

  const setEnManual = useCallback((value: string) => {
    setF((prev) => {
      if (value.trim().length === 0) {
        // Cleared EN → back to "no translation yet"; the backend will auto-translate on save.
        return { ...initialFieldState(prev.vi, value) };
      }
      return {
        ...prev,
        en: value,
        origin: 'MANUAL',
        sourceHash: null,
        manuallyEdited: true,
        state: 'READY',
        manualNeedsReview: false,
        basisVi: normalizeVi(prev.vi),
      };
    });
  }, []);

  const beginTranslating = useCallback(
    () => setF((prev) => ({ ...prev, state: 'TRANSLATING' })), []);

  const applyPreview = useCallback((translated: string, hash: string) => {
    setF((prev) => ({
      ...prev,
      en: translated,
      origin: 'AUTO_PREVIEW',
      sourceHash: hash,
      manuallyEdited: false,
      state: 'READY',
      manualNeedsReview: false,
      basisVi: normalizeVi(prev.vi),
    }));
  }, []);

  const markFailed = useCallback(
    () => setF((prev) => ({ ...prev, state: prev.state === 'TRANSLATING' ? 'FAILED' : prev.state })), []);
  const markStale = useCallback(() => setF((prev) => ({ ...prev, state: 'STALE' })), []);

  const reset = useCallback(
    (newVi: string, newEn: string) => setF(initialFieldState(newVi, newEn)), []);

  return {
    vi: f.vi,
    en: f.en,
    origin: f.origin,
    sourceHash: f.sourceHash,
    manuallyEdited: f.manuallyEdited,
    state: f.state,
    manualNeedsReview: f.manualNeedsReview,
    setVi, setEnManual, beginTranslating, applyPreview, markFailed, markStale, reset,
  };
}

/** The origin/hash pair this field should carry in the save payload (§16/§21). */
export function fieldPayload(field: BilingualFieldValue): {
  en: string | null;
  origin: TranslationOrigin;
  sourceHash: string | null;
} {
  const en = field.en.trim();
  if (!en) return { en: null, origin: 'NONE', sourceHash: null };
  if (field.manuallyEdited) return { en, origin: 'MANUAL', sourceHash: null };
  if (field.origin === 'AUTO_PREVIEW' && field.state === 'READY')
    return { en, origin: 'AUTO_PREVIEW', sourceHash: field.sourceHash };
  // Prefilled stored EN that was never touched (edit modal): the backend keeps or re-translates it.
  return { en: null, origin: 'NONE', sourceHash: null };
}

/** Non-null when the field must block the save (§15.1). */
export function fieldBlockingError(field: BilingualFieldValue, label: string): string | null {
  if (field.state === 'STALE' && !field.manuallyEdited)
    return `${label}: nội dung tiếng Việt đã thay đổi. Vui lòng dịch lại hoặc chỉnh bản tiếng Anh.`;
  return null;
}

/** Helper text under an EN input, matching the field's current translation state. */
export function EnFieldHint({ field }: { field: BilingualFieldValue }) {
  if (field.state === 'TRANSLATING')
    return <p className="text-[11px] text-slate-400">Đang dịch…</p>;
  if (field.state === 'STALE' && !field.manuallyEdited)
    return <p className="text-[11px] text-amber-600 font-normal">Nội dung tiếng Việt đã thay đổi. Vui lòng dịch lại.</p>;
  if (field.manualNeedsReview)
    return <p className="text-[11px] text-amber-600 font-normal">Tên tiếng Việt đã thay đổi. Hãy kiểm tra lại bản tiếng Anh trước khi lưu.</p>;
  if (field.state === 'FAILED')
    return <p className="text-[11px] text-red-500 font-normal">Dịch tự động thất bại. Bạn có thể thử lại hoặc tự nhập bản tiếng Anh.</p>;
  if (field.origin === 'AUTO_PREVIEW' && field.state === 'READY')
    return <p className="text-[11px] text-slate-400">Bản dịch tự động — bạn có thể chỉnh sửa trước khi lưu.</p>;
  if (field.manuallyEdited)
    return <p className="text-[11px] text-slate-400">Bản tiếng Anh nhập thủ công.</p>;
  return <p className="text-[11px] text-slate-400">Để trống nếu muốn hệ thống tự dịch khi lưu.</p>;
}

/** "Dịch sang tiếng Anh" button — spinner + disabled while a request is in flight (double-click safe). */
export function TranslateButton({
  onClick,
  disabled,
  translating,
  label = 'Dịch sang EN',
}: {
  onClick: () => void;
  disabled?: boolean;
  translating: boolean;
  label?: string;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled || translating}
      title="Dịch sang tiếng Anh"
      className="inline-flex items-center gap-2 px-4 py-2 rounded-xl border border-[#004c91]/30 text-sm font-bold text-[#004c91] hover:bg-blue-50/60 disabled:opacity-50 disabled:cursor-not-allowed transition-colors outline-none"
    >
      {translating ? <Loader2 className="w-4 h-4 animate-spin" /> : <Languages className="w-4 h-4" />}
      {translating ? 'Đang dịch…' : label}
    </button>
  );
}

/** Shared props of the modals' area dropdown entries. */
export interface AreaOption {
  areaId: number;
  areaName: string;
  coverUrl?: string | null;
  coverMediaType?: 'IMAGE' | 'VIDEO';
}
