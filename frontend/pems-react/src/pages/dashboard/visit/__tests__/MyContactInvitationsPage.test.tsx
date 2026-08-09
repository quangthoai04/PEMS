import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

vi.mock('../../../../features/visit-request/api/visitRequestV2Api', () => ({
  getMyOperationalContactInvitations: vi.fn(),
  acceptMyOperationalContactInvitation: vi.fn(),
  declineMyOperationalContactInvitation: vi.fn(),
}));

const showSuccessToast = vi.fn();
const showErrorToast = vi.fn();
const showMessageErrorToast = vi.fn();
vi.mock('../../../../shared/utils/toast', () => ({
  showSuccessToast: (...a: unknown[]) => showSuccessToast(...a),
  showErrorToast: (...a: unknown[]) => showErrorToast(...a),
  showMessageErrorToast: (...a: unknown[]) => showMessageErrorToast(...a),
}));

import { MyContactInvitationsPage } from '../MyContactInvitationsPage';
import {
  acceptMyOperationalContactInvitation,
  declineMyOperationalContactInvitation,
  getMyOperationalContactInvitations,
  type MyOperationalContactInvitation,
} from '../../../../features/visit-request/api/visitRequestV2Api';

const HOUR = 60 * 60 * 1000;

const invitation = (
  overrides: Partial<MyOperationalContactInvitation> = {},
): MyOperationalContactInvitation => ({
  identityChangeId: 501,
  visitRequestId: 300,
  visitInstanceId: 3006,
  kind: 'INITIAL_CONFIRMATION',
  requestCode: 'VR-2026-0300',
  campusName: 'FPTU Hà Nội',
  delegationName: 'Đoàn Đại học Kyoto',
  plannedStartAt: '2026-09-01T09:00:00',
  plannedEndAt: '2026-09-01T11:00:00',
  registrantFullName: 'Trần Thị B',
  registrantOrganization: 'Kyoto University',
  expiresAt: new Date(Date.now() + 48 * HOUR).toISOString(),
  ...overrides,
});

/** An axios-shaped rejection carrying a stable backend error code. */
const apiError = (errorCode: string) => ({
  response: { status: 409, data: { errorCode, message: 'server message' } },
});

const renderPage = () =>
  render(
    <MemoryRouter>
      <MyContactInvitationsPage />
    </MemoryRouter>,
  );

describe('MyContactInvitationsPage (V09 — Lời mời đầu mối của tôi)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows the empty state when the account has no invitations to answer', async () => {
    vi.mocked(getMyOperationalContactInvitations).mockResolvedValue([]);

    renderPage();

    expect(
      await screen.findByText('You have no operational contact invitations to answer right now.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Accept/ })).not.toBeInTheDocument();
  });

  it('offers Accept and Decline for a pending invitation, with the deciding facts', async () => {
    vi.mocked(getMyOperationalContactInvitations).mockResolvedValue([invitation()]);

    renderPage();

    expect(await screen.findByRole('button', { name: /Accept/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Decline/ })).toBeInTheDocument();
    expect(screen.getByText('Đoàn Đại học Kyoto')).toBeInTheDocument();
    expect(screen.getByText('VR-2026-0300')).toBeInTheDocument();
    expect(screen.getByText('FPTU Hà Nội')).toBeInTheDocument();
  });

  it('accepts once, refreshes, and drops the decision controls for that invitation', async () => {
    vi.mocked(getMyOperationalContactInvitations)
      .mockResolvedValueOnce([invitation()])
      // Answering settles the invitation, so the backend no longer lists it.
      .mockResolvedValueOnce([]);
    vi.mocked(acceptMyOperationalContactInvitation).mockResolvedValue({
      visitRequestId: 300, visitInstanceId: 3006, requestCode: 'VR-2026-0300',
      kind: 'INITIAL_CONFIRMATION', changeStatus: 'APPLIED', campusStatus: 'WAITING_REQUEST_APPROVAL',
      requestStatus: 'PENDING_APPROVAL', idempotent: false,
      message: 'Bạn đã xác nhận làm đầu mối vận hành tại FPTU Hà Nội.',
    });

    renderPage();
    fireEvent.click(await screen.findByRole('button', { name: /Accept/ }));

    await waitFor(() => expect(screen.getByText('Accepted')).toBeInTheDocument());
    expect(acceptMyOperationalContactInvitation).toHaveBeenCalledTimes(1);
    expect(acceptMyOperationalContactInvitation).toHaveBeenCalledWith(501);
    // Accepting is what creates the relation, so the list is re-read rather than guessed at.
    expect(getMyOperationalContactInvitations).toHaveBeenCalledTimes(2);
    expect(screen.queryByRole('button', { name: /Accept/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Decline/ })).not.toBeInTheDocument();
  });

  it('does not post twice when Accept is clicked repeatedly', async () => {
    vi.mocked(getMyOperationalContactInvitations).mockResolvedValue([invitation()]);
    let resolveAccept: (v: unknown) => void = () => {};
    vi.mocked(acceptMyOperationalContactInvitation).mockImplementation(
      () => new Promise(resolve => { resolveAccept = resolve; }) as never,
    );

    renderPage();
    const accept = await screen.findByRole('button', { name: /Accept/ });
    fireEvent.click(accept);
    fireEvent.click(accept);
    fireEvent.click(accept);

    await waitFor(() => expect(accept).toBeDisabled());
    expect(acceptMyOperationalContactInvitation).toHaveBeenCalledTimes(1);
    resolveAccept({
      visitRequestId: 300, visitInstanceId: 3006, requestCode: 'VR-2026-0300',
      kind: 'INITIAL_CONFIRMATION', changeStatus: 'APPLIED', campusStatus: 'WAITING_REQUEST_APPROVAL',
      requestStatus: 'PENDING_APPROVAL', idempotent: false, message: 'ok',
    });
  });

  it('declines with the typed reason and then shows the declined outcome', async () => {
    vi.mocked(getMyOperationalContactInvitations)
      .mockResolvedValueOnce([invitation()])
      .mockResolvedValueOnce([]);
    vi.mocked(declineMyOperationalContactInvitation).mockResolvedValue({
      visitRequestId: 300, visitInstanceId: 3006, requestCode: 'VR-2026-0300',
      kind: 'INITIAL_CONFIRMATION', changeStatus: 'DECLINED', campusStatus: 'WAITING_CONTACT_CONFIRMATION',
      requestStatus: 'PENDING_CONTACT_CONFIRMATION', idempotent: false,
      message: 'Bạn đã từ chối lời mời.',
    });

    renderPage();
    fireEvent.click(await screen.findByRole('button', { name: /Decline/ }));
    fireEvent.change(screen.getByLabelText('Reason for declining (optional)'), {
      target: { value: 'Tôi không phụ trách đoàn này' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Confirm decline' }));

    await waitFor(() => expect(screen.getByText('Declined')).toBeInTheDocument());
    expect(declineMyOperationalContactInvitation).toHaveBeenCalledTimes(1);
    expect(declineMyOperationalContactInvitation).toHaveBeenCalledWith(
      501, 'Tôi không phụ trách đoàn này',
    );
    expect(screen.queryByRole('button', { name: /^Accept/ })).not.toBeInTheDocument();
  });

  it('offers no decision for an invitation whose validity has run out', async () => {
    vi.mocked(getMyOperationalContactInvitations).mockResolvedValue([
      invitation({ expiresAt: new Date(Date.now() - HOUR).toISOString() }),
    ]);

    renderPage();

    expect(
      await screen.findByText('The invitation has expired. Send a new one.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Accept/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Decline/ })).not.toBeInTheDocument();
  });

  it('maps a stable backend code to its own sentence and re-reads the settled row', async () => {
    vi.mocked(getMyOperationalContactInvitations)
      .mockResolvedValueOnce([invitation()])
      .mockResolvedValueOnce([]);
    vi.mocked(acceptMyOperationalContactInvitation).mockRejectedValue(
      apiError('OPERATIONAL_CONTACT_CONFIRMATION_EXPIRED'),
    );

    renderPage();
    fireEvent.click(await screen.findByRole('button', { name: /Accept/ }));

    await waitFor(() =>
      expect(showMessageErrorToast).toHaveBeenCalledWith('The invitation has expired. Send a new one.'),
    );
    // Stale row → re-read, so it stops offering an answer nobody can give any more.
    await waitFor(() => expect(getMyOperationalContactInvitations).toHaveBeenCalledTimes(2));
    expect(showErrorToast).not.toHaveBeenCalled();
  });

  it('maps an email mismatch to the "sign in as the invited address" sentence', async () => {
    vi.mocked(getMyOperationalContactInvitations).mockResolvedValue([invitation()]);
    vi.mocked(acceptMyOperationalContactInvitation).mockRejectedValue(
      apiError('OPERATIONAL_CONTACT_EMAIL_MISMATCH'),
    );

    renderPage();
    fireEvent.click(await screen.findByRole('button', { name: /Accept/ }));

    await waitFor(() =>
      expect(showMessageErrorToast).toHaveBeenCalledWith(
        'The signed-in account is not the invited address. Please sign in with the account for the invited email.',
      ),
    );
  });

  it('never links a pending invitee to the full request detail', async () => {
    vi.mocked(getMyOperationalContactInvitations).mockResolvedValue([invitation()]);

    const { container } = renderPage();
    await screen.findByRole('button', { name: /Accept/ });

    // A pending invitee holds no relation the system has granted; VisitFormReadService refuses them
    // the request, so this screen must not offer a door that answers 403.
    expect(container.innerHTML).not.toContain('/dashboard/visit/v2/');
    expect(container.querySelector('a')).toBeNull();
  });
});
