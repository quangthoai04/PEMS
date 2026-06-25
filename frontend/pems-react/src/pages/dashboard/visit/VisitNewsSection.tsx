/**
 * VisitNewsSection — tin tức gắn với 1 campus instance (Phase 4).
 *
 * Một instance có thể có NHIỀU bài. Chỉ Host / IC-Staff đã ACCEPT / Student đã ACCEPT mới viết được
 * (backend enforce). Visitor chỉ thấy bài đã đăng (PUBLISHED). Việc duyệt/đăng bài theo UC News hiện có.
 */
import { useCallback, useEffect, useState } from 'react';
import { ChevronUp, ChevronDown, Newspaper, Plus, Edit3, Save, X, Send } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';
import type { VisitNews, VisitNewsList } from '../../../features/delegations/types/delegations.types';

const errMsg = (e: any, fallback: string) => e?.response?.data?.message || fallback;
const formatDateTime = (value?: string | null) =>
  value ? new Date(value).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' }) : '-';

const STATUS_META: Record<string, { label: string; cls: string }> = {
  PENDING_REVIEW: { label: 'Chờ duyệt', cls: 'bg-yellow-50 text-yellow-700 border-yellow-200' },
  PUBLISHED: { label: 'Đã đăng', cls: 'bg-green-50 text-green-700 border-green-200' },
  REJECTED: { label: 'Bị từ chối', cls: 'bg-red-50 text-red-700 border-red-200' },
  HIDDEN: { label: 'Đã ẩn', cls: 'bg-gray-100 text-gray-600 border-gray-200' },
};

type Draft = { newsId: number | null; title: string; summary: string; body: string; rowVersion: number };
const emptyDraft = (): Draft => ({ newsId: null, title: '', summary: '', body: '', rowVersion: 0 });

export function VisitNewsSection({ visitInstanceId }: { visitInstanceId: number }) {
  const [expanded, setExpanded] = useState(true);
  const [list, setList] = useState<VisitNewsList | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState<Draft | null>(null); // null = no form open

  const load = useCallback(async () => {
    setLoading(true);
    try { setList(await delegationsApi.visitNews.list(visitInstanceId)); setError(null); }
    catch (e: any) { setError(errMsg(e, 'Không thể tải tin tức. Vui lòng thử lại.')); }
    finally { setLoading(false); }
  }, [visitInstanceId]);

  useEffect(() => { load(); }, [load]);

  const startCreate = () => { setForm(emptyDraft()); setError(null); };
  const startEdit = (n: VisitNews) =>
    { setForm({ newsId: n.newsId, title: n.title, summary: n.summary ?? '', body: n.body ?? '', rowVersion: n.rowVersion }); setError(null); };

  const save = async () => {
    if (!form) return;
    if (!form.title.trim()) { setError('Tiêu đề bài tin không được để trống.'); return; }
    setBusy(true);
    try {
      if (form.newsId == null) {
        await delegationsApi.visitNews.create(visitInstanceId, { title: form.title.trim(), summary: form.summary || null, body: form.body || null });
      } else {
        await delegationsApi.visitNews.update(form.newsId, { title: form.title.trim(), summary: form.summary || null, body: form.body || null, rowVersion: form.rowVersion });
      }
      setForm(null);
      await load();
    } catch (e: any) { setError(errMsg(e, 'Không thể lưu bài tin. Vui lòng thử lại.')); }
    finally { setBusy(false); }
  };

  const resubmit = async (n: VisitNews) => {
    setBusy(true);
    try { await delegationsApi.visitNews.submitReview(n.newsId); await load(); }
    catch (e: any) { setError(errMsg(e, 'Không thể gửi duyệt lại.')); }
    finally { setBusy(false); }
  };

  const items = list?.items ?? [];

  return (
    <div className="bg-white rounded-2xl border border-gray-200 overflow-hidden shadow-sm transition-all relative">
      <div className="bg-[#00a651] px-6 py-4 flex items-center justify-between cursor-pointer" onClick={() => setExpanded(!expanded)}>
        <h2 className="text-xl font-bold text-white flex items-center gap-2">
          <Newspaper className="w-5 h-5" /> Tin tức đoàn khách
        </h2>
        <button type="button" className="text-white hover:bg-white/20 p-1 rounded-full transition-colors">
          {expanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
        </button>
      </div>

      <AnimatePresence>
        {expanded && (
          <motion.div initial={{ height: 0 }} animate={{ height: 'auto' }} exit={{ height: 0 }} className="overflow-hidden">
            <div className="p-4 sm:p-6 md:p-8 space-y-4">
              {error && <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm font-medium text-red-700">{error}</div>}

              {/* Create button */}
              {list?.canCreate && !form && (
                <div className="flex justify-end">
                  <button type="button" onClick={startCreate}
                    className="px-5 py-2.5 bg-[#f37021] text-white font-bold rounded-xl shadow-sm hover:bg-[#e0611d] transition-colors inline-flex items-center gap-2">
                    <Plus className="w-4 h-4" /> Tạo bài tin tức
                  </button>
                </div>
              )}

              {/* Create / edit form */}
              {form && (
                <div className="rounded-2xl border border-slate-200 bg-slate-50/60 p-4 space-y-3">
                  <p className="text-sm font-bold text-[#004c91]">{form.newsId == null ? 'Bài tin mới' : 'Chỉnh sửa bài tin'}</p>
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-1.5">Tiêu đề <span className="text-red-500">*</span></label>
                    <input type="text" value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })}
                      className="w-full px-4 py-2.5 rounded-xl border border-gray-200 text-sm font-semibold text-gray-800 outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20" />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-1.5">Tóm tắt</label>
                    <input type="text" value={form.summary} onChange={(e) => setForm({ ...form, summary: e.target.value })}
                      className="w-full px-4 py-2.5 rounded-xl border border-gray-200 text-sm text-gray-800 outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20" />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-1.5">Nội dung</label>
                    <textarea value={form.body} onChange={(e) => setForm({ ...form, body: e.target.value })} rows={6}
                      placeholder="Nhập nội dung bài tin..."
                      className="w-full px-4 py-3 rounded-xl border border-gray-200 text-sm text-gray-800 outline-none resize-y focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20" />
                  </div>
                  <div className="flex items-center justify-end gap-3">
                    <button type="button" onClick={() => setForm(null)} disabled={busy}
                      className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-50 inline-flex items-center gap-2 disabled:opacity-50">
                      <X className="w-4 h-4" /> Hủy
                    </button>
                    <button type="button" onClick={save} disabled={busy || !form.title.trim()}
                      className="px-6 py-2.5 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#003b70] shadow-sm inline-flex items-center gap-2 disabled:opacity-50">
                      <Save className="w-4 h-4" /> {busy ? 'Đang lưu...' : (form.newsId == null ? 'Gửi duyệt' : 'Lưu & gửi duyệt')}
                    </button>
                  </div>
                </div>
              )}

              {/* List */}
              {loading ? (
                <div className="py-8 text-center text-slate-500 font-medium">Đang tải tin tức...</div>
              ) : items.length === 0 && !form ? (
                <div className="py-8 text-center">
                  <Newspaper className="w-10 h-10 mx-auto text-slate-300 mb-3" />
                  <p className="text-slate-600 font-medium">Chưa có bài tin tức nào cho chuyến thăm này.</p>
                </div>
              ) : (
                <div className="space-y-3">
                  {items.map((n) => {
                    const meta = STATUS_META[n.status] ?? { label: n.status, cls: 'bg-gray-100 text-gray-700 border-gray-200' };
                    return (
                      <div key={n.newsId} className="rounded-2xl border border-slate-200 bg-white p-4">
                        <div className="flex items-start justify-between gap-3">
                          <div className="min-w-0">
                            <p className="text-base font-bold text-[#004c91] truncate">{n.title}</p>
                            <p className="text-xs text-slate-500">
                              {n.authorName || `#${n.authorUserId}`} · {formatDateTime(n.submittedAt)}
                            </p>
                          </div>
                          <span className={`inline-flex shrink-0 justify-center whitespace-nowrap rounded-full border px-2.5 py-1 text-xs font-semibold ${meta.cls}`}>{meta.label}</span>
                        </div>
                        {n.summary && <p className="mt-2 text-sm font-medium text-slate-700">{n.summary}</p>}
                        {n.body && <p className="mt-1 text-sm text-slate-600 whitespace-pre-wrap line-clamp-4">{n.body}</p>}
                        {n.status === 'REJECTED' && n.reviewNote && (
                          <p className="mt-2 text-xs font-medium text-red-600">Lý do từ chối: {n.reviewNote}</p>
                        )}
                        {n.canEdit && !form && (
                          <div className="mt-3 flex items-center justify-end gap-2 border-t border-slate-100 pt-3">
                            {n.status === 'REJECTED' && (
                              <button type="button" onClick={() => resubmit(n)} disabled={busy}
                                className="px-4 py-2 rounded-lg text-sm font-bold text-[#004c91] bg-blue-50 hover:bg-blue-100 inline-flex items-center gap-1.5 disabled:opacity-50">
                                <Send className="w-4 h-4" /> Gửi duyệt lại
                              </button>
                            )}
                            <button type="button" onClick={() => startEdit(n)} disabled={busy}
                              className="px-4 py-2 rounded-lg text-sm font-bold text-white bg-[#f37021] hover:bg-[#e0611d] inline-flex items-center gap-1.5 disabled:opacity-50">
                              <Edit3 className="w-4 h-4" /> Sửa
                            </button>
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
