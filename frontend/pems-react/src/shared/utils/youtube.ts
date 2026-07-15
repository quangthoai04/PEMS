/**
 * Client-side YouTube URL helpers (mirrors the backend YouTubeUrlParser). Used only for previews and
 * input validation in the gallery editor — the backend is always the authority and re-validates on submit.
 */

const WATCH_HOSTS = new Set(['youtube.com', 'www.youtube.com', 'm.youtube.com', 'music.youtube.com']);
const SHORT_HOST = 'youtu.be';
const VIDEO_ID_RE = /^[A-Za-z0-9_-]{11}$/;

/** Extracts a canonical 11-char YouTube video id from a supported URL, or null when invalid. */
export function parseYouTubeVideoId(input: string | null | undefined): string | null {
  if (!input) return null;
  const raw = input.trim();
  if (!raw || raw.length > 400) return null;

  let uri: URL;
  try {
    uri = new URL(raw);
  } catch {
    return null;
  }
  if (uri.protocol !== 'http:' && uri.protocol !== 'https:') return null;

  const host = uri.host.toLowerCase();
  let candidate: string | null = null;

  if (host === SHORT_HOST) {
    candidate = uri.pathname.replace(/^\/+/, '').split('/')[0] || null;
  } else if (WATCH_HOSTS.has(host)) {
    const segments = uri.pathname.replace(/^\/+|\/+$/g, '').split('/').filter(Boolean);
    if (segments.length === 0) {
      candidate = uri.searchParams.get('v');
    } else {
      const first = segments[0].toLowerCase();
      if (first === 'watch') candidate = uri.searchParams.get('v');
      else if (['shorts', 'embed', 'v', 'live'].includes(first) && segments.length >= 2) candidate = segments[1];
    }
  } else {
    return null;
  }

  return candidate && VIDEO_ID_RE.test(candidate) ? candidate : null;
}

export const youtubeWatchUrl = (id: string) => `https://www.youtube.com/watch?v=${id}`;
export const youtubeEmbedUrl = (id: string) => `https://www.youtube-nocookie.com/embed/${id}`;
export const youtubeThumbnailUrl = (id: string) => `https://i.ytimg.com/vi/${id}/hqdefault.jpg`;
