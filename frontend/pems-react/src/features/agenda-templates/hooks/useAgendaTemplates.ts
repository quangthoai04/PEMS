import { useCallback, useEffect, useRef, useState } from 'react';
import agendaTemplatesApi, { ListTemplatesParams } from '../api/agendaTemplatesApi';
import type { AgendaTemplateSummary } from '../types/agendaTemplates.types';

function errMessage(e: unknown, fallback: string): string {
  const anyErr = e as { response?: { data?: { message?: string } } };
  return anyErr?.response?.data?.message ?? fallback;
}

/** Loads the agenda-template list for the management screen and exposes a reload(). */
export const useAgendaTemplates = (params: ListTemplatesParams = {}) => {
  const [templates, setTemplates] = useState<AgendaTemplateSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const paramsRef = useRef(params);
  paramsRef.current = params;

  const reload = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await agendaTemplatesApi.list(paramsRef.current);
      setTemplates(res.templates);
    } catch (e) {
      setError(errMessage(e, 'Không tải được danh sách mẫu agenda.'));
    } finally {
      setLoading(false);
    }
  }, []);

  const key = JSON.stringify(params);
  useEffect(() => {
    void reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  return { templates, loading, error, reload };
};

export default useAgendaTemplates;
