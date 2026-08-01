import { useCallback, useEffect, useRef, useState } from 'react';
import { accountManagementApi } from '../api/accountManagementApi';
import { getAccountErrorMessage } from '../api/accountError';

interface UseRelatedVisitorNationalitiesResult {
  /** Real, campus-scoped nationalities — empty while loading, on error, or when there are none. */
  options: string[];
  loading: boolean;
  error: string | null;
  /** Retry after a failure; also used to refresh the options. */
  retry: () => void;
}

/**
 * Staff Leader "Visitor liên quan" tab — the nationality filter options.
 *
 * Kept apart from `useRelatedVisitors` on purpose: the dropdown loads and fails independently of
 * the table, so a nationality request that errors leaves the Visitor list working (and offers its
 * own retry) instead of blanking the screen.
 *
 * `enabled=false` skips the call entirely — the internal-accounts mode must not touch this
 * endpoint. Stale responses are dropped (last request wins).
 */
export function useRelatedVisitorNationalities(
  enabled = true,
): UseRelatedVisitorNationalitiesResult {
  const [options, setOptions] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const requestIdRef = useRef(0);

  const load = useCallback(async () => {
    if (!enabled) return;
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);

    try {
      const result = await accountManagementApi.getRelatedVisitorNationalities();
      if (requestId !== requestIdRef.current) return;
      setOptions(Array.isArray(result?.items) ? result.items : []);
    } catch (err) {
      if (requestId !== requestIdRef.current) return;
      setError(getAccountErrorMessage(err, 'Không thể tải danh sách quốc tịch. Vui lòng thử lại.'));
      setOptions([]);
    } finally {
      if (requestId === requestIdRef.current) setLoading(false);
    }
  }, [enabled]);

  useEffect(() => {
    if (!enabled) {
      // Leaving Visitor mode invalidates any in-flight response, so a late arrival cannot
      // repopulate the dropdown of a mode that no longer wants it.
      requestIdRef.current++;
      setOptions([]);
      setLoading(false);
      setError(null);
      return;
    }
    void load();
  }, [enabled, load]);

  return { options, loading, error, retry: load };
}

export default useRelatedVisitorNationalities;
