import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import {
  getPerCampusFormV2Capability,
  type PerCampusFormV2Capability,
} from '../../features/visit-request/api/featureApi';

// ──────────────────────────────────────────────────────────────────────────────
// Single shared source of the per-campus form v2 capability. Fetched ONCE per page
// load (module-level promise cache) and exposed through a context so every entry
// point (homepage CTA, dashboard create) reads the same authoritative state.
//
// Fail-safe contract: while loading, on error, or outside the provider, `enabled`
// is false so the browser keeps using v1. It is NEVER guessed on the client.
// ──────────────────────────────────────────────────────────────────────────────

export type CapabilityStatus = 'loading' | 'ready' | 'error';

export interface PerCampusV2CapabilityState {
  status: CapabilityStatus;
  /** true ONLY when the backend reports both read+write enabled. Fail-safe false otherwise. */
  enabled: boolean;
  readEnabled: boolean;
  writeEnabled: boolean;
}

const LOADING_STATE: PerCampusV2CapabilityState = {
  status: 'loading', enabled: false, readEnabled: false, writeEnabled: false,
};

const FAILSAFE_STATE: PerCampusV2CapabilityState = {
  status: 'error', enabled: false, readEnabled: false, writeEnabled: false,
};

// Outside the provider we return the fail-safe (v1) state so a missing provider can
// never accidentally route users into v2.
const PerCampusV2CapabilityContext = createContext<PerCampusV2CapabilityState>(FAILSAFE_STATE);

// Session cache: one in-flight/resolved promise shared by every provider mount, so the
// capability is fetched at most once per page load.
let cachedCapability: Promise<PerCampusFormV2Capability> | null = null;

function loadCapability(): Promise<PerCampusFormV2Capability> {
  if (!cachedCapability) {
    cachedCapability = getPerCampusFormV2Capability();
  }
  return cachedCapability;
}

/** Test-only: clears the module-level capability cache so each test starts clean. */
export function __resetPerCampusV2CapabilityCache(): void {
  cachedCapability = null;
}

export function PerCampusV2CapabilityProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<PerCampusV2CapabilityState>(LOADING_STATE);

  useEffect(() => {
    let active = true;
    loadCapability()
      .then((cap) => {
        if (!active) return;
        setState({
          status: 'ready',
          enabled: !!cap.enabled,
          readEnabled: !!cap.readEnabled,
          writeEnabled: !!cap.writeEnabled,
        });
      })
      .catch(() => {
        if (!active) return;
        // Drop the cached rejection so a later mount/navigation can retry, and fail SAFE to v1.
        cachedCapability = null;
        setState(FAILSAFE_STATE);
      });
    return () => { active = false; };
  }, []);

  return (
    <PerCampusV2CapabilityContext.Provider value={state}>
      {children}
    </PerCampusV2CapabilityContext.Provider>
  );
}

/**
 * Read the shared per-campus v2 capability. Returns the fail-safe (v1) state outside the
 * provider or before the fetch resolves — callers should treat `enabled=false` as "use v1".
 */
export function usePerCampusV2Capability(): PerCampusV2CapabilityState {
  return useContext(PerCampusV2CapabilityContext);
}
