/**
 * Fetches the recipient ceiling from the server.
 *
 * The number is `EmailRecipientOptions.MaxRecipients`, the same value `EmailRecipientValidator` will
 * enforce. It is fetched rather than duplicated so that raising or lowering the limit in configuration
 * takes effect in the UI without a frontend release.
 *
 * When the request fails the hook reports `null`, and the caller shows a notice instead of a counter.
 * It deliberately does NOT fall back to a number: a guessed ceiling either blocks sends the server
 * would have accepted, or promises room the server will refuse. The draft is untouched either way, and
 * the server remains the thing that actually enforces the limit.
 */
import { useEffect, useState } from 'react';
import { emailsApi } from '../api/emailsApi';
import { isUsableLimit, type RecipientLimit } from '../types/recipients';

export type RecipientLimitStatus = 'loading' | 'ready' | 'unavailable';

export interface UseRecipientLimitResult {
  limit: RecipientLimit;
  status: RecipientLimitStatus;
}

export function useRecipientLimit(enabled = true): UseRecipientLimitResult {
  const [limit, setLimit] = useState<RecipientLimit>(null);
  const [status, setStatus] = useState<RecipientLimitStatus>(enabled ? 'loading' : 'unavailable');

  useEffect(() => {
    if (!enabled) return;
    let cancelled = false;

    (async () => {
      try {
        const response = await emailsApi.getRecipientLimits();
        if (cancelled) return;

        const value = response?.data?.maxRecipients;
        // A non-positive or absent configuration is not a usable ceiling. Treated as unavailable
        // rather than rendered as "3/0"; the server refuses such an envelope regardless.
        if (isUsableLimit(value)) {
          setLimit(value);
          setStatus('ready');
        } else {
          setLimit(null);
          setStatus('unavailable');
        }
      } catch {
        if (cancelled) return;
        setLimit(null);
        setStatus('unavailable');
      }
    })();

    return () => { cancelled = true; };
  }, [enabled]);

  return { limit, status };
}

export default useRecipientLimit;
