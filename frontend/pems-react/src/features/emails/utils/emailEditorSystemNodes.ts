/**
 * Makes the system action node survive the rich-text editor.
 *
 * <b>The defect.</b> The backend hands the editor a body carrying
 * `<div data-system-block="action"></div>` at the position the template gave the buttons. Quill does not
 * know that element, and an unknown empty block is not preserved — it is DROPPED. Measured on the version
 * this app actually loads (quill 2.0.3):
 *
 *     in   <p>hello</p><div data-system-block="action"></div><p>bye</p>
 *     out  <p>hello</p><p>bye</p>
 *
 * So opening the editor destroyed the position, and the send fell back to appending the block at the end —
 * silently, with every test still green, because nothing rendered a real Quill.
 *
 * <b>The fix.</b> Register the node as a Quill block embed so Parchment keeps it as one indivisible
 * object. It is `contenteditable="false"`, which is what gives §9.3 and §9.4 at once: the sender can
 * select it, drag it, cut and paste it — but cannot type inside it, split it, or edit what it will become.
 *
 * <b>The import that matters.</b> `Quill` comes from `react-quill-new`, NOT from `quill`. This project has
 * two copies: `react-quill-new` bundles its own quill 2.0.3, while the top-level `quill` is 1.3.7 (a
 * dependency of the legacy `react-quill`). Registering against the top-level one compiles, runs, throws
 * nothing — and has no effect whatsoever on the editor the user sees.
 */
import { Quill } from 'react-quill-new';

/** The class Parchment matches on. The backend contract is the `data-` attribute; this is Quill's half. */
export const ACTION_BLOT_CLASS = 'pems-system-action-block';

/** The name the blot is registered under, and the format name used in `formats`. */
export const ACTION_BLOT_NAME = 'pemsSystemActionBlock';

/**
 * What the sender sees in the editor. It is a stand-in, not the buttons: the real block is minted by the
 * backend at send time, and drawing something that looks clickable here would invite the belief that this
 * is what the recipient receives.
 */
const EDITOR_LABEL = 'Khối nút phản hồi — hệ thống tự gắn khi gửi (có thể di chuyển, không sửa được)';

/** The canonical form the backend emits and expects back. Carries nothing actionable. */
const CANONICAL_NODE = '<div data-system-block="action"></div>';

// `[^<]*` rather than `[\s\S]*?`: the content is text (the label below), never nested markup, and a lazy
// any-character match would stop at the first `</div>` of a nested block and strand its closing tag.
// Kept identical to the patterns in `systemActionNode.ts` and `EmailSystemBlockNodes` on the backend.
const ACTION_NODE = /<div\b[^>]*\bdata-system-block\s*=\s*(?:"action"|'action'|action)[^>]*>[^<]*<\/div\s*>/gi;

let registered = false;

/**
 * Registers the blot exactly once. Returns whether the editor will now preserve the node.
 *
 * <b>Why it cannot throw.</b> This is called at module scope so registration happens before any editor is
 * constructed — Quill drops an element it has no blot for, so registering late would mean the first
 * message opened had its action position deleted. Module-scope work that throws takes the whole component
 * down with it, and `Quill` is not always there to register against: several test files mock
 * `react-quill-new` with a stub that exports only the component. Those tests are not about the editor, and
 * a preview modal that fails to import is a poor way to tell them so.
 *
 * It reports rather than asserts because the two cases are genuinely different: absent Quill is normal in
 * a mocked test, and a real failure to register shows up in `emailEditorSystemNodes.test.ts`, which uses
 * the real editor and asserts the round trip keeps the node.
 */
export function registerSystemActionBlot(): boolean {
  if (registered) return true;

  /* eslint-disable @typescript-eslint/no-explicit-any */
  let q: any;
  try {
    // The ACCESS is what has to be guarded, not just the value. Reading a named export that a test double
    // does not define throws on the binding itself, so `if (!Quill)` would never be reached.
    q = Quill as any;
  } catch {
    return false;
  }

  if (!q || typeof q.import !== 'function' || typeof q.register !== 'function') return false;

  const BlockEmbed: any = q.import('blots/block/embed');
  if (!BlockEmbed) return false;

  class SystemActionBlot extends BlockEmbed {
    static blotName = ACTION_BLOT_NAME;
    static tagName = 'div';
    static className = ACTION_BLOT_CLASS;

    static create(): HTMLElement {
      const node: HTMLElement = super.create();
      node.setAttribute('data-system-block', 'action');
      // Not editable, still selectable: that is the whole contract — movable, not alterable.
      node.setAttribute('contenteditable', 'false');
      node.textContent = EDITOR_LABEL;
      return node;
    }

    /** Embeds must report a value; the node carries no data, so its presence IS the value. */
    static value(): string {
      return 'action';
    }
  }

  q.register(SystemActionBlot, true);
  /* eslint-enable @typescript-eslint/no-explicit-any */
  registered = true;
  return true;
}

/**
 * Canonical node → the shape Quill will recognise.
 *
 * Parchment matches a blot by tag name AND class, so the class has to be present before Quill parses the
 * HTML. The backend never sends it (its contract is the `data-` attribute), which is why this exists.
 */
export function toEditorHtml(html: string | null | undefined): string {
  if (!html) return '';
  ACTION_NODE.lastIndex = 0;
  return html.replace(
    ACTION_NODE,
    () => `<div class="${ACTION_BLOT_CLASS}" data-system-block="action" contenteditable="false">`
      + `${EDITOR_LABEL}</div>`,
  );
}

/**
 * Editor HTML → the canonical node the backend expects.
 *
 * This is not cosmetic. What comes out of Quill carries the editor's class, its `contenteditable` flag and
 * the human-readable LABEL as text content — and that label must never reach a recipient. Normalising here
 * means the body the backend hashes and sends is the same shape whether or not anybody opened the editor.
 */
export function fromEditorHtml(html: string | null | undefined): string {
  if (!html) return '';
  ACTION_NODE.lastIndex = 0;
  return html.replace(ACTION_NODE, () => CANONICAL_NODE);
}
