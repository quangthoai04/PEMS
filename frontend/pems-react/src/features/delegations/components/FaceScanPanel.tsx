/**
 * FaceScanPanel — Sau tiếp khách → Lưu trữ ảnh đoàn khách → Scan và gán tên khuôn mặt.
 *
 * Flow: ảnh đã upload vào visit_photos → Google Cloud Vision FACE_DETECTION (qua backend) →
 * hiển thị khung mặt (bounding box normalized 0..1) → Staff chọn thủ công khách thuộc đúng visit
 * instance hoặc bỏ qua → xác nhận (batch) → photo_face_tags + lịch sử quét.
 * Backend là nguồn quyền/dữ liệu duy nhất — không có mock/fallback giả khi API lỗi.
 */
import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { motion } from 'motion/react';
import {
  Image as ImageIcon, Sparkles, User, Tag, CheckCircle2, Search, Check,
  Minimize2, Loader2, AlertCircle, Upload,
} from 'lucide-react';
import { visitPhotosApi } from '../api/visitPhotosApi';
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
}

interface FaceScanPanelProps {
  visitInstanceId: number;
  photos: FaceScanPhoto[];
  isReadOnly: boolean;
  onUploadClick: () => void;
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

export function FaceScanPanel({ visitInstanceId, photos, isReadOnly, onUploadClick }: FaceScanPanelProps) {
  const { t } = useTranslation('visitFaceScan');

  const [selectedPhotoId, setSelectedPhotoId] = useState<number | null>(photos[0]?.visitPhotoId ?? null);
  const [scans, setScans] = useState<VisitPhotoFaceScan[]>([]);
  const [loadingScans, setLoadingScans] = useState(false);
  const [scanning, setScanning] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [taggableGuests, setTaggableGuests] = useState<TaggableGuest[]>([]);
  const [guestsLoading, setGuestsLoading] = useState(false);
  const [activeFaceId, setActiveFaceId] = useState<number | null>(null);
  const [pendingActions, setPendingActions] = useState<Record<number, { guestMemberId: number | null; ignored: boolean }>>({});
  const [searchGuestKeyword, setSearchGuestKeyword] = useState('');

  const selectedPhoto = photos.find((p) => p.visitPhotoId === selectedPhotoId) ?? null;
  const currentScan = scans[0] ?? null; // scans are ordered newest-first by the backend

  const selectedImageUrl = useAuthenticatedImage(selectedPhoto?.url ?? null);

  // Keep selection valid as the real photo list changes (upload/remove).
  useEffect(() => {
    if (photos.length === 0) {
      if (selectedPhotoId !== null) setSelectedPhotoId(null);
      return;
    }
    if (!photos.some((p) => p.visitPhotoId === selectedPhotoId)) {
      setSelectedPhotoId(photos[0].visitPhotoId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [photos]);

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

  const allDetectedResolved = detections.length > 0 && detections
    .filter((d) => d.reviewStatus === 'DETECTED')
    .every((d) => pendingActions[d.faceDetectionId] !== undefined);
  const canConfirm = currentScan?.status === 'SUCCEEDED' && detections.length > 0 && allDetectedResolved;

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
          return {
            faceDetectionId: d.faceDetectionId,
            guestMemberId: action?.guestMemberId ?? null,
            ignored: !!action?.ignored,
          };
        });
      const result = await visitPhotosApi.confirmFaceTags(currentScan.faceScanId, currentScan.rowVersion, faces);
      setScans((prev) => prev.map((s) => (s.faceScanId === result.faceScanId ? result : s)));
      setPendingActions({});
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
    <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
      {/* Column left side: Album items list & upload control */}
      <div className="lg:col-span-1 space-y-4 flex flex-col justify-between animate-fadeIn">
        <div className="space-y-3">
          <div className="space-y-2 max-h-[300px] overflow-y-auto pr-2">
            {photos.length === 0 && (
              <p className="text-xs text-gray-400 font-medium px-1 py-3 text-center">{t('album.empty')}</p>
            )}
            {photos.map((photo) => (
              <div
                key={photo.visitPhotoId}
                onClick={() => setSelectedPhotoId(photo.visitPhotoId)}
                className={`flex items-center gap-3 p-2 rounded-xl border cursor-pointer group transition-all ${selectedPhotoId === photo.visitPhotoId ? 'bg-[#004c91]/5 border-[#004c91] ring-2 ring-[#004c91]/10' : 'bg-white border-gray-100 hover:border-gray-300'}`}
              >
                <div className="w-12 h-12 rounded-lg bg-gray-100 overflow-hidden shrink-0 border border-gray-200 relative">
                  <PhotoThumbnail url={photo.url} alt={photo.name} />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-xs font-bold text-gray-800 truncate" title={photo.name}>{photo.name}</p>
                </div>
              </div>
            ))}
          </div>
        </div>

        {!isReadOnly && (
          <div className="pt-2">
            <button
              type="button"
              onClick={onUploadClick}
              className="w-full flex items-center justify-center gap-2 px-4 py-3 bg-[#004c91] text-white hover:bg-[#003e79] rounded-xl font-bold text-sm shadow-md transition-all active:scale-[0.98]"
            >
              <Upload className="w-4 h-4" /> {t('album.uploadButton')}
            </button>
            <p className="text-[10px] text-gray-400 mt-1 text-center font-medium">{t('album.uploadHint')}</p>
          </div>
        )}
      </div>

      {/* Column right side: interactive preview + scan controls */}
      <div className="lg:col-span-3 border border-gray-200 rounded-2xl overflow-hidden bg-gray-50 flex flex-col min-h-[420px] relative">
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

            <div className="flex-1 flex items-center justify-center p-3 relative select-none overflow-hidden">
              <div className="relative max-w-full max-h-[380px] rounded-lg overflow-hidden border border-gray-300 bg-white">
                {selectedImageUrl ? (
                  <img
                    src={selectedImageUrl}
                    alt={selectedPhoto.name}
                    className={`max-w-full max-h-[360px] object-contain transition-all ${scanning ? 'brightness-50' : ''}`}
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
                  detections.map((detection) => {
                    const { name, ignored } = resolveDisplayName(detection);
                    const isHighlightedBySearch = searchGuestKeyword
                      ? (name ?? '').toLowerCase().includes(searchGuestKeyword.toLowerCase())
                      : true;
                    if (searchGuestKeyword && !isHighlightedBySearch) return null;

                    const canEdit = !isReadOnly && detection.reviewStatus === 'DETECTED' && currentScan.status === 'SUCCEEDED';
                    const availableGuests = taggableGuests.filter((g) =>
                      !resolvedGuestIds.has(g.guestMemberId) || pendingActions[detection.faceDetectionId]?.guestMemberId === g.guestMemberId);

                    return (
                      <div
                        key={detection.faceDetectionId}
                        style={{
                          left: `${detection.boundingBoxX * 100}%`,
                          top: `${detection.boundingBoxY * 100}%`,
                          width: `${detection.boundingBoxWidth * 100}%`,
                          height: `${detection.boundingBoxHeight * 100}%`,
                        }}
                        className={`absolute border-2 ${canEdit ? 'cursor-pointer' : 'cursor-default'} transition-all rounded-md group/box ${
                          ignored
                            ? 'border-gray-400 bg-gray-400/10'
                            : name
                              ? 'border-emerald-500 bg-emerald-500/10 hover:bg-emerald-500/20'
                              : 'border-orange-500 bg-orange-500/10 hover:bg-orange-600/30'
                        }`}
                        onClick={() => {
                          if (!canEdit) return;
                          setActiveFaceId((prev) => (prev === detection.faceDetectionId ? null : detection.faceDetectionId));
                        }}
                      >
                        <div className="absolute bottom-full left-1/2 -translate-x-1/2 mb-1.5 whitespace-nowrap z-20">
                          <span className={`px-2 py-0.5 rounded-md text-[9px] font-bold shadow-md text-white ${ignored ? 'bg-gray-500' : name ? 'bg-emerald-600' : 'bg-orange-600'}`}>
                            {ignored ? t('tag.ignored') : name || t('tag.unassigned')}
                          </span>
                        </div>

                        {activeFaceId === detection.faceDetectionId && (
                          <div
                            className="absolute top-full left-1/2 -translate-x-1/2 mt-2 bg-white border border-gray-200 p-3 rounded-2xl shadow-xl z-40 w-56 text-left space-y-2.5"
                            onClick={(e) => e.stopPropagation()}
                          >
                            <div className="flex items-center justify-between border-b border-gray-100 pb-1.5">
                              <span className="text-[10px] uppercase font-bold text-gray-500">{t('tag.dropdownTitle')}</span>
                              <button onClick={() => setActiveFaceId(null)} className="text-gray-400 hover:text-gray-600 p-0.5 hover:bg-gray-100 rounded-full">
                                <Minimize2 className="w-3 h-3" />
                              </button>
                            </div>

                            <div className="space-y-1 max-h-[180px] overflow-y-auto pr-1">
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
                <div className="flex flex-wrap gap-2">
                  {detections.map((d, idx) => {
                    const { name, ignored } = resolveDisplayName(d);
                    return (
                      <div
                        key={d.faceDetectionId}
                        className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-xl text-xs font-bold border transition-colors ${name ? 'bg-emerald-50 text-emerald-800 border-emerald-200' : ignored ? 'bg-gray-100 text-gray-500 border-gray-200' : 'bg-orange-50 text-orange-700 border-orange-200'}`}
                      >
                        <User className="w-3.5 h-3.5" />
                        <span>
                          {t('tag.facePosition', { index: idx + 1 })}: {name || (ignored ? t('tag.ignored') : t('tag.unassigned'))}
                        </span>
                      </div>
                    );
                  })}
                </div>
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
          <div className="flex-1 flex flex-col items-center justify-center text-gray-400 p-8">
            <ImageIcon className="w-12 h-12 text-gray-300 mb-2" />
            <p className="text-sm font-medium">{t('album.selectPrompt')}</p>
          </div>
        )}
      </div>
    </div>
  );
}
