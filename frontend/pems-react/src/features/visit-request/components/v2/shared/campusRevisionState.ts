/**
 * How a campus's content version should be DESCRIBED, given where that campus is in its lifecycle.
 *
 * The card used to print "Nội dung v1 · Phê duyệt v1" unconditionally, which reads as "approved at
 * revision 1" — on a campus that nobody has looked at yet. That is not a formatting nit: the create
 * service writes `ApprovalRevision = 1` on the very first detail row, so the number is 1 from the
 * moment the request is submitted and `approvalRevision > 0` says nothing whatsoever about approval.
 *
 * Approval is therefore derived from the lifecycle facts that actually carry it — the instance status,
 * whether a decision was recorded, and whether a proposal is waiting — and the stored revision numbers
 * are left exactly as they are. Renumbering them in the database to make the UI read better would
 * corrupt the optimistic-concurrency and amendment baselines that depend on them.
 */

export type CampusRevisionTone = 'waiting' | 'active' | 'rejected' | 'cancelled' | 'closed';

export interface CampusRevisionState {
  tone: CampusRevisionTone;
  /** i18n key for the headline ("what content is in force"). */
  headlineKey: string;
  /** i18n key for the qualifier ("and where does approval stand"), or null when there is nothing to add. */
  noteKey: string | null;
  /** Interpolation values for both keys. */
  values: { form: number; approval: number; amendmentNo?: number };
}

interface Input {
  instanceStatus: string;
  formRevision: number;
  approvalRevision: number;
  decidedAt?: string | null;
  activeAmendmentNo?: number | null;
}

export const resolveCampusRevisionState = ({
  instanceStatus, formRevision, approvalRevision, decidedAt, activeAmendmentNo,
}: Input): CampusRevisionState => {
  const status = (instanceStatus ?? '').trim().toUpperCase();
  const values = { form: formRevision, approval: approvalRevision };

  if (status === 'REJECTED') {
    return { tone: 'rejected', headlineKey: 'visitRequestV2:revision.rejected', noteKey: null, values };
  }
  if (status === 'CANCELLED') {
    return {
      tone: 'cancelled',
      headlineKey: 'visitRequestV2:revision.current',
      noteKey: 'visitRequestV2:revision.cancelledNote',
      values,
    };
  }
  if (status === 'CLOSED') {
    return {
      tone: 'closed',
      headlineKey: 'visitRequestV2:revision.applied',
      noteKey: 'visitRequestV2:revision.closedNote',
      values,
    };
  }

  // Nothing decided yet: say so plainly instead of showing an approval number that only means
  // "this is the first content revision".
  const decided = status !== 'WAITING_REQUEST_APPROVAL' && Boolean(decidedAt);
  if (!decided) {
    return {
      tone: 'waiting',
      headlineKey: 'visitRequestV2:revision.current',
      noteKey: 'visitRequestV2:revision.notApprovedYet',
      values,
    };
  }

  // Decided and live. A pending proposal is reported separately so it is never mistaken for the
  // content currently in force.
  if (activeAmendmentNo != null) {
    return {
      tone: 'active',
      headlineKey: 'visitRequestV2:revision.applied',
      noteKey: 'visitRequestV2:revision.amendmentPending',
      values: { ...values, amendmentNo: activeAmendmentNo },
    };
  }

  return {
    tone: 'active',
    headlineKey: 'visitRequestV2:revision.applied',
    noteKey: 'visitRequestV2:revision.approvedAt',
    values,
  };
};
