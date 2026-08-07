import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { SubmittedVisitRequestInfoPanel } from '../components/SubmittedVisitRequestInfoPanel';
import type { SubmittedVisitRequestFormDetail } from '../types/delegations.types';

/**
 * Pins `hideOperationalContact`: department reception staff/leader are authorized to open this
 * detail (assigned a logistics item or invited as a participant — see
 * GetSubmittedVisitRequestFormDetailQueryHandler), but who the guest coordinates with at the
 * campus is IC's business, not theirs. Every other caller (HO, campus Staff Leader, host, the
 * visitor themselves) leaves the prop unset and must keep seeing the section.
 */

const CONTACT_HEADING = 'Đầu mối đoàn khách phối hợp tại cơ sở';

const detail = (): SubmittedVisitRequestFormDetail =>
  ({
    visitRequestId: 7,
    requestCode: 'VR-7',
    requestStatus: 'APPROVED',
    visitScope: 'SINGLE_CAMPUS',
    delegationName: 'Đoàn ABC',
    registrant: { fullName: 'Người Đăng Ký', email: 'reg@x.vn' },
    campuses: [{
      visitInstanceId: 1,
      campusId: 1,
      campusCode: 'HN',
      campusName: 'Hòa Lạc',
      plannedStartAt: '2026-08-27T09:00:00',
      plannedEndAt: '2026-08-27T12:00:00',
      instanceStatus: 'ASSIGNED',
      isOwnCampus: false,
      operationalContact: {
        fullName: 'Đầu Mối Cơ Sở',
        organization: 'FPT University - IC Hà Nội',
        jobTitle: 'Chuyên viên Hợp tác Quốc tế',
        phone: '0901000004',
        email: 'staff.hn@fpt.edu.vn',
        confirmed: true,
      },
    }],
    guestMembers: [],
    externalSupportMembers: [],
    canApprove: false,
    canReject: false,
    canCancel: false,
  }) as SubmittedVisitRequestFormDetail;

describe('SubmittedVisitRequestInfoPanel — hideOperationalContact', () => {
  it('shows the operational-contact section by default (HO/Staff Leader/host/visitor)', () => {
    render(<SubmittedVisitRequestInfoPanel data={detail()} />);
    expect(screen.getByText(CONTACT_HEADING)).toBeInTheDocument();
    expect(screen.getByText('staff.hn@fpt.edu.vn')).toBeInTheDocument();
  });

  it('hides the operational-contact section when the department reception views it', () => {
    render(<SubmittedVisitRequestInfoPanel data={detail()} hideOperationalContact />);
    expect(screen.queryByText(CONTACT_HEADING)).toBeNull();
    expect(screen.queryByText('staff.hn@fpt.edu.vn')).toBeNull();
  });

  it('keeps every other section intact when hiding the operational contact', () => {
    render(<SubmittedVisitRequestInfoPanel data={detail()} hideOperationalContact />);
    expect(screen.getByText('Thông tin chuyến thăm')).toBeInTheDocument();
    expect(screen.getByText('Cơ sở & thời gian dự kiến')).toBeInTheDocument();
    expect(screen.getByText('Đoàn ABC')).toBeInTheDocument();
  });
});
