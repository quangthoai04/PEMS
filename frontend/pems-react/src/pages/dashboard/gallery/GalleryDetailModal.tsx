/** Read-only detail modal for a gallery item (UC-GAL-03) + EverAI TTS status & "Tạo lại audio". */

import React, { useEffect, useState } from 'react';
import { motion } from 'motion/react';
import {
  X, ChevronRight, Image as ImageIcon, ImageOff, Film, Loader2, AlertTriangle,
  Volume2, CheckCircle2, XCircle, RefreshCw, Clock,
} from 'lucide-react';
import { useAuthenticatedMedia } from '../../../shared/hooks/useAuthenticatedImage';
import { youtubeEmbedUrl, youtubeThumbnailUrl } from '../../../shared/utils/youtube';
import { galleryManagementApi } from '../../../features/gallery-management/api/galleryManagementApi';
import { getGalleryErrorMessage } from '../../../features/gallery-management/api/galleryError';
import type {
  GalleryItemDetail,
  GalleryItemTtsStatus,
  GalleryMedia,
  GalleryMediaKind,
} from '../../../features/gallery-management/types/galleryManagement.types';
import { formatVietnamDate } from '../../../shared/utils/vietnamTime';

const MEDIA_KIND_LABEL: Record<GalleryMediaKind, string> = {
  IMAGE: 'Hình ảnh',
  VIDEO: 'Video',
  MIXED: 'Hỗn hợp',
};

/** Badge presentation for each narration status (also decides the small leading icon). */
const TTS_STATUS_META: Record<GalleryItemTtsStatus['status'], { label: string; className: string; icon: React.ReactNode }> = {
  READY: {
    label: 'Giọng đọc: Sẵn sàng',
    className: 'bg-green-100 text-green-700 border-green-200',
    icon: <CheckCircle2 className="w-3.5 h-3.5" />,
  },
  PROCESSING: {
    label: 'Giọng đọc: Đang tạo…',
    className: 'bg-blue-100 text-[#004c91] border-blue-200',
    icon: <Loader2 className="w-3.5 h-3.5 animate-spin" />,
  },
  FAILED: {
    label: 'Giọng đọc: Lỗi',
    className: 'bg-red-100 text-red-700 border-red-200',
    icon: <XCircle className="w-3.5 h-3.5" />,
  },
  STALE: {
    label: 'Giọng đọc: Cần tạo lại',
    className: 'bg-amber-100 text-amber-700 border-amber-200',
    icon: <RefreshCw className="w-3.5 h-3.5" />,
  },
  NOT_CREATED: {
    label: 'Giọng đọc: Chưa tạo',
    className: 'bg-slate-100 text-slate-600 border-slate-200',
    icon: <Clock className="w-3.5 h-3.5" />,
  },
  DISABLED: {
    label: 'Giọng đọc: Đang tắt',
    className: 'bg-slate-100 text-slate-500 border-slate-200',
    icon: <Volume2 className="w-3.5 h-3.5" />,
  },
  INVALID_DESCRIPTION: {
    label: 'Giọng đọc: Mô tả không hợp lệ',
    className: 'bg-slate-100 text-slate-500 border-slate-200',
    icon: <AlertTriangle className="w-3.5 h-3.5" />,
  },
};

function formatDate(iso?: string | null): string {
  if (!iso) return '—';
  return formatVietnamDate(iso, { fallback: iso });
}

/** Main preview for one media — YouTube renders an embedded iframe, uploaded files stream via the proxy. */
function MediaPreview({ media }: { media: GalleryMedia }) {
  if (media.sourceType === 'YOUTUBE' && media.youtubeVideoId) {
    return (
      <div className="w-full h-full bg-black">
        <iframe
          src={media.embedUrl || youtubeEmbedUrl(media.youtubeVideoId)}
          title={media.altText || 'YouTube video'}
          className="w-full h-full"
          allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
          allowFullScreen
        />
      </div>
    );
  }
  return <PreviewMedia fileUrl={media.fileUrl || ''} mediaType={media.mediaType} />;
}

/** A YouTube thumbnail (direct from YouTube — never the /api/files proxy). */
function YoutubeThumb({ videoId, active, onClick }: { videoId: string; active: boolean; onClick: () => void }) {
  const [failed, setFailed] = useState(false);
  return (
    <button
      onClick={onClick}
      className={`relative w-16 h-16 rounded-xl overflow-hidden shrink-0 border-2 transition-all ${active ? 'border-[#004c91]' : 'border-transparent opacity-70 hover:opacity-100'}`}
    >
      {failed
        ? <div className="w-full h-full bg-slate-900 flex items-center justify-center text-white/50"><Film className="w-4 h-4" /></div>
        : <img src={youtubeThumbnailUrl(videoId)} onError={() => setFailed(true)} className="w-full h-full object-cover" alt="" />}
      <span className="absolute bottom-0.5 right-0.5 bg-red-600 rounded p-0.5">
        <Film className="w-3 h-3 text-white" />
      </span>
    </button>
  );
}

function PreviewMedia({ fileUrl, mediaType }: { fileUrl: string; mediaType: 'IMAGE' | 'VIDEO' }) {
  const { url, status } = useAuthenticatedMedia(fileUrl);
  if (status === 'error') {
    return (
      <div className="w-full h-full flex flex-col items-center justify-center text-amber-600 gap-2 p-6 text-center">
        <ImageOff className="w-8 h-8" />
        <span className="text-sm font-semibold">Media này chưa có file khả dụng.</span>
      </div>
    );
  }
  if (!url) {
    return (
      <div className="w-full h-full flex items-center justify-center">
        <Loader2 className="w-6 h-6 text-slate-300 animate-spin" />
      </div>
    );
  }
  return mediaType === 'VIDEO'
    ? <video src={url} className="w-full h-full object-contain bg-black" controls playsInline />
    : <img src={url} className="w-full h-full object-cover" alt="" />;
}

function Thumb({
  fileUrl,
  mediaType,
  active,
  onClick,
}: {
  fileUrl: string;
  mediaType: 'IMAGE' | 'VIDEO';
  active: boolean;
  onClick: () => void;
}) {
  const { url, status } = useAuthenticatedMedia(fileUrl);
  return (
    <button
      onClick={onClick}
      className={`relative w-16 h-16 rounded-xl overflow-hidden shrink-0 border-2 transition-all ${active ? 'border-[#004c91]' : 'border-transparent opacity-70 hover:opacity-100'}`}
    >
      {url ? (
        mediaType === 'VIDEO'
          ? <video src={url} className="w-full h-full object-cover" muted />
          : <img src={url} className="w-full h-full object-cover" alt="" />
      ) : (
        <div className="w-full h-full bg-slate-100 flex items-center justify-center">
          {status === 'error'
            ? <ImageOff className="w-4 h-4 text-slate-300" />
            : <Loader2 className="w-4 h-4 text-slate-300 animate-spin" />}
        </div>
      )}
      {mediaType === 'VIDEO' && (
        <span className="absolute bottom-0.5 right-0.5 bg-black/60 rounded p-0.5">
          <Film className="w-3 h-3 text-white" />
        </span>
      )}
    </button>
  );
}

export function GalleryDetailModal({
  detail,
  loading,
  onClose,
  onEdit,
}: {
  detail: GalleryItemDetail | null;
  loading: boolean;
  onClose: () => void;
  onEdit: () => void;
}) {
  const [selectedIdx, setSelectedIdx] = useState(0);

  // ── EverAI TTS narration status + "Tạo lại audio" ──
  const [ttsStatus, setTtsStatus] = useState<GalleryItemTtsStatus | null>(null);
  const [regenerating, setRegenerating] = useState(false);
  const [regenNote, setRegenNote] = useState<{ type: 'info' | 'error'; text: string } | null>(null);
  const itemId = detail?.galleryItemId;

  // Reset + fetch the narration status whenever the shown item changes.
  useEffect(() => {
    setSelectedIdx(0);
    setRegenNote(null);
    setTtsStatus(null);
    if (itemId == null) return;
    let active = true;
    galleryManagementApi
      .getTtsAudioStatus(itemId)
      .then((s) => { if (active) setTtsStatus(s); })
      .catch(() => { if (active) setTtsStatus(null); });
    return () => { active = false; };
  }, [itemId]);

  // While a job is PROCESSING, re-poll every 3s so the badge updates to READY/FAILED on its own.
  useEffect(() => {
    if (itemId == null || ttsStatus?.status !== 'PROCESSING') return;
    const timer = window.setTimeout(async () => {
      try {
        const s = await galleryManagementApi.getTtsAudioStatus(itemId);
        setTtsStatus(s);
      } catch { /* transient — the next tick retries */ }
    }, 3000);
    return () => window.clearTimeout(timer);
  }, [itemId, ttsStatus]);

  const handleRegenerate = async () => {
    if (itemId == null || regenerating) return;
    setRegenerating(true);
    setRegenNote(null);
    try {
      const result = await galleryManagementApi.regenerateTtsAudio(itemId);
      setRegenNote({
        type: 'info',
        text: result.message || (result.status === 'UP_TO_DATE'
          ? 'Giọng đọc đã là bản mới nhất, không cần tạo lại.'
          : 'Giọng đọc đang được tạo lại.'),
      });
      // Refresh the badge (a queued job flips it to PROCESSING → the poll effect takes over).
      try {
        const s = await galleryManagementApi.getTtsAudioStatus(itemId);
        setTtsStatus(s);
      } catch { /* keep the last known status */ }
    } catch (err) {
      setRegenNote({ type: 'error', text: getGalleryErrorMessage(err) });
    } finally {
      setRegenerating(false);
    }
  };

  const media = detail?.media ?? [];
  const selected = media[selectedIdx] ?? media[0];

  const statusMeta = ttsStatus ? TTS_STATUS_META[ttsStatus.status] : null;
  // Allow regenerate when the backend says so (unknown status → allow; backend re-checks anyway).
  const canRegen = ttsStatus?.canRegenerate ?? true;
  const regenDisabled = regenerating || !canRegen;
  const regenHint =
    ttsStatus?.status === 'READY' ? 'Giọng đọc đã là bản mới nhất cho mô tả hiện tại.'
      : ttsStatus?.status === 'PROCESSING' ? 'Giọng đọc đang được tạo, vui lòng chờ.'
        : ttsStatus?.status === 'DISABLED' ? 'Tính năng giọng đọc đang tắt.'
          : ttsStatus?.status === 'INVALID_DESCRIPTION' ? 'Mô tả không hợp lệ để tạo giọng đọc.'
            : undefined;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-auto bg-black/60 backdrop-blur-sm p-4 font-sans">
      <motion.div
        initial={{ opacity: 0, scale: 0.95, y: 20 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={{ opacity: 0, scale: 0.95, y: 20 }}
        className="bg-white rounded-3xl w-full max-w-4xl overflow-hidden shadow-2xl relative flex flex-col md:flex-row min-h-[300px] max-h-[90vh]"
      >
        <button
          onClick={onClose}
          className="absolute top-4 right-4 w-8 h-8 bg-black/20 hover:bg-black/40 text-white rounded-full flex items-center justify-center transition-colors z-10 backdrop-blur-md"
        >
          <X className="w-4 h-4" />
        </button>

        {loading || !detail ? (
          <div className="flex-1 flex items-center justify-center p-16">
            <Loader2 className="w-8 h-8 text-[#004c91] animate-spin" />
          </div>
        ) : (
          <>
            <div className="w-full md:w-1/2 p-6 bg-slate-100 flex flex-col gap-4">
              <div className="flex-1 rounded-2xl overflow-hidden border border-slate-200 bg-white relative min-h-[240px]">
                {selected ? (
                  <MediaPreview media={selected} />
                ) : (
                  <div className="w-full h-full flex flex-col items-center justify-center text-amber-600 gap-2 p-6 text-center">
                    <AlertTriangle className="w-8 h-8" />
                    <span className="text-sm font-semibold">Media này chưa có file khả dụng.</span>
                  </div>
                )}
                <div className="absolute top-4 left-4 bg-white/90 backdrop-blur-sm px-3 py-1 rounded-lg text-xs font-bold text-[#004c91] flex items-center gap-1.5">
                  {detail.mediaKind === 'VIDEO' ? <Film className="w-4 h-4" /> : <ImageIcon className="w-4 h-4" />}
                  {MEDIA_KIND_LABEL[detail.mediaKind]}
                </div>
              </div>
              {media.length > 1 && (
                <div className="flex gap-2 overflow-x-auto pb-1">
                  {media.map((m, i) => (
                    m.sourceType === 'YOUTUBE' && m.youtubeVideoId ? (
                      <YoutubeThumb
                        key={m.mediaId}
                        videoId={m.youtubeVideoId}
                        active={i === selectedIdx}
                        onClick={() => setSelectedIdx(i)}
                      />
                    ) : (
                      <Thumb
                        key={m.mediaId}
                        fileUrl={(m.thumbnailUrl || m.fileUrl) ?? ''}
                        mediaType={m.mediaType}
                        active={i === selectedIdx}
                        onClick={() => setSelectedIdx(i)}
                      />
                    )
                  ))}
                </div>
              )}
            </div>

            {/* Right column: header (fixed) → description (scrolls) → footer (fixed) so a long
                description never stretches the modal beyond max-h-[90vh]. */}
            <div className="w-full md:w-1/2 flex flex-col min-h-0">
              <div className="p-8 pb-4 shrink-0">
                <div className="flex flex-wrap items-center gap-2 mb-4">
                  <span
                    className={`inline-flex items-center px-2.5 py-1 rounded-full text-[11px] font-bold ${detail.status === 'PUBLISHED' ? 'bg-green-100 text-green-700 border border-green-200' : 'bg-slate-100 text-slate-700 border border-slate-200'}`}
                  >
                    {detail.status === 'PUBLISHED' ? 'Hiển thị' : 'Đã ẩn'}
                  </span>
                  <span className="inline-flex items-center px-2.5 py-1 rounded-full text-[11px] font-bold bg-blue-100 text-[#004c91] border border-blue-200">
                    {detail.campus.campusName}
                  </span>
                  <span
                    className={`inline-flex items-center px-2.5 py-1 rounded-full text-[11px] font-bold ${
                      detail.itemType === 'VISIT_DELEGATION'
                        ? 'bg-orange-100 text-orange-700 border border-orange-200'
                        : 'bg-slate-100 text-slate-700 border border-slate-200'
                    }`}
                  >
                    {detail.itemTypeLabel || (detail.itemType === 'VISIT_DELEGATION' ? 'Đoàn khách' : 'Media')}
                  </span>
                  {statusMeta && (
                    <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-bold border ${statusMeta.className}`}>
                      {statusMeta.icon}
                      {statusMeta.label}
                    </span>
                  )}
                </div>

                <h3 className="text-2xl font-black text-gray-900 mb-2 leading-tight">{detail.title}</h3>
                <div className="flex items-center gap-1.5 text-sm font-semibold text-slate-600 border-b border-slate-100 pb-4">
                  <span className="text-[#004c91]">{detail.area.areaName}</span>
                  <ChevronRight className="w-4 h-4 text-slate-300" />
                  <span>{detail.location.locationName}</span>
                </div>
              </div>

              {/* Scrollable description */}
              <div className="px-8 flex-1 overflow-y-auto min-h-0">
                <p className="text-sm text-slate-500 leading-relaxed whitespace-pre-wrap break-words [overflow-wrap:anywhere]">
                  {detail.description || 'Chưa có mô tả'}
                </p>
                {ttsStatus?.status === 'FAILED' && ttsStatus.errorMessage && (
                  <p className="mt-3 text-xs text-red-600 bg-red-50 border border-red-100 rounded-lg px-3 py-2">
                    Lỗi tạo giọng đọc: {ttsStatus.errorMessage}
                  </p>
                )}
              </div>

              {/* Fixed footer: meta + actions */}
              <div className="p-8 pt-4 shrink-0 border-t border-slate-100">
                <div className="bg-slate-50 rounded-xl p-4 border border-slate-100 grid grid-cols-2 gap-4 mb-4">
                  <div>
                    <span className="block text-[10px] font-bold text-slate-400 uppercase mb-1">Ngày tạo</span>
                    <span className="text-sm font-bold text-gray-800">{formatDate(detail.createdAt)}</span>
                  </div>
                  <div>
                    <span className="block text-[10px] font-bold text-slate-400 uppercase mb-1">Người tạo</span>
                    <span className="text-sm font-bold text-gray-800">{detail.createdByName ?? '—'}</span>
                  </div>
                </div>

                {regenNote && (
                  <p className={`text-xs font-semibold mb-2 ${regenNote.type === 'error' ? 'text-red-600' : 'text-green-700'}`}>
                    {regenNote.text}
                  </p>
                )}

                <div className="flex gap-3">
                  <button
                    onClick={handleRegenerate}
                    disabled={regenDisabled}
                    title={regenDisabled ? regenHint : 'Tạo lại giọng đọc cho mô tả hiện tại'}
                    className="flex-1 bg-white text-[#004c91] border border-[#004c91]/40 py-2.5 rounded-xl font-bold flex items-center justify-center gap-2 hover:bg-blue-50 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    {regenerating
                      ? <Loader2 className="w-4 h-4 animate-spin" />
                      : <Volume2 className="w-4 h-4" />}
                    Tạo lại audio
                  </button>
                  <button
                    onClick={onEdit}
                    className="flex-1 bg-[#004c91] text-white py-2.5 rounded-xl font-bold flex items-center justify-center hover:bg-[#00386b] transition-colors"
                  >
                    Chỉnh sửa
                  </button>
                </div>
              </div>
            </div>
          </>
        )}
      </motion.div>
    </div>
  );
}

export default GalleryDetailModal;
