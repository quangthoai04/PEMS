/**
 * What the composer does when the draft it was opened on cannot be loaded.
 *
 * The incident: "Gửi cập nhật chuẩn bị" handed the composer a draftId, the composer's GET answered
 * 404 "EmailDraft ({id}) was not found.", and the composer stayed open — showing the generated body
 * the caller had passed as `initialBodyHtml`, which is indistinguishable from a draft that loaded.
 * From there the Host could edit, preview, and press send; `handleSend` found no draft id and took
 * its "no draft yet" branch, CREATING a fresh draft and sending it. A dead id therefore produced a
 * real email that no draft in the database had ever backed, and nothing on screen had said anything
 * was wrong.
 *
 * So these tests are not about an error message. They are about what is NOT on the screen: no form,
 * no send, and no write of any kind that the Host did not explicitly ask for.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';

const createDraft = vi.fn();
const updateDraft = vi.fn();
const sendDraft = vi.fn();
const getDraft = vi.fn();
const discardDraft = vi.fn();
const getRecipientLimits = vi.fn();
const getEmailTemplateList = vi.fn();

vi.mock('../api/emailDraftsApi', () => ({
  emailDraftsApi: {
    createDraft: (...a: unknown[]) => createDraft(...a),
    updateDraft: (...a: unknown[]) => updateDraft(...a),
    sendDraft: (...a: unknown[]) => sendDraft(...a),
    getDraft: (...a: unknown[]) => getDraft(...a),
    discardDraft: (...a: unknown[]) => discardDraft(...a),
  },
}));

vi.mock('../api/emailsApi', () => ({
  emailsApi: {
    getRecipientLimits: (...a: unknown[]) => getRecipientLimits(...a),
    getEmailTemplateList: (...a: unknown[]) => getEmailTemplateList(...a),
  },
}));

vi.mock('../../../shared/api/filesApi', () => ({ filesApi: { upload: vi.fn(), download: vi.fn() } }));
vi.mock('../../../shared/auth/authStorage', () => ({ authStorage: { getToken: () => 'test-token' } }));
vi.mock('../../../components/modals/ConfirmModal', () => ({ ConfirmModal: () => null }));

vi.mock('react-quill-new', () => ({
  default: ({ value, onChange }: { value: string; onChange: (v: string) => void }) => (
    <textarea aria-label="body" value={value} onChange={e => onChange(e.target.value)} />
  ),
}));
vi.mock('react-quill-new/dist/quill.snow.css', () => ({}));

import { EmailComposeModal } from '../components/EmailComposeModal';
import i18n from '../../../shared/i18n/config';

/** The generated body the setup-progress caller passes in — the text that used to fake a loaded draft. */
const GENERATED_BODY = '<p>Kính gửi quý đoàn, đính kèm là Báo cáo Lịch trình.</p>';

const httpError = (status: number, errorCode: string, message: string) =>
  Object.assign(new Error(message), {
    isAxiosError: true,
    response: { status, data: { success: false, errorCode, message } },
  });

const baseProps = {
  open: true as const,
  onClose: vi.fn(),
  pushToast: vi.fn(),
  initialDraftId: 77,
  initialBodyHtml: GENERATED_BODY,
  lockedTemplate: true,
  contextTitle: 'Gửi cập nhật chuẩn bị',
};

const renderComposer = (extra: Record<string, unknown> = {}) =>
  render(<EmailComposeModal {...baseProps} {...extra} />);

const failureShown = async () =>
  await waitFor(() => expect(screen.getByTestId('draft-load-failure')).toBeTruthy());

beforeEach(() => {
  void i18n.changeLanguage('vi');
  vi.clearAllMocks();
  getRecipientLimits.mockResolvedValue({ data: { maxRecipients: 50 } });
  getEmailTemplateList.mockResolvedValue({ data: { items: [] } });
});

describe('composer opened on a draft that does not exist', () => {
  beforeEach(() => {
    getDraft.mockRejectedValue(
      httpError(404, 'EMAIL_DRAFT_NOT_FOUND', 'EmailDraft (77) was not found.'),
    );
  });

  it('replaces the form with a failure screen instead of showing a composer', async () => {
    renderComposer();
    await failureShown();

    // The controls that could send are absent, not merely disabled — there is no state in which
    // pressing them would be correct.
    expect(screen.queryByLabelText('body')).toBeNull();
    expect(screen.queryByText('Gửi email')).toBeNull();
    expect(screen.queryByText('Xem trước')).toBeNull();
  });

  it('does not present the generated body as if the draft had loaded', async () => {
    renderComposer();
    await failureShown();

    expect(screen.queryByText(/Báo cáo Lịch trình/)).toBeNull();
  });

  it('says plainly that nothing on the server was changed', async () => {
    renderComposer();
    await failureShown();

    expect(screen.getByText(/Dữ liệu trên hệ thống không bị thay đổi/)).toBeTruthy();
  });

  /**
   * The heart of it: a failed load must not write. Autosave is debounced, so the assertion is made
   * after enough real time for a debounce to have fired.
   */
  it('never creates or updates a draft on its own', async () => {
    renderComposer();
    await failureShown();
    await new Promise(resolve => setTimeout(resolve, 1500));

    expect(createDraft).not.toHaveBeenCalled();
    expect(updateDraft).not.toHaveBeenCalled();
    expect(sendDraft).not.toHaveBeenCalled();
  });

  it('offers "Tạo bản nháp mới" only when the caller can rebuild one', async () => {
    const { unmount } = renderComposer();
    await failureShown();
    expect(screen.queryByTestId('draft-load-failure-recreate')).toBeNull();
    unmount();

    renderComposer({ onRecreateDraft: vi.fn() });
    await failureShown();
    expect(screen.getByTestId('draft-load-failure-recreate')).toBeTruthy();
  });

  /**
   * The rebuild goes through the caller's prepare endpoint, not through this component's
   * create-a-draft path — which is what keeps the flow's own rules (host, stage, language, the
   * mandatory report) applying to the replacement.
   */
  it('rebuilds through the caller rather than creating a draft itself', async () => {
    const onRecreateDraft = vi.fn().mockResolvedValue(undefined);
    renderComposer({ onRecreateDraft });
    await failureShown();

    screen.getByTestId('draft-load-failure-recreate').click();

    await waitFor(() => expect(onRecreateDraft).toHaveBeenCalledTimes(1));
    expect(createDraft).not.toHaveBeenCalled();
  });
});

describe('composer opened on a draft that is no longer editable', () => {
  /**
   * A draft sent from another tab is not missing. Offering "Tạo bản nháp mới" here would invite the
   * Host to send the same message a second time — the message they are being told already went.
   */
  it('does not offer to recreate a draft that has already been sent', async () => {
    getDraft.mockRejectedValue(httpError(
      409, 'EMAIL_DRAFT_NOT_EDITABLE',
      'Email nháp này đã được gửi. Nội dung đã gửi xem được trong lịch sử email.',
    ));

    renderComposer({ onRecreateDraft: vi.fn() });
    await failureShown();

    expect(screen.getByText(/Email nháp này không còn soạn được/)).toBeTruthy();
    expect(screen.queryByTestId('draft-load-failure-recreate')).toBeNull();
  });

  /** Somebody else's draft is likewise not a thing to replace. */
  it('does not offer to recreate somebody else\'s draft', async () => {
    getDraft.mockRejectedValue(httpError(
      403, 'FORBIDDEN', 'Bạn chỉ được xem email nháp do chính mình tạo.',
    ));

    renderComposer({ onRecreateDraft: vi.fn() });
    await failureShown();

    expect(screen.getByText(/Bạn không có quyền mở email nháp này/)).toBeTruthy();
    expect(screen.queryByTestId('draft-load-failure-recreate')).toBeNull();
  });
});

describe('composer opened without a draft id', () => {
  /**
   * The guard that protects every screen that is not the setup-progress flow: a plain "soạn email"
   * composer supplies no draft id, must not fetch one, and must render the form as it always has.
   */
  it('renders the normal form and fetches nothing', async () => {
    render(<EmailComposeModal open onClose={vi.fn()} pushToast={vi.fn()} />);

    await waitFor(() => expect(screen.getByLabelText('body')).toBeTruthy());
    expect(getDraft).not.toHaveBeenCalled();
    expect(screen.queryByTestId('draft-load-failure')).toBeNull();
  });
});
