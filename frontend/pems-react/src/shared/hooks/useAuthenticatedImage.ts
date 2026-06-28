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

export type AuthenticatedMediaStatus = 'idle' | 'loading' | 'loaded' | 'error';

export interface AuthenticatedMediaResult {
  url: string | null;
  status: AuthenticatedMediaStatus;
}

/**
 * Like {@link useAuthenticatedImage} but exposes a load status so callers can render a placeholder
 * instead of spinning forever when the underlying file can't be fetched (e.g. a Drive object that no
 * longer exists / isn't configured → the backend returns 422). Fetches at most once per `path`.
 */
export function useAuthenticatedMedia(path: string | null | undefined): AuthenticatedMediaResult {
  const [state, setState] = useState<AuthenticatedMediaResult>({ url: null, status: 'idle' });

  useEffect(() => {
    if (!path) {
      setState({ url: null, status: 'idle' });
      return;
    }

    let cancelled = false;
    let created: string | null = null;
    setState({ url: null, status: 'loading' });

    (async () => {
      try {
        const { data } = await httpClient.get<Blob>(path, { baseURL: '', responseType: 'blob' });
        if (cancelled) return;
        created = URL.createObjectURL(data);
        setState({ url: created, status: 'loaded' });
      } catch {
        if (!cancelled) setState({ url: null, status: 'error' });
      }
    })();

    return () => {
      cancelled = true;
      if (created) URL.revokeObjectURL(created);
    };
  }, [path]);

  return state;
}
