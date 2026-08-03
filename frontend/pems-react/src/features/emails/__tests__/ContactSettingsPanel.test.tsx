/**
 * Card 4, "Cấu hình thông tin liên hệ".
 *
 * The reported defect: the card showed "Không tìm thấy dữ liệu cần xử lý." and nothing else. That is
 * the toast helper's generic HTTP-404 sentence, reached whenever a response carries no error code —
 * and the failure it was actually describing was a running API built before this endpoint existed,
 * where no data is missing at all. Three different repairs (restart the API, run the policy patch,
 * align the catalog) arrived as one sentence that named none of them.
 *
 * Each test below asserts the card says which one.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

const getEmailContactSettings = vi.fn();
const updateEmailContactSettings = vi.fn();
const previewEmailContactBlock = vi.fn();
const restoreEmailContactSettingsDefault = vi.fn();

vi.mock('../api/emailsApi', () => ({
  emailsApi: {
    getEmailContactSettings: (...a: unknown[]) => getEmailContactSettings(...a),
    updateEmailContactSettings: (...a: unknown[]) => updateEmailContactSettings(...a),
    previewEmailContactBlock: (...a: unknown[]) => previewEmailContactBlock(...a),
    restoreEmailContactSettingsDefault: (...a: unknown[]) => restoreEmailContactSettingsDefault(...a),
  },
}));

import { ContactSettingsPanel } from '../components/ContactSettingsPanel';

/** A fully configured template: every field is the template's own. */
const CONFIGURED = {
  templateCode: 'VISIT_PARTICIPANT_INVITATION',
  requirement: 'REQUIRED',
  contactSource: 'HOST',
  showEmail: true,
  showPhone: true,
  showDepartment: false,
  showCampus: true,
  showSender: false,
  headingVi: 'Thông tin liên hệ',
  headingEn: 'Contact information',
  replyToSource: 'CONTACT',
  blockPlaceholder: '{{contactInformationBlock}}',
  bodyCarriesBlockVi: true,
  bodyCarriesBlockEn: true,
  hasOwnPolicyRow: true,
  requirementSource: 'TEMPLATE',
  contactSourceSource: 'TEMPLATE',
  showEmailSource: 'TEMPLATE',
  showPhoneSource: 'TEMPLATE',
  showDepartmentSource: 'TEMPLATE',
  showCampusSource: 'TEMPLATE',
  showSenderSource: 'TEMPLATE',
  replyToSourceSource: 'TEMPLATE',
  headingSource: 'TEMPLATE',
  availableRequirements: ['NONE', 'OPTIONAL', 'REQUIRED'],
  availableSources: ['HOST', 'SENDER', 'HOST_THEN_SENDER', 'CAMPUS_DEFAULT', 'DEPARTMENT_DEFAULT', 'SUPPORT_CONTACT'],
  availableReplyToSources: ['NONE', 'CONTACT', 'SENDER'],
  capability: 'SUPPORTED',
  editable: true,
  capabilityReasonCode: 'OPERATOR_CHOICE',
  capabilityReasonVi: 'Mẫu này có thể kèm khối thông tin liên hệ; mức hiển thị do người quản trị chọn.',
};

/**
 * A template that can never carry the block: the message IS a one-time credential.
 *
 * The backend answers with an empty `availableRequirements` and `editable: false`, so the card has
 * nothing to render a form out of — which is deliberate. Sending the full set of levels for a template
 * whose policy the API refuses to write is how an operator came to set "Tùy chọn" on
 * ACCOUNT_EMAIL_CONFIRMATION and then be refused the block that setting invites.
 */
const UNSUPPORTED = {
  ...CONFIGURED,
  templateCode: 'ACCOUNT_EMAIL_CONFIRMATION',
  requirement: 'NONE',
  bodyCarriesBlockVi: false,
  bodyCarriesBlockEn: false,
  availableRequirements: [] as string[],
  capability: 'UNSUPPORTED',
  editable: false,
  capabilityReasonCode: 'ONE_TIME_CREDENTIAL',
  capabilityReasonVi: 'Mẫu này không dùng khối thông tin liên hệ vì email chứa liên kết xác nhận dùng một lần.',
};

/** An axios-shaped rejection. */
const httpError = (status: number, data?: unknown) =>
  Object.assign(new Error(`Request failed with status code ${status}`), {
    response: { status, data },
    request: {},
  });

beforeEach(() => {
  vi.clearAllMocks();
  vi.useRealTimers();
  getEmailContactSettings.mockResolvedValue({ data: CONFIGURED });
  previewEmailContactBlock.mockResolvedValue({ data: { html: '<table>sample</table>', rendersBlock: true } });
});

const renderPanel = (code = 'VISIT_PARTICIPANT_INVITATION') =>
  render(<ContactSettingsPanel templateCode={code} canEdit />);

describe('card 4 renders the real form', () => {
  it('shows every control the configuration needs', async () => {
    renderPanel();

    await screen.findByTestId('contact-settings-panel');

    // Requirement, contact source, the five visibility toggles, both headings, Reply-To.
    expect(screen.getByText('Bắt buộc')).toBeInTheDocument();
    expect(screen.getByLabelText('Lấy đầu mối từ')).toBeInTheDocument();
    expect(screen.getByLabelText('Email công việc')).toBeInTheDocument();
    expect(screen.getByLabelText('Số điện thoại')).toBeInTheDocument();
    expect(screen.getByLabelText('Phòng ban')).toBeInTheDocument();
    expect(screen.getByLabelText('Cơ sở')).toBeInTheDocument();
    expect(screen.getByLabelText('Dòng “Được gửi bởi”')).toBeInTheDocument();
    expect(screen.getByLabelText('Tiêu đề khối (VI)')).toBeInTheDocument();
    expect(screen.getByLabelText('Tiêu đề khối (EN)')).toBeInTheDocument();
    expect(screen.getByLabelText('Reply-To')).toBeInTheDocument();

    expect(screen.queryByTestId('contact-settings-error')).not.toBeInTheDocument();
  });

  it('saves a toggle and shows what came back', async () => {
    updateEmailContactSettings.mockResolvedValue({
      data: { ...CONFIGURED, showPhone: false, showDepartment: true },
    });

    renderPanel();
    await screen.findByTestId('contact-settings-panel');

    fireEvent.click(screen.getByLabelText('Số điện thoại'));
    fireEvent.click(screen.getByText('Lưu cấu hình liên hệ'));

    await waitFor(() => expect(updateEmailContactSettings).toHaveBeenCalledTimes(1));

    const [code, payload] = updateEmailContactSettings.mock.calls[0];
    expect(code).toBe('VISIT_PARTICIPANT_INVITATION');
    expect(payload.showPhone).toBe(false);

    await waitFor(() => {
      expect((screen.getByLabelText('Số điện thoại') as HTMLInputElement).checked).toBe(false);
      expect((screen.getByLabelText('Phòng ban') as HTMLInputElement).checked).toBe(true);
    });
  });
});

/**
 * Capability — whether the block may appear AT ALL, which is not the same question as the requirement
 * level (§2, §5).
 *
 * The reported defect ran through both halves of this distinction. The card offered the whole form on
 * ACCOUNT_EMAIL_CONFIRMATION, whose message is a one-time confirmation link; an operator chose "Tùy
 * chọn", saved it, added {{contactInformationBlock}} — and met EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWED,
 * with nothing on screen connecting the refusal to the setting that had invited it.
 */
describe('a template that cannot carry the block says so, and shows no form', () => {
  const renderUnsupported = async (props: Record<string, unknown> = {}) => {
    getEmailContactSettings.mockResolvedValue({ data: UNSUPPORTED });
    render(<ContactSettingsPanel templateCode="ACCOUNT_EMAIL_CONFIRMATION" canEdit {...props} />);
    return screen.findByTestId('contact-settings-unsupported');
  };

  it('states the reason rather than an error', async () => {
    const card = await renderUnsupported();

    expect(card).toHaveAttribute('data-capability', 'UNSUPPORTED');
    expect(card).toHaveTextContent('không dùng khối thông tin liên hệ');
    expect(card).toHaveTextContent('liên kết xác nhận dùng một lần');
    expect(card).toHaveTextContent('Không có cấu hình cần chỉnh sửa');
    expect(screen.queryByTestId('contact-settings-error')).not.toBeInTheDocument();
  });

  it('offers no control, no save and no restore', async () => {
    await renderUnsupported();

    expect(screen.queryByRole('radio')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Lấy đầu mối từ')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Email công việc')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Tiêu đề khối (VI)')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Reply-To')).not.toBeInTheDocument();
    expect(screen.queryByText('Lưu cấu hình liên hệ')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Phục hồi về cấu hình mặc định của mẫu/ }))
      .not.toBeInTheDocument();

    expect(updateEmailContactSettings).not.toHaveBeenCalled();
    expect(restoreEmailContactSettingsDefault).not.toHaveBeenCalled();
  });

  /** Nothing to preview, and nothing to ask for: the block cannot render on this template. */
  it('hands up an empty block without asking the server to render one', async () => {
    const onBlockPreviewChange = vi.fn();
    await renderUnsupported({ onBlockPreviewChange });

    await waitFor(() => expect(onBlockPreviewChange).toHaveBeenCalledWith(''));
    await new Promise(r => setTimeout(r, 400));
    expect(previewEmailContactBlock).not.toHaveBeenCalled();
  });

  it('never reports unsaved changes, because there is nothing to change', async () => {
    const onDirtyChange = vi.fn();
    await renderUnsupported({ onDirtyChange });

    expect(onDirtyChange).toHaveBeenCalledWith(false);
    expect(onDirtyChange).not.toHaveBeenCalledWith(true);
  });
});

/**
 * A template whose wording tells the recipient to make contact may be OPTIONAL or REQUIRED, but not
 * NONE: choosing it would leave the instruction with no address, and the API refuses the write. The
 * levels come from the backend already narrowed, so the card cannot disagree with what will be accepted.
 */
describe('a template whose text instructs the reader to make contact', () => {
  it('does not offer "Không hiển thị", and says why', async () => {
    getEmailContactSettings.mockResolvedValue({
      data: {
        ...CONFIGURED,
        capability: 'REQUIRED',
        availableRequirements: ['OPTIONAL', 'REQUIRED'],
        capabilityReasonVi:
          'Nội dung mẫu này có câu yêu cầu người nhận liên hệ, nên email phải kèm khối thông tin liên hệ.',
      },
    });

    renderPanel();
    await screen.findByTestId('contact-settings-panel');

    const radios = screen.getAllByRole('radio') as HTMLInputElement[];
    expect(radios).toHaveLength(2);
    // Asserted on the CONTROL, not on the words: the sentence explaining the omission names the level
    // it is explaining, and matching text alone would fail on the explanation itself.
    expect(screen.queryByRole('radio', { name: /Không hiển thị/ })).not.toBeInTheDocument();

    expect(screen.getByTestId('contact-settings-level-locked'))
      .toHaveTextContent('phải kèm khối thông tin liên hệ');
  });
});

describe('a NO_CONTACT template is a state, not a failure', () => {
  it('says so in words instead of showing an error', async () => {
    getEmailContactSettings.mockResolvedValue({
      // SUPPORTED with the level at NONE — the operator's choice, not a capability. A template that
      // cannot carry the block at all is the case above, and it looks nothing like this one.
      data: { ...CONFIGURED, templateCode: 'ACCOUNT_ROLE_CHANGED', requirement: 'NONE',
              capability: 'SUPPORTED',
              bodyCarriesBlockVi: false, bodyCarriesBlockEn: false },
    });

    renderPanel('ACCOUNT_ROLE_CHANGED');

    const notice = await screen.findByTestId('contact-settings-no-contact');
    expect(notice).toHaveTextContent('Không hiển thị thông tin liên hệ');
    expect(screen.queryByTestId('contact-settings-error')).not.toBeInTheDocument();

    // Still switchable — NONE is a choice, not a lock. ("Tùy chọn" also appears inside the notice
    // above, so this asserts on the radio itself rather than on the word.)
    const radios = screen.getAllByRole('radio') as HTMLInputElement[];
    expect(radios).toHaveLength(3);
    expect(radios.some(r => !r.checked)).toBe(true);
    expect(radios.every(r => !r.disabled)).toBe(true);
  });
});

/**
 * The per-field "Đang kế thừa · <level>" badges and their summary line were REMOVED from this card.
 *
 * They described a distinction the card can no longer act on: there is no control here for clearing a
 * field back to inheritance, so naming the level a value arrived from told an operator something true
 * and gave them nothing to do about it. What replaced them is one honest action — put this template's
 * own values back to the shipped defaults — asserted below.
 *
 * These tests therefore assert the ABSENCE deliberately, including on the payload that used to produce
 * two badges, so a re-introduction has to be a decision rather than an accident.
 */
describe('per-field inheritance badges are not part of this card', () => {
  it('says nothing when every field is the template’s own', async () => {
    renderPanel();
    await screen.findByTestId('contact-settings-panel');

    expect(screen.queryByTestId('contact-settings-inherited')).not.toBeInTheDocument();
    expect(screen.queryByTestId('contact-settings-inherit-summary')).not.toBeInTheDocument();
  });

  it('says nothing either when fields really do come from another level', async () => {
    getEmailContactSettings.mockResolvedValue({
      data: {
        ...CONFIGURED,
        hasOwnPolicyRow: true,
        showPhoneSource: 'SYSTEM',
        headingSource: 'SHIPPED_DEFAULT',
      },
    });

    renderPanel();
    await screen.findByTestId('contact-settings-panel');

    expect(screen.queryByTestId('contact-settings-inherited')).not.toBeInTheDocument();
    expect(screen.queryByTestId('contact-settings-inherit-summary')).not.toBeInTheDocument();
  });
});

describe('restoring the template’s default contact configuration', () => {
  /**
   * The wording is load-bearing. The endpoint WRITES the shipped defaults onto this template's own
   * policy row; it does not delete the row so that campus/system configuration flows through again.
   * Calling it "quay lại kế thừa" would describe an operation nothing implements.
   */
  it('names what it does and asks before doing it', async () => {
    renderPanel();
    await screen.findByTestId('contact-settings-panel');

    fireEvent.click(screen.getByRole('button', { name: /Phục hồi về cấu hình mặc định của mẫu/ }));

    expect(await screen.findByRole('heading', { name: 'Phục hồi về cấu hình mặc định của mẫu' }))
      .toBeInTheDocument();
    expect(screen.getByText(/giá trị mặc định của mẫu này/)).toBeInTheDocument();
    expect(screen.getByText(/Nội dung email không thay đổi/)).toBeInTheDocument();
    expect(screen.queryByText(/kế thừa/)).not.toBeInTheDocument();

    // Nothing is sent until the operator confirms.
    expect(restoreEmailContactSettingsDefault).not.toHaveBeenCalled();
  });

  it('calls the endpoint on confirm and adopts the response', async () => {
    const restored = { ...CONFIGURED, showPhone: false, replyToSource: 'NONE' };
    restoreEmailContactSettingsDefault.mockResolvedValue({ data: restored });
    const pushToast = vi.fn();

    render(<ContactSettingsPanel templateCode="VISIT_PARTICIPANT_INVITATION" canEdit pushToast={pushToast} />);
    await screen.findByTestId('contact-settings-panel');

    fireEvent.click(screen.getByRole('button', { name: /Phục hồi về cấu hình mặc định của mẫu/ }));
    fireEvent.click(await screen.findByRole('button', { name: 'Xác nhận' }));

    await waitFor(() =>
      expect(restoreEmailContactSettingsDefault).toHaveBeenCalledWith('VISIT_PARTICIPANT_INVITATION'));

    await waitFor(() => expect(pushToast).toHaveBeenCalledWith(
      'success', 'Đã phục hồi về cấu hình mặc định của mẫu.'));

    // The form shows the restored values, not the ones it was holding.
    const phone = screen.getByLabelText('Số điện thoại') as HTMLInputElement;
    expect(phone.checked).toBe(false);
  });

  /**
   * The backend refuses this when the body no longer carries {{contactInformationBlock}} and the shipped
   * default is REQUIRED. The refusal has to reach the card: restoring is the operator's answer to a
   * broken configuration, so a silent failure leaves them pressing a button that appears to do nothing.
   *
   * Asserted on the error REGION, not on the sentence: the API answers in Vietnamese and the toast
   * helper deliberately swaps an untranslated Vietnamese message for a status-based one when the UI is
   * in English, so pinning the wording here would pin the locale the suite happens to run in.
   */
  it('reports a refusal in the card rather than swallowing it', async () => {
    restoreEmailContactSettingsDefault.mockRejectedValue(
      httpError(422, { message: 'Không thể phục hồi cấu hình liên hệ về mức Bắt buộc…' }));
    const pushToast = vi.fn();

    render(<ContactSettingsPanel templateCode="VISIT_PARTICIPANT_INVITATION" canEdit pushToast={pushToast} />);
    await screen.findByTestId('contact-settings-panel');

    fireEvent.click(screen.getByRole('button', { name: /Phục hồi về cấu hình mặc định của mẫu/ }));
    fireEvent.click(await screen.findByRole('button', { name: 'Xác nhận' }));

    const shown = await screen.findByTestId('contact-settings-save-error');
    expect(shown.textContent?.trim()).not.toBe('');

    // And it is not reported as a success.
    expect(pushToast).not.toHaveBeenCalledWith('success', expect.anything());
  });
});

/**
 * The preview pane has to follow the UNSAVED draft: an operator unticking "Số điện thoại" must see the
 * row leave before they commit to it. Rendering stays on the backend so the block's markup and its
 * visibility rules have exactly one implementation — see EmailContactHtmlRenderer.
 */
describe('the contact block preview follows the draft', () => {
  it('renders the block from the loaded policy and hands it upward', async () => {
    const onBlockPreviewChange = vi.fn();

    render(
      <ContactSettingsPanel
        templateCode="VISIT_PARTICIPANT_INVITATION"
        canEdit
        language="VI"
        onBlockPreviewChange={onBlockPreviewChange}
      />,
    );

    await screen.findByTestId('contact-settings-panel');
    await waitFor(() => expect(onBlockPreviewChange).toHaveBeenCalledWith('<table>sample</table>'));

    const [code, payload] = previewEmailContactBlock.mock.calls[0];
    expect(code).toBe('VISIT_PARTICIPANT_INVITATION');
    expect(payload.language).toBe('VI');
    expect(payload.showPhone).toBe(true);
  });

  it('re-renders with the new toggle when one is changed', async () => {
    const onBlockPreviewChange = vi.fn();

    render(
      <ContactSettingsPanel
        templateCode="VISIT_PARTICIPANT_INVITATION"
        canEdit
        onBlockPreviewChange={onBlockPreviewChange}
      />,
    );

    await screen.findByTestId('contact-settings-panel');
    await waitFor(() => expect(previewEmailContactBlock).toHaveBeenCalledTimes(1));

    previewEmailContactBlock.mockResolvedValue({
      data: { html: '<table>no phone</table>', rendersBlock: true },
    });
    fireEvent.click(screen.getByLabelText('Số điện thoại'));

    await waitFor(() => expect(previewEmailContactBlock).toHaveBeenCalledTimes(2));

    const [, payload] = previewEmailContactBlock.mock.calls[1];
    expect(payload.showPhone).toBe(false);

    await waitFor(() =>
      expect(onBlockPreviewChange).toHaveBeenLastCalledWith('<table>no phone</table>'));
  });

  /** NO_CONTACT: the backend answers with an empty block and the pane simply shows no card. */
  it('hands up an empty block for a NO_CONTACT policy', async () => {
    getEmailContactSettings.mockResolvedValue({
      data: { ...CONFIGURED, requirement: 'NONE' },
    });
    previewEmailContactBlock.mockResolvedValue({ data: { html: '', rendersBlock: false } });

    const onBlockPreviewChange = vi.fn();

    render(
      <ContactSettingsPanel
        templateCode="AUTH_PASSWORD_RESET_OTP"
        canEdit
        onBlockPreviewChange={onBlockPreviewChange}
      />,
    );

    await screen.findByTestId('contact-settings-no-contact');
    await waitFor(() => expect(onBlockPreviewChange).toHaveBeenCalledWith(''));
  });

  /**
   * A failed preview clears the pane instead of leaving the previous policy's block on screen, which
   * would show toggles that are no longer set.
   */
  it('clears the preview when the render request fails', async () => {
    previewEmailContactBlock.mockRejectedValue(httpError(500));
    const onBlockPreviewChange = vi.fn();

    render(
      <ContactSettingsPanel
        templateCode="VISIT_PARTICIPANT_INVITATION"
        canEdit
        onBlockPreviewChange={onBlockPreviewChange}
      />,
    );

    await screen.findByTestId('contact-settings-panel');
    await waitFor(() => expect(onBlockPreviewChange).toHaveBeenCalledWith(''));
  });

  /** No callback, no requests — the panel is usable on its own without a preview pane attached. */
  it('does not render a preview when nobody is listening', async () => {
    renderPanel();
    await screen.findByTestId('contact-settings-panel');

    await new Promise(r => setTimeout(r, 400));
    expect(previewEmailContactBlock).not.toHaveBeenCalled();
  });
});

describe('a failure says which repair it needs', () => {
  it('names an out-of-date API on a routing 404 with no error code', async () => {
    getEmailContactSettings.mockRejectedValue(httpError(404));

    renderPanel();

    const box = await screen.findByTestId('contact-settings-error');
    expect(box).toHaveAttribute('data-failure-kind', 'endpoint-missing');
    expect(box).toHaveTextContent('API đang chạy chưa có chức năng này');
    expect(box).toHaveTextContent('khởi động lại API');

    // And explicitly NOT the sentence that used to stand for every failure.
    expect(box).not.toHaveTextContent('Không tìm thấy dữ liệu cần xử lý');
  });

  it('names the missing patch when the policy store cannot be read', async () => {
    getEmailContactSettings.mockRejectedValue(
      httpError(422, { errorCode: 'EMAIL_CONTACT_POLICY_STORE_UNAVAILABLE', message: 'x' }),
    );

    renderPanel();

    const box = await screen.findByTestId('contact-settings-error');
    expect(box).toHaveAttribute('data-failure-kind', 'store-unavailable');
    expect(box).toHaveTextContent('2026-08-03_email_contact_information_block.sql');
  });

  it('names the catalog patch when the template is not in the catalog', async () => {
    getEmailContactSettings.mockRejectedValue(
      httpError(404, { errorCode: 'EMAIL_TEMPLATE_NOT_FOUND', message: 'x' }),
    );

    renderPanel();

    const box = await screen.findByTestId('contact-settings-error');
    expect(box).toHaveAttribute('data-failure-kind', 'template-not-catalogued');
    expect(box).toHaveTextContent('2026-08-03_email_template_catalog_alignment.sql');
  });

  it('tells an unauthenticated operator to sign in again', async () => {
    getEmailContactSettings.mockRejectedValue(httpError(401));

    renderPanel();

    const box = await screen.findByTestId('contact-settings-error');
    expect(box).toHaveAttribute('data-failure-kind', 'unauthenticated');
    expect(box).toHaveTextContent('Đăng nhập lại');
  });

  it('distinguishes a permission refusal from a missing session', async () => {
    getEmailContactSettings.mockRejectedValue(httpError(403));

    renderPanel();

    const box = await screen.findByTestId('contact-settings-error');
    expect(box).toHaveAttribute('data-failure-kind', 'forbidden');
    expect(box).toHaveTextContent('Head Office');
  });

  /**
   * The message is passed through `getApiErrorMessage`, which under the EN locale deliberately drops a
   * raw Vietnamese backend string rather than mixing languages — so the fixture uses one that survives
   * that rule. What matters here is that a genuine server fault is NOT reported as a missing endpoint.
   */
  it('relays a real server error rather than calling it a missing endpoint', async () => {
    getEmailContactSettings.mockRejectedValue(
      httpError(500, { message: 'Query against table users failed.' }),
    );

    renderPanel();

    const box = await screen.findByTestId('contact-settings-error');
    expect(box).toHaveAttribute('data-failure-kind', 'server-error');
    expect(box).toHaveTextContent('Query against table users failed.');
    expect(box).not.toHaveTextContent('khởi động lại API');
  });

  it('offers a retry that re-requests the settings', async () => {
    getEmailContactSettings.mockRejectedValueOnce(httpError(404));

    renderPanel();
    await screen.findByTestId('contact-settings-error');

    getEmailContactSettings.mockResolvedValue({ data: CONFIGURED });
    fireEvent.click(screen.getByText('Tải lại'));

    await screen.findByTestId('contact-settings-panel');
    expect(getEmailContactSettings).toHaveBeenCalledTimes(2);
  });
});
