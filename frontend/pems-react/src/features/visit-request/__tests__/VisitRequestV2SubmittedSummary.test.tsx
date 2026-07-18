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
  operationalContact: { fullName: 'Op A', organization: 'OrgA', phone: '+84900000001', email: 'op-a@example.com' },
  workingLanguage: 'EN',
  transportationNote: '',
  mediaConsentStatus: 'DECLINED',
  mediaConsentNote: '',
  notes: '',
  ...over,
});

const values = (campusVisits: CampusVisitSchema[]): VisitRequestV2Schema => ({
  registerInfo: {
    fullName: 'Registrant', organization: 'Reg Org', jobTitle: 'Head',
    phone: '+84911111111', email: 'reg@example.com', nationality: 'VN',
  },
  contactPoint: { fullName: 'Contact', organization: 'C Org', phone: '+84922222222', email: 'contact@example.com' },
  partnerSelectionMode: 'NEW_ORGANIZATION',
  partnerId: null,
  campusVisits,
});

const response = (instances: V2CreateResponse['instances'], mixed: boolean): V2CreateResponse => ({
  visitRequestId: 900,
  requestCode: 'VR-TEST-900',
  visitScope: instances.length > 1 ? 'MULTI_CAMPUS' : 'SINGLE_CAMPUS',
  hasMixedCampusDetails: mixed,
  primaryContactAccessStatus: 'PENDING_CONFIRMATION',
  contactClaimPending: true,
  instances,
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
      operationalContact: { fullName: 'Op Only', organization: '', phone: '+84900000009', email: '' } })];
    render(<VisitRequestV2SubmittedSummary
      response={response([{ visitInstanceId: 11, campusId: 1, status: 'WAITING_REQUEST_APPROVAL' }], false)}
      values={values(cvs)} />);

    const card = screen.getByTestId('campus-summary-0');
    expect(within(card).getByText('Op Only')).toBeInTheDocument();
    // phone still shows; email empty is fine (no crash, phone rendered).
    expect(within(card).getByText(/\+84900000009/)).toBeInTheDocument();
  });
});
