import { describe, expect, it } from 'vitest';
import { buildChangedOnlyPayload, type SafeEditInstanceDraft, type SafeEditRegistrantDraft } from '../utils/safeEditDiff';
import type { ResolvedVisitForm } from '../api/visitRequestV2Api';
import { campusFixture } from './fixtures';

/**
 * GitHub "SAFE EDIT REGISTRANT UX HARDENING" — Test F. `VisitSafeEditModal.tsx` disables the registrant
 * fieldset (and, after this task, each custom control inside it) when `canEditShared` is false, but a
 * disabled DOM node is a UX affordance, not a security boundary — a stale `registrant` draft, a future
 * field added without wiring its own `disabled` prop, or a bypassed control must still be unable to
 * reach the outgoing payload. These tests call the pure payload builder directly, with a draft that
 * genuinely differs from `form.registrant`, and prove `canEditShared=false` alone is what keeps
 * `payload.registrant` null — no rendering involved.
 */

const baseForm = (): ResolvedVisitForm => ({
  visitRequestId: 1,
  requestCode: 'VR-1',
  rowVersion: 4,
  hasMixedCampusDetails: false,
  visitScope: 'SINGLE_CAMPUS',
  requestStatus: 'APPROVED',
  createdSource: 'PUBLIC',
  submittedAt: '2026-07-15T08:00:00',
  partnerId: null,
  cancelledByUserId: null,
  cancelledByName: null,
  cancelledAt: null,
  cancellationReason: null,
  registrant: {
    fullName: 'Reg', organization: 'Org', jobTitle: 'Head', phone: '+84900000001', email: 'r@x.vn', nationality: 'VN',
  },
  confirmationSummary: { total: 1, confirmed: 0, pending: 1, declined: 0, expired: 0, gateOpen: false },
  requestOutcome: null,
  campusVisits: [],
  viewer: { relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false, allowedActions: [] },
});

const divergedRegistrant: SafeEditRegistrantDraft = {
  fullName: 'Someone else entirely',
  nationality: 'JP',
  organization: 'Other Org',
  jobTitle: 'Other Job',
  phone: '+84900000099',
  partnerId: 42,
};

describe('buildChangedOnlyPayload — the registrant lock is enforced by the function itself, not only by disabled DOM', () => {
  it('never builds a Registrant patch when canEditShared is false, even though the draft genuinely differs', () => {
    const payload = buildChangedOnlyPayload(baseForm(), divergedRegistrant, [], false);
    // Nothing else changed either, so the whole call is correctly a no-op — not a silently-dropped edit.
    expect(payload).toBeNull();
  });

  it('a locked, diverged registrant draft never leaks into a payload that DOES contain a real campus change', () => {
    const form = baseForm();
    const campus = campusFixture();
    form.campusVisits = [campus];
    const instances: SafeEditInstanceDraft[] = [{
      visitInstanceId: campus.visitInstanceId,
      expectedRowVersion: campus.rowVersion,
      campusName: campus.campusName,
      transportationNote: 'Xe 29 chỗ',
      mediaConsentStatus: campus.mediaConsentStatus,
      notes: campus.notes ?? '',
      contactFullName: campus.operationalContact.fullName,
      contactOrganization: campus.operationalContact.organization,
      contactJobTitle: campus.operationalContact.jobTitle,
      contactPhone: campus.operationalContact.phone ?? '',
      contactGuestMemberId: campus.operationalContact.guestMemberId ?? null,
    }];

    const payload = buildChangedOnlyPayload(form, divergedRegistrant, instances, false);

    expect(payload).not.toBeNull();
    expect(payload!.registrant).toBeNull();
    expect(payload!.instances).toHaveLength(1);
    expect(payload!.instances![0]).toMatchObject({
      visitInstanceId: campus.visitInstanceId, transportationNote: 'Xe 29 chỗ',
    });
  });

  it('builds a Registrant patch normally once canEditShared is true', () => {
    const changed: SafeEditRegistrantDraft = {
      fullName: 'New Name', nationality: 'VN', organization: 'Org', jobTitle: 'Head', phone: '+84900000001', partnerId: null,
    };
    const payload = buildChangedOnlyPayload(baseForm(), changed, [], true);
    expect(payload?.registrant).toMatchObject({ fullName: 'New Name' });
  });

  it('an UNCHANGED registrant draft builds no patch even when canEditShared is true', () => {
    const form = baseForm();
    const unchanged: SafeEditRegistrantDraft = {
      fullName: form.registrant.fullName,
      nationality: form.registrant.nationality,
      organization: form.registrant.organization,
      jobTitle: form.registrant.jobTitle,
      phone: form.registrant.phone,
      partnerId: form.partnerId,
    };
    const payload = buildChangedOnlyPayload(form, unchanged, [], true);
    expect(payload).toBeNull();
  });
});
