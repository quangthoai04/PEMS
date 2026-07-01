import { useState, useCallback, useEffect } from 'react';
import { minutesApi } from './minutesApi';
import { MinutesFilterParams, MinutesListResponse, MinutesDetail } from './types';

export const useMinutes = () => {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [listData, setListData] = useState<MinutesListResponse | null>(null);
  const [detailData, setDetailData] = useState<MinutesDetail | null>(null);

  const fetchList = useCallback(async (params: MinutesFilterParams) => {
    setLoading(true);
    setError(null);
    try {
      const data = await minutesApi.list(params);
      setListData(data);
    } catch (err: any) {
      setError(err?.response?.data?.message || 'Failed to fetch minutes list');
      setListData(null);
    } finally {
      setLoading(false);
    }
  }, []);

  const fetchDetail = useCallback(async (minutesId: number) => {
    setLoading(true);
    setError(null);
    try {
      const data = await minutesApi.detail(minutesId);
      setDetailData(data);
    } catch (err: any) {
      setError(err?.response?.data?.message || 'Failed to fetch minutes detail');
      setDetailData(null);
    } finally {
      setLoading(false);
    }
  }, []);

  const clearDetail = useCallback(() => {
    setDetailData(null);
  }, []);

  const exportPdf = useCallback(async (minutesId: number, filename?: string) => {
    try {
      await minutesApi.exportPdf(minutesId, filename);
    } catch (err: any) {
      console.error('Export PDF failed:', err);
      // Could show toast here if toast is available
    }
  }, []);

  const exportExcel = useCallback(async (minutesId: number, filename?: string) => {
    try {
      await minutesApi.exportExcel(minutesId, filename);
    } catch (err: any) {
      console.error('Export Excel failed:', err);
    }
  }, []);

  return {
    loading,
    error,
    listData,
    detailData,
    fetchList,
    fetchDetail,
    clearDetail,
    exportPdf,
    exportExcel,
  };
};
