import { render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('../api/visitRequestV2Api', () => ({
  getActiveAmendment: vi.fn(),
  approveAmendment: vi.fn(),
  rejectAmendment: vi.fn(),
  withdrawAmendment: vi.fn(),
}));

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string, opts?: Record<string, unknown>) => (opts ? `${key}:${JSON.stringify(opts)}` : key), i18n: { language: 'en' } }),
}));

const showSuccessToast = vi.fn();
const showErrorToast = vi.fn();
vi.mock('../../../shared/utils/toast', () => ({
  showSuccessToast: (...args: unknown[]) => showSuccessToast(...args),
  showErrorToast: (...args: unknown[]) => showErrorToast(...args),
}));

import VisitAmendmentPanel from '../components/VisitAmendmentPanel';
import { getActiveAmendment, type AmendmentDto } from '../api/visitRequestV2Api';

/**
 * Amendment review — no internal identity ever reaches the DOM (plan CanhIter3FixBug FIX-K).
 *
 * Before this fix, a member-list change row was rendered with `Object.values(dto)`, which prints
 * EVERY property of the backend's VisitorDto/SupportTeamMemberDto — including `organizationPartnerId`
 * (an internal numeric id) and `clientMemberKey` (a per-submission UUID). The operational-contact
 * member-key change row rendered its raw UUID value directly. Neither may ever be visible to a reviewer.
 */
describe('VisitAmendmentPanel', () => {
  const uuid = '5f9e366b-8679-4399-aed3-e544d665f67e';
  const partnerId = 987654;

  const amendment: AmendmentDto = {
    amendmentId: 1,
    visitRequestId: 10,
    visitInstanceId: 31,
    amendmentNo: 1,
    status: 'PENDING_APPROVAL',
    baseFormRevision: 1,
    baseApprovalRevision: 1,
    requestedBy: 8,
    requestedByName: 'Người đăng ký',
    requestedAt: '2026-08-01T09:00:00',
    reason: 'Cập nhật đoàn',
    decidedBy: null,
    decidedByName: null,
    decidedAt: null,
    decisionNote: null,
    expiresAt: null,
    changes: [
      {
        fieldPath: 'instance.members.visitors',
        changeClass: 'APPROVAL_SENSITIVE',
        oldValueJson: JSON.stringify([
          { fullName: 'Khách Cũ', nationality: 'VN', jobTitle: 'GV', organization: 'Org A', organizationPartnerId: null, clientMemberKey: null },
        ]),
        newValueJson: JSON.stringify([
          { fullName: 'Khách Mới', nationality: 'VN', jobTitle: 'Trưởng đoàn', organization: 'Org B', organizationPartnerId: partnerId, clientMemberKey: uuid },
        ]),
      },
      {
        fieldPath: 'instance.operationalContact.clientMemberKey',
        changeClass: 'APPROVAL_SENSITIVE',
        oldValueJson: null,
        newValueJson: JSON.stringify(uuid),
      },
      {
        fieldPath: 'instance.purpose',
        changeClass: 'APPROVAL_SENSITIVE',
        oldValueJson: JSON.stringify('Mục đích cũ'),
        newValueJson: JSON.stringify('Mục đích mới'),
      },
    ],
  };

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getActiveAmendment).mockResolvedValue(amendment);
  });

  it('FIX-K: never renders the raw partner id or client member UUID for a member-list change', async () => {
    const { container } = render(
      <VisitAmendmentPanel visitRequestId={10} visitInstanceId={31} canDecide={false} canWithdraw={false} />,
    );

    // "Khách Mới" legitimately appears twice once the contact-key row resolves to the same member —
    // see the next test — so this must tolerate multiple matches, not assume exactly one.
    await waitFor(() => expect(screen.getAllByText(/Khách Mới/).length).toBeGreaterThan(0));

    const text = container.textContent ?? '';
    expect(text).not.toContain(String(partnerId));
    expect(text).not.toContain(uuid);
    expect(text).not.toContain('organizationPartnerId');
    expect(text).not.toContain('clientMemberKey');
    // The allow-listed business fields ARE still shown.
    expect(text).toContain('Khách Mới');
    expect(text).toContain('Trưởng đoàn');
    expect(text).toContain('Org B');
  });

  it('FIX-K: resolves the operational-contact member-key row to the member it names, not the UUID', async () => {
    render(<VisitAmendmentPanel visitRequestId={10} visitInstanceId={31} canDecide={false} canWithdraw={false} />);

    // The key resolves to the SAME member named in the Visitors change row above it (both come from
    // this amendment's own proposed values), rather than the bare key or a guess.
    await waitFor(() => expect(screen.getAllByText(/Khách Mới/).length).toBeGreaterThanOrEqual(2));
  });

  it('FIX-K: an unknown fieldPath never renders the raw internal path string', async () => {
    vi.mocked(getActiveAmendment).mockResolvedValue({
      ...amendment,
      changes: [
        {
          fieldPath: 'instance.someBrandNewFieldNotYetMapped',
          changeClass: 'APPROVAL_SENSITIVE',
          oldValueJson: JSON.stringify('a'),
          newValueJson: JSON.stringify('b'),
        },
      ],
    });

    const { container } = render(
      <VisitAmendmentPanel visitRequestId={10} visitInstanceId={31} canDecide={false} canWithdraw={false} />,
    );

    await waitFor(() => expect(container.textContent).not.toBe(''));
    expect(container.textContent ?? '').not.toContain('instance.someBrandNewFieldNotYetMapped');
  });

  // Plan CanhIter3FixBug "Đầu mối hiện tại có nằm trong danh sách đoàn không?" — a relationship-only
  // change (member list untouched) resolves its PERSISTENT guestMemberId old/new values to names from
  // the campus's current roster, and never renders the raw numeric id.
  it('resolves a relationship-only guestMemberId change to names, never the raw id', async () => {
    vi.mocked(getActiveAmendment).mockResolvedValue({
      ...amendment,
      changes: [
        {
          fieldPath: 'instance.operationalContact.guestMemberId',
          changeClass: 'APPROVAL_SENSITIVE',
          oldValueJson: null, // was "not in the delegation"
          newValueJson: JSON.stringify(555),
        },
      ],
    });

    const { container } = render(
      <VisitAmendmentPanel
        visitRequestId={10}
        visitInstanceId={31}
        canDecide={false}
        canWithdraw={false}
        members={[
          { guestMemberId: 555, memberType: 'GUEST', fullName: 'Trần Văn C', organization: 'Org C', jobTitle: 'GV', nationality: 'VN', displayOrder: 1 },
        ]}
      />,
    );

    await waitFor(() => expect(screen.getByText(/Trần Văn C/)).toBeInTheDocument());
    expect(container.textContent ?? '').not.toContain('555');
  });

  it('falls back to a generic label when the roster cannot explain a guestMemberId (never a guess)', async () => {
    vi.mocked(getActiveAmendment).mockResolvedValue({
      ...amendment,
      changes: [
        {
          fieldPath: 'instance.operationalContact.guestMemberId',
          changeClass: 'APPROVAL_SENSITIVE',
          oldValueJson: JSON.stringify(111),
          newValueJson: JSON.stringify(222),
        },
      ],
    });

    const { container } = render(
      <VisitAmendmentPanel visitRequestId={10} visitInstanceId={31} canDecide={false} canWithdraw={false} members={[]} />,
    );

    await waitFor(() => expect(container.textContent).not.toBe(''));
    expect(container.textContent ?? '').not.toContain('111');
    expect(container.textContent ?? '').not.toContain('222');
  });
});
