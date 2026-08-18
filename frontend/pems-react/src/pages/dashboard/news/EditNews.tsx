import React, { useState, useEffect } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { motion } from 'motion/react';
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';
import { UploadCloud, Plus, Trash2, ArrowLeft, Languages, RotateCw, FolderOpen } from 'lucide-react';
import toast from 'react-hot-toast';
import httpClient from '../../../shared/api/httpClient';
import { useAuthenticatedImage } from '../../../shared/hooks/useAuthenticatedImage';
import { uploadFileToEndpoint } from '../../../shared/api/fileUploadApi';
import { validateFile } from '../../../shared/utils/fileValidation';
import { SectionImagesEditor, type SectionImageItem } from './components/SectionImagesEditor';
import { BilingualColumns, LanguageColumnLabel } from './components/BilingualColumns';
import { useBilingualTranslate } from './components/useBilingualTranslate';
import { CollapsibleSection } from './components/CollapsibleSection';
import { AutoGrowInput, AutoGrowTextarea } from './components/AutoGrowInput';
import { VisitInstancePhotoPicker } from './components/VisitInstancePhotoPicker';

// ─── Types ───────────────────────────────────────────────────────────────────

interface Section {
  id: number;
  sectionTitle: string;
  sectionBodyHtml: string;      // text-only, no <img> tags
  sectionImages: SectionImageItem[];
  legacyBase64?: string;        // ảnh base64 duy nhất của bài cũ — sẽ được migrate lên Drive khi lưu
  englishSectionTitle: string;
  englishSectionBodyHtml: string;
  englishTitleTouched: boolean;
  englishBodyTouched: boolean;
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
function LegacyImagePreview({ src, alt }: { src: string; alt: string }) {
  const isBackendUrl = src.startsWith('/');
  const authSrc = useAuthenticatedImage(isBackendUrl ? src : null);
  const resolved = isBackendUrl ? authSrc : src;
  if (!resolved) return <div className="w-full h-40 bg-gray-100 animate-pulse rounded-lg" />;
  return <img src={resolved} alt={alt} className="w-full h-auto object-contain rounded-lg" />;
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
  // Đi từ tab Sau tiếp khách/Đóng góp: lưu xong quay lại đúng trang cũ.
  const returnTo = searchParams.get('returnTo');

  // ── Meta state ──
  const [loading,    setLoading]    = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [rowVersion, setRowVersion] = useState(0);
  const [newsStatus, setNewsStatus] = useState('');
  const [availableLanguages, setAvailableLanguages] = useState<string[]>([]);
  // Đoàn gắn với bài viết (nếu có) — ảnh mới tải lên khi sửa vẫn phải vào đúng thư mục đoàn đó.
  const [visitInstanceId, setVisitInstanceId] = useState<number | null>(null);

  // ── Form fields ──
  const [title,    setTitle]    = useState('');
  const [summary,  setSummary]  = useState('');
  const [sections, setSections] = useState<Section[]>([
    { id: 1, sectionTitle: '', sectionBodyHtml: '', sectionImages: [], englishSectionTitle: '', englishSectionBodyHtml: '', englishTitleTouched: false, englishBodyTouched: false },
  ]);

  // Bilingual editor. This page always edits the Vietnamese original; the English column is
  // shown whenever either (a) the post already has an English translation — loaded alongside
  // the Vietnamese one so both display side-by-side and are saved independently — or (b) the
  // user turns on the toggle to add a first English translation. Auto-translate (debounce +
  // "Dịch lại") only runs for case (b): once English already exists, its text is user-owned and
  // should not be silently overwritten by further Vietnamese edits.
  const englishAlreadyExists = availableLanguages.includes('en');
  const [addingEnglish, setAddingEnglish] = useState(false);
  const showEnglishColumn = languageCode === 'vi' && (englishAlreadyExists || addingEnglish);
  const [englishTitle, setEnglishTitle] = useState('');
  const [englishSummary, setEnglishSummary] = useState('');
  const [englishTitleTouched, setEnglishTitleTouched] = useState(false);
  const [englishSummaryTouched, setEnglishSummaryTouched] = useState(false);

  const { translating, retranslateNow } = useBilingualTranslate({
    enabled: addingEnglish && !englishAlreadyExists,
    newsId,
    title,
    summary,
    sections: sections.map((s, i) => ({ sectionOrder: i + 1, sectionTitle: s.sectionTitle, sectionBodyHtml: s.sectionBodyHtml })),
    onTranslated: (result) => {
      if (!englishTitleTouched) setEnglishTitle(result.title);
      if (!englishSummaryTouched) setEnglishSummary(result.summary);
      setSections(prev => prev.map((s, i) => {
        const match = result.sections.find(r => r.sectionOrder === i + 1);
        if (!match) return s;
        return {
          ...s,
          englishSectionTitle: s.englishTitleTouched ? s.englishSectionTitle : match.sectionTitle,
          englishSectionBodyHtml: s.englishBodyTouched ? s.englishSectionBodyHtml : match.sectionBodyHtml,
        };
      }));
    },
  });

  async function handleRetranslate() {
    setEnglishTitleTouched(false);
    setEnglishSummaryTouched(false);
    setSections(prev => prev.map(s => ({ ...s, englishTitleTouched: false, englishBodyTouched: false })));
    const ok = await retranslateNow();
    if (ok) toast.success('Đã dịch lại sang tiếng Anh.');
    else toast.error('Không thể dịch tự động. Vui lòng thử lại.');
  }

  // ── Cover image ──
  const [currentCoverFileId, setCurrentCoverFileId] = useState<number | null>(null);
  const [existingCoverUrl,   setExistingCoverUrl]   = useState<string | null>(null);
  const [coverPreviewUrl,    setCoverPreviewUrl]     = useState<string | null>(null);
  const [coverUploading,     setCoverUploading]      = useState(false);
  const [showCoverPicker,    setShowCoverPicker]     = useState(false);
  const existingCoverSrc = useAuthenticatedImage(existingCoverUrl);

  // ── Load news on mount ────────────────────────────────────────────────────

  useEffect(() => {
    if (!newsId) return;
    let cancelled = false;

    type RawSection = {
      sectionTitle?: string;
      sectionBodyHtml?: string;
      files?: { fileId: number; url?: string; usageType?: string }[];
    };

    function mapSections(rawSections: RawSection[]): Section[] {
      return rawSections.map((s, i) => {
        const rawHtml = s.sectionBodyHtml ?? '';

        // Ưu tiên ảnh thật từ news_section_files (có thể nhiều ảnh); nếu bài cũ nhúng
        // base64 trong HTML (chỉ 1 ảnh legacy tối đa) thì giữ lại để migrate khi lưu.
        const inlineFiles = (s.files ?? []).filter(f => f.usageType === 'INLINE_IMAGE' && f.url);
        const legacySrc = inlineFiles.length === 0 ? extractFirstImgSrc(rawHtml) : null;

        const sectionImages: SectionImageItem[] = inlineFiles.map(f => ({
          fileId: f.fileId, previewUrl: f.url!, uploading: false,
        }));

        return {
          id:              i + 1,
          sectionTitle:    s.sectionTitle ?? '',
          sectionBodyHtml: stripImgTags(rawHtml),
          sectionImages,
          legacyBase64:    legacySrc?.startsWith('data:') ? legacySrc : undefined,
          englishSectionTitle: '',
          englishSectionBodyHtml: '',
          englishTitleTouched: false,
          englishBodyTouched: false,
        };
      });
    }

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
        setVisitInstanceId(data.visitInstanceId ?? null);
        const langs: string[] = Array.isArray(data.availableLanguages) ? data.availableLanguages : [];
        setAvailableLanguages(langs);

        if (data.coverFile?.fileId) {
          setCurrentCoverFileId(data.coverFile.fileId);
          setExistingCoverUrl(data.coverFile.url ?? null);
        }

        const viSections: Section[] = Array.isArray(data.sections) && data.sections.length > 0
          ? mapSections(data.sections)
          : [];

        // Khi bài đã có bản tiếng Anh, tải luôn để hiển thị 2 cột song song thay vì phải
        // chuyển qua trang sửa riêng (?lang=en).
        if (languageCode === 'vi' && langs.includes('en')) {
          try {
            const { data: enData } = await httpClient.get(`/news/${newsId}`, { params: { languageCode: 'en' } });
            if (cancelled) return;
            setEnglishTitle(enData.title ?? '');
            setEnglishSummary(enData.summary ?? '');
            const enSections: RawSection[] = Array.isArray(enData.sections) ? enData.sections : [];
            setSections(
              (viSections.length > 0 ? viSections : mapSections([])).map((s, i) => ({
                ...s,
                englishSectionTitle: enSections[i]?.sectionTitle ?? '',
                englishSectionBodyHtml: stripImgTags(enSections[i]?.sectionBodyHtml ?? ''),
              })),
            );
          } catch {
            // Bản tiếng Anh tồn tại theo availableLanguages nhưng tải lỗi — vẫn hiển thị
            // được bản tiếng Việt, chỉ là chưa có cột tiếng Anh.
            if (viSections.length > 0) setSections(viSections);
          }
        } else if (viSections.length > 0) {
          setSections(viSections);
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
      const result = await uploadFileToEndpoint('/news/cover-upload', 'file', file, 'post',
        visitInstanceId ? { visitInstanceId } : undefined);
      setCurrentCoverFileId(result.fileId);
    } catch {
      toast.error('Tải ảnh bìa thất bại. Vui lòng thử lại.');
      setCoverPreviewUrl(null);
    } finally {
      setCoverUploading(false);
    }
  }

  function handleCoverPickedFromVisitInstance(photos: { fileId: number; url: string }[]) {
    const picked = photos[0];
    if (!picked) return;
    setCoverPreviewUrl(null);
    setExistingCoverUrl(picked.url);
    setCurrentCoverFileId(picked.fileId);
  }

  // ── Section CRUD ──────────────────────────────────────────────────────────

  function addSection() {
    if (sections.length >= 10) return;
    setSections(prev => [
      ...prev,
      { id: Date.now(), sectionTitle: '', sectionBodyHtml: '', sectionImages: [], englishSectionTitle: '', englishSectionBodyHtml: '', englishTitleTouched: false, englishBodyTouched: false },
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

  function updateEnglishSection(index: number, field: 'englishSectionTitle' | 'englishSectionBodyHtml', value: string) {
    setSections(prev => {
      const next = [...prev];
      const touchedField = field === 'englishSectionTitle' ? 'englishTitleTouched' : 'englishBodyTouched';
      next[index] = { ...next[index], [field]: value, [touchedField]: true };
      return next;
    });
  }

  /** ReactQuill fires onChange on mount/programmatic value sync too (source 'api'/'silent'), not
   * only on real typing (source 'user') — applying a translated value into the EN Quill editor
   * must not be mistaken for the user manually editing it, or it would permanently block further
   * auto-translate updates. Only forward genuine user edits to updateEnglishSection. */
  function updateEnglishSectionBodyFromQuill(index: number, value: string, source: string) {
    if (source !== 'user') return;
    updateEnglishSection(index, 'englishSectionBodyHtml', value);
  }

  function updateSectionImages(index: number, updater: (prev: SectionImageItem[]) => SectionImageItem[]) {
    setSections(prev => {
      const next = [...prev];
      next[index] = { ...next[index], sectionImages: updater(next[index].sectionImages) };
      return next;
    });
  }

  // ── Submit ────────────────────────────────────────────────────────────────

  async function handleSubmit() {
    if (!title.trim())   { toast.error('Vui lòng nhập tiêu đề tin tức.'); return; }
    if (title.length > 150) { toast.error('Tiêu đề không được vượt quá 150 ký tự.'); return; }
    if (!summary.trim()) { toast.error('Vui lòng nhập mô tả ngắn.'); return; }
    if (summary.length > 500) { toast.error('Mô tả ngắn không được vượt quá 500 ký tự.'); return; }
    if (coverUploading)  { toast.error('Vui lòng chờ ảnh bìa tải lên xong.'); return; }
    const overLongSection = sections.find(s => s.sectionTitle.length > 255);
    if (overLongSection) {
      toast.error(`Tiêu đề mục ${sections.indexOf(overLongSection) + 1} không được vượt quá 255 ký tự.`);
      return;
    }
    const emptyBody = sections.find(s => !s.sectionBodyHtml.replace(/<[^>]+>/g, '').trim());
    if (emptyBody) {
      toast.error(`Nội dung chi tiết mục ${sections.indexOf(emptyBody) + 1} không được để trống.`);
      return;
    }
    if (sections.some(s => s.sectionImages.some(img => img.uploading))) {
      toast.error('Ảnh nội dung đang được tải lên, vui lòng chờ.');
      return;
    }
    if (showEnglishColumn) {
      if (!englishTitle.trim()) { toast.error('Tiêu đề tiếng Anh không được để trống.'); return; }
      if (englishTitle.length > 150) { toast.error('Tiêu đề tiếng Anh không được vượt quá 150 ký tự.'); return; }
      if (englishSummary.length > 500) { toast.error('Mô tả ngắn (Anh) không được vượt quá 500 ký tự.'); return; }
      for (const s of sections) {
        if (s.englishSectionTitle.length > 255) {
          toast.error(`Tiêu đề tiếng Anh của mục ${sections.indexOf(s) + 1} không được vượt quá 255 ký tự.`);
          return;
        }
        const strippedEn = s.englishSectionBodyHtml.replace(/<[^>]+>/g, '').trim();
        if (!strippedEn) {
          toast.error(`Nội dung tiếng Anh của mục ${sections.indexOf(s) + 1} không được để trống.`);
          return;
        }
      }
    }

    setSubmitting(true);
    try {
      // Bài cũ còn ảnh base64 nhúng trong HTML → migrate lên Drive trước khi lưu
      // (backend từ chối payload chứa data:image).
      const migrated = [...sections];
      for (let i = 0; i < migrated.length; i++) {
        const legacy = migrated[i].legacyBase64;
        if (legacy && migrated[i].sectionImages.length === 0) {
          const file = await dataUrlToFile(legacy, `news-section-${newsId}-${i + 1}.png`);
          const uploaded = await uploadFileToEndpoint('/news/section-file-upload', 'file', file);
          migrated[i] = {
            ...migrated[i],
            sectionImages: [{ fileId: uploaded.fileId, previewUrl: legacy, uploading: false }],
            legacyBase64: undefined,
          };
        }
      }
      const broken = migrated.find(s => s.sectionImages.some(img => img.fileId === null));
      if (broken) {
        toast.error(`Một số ảnh của mục ${migrated.indexOf(broken) + 1} chưa tải lên thành công. Vui lòng chọn lại ảnh.`);
        setSubmitting(false);
        return;
      }

      const toSectionFiles = (images: SectionImageItem[]) =>
        images.filter(img => img.fileId).map((img, i) => ({
          fileId: img.fileId as number,
          usageType: 'INLINE_IMAGE',
          displayOrder: i + 1,
        }));

      // rowVersion lives on the News row itself (not per-translation) — saving the Vietnamese
      // translation already bumps it server-side, so the English save right after must reuse the
      // NEW version the first call returns, not the one captured at page load, or the optimistic
      // concurrency check on the second PUT falsely reports "edited by someone else".
      const { data: viSaveResult } = await httpClient.put(`/news/${newsId}`, {
        rowVersion,
        coverFileId: currentCoverFileId ?? null,
        title:       title.trim(),
        summary:     summary.trim(),
        languageCode,
        contentSections: migrated.map((s, i) => ({
          sectionOrder:    i + 1,
          sectionTitle:    s.sectionTitle.trim(),
          sectionBodyHtml: s.sectionBodyHtml,
          sectionFiles:    toSectionFiles(s.sectionImages),
        })),
      });
      const latestRowVersion: number = viSaveResult?.newRowVersion ?? rowVersion;

      // Lưu bản tiếng Anh — dùng đúng kiến trúc multilingual hiện có, không tạo cơ chế lưu mới:
      // bản đã tồn tại thì PUT như một bản dịch bình thường (?lang=en); bản mới thì
      // AddMultilingualNewsCommand (giống hệt luồng "thêm bản dịch" đã có).
      if (showEnglishColumn) {
        try {
          if (englishAlreadyExists) {
            await httpClient.put(`/news/${newsId}`, {
              rowVersion: latestRowVersion,
              coverFileId: currentCoverFileId ?? null,
              title: englishTitle.trim(),
              summary: englishSummary.trim(),
              languageCode: 'en',
              contentSections: migrated.map((s, i) => ({
                sectionOrder:    i + 1,
                sectionTitle:    s.englishSectionTitle.trim(),
                sectionBodyHtml: s.englishSectionBodyHtml,
                sectionFiles:    toSectionFiles(s.sectionImages),
              })),
            });
          } else {
            await httpClient.post('/news/addmultilingualnews', {
              newsId,
              languageCode: 'en',
              title: englishTitle.trim(),
              summary: englishSummary.trim(),
              sections: migrated.map((s, i) => ({
                sectionOrder:    i + 1,
                sectionTitle:    s.englishSectionTitle.trim(),
                sectionBodyHtml: s.englishSectionBodyHtml,
                sectionFiles:    toSectionFiles(s.sectionImages),
              })),
              copySectionFilesFromLanguage: 'vi',
            });
          }
        } catch (err: unknown) {
          const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
          toast.error(msg ?? 'Đã lưu bản tiếng Việt nhưng không thể lưu bản tiếng Anh. Vui lòng thử dịch lại.');
        }
      }

      toast.success(
        newsStatus === 'REJECTED'
          ? 'Bài viết đã được cập nhật và nộp lại chờ Staff Leader duyệt!'
          : 'Bài viết đã được cập nhật thành công!',
      );
      setTimeout(() => navigate(returnTo || '/dashboard/news'), 1500);
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
      className="p-4 sm:p-6 md:p-8 pb-12 max-w-7xl mx-auto"
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
        <CollapsibleSection
          title="1. Thông tin cơ bản"
          headerExtra={languageCode === 'vi' && (
            <>
              {showEnglishColumn && !englishAlreadyExists && (
                <button
                  type="button"
                  onClick={handleRetranslate}
                  disabled={translating}
                  title="Dịch lại sang tiếng Anh"
                  className="flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold text-white/90 border border-white/40 rounded-lg hover:bg-white/10 transition-colors disabled:opacity-50"
                >
                  <RotateCw className={`w-3.5 h-3.5 ${translating ? 'animate-spin' : ''}`} />
                  {translating ? 'Đang dịch...' : 'Dịch lại'}
                </button>
              )}
              <button
                type="button"
                onClick={() => { if (!englishAlreadyExists) setAddingEnglish(v => !v); }}
                disabled={englishAlreadyExists}
                title={
                  englishAlreadyExists
                    ? 'Bài đã có bản dịch tiếng Anh'
                    : addingEnglish ? 'Tắt soạn song ngữ' : 'Bật soạn song ngữ Việt – Anh'
                }
                className={`flex items-center justify-center w-8 h-8 rounded-lg transition-colors ${
                  showEnglishColumn ? 'bg-white text-[#004c91]' : 'bg-white/10 text-white hover:bg-white/20'
                } ${englishAlreadyExists ? 'cursor-default' : ''}`}
              >
                <Languages className="w-4 h-4" />
              </button>
            </>
          )}
        >
          <div className="flex flex-col gap-6">

            <BilingualColumns
              showEnglish={showEnglishColumn}
              left={
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <label className="block text-gray-900 font-bold">
                      Tiêu đề tin tức <span className="text-red-500">*</span>
                      {showEnglishColumn && <LanguageColumnLabel>VI</LanguageColumnLabel>}
                    </label>
                    <span className={`text-xs font-normal shrink-0 ml-2 ${title.length > 150 ? 'text-red-500' : 'text-gray-400'}`}>
                      {title.length}/150
                    </span>
                  </div>
                  <AutoGrowInput
                    value={title}
                    onChange={setTitle}
                    placeholder="Nhập tiêu đề tin tức..."
                    className={`w-full px-4 py-3 border rounded-xl focus:outline-none focus:ring-1 transition-colors text-gray-800 ${
                      title.length > 150
                        ? 'border-red-400 focus:border-red-500 focus:ring-red-300'
                        : 'border-gray-300 focus:border-[#004c91] focus:ring-[#004c91] hover:border-[#004c91]'
                    }`}
                  />
                  {title.length > 150 && (
                    <p className="text-xs text-red-500 mt-1">Tiêu đề không được vượt quá 150 ký tự.</p>
                  )}
                </div>
              }
              right={
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <label className="block text-gray-900 font-bold">
                      Title <span className="text-red-500">*</span>
                      <LanguageColumnLabel>EN</LanguageColumnLabel>
                    </label>
                    <span className={`text-xs font-normal shrink-0 ml-2 ${englishTitle.length > 150 ? 'text-red-500' : 'text-gray-400'}`}>
                      {englishTitle.length}/150
                    </span>
                  </div>
                  <AutoGrowInput
                    value={englishTitle}
                    onChange={v => { setEnglishTitle(v); setEnglishTitleTouched(true); }}
                    placeholder="Enter news title..."
                    className={`w-full px-4 py-3 border rounded-xl focus:outline-none focus:ring-1 transition-colors text-gray-800 ${
                      englishTitle.length > 150
                        ? 'border-red-400 focus:border-red-500 focus:ring-red-300'
                        : 'border-gray-300 focus:border-[#004c91] focus:ring-[#004c91] hover:border-[#004c91]'
                    }`}
                  />
                  {englishTitle.length > 150 && (
                    <p className="text-xs text-red-500 mt-1">Title must not exceed 150 characters.</p>
                  )}
                </div>
              }
            />

            <BilingualColumns
              showEnglish={showEnglishColumn}
              left={
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <label className="block text-gray-900 font-bold">
                      Mô tả ngắn <span className="text-red-500">*</span>
                      {showEnglishColumn && <LanguageColumnLabel>VI</LanguageColumnLabel>}
                    </label>
                    <span className={`text-xs font-normal shrink-0 ml-2 ${summary.length > 500 ? 'text-red-500' : 'text-gray-400'}`}>
                      {summary.length}/500
                    </span>
                  </div>
                  <AutoGrowTextarea
                    value={summary}
                    onChange={setSummary}
                    placeholder="Nhập mô tả ngắn gọn..."
                    className={`w-full p-4 border rounded-xl focus:outline-none focus:ring-1 transition-colors text-gray-800 ${
                      summary.length > 500
                        ? 'border-red-400 focus:border-red-500 focus:ring-red-300'
                        : 'border-gray-300 focus:border-[#004c91] focus:ring-[#004c91] hover:border-[#004c91]'
                    }`}
                  />
                  {summary.length > 500 && (
                    <p className="text-xs text-red-500 mt-1">Mô tả ngắn không được vượt quá 500 ký tự.</p>
                  )}
                </div>
              }
              right={
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <label className="block text-gray-900 font-bold">
                      Summary
                      <LanguageColumnLabel>EN</LanguageColumnLabel>
                    </label>
                    <span className={`text-xs font-normal shrink-0 ml-2 ${englishSummary.length > 500 ? 'text-red-500' : 'text-gray-400'}`}>
                      {englishSummary.length}/500
                    </span>
                  </div>
                  <AutoGrowTextarea
                    value={englishSummary}
                    onChange={v => { setEnglishSummary(v); setEnglishSummaryTouched(true); }}
                    placeholder="Enter a short summary..."
                    className={`w-full p-4 border rounded-xl focus:outline-none focus:ring-1 transition-colors text-gray-800 ${
                      englishSummary.length > 500
                        ? 'border-red-400 focus:border-red-500 focus:ring-red-300'
                        : 'border-gray-300 focus:border-[#004c91] focus:ring-[#004c91] hover:border-[#004c91]'
                    }`}
                  />
                  {englishSummary.length > 500 && (
                    <p className="text-xs text-red-500 mt-1">Summary must not exceed 500 characters.</p>
                  )}
                </div>
              }
            />

          </div>
        </CollapsibleSection>

        {/* ── 2. Ảnh đại diện ── */}
        <CollapsibleSection title="2. Ảnh đại diện">
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
            {visitInstanceId && (
              <button
                type="button"
                onClick={() => setShowCoverPicker(true)}
                className="mt-3 flex items-center gap-2 px-4 py-2.5 border-2 border-dashed border-gray-300 rounded-xl hover:border-[#004c91] hover:bg-[#eef5fa] transition-colors group w-full justify-center"
              >
                <FolderOpen className="w-4 h-4 text-gray-400 group-hover:text-[#004c91] shrink-0 transition-colors" />
                <span className="text-sm text-gray-500 group-hover:text-[#004c91] font-medium transition-colors">
                  Chọn từ ảnh đoàn
                </span>
              </button>
            )}
            {showCoverPicker && visitInstanceId && (
              <VisitInstancePhotoPicker
                visitInstanceId={visitInstanceId}
                alreadyPickedFileIds={currentCoverFileId !== null ? [currentCoverFileId] : []}
                maxPickable={1}
                onClose={() => setShowCoverPicker(false)}
                onPick={handleCoverPickedFromVisitInstance}
              />
            )}
        </CollapsibleSection>

        {/* ── 3. Nội dung chi tiết ── */}
        <CollapsibleSection title="3. Nội dung chi tiết">
          <div className="flex flex-col gap-8">

            {sections.map((section, index) => (
              <div key={section.id}>
                {index > 0 && <div className="h-px bg-gray-100 w-full mb-8" />}
                <div className="flex flex-col gap-5">

                  {/* Section heading row */}
                  <div className="flex items-center justify-between mb-2">
                    <label className="block text-gray-900 font-bold">
                      Mục {index + 1} <span className="text-red-500">*</span>
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

                  <BilingualColumns
                    showEnglish={showEnglishColumn}
                    left={
                      <div>
                        <div className="flex items-center justify-between mb-1.5">
                          <label className="block text-gray-900 font-bold text-sm">
                            Tiêu đề mục
                            {showEnglishColumn && <LanguageColumnLabel>VI</LanguageColumnLabel>}
                          </label>
                          <span className={`text-xs font-normal shrink-0 ml-2 ${section.sectionTitle.length > 255 ? 'text-red-500' : 'text-gray-400'}`}>
                            {section.sectionTitle.length}/255
                          </span>
                        </div>
                        <AutoGrowInput
                          value={section.sectionTitle}
                          onChange={v => updateSection(index, 'sectionTitle', v)}
                          placeholder="Nhập tiêu đề mục nội dung..."
                          className={`w-full px-4 py-3 border rounded-xl focus:outline-none focus:ring-1 transition-colors text-gray-800 ${
                            section.sectionTitle.length > 255
                              ? 'border-red-400 focus:border-red-500 focus:ring-red-300'
                              : 'border-gray-300 focus:border-[#004c91] focus:ring-[#004c91] hover:border-[#004c91]'
                          }`}
                        />
                        {section.sectionTitle.length > 255 && (
                          <p className="text-xs text-red-500 mt-1">Tiêu đề mục không được vượt quá 255 ký tự.</p>
                        )}
                      </div>
                    }
                    right={
                      <div>
                        <div className="flex items-center justify-between mb-1.5">
                          <label className="block text-gray-900 font-bold text-sm">
                            Section title
                            <LanguageColumnLabel>EN</LanguageColumnLabel>
                          </label>
                          <span className={`text-xs font-normal shrink-0 ml-2 ${section.englishSectionTitle.length > 255 ? 'text-red-500' : 'text-gray-400'}`}>
                            {section.englishSectionTitle.length}/255
                          </span>
                        </div>
                        <AutoGrowInput
                          value={section.englishSectionTitle}
                          onChange={v => updateEnglishSection(index, 'englishSectionTitle', v)}
                          placeholder="Enter section title..."
                          className={`w-full px-4 py-3 border rounded-xl focus:outline-none focus:ring-1 transition-colors text-gray-800 ${
                            section.englishSectionTitle.length > 255
                              ? 'border-red-400 focus:border-red-500 focus:ring-red-300'
                              : 'border-gray-300 focus:border-[#004c91] focus:ring-[#004c91] hover:border-[#004c91]'
                          }`}
                        />
                        {section.englishSectionTitle.length > 255 && (
                          <p className="text-xs text-red-500 mt-1">Section title must not exceed 255 characters.</p>
                        )}
                      </div>
                    }
                  />

                  <BilingualColumns
                    showEnglish={showEnglishColumn}
                    left={
                      <div>
                        <label className="block text-gray-900 font-bold mb-2">
                          Nội dung <span className="text-red-500">*</span>
                          {showEnglishColumn && <LanguageColumnLabel>VI</LanguageColumnLabel>}
                        </label>
                        <div className="news-quill-compact border border-gray-300 rounded-lg overflow-hidden focus-within:border-[#004c91] focus-within:ring-1 focus-within:ring-[#004c91] transition-colors">
                          {/* @ts-ignore */}
                          <ReactQuill
                            key={`quill-vi-${section.id}`}
                            theme="snow"
                            value={section.sectionBodyHtml}
                            onChange={value => updateSection(index, 'sectionBodyHtml', value)}
                            placeholder="Nhập nội dung chi tiết..."
                            className="bg-white"
                            modules={QUILL_MODULES}
                          />
                        </div>
                      </div>
                    }
                    right={
                      <div>
                        <label className="block text-gray-900 font-bold mb-2">
                          Content
                          <LanguageColumnLabel>EN</LanguageColumnLabel>
                        </label>
                        <div className="news-quill-compact border border-gray-300 rounded-lg overflow-hidden focus-within:border-[#004c91] focus-within:ring-1 focus-within:ring-[#004c91] transition-colors">
                          {/* @ts-ignore */}
                          <ReactQuill
                            key={`quill-en-${section.id}`}
                            theme="snow"
                            value={section.englishSectionBodyHtml}
                            onChange={(value, _delta, source) => updateEnglishSectionBodyFromQuill(index, value, source)}
                            placeholder="Enter detailed content..."
                            className="bg-white"
                            modules={QUILL_MODULES}
                          />
                        </div>
                      </div>
                    }
                  />

                  {/* Legacy single embedded image from an old post — offered for migration only. */}
                  {section.legacyBase64 && section.sectionImages.length === 0 && (
                    <div>
                      <label className="block text-gray-900 font-bold mb-2">Ảnh cũ (sẽ được chuyển sang hệ thống lưu trữ mới khi lưu)</label>
                      <div className="rounded-xl overflow-hidden border border-gray-200 bg-gray-50">
                        <LegacyImagePreview src={section.legacyBase64} alt={`Ảnh cũ mục ${index + 1}`} />
                      </div>
                    </div>
                  )}

                  <SectionImagesEditor
                    images={section.sectionImages}
                    onChange={updater => updateSectionImages(index, updater)}
                    uploadEndpoint="/news/section-file-upload"
                    visitInstanceId={visitInstanceId}
                  />

                </div>
              </div>
            ))}

            <div className="text-right text-sm text-gray-400 italic mt-2">
              {sections.length}/10 mục nội dung
            </div>
          </div>
        </CollapsibleSection>

        {/* ── Buttons ── */}
        <div className="flex items-center justify-end gap-3 pt-6 border-t border-gray-200">
          <button
            onClick={() => navigate(returnTo || '/dashboard/news')}
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
