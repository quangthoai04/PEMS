import React, { useState, useEffect } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { motion } from 'motion/react';
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';
import { UploadCloud, Plus, Trash2, ArrowLeft, ImagePlus, X } from 'lucide-react';
import toast from 'react-hot-toast';
import httpClient from '../../../shared/api/httpClient';
import { useAuthenticatedImage } from '../../../shared/hooks/useAuthenticatedImage';
import { uploadFileToEndpoint } from '../../../shared/api/fileUploadApi';
import { validateFile } from '../../../shared/utils/fileValidation';

// ─── Types ───────────────────────────────────────────────────────────────────

interface SectionImage {
  fileId: number | null;        // id trong bảng files (null = chưa upload xong / ảnh legacy)
  previewUrl: string;           // blob:/data: URL hoặc /api/files/{id}/content
  uploading: boolean;
  legacyBase64?: string;        // ảnh base64 của bài cũ — sẽ được migrate lên Drive khi lưu
}

interface Section {
  id: number;
  sectionTitle: string;
  sectionBodyHtml: string;      // text-only, no <img> tags
  sectionImage: SectionImage | null;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

// Extract the first <img> src from an HTML string (legacy posts embedded base64 images)
function extractFirstImgSrc(html: string): string | null {
  const m = /<img[^>]+src=["']([^"']+)["']/i.exec(html);
  return m ? m[1] : null;
}

// Strip all <img> tags (and the empty <p> wrappers they leave behind)
function stripImgTags(html: string): string {
  return html
    .replace(/<img[^>]*\/?>/gi, '')
    .replace(/<p>(\s|&nbsp;)*<\/p>/gi, '')
    .trim();
}

// Convert a legacy data: URL into a File so it can be migrated onto Google Drive.
async function dataUrlToFile(dataUrl: string, fileName: string): Promise<File> {
  const res = await fetch(dataUrl);
  const blob = await res.blob();
  return new File([blob], fileName, { type: blob.type || 'image/png' });
}

// Preview that works for blob:/data: URLs (plain <img>) and backend /api/files
// URLs (need the Authorization header → authenticated blob fetch).
function SectionImagePreview({ image, alt }: { image: SectionImage; alt: string }) {
  const isBackendUrl = image.previewUrl.startsWith('/');
  const authSrc = useAuthenticatedImage(isBackendUrl ? image.previewUrl : null);
  const src = isBackendUrl ? authSrc : image.previewUrl;
  if (!src) return <div className="w-full h-40 bg-gray-100 animate-pulse rounded-lg" />;
  return <img src={src} alt={alt} className="w-full h-auto object-contain rounded-lg" />;
}

// ─── Quill toolbar (no image button — images managed separately) ───────────────

const QUILL_MODULES = {
  toolbar: [
    ['bold', 'italic', 'underline', 'strike'],
    [{ align: [] }],
    [{ list: 'ordered' }, { list: 'bullet' }],
    ['link'],
    ['clean'],
  ],
};

// ─── Component ────────────────────────────────────────────────────────────────

export function EditNews() {
  const navigate = useNavigate();
  const { id }   = useParams<{ id: string }>();
  const newsId   = Number(id);
  const [searchParams] = useSearchParams();
  // Bản dịch đang chỉnh sửa (mặc định bản gốc tiếng Việt)
  const languageCode = searchParams.get('lang') ?? 'vi';

  // ── Meta state ──
  const [loading,    setLoading]    = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [rowVersion, setRowVersion] = useState(0);
  const [newsStatus, setNewsStatus] = useState('');

  // ── Form fields ──
  const [title,    setTitle]    = useState('');
  const [summary,  setSummary]  = useState('');
  const [sections, setSections] = useState<Section[]>([
    { id: 1, sectionTitle: '', sectionBodyHtml: '', sectionImage: null },
  ]);

  // ── Cover image ──
  const [currentCoverFileId, setCurrentCoverFileId] = useState<number | null>(null);
  const [existingCoverUrl,   setExistingCoverUrl]   = useState<string | null>(null);
  const [coverPreviewUrl,    setCoverPreviewUrl]     = useState<string | null>(null);
  const [coverUploading,     setCoverUploading]      = useState(false);
  const existingCoverSrc = useAuthenticatedImage(existingCoverUrl);

  // ── Load news on mount ────────────────────────────────────────────────────

  useEffect(() => {
    if (!newsId) return;
    let cancelled = false;

    async function fetchNews() {
      setLoading(true);
      try {
        const { data } = await httpClient.get(`/news/${newsId}`, {
          params: languageCode !== 'vi' ? { languageCode } : undefined,
        });
        if (cancelled) return;

        setRowVersion(data.rowVersion ?? 0);
        setNewsStatus(data.status ?? '');
        setTitle(data.title ?? '');
        setSummary(data.summary ?? '');

        if (data.coverFile?.fileId) {
          setCurrentCoverFileId(data.coverFile.fileId);
          setExistingCoverUrl(data.coverFile.url ?? null);
        }

        if (Array.isArray(data.sections) && data.sections.length > 0) {
          setSections(
            data.sections.map(
              (
                s: {
                  sectionTitle?: string;
                  sectionBodyHtml?: string;
                  files?: { fileId: number; url?: string; usageType?: string }[];
                },
                i: number,
              ) => {
                const rawHtml = s.sectionBodyHtml ?? '';

                // Ưu tiên ảnh thật từ news_section_files; nếu bài cũ nhúng base64
                // trong HTML thì giữ lại để migrate lên Drive khi lưu.
                const inlineFile = (s.files ?? []).find(f => f.usageType === 'INLINE_IMAGE' && f.url);
                const legacySrc = inlineFile ? null : extractFirstImgSrc(rawHtml);

                let sectionImage: SectionImage | null = null;
                if (inlineFile) {
                  sectionImage = { fileId: inlineFile.fileId, previewUrl: inlineFile.url!, uploading: false };
                } else if (legacySrc) {
                  sectionImage = legacySrc.startsWith('data:')
                    ? { fileId: null, previewUrl: legacySrc, uploading: false, legacyBase64: legacySrc }
                    : { fileId: null, previewUrl: legacySrc, uploading: false };
                }

                return {
                  id:              i + 1,
                  sectionTitle:    s.sectionTitle ?? '',
                  sectionBodyHtml: stripImgTags(rawHtml),
                  sectionImage,
                };
              },
            ),
          );
        }
      } catch {
        if (!cancelled) {
          toast.error('Không thể tải thông tin bài viết.');
          navigate('/dashboard/news');
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    fetchNews();
    return () => { cancelled = true; };
  }, [newsId, navigate, languageCode]);

  // ── Cover upload ──────────────────────────────────────────────────────────

  async function handleCoverUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    e.target.value = '';

    const v = validateFile(file, 'NEWS_IMAGE');
    if (!v.ok) { toast.error(v.message ?? 'File không hợp lệ.'); return; }

    setCoverPreviewUrl(URL.createObjectURL(file));
    setCoverUploading(true);
    try {
      const result = await uploadFileToEndpoint('/news/cover-upload', 'file', file);
      setCurrentCoverFileId(result.fileId);
    } catch {
      toast.error('Tải ảnh bìa thất bại. Vui lòng thử lại.');
      setCoverPreviewUrl(null);
    } finally {
      setCoverUploading(false);
    }
  }

  // ── Section image — upload lên Google Drive ngay khi chọn (không lưu base64) ──

  async function handleSectionImagePick(index: number, e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    e.target.value = '';

    const v = validateFile(file, 'NEWS_IMAGE');
    if (!v.ok) { toast.error(v.message ?? 'File không hợp lệ.'); return; }

    const sectionId = sections[index]?.id;
    const previewUrl = URL.createObjectURL(file);
    setSections(prev => prev.map(s =>
      s.id === sectionId ? { ...s, sectionImage: { fileId: null, previewUrl, uploading: true } } : s
    ));

    try {
      const uploaded = await uploadFileToEndpoint('/news/section-file-upload', 'file', file);
      setSections(prev => prev.map(s =>
        s.id === sectionId && s.sectionImage
          ? { ...s, sectionImage: { fileId: uploaded.fileId, previewUrl: s.sectionImage.previewUrl, uploading: false } }
          : s
      ));
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? 'Không thể tải ảnh nội dung lên.');
      setSections(prev => prev.map(s =>
        s.id === sectionId ? { ...s, sectionImage: null } : s
      ));
    }
  }

  function removeSectionImage(index: number) {
    setSections(prev => {
      const next = [...prev];
      next[index] = { ...next[index], sectionImage: null };
      return next;
    });
  }

  // ── Section CRUD ──────────────────────────────────────────────────────────

  function addSection() {
    if (sections.length >= 10) return;
    setSections(prev => [
      ...prev,
      { id: Date.now(), sectionTitle: '', sectionBodyHtml: '', sectionImage: null },
    ]);
  }

  function removeSection(index: number) {
    setSections(prev => prev.filter((_, i) => i !== index));
  }

  function updateSection(index: number, field: 'sectionTitle' | 'sectionBodyHtml', value: string) {
    setSections(prev => {
      const next = [...prev];
      next[index] = { ...next[index], [field]: value };
      return next;
    });
  }

  // ── Submit ────────────────────────────────────────────────────────────────

  async function handleSubmit() {
    if (!title.trim())   { toast.error('Vui lòng nhập tiêu đề tin tức.'); return; }
    if (!summary.trim()) { toast.error('Vui lòng nhập mô tả ngắn.'); return; }
    if (coverUploading)  { toast.error('Vui lòng chờ ảnh bìa tải lên xong.'); return; }
    if (sections.some(s => !s.sectionTitle.trim())) {
      toast.error('Vui lòng nhập tiêu đề cho tất cả các mục nội dung.');
      return;
    }
    const emptyBody = sections.find(s => !s.sectionBodyHtml.replace(/<[^>]+>/g, '').trim());
    if (emptyBody) {
      toast.error(`Nội dung chi tiết mục ${sections.indexOf(emptyBody) + 1} không được để trống.`);
      return;
    }
    if (sections.some(s => s.sectionImage?.uploading)) {
      toast.error('Ảnh nội dung đang được tải lên, vui lòng chờ.');
      return;
    }

    setSubmitting(true);
    try {
      // Bài cũ còn ảnh base64 nhúng trong HTML → migrate lên Drive trước khi lưu
      // (backend từ chối payload chứa data:image).
      const migrated = [...sections];
      for (let i = 0; i < migrated.length; i++) {
        const img = migrated[i].sectionImage;
        if (img && img.fileId === null && img.legacyBase64) {
          const file = await dataUrlToFile(img.legacyBase64, `news-section-${newsId}-${i + 1}.png`);
          const uploaded = await uploadFileToEndpoint('/news/section-file-upload', 'file', file);
          migrated[i] = { ...migrated[i], sectionImage: { ...img, fileId: uploaded.fileId, legacyBase64: undefined } };
        }
      }
      const broken = migrated.find(s => s.sectionImage && s.sectionImage.fileId === null);
      if (broken) {
        toast.error(`Ảnh của mục ${migrated.indexOf(broken) + 1} chưa tải lên thành công. Vui lòng chọn lại ảnh.`);
        setSubmitting(false);
        return;
      }

      await httpClient.put(`/news/${newsId}`, {
        rowVersion,
        coverFileId: currentCoverFileId ?? null,
        title:       title.trim(),
        summary:     summary.trim(),
        languageCode,
        contentSections: migrated.map((s, i) => ({
          sectionOrder:    i + 1,
          sectionTitle:    s.sectionTitle.trim(),
          sectionBodyHtml: s.sectionBodyHtml,
          sectionFiles:    s.sectionImage?.fileId
            ? [{ fileId: s.sectionImage.fileId, usageType: 'INLINE_IMAGE', displayOrder: 1 }]
            : [],
        })),
      });

      toast.success(
        newsStatus === 'REJECTED'
          ? 'Bài viết đã được cập nhật và nộp lại chờ duyệt!'
          : 'Bài viết đã được cập nhật thành công!',
      );
      setTimeout(() => navigate('/dashboard/news'), 1500);
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message
        ?? 'Có lỗi xảy ra. Vui lòng thử lại.';
      toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  }

  // ── Loading state ─────────────────────────────────────────────────────────

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <div className="w-8 h-8 border-4 border-[#004c91] border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  const coverDisplaySrc = coverPreviewUrl ?? existingCoverSrc;

  // ─────────────────────────────────────────────────────────────────────────

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3 }}
      className="p-4 sm:p-6 md:p-8 pb-12 max-w-5xl mx-auto"
    >
      {/* Breadcrumb */}
      <div className="mb-6 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors">
          Dashboard
        </button>
        <span className="mx-2">/</span>
        <button onClick={() => navigate('/dashboard/news')} className="hover:text-[#004c91] transition-colors">
          Quản lý tin tức
        </button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Chỉnh sửa tin tức</span>
      </div>

      <div className="mb-8">
        <h1 className="text-3xl font-bold text-[#004c91]">
          Chỉnh sửa tin tức
          {languageCode !== 'vi' && (
            <span className="ml-3 align-middle text-sm font-bold px-3 py-1 rounded-full bg-[#eef5fa] text-[#004c91] border border-[#b6d4f0]">
              Bản dịch: {languageCode}
            </span>
          )}
        </h1>
        {newsStatus === 'REJECTED' && (
          <p className="mt-3 text-sm text-orange-700 bg-orange-50 border border-orange-200 rounded-xl px-4 py-2.5 inline-block">
            Bài viết đã bị từ chối. Sau khi chỉnh sửa, bài sẽ được nộp lại để Staff Leader duyệt.
          </p>
        )}
      </div>

      <div className="space-y-8">

        {/* ── 1. Thông tin cơ bản ── */}
        <section className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
          <div className="bg-[#004c91] px-6 py-3.5">
            <h2 className="text-[15px] font-bold text-white uppercase tracking-wide">1. Thông tin cơ bản</h2>
          </div>
          <div className="p-6 flex flex-col gap-6">

            <div>
              <label className="block text-gray-900 font-bold mb-2">
                Tiêu đề tin tức <span className="text-red-500">*</span>
              </label>
              <div className="relative">
                <input
                  type="text"
                  maxLength={150}
                  value={title}
                  onChange={e => setTitle(e.target.value)}
                  placeholder="Nhập tiêu đề tin tức..."
                  className="w-full pr-16 pl-4 py-3 border border-gray-300 rounded-xl focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] hover:border-[#004c91] transition-colors text-gray-800"
                />
                <span className="absolute right-4 top-1/2 -translate-y-1/2 text-sm text-gray-400 font-medium">
                  {title.length}/150
                </span>
              </div>
            </div>

            {/* Summary — no character limit */}
            <div>
              <label className="block text-gray-900 font-bold mb-2">
                Mô tả ngắn <span className="text-red-500">*</span>
              </label>
              <textarea
                rows={3}
                value={summary}
                onChange={e => setSummary(e.target.value)}
                placeholder="Nhập mô tả ngắn gọn..."
                className="w-full p-4 border border-gray-300 rounded-xl focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] hover:border-[#004c91] transition-colors text-gray-800 resize-none"
              />
            </div>

          </div>
        </section>

        {/* ── 2. Ảnh đại diện ── */}
        <section className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
          <div className="bg-[#004c91] px-6 py-3.5">
            <h2 className="text-[15px] font-bold text-white uppercase tracking-wide">2. Ảnh đại diện</h2>
          </div>
          <div className="p-6">
            <label className="block w-full cursor-pointer">
              <input
                type="file"
                accept="image/png,image/jpeg,image/jpg,image/webp"
                className="hidden"
                onChange={handleCoverUpload}
                disabled={coverUploading}
              />
              <div
                className={`relative bg-[#eef5fa] border-2 border-dashed border-[#b6d4f0] rounded-xl flex flex-col items-center justify-center text-center hover:bg-[#e4f0fa] transition-colors overflow-hidden ${
                  coverDisplaySrc ? 'p-2' : 'p-12 min-h-[240px]'
                }`}
              >
                {coverUploading && (
                  <div className="absolute inset-0 bg-white/70 flex items-center justify-center z-10 rounded-xl">
                    <div className="w-8 h-8 border-4 border-[#004c91] border-t-transparent rounded-full animate-spin" />
                  </div>
                )}
                {coverDisplaySrc ? (
                  <img
                    src={coverDisplaySrc}
                    alt="Ảnh đại diện"
                    className="w-full max-h-[360px] object-contain rounded-lg"
                  />
                ) : (
                  <>
                    <div className="w-16 h-16 bg-white rounded-full flex items-center justify-center mb-4 shadow-sm">
                      <UploadCloud className="w-8 h-8 text-[#004c91]" />
                    </div>
                    <h3 className="text-lg font-bold text-[#004c91] mb-1">Kéo thả ảnh vào đây</h3>
                    <p className="text-gray-500 text-sm">Hoặc click để tải ảnh lên (PNG, JPG, WEBP – tối đa 5MB)</p>
                  </>
                )}
              </div>
            </label>
            {coverDisplaySrc && !coverUploading && (
              <p className="mt-2 text-xs text-gray-400 text-center">Click vào ảnh để thay ảnh mới</p>
            )}
          </div>
        </section>

        {/* ── 3. Nội dung chi tiết ── */}
        <section className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
          <div className="bg-[#004c91] px-6 py-3.5">
            <h2 className="text-[15px] font-bold text-white uppercase tracking-wide">3. Nội dung chi tiết</h2>
          </div>
          <div className="p-6 flex flex-col gap-8">

            {sections.map((section, index) => (
              <div key={section.id}>
                {index > 0 && <div className="h-px bg-gray-100 w-full mb-8" />}
                <div className="flex flex-col gap-5">

                  {/* Section heading row */}
                  <div>
                    <div className="flex items-center justify-between mb-2">
                      <label className="block text-gray-900 font-bold">
                        Tiêu đề mục {index + 1} <span className="text-red-500">*</span>
                      </label>
                      <div className="flex items-center gap-2">
                        {index > 0 && (
                          <button
                            onClick={() => removeSection(index)}
                            className="flex items-center gap-1.5 bg-red-50 hover:bg-red-100 text-red-600 px-3 py-1.5 rounded-lg text-sm font-bold transition-colors"
                          >
                            <Trash2 className="w-4 h-4" /> Xóa mục
                          </button>
                        )}
                        {index === sections.length - 1 && sections.length < 10 && (
                          <button
                            onClick={addSection}
                            className="flex items-center gap-1.5 bg-[#004c91] hover:bg-[#003a70] text-white px-3 py-1.5 rounded-lg text-sm font-bold transition-colors shadow-sm"
                          >
                            <Plus className="w-4 h-4" /> Thêm mục
                          </button>
                        )}
                      </div>
                    </div>
                    <input
                      type="text"
                      value={section.sectionTitle}
                      onChange={e => updateSection(index, 'sectionTitle', e.target.value)}
                      placeholder="Nhập tiêu đề mục nội dung..."
                      className="w-full px-4 py-3 border border-gray-300 rounded-xl focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] hover:border-[#004c91] transition-colors text-gray-800"
                    />
                  </div>

                  {/* Text body — image button removed from Quill toolbar */}
                  <div>
                    <label className="block text-gray-900 font-bold mb-2">
                      Nội dung <span className="text-red-500">*</span>
                    </label>
                    <div className="border border-gray-300 rounded-lg overflow-hidden focus-within:border-[#004c91] focus-within:ring-1 focus-within:ring-[#004c91] transition-colors">
                      {/* @ts-ignore */}
                      <ReactQuill
                        key={`quill-${section.id}`}
                        theme="snow"
                        value={section.sectionBodyHtml}
                        onChange={value => updateSection(index, 'sectionBodyHtml', value)}
                        placeholder="Nhập nội dung chi tiết..."
                        className="bg-white"
                        modules={QUILL_MODULES}
                      />
                    </div>
                  </div>

                  {/* Section image — separate zone, max 1 per section */}
                  <div>
                    <label className="block text-gray-900 font-bold mb-2">Ảnh minh họa</label>

                    {section.sectionImage ? (
                      <div className="relative rounded-xl overflow-hidden border border-gray-200 bg-gray-50">
                        <SectionImagePreview image={section.sectionImage} alt={`Ảnh mục ${index + 1}`} />
                        {section.sectionImage.uploading && (
                          <div className="absolute inset-0 flex items-center justify-center bg-black/30 rounded-lg">
                            <div className="flex items-center gap-2 text-white font-bold text-sm">
                              <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                              Đang tải lên...
                            </div>
                          </div>
                        )}
                        {/* Remove button */}
                        <button
                          type="button"
                          onClick={() => removeSectionImage(index)}
                          className="absolute top-2 right-2 w-8 h-8 bg-red-500 hover:bg-red-600 text-white rounded-full flex items-center justify-center shadow-lg transition-colors"
                          title="Xóa ảnh"
                        >
                          <X className="w-4 h-4" />
                        </button>
                        {/* Replace button */}
                        <label className="absolute bottom-2 right-2 cursor-pointer">
                          <input
                            type="file"
                            accept="image/png,image/jpeg,image/jpg,image/webp"
                            className="hidden"
                            onChange={e => handleSectionImagePick(index, e)}
                          />
                          <span className="flex items-center gap-1.5 bg-white/90 hover:bg-white text-[#004c91] text-xs font-bold px-3 py-1.5 rounded-lg shadow transition-colors border border-gray-200 cursor-pointer">
                            <ImagePlus className="w-3.5 h-3.5" /> Thay ảnh
                          </span>
                        </label>
                      </div>
                    ) : (
                      <label className="block cursor-pointer">
                        <input
                          type="file"
                          accept="image/png,image/jpeg,image/jpg,image/webp"
                          className="hidden"
                          onChange={e => handleSectionImagePick(index, e)}
                        />
                        <div className="flex items-center gap-3 p-4 border-2 border-dashed border-gray-300 rounded-xl hover:border-[#004c91] hover:bg-[#eef5fa] transition-colors group cursor-pointer">
                          <ImagePlus className="w-5 h-5 text-gray-400 group-hover:text-[#004c91] shrink-0 transition-colors" />
                          <span className="text-sm text-gray-500 group-hover:text-[#004c91] font-medium transition-colors">
                            Thêm ảnh minh họa (tùy chọn — tối đa 1 ảnh mỗi mục)
                          </span>
                        </div>
                      </label>
                    )}
                  </div>

                </div>
              </div>
            ))}

            <div className="text-right text-sm text-gray-400 italic mt-2">
              {sections.length}/10 mục nội dung
            </div>
          </div>
        </section>

        {/* ── Buttons ── */}
        <div className="flex items-center justify-end gap-3 pt-6 border-t border-gray-200">
          <button
            onClick={() => navigate('/dashboard/news')}
            className="flex items-center gap-1.5 px-6 py-2.5 rounded-xl border border-gray-300 text-gray-700 font-bold hover:border-[#004c91] hover:text-[#004c91] transition-colors"
          >
            <ArrowLeft className="w-4 h-4" /> Quay lại
          </button>
          <button
            onClick={handleSubmit}
            disabled={submitting || coverUploading}
            className="flex items-center gap-2 px-8 py-2.5 text-white bg-[#f37021] rounded-xl font-bold hover:-translate-y-1 hover:shadow-lg transition-all duration-300 disabled:opacity-50 disabled:cursor-not-allowed disabled:transform-none"
          >
            {submitting && (
              <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
            )}
            {newsStatus === 'REJECTED' ? 'Lưu và nộp lại' : 'Lưu thay đổi'}
          </button>
        </div>

      </div>
    </motion.div>
  );
}
