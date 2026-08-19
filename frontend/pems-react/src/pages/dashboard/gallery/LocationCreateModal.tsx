/**
 * "Thêm khu vực mới" modal (UC-LOC-04/05) with VI → EN translation preview.
 * Two modes: attach the location to an existing area (only the location is translated) or create a
 * brand-new area + first location (ONE batched preview request for both names). The EN inputs are
 * editable; a previewed EN whose VI changed afterwards must be re-translated before saving.
 */

import React, { useEffect, useMemo, useRef, useState } from 'react';
import { motion } from 'motion/react';
import { ChevronDown, Loader2, X } from 'lucide-react';
import { galleryManagementApi } from '../../../features/gallery-management/api/galleryManagementApi';
import { getGalleryErrorMessage } from '../../../features/gallery-management/api/galleryError';
import { validateFile } from '../../../shared/utils/fileValidation';
import type {
  GalleryLocationDetail,
  GalleryLocationMode,
} from '../../../features/gallery-management/types/galleryManagement.types';
import type { AreaOption } from './locationModalShared';
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

export function LocationCreateModal({
  activeAreas,
  areasLoading,
  onClose,
  onSaved,
  onError,
}: {
  activeAreas: AreaOption[];
  areasLoading: boolean;
  onClose: () => void;
  onSaved: (res: GalleryLocationDetail) => void;
  onError: (message: string) => void;
}) {
  const [areaMode, setAreaMode] = useState<GalleryLocationMode>('EXISTING_AREA');
  const [areaId, setAreaId] = useState<number | ''>(activeAreas[0]?.areaId ?? '');
  const areaField = useBilingualField();
  const locationField = useBilingualField();
  const [areaCover, setAreaCover] = useState<File | null>(null);
  const [locationCover, setLocationCover] = useState<File | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [translating, setTranslating] = useState(false);
  // The EN inputs stay hidden until the user presses "Dịch sang EN" once — they then appear under
  // their VI counterparts (editable, incl. manual entry when the provider failed).
  const [showEnFields, setShowEnFields] = useState(false);

  // Newly created areas can appear while the modal is open (options upsert) — keep a valid selection.
  useEffect(() => {
    if (areaId === '' && activeAreas.length > 0) setAreaId(activeAreas[0].areaId);
  }, [activeAreas, areaId]);

  // Stale-response guard: only the latest preview request may write into the EN inputs.
  const previewRequestIdRef = useRef(0);

  const handleTranslate = async () => {
    if (translating) return; // double-click guard
    const locationVi = normalizeVi(locationField.vi);
    const areaVi = normalizeVi(areaField.vi);
    if (!locationVi) {
      onError('Vui lòng nhập vị trí cụ thể trước khi dịch.');
      return;
    }
    if (areaMode === 'NEW_AREA' && !areaVi) {
      onError('Vui lòng nhập tên khu vực/tòa trước khi dịch.');
      return;
    }

    // First translate click reveals the EN inputs (kept visible afterwards, even on provider
    // failure, so the Staff Leader can still type the EN by hand).
    setShowEnFields(true);

    const requestId = ++previewRequestIdRef.current;
    setTranslating(true);
    if (areaMode === 'NEW_AREA') areaField.beginTranslating();
    locationField.beginTranslating();
    try {
      const res = await galleryManagementApi.previewLocationTranslation(
        areaMode === 'NEW_AREA'
          ? { mode: 'NEW_AREA', areaNameVi: areaVi, locationNameVi: locationVi }
          : { mode: 'EXISTING_AREA', locationNameVi: locationVi },
      );
      if (requestId !== previewRequestIdRef.current) return; // an older response — never apply

      // Apply per field ONLY while the VI still matches what was translated (anti-stale, §11).
      if (areaMode === 'NEW_AREA' && res.area) {
        if (normalizeVi(areaField.vi) === res.area.sourceText) {
          areaField.applyPreview(res.area.translatedText, res.area.sourceHash);
        } else {
          areaField.markStale();
        }
      }
      if (res.location) {
        if (normalizeVi(locationField.vi) === res.location.sourceText) {
          locationField.applyPreview(res.location.translatedText, res.location.sourceHash);
        } else {
          locationField.markStale();
        }
      }
    } catch (err) {
      if (requestId !== previewRequestIdRef.current) return;
      if (areaMode === 'NEW_AREA') areaField.markFailed();
      locationField.markFailed();
      onError(getGalleryErrorMessage(err));
    } finally {
      if (requestId === previewRequestIdRef.current) setTranslating(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmedLocation = locationField.vi.trim();
    if (!trimmedLocation) {
      onError('Vui lòng nhập vị trí cụ thể.');
      return;
    }
    if (areaMode === 'EXISTING_AREA' && areaId === '') {
      onError('Vui lòng chọn khu vực/tòa.');
      return;
    }
    if (areaMode === 'NEW_AREA' && !areaField.vi.trim()) {
      onError('Vui lòng nhập tên khu vực/tòa mới.');
      return;
    }
    // A stale auto-preview must never be saved as READY (§15.1).
    const staleError =
      (areaMode === 'NEW_AREA' ? fieldBlockingError(areaField, 'Khu vực') : null)
      ?? fieldBlockingError(locationField, 'Vị trí');
    if (staleError) {
      onError(staleError);
      return;
    }
    // A new area always needs its own MP4 cover VIDEO.
    if (areaMode === 'NEW_AREA' && !areaCover) {
      onError('Vui lòng chọn một video đại diện cho khu vực.');
      return;
    }
    if (!locationCover) {
      onError('Vui lòng upload ảnh đại diện vị trí.');
      return;
    }
    // Client-side sanity checks (backend re-validates).
    if (!validateFile(locationCover, 'GALLERY_IMAGE').ok) {
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
      const res = await galleryManagementApi.createLocation({
        mode: areaMode,
        areaId: areaMode === 'EXISTING_AREA' ? Number(areaId) : null,
        newAreaName: areaMode === 'NEW_AREA' ? areaField.vi.trim() : null,
        newAreaNameEn: areaMode === 'NEW_AREA' ? areaPayload.en : null,
        areaTranslationOrigin: areaMode === 'NEW_AREA' ? areaPayload.origin : undefined,
        areaTranslationSourceHash: areaMode === 'NEW_AREA' ? areaPayload.sourceHash : null,
        locationName: trimmedLocation,
        locationNameEn: locationPayload.en,
        locationTranslationOrigin: locationPayload.origin,
        locationTranslationSourceHash: locationPayload.sourceHash,
        areaCoverVideo: areaMode === 'NEW_AREA' ? areaCover : null,
        locationCoverImage: locationCover,
      });
      onSaved(res);
    } catch (err) {
      onError(getGalleryErrorMessage(err));
    } finally {
      setSubmitting(false);
    }
  };

  const canTranslate = areaMode === 'NEW_AREA'
    ? !!(areaField.vi.trim() && locationField.vi.trim())
    : !!locationField.vi.trim();

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 font-sans">
      <motion.div
        initial={{ opacity: 0, scale: 0.95 }}
        animate={{ opacity: 1, scale: 1 }}
        exit={{ opacity: 0, scale: 0.95 }}
        className="bg-white rounded-3xl w-full max-w-lg overflow-hidden shadow-2xl relative max-h-[92dvh] flex flex-col"
      >
        <div className="px-6 py-4 border-b border-slate-100 flex items-center justify-between bg-slate-50 shrink-0">
          <h3 className="text-xl font-black text-[#004c91]">Thêm khu vực mới</h3>
          <button onClick={onClose} className="w-8 h-8 bg-white border border-slate-200 text-slate-500 rounded-full flex items-center justify-center hover:text-red-500 outline-none">
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="p-6 overflow-y-auto">
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-3">
              <label className="text-xs font-bold text-slate-500 uppercase tracking-wide border-b border-slate-100 pb-2 block">Tên Khu Vực / Tòa <span className="text-red-500">*</span></label>
              <div className="flex items-center gap-4">
                <label className="flex items-center gap-2 cursor-pointer text-sm font-medium text-slate-700">
                  <input
                    type="radio"
                    name="areaMode"
                    value="EXISTING_AREA"
                    checked={areaMode === 'EXISTING_AREA'}
                    // Keep the EN inputs visible if a translation already exists — an EN must never
                    // be saved while hidden from the user.
                    onChange={() => { setAreaMode('EXISTING_AREA'); setShowEnFields(!!locationField.en.trim()); }}
                    className="w-4 h-4 text-[#004c91] border-gray-300 focus:ring-[#004c91]"
                  />
                  Khu vực có sẵn
                </label>
                <label className="flex items-center gap-2 cursor-pointer text-sm font-medium text-slate-700">
                  <input
                    type="radio"
                    name="areaMode"
                    value="NEW_AREA"
                    checked={areaMode === 'NEW_AREA'}
                    onChange={() => { setAreaMode('NEW_AREA'); setShowEnFields(!!(locationField.en.trim() || areaField.en.trim())); }}
                    className="w-4 h-4 text-[#004c91] border-gray-300 focus:ring-[#004c91]"
                  />
                  Khu vực mới
                </label>
              </div>

              {areaMode === 'EXISTING_AREA' ? (
                <div className="relative">
                  <select
                    value={areaId === '' ? '' : String(areaId)}
                    onChange={(e) => setAreaId(e.target.value === '' ? '' : Number(e.target.value))}
                    disabled={areasLoading || activeAreas.length === 0}
                    className="w-full px-4 py-2.5 pr-10 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-normal appearance-none bg-white disabled:bg-slate-50 disabled:text-slate-400"
                  >
                    {activeAreas.length === 0 ? (
                      <option value="">Chưa có khu vực hoạt động</option>
                    ) : (
                      activeAreas.map((a) => (
                        <option key={a.areaId} value={a.areaId}>{a.areaName}</option>
                      ))
                    )}
                  </select>
                  <ChevronDown className="w-4 h-4 text-slate-400 absolute right-4 top-1/2 -translate-y-1/2 pointer-events-none" />
                </div>
              ) : (
                <>
                  <input
                    type="text"
                    value={areaField.vi}
                    onChange={(e) => areaField.setVi(e.target.value)}
                    className={inputClass}
                    placeholder="Nhập tên khu vực mới (VD: TÒA DELTA)"
                  />
                  {/* Hidden until the first "Dịch sang EN" click — then shown right under the VI input. */}
                  {showEnFields && (
                    <div className="space-y-1">
                      <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wide">Tên khu vực / tòa (EN)</label>
                      <input
                        type="text"
                        value={areaField.en}
                        onChange={(e) => areaField.setEnManual(e.target.value)}
                        className={inputClass}
                        placeholder="VD: DELTA BUILDING"
                      />
                      <EnFieldHint field={areaField} />
                    </div>
                  )}
                  <CoverVideoField
                    label="Video đại diện khu vực"
                    required
                    file={areaCover}
                    onPick={setAreaCover}
                    hint="Chỉ chấp nhận MP4, tối đa 100 MB và tối đa 120 giây (2 phút). Khuyến nghị video ngang, H.264, Full HD hoặc thấp hơn."
                  />
                </>
              )}
            </div>

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

            {/* Hidden until the first "Dịch sang EN" click — then shown right under the VI input. */}
            {showEnFields && (
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
            )}

            <div className="space-y-1">
              <TranslateButton
                onClick={handleTranslate}
                disabled={!canTranslate}
                translating={translating}
                label={areaMode === 'NEW_AREA' ? 'Dịch khu vực + vị trí sang EN' : 'Dịch sang EN'}
              />
              {!showEnFields && (
                <p className="text-[11px] text-slate-400">
                  Nhập tên tiếng Việt rồi bấm dịch — bản tiếng Anh sẽ hiện bên dưới để bạn xem và chỉnh sửa trước khi lưu.
                </p>
              )}
            </div>

            <CoverImageField
              label="Ảnh đại diện vị trí"
              required
              file={locationCover}
              onPick={setLocationCover}
              hint="Ảnh mặt trước/không gian vị trí — chỉ 1 ảnh (JPG/PNG/WEBP ≤5MB)."
            />

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
                Tạo mới
              </button>
            </div>
          </form>
        </div>
      </motion.div>
    </div>
  );
}

export default LocationCreateModal;
