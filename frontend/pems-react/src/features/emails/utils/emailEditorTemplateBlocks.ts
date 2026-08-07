/**
 * System blocks inside a TEMPLATE, as objects rather than as text (V4 §5, §7).
 *
 * <b>Two spellings, and why they are not the same thing.</b> A system block is markup the backend builds
 * at send time — the action buttons with their one-time links, the setup-summary tables with real visit
 * data. It appears in two places, in two forms, and conflating them is what this file exists to stop:
 *
 *   TEMPLATE (this file)   `{{actionBlock}}` in the stored body. The renderer substitutes it there, so
 *                          the position an author chose is the position the recipient sees.
 *   COMPOSE (the other)    `<div data-system-block="action">` in a message somebody is editing at send
 *                          time — see `emailEditorSystemNodes.ts`. That node marks a position inside
 *                          content that has ALREADY been rendered, where a placeholder would be text an
 *                          author could type, duplicate, or move into a subject.
 *
 * <b>The defect this closes.</b> The shared editor only knew the COMPOSE spelling. A template body was
 * handed to `variablesToChips`, which matches any `{{name}}`, so `{{actionBlock}}` became an ordinary
 * variable chip labelled "actionBlock": indistinguishable from a data variable, offering no hint that the
 * system builds it, and deletable as if it were one. Worse, "Chèn khối nút phản hồi" inserted the COMPOSE
 * node into template content — which the renderer never substitutes, because it substitutes
 * `{{actionBlock}}`. The saved template then carried an empty `<div>` where its buttons should be, and
 * the recipient got a message with nothing to press.
 *
 * <b>What a block looks like here.</b> One atomic block embed, not editable inside, carrying the block
 * NAME in a data attribute and a human title as its text. It can be selected, moved and deleted whole —
 * which is the contract V4 §5 asks for — and it serialises back to exactly the placeholder it came from.
 */
import { Quill } from 'react-quill-new';
import { SYSTEM_BLOCK_LABELS, SYSTEM_BLOCK_NAMES } from '../types/templateContract';

export const TEMPLATE_BLOCK_BLOT_NAME = 'pemsTemplateBlock';
export const TEMPLATE_BLOCK_CLASS = 'pems-template-block';
export const TEMPLATE_BLOCK_ATTRIBUTE = 'data-template-block';

/** What a block node carries: the placeholder name, and what a person calls it. */
export interface TemplateBlockValue {
  name: string;
  label: string;
}

/** The title shown inside the node. Falls back to the raw name so an unknown block is still visible. */
export function templateBlockLabel(name: string): string {
  return SYSTEM_BLOCK_LABELS[name]?.title ?? name;
}

/** Matches one rendered block node, however Quill or a sanitiser rewrote its attributes. */
const BLOCK_NODE = new RegExp(
  `<div\\b[^>]*\\b${TEMPLATE_BLOCK_ATTRIBUTE}\\s*=\\s*"([^"]+)"[^>]*>[^<]*</div\\s*>`,
  'gi',
);

/** Matches `{{blockName}}` for the blocks the backend actually builds — and nothing else. */
function placeholderPattern(): RegExp {
  return new RegExp(
    `(?:\\{\\{|%7B%7B)\\s*(${SYSTEM_BLOCK_NAMES.join('|')})\\s*(?:\\}\\}|%7D%7D)`,
    'g',
  );
}

let registered = false;

/**
 * Registers the block blot. Reports rather than throws, for the reason every registration here does: it
 * runs at module scope, and several test files mock `react-quill-new` with a stub carrying no `Quill`.
 */
export function registerTemplateBlockBlot(): boolean {
  if (registered) return true;

  /* eslint-disable @typescript-eslint/no-explicit-any */
  let q: any;
  try {
    q = Quill as any;
  } catch {
    return false;
  }
  if (!q || typeof q.import !== 'function' || typeof q.register !== 'function') return false;

  const BlockEmbed: any = q.import('blots/block/embed');
  if (!BlockEmbed) return false;

  class TemplateBlock extends BlockEmbed {
    static blotName = TEMPLATE_BLOCK_BLOT_NAME;
    static tagName = 'div';
    static className = TEMPLATE_BLOCK_CLASS;

    static create(value: TemplateBlockValue | string): HTMLElement {
      const node: HTMLElement = super.create(value);
      const name = typeof value === 'string' ? value : value?.name ?? '';
      const label = typeof value === 'string' ? templateBlockLabel(value) : value?.label ?? name;

      node.setAttribute(TEMPLATE_BLOCK_ATTRIBUTE, name);
      // Movable, not alterable: the sentence around a block is the author's, its contents are not.
      node.setAttribute('contenteditable', 'false');
      node.textContent = label;
      return node;
    }

    static value(node: HTMLElement): TemplateBlockValue {
      const name = node.getAttribute(TEMPLATE_BLOCK_ATTRIBUTE) ?? '';
      return { name, label: node.textContent || templateBlockLabel(name) };
    }
  }

  q.register(TemplateBlock, true);
  /* eslint-enable @typescript-eslint/no-explicit-any */
  registered = true;
  return true;
}

/**
 * Stored template → editor: `{{actionBlock}}` becomes the protected node.
 *
 * MUST run before `variablesToChips`, which matches any `{{name}}` and would otherwise turn a block into
 * a data-variable chip. Only the names the backend actually builds are converted — a mistyped
 * `{{actionBlok}}` stays a variable so the contract check can report it as the unknown variable it is.
 */
export function templateBlocksToNodes(html: string | null | undefined): string {
  if (!html) return '';

  return html.replace(placeholderPattern(), (_whole, name: string) =>
    `<div class="${TEMPLATE_BLOCK_CLASS}" ${TEMPLATE_BLOCK_ATTRIBUTE}="${name}"`
    + ` contenteditable="false">${escapeText(templateBlockLabel(name))}</div>`);
}

/**
 * Editor → stored template: the node becomes its placeholder again.
 *
 * The human title is never stored and never sent — only the name the renderer substitutes survives, so
 * renaming a block on screen cannot change what the recipient receives.
 */
export function nodesToTemplateBlocks(html: string | null | undefined): string {
  if (!html) return '';

  BLOCK_NODE.lastIndex = 0;
  return html.replace(BLOCK_NODE, (_whole: string, name: string) => `{{${name}}}`);
}

/** How many times one block appears in stored content — the count §9.5 caps at one. */
export function countTemplateBlocks(html: string | null | undefined, name: string): number {
  if (!html) return 0;

  const pattern = new RegExp(`(?:\\{\\{|%7B%7B)\\s*${name}\\s*(?:\\}\\}|%7D%7D)`, 'g');
  return (html.match(pattern) ?? []).length;
}

function escapeText(value: string): string {
  return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}
