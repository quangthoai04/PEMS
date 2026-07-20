import { useCallback, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { usePerCampusV2Capability } from './perCampusV2Capability';
import {
  V2_PUBLIC_REGISTRATION_PATH,
  V2_AUTHENTICATED_CREATE_PATH,
} from './perCampusV2Entry';

// ──────────────────────────────────────────────────────────────────────────────
// The single "Đăng ký tham quan" / "Tạo đơn" click behaviour, shared by every entry
// point (homepage hero + final CTA, FAQ, Partners, dashboard visit management).
//
// Centralising it fixes a real defect: FAQ and Partners used to open the v1 popup
// unconditionally, so users landed on the OLD form even when per-campus v2 was ON.
//
// The decision has FOUR outcomes, never conflated:
//   • ready + enabled  → route to the v2 form (public or authenticated);
//   • ready + disabled → open the v1 popup (a real backend "v2 is OFF");
//   • error            → surface an error with a Retry — NEVER a silent v1 fallback,
//                        so a CORS/timeout/network blip cannot quietly downgrade users;
//   • loading          → ask the user to wait while the capability resolves.
// ──────────────────────────────────────────────────────────────────────────────

const ERROR_TOAST_ID = 'v2-capability-error';
const LOADING_TOAST_ID = 'v2-capability-loading';

import type { CapabilityStatus } from './perCampusV2Capability';

/** The four mutually-exclusive outcomes of a visit-registration CTA click. */
export type VisitEntryOutcome = 'v2-route' | 'v1-popup' | 'error' | 'loading';

/**
 * Pure decision shared by every entry point. `error` and `loading` are deliberately NOT collapsed
 * into `v1-popup`: a fetch failure or an in-flight check must never silently downgrade users to the
 * legacy v1 form. Only a real backend "v2 is OFF" (ready + not enabled) opens v1.
 */
export function resolveVisitEntryOutcome(status: CapabilityStatus, enabled: boolean): VisitEntryOutcome {
  if (status === 'error') return 'error';
  if (status === 'loading') return 'loading';
  return enabled ? 'v2-route' : 'v1-popup';
}

/** Shows the capability-error toast with a Retry that re-fetches. Reused by all entry points. */
export function notifyCapabilityError(retry: () => void): void {
  toast.error(
    (tt) => (
      <span className="flex items-center gap-3">
        <span>Không kiểm tra được chế độ đăng ký. Vui lòng thử lại.</span>
        <button
          type="button"
          className="rounded-md bg-white/20 px-2 py-1 text-xs font-bold hover:bg-white/30"
          onClick={() => { toast.dismiss(tt.id); retry(); }}
        >
          Thử lại
        </button>
      </span>
    ),
    { id: ERROR_TOAST_ID, duration: 8000 },
  );
}

export function notifyCapabilityLoading(): void {
  toast.loading('Đang kiểm tra chế độ đăng ký…', { id: LOADING_TOAST_ID, duration: 2500 });
}

export function dismissCapabilityToasts(): void {
  toast.dismiss(ERROR_TOAST_ID);
  toast.dismiss(LOADING_TOAST_ID);
}

export interface VisitEntryCta {
  /** Wire this to the button's onClick. */
  trigger: () => void;
  /** v1 popup visibility (only ever opened for a real backend OFF). */
  popupOpen: boolean;
  closePopup: () => void;
  /** Exposed for callers that want to disable the button while the capability loads. */
  isResolving: boolean;
}

export function useVisitEntryCta(mode: 'public' | 'authenticated'): VisitEntryCta {
  const navigate = useNavigate();
  const { status, enabled, retry } = usePerCampusV2Capability();
  const [popupOpen, setPopupOpen] = useState(false);

  const trigger = useCallback(() => {
    const outcome = resolveVisitEntryOutcome(status, enabled);
    if (outcome === 'error') { notifyCapabilityError(retry); return; }
    if (outcome === 'loading') { notifyCapabilityLoading(); return; }
    dismissCapabilityToasts();
    if (outcome === 'v2-route') {
      navigate(mode === 'public' ? V2_PUBLIC_REGISTRATION_PATH : V2_AUTHENTICATED_CREATE_PATH);
    } else {
      // A real backend "v2 is OFF" — the only path that opens the legacy v1 popup.
      setPopupOpen(true);
    }
  }, [status, enabled, mode, navigate, retry]);

  return {
    trigger,
    popupOpen,
    closePopup: () => setPopupOpen(false),
    isResolving: status === 'loading',
  };
}
