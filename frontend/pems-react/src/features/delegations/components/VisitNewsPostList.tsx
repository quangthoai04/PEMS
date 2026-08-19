/**
 * VisitNewsPostList — danh sách bài tin tức của MỘT visit instance (logic duyệt mới).
 *
 * Dùng chung cho tab "Sau tiếp khách" (VisitNewsSection) và trang "Đóng góp kết quả"
 * (NewsContributionSection) để KHÔNG có form tạo tin thứ hai: nút Tạo/Sửa điều hướng sang
 * đúng form News Management (/dashboard/news/create|edit) kèm ?visitInstanceId & returnTo.
 *
 * Backend là nguồn quyền duy nhất (GET /news/visit-instances/{id}):
 *  • participant chỉ nhận bài của chính mình; Host nhận mọi bài của chuyến; Staff Leader
 *    đúng campus nhận mọi bài kèm canApprove/canReject.
 *  • canEdit chỉ true cho TÁC GIẢ khi bài PENDING_REVIEW/REJECTED.
 * Frontend chỉ ẩn/hiện nút theo các cờ đó.
 */
import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { Newspaper, Plus, Edit3, Send, CheckCircle, XCircle } from 'lucide-react';
import toast from 'react-hot-toast';
import httpClient from '../../../shared/api/httpClient';
import { delegationsApi } from '../api/delegationsApi';
import type { VisitNews, VisitNewsList } from '../types/delegations.types';

import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
const errMsg = (e: any, fallback: string) => e?.response?.data?.message || fallback;
const formatDateTime = (value?: string | null) =>
  value ? formatVietnamDateTime(value) : '-';

const STATUS_META: Record<string, { label: string; cls: string }> = {
  PENDING_REVIEW: { label: 'Chờ Staff Leader duyệt', cls: 'bg-yellow-50 text-yellow-700 border-yellow-200' },
  PUBLISHED: { label: 'Đã đăng', cls: 'bg-green-50 text-green-700 border-green-200' },
  REJECTED: { label: 'Bị từ chối', cls: 'bg-red-50 text-red-700 border-red-200' },
  HIDDEN: { label: 'Đã ẩn', cls: 'bg-gray-100 text-gray-600 border-gray-200' },
};

interface Props {
  visitInstanceId: number | string;
  /** Ẩn nút tạo bài dù backend cho phép (vd. chuyến không yêu cầu tin / khách không đồng ý truyền thông). */
  createBlocked?: boolean;
  /** Thông báo khi danh sách rỗng. */
  emptyText?: string;
}

export function VisitNewsPostList({ visitInstanceId, createBlocked = false, emptyText }: Props) {
  const navigate = useNavigate();
  const location = useLocation();
  const returnTo = encodeURIComponent(location.pathname + location.search);

  const [list, setList] = useState<VisitNewsList | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [rejectTarget, setRejectTarget] = useState<VisitNews | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  const [approveTarget, setApproveTarget] = useState<VisitNews | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try { setList(await delegationsApi.visitNews.list(visitInstanceId)); setError(null); }
    catch (e: any) { setError(errMsg(e, 'Không thể tải tin tức. Vui lòng thử lại.')); }
    finally { setLoading(false); }
  }, [visitInstanceId]);

  useEffect(() => { load(); }, [load]);

  const goCreate = () =>
    navigate(`/dashboard/news/create?visitInstanceId=${visitInstanceId}&returnTo=${returnTo}`);
  const goEdit = (n: VisitNews) =>
    navigate(`/dashboard/news/${n.newsId}/edit?returnTo=${returnTo}`);

  // Gửi duyệt lại bài bị từ chối (tác giả) — endpoint hiện có, backend enforce author-only.
  const resubmit = async (n: VisitNews) => {
    setBusy(true);
    const toastId = toast.loading('Đang gửi duyệt lại...');
    try {
      await httpClient.post(`/news/visit-instance-news/${n.newsId}/submit-review`);
      toast.success('Đã gửi bài viết, đang chờ Staff Leader duyệt.', { id: toastId });
      await load();
    } catch (e: any) { toast.error(errMsg(e, 'Không thể gửi duyệt lại.'), { id: toastId }); }
    finally { setBusy(false); }
  };

  // Duyệt/từ chối — cùng endpoint với News Management (PATCH /news/{id}/review).
  const review = async (n: VisitNews, action: 'APPROVE' | 'REJECT', reason?: string) => {
    setBusy(true);
    const toastId = toast.loading(action === 'APPROVE' ? 'Đang duyệt bài viết...' : 'Đang từ chối bài viết...');
    try {
      await httpClient.patch(`/news/${n.newsId}/review`, {
        action,
        reason: reason ?? null,
        rowVersion: n.rowVersion,
      });
      toast.success(action === 'APPROVE' ? 'Đã duyệt và xuất bản bài viết.' : 'Đã từ chối bài viết.', { id: toastId });
      setApproveTarget(null);
      setRejectTarget(null);
      setRejectReason('');
      await load();
    } catch (e: any) { toast.error(errMsg(e, 'Không thể xử lý bài viết.'), { id: toastId }); }
    finally { setBusy(false); }
  };

  const items = list?.items ?? [];

  return (
    <div className="space-y-4">
      {error && <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm font-normal text-red-700">{error}</div>}

      {/* Tạo bài — điều hướng sang form News Management (không có form thứ hai) */}
      {list?.canCreate && !createBlocked && (
        <div className="flex justify-end">
          <button type="button" onClick={goCreate}
            className="px-5 py-2.5 bg-[#f37021] text-white font-bold rounded-xl shadow-sm hover:bg-[#e0611d] transition-colors inline-flex items-center gap-2">
            <Plus className="w-4 h-4" /> Tạo bài tin tức
          </button>
        </div>
      )}

      {loading ? (
        <div className="py-8 text-center text-slate-500 font-normal">Đang tải tin tức...</div>
      ) : items.length === 0 ? (
        <div className="py-8 text-center">
          <Newspaper className="w-10 h-10 mx-auto text-slate-300 mb-3" />
          <p className="text-slate-600 font-normal">{emptyText ?? 'Chưa có bài tin tức nào cho chuyến thăm này.'}</p>
        </div>
      ) : (
        <div className="space-y-3">
          {items.map((n) => {
            const meta = STATUS_META[n.status] ?? { label: n.status, cls: 'bg-gray-100 text-gray-700 border-gray-200' };
            return (
              <div key={n.newsId} className="rounded-2xl border border-slate-200 bg-white p-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <button
                      type="button"
                      onClick={() => navigate(`/dashboard/news/${n.newsId}`)}
                      className="text-base font-bold text-[#004c91] truncate hover:underline text-left"
                      title="Xem chi tiết bài viết"
                    >
                      {n.title}
                    </button>
                    <p className="text-xs text-slate-500">
                      {n.authorName || `#${n.authorUserId}`} · {formatDateTime(n.submittedAt)}
                    </p>
                  </div>
                  <span className={`inline-flex shrink-0 justify-center whitespace-nowrap rounded-full border px-2.5 py-1 text-xs font-semibold ${meta.cls}`}>{meta.label}</span>
                </div>
                {n.summary && <p className="mt-2 text-sm font-normal text-slate-700">{n.summary}</p>}
                {n.status === 'REJECTED' && n.reviewNote && (
                  <p className="mt-2 text-xs font-normal text-red-600">Lý do từ chối: {n.reviewNote}</p>
                )}

                {(n.canEdit || n.canApprove || n.canReject) && (
                  <div className="mt-3 flex flex-wrap items-center justify-end gap-2 border-t border-slate-100 pt-3">
                    {n.canEdit && n.status === 'REJECTED' && (
                      <button type="button" onClick={() => resubmit(n)} disabled={busy}
                        className="px-4 py-2 rounded-lg text-sm font-bold text-[#004c91] bg-blue-50 hover:bg-blue-100 inline-flex items-center gap-1.5 disabled:opacity-50">
                        <Send className="w-4 h-4" /> Gửi duyệt lại
                      </button>
                    )}
                    {n.canEdit && (
                      <button type="button" onClick={() => goEdit(n)} disabled={busy}
                        className="px-4 py-2 rounded-lg text-sm font-bold text-white bg-[#f37021] hover:bg-[#e0611d] inline-flex items-center gap-1.5 disabled:opacity-50">
                        <Edit3 className="w-4 h-4" /> Sửa
                      </button>
                    )}
                    {n.canApprove && (
                      <button type="button" onClick={() => setApproveTarget(n)} disabled={busy}
                        className="px-4 py-2 rounded-lg text-sm font-bold text-white bg-green-600 hover:bg-green-700 inline-flex items-center gap-1.5 disabled:opacity-50">
                        <CheckCircle className="w-4 h-4" /> Duyệt bài
                      </button>
                    )}
                    {n.canReject && (
                      <button type="button" onClick={() => { setRejectTarget(n); setRejectReason(''); }} disabled={busy}
                        className="px-4 py-2 rounded-lg text-sm font-bold text-white bg-red-500 hover:bg-red-600 inline-flex items-center gap-1.5 disabled:opacity-50">
                        <XCircle className="w-4 h-4" /> Từ chối
                      </button>
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      {/* Popup xác nhận duyệt */}
      {approveTarget && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl shadow-xl p-8 max-w-sm w-full max-h-[calc(100dvh-2rem)] overflow-y-auto">
            <div className="flex justify-center mb-4"><CheckCircle className="w-12 h-12 text-green-500" /></div>
            <h3 className="text-lg font-bold text-gray-800 text-center mb-2">Xác nhận duyệt bài viết</h3>
            <p className="text-gray-500 text-center text-sm mb-6">
              Duyệt bài "{approveTarget.title}"? Bài viết sẽ được xuất bản sau khi duyệt.
            </p>
            <div className="flex gap-3">
              <button onClick={() => setApproveTarget(null)} disabled={busy}
                className="flex-1 border border-gray-300 text-gray-600 font-semibold py-2.5 rounded-xl hover:bg-gray-50 transition-colors">
                Hủy
              </button>
              <button onClick={() => review(approveTarget, 'APPROVE')} disabled={busy}
                className="flex-1 bg-green-600 text-white font-semibold py-2.5 rounded-xl hover:bg-green-700 transition-colors flex items-center justify-center gap-2">
                {busy && <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />}
                Xác nhận duyệt
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Popup từ chối — bắt buộc nhập lý do */}
      {rejectTarget && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl shadow-xl p-8 max-w-sm w-full max-h-[calc(100dvh-2rem)] overflow-y-auto">
            <div className="flex justify-center mb-4"><XCircle className="w-12 h-12 text-red-500" /></div>
            <h3 className="text-lg font-bold text-gray-800 text-center mb-2">Từ chối bài viết</h3>
            <p className="text-gray-500 text-center text-sm mb-4">Vui lòng nhập lý do từ chối để tác giả biết và chỉnh sửa.</p>
            <label className="block text-sm font-semibold text-gray-700 mb-1.5">
              Lý do từ chối <span className="text-red-500">*</span>
            </label>
            <textarea
              value={rejectReason}
              onChange={e => setRejectReason(e.target.value)}
              placeholder="Nhập lý do..."
              rows={4}
              maxLength={500}
              className="w-full border border-gray-300 rounded-xl px-4 py-3 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-red-300 mb-1"
            />
            <p className="text-right text-xs text-gray-400 mb-5">{rejectReason.length}/500</p>
            <div className="flex gap-3">
              <button onClick={() => setRejectTarget(null)} disabled={busy}
                className="flex-1 border border-gray-300 text-gray-600 font-semibold py-2.5 rounded-xl hover:bg-gray-50 transition-colors">
                Hủy
              </button>
              <button
                onClick={() => { if (rejectReason.trim()) review(rejectTarget, 'REJECT', rejectReason.trim()); }}
                disabled={busy || !rejectReason.trim()}
                className="flex-1 bg-red-500 text-white font-semibold py-2.5 rounded-xl hover:bg-red-600 transition-colors flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed">
                {busy && <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />}
                Xác nhận từ chối
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
