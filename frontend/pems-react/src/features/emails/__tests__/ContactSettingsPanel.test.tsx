/**
 * Card 4, "Cấu hình thông tin liên hệ".
 *
 * Two rounds of defects are pinned here.
 *
 * The first: the card showed "Không tìm thấy dữ liệu cần xử lý." and nothing else. That is the toast
 * helper's generic HTTP-404 sentence, reached whenever a response carries no error code — and the failure
 * it was actually describing was a running API built before this endpoint existed, where no data is
 * missing at all. Three different repairs (restart the API, run the policy patch, align the catalog)
 * arrived as one sentence that named none of them.
 *
 * The second: the card owned its own draft, its own dirty flag and its own save and restore buttons. An
 * operator had to remember two saves; either could succeed while the other failed, leaving a body and a
 * policy contradicting each other; and choosing "Không hiển thị" while the body still carried the block
 * produced a state both halves refuse. The card is now controlled — it renders `value`, reports edits
 * through `onChange`, and asks the editor before switching to NONE.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { useState } from 'react';
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

import { ContactSettingsPanel, toContactPayload } from '../components/ContactSettingsPanel';
import type { EmailContactSettings, EmailContactSettingsPayload } from '../api/emailsApi';

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
} as unknown as EmailContactSettings;

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
} as unknown as EmailContactSettings;

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

/**
 * The card is controlled, so a test needs the half the editor owns.
 *
 * The harness is as thin as it can be and still be honest: it holds the value, applies `onChange`, and
 * seeds itself from `onLoaded` exactly as the editor does. `onRequestHide` deliberately does NOT set the
 * level by default — that is the editor's decision to make, and a harness that applied it silently would
 * hide the very handoff these tests exist to pin.
 */
function Harness({
  templateCode = 'VISIT_PARTICIPANT_INVITATION',
  onRequestHide,
  ...rest
}: Partial<React.ComponentProps<typeof ContactSettingsPanel>> = {}) {
  const [value, setValue] = useState<EmailContactSettingsPayload | null>(null);

  return (
    <ContactSettingsPanel
      templateCode={templateCode}
      canEdit
      value={value}
      onChange={setValue}
      onLoaded={s => setValue(toContactPayload(s))}
      onRequestHide={onRequestHide ?? (() => {})}
      {...rest}
    />
  );
}

const renderPanel = (code = 'VISIT_PARTICIPANT_INVITATION', props = {}) =>
  render(<Harness templateCode={code} {...props} />);

describe('card 4 renders the real form', () => {
  it('shows every control the configuration needs', async () => {
    renderPanel();

    await screen.findByTestId('contact-settings-panel');

    // Requirement, contact source, the five visibility toggles, both headings, Reply-To.
    expect(screen.getByText('Bắt buộc')).toBeInTheDocument();
    // Named for what it chooses. "Lấy đầu mối từ" was how the policy is discussed internally, not what
    // the field does on screen.
    expect(screen.getByLabelText('Nguồn thông tin liên hệ')).toBeInTheDocument();
    expect(screen.queryByLabelText('Lấy đầu mối từ')).not.toBeInTheDocument();
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

  it('reports a toggle upward instead of saving it', async () => {
    const onChange = vi.fn();
    render(
      <Harness onChange={onChange} value={toContactPayload(CONFIGURED)} />,
    );

    await screen.findByTestId('contact-settings-panel');
    fireEvent.click(screen.getByLabelText('Số điện thoại'));

    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ showPhone: false, requirement: 'REQUIRED' }));
    // Nothing is written from here. The editor's one save writes both halves together.
    expect(updateEmailContactSettings).not.toHaveBeenCalled();
  });
});

/**
 * §3.1 / §11 of the atomic-save prompt: the card's own save and restore are gone.
 *
 * Asserted as an absence, and deliberately: re-introducing either would re-introduce the partial save
 * this whole change exists to remove, so it has to be a decision rather than an accident.
 */
describe('the card has no buttons of its own', () => {
  it('offers neither a save nor a restore', async () => {
    renderPanel();
    await screen.findByTestId('contact-settings-panel');

    expect(screen.queryByRole('button', { name: /Lưu cấu hình liên hệ/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Phục hồi mặc định/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Phục hồi về cấu hình mặc định của mẫu/ }))
      .not.toBeInTheDocument();
  });

  it('never calls the standalone contact endpoints', async () => {
    renderPanel();
    await screen.findByTestId('contact-settings-panel');

    fireEvent.click(screen.getByLabelText('Cơ sở'));
    fireEvent.change(screen.getByLabelText('Tiêu đề khối (VI)'), { target: { value: 'Liên hệ' } });

    await new Promise(r => setTimeout(r, 400));
    expect(updateEmailContactSettings).not.toHaveBeenCalled();
    expect(restoreEmailContactSettingsDefault).not.toHaveBeenCalled();
  });

  /** One dirty indicator lives in the editor footer; the card no longer carries a second. */
  it('shows no dirty indicator of its own', async () => {
    renderPanel();
    await screen.findByTestId('contact-settings-panel');

    fireEvent.click(screen.getByLabelText('Cơ sở'));

    expect(screen.queryByTestId('contact-settings-dirty')).not.toBeInTheDocument();
  });
});

/**
 * §4 of the visibility prompt: choosing "Không hiển thị" is reported, not applied.
 *
 * The bodies belong to the editor, and switching to NONE may require them to change. Applying it here
 * would either delete from content the card does not own — an edit the operator never made and cannot
 * see — or leave a template that cannot be saved with nothing on screen explaining why.
 */
describe('choosing "Không hiển thị"', () => {
  it('asks the editor rather than setting the level itself', async () => {
    const onRequestHide = vi.fn();
    const onChange = vi.fn();

    render(
      <Harness
        value={toContactPayload(CONFIGURED)}
        onChange={onChange}
        onRequestHide={onRequestHide}
      />,
    );

    await screen.findByTestId('contact-settings-panel');
    fireEvent.click(screen.getByTestId('contact-requirement-NONE'));

    expect(onRequestHide).toHaveBeenCalledTimes(1);
    // Not applied here, and specifically not applied as a level change that the editor never approved.
    expect(onChange).not.toHaveBeenCalled();
  });

  it('applies the other two levels directly, because they need no decision', async () => {
    const onRequestHide = vi.fn();
    const onChange = vi.fn();

    render(
      <Harness
        value={toContactPayload(CONFIGURED)}
        onChange={onChange}
        onRequestHide={onRequestHide}
      />,
    );

    await screen.findByTestId('contact-settings-panel');
    fireEvent.click(screen.getByTestId('contact-requirement-OPTIONAL'));

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ requirement: 'OPTIONAL' }));
    expect(onRequestHide).not.toHaveBeenCalled();
  });
});

/**
 * §13 of the visibility prompt: message and action are separate elements.
 *
 * The failure this replaces rendered them as one unbroken run of text ending in a raw error code
 * immediately followed by the button label — "…EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWEDXóa khối không
 * hợp lệ" — which is neither readable nor clickable.
 */
describe('a cross-field refusal on the card', () => {
  const conflict = {
    message: 'Khối thông tin liên hệ vẫn tồn tại trong nội dung, nhưng mức hiển thị đang là "Không hiển thị".',
    actionLabel: 'Xóa khối khỏi nội dung',
    onAction: vi.fn(),
  };

  it('renders the sentence and the button as separate elements', async () => {
    renderPanel('VISIT_PARTICIPANT_INVITATION', { crossFieldError: conflict });
    await screen.findByTestId('contact-settings-panel');

    const box = await screen.findByTestId('contact-settings-cross-field-error');
    expect(box).toHaveTextContent('mức hiển thị đang là "Không hiển thị"');

    const button = screen.getByTestId('contact-settings-remove-block');
    expect(button.tagName).toBe('BUTTON');
    expect(button).toHaveTextContent('Xóa khối khỏi nội dung');
    // The message is not inside the button, and the button's label is not inside the message.
    expect(button).not.toHaveTextContent('mức hiển thị');
  });

  it('shows no raw error code in the sentence', async () => {
    renderPanel('VISIT_PARTICIPANT_INVITATION', { crossFieldError: conflict });
    const box = await screen.findByTestId('contact-settings-cross-field-error');

    expect(box.textContent).not.toMatch(/EMAIL_TEMPLATE_[A-Z_]+/);
  });

  it('runs the action when the button is pressed', async () => {
    const onAction = vi.fn();
    renderPanel('VISIT_PARTICIPANT_INVITATION', {
      crossFieldError: { ...conflict, onAction },
    });

    fireEvent.click(await screen.findByTestId('contact-settings-remove-block'));
    expect(onAction).toHaveBeenCalledTimes(1);
  });

  /**
   * §9: an unsupported template has no form, and that is exactly where a stale block is easiest to
   * miss — the card has just said there is nothing here to configure.
   */
  it('still warns about a stale block on a template with no form', async () => {
    getEmailContactSettings.mockResolvedValue({ data: UNSUPPORTED });

    renderPanel('ACCOUNT_EMAIL_CONFIRMATION', {
      contactSupported: false,
      crossFieldError: conflict,
    });

    await screen.findByTestId('contact-settings-unsupported');
    expect(await screen.findByTestId('contact-settings-cross-field-error')).toBeInTheDocument();
    expect(screen.getByTestId('contact-settings-remove-block')).toBeInTheDocument();
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
    render(<Harness templateCode="ACCOUNT_EMAIL_CONFIRMATION" {...props} />);
    return screen.findByTestId('contact-settings-unsupported');
  };

  it('states the reason rather than an error', async () => {
    await renderUnsupported();

    const card = screen.getByTestId('contact-settings-unsupported');
    expect(card.parentElement).toHaveAttribute('data-capability', 'UNSUPPORTED');
    expect(card).toHaveTextContent('không dùng khối thông tin liên hệ');
    expect(card).toHaveTextContent('liên kết xác nhận dùng một lần');
    expect(card).toHaveTextContent('Không có cấu hình cần chỉnh sửa');
    expect(screen.queryByTestId('contact-settings-error')).not.toBeInTheDocument();
  });

  it('offers no control at all', async () => {
    await renderUnsupported();

    expect(screen.queryByRole('radio')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Nguồn thông tin liên hệ')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Email công việc')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Tiêu đề khối (VI)')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Reply-To')).not.toBeInTheDocument();

    expect(updateEmailContactSettings).not.toHaveBeenCalled();
    expect(restoreEmailContactSettingsDefault).not.toHaveBeenCalled();
  });

  /** Nothing to preview, and nothing to ask for: the block cannot render on this template. */
  it('hands up an empty block without asking the server to render one', async () => {
    const onBlockPreviewChange = vi.fn();
    await renderUnsupported({ onBlockPreviewChange, contactSupported: false });

    await waitFor(() => expect(onBlockPreviewChange).toHaveBeenCalledWith(''));
    await new Promise(r => setTimeout(r, 400));
    expect(previewEmailContactBlock).not.toHaveBeenCalled();
  });

  /**
   * The caller's contract settles it on its own.
   *
   * The two responses that carry this fact must not be able to disagree in the unsafe direction: an API
   * built before the capability split answers the settings endpoint without `capability`, and reading
   * that alone put the whole configuration form on a template whose policy the backend refuses to write
   * — the reported defect. With the contract saying no, the card does not even ask.
   */
  it('shows the reason without a settings request when the contract already says so', async () => {
    render(
      <Harness
        templateCode="ACCOUNT_EMAIL_CONFIRMATION"
        contactSupported={false}
        contactReasonVi="Mẫu này không dùng khối thông tin liên hệ vì email chứa liên kết xác nhận dùng một lần."
      />,
    );

    const card = await screen.findByTestId('contact-settings-unsupported');
    expect(card).toHaveTextContent('liên kết xác nhận dùng một lần');
    expect(getEmailContactSettings).not.toHaveBeenCalled();
    expect(screen.queryByTestId('contact-settings-loading')).not.toBeInTheDocument();
    expect(screen.queryByTestId('contact-settings-panel')).not.toBeInTheDocument();
  });

  /** An API that answers without `capability` no longer decides this by itself. */
  it('still shows no form when the settings response omits the capability', async () => {
    const { capability, editable, ...withoutCapability } =
      UNSUPPORTED as unknown as Record<string, unknown>;
    void capability; void editable;
    getEmailContactSettings.mockResolvedValue({ data: withoutCapability });

    render(<Harness templateCode="ACCOUNT_EMAIL_CONFIRMATION" contactSupported={false} />);

    await screen.findByTestId('contact-settings-unsupported');
    expect(screen.queryByRole('radio')).not.toBeInTheDocument();
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
    expect(screen.queryByTestId('contact-requirement-NONE')).not.toBeInTheDocument();

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

  /** The explanatory note steps aside when there is a refusal to read instead. */
  it('yields to the conflict message when the body still has the block', async () => {
    getEmailContactSettings.mockResolvedValue({
      data: { ...CONFIGURED, requirement: 'NONE', capability: 'SUPPORTED' },
    });

    renderPanel('ACCOUNT_ROLE_CHANGED', {
      crossFieldError: { message: 'Khối vẫn còn trong nội dung.', actionLabel: 'Xóa khối khỏi nội dung', onAction: vi.fn() },
    });

    await screen.findByTestId('contact-settings-cross-field-error');
    expect(screen.queryByTestId('contact-settings-no-contact')).not.toBeInTheDocument();
  });
});

/**
 * The per-field "Đang kế thừa · <level>" badges and their summary line were REMOVED from this card.
 *
 * They described a distinction the card can no longer act on: there is no control here for clearing a
 * field back to inheritance, so naming the level a value arrived from told an operator something true
 * and gave them nothing to do about it.
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

/**
 * The preview pane has to follow the UNSAVED draft: an operator unticking "Số điện thoại" must see the
 * row leave before they commit to it. Rendering stays on the backend so the block's markup and its
 * visibility rules have exactly one implementation — see EmailContactHtmlRenderer.
 */
describe('the contact block preview follows the draft', () => {
  it('renders the block from the loaded policy and hands it upward', async () => {
    const onBlockPreviewChange = vi.fn();

    render(<Harness language="VI" onBlockPreviewChange={onBlockPreviewChange} />);

    await screen.findByTestId('contact-settings-panel');
    await waitFor(() => expect(onBlockPreviewChange).toHaveBeenCalledWith('<table>sample</table>'));

    const [code, payload] = previewEmailContactBlock.mock.calls[0];
    expect(code).toBe('VISIT_PARTICIPANT_INVITATION');
    expect(payload.language).toBe('VI');
    expect(payload.showPhone).toBe(true);
  });

  it('re-renders with the new toggle when one is changed', async () => {
    const onBlockPreviewChange = vi.fn();

    render(<Harness onBlockPreviewChange={onBlockPreviewChange} />);

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

  /**
   * §10: a hidden level renders nothing, and the preview says so without asking.
   *
   * Answered locally rather than by a round trip because the answer is not in doubt — and because a
   * preview showing a contact card over a policy of "Không hiển thị" would tell an operator their
   * setting had not taken effect.
   */
  it('hands up an empty block for a NO_CONTACT policy without calling the server', async () => {
    getEmailContactSettings.mockResolvedValue({
      data: { ...CONFIGURED, requirement: 'NONE' },
    });

    const onBlockPreviewChange = vi.fn();

    render(<Harness templateCode="ACCOUNT_ROLE_CHANGED" onBlockPreviewChange={onBlockPreviewChange} />);

    await screen.findByTestId('contact-settings-no-contact');
    await waitFor(() => expect(onBlockPreviewChange).toHaveBeenCalledWith(''));

    await new Promise(r => setTimeout(r, 400));
    expect(previewEmailContactBlock).not.toHaveBeenCalled();
  });

  /**
   * A failed preview clears the pane instead of leaving the previous policy's block on screen, which
   * would show toggles that are no longer set.
   */
  it('clears the preview when the render request fails', async () => {
    previewEmailContactBlock.mockRejectedValue(httpError(500));
    const onBlockPreviewChange = vi.fn();

    render(<Harness onBlockPreviewChange={onBlockPreviewChange} />);

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
