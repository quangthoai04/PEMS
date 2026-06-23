import { useState, useEffect } from 'react';
import { departmentLeaderDashboardApi, DepartmentLeaderDashboardSummary } from '../api/departmentLeaderDashboardApi';

export function useDepartmentLeaderDashboard() {
  const [data, setData] = useState<DepartmentLeaderDashboardSummary | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const fetchSummary = async () => {
    try {
      setLoading(true);
      setError(null);
      const summary = await departmentLeaderDashboardApi.getSummary();
      setData(summary);
    } catch (err: any) {
      setError(err?.response?.data?.message || err.message || 'Lỗi khi tải dữ liệu tổng quan');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchSummary();
  }, []);

  return { data, loading, error, refetch: fetchSummary };
}
