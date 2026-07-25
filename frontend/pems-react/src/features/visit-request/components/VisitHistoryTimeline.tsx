import { useCallback, useEffect, useState } from 'react';
import { AlertCircle, Loader2, RefreshCw } from 'lucide-react';
import { getVisitRequestHistory, type VisitRequestHistory } from '../api/visitRequestV2Api';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';

interface Props {
  visitRequestId: number;
}

const KIND_LABELS: Record<string, string> = {
  REQUEST_REVISION: 'Thông tin chung',
  INSTANCE_REVISION: 'Nội dung cơ sở',
  AMENDMENT: 'Đề xuất thay đổi',
  AMENDMENT_DECISION: 'Quyết định đề xuất',
  IDENTITY: 'Đầu mối liên hệ',
  DECISION: 'Quyết định cơ sở',
};

/** Decisions and amendment outcomes are the turning points of a request — they get the accent. */
const EMPHASISED_KINDS = new Set(['DECISION', 'AMENDMENT_DECISION']);

/**
 * Scoped, masked business-history timeline (plan §9.5/§19): applied revisions, PROPOSED amendments
 * (clearly separated — a proposal is never presented as active content), campus decisions and —
 * for request managers/HO only — the masked identity events. The server scopes the entries; this
 * component renders exactly what it was given.
 *
 * Times are formatted with the shared wall-clock helper: PEMS stores DATETIME as Vietnam local time,
 * so passing them through `new Date()` shifted every timestamp by the viewer's own offset.
 */
export default function VisitHistoryTimeline({ visitRequestId }: Props) {
  const [history, setHistory] = useState<VisitRequestHistory | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    getVisitRequestHistory(visitRequestId)
      .then(h => { if (!cancelled) setHistory(h); })
      .catch(() => { if (!cancelled) setError('Không thể tải lịch sử thay đổi.'); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [visitRequestId]);

  useEffect(() => load(), [load]);

  if (loading) {
    return (
      <p className="flex items-center gap-2 text-sm text-slate-500" role="status">
        <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Đang tải lịch sử…
      </p>
    );
  }

  if (error) {
    return (
      <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">
        <div className="flex items-start gap-2">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
          <p className="font-semibold">{error}</p>
        </div>
        <button
          type="button"
          data-testid="history-retry"
          onClick={() => load()}
          className="mt-2 inline-flex items-center gap-1.5 rounded-lg border border-red-300 bg-white px-3 py-1.5 text-sm font-bold text-red-700 hover:bg-red-100"
        >
          <RefreshCw className="h-4 w-4" aria-hidden /> Thử lại
        </button>
      </div>
    );
  }

  if (!history || history.entries.length === 0) {
    return <p className="text-sm italic text-slate-400">Chưa có thay đổi nào được ghi nhận.</p>;
  }

  return (
    <ol aria-label="Lịch sử thay đổi" className="relative space-y-4 border-l-2 border-[#004c91]/20 pl-5">
      {history.entries.map((e, idx) => {
        const emphasised = EMPHASISED_KINDS.has(e.kind);
        return (
          <li key={`${e.at}-${idx}`} className="relative text-sm">
            <span
              aria-hidden
              className={`absolute -left-[27px] top-1 h-3 w-3 rounded-full ring-2 ring-white ${
                emphasised ? 'bg-[#f37021]' : 'bg-[#004c91]'
              }`}
            />
            <div className="flex flex-wrap items-center gap-2">
              <span
                className={`rounded px-1.5 py-0.5 text-[11px] font-bold ${
                  emphasised ? 'bg-[#f37021]/10 text-[#f37021]' : 'bg-slate-100 text-slate-600'
                }`}
              >
                {KIND_LABELS[e.kind] ?? 'Thay đổi khác'}
              </span>
              {e.kind === 'AMENDMENT' && (
                <span className="rounded bg-amber-100 px-1.5 py-0.5 text-[11px] font-semibold text-amber-800">
                  Chưa phải nội dung hiệu lực
                </span>
              )}
              <time className="text-xs font-medium text-slate-400" dateTime={e.at}>
                {formatVietnamDateTime(e.at)}
              </time>
              {e.actorName && (
                <span className="text-xs font-medium text-slate-500">· {e.actorName}</span>
              )}
            </div>
            <p className="mt-0.5 break-words font-semibold text-slate-800">{e.title}</p>
            {e.detail && <p className="break-words text-xs text-slate-500">{e.detail}</p>}
          </li>
        );
      })}
    </ol>
  );
}
