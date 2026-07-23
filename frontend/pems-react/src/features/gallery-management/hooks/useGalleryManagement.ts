import { useCallback, useEffect, useRef, useState } from 'react';
import { galleryManagementApi } from '../api/galleryManagementApi';
import { getGalleryErrorMessage } from '../api/galleryError';
import type {
  GalleryAreaOption,
  GalleryFilterArea,
  GalleryFilterOptions,
  GalleryListItem,
  GalleryListQueryParams,
  GalleryLocationListItem,
  GalleryLocationListQueryParams,
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

export interface UseGalleryFilterOptionsResult {
  options: GalleryFilterOptions | null;
  areas: GalleryAreaOption[];
  loading: boolean;
  error: string | null;
  refetch: () => Promise<void>;
  upsertArea: (area: GalleryFilterArea) => void;
}

/**
 * Loads the area + location reference data for the caller's campus (filters + upload picker).
 * Exposes `refetch` so a freshly created/renamed area shows up in the dropdowns WITHOUT an F5, and
 * `upsertArea` for an optimistic insert/update straight from a create/update response.
 * Stale responses are dropped (last request wins); a failed refresh keeps the current data.
 */
export function useGalleryFilterOptions(enabled = true): UseGalleryFilterOptionsResult {
  const [options, setOptions] = useState<GalleryFilterOptions | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const requestIdRef = useRef(0);

  const refetch = useCallback(async () => {
    if (!enabled) return;
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);

    try {
      const result = await galleryManagementApi.getFilterOptions();
      if (requestId === requestIdRef.current) setOptions(result);
    } catch (err) {
      // Keep whatever we already have — an options refresh failure must not blank the dropdowns.
      if (requestId === requestIdRef.current) {
        setError(getGalleryErrorMessage(err, 'Không tải được danh sách khu vực. Vui lòng thử lại.'));
      }
    } finally {
      if (requestId === requestIdRef.current) setLoading(false);
    }
  }, [enabled]);

  useEffect(() => {
    void refetch();
  }, [refetch]);

  /** Inserts or updates one area (matched by areaId) without waiting for the next refetch. */
  const upsertArea = useCallback((area: GalleryFilterArea) => {
    setOptions((prev) => {
      const areas: GalleryAreaOption[] = prev?.areas ?? [];
      const exists = areas.some((a) => a.areaId === area.areaId);
      const next = exists
        // Merge so fields the summary payload doesn't carry (cover, locations) are preserved.
        ? areas.map((a) => (a.areaId === area.areaId ? { ...a, ...area } : a))
        : [...areas, { ...area, locations: [] }];
      return { ...(prev ?? {}), areas: next };
    });
  }, []);

  return { options, areas: options?.areas ?? [], loading, error, refetch, upsertArea };
}

interface UseLocationListResult {
  data: PaginatedResult<GalleryLocationListItem> | null;
  items: GalleryLocationListItem[];
  loading: boolean;
  error: string | null;
  refetch: () => void;
}

/**
 * UC-LOC-01/02/03 — fetches the campus-scoped, paged area/location list whenever `params` changes.
 * Stale responses are dropped (last request wins).
 */
export function useGalleryLocationList(
  params: GalleryLocationListQueryParams,
  enabled = true,
): UseLocationListResult {
  const [data, setData] = useState<PaginatedResult<GalleryLocationListItem> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const requestIdRef = useRef(0);

  const refetch = useCallback(async () => {
    if (!enabled) return;
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);

    try {
      const result = await galleryManagementApi.getLocations(params);
      if (requestId === requestIdRef.current) setData(result);
    } catch (err) {
      if (requestId === requestIdRef.current) {
        setError(getGalleryErrorMessage(err, 'Đã có lỗi xảy ra khi tải danh sách khu vực. Vui lòng thử lại.'));
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

export default useGalleryList;
