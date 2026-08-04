/**
 * The one place the editor asks whether content carries `{{contactInformationBlock}}`, and the one place
 * it removes one.
 *
 * <p>Mirrors `EmailContactBlockText` on the backend, deliberately down to which spellings count. The
 * detection decides whether the save button is disabled and whether a modal appears; the backend's
 * decides whether the save is refused. If the two disagreed, the screen would either disable a save the
 * API would have accepted, or offer one it is about to refuse — and the second of those is how an
 * operator ends up staring at a server error about a block the screen told them was fine.</p>
 *
 * Written as a module rather than as a regex inlined at each call site because there were four call
 * sites and they would not have stayed identical.
 */

/** The block's placeholder name, as the backend's `EmailTrustedBlocks.ContactInformationBlock` spells it. */
export const CONTACT_BLOCK_NAME = 'contactInformationBlock';

/** The literal an operator sees in the editor. */
export const CONTACT_BLOCK_MARKER = `{{${CONTACT_BLOCK_NAME}}}`;

/**
 * Every form of the placeholder this editor may encounter.
 *
 * Two spellings, not one. `{{contactInformationBlock}}` is what is stored; `%7B%7B…%7D%7D` is what a
 * rich-text editor produces when the placeholder passes through a URL — Quill does this to text inside
 * an anchor. Both resolve at send time, so both count as "the block is present"; matching only the first
 * would let an encoded one survive a NONE policy and reach the send path, where nothing would replace it.
 *
 * The name is matched EXACTLY. No case folding and no partial match, so `{{contactInformationBlockX}}`
 * and `{{contactinformationblock}}` are not this block — the backend's substitution is case-sensitive, so
 * neither of those would ever be replaced, and treating them as the block here would offer a removal that
 * fixes nothing while hiding the real fault (an unknown placeholder) behind the wrong message.
 *
 * Built fresh per call rather than held at module scope: a `/g` regex carries `lastIndex` between uses,
 * and a shared one silently returns false on every other `.test()`.
 */
function blockPattern(): RegExp {
  return new RegExp(`(?:\\{\\{|%7B%7B)\\s*${CONTACT_BLOCK_NAME}\\s*(?:\\}\\}|%7D%7D)`, 'g');
}

/** True when this content carries the contact block at least once, in either spelling. */
export function containsContactInformationBlock(content: string | null | undefined): boolean {
  if (!content) return false;
  return blockPattern().test(content);
}

/**
 * Removes every occurrence of the contact block, and nothing else.
 *
 * <p><b>What it deliberately does not do.</b> It does not touch the text around the block, does not
 * reflow paragraphs, and does not strip attributes — an operator wrote that text and a "tidy-up" they did
 * not ask for is an edit they cannot see. The single exception is a paragraph the removal has emptied:
 * `<p>{{contactInformationBlock}}</p>` is markup that existed only to hold the block, and leaving
 * `<p></p>` behind adds a blank line to every mail sent from that template. A paragraph that still has
 * other content is left exactly as it is, blank line or not.</p>
 */
export function removeContactInformationBlock(content: string | null | undefined): string {
  if (!content) return '';

  const withoutBlock = content.replace(blockPattern(), '');

  // Only paragraphs and list items that the removal EMPTIED. The `&nbsp;`/`<br>` alternatives are there
  // because that is what a rich-text editor leaves in a paragraph it considers blank, not because
  // whitespace is being trimmed generally.
  return withoutBlock.replace(
    /<(p|li|div)(\s[^>]*)?>(?:\s|&nbsp;|<br\s*\/?>)*<\/\1>/gi,
    '',
  );
}

/**
 * Removes the block from both languages at once, which is the only way the action is ever offered.
 *
 * <p>One function rather than two calls at each site because "xóa khối khỏi nội dung" means both bodies:
 * a policy is one setting for the whole template, so leaving the block in the English body while removing
 * it from the Vietnamese one produces a template that is still refused, having asked the operator to fix
 * it once already.</p>
 */
export function removeContactBlockFromBoth(bodies: { vi: string; en: string }): { vi: string; en: string } {
  return {
    vi: removeContactInformationBlock(bodies.vi),
    en: removeContactInformationBlock(bodies.en),
  };
}
