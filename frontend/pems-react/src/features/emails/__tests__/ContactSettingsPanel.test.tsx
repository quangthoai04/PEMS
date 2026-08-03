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

vi.mock('../api/emailsApi', () => ({
  emailsApi: {
    getEmailContactSettings: (...a: unknown[]) => getEmailContactSettings(...a),
    updateEmailContactSettings: (...a: unknown[]) => updateEmailContactSettings(...a),
    previewEmailContactBlock: (...a: unknown[]) => previewEmailContactBlock(...a),
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

describe('a NO_CONTACT template is a state, not a failure', () => {
  it('says so in words instead of showing an error', async () => {
    getEmailContactSettings.mockResolvedValue({
      data: { ...CONFIGURED, templateCode: 'AUTH_PASSWORD_RESET_OTP', requirement: 'NONE',
              bodyCarriesBlockVi: false, bodyCarriesBlockEn: false },
    });

    renderPanel('AUTH_PASSWORD_RESET_OTP');

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

describe('inheritance is shown only where a field is really inherited', () => {
  it('says nothing when every field is the template’s own', async () => {
    renderPanel();
    await screen.findByTestId('contact-settings-panel');

    expect(screen.queryByTestId('contact-settings-inherited')).not.toBeInTheDocument();
    expect(screen.queryByTestId('contact-settings-inherit-summary')).not.toBeInTheDocument();
  });

  /**
   * The case the old `isDefault = !hasOverride` flag could never reach: a row EXISTS, so the flag said
   * "overridden", while two of its columns are NULL and are really coming from further down.
   */
  it('marks the individual fields that come from another level', async () => {
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

    const badges = await screen.findAllByTestId('contact-settings-inherited');
    expect(badges).toHaveLength(2);

    expect(screen.getByTestId('contact-settings-inherit-summary'))
      .toHaveTextContent('2 trường chưa đặt riêng cho mẫu này');
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
