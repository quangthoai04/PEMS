import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'motion/react';
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';
import { UploadCloud, Plus, Trash2, ArrowLeft, ImagePlus, X, Calendar, MapPin, CheckCircle2 } from 'lucide-react';
import toast from 'react-hot-toast';
import httpClient from '../../../shared/api/httpClient';
import { uploadFileToEndpoint } from '../../../shared/api/fileUploadApi';
import { validateFile } from '../../../shared/utils/fileValidation';

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

interface SectionImage {
  fileId: number | null;   // set after the Drive upload succeeds
  previewUrl: string;      // local object URL for instant preview
  uploading: boolean;
}

interface ContentSection {
  id: number;
  sectionOrder: number;
  sectionTitle: string;
  sectionBodyHtml: string;
  sectionImage: SectionImage | null;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function formatDate(dateStr?: string): string {
  if (!dateStr) return '—';
  const fixed = dateStr.endsWith('Z') ? dateStr : dateStr + 'Z';
  return new Date(fixed).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

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

// ─── Component ────────────────────────────────────────────────────────────────

export function CreateNews() {
  const navigate = useNavigate();

  // Step 0: Eligible visit instances
  const [eligibleVisits, setEligibleVisits]   = useState<EligibleVisit[]>([]);
  const [loadingVisits, setLoadingVisits]     = useState(true);
  const [selectedVisit, setSelectedVisit]     = useState<EligibleVisit | null>(null);
  const [showVisitList, setShowVisitList]     = useState(true);

  // Step 1: Basic info
  const [title,   setTitle]   = useState('');
  const [summary, setSummary] = useState('');

  // Step 2: Cover image
  const [imagePreview,    setImagePreview]    = useState<string | null>(null);
  const [coverFileId,     setCoverFileId]     = useState<number | null>(null);
  const [coverUploading,  setCoverUploading]  = useState(false);

  // Step 3: Content sections
  const [sections, setSections] = useState<ContentSection[]>([
    { id: 1, sectionOrder: 1, sectionTitle: '', sectionBodyHtml: '', sectionImage: null },
  ]);
  const [sectionToDelete, setSectionToDelete] = useState<number | null>(null);

  // Submit state
  const [submitting, setSubmitting] = useState(false);

  // ── Load eligible visits ───────────────────────────────────────────────────

  useEffect(() => {
    let cancelled = false;
    const fetchVisits = async () => {
      setLoadingVisits(true);
      try {
        const { data } = await httpClient.get<{ items: EligibleVisit[] }>(
          '/news/eligible-visit-instances',
          { params: { includeAlreadyHasNews: false } }
        );
        if (!cancelled) setEligibleVisits(data.items ?? []);
      } catch {
        if (!cancelled) setEligibleVisits([]);
      } finally {
        if (!cancelled) setLoadingVisits(false);
      }
    };
    fetchVisits();
    return () => { cancelled = true; };
  }, []);

  // ── Visit selection ────────────────────────────────────────────────────────

  const handleSelectVisit = (visit: EligibleVisit) => {
    if (!visit.canSelect) return;
    setSelectedVisit(visit);
    setShowVisitList(false);
  };

  const handleChangeVisit = () => {
    setSelectedVisit(null);
    setShowVisitList(true);
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
          ? { ...s, sectionImage: { ...s.sectionImage, fileId: uploaded.fileId, uploading: false } }
          : s
      ));
    } catch (err: any) {
      toast.error(err?.response?.data?.message ?? 'Không thể tải ảnh nội dung lên.');
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

  // ── Section CRUD ───────────────────────────────────────────────────────────

  const addSection = () => {
    if (sections.length >= 10) return;
    setSections(prev => [
      ...prev,
      { id: Date.now(), sectionOrder: prev.length + 1, sectionTitle: '', sectionBodyHtml: '', sectionImage: null },
    ]);
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

  // ── Submit ─────────────────────────────────────────────────────────────────

  const handleSubmit = async () => {
    if (!selectedVisit)   { toast.error('Vui lòng chọn chuyến tiếp khách trước.'); return; }
    if (!title.trim())    { toast.error('Tiêu đề không được để trống.'); return; }
    if (title.length > 150) { toast.error('Tiêu đề không được vượt quá 150 ký tự.'); return; }
    if (!summary.trim())  { toast.error('Mô tả ngắn không được để trống.'); return; }
    for (const s of sections) {
      if (!s.sectionTitle.trim()) {
        toast.error(`Tiêu đề mục ${s.sectionOrder} không được để trống.`);
        return;
      }
      const stripped = s.sectionBodyHtml.replace(/<[^>]+>/g, '').trim();
      if (!stripped) {
        toast.error(`Nội dung chi tiết mục ${s.sectionOrder} không được để trống.`);
        return;
      }
    }
    if (coverUploading) { toast.error('Ảnh đại diện đang được tải lên, vui lòng chờ.'); return; }
    if (imagePreview !== null && coverFileId === null) {
      toast.error('Ảnh bìa chưa được tải lên thành công. Vui lòng chọn lại ảnh trước khi lưu.');
      return;
    }
    if (sections.some(s => s.sectionImage?.uploading)) {
      toast.error('Ảnh nội dung đang được tải lên, vui lòng chờ.');
      return;
    }
    const brokenImage = sections.find(s => s.sectionImage && s.sectionImage.fileId === null);
    if (brokenImage) {
      toast.error(`Ảnh của mục ${brokenImage.sectionOrder} chưa tải lên thành công. Vui lòng chọn lại ảnh.`);
      return;
    }

    setSubmitting(true);
    try {
      const payload = {
        visitInstanceId: selectedVisit.visitInstanceId,
        coverFileId,
        title: title.trim(),
        summary: summary.trim(),
        contentSections: sections.map(s => ({
          sectionOrder:    s.sectionOrder,
          sectionTitle:    s.sectionTitle.trim(),
          sectionBodyHtml: s.sectionBodyHtml,
          sectionFiles:    s.sectionImage?.fileId
            ? [{ fileId: s.sectionImage.fileId, usageType: 'INLINE_IMAGE', displayOrder: 1 }]
            : [],
        })),
      };

      const { data } = await httpClient.post<{ success: boolean; message: string }>('/news', payload);

      if (data.success) {
        toast.success('Tạo tin tức thành công! Bài viết đang chờ duyệt.');
        setTimeout(() => navigate('/dashboard/news'), 1200);
      } else {
        toast.error(data.message ?? 'Không thể tạo tin tức.');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.message ?? 'Có lỗi xảy ra. Vui lòng thử lại.');
    } finally {
      setSubmitting(false);
    }
  };

  const formDisabled = !selectedVisit;

  // ─────────────────────────────────────────────────────────────────────────

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      transition={{ duration: 0.3 }}
      className="p-4 sm:p-6 md:p-8 pb-12 max-w-5xl mx-auto"
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

        {/* ── Section 0: Chọn chuyến tiếp khách ── */}
        <section className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
          <div className="bg-[#004c91] px-6 py-3.5">
            <h2 className="text-[15px] font-bold text-white uppercase tracking-wide">0. CHỌN CHUYẾN TIẾP KHÁCH ĐÃ ĐÓNG</h2>
          </div>
          <div className="p-6">
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
                    <span className="px-2 py-0.5 bg-gray-100 text-gray-600 rounded-full text-xs font-bold">Đã đóng đoàn</span>
                    <span className="px-2 py-0.5 bg-[#eaffe4] text-[#0aa14f] rounded-full text-xs font-bold">Chưa có bài</span>
                  </div>
                </div>
                <button
                  onClick={handleChangeVisit}
                  className="flex-shrink-0 px-3 py-1.5 text-sm font-bold text-[#004c91] border border-[#004c91] rounded-lg hover:bg-[#004c91] hover:text-white transition-colors"
                >
                  Đổi chuyến
                </button>
              </div>
            )}

            {showVisitList && (
              <>
                {loadingVisits ? (
                  <div className="flex items-center justify-center gap-2 py-8 text-gray-400">
                    <div className="w-5 h-5 border-2 border-[#004c91] border-t-transparent rounded-full animate-spin" />
                    <span>Đang tải danh sách chuyến tiếp khách...</span>
                  </div>
                ) : eligibleVisits.length === 0 ? (
                  <div className="py-10 text-center">
                    <p className="text-gray-700 font-bold mb-2">Bạn chưa có chuyến tiếp khách đã đóng để viết tin tức.</p>
                    <p className="text-gray-500 text-sm">Bạn chỉ có thể tạo tin tức cho chuyến tiếp khách mà bạn đã xác nhận tham gia và đã được đóng đoàn.</p>
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
                              <span className="px-2 py-0.5 bg-gray-100 rounded-full font-bold">Đã đóng đoàn</span>
                              {visit.hasNews
                                ? <span className="px-2 py-0.5 bg-yellow-50 text-yellow-700 rounded-full font-bold">Đã có bài viết</span>
                                : <span className="px-2 py-0.5 bg-[#eaffe4] text-[#0aa14f] rounded-full font-bold">Chưa có bài</span>
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
                  </div>
                )}
              </>
            )}

            {!selectedVisit && !loadingVisits && (
              <p className="mt-4 text-sm text-amber-600 font-medium">
                ⚠ Vui lòng chọn chuyến tiếp khách đã đóng trước khi tạo tin tức.
              </p>
            )}
          </div>
        </section>

        {/* ── Section 1: THÔNG TIN CƠ BẢN ── */}
        <section className={`bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden transition-opacity ${formDisabled ? 'opacity-50' : ''}`}>
          <div className="bg-[#004c91] px-6 py-3.5">
            <h2 className="text-[15px] font-bold text-white uppercase tracking-wide">1. THÔNG TIN CƠ BẢN</h2>
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
                  placeholder="Nhập tiêu đề tin tức..."
                  value={title}
                  disabled={formDisabled}
                  onChange={e => setTitle(e.target.value)}
                  className="w-full pr-16 pl-4 py-3 border border-gray-300 rounded-xl focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] hover:border-[#004c91] transition-colors text-gray-800 disabled:bg-gray-50 disabled:cursor-not-allowed"
                />
                <span className="absolute right-4 top-1/2 -translate-y-1/2 text-sm text-gray-400 font-medium">{title.length}/150</span>
              </div>
            </div>

            <div>
              <label className="block text-gray-900 font-bold mb-2">
                Mô tả ngắn <span className="text-red-500">*</span>
              </label>
              <textarea
                rows={3}
                placeholder="Nhập mô tả ngắn gọn..."
                value={summary}
                disabled={formDisabled}
                onChange={e => setSummary(e.target.value)}
                className="w-full p-4 border border-gray-300 rounded-xl focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] hover:border-[#004c91] transition-colors text-gray-800 resize-none disabled:bg-gray-50 disabled:cursor-not-allowed"
              />
            </div>
          </div>
        </section>

        {/* ── Section 2: ẢNH ĐẠI DIỆN ── */}
        <section className={`bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden transition-opacity ${formDisabled ? 'opacity-50' : ''}`}>
          <div className="bg-[#004c91] px-6 py-3.5">
            <h2 className="text-[15px] font-bold text-white uppercase tracking-wide">2. ẢNH ĐẠI DIỆN</h2>
          </div>
          <div className="p-6">
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
          </div>
        </section>

        {/* ── Section 3: NỘI DUNG CHI TIẾT ── */}
        <section className={`bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden transition-opacity ${formDisabled ? 'opacity-50' : ''}`}>
          <div className="bg-[#004c91] px-6 py-3.5">
            <h2 className="text-[15px] font-bold text-white uppercase tracking-wide">3. NỘI DUNG CHI TIẾT</h2>
          </div>

          <div className={`p-6 flex flex-col gap-8 ${formDisabled ? 'pointer-events-none' : ''}`}>
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
                    <input
                      type="text"
                      placeholder="Nhập tiêu đề mục nội dung..."
                      value={section.sectionTitle}
                      onChange={e => updateSection(index, 'sectionTitle', e.target.value)}
                      className="w-full px-4 py-3 border border-gray-300 rounded-xl focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] hover:border-[#004c91] transition-colors text-gray-800"
                    />
                  </div>

                  {/* Text body — no image button in toolbar */}
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
                    <label className="block text-gray-900 font-bold mb-2">Hình ảnh</label>

                    {section.sectionImage ? (
                      <div className="relative rounded-xl overflow-hidden border border-gray-200 bg-gray-50">
                        <img
                          src={section.sectionImage.previewUrl}
                          alt={`Ảnh mục ${index + 1}`}
                          className="w-full h-auto object-contain rounded-lg"
                        />
                        {section.sectionImage.uploading && (
                          <div className="absolute inset-0 flex items-center justify-center bg-black/30 rounded-lg">
                            <div className="flex items-center gap-2 text-white font-bold text-sm">
                              <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                              Đang tải lên...
                            </div>
                          </div>
                        )}
                        <button
                          type="button"
                          onClick={() => removeSectionImage(index)}
                          className="absolute top-2 right-2 w-8 h-8 bg-red-500 hover:bg-red-600 text-white rounded-full flex items-center justify-center shadow-lg transition-colors"
                          title="Xóa ảnh"
                        >
                          <X className="w-4 h-4" />
                        </button>
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
                            Thêm hình ảnh (tùy chọn — tối đa 1 ảnh mỗi mục)
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
        <div className="flex items-center justify-between gap-4 pt-6 border-t border-gray-200">
          <button
            onClick={() => navigate('/dashboard/news')}
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
