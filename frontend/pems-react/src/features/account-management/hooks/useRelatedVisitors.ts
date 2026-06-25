import { useCallback, useEffect, useRef, useState } from 'react';
import { accountManagementApi } from '../api/accountManagementApi';
import { getAccountErrorMessage } from '../api/accountError';
import type {
  PaginatedResult,
  RelatedVisitorListItem,
  RelatedVisitorQueryParams,
} from '../types/accountManagement.types';

interface UseRelatedVisitorsResult {
  data: PaginatedResult<RelatedVisitorListItem> | null;
  visitors: RelatedVisitorListItem[];
  loading: boolean;
  error: string | null;
  refetch: () => void;
}

/**
 * Staff Leader "Visitor liên quan" tab — fetches the scoped, paged related-visitor list whenever
 * `params` changes. Stale responses are dropped (last request wins) so fast typing / filter
 * changes can't render out of order. Pass `enabled=false` to skip fetching (inactive tab).
 */
export function useRelatedVisitors(
  params: RelatedVisitorQueryParams,
  enabled = true,
): UseRelatedVisitorsResult {
  const [data, setData] = useState<PaginatedResult<RelatedVisitorListItem> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const requestIdRef = useRef(0);

  const refetch = useCallback(async () => {
    if (!enabled) return;
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);

    try {
      const result = await accountManagementApi.getRelatedVisitors(params);
      if (requestId === requestIdRef.current) setData(result);
    } catch (err) {
      if (requestId === requestIdRef.current) {
        setError(getAccountErrorMessage(err, 'Đã có lỗi xảy ra khi tải danh sách Visitor liên quan. Vui lòng thử lại.'));
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
    visitors: data?.items ?? [],
    loading,
    error,
    refetch,
  };
}

export default useRelatedVisitors;
