import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CampusVisitDetailCard } from '../components/v2/CampusVisitDetailCard';
import { campusFixture } from './fixtures';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';

// Only exercised by the profile-mismatch-icon describe block below (visitRequestId set): every other
// test in this file renders read-only (no visitRequestId), so ContactIdentityActions never mounts and
// never calls these. A full replacement, like ContactIdentityActions.test.tsx's own mock, since no test
// here opens the identity-change form and needs the real request/response shapes.
vi.mock('../api/visitRequestV2Api', () => ({
  getOperationalContactState: vi.fn(),
  resendOperationalContactConfirmation: vi.fn(),
  reinviteOperationalContactConfirmation: vi.fn(),
  replaceOperationalContact: vi.fn(),
  initiateOperationalContactTransfer: vi.fn(),
  cancelOperationalContactChange: vi.fn(),
  syncOwnAccountProfile: vi.fn(),
}));

import { getOperationalContactState } from '../api/visitRequestV2Api';

// jsdom's navigator.language is en-US → the i18n config initializes in EN; assertions
// below use the EN strings (plus structural roles/aria, which are language-free).

describe('CampusVisitDetailCard', () => {
  it('renders THIS campus snapshot: name, status, content, host, decision, revision', () => {
    render(<CampusVisitDetailCard campus={campusFixture()} />);

    expect(screen.getByText('FPTU Hà Nội')).toBeInTheDocument();
    // The host moved out of the decision grid into its own block; the decision grid keeps who
    // decided and when, which is a different question from who is running the campus.
    expect(screen.getByTestId(`reception-host-${'10'}-current-name`)).toBeInTheDocument();
    expect(screen.getByText('Đoàn ĐH ABC')).toBeInTheDocument();
    expect(screen.getByText('Trao đổi hợp tác')).toBeInTheDocument();
    expect(screen.getByText('Nội dung làm việc HN')).toBeInTheDocument();
    expect(screen.getByText('Host Hà Nội')).toBeInTheDocument();
    expect(screen.getByText(/Leader HN/)).toBeInTheDocument();
    // The fixture is ASSIGNED with a recorded decision → the content is in force and approved.
    expect(screen.getByText('Content version 2 is in force')).toBeInTheDocument();
    expect(screen.getByText('Approved at round 1')).toBeInTheDocument();
    // Masked-scope guarantee: the card shows ONLY what it was given (no sibling data).
    expect(screen.queryByText(/HCM/)).not.toBeInTheDocument();
  });

  it('does not claim approval on a campus nobody has decided yet', () => {
    // approvalRevision is 1 from the moment the request is created, so the old unconditional
    // "Approval v1" told a waiting visitor their campus had already approved.
    render(<CampusVisitDetailCard campus={campusFixture({
      instanceStatus: 'WAITING_REQUEST_APPROVAL', decidedAt: null, decidedByName: null,
      formRevision: 1, approvalRevision: 1,
    })} />);

    expect(screen.getByText('Approval status: not approved yet')).toBeInTheDocument();
    expect(screen.getByText('Current content: version 1')).toBeInTheDocument();
    expect(screen.queryByText(/Approved at round/)).not.toBeInTheDocument();
  });

  it('never renders a raw enum, even for a status the UI has not been taught', () => {
    render(<CampusVisitDetailCard campus={campusFixture({ instanceStatus: 'SOME_FUTURE_STATE' })} />);

    expect(screen.queryByText('SOME_FUTURE_STATE')).not.toBeInTheDocument();
    expect(screen.getByTestId('campus-status-10')).toHaveTextContent('Unknown');
  });

  it('shows the delegation up front, numbered, without a toggle to find it behind', () => {
    // The people list used to start collapsed. "Who is coming" is the reason this card gets
    // opened at all, so it is now on the page from the first render.
    render(<CampusVisitDetailCard campus={campusFixture()} />);

    // Both layouts are in the DOM (CSS picks one), so assertions are scoped to the table.
    const table = within(screen.getByTestId('campus-visitors-10')).getByRole('table');
    const cells = within(table).getAllByRole('cell').map(c => c.textContent);
    // The ordinal is derived from the row position, never stored.
    expect(cells.slice(0, 5)).toEqual(['1', 'Khách Một', 'GV', 'ĐH ABC', 'VN']);
    expect(within(screen.getByTestId('campus-visitors-10')).getByText('1 people')).toBeInTheDocument();
  });

  it('keeps every field for a person on the narrow layout too', () => {
    // The mobile cards are always rendered (CSS decides which layout is visible), so dropping a
    // field to shorten them would silently lose data on a phone.
    render(<CampusVisitDetailCard campus={campusFixture()} />);

    const visitors = screen.getByTestId('campus-visitors-10');
    const mobileCards = visitors.querySelector('ul');
    expect(mobileCards).not.toBeNull();
    expect(within(mobileCards as HTMLElement).getByText('Khách Một')).toBeInTheDocument();
    expect(within(mobileCards as HTMLElement).getByText('GV')).toBeInTheDocument();
    expect(within(mobileCards as HTMLElement).getByText('ĐH ABC')).toBeInTheDocument();
    expect(within(mobileCards as HTMLElement).getByText('VN')).toBeInTheDocument();
  });

  it('states an empty support list instead of hiding the section', () => {
    render(<CampusVisitDetailCard campus={campusFixture()} />);

    const support = screen.getByTestId('campus-support-10');
    expect(within(support).getByText('No accompanying support staff.')).toBeInTheDocument();
  });

  it('shows the pending-amendment badge only when an active amendment exists', () => {
    const { rerender } = render(<CampusVisitDetailCard campus={campusFixture()} />);
    expect(screen.queryByText(/Amendment #/)).not.toBeInTheDocument();

    rerender(
      <CampusVisitDetailCard
        campus={campusFixture({
          activeAmendment: { amendmentId: 9, amendmentNo: 2, status: 'PENDING', requestedAt: '2026-07-21T08:00:00', changedFieldCount: 3 },
        })}
      />,
    );
    expect(screen.getByText('Amendment #2 pending')).toBeInTheDocument();
  });

  it('renders visitTypeOther text when the type is OTHER', () => {
    render(
      <CampusVisitDetailCard campus={campusFixture({ visitType: 'OTHER', visitTypeOther: 'Thăm phòng lab' })} />,
    );
    expect(screen.getByText('Thăm phòng lab')).toBeInTheDocument();
  });
});

// ── One note, one consent answer ─────────────────────────────────────────────
/**
 * The consent row used to append the media note after an em dash — "Agreed — <note>" — which read
 * as a single fact. They were always two, and only one of them survived the business change: the
 * consent answer stands alone, and the guest's general note ("Ghi chú gửi FPTU") is its own row.
 */
describe('CampusVisitDetailCard — media consent and the general note are separate rows', () => {
  it('renders the guest note on its own, whatever the consent answer is', () => {
    render(<CampusVisitDetailCard campus={campusFixture({
      mediaConsentStatus: 'AGREED',
      notes: 'Đoàn có hai khách lớn tuổi, mong hỗ trợ xe điện.',
    })} />);

    expect(screen.getByText('Đoàn có hai khách lớn tuổi, mong hỗ trợ xe điện.')).toBeInTheDocument();
    // Not glued onto the consent answer, and no leftover media-note label.
    expect(screen.queryByText(/Agreed —/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Media note/i)).not.toBeInTheDocument();
  });

  it('shows a declined consent and a note together — the two do not gate each other', () => {
    render(<CampusVisitDetailCard campus={campusFixture({
      mediaConsentStatus: 'DECLINED',
      notes: 'Cần phiên dịch Anh - Việt buổi chiều.',
    })} />);

    expect(screen.getByText('Cần phiên dịch Anh - Việt buổi chiều.')).toBeInTheDocument();
  });
});

// ── Reception host and approval share one card ───────────────────────────────
/**
 * These two used to be two separate bordered blocks. They are now one card with a divider between
 * a "who is hosting" section and a "what was decided" section — see CampusVisitDetailCard.tsx.
 */
describe('CampusVisitDetailCard — reception host and approval info share one card', () => {
  it('renders both sections inside a single card, separated by one divider', () => {
    render(<CampusVisitDetailCard campus={campusFixture()} />);

    // getByTestId throws on more than one match, so this alone proves there is exactly one
    // container for the id — the old two-card layout had no single element that could hold both.
    const card = screen.getByTestId('campus-host-approval-10');

    expect(within(card).getByText('Reception Host')).toBeInTheDocument();
    expect(within(card).getByText('Approval information')).toBeInTheDocument();
    expect(card.querySelectorAll('hr')).toHaveLength(1);
  });

  it('renders the reception host fields inside the merged card', () => {
    render(<CampusVisitDetailCard campus={campusFixture()} />);
    const card = screen.getByTestId('campus-host-approval-10');

    expect(within(card).getByTestId('reception-host-10-current-name')).toHaveTextContent('Host Hà Nội');
    expect(within(card).getByTestId('reception-host-10-current-department')).toHaveTextContent('Phòng Hợp tác Quốc tế');
    expect(within(card).getByTestId('reception-host-10-current-phone')).toHaveTextContent('+84900000005');
    expect(within(card).getByTestId('reception-host-10-current-email')).toHaveTextContent('host.hn@fptu.edu.vn');
  });

  it('renders the approval fields inside the merged card', () => {
    render(<CampusVisitDetailCard campus={campusFixture()} />);
    const card = screen.getByTestId('campus-host-approval-10');

    expect(within(card).getByText('Leader HN')).toBeInTheDocument();
    expect(within(card).getByText(formatVietnamDateTime('2026-07-20T10:00:00'))).toBeInTheDocument();
    expect(within(card).getByText('OK')).toBeInTheDocument();
  });

  it('keeps the content-status headline and its qualifier as separate lines, not glued together', () => {
    render(<CampusVisitDetailCard campus={campusFixture()} />);

    const headline = screen.getByText('Content version 2 is in force');
    const revisionCell = headline.closest('dd');
    expect(revisionCell).not.toBeNull();
    // The qualifier is its own child element, so the two never concatenate into one run-on string.
    const qualifier = within(revisionCell as HTMLElement).getByText('Approved at round 1');
    expect(qualifier.tagName).toBe('SPAN');
  });

  it('keeps the host and approval sections in their own grids, so each can go 1-column on mobile', () => {
    render(<CampusVisitDetailCard campus={campusFixture()} />);
    const card = screen.getByTestId('campus-host-approval-10');

    const grids = card.querySelectorAll('dl.grid');
    expect(grids).toHaveLength(2);
    grids.forEach(grid => {
      expect(grid.className).toContain('grid-cols-1');
      expect(grid.className).toContain('sm:grid-cols-2');
    });
  });

  it('omits the divider when the campus has no host and no active proposal', () => {
    render(<CampusVisitDetailCard campus={campusFixture({ currentHost: null, proposedHost: null })} />);
    const card = screen.getByTestId('campus-host-approval-10');

    expect(card.querySelectorAll('hr')).toHaveLength(0);
    expect(within(card).getByText('Approval information')).toBeInTheDocument();
  });
});

// ── Profile-mismatch offer: a small icon in the contact card's title row, not a standing banner ─────
/**
 * `ContactIdentityActions` fetches the signed-in contact's `profileDifference` and reports it up
 * through `onProfileDifferenceChange`; this card is what turns that into the icon sitting right after
 * "Guest Delegation Coordination Contact at Campus" — the header row `OperationalContactReadOnly`
 * already reserves via `titleTrailing`. Clicking it opens the popover (question + diff + actions);
 * there is no third step.
 */
describe('CampusVisitDetailCard — profile-mismatch icon in the contact title row', () => {
  const CONTACT_TRANSFER_ACTIONS = ['VIEW', 'UPDATE_OPERATIONAL_CONTACT_PROFILE', 'INITIATE_OPERATIONAL_CONTACT_TRANSFER'];

  const stateWithDifference = {
    visitRequestId: 1, visitInstanceId: 10, campusStatus: 'ASSIGNED',
    contactConfirmed: true, confirmedEmailMasked: 'd***@x.vn', confirmedAt: '2026-08-01T09:00:00',
    confirmationSource: 'EMAIL_CONFIRMATION',
    pendingChangeKind: null, pendingChangeStatus: null, pendingEmailMasked: null,
    expiresAt: null, resendCount: 0, tokenVersion: 1,
    profileDifference: {
      fullNameDiffers: true, phoneDiffers: false,
      accountFullName: 'Nguyen Van A', accountPhone: null,
      snapshotFullName: 'Đầu Mối HN', snapshotPhone: null,
    },
  };

  it('shows the icon right after the contact title, and opens the popover on click', async () => {
    vi.mocked(getOperationalContactState).mockResolvedValue(stateWithDifference);

    render(
      <CampusVisitDetailCard
        campus={campusFixture({ allowedActions: CONTACT_TRANSFER_ACTIONS })}
        visitRequestId={1}
      />,
    );

    const trigger = await screen.findByTestId('profile-sync-trigger-10');
    // Same row as the title, immediately after it — not the far end of the card, not a standing block.
    const title = screen.getByText('Guest Delegation Coordination Contact at Campus');
    expect(title.parentElement?.contains(trigger)).toBe(true);
    // The profile/attention glyph, not the Info/help one it used to be.
    expect(trigger.querySelector('svg.lucide-badge-alert')).not.toBeNull();
    expect(trigger.querySelector('svg.lucide-info')).toBeNull();
    expect(screen.queryByTestId('profile-sync-popover-10')).not.toBeInTheDocument();

    await userEvent.click(trigger);

    expect(screen.getByTestId('profile-sync-popover-10')).toBeInTheDocument();
    expect(screen.getByTestId('profile-sync-fullname')).toHaveTextContent('Nguyen Van A');
    expect(screen.getByTestId('profile-sync-fullname')).toHaveTextContent('Đầu Mối HN');
  });

  it('renders no icon once there is nothing to reconcile', async () => {
    vi.mocked(getOperationalContactState).mockResolvedValue({ ...stateWithDifference, profileDifference: null });

    render(
      <CampusVisitDetailCard
        campus={campusFixture({ allowedActions: CONTACT_TRANSFER_ACTIONS })}
        visitRequestId={1}
      />,
    );

    await screen.findByText('Guest Delegation Coordination Contact at Campus');
    expect(screen.queryByTestId('profile-sync-trigger-10')).not.toBeInTheDocument();
  });
});
