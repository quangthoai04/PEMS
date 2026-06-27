/**
 * Defensive client-side stripper for legacy email action artifacts (spec §3.5).
 *
 * The backend is the source of truth — it strips action artifacts and injects the single canonical
 * action block on send. This util just keeps the editor clean when a template body loaded from the
 * server still contains a legacy action row (so the host never sees / re-saves duplicate buttons).
 *
 * Removes: the backend's marker-wrapped block, anchors whose href targets a system action
 * ({{xxxUrl}} raw or URL-encoded, or the /public/email-actions endpoint), bare action placeholders,
 * legacy plain-text button pairs ("Chấp nhận tham gia | Từ chối"), and the empty separators left
 * behind. Normal links (school site, Google Drive, docs) are untouched.
 */

const ACTION_VARS = 'accept|decline|negotiate|approveProposal|rejectProposal|confirmBorrow|confirmReturn|assign|detail';
// An href that points at a system action: {{xxxUrl}} (raw or %7B%7B..%7D%7D encoded) or the token endpoint.
const ACTION_HREF = new RegExp(
  `(\\{\\{\\s*(?:${ACTION_VARS})Url\\s*\\}\\}|%7[bB]%7[bB]\\s*(?:${ACTION_VARS})Url\\s*%7[dD]%7[dD]|/public/email-actions/|api/public/email-actions)`,
  'i',
);

export function stripLegacyActionHtml(html: string): string {
  if (!html) return html;
  let s = html;

  if (typeof window !== 'undefined' && window.DOMParser) {
    const doc = new window.DOMParser().parseFromString(html, 'text/html');
    doc.querySelectorAll('a[href]').forEach((a) => {
      if (ACTION_HREF.test(a.getAttribute('href') || '')) a.remove();
    });
    s = doc.body.innerHTML;
  }

  // Marker-wrapped canonical block (in case a previously-sent body is reloaded).
  s = s.replace(/<!--\s*PEMS_ACTION_BLOCK_START\s*-->[\s\S]*?<!--\s*PEMS_ACTION_BLOCK_END\s*-->/gi, '');
  // Bare placeholders (raw + URL-encoded).
  s = s.replace(new RegExp(`\\{\\{\\s*(?:${ACTION_VARS})Url\\s*\\}\\}`, 'gi'), '');
  s = s.replace(new RegExp(`%7[bB]%7[bB]\\s*(?:${ACTION_VARS})Url\\s*%7[dD]%7[dD]`, 'gi'), '');
  // Legacy plain-text button pairs joined by a pipe.
  s = s.replace(
    /(Chấp nhận tham gia|Chấp nhận phối hợp|Chấp nhận|Đồng ý|Nhận nhiệm vụ|Accept invitation|Accept coordination|Accept assignment|Accept)(?:&nbsp;|\s)*\|(?:&nbsp;|\s)*(Từ chối|Decline)(?:(?:&nbsp;|\s)*\|(?:&nbsp;|\s)*(Gán nhân sự|Assign staff))?/gi,
    '',
  );
  // A lone pipe stranded between tags after its anchors were removed.
  s = s.replace(/>(?:\s|&nbsp;)*\|(?:\s|&nbsp;|\|)*</gi, '><');
  // A <p>/<div> now holding only separators/whitespace.
  s = s.replace(/<(p|div)(?:\s[^>]*)?>(?:\s|\||&nbsp;|&amp;|<br\s*\/?>)*<\/\1>/gi, '');

  return s.trim();
}
