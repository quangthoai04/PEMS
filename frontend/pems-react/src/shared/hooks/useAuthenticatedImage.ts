import { useEffect, useState } from 'react';
import httpClient from '../api/httpClient';

/**
 * Fetches an image that lives behind an authenticated backend route (e.g.
 * `/api/files/{id}/content`) as a blob and returns a local object URL usable in `<img src>`.
 *
 * A plain `<img src="/api/files/.../content">` cannot work because the JWT lives in localStorage,
 * not a cookie — the browser would send the request without an Authorization header. Going through
 * httpClient lets the auth interceptor attach the Bearer token.
 *
 * `path` is expected to already start with `/api` (as `users.avatar_url` does), so we override
 * baseURL to '' to avoid the `/api` double-prefix from httpClient's default baseURL.
 *
 * Returns null while loading or on error (caller falls back to a default avatar).
 */
export function useAuthenticatedImage(path: string | null | undefined): string | null {
  const [objectUrl, setObjectUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!path) {
      setObjectUrl(null);
      return;
    }

    let cancelled = false;
    let created: string | null = null;

    (async () => {
      try {
        const { data } = await httpClient.get<Blob>(path, { baseURL: '', responseType: 'blob' });
        if (cancelled) return;
        created = URL.createObjectURL(data);
        setObjectUrl(created);
      } catch {
        if (!cancelled) setObjectUrl(null);
      }
    })();

    return () => {
      cancelled = true;
      if (created) URL.revokeObjectURL(created);
    };
  }, [path]);

  return objectUrl;
}
