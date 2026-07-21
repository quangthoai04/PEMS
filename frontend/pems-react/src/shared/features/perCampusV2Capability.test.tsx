import { describe, expect, it, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';

vi.mock('../../features/visit-request/api/featureApi', () => ({
  getPerCampusFormV2Capability: vi.fn(),
}));

import { getPerCampusFormV2Capability } from '../../features/visit-request/api/featureApi';
import {
  PerCampusV2CapabilityProvider,
  usePerCampusV2Capability,
  __resetPerCampusV2CapabilityCache,
} from './perCampusV2Capability';

const mockGet = vi.mocked(getPerCampusFormV2Capability);

const wrapper = ({ children }: { children: ReactNode }) => (
  <PerCampusV2CapabilityProvider>{children}</PerCampusV2CapabilityProvider>
);

describe('PerCampusV2CapabilityProvider', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    __resetPerCampusV2CapabilityCache();
  });

  it('reports enabled when the backend enables both flags', async () => {
    mockGet.mockResolvedValue({ readEnabled: true, writeEnabled: true, enabled: true });

    const { result } = renderHook(() => usePerCampusV2Capability(), { wrapper });

    expect(result.current.status).toBe('loading');
    expect(result.current.enabled).toBe(false); // fail-safe while loading

    await waitFor(() => expect(result.current.status).toBe('ready'));
    expect(result.current.enabled).toBe(true);
    expect(result.current.readEnabled).toBe(true);
    expect(result.current.writeEnabled).toBe(true);
  });

  it('reports NOT enabled when the backend derives enabled=false (e.g. write off)', async () => {
    mockGet.mockResolvedValue({ readEnabled: true, writeEnabled: false, enabled: false });

    const { result } = renderHook(() => usePerCampusV2Capability(), { wrapper });

    await waitFor(() => expect(result.current.status).toBe('ready'));
    expect(result.current.enabled).toBe(false);
  });

  it('fails SAFE to v1 (enabled=false) when the capability request errors', async () => {
    mockGet.mockRejectedValue(new Error('network down'));

    const { result } = renderHook(() => usePerCampusV2Capability(), { wrapper });

    await waitFor(() => expect(result.current.status).toBe('error'));
    expect(result.current.enabled).toBe(false);
  });

  it('fetches the capability only once across multiple consumers (session cache)', async () => {
    mockGet.mockResolvedValue({ readEnabled: true, writeEnabled: true, enabled: true });

    const first = renderHook(() => usePerCampusV2Capability(), { wrapper });
    await waitFor(() => expect(first.result.current.status).toBe('ready'));

    renderHook(() => usePerCampusV2Capability(), { wrapper });
    renderHook(() => usePerCampusV2Capability(), { wrapper });

    expect(mockGet).toHaveBeenCalledTimes(1);
  });

  it('returns the fail-safe state when used outside the provider', () => {
    const { result } = renderHook(() => usePerCampusV2Capability());
    expect(result.current.enabled).toBe(false);
  });

  it('retry() re-fetches after a transient failure and recovers to enabled', async () => {
    // First fetch fails (CORS/timeout), then the retry succeeds — the entry points rely on this to
    // surface an error + Retry instead of silently downgrading to v1.
    mockGet
      .mockRejectedValueOnce(new Error('network down'))
      .mockResolvedValueOnce({ readEnabled: true, writeEnabled: true, enabled: true });

    const { result } = renderHook(() => usePerCampusV2Capability(), { wrapper });

    await waitFor(() => expect(result.current.status).toBe('error'));
    expect(result.current.enabled).toBe(false);

    result.current.retry();

    await waitFor(() => expect(result.current.status).toBe('ready'));
    expect(result.current.enabled).toBe(true);
    expect(mockGet).toHaveBeenCalledTimes(2);
  });
});
