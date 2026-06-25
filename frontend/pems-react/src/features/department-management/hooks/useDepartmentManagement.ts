import { useCallback, useEffect, useRef, useState } from 'react';
import { departmentManagementApi } from '../api/departmentManagementApi';
import { getDepartmentErrorMessage } from '../api/departmentError';
import type {
  DepartmentListItem,
  DepartmentListQueryParams,
  PaginatedResult,
} from '../types/departmentManagement.types';

interface UseDepartmentListResult {
  data: PaginatedResult<DepartmentListItem> | null;
  departments: DepartmentListItem[];
  loading: boolean;
  error: string | null;
  refetch: () => void;
}

/**
 * UC-104/UC-103 — fetches the campus-scoped, paged department list whenever `params` changes.
 * Stale responses are dropped (last request wins). Pass `enabled=false` to skip fetching.
 */
export function useDepartmentList(
  params: DepartmentListQueryParams,
  enabled = true,
): UseDepartmentListResult {
  const [data, setData] = useState<PaginatedResult<DepartmentListItem> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const requestIdRef = useRef(0);

  const refetch = useCallback(async () => {
    if (!enabled) return;
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);

    try {
      const result = await departmentManagementApi.getDepartments(params);
      if (requestId === requestIdRef.current) setData(result);
    } catch (err) {
      if (requestId === requestIdRef.current) {
        setError(getDepartmentErrorMessage(err, 'Đã có lỗi xảy ra khi tải danh sách phòng ban. Vui lòng thử lại.'));
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
    departments: data?.items ?? [],
    loading,
    error,
    refetch,
  };
}

export default useDepartmentList;
