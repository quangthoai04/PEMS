import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

const navigateMock = vi.fn();
vi.mock('react-router-dom', () => ({ useNavigate: () => navigateMock }));

/**
 * Entry-mode resolution (CanhIter3FixBug §2-§8) — the CTA used to be handed a hard-coded
 * 'public' | 'authenticated' string by whichever page rendered it, so a signed-in visitor on the
 * homepage still got the anonymous OTP form while the exact same account on the dashboard got the
 * authenticated one. `useVisitEntryCta` now takes no argument and resolves the mode itself from the
 * signed-in state, so every call site (homepage hero, final CTA, FAQ, Partners) is structurally
 * unable to disagree with the dashboard about which shell a given user should see.
 */

const toastError = vi.fn();
const toastLoading = vi.fn();
const toastDismiss = vi.fn();
vi.mock('react-hot-toast', () => ({
  default: Object.assign(vi.fn(), {
    error: (...a: unknown[]) => toastError(...a),
    loading: (...a: unknown[]) => toastLoading(...a),
    dismiss: (...a: unknown[]) => toastDismiss(...a),
  }),
}));

const retryMock = vi.fn();
const capabilityMock = vi.fn();
vi.mock('./perCampusV2Capability', () => ({
  usePerCampusV2Capability: () => capabilityMock(),
}));

type AuthState = {
  user: { userId: string } | null;
  isAuthenticated: boolean;
  isReady: boolean;
  effectiveRole: string | null;
};
let authState: AuthState = {
  user: null, isAuthenticated: false, isReady: true, effectiveRole: null,
};
vi.mock('../auth/AuthContext', () => ({
  useAuthContext: () => authState,
}));

import { useVisitEntryCta } from './useVisitEntryCta';

const READY_ENABLED = { status: 'ready' as const, enabled: true, readEnabled: true, writeEnabled: true, retry: retryMock };

describe('useVisitEntryCta — entry mode resolved from sign-in state', () => {
  beforeEach(() => {
    capabilityMock.mockReset();
    toastError.mockClear();
    toastLoading.mockClear();
    toastDismiss.mockClear();
    retryMock.mockClear();
    navigateMock.mockClear();
    authState = { user: null, isAuthenticated: false, isReady: true, effectiveRole: null };
  });

  it('signed-out visitor → public mode, no draft namespace', () => {
    authState = { user: null, isAuthenticated: false, isReady: true, effectiveRole: null };
    capabilityMock.mockReturnValue(READY_ENABLED);

    const { result } = renderHook(() => useVisitEntryCta());

    expect(result.current.v2Mode).toBe('public');
    expect(result.current.draftNamespace).toBeUndefined();
  });

  it('signed-in allowed role (Visitor) → authenticated mode, keyed by account', () => {
    authState = { user: { userId: '42' }, isAuthenticated: true, isReady: true, effectiveRole: 'VISITOR' };
    capabilityMock.mockReturnValue(READY_ENABLED);

    const { result } = renderHook(() => useVisitEntryCta());

    expect(result.current.v2Mode).toBe('authenticated');
    // Same helper the dashboard modal and the standalone /visit/create-v2 route key their draft
    // with — a homepage CTA opened by the same account must land on the same draft, not a fork.
    expect(result.current.draftNamespace).toBe('u42');
  });

  it('trigger() opens the v2 modal for an allowed signed-in role (Visitor)', () => {
    authState = { user: { userId: '7' }, isAuthenticated: true, isReady: true, effectiveRole: 'VISITOR' };
    capabilityMock.mockReturnValue(READY_ENABLED);

    const { result } = renderHook(() => useVisitEntryCta());
    act(() => { result.current.trigger(); });

    expect(result.current.v2ModalOpen).toBe(true);
    expect(result.current.v2Mode).toBe('authenticated');
    expect(navigateMock).not.toHaveBeenCalled();
  });

  it.each(['STAFF', 'STAFF_LEADER'])('trigger() opens the v2 modal for allowed role %s', (role) => {
    authState = { user: { userId: '9' }, isAuthenticated: true, isReady: true, effectiveRole: role };
    capabilityMock.mockReturnValue(READY_ENABLED);

    const { result } = renderHook(() => useVisitEntryCta());
    act(() => { result.current.trigger(); });

    expect(result.current.v2ModalOpen).toBe(true);
    expect(navigateMock).not.toHaveBeenCalled();
  });

  it.each(['ADMIN', 'HO', 'DEPARTMENT_LEAD', 'DEPARTMENT', 'STUDENT'])(
    'signed-in FORBIDDEN role %s → routed to /403, never the form, never a public fallback',
    (role) => {
      authState = { user: { userId: '5' }, isAuthenticated: true, isReady: true, effectiveRole: role };
      capabilityMock.mockReturnValue(READY_ENABLED);

      const { result } = renderHook(() => useVisitEntryCta());
      act(() => { result.current.trigger(); });

      expect(result.current.v2ModalOpen).toBe(false);
      expect(navigateMock).toHaveBeenCalledWith('/403');
      // Never silently downgraded to the anonymous public shell — a denied role must not be able
      // to bypass its own denial by pretending to be nobody.
      expect(result.current.v2Mode).not.toBe('public');
    },
  );

  it('signed-in but unmappable account (effectiveRole null) → /invalid-account, not the form', () => {
    authState = { user: { userId: '11' }, isAuthenticated: true, isReady: true, effectiveRole: null };
    capabilityMock.mockReturnValue(READY_ENABLED);

    const { result } = renderHook(() => useVisitEntryCta());
    act(() => { result.current.trigger(); });

    expect(result.current.v2ModalOpen).toBe(false);
    expect(navigateMock).toHaveBeenCalledWith('/invalid-account');
  });

  it('auth still bootstrapping → isResolving true, trigger() does NOT open the modal', () => {
    authState = { user: null, isAuthenticated: false, isReady: false, effectiveRole: null };
    capabilityMock.mockReturnValue(READY_ENABLED);

    const { result } = renderHook(() => useVisitEntryCta());
    expect(result.current.isResolving).toBe(true);

    act(() => { result.current.trigger(); });

    // Must not guess public for someone who may turn out to be signed in.
    expect(result.current.v2ModalOpen).toBe(false);
    expect(toastLoading).toHaveBeenCalledTimes(1);
    expect(navigateMock).not.toHaveBeenCalled();
  });

  it('capability still loading (auth already ready) → isResolving true, trigger() waits', () => {
    authState = { user: null, isAuthenticated: false, isReady: true, effectiveRole: null };
    capabilityMock.mockReturnValue({ status: 'loading', enabled: false, readEnabled: false, writeEnabled: false, retry: retryMock });

    const { result } = renderHook(() => useVisitEntryCta());
    expect(result.current.isResolving).toBe(true);

    act(() => { result.current.trigger(); });

    expect(result.current.v2ModalOpen).toBe(false);
    expect(toastLoading).toHaveBeenCalledTimes(1);
  });

  it('capability errored → error + retry, never a silent v1/public fallback', () => {
    authState = { user: null, isAuthenticated: false, isReady: true, effectiveRole: null };
    capabilityMock.mockReturnValue({ status: 'error', enabled: false, readEnabled: false, writeEnabled: false, retry: retryMock });

    const { result } = renderHook(() => useVisitEntryCta());
    act(() => { result.current.trigger(); });

    expect(result.current.v2ModalOpen).toBe(false);
    expect(toastError).toHaveBeenCalledTimes(1);
  });

  it('capability ready but disabled (real backend OFF) → disabled toast, no modal', () => {
    authState = { user: null, isAuthenticated: false, isReady: true, effectiveRole: null };
    capabilityMock.mockReturnValue({ status: 'ready', enabled: false, readEnabled: false, writeEnabled: false, retry: retryMock });

    const { result } = renderHook(() => useVisitEntryCta());
    act(() => { result.current.trigger(); });

    expect(result.current.v2ModalOpen).toBe(false);
    expect(toastError).toHaveBeenCalledTimes(1);
  });
});
