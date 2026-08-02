import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import type { AxiosError } from 'axios';

vi.mock('../../../shared/api/httpClient', () => ({
  default: { get: vi.fn(), post: vi.fn() },
}));

import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import { accountManagementApi } from '../api/accountManagementApi';
import { useRelatedVisitorNationalities } from '../hooks/useRelatedVisitorNationalities';

const get = httpClient.get as unknown as ReturnType<typeof vi.fn>;

function apiError(status: number, errorCode?: string): AxiosError {
  return {
    isAxiosError: true,
    name: 'AxiosError',
    message: 'Request failed',
    toJSON: () => ({}),
    response: { status, data: { errorCode }, statusText: '', headers: {}, config: {} as never },
  } as AxiosError;
}

describe('accountManagementApi.getRelatedVisitorNationalities', () => {
  beforeEach(() => get.mockReset());

  it('uses a declared endpoint of its own — the options are not scraped off the list route', () => {
    expect(API_ENDPOINTS.accounts.relatedVisitorNationalities)
      .toBe('/accounts/staff-leader/related-visitors/nationalities');
    expect(API_ENDPOINTS.accounts.relatedVisitorNationalities)
      .not.toBe(API_ENDPOINTS.accounts.relatedVisitors);
  });

  // No page/pageSize/campusId: the options must cover every related Visitor, and the campus scope
  // is the server's to decide.
  it('sends no parameters at all', async () => {
    get.mockResolvedValueOnce({ data: { items: ['Nhật Bản'] } });

    const result = await accountManagementApi.getRelatedVisitorNationalities();

    expect(get).toHaveBeenCalledTimes(1);
    expect(get).toHaveBeenCalledWith(API_ENDPOINTS.accounts.relatedVisitorNationalities);
    expect(get.mock.calls[0][1]).toBeUndefined();
    expect(result.items).toEqual(['Nhật Bản']);
  });

  it('surfaces a failure to the caller instead of swallowing it', async () => {
    get.mockRejectedValueOnce(apiError(403, 'RELATED_VISITOR_FORBIDDEN'));

    await expect(accountManagementApi.getRelatedVisitorNationalities()).rejects.toBeDefined();
  });
});

describe('useRelatedVisitorNationalities', () => {
  beforeEach(() => get.mockReset());

  it('loads the real options from the endpoint', async () => {
    get.mockResolvedValueOnce({ data: { items: ['Hàn Quốc', 'Nhật Bản', 'Pháp'] } });

    const { result } = renderHook(() => useRelatedVisitorNationalities());

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.options).toEqual(['Hàn Quốc', 'Nhật Bản', 'Pháp']);
    expect(result.current.error).toBeNull();
  });

  it('never fires while disabled — internal mode must not touch this endpoint', async () => {
    const { result } = renderHook(() => useRelatedVisitorNationalities(false));

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(get).not.toHaveBeenCalled();
    expect(result.current.options).toEqual([]);
  });

  it('reports the failure and offers a retry that succeeds', async () => {
    get.mockRejectedValueOnce(apiError(500));

    const { result } = renderHook(() => useRelatedVisitorNationalities());

    await waitFor(() => expect(result.current.error).not.toBeNull());
    expect(result.current.options).toEqual([]);

    get.mockResolvedValueOnce({ data: { items: ['Singapore'] } });
    await act(async () => { result.current.retry(); });

    await waitFor(() => expect(result.current.options).toEqual(['Singapore']));
    expect(result.current.error).toBeNull();
  });

  // Switching mode mid-flight must not let the late response repopulate a dropdown that is gone.
  it('drops a response that lands after the hook was disabled', async () => {
    let resolveLate: ((value: unknown) => void) | undefined;
    get.mockReturnValueOnce(new Promise((resolve) => { resolveLate = resolve; }));

    const { result, rerender } = renderHook(
      ({ enabled }) => useRelatedVisitorNationalities(enabled),
      { initialProps: { enabled: true } },
    );

    rerender({ enabled: false });
    await act(async () => { resolveLate?.({ data: { items: ['Bồ Đào Nha'] } }); });

    expect(result.current.options).toEqual([]);
  });
});
