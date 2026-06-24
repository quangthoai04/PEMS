/**
 * MinutesCard — biên bản cuộc họp cho 1 campus instance (Phase 3).
 *
 * Mỗi visit_instance chỉ có 1 biên bản. Chỉ Host hoặc participant đã ACCEPT mới tạo/sửa được, và
 * tại một thời điểm chỉ 1 người được sửa (cơ chế lock có token + hết hạn 10 phút). Mọi quyền do
 * backend quyết (canCreate/canEdit + trạng thái lock) — frontend chỉ render theo dữ liệu trả về.
 *
 * TODO (backlog) — điểm danh biên bản (minute_participants) chưa làm UI:
 *   - Preload khách từ visit_guest_members + người nội bộ từ visit_participants (+ Host).
 *   - Lưu snapshot vào minute_participants; cho người sửa tick ai có mặt (PRESENT/ABSENT/EXCUSED).
 */
import { useCallback, useEffect, useRef, useState } from 'react';
import { ChevronUp, ChevronDown, FileText, Lock, Edit3, Save, X, Plus, Clock } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';
import type { VisitMinute } from '../../../features/delegations/types/delegations.types';

const formatDateTime = (value?: string | null) =>
  value ? new Date(value).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' }) : '-';

const errMsg = (e: any, fallback: string) => e?.response?.data?.message || fallback;

export function MinutesCard({ visitInstanceId }: { visitInstanceId: number }) {
  const [expanded, setExpanded] = useState(true);
  const [data, setData] = useState<VisitMinute | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // Editing session (only set while this user holds the lock).
  const [editing, setEditing] = useState(false);
  const [draftTitle, setDraftTitle] = useState('');
  const [draftContent, setDraftContent] = useState('');
  const tokenRef = useRef<string | null>(null);
  const minutesIdRef = useRef<number | null>(null);
  const rowVersionRef = useRef<number>(0);
  const expiresRef = useRef<string | null>(null);
  const [remainingMs, setRemainingMs] = useState<number>(0);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const d = await delegationsApi.minutes.get(visitInstanceId);
      setData(d);
      setError(null);
    } catch (e: any) {
      setError(errMsg(e, 'Không thể tải biên bản. Vui lòng thử lại.'));
    } finally {
      setLoading(false);
    }
  }, [visitInstanceId]);

  useEffect(() => { load(); }, [load]);

  // Countdown of the edit session; auto-exit when the lock expires.
  useEffect(() => {
    if (!editing) return;
    const tick = () => {
      const ms = expiresRef.current ? new Date(expiresRef.current).getTime() - Date.now() : 0;
      setRemainingMs(ms);
      if (ms <= 0) { setEditing(false); load(); }
    };
    tick();
    const t = setInterval(tick, 1000);
    return () => clearInterval(t);
  }, [editing, load]);

  const enterEditing = (d: VisitMinute) => {
    tokenRef.current = d.editLockToken ?? null;
    minutesIdRef.current = d.minutesId ?? null;
    rowVersionRef.current = d.rowVersion;
    expiresRef.current = d.editLockExpiresAt ?? null;
    setDraftTitle(d.title ?? 'Biên bản cuộc họp');
    setDraftContent(d.content ?? '');
    setData(d);
    setEditing(true);
    setError(null);
  };

  const handleCreate = async () => {
    setBusy(true);
    try { enterEditing(await delegationsApi.minutes.createOrLock(visitInstanceId)); }
    catch (e: any) { setError(errMsg(e, 'Không thể tạo biên bản. Vui lòng thử lại.')); }
    finally { setBusy(false); }
  };

  const handleEdit = async () => {
    if (!data?.minutesId) return;
    setBusy(true);
    try { enterEditing(await delegationsApi.minutes.acquireLock(data.minutesId)); }
    catch (e: any) { setError(errMsg(e, 'Không thể mở biên bản để chỉnh sửa.')); await load(); }
    finally { setBusy(false); }
  };

  const handleSave = async () => {
    if (!minutesIdRef.current || !tokenRef.current) return;
    if (!draftTitle.trim()) { setError('Tiêu đề biên bản không được để trống.'); return; }
    setBusy(true);
    try {
      const d = await delegationsApi.minutes.save(minutesIdRef.current, {
        title: draftTitle.trim(),
        content: draftContent,
        editLockToken: tokenRef.current,
        rowVersion: rowVersionRef.current,
      });
      setEditing(false);
      tokenRef.current = null;
      setData(d);
      setError(null);
    } catch (e: any) {
      setError(errMsg(e, 'Không thể lưu biên bản. Vui lòng thử lại.'));
    } finally { setBusy(false); }
  };

  const handleCancel = async () => {
    const id = minutesIdRef.current, token = tokenRef.current;
    setEditing(false);
    tokenRef.current = null;
    if (id && token) {
      try { await delegationsApi.minutes.releaseLock(id, token); } catch { /* lock will expire anyway */ }
    }
    await load();
  };

  // Best-effort: release the lock if the user navigates away mid-edit.
  useEffect(() => {
    return () => {
      const id = minutesIdRef.current, token = tokenRef.current;
      if (id && token) { delegationsApi.minutes.releaseLock(id, token).catch(() => {}); }
    };
  }, []);

  const mm = String(Math.max(0, Math.floor(remainingMs / 60000))).padStart(2, '0');
  const ss = String(Math.max(0, Math.floor((remainingMs % 60000) / 1000))).padStart(2, '0');

  return (
    <div className="bg-white rounded-2xl border border-gray-200 overflow-hidden shadow-sm transition-all relative">
      <div
        className="bg-[#004c91] px-6 py-4 flex items-center justify-between border-b border-[#003366] cursor-pointer"
        onClick={() => setExpanded(!expanded)}
      >
        <h2 className="text-xl font-bold text-white flex items-center gap-2">
          <span className="w-8 h-8 rounded-full bg-[#f37021] flex items-center justify-center text-sm">2</span>
          Biên bản cuộc họp
        </h2>
        <button type="button" className="text-white hover:bg-white/20 p-1 rounded-full transition-colors">
          {expanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
        </button>
      </div>

      <AnimatePresence>
        {expanded && (
          <motion.div initial={{ height: 0 }} animate={{ height: 'auto' }} exit={{ height: 0 }} className="overflow-hidden">
            <div className="p-4 sm:p-6 md:p-8">
              {error && (
                <div className="mb-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm font-medium text-red-700">{error}</div>
              )}

              {loading ? (
                <div className="py-8 text-center text-slate-500 font-medium">Đang tải biên bản...</div>
              ) : !data ? null : !data.exists ? (
                /* Chưa có biên bản */
                <div className="text-center py-6">
                  <FileText className="w-10 h-10 mx-auto text-slate-300 mb-3" />
                  <p className="text-slate-600 font-medium mb-4">Chưa có biên bản cho chuyến thăm này.</p>
                  {data.canCreate && (
                    <button type="button" onClick={handleCreate} disabled={busy}
                      className="px-6 py-3 bg-[#f37021] text-white font-bold rounded-xl shadow-sm hover:bg-[#e0611d] transition-colors inline-flex items-center gap-2 disabled:opacity-50">
                      <Plus className="w-5 h-5" /> {busy ? 'Đang tạo...' : 'Tạo biên bản'}
                    </button>
                  )}
                </div>
              ) : editing ? (
                /* Chính mình đang chỉnh sửa */
                <div className="space-y-4">
                  <div className="flex items-center gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-2.5 text-sm font-bold text-amber-800">
                    <Clock className="w-4 h-4" /> Bạn đang chỉnh sửa biên bản. Phiên sửa còn {mm}:{ss}
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-2">Tên biên bản <span className="text-red-500">*</span></label>
                    <input type="text" value={draftTitle} onChange={(e) => setDraftTitle(e.target.value)}
                      className="w-full px-4 py-2.5 rounded-xl border border-gray-200 text-sm font-semibold text-gray-800 outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20" />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-2">Nội dung biên bản</label>
                    <textarea value={draftContent} onChange={(e) => setDraftContent(e.target.value)} rows={10}
                      placeholder="Nhập nội dung biên bản cuộc họp..."
                      className="w-full px-4 py-3 rounded-xl border border-gray-200 text-sm text-gray-800 outline-none resize-y focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20" />
                  </div>
                  <div className="flex items-center justify-end gap-3">
                    <button type="button" onClick={handleCancel} disabled={busy}
                      className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-50 transition-colors inline-flex items-center gap-2 disabled:opacity-50">
                      <X className="w-4 h-4" /> Hủy chỉnh sửa
                    </button>
                    <button type="button" onClick={handleSave} disabled={busy || !draftTitle.trim()}
                      className="px-6 py-2.5 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#003b70] shadow-sm transition-colors inline-flex items-center gap-2 disabled:opacity-50">
                      <Save className="w-4 h-4" /> {busy ? 'Đang lưu...' : 'Lưu'}
                    </button>
                  </div>
                </div>
              ) : (
                /* Đã có biên bản, không ở chế độ sửa */
                <div className="space-y-4">
                  {data.isLockedByOther ? (
                    <div className="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm font-medium text-amber-800">
                      <Lock className="w-4 h-4 mt-0.5 shrink-0" />
                      <span>
                        Biên bản đang được chỉnh sửa bởi <b>{data.editLockedByName || 'người khác'}</b>. Bạn chỉ có thể xem nội dung hiện tại;
                        quyền sửa sẽ mở lại sau khi người này lưu hoặc phiên sửa hết hạn.
                      </span>
                    </div>
                  ) : null}

                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div className="min-w-0">
                      <p className="text-base font-bold text-[#004c91] truncate">{data.title || 'Biên bản cuộc họp'}</p>
                      <p className="text-xs text-slate-500">
                        Trạng thái: {data.status === 'SAVED' ? 'Đã lưu' : 'Bản nháp'} · Cập nhật lần cuối: {formatDateTime(data.editLockedAt)}
                      </p>
                    </div>
                    {data.canEdit && !data.isLockedByOther && (
                      <button type="button" onClick={handleEdit} disabled={busy}
                        className="px-5 py-2.5 rounded-xl font-bold text-white bg-[#f37021] hover:bg-[#e0611d] shadow-sm transition-colors inline-flex items-center gap-2 disabled:opacity-50">
                        <Edit3 className="w-4 h-4" /> Sửa
                      </button>
                    )}
                  </div>

                  <div className="rounded-xl border border-gray-200 bg-gray-50/60 px-4 py-3 min-h-[120px] whitespace-pre-wrap text-sm text-gray-800">
                    {data.content?.trim() ? data.content : <span className="text-slate-400 italic">Chưa có nội dung.</span>}
                  </div>
                </div>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
