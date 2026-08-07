/**
 * The one pipeline behind "Xem trước hiển thị" on the template screen (V4 §21, §22).
 *
 * <b>Why it is a function and not four expressions in a component.</b> The preview used to be assembled
 * inline, in the middle of a 1700-line screen: substitute samples, substitute blocks, sanitise at the
 * point of render. Every step was correct and none of them was testable on its own, so the ordering rule
 * that makes it correct — samples BEFORE blocks — lived only in a comment next to the JSX. That ordering
 * is not cosmetic: a block's sample markup is trusted HTML built by the backend, and running the variable
 * substitution over it afterwards would let a sample value be scanned as a template.
 *
 * <b>What it is, and what it is emphatically not.</b> This shows the SHAPE of a message using the
 * contract's sample values and inert block markup. It is not evidence that a send will work: the real
 * values, the real action links and the real setup tables come from the runtime render on the backend,
 * against a real context, and that path is proved by its own tests. Two things are true at once — this
 * preview must agree with the editor, and it can never stand in for the final preview.
 *
 * <b>Input is the canonical draft, never the editor's DOM.</b> `formData` is what would be saved; reading
 * `.ql-editor.innerHTML` instead would preview chips, wrappers and `contenteditable` attributes that are
 * editor furniture, and would show something no recipient will ever receive.
 */
import { sanitizeHtml } from '../../../shared/security/sanitizeHtml';
import {
  type TemplateContract, applySamples, applySystemBlocks,
} from '../types/templateContract';

export interface TemplateDraftPreview {
  /** Plain text, samples substituted. A subject is a header — never markup. */
  subject: string;
  /** Sanitised HTML, ready to render. */
  bodyHtml: string;
}

/** One canonical draft: exactly the two fields of the language currently being edited. */
export interface TemplateDraft {
  subject: string;
  body: string;
}

/**
 * Builds the preview for one language of a draft.
 *
 * With no contract yet — it is fetched per template and may still be in flight — the draft is shown as it
 * stands, still carrying its placeholders. That is deliberate: substituting from a stale or absent
 * contract would print another template's sample values, and blanking the pane would suggest the content
 * is empty.
 */
export function buildTemplateDraftPreview(
  contract: TemplateContract | null | undefined,
  draft: TemplateDraft,
): TemplateDraftPreview {
  const subject = contract ? applySamples(contract, draft.subject ?? '') : (draft.subject ?? '');

  // Variables first, then blocks — see the note above. Sanitising last, once, means the sample markup
  // the backend supplied is held to the same rules as the author's own content.
  const substituted = contract
    ? applySystemBlocks(contract, applySamples(contract, draft.body ?? ''))
    : (draft.body ?? '');

  return { subject, bodyHtml: sanitizeHtml(substituted) };
}
