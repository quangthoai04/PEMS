import React, { useState, useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { motion } from 'motion/react';
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';
import { UploadCloud, Plus, Trash2, ArrowLeft, Calendar, MapPin, CheckCircle2, Languages, RotateCw } from 'lucide-react';
import toast from 'react-hot-toast';
import httpClient from '../../../shared/api/httpClient';
import { uploadFileToEndpoint } from '../../../shared/api/fileUploadApi';
import { validateFile } from '../../../shared/utils/fileValidation';
import { useAuth } from '../../../shared/hooks/useAuth';
import { SectionImagesEditor, type SectionImageItem } from './components/SectionImagesEditor';
import { BilingualColumns, LanguageColumnLabel } from './components/BilingualColumns';
import { useBilingualTranslate } from './components/useBilingualTranslate';
import { CollapsibleSection } from './components/CollapsibleSection';
import { AutoGrowInput, AutoGrowTextarea } from './components/AutoGrowInput';

import { formatVietnamDate } from '../../../shared/utils/vietnamTime';
// ─── Types ───────────────────────────────────────────────────────────────────

interface EligibleVisit {
  visitInstanceId: number;
  visitTitle: string;
  campusName: string;
  plannedStartAt: string;
  plannedEndAt: string;
  closedAt?: string;
  status: string;
  hasNews: boolean;
  canSelect: boolean;
}

interface ContentSection {
  id: number;
  sectionOrder: number;
  sectionTitle: string;
  sectionBodyHtml: string;
  sectionImages: SectionImageItem[];
  // English mirror — only used when bilingual mode is on.
  englishSectionTitle: string;
  englishSectionBodyHtml: string;
  englishTitleTouched: boolean;
  englishBodyTouched: boolean;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function formatDate(dateStr?: string): string {
  return formatVietnamDate(dateStr);
}

// Nhãn trạng thái chuyến trong cửa sổ viết tin (AFTER_VISIT trở đi).
const INSTANCE_STATUS_LABELS: Record<string, string> = {
  AFTER_VISIT: 'Sau tiếp khách',
  CLOSED: 'Đã đóng đoàn',
};
const instanceStatusLabel = (status: string) => INSTANCE_STATUS_LABELS[status] ?? status;

// ─── Quill toolbar (no image button — images managed separately) ──────────────

const QUILL_MODULES = {
  toolbar: [
    ['bold', 'italic', 'underline', 'strike'],
    [{ align: [] }],
    [{ list: 'ordered' }, { list: 'bullet' }],
    ['link'],
    ['clean'],
  ],
};

let sectionIdCounter = 1;
function newSection(sectionOrder: number): ContentSection {
  return {
    id: Date.now() + sectionIdCounter++,
    sectionOrder,
    sectionTitle: '',
    sectionBodyHtml: '',
    sectionImages: [],
    englishSectionTitle: '',
    englishSectionBodyHtml: '',
    englishTitleTouched: false,
    englishBodyTouched: false,
  };
}

// ─── Component ────────────────────────────────────────────────────────────────

export function CreateNews() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { user } = useAuth();
  // Đi từ tab "Sau tiếp khách"/trang Đóng góp: chuyến được chọn sẵn + quay lại đúng trang cũ.
  const presetVisitInstanceId = searchParams.get('visitInstanceId');
  const returnTo = searchParams.get('returnTo');

  // Staff thường được tạo tin không gắn đoàn; Student vẫn bắt buộc chọn đoàn.
  const isStaff = user?.roleCode === 'STAFF' && (user?.subRole ?? '') === 'STAFF';
  const isStudent = user?.roleCode === 'STUDENT';
  const visitRequired = !isStaff; // Student (hoặc role khác) → bắt buộc; Staff → tùy chọn

  // Step 0: Eligible visit instances
  const [eligibleVisits, setEligibleVisits]   = useState<EligibleVisit[]>([]);
  const [loadingVisits, setLoadingVisits]     = useState(true);
  const [selectedVisit, setSelectedVisit]     = useState<EligibleVisit | null>(null);
  const [showVisitList, setShowVisitList]     = useState(!presetVisitInstanceId);
  const [skippedVisit, setSkippedVisit]       = useState(false); // Staff: "không gắn đoàn nào"
  // Khi có preset: khóa chuyến, không cho đổi; lỗi preset hiển thị riêng.
  const [presetError, setPresetError]         = useState<string | null>(null);
  const [presetExistingNewsId, setPresetExistingNewsId] = useState<number | null>(null);

  // Step 1: Basic info
  const [title,   setTitle]   = useState('');
  const [summary, setSummary] = useState('');

  // Bilingual editor — on by default, both VI/EN columns show immediately when composing a new
  // post rather than requiring the user to opt in.
  const [bilingual, setBilingual] = useState(true);
  const [englishTitle, setEnglishTitle] = useState('');
  const [englishSummary, setEnglishSummary] = useState('');
  const [englishTitleTouched, setEnglishTitleTouched] = useState(false);
  const [englishSummaryTouched, setEnglishSummaryTouched] = useState(false);

  // Step 2: Cover image
  const [imagePreview,    setImagePreview]    = useState<string | null>(null);
  const [coverFileId,     setCoverFileId]     = useState<number | null>(null);
  const [coverUploading,  setCoverUploading]  = useState(false);

  // Step 3: Content sections
  const [sections, setSections] = useState<ContentSection[]>([newSection(1)]);
  const [sectionToDelete, setSectionToDelete] = useState<number | null>(null);

  // Submit state
  const [submitting, setSubmitting] = useState(false);

  // ── Bilingual auto-translate (debounced, cancels stale responses) ──────────

  const { translating, retranslateNow } = useBilingualTranslate({
    enabled: bilingual,
    title,
    summary,
    sections: sections.map(s => ({
      sectionOrder: s.sectionOrder,
      sectionTitle: s.sectionTitle,
      sectionBodyHtml: s.sectionBodyHtml,
    })),
    onTranslated: (result) => {
      if (!englishTitleTouched) setEnglishTitle(result.title);
      if (!englishSummaryTouched) setEnglishSummary(result.summary);
      setSections(prev => prev.map(s => {
        const match = result.sections.find(r => r.sectionOrder === s.sectionOrder);
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

  // ── Load eligible visits ───────────────────────────────────────────────────

  useEffect(() => {
    let cancelled = false;
    const fetchVisits = async () => {
      setLoadingVisits(true);
      try {
        // Khi có chuyến chọn sẵn (?visitInstanceId): lấy cả chuyến đã có bài để báo đúng lý do.
        const { data } = await httpClient.get<{ items: EligibleVisit[] }>(
          '/news/eligible-visit-instances',
          { params: { includeAlreadyHasNews: !!presetVisitInstanceId } }
        );
        if (cancelled) return;
        const items = data.items ?? [];

        if (presetVisitInstanceId) {
          const preset = items.find(v => String(v.visitInstanceId) === presetVisitInstanceId);
          if (!preset) {
            setPresetError('Chuyến tiếp khách này chưa đủ điều kiện để viết tin tức (chưa vào giai đoạn Sau tiếp khách, không yêu cầu tin tức, hoặc bạn không phải Host/người tham gia).');
          } else if (preset.hasNews) {
            setPresetExistingNewsId(preset.visitInstanceId);
            setSelectedVisit(preset);
            setPresetError('Bạn đã có bài viết cho chuyến này. Vui lòng chỉnh sửa bài hiện có trong Quản lý tin tức.');
          } else {
            setSelectedVisit(preset);
          }
          setEligibleVisits(items);
        } else {
          setEligibleVisits(items);
        }
      } catch {
        if (!cancelled) {
          setEligibleVisits([]);
          if (presetVisitInstanceId) setPresetError('Không thể tải thông tin chuyến tiếp khách. Vui lòng thử lại.');
        }
      } finally {
        if (!cancelled) setLoadingVisits(false);
      }
    };
    fetchVisits();
    return () => { cancelled = true; };
  }, [presetVisitInstanceId]);

  // ── Visit selection ────────────────────────────────────────────────────────

  const handleSelectVisit = (visit: EligibleVisit) => {
    if (!visit.canSelect) return;
    setSelectedVisit(visit);
    setSkippedVisit(false);
    setShowVisitList(false);
  };

  const handleChangeVisit = () => {
    setSelectedVisit(null);
    setSkippedVisit(false);
    setShowVisitList(true);
  };

  const handleSkipVisit = () => {
    setSelectedVisit(null);
    setSkippedVisit(true);
    setShowVisitList(false);
  };

  // ── Cover upload ───────────────────────────────────────────────────────────

  const handleImageUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    e.target.value = '';

    const check = validateFile(file, 'NEWS_IMAGE');
    if (!check.ok) { toast.error(check.message ?? 'Ảnh không hợp lệ.'); return; }

    setImagePreview(URL.createObjectURL(file));
    setCoverFileId(null);
    setCoverUploading(true);
    try {
      const uploaded = await uploadFileToEndpoint('/news/cover-upload', 'file', file);
      setCoverFileId(uploaded.fileId);
    } catch (err: any) {
      toast.error(err?.response?.data?.message ?? 'Không thể tải ảnh bìa lên.');
      setCoverFileId(null);
      setImagePreview(null);
    } finally {
      setCoverUploading(false);
    }
  };

  // ── Section CRUD ───────────────────────────────────────────────────────────

  const addSection = () => {
    if (sections.length >= 10) return;
    setSections(prev => [...prev, newSection(prev.length + 1)]);
  };

  const confirmDeleteSection = () => {
    if (sectionToDelete === null) return;
    setSections(prev =>
      prev.filter((_, i) => i !== sectionToDelete).map((s, i) => ({ ...s, sectionOrder: i + 1 }))
    );
    setSectionToDelete(null);
  };

  const updateSection = (index: number, field: 'sectionTitle' | 'sectionBodyHtml', value: string) => {
    setSections(prev => {
      const next = [...prev];
      next[index] = { ...next[index], [field]: value };
      return next;
    });
  };

  /** field update triggered by the user directly editing the English column — marks it "touched"
   * so future auto-translate results no longer overwrite it. */
  const updateEnglishSection = (index: number, field: 'englishSectionTitle' | 'englishSectionBodyHtml', value: string) => {
    setSections(prev => {
      const next = [...prev];
      const touchedField = field === 'englishSectionTitle' ? 'englishTitleTouched' : 'englishBodyTouched';
      next[index] = { ...next[index], [field]: value, [touchedField]: true };
      return next;
    });
  };

  /** ReactQuill fires onChange on mount/programmatic value sync too (source 'api'/'silent'), not
   * only on real typing (source 'user') — applying a translated value into the EN Quill editor
   * must not be mistaken for the user manually editing it, or it would permanently block further
   * auto-translate updates. Only forward genuine user edits to updateEnglishSection. */
  const updateEnglishSectionBodyFromQuill = (index: number, value: string, source: string) => {
    if (source !== 'user') return;
    updateEnglishSection(index, 'englishSectionBodyHtml', value);
  };

  const updateSectionImages = (index: number, updater: (prev: SectionImageItem[]) => SectionImageItem[]) => {
    setSections(prev => {
      const next = [...prev];
      next[index] = { ...next[index], sectionImages: updater(next[index].sectionImages) };
      return next;
    });
  };

  // ── Submit ─────────────────────────────────────────────────────────────────

  const handleSubmit = async () => {
    if (visitRequired && !selectedVisit) { toast.error('Vui lòng chọn chuyến tiếp khách trước.'); return; }
    if (!title.trim())    { toast.error('Tiêu đề không được để trống.'); return; }
    if (title.length > 150) { toast.error('Tiêu đề không được vượt quá 150 ký tự.'); return; }
    if (!summary.trim())  { toast.error('Mô tả ngắn không được để trống.'); return; }
    if (summary.length > 500) { toast.error('Mô tả ngắn không được vượt quá 500 ký tự.'); return; }
    for (const s of sections) {
      if (!s.sectionTitle.trim()) {
        toast.error(`Tiêu đề mục ${s.sectionOrder} không được để trống.`);
        return;
      }
      if (s.sectionTitle.length > 255) {
        toast.error(`Tiêu đề mục ${s.sectionOrder} không được vượt quá 255 ký tự.`);
        return;
      }
      const stripped = s.sectionBodyHtml.replace(/<[^>]+>/g, '').trim();
      if (!stripped) {
        toast.error(`Nội dung chi tiết mục ${s.sectionOrder} không được để trống.`);
        return;
      }
    }
    if (bilingual) {
      if (!englishTitle.trim()) { toast.error('Tiêu đề tiếng Anh không được để trống.'); return; }
      if (englishTitle.length > 150) { toast.error('Tiêu đề tiếng Anh không được vượt quá 150 ký tự.'); return; }
      if (englishSummary.length > 500) { toast.error('Mô tả ngắn (Anh) không được vượt quá 500 ký tự.'); return; }
      for (const s of sections) {
        if (!s.englishSectionTitle.trim()) {
          toast.error(`Tiêu đề tiếng Anh của mục ${s.sectionOrder} không được để trống.`);
          return;
        }
        if (s.englishSectionTitle.length > 255) {
          toast.error(`Tiêu đề tiếng Anh của mục ${s.sectionOrder} không được vượt quá 255 ký tự.`);
          return;
        }
        const strippedEn = s.englishSectionBodyHtml.replace(/<[^>]+>/g, '').trim();
        if (!strippedEn) {
          toast.error(`Nội dung tiếng Anh của mục ${s.sectionOrder} không được để trống.`);
          return;
        }
      }
    }
    if (coverUploading) { toast.error('Ảnh đại diện đang được tải lên, vui lòng chờ.'); return; }
    if (imagePreview !== null && coverFileId === null) {
      toast.error('Ảnh bìa chưa được tải lên thành công. Vui lòng chọn lại ảnh trước khi lưu.');
      return;
    }
    if (sections.some(s => s.sectionImages.some(img => img.uploading))) {
      toast.error('Ảnh nội dung đang được tải lên, vui lòng chờ.');
      return;
    }
    const brokenImageSection = sections.find(s => s.sectionImages.some(img => img.fileId === null));
    if (brokenImageSection) {
      toast.error(`Một số ảnh của mục ${brokenImageSection.sectionOrder} chưa tải lên thành công. Vui lòng chọn lại ảnh.`);
      return;
    }

    setSubmitting(true);
    try {
      const toSectionFiles = (images: SectionImageItem[]) =>
        images.filter(img => img.fileId).map((img, i) => ({
          fileId: img.fileId as number,
          usageType: 'INLINE_IMAGE',
          displayOrder: i + 1,
        }));

      const payload: Record<string, unknown> = {
        visitInstanceId: selectedVisit ? selectedVisit.visitInstanceId : null,
        coverFileId,
        title: title.trim(),
        summary: summary.trim(),
        contentSections: sections.map(s => ({
          sectionOrder:    s.sectionOrder,
          sectionTitle:    s.sectionTitle.trim(),
          sectionBodyHtml: s.sectionBodyHtml,
          sectionFiles:    toSectionFiles(s.sectionImages),
        })),
      };

      if (bilingual) {
        payload.englishTitle = englishTitle.trim();
        payload.englishSummary = englishSummary.trim();
        payload.englishContentSections = sections.map(s => ({
          sectionOrder:    s.sectionOrder,
          sectionTitle:    s.englishSectionTitle.trim(),
          sectionBodyHtml: s.englishSectionBodyHtml,
          sectionFiles:    toSectionFiles(s.sectionImages),
        }));
      }

      const { data } = await httpClient.post<{ success: boolean; message: string }>('/news', payload);

      if (data.success) {
        toast.success(data.message || 'Đã gửi bài viết, đang chờ Staff Leader duyệt.');
        setTimeout(() => navigate(returnTo || '/dashboard/news'), 1200);
      } else {
        toast.error(data.message ?? 'Không thể tạo tin tức.');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.message ?? 'Có lỗi xảy ra. Vui lòng thử lại.');
    } finally {
      setSubmitting(false);
    }
  };

  // Khóa form khi (bắt buộc chọn chuyến mà chưa chọn) hoặc user đã có bài cho chuyến preset.
  const visitStepDone = !visitRequired ? (selectedVisit !== null || skippedVisit) : selectedVisit !== null;
  const formDisabled = !visitStepDone || presetExistingNewsId !== null;

  // ─────────────────────────────────────────────────────────────────────────

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      transition={{ duration: 0.3 }}
      className="p-4 sm:p-6 md:p-8 pb-12 max-w-7xl mx-auto"
    >
      {/* Breadcrumb */}
      <div className="mb-6 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors">Dashboard</button>
        <span className="mx-2">/</span>
        <button onClick={() => navigate('/dashboard/news')} className="hover:text-[#004c91] transition-colors">Quản lý tin tức</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Tạo tin mới</span>
      </div>

      <div className="mb-8">
        <h1 className="text-3xl font-bold text-[#004c91]">Tạo tin tức</h1>
      </div>

      <div className="space-y-8">

        {/* ── Section 0: Chuyến tiếp khách ── */}
        <section className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
          <div className="bg-[#004c91] px-6 py-3.5">
            <h2 className="text-[15px] font-bold text-white uppercase tracking-wide">
              {presetVisitInstanceId ? '0. CHUYẾN TIẾP KHÁCH' : '0. CHỌN CHUYẾN SAU TIẾP KHÁCH'}
              {!visitRequired && !presetVisitInstanceId && <span className="ml-2 font-normal normal-case text-white/70">(tùy chọn)</span>}
            </h2>
          </div>
          <div className="p-6">
            {loadingVisits && presetVisitInstanceId && (
              <div className="flex items-center justify-center gap-2 py-8 text-gray-400">
                <div className="w-5 h-5 border-2 border-[#004c91] border-t-transparent rounded-full animate-spin" />
                <span>Đang tải thông tin chuyến tiếp khách...</span>
              </div>
            )}

            {presetError && !loadingVisits && (
              <div className="mb-4 p-4 bg-amber-50 border border-amber-300 rounded-xl text-sm font-semibold text-amber-800">
                {presetError}
              </div>
            )}

            {selectedVisit && (
              <div className="mb-4 p-4 bg-[#eef5fa] border border-[#b6d4f0] rounded-xl flex items-start justify-between gap-4">
                <div>
                  <div className="flex items-center gap-2 mb-1">
                    <CheckCircle2 className="w-5 h-5 text-[#0aa14f] flex-shrink-0" />
                    <span className="font-bold text-gray-800">{selectedVisit.visitTitle}</span>
                  </div>
                  <div className="flex flex-wrap gap-4 text-sm text-gray-600 mt-1 pl-7">
                    <span className="flex items-center gap-1"><MapPin className="w-3.5 h-3.5 text-gray-400" />{selectedVisit.campusName}</span>
                    <span className="flex items-center gap-1"><Calendar className="w-3.5 h-3.5 text-gray-400" />{formatDate(selectedVisit.plannedStartAt)} – {formatDate(selectedVisit.plannedEndAt)}</span>
                    <span className="px-2 py-0.5 bg-gray-100 text-gray-600 rounded-full text-xs font-bold">{instanceStatusLabel(selectedVisit.status)}</span>
                    {selectedVisit.hasNews
                      ? <span className="px-2 py-0.5 bg-yellow-50 text-yellow-700 rounded-full text-xs font-bold">Bạn đã có bài viết</span>
                      : <span className="px-2 py-0.5 bg-[#eaffe4] text-[#0aa14f] rounded-full text-xs font-bold">Bạn chưa có bài</span>
                    }
                  </div>
                </div>
                {/* Chuyến được chọn sẵn từ tab Sau tiếp khách/Đóng góp thì khóa, không cho đổi */}
                {!presetVisitInstanceId && (
                  <button
                    onClick={handleChangeVisit}
                    className="flex-shrink-0 px-3 py-1.5 text-sm font-bold text-[#004c91] border border-[#004c91] rounded-lg hover:bg-[#004c91] hover:text-white transition-colors"
                  >
                    Đổi chuyến
                  </button>
                )}
              </div>
            )}

            {skippedVisit && !presetVisitInstanceId && (
              <div className="mb-4 p-4 bg-gray-50 border border-gray-200 rounded-xl flex items-center justify-between gap-4">
                <span className="text-sm font-semibold text-gray-600">Tin tức này sẽ không gắn với chuyến tiếp khách nào.</span>
                <button
                  onClick={handleChangeVisit}
                  className="flex-shrink-0 px-3 py-1.5 text-sm font-bold text-[#004c91] border border-[#004c91] rounded-lg hover:bg-[#004c91] hover:text-white transition-colors"
                >
                  Chọn chuyến
                </button>
              </div>
            )}

            {showVisitList && !presetVisitInstanceId && (
              <>
                {loadingVisits ? (
                  <div className="flex items-center justify-center gap-2 py-8 text-gray-400">
                    <div className="w-5 h-5 border-2 border-[#004c91] border-t-transparent rounded-full animate-spin" />
                    <span>Đang tải danh sách chuyến tiếp khách...</span>
                  </div>
                ) : eligibleVisits.length === 0 ? (
                  <div className="py-10 text-center">
                    <p className="text-gray-700 font-bold mb-2">Bạn chưa có chuyến tiếp khách nào ở giai đoạn Sau tiếp khách để viết tin tức.</p>
                    <p className="text-gray-500 text-sm">Bạn chỉ có thể tạo tin tức cho chuyến tiếp khách mà bạn là Host hoặc đã xác nhận tham gia, từ giai đoạn Sau tiếp khách trở đi.</p>
                    {!visitRequired && (
                      <button
                        onClick={handleSkipVisit}
                        className="mt-4 px-4 py-2 bg-[#004c91] text-white text-sm font-bold rounded-lg hover:bg-[#003a70] transition-colors"
                      >
                        Tạo tin không gắn đoàn
                      </button>
                    )}
                  </div>
                ) : (
                  <div className="space-y-3">
                    <p className="text-sm text-gray-500 mb-3">Chọn một chuyến tiếp khách để tạo bài tin tức:</p>
                    {eligibleVisits.map(visit => (
                      <div
                        key={visit.visitInstanceId}
                        className={`border rounded-xl p-4 transition-all ${visit.canSelect ? 'border-gray-200 hover:border-[#004c91] hover:bg-[#f0f6fc] cursor-pointer' : 'border-gray-100 bg-gray-50 opacity-60 cursor-not-allowed'}`}
                        onClick={() => visit.canSelect && handleSelectVisit(visit)}
                      >
                        <div className="flex items-center justify-between gap-3">
                          <div className="min-w-0">
                            <div className="font-bold text-gray-800 text-sm truncate">{visit.visitTitle}</div>
                            <div className="flex flex-wrap gap-3 mt-1.5 text-xs text-gray-500">
                              <span className="flex items-center gap-1"><MapPin className="w-3 h-3" />{visit.campusName}</span>
                              <span className="flex items-center gap-1"><Calendar className="w-3 h-3" />{formatDate(visit.plannedStartAt)} – {formatDate(visit.plannedEndAt)}</span>
                              <span className="px-2 py-0.5 bg-gray-100 rounded-full font-bold">{instanceStatusLabel(visit.status)}</span>
                              {visit.hasNews
                                ? <span className="px-2 py-0.5 bg-yellow-50 text-yellow-700 rounded-full font-bold">Bạn đã có bài viết</span>
                                : <span className="px-2 py-0.5 bg-[#eaffe4] text-[#0aa14f] rounded-full font-bold">Bạn chưa có bài</span>
                              }
                            </div>
                          </div>
                          {visit.canSelect
                            ? <button className="flex-shrink-0 px-3 py-1.5 bg-[#004c91] text-white text-xs font-bold rounded-lg hover:bg-[#003a70] transition-colors">Chọn chuyến này</button>
                            : <span className="flex-shrink-0 px-3 py-1.5 bg-gray-200 text-gray-400 text-xs font-bold rounded-lg">Không thể chọn</span>
                          }
                        </div>
                      </div>
                    ))}
                    {!visitRequired && (
                      <button
                        onClick={handleSkipVisit}
                        className="w-full mt-2 px-4 py-2.5 border border-dashed border-gray-300 text-gray-500 text-sm font-bold rounded-xl hover:border-[#004c91] hover:text-[#004c91] transition-colors"
                      >
                        Không gắn với đoàn nào
                      </button>
                    )}
                  </div>
                )}
              </>
            )}

            {!selectedVisit && !skippedVisit && !loadingVisits && !presetError && visitRequired && (
              <p className="mt-4 text-sm text-amber-600 font-medium">
                ⚠ Vui lòng chọn chuyến tiếp khách trước khi tạo tin tức.
              </p>
            )}
          </div>
        </section>

        {/* ── Section 1: THÔNG TIN CƠ BẢN ── */}
        <CollapsibleSection
          title="1. THÔNG TIN CƠ BẢN"
          disabled={formDisabled}
          headerExtra={
            <>
              {bilingual && (
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
                onClick={() => setBilingual(v => !v)}
                title={bilingual ? 'Tắt soạn song ngữ' : 'Bật soạn song ngữ Việt – Anh'}
                className={`flex items-center justify-center w-8 h-8 rounded-lg transition-colors ${
                  bilingual ? 'bg-white text-[#004c91]' : 'bg-white/10 text-white hover:bg-white/20'
                }`}
              >
                <Languages className="w-4 h-4" />
              </button>
            </>
          }
        >
          <div className="flex flex-col gap-6">
            <BilingualColumns
              showEnglish={bilingual}
              left={
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <label className="block text-gray-900 font-bold">
                      Tiêu đề tin tức <span className="text-red-500">*</span>
                      {bilingual && <LanguageColumnLabel>VI</LanguageColumnLabel>}
                    </label>
                    <span className={`text-xs font-medium shrink-0 ml-2 ${title.length > 150 ? 'text-red-500' : 'text-gray-400'}`}>
                      {title.length}/150
                    </span>
                  </div>
                  <AutoGrowInput
                    placeholder="Nhập tiêu đề tin tức..."
                    value={title}
                    disabled={formDisabled}
                    onChange={setTitle}
                    className={`w-full px-4 py-3 border rounded-xl focus:outline-none focus:ring-1 transition-colors text-gray-800 disabled:bg-gray-50 disabled:cursor-not-allowed ${
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
                    <span className={`text-xs font-medium shrink-0 ml-2 ${englishTitle.length > 150 ? 'text-red-500' : 'text-gray-400'}`}>
                      {englishTitle.length}/150
                    </span>
                  </div>
                  <AutoGrowInput
                    placeholder="Enter news title..."
                    value={englishTitle}
                    disabled={formDisabled}
                    onChange={v => { setEnglishTitle(v); setEnglishTitleTouched(true); }}
                    className={`w-full px-4 py-3 border rounded-xl focus:outline-none focus:ring-1 transition-colors text-gray-800 disabled:bg-gray-50 disabled:cursor-not-allowed ${
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
              showEnglish={bilingual}
              left={
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <label className="block text-gray-900 font-bold">
                      Mô tả ngắn <span className="text-red-500">*</span>
                      {bilingual && <LanguageColumnLabel>VI</LanguageColumnLabel>}
                    </label>
                    <span className={`text-xs font-medium shrink-0 ml-2 ${summary.length > 500 ? 'text-red-500' : 'text-gray-400'}`}>
                      {summary.length}/500
                    </span>
                  </div>
                  <AutoGrowTextarea
                    placeholder="Nhập mô tả ngắn gọn..."
                    value={summary}
                    disabled={formDisabled}
                    onChange={setSummary}
                    className={`w-full p-4 border rounded-xl focus:outline-none focus:ring-1 transition-colors text-gray-800 disabled:bg-gray-50 disabled:cursor-not-allowed ${
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
                    <span className={`text-xs font-medium shrink-0 ml-2 ${englishSummary.length > 500 ? 'text-red-500' : 'text-gray-400'}`}>
                      {englishSummary.length}/500
                    </span>
                  </div>
                  <AutoGrowTextarea
                    placeholder="Enter a short summary..."
                    value={englishSummary}
                    disabled={formDisabled}
                    onChange={v => { setEnglishSummary(v); setEnglishSummaryTouched(true); }}
                    className={`w-full p-4 border rounded-xl focus:outline-none focus:ring-1 transition-colors text-gray-800 disabled:bg-gray-50 disabled:cursor-not-allowed ${
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

        {/* ── Section 2: ẢNH ĐẠI DIỆN ── */}
        <CollapsibleSection title="2. ẢNH ĐẠI DIỆN" disabled={formDisabled}>
            <label className={`block w-full cursor-pointer ${formDisabled ? 'pointer-events-none' : ''}`}>
              <input type="file" accept="image/png,image/jpeg,image/jpg,image/webp" className="hidden" onChange={handleImageUpload} disabled={formDisabled} />
              <div className={`relative bg-[#eef5fa] border-2 border-dashed border-[#b6d4f0] rounded-xl flex flex-col items-center justify-center text-center hover:bg-[#e4f0fa] transition-colors overflow-hidden ${imagePreview ? 'p-2' : 'p-12 min-h-[200px]'}`}>
                {imagePreview ? (
                  <div className="relative w-full">
                    <img src={imagePreview} alt="Preview" className="w-full max-h-[360px] object-contain rounded-lg" />
                    {coverUploading && (
                      <div className="absolute inset-0 flex items-center justify-center bg-black/30 rounded-lg">
                        <div className="flex items-center gap-2 text-white font-bold text-sm">
                          <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                          Đang tải lên...
                        </div>
                      </div>
                    )}
                  </div>
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
            {imagePreview && !coverUploading && (
              <p className="mt-2 text-xs text-gray-400 text-center">Click vào ảnh để thay ảnh mới</p>
            )}
        </CollapsibleSection>

        {/* ── Section 3: NỘI DUNG CHI TIẾT ── */}
        <CollapsibleSection title="3. NỘI DUNG CHI TIẾT" disabled={formDisabled}>
          <div className={`flex flex-col gap-8 ${formDisabled ? 'pointer-events-none' : ''}`}>
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
                          onClick={() => setSectionToDelete(index)}
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
                    showEnglish={bilingual}
                    left={
                      <div>
                        <div className="flex items-center justify-between mb-1.5">
                          <label className="block text-gray-900 font-bold text-sm">
                            Tiêu đề mục <span className="text-red-500">*</span>
                            {bilingual && <LanguageColumnLabel>VI</LanguageColumnLabel>}
                          </label>
                          <span className={`text-xs font-medium shrink-0 ml-2 ${section.sectionTitle.length > 255 ? 'text-red-500' : 'text-gray-400'}`}>
                            {section.sectionTitle.length}/255
                          </span>
                        </div>
                        <AutoGrowInput
                          placeholder="Nhập tiêu đề mục nội dung..."
                          value={section.sectionTitle}
                          onChange={v => updateSection(index, 'sectionTitle', v)}
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
                          <span className={`text-xs font-medium shrink-0 ml-2 ${section.englishSectionTitle.length > 255 ? 'text-red-500' : 'text-gray-400'}`}>
                            {section.englishSectionTitle.length}/255
                          </span>
                        </div>
                        <AutoGrowInput
                          placeholder="Enter section title..."
                          value={section.englishSectionTitle}
                          onChange={v => updateEnglishSection(index, 'englishSectionTitle', v)}
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
                    showEnglish={bilingual}
                    left={
                      <div>
                        <label className="block text-gray-900 font-bold mb-2">
                          Nội dung <span className="text-red-500">*</span>
                          {bilingual && <LanguageColumnLabel>VI</LanguageColumnLabel>}
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

                  <SectionImagesEditor
                    images={section.sectionImages}
                    onChange={updater => updateSectionImages(index, updater)}
                    uploadEndpoint="/news/section-file-upload"
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
        <div className="flex items-center justify-between gap-4 pt-6 border-t border-gray-200">
          <button
            onClick={() => navigate(returnTo || '/dashboard/news')}
            className="flex items-center gap-1.5 px-6 py-2.5 rounded-xl border border-gray-300 text-gray-700 font-bold hover:border-[#004c91] hover:text-[#004c91] transition-colors"
          >
            <ArrowLeft className="w-4 h-4" /> Quay lại
          </button>
          <button
            onClick={handleSubmit}
            disabled={formDisabled || submitting || coverUploading}
            className="px-8 py-2.5 text-white bg-[#f37021] rounded-xl font-bold hover:-translate-y-1 hover:shadow-lg transition-all duration-300 disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:translate-y-0 flex items-center gap-2"
          >
            {submitting && <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />}
            Gửi duyệt
          </button>
        </div>
      </div>

      {/* Delete Section Confirmation Modal */}
      {sectionToDelete !== null && (
        <div className="fixed inset-0 z-50 flex items-center justify-center">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setSectionToDelete(null)} />
          <motion.div
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            className="bg-white rounded-2xl p-6 max-w-md w-full mx-4 relative z-10 shadow-xl"
          >
            <h3 className="text-xl font-bold text-gray-900 mb-2">Xác nhận xóa</h3>
            <p className="text-gray-600 mb-6 leading-relaxed">
              Bạn có chắc chắn muốn xóa <strong>Mục {sectionToDelete + 1}</strong> không? Dữ liệu đã nhập sẽ mất.
            </p>
            <div className="flex gap-3 justify-end">
              <button onClick={() => setSectionToDelete(null)} className="px-5 py-2 rounded-xl text-gray-600 font-bold hover:bg-gray-100 transition-colors">
                Hủy
              </button>
              <button onClick={confirmDeleteSection} className="px-5 py-2 rounded-xl bg-red-600 text-white font-bold hover:bg-red-700 transition-colors shadow-sm">
                Xác nhận xóa
              </button>
            </div>
          </motion.div>
        </div>
      )}
    </motion.div>
  );
}
