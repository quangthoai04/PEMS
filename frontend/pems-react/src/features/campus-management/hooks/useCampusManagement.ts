import { useCallback, useEffect, useRef, useState } from 'react';
import { campusManagementApi } from '../api/campusManagementApi';
import { getAuthErrorMessage } from '../../authentication/api/authError';
import type {
  CampusDetail,
  CampusFilterOptions,
  CampusListItem,
  CampusListQueryParams,
  ManageCampusStatusRequest,
  ManageCampusStatusResponse,
  PaginatedResult,
} from '../types/campusManagement.types';

/**
 * Loads the paged campus list (UC-82/UC-83) reactively from the given query params.
 * Re-fetches whenever `params` changes; exposes loading/error/data + a manual refetch.
 */
export function useCampusList(params: CampusListQueryParams) {
  const [data, setData] = useState<PaginatedResult<CampusListItem> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Serialize params so the effect only re-runs on real changes.
  const key = JSON.stringify(params);
  const reqIdRef = useRef(0);

  const fetchList = useCallback(async () => {
    const reqId = ++reqIdRef.current;
    setLoading(true);
    setError(null);
    try {
      const result = await campusManagementApi.getCampuses(params);
      if (reqId === reqIdRef.current) setData(result);
    } catch (err) {
      if (reqId === reqIdRef.current) {
        setError(getAuthErrorMessage(err, 'Không thể tải danh sách campus. Vui lòng thử lại.'));
      }
    } finally {
      if (reqId === reqIdRef.current) setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  useEffect(() => {
    fetchList();
  }, [fetchList]);

  return { data, loading, error, refetch: fetchList };
}

/** UC-84 — loads a single campus' full detail by id, with loading/error + refetch. */
export function useCampusDetail(campusId: string | number | undefined) {
  const [data, setData] = useState<CampusDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notFound, setNotFound] = useState(false);

  const fetchDetail = useCallback(async () => {
    if (campusId === undefined || campusId === '') return;
    setLoading(true);
    setError(null);
    setNotFound(false);
    try {
      const result = await campusManagementApi.getCampusDetails(campusId);
      setData(result);
    } catch (err) {
      const status = (err as { response?: { status?: number } })?.response?.status;
      if (status === 404) setNotFound(true);
      else setError(getAuthErrorMessage(err, 'Không thể tải chi tiết campus. Vui lòng thử lại.'));
    } finally {
      setLoading(false);
    }
  }, [campusId]);

  useEffect(() => {
    fetchDetail();
  }, [fetchDetail]);

  return { data, loading, error, notFound, refetch: fetchDetail };
}

/**
 * Loads UC-83 filter options once (cities/campuses/statuses from the database).
 *
 * `GET /campuses/filter-options` is HO-only server-side (Campus Management, RoleAccessPolicy.
 * CanAccessCampusManagement — ADMIN included). Every caller outside the Campus Management
 * screen itself must pass `enabled: false` for any viewer who isn't HO — Patch 6 hardening,
 * after this hook firing unconditionally on every mount was found sending this request (and
 * getting a 403 the catch below silently swallowed) for every non-HO role on four different
 * management screens. Defaults to `true` so `CampusManagement.tsx` itself — HO-only by route
 * guard, so always a valid caller — needs no change.
 */
export function useCampusFilterOptions(options?: { enabled?: boolean }) {
  const enabled = options?.enabled ?? true;
  const [result, setResult] = useState<CampusFilterOptions | null>(null);

  useEffect(() => {
    if (!enabled) {
      setResult(null);
      return;
    }
    let active = true;
    campusManagementApi
      .getFilterOptions()
      .then((res) => {
        if (active) setResult(res);
      })
      .catch(() => {
        /* filter options are best-effort; the list still works without them */
      });
    return () => {
      active = false;
    };
  }, [enabled]);

  return result;
}

/** UC-86 mutation — enable/disable a campus, with submitting/error state. */
export function useManageCampusStatus() {
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const manageCampusStatus = useCallback(
    async (payload: ManageCampusStatusRequest): Promise<ManageCampusStatusResponse | null> => {
      setSubmitting(true);
      setError(null);
      try {
        return await campusManagementApi.manageCampusStatus(payload);
      } catch (err) {
        setError(getAuthErrorMessage(err, 'Không thể cập nhật trạng thái campus. Vui lòng thử lại.'));
        return null;
      } finally {
        setSubmitting(false);
      }
    },
    [],
  );

  return { submitting, error, setError, manageCampusStatus };
}
