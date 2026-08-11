/**
 * Which recipient an invitation preview is BOUND to.
 *
 * <b>What this pins.</b> An edited invitation is approved by a signed token, and the token carries the
 * scope the approval belongs to: `visitInstance:{id}|participant:{userId}`. The send recomputes that
 * string from the ids IT resolved and refuses a token that does not match — which is what stops a
 * message approved for one invitee being delivered, word for word, to another.
 *
 * IC_SUPPORT and STUDENT name their invitee in the payload, so the scope was easy to build and correct.
 * DEPT_SUPPORT does not: it names a DEPARTMENT, and the backend resolves that department's active
 * leader. Reading `payload.userId` therefore produced NO scope at all for it, while the send computed
 * one from the leader — so every edited or attachment-carrying department invitation was rejected with
 * "Bản xem trước thuộc về một email khác". The preview must be bound to the same leader the send will
 * resolve, which is exactly what these tests assert.
 *
 * The preview modal is stubbed: what it does with the body is its own suite's subject
 * (EmailPreviewModal.stages.test.tsx). What matters here is the scope this section hands it, and that
 * the content the modal approves reaches the invite call unchanged.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ParticipantInvitationSection } from '../components/ParticipantInvitationSection';
import { delegationsApi } from '../api/delegationsApi';
import type { EmailPreviewSendPayload } from '../components/EmailPreviewModal';

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

/**
 * Stub modal. It exposes the one control the section owns — "send what was approved" — and reports the
 * recipient it was bound to, so a preview opened for the wrong person is visible in the test.
 */
vi.mock('../components/EmailPreviewModal', () => ({
  EmailPreviewModal: ({ open, recipient, onSend }: any) =>
    open ? (
      <div data-testid="preview-modal">
        <span data-testid="preview-recipient">{recipient?.email ?? 'khong-co'}</span>
        <button type="button" onClick={() => onSend({} as EmailPreviewSendPayload)}>
          gui-nguyen-mau
        </button>
        <button
          type="button"
          onClick={() =>
            onSend({
              approvedContent: {
                finalPreviewToken: 'tok-final',
                subject: 'Nhờ phòng ban hỗ trợ',
                bodyHtml: '<p>Nội dung đã sửa</p>',
                attachments: [{ fileId: 77 }],
              },
            } as EmailPreviewSendPayload)
          }
        >
          gui-da-sua-kem-file
        </button>
      </div>
    ) : null,
}));

vi.mock('../components/SentEmailsModal', () => ({ SentEmailsModal: () => null }));

const VISIT_INSTANCE_ID = 4242;

const staffCandidate = {
  userId: 300, fullName: 'Trần Văn Staff', email: 'staff300@fpt.edu.vn', subRole: 'STAFF',
  departmentName: 'Phòng Hợp tác Quốc tế', campusName: 'FPT Đà Nẵng',
  conflictCount: 0, hasPrivateConflict: false, canInvite: true,
};
const studentCandidate = {
  userId: 400, fullName: 'Lê Thị Sinh Viên', email: 'sv400@fpt.edu.vn', subRole: null,
  studentCode: 'DN2026', campusName: 'FPT Đà Nẵng',
  conflictCount: 0, hasPrivateConflict: false, canInvite: true,
};
/** The department names no user; its leader (500) is who the backend will actually invite. */
const department = {
  departmentId: 20, departmentName: 'Phòng Hành chính', campusId: 1, campusName: 'FPT Đà Nẵng',
  leaderUserId: 500, leaderName: 'Phạm Thị Trưởng Phòng', leaderEmail: 'leader500@fpt.edu.vn',
  canInviteParticipant: true, participantDisabledReason: null,
  canReceiveLogistics: true, logisticsDisabledReason: null,
};

function renderSection() {
  return render(
    <ParticipantInvitationSection
      visitInstanceId={VISIT_INSTANCE_ID}
      relation="HOST"
      instanceStatus="BEFORE_VISIT"
      currentUserId={100}
      host={{ userId: 100, fullName: 'Nguyễn Văn Host', statusLabel: 'Đã được phân công' } as any}
      participants={[]}
      onChanged={vi.fn()}
      pushToast={vi.fn()}
      delegationName="Đoàn Đại học Kyoto"
      campusName="FPT Đà Nẵng"
      plannedStartAt="2026-08-12T09:00:00"
      plannedEndAt="2026-08-12T11:30:00"
    />,
  );
}

/** The scopeKey of the most recent preview request. */
const lastScopeKey = () => {
  const calls = vi.mocked(delegationsApi.previewEmailTemplate).mock.calls;
  return (calls[calls.length - 1][0] as any).scopeKey;
};

/** Opens the eye ("Xem trước & sửa email") button of the row the dropdown is showing. */
async function openBoundPreview(user: ReturnType<typeof userEvent.setup>, searchPlaceholder: RegExp) {
  await user.click(screen.getByPlaceholderText(searchPlaceholder));
  const previewButton = await screen.findByTitle('Xem trước & sửa email', {}, { timeout: 5000 });
  await user.click(previewButton);
  await screen.findByTestId('preview-modal');
}

describe('ParticipantInvitationSection — the scope an invitation preview is bound to', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(delegationsApi.getParticipantCandidates).mockImplementation(
      async (_id: any, type: any) => [type === 'STUDENT' ? studentCandidate : staffCandidate] as any);
    vi.mocked(delegationsApi.getSupportDepartments).mockResolvedValue([department] as any);
    vi.mocked(delegationsApi.previewEmailTemplate).mockResolvedValue({
      subject: 'Thư mời',
      editableBodyHtml: '<p>Noi dung</p>',
      initialFinalPreviewHtml: '<div>Ban day du</div>',
      isActionTemplate: true,
      runtimeEditable: true,
      previewToken: 'tok-prepare',
    } as any);
    vi.mocked(delegationsApi.inviteVisitParticipant).mockResolvedValue({
      emailStatus: 'SENT', emailRecipient: 'nguoi.nhan@fpt.edu.vn',
    } as any);
  });

  // ── TC-EMAIL-01 ────────────────────────────────────────────────────────────────────────────────
  it('IC_SUPPORT binds the preview to the invited user', async () => {
    const user = userEvent.setup();
    renderSection();

    await openBoundPreview(user, /Tìm theo tên\.\.\./);

    expect(lastScopeKey()).toBe(`visitInstance:${VISIT_INSTANCE_ID}|participant:300`);
    expect(screen.getByTestId('preview-recipient')).toHaveTextContent('staff300@fpt.edu.vn');
  });

  // ── TC-EMAIL-02 ────────────────────────────────────────────────────────────────────────────────
  it('STUDENT binds the preview to the invited student', async () => {
    const user = userEvent.setup();
    renderSection();

    await openBoundPreview(user, /Tìm theo tên \/ mã SV\.\.\./);

    expect(lastScopeKey()).toBe(`visitInstance:${VISIT_INSTANCE_ID}|participant:400`);
  });

  // ── TC-EMAIL-03 ────────────────────────────────────────────────────────────────────────────────
  it('DEPT_SUPPORT binds the preview to the LEADER the send will resolve, not the department', async () => {
    const user = userEvent.setup();
    renderSection();

    await openBoundPreview(user, /Tìm phòng ban\.\.\./);

    // The whole bug, in one assertion: the leader's id, not null and not the department's id.
    expect(lastScopeKey()).toBe(`visitInstance:${VISIT_INSTANCE_ID}|participant:500`);
    expect(lastScopeKey()).not.toBeNull();
    expect(lastScopeKey()).not.toContain('department:');
    expect(screen.getByTestId('preview-recipient')).toHaveTextContent('leader500@fpt.edu.vn');
  });

  // ── TC-EMAIL-04 ────────────────────────────────────────────────────────────────────────────────
  it('a DEPT_SUPPORT invitation edited and given an attachment sends that approved content', async () => {
    const user = userEvent.setup();
    renderSection();

    await openBoundPreview(user, /Tìm phòng ban\.\.\./);
    await user.click(screen.getByRole('button', { name: 'gui-da-sua-kem-file' }));

    await waitFor(() => expect(delegationsApi.inviteVisitParticipant).toHaveBeenCalledTimes(1));
    const [instanceId, payload] = vi.mocked(delegationsApi.inviteVisitParticipant).mock.calls[0] as any;
    expect(instanceId).toBe(VISIT_INSTANCE_ID);
    // The department is what the send is told; the leader is what it resolves. Both stay true here.
    expect(payload.participantType).toBe('DEPT_SUPPORT');
    expect(payload.departmentId).toBe(20);
    expect(payload.approvedContent.finalPreviewToken).toBe('tok-final');
    expect(payload.approvedContent.attachments).toEqual([{ fileId: 77 }]);
    // scopeParticipantUserId is a PREVIEW concern — sending it would invite a userId the backend
    // must derive itself, which is the property that makes the scope check meaningful.
    expect(payload).not.toHaveProperty('scopeParticipantUserId');
  });

  it('sending unchanged carries no approved content, so the backend renders the template', async () => {
    const user = userEvent.setup();
    renderSection();

    await openBoundPreview(user, /Tìm phòng ban\.\.\./);
    await user.click(screen.getByRole('button', { name: 'gui-nguyen-mau' }));

    await waitFor(() => expect(delegationsApi.inviteVisitParticipant).toHaveBeenCalledTimes(1));
    const payload = (vi.mocked(delegationsApi.inviteVisitParticipant).mock.calls[0] as any)[1];
    expect(payload.approvedContent).toBeUndefined();
  });

  // ── TC-EMAIL-05 (frontend half) ────────────────────────────────────────────────────────────────
  /**
   * The security check is the backend's, and it stands on the scope being SPECIFIC. A department with
   * no active leader has nobody to bind to, so no editable preview is offered — the alternative, an
   * approval scoped to nothing, is precisely the replayable token the check exists to prevent.
   */
  it('offers no bound preview for a department with no active leader', async () => {
    vi.mocked(delegationsApi.getSupportDepartments).mockResolvedValue([{
      ...department,
      leaderUserId: null, leaderName: null, leaderEmail: null,
      canInviteParticipant: false,
      participantDisabledReason: 'Phòng này chưa có trưởng phòng đang hoạt động.',
    }] as any);
    const user = userEvent.setup();
    renderSection();

    await user.click(screen.getByPlaceholderText(/Tìm phòng ban\.\.\./));
    await screen.findByText('Phòng này chưa có trưởng phòng đang hoạt động.');

    expect(screen.queryByTitle('Xem trước & sửa email')).toBeNull();
    expect(delegationsApi.previewEmailTemplate).not.toHaveBeenCalled();
  });

  /**
   * The panel-header "xem mẫu" link is not a message to anybody: it has no recipient, so it must get no
   * scope and no bindable token, which is what keeps a template preview from being usable as approval.
   */
  it('the unbound template preview carries no scope at all', async () => {
    const user = userEvent.setup();
    renderSection();

    await user.click(screen.getAllByRole('button', { name: /Xem trước email mời/ })[0]);

    await waitFor(() => expect(delegationsApi.previewEmailTemplate).toHaveBeenCalled());
    expect(lastScopeKey()).toBeNull();
  });
});
