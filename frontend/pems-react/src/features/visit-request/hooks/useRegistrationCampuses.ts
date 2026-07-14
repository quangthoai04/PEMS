import { useEffect, useState } from 'react';
import { visitRequestApi, type RegistrationCampusOption } from '../api/visitRequestApi';

/**
 * Loads the campus options for the visit registration form once (UC-86 §10). The backend
 * returns ONLY campuses available for registration — the UI renders the list as-is and never
 * infers Staff Leader / IC department state itself.
 */
export function useRegistrationCampuses() {
  const [campuses, setCampuses] = useState<RegistrationCampusOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const data = await visitRequestApi.getRegistrationCampuses();
        if (!cancelled) setCampuses(data);
      } catch {
        // Non-fatal for the page shell: the select shows an error placeholder and the
        // backend would reject any submit anyway (BR-86-06).
        if (!cancelled) setError(true);
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  return { campuses, loading, error };
}
