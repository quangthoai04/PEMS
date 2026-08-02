/**
 * Which stored files this app will render, and which it will only hand over.
 *
 * The allowlist is deliberately short. A preview puts bytes somebody else uploaded into this page's
 * document, so the question is not "can a browser display this?" — it is "is displaying this ever a
 * way to run something?". HTML, SVG, XHTML and XML all carry script and are therefore never rendered
 * inline no matter what the row claims about them; they are perfectly good downloads instead.
 *
 * The backend enforces the same refusal independently (`FileResponseSafety.SafeInlineContentType`
 * reports those types as `application/octet-stream` on the inline route), so a file that lies about
 * its type cannot be rendered as a page even if this list were wrong.
 */

export type FilePreviewKind = 'pdf' | 'image' | 'text' | 'unsupported';

/** The shape every attachment surface already has: an id, a name, and what the backend knows of it. */
export interface PreviewableFile {
  /**
   * Null/undefined while an upload is still in flight. Nothing may be fetched for such a file — there
   * is no row to authorize yet, so asking would be a guaranteed 404 with a confusing message.
   */
  fileId?: number | null;
  name: string;
  mimeType?: string | null;
  size?: number | null;
}

/**
 * A text file is read into the page as a string, so it is capped. 1 MB is far above any real note or
 * log excerpt and far below the point where rendering one would freeze the tab.
 */
export const TEXT_PREVIEW_MAX_BYTES = 1024 * 1024;

const IMAGE_MIME_TYPES = new Set([
  'image/png',
  'image/jpeg',
  'image/jpg',
  'image/webp',
  'image/gif',
]);

const PDF_MIME_TYPES = new Set(['application/pdf']);

const TEXT_MIME_TYPES = new Set(['text/plain']);

/**
 * Types that must never reach an inline renderer, checked BEFORE the extension fallback so a
 * `.png`-named HTML file cannot slip through by having its MIME ignored.
 */
const NEVER_INLINE_MIME_TYPES = new Set([
  'text/html',
  'application/xhtml+xml',
  'image/svg+xml',
  'text/xml',
  'application/xml',
  'text/xsl',
  'application/xslt+xml',
  'text/javascript',
  'application/javascript',
]);

const NEVER_INLINE_EXTENSIONS = new Set([
  '.html', '.htm', '.xhtml', '.svg', '.xml', '.xsl', '.js', '.mjs', '.mhtml',
]);

/** Only consulted when the backend told us nothing usable — never to override a MIME it did send. */
const EXTENSION_FALLBACK: Record<string, FilePreviewKind> = {
  '.pdf': 'pdf',
  '.png': 'image',
  '.jpg': 'image',
  '.jpeg': 'image',
  '.webp': 'image',
  '.gif': 'image',
  '.txt': 'text',
};

/** MIME values that carry no information, so the name is the only thing left to go on. */
const UNINFORMATIVE_MIME_TYPES = new Set([
  '',
  'application/octet-stream',
  'binary/octet-stream',
  'application/unknown',
]);

/** The bare type, lower-cased, with any `; charset=…` parameter dropped. */
function bareMimeType(mimeType?: string | null): string {
  return (mimeType ?? '').split(';')[0].trim().toLowerCase();
}

export function fileExtension(name?: string | null): string {
  const value = (name ?? '').trim().toLowerCase();
  const dot = value.lastIndexOf('.');
  return dot > 0 ? value.slice(dot) : '';
}

/**
 * How this file should be shown, if at all.
 *
 * MIME from the backend decides. The filename extension is a FALLBACK for rows whose `mime_type` is
 * null or `application/octet-stream` — it never overrides a type the backend did supply, and it can
 * never promote a file into a renderable kind that the deny-list covers.
 */
export function resolvePreviewKind(file: PreviewableFile | null | undefined): FilePreviewKind {
  if (!file) return 'unsupported';

  const mime = bareMimeType(file.mimeType);
  const extension = fileExtension(file.name);

  // Refuse first, on either signal. A disagreement between the two is itself a reason not to render.
  if (NEVER_INLINE_MIME_TYPES.has(mime) || NEVER_INLINE_EXTENSIONS.has(extension)) return 'unsupported';

  if (PDF_MIME_TYPES.has(mime)) return 'pdf';
  if (IMAGE_MIME_TYPES.has(mime)) return 'image';
  if (TEXT_MIME_TYPES.has(mime)) return withinTextLimit(file) ? 'text' : 'unsupported';

  if (UNINFORMATIVE_MIME_TYPES.has(mime)) {
    const guess = EXTENSION_FALLBACK[extension];
    if (guess === 'text') return withinTextLimit(file) ? 'text' : 'unsupported';
    if (guess) return guess;
  }

  return 'unsupported';
}

function withinTextLimit(file: PreviewableFile): boolean {
  return file.size == null || file.size <= TEXT_PREVIEW_MAX_BYTES;
}

/** True when a file has bytes to fetch AND a kind this app renders. */
export function canPreview(file: PreviewableFile | null | undefined): boolean {
  return hasStoredBytes(file) && resolvePreviewKind(file) !== 'unsupported';
}

/**
 * True once the file exists server-side. An attachment still uploading has no `file_id`, and every
 * view/download control stays inert until it does.
 */
export function hasStoredBytes(file: PreviewableFile | null | undefined): file is PreviewableFile & { fileId: number } {
  return typeof file?.fileId === 'number' && file.fileId > 0;
}
