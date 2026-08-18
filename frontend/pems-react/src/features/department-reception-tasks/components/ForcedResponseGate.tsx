/**
 * Reminder gate for overdue logistics requests (visit_logistics_items.due_at, auto-computed
 * as usage_start - 24h — never user-facing as an input). Mounted once at DashboardLayout so it stays
 * on screen across in-app navigation (DashboardLayout never unmounts on a nested route change):
 *   - Dept Leader: a request sent to their department is still REQUESTED (nobody took/assigned it)
 *     past its deadline.
 *   - Dept Staff: a request assigned to them is still ASSIGNED (not yet accepted/declined) past its
 *     deadline.
 * One item is shown at a time (oldest deadline first); resolving one auto-advances to the next via
 * refetching the queue. Not blocking: the user can dismiss via the X button or by clicking the
 * backdrop and handle it later — dismissed items are tracked per item id so they stay hidden even
 * across the 60s poll, but a genuinely new overdue item still surfaces its own reminder.
 */
import React, { useCallback, useEffect, useState } from 'react';
import { AlertCircle, CheckCircle2, Loader2, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { useAuth } from '../../../shared/hooks/useAuth';
import { normalizeApiError } from '../../../shared/api/normalizeApiError';
import { departmentReceptionTasksApi } from '../api/departmentReceptionTasksApi';

type OverdueItem = { logisticsItemId: number; title: string; dueAt: string };
type Candidate = { userId: number; name: string; email: string };

export function ForcedResponseGate() {
  const { user } = useAuth();
  const isDeptLeader = user?.roleCode?.toUpperCase() === 'DEPARTMENT' && user?.subRole?.toUpperCase() === 'LEADER';
  const isDeptStaff = user?.roleCode?.toUpperCase() === 'DEPARTMENT' && !isDeptLeader;
  const isDeptRole = isDeptLeader || isDeptStaff;

  const [queue, setQueue] = useState<OverdueItem[]>([]);
  const [dismissedIds, setDismissedIds] = useState<Set<number>>(new Set());
  const [loaded, setLoaded] = useState(false);
  const [detail, setDetail] = useState<any | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [rejecting, setRejecting] = useState(false);
  const [reason, setReason] = useState('');
  const [assigning, setAssigning] = useState(false);
  const [candidates, setCandidates] = useState<Candidate[] | null>(null);

  const fetchQueue = useCallback(async () => {
    if (!isDeptRole) return;
    try {
      const data = await departmentReceptionTasksApi.getOverdueResponses();
      setQueue(Array.isArray(data?.items) ? data.items : []);
    } catch (e) {
      console.error(e);
    } finally {
      setLoaded(true);
    }
  }, [isDeptRole]);

  useEffect(() => { fetchQueue(); }, [fetchQueue]);

  // Catches deadlines that pass while the user is already in the dashboard, not just at login.
  useEffect(() => {
    if (!isDeptRole) return;
    const id = setInterval(fetchQueue, 60000);
    return () => clearInterval(id);
  }, [isDeptRole, fetchQueue]);

  const visibleQueue = queue.filter((item) => !dismissedIds.has(item.logisticsItemId));
  const currentId = visibleQueue[0]?.logisticsItemId ?? null;

  useEffect(() => {
    setRejecting(false);
    setReason('');
    setAssigning(false);
    setCandidates(null);
    if (currentId == null) { setDetail(null); return; }
    let cancelled = false;
    setDetailLoading(true);
    departmentReceptionTasksApi.getRequestDetail(currentId)
      .then((d: any) => { if (!cancelled) setDetail(d); })
      .catch((e: unknown) => console.error(e))
      .finally(() => { if (!cancelled) setDetailLoading(false); });
    return () => { cancelled = true; };
  }, [currentId]);

  const handleDismiss = () => {
    if (currentId == null) return;
    setDismissedIds((prev) => new Set(prev).add(currentId));
  };

  const afterAction = (message: string) => {
    toast.success(message);
    fetchQueue();
  };

  const runAction = async (fn: () => Promise<unknown>, successMessage: string) => {
    setBusy(true);
    try {
      await fn();
      afterAction(successMessage);
    } catch (e) {
      toast.error(normalizeApiError(e).message);
    } finally {
      setBusy(false);
    }
  };

  const handleConfirm = () => currentId != null
    && runAction(() => departmentReceptionTasksApi.confirmRequest(currentId), 'Đã xác nhận nhiệm vụ.');

  const handleReject = () => currentId != null && reason.trim()
    && runAction(() => departmentReceptionTasksApi.rejectRequest(currentId, reason.trim()), 'Đã từ chối yêu cầu.');

  const handleAccept = () => currentId != null
    && runAction(() => departmentReceptionTasksApi.acceptAssignment(currentId), 'Đã chấp nhận nhiệm vụ.');

  const handleDecline = () => currentId != null && reason.trim()
    && runAction(() => departmentReceptionTasksApi.declineAssignment(currentId, reason.trim()), 'Đã từ chối nhiệm vụ.');

  const openAssign = async () => {
    setAssigning(true);
    if (candidates != null) return;
    try {
      const data = await departmentReceptionTasksApi.getAssigneeCandidates();
      setCandidates(Array.isArray(data) ? data : []);
    } catch (e) {
      toast.error(normalizeApiError(e).message);
      setCandidates([]);
    }
  };

  const handleAssign = (candidateUserId: number) => currentId != null
    && runAction(() => departmentReceptionTasksApi.assignAssignee(currentId, candidateUserId), 'Đã phân công người phụ trách.');

  if (!isDeptRole || !loaded || visibleQueue.length === 0) return null;

  const showActions = !detailLoading && detail && !rejecting && !assigning;

  return (
    <div
      className="fixed inset-0 z-[200] flex items-center justify-center bg-black/60 backdrop-blur-sm p-4"
      onClick={handleDismiss}
    >
      <div onClick={(e) => e.stopPropagation()} className="w-full max-w-lg bg-white rounded-2xl shadow-2xl overflow-hidden">
        <div className="bg-[#004c91] px-5 py-4 flex items-center gap-3">
          <AlertCircle className="w-6 h-6 text-white shrink-0" />
          <div className="flex-1">
            <div className="text-white font-black text-base">Yêu cầu quá hạn phản hồi</div>
            <div className="text-blue-100 text-xs">Bạn nên xử lý đơn này sớm. Có thể đóng và xử lý sau.</div>
          </div>
          <button
            type="button"
            onClick={handleDismiss}
            aria-label="Đóng"
            className="text-blue-100 hover:text-white shrink-0 p-1 -m-1"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-5 space-y-3 max-h-[60vh] overflow-y-auto">
          {detailLoading || !detail ? (
            <div className="flex items-center justify-center py-10 text-slate-400">
              <Loader2 className="w-6 h-6 animate-spin" />
            </div>
          ) : (
            <>
              <div>
                <div className="text-xs font-bold text-slate-400 uppercase">Hạng mục</div>
                <div className="text-base font-normal text-slate-800">{detail.title}</div>
              </div>
              {detail.delegationName && (
                <div>
                  <div className="text-xs font-bold text-slate-400 uppercase">Đoàn khách</div>
                  <div className="text-sm font-normal text-[#004c91]">{detail.delegationName}</div>
                </div>
              )}
              {(detail.startTime || detail.endTime) && (
                <div>
                  <div className="text-xs font-bold text-slate-400 uppercase">Thời gian sử dụng</div>
                  <div className="text-sm font-normal text-slate-700">{detail.startTime} - {detail.endTime} {detail.date}</div>
                </div>
              )}
              {detail.description && (
                <div>
                  <div className="text-xs font-bold text-slate-400 uppercase">Nội dung chi tiết</div>
                  <div className="text-sm text-slate-600 whitespace-pre-line">{detail.description}</div>
                </div>
              )}

              {isDeptLeader && assigning && (
                <div className="border border-slate-200 rounded-xl p-3">
                  <div className="text-xs font-bold text-slate-500 mb-2">Chọn người phụ trách</div>
                  {candidates == null ? (
                    <Loader2 className="w-4 h-4 animate-spin text-slate-400" />
                  ) : candidates.length === 0 ? (
                    <div className="text-xs text-slate-400">Không có nhân sự khả dụng.</div>
                  ) : (
                    <div className="space-y-1 max-h-40 overflow-y-auto">
                      {candidates.map((c) => (
                        <button
                          key={c.userId}
                          type="button"
                          disabled={busy}
                          onClick={() => handleAssign(c.userId)}
                          className="w-full text-left px-3 py-2 rounded-lg text-sm font-semibold text-slate-700 hover:bg-blue-50 disabled:opacity-50"
                        >
                          {c.name}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </>
          )}
        </div>

        <div className="bg-slate-50 border-t border-slate-200 px-5 py-3">
          {rejecting && (
            <textarea
              rows={2}
              autoFocus
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Lý do từ chối (bắt buộc)..."
              className="w-full mb-2 px-3 py-2 text-sm border border-slate-200 rounded-xl outline-none focus:border-rose-400 resize-none"
            />
          )}
          <div className="flex flex-wrap items-center justify-end gap-2">
            {showActions && isDeptLeader && (
              <>
                <button type="button" onClick={openAssign} disabled={busy}
                  className="px-4 py-2 bg-white border border-slate-200 text-slate-700 text-xs font-black rounded-xl hover:bg-slate-100 disabled:opacity-50">
                  Phân công người phụ trách
                </button>
                <button type="button" onClick={() => setRejecting(true)} disabled={busy}
                  className="px-4 py-2 bg-white border border-rose-200 text-rose-600 text-xs font-black rounded-xl hover:bg-rose-50 disabled:opacity-50">
                  Từ chối
                </button>
                <button type="button" onClick={handleConfirm} disabled={busy}
                  className="px-4 py-2 bg-[#004c91] hover:bg-[#003b70] text-white text-xs font-black rounded-xl disabled:opacity-50 inline-flex items-center gap-1.5">
                  {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <CheckCircle2 className="w-3.5 h-3.5" />}
                  Xác nhận nhiệm vụ
                </button>
              </>
            )}
            {showActions && isDeptStaff && (
              <>
                <button type="button" onClick={() => setRejecting(true)} disabled={busy}
                  className="px-4 py-2 bg-white border border-rose-200 text-rose-600 text-xs font-black rounded-xl hover:bg-rose-50 disabled:opacity-50">
                  Từ chối
                </button>
                <button type="button" onClick={handleAccept} disabled={busy}
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-black rounded-xl disabled:opacity-50 inline-flex items-center gap-1.5">
                  {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <CheckCircle2 className="w-3.5 h-3.5" />}
                  Chấp nhận
                </button>
              </>
            )}
            {rejecting && (
              <>
                <button type="button" onClick={isDeptLeader ? handleReject : handleDecline} disabled={busy || !reason.trim()}
                  className="px-4 py-2 bg-rose-600 hover:bg-rose-700 text-white text-xs font-black rounded-xl disabled:opacity-50">
                  Xác nhận từ chối
                </button>
                <button type="button" onClick={() => { setRejecting(false); setReason(''); }} disabled={busy}
                  className="px-3 py-2 text-xs font-bold text-slate-600 bg-slate-100 rounded-xl hover:bg-slate-200 disabled:opacity-50">
                  Hủy
                </button>
              </>
            )}
            {assigning && (
              <button type="button" onClick={() => setAssigning(false)} disabled={busy}
                className="px-3 py-2 text-xs font-bold text-slate-600 bg-slate-100 rounded-xl hover:bg-slate-200 disabled:opacity-50">
                Quay lại
              </button>
            )}
          </div>
          {visibleQueue.length > 1 && (
            <div className="mt-2 text-[11px] text-slate-400 text-center">
              Còn {visibleQueue.length - 1} đơn quá hạn khác đang chờ xử lý sau đơn này.
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
