/** Read-only detail modal for a gallery item (UC-GAL-03). */

import React, { useEffect, useState } from 'react';
import { motion } from 'motion/react';
import { X, ChevronRight, Image as ImageIcon, ImageOff, Film, Loader2, AlertTriangle } from 'lucide-react';
import { useAuthenticatedMedia } from '../../../shared/hooks/useAuthenticatedImage';
import type { GalleryItemDetail, GalleryMediaKind } from '../../../features/gallery-management/types/galleryManagement.types';

const MEDIA_KIND_LABEL: Record<GalleryMediaKind, string> = {
  IMAGE: 'Hình ảnh',
  VIDEO: 'Video',
  MIXED: 'Hỗn hợp',
};

function formatDate(iso?: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString('en-GB');
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

  useEffect(() => {
    setSelectedIdx(0);
  }, [detail?.galleryItemId]);

  const media = detail?.media ?? [];
  const selected = media[selectedIdx] ?? media[0];

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-auto bg-black/60 backdrop-blur-sm p-4 font-sans">
      <motion.div
        initial={{ opacity: 0, scale: 0.95, y: 20 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={{ opacity: 0, scale: 0.95, y: 20 }}
        className="bg-white rounded-3xl w-full max-w-4xl overflow-hidden shadow-2xl relative flex flex-col md:flex-row min-h-[300px]"
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
                  <PreviewMedia fileUrl={selected.fileUrl} mediaType={selected.mediaType} />
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
                    <Thumb
                      key={m.mediaId}
                      fileUrl={m.thumbnailUrl || m.fileUrl}
                      mediaType={m.mediaType}
                      active={i === selectedIdx}
                      onClick={() => setSelectedIdx(i)}
                    />
                  ))}
                </div>
              )}
            </div>

            <div className="w-full md:w-1/2 p-8 flex flex-col">
              <div className="flex items-center gap-2 mb-4">
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
              </div>

              <h3 className="text-2xl font-black text-gray-900 mb-2 leading-tight">{detail.title}</h3>
              <div className="flex items-center gap-1.5 text-sm font-semibold text-slate-600 mb-4 border-b border-slate-100 pb-4">
                <span className="text-[#004c91]">{detail.area.areaName}</span>
                <ChevronRight className="w-4 h-4 text-slate-300" />
                <span>{detail.location.locationName}</span>
              </div>
              <p className="text-sm text-slate-500 mb-6 leading-relaxed whitespace-pre-wrap flex-1">
                {detail.description || 'Chưa có mô tả'}
              </p>

              <div className="bg-slate-50 rounded-xl p-4 border border-slate-100 grid grid-cols-2 gap-4 mb-6">
                <div>
                  <span className="block text-[10px] font-bold text-slate-400 uppercase mb-1">Ngày tạo</span>
                  <span className="text-sm font-bold text-gray-800">{formatDate(detail.createdAt)}</span>
                </div>
                <div>
                  <span className="block text-[10px] font-bold text-slate-400 uppercase mb-1">Người tạo</span>
                  <span className="text-sm font-bold text-gray-800">{detail.createdByName ?? '—'}</span>
                </div>
              </div>

              <button
                onClick={onEdit}
                className="w-full bg-[#004c91] text-white py-2.5 rounded-xl font-bold flex items-center justify-center hover:bg-[#00386b] transition-colors mt-auto"
              >
                Chỉnh sửa
              </button>
            </div>
          </>
        )}
      </motion.div>
    </div>
  );
}

export default GalleryDetailModal;
