/**
 * FaceScanPanel — Sau tiếp khách → Lưu trữ ảnh đoàn khách → Scan và gán tên khuôn mặt.
 *
 * Flow: ảnh đã upload vào visit_photos → Google Cloud Vision FACE_DETECTION (qua backend) →
 * hiển thị khung mặt (bounding box normalized 0..1) → Staff chọn thủ công khách thuộc đúng visit
 * instance hoặc bỏ qua → xác nhận (batch) → photo_face_tags + lịch sử quét.
 * Backend là nguồn quyền/dữ liệu duy nhất — không có mock/fallback giả khi API lỗi.
 */
import React, { forwardRef, useEffect, useImperativeHandle, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { motion, AnimatePresence } from 'motion/react';
import toast from 'react-hot-toast';
import {
  Image as ImageIcon, Sparkles, User, Tag, CheckCircle2, Search, Check,
  Minimize2, Loader2, AlertCircle, Upload, FolderOpen, ExternalLink, RefreshCw, X, Crop, Download,
} from 'lucide-react';
import { visitPhotosApi } from '../api/visitPhotosApi';
import { cropFaceToDataUrl } from '../utils/faceCropUtils';
import { uploadFileToEndpoint } from '../../../shared/api/fileUploadApi';
import { partnersApi } from '../../../features/partners/api/partnersApi';
import type {
  ConfirmFaceTagItem, TaggableGuest, VisitPhotoFaceDetection, VisitPhotoFaceScan,
} from '../types/visitPhotos.types';
import { useAuthenticatedImage } from '../../../shared/hooks/useAuthenticatedImage';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import {
  getApiErrorMessage, showLoadingToast, updateToastSuccess, updateToastMessageError,
} from '../../../shared/utils/toast';

export interface FaceScanPhoto {
  visitPhotoId: number;
  url: string;
  name: string;
  uploadedByName?: string;
  uploadedAt?: string;
}

interface FaceScanPanelProps {
  visitInstanceId: number;
  photos: FaceScanPhoto[];
  isReadOnly: boolean;
  onUploadClick: () => void;
  folderUrl?: string;
  onRefreshPhotos?: () => void | Promise<void>;
}

/** Imperative handle — cho phép nơi nhúng (header ngoài) mở modal "Chọn từ thư mục ảnh đoàn" mà không cần nâng state isAlbumModalOpen lên. */
export interface FaceScanPanelHandle {
  openAlbumModal: () => void;
}

function PhotoThumbnail({ url, alt }: { url: string; alt: string }) {
  const src = useAuthenticatedImage(url);
  return src ? (
    <img src={src} alt={alt} className="w-full h-full object-cover" />
  ) : (
    <div className="w-full h-full flex items-center justify-center bg-gray-100">
      <ImageIcon className="w-4 h-4 text-gray-300" />
    </div>
  );
}

const STATUS_BADGE_CLASS: Record<string, string> = {
  PENDING: 'bg-gray-100 text-gray-600 border-gray-200',
  PROCESSING: 'bg-blue-50 text-blue-700 border-blue-100',
  SUCCEEDED: 'bg-amber-50 text-amber-700 border-amber-200',
  FAILED: 'bg-red-50 text-red-600 border-red-100',
  CONFIRMED: 'bg-emerald-50 text-emerald-700 border-emerald-200',
};

export const FaceScanPanel = forwardRef<FaceScanPanelHandle, FaceScanPanelProps>(function FaceScanPanel({
  visitInstanceId, photos, isReadOnly, onUploadClick, folderUrl, onRefreshPhotos,
}, ref) {
  const { t } = useTranslation('visitFaceScan');

  const [selectedPhotoId, setSelectedPhotoId] = useState<number | null>(() => photos[0]?.visitPhotoId ?? null);
  const [userSelectedPhotoId, setUserSelectedPhotoId] = useState<number | null>(null);
  const [scans, setScans] = useState<VisitPhotoFaceScan[]>([]);
  const [loadingScans, setLoadingScans] = useState(false);
  const [scanning, setScanning] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [taggableGuests, setTaggableGuests] = useState<TaggableGuest[]>([]);
  const [guestsLoading, setGuestsLoading] = useState(false);
  const [activeFaceId, setActiveFaceId] = useState<number | null>(null);
  const [pendingActions, setPendingActions] = useState<Record<number, { guestMemberId: number | null; ignored: boolean }>>({});
  const [searchGuestKeyword, setSearchGuestKeyword] = useState('');
  const [isAlbumModalOpen, setIsAlbumModalOpen] = useState(false);

  useImperativeHandle(ref, () => ({
    openAlbumModal: () => {
      if (onRefreshPhotos) void onRefreshPhotos();
      setIsAlbumModalOpen(true);
    },
  }), [onRefreshPhotos]);

  const selectedPhoto = photos.find((p) => p.visitPhotoId === selectedPhotoId) ?? null;
  const currentScan = scans[0] ?? null; // scans are ordered newest-first by the backend

  const selectedImageUrl = useAuthenticatedImage(selectedPhoto?.url ?? null);

  const selectPhoto = (photoId: number | null) => {
    setSelectedPhotoId(photoId);
    setUserSelectedPhotoId(photoId);
    if (photoId && visitInstanceId) {
      try {
        localStorage.setItem(`pems_facescan_selected_photo_${visitInstanceId}`, String(photoId));
      } catch {}
    }
  };

  // Auto-detect photo with confirmed/succeeded face tags from DB across all logins/devices
  useEffect(() => {
    if (photos.length === 0) {
      if (selectedPhotoId !== null) setSelectedPhotoId(null);
      return;
    }

    // If user explicitly clicked a photo in this session and it's in photos list, keep it
    if (userSelectedPhotoId && photos.some((p) => p.visitPhotoId === userSelectedPhotoId)) {
      setSelectedPhotoId(userSelectedPhotoId);
      return;
    }

    let cancelled = false;

    const findConfirmedPhotoFromDb = async () => {
      try {
        const scanResults = await Promise.all(
          photos.map(async (p) => {
            try {
              const list = await visitPhotosApi.getFaceScans(p.visitPhotoId);
              return { photoId: p.visitPhotoId, scans: list };
            } catch {
              return { photoId: p.visitPhotoId, scans: [] };
            }
          })
        );
        if (cancelled) return;

        // 1. Prefer photo with CONFIRMED status or confirmed detections in DB
        const confirmedItem = scanResults.find((r) =>
          r.scans.some((s) => s.status === 'CONFIRMED' || s.detections?.some((d) => d.reviewStatus === 'CONFIRMED'))
        );
        if (confirmedItem) {
          setSelectedPhotoId(confirmedItem.photoId);
          return;
        }

        // 2. Prefer photo with SUCCEEDED status & face detections
        const succeededItem = scanResults.find((r) =>
          r.scans.some((s) => s.status === 'SUCCEEDED' && s.detections?.length > 0)
        );
        if (succeededItem) {
          setSelectedPhotoId(succeededItem.photoId);
          return;
        }
      } catch {}

      if (cancelled) return;

      // 3. Fallback to localStorage saved ID if present in photos
      try {
        const saved = localStorage.getItem(`pems_facescan_selected_photo_${visitInstanceId}`);
        const savedId = saved ? Number(saved) : null;
        if (savedId && photos.some((p) => p.visitPhotoId === savedId)) {
          setSelectedPhotoId(savedId);
          return;
        }
      } catch {}

      // 4. Default to first photo
      setSelectedPhotoId((prev) => (prev && photos.some((p) => p.visitPhotoId === prev) ? prev : photos[0].visitPhotoId));
    };

    void findConfirmedPhotoFromDb();

    return () => {
      cancelled = true;
    };
  }, [photos, visitInstanceId, userSelectedPhotoId]);

  // Taggable guests of THIS exact visit instance — loaded once.
  useEffect(() => {
    let cancelled = false;
    setGuestsLoading(true);
    visitPhotosApi.getTaggableGuests(visitInstanceId)
      .then((list) => { if (!cancelled) setTaggableGuests(list); })
      .catch(() => { if (!cancelled) setTaggableGuests([]); })
      .finally(() => { if (!cancelled) setGuestsLoading(false); });
    return () => { cancelled = true; };
  }, [visitInstanceId]);

  // Scan history (newest/lịch sử) whenever the selected photo changes.
  useEffect(() => {
    setPendingActions({});
    setActiveFaceId(null);
    setSearchGuestKeyword('');
    if (!selectedPhotoId) { setScans([]); return; }
    let cancelled = false;
    setLoadingScans(true);
    visitPhotosApi.getFaceScans(selectedPhotoId)
      .then((list) => { if (!cancelled) setScans(list); })
      .catch(() => { if (!cancelled) setScans([]); })
      .finally(() => { if (!cancelled) setLoadingScans(false); });
    return () => { cancelled = true; };
  }, [selectedPhotoId]);

  const detections = currentScan?.detections ?? [];
  const isScanBusy = currentScan?.status === 'PENDING' || currentScan?.status === 'PROCESSING';

  const resolvedGuestIds = useMemo(() => {
    const s = new Set<number>();
    Object.values(pendingActions).forEach((a) => { if (a.guestMemberId) s.add(a.guestMemberId); });
    return s;
  }, [pendingActions]);

  const [croppedAvatarModal, setCroppedAvatarModal] = useState<{ guestName: string; guestMemberId?: number | null; dataUrl: string } | null>(null);
  const [updatingAvatar, setUpdatingAvatar] = useState(false);

  const handleCropAvatar = async (detection: VisitPhotoFaceDetection) => {
    if (!selectedImageUrl) return;
    try {
      const dataUrl = await cropFaceToDataUrl(selectedImageUrl, detection);
      const { name } = resolveDisplayName(detection);
      let guestMemberId: number | null = detection.guestMemberId ?? null;
      if (!guestMemberId && pendingActions[detection.faceDetectionId]?.guestMemberId) {
        guestMemberId = pendingActions[detection.faceDetectionId].guestMemberId;
      }
      const guestName = name || 'Khách đối tác';
      setCroppedAvatarModal({ guestName, guestMemberId, dataUrl });
    } catch {
      toast.error('Không thể cắt ảnh avatar từ khuôn mặt này.');
    }
  };

  const handleUpdateAvatar = async () => {
    if (!croppedAvatarModal) return;
    setUpdatingAvatar(true);
    const toastId = showLoadingToast('Đang tải lên và cập nhật Avatar...', 'update-avatar');
    try {
      const res = await fetch(croppedAvatarModal.dataUrl);
      const blob = await res.blob();
      const file = new File([blob], `avatar_${croppedAvatarModal.guestName.replace(/\s+/g, '_')}.png`, { type: 'image/png' });

      const uploaded = await uploadFileToEndpoint('/files/upload', 'file', file, 'post', { purpose: 'PARTNER_CONTACT_AVATAR' });

      const links = await partnersApi.getVisitPartnerLinks(visitInstanceId);
      let targetLink = links.find((l) => l.guestMemberId === croppedAvatarModal.guestMemberId);
      if (!targetLink && croppedAvatarModal.guestName) {
        targetLink = links.find((l) => l.partnerName && croppedAvatarModal.guestName.toLowerCase().includes(l.partnerName.toLowerCase()));
      }

      if (targetLink && targetLink.partnerId) {
        let contactId = targetLink.partnerContactId;
        if (!contactId) {
          const contacts = await partnersApi.getContacts(targetLink.partnerId);
          const matched = contacts.find((c) => c.fullName.toLowerCase() === croppedAvatarModal.guestName.toLowerCase());
          if (matched) contactId = matched.contactId;
        }

        if (contactId) {
          await partnersApi.updateContact(targetLink.partnerId, contactId, {
            fullName: croppedAvatarModal.guestName,
            avatarFileId: uploaded.fileId,
          });
          updateToastSuccess(toastId, `Đã cập nhật Avatar cho người liên hệ ${croppedAvatarModal.guestName} thành công!`);
          setCroppedAvatarModal(null);
          return;
        }
      }

      updateToastMessageError(toastId, `Chưa tìm thấy đối tác liên kết với ${croppedAvatarModal.guestName}. Vui lòng liên kết đối tác trước!`);
    } catch (err: any) {
      updateToastMessageError(toastId, getApiErrorMessage(err, 'Không thể cập nhật Avatar người liên hệ.'));
    } finally {
      setUpdatingAvatar(false);
    }
  };

  const canConfirm = currentScan?.status === 'SUCCEEDED' && detections.length > 0;

  const handleScan = async () => {
    if (!selectedPhotoId || scanning || isScanBusy) return;
    setScanning(true);
    const toastId = showLoadingToast(t('scan.startingToast'), 'face-scan');
    try {
      const result = await visitPhotosApi.startFaceScan(selectedPhotoId);
      setScans((prev) => [result, ...prev]);
      setPendingActions({});
      if (result.status === 'FAILED') {
        updateToastMessageError(toastId, result.errorMessage || t('scan.errorFallback'));
      } else {
        updateToastSuccess(toastId, t('scan.successToast'));
      }
    } catch (e) {
      updateToastMessageError(toastId, getApiErrorMessage(e, t('scan.errorFallback')));
      try {
        const list = await visitPhotosApi.getFaceScans(selectedPhotoId);
        setScans(list);
      } catch { /* best effort refresh */ }
    } finally {
      setScanning(false);
    }
  };

  const handleTag = (faceDetectionId: number, guestMemberId: number | null, ignored: boolean) => {
    setPendingActions((prev) => ({ ...prev, [faceDetectionId]: { guestMemberId, ignored } }));
    setActiveFaceId(null);
  };

  const handleConfirm = async () => {
    if (!currentScan || !canConfirm) return;
    setConfirming(true);
    const toastId = showLoadingToast(t('confirm.startingToast'), 'face-confirm');
    try {
      const faces: ConfirmFaceTagItem[] = detections
        .filter((d) => d.reviewStatus === 'DETECTED')
        .map((d) => {
          const action = pendingActions[d.faceDetectionId];
          const isGuestAssigned = action && action.guestMemberId !== null && !action.ignored;
          return {
            faceDetectionId: d.faceDetectionId,
            guestMemberId: isGuestAssigned ? action.guestMemberId : null,
            ignored: !isGuestAssigned,
          };
        });
      const result = await visitPhotosApi.confirmFaceTags(currentScan.faceScanId, currentScan.rowVersion, faces);
      setScans((prev) => prev.map((s) => (s.faceScanId === result.faceScanId ? result : s)));
      setPendingActions({});
      void onRefreshPhotos?.();
      updateToastSuccess(toastId, t('confirm.successToast'));
    } catch (e) {
      updateToastMessageError(toastId, getApiErrorMessage(e, t('confirm.errorFallback')));
    } finally {
      setConfirming(false);
    }
  };

  const resolveDisplayName = (detection: VisitPhotoFaceDetection): { name: string | null; ignored: boolean } => {
    if (detection.reviewStatus === 'CONFIRMED') return { name: detection.guestFullName ?? null, ignored: false };
    if (detection.reviewStatus === 'IGNORED') return { name: null, ignored: true };
    const pending = pendingActions[detection.faceDetectionId];
    if (!pending) return { name: null, ignored: false };
    if (pending.ignored) return { name: null, ignored: true };
    const guest = taggableGuests.find((g) => g.guestMemberId === pending.guestMemberId);
    return { name: guest?.fullName ?? null, ignored: false };
  };

  return (
    <div className="animate-fadeIn">
      {/* Interactive preview + scan controls — full width (danh sách ảnh bên trái đã bỏ, chọn ảnh qua modal "Chọn từ thư mục ảnh đoàn"). */}
      <div className="border border-gray-200 rounded-2xl overflow-hidden bg-gray-50 flex flex-col min-h-[85vh] relative">
        {selectedPhoto ? (
          <>
            <div className="bg-white px-5 py-3 border-b border-gray-200 flex items-center justify-between flex-wrap gap-2">
              <div className="flex items-center gap-2">
                <ImageIcon className="w-4 h-4 text-gray-500" />
                <span className="text-xs font-bold text-gray-700 truncate max-w-[260px]" title={selectedPhoto.name}>
                  {selectedPhoto.name}
                </span>
              </div>

              <div className="flex items-center gap-3">
                <div className="relative hidden sm:block">
                  <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400 w-3.5 h-3.5" />
                  <input
                    type="text"
                    placeholder={t('search.placeholder') ?? ''}
                    value={searchGuestKeyword}
                    onChange={(e) => setSearchGuestKeyword(e.target.value)}
                    className="pl-8 pr-3 py-1 bg-gray-100 hover:bg-gray-200/50 focus:bg-white text-xs border border-transparent focus:border-[#004c91] rounded-lg outline-none w-[200px]"
                  />
                </div>

                {!isReadOnly && (
                  <button
                    type="button"
                    onClick={handleScan}
                    disabled={scanning || isScanBusy}
                    className="px-3.5 py-1.5 bg-[#f37021] hover:bg-orange-600 disabled:opacity-50 text-white rounded-lg text-xs font-extrabold shadow-sm transition-all flex items-center gap-1.5"
                  >
                    <Sparkles className="w-3.5 h-3.5" />
                    {scanning || isScanBusy
                      ? t('scan.scanning')
                      : currentScan
                        ? t('scan.rescanButton')
                        : t('scan.button')}
                  </button>
                )}
              </div>
            </div>

            {searchGuestKeyword && (
              <div className="bg-yellow-50 px-5 py-2 border-b border-yellow-100 text-xs font-semibold text-yellow-900 flex items-center gap-2">
                <Tag className="w-3.5 h-3.5 text-[#f37021]" />
                <span>{t('search.filteringFor')} <strong className="text-[#004c91]">"{searchGuestKeyword}"</strong></span>
                <button onClick={() => setSearchGuestKeyword('')} className="ml-auto underline hover:text-[#004c91] text-gray-500 font-bold">
                  {t('search.clear')}
                </button>
              </div>
            )}

            <div className="flex-1 flex items-center justify-center p-3 relative select-none">
              <div className="relative max-w-full max-h-[80vh] rounded-lg border border-gray-300 bg-white">
                {selectedImageUrl ? (
                  <img
                    src={selectedImageUrl}
                    alt={selectedPhoto.name}
                    className={`max-w-full max-h-[78vh] object-contain rounded-lg transition-all ${scanning ? 'brightness-50' : ''}`}
                  />
                ) : (
                  <div className="w-[320px] h-[240px] flex items-center justify-center">
                    <Loader2 className="w-6 h-6 text-gray-300 animate-spin" />
                  </div>
                )}

                {scanning && (
                  <motion.div
                    initial={{ top: '0%' }}
                    animate={{ top: '100%' }}
                    transition={{ repeat: Infinity, duration: 1.5, ease: 'linear' }}
                    className="absolute left-0 right-0 h-1 bg-gradient-to-r from-orange-500 via-yellow-400 to-orange-500 shadow-[0_0_15px_4px_rgba(243,112,33,1)] z-10"
                  />
                )}

                {currentScan?.status === 'SUCCEEDED' || currentScan?.status === 'CONFIRMED' ? (
                  detections.map((detection, idx) => {
                    const { name, ignored } = resolveDisplayName(detection);
                    const isHighlightedBySearch = searchGuestKeyword
                      ? (name ?? '').toLowerCase().includes(searchGuestKeyword.toLowerCase())
                      : true;
                    if (searchGuestKeyword && !isHighlightedBySearch) return null;

                    const canEdit = !isReadOnly && detection.reviewStatus === 'DETECTED' && currentScan.status === 'SUCCEEDED';
                    const availableGuests = taggableGuests.filter((g) =>
                      !resolvedGuestIds.has(g.guestMemberId) || pendingActions[detection.faceDetectionId]?.guestMemberId === g.guestMemberId);

                    const isActive = activeFaceId === detection.faceDetectionId;
                    const isLowerHalf = detection.boundingBoxY >= 0.5;

                    const isTopEdge = detection.boundingBoxY < 0.2;
                    const stemHeight = 24 + (idx % 3) * 20; // Stem heights: 24px, 44px, 64px elevated high above background heads
                    const badgeBgClass = name
                      ? 'bg-emerald-950/90 text-emerald-200 border-emerald-400/50'
                      : 'bg-orange-950/90 text-orange-200 border-orange-400/50';
                    const stemColor = name ? 'bg-emerald-400/90' : 'bg-orange-400/90';

                    const hasBadge = Boolean(name || isActive);

                    return (
                      <div
                        key={detection.faceDetectionId}
                        style={{
                          left: `${detection.boundingBoxX * 100}%`,
                          top: `${detection.boundingBoxY * 100}%`,
                          width: `${detection.boundingBoxWidth * 100}%`,
                          height: `${detection.boundingBoxHeight * 100}%`,
                        }}
                        className={`absolute border-[1.5px] ${canEdit ? 'cursor-pointer' : 'cursor-default'} transition-all rounded-sm group/box ${
                          isActive ? 'z-50 ring-2 ring-[#004c91]' : 'z-10'
                        } ${
                          ignored
                            ? 'border-gray-400/40 bg-gray-400/5'
                            : name
                              ? 'border-emerald-400 bg-emerald-400/10 hover:bg-emerald-400/20'
                              : 'border-orange-400/80 bg-orange-400/5 hover:bg-orange-400/20'
                        }`}
                        onClick={() => {
                          if (!canEdit) return;
                          setActiveFaceId((prev) => (prev === detection.faceDetectionId ? null : detection.faceDetectionId));
                        }}
                      >
                        {/* Hiển thị thẻ tên + đường kẻ nối CHỈ KHI khuôn mặt đã được gán tên hoặc đang click chọn */}
                        {hasBadge && (
                          <>
                            {/* Chấm ghim ở viền khung mặt */}
                            <div className={`absolute left-1/2 -translate-x-1/2 w-1 h-1 rounded-full ${name ? 'bg-emerald-400' : 'bg-orange-400'} ${isTopEdge ? '-bottom-0.5' : '-top-0.5'}`} />

                            {/* Leader line / Stem vươn cao khỏi đầu người đằng sau */}
                            <div
                              style={{ height: `${stemHeight}px` }}
                              className={`absolute left-1/2 -translate-x-1/2 w-[1px] pointer-events-none ${stemColor} ${
                                isTopEdge ? 'top-full' : 'bottom-full'
                              }`}
                            />

                            {/* Floating compact pill badge */}
                            <div
                              style={isTopEdge ? { top: `calc(100% + ${stemHeight}px)` } : { bottom: `calc(100% + ${stemHeight}px)` }}
                              className="absolute left-1/2 -translate-x-1/2 whitespace-nowrap z-20 pointer-events-auto"
                            >
                              <span
                                className={`px-1.5 py-0.5 rounded-full text-[7px] sm:text-[8px] font-semibold leading-none tracking-tight block max-w-[80px] sm:max-w-[95px] truncate border shadow-md backdrop-blur-xs ${badgeBgClass}`}
                                title={name || t('tag.unassigned')}
                              >
                                {name || t('tag.unassigned')}
                              </span>
                            </div>
                          </>
                        )}

                        {isActive && (
                          <div
                            className={`absolute ${isLowerHalf ? 'bottom-full mb-2' : 'top-full mt-2'} left-1/2 -translate-x-1/2 bg-white border border-gray-200 p-3 rounded-2xl shadow-2xl z-50 w-60 text-left space-y-2.5 max-h-[260px] flex flex-col`}
                            onClick={(e) => e.stopPropagation()}
                          >
                            <div className="flex items-center justify-between border-b border-gray-100 pb-1.5 shrink-0">
                              <span className="text-[10px] uppercase font-bold text-gray-500">{t('tag.dropdownTitle')}</span>
                              <button onClick={() => setActiveFaceId(null)} className="text-gray-400 hover:text-gray-600 p-1 hover:bg-gray-100 rounded-full transition-colors" title="Đóng">
                                <X className="w-3.5 h-3.5" />
                              </button>
                            </div>

                            <div className="space-y-1 overflow-y-auto pr-1 flex-1 max-h-[190px]">
                              <button
                                type="button"
                                onClick={() => handleTag(detection.faceDetectionId, null, true)}
                                className={`w-full text-left px-2 py-1.5 rounded-lg text-xs font-semibold flex items-center justify-between transition-colors ${pendingActions[detection.faceDetectionId]?.ignored ? 'bg-orange-50 text-[#f37021]' : 'hover:bg-slate-50 text-gray-600'}`}
                              >
                                <span>{t('tag.notInDelegation')}</span>
                                {pendingActions[detection.faceDetectionId]?.ignored && <Check className="w-3.5 h-3.5" />}
                              </button>

                              {guestsLoading && (
                                <p className="text-[11px] text-gray-400 px-2 py-1.5">{t('tag.loadingGuests')}</p>
                              )}
                              {!guestsLoading && availableGuests.length === 0 && (
                                <p className="text-[11px] text-gray-400 px-2 py-1.5">{t('tag.noGuestsFound')}</p>
                              )}
                              {availableGuests.map((guest) => (
                                <button
                                  key={guest.guestMemberId}
                                  type="button"
                                  onClick={() => handleTag(detection.faceDetectionId, guest.guestMemberId, false)}
                                  className={`w-full text-left px-2 py-1.5 rounded-lg text-xs font-semibold flex flex-col transition-colors ${pendingActions[detection.faceDetectionId]?.guestMemberId === guest.guestMemberId ? 'bg-[#004c91]/5 text-[#004c91]' : 'hover:bg-slate-50 text-gray-700'}`}
                                >
                                  <div className="flex items-center justify-between w-full font-bold">
                                    <span>{guest.fullName}</span>
                                    {pendingActions[detection.faceDetectionId]?.guestMemberId === guest.guestMemberId && <Check className="w-3.5 h-3.5 text-[#004c91]" />}
                                  </div>
                                  <span className="text-[9px] text-gray-400 mt-0.5">{guest.jobTitle} · {guest.organization}</span>
                                </button>
                              ))}
                            </div>
                          </div>
                        )}
                      </div>
                    );
                  })
                ) : null}
              </div>

              {(scanning || isScanBusy) && (
                <div className="absolute inset-0 flex flex-col items-center justify-center text-white p-6 bg-slate-900/60 z-10">
                  <div className="animate-spin rounded-full h-8 w-8 border-2 border-t-transparent border-white mb-2"></div>
                  <p className="text-sm font-bold tracking-wide">{t('scan.scanning')}</p>
                </div>
              )}

              {!currentScan && !scanning && !loadingScans && (
                <div className="absolute bottom-3 left-1/2 -translate-x-1/2 bg-slate-900/80 backdrop-blur-sm px-4 py-1.5 rounded-full text-[10px] text-white font-bold tracking-wide shadow-md">
                  {t('scan.watermarkIdle')}
                </div>
              )}

              {currentScan?.status === 'SUCCEEDED' && detections.length > 0 && (
                <div className="absolute bottom-3 left-1/2 -translate-x-1/2 bg-emerald-900/90 backdrop-blur-sm px-4 py-1.5 rounded-full text-[10px] text-white font-bold tracking-wide shadow-md">
                  {t('scan.watermarkDone')}
                </div>
              )}

              {currentScan?.status === 'SUCCEEDED' && detections.length === 0 && (
                <div className="absolute bottom-3 left-1/2 -translate-x-1/2 bg-slate-900/80 backdrop-blur-sm px-4 py-1.5 rounded-full text-[10px] text-white font-bold tracking-wide shadow-md">
                  {t('scan.noFacesDetected')}
                </div>
              )}

              {currentScan?.status === 'FAILED' && (
                <div className="absolute bottom-3 left-1/2 -translate-x-1/2 bg-red-900/90 backdrop-blur-sm px-4 py-1.5 rounded-full text-[10px] text-white font-bold tracking-wide shadow-md flex items-center gap-1.5">
                  <AlertCircle className="w-3 h-3" /> {currentScan.errorMessage || t('scan.errorFallback')}
                </div>
              )}
            </div>

            {/* Confirm action + tag summary */}
            {currentScan?.status === 'SUCCEEDED' && detections.length > 0 && (
              <div className="bg-white border-t border-gray-100 p-4 space-y-3">
                <div className="flex items-center justify-between flex-wrap gap-2">
                  <p className="text-xs font-bold text-gray-500 uppercase tracking-wide">{t('tag.summaryTitle')}</p>
                  {!isReadOnly && (
                    <button
                      type="button"
                      onClick={handleConfirm}
                      disabled={!canConfirm || confirming}
                      title={!canConfirm ? t('confirm.pendingRequired') ?? '' : undefined}
                      className="px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-700 disabled:opacity-40 text-white rounded-lg text-xs font-extrabold shadow-sm transition-all flex items-center gap-1.5"
                    >
                      {confirming ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <CheckCircle2 className="w-3.5 h-3.5" />}
                      {t('confirm.button')}
                    </button>
                  )}
                </div>
                {(() => {
                  const identifiedDetections = detections
                    .map((d, idx) => ({ d, originalIndex: idx + 1, ...resolveDisplayName(d) }))
                    .filter((item) => Boolean(item.name));

                  if (identifiedDetections.length === 0) {
                    return (
                      <p className="text-xs font-semibold text-gray-400 italic py-1">
                        Chưa có khuôn mặt nào được định danh trong hình này. Nhấp chọn từng khuôn mặt trên hình để gán tên tương ứng.
                      </p>
                    );
                  }

                  return (
                    <div className="flex flex-wrap gap-2">
                      {identifiedDetections.map(({ d, originalIndex, name, ignored }) => (
                        <div
                          key={d.faceDetectionId}
                          className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-xl text-xs font-bold border transition-colors ${
                            name
                              ? 'bg-emerald-50 text-emerald-800 border-emerald-200 shadow-sm'
                              : 'bg-gray-100 text-gray-600 border-gray-200'
                          }`}
                        >
                          <User className="w-3.5 h-3.5" />
                          <span>
                            Vị trí #{originalIndex}: {name || t('tag.ignored')}
                          </span>
                          {name && (
                            <button
                              type="button"
                              onClick={(e) => { e.stopPropagation(); void handleCropAvatar(d); }}
                              className="p-1 text-emerald-600 hover:text-emerald-900 hover:bg-emerald-100 rounded-md transition-colors ml-1"
                              title="Cắt khuôn mặt này làm Avatar Người liên hệ đối tác"
                            >
                              <Crop className="w-3.5 h-3.5" />
                            </button>
                          )}
                        </div>
                      ))}
                    </div>
                  );
                })()}
              </div>
            )}

            {/* Scan history */}
            <div className="bg-white border-t border-gray-100 p-4">
              <p className="text-xs font-bold text-gray-500 uppercase tracking-wide mb-2">{t('history.title')}</p>
              {loadingScans ? (
                <div className="flex justify-center py-3"><Loader2 className="w-4 h-4 animate-spin text-gray-400" /></div>
              ) : scans.length === 0 ? (
                <p className="text-xs text-gray-400">{t('history.empty')}</p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-xs">
                    <thead>
                      <tr className="text-left text-gray-400 uppercase tracking-wide">
                        <th className="font-bold pb-1.5 pr-3">{t('history.time')}</th>
                        <th className="font-bold pb-1.5 pr-3">{t('history.scannedBy')}</th>
                        <th className="font-bold pb-1.5 pr-3">{t('history.status')}</th>
                        <th className="font-bold pb-1.5 pr-3">{t('history.faces')}</th>
                        <th className="font-bold pb-1.5">{t('history.error')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {scans.map((s) => (
                        <tr key={s.faceScanId} className="border-t border-gray-50">
                          <td className="py-1.5 pr-3 text-gray-600 whitespace-nowrap">{formatVietnamDateTime(s.requestedAt)}</td>
                          <td className="py-1.5 pr-3 text-gray-600">{s.requestedByName || '—'}</td>
                          <td className="py-1.5 pr-3">
                            <span className={`px-2 py-0.5 rounded-md border text-[10px] font-bold ${STATUS_BADGE_CLASS[s.status] ?? ''}`}>
                              {t(`status.${s.status}`)}
                            </span>
                          </td>
                          <td className="py-1.5 pr-3 text-gray-600 whitespace-nowrap">
                            {s.detectedFaceCount}/{s.reviewedFaceCount}/{s.ignoredFaceCount}
                          </td>
                          <td className="py-1.5 text-red-500">{s.errorMessage || ''}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </>
        ) : (
          <div className="flex-1 flex flex-col items-center justify-center text-gray-400 p-8 text-center space-y-4">
            <div className="w-16 h-16 rounded-2xl bg-blue-50 flex items-center justify-center text-[#004c91] border border-blue-100 shadow-sm">
              <FolderOpen className="w-8 h-8" />
            </div>
            <div>
              <p className="text-base font-bold text-gray-800">Chưa chọn ảnh để quét và gán tên khuôn mặt</p>
              <p className="text-xs text-gray-500 mt-1 max-w-md">
                Tải ảnh mới lên hoặc chọn ảnh sẵn có trong thư mục Google Drive của đoàn để tiến hành quét nhận diện.
              </p>
            </div>

            <div className="flex flex-wrap items-center justify-center gap-3 pt-2">
              {!isReadOnly && (
                <button
                  type="button"
                  onClick={onUploadClick}
                  className="flex items-center gap-2 px-5 py-2.5 bg-[#004c91] text-white hover:bg-[#00386b] rounded-xl font-extrabold text-xs shadow-md transition-all active:scale-95 cursor-pointer"
                >
                  <Upload className="w-4 h-4" /> Tải lên ảnh chụp thực tế
                </button>
              )}
              <button
                type="button"
                onClick={() => {
                  if (onRefreshPhotos) void onRefreshPhotos();
                  setIsAlbumModalOpen(true);
                }}
                className="flex items-center gap-2 px-5 py-2.5 bg-white text-[#004c91] border border-[#004c91] hover:bg-blue-50 rounded-xl font-extrabold text-xs shadow-sm transition-all active:scale-95 cursor-pointer"
              >
                <FolderOpen className="w-4 h-4" /> Chọn từ thư mục ảnh đoàn (Google Drive)
              </button>
            </div>
          </div>
        )}
      </div>

      {/* MODAL: CHỌN ẢNH TỪ THƯ MỤC ẢNH ĐOÀN (GOOGLE DRIVE) */}
      <AnimatePresence>
        {isAlbumModalOpen && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              onClick={() => setIsAlbumModalOpen(false)}
              className="absolute inset-0 bg-slate-900/60 backdrop-blur-sm cursor-pointer"
            />

            <motion.div
              initial={{ scale: 0.95, opacity: 0, y: 15 }}
              animate={{ scale: 1, opacity: 1, y: 0 }}
              exit={{ scale: 0.95, opacity: 0, y: 15 }}
              className="bg-white rounded-3xl shadow-xl w-full max-w-4xl overflow-hidden relative border border-gray-100 z-10 text-left font-sans flex flex-col max-h-[85vh]"
            >
              {/* Header */}
              <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between gap-3 shrink-0 flex-wrap">
                <div className="flex items-center gap-3">
                  <div className="w-10 h-10 rounded-xl bg-blue-50 flex items-center justify-center text-[#004c91] border border-blue-100 shrink-0">
                    <FolderOpen className="w-5 h-5" />
                  </div>
                  <div>
                    <h3 className="text-base font-bold text-gray-900">Thư mục ảnh đoàn (Google Drive)</h3>
                    <p className="text-xs text-gray-500">Các ảnh chụp đoàn do sinh viên / thành viên đã tải lên Google Drive của đoàn.</p>
                  </div>
                </div>

                <div className="flex items-center gap-2">
                  {folderUrl && (
                    <a
                      href={folderUrl}
                      target="_blank"
                      rel="noreferrer"
                      className="px-3 py-1.5 bg-blue-50 text-[#004c91] hover:bg-blue-100 rounded-xl text-xs font-extrabold border border-blue-200 transition-colors flex items-center gap-1.5 cursor-pointer"
                    >
                      <FolderOpen className="w-3.5 h-3.5" /> Mở Google Drive <ExternalLink className="w-3 h-3" />
                    </a>
                  )}
                  {onRefreshPhotos && (
                    <button
                      type="button"
                      onClick={() => void onRefreshPhotos()}
                      className="px-3 py-1.5 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-xl text-xs font-bold transition-colors flex items-center gap-1.5 cursor-pointer"
                    >
                      <RefreshCw className="w-3.5 h-3.5" /> Làm mới
                    </button>
                  )}
                  <button
                    type="button"
                    onClick={() => setIsAlbumModalOpen(false)}
                    className="p-1.5 hover:bg-gray-100 rounded-full text-gray-400 hover:text-gray-600 transition-colors cursor-pointer"
                  >
                    <X className="w-5 h-5" />
                  </button>
                </div>
              </div>

              {/* Body */}
              <div className="p-6 overflow-y-auto flex-1">
                {photos.length === 0 ? (
                  <div className="py-12 text-center text-gray-400 space-y-3">
                    <FolderOpen className="w-12 h-12 text-gray-300 mx-auto" />
                    <p className="text-sm font-bold text-gray-700">Chưa có ảnh nào trong thư mục đoàn.</p>
                    <p className="text-xs text-gray-400 max-w-md mx-auto">
                      Bạn có thể dùng nút "Tải lên ảnh chụp thực tế" ngoài màn hình chính để tải ảnh trực tiếp vào thư mục Google Drive của đoàn.
                    </p>
                  </div>
                ) : (
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-4">
                    {photos.map((photo) => (
                      <div
                        key={photo.visitPhotoId}
                        onClick={() => {
                          selectPhoto(photo.visitPhotoId);
                          setIsAlbumModalOpen(false);
                        }}
                        className={`group bg-white rounded-2xl border p-3 flex flex-col justify-between cursor-pointer transition-all hover:shadow-md ${
                          selectedPhotoId === photo.visitPhotoId
                            ? 'border-[#004c91] ring-2 ring-[#004c91]/20 bg-[#004c91]/5'
                            : 'border-gray-200 hover:border-[#004c91]'
                        }`}
                      >
                        <div className="aspect-square w-full rounded-xl overflow-hidden bg-gray-100 border border-gray-200 mb-2 relative">
                          <PhotoThumbnail url={photo.url} alt={photo.name} />
                        </div>
                        <div className="space-y-0.5">
                          <p className="text-xs font-bold text-gray-800 truncate" title={photo.name}>{photo.name}</p>
                          {photo.uploadedByName && (
                            <p className="text-[10px] text-gray-400 truncate">Đăng bởi: {photo.uploadedByName}</p>
                          )}
                        </div>
                        <button
                          type="button"
                          className="mt-3 w-full py-1.5 bg-[#004c91] group-hover:bg-[#00386b] text-white rounded-lg text-xs font-extrabold transition-colors shadow-sm"
                        >
                          {selectedPhotoId === photo.visitPhotoId ? 'Đang chọn' : 'Chọn ảnh để quét'}
                        </button>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {/* Footer */}
              <div className="px-6 py-3 bg-gray-50 border-t border-gray-100 flex items-center justify-between text-xs text-gray-500 font-medium">
                <span>Tổng cộng: <strong>{photos.length}</strong> ảnh trong thư mục đoàn.</span>
                <button
                  type="button"
                  onClick={() => setIsAlbumModalOpen(false)}
                  className="px-4 py-1.5 bg-white hover:bg-gray-100 text-gray-700 border border-gray-200 rounded-lg font-bold cursor-pointer"
                >
                  Đóng
                </button>
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>

      {/* Cropped Avatar Modal */}
      {croppedAvatarModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl p-6 max-w-sm w-full space-y-4 shadow-2xl animate-in zoom-in-95">
            <div className="flex items-center justify-between border-b border-gray-100 pb-3">
              <h3 className="font-extrabold text-[#004c91] text-sm flex items-center gap-2">
                <Crop className="w-4 h-4 text-[#f37021]" /> Ảnh đại diện (Avatar) đối tác
              </h3>
              <button onClick={() => setCroppedAvatarModal(null)} className="text-gray-400 hover:text-gray-600 p-1 rounded-lg">
                <X className="w-4 h-4" />
              </button>
            </div>
            <div className="text-center space-y-3">
              <div className="w-32 h-32 rounded-full overflow-hidden mx-auto border-4 border-[#004c91]/20 shadow-md">
                <img src={croppedAvatarModal.dataUrl} alt={croppedAvatarModal.guestName} className="w-full h-full object-cover" />
              </div>
              <div>
                <p className="font-bold text-gray-800 text-sm">{croppedAvatarModal.guestName}</p>
                <p className="text-xs text-gray-400 font-medium">Đã tự động trích xuất tỷ lệ 1:1 từ vị trí khuôn mặt</p>
              </div>
            </div>
            <div className="pt-2 flex items-center gap-2">
              <button
                type="button"
                onClick={() => void handleUpdateAvatar()}
                disabled={updatingAvatar}
                className="flex-1 flex items-center justify-center gap-1.5 py-2.5 bg-[#00a651] text-white hover:bg-[#008943] disabled:opacity-50 rounded-xl font-bold text-xs shadow-sm transition-all cursor-pointer"
              >
                {updatingAvatar ? <Loader2 className="w-4 h-4 animate-spin" /> : <Crop className="w-4 h-4" />}
                Cập nhật Avatar đối tác
              </button>
              <button
                type="button"
                onClick={() => setCroppedAvatarModal(null)}
                className="px-3.5 py-2.5 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-xl font-bold text-xs transition-colors cursor-pointer"
              >
                Hủy
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
});
