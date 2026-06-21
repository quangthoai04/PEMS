import { useState, useEffect, useCallback, useRef } from 'react';
import { authenticationApi } from '../api/authenticationApi';
import type { CampusOption } from '../types/authentication.types';

interface UseActiveCampusesResult {
  campuses: CampusOption[];
  loading: boolean;
  error: string;
  retryCount: number;
  reload: () => void;
}

// Global cache to avoid refetching on every mount during the session
let cachedCampuses: CampusOption[] | null = null;

export function useActiveCampuses(portal: 'INTERNAL' | 'VISITOR'): UseActiveCampusesResult {
  const [campuses, setCampuses] = useState<CampusOption[]>(cachedCampuses || []);
  const [loading, setLoading] = useState<boolean>(portal === 'INTERNAL' && !cachedCampuses);
  const [error, setError] = useState<string>('');
  const [retryCount, setRetryCount] = useState<number>(0);
  const loadingRef = useRef(false);

  const loadCampuses = useCallback(async (isRetry = false) => {
    if (portal !== 'INTERNAL') {
      setLoading(false);
      return;
    }
    
    if (cachedCampuses && !isRetry) {
      setCampuses(cachedCampuses);
      setLoading(false);
      setError('');
      return;
    }

    if (loadingRef.current) return;
    loadingRef.current = true;
    setLoading(true);
    setError('');

    try {
      const data = await authenticationApi.getActiveCampuses();
      const validData = Array.isArray(data) ? data : [];
      
      if (validData.length === 0) {
        setError('Không có cơ sở nào đang hoạt động. Vui lòng liên hệ quản trị viên.');
        setLoading(false);
      } else {
        cachedCampuses = validData;
        setCampuses(validData);
        setError('');
        setLoading(false);
      }
    } catch (err) {
      console.error('Failed to load campuses:', err);
      if (retryCount < 3) {
        const delay = Math.pow(2, retryCount) * 1000; // 1s, 2s, 4s
        setTimeout(() => {
          setRetryCount(prev => prev + 1);
        }, delay);
      } else {
        setError('Không thể tải danh sách cơ sở. Vui lòng kiểm tra kết nối hoặc thử lại.');
        setLoading(false);
      }
    } finally {
      loadingRef.current = false;
    }
  }, [portal, retryCount]);

  useEffect(() => {
    if (portal === 'INTERNAL') {
      loadCampuses();
    }
  }, [loadCampuses, portal, retryCount]);

  const reload = useCallback(() => {
    setRetryCount(0);
    setError('');
    loadCampuses(true);
  }, [loadCampuses]);

  return { campuses, loading, error, retryCount, reload };
}
