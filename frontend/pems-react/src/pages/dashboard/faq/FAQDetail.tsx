import React, { useEffect, useState } from 'react';
import {
  HelpCircle, ChevronLeft, MessageCircle, Info,
  Edit2, Save, X, Loader2, AlertCircle, Languages, RotateCw,
} from 'lucide-react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion } from 'motion/react';
import toast from 'react-hot-toast';
import httpClient from '../../../shared/api/httpClient';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import { useFaqBilingualTranslate } from './useFaqBilingualTranslate';
import { BilingualColumns, LanguageColumnLabel } from '../news/components/BilingualColumns';

const FAQ_TYPE_OPTIONS = [
  { value: 'ACCOUNT_ACCESS',       label: 'Tài khoản & truy cập' },
  { value: 'VISIT_REQUEST',        label: 'Đăng ký tham quan' },
  { value: 'DELEGATION_MANAGEMENT',label: 'Quản lý đoàn' },
  { value: 'LOGISTICS_RESOURCE',   label: 'Hậu cần' },
  { value: 'DOCUMENT_MEDIA',       label: 'Tài liệu' },
  { value: 'NOTIFICATION_EMAIL',   label: 'Thông báo' },
  { value: 'OTHER',                label: 'Khác' },
];

interface FaqDetail {
  faqId: number;
  faqType: string;
  faqTypeLabel: string;
  question: string;
  answer: string;
  englishQuestion?: string | null;
  englishAnswer?: string | null;
  hasEnglishTranslation?: boolean;
  displayOrder: number;
  status: string;
  statusLabel: string;
  createdAt: string;
  createdBy?: number;
  createdByName?: string;
  updatedAt?: string;
  updatedBy?: number;
  updatedByName?: string;
  changed?: boolean;
}

export function FAQDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const isHO = user?.role?.toUpperCase() === 'HO';

  const [faq, setFaq] = useState<FaqDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState<string | null>(null);

  const [viewLang, setViewLang] = useState<'vi' | 'en'>('vi');

  const [isEditing, setIsEditing] = useState(false);
  const [editForm, setEditForm] = useState({ question: '', answer: '' });

  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  // Bilingual editor. `hadEnglishAtEditStart` freezes whether an EN translation already existed
  // when THIS edit session began — the EN column is shown immediately (both translations loaded
  // from the DB) without auto-translating; `addingEnglish` is only for a FAQ that has no EN yet
  // (clicking the toggle then translates once). Either way "Dịch lại" is always available so a
  // hand-edited EN can be explicitly overwritten on request.
  const [hadEnglishAtEditStart, setHadEnglishAtEditStart] = useState(false);
  const [addingEnglish, setAddingEnglish] = useState(false);
  const showEnglishColumn = hadEnglishAtEditStart || addingEnglish;
  const [englishQuestion, setEnglishQuestion] = useState('');
  const [englishAnswer, setEnglishAnswer] = useState('');
  const [englishQuestionTouched, setEnglishQuestionTouched] = useState(false);
  const [englishAnswerTouched, setEnglishAnswerTouched] = useState(false);

  const { translating: translatingFaq, retranslateNow: retranslateFaq } = useFaqBilingualTranslate({
    enabled: addingEnglish && !hadEnglishAtEditStart,
    question: editForm.question,
    answer: editForm.answer,
    onTranslated: (result) => {
      if (!englishQuestionTouched) setEnglishQuestion(result.question);
      if (!englishAnswerTouched) setEnglishAnswer(result.answer);
    },
  });

  async function handleRetranslateFaq() {
    setEnglishQuestionTouched(false);
    setEnglishAnswerTouched(false);
    const ok = await retranslateFaq();
    if (ok) toast.success('Đã dịch lại sang tiếng Anh.');
    else toast.error('Không thể dịch tự động. Vui lòng thử lại.');
  }

  useEffect(() => {
    window.scrollTo(0, 0);
    if (!id) return;

    let cancelled = false;
    const fetch = async () => {
      setLoading(true);
      setFetchError(null);
      try {
        const { data } = await httpClient.get<FaqDetail>(`/faqs/${id}`);
        if (!cancelled) setFaq(data);
      } catch (err: any) {
        if (!cancelled)
          setFetchError(err?.response?.data?.message || 'Không thể tải dữ liệu FAQ.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    fetch();
    return () => { cancelled = true; };
  }, [id]);

  const handleEdit = () => {
    if (!faq) return;
    setEditForm({ question: faq.question, answer: faq.answer });
    setHadEnglishAtEditStart(!!faq.hasEnglishTranslation);
    setAddingEnglish(false);
    setEnglishQuestion(faq.englishQuestion ?? '');
    setEnglishAnswer(faq.englishAnswer ?? '');
    setEnglishQuestionTouched(false);
    setEnglishAnswerTouched(false);
    setSaveError(null);
    setIsEditing(true);
  };

  const handleCancel = () => {
    setIsEditing(false);
    setSaveError(null);
  };

  const handleSave = async () => {
    if (!faq || !id) return;
    setIsSaving(true);
    setSaveError(null);
    try {
      const { data } = await httpClient.put<FaqDetail>(`/faqs/${id}`, {
        question: editForm.question,
        answer: editForm.answer,
        // Omitted entirely when the EN panel was never opened for a FAQ that has no EN yet — the
        // backend then auto-translates once and stores the result, same rule as create.
        ...(showEnglishColumn
          ? { englishQuestion: englishQuestion.trim(), englishAnswer: englishAnswer.trim() }
          : {}),
      });
      setFaq(data);
      setIsEditing(false);
      toast.success((data as any).changed ? 'Cập nhật thành công!' : 'Không có thay đổi nào.', {
        duration: 3000,
      });
    } catch (err: any) {
      setSaveError(err?.response?.data?.message || 'Lưu thất bại. Vui lòng thử lại.');
    } finally {
      setIsSaving(false);
    }
  };

  // Giờ Việt Nam cố định (API trả ISO +07:00) — không phụ thuộc timezone trình duyệt.
  const formatDate = (dateStr?: string) => formatVietnamDateTime(dateStr);

  const getStatusBadge = (status: string) =>
    status === 'PUBLISHED'
      ? <span className="inline-block px-3 py-1 bg-[#eaffe4] text-[#0aa14f] font-bold rounded-full text-xs border border-[#ceefda]">Hiển thị</span>
      : <span className="inline-block px-3 py-1 bg-gray-100 text-gray-600 font-bold rounded-full text-xs border border-gray-200">Ẩn</span>;

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <Loader2 className="w-8 h-8 animate-spin text-[#004c91]" />
      </div>
    );
  }

  if (fetchError || !faq) {
    return (
      <div className="p-8 text-center">
        <AlertCircle className="w-12 h-12 text-red-400 mx-auto mb-3" />
        <p className="text-gray-600 mb-4">{fetchError || 'Không tìm thấy FAQ.'}</p>
        <button onClick={() => navigate('/dashboard/faq')} className="px-6 py-2.5 bg-[#004c91] text-white rounded-xl font-bold">
          Quay lại
        </button>
      </div>
    );
  }

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      transition={{ duration: 0.3 }}
      className="p-4 md:p-8 space-y-6 bg-gray-50/50 min-h-screen"
    >
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <span className="hover:text-[#004c91] cursor-pointer transition-colors" onClick={() => navigate('/dashboard')}>Dashboard</span>
        <span>/</span>
        <span className="hover:text-[#004c91] cursor-pointer transition-colors" onClick={() => navigate('/dashboard/faq')}>Quản lý FAQ</span>
        <span>/</span>
        <span className="text-[#004c91] font-medium">Chi tiết FAQ</span>
      </div>

      <div className="border-b border-gray-100 pb-4">
        <h1 className="text-3xl font-bold text-[#004c91]">Chi tiết FAQ</h1>
      </div>

      {saveError && (
        <div className="flex items-center gap-2 px-4 py-3 bg-red-50 border border-red-200 rounded-xl text-red-700 font-medium text-sm">
          <AlertCircle className="w-4 h-4 flex-shrink-0" />
          {saveError}
        </div>
      )}

      <div className="bg-white rounded-3xl shadow-sm border border-gray-100 overflow-hidden">
        {/* Header: Type + Question */}
        <div className="bg-[#004c91] p-8 md:p-10 relative overflow-hidden">
          <div className="absolute top-0 right-0 p-8 opacity-10 pointer-events-none transform translate-x-4 -translate-y-4">
            <HelpCircle className="w-48 h-48 text-white" />
          </div>

          <div className="relative z-10 flex flex-col gap-4">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                <span className="inline-flex items-center gap-1.5 px-3 py-1 bg-white rounded-full text-xs font-bold text-[#004c91] shadow-sm">
                  <Info className="w-3.5 h-3.5" />
                  {faq.faqTypeLabel}
                </span>
                {getStatusBadge(faq.status)}
              </div>

              {!isEditing && (
                <div className="flex items-center gap-3">
                  <div className="flex items-center bg-white/10 p-1 rounded-xl border border-white/20">
                    <button
                      type="button"
                      onClick={() => setViewLang('vi')}
                      className={`px-3 py-1 rounded-lg text-xs font-bold transition-all ${
                        viewLang === 'vi' ? 'bg-white text-[#004c91] shadow-sm' : 'text-white/80 hover:text-white'
                      }`}
                    >
                      Tiếng Việt
                    </button>
                    <button
                      type="button"
                      onClick={() => setViewLang('en')}
                      className={`px-3 py-1 rounded-lg text-xs font-bold transition-all ${
                        viewLang === 'en' ? 'bg-white text-[#004c91] shadow-sm' : 'text-white/80 hover:text-white'
                      }`}
                    >
                      English
                    </button>
                  </div>
                  {isHO && (
                    <button
                      onClick={handleEdit}
                      className="flex items-center gap-2 px-4 py-2 bg-white text-[#004c91] font-bold rounded-xl hover:bg-orange-50 hover:text-[#f37021] transition-all cursor-pointer shadow-sm"
                      title="Chỉnh sửa"
                    >
                      <Edit2 className="w-4 h-4" />
                      <span className="text-sm">Chỉnh sửa</span>
                    </button>
                  )}
                </div>
              )}

              {isEditing && (
                <div className="flex items-center gap-2">
                  {showEnglishColumn && (
                    <button
                      type="button"
                      onClick={handleRetranslateFaq}
                      disabled={translatingFaq}
                      title="Dịch lại từ tiếng Việt"
                      className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs font-bold text-white/80 hover:bg-white/10 disabled:opacity-50"
                    >
                      <RotateCw className={`w-3.5 h-3.5 ${translatingFaq ? 'animate-spin' : ''}`} />
                      {translatingFaq ? 'Đang dịch...' : 'Dịch lại'}
                    </button>
                  )}
                  <button
                    type="button"
                    onClick={() => { if (!hadEnglishAtEditStart) setAddingEnglish(v => !v); }}
                    title={
                      hadEnglishAtEditStart
                        ? 'FAQ đã có bản dịch tiếng Anh'
                        : addingEnglish ? 'Tắt soạn song ngữ' : 'Bật soạn song ngữ Việt – Anh'
                    }
                    className={`p-1.5 rounded-lg transition-colors ${
                      showEnglishColumn ? 'bg-white text-[#004c91]' : 'bg-white/10 text-white hover:bg-white/20'
                    } ${hadEnglishAtEditStart ? 'cursor-default' : ''}`}
                  >
                    <Languages className="w-4 h-4" />
                  </button>
                </div>
              )}
            </div>

            <BilingualColumns
              showEnglish={isEditing && showEnglishColumn}
              left={
                isEditing ? (
                  <textarea
                    value={editForm.question}
                    onChange={(e) => setEditForm({ ...editForm, question: e.target.value })}
                    className="w-full text-2xl md:text-3xl font-normal text-white bg-transparent border border-white/30 focus:border-white focus:bg-white/10 p-3 rounded-2xl outline-none transition-all resize-none placeholder:text-white/50"
                    rows={2}
                    maxLength={500}
                    placeholder="Nhập câu hỏi..."
                  />
                ) : (
                  <h2 className="text-2xl md:text-3xl font-bold text-white leading-snug">
                    {viewLang === 'en' ? (faq.englishQuestion || faq.question) : faq.question}
                  </h2>
                )
              }
              right={
                <textarea
                  value={englishQuestion}
                  onChange={(e) => { setEnglishQuestion(e.target.value); setEnglishQuestionTouched(true); }}
                  className="w-full text-2xl md:text-3xl font-normal text-white bg-transparent border border-white/30 focus:border-white focus:bg-white/10 p-3 rounded-2xl outline-none transition-all resize-none placeholder:text-white/50"
                  rows={2}
                  maxLength={500}
                  placeholder="Question (English)..."
                />
              }
            />
          </div>
        </div>

        {/* Answer */}
        <div className="p-6 md:p-10">
          <h3 className="text-xs font-bold tracking-widest text-[#f37021] uppercase mb-4 flex items-center gap-2">
            <MessageCircle className="w-4 h-4" />
            Câu trả lời {viewLang === 'en' ? '(English)' : '(Tiếng Việt)'}
          </h3>
          <div className="bg-[#e6eff7] rounded-2xl p-6 md:p-8 border border-blue-100/50 min-h-[200px]">
            <BilingualColumns
              showEnglish={isEditing && showEnglishColumn}
              left={
                isEditing ? (
                  <textarea
                    value={editForm.answer}
                    onChange={(e) => setEditForm({ ...editForm, answer: e.target.value })}
                    className="w-full text-gray-900 leading-relaxed text-[15px] p-4 bg-white/80 focus:bg-white border border-blue-200 rounded-xl outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all resize-none min-h-[180px]"
                    placeholder="Nhập câu trả lời..."
                  />
                ) : (
                  <p className="text-gray-700 leading-relaxed text-[15px] whitespace-pre-line font-medium">
                    {viewLang === 'en' ? (faq.englishAnswer || faq.answer) : faq.answer}
                  </p>
                )
              }

              right={
                <div className="space-y-1.5">
                  <label className="text-xs font-bold text-gray-500 uppercase tracking-wider block">
                    Answer <LanguageColumnLabel>EN</LanguageColumnLabel>
                  </label>
                  <textarea
                    value={englishAnswer}
                    onChange={(e) => { setEnglishAnswer(e.target.value); setEnglishAnswerTouched(true); }}
                    className="w-full text-gray-900 leading-relaxed text-[15px] p-4 bg-white/80 focus:bg-white border border-blue-200 rounded-xl outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all resize-none min-h-[180px]"
                    placeholder="Answer (English)..."
                  />
                </div>
              }
            />
          </div>

          {/* Metadata */}
          <div className="mt-6 grid grid-cols-1 sm:grid-cols-2 gap-4 p-5 bg-gray-50 rounded-2xl border border-gray-100">
            <div>
              <p className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-1">Người tạo</p>
              <p className="text-sm font-semibold text-gray-700">{faq.createdByName || '—'}</p>
            </div>
            <div>
              <p className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-1">Ngày tạo</p>
              <p className="text-sm font-semibold text-gray-700">{formatDate(faq.createdAt)}</p>
            </div>
            <div>
              <p className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-1">Người cập nhật</p>
              <p className="text-sm font-semibold text-gray-700">{faq.updatedByName || '—'}</p>
            </div>
            <div>
              <p className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-1">Ngày cập nhật</p>
              <p className="text-sm font-semibold text-gray-700">{formatDate(faq.updatedAt)}</p>
            </div>
          </div>

          {/* Actions */}
          <div className="mt-6 flex items-center justify-end gap-3 pt-6 border-t border-gray-100">
            {!isEditing ? (
              <button
                onClick={() => navigate('/dashboard/faq')}
                className="flex items-center gap-2 px-6 py-2.5 bg-white border border-gray-200 text-gray-700 font-bold rounded-xl hover:bg-gray-50 hover:text-[#004c91] transition-colors shadow-sm cursor-pointer"
              >
                <ChevronLeft className="w-4 h-4" />
                Quay lại
              </button>
            ) : (
              <>
                <button
                  onClick={handleCancel}
                  disabled={isSaving}
                  className="flex items-center gap-2 px-6 py-2.5 bg-white border border-gray-200 text-gray-700 font-bold rounded-xl hover:bg-gray-50 transition-colors shadow-sm cursor-pointer disabled:opacity-50"
                >
                  <X className="w-4 h-4" />
                  Hủy
                </button>
                <button
                  onClick={handleSave}
                  disabled={isSaving}
                  className="flex items-center gap-2 px-6 py-2.5 bg-[#004c91] text-white font-bold rounded-xl hover:bg-[#003366] transition-colors shadow-sm cursor-pointer disabled:opacity-50"
                >
                  {isSaving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                  {isSaving ? 'Đang lưu...' : 'Lưu thay đổi'}
                </button>
              </>
            )}
          </div>
        </div>
      </div>
    </motion.div>
  );
}
