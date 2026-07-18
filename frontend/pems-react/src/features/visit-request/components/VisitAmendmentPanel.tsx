import { useCallback, useEffect, useState } from 'react';
import {
  approveAmendment,
  getActiveAmendment,
  rejectAmendment,
  withdrawAmendment,
  type AmendmentDto,
} from '../api/visitRequestV2Api';

interface Props {
  visitRequestId: number;
  visitInstanceId: number;
  /** True when the current user is the CURRENT Staff Leader of THIS campus (decision rights). */
  canDecide: boolean;
  /** True when the current user is the requester side (registrant/ACTIVE contact) — may withdraw. */
  canWithdraw: boolean;
  onChanged?: () => void;
}

const FIELD_LABELS: Record<string, string> = {
  'instance.delegationName': 'Tên đoàn',
  'instance.visitType': 'Loại hình',
  'instance.visitTypeOther': 'Loại hình khác',
  'instance.purpose': 'Mục đích',
  'instance.workingContent': 'Nội dung làm việc',
  'instance.workingLanguage': 'Ngôn ngữ',
  'instance.operationalContact.fullName': 'Đầu mối phối hợp — họ tên',
  'instance.operationalContact.organization': 'Đầu mối phối hợp — đơn vị',
  'instance.operationalContact.phone': 'Đầu mối phối hợp — điện thoại',
  'instance.operationalContact.email': 'Đầu mối phối hợp — email',
  'instance.members.visitors': 'Danh sách khách',
  'instance.members.externalSupport': 'Nhân sự hỗ trợ',
  'instance.plannedStartAt': 'Bắt đầu',
  'instance.plannedEndAt': 'Kết thúc',
};

const pretty = (json: string | null): string => {
  if (json == null) return '—';
  try {
    const value = JSON.parse(json) as unknown;
    if (Array.isArray(value)) {
      return value
        .map(v => (typeof v === 'object' && v !== null ? Object.values(v as Record<string, unknown>).filter(Boolean).join(' · ') : String(v)))
        .join('\n');
    }
    return typeof value === 'string' ? value : JSON.stringify(value);
  } catch {
    return json;
  }
};

/**
 * Pending-amendment panel (plan §9.5): shows the PROPOSED old→new diff clearly separated from the
 * active content; the CURRENT campus Staff Leader approves/rejects (reject requires a reason); the
 * requester may withdraw. Nothing here ever presents the proposal as applied.
 */
export default function VisitAmendmentPanel({
  visitRequestId,
  visitInstanceId,
  canDecide,
  canWithdraw,
  onChanged,
}: Props) {
  const [amendment, setAmendment] = useState<AmendmentDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [rejectMode, setRejectMode] = useState(false);
  const [note, setNote] = useState('');

  const refresh = useCallback(async () => {
    try {
      setAmendment(await getActiveAmendment(visitRequestId, visitInstanceId));
    } catch {
      setAmendment(null);
    }
  }, [visitRequestId, visitInstanceId]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  if (!amendment) return null;

  const run = async (fn: () => Promise<{ message: string }>) => {
    setBusy(true);
    setMessage(null);
    try {
      const result = await fn();
      setMessage(result.message);
      setRejectMode(false);
      setNote('');
      await refresh();
      onChanged?.();
    } catch (err: unknown) {
      setMessage(
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          'Không thể xử lý đề xuất. Vui lòng tải lại và thử lại.',
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <section
      aria-label="Đề xuất thay đổi đang chờ duyệt"
      className="rounded-xl border border-amber-300 dark:border-amber-700 bg-amber-50/60 dark:bg-amber-900/20 p-4"
    >
      <div className="flex flex-wrap items-center gap-2">
        <h3 className="text-sm font-semibold text-amber-900 dark:text-amber-100">
          Đề xuất thay đổi #{amendment.amendmentNo} — chờ Staff Leader duyệt
        </h3>
        <span className="rounded bg-amber-200/70 dark:bg-amber-800/60 px-1.5 py-0.5 text-[11px] text-amber-900 dark:text-amber-100">
          Nội dung đang hiệu lực KHÔNG thay đổi cho tới khi duyệt
        </span>
      </div>
      <p className="mt-1 text-xs text-amber-800 dark:text-amber-200">
        Người đề xuất: {amendment.requestedByName ?? '—'} · {new Date(amendment.requestedAt).toLocaleString('vi-VN')}
        {amendment.reason ? ` · Lý do: ${amendment.reason}` : ''}
      </p>

      <div className="mt-3 overflow-x-auto">
        <table className="w-full min-w-[480px] text-left text-xs">
          <caption className="sr-only">Chi tiết thay đổi đề xuất (giá trị cũ và mới)</caption>
          <thead>
            <tr className="border-b border-amber-200 dark:border-amber-800 text-amber-900 dark:text-amber-100">
              <th scope="col" className="py-1 pr-2 font-medium">Trường</th>
              <th scope="col" className="py-1 pr-2 font-medium">Đang hiệu lực</th>
              <th scope="col" className="py-1 font-medium">Đề xuất</th>
            </tr>
          </thead>
          <tbody>
            {amendment.changes.map(c => (
              <tr key={c.fieldPath} className="border-b border-amber-100 dark:border-amber-900 align-top">
                <th scope="row" className="py-1.5 pr-2 font-medium text-gray-800 dark:text-gray-100">
                  {FIELD_LABELS[c.fieldPath] ?? c.fieldPath}
                </th>
                <td className="py-1.5 pr-2 whitespace-pre-wrap text-gray-600 dark:text-gray-300">{pretty(c.oldValueJson)}</td>
                <td className="py-1.5 whitespace-pre-wrap font-medium text-gray-900 dark:text-gray-50">{pretty(c.newValueJson)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="mt-3 flex flex-wrap gap-2">
        {canDecide && !rejectMode && (
          <>
            <button
              type="button"
              disabled={busy}
              className="rounded-lg bg-green-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50"
              onClick={() => void run(() => approveAmendment(visitInstanceId, amendment.amendmentId, note || undefined))}
            >
              Duyệt & áp dụng
            </button>
            <button
              type="button"
              disabled={busy}
              className="rounded-lg border border-red-300 px-3 py-1.5 text-sm text-red-600 hover:bg-red-50 dark:border-red-700 dark:text-red-300"
              onClick={() => setRejectMode(true)}
            >
              Từ chối…
            </button>
          </>
        )}
        {canDecide && rejectMode && (
          <div className="w-full space-y-2">
            <label htmlFor="amendment-reject-note" className="block text-xs text-amber-900 dark:text-amber-100">
              Lý do từ chối <span className="text-red-500">*</span>
            </label>
            <textarea
              id="amendment-reject-note"
              required
              rows={2}
              maxLength={500}
              className="w-full rounded-lg border border-amber-300 dark:border-amber-700 bg-white dark:bg-gray-800 p-2 text-sm"
              value={note}
              onChange={e => setNote(e.target.value)}
            />
            <div className="flex gap-2">
              <button
                type="button"
                disabled={busy || note.trim().length === 0}
                className="rounded-lg bg-red-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
                onClick={() => void run(() => rejectAmendment(visitInstanceId, amendment.amendmentId, note.trim()))}
              >
                Xác nhận từ chối
              </button>
              <button
                type="button"
                className="rounded-lg border border-gray-300 dark:border-gray-600 px-3 py-1.5 text-sm"
                onClick={() => setRejectMode(false)}
              >
                Quay lại
              </button>
            </div>
          </div>
        )}
        {canWithdraw && !canDecide && (
          <button
            type="button"
            disabled={busy}
            className="rounded-lg border border-gray-300 dark:border-gray-600 px-3 py-1.5 text-sm"
            onClick={() => void run(() => withdrawAmendment(visitRequestId, visitInstanceId, amendment.amendmentId))}
          >
            Rút đề xuất
          </button>
        )}
      </div>
      {message && (
        <p className="mt-2 text-sm text-gray-800 dark:text-gray-100" role="status">
          {message}
        </p>
      )}
    </section>
  );
}
