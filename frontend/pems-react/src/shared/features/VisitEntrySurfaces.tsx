import React from 'react';
import { VisitRequestV2Modal } from '../../features/visit-request/components/v2/VisitRequestV2Modal';
import type { VisitEntryCta } from './useVisitEntryCta';

interface Props {
  cta: VisitEntryCta;
  /** Per-user draft namespace (authenticated mode only). */
  draftNamespace?: string;
  /** Called after a successful v2 create — e.g. to refresh a dashboard list. */
  onV2Success?: () => void;
}

/**
 * The two mutually-exclusive surfaces a visit-registration CTA can open, rendered once so no entry
 * point re-implements the wiring: the v2 modal when the capability is ON, the legacy v1 popup only
 * when the backend explicitly reports OFF. Every CTA (home hero, final CTA, FAQ, Partners,
 * dashboard) renders this same pair.
 */
export const VisitEntrySurfaces: React.FC<Props> = ({ cta, draftNamespace, onV2Success }) => (
  <>
    <VisitRequestV2Modal
      isOpen={cta.v2ModalOpen}
      onClose={cta.closeV2Modal}
      mode={cta.v2Mode}
      draftNamespace={draftNamespace}
      // Success does NOT close the modal. It used to call `cta.closeV2Modal()` here, which unmounted
      // the shell in the same tick as it rendered the receipt — so on every public CTA (hero, final
      // CTA, FAQ, Partners) the form simply vanished after the OTP and a four-second toast was the
      // only evidence a request had been created. The user is the one who decides when they are done
      // reading the request code; closing is `onClose`, and nothing else.
      onSuccess={() => { onV2Success?.(); }}
    />
  </>
);
