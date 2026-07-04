import { useCallback, useEffect, useState } from 'react';
import { AxiosError } from 'axios';
import { reportsApi } from '../api/reportsApi';
import type {
  HoExportFormat,
  HoReportFilters,
  HoReportOverview,
  HoReportSection,
} from '../types/reports.types';

export const DEFAULT_HO_FILTERS: HoReportFilters = {
  preset: 'THIS_YEAR',
  fromDate: undefined,
  toDate: undefined,
  campusId: undefined,
  visitScope: 'ALL',
  requestStatus: 'ALL',
  campusInstanceStatus: 'ALL',
  visitType: 'ALL',
};

export type HoReportErrorKind = 'FORBIDDEN' | 'ERROR';

function classifyError(err: unknown): HoReportErrorKind {
  const status = (err as AxiosError)?.response?.status;
  return status === 403 ? 'FORBIDDEN' : 'ERROR';
}

/**
 * State cho trang HO Report: draft filters (đang chỉnh) vs appliedFilters (đã bấm Áp dụng —
 * chỉ appliedFilters mới trigger fetch), data/loading/error và export.
 */
export function useHoReport() {
  const [filters, setFilters] = useState<HoReportFilters>(DEFAULT_HO_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<HoReportFilters>(DEFAULT_HO_FILTERS);
  const [data, setData] = useState<HoReportOverview | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<HoReportErrorKind | null>(null);
  const [exportLoading, setExportLoading] = useState(false);
  const [exportError, setExportError] = useState<HoReportErrorKind | null>(null);

  const fetchOverview = useCallback(async (applied: HoReportFilters) => {
    setLoading(true);
    setError(null);
    try {
      const overview = await reportsApi.getHoReportOverview(applied);
      setData(overview);
    } catch (err) {
      setError(classifyError(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchOverview(appliedFilters);
  }, [appliedFilters, fetchOverview]);

  /** Áp bộ lọc đang chỉnh (hoặc bộ lọc truyền vào — dùng cho click lifecycle step). */
  const applyFilters = useCallback((override?: HoReportFilters) => {
    const next = { ...(override ?? filters) };
    if (override) setFilters(next);
    setAppliedFilters((prev) => {
      // Nếu không đổi gì thì vẫn refetch để người dùng thấy dữ liệu mới nhất.
      if (JSON.stringify(prev) === JSON.stringify(next)) {
        fetchOverview(next);
        return prev;
      }
      return next;
    });
  }, [filters, fetchOverview]);

  const resetFilters = useCallback(() => {
    setFilters(DEFAULT_HO_FILTERS);
    setAppliedFilters(DEFAULT_HO_FILTERS);
  }, []);

  const refetch = useCallback(() => {
    fetchOverview(appliedFilters);
  }, [appliedFilters, fetchOverview]);

  /** Export theo appliedFilters (đúng dữ liệu đang hiển thị), tải file về máy. */
  const exportReport = useCallback(async (format: HoExportFormat, sections: HoReportSection[]) => {
    if (exportLoading) return; // chặn double click
    setExportLoading(true);
    setExportError(null);
    try {
      const { blob, fileName } = await reportsApi.exportHoReport({
        ...appliedFilters,
        exportFormat: format,
        reportSections: sections,
      });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (err) {
      setExportError(classifyError(err));
      throw err;
    } finally {
      setExportLoading(false);
    }
  }, [appliedFilters, exportLoading]);

  return {
    filters,
    setFilters,
    appliedFilters,
    data,
    loading,
    error,
    refetch,
    applyFilters,
    resetFilters,
    exportReport,
    exportLoading,
    exportError,
  };
}

/** Giữ export cũ để không phá import hiện có (module trước đây là scaffold rỗng). */
export const useReports = useHoReport;
