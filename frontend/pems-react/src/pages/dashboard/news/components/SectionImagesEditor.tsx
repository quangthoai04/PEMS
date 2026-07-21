import React from 'react';
import { ImagePlus, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { uploadFileToEndpoint } from '../../../../shared/api/fileUploadApi';
import { validateFile } from '../../../../shared/utils/fileValidation';
import { useAuthenticatedImage } from '../../../../shared/hooks/useAuthenticatedImage';

export const MAX_SECTION_FILES = 10;

export interface SectionImageItem {
  fileId: number | null; // set after the Drive upload succeeds
  previewUrl: string;    // local object URL / backend URL for preview
  uploading: boolean;
}

interface SectionImagesEditorProps {
  images: SectionImageItem[];
  /** Functional updater (like React's setState) — required so concurrent uploads never clobber
   * each other's placeholder by working off a stale snapshot of the array. */
  onChange: (updater: (prev: SectionImageItem[]) => SectionImageItem[]) => void;
  uploadEndpoint: string;
  disabled?: boolean;
}

/**
 * Multi-file (up to MAX_SECTION_FILES) image picker for one News content section — replaces the
 * previous single-image slot. Uploads each file to Google Drive immediately on selection (same
 * pipeline as before, just looped), shows a grid of previews, and lets the user remove any one
 * of them individually. Shared by CreateNews.tsx and EditNews.tsx so both forms behave identically.
 */
function SectionImageThumb({ src, alt }: { src: string; alt: string }) {
  const isLocal = src.startsWith('blob:') || src.startsWith('data:');
  const authSrc = useAuthenticatedImage(isLocal ? null : src);
  const resolved = isLocal ? src : authSrc;
  if (!resolved) return <div className="w-full h-full bg-gray-100 animate-pulse" />;
  return <img src={resolved} alt={alt} className="w-full h-full object-cover" />;
}

export function SectionImagesEditor({ images, onChange, uploadEndpoint, disabled }: SectionImagesEditorProps) {
  const remainingSlots = MAX_SECTION_FILES - images.length;

  async function handlePick(e: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(e.target.files ?? []);
    e.target.value = '';
    if (files.length === 0) return;

    if (files.length > remainingSlots) {
      toast.error(`Chỉ có thể thêm tối đa ${MAX_SECTION_FILES} ảnh/video mỗi mục (còn lại ${remainingSlots} chỗ trống).`);
    }
    const accepted = files.slice(0, Math.max(0, remainingSlots));

    // Upload every accepted file in parallel, each tracked by its own placeholder object
    // identity — every onChange call is a functional update over the latest array, so
    // concurrent uploads (or a slow one finishing after a fast one) never clobber each other.
    await Promise.all(accepted.map(async file => {
      const check = validateFile(file, 'NEWS_IMAGE');
      if (!check.ok) {
        toast.error(check.message ?? 'File không hợp lệ.');
        return;
      }

      const previewUrl = URL.createObjectURL(file);
      const placeholder: SectionImageItem = { fileId: null, previewUrl, uploading: true };
      onChange(prev => [...prev, placeholder]);

      try {
        const uploaded = await uploadFileToEndpoint(uploadEndpoint, 'file', file);
        onChange(prev => prev.map(img => img === placeholder ? { ...img, fileId: uploaded.fileId, uploading: false } : img));
      } catch (err: any) {
        toast.error(err?.response?.data?.message ?? 'Không thể tải ảnh lên.');
        onChange(prev => prev.filter(img => img !== placeholder));
      }
    }));
  }

  function removeAt(index: number) {
    onChange(prev => prev.filter((_, i) => i !== index));
  }

  // previewUrl is either a local blob:/data: URL (just picked, safe to render directly) or a
  // backend `/api/files/{id}/content` path (loaded from a saved post) — the latter requires the
  // Authorization header, which a plain <img src> cannot send, so it needs the authenticated
  // fetch hook instead (same pattern as NewsDetailDashboard's AuthenticatedImage).

  return (
    <div>
      <label className="block text-gray-900 font-bold mb-2">
        Hình ảnh <span className="text-xs font-normal text-gray-400">(tối đa {MAX_SECTION_FILES} ảnh mỗi mục)</span>
      </label>

      {images.length > 0 && (
        <div className="grid grid-cols-3 sm:grid-cols-4 md:grid-cols-5 gap-3 mb-3">
          {images.map((img, index) => (
            <div key={index} className="relative rounded-xl overflow-hidden border border-gray-200 bg-gray-50 aspect-square">
              <SectionImageThumb src={img.previewUrl} alt={`Ảnh ${index + 1}`} />
              {img.uploading && (
                <div className="absolute inset-0 flex items-center justify-center bg-black/30">
                  <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                </div>
              )}
              {!disabled && (
                <button
                  type="button"
                  onClick={() => removeAt(index)}
                  className="absolute top-1.5 right-1.5 w-6 h-6 bg-red-500 hover:bg-red-600 text-white rounded-full flex items-center justify-center shadow-lg transition-colors"
                  title="Xóa ảnh"
                >
                  <X className="w-3.5 h-3.5" />
                </button>
              )}
            </div>
          ))}
        </div>
      )}

      {!disabled && remainingSlots > 0 && (
        <label className="block cursor-pointer">
          <input
            type="file"
            accept="image/png,image/jpeg,image/jpg,image/webp"
            multiple
            className="hidden"
            onChange={handlePick}
          />
          <div className="flex items-center gap-3 p-4 border-2 border-dashed border-gray-300 rounded-xl hover:border-[#004c91] hover:bg-[#eef5fa] transition-colors group cursor-pointer">
            <ImagePlus className="w-5 h-5 text-gray-400 group-hover:text-[#004c91] shrink-0 transition-colors" />
            <span className="text-sm text-gray-500 group-hover:text-[#004c91] font-medium transition-colors">
              Thêm hình ảnh ({images.length}/{MAX_SECTION_FILES})
            </span>
          </div>
        </label>
      )}
    </div>
  );
}
