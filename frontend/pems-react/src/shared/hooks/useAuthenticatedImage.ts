import { useEffect, useState } from 'react';
import httpClient from '../api/httpClient';

interface AuthenticatedImage {
  /** Object URL for the fetched blob, or null while loading / on error / when no path. */
  src: string | null;
  loading: boolean;
}

/**
 * Fetches an image served by an authenticated backend endpoint (e.g. the avatar proxy
 * `/api/files/{id}/content`) and exposes it as a blob object URL. A plain `<img src>` cannot
 * attach the Bearer token, so we fetch through httpClient (which does) and hand back an object
 * URL. The URL is revoked on change/unmount to avoid leaks.
 *
 * `path` is used as an origin-relative URL (baseURL is bypassed) so an already `/api`-prefixed
 * value like `/api/files/123/content` is not double-prefixed.
 */
export function useAuthenticatedImage(path: string | null | undefined): AuthenticatedImage {
  const [src, setSrc] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!path) {
      setSrc(null);
      return;
    }

    let cancelled = false;
    let objectUrl: string | null = null;
    setLoading(true);

    httpClient
      .get(path, { baseURL: '', responseType: 'blob' })
      .then((res) => {
        if (cancelled) return;
        objectUrl = URL.createObjectURL(res.data as Blob);
        setSrc(objectUrl);
      })
      .catch(() => {
        if (!cancelled) setSrc(null);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [path]);

  return { src, loading };
}
