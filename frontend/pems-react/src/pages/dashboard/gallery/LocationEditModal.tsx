/**
 * "Chỉnh sửa khu vực và vị trí" modal — DIRECT edit of the current area + location (no existing/new
 * radio, no move, no new area; both ids stay unchanged). Loads the authoritative detail first, prefills
 * the bilingual names + covers, and offers a "Dịch sang EN" preview that only translates the fields
 * whose Vietnamese name changed (or whose EN is missing). Covers are optional — kept when not replaced.
 */

import React, { useEffect, useRef, useState } from 'react';
import { motion } from 'motion/react';
import { AlertCircle, Loader2, X } from 'lucide-react';
import { galleryManagementApi } from '../../../features/gallery-management/api/galleryManagementApi';
import {
  getGalleryErrorCode,
  getGalleryErrorMessage,
} from '../../../features/gallery-management/api/galleryError';
import { validateFile } from '../../../shared/utils/fileValidation';
import type {
  GalleryLocationDetail,
  GalleryLocationEditDetail,
} from '../../../features/gallery-management/types/galleryManagement.types';
import {
  CoverImageField,
  CoverVideoField,
  EnFieldHint,
  TranslateButton,
  fieldBlockingError,
  fieldPayload,
  normalizeVi,
  useBilingualField,
  validateAreaVideo,
} from './locationModalShared';

const inputClass =
  'w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium';

export function LocationEditModal({
  locationId,
  onClose,
  onSaved,
  onError,
}: {
  locationId: number;
  onClose: () => void;
  onSaved: (res: GalleryLocationDetail) => void;
  onError: (message: string) => void;
}) {
  const [detail, setDetail] = useState<GalleryLocationEditDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const areaField = useBilingualField();
  const locationField = useBilingualField();
  const [areaCover, setAreaCover] = useState<File | null>(null);
  const [locationCover, setLocationCover] = useState<File | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [translating, setTranslating] = useState(false);

  const previewRequestIdRef = useRef(0);

  // Authoritative detail load — never trust the (possibly EN-less) list row (§19).
  const loadDetail = async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const d = await galleryManagementApi.getLocationDetail(locationId);
      setDetail(d);
      areaField.reset(d.areaName, d.areaNameEn ?? '');
      locationField.reset(d.locationName, d.locationNameEn ?? '');
    } catch (err) {
      setLoadError(getGalleryErrorMessage(err, 'Không tải được thông tin vị trí. Vui lòng thử lại.'));
    } finally {
      setLoading(false);
    }
  };
  useEffect(() => {
    void loadDetail();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [locationId]);

  const areaViChanged = detail
    ? normalizeVi(areaField.vi) !== normalizeVi(detail.areaName)
    : false;
  const locationViChanged = detail
    ? normalizeVi(locationField.vi) !== normalizeVi(detail.locationName)
    : false;

  // Only translate what needs it: a changed VI or a missing EN (§20).
  const includeArea = areaViChanged || !areaField.en.trim();
  const includeLocation = locationViChanged || !locationField.en.trim();

  const handleTranslate = async () => {
    if (translating || !detail) return; // double-click guard
    const areaVi = normalizeVi(areaField.vi);
    const locationVi = normalizeVi(locationField.vi);
    if (includeArea && !areaVi) {
      onError('Vui lòng nhập tên khu vực/tòa trước khi dịch.');
      return;
    }
    if (includeLocation && !locationVi) {
      onError('Vui lòng nhập vị trí cụ thể trước khi dịch.');
      return;
    }
    if (!includeArea && !includeLocation) {
      onError('Không có nội dung nào cần dịch.');
      return;
    }

    const requestId = ++previewRequestIdRef.current;
    setTranslating(true);
    if (includeArea) areaField.beginTranslating();
    if (includeLocation) locationField.beginTranslating();
    try {
      const res = await galleryManagementApi.previewLocationTranslation({
        mode: 'EDIT',
        areaNameVi: includeArea ? areaVi : null,
        locationNameVi: includeLocation ? locationVi : null,
        includeArea,
        includeLocation,
      });
      if (requestId !== previewRequestIdRef.current) return; // an older response — never apply

      if (includeArea && res.area) {
        if (normalizeVi(areaField.vi) === res.area.sourceText) {
          areaField.applyPreview(res.area.translatedText, res.area.sourceHash);
        } else {
          areaField.markStale();
        }
      }
      if (includeLocation && res.location) {
        if (normalizeVi(locationField.vi) === res.location.sourceText) {
          locationField.applyPreview(res.location.translatedText, res.location.sourceHash);
        } else {
          locationField.markStale();
        }
      }
    } catch (err) {
      if (requestId !== previewRequestIdRef.current) return;
      if (includeArea) areaField.markFailed();
      if (includeLocation) locationField.markFailed();
      onError(getGalleryErrorMessage(err));
    } finally {
      if (requestId === previewRequestIdRef.current) setTranslating(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!detail) return;
    const trimmedArea = areaField.vi.trim();
    const trimmedLocation = locationField.vi.trim();
    if (!trimmedArea) {
      onError('Vui lòng nhập tên khu vực/tòa.');
      return;
    }
    if (!trimmedLocation) {
      onError('Vui lòng nhập vị trí cụ thể.');
      return;
    }
    // A stale auto-preview must never be saved as READY (§15.1).
    const staleError = fieldBlockingError(areaField, 'Khu vực')
      ?? fieldBlockingError(locationField, 'Vị trí');
    if (staleError) {
      onError(staleError);
      return;
    }
    // Covers are optional on edit — validate only when replaced.
    if (locationCover && !validateFile(locationCover, 'GALLERY_IMAGE').ok) {
      onError('Ảnh đại diện vị trí không đúng định dạng.');
      return;
    }
    if (areaCover) {
      const videoError = await validateAreaVideo(areaCover);
      if (videoError) {
        onError(videoError);
        return;
      }
    }

    const areaPayload = fieldPayload(areaField);
    const locationPayload = fieldPayload(locationField);

    setSubmitting(true);
    try {
      const res = await galleryManagementApi.updateLocation({
        locationId,
        areaName: trimmedArea,
        areaNameEn: areaPayload.en,
        areaTranslationOrigin: areaPayload.origin,
        areaTranslationSourceHash: areaPayload.sourceHash,
        locationName: trimmedLocation,
        locationNameEn: locationPayload.en,
        locationTranslationOrigin: locationPayload.origin,
        locationTranslationSourceHash: locationPayload.sourceHash,
        areaCoverVideo: areaCover,
        locationCoverImage: locationCover,
      });
      onSaved(res);
    } catch (err) {
      // Backend detected a stale preview — keep the modal open and ask for a re-translate (§32).
      if (getGalleryErrorCode(err) === 'GALLERY_TRANSLATION_PREVIEW_STALE') {
        if (areaField.origin === 'AUTO_PREVIEW') areaField.markStale();
        if (locationField.origin === 'AUTO_PREVIEW') locationField.markStale();
      }
      onError(getGalleryErrorMessage(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 font-sans">
      <motion.div
        initial={{ opacity: 0, scale: 0.95 }}
        animate={{ opacity: 1, scale: 1 }}
        exit={{ opacity: 0, scale: 0.95 }}
        className="bg-white rounded-3xl w-full max-w-lg overflow-hidden shadow-2xl relative max-h-[92vh] flex flex-col"
      >
        <div className="px-6 py-4 border-b border-slate-100 flex items-center justify-between bg-slate-50 shrink-0">
          <h3 className="text-xl font-black text-[#004c91]">Chỉnh sửa khu vực và vị trí</h3>
          <button onClick={onClose} className="w-8 h-8 bg-white border border-slate-200 text-slate-500 rounded-full flex items-center justify-center hover:text-red-500 outline-none">
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="p-6 overflow-y-auto">
          {loading ? (
            <div className="py-14 text-center text-slate-500">
              <Loader2 className="w-8 h-8 text-[#004c91] mx-auto mb-3 animate-spin" />
              <p className="font-medium text-slate-600">Đang tải thông tin vị trí...</p>
            </div>
          ) : loadError ? (
            <div className="py-14 text-center text-red-500">
              <AlertCircle className="w-10 h-10 mx-auto mb-3" />
              <p className="font-semibold mb-3">{loadError}</p>
              <button onClick={() => void loadDetail()} className="px-4 py-2 rounded-lg bg-[#004c91] text-white text-sm font-bold outline-none">
                Thử lại
              </button>
            </div>
          ) : detail && (
            <form onSubmit={handleSubmit} className="space-y-4">
              <div className="space-y-1.5">
                <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Tên khu vực / tòa (VI) <span className="text-red-500">*</span></label>
                <input
                  type="text"
                  value={areaField.vi}
                  onChange={(e) => areaField.setVi(e.target.value)}
                  className={inputClass}
                  placeholder="VD: TÒA DELTA"
                />
              </div>

              <div className="space-y-1">
                <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Tên khu vực / tòa (EN)</label>
                <input
                  type="text"
                  value={areaField.en}
                  onChange={(e) => areaField.setEnManual(e.target.value)}
                  className={inputClass}
                  placeholder="VD: DELTA BUILDING"
                />
                <EnFieldHint field={areaField} />
              </div>

              <CoverVideoField
                label="Video đại diện khu vực"
                file={areaCover}
                onPick={setAreaCover}
                existingUrl={detail.areaCoverUrl}
                existingMediaType={detail.areaCoverMediaType ?? 'IMAGE'}
                hint="Để trống nếu muốn giữ video/ảnh khu vực hiện tại. Chỉ MP4, tối đa 100 MB và 120 giây (2 phút)."
              />

              <p className="text-[11px] text-amber-600 bg-amber-50 border border-amber-100 rounded-lg px-3 py-2 font-medium">
                Thay đổi tên hoặc video đại diện khu vực sẽ áp dụng cho tất cả vị trí thuộc khu vực này.
              </p>

              <div className="space-y-1.5">
                <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Vị trí cụ thể (VI) <span className="text-red-500">*</span></label>
                <input
                  type="text"
                  value={locationField.vi}
                  onChange={(e) => locationField.setVi(e.target.value)}
                  className={inputClass}
                  placeholder="VD: Sảnh chính"
                />
              </div>

              <div className="space-y-1">
                <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Vị trí cụ thể (EN)</label>
                <input
                  type="text"
                  value={locationField.en}
                  onChange={(e) => locationField.setEnManual(e.target.value)}
                  className={inputClass}
                  placeholder="VD: Main Lobby"
                />
                <EnFieldHint field={locationField} />
              </div>

              <CoverImageField
                label="Ảnh đại diện vị trí"
                file={locationCover}
                onPick={setLocationCover}
                existingUrl={detail.locationCoverUrl}
                hint="Để trống nếu muốn giữ ảnh hiện tại. Chỉ 1 ảnh (JPG/PNG/WEBP ≤5MB)."
              />

              <div>
                <TranslateButton
                  onClick={handleTranslate}
                  disabled={!includeArea && !includeLocation}
                  translating={translating}
                />
              </div>

              <div className="flex justify-end gap-3 pt-4 border-t border-slate-100 mt-4">
                <button type="button" onClick={onClose} className="px-5 py-2.5 rounded-xl text-sm font-bold text-slate-600 bg-slate-100 hover:bg-slate-200 outline-none">
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={submitting || translating}
                  className="px-5 py-2.5 rounded-xl text-sm font-bold text-white bg-[#f37021] hover:bg-[#e85c0d] disabled:opacity-60 flex items-center gap-2 outline-none"
                >
                  {submitting && <Loader2 className="w-4 h-4 animate-spin" />}
                  Cập nhật
                </button>
              </div>
            </form>
          )}
        </div>
      </motion.div>
    </div>
  );
}

export default LocationEditModal;
