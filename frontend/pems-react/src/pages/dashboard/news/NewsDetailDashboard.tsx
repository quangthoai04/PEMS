import React, { useState, useEffect } from 'react';
import { Calendar, User, Clock, CheckCircle, XCircle, EyeOff, Eye, ArrowLeft } from 'lucide-react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'motion/react';
import toast, { Toaster } from 'react-hot-toast';
import { sanitizeHtml } from '../../../shared/security/sanitizeHtml';
import httpClient from '../../../shared/api/httpClient';

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
  title: string;
  summary?: string;
  slug?: string;
  coverFile?: CoverFile;
  sections: Section[];
  availableActions: AvailableActions;
}

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

// ─── Main Component ───────────────────────────────────────────────────────────

export function NewsDetailDashboard() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [news, setNews] = useState<NewsDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [showApprovePopup, setShowApprovePopup] = useState(false);
  const [showRejectPopup, setShowRejectPopup]   = useState(false);
  const [actionLoading, setActionLoading]        = useState(false);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;

    async function fetchDetail() {
      setLoading(true);
      setError(null);
      try {
        const { data } = await httpClient.get<NewsDetail>(`/news/${id}`);
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
  }, [id]);

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
      <Toaster position="top-right" />

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

          {/* Action buttons for Staff Leader */}
          <div className="flex gap-2 flex-wrap">
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

        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-8 sm:p-12">

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
            {news.updatedAt && (
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
              <img src={news.coverFile.url} alt={news.title} className="w-full h-auto object-cover" />
            </div>
          )}

          {/* Sections */}
          <div className="news-content text-gray-800 space-y-10">
            {news.sections.map(section => (
              <div key={section.sectionId}>
                <h3 className="text-2xl font-bold text-gray-900 mb-4">{section.sectionTitle}</h3>
                <div dangerouslySetInnerHTML={{ __html: sanitizeHtml(section.sectionBodyHtml) }} />
                {section.files.filter(f => f.usageType === 'INLINE_IMAGE' && f.url).map(f => (
                  <img
                    key={f.sectionFileId}
                    src={f.url}
                    alt={f.fileName ?? ''}
                    className="w-full rounded-lg mt-4 mb-2 border border-gray-100 shadow-sm"
                  />
                ))}
              </div>
            ))}
          </div>

          <style>{`
            .news-content p { font-size: 1.05rem; margin-bottom: 1.25rem; line-height: 1.8; }
            .news-content blockquote { border-left: 4px solid #f37021; padding: 1.25rem; font-style: italic; color: #4b5563; margin: 2rem 0; background-color: #fff7ed; border-radius: 0 0.5rem 0.5rem 0; }
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
