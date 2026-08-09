/**
 * Sharing a public VisitFPTU gallery item.
 *
 * There is ONE shared URL — the canonical deep link
 * `/visit-fptu/{campusCode}?locationId={locationId}&itemId={galleryItemId}` — used by both the
 * "Sao chép liên kết" action and the Facebook share window. It is rebuilt from the item's own
 * campus/location/item ids rather than read off `window.location.href`, so the link never carries
 * whatever unrelated query params the page happens to hold at that moment (and never points at a
 * stale item after the modal stepped to the next one).
 *
 * A Facebook crawler hitting that same URL is rewritten by Vercel to the backend Open Graph endpoint,
 * so the card shows the item's real title/description/image — nothing extra is needed here (no
 * Facebook SDK, no App ID, no separate /share/ URL).
 */

export interface GalleryShareTarget {
  campusCode: string;
  locationId: number;
  galleryItemId: number;
}

/** The canonical, absolute deep link of one gallery item. */
export function buildGalleryShareUrl({
  campusCode,
  locationId,
  galleryItemId,
}: GalleryShareTarget): string {
  const url = new URL(`/visit-fptu/${campusCode.toLowerCase()}`, window.location.origin);
  url.searchParams.set('locationId', String(locationId));
  url.searchParams.set('itemId', String(galleryItemId));
  return url.toString();
}

/** Facebook's sharer — it scrapes the URL itself, so only the encoded link is passed. */
export function buildFacebookShareUrl(url: string): string {
  return `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(url)}`;
}

/** Opens the Facebook share dialog in a popup window (never the current tab). */
export function openFacebookShare(url: string): void {
  window.open(buildFacebookShareUrl(url), '_blank', 'noopener,noreferrer,width=640,height=720');
}

/**
 * Copies text to the clipboard. The async Clipboard API is unavailable on http origins and in older
 * browsers, so a hidden-textarea + `execCommand('copy')` fallback keeps the action working there.
 * Resolves to whether the text actually reached the clipboard — the caller decides which toast to show.
 */
export async function copyTextToClipboard(text: string): Promise<boolean> {
  if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      // Permission denied / insecure context → try the legacy path below.
    }
  }

  if (typeof document === 'undefined') return false;

  try {
    const textarea = document.createElement('textarea');
    textarea.value = text;
    // Keep it out of view and out of the tab order, but still selectable.
    textarea.setAttribute('readonly', '');
    textarea.style.position = 'fixed';
    textarea.style.top = '-1000px';
    textarea.style.opacity = '0';
    document.body.appendChild(textarea);
    textarea.select();
    const ok = document.execCommand('copy');
    document.body.removeChild(textarea);
    return ok;
  } catch {
    return false;
  }
}
