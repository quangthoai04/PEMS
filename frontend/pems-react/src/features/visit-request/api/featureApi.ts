import httpClient from '../../../shared/api/httpClient';

// ──────────────────────────────────────────────────────────────────────────────
// Public, read-only feature-capability API. The browser uses this as the SINGLE
// authority for the per-campus form v2 cutover — it must never guess the flags and
// must fail SAFE to v1 when this call fails (see PerCampusV2CapabilityProvider).
// ──────────────────────────────────────────────────────────────────────────────

export interface PerCampusFormV2Capability {
  readEnabled: boolean;
  writeEnabled: boolean;
  /** true only when BOTH read and write are enabled — the only state safe to submit AND read a v2 request. */
  enabled: boolean;
}

export const getPerCampusFormV2Capability = () =>
  httpClient
    .get<PerCampusFormV2Capability>('/public/features/per-campus-form-v2')
    .then((r) => r.data);
