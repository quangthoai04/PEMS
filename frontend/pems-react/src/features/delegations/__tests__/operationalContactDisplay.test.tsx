import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { RegistrantInfoReadOnly, DelegationInfoReadOnly } from '../components/RequestInfoReadOnly';
import type { VisitProcessRequestSummary } from '../types/delegations.types';

/**
 * The operational contact must be readable on every detail screen, in full, and never confused with
 * the registrant.
 *
 * Three separate things went wrong here and each is pinned below.
 *
 * The block was hidden when `fullName` was blank — which is exactly the state of a contact who has
 * been invited and has not answered yet, so the reader lost the block at the only moment it told
 * them something. Fields were dropped, so a screen showed a name and a phone and left the campus
 * unable to tell whether the person could decide anything. And the contact sat among the
 * registrant's fields under a vague label, so the two people read as one.
 *
 * `DelegationInfoReadOnly` is the shared renderer behind VisitProcess and VisitProcessSummaryPage,
 * so pinning it here covers both.
 */

const FIVE_FIELDS = ['Họ và tên', 'Đơn vị công tác', 'Chức vụ', 'Số điện thoại', 'Email'] as const;
const CONTACT_HEADING = 'Đầu mối đoàn khách phối hợp tại cơ sở';
const EMPTY = 'Chưa có thông tin';

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

/** The contact heading and the fields that follow it, isolated from the registrant's identical labels. */
const contactBlock = () => screen.getByText(CONTACT_HEADING).parentElement as HTMLElement;

describe('operational contact on the visit detail screens', () => {
  it('shows all five fields under their own heading', () => {
    render(<DelegationInfoReadOnly summary={summary()} />);
    const block = within(contactBlock());

    for (const label of FIVE_FIELDS) expect(block.getByText(`${label}:`)).toBeInTheDocument();
    expect(block.getByText('Đầu Mối Cơ Sở')).toBeInTheDocument();
    expect(block.getByText('ĐH Đối Tác')).toBeInTheDocument();
    expect(block.getByText('Trưởng phòng Hợp tác Quốc tế')).toBeInTheDocument();
    expect(block.getByText('+84900000002')).toBeInTheDocument();
    expect(block.getByText('op@partner.vn')).toBeInTheDocument();
  });

  it('still renders every field while the contact has not answered the invitation yet', () => {
    // Nothing is known but the address the invitation went to. The block must not vanish: "invited,
    // no answer" is a state the reader has to be able to see.
    render(<DelegationInfoReadOnly summary={summary({
      operationalContactFullName: null,
      operationalContactOrganization: null,
      operationalContactJobTitle: null,
      operationalContactPhone: null,
      operationalContactEmail: 'invited@partner.vn',
    })} />);
    const block = within(contactBlock());

    for (const label of FIVE_FIELDS) expect(block.getByText(`${label}:`)).toBeInTheDocument();
    // A blank name does not take the known address down with it.
    expect(block.getByText('invited@partner.vn')).toBeInTheDocument();
    expect(block.getAllByText(EMPTY).length).toBe(4);
  });

  it('never falls back to the registrant to fill a missing contact field', () => {
    // A fallback would read as "the registrant is the contact", which is a different relation and
    // often a different person. A blank stays blank.
    render(<DelegationInfoReadOnly summary={summary({
      operationalContactJobTitle: null,
      operationalContactPhone: null,
    })} />);
    const block = within(contactBlock());

    expect(block.queryByText('Trưởng khoa')).toBeNull();       // registrant job title
    expect(block.queryByText('+84900000001')).toBeNull();      // registrant phone
    expect(block.getAllByText(EMPTY).length).toBe(2);
  });

  it('keeps the registrant and the contact in separate blocks with distinct labels', () => {
    render(
      <div>
        <RegistrantInfoReadOnly summary={summary()} />
        <DelegationInfoReadOnly summary={summary()} />
      </div>,
    );

    // The registrant block names the person as the registrant, not as a generic "contact".
    expect(screen.getByText('Họ và tên người đăng ký:')).toBeInTheDocument();
    expect(screen.getByText('Chức danh / phòng ban:')).toBeInTheDocument();
    // The contact block is separately headed, and holds the contact's values — not the registrant's.
    const block = within(contactBlock());
    expect(block.getByText('Đầu Mối Cơ Sở')).toBeInTheDocument();
    expect(block.queryByText('Người Đăng Ký')).toBeNull();
  });
});
