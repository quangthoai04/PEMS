import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';

vi.mock('../hooks/useRegistrationCampuses', () => ({
  useRegistrationCampuses: () => ({
    campuses: [
      { campusId: 1, campusCode: 'HN', campusName: 'FPT Hà Nội', city: null },
      { campusId: 2, campusCode: 'HCM', campusName: 'FPT HCM', city: null },
    ],
    loading: false,
    error: false,
  }),
}));

import { VisitRequestV2SubmittedSummary } from '../components/v2/VisitRequestV2SubmittedSummary';
import type { CampusVisitSchema, VisitRequestV2Schema } from '../schema/visitRequestV2.schema';
import type { V2CreateResponse } from '../api/visitRequestV2Api';

const campus = (over: Partial<CampusVisitSchema>): CampusVisitSchema => ({
  clientKey: over.clientKey ?? 'ck-' + Math.random(),
  campus: 'HN',
  startDatetime: '2026-09-01T09:00',
  endDatetime: '2026-09-01T11:30',
  delegationName: 'Delegation A',
  visitType: 'MEETING',
  visitTypeOther: '',
  purpose: 'Purpose A',
  workingContent: '',
  visitors: [{ fullName: 'Visitor A', jobTitle: 'Dean', organization: 'Univ A', nationality: 'US' }],
  supportTeam: [],
  operationalContact: { fullName: 'Op A', organization: 'OrgA', jobTitle: 'Trưởng phòng Hợp tác', phone: '+84900000001', email: 'op-a@example.com' },
  workingLanguage: 'EN',
  transportationNote: '',
  mediaConsentStatus: 'DECLINED',
  notes: '',
  ...over,
});

const values = (campusVisits: CampusVisitSchema[]): VisitRequestV2Schema => ({
  registerInfo: {
    fullName: 'Registrant', organization: 'Reg Org', jobTitle: 'Head',
    phone: '+84911111111', email: 'reg@example.com', nationality: 'VN',
  },
  partnerSelectionMode: 'NEW_ORGANIZATION',
  partnerId: null,
  campusVisits,
});

const response = (instances: V2CreateResponse['instances'], mixed: boolean): V2CreateResponse => ({
  visitRequestId: 900,
  requestCode: 'VR-TEST-900',
  visitScope: instances.length > 1 ? 'MULTI_CAMPUS' : 'SINGLE_CAMPUS',
  hasMixedCampusDetails: mixed,
  status: 'WAITING_REQUEST_APPROVAL',
  submittedAt: '2026-08-01T09:30:00',
  campusCount: instances.length,
  instances,
  pendingConfirmations: 0,
  idempotent: false,
});

describe('VisitRequestV2SubmittedSummary', () => {
  it('renders one card per campus with each campus its OWN content (mixed)', () => {
    const cvs = [
      campus({ clientKey: 'a', campus: 'HN', delegationName: 'Đoàn HN', purpose: 'Mục đích HN' }),
      campus({ clientKey: 'b', campus: 'HCM', delegationName: 'Đoàn HCM', purpose: 'Mục đích HCM' }),
    ];
    render(<VisitRequestV2SubmittedSummary
      response={response([{ visitInstanceId: 11, campusId: 1, status: 'WAITING_REQUEST_APPROVAL' },
                          { visitInstanceId: 22, campusId: 2, status: 'WAITING_REQUEST_APPROVAL' }], true)}
      values={values(cvs)} />);

    const cardA = screen.getByTestId('campus-summary-0');
    const cardB = screen.getByTestId('campus-summary-1');

    // Campus A shows A's content and NOT B's; campus B shows B's.
    expect(within(cardA).getByText(/FPT Hà Nội/)).toBeInTheDocument();
    expect(within(cardA).getByText('Đoàn HN')).toBeInTheDocument();
    expect(within(cardA).queryByText('Đoàn HCM')).toBeNull();
    expect(within(cardB).getByText(/FPT HCM/)).toBeInTheDocument();
    expect(within(cardB).getByText('Đoàn HCM')).toBeInTheDocument();

    // Request code + mixed badge at the request level.
    expect(screen.getByText('VR-TEST-900')).toBeInTheDocument();
    expect(screen.getByText(/Varies by campus/i)).toBeInTheDocument();
  });

  it('renders every campus of a multi-SAME request (never just the first as representative)', () => {
    const cvs = [
      campus({ clientKey: 'a', campus: 'HN', delegationName: 'Đoàn chung' }),
      campus({ clientKey: 'b', campus: 'HCM', delegationName: 'Đoàn chung' }),
    ];
    render(<VisitRequestV2SubmittedSummary
      response={response([{ visitInstanceId: 11, campusId: 1, status: 'WAITING_REQUEST_APPROVAL' },
                          { visitInstanceId: 22, campusId: 2, status: 'WAITING_REQUEST_APPROVAL' }], false)}
      values={values(cvs)} />);

    expect(screen.getByTestId('campus-summary-0')).toBeInTheDocument();
    expect(screen.getByTestId('campus-summary-1')).toBeInTheDocument();
    expect(screen.getByText(/Same across all campuses/i)).toBeInTheDocument();
  });

  it('renders a blank optional operational contact org/email without crashing', () => {
    const cvs = [campus({ clientKey: 'a', campus: 'HN',
      operationalContact: { fullName: 'Op Only', organization: '', jobTitle: 'Trưởng phòng Hợp tác', phone: '+84900000009', email: '' } })];
    render(<VisitRequestV2SubmittedSummary
      response={response([{ visitInstanceId: 11, campusId: 1, status: 'WAITING_REQUEST_APPROVAL' }], false)}
      values={values(cvs)} />);

    const card = screen.getByTestId('campus-summary-0');
    // Name and job title share one line (blank org is dropped from the join), so this matches on
    // the name rather than the whole node.
    expect(within(card).getByText(/Op Only/)).toBeInTheDocument();
    // phone still shows; email empty is fine (no crash, phone rendered).
    expect(within(card).getByText(/\+84900000009/)).toBeInTheDocument();
  });
});

// ── The post-submit summary shows what was actually submitted ────────────────
/**
 * This is the screen a guest reads immediately after submitting, so a field the form collected and
 * this page omits reads as "it did not go through". Two were wrong here: the consent answer carried
 * the media note glued on after an em dash, and the operational contact's job title — asked for on
 * the form, and the line that says whether that person can settle a schedule — was dropped.
 */
describe('VisitRequestV2SubmittedSummary — full submitted form parity', () => {
  const withContact = (over: Partial<CampusVisitSchema> = {}) => campus({
    clientKey: 'p', campus: 'HN',
    operationalContact: {
      fullName: 'Trần Thị B', organization: 'ĐH Đối Tác',
      jobTitle: 'Trưởng phòng Hợp tác Quốc tế', phone: '+84900000002', email: 'b@example.com',
    },
    ...over,
  });

  const renderOne = (cv: CampusVisitSchema) => render(<VisitRequestV2SubmittedSummary
    response={response([{ visitInstanceId: 11, campusId: 1, status: 'WAITING_REQUEST_APPROVAL' }], false)}
    values={values([cv])} />);

  it('shows the consent answer and the guest note as two separate facts', () => {
    renderOne(withContact({ mediaConsentStatus: 'AGREED', notes: 'Xin hỗ trợ xe điện cho khách lớn tuổi.' }));
    const card = screen.getByTestId('campus-summary-0');

    expect(within(card).getByText('Xin hỗ trợ xe điện cho khách lớn tuổi.')).toBeInTheDocument();
    // Never "Đồng ý — <note>" / "Agreed — <note>".
    expect(within(card).queryByText(/—\s*Xin hỗ trợ xe điện/)).not.toBeInTheDocument();
  });

  it('shows the note when consent is DECLINED too', () => {
    renderOne(withContact({ mediaConsentStatus: 'DECLINED', notes: 'Cần phiên dịch buổi chiều.' }));
    expect(within(screen.getByTestId('campus-summary-0')).getByText('Cần phiên dịch buổi chiều.')).toBeInTheDocument();
  });

  it("shows the operational contact's job title, which the form asked for", () => {
    renderOne(withContact());
    expect(within(screen.getByTestId('campus-summary-0'))
      .getByText(/Trưởng phòng Hợp tác Quốc tế/)).toBeInTheDocument();
  });
});
