/**
 * The Host's "Mô tả chi tiết" on a logistics request, as every recipient surface must show it.
 *
 * Extracted so the rule lives in ONE place: the calendar/task feeds these screens are built from do
 * not carry a description (the department calendar's `title` is literally "Yêu cầu: " + the item
 * title), and every surface had independently fallen back to something from that feed — so the
 * heading "Nội dung chi tiết công việc" sat above the request's own name while the real instructions
 * stayed unread in the column. There is deliberately no fallback to a title here.
 */
import React from 'react';

/** What the detail endpoint returns; only `description` matters to this module. */
export interface LogisticsDetailLike {
  description?: unknown;
}

export type LogisticsDescriptionState =
  | { state: 'loading' }
  | { state: 'empty' }
  | { state: 'text'; text: string };

/** Wording for a request saved without a description — legacy rows, or a Host who left it blank. */
export const EMPTY_DESCRIPTION_TEXT = 'Chưa có mô tả chi tiết.';
export const LOADING_DESCRIPTION_TEXT = 'Đang tải nội dung…';

/**
 * Resolves what to show.
 *
 * `null`/`undefined` means the detail request has not come back yet, which is NOT the same as "there
 * is no description" and must not flash the empty state. A value of only whitespace is treated as
 * absent, matching the server, which stores `NULL` for one.
 */
export function resolveLogisticsDescription(detail: LogisticsDetailLike | null | undefined): LogisticsDescriptionState {
  if (detail === null || detail === undefined) return { state: 'loading' };

  const raw = detail.description;
  const text = typeof raw === 'string' ? raw.trim() : '';
  return text ? { state: 'text', text } : { state: 'empty' };
}

/**
 * Renders the description.
 *
 * `whitespace-pre-wrap` keeps the line breaks the Host typed, `break-words` keeps a long unbroken
 * string from widening the modal, and the text goes in as a text node — never
 * `dangerouslySetInnerHTML` — so anything HTML-ish in it is displayed rather than executed.
 */
export function LogisticsWorkContent({
  detail,
  className = '',
}: {
  detail: LogisticsDetailLike | null | undefined;
  className?: string;
}) {
  const resolved = resolveLogisticsDescription(detail);

  if (resolved.state === 'text') {
    return (
      <div className={`break-words ${className}`}>
        {resolved.text.split('\n').map((line, idx) => (
          <p 
            key={idx} 
            className={
              idx > 0 && line.startsWith('*') 
                ? 'mt-4 font-bold text-gray-900 border-l-2 border-[#004c91] pl-3 py-1 bg-blue-50/50' 
                : 'mb-2 leading-relaxed'
            }
          >
            {line}
          </p>
        ))}
      </div>
    );
  }

  return (
    <p className="text-sm italic text-gray-400">
      {resolved.state === 'loading' ? LOADING_DESCRIPTION_TEXT : EMPTY_DESCRIPTION_TEXT}
    </p>
  );
}
