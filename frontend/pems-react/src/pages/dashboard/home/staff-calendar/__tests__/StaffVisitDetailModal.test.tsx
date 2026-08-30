import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

// F4: the modal's "Hình thức" row must translate every real backend visitType value (VisitTypes.cs)
// via the shared VISIT_TYPE_LABELS map (features/delegations/components/RequestInfoReadOnly) instead
// of its own incomplete local copy, and must not crash on a value neither map knows about yet.
vi.mock('../../../../../features/dashboard/api/staffCalendarApi', () => ({
  staffCalendarApi: { getDetail: vi.fn() },
}));

import { StaffVisitDetailModal } from '../StaffVisitDetailModal';
import { staffCalendarApi } from '../../../../../features/dashboard/api/staffCalendarApi';
import type { StaffCalendarDetail } from '../../../../../features/dashboard/api/staffCalendarApi';

const mockGetDetail = vi.mocked(staffCalendarApi.getDetail);

const baseDetail: StaffCalendarDetail = {
  visitRequestId: 1,
  visitInstanceId: 1,
  requestCode: 'REQ-001',
  delegationName: 'Đoàn ABC',
  visitScope: 'SINGLE_CAMPUS',
  requestStatus: 'APPROVED',
  campusStatus: 'ASSIGNED',
  displayStatus: 'Đã gán host',
  colorType: 'PROCESSED',
  rowVersion: 1,
  campusId: 1,
  campusName: 'Hòa Lạc',
  plannedStartAt: '2026-09-01T09:00:00',
  plannedEndAt: '2026-09-01T11:00:00',
  registrantFullName: null,
  registrantOrganization: null,
  registrantJobTitle: null,
  registrantNationality: null,
  registrantPhone: null,
  registrantEmail: null,
  operationalContactFullName: null,
  operationalContactOrganization: null,
  operationalContactJobTitle: null,
  operationalContactPhone: null,
  operationalContactEmail: null,
  purpose: null,
  workingContent: null,
  visitType: null,
  visitTypeOther: null,
  guestCount: 0,
  workingLanguage: null,
  mediaConsentStatus: null,
  transportationNote: null,
  notes: null,
  currentHostUserId: null,
  currentHostName: null,
  currentHostEmail: null,
  hostAssignedAt: null,
  hostAssignedByName: null,
  isCurrentHost: false,
  decisionNote: null,
  decidedByName: null,
  decidedAt: null,
  isCancelled: false,
  cancellationReason: null,
  cancelledAt: null,
  isPast: false,
  isExpired: false,
  participantResponses: [],
  allowedActions: {
    canViewDetail: true,
    canApprove: false,
    canReject: false,
    canAssignHost: false,
    canSetupDelegation: false,
  },
};

function renderWithVisitType(visitType: string | null) {
  mockGetDetail.mockResolvedValue({ ...baseDetail, visitType });
  return render(
    <MemoryRouter>
      <StaffVisitDetailModal isOpen visitInstanceId={1} onClose={() => {}} />
    </MemoryRouter>,
  );
}

describe('StaffVisitDetailModal — "Hình thức" label covers every real backend visit type', () => {
  beforeEach(() => vi.clearAllMocks());

  it.each([
    ['CAMPUS_TOUR', 'Tham quan cơ sở (Campus tour)'],
    ['MEETING', 'Họp trao đổi'],
    ['WORKSHOP', 'Hội thảo'],
    ['SIGNING_CEREMONY', 'Lễ ký kết'],
    ['EXCHANGE', 'Giao lưu'],
    ['OTHER', 'Khác'],
  ])('renders a human-readable label for %s', async (visitType, expectedLabel) => {
    renderWithVisitType(visitType);
    expect(await screen.findByText(expectedLabel)).toBeInTheDocument();
  });

  it('falls back to the raw value for an unknown future visit type instead of crashing', async () => {
    renderWithVisitType('SOMETHING_NEW');
    expect(await screen.findByText('SOMETHING_NEW')).toBeInTheDocument();
  });
});
