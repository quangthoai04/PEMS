import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { departmentLeaderPersonnelApi } from '../api/departmentLeaderPersonnelApi';
import { getDepartmentLeaderErrorMessage, isDepartmentLeaderScopeLost } from '../api/departmentLeaderError';
import type {
  MyDepartment,
  PagedPersonnel,
  PersonnelStatusFilter,
} from '../types/departmentLeaderPersonnel.types';

/** How long the search box waits after the last keystroke before hitting the server. */
const SEARCH_DEBOUNCE_MS = 400;

export interface UseMyDepartmentPersonnelResult {
  department: MyDepartment | null;
  isLoadingDepartment: boolean;
  departmentError: string | null;

  page: PagedPersonnel | null;
  isLoadingList: boolean;
  listError: string | null;

  /** What the input shows — updates on every keystroke. */
  keyword: string;
  setKeyword: (value: string) => void;
  /** What was actually sent — lags `keyword` by the debounce interval. */
  appliedKeyword: string;

  status: PersonnelStatusFilter;
  setStatus: (value: PersonnelStatusFilter) => void;

  currentPage: number;
  setCurrentPage: (value: number) => void;
  pageSize: number;
  setPageSize: (value: number) => void;

  /** True when a filter is active and produced no rows — distinct from an empty department. */
  isNoResult: boolean;
  /** True when the department genuinely has no personnel yet. */
  isEmpty: boolean;

  /** True when the session lost Department Leader authority; the page should stop and sign out. */
  scopeLost: boolean;

  refreshAll: () => Promise<void>;
  refreshList: () => Promise<void>;
  refreshDepartment: () => Promise<void>;
}

/**
 * Owns the list state for /dashboard/my-department: debounced search, status filter, paging, and the
 * department header.
 *
 * Two behaviours matter here. Search and filter changes RESET the page to 1 — otherwise a narrower
 * result set leaves the user stranded on a page that no longer exists and the table renders empty
 * for no visible reason. And every fetch is guarded by a request sequence number so a slow earlier
 * response cannot overwrite a newer one (a real hazard with debounced typing).
 */
export function useMyDepartmentPersonnel(): UseMyDepartmentPersonnelResult {
  const [department, setDepartment] = useState<MyDepartment | null>(null);
  const [isLoadingDepartment, setIsLoadingDepartment] = useState(true);
  const [departmentError, setDepartmentError] = useState<string | null>(null);

  const [page, setPage] = useState<PagedPersonnel | null>(null);
  const [isLoadingList, setIsLoadingList] = useState(true);
  const [listError, setListError] = useState<string | null>(null);

  const [keyword, setKeywordState] = useState('');
  const [appliedKeyword, setAppliedKeyword] = useState('');
  const [status, setStatusState] = useState<PersonnelStatusFilter>('ALL');
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSizeState] = useState(10);
  const [scopeLost, setScopeLost] = useState(false);

  // Discards responses from superseded requests — without this, a slow request for "ngu" can land
  // after the fast one for "nguyen" and repopulate the table with stale rows.
  const listRequestId = useRef(0);

  // ── Debounce: only `appliedKeyword` triggers a fetch. ──
  useEffect(() => {
    const timer = setTimeout(() => setAppliedKeyword(keyword.trim()), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [keyword]);

  // Any change to what is being filtered invalidates the current page number.
  useEffect(() => {
    setCurrentPage(1);
  }, [appliedKeyword, status, pageSize]);

  const setKeyword = useCallback((value: string) => setKeywordState(value), []);
  const setStatus = useCallback((value: PersonnelStatusFilter) => setStatusState(value), []);
  const setPageSize = useCallback((value: number) => setPageSizeState(value), []);

  const refreshDepartment = useCallback(async () => {
    setIsLoadingDepartment(true);
    setDepartmentError(null);
    try {
      setDepartment(await departmentLeaderPersonnelApi.getMyDepartment());
    } catch (error) {
      if (isDepartmentLeaderScopeLost(error)) setScopeLost(true);
      setDepartmentError(getDepartmentLeaderErrorMessage(error, 'Không tải được thông tin phòng ban.'));
    } finally {
      setIsLoadingDepartment(false);
    }
  }, []);

  const refreshList = useCallback(async () => {
    const requestId = ++listRequestId.current;
    setIsLoadingList(true);
    setListError(null);
    try {
      const result = await departmentLeaderPersonnelApi.listPersonnel({
        keyword: appliedKeyword || undefined,
        status,
        page: currentPage,
        pageSize,
      });
      if (requestId !== listRequestId.current) return; // superseded
      setPage(result);
    } catch (error) {
      if (requestId !== listRequestId.current) return;
      if (isDepartmentLeaderScopeLost(error)) setScopeLost(true);
      setListError(getDepartmentLeaderErrorMessage(error, 'Không tải được danh sách nhân sự.'));
      setPage(null);
    } finally {
      if (requestId === listRequestId.current) setIsLoadingList(false);
    }
  }, [appliedKeyword, status, currentPage, pageSize]);

  const refreshAll = useCallback(async () => {
    await Promise.all([refreshDepartment(), refreshList()]);
  }, [refreshDepartment, refreshList]);

  useEffect(() => {
    void refreshDepartment();
  }, [refreshDepartment]);

  useEffect(() => {
    void refreshList();
  }, [refreshList]);

  const isFiltering = appliedKeyword.length > 0 || status !== 'ALL';
  const rowCount = page?.items.length ?? 0;

  const isNoResult = useMemo(
    () => !isLoadingList && !listError && rowCount === 0 && isFiltering,
    [isLoadingList, listError, rowCount, isFiltering],
  );

  const isEmpty = useMemo(
    () => !isLoadingList && !listError && rowCount === 0 && !isFiltering,
    [isLoadingList, listError, rowCount, isFiltering],
  );

  return {
    department,
    isLoadingDepartment,
    departmentError,
    page,
    isLoadingList,
    listError,
    keyword,
    setKeyword,
    appliedKeyword,
    status,
    setStatus,
    currentPage,
    setCurrentPage,
    pageSize,
    setPageSize,
    isNoResult,
    isEmpty,
    scopeLost,
    refreshAll,
    refreshList,
    refreshDepartment,
  };
}
