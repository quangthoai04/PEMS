/**
 * Every Quill format the email editor needs, registered once, in the shape an EMAIL can carry.
 *
 * <b>Why this file has to exist.</b> Quill's defaults are built for a web page with a stylesheet, and an
 * email has neither. Measured on quill 2.0.3, the version this app loads:
 *
 *     align  →  <p class="ql-align-center">     …a class no mail client has a rule for
 *     indent →  <p class="ql-indent-2">          …likewise
 *
 * Both arrive at the recipient as ordinary left-aligned text. Registering the STYLE attributors instead
 * produces `style="text-align: center"`, which is what V4 §7.2 asks for and what Outlook and Gmail
 * actually honour.
 *
 * `attributors/style/indent` does not exist — Quill's indent is class-only — so the margin-left indent
 * V4 §7.2 specifies is built here as a custom attributor over a fixed ladder of values.
 *
 * <b>The import that matters.</b> `Quill` comes from `react-quill-new`, never from `quill`: this project
 * has two copies (react-quill-new bundles 2.0.3, the top level is 1.3.7 for the legacy `react-quill`), and
 * registering against the wrong one compiles, runs, throws nothing, and changes nothing the user sees.
 */
import { Quill } from 'react-quill-new';
import { registerSystemActionBlot } from './emailEditorSystemNodes';
import { registerVariableChipBlot } from './emailEditorVariableChips';
import { registerEmailTableBlot } from './emailEditorTable';
import { registerTemplateBlockBlot } from './emailEditorTemplateBlocks';

/**
 * V4 §6.2. Bare family names rather than full stacks: the value has to survive a round trip through the
 * DOM and back into the whitelist, and `font-family: Georgia` reads back as `Georgia` while
 * `Georgia, serif` is re-serialised by the CSS parser in ways that no longer match. Mail clients fall back
 * on their own when a family is missing, which is the behaviour a stack would have bought.
 */
export const EMAIL_FONTS = [
  'Arial', 'Verdana', 'Tahoma', 'Trebuchet MS', 'Georgia', 'Times New Roman', 'Courier New',
] as const;

/** V4 §6.3 — a fixed ladder, so a body cannot arrive with 7px text or 96px headings. */
export const EMAIL_SIZES = ['12px', '14px', '16px', '18px', '20px', '24px', '28px', '32px'] as const;

/** V4 §7.2 — indent is a margin, and only at these steps. */
export const EMAIL_INDENTS = ['16px', '32px', '48px'] as const;

/** The class the divider blot carries, so the toolbar can find and style it. */
export const DIVIDER_BLOT_NAME = 'emailDivider';

let registered = false;

/**
 * Registers every email format. Returns whether the editor is now configured.
 *
 * Reports rather than throws, for the same reason `registerSystemActionBlot` does: this runs at module
 * scope so it happens before any editor is constructed, and several test files mock `react-quill-new`
 * with a stub that has no `Quill`. Reading a missing named export throws on the BINDING, so the guard has
 * to wrap the access rather than test the value.
 */
export function registerEmailEditorFormats(): boolean {
  if (registered) return true;

  /* eslint-disable @typescript-eslint/no-explicit-any */
  let q: any;
  try {
    q = Quill as any;
  } catch {
    return false;
  }
  if (!q || typeof q.import !== 'function' || typeof q.register !== 'function') return false;

  // ── Inline styles instead of classes ──
  // Each of these exists in Quill as BOTH a class attributor (the default) and a style attributor.
  // Registering the style one replaces the default for that format.
  const styleAttributors = [
    'attributors/style/align',
    'attributors/style/direction',
    'attributors/style/color',
    'attributors/style/background',
  ];

  for (const path of styleAttributors) {
    const attributor = q.import(path);
    if (attributor) q.register(attributor, true);
  }

  const SizeStyle = q.import('attributors/style/size');
  if (SizeStyle) {
    SizeStyle.whitelist = [...EMAIL_SIZES];
    q.register(SizeStyle, true);
  }

  const FontStyle = q.import('attributors/style/font');
  if (FontStyle) {
    FontStyle.whitelist = [...EMAIL_FONTS];
    q.register(FontStyle, true);
  }

  // ── Indent as a real margin ──
  const Parchment = q.import('parchment');
  if (Parchment?.StyleAttributor && Parchment?.Scope) {
    const IndentStyle = new Parchment.StyleAttributor('indent', 'margin-left', {
      scope: Parchment.Scope.BLOCK,
      whitelist: [...EMAIL_INDENTS],
    });
    q.register(IndentStyle, true);
  }

  // ── Divider ──
  // `<hr>` is DROPPED by default (measured: `<p>a</p><hr><p>b</p>` → `<p>a</p><p>b</p>`), because Quill
  // has no blot for it. A horizontal rule is the one separator that renders identically everywhere in
  // mail, so it is worth a blot of its own.
  const BlockEmbed = q.import('blots/block/embed');
  if (BlockEmbed) {
    class EmailDivider extends BlockEmbed {
      static blotName = DIVIDER_BLOT_NAME;
      static tagName = 'hr';

      static create(): HTMLElement {
        const node: HTMLElement = super.create();
        // Inline, because a mail client has no stylesheet to give an <hr> a colour.
        node.setAttribute('style', 'border:none;border-top:1px solid #e2e8f0;margin:20px 0');
        return node;
      }
    }
    q.register(EmailDivider, true);
  }
  /* eslint-enable @typescript-eslint/no-explicit-any */

  // All three travel with the same content and must be registered before any editor is built: Quill
  // drops what it has no blot for, so a late registration silently deletes an action area, a variable or
  // a table from the first document opened.
  registerSystemActionBlot();
  registerVariableChipBlot();
  registerEmailTableBlot();
  // The TEMPLATE spelling of a system block — `{{actionBlock}}` as an object. Registered here with the
  // rest so a template opened before it existed cannot lose one: Quill drops what it has no blot for.
  registerTemplateBlockBlot();

  registered = true;
  return true;
}

/**
 * The formats the editor will accept. Anything absent here is dropped on the way in — which is how
 * V4 §6.4's "no raw HTML" is enforced in the editor itself rather than only at the backend boundary.
 *
 * `video`, `formula`, `code-block`, `script` and `header` are deliberately excluded: an email is not a
 * document with headings and footnotes, and an <iframe> for a video is exactly what §14.2 forbids.
 */
export const EMAIL_EDITOR_FORMATS = [
  'bold', 'italic', 'underline', 'strike',
  'color', 'background', 'font', 'size',
  'align', 'indent', 'direction',
  'list', 'blockquote',
  'link', 'image',
  DIVIDER_BLOT_NAME,
  'pemsSystemActionBlock',
  'pemsTemplateBlock',
  'pemsVariable',
  'pemsEmailTable',
];
