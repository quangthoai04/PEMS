import { useEffect, useState } from 'react';
import { getVisitRequestHistory, type VisitRequestHistory } from '../api/visitRequestV2Api';

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

/**
 * Scoped, masked business-history timeline (plan §9.5): applied revisions, PROPOSED amendments
 * (clearly separated — a proposal is never presented as active content), campus decisions and —
 * for request managers/HO only — the masked identity events. The server scopes the entries; this
 * component renders exactly what it was given.
 */
export default function VisitHistoryTimeline({ visitRequestId }: Props) {
  const [history, setHistory] = useState<VisitRequestHistory | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    getVisitRequestHistory(visitRequestId)
      .then(h => {
        if (!cancelled) setHistory(h);
      })
      .catch(() => {
        if (!cancelled) setError('Không thể tải lịch sử thay đổi.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [visitRequestId]);

  if (loading) return <p className="text-sm text-gray-500" role="status">Đang tải lịch sử…</p>;
  if (error) return <p className="text-sm text-red-600" role="alert">{error}</p>;
  if (!history || history.entries.length === 0)
    return <p className="text-sm text-gray-500">Chưa có thay đổi nào được ghi nhận.</p>;

  return (
    <ol aria-label="Lịch sử thay đổi" className="space-y-3">
      {history.entries.map((e, idx) => (
        <li key={`${e.at}-${idx}`} className="flex gap-3 text-sm">
          <div className="mt-1 h-2 w-2 shrink-0 rounded-full bg-orange-500" aria-hidden />
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <span className="rounded bg-gray-100 dark:bg-gray-700 px-1.5 py-0.5 text-[11px] font-medium text-gray-600 dark:text-gray-300">
                {KIND_LABELS[e.kind] ?? e.kind}
              </span>
              {e.kind === 'AMENDMENT' && (
                <span className="rounded bg-amber-100 dark:bg-amber-900/40 px-1.5 py-0.5 text-[11px] text-amber-800 dark:text-amber-200">
                  Chưa phải nội dung hiệu lực
                </span>
              )}
              <time className="text-xs text-gray-400" dateTime={e.at}>
                {new Date(e.at).toLocaleString('vi-VN')}
              </time>
            </div>
            <p className="mt-0.5 font-medium text-gray-900 dark:text-gray-100 break-words">{e.title}</p>
            {e.detail && <p className="text-xs text-gray-500 dark:text-gray-400 break-words">{e.detail}</p>}
          </div>
        </li>
      ))}
    </ol>
  );
}
