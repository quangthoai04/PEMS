import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { DelegationInfoReadOnly } from '../components/RequestInfoReadOnly';
import type { VisitProcessRequestSummary } from '../types/delegations.types';

/**
 * "Ghi chú gửi FPTU" is the guest's one general remark about a campus — dietary needs, accessibility,
 * a document to have ready. It lives in visit_instance_form_details.notes and reaches this renderer as
 * `summary.notes`.
 *
 * It was rendered behind `summary?.notes?.trim() && …`, so the row disappeared whenever the guest left
 * it blank. That is not a harmless saving of space: the host preparing the visit cannot distinguish
 * "the guest was asked and had nothing to add" from "this form never asked", and only the first of
 * those means it is safe to stop looking for a wheelchair route or a vegetarian meal. Every other
 * field on this block already renders its own "Chưa có thông tin" placeholder; notes now does too.
 *
 * The second half of this file pins the field apart from the four other things called a "note" in this
 * system. `transportationNote` identifies the vehicle; `decisionNote` is why a campus was rejected;
 * `cancellationReason` is why it was called off; a participant's `note` is their own invitation text.
 * None of them is this field, and none may ever be shown in its place.
 *
 * `DelegationInfoReadOnly` is the shared renderer behind VisitProcess and VisitProcessSummaryPage.
 */

const EMPTY = 'Chưa có thông tin';
const NOTES_LABEL = 'Ghi chú gửi FPTU:';
const TRANSPORT_LABEL = 'Nhận diện phương tiện di chuyển:';

const summary = (overrides: Partial<VisitProcessRequestSummary> = {}): VisitProcessRequestSummary => ({
  registrantName: 'Người Đăng Ký',
  registrantOrganization: 'ĐH Nguồn',
  registrantJobTitle: 'Trưởng khoa',
  registrantPhone: '+84900000001',
  registrantEmail: 'reg@x.vn',
  registrantNationality: 'VN',
  delegationName: 'Đoàn ĐH ABC',
  visitScope: 'SINGLE_CAMPUS',
  visitType: 'MEETING',
  purpose: 'Trao đổi hợp tác',
  workingContent: 'Nội dung làm việc',
  workingLanguage: 'VI',
  mediaConsentStatus: 'AGREED',
  transportationNote: null,
  notes: null,
  operationalContactFullName: 'Đầu Mối Cơ Sở',
  operationalContactOrganization: 'ĐH Đối Tác',
  operationalContactJobTitle: 'Trưởng phòng Hợp tác Quốc tế',
  operationalContactPhone: '+84900000002',
  operationalContactEmail: 'op@partner.vn',
  campuses: [],
  guestMembers: [],
  externalSupportMembers: [],
  ...overrides,
}) as VisitProcessRequestSummary;

/** The value paragraph that follows a multiline label, so a blank one can be asserted on. */
const valueAfter = (label: string) => screen.getByText(label).parentElement?.querySelector('p');

describe('Ghi chú gửi FPTU on the shared full-detail renderer', () => {
  it('shows the guest note when there is one', () => {
    render(<DelegationInfoReadOnly summary={summary({
      notes: 'Đoàn có 2 khách ăn chay và cần lối đi hỗ trợ xe lăn.',
    })} />);

    expect(screen.getByText(NOTES_LABEL)).toBeInTheDocument();
    expect(screen.getByText('Đoàn có 2 khách ăn chay và cần lối đi hỗ trợ xe lăn.')).toBeInTheDocument();
  });

  it('keeps the row, with a placeholder, when the guest wrote nothing', () => {
    render(<DelegationInfoReadOnly summary={summary({ notes: null })} />);

    expect(screen.getByText(NOTES_LABEL)).toBeInTheDocument();
    expect(valueAfter(NOTES_LABEL)).toHaveTextContent(EMPTY);
  });

  it('keeps the row when the guest submitted only whitespace', () => {
    render(<DelegationInfoReadOnly summary={summary({ notes: '   ' })} />);

    expect(screen.getByText(NOTES_LABEL)).toBeInTheDocument();
    expect(valueAfter(NOTES_LABEL)).toHaveTextContent(EMPTY);
  });

  it('keeps the transportation row on the same terms', () => {
    render(<DelegationInfoReadOnly summary={summary({ transportationNote: null })} />);

    expect(screen.getByText(TRANSPORT_LABEL)).toBeInTheDocument();
    expect(valueAfter(TRANSPORT_LABEL)).toHaveTextContent(EMPTY);
  });
});

describe('Ghi chú gửi FPTU is not any of the other notes', () => {
  it('shows the guest note and the vehicle note as two separate rows', () => {
    render(<DelegationInfoReadOnly summary={summary({
      notes: 'Khách cần xe lăn',
      transportationNote: 'Đoàn sử dụng shuttle từ cổng chính.',
    })} />);

    expect(valueAfter(NOTES_LABEL)).toHaveTextContent('Khách cần xe lăn');
    expect(valueAfter(TRANSPORT_LABEL)).toHaveTextContent('Đoàn sử dụng shuttle từ cổng chính.');
  });

  it('leaves the guest note empty when only the vehicle note was filled in', () => {
    render(<DelegationInfoReadOnly summary={summary({
      notes: null,
      transportationNote: 'Xe 45 chỗ, biển 29B-123.45',
    })} />);

    // The vehicle note must never be promoted into the general-note row to fill the gap.
    expect(valueAfter(NOTES_LABEL)).toHaveTextContent(EMPTY);
    expect(valueAfter(TRANSPORT_LABEL)).toHaveTextContent('Xe 45 chỗ, biển 29B-123.45');
  });
});

/**
 * A cancelled or rejected campus is still a campus somebody has to account for, and the form the guest
 * submitted does not stop existing because the answer was no. These pin that the renderer reads only
 * the form fields — no status reaches it, so no status can take a field away — and that the
 * cancellation / decision text arrives alongside the form rather than in place of it.
 */
describe('the form survives every terminal status', () => {
  it('still shows the guest note on a cancelled request', () => {
    render(<DelegationInfoReadOnly summary={summary({
      notes: 'Khách cần xe lăn',
      // What the request row carries on cancellation. It is NOT form content and never lands here.
    })} />);

    expect(screen.getByText('Tên đoàn khách:')).toBeInTheDocument();
    expect(screen.getByText('Mục đích thăm:')).toBeInTheDocument();
    expect(valueAfter(NOTES_LABEL)).toHaveTextContent('Khách cần xe lăn');
    // The cancellation reason belongs to the request, not the form, so it is nowhere in this block.
    expect(screen.queryByText(/Đoàn hủy do thay đổi lịch/)).toBeNull();
  });

  it('still shows the guest note on a rejected request', () => {
    render(<DelegationInfoReadOnly summary={summary({ notes: 'Khách ăn chay' })} />);

    expect(valueAfter(NOTES_LABEL)).toHaveTextContent('Khách ăn chay');
    // A rejection's decisionNote is a different column and must never overwrite the guest's note.
    expect(screen.queryByText(/Không đủ thời gian chuẩn bị/)).toBeNull();
  });
});

/**
 * MIXED requests: each campus instance holds its own row in visit_instance_form_details, and the
 * backend resolves the target instance before this renderer sees anything. Rendering the two campus
 * summaries pins that the component carries no request-level fallback that could bleed one into
 * the other.
 */
describe('multi-campus notes stay with their own campus', () => {
  it('renders each campus its own note', () => {
    const { unmount } = render(<DelegationInfoReadOnly summary={summary({ notes: 'HN note' })} />);
    expect(valueAfter(NOTES_LABEL)).toHaveTextContent('HN note');
    expect(screen.queryByText('HCM note')).toBeNull();
    unmount();

    render(<DelegationInfoReadOnly summary={summary({ notes: 'HCM note' })} />);
    expect(valueAfter(NOTES_LABEL)).toHaveTextContent('HCM note');
    expect(screen.queryByText('HN note')).toBeNull();
  });
});
