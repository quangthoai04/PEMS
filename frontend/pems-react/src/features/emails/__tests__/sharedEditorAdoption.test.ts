/**
 * Every screen that authors an email PEMS sends uses the one shared editor (V4 §5.1).
 *
 * <b>Why a source scan.</b> The rule is about which component a screen renders, and a screen can stop
 * obeying it without any test failing: building a local `<ReactQuill>` with a five-button toolbar is a
 * perfectly working editor. It only goes wrong later, in someone's inbox, when the formatting an author
 * reached for was not among the five — or was among them here and not there, so the same wording came out
 * differently depending on which screen it was typed into. That is exactly what happened: the template
 * screen and the send modal each had their own, and they had already drifted apart on images, fonts and
 * alignment before anybody noticed.
 *
 * <b>Scope.</b> Only the surfaces that write mail through the template/compose pipeline. The legacy
 * `CreateEmail`/`EditEmail`/`EmailDetail` prototypes and the news editors are deliberately NOT listed:
 * they are a different question, and widening this to "no file may import ReactQuill" would turn one
 * honest rule into a lint everyone learns to work around.
 */
import { readFileSync } from 'fs';
import { resolve } from 'path';
import { describe, expect, it } from 'vitest';

/** `src/` — this file lives at `src/features/emails/__tests__/`. */
const ROOT = resolve(__dirname, '../../..');

/** The screens that compose or author outbound email content. */
const AUTHORING_SURFACES = [
  'features/emails/components/EmailComposeModal.tsx',
  'features/delegations/components/EmailPreviewModal.tsx',
  'pages/dashboard/emails/TemplateManagement.tsx',
];

function read(relative: string): string {
  return readFileSync(resolve(ROOT, relative), 'utf8');
}

describe('the shared editor is the only editor for outbound email', () => {
  it.each(AUTHORING_SURFACES)('%s renders EmailRichTextEditor', (file) => {
    expect(read(file)).toMatch(/<EmailRichTextEditor\b/);
  });

  it.each(AUTHORING_SURFACES)('%s builds no editor of its own', (file) => {
    const source = read(file);

    // An import of the library itself. `EmailRichTextEditor` imports it — that is the one place that may.
    expect(source).not.toMatch(/from\s+['"]react-quill(-new)?['"]/);
    expect(source).not.toMatch(/from\s+['"]quill['"]/);

    // A toolbar container is the tell for a second editor even where the import came in indirectly.
    expect(source).not.toMatch(/toolbar\s*:\s*\[/);
    expect(source).not.toMatch(/<ReactQuill\b/);
  });

  /**
   * The composer places its inline images through the editor rather than through a Quill ref of its own.
   *
   * Reaching for the ref was how the old screen inserted an upload at the caret, and it is a second
   * implementation of placement — the part of the job the editor already owns. What stays here is the
   * inline-image MAP, which is genuinely this screen's: `finalizeBody` needs it to rewrite each src to
   * `cid:{contentId}` before the message is built.
   */
  it('the composer keeps the cid mapping and gives up the caret', () => {
    const source = read('features/emails/components/EmailComposeModal.tsx');

    expect(source).toMatch(/onUploadImage=\{uploadInlineImage\}/);
    expect(source).toMatch(/inlineMapRef\.current\.set\(proxyUrl/);
    expect(source).not.toMatch(/quillRef/);
    expect(source).not.toMatch(/\.insertEmbed\(/);   // the CALL; the word survives in a comment
  });

  /**
   * …and the mode each surface asks for, which is the whole of the difference between them.
   */
  it.each([
    ['pages/dashboard/emails/TemplateManagement.tsx', 'TEMPLATE'],
    ['features/emails/components/EmailComposeModal.tsx', 'COMPOSE'],
    ['features/delegations/components/EmailPreviewModal.tsx', 'COMPOSE'],
  ])('%s asks for %s mode', (file, mode) => {
    expect(read(file)).toMatch(new RegExp(`mode="${mode}"`));
  });
});
