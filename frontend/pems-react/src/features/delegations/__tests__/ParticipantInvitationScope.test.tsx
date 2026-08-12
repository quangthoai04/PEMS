import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';

/**
 * The scope a preview token is bound to must name the person the SEND will actually reach.
 *
 * For a department invitation the payload carries a departmentId, not a userId — the backend turns it
 * into that department's active leader and builds the scope from THAT id. The screen used to read
 * `payload.userId`, which a department payload does not have, so the preview was requested with no
 * scope at all and the token it returned no longer matched the one the send recomputed. Every
 * department invitation died on "Bản xem trước thuộc về một email khác", including the very first one.
 *
 * These tests pin the scope for all three invitee kinds. They do not weaken the check: the token is
 * still bound to a recipient and still verified server-side — it is now bound to the RIGHT recipient.
 */

vi.mock('../api/delegationsApi', () => ({
  delegationsApi: {
    getParticipantCandidates: vi.fn(),
    getSupportDepartments: vi.fn(),
    previewEmailTemplate: vi.fn(),
    inviteVisitParticipant: vi.fn(),
    removeVisitParticipant: vi.fn(),
    getParticipantSentEmails: vi.fn(),
  },
}));
vi.mock('../components/EmailPreviewModal', () => ({ EmailPreviewModal: () => null }));
vi.mock('../components/SentEmailsModal', () => ({ SentEmailsModal: () => null }));

import { delegationsApi } from '../api/delegationsApi';
import { ParticipantInvitationSection } from '../components/ParticipantInvitationSection';

const api = delegationsApi as unknown as Record<string, ReturnType<typeof vi.fn>>;

const VISIT_INSTANCE_ID = 5;

const department = (over: Record<string, unknown> = {}) => ({
  departmentId: 42,
  departmentName: 'Phòng Hành chính',
  campusId: 1,
  campusName: 'Hòa Lạc',
  leaderUserId: 77,
  leaderName: 'Phạm Thị Trưởng Phòng',
  leaderEmail: 'leader@fpt.edu.vn',
  canInviteParticipant: true,
  participantDisabledReason: null,
  canReceiveLogistics: true,
  logisticsDisabledReason: null,
  ...over,
});

const candidate = (over: Record<string, unknown> = {}) => ({
  userId: 9,
  fullName: 'Trần Văn Support',
  email: 'support@fpt.edu.vn',
  departmentName: 'Phòng Hợp tác Quốc tế',
  campusName: 'Hòa Lạc',
  subRole: 'STAFF',
  studentCode: null,
  conflictCount: 0,
  hasPrivateConflict: false,
  canInvite: true,
  ...over,
});

function renderSection() {
  return render(
    <ParticipantInvitationSection
      visitInstanceId={VISIT_INSTANCE_ID}
      relation="HOST"
      instanceStatus="BEFORE_VISIT"
      currentUserId={100}
      host={{ userId: 100, fullName: 'Nguyễn Văn Host', departmentName: 'IC', statusLabel: 'Đã được phân công' } as never}
      participants={[]}
      onChanged={() => {}}
      pushToast={() => {}}
      delegationName="Đoàn ABC"
      campusName="Hòa Lạc"
      plannedStartAt="2026-08-01T09:00"
      plannedEndAt="2026-08-01T11:00"
    />,
  );
}

/** Opens one of the search dropdowns and waits for its first row. */
async function openDropdown(placeholder: string, rowText: string | RegExp) {
  fireEvent.focus(screen.getByPlaceholderText(placeholder));
  await waitFor(() => expect(screen.getByText(rowText)).toBeInTheDocument());
}

const scopeOfLastPreview = () => api.previewEmailTemplate.mock.calls.at(-1)?.[0].scopeKey;

describe('invitation preview scope', () => {
  beforeEach(() => {
    Object.values(api).forEach((fn) => fn.mockReset());
    api.getParticipantCandidates.mockResolvedValue([]);
    api.getSupportDepartments.mockResolvedValue([]);
    api.previewEmailTemplate.mockResolvedValue({
      subject: 's', editableBodyHtml: '<p>b</p>', initialFinalPreviewHtml: '<p>b</p>',
      isActionTemplate: true, runtimeEditable: true, previewToken: 'tok',
    });
  });

  it('binds a department preview to the LEADER, not to an empty scope', async () => {
    api.getSupportDepartments.mockResolvedValue([department()]);
    renderSection();

    await openDropdown('Tìm phòng ban...', 'Phòng Hành chính');
    fireEvent.click(screen.getByTitle('Xem trước & sửa email'));

    await waitFor(() => expect(api.previewEmailTemplate).toHaveBeenCalled());
    // The id the backend resolves the departmentId into — the scope the send recomputes.
    expect(scopeOfLastPreview()).toBe('visitInstance:5|participant:77');
  });

  it('binds an IC support preview to the invitee', async () => {
    api.getParticipantCandidates.mockImplementation((_id: number, type: string) =>
      Promise.resolve(type === 'IC_SUPPORT' ? [candidate()] : []));
    renderSection();

    await openDropdown('Tìm theo tên...', 'Trần Văn Support');
    fireEvent.click(screen.getByTitle('Xem trước & sửa email'));

    await waitFor(() => expect(api.previewEmailTemplate).toHaveBeenCalled());
    expect(scopeOfLastPreview()).toBe('visitInstance:5|participant:9');
  });

  it('binds a student preview to the invitee', async () => {
    api.getParticipantCandidates.mockImplementation((_id: number, type: string) =>
      Promise.resolve(type === 'STUDENT' ? [candidate({ userId: 11, fullName: 'Lê Sinh Viên' })] : []));
    renderSection();

    await openDropdown('Tìm theo tên / mã SV...', 'Lê Sinh Viên');
    fireEvent.click(screen.getByTitle('Xem trước & sửa email'));

    await waitFor(() => expect(api.previewEmailTemplate).toHaveBeenCalled());
    expect(scopeOfLastPreview()).toBe('visitInstance:5|participant:11');
  });

  it('leaves the panel-header sample preview unbound — no recipient, no token to reuse', async () => {
    renderSection();

    fireEvent.click(screen.getAllByText('Xem trước email mời')[0]);

    await waitFor(() => expect(api.previewEmailTemplate).toHaveBeenCalled());
    expect(scopeOfLastPreview()).toBeNull();
    expect(api.previewEmailTemplate.mock.calls.at(-1)?.[0].visitInstanceId).toBeNull();
  });

  it('offers no preview or invite for a department whose leader has no address', async () => {
    api.getSupportDepartments.mockResolvedValue([department({ leaderEmail: '  ' })]);
    renderSection();

    await openDropdown('Tìm phòng ban...', 'Phòng Hành chính');

    expect(screen.queryByTitle('Xem trước & sửa email')).toBeNull();
    expect(screen.getByRole('button', { name: /Mời/ })).toBeDisabled();
    expect(screen.getByText(/chưa có email/i)).toBeInTheDocument();
  });

  it('offers no preview for a department with no resolvable leader', async () => {
    api.getSupportDepartments.mockResolvedValue([department({
      leaderUserId: null, leaderName: null, leaderEmail: null,
      canInviteParticipant: false,
      participantDisabledReason: 'Phòng này chưa có trưởng phòng đang hoạt động.',
    })]);
    renderSection();

    await openDropdown('Tìm phòng ban...', 'Phòng Hành chính');

    expect(screen.queryByTitle('Xem trước & sửa email')).toBeNull();
    expect(api.previewEmailTemplate).not.toHaveBeenCalled();
  });
});
