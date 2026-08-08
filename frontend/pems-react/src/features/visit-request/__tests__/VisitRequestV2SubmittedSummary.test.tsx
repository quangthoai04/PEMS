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
  pendingContactConfirmations: 0,
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

  /**
   * The receipt used to print "Đối tác đã có trong hệ thống (ID 109)". That number is the partners
   * table's primary key: it means nothing to the person reading their receipt, and it is not a
   * detail an internal system should hand out. The link itself is unchanged — `partnerId` is still
   * what the payload carried and what the backend joined on; it simply is not rendered.
   */
  it('names the linked partner without ever showing its database id', () => {
    const linked: VisitRequestV2Schema = {
      ...values([campus({ clientKey: 'a', campus: 'HN' })]),
      partnerSelectionMode: 'EXISTING_PARTNER',
      partnerId: 109,
      registerInfo: {
        fullName: 'Registrant', organization: 'Andes University Exchange Office', jobTitle: 'Head',
        phone: '+84911111111', email: 'reg@example.com', nationality: 'VN',
      },
    };
    render(<VisitRequestV2SubmittedSummary
      response={response([{ visitInstanceId: 11, campusId: 1, status: 'WAITING_REQUEST_APPROVAL' }], false)}
      values={linked} />);

    const partner = screen.getByTestId('v2-summary-partner-existing');
    expect(partner).toHaveTextContent('Andes University Exchange Office');
    expect(partner).toHaveTextContent(/Existing partner/i);
    expect(partner.textContent).not.toMatch(/109/);
    expect(screen.queryByText(/ID\s*109/i)).toBeNull();
  });

  it('says "new organization" when nothing was linked, and shows no id there either', () => {
    render(<VisitRequestV2SubmittedSummary
      response={response([{ visitInstanceId: 11, campusId: 1, status: 'WAITING_REQUEST_APPROVAL' }], false)}
      values={values([campus({ clientKey: 'a', campus: 'HN' })])} />);

    expect(screen.queryByTestId('v2-summary-partner-existing')).toBeNull();
    expect(screen.getByText(/New organization/i)).toBeInTheDocument();
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

  /**
   * Working content, the transport note and the note to FPTU used to be rendered only when non-empty,
   * so a guest who skipped an optional field got a receipt on which that field had never been asked.
   * On the one screen whose whole job is to say "here is what we received", a silently absent row
   * reads as a lost answer. Each now falls back to the "none" placeholder instead of vanishing.
   */
  it('keeps the optional rows, with a placeholder, when the guest left them blank', () => {
    renderOne(withContact({ workingContent: '', transportationNote: '', notes: '' }));
    const card = within(screen.getByTestId('campus-summary-0'));

    for (const label of ['Working content', 'Transportation', 'Note to FPTU']) {
      const row = card.getByText(label).parentElement as HTMLElement;
      expect(within(row).getByText('—')).toBeInTheDocument();
    }
  });

  it('shows the note to FPTU and the transport note as two separate rows', () => {
    renderOne(withContact({
      transportationNote: 'Đoàn sử dụng shuttle từ cổng chính.',
      notes: 'Đoàn có một khách dị ứng hải sản.',
    }));
    const card = within(screen.getByTestId('campus-summary-0'));

    const transport = card.getByText('Transportation').parentElement as HTMLElement;
    const note = card.getByText('Note to FPTU').parentElement as HTMLElement;
    expect(within(transport).getByText('Đoàn sử dụng shuttle từ cổng chính.')).toBeInTheDocument();
    expect(within(note).getByText('Đoàn có một khách dị ứng hải sản.')).toBeInTheDocument();
  });

  it('never fills a blank note to FPTU from the transport note', () => {
    renderOne(withContact({ transportationNote: 'Xe 45 chỗ, biển 29B-123.45', notes: '' }));
    const card = within(screen.getByTestId('campus-summary-0'));

    const note = card.getByText('Note to FPTU').parentElement as HTMLElement;
    expect(within(note).getByText('—')).toBeInTheDocument();
    expect(within(note).queryByText(/29B-123\.45/)).toBeNull();
  });
});

/**
 * Each campus card is built from that campus's own entry in `values.campusVisits`. These pin that a
 * per-campus note never leaks sideways — neither by a request-level fallback nor by the first campus
 * standing in as representative.
 */
describe('VisitRequestV2SubmittedSummary — per-campus notes', () => {
  it('gives each campus of a MIXED request its own note to FPTU', () => {
    const cvs = [
      campus({ clientKey: 'a', campus: 'HN', notes: 'HN note' }),
      campus({ clientKey: 'b', campus: 'HCM', notes: 'HCM note' }),
    ];
    render(<VisitRequestV2SubmittedSummary
      response={response([{ visitInstanceId: 11, campusId: 1, status: 'WAITING_REQUEST_APPROVAL' },
                          { visitInstanceId: 22, campusId: 2, status: 'WAITING_REQUEST_APPROVAL' }], true)}
      values={values(cvs)} />);

    const cardA = within(screen.getByTestId('campus-summary-0'));
    const cardB = within(screen.getByTestId('campus-summary-1'));

    expect(cardA.getByText('HN note')).toBeInTheDocument();
    expect(cardA.queryByText('HCM note')).toBeNull();
    expect(cardB.getByText('HCM note')).toBeInTheDocument();
    expect(cardB.queryByText('HN note')).toBeNull();
  });

  it('leaves one campus blank while the other has a note', () => {
    const cvs = [
      campus({ clientKey: 'a', campus: 'HN', notes: 'Chỉ HN có ghi chú' }),
      campus({ clientKey: 'b', campus: 'HCM', notes: '' }),
    ];
    render(<VisitRequestV2SubmittedSummary
      response={response([{ visitInstanceId: 11, campusId: 1, status: 'WAITING_REQUEST_APPROVAL' },
                          { visitInstanceId: 22, campusId: 2, status: 'WAITING_REQUEST_APPROVAL' }], true)}
      values={values(cvs)} />);

    const cardB = within(screen.getByTestId('campus-summary-1'));
    const note = cardB.getByText('Note to FPTU').parentElement as HTMLElement;
    // Blank stays blank: HN's note must not be borrowed to fill HCM's row.
    expect(within(note).getByText('—')).toBeInTheDocument();
    expect(cardB.queryByText('Chỉ HN có ghi chú')).toBeNull();
  });
});
