import React, { useState, useEffect } from 'react';
import { Calendar, User, Clock, CheckCircle, XCircle, EyeOff, Eye, ArrowLeft, Edit2, Globe, Languages, X } from 'lucide-react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'motion/react';
import toast from 'react-hot-toast';
import { sanitizeHtml } from '../../../shared/security/sanitizeHtml';
import httpClient from '../../../shared/api/httpClient';
import { useAuthenticatedImage } from '../../../shared/hooks/useAuthenticatedImage';
import { useAuth } from '../../../shared/hooks/useAuth';

// ─── Types ───────────────────────────────────────────────────────────────────

interface SectionFile {
  sectionFileId: number;
  fileId: number;
  url?: string;
  thumbnailUrl?: string;
  fileName?: string;
  mimeType?: string;
  usageType: string;
  displayOrder: number;
}

interface Section {
  sectionId: number;
  sectionOrder: number;
  sectionTitle: string;
  sectionBodyHtml: string;
  sectionBodyText?: string;
  files: SectionFile[];
}

interface CoverFile {
  fileId: number;
  url?: string;
  thumbnailUrl?: string;
  fileName?: string;
  mimeType?: string;
}

interface AvailableActions {
  canViewDetail: boolean;
  canEdit: boolean;
  canApprove: boolean;
  canReject: boolean;
  canHide: boolean;
  canShow: boolean;
  canTranslate: boolean;
}

interface NewsDetail {
  newsId: number;
  visitInstanceId?: number;
  campusId?: number;
  campusName?: string;
  status: string;
  statusLabel: string;
  authorUserId: number;
  authorName: string;
  createdAt: string;
  updatedAt?: string;
  submittedAt?: string;
  reviewedBy?: number;
  reviewedByName?: string;
  reviewedAt?: string;
  reviewNote?: string;
  publishedAt?: string;
  rowVersion: number;
  languageCode: string;
  availableLanguages: string[];
  title: string;
  summary?: string;
  slug?: string;
  coverFile?: CoverFile;
  sections: Section[];
  availableActions: AvailableActions;
}

const LANGUAGE_LABELS: Record<string, string> = {
  vi: 'Tiếng Việt',
  en: 'English',
  ja: '日本語',
  ko: '한국어',
  'zh-CN': '中文',
};

const ALL_LANGUAGES = ['vi', 'en', 'ja', 'ko', 'zh-CN'];

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatDate(iso?: string) {
  if (!iso) return null;
  const s = iso.endsWith('Z') ? iso : iso + 'Z';
  return new Date(s).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

function formatDateTime(iso?: string) {
  if (!iso) return null;
  const s = iso.endsWith('Z') ? iso : iso + 'Z';
  return new Date(s).toLocaleString('vi-VN', {
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit'
  });
}

function StatusBadge({ status, label }: { status: string; label: string }) {
  const styles: Record<string, string> = {
    PENDING_REVIEW: 'bg-yellow-50 text-yellow-700 border-yellow-200',
    REJECTED:       'bg-red-50 text-red-600 border-red-200',
    PUBLISHED:      'bg-green-50 text-green-700 border-green-200',
    HIDDEN:         'bg-gray-100 text-gray-500 border-gray-200',
  };
  return (
    <span className={`text-[11px] font-bold px-2.5 py-0.5 rounded-full border whitespace-nowrap ${styles[status] ?? 'bg-gray-100 text-gray-500 border-gray-200'}`}>
      {label}
    </span>
  );
}

// ─── Popup: Xác nhận duyệt ───────────────────────────────────────────────────

function ApprovePopup({ onConfirm, onCancel, loading }: { onConfirm: () => void; onCancel: () => void; loading: boolean }) {
  return (
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 px-4">
      <motion.div initial={{ scale: 0.9, opacity: 0 }} animate={{ scale: 1, opacity: 1 }} className="bg-white rounded-2xl shadow-xl p-8 max-w-sm w-full">
        <div className="flex justify-center mb-4">
          <CheckCircle className="w-12 h-12 text-green-500" />
        </div>
        <h3 className="text-lg font-bold text-gray-800 text-center mb-2">Xác nhận duyệt bài viết</h3>
        <p className="text-gray-500 text-center text-sm mb-6">Bạn chắc chắn muốn duyệt bài viết này? Bài viết sẽ được xuất bản sau khi duyệt.</p>
        <div className="flex gap-3">
          <button onClick={onCancel} disabled={loading} className="flex-1 border border-gray-300 text-gray-600 font-semibold py-2.5 rounded-xl hover:bg-gray-50 transition-colors">
            Hủy
          </button>
          <button onClick={onConfirm} disabled={loading} className="flex-1 bg-green-600 text-white font-semibold py-2.5 rounded-xl hover:bg-green-700 transition-colors flex items-center justify-center gap-2">
            {loading && <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />}
            Xác nhận duyệt
          </button>
        </div>
      </motion.div>
    </div>
  );
}

// ─── Popup: Từ chối ──────────────────────────────────────────────────────────

function RejectPopup({ onConfirm, onCancel, loading }: { onConfirm: (reason: string) => void; onCancel: () => void; loading: boolean }) {
  const [reason, setReason] = useState('');

  return (
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 px-4">
      <motion.div initial={{ scale: 0.9, opacity: 0 }} animate={{ scale: 1, opacity: 1 }} className="bg-white rounded-2xl shadow-xl p-8 max-w-sm w-full">
        <div className="flex justify-center mb-4">
          <XCircle className="w-12 h-12 text-red-500" />
        </div>
        <h3 className="text-lg font-bold text-gray-800 text-center mb-2">Từ chối bài viết</h3>
        <p className="text-gray-500 text-center text-sm mb-4">Vui lòng nhập lý do từ chối để tác giả biết và chỉnh sửa.</p>
        <label className="block text-sm font-semibold text-gray-700 mb-1.5">
          Lý do từ chối <span className="text-red-500">*</span>
        </label>
        <textarea
          value={reason}
          onChange={e => setReason(e.target.value)}
          placeholder="Nhập lý do..."
          rows={4}
          maxLength={500}
          className="w-full border border-gray-300 rounded-xl px-4 py-3 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-red-300 mb-1"
        />
        <p className="text-right text-xs text-gray-400 mb-5">{reason.length}/500</p>
        <div className="flex gap-3">
          <button onClick={onCancel} disabled={loading} className="flex-1 border border-gray-300 text-gray-600 font-semibold py-2.5 rounded-xl hover:bg-gray-50 transition-colors">
            Hủy
          </button>
          <button
            onClick={() => { if (reason.trim()) onConfirm(reason.trim()); }}
            disabled={loading || !reason.trim()}
            className="flex-1 bg-red-500 text-white font-semibold py-2.5 rounded-xl hover:bg-red-600 transition-colors flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {loading && <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />}
            Xác nhận từ chối
          </button>
        </div>
      </motion.div>
    </div>
  );
}

// ─── Popup: Dịch bài viết (auto-translate + chỉnh tay trước khi lưu) ─────────

interface TranslateDraftSection {
  sectionOrder: number;
  sectionTitle: string;
  sectionBodyHtml: string;
}

function TranslateModal({ news, onClose, onSaved }: {
  news: NewsDetail;
  onClose: () => void;
  onSaved: (languageCode: string) => void;
}) {
  const existing = news.availableLanguages ?? [news.languageCode];
  const targetOptions = ALL_LANGUAGES.filter(l => !existing.includes(l));

  const [sourceLang] = useState(news.languageCode);
  const [targetLang, setTargetLang] = useState(targetOptions[0] ?? '');
  const [translating, setTranslating] = useState(false);
  const [saving, setSaving] = useState(false);

  // Prefill bằng nội dung nguồn để người dùng có thể dịch tay khi chưa cấu hình API dịch.
  const [draftTitle, setDraftTitle] = useState(news.title);
  const [draftSummary, setDraftSummary] = useState(news.summary ?? '');
  const [draftSections, setDraftSections] = useState<TranslateDraftSection[]>(
    news.sections.map(s => ({
      sectionOrder: s.sectionOrder,
      sectionTitle: s.sectionTitle,
      sectionBodyHtml: s.sectionBodyHtml,
    })),
  );

  async function handleAutoTranslate() {
    if (!targetLang) return;
    setTranslating(true);
    const toastId = toast.loading('Đang dịch bài viết...');
    try {
      const { data } = await httpClient.post(`/news/${news.newsId}/translations/auto-translate`, {
        sourceLanguage: sourceLang,
        targetLanguage: targetLang,
        save: false,
      });
      setDraftTitle(data.title ?? '');
      setDraftSummary(data.summary ?? '');
      setDraftSections((data.sections ?? []).map((s: TranslateDraftSection) => ({
        sectionOrder: s.sectionOrder,
        sectionTitle: s.sectionTitle,
        sectionBodyHtml: s.sectionBodyHtml,
      })));
      toast.success('Đã dịch xong. Kiểm tra lại nội dung trước khi lưu.', { id: toastId });
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? 'Không thể dịch bài viết. Kiểm tra cấu hình API dịch.', { id: toastId });
    } finally {
      setTranslating(false);
    }
  }

  async function handleSave() {
    if (!targetLang) return;
    if (!draftTitle.trim()) { toast.error('Tiêu đề bản dịch không được để trống.'); return; }
    setSaving(true);
    const toastId = toast.loading('Đang lưu bản dịch...');
    try {
      await httpClient.post('/news/addmultilingualnews', {
        newsId: news.newsId,
        languageCode: targetLang,
        title: draftTitle.trim(),
        summary: draftSummary.trim(),
        sections: draftSections.map(s => ({
          sectionOrder: s.sectionOrder,
          sectionTitle: s.sectionTitle,
          sectionBodyHtml: s.sectionBodyHtml,
        })),
        copySectionFilesFromLanguage: sourceLang,
      });
      toast.success('Dịch bài viết thành công.', { id: toastId });
      onSaved(targetLang);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? 'Không thể lưu bản dịch.', { id: toastId });
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 px-4 py-8">
      <motion.div
        initial={{ scale: 0.95, opacity: 0 }}
        animate={{ scale: 1, opacity: 1 }}
        className="bg-white rounded-2xl shadow-xl w-full max-w-3xl max-h-[90vh] flex flex-col"
      >
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <h3 className="text-lg font-bold text-gray-800 flex items-center gap-2">
            <Languages className="w-5 h-5 text-[#004c91]" /> Dịch bài viết
          </h3>
          <button onClick={onClose} className="p-1.5 rounded-lg text-gray-400 hover:bg-gray-100 hover:text-gray-600 transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <div className="p-6 overflow-y-auto flex-1 space-y-5">
          {targetOptions.length === 0 ? (
            <p className="text-gray-500 text-sm">Bài viết đã có đủ bản dịch cho tất cả ngôn ngữ được hỗ trợ.</p>
          ) : (
            <>
              <div className="flex flex-wrap items-end gap-4">
                <div>
                  <label className="block text-sm font-semibold text-gray-700 mb-1.5">Ngôn ngữ nguồn</label>
                  <div className="px-3 py-2 border border-gray-200 rounded-xl bg-gray-50 text-sm font-medium text-gray-600">
                    {LANGUAGE_LABELS[sourceLang] ?? sourceLang}
                  </div>
                </div>
                <div>
                  <label className="block text-sm font-semibold text-gray-700 mb-1.5">
                    Ngôn ngữ đích <span className="text-red-500">*</span>
                  </label>
                  <select
                    value={targetLang}
                    onChange={e => setTargetLang(e.target.value)}
                    className="px-3 py-2 border border-gray-300 rounded-xl text-sm focus:outline-none focus:border-[#004c91] bg-white"
                  >
                    {targetOptions.map(l => (
                      <option key={l} value={l}>{LANGUAGE_LABELS[l] ?? l}</option>
                    ))}
                  </select>
                </div>
                <button
                  onClick={handleAutoTranslate}
                  disabled={translating || saving || !targetLang}
                  className="flex items-center gap-2 px-4 py-2 bg-[#004c91] text-white text-sm font-bold rounded-xl hover:bg-[#003a70] transition-colors disabled:opacity-50"
                >
                  {translating && <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />}
                  Dịch tự động
                </button>
              </div>

              <p className="text-xs text-gray-400">
                Có thể bấm “Dịch tự động” (Google Cloud Translation) hoặc tự chỉnh nội dung bên dưới rồi lưu.
                Ảnh của bài viết được giữ nguyên cho bản dịch.
              </p>

              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-1.5">Tiêu đề</label>
                <input
                  type="text"
                  value={draftTitle}
                  onChange={e => setDraftTitle(e.target.value)}
                  className="w-full px-4 py-2.5 border border-gray-300 rounded-xl text-sm focus:outline-none focus:border-[#004c91]"
                />
              </div>

              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-1.5">Mô tả ngắn</label>
                <textarea
                  rows={2}
                  value={draftSummary}
                  onChange={e => setDraftSummary(e.target.value)}
                  className="w-full px-4 py-2.5 border border-gray-300 rounded-xl text-sm resize-none focus:outline-none focus:border-[#004c91]"
                />
              </div>

              {draftSections.map((s, i) => (
                <div key={s.sectionOrder} className="border border-gray-200 rounded-xl p-4 space-y-3">
                  <label className="block text-xs font-bold text-gray-500 uppercase">Mục {i + 1}</label>
                  <input
                    type="text"
                    value={s.sectionTitle}
                    onChange={e => setDraftSections(prev => prev.map((p, pi) => pi === i ? { ...p, sectionTitle: e.target.value } : p))}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:border-[#004c91]"
                    placeholder="Tiêu đề mục..."
                  />
                  <div
                    className="text-sm text-gray-600 border border-gray-100 bg-gray-50 rounded-lg p-3 max-h-40 overflow-y-auto"
                    dangerouslySetInnerHTML={{ __html: sanitizeHtml(s.sectionBodyHtml) }}
                  />
                </div>
              ))}
            </>
          )}
        </div>

        {/* Footer */}
        <div className="flex justify-end gap-3 px-6 py-4 border-t border-gray-100">
          <button
            onClick={onClose}
            disabled={saving || translating}
            className="px-5 py-2.5 border border-gray-300 text-gray-600 font-semibold rounded-xl hover:bg-gray-50 transition-colors text-sm"
          >
            Hủy
          </button>
          {targetOptions.length > 0 && (
            <button
              onClick={handleSave}
              disabled={saving || translating || !targetLang}
              className="flex items-center gap-2 px-5 py-2.5 bg-[#f37021] text-white font-bold rounded-xl hover:bg-[#d9621a] transition-colors text-sm disabled:opacity-50"
            >
              {saving && <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />}
              Lưu bản dịch
            </button>
          )}
        </div>
      </motion.div>
    </div>
  );
}

// ─── Authenticated image wrapper ─────────────────────────────────────────────

function AuthenticatedImage({ url, alt, className, style }: {
  url?: string | null;
  alt?: string;
  className?: string;
  style?: React.CSSProperties;
}) {
  // External URLs (Google Drive, CDN, etc.) must NOT be fetched through httpClient
  // because it would attach our JWT and trigger a spurious 401 → logout cycle.
  const isExternal = !!url && (url.startsWith('http://') || url.startsWith('https://'));
  const authSrc = useAuthenticatedImage(isExternal ? null : (url ?? null));

  if (!url) return null;
  if (isExternal) return <img src={url} alt={alt ?? ''} className={className} style={style} />;
  if (!authSrc) return null;
  return <img src={authSrc} alt={alt ?? ''} className={className} style={style} />;
}

// ─── Main Component ───────────────────────────────────────────────────────────

export function NewsDetailDashboard() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();
  const isStaffLeader = user?.roleCode === 'STAFF' && user?.subRole === 'LEADER';

  const [news, setNews] = useState<NewsDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [showApprovePopup, setShowApprovePopup]   = useState(false);
  const [showRejectPopup, setShowRejectPopup]     = useState(false);
  const [showTranslateModal, setShowTranslateModal] = useState(false);
  const [actionLoading, setActionLoading]          = useState(false);
  const [selectedLang, setSelectedLang]            = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;

    async function fetchDetail() {
      setLoading(true);
      setError(null);
      try {
        const { data } = await httpClient.get<NewsDetail>(`/news/${id}`, {
          params: selectedLang ? { languageCode: selectedLang } : undefined,
        });
        if (!cancelled) setNews(data);
      } catch (err: unknown) {
        if (!cancelled) {
          const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
          setError(msg ?? 'Không thể tải chi tiết bài viết.');
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    fetchDetail();
    return () => { cancelled = true; };
  }, [id, selectedLang]);

  async function handleReview(action: 'APPROVE' | 'REJECT', reason?: string) {
    if (!news) return;
    setActionLoading(true);
    try {
      await httpClient.patch(`/news/${news.newsId}/review`, {
        action,
        reason: reason ?? null,
        rowVersion: news.rowVersion
      });
      toast.success(action === 'APPROVE' ? 'Bài viết đã được duyệt thành công!' : 'Bài viết đã bị từ chối.');
      setShowApprovePopup(false);
      setShowRejectPopup(false);
      setTimeout(() => navigate('/dashboard/news'), 1200);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? 'Có lỗi xảy ra, vui lòng thử lại.');
    } finally {
      setActionLoading(false);
    }
  }

  async function handleVisibility(targetStatus: 'HIDDEN' | 'PUBLISHED') {
    if (!news) return;
    setActionLoading(true);
    try {
      await httpClient.patch(`/news/${news.newsId}/visibility`, {
        targetStatus,
        rowVersion: news.rowVersion
      });
      toast.success(targetStatus === 'HIDDEN' ? 'Bài viết đã được ẩn.' : 'Bài viết đã được hiển thị lại.');
      setTimeout(() => navigate('/dashboard/news'), 1200);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? 'Có lỗi xảy ra, vui lòng thử lại.');
    } finally {
      setActionLoading(false);
    }
  }

  // ── Render states ──

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <div className="w-8 h-8 border-4 border-[#004c91] border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  if (error || !news) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[60vh] gap-4">
        <p className="text-red-500 font-semibold">{error ?? 'Không tìm thấy bài viết.'}</p>
        <button onClick={() => navigate('/dashboard/news')} className="text-[#004c91] hover:underline font-semibold">
          ← Quay lại danh sách
        </button>
      </div>
    );
  }

  const actions = news.availableActions;

  return (
    <>
      <AnimatePresence>
        {showApprovePopup && (
          <ApprovePopup
            onConfirm={() => handleReview('APPROVE')}
            onCancel={() => setShowApprovePopup(false)}
            loading={actionLoading}
          />
        )}
        {showRejectPopup && (
          <RejectPopup
            onConfirm={reason => handleReview('REJECT', reason)}
            onCancel={() => setShowRejectPopup(false)}
            loading={actionLoading}
          />
        )}
        {showTranslateModal && news && (
          <TranslateModal
            news={news}
            onClose={() => setShowTranslateModal(false)}
            onSaved={lang => {
              setShowTranslateModal(false);
              setSelectedLang(lang); // chuyển sang xem bản dịch vừa tạo
            }}
          />
        )}
      </AnimatePresence>

      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        exit={{ opacity: 0, y: -20 }}
        transition={{ duration: 0.3 }}
        className="p-4 sm:p-6 md:p-8 pb-12 min-h-full max-w-4xl mx-auto"
      >
        {/* Breadcrumb */}
        <div className="mb-6 flex items-center text-sm font-medium text-gray-500">
          <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors">Dashboard</button>
          <span className="mx-2">/</span>
          <button onClick={() => navigate('/dashboard/news')} className="hover:text-[#004c91] transition-colors">Quản lý tin tức</button>
          <span className="mx-2">/</span>
          <span className="text-[#004c91]">Xem chi tiết</span>
        </div>

        <div className="mb-6 flex items-center justify-between flex-wrap gap-3">
          <h1 className="text-3xl font-bold text-[#004c91]">Xem chi tiết</h1>

          {/* Action buttons */}
          <div className="flex gap-2 flex-wrap">
            {actions.canEdit && (
              <button
                onClick={() => navigate(`/dashboard/news/${news.newsId}/edit${news.languageCode !== 'vi' ? `?lang=${news.languageCode}` : ''}`)}
                className="flex items-center gap-2 bg-white border border-[#004c91] text-[#004c91] font-semibold px-4 py-2 rounded-xl hover:bg-[#eef5fa] transition-colors text-sm"
              >
                <Edit2 className="w-4 h-4" /> Chỉnh sửa
              </button>
            )}
            {actions.canTranslate && (
              <button
                onClick={() => setShowTranslateModal(true)}
                className="flex items-center gap-2 bg-white border border-[#004c91] text-[#004c91] font-semibold px-4 py-2 rounded-xl hover:bg-[#eef5fa] transition-colors text-sm"
              >
                <Languages className="w-4 h-4" /> Dịch bài viết
              </button>
            )}
            {actions.canApprove && (
              <button
                onClick={() => setShowApprovePopup(true)}
                className="flex items-center gap-2 bg-green-600 text-white font-semibold px-4 py-2 rounded-xl hover:bg-green-700 transition-colors text-sm"
              >
                <CheckCircle className="w-4 h-4" /> Duyệt bài
              </button>
            )}
            {actions.canReject && (
              <button
                onClick={() => setShowRejectPopup(true)}
                className="flex items-center gap-2 bg-red-500 text-white font-semibold px-4 py-2 rounded-xl hover:bg-red-600 transition-colors text-sm"
              >
                <XCircle className="w-4 h-4" /> Từ chối
              </button>
            )}
            {actions.canHide && (
              <button
                onClick={() => handleVisibility('HIDDEN')}
                disabled={actionLoading}
                className="flex items-center gap-2 bg-gray-500 text-white font-semibold px-4 py-2 rounded-xl hover:bg-gray-600 transition-colors text-sm disabled:opacity-50"
              >
                <EyeOff className="w-4 h-4" /> Ẩn bài
              </button>
            )}
            {actions.canShow && (
              <button
                onClick={() => handleVisibility('PUBLISHED')}
                disabled={actionLoading}
                className="flex items-center gap-2 bg-[#004c91] text-white font-semibold px-4 py-2 rounded-xl hover:bg-[#003a70] transition-colors text-sm disabled:opacity-50"
              >
                <Eye className="w-4 h-4" /> Hiển thị lại
              </button>
            )}
          </div>
        </div>

        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-8 sm:p-12 overflow-hidden">

          {/* Title */}
          <h1 className="text-3xl md:text-4xl font-bold text-gray-900 leading-tight mb-6 mt-0">
            {news.title}
          </h1>

          {/* Meta Info */}
          <div className="flex items-center flex-wrap gap-2.5 mb-6">
            <div className="flex items-center gap-1 text-gray-500 text-[14px] font-medium">
              <Calendar className="w-4 h-4" />
              <span>{formatDate(news.createdAt)}</span>
            </div>
            <div className="flex items-center gap-1 text-gray-500 text-[14px] font-medium">
              <User className="w-4 h-4" />
              <span>{news.authorName}</span>
            </div>
            {news.campusName && (
              <div className="text-gray-500 text-[14px] font-medium">
                Campus: {news.campusName}
              </div>
            )}
            <StatusBadge status={news.status} label={news.statusLabel} />
            {news.availableLanguages && news.availableLanguages.length > 1 && (
              <div className="flex items-center gap-1.5">
                <Globe className="w-4 h-4 text-gray-400" />
                {news.availableLanguages.map(code => (
                  <button
                    key={code}
                    onClick={() => setSelectedLang(code === 'vi' ? null : code)}
                    className={`px-2.5 py-0.5 rounded-full text-[11px] font-bold border transition-colors ${
                      code === news.languageCode
                        ? 'bg-[#004c91] text-white border-[#004c91]'
                        : 'bg-white text-gray-600 border-gray-300 hover:border-[#004c91] hover:text-[#004c91]'
                    }`}
                  >
                    {LANGUAGE_LABELS[code] ?? code}
                  </button>
                ))}
              </div>
            )}
            {news.updatedAt && !isStaffLeader && news.updatedAt !== news.reviewedAt && (
              <>
                <span className="text-gray-300">|</span>
                <div className="flex items-center gap-1 text-gray-500 text-[14px] font-medium italic">
                  <Clock className="w-4 h-4" />
                  <span>Cập nhật: {formatDateTime(news.updatedAt)}</span>
                </div>
              </>
            )}
          </div>

          {/* Rejection note */}
          {news.status === 'REJECTED' && news.reviewNote && (
            <div className="bg-red-50 border border-red-200 rounded-xl px-5 py-4 mb-6">
              <p className="text-sm font-semibold text-red-600 mb-1">Lý do từ chối:</p>
              <p className="text-sm text-red-700">{news.reviewNote}</p>
              {news.reviewedByName && (
                <p className="text-xs text-red-400 mt-1">— {news.reviewedByName}, {formatDateTime(news.reviewedAt) ?? ''}</p>
              )}
            </div>
          )}

          <div className="h-[1px] bg-gray-200 w-full mb-8" />

          {/* Summary / Sapo */}
          {news.summary && (
            <div className="flex mb-8">
              <div className="w-[3px] bg-[#f37021] shrink-0 mr-4 rounded-sm" />
              <p className="text-[17px] text-gray-700 italic leading-relaxed">{news.summary}</p>
            </div>
          )}

          {/* Cover image */}
          {news.coverFile?.url && (
            <div className="mb-10 rounded-lg overflow-hidden border border-gray-100 shadow-sm">
              <AuthenticatedImage url={news.coverFile.url} alt={news.title} className="w-full h-auto object-cover" />
            </div>
          )}

          {/* Sections */}
          <div className="news-content text-gray-800 space-y-10">
            {news.sections.map(section => (
              <div key={section.sectionId}>
                <h3 className="text-2xl font-bold text-gray-900 mb-4">{section.sectionTitle}</h3>
                <div dangerouslySetInnerHTML={{ __html: sanitizeHtml(section.sectionBodyHtml) }} />
                {section.files.filter(f => f.usageType === 'INLINE_IMAGE' && f.url).map(f => (
                  <AuthenticatedImage
                    key={f.sectionFileId}
                    url={f.url}
                    alt={f.fileName ?? ''}
                    className="rounded-lg mt-4 mb-2 border border-gray-100 shadow-sm mx-auto block" style={{ maxWidth: '65%', maxHeight: '380px', objectFit: 'contain' }}
                  />
                ))}
              </div>
            ))}
          </div>

          <style>{`
            .news-content { max-width: 100%; min-width: 0; overflow-x: hidden; }
            .news-content p,
            .news-content span,
            .news-content strong,
            .news-content b,
            .news-content em,
            .news-content i,
            .news-content u,
            .news-content s,
            .news-content li,
            .news-content h1,
            .news-content h2,
            .news-content h3,
            .news-content h4 { white-space: normal !important; word-break: normal !important; overflow-wrap: break-word !important; hyphens: none !important; }
            .news-content p { font-size: 1.05rem; margin-bottom: 1.25rem; line-height: 1.8; }
            .news-content blockquote { border-left: 4px solid #f37021; padding: 1.25rem; font-style: italic; color: #4b5563; margin: 2rem 0; background-color: #fff7ed; border-radius: 0 0.5rem 0.5rem 0; }
            .news-content img { max-width: 100%; height: auto; object-fit: contain; border-radius: 0.5rem; display: block; margin: 1rem auto; }
            .news-content a { word-break: normal !important; overflow-wrap: break-word !important; white-space: normal !important; hyphens: none !important; }
          `}</style>

          <div className="h-[1px] bg-gray-200 w-full mt-12 mb-6" />

          <button onClick={() => navigate('/dashboard/news')} className="text-[#004c91] font-semibold hover:underline flex items-center gap-1">
            <ArrowLeft className="w-4 h-4" /> Quay lại danh sách
          </button>
        </div>
      </motion.div>
    </>
  );
}
