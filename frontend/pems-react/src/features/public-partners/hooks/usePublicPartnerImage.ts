import { useEffect, useState } from 'react';
import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';

/**
 * Fetches a partner logo/cover through the anonymous, partner-scoped media proxy
 * (`/api/public/partners/media/{fileId}/content`) as a blob object URL.
 *
 * Unlike `useAuthenticatedImage`, this never depends on a session — the endpoint is
 * `[AllowAnonymous]` and only ever serves a file that is the LogoFileId/CoverFileId of an
 * APPROVED + PUBLIC partner, so anonymous visitors of the public partner pages can render it.
 *
 * Returns null while loading, on error, or when there's no file id (caller renders its own
 * fallback — initials badge for logo, gradient for cover).
 */
export function usePublicPartnerImage(fileId: number | null | undefined): string | null {
  const [objectUrl, setObjectUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!fileId) {
      setObjectUrl(null);
      return;
    }

    let cancelled = false;
    let created: string | null = null;

    (async () => {
      try {
        const { data } = await httpClient.get<Blob>(API_ENDPOINTS.publicPartners.mediaContent(fileId), {
          responseType: 'blob',
        });
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
  }, [fileId]);

  return objectUrl;
}
