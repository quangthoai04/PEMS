import { useCallback, useEffect, useRef, useState } from 'react';
import { galleryManagementApi } from '../api/galleryManagementApi';
import { getGalleryErrorMessage } from '../api/galleryError';
import type {
  GalleryFilterOptions,
  GalleryListItem,
  GalleryListQueryParams,
  PaginatedResult,
} from '../types/galleryManagement.types';

interface UseGalleryListResult {
  data: PaginatedResult<GalleryListItem> | null;
  items: GalleryListItem[];
  loading: boolean;
  error: string | null;
  refetch: () => void;
}

/**
 * UC-GAL-01 / UC-GAL-02 — fetches the campus-scoped, paged gallery list whenever `params` changes.
 * Stale responses are dropped (last request wins). Pass `enabled=false` to skip fetching.
 */
export function useGalleryList(
  params: GalleryListQueryParams,
  enabled = true,
): UseGalleryListResult {
  const [data, setData] = useState<PaginatedResult<GalleryListItem> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const requestIdRef = useRef(0);

  const refetch = useCallback(async () => {
    if (!enabled) return;
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);

    try {
      const result = await galleryManagementApi.getGalleryItems(params);
      if (requestId === requestIdRef.current) setData(result);
    } catch (err) {
      if (requestId === requestIdRef.current) {
        setError(getGalleryErrorMessage(err, 'Đã có lỗi xảy ra khi tải danh sách gallery. Vui lòng thử lại.'));
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
    items: data?.items ?? [],
    loading,
    error,
    refetch,
  };
}

/**
 * Loads the area + location reference data for the caller's campus once (filters + upload picker).
 */
export function useGalleryFilterOptions(enabled = true) {
  const [options, setOptions] = useState<GalleryFilterOptions | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!enabled) return;
    let cancelled = false;
    setLoading(true);
    (async () => {
      try {
        const result = await galleryManagementApi.getFilterOptions();
        if (!cancelled) setOptions(result);
      } catch {
        if (!cancelled) setOptions(null);
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [enabled]);

  return { options, areas: options?.areas ?? [], loading };
}

export default useGalleryList;
