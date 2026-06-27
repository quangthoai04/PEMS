/**
 * Helpers for inline images in the email rich editor.
 *
 * In the editor an inline image is `<img src="{dataUrl}" data-content-id="{cid}" data-file-id="{id}">`
 * so it previews instantly. Before saving/sending we swap the bulky data: URL for `cid:{contentId}`
 * (compact + what the MIME builder expects). On reload, cid images are resolved back to authenticated
 * blob URLs for preview.
 */
import { filesApi } from '../../../shared/api/filesApi';

export interface InlineImageRef {
  fileId: number;
  contentId: string;
}

/** A stable content-id for an inline image (unique within one email/draft). */
export function contentIdForFile(fileId: number): string {
  return `pems-inline-${fileId}`;
}

/**
 * Replaces inline-image `src` (data: or blob: URLs) with `cid:{contentId}` for any `<img>` carrying a
 * `data-content-id`. Used right before persisting/sending so the stored body references cids.
 */
export function bodyToCidHtml(html: string): string {
  if (!html || typeof window === 'undefined' || !window.DOMParser) return html;
  const doc = new window.DOMParser().parseFromString(html, 'text/html');
  doc.querySelectorAll('img[data-content-id]').forEach((img) => {
    const cid = img.getAttribute('data-content-id');
    if (cid) img.setAttribute('src', `cid:${cid}`);
  });
  return doc.body.innerHTML;
}

/** Collects the inline-image refs (fileId + contentId) currently present in the editor body. */
export function collectInlineImages(html: string): InlineImageRef[] {
  const out: InlineImageRef[] = [];
  if (!html || typeof window === 'undefined' || !window.DOMParser) return out;
  const doc = new window.DOMParser().parseFromString(html, 'text/html');
  doc.querySelectorAll('img[data-content-id][data-file-id]').forEach((img) => {
    const cid = img.getAttribute('data-content-id');
    const fid = Number(img.getAttribute('data-file-id'));
    if (cid && fid) out.push({ fileId: fid, contentId: cid });
  });
  return out;
}

/**
 * Resolves `<img src="cid:..">` references back to authenticated blob object URLs so a reloaded draft
 * or a sent-email body shows its inline images. `cidToFileId` maps each content-id to its file id.
 * Returns the rewritten HTML (best-effort; unresolved cids are left as-is).
 */
export async function resolveCidImages(
  html: string,
  cidToFileId: Record<string, number>,
): Promise<string> {
  if (!html || typeof window === 'undefined' || !window.DOMParser) return html;
  const doc = new window.DOMParser().parseFromString(html, 'text/html');
  const imgs = Array.from(doc.querySelectorAll('img[src^="cid:"]'));
  await Promise.all(
    imgs.map(async (img) => {
      const src = img.getAttribute('src') || '';
      const cid = src.slice(4); // strip "cid:"
      const fileId = cidToFileId[cid];
      if (!fileId) return;
      try {
        const url = await filesApi.fetchObjectUrl(fileId);
        img.setAttribute('src', url);
        img.setAttribute('data-content-id', cid);
        img.setAttribute('data-file-id', String(fileId));
      } catch {
        /* leave the cid src in place if the blob can't be fetched */
      }
    }),
  );
  return doc.body.innerHTML;
}
