/**
 * The marker the backend uses for "the action buttons go HERE".
 *
 * The prepared body no longer has its action block cut out and returned separately — it carries an inert
 * `<div data-system-block="action">` at the position the template gave it, and the backend substitutes the
 * real (or, in a preview, disabled) block over that node when the message is assembled.
 *
 * These helpers exist so the read-only stages can SHOW the buttons where they will actually appear. Before
 * this, the modal printed them in a separate panel underneath the message, which was the only honest thing
 * it could do while the body had a hole in it — but it meant a sender approving a message could not see
 * that a sentence like "chọn một phương án bên dưới" pointed at their signature rather than at the buttons.
 *
 * Nothing here makes the block editable, and nothing here mints a URL: the node is empty, and the markup
 * substituted into it for display is the DISABLED copy the backend already sends for preview.
 */

/** The canonical node the backend emits. */
export const SYSTEM_ACTION_NODE = '<div data-system-block="action"></div>';

/**
 * Matches one action node however the sanitiser or editor respelled its attributes.
 * Mirrors `EmailSystemBlockNodes.ActionNodePattern` on the backend.
 */
// Tolerates text INSIDE the node, which is how the editor's copy carries its visible label. All three
// copies of this pattern — here, `emailEditorSystemNodes.ts`, and `EmailSystemBlockNodes` on the backend —
// must agree, or a node one of them can see becomes a node another silently ignores.
const ACTION_NODE = /<div\b[^>]*\bdata-system-block\s*=\s*(?:"action"|'action'|action)[^>]*>[^<]*<\/div\s*>/gi;

/** True when the body carries a system action node. */
export function hasSystemActionNode(html: string | null | undefined): boolean {
  if (!html) return false;
  ACTION_NODE.lastIndex = 0;
  return ACTION_NODE.test(html);
}

/** How many action nodes the body carries — more than one is refused by the backend. */
export function countSystemActionNodes(html: string | null | undefined): number {
  if (!html) return 0;
  ACTION_NODE.lastIndex = 0;
  return (html.match(ACTION_NODE) ?? []).length;
}

/**
 * Renders the body with `blockHtml` shown at the node's position, for the read-only stages.
 *
 * Returns the body unchanged when there is no node, which is the case for templates with no action and
 * for content prepared before the node existed. The caller then falls back to the separate panel.
 */
export function renderSystemActionNode(
  html: string | null | undefined,
  blockHtml: string | null | undefined,
): string {
  if (!html) return '';
  if (!blockHtml) return html;

  ACTION_NODE.lastIndex = 0;
  // `$` in the replacement is literal here: an action URL may carry one, and `$&` would otherwise splice
  // the matched node back into the output.
  return html.replace(ACTION_NODE, () => blockHtml);
}
