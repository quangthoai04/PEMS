/**
 * The "Nháp" list inside Email Management.
 *
 * Its own component and its own endpoint, because drafts are not sent mail: they are Own-scope, still
 * editable, and carry a different shape. Selecting a row reopens it in the composer by id — the list
 * never carries the body or the recipient addresses, so nothing here can leak a blind copy.
 */
import { useCallback, useEffect, useState } from 'react';
import { Loader2, FileText, Paperclip, Users } from 'lucide-react';
import { emailDraftsApi, type EmailDraftSummaryDto } from '../api/emailDraftsApi';
import { formatVietnamTime } from '../../../shared/utils/vietnamTime';

export interface DraftsPanelProps {
  /** Opens the composer on this draft. */
  onOpenDraft: (draftId: number) => void;
  /** Bumped by the parent after a send or discard so the list reflects it. */
  refreshToken?: number;
}

export function DraftsPanel({ onOpenDraft, refreshToken = 0 }: DraftsPanelProps) {
  const [items, setItems] = useState<EmailDraftSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await emailDraftsApi.listDrafts({ page: 1, pageSize: 50 });
      setItems(result.items ?? []);
    } catch {
      setError('Không tải được danh sách email nháp. Vui lòng thử lại.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load, refreshToken]);

  if (loading) {
    return (
      <div className="flex items-center justify-center gap-2 py-12 text-sm text-gray-500" role="status">
        <Loader2 className="h-4 w-4 animate-spin" /> Đang tải email nháp…
      </div>
    );
  }

  if (error) {
    return (
      <div className="py-12 text-center" role="alert">
        <p className="text-sm text-red-600">{error}</p>
        <button type="button" onClick={() => void load()}
          className="mt-3 rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50">
          Thử lại
        </button>
      </div>
    );
  }

  if (items.length === 0) {
    return (
      <div className="py-12 text-center text-sm text-gray-500" data-testid="drafts-empty">
        <FileText className="mx-auto mb-2 h-8 w-8 text-gray-300" />
        Chưa có email nháp nào.
      </div>
    );
  }

  return (
    <ul className="divide-y divide-gray-100" data-testid="drafts-list">
      {items.map(draft => (
        <li key={draft.emailDraftId}>
          <button
            type="button"
            onClick={() => onOpenDraft(draft.emailDraftId)}
            className="flex w-full items-center justify-between gap-4 px-4 py-3 text-left hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#004c91]"
          >
            <span className="min-w-0 flex-1">
              <span className="block truncate text-sm font-medium text-gray-900">
                {draft.subject?.trim() || '(Không có tiêu đề)'}
              </span>
              <span className="mt-0.5 flex items-center gap-3 text-xs text-gray-500">
                <span className="inline-flex items-center gap-1">
                  <Users className="h-3.5 w-3.5" /> {draft.recipientCount} người nhận
                </span>
                {draft.attachmentCount > 0 && (
                  <span className="inline-flex items-center gap-1">
                    <Paperclip className="h-3.5 w-3.5" /> {draft.attachmentCount}
                  </span>
                )}
              </span>
            </span>
            <span className="shrink-0 text-xs text-gray-400">
              {formatVietnamTime(draft.updatedAt)}
            </span>
          </button>
        </li>
      ))}
    </ul>
  );
}

export default DraftsPanel;
