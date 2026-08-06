import type { ResolvedCampusVisit } from '../api/visitRequestV2Api';

/** Shared read-model fixture for the v2 detail component tests. */
export const campusFixture = (overrides: Partial<ResolvedCampusVisit> = {}): ResolvedCampusVisit => ({
  visitInstanceId: 10,
  campusId: 1,
  campusCode: 'HN',
  campusName: 'FPTU Hà Nội',
  plannedStartAt: '2026-08-01T09:00:00',
  plannedEndAt: '2026-08-01T11:30:00',
  timezone: 'Asia/Ho_Chi_Minh',
  // A CAMPUS instance is never "APPROVED" — that value belongs to visit_requests.status. The
  // decided-and-hosted state of an instance is ASSIGNED (see the visit_request_campuses enum).
  instanceStatus: 'ASSIGNED',
  currentHostUserId: 5,
  currentHostName: 'Host Hà Nội',
  decidedByUserId: 6,
  decidedByName: 'Leader HN',
  decidedAt: '2026-07-20T10:00:00',
  decisionActorRole: 'STAFF_LEADER',
  decisionNote: 'OK',
  cancelledByUserId: null,
  cancelledByName: null,
  cancelledAt: null,
  cancellationActorType: null,
  cancellationSource: null,
  cancellationReason: null,
  delegationName: 'Đoàn ĐH ABC',
  visitType: 'MEETING',
  visitTypeOther: null,
  purpose: 'Trao đổi hợp tác',
  workingContent: 'Nội dung làm việc HN',
  visitors: [
    { guestMemberId: 1, memberType: 'VISITOR', fullName: 'Khách Một', organization: 'ĐH ABC', jobTitle: 'GV', nationality: 'VN', displayOrder: 1 },
  ],
  supportMembers: [],
  operationalContact: {
    fullName: 'Đầu Mối HN', organization: 'ĐH ABC', jobTitle: 'Trưởng phòng',
    phone: '+84912345678', email: 'dm@x.vn',
    confirmationStatus: 'CONFIRMED', confirmationSource: 'EMAIL_CONFIRMATION',
    confirmedAt: '2026-08-01T09:00:00',
  },
  // In step with currentHostUserId/currentHostName: the host BLOCK renders from this object, and a
  // fixture carrying only the flat scalars models a payload the backend no longer sends.
  currentHost: {
    userId: 5, fullName: 'Host Hà Nội', email: 'host.hn@fptu.edu.vn',
    phone: '+84900000005', departmentName: 'Phòng Hợp tác Quốc tế',
  },
  proposedHost: null,
  hostSelection: {
    canProposeSelfAsHost: false,
    canProposeOtherHost: false,
    canWaitForLaterAssignment: false,
    canUpdateProposedHost: false,
  },
  workingLanguage: 'VI',
  transportationNote: null,
  mediaConsentStatus: 'DECLINED',
  mediaConsentNote: null,
  formRevision: 2,
  approvalRevision: 1,
  rowVersion: 3,
  activeAmendment: null,
  // ASSIGNED and well ahead of its start, so the backend would grant the per-campus mutations. The
  // components gate on these codes, never on status, so a fixture without them models a campus the
  // backend has closed — which is a case worth testing, but not the default one.
  allowedActions: ['SUBMIT_SAFE_EDIT', 'SUBMIT_AMENDMENT'],
  ...overrides,
});
