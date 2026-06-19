import { useCallback, useEffect, useRef, useState } from 'react';
import { accountManagementApi } from '../api/accountManagementApi';
import { getAccountErrorMessage } from '../api/accountError';
import type {
  AccountListItem,
  AccountListQueryParams,
  PaginatedResult,
} from '../types/accountManagement.types';

interface UseAccountListResult {
  data: PaginatedResult<AccountListItem> | null;
  accounts: AccountListItem[];
  loading: boolean;
  error: string | null;
  refetch: () => void;
}

/**
 * UC-95/UC-99 — fetches the scoped, paged account list from the API whenever `params`
 * changes. Stale responses are ignored (last request wins) so fast typing / filter
 * changes can't render out-of-order results.
 *
 * Pass a memoized `params` object (e.g. via useMemo) so the effect only re-runs when a
 * value actually changes. Set `enabled=false` to skip fetching (e.g. on an inactive tab).
 */
export function useAccountList(
  params: AccountListQueryParams,
  enabled = true,
): UseAccountListResult {
  const [data, setData] = useState<PaginatedResult<AccountListItem> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const requestIdRef = useRef(0);

  const refetch = useCallback(async () => {
    if (!enabled) return;
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);

    try {
      const result = await accountManagementApi.getAccounts(params);
      if (requestId === requestIdRef.current) setData(result);
    } catch (err) {
      if (requestId === requestIdRef.current) {
        setError(getAccountErrorMessage(err));
        setData(null);
      }
    } finally {
      if (requestId === requestIdRef.current) setLoading(false);
    }
  }, [params, enabled]);

  useEffect(() => {
    void refetch();
  }, [refetch]);

  return {
    data,
    accounts: data?.items ?? [],
    loading,
    error,
    refetch,
  };
}

export default useAccountList;
