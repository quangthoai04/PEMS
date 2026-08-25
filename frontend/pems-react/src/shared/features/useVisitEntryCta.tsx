import { useCallback, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import i18n from '../i18n/config';
import { usePerCampusV2Capability } from './perCampusV2Capability';
import { useAuth } from '../hooks/useAuth';
import { visitDraftNamespace } from '../../features/visit-request/utils/visitRequestV2DraftStorage';
import { canCreateVisitRequestV2 } from '../auth/visitRequestV2Access';

// ──────────────────────────────────────────────────────────────────────────────
// The single "Đăng ký tham quan" / "Tạo đơn" click behaviour, shared by every entry
// point (homepage hero + final CTA, FAQ, Partners, dashboard visit management).
//
// Centralising it fixes a real defect: FAQ and Partners used to open the v1 popup
// unconditionally, so users landed on the OLD form even when per-campus v2 was ON.
//
// The decision has FOUR outcomes, never conflated:
//   • ready + enabled  → open the v2 form IN A MODAL over the current page;
//   • ready + disabled → open the v1 popup (a real backend "v2 is OFF");
//   • error            → surface an error with a Retry — NEVER a silent v1 fallback,
//                        so a CORS/timeout/network blip cannot quietly downgrade users;
//   • loading          → ask the user to wait while the capability resolves.
//
// v2 opens as a modal rather than a navigation so the CTA behaves like v1 did: the user keeps
// their place on the page. The v2 ROUTES still exist for deep links and refresh, and render the
// same shared form component — the modal is a shell, not a second implementation.
// ──────────────────────────────────────────────────────────────────────────────

const ERROR_TOAST_ID = 'v2-capability-error';
const LOADING_TOAST_ID = 'v2-capability-loading';

import type { CapabilityStatus } from './perCampusV2Capability';

/** The four mutually-exclusive outcomes of a visit-registration CTA click. */
export type VisitEntryOutcome = 'v2-modal' | 'error' | 'loading' | 'disabled';

/**
 * Pure decision shared by every entry point. `error` and `loading` are deliberately NOT collapsed
 * into `v1-popup`: a fetch failure or an in-flight check must never silently downgrade users to the
 * legacy v1 form. Only a real backend "v2 is OFF" (ready + not enabled) opens v1.
 */
export function resolveVisitEntryOutcome(status: CapabilityStatus, enabled: boolean): VisitEntryOutcome {
  if (status === 'error') return 'error';
  if (status === 'loading') return 'loading';
  return enabled ? 'v2-modal' : 'disabled';
}

/** Shows the capability-error toast with a Retry that re-fetches. Reused by all entry points. */
export function notifyCapabilityError(retry: () => void): void {
  toast.error(
    (tt) => (
      <span className="flex items-center gap-3">
        <span>{i18n.t('common:visitEntryCta.capabilityErrorMessage')}</span>
        <button
          type="button"
          className="rounded-md bg-white/20 px-2 py-1 text-xs font-bold hover:bg-white/30"
          onClick={() => { toast.dismiss(tt.id); retry(); }}
        >
          {i18n.t('common:visitEntryCta.retry')}
        </button>
      </span>
    ),
    { id: ERROR_TOAST_ID, duration: 8000 },
  );
}

export function notifyCapabilityDisabled(): void {
  toast.error(
    i18n.t('common:visitEntryCta.capabilityDisabled'),
    { id: ERROR_TOAST_ID, duration: 8000 },
  );
}

export function notifyCapabilityLoading(): void {
  toast.loading(i18n.t('common:visitEntryCta.capabilityLoading'), { id: LOADING_TOAST_ID, duration: 2500 });
}

export function dismissCapabilityToasts(): void {
  toast.dismiss(ERROR_TOAST_ID);
  toast.dismiss(LOADING_TOAST_ID);
}

export interface VisitEntryCta {
  /** Wire this to the button's onClick. */
  trigger: () => void;
  /** v2 modal visibility — the CTA outcome when the capability is ON. */
  v2ModalOpen: boolean;
  closeV2Modal: () => void;
  /** Which shell the v2 modal should render (public OTP flow vs authenticated direct create) —
   * resolved from the SIGNED-IN STATE, never from a value the call site chooses. */
  v2Mode: 'public' | 'authenticated';
  /** Per-account draft namespace when signed in; `undefined` for public (and while auth is still
   * resolving, since the account is not confirmed yet). Same helper the dashboard modal and the
   * standalone `/visit/create-v2` route key their draft with, so all three are the same draft. */
  draftNamespace: string | undefined;
  /** Exposed for callers that want to disable the button while the capability OR auth resolves. */
  isResolving: boolean;
}

/**
 * The single source of truth for "which shell does a visit-registration CTA open" — resolved from
 * whether the current visitor is signed in, NEVER from which page the button sits on. Every entry
 * point (homepage hero + final CTA, FAQ, Partners) calls this with no argument, so a click on the
 * homepage and a click on the dashboard by the same signed-in user open the exact same
 * authenticated, self-registration form; a page can no longer hard-code the wrong mode.
 *
 * Auth bootstrap must have settled before a mode decision means anything — deciding while it is
 * still in flight risks opening the public OTP shell for someone who turns out to be signed in.
 * `trigger()` waits for it (surfacing the same "please wait" toast the capability check uses) and
 * `isResolving` covers both waits so callers can disable the button for either.
 */
export function useVisitEntryCta(): VisitEntryCta {
  const { isAuthenticated, isReady: authReady, user, effectiveRole } = useAuth();
  const { status, enabled, retry } = usePerCampusV2Capability();
  const [v2ModalOpen, setV2ModalOpen] = useState(false);
  const navigate = useNavigate();

  const v2Mode: 'public' | 'authenticated' = isAuthenticated ? 'authenticated' : 'public';

  const trigger = useCallback(() => {
    if (!authReady) { notifyCapabilityLoading(); return; }

    // Role decides BEFORE capability, and wins outright: a signed-in account whose role the
    // backend would reject (CreateVisitRequestV2CommandHandler: Visitor/Staff/Staff Leader only)
    // must never be offered a "wait for it" or "try later" outcome that implies a form is coming,
    // and must never be quietly handed the anonymous public form as a fallback — that would let a
    // denied identity bypass its own role by pretending to be nobody. It gets routed to the exact
    // same /403 (or /invalid-account) every other guarded area of the app already uses.
    if (isAuthenticated) {
      if (!effectiveRole) { navigate('/invalid-account'); return; }
      if (!canCreateVisitRequestV2(effectiveRole)) { navigate('/403'); return; }
    }

    const outcome = resolveVisitEntryOutcome(status, enabled);
    if (outcome === 'error') { notifyCapabilityError(retry); return; }
    if (outcome === 'loading') { notifyCapabilityLoading(); return; }
    dismissCapabilityToasts();
    if (outcome === 'v2-modal') {
      // Open over the current page — no navigation, matching the v1 CTA experience.
      setV2ModalOpen(true);
    } else {
      notifyCapabilityDisabled();
    }
  }, [authReady, isAuthenticated, effectiveRole, navigate, status, enabled, retry]);

  return {
    trigger,
    v2ModalOpen,
    closeV2Modal: () => setV2ModalOpen(false),
    v2Mode,
    draftNamespace: isAuthenticated ? visitDraftNamespace(user?.userId) : undefined,
    isResolving: !authReady || status === 'loading',
  };
}
