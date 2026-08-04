/**
 * The template-management screen (G11-I catalog, G11-J contract).
 *
 * Two reported defects are pinned here as behaviour rather than as markup:
 *   - opening a canonical template must produce NO warning, and must not offer another module's
 *     variables (the screen used to do both, from a list compiled into the frontend);
 *   - the catalog is fixed, so there is no "Thêm mẫu mới" and no per-row status toggle.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';

const getEmailTemplateList = vi.fn();
const getEmailTemplateDetail = vi.fn();
const getEmailTemplateContract = vi.fn();
const updateEmailTemplate = vi.fn();
const restoreEmailTemplateDefault = vi.fn();
const getEmailContactSettings = vi.fn();
const updateEmailContactSettings = vi.fn();
const previewEmailContactBlock = vi.fn();
const restoreEmailContactSettingsDefault = vi.fn();

vi.mock('../../../features/emails/api/emailsApi', () => ({
  emailsApi: {
    getEmailTemplateList: (...a: unknown[]) => getEmailTemplateList(...a),
    getEmailTemplateDetail: (...a: unknown[]) => getEmailTemplateDetail(...a),
    getEmailTemplateContract: (...a: unknown[]) => getEmailTemplateContract(...a),
    updateEmailTemplate: (...a: unknown[]) => updateEmailTemplate(...a),
    restoreEmailTemplateDefault: (...a: unknown[]) => restoreEmailTemplateDefault(...a),
    getEmailContactSettings: (...a: unknown[]) => getEmailContactSettings(...a),
    updateEmailContactSettings: (...a: unknown[]) => updateEmailContactSettings(...a),
    previewEmailContactBlock: (...a: unknown[]) => previewEmailContactBlock(...a),
    restoreEmailContactSettingsDefault: (...a: unknown[]) => restoreEmailContactSettingsDefault(...a),
  },
}));

// react-quill-new pulls in a DOM range API jsdom does not implement; the editor's behaviour is not
// what these tests are about.
vi.mock('react-quill-new', () => ({
  default: ({ value, onChange }: { value: string; onChange: (v: string) => void }) => (
    <textarea data-testid="quill" value={value} onChange={e => onChange(e.target.value)} />
  ),
}));
vi.mock('react-quill-new/dist/quill.snow.css', () => ({}));

import { TemplateManagement } from '../../../pages/dashboard/emails/TemplateManagement';

const ACCOUNT_CONTRACT = {
  templateCode: 'ACCOUNT_EMAIL_CONFIRMATION',
  module: 'ACCOUNT',
  isSystemTemplate: true,
  variables: [
    { name: 'campusName', label: 'Cơ sở', sample: 'FPTU Hà Nội', required: false, sensitive: false, forbiddenInSubject: false },
    { name: 'expiresInHours', label: 'Hiệu lực (giờ)', sample: '24', required: false, sensitive: false, forbiddenInSubject: false },
    { name: 'fullName', label: 'Họ tên người nhận', sample: 'Nguyễn Văn An', required: false, sensitive: false, forbiddenInSubject: false },
    { name: 'roleName', label: 'Vai trò', sample: 'Cán bộ', required: false, sensitive: false, forbiddenInSubject: false },
  ],
  // Data variables only — the blocks live in their own lists below, which is what stops a block from
  // being judged (and refused) as if it were a variable.
  allowedVariables: ['fullName', 'roleName', 'campusName', 'expiresInHours'],
  requiredVariables: [],
  optionalVariables: ['fullName', 'roleName', 'campusName', 'expiresInHours'],
  requiredSystemBlocks: [],
  optionalSystemBlocks: ['actionBlock'],
  // As the backend sends it: `<span>` buttons built by the same helpers the send uses, no href.
  systemBlockPreviews: {
    actionBlock:
      '<div style="text-align:center"><span style="background:#9aa6b2">Chấp nhận</span>'
      + '<span style="background:#9aa6b2">Từ chối</span></div>',
  },
  sensitiveVariables: [],
  forbiddenInSubject: ['actionBlock'],
  actionSupported: false,
  actionRequired: false,
  systemActionDescription: null,
  carriesSecret: true,
  allowCc: false,
  allowBcc: false,
  securityClassification: 'SENSITIVE',
  editableFields: ['name', 'description', 'subjectVi', 'subjectEn', 'bodyVi', 'bodyEn'],
};

const DETAIL = {
  emailTemplateId: 7,
  templateCode: 'ACCOUNT_EMAIL_CONFIRMATION',
  name: 'Xác nhận email tài khoản',
  description: 'Gửi khi tài khoản được tạo',
  subjectVi: '[PEMS] Xác nhận địa chỉ email',
  bodyVi: '<p>Chào {{fullName}}, vai trò {{roleName}} tại {{campusName}}. Hiệu lực {{expiresInHours}} giờ.</p>',
  subjectEn: '[PEMS] Confirm your email',
  bodyEn: '<p>Hello {{fullName}}, role {{roleName}} at {{campusName}}. Valid {{expiresInHours}} hours.</p>',
  status: 'ACTIVE',
  createdAt: '2026-07-01T08:00:00+07:00',
  updatedAt: null,
  revision: 4,
  hasShippedDefault: true,
};

/**
 * What the API answers for a row that is NOT a registered system template — a template from the
 * pre-registry catalog that survives because sent emails still point at it. Every list is empty,
 * exactly as `EmailTemplateContractDto` leaves them when `EmailTemplateContracts.Describe` returns
 * null. Those empty lists are the trap this fixture exists to reproduce.
 */
const HISTORICAL_CONTRACT = {
  templateCode: 'VISIT_REQUEST_APPROVED',
  module: '',
  isSystemTemplate: false,
  variables: [],
  allowedVariables: [],
  requiredVariables: [],
  optionalVariables: [],
  sensitiveVariables: [],
  forbiddenInSubject: [],
  actionSupported: false,
  actionRequired: false,
  systemActionDescription: null,
  carriesSecret: false,
  allowCc: false,
  allowBcc: false,
  securityClassification: 'STANDARD',
  editableFields: [],
};

/** A real row from the legacy seed, PascalCase variables and all. */
const HISTORICAL_DETAIL = {
  emailTemplateId: 2,
  templateCode: 'VISIT_REQUEST_APPROVED',
  name: 'Thông báo duyệt đơn thăm',
  description: 'Gửi khi request được HO hoặc Staff Leader duyệt.',
  subjectVi: '[PEMS] Đơn thăm {{RequestCode}} đã được duyệt',
  bodyVi: '<p>Chào {{RecipientName}}, đoàn {{DelegationName}} đã được duyệt. {{DecisionNote}}</p>',
  subjectEn: '[PEMS] Visit request {{RequestCode}} approved',
  bodyEn: '<p>Hello {{RecipientName}}, {{DelegationName}} was approved. {{DecisionNote}}</p>',
  status: 'ACTIVE',
  createdAt: '2026-01-01T08:00:00+07:00',
  updatedAt: null,
  revision: 1,
  hasShippedDefault: false,
};

const pushToast = vi.fn();

beforeEach(() => {
  vi.clearAllMocks();
  getEmailContactSettings.mockResolvedValue({
    data: {
      templateCode: 'ACCOUNT_EMAIL_CONFIRMATION', requirement: 'NONE', contactSource: 'SENDER',
      showEmail: true, showPhone: true, showDepartment: false, showCampus: false, showSender: false,
      headingVi: '', headingEn: '', replyToSource: 'NONE',
      blockPlaceholder: '{{contactInformationBlock}}',
      bodyCarriesBlockVi: false, bodyCarriesBlockEn: false, isDefault: true,
      availableRequirements: ['NONE', 'OPTIONAL', 'REQUIRED'],
      availableSources: ['HOST', 'SENDER'], availableReplyToSources: ['NONE', 'CONTACT', 'SENDER'],
    },
  });
  previewEmailContactBlock.mockResolvedValue({ data: { html: '', rendersBlock: false } });
  getEmailTemplateList.mockResolvedValue({
    data: { items: [{ emailTemplateId: 7, templateCode: 'ACCOUNT_EMAIL_CONFIRMATION', name: 'Xác nhận email tài khoản', status: 'ACTIVE' }] },
  });
  getEmailTemplateDetail.mockResolvedValue({ data: DETAIL });
  getEmailTemplateContract.mockResolvedValue({ data: ACCOUNT_CONTRACT });
  updateEmailTemplate.mockResolvedValue({ data: { success: true, revision: 5, updatedAt: '2026-07-30T10:00:00+07:00' } });
  restoreEmailTemplateDefault.mockResolvedValue({
    data: {
      success: true, revision: 5, updatedAt: '2026-07-30T10:00:00+07:00',
      name: 'Tên hệ thống', description: 'Mô tả hệ thống',
      subjectVi: '[PEMS] Tiêu đề hệ thống', bodyVi: '<p>Nội dung hệ thống</p>',
      subjectEn: '[PEMS] System subject', bodyEn: '<p>System body</p>',
    },
  });
});

const openEditor = async () => {
  render(<TemplateManagement pushToast={pushToast} />);
  const edit = await screen.findByLabelText('Chỉnh sửa ACCOUNT_EMAIL_CONFIRMATION');
  fireEvent.click(edit);
  await screen.findByLabelText('Tên mẫu *');
};

/**
 * The ONE save on this screen. It writes the content fields and the contact settings together, in one
 * request the server runs as one transaction.
 *
 * It was "Lưu thay đổi mẫu" while card 4 carried a second save button of its own, because a bare "Lưu"
 * next to another save named neither its scope nor its difference. With one button the qualifier is
 * noise, and its absence is asserted below.
 */
const saveButton = () => screen.getByRole('button', { name: /Lưu thay đổi/ });

/**
 * Makes one real change to the content, which is what the save button now requires.
 *
 * Not a workaround for the test: the button is disabled until something differs from what is stored,
 * because a save with nothing to save still bumps the revision and writes an audit row claiming the
 * content changed. Every test that saves has to change something first, exactly as an operator does.
 */
const editName = (value = 'Tên mẫu đã sửa') =>
  fireEvent.change(screen.getByLabelText('Tên mẫu *'), { target: { value } });

describe('TemplateManagement — the catalog is fixed (G11-I)', () => {
  it('offers no way to create a template', async () => {
    render(<TemplateManagement pushToast={pushToast} />);
    await screen.findByText('ACCOUNT_EMAIL_CONFIRMATION');

    expect(screen.queryByText('Thêm mẫu mới')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /thêm mẫu/i })).not.toBeInTheDocument();
  });

  /**
   * The status column was replaced by the admin description (2026-08-03). The guarantee it carried is
   * unchanged and asserted here directly: deactivating a system template breaks the feature that sends
   * it, so the list offers no control that could. Asserting the column is gone is not enough — a toggle
   * added anywhere else in the row would slip past that.
   */
  it('offers no control for switching a template off', async () => {
    render(<TemplateManagement pushToast={pushToast} />);
    await screen.findByText('ACCOUNT_EMAIL_CONFIRMATION');

    expect(screen.queryByRole('switch')).not.toBeInTheDocument();
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /tạm khóa|kích hoạt|vô hiệu/i })).not.toBeInTheDocument();

    // The only per-row action is opening the editor.
    const rowButtons = screen.getAllByRole('button').filter(b => b.closest('tbody'));
    expect(rowButtons.every(b => /^Chỉnh sửa /.test(b.getAttribute('aria-label') ?? ''))).toBe(true);
  });

  it('shows the admin description for each template', async () => {
    render(<TemplateManagement pushToast={pushToast} />);
    await screen.findByText('ACCOUNT_EMAIL_CONFIRMATION');

    expect(screen.getByText('Mô tả quản trị')).toBeInTheDocument();
  });

  it('shows the template code as locked, with no input to change it', async () => {
    await openEditor();

    // Shown in several places (heading, locked field, sidebar note) — all of them read-only.
    expect(screen.getAllByText('ACCOUNT_EMAIL_CONFIRMATION').length).toBeGreaterThan(0);

    // The old form had a "Mã mẫu *" text input; there must be no editable field for it now.
    expect(screen.queryByLabelText(/Mã mẫu \*/)).not.toBeInTheDocument();
    expect(document.querySelector('input[value="ACCOUNT_EMAIL_CONFIRMATION"]')).toBeNull();
  });

  it('sends the content fields, the contact settings and the concurrency token — and nothing else', async () => {
    await openEditor();
    editName();
    fireEvent.click(saveButton());

    await waitFor(() => expect(updateEmailTemplate).toHaveBeenCalled());

    const [, payload] = updateEmailTemplate.mock.calls[0];
    expect(Object.keys(payload).sort()).toEqual(
      ['bodyEn', 'bodyVi', 'contactSettings', 'description', 'expectedRevision',
       'name', 'subjectEn', 'subjectVi'],
    );
    expect(payload).not.toHaveProperty('templateCode');
    expect(payload).not.toHaveProperty('status');
    expect(payload).not.toHaveProperty('purpose');
    expect(payload).not.toHaveProperty('variablesText');
  });

  /**
   * §3.1 and §5 of the atomic-save prompt: one request, not two calls made to look like one.
   *
   * The most valuable assertion in this file, because the failure it guards against is invisible on
   * screen — a footer with one button can perfectly well call two endpoints in a row, and then the
   * second one failing leaves the first one written.
   */
  it('writes both halves through one call, and never the standalone contact endpoints', async () => {
    await openEditor();
    await screen.findByTestId('contact-settings-panel');

    editName();
    // A change in each half, made in one editing session. The point of the assertion below is that they
    // leave together: two calls would mean either could land without the other.
    fireEvent.click(screen.getByTestId('contact-requirement-OPTIONAL'));
    fireEvent.click(saveButton());

    await waitFor(() => expect(updateEmailTemplate).toHaveBeenCalledTimes(1));

    const [, payload] = updateEmailTemplate.mock.calls[0];
    expect(payload.name).toBe('Tên mẫu đã sửa');
    expect(payload.contactSettings).toMatchObject({ requirement: 'OPTIONAL' });

    expect(updateEmailContactSettings).not.toHaveBeenCalled();
    expect(restoreEmailContactSettingsDefault).not.toHaveBeenCalled();
  });

  /**
   * The token is the server's revision, sent back exactly as received. It replaced updated_at, which
   * could not tell two saves inside the same second apart.
   */
  it('sends the revision the server gave it, unmodified', async () => {
    await openEditor();
    editName();
    fireEvent.click(saveButton());

    await waitFor(() => expect(updateEmailTemplate).toHaveBeenCalled());
    expect(updateEmailTemplate.mock.calls[0][1].expectedRevision).toBe(DETAIL.revision);
  });

  /** The old form overwrote the English content with the Vietnamese text on every save. */
  it('does not overwrite the English content with the Vietnamese one', async () => {
    await openEditor();
    editName();
    fireEvent.click(saveButton());

    await waitFor(() => expect(updateEmailTemplate).toHaveBeenCalled());

    const [, payload] = updateEmailTemplate.mock.calls[0];
    expect(payload.subjectEn).toBe(DETAIL.subjectEn);
    expect(payload.bodyEn).toBe(DETAIL.bodyEn);
    expect(payload.subjectEn).not.toBe(payload.subjectVi);
  });
});

describe('TemplateManagement — the variable contract (G11-J)', () => {
  it('asks the backend for the contract of the template being opened', async () => {
    await openEditor();
    await waitFor(() =>
      expect(getEmailTemplateContract).toHaveBeenCalledWith('ACCOUNT_EMAIL_CONFIRMATION', 'VI'));
  });

  /** The reported defect: a canonical template opened with a warning on every variable it used. */
  it('opens a canonical template with no warnings at all', async () => {
    await openEditor();
    await screen.findByText('Họ tên người nhận');

    expect(screen.queryByTestId('issues-subjectVi')).not.toBeInTheDocument();
    expect(screen.queryByTestId('issues-bodyVi')).not.toBeInTheDocument();
    expect(screen.queryByText(/chưa được định nghĩa hoặc sai định dạng/i)).not.toBeInTheDocument();
  });

  it('offers only this template’s variables in the sidebar', async () => {
    await openEditor();
    await screen.findByText('Họ tên người nhận');

    for (const label of ['Vai trò', 'Cơ sở', 'Hiệu lực (giờ)']) {
      expect(screen.getByText(label)).toBeInTheDocument();
    }

    // The logistics labels the old hard-coded sidebar showed on every template.
    for (const label of ['Tên yêu cầu hậu cần', 'Trưởng phòng ban', 'Bắt đầu sử dụng']) {
      expect(screen.queryByText(label)).not.toBeInTheDocument();
    }
  });

  it('never offers the action block as an insertable variable', async () => {
    await openEditor();
    await screen.findByText('Họ tên người nhận');

    // Not in the VARIABLE sidebar. It is listed in the system-block notice instead, which says the
    // backend fills it in — a different claim from "here is a field you may supply".
    const sidebar = screen.getByTestId('variable-sidebar');
    expect(within(sidebar).queryByText('actionBlock')).not.toBeInTheDocument();
  });

  /**
   * System blocks are shown as protected regions, with the block's own name and what fills it.
   *
   * Only {{actionBlock}} was ever named on this screen, so {{contactInformationBlock}} — mandatory on
   * fourteen templates — appeared nowhere: an operator rewording a paragraph deleted it without being
   * told it existed, and met the refusal afterwards.
   */
  it('shows every system block as a protected region, required ones marked', async () => {
    getEmailTemplateContract.mockResolvedValue({
      data: {
        ...ACCOUNT_CONTRACT,
        requiredSystemBlocks: ['contactInformationBlock'],
        optionalSystemBlocks: ['actionBlock'],
      },
    });

    await openEditor();

    const notice = await screen.findByTestId('system-blocks-notice');
    expect(notice).toHaveTextContent('{{contactInformationBlock}}');
    expect(notice).toHaveTextContent('bắt buộc giữ');
    expect(notice).toHaveTextContent('{{actionBlock}}');
    expect(notice).toHaveTextContent('tùy chọn');

    expect(screen.getByTestId('system-block-required-contactInformationBlock')).toBeInTheDocument();
    expect(screen.getByTestId('system-block-optional-actionBlock')).toBeInTheDocument();
  });

  /**
   * The editor and the preview answer different questions, so they show different things.
   *
   * The operator needs the PLACEHOLDER in the content — it is the only handle they have on where the
   * block sits — and the RENDERED buttons in the preview, because that is what a recipient receives.
   * Substituting in the editor would delete their handle; leaving the raw braces in the preview reads
   * as an unresolved variable, which is the confusion this whole change removes.
   */
  it('keeps the placeholder in the editor and shows the sample button in the preview', async () => {
    await openEditor();
    await screen.findByText('Họ tên người nhận');

    fireEvent.change(screen.getByTestId('quill'), {
      target: { value: '<p>Chào {{fullName}}.</p>{{actionBlock}}' },
    });

    // Editor: the placeholder survives, untouched.
    await waitFor(() =>
      expect((screen.getByTestId('quill') as HTMLTextAreaElement).value).toContain('{{actionBlock}}'));

    // Preview: the button markup, and no placeholder left behind.
    const preview = screen.getByTestId('preview-body');
    await waitFor(() => {
      expect(preview.innerHTML).toContain('Chấp nhận');
      expect(preview.innerHTML).toContain('Từ chối');
      expect(preview.textContent).not.toContain('{{actionBlock}}');
    });

    // A preview mints nothing: no anchor, so a click cannot navigate anywhere.
    expect(preview.querySelector('a')).toBeNull();
    expect(preview.innerHTML).not.toMatch(/https?:\/\//i);
  });

  /**
   * The reported defect itself, at the screen: a legal block must not be reported as an unknown
   * VARIABLE. Before the split this produced "Biến {{contactInformationBlock}} không tồn tại trong hệ
   * thống" on a template where the block is required.
   */
  it('does not flag a legal system block written into the body', async () => {
    getEmailTemplateContract.mockResolvedValue({
      data: {
        ...ACCOUNT_CONTRACT,
        requiredSystemBlocks: [],
        optionalSystemBlocks: ['actionBlock', 'contactInformationBlock'],
        contactSupported: true,
        contactRequirement: 'OPTIONAL',
      },
    });
    // The LEVEL has to permit it too, not merely the capability. Under OPTIONAL a body may carry the
    // block or drop it, which is the state this test is about; the NONE case is its own test below,
    // because there the block IS a fault and reporting nothing would be the defect.
    getEmailContactSettings.mockResolvedValue({
      data: {
        templateCode: 'ACCOUNT_EMAIL_CONFIRMATION', requirement: 'OPTIONAL', contactSource: 'SENDER',
        showEmail: true, showPhone: true, showDepartment: false, showCampus: false, showSender: false,
        headingVi: '', headingEn: '', replyToSource: 'NONE',
        blockPlaceholder: '{{contactInformationBlock}}',
        bodyCarriesBlockVi: false, bodyCarriesBlockEn: false,
        availableRequirements: ['NONE', 'OPTIONAL', 'REQUIRED'],
        availableSources: ['HOST', 'SENDER'], availableReplyToSources: ['NONE', 'CONTACT', 'SENDER'],
        capability: 'SUPPORTED',
      },
    });

    await openEditor();
    await screen.findByTestId('contact-settings-panel');
    await screen.findByText('Họ tên người nhận');

    fireEvent.change(screen.getByTestId('quill'), {
      target: { value: '<p>Chào {{fullName}}.</p>{{contactInformationBlock}}' },
    });

    await waitFor(() => {
      expect(screen.queryByTestId('issues-bodyVi')).not.toBeInTheDocument();
    });
  });

  it('flags a variable from another module, naming the field and the variable', async () => {
    await openEditor();
    await screen.findByText('Họ tên người nhận');

    fireEvent.change(screen.getByTestId('quill'), {
      target: { value: '<p>{{logisticsTitle}}</p>' },
    });

    const issues = await screen.findByTestId('issues-bodyVi');
    expect(issues).toHaveTextContent('logisticsTitle');
    // The code is matched on the ATTRIBUTE, not in the sentence. It used to be rendered as visible text
    // butted straight against the action button's label — "…NOT_ALLOWEDXóa khối không hợp lệ" — which is
    // neither readable nor something an operator can act on. It stays in the DOM for tests and support.
    expect(issues.querySelector('[data-error-code="EMAIL_TEMPLATE_VARIABLE_UNKNOWN"]')).not.toBeNull();
    expect(issues.textContent).not.toMatch(/EMAIL_TEMPLATE_[A-Z_]+/);
  });

  it('disables the save button while a blocking issue is present', async () => {
    await openEditor();
    await screen.findByText('Họ tên người nhận');

    // A real edit, so the button is not merely disabled for having nothing to save.
    fireEvent.change(screen.getByTestId('quill'), { target: { value: '<p>{{ghost}}</p>' } });

    await waitFor(() => expect(saveButton()).toBeDisabled());
    expect(updateEmailTemplate).not.toHaveBeenCalled();
  });

  /**
   * Validation must not run before the contract lands. The previous screen validated from its first
   * render, so the warning was on screen before any request had been made.
   */
  it('shows no warnings while the contract is still loading', async () => {
    let resolve!: (v: unknown) => void;
    getEmailTemplateContract.mockReturnValue(new Promise(r => { resolve = r; }));

    await openEditor();

    expect(screen.getByTestId('contract-loading')).toBeInTheDocument();
    expect(screen.queryByTestId('issues-bodyVi')).not.toBeInTheDocument();

    resolve({ data: ACCOUNT_CONTRACT });
    await screen.findByText('Họ tên người nhận');
  });

  it('does not validate against a stale contract when the request fails', async () => {
    getEmailTemplateContract.mockRejectedValue(new Error('network'));

    await openEditor();
    await screen.findByTestId('contract-error');

    // No contract means no verdict — showing warnings derived from the wrong template is worse
    // than showing none.
    expect(screen.queryByTestId('issues-bodyVi')).not.toBeInTheDocument();
  });

  it('warns that a secret-bearing template cannot be copied', async () => {
    await openEditor();
    await screen.findByText(/không được phép CC\/BCC/);
  });

  it('surfaces backend field-level issues after a refused save', async () => {
    updateEmailTemplate.mockRejectedValue({
      response: {
        data: {
          errorCode: 'EMAIL_TEMPLATE_VARIABLE_UNKNOWN',
          issues: [{
            field: 'bodyVi',
            code: 'EMAIL_TEMPLATE_VARIABLE_UNKNOWN',
            variableName: 'serverOnlyGhost',
            messageVi: 'Biến không thuộc mẫu này.',
            messageEn: 'Variable does not belong.',
            severity: 'ERROR',
          }],
        },
      },
    });

    await openEditor();
    await screen.findByText('Họ tên người nhận');
    editName();
    fireEvent.click(saveButton());

    const issues = await screen.findByTestId('issues-bodyVi');
    expect(issues).toHaveTextContent('serverOnlyGhost');
  });

  it('reports a concurrency conflict in its own words', async () => {
    updateEmailTemplate.mockRejectedValue({
      response: {
        data: {
          errorCode: 'EMAIL_TEMPLATE_CONCURRENCY_CONFLICT',
          message: 'Mẫu email này đã được người khác cập nhật sau khi bạn mở nó.',
        },
      },
    });

    await openEditor();
    await screen.findByText('Họ tên người nhận');
    editName();
    fireEvent.click(saveButton());

    await waitFor(() =>
      expect(pushToast).toHaveBeenCalledWith('error', expect.stringContaining('người khác cập nhật')));
  });

  it('previews with the backend’s samples, not names compiled into the screen', async () => {
    await openEditor();
    await screen.findByText('Họ tên người nhận');

    // The sample the API returned, substituted into the preview pane.
    await waitFor(() => expect(screen.getByText(/Nguyễn Văn An/)).toBeInTheDocument());
  });
});

/**
 * Restore to default (G11-I).
 *
 * The catalog is fixed, so an operator who edits a template into an unusable state cannot create a
 * replacement and cannot delete it. Until this existed the only way back was for somebody with database
 * access to re-run a SQL script, which is not something an HO operator can do.
 */
describe('TemplateManagement — restore to default (G11-I)', () => {
  const openAndRestore = async () => {
    await openEditor();
    fireEvent.click(screen.getByTestId('restore-default'));
    // The confirmation is deliberate: this discards work rather than saving it. The dialog's button is
    // named for the action rather than "Xác nhận", so the last thing an operator reads before losing an
    // afternoon's wording says what they are about to do.
    fireEvent.click(await screen.findByRole('button', { name: 'Khôi phục mặc định' }));
  };

  it('offers restore for a template that ships a default', async () => {
    await openEditor();
    expect(screen.getByTestId('restore-default')).toBeInTheDocument();
  });

  it('does not offer restore when the backend ships no default for the code', async () => {
    getEmailTemplateDetail.mockResolvedValue({ data: { ...DETAIL, hasShippedDefault: false } });

    await openEditor();
    expect(screen.queryByTestId('restore-default')).not.toBeInTheDocument();
  });

  it('does not offer restore without a revision to restore against', async () => {
    // No revision means no concurrency token, and a restore without one is an unconditional overwrite.
    getEmailTemplateDetail.mockResolvedValue({ data: { ...DETAIL, revision: undefined } });

    await openEditor();
    expect(screen.queryByTestId('restore-default')).not.toBeInTheDocument();
  });

  it('asks for confirmation before discarding the operator’s wording', async () => {
    await openEditor();
    fireEvent.click(screen.getByTestId('restore-default'));

    // Nothing is sent until the confirmation is answered.
    expect(restoreEmailTemplateDefault).not.toHaveBeenCalled();

    // §12 of the atomic-save prompt: the dialog itemises what goes. "Restore defaults" on a screen with
    // four groups of fields does not say which of them it means, and an operator who spent an afternoon
    // on the Vietnamese copy must not discover the answer afterwards. The code is matched inside the
    // dialog text rather than on the page, where it legitimately appears several times (heading, locked
    // field, sidebar note).
    expect(await screen.findByRole('heading', { name: 'Khôi phục toàn bộ mẫu?' })).toBeInTheDocument();

    const dialogText = screen.getByText(/Thao tác này sẽ khôi phục mẫu "ACCOUNT_EMAIL_CONFIRMATION"/);
    expect(dialogText.textContent).toMatch(/Tên và mô tả mẫu/);
    expect(dialogText.textContent).toMatch(/Tiêu đề và nội dung tiếng Việt/);
    expect(dialogText.textContent).toMatch(/Tiêu đề và nội dung tiếng Anh/);
    // The fourth item, because this fixture's contract does not say the block is unsupported.
    expect(dialogText.textContent).toMatch(/Cấu hình thông tin liên hệ và Reply-To/);
  });

  /**
   * §8: an unsupported template has no contact configuration, so the dialog must not promise to restore
   * one. Saying it anyway would be a promise the transaction does not keep — and would leave an operator
   * expecting a policy change that was never possible.
   */
  it('says so instead when the template has no contact configuration', async () => {
    getEmailTemplateContract.mockResolvedValue({
      data: { ...ACCOUNT_CONTRACT, contactSupported: false },
    });

    await openEditor();
    fireEvent.click(screen.getByTestId('restore-default'));

    const dialogText = await screen.findByText(/Thao tác này sẽ khôi phục mẫu/);
    expect(dialogText.textContent).toMatch(/không dùng khối thông tin liên hệ/);
    expect(dialogText.textContent).not.toMatch(/• Cấu hình thông tin liên hệ và Reply-To/);
  });

  it('sends the template id and the revision it was shown', async () => {
    await openAndRestore();

    await waitFor(() => expect(restoreEmailTemplateDefault).toHaveBeenCalled());
    expect(restoreEmailTemplateDefault.mock.calls[0][0]).toBe(DETAIL.emailTemplateId);
    expect(restoreEmailTemplateDefault.mock.calls[0][1]).toBe(DETAIL.revision);
  });

  it('shows the restored content and keeps the new revision', async () => {
    await openAndRestore();

    await waitFor(() => expect(pushToast).toHaveBeenCalledWith('success', expect.stringContaining('khôi phục')));

    // The operator's discarded text must not stay on screen next to a bumped revision — that would
    // invite them to "save" it straight back.
    await waitFor(() =>
      expect((screen.getByLabelText('Tên mẫu *') as HTMLInputElement).value).toBe('Tên hệ thống'));

    // The next save carries the revision the restore returned, not the one it replaced. The restored
    // content is what is now STORED, so a further edit is needed before there is anything to save —
    // which is also the guarantee that restore left nothing pending behind it.
    expect(saveButton()).toBeDisabled();
    editName('Tên hệ thống, đã sửa lại');
    fireEvent.click(saveButton());
    await waitFor(() => expect(updateEmailTemplate).toHaveBeenCalled());
    expect(updateEmailTemplate.mock.calls[0][1].expectedRevision).toBe(5);
  });

  it('reports a concurrency conflict and does not retry', async () => {
    restoreEmailTemplateDefault.mockRejectedValue({
      response: { status: 409, data: { errorCode: 'EMAIL_TEMPLATE_CONCURRENCY_CONFLICT', message: 'Đã có người khác cập nhật.' } },
    });

    await openAndRestore();

    await waitFor(() => expect(pushToast).toHaveBeenCalledWith('error', expect.stringContaining('người khác')));
    // Restoring harder would overwrite exactly the change the conflict warns about.
    expect(restoreEmailTemplateDefault).toHaveBeenCalledTimes(1);
  });

  it('reports a missing default with its own message', async () => {
    restoreEmailTemplateDefault.mockRejectedValue({
      response: { status: 409, data: { errorCode: 'EMAIL_TEMPLATE_DEFAULT_UNAVAILABLE' } },
    });

    await openAndRestore();

    await waitFor(() =>
      expect(pushToast).toHaveBeenCalledWith('error', expect.stringContaining('không có nội dung mặc định')));
  });
});

/**
 * The whole catalog has to arrive.
 *
 * `ViewEmailTemplateListQuery` defaults to PageSize = 10 and this screen called it with no arguments,
 * so it showed the first ten of thirty. That is not a paging cosmetic: filtering here runs over what
 * was loaded, so searching for one of the missing twenty answered "Không tìm thấy mẫu email nào" — and
 * this is the only screen offering Chỉnh sửa and Phục hồi mặc định, both HO-only. Twenty system
 * templates could not be corrected or restored by anyone, which is exactly the repair path the fixed
 * catalog is supposed to guarantee.
 */
describe('TemplateManagement — the whole catalog is loaded', () => {
  const catalogOf = (n: number) =>
    Array.from({ length: n }, (_, i) => ({
      emailTemplateId: i + 1,
      templateCode: `TEMPLATE_${String(i + 1).padStart(2, '0')}`,
      name: `Mẫu ${i + 1}`,
      status: 'ACTIVE',
    }));

  /**
   * Answers the way the real endpoint does — including its DEFAULT of ten when no page size is asked
   * for. Without that default the mock hands back all thirty whatever the caller requested, and the
   * two tests below would pass against the very defect they exist to catch.
   */
  const servingCatalog = (total: number) => {
    const all = catalogOf(total);
    return (params?: { page?: number; pageSize?: number }) => {
      const pageSize = params?.pageSize && params.pageSize > 0 ? params.pageSize : 10;
      const page = params?.page && params.page > 0 ? params.page : 1;
      return Promise.resolve({
        data: {
          templates: all.slice((page - 1) * pageSize, page * pageSize),
          totalItems: total,
          totalPages: Math.ceil(total / pageSize),
        },
      });
    };
  };

  it('asks for a page big enough to hold the catalog, not the default ten', async () => {
    getEmailTemplateList.mockImplementation(servingCatalog(30));

    render(<TemplateManagement pushToast={pushToast} />);
    await screen.findByText('TEMPLATE_01');

    expect(getEmailTemplateList).toHaveBeenCalledTimes(1);
    const [params] = getEmailTemplateList.mock.calls[0] as [{ page?: number; pageSize?: number }];
    expect(params, 'the request must carry an explicit page size').toBeDefined();
    expect(params.pageSize).toBeGreaterThanOrEqual(30);
  });

  /**
   * The whole catalog is still LOADED in one request; it is now PAGED in the browser, ten rows at a
   * time. That is a display choice and it must not become the server-side truncation this suite exists
   * to catch — so the guarantee is asserted the only way it still can be: by walking to the last page
   * and finding the last template there.
   */
  it('holds every template in the catalog, and the last one is reachable by paging', async () => {
    getEmailTemplateList.mockImplementation(servingCatalog(30));

    render(<TemplateManagement pushToast={pushToast} />);
    await screen.findByText('TEMPLATE_01');

    // The footer counts the whole catalog, not the current page.
    expect(screen.getByText(/trong/).textContent).toMatch(/30\s*mẫu/);

    // The 30th is the one a PageSize=10 REQUEST loses for good. Paging to it proves it was loaded.
    expect(screen.queryByText('TEMPLATE_30')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '3' }));

    expect(screen.getByText('TEMPLATE_30')).toBeInTheDocument();
    expect(getEmailTemplateList).toHaveBeenCalledTimes(1);   // paging is local; no second fetch
  });

  it('shows the whole catalog on one page when the page size is raised', async () => {
    getEmailTemplateList.mockImplementation(servingCatalog(30));

    render(<TemplateManagement pushToast={pushToast} />);
    await screen.findByText('TEMPLATE_01');

    fireEvent.change(screen.getByLabelText('Hiển thị:'), { target: { value: '50' } });

    expect(screen.getAllByLabelText(/^Chỉnh sửa TEMPLATE_/)).toHaveLength(30);
    expect(screen.getByText('TEMPLATE_30')).toBeInTheDocument();
  });

  it('finds a template that a ten-row page would never have loaded', async () => {
    getEmailTemplateList.mockImplementation(servingCatalog(30));

    render(<TemplateManagement pushToast={pushToast} />);
    await screen.findByText('TEMPLATE_01');

    fireEvent.change(screen.getByLabelText('Tìm kiếm mẫu'), { target: { value: 'TEMPLATE_27' } });

    expect(screen.getByText('TEMPLATE_27')).toBeInTheDocument();
    expect(screen.queryByText('Không tìm thấy mẫu email nào')).not.toBeInTheDocument();
  });

  it('says so on screen when the server reports more templates than it returned', async () => {
    getEmailTemplateList.mockResolvedValue({ data: { templates: catalogOf(10), totalItems: 30, totalPages: 3 } });

    render(<TemplateManagement pushToast={pushToast} />);

    const warning = await screen.findByTestId('catalog-truncated');
    expect(warning).toHaveTextContent('10/30');
    // The list is still shown — hiding it would remove the ten that DID load — but it is labelled
    // incomplete, so "not found" can never again be mistaken for "does not exist".
    expect(screen.getByText('TEMPLATE_01')).toBeInTheDocument();
  });

  it('shows no warning when the catalog arrived whole', async () => {
    getEmailTemplateList.mockImplementation(servingCatalog(30));

    render(<TemplateManagement pushToast={pushToast} />);
    await screen.findByText('TEMPLATE_01');

    expect(screen.queryByTestId('catalog-truncated')).not.toBeInTheDocument();
  });
});

/**
 * Historical rows — templates that are in `email_templates` but not in the code registry.
 *
 * The reported symptom was VISIT_REQUEST_APPROVED opening with an error on every variable it uses.
 * The cause: the contract API answers for an unregistered code with `isSystemTemplate: false` and
 * EMPTY variable lists, and the screen validated against those lists all the same — so every
 * placeholder in the body was "not part of this template", and the resulting blocking issues also
 * disabled the save button. An operator could not tell that apart from a corrupt template.
 */
describe('TemplateManagement — historical (non-system) templates', () => {
  // Both rows, because that is what an upgraded database looks like: canonical templates alongside
  // the legacy ones their sent history still points at. It also lets a test open one after the other
  // without a refetch, which is exactly the state-leak case worth covering.
  const MIXED_CATALOG = {
    data: {
      items: [
        { emailTemplateId: 2, templateCode: 'VISIT_REQUEST_APPROVED', name: 'Thông báo duyệt đơn thăm', status: 'ACTIVE' },
        { emailTemplateId: 7, templateCode: 'ACCOUNT_EMAIL_CONFIRMATION', name: 'Xác nhận email tài khoản', status: 'ACTIVE' },
      ],
    },
  };

  const openHistorical = async () => {
    getEmailTemplateList.mockResolvedValue(MIXED_CATALOG);
    getEmailTemplateDetail.mockResolvedValue({ data: HISTORICAL_DETAIL });
    getEmailTemplateContract.mockResolvedValue({ data: HISTORICAL_CONTRACT });

    render(<TemplateManagement pushToast={pushToast} />);
    fireEvent.click(await screen.findByLabelText('Chỉnh sửa VISIT_REQUEST_APPROVED'));
    await screen.findByTestId('contract-not-system');
  };

  it('does not mark every variable in the body as unknown', async () => {
    await openHistorical();

    // Asserted on the issue lists and on the error copy, not on the placeholders themselves: the
    // preview legitimately renders {{RequestCode}} verbatim, because a historical template has no
    // samples to substitute into it. Seeing the text is not the defect; being told it is WRONG is.
    expect(screen.queryByText(/EMAIL_TEMPLATE_VARIABLE_UNKNOWN/)).not.toBeInTheDocument();
    expect(screen.queryByText(/không thuộc mẫu/)).not.toBeInTheDocument();
    expect(screen.queryByTestId('issues-bodyVi')).not.toBeInTheDocument();
    expect(screen.queryByTestId('issues-subjectVi')).not.toBeInTheDocument();
    expect(screen.queryByTestId('issues-bodyEn')).not.toBeInTheDocument();
    expect(screen.queryByTestId('issues-subjectEn')).not.toBeInTheDocument();
  });

  it('says what the row is, in place of the warnings', async () => {
    await openHistorical();

    const notice = screen.getByTestId('contract-not-system');
    expect(notice).toHaveTextContent('chỉ xem');
    expect(notice).toHaveTextContent('không còn thuộc danh mục mẫu hệ thống');
    // Names the reason the variables are unchecked, rather than leaving it to be inferred.
    expect(notice).toHaveTextContent('không thể kiểm tra biến');
  });

  it('presents the content read-only, matching what the API will accept', async () => {
    await openHistorical();

    expect(screen.getByLabelText('Tên mẫu *')).toBeDisabled();
    expect(screen.getByLabelText('Mô tả quản trị')).toBeDisabled();
    expect(screen.getByLabelText('Tiêu đề (Subject)')).toBeDisabled();
    expect(saveButton()).toBeDisabled();
  });

  it('never calls the update endpoint for a historical row', async () => {
    await openHistorical();

    // The button is disabled, so reach the handler the way a keyboard user would.
    fireEvent.submit(screen.getByLabelText('Tên mẫu *').closest('form')!);

    await waitFor(() => expect(pushToast).toHaveBeenCalledWith('error', expect.stringContaining('lịch sử')));
    expect(updateEmailTemplate).not.toHaveBeenCalled();
  });

  it('does not offer contact settings for a code no sender uses', async () => {
    await openHistorical();

    expect(screen.queryByText('4. Cấu hình thông tin liên hệ')).not.toBeInTheDocument();
    expect(getEmailContactSettings).not.toHaveBeenCalled();
  });

  it('still fails closed on a system template opened afterwards', async () => {
    await openHistorical();

    // Going back to the list and opening a registered template must restore full validation — the
    // read-only state belongs to the row, not to the screen.
    getEmailTemplateDetail.mockResolvedValue({
      data: { ...DETAIL, bodyVi: '<p>Chào {{fullName}} — {{logisticsTitle}}</p>' },
    });
    getEmailTemplateContract.mockResolvedValue({ data: ACCOUNT_CONTRACT });

    fireEvent.click(screen.getByLabelText('Đóng'));
    fireEvent.click(await screen.findByLabelText('Chỉnh sửa ACCOUNT_EMAIL_CONFIRMATION'));

    const issues = await screen.findByTestId('issues-bodyVi');
    expect(issues).toHaveTextContent('logisticsTitle');
    // Dirty and blocked, so the disabled state is the validation's and not "nothing to save".
    editName();
    await waitFor(() => expect(saveButton()).toBeDisabled());
    expect(screen.queryByTestId('contract-not-system')).not.toBeInTheDocument();
  });
});

/**
 * The action block is described ONCE, by whoever knows this template (§4).
 *
 * The reported defect: opening ACCOUNT_EMAIL_CONFIRMATION showed two descriptions of {{actionBlock}},
 * one under the other — the backend's ("hệ thống sẽ chèn nút Xác nhận email") and the screen's generic
 * one ("đồng ý / từ chối / xem chi tiết"). The second described buttons this template does not have, on
 * a block that appears once in the body, so an operator could not tell which of the two was true.
 */
describe('TemplateManagement — one description per system block (§4)', () => {
  /** As the backend now answers for it: a registered confirm action, and no contact block at all. */
  const CONFIRMATION_CONTRACT = {
    ...ACCOUNT_CONTRACT,
    requiredSystemBlocks: ['actionBlock'],
    optionalSystemBlocks: [],
    actionSupported: true,
    actionRequired: true,
    systemActionDescription:
      'Nút "Xác nhận email" (kèm liên kết một lần) sẽ được hệ thống tự gắn khi gửi email. '
      + 'Liên kết kích hoạt tài khoản và chỉ dùng được một lần.',
    contactSupported: false,
    contactRequired: false,
    contactSettingsEditable: false,
    contactReasonCode: 'ONE_TIME_CREDENTIAL',
    contactReasonVi: 'Mẫu này không dùng khối thông tin liên hệ vì email chứa liên kết xác nhận dùng một lần.',
  };

  /** The shipped body: it writes the placeholder, which is why the block is required. */
  const CONFIRMATION_DETAIL = {
    ...DETAIL,
    bodyVi: '<p>Chào {{fullName}}, vai trò {{roleName}} tại {{campusName}}.</p>{{actionBlock}}',
    bodyEn: '<p>Hello {{fullName}}, role {{roleName}} at {{campusName}}.</p>{{actionBlock}}',
  };

  const openConfirmation = async () => {
    getEmailTemplateDetail.mockResolvedValue({ data: CONFIRMATION_DETAIL });
    getEmailTemplateContract.mockResolvedValue({ data: CONFIRMATION_CONTRACT });
    await openEditor();
    await screen.findByTestId('system-blocks-notice');
  };

  it('describes the action block with the backend wording for THIS template', async () => {
    await openConfirmation();

    const notice = screen.getByTestId('system-blocks-notice');
    expect(notice).toHaveTextContent('Xác nhận email');
    expect(notice).toHaveTextContent('bắt buộc giữ');
  });

  it('does not also show the generic hint next to it', async () => {
    await openConfirmation();

    // The generic sentence from SYSTEM_BLOCK_LABELS. It is not wrong in general — it is wrong HERE,
    // and showing both is what left an operator with two answers to one question.
    expect(screen.queryByText(/đồng ý \/ từ chối \/ xem chi tiết/)).not.toBeInTheDocument();
  });

  it('lists each block exactly once', async () => {
    await openConfirmation();

    const entries = screen.getByTestId('system-blocks-notice').querySelectorAll('li');
    expect(entries).toHaveLength(1);
    expect(screen.getAllByTestId('system-block-required-actionBlock')).toHaveLength(1);
    expect(screen.queryByTestId('system-block-optional-actionBlock')).not.toBeInTheDocument();
  });

  /**
   * A template whose send path attaches no action must not be described as if it did. The backend leaves
   * the block out of both lists for those, and nothing on this screen may put it back.
   */
  it('shows no action block for a template with no action spec', async () => {
    getEmailTemplateContract.mockResolvedValue({
      data: {
        ...ACCOUNT_CONTRACT,
        requiredSystemBlocks: [],
        optionalSystemBlocks: [],
        actionSupported: false,
        actionRequired: false,
        systemActionDescription: null,
      },
    });

    await openEditor();
    await screen.findByText('Họ tên người nhận');

    expect(screen.queryByTestId('system-blocks-notice')).not.toBeInTheDocument();
  });

  /** Writing the block into a template that has none is refused, and the refusal names the repair. */
  it('refuses an action block written into a template that has no action', async () => {
    getEmailTemplateContract.mockResolvedValue({
      data: {
        ...ACCOUNT_CONTRACT,
        requiredSystemBlocks: [],
        optionalSystemBlocks: [],
        actionSupported: false,
        actionRequired: false,
      },
    });

    await openEditor();
    await screen.findByText('Họ tên người nhận');

    fireEvent.change(screen.getByTestId('quill'), {
      target: { value: '<p>Chào {{fullName}}.</p>{{actionBlock}}' },
    });

    const issues = await screen.findByTestId('issues-bodyVi');
    expect(issues.querySelector('[data-error-code="EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWED"]')).not.toBeNull();
    expect(saveButton()).toBeDisabled();
  });
});

/**
 * Contact capability (§5).
 *
 * A template that cannot carry {{contactInformationBlock}} shows the reason, not a form. The form was
 * the defect: an operator could pick "Tùy chọn", save it, add the block that setting invites — and be
 * refused with EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWED, with nothing on screen tying the two together.
 */
describe('TemplateManagement — contact capability (§5)', () => {
  const UNSUPPORTED_CONTRACT = {
    ...ACCOUNT_CONTRACT,
    contactSupported: false,
    contactRequired: false,
    contactSettingsEditable: false,
    contactReasonCode: 'ONE_TIME_CREDENTIAL',
    contactReasonVi: 'Mẫu này không dùng khối thông tin liên hệ vì email chứa liên kết xác nhận dùng một lần.',
  };

  const openUnsupported = async () => {
    getEmailTemplateContract.mockResolvedValue({ data: UNSUPPORTED_CONTRACT });
    getEmailContactSettings.mockResolvedValue({
      data: {
        templateCode: 'ACCOUNT_EMAIL_CONFIRMATION', requirement: 'NONE', contactSource: 'SUPPORT_CONTACT',
        showEmail: true, showPhone: true, showDepartment: false, showCampus: false, showSender: false,
        headingVi: '', headingEn: '', replyToSource: 'NONE',
        blockPlaceholder: '{{contactInformationBlock}}',
        bodyCarriesBlockVi: false, bodyCarriesBlockEn: false,
        availableRequirements: [], availableSources: [], availableReplyToSources: [],
        capability: 'UNSUPPORTED',
        editable: false,
        capabilityReasonCode: 'ONE_TIME_CREDENTIAL',
        capabilityReasonVi: 'Mẫu này không dùng khối thông tin liên hệ vì email chứa liên kết xác nhận dùng một lần.',
      },
    });
    await openEditor();
    await screen.findByTestId('contact-settings-unsupported');
  };

  it('says why instead of showing the configuration form', async () => {
    await openUnsupported();

    const card = screen.getByTestId('contact-settings-unsupported');
    expect(card).toHaveTextContent('không dùng khối thông tin liên hệ');
    expect(card).toHaveTextContent('liên kết xác nhận dùng một lần');
    expect(card).toHaveTextContent('Không có cấu hình cần chỉnh sửa');
  });

  it('offers none of the controls a supported template has', async () => {
    await openUnsupported();

    expect(screen.queryByRole('radio')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Nguồn thông tin liên hệ')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Reply-To')).not.toBeInTheDocument();
    expect(screen.queryByText('Lưu cấu hình liên hệ')).not.toBeInTheDocument();
    expect(screen.queryByText(/Phục hồi mặc định/)).not.toBeInTheDocument();
  });

  /** §5.4: the block is refused where it cannot resolve, and removing it is offered — never applied. */
  it('refuses the contact block in the body and offers to remove it', async () => {
    await openUnsupported();

    fireEvent.change(screen.getByTestId('quill'), {
      target: { value: '<p>Chào {{fullName}}.</p>{{contactInformationBlock}}' },
    });

    const issues = await screen.findByTestId('issues-bodyVi');
    expect(issues.querySelector('[data-error-code="EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWED"]')).not.toBeNull();
    expect(issues).toHaveTextContent('Hãy xóa khối khỏi nội dung');
    expect(saveButton()).toBeDisabled();

    // Still there: nothing is deleted until the operator asks for it.
    expect((screen.getByTestId('quill') as HTMLTextAreaElement).value)
      .toContain('{{contactInformationBlock}}');

    fireEvent.click(screen.getByTestId('remove-block-bodyVi-contactInformationBlock'));

    await waitFor(() =>
      expect((screen.getByTestId('quill') as HTMLTextAreaElement).value)
        .not.toContain('{{contactInformationBlock}}'));
    await waitFor(() => expect(screen.queryByTestId('issues-bodyVi')).not.toBeInTheDocument());
  });

  /** A supported template still gets the whole form. */
  it('shows the form for a template that supports the block', async () => {
    getEmailTemplateContract.mockResolvedValue({
      data: {
        ...ACCOUNT_CONTRACT,
        contactSupported: true,
        optionalSystemBlocks: ['actionBlock', 'contactInformationBlock'],
      },
    });

    await openEditor();

    await screen.findByTestId('contact-settings-panel');
    expect(screen.queryByTestId('contact-settings-unsupported')).not.toBeInTheDocument();
    expect(screen.getAllByRole('radio').length).toBeGreaterThan(0);
  });
});

/**
 * "Mô tả quản trị" (§7).
 *
 * It was a half-width single-line input, so a description longer than the field was cut off with no way
 * to read the rest — in the editor, which is the one place the whole text has to be visible.
 */
describe('TemplateManagement — the admin description (§7)', () => {
  const LONG = 'Gửi cho tài khoản vừa tạo ở trạng thái chờ xác nhận email. '
    + 'Liên kết chỉ dùng một lần và hết hiệu lực theo thời hạn ghi trong email. '
    + 'Không dùng cho tài khoản đã kích hoạt.';

  it('is a textarea, not a single-line input', async () => {
    await openEditor();

    expect(screen.getByLabelText('Mô tả quản trị').tagName).toBe('TEXTAREA');
  });

  it('holds the whole description and caps it where the API does', async () => {
    getEmailTemplateDetail.mockResolvedValue({ data: { ...DETAIL, description: LONG } });

    await openEditor();

    const field = screen.getByLabelText('Mô tả quản trị') as HTMLTextAreaElement;
    expect(field.value).toBe(LONG);
    // 500 is UpdateEmailTemplateCommandValidator.DescriptionMaxLength and the column width.
    expect(field.maxLength).toBe(500);
    expect(screen.getByTestId('description-counter')).toHaveTextContent(`${LONG.length}/500`);
  });

  it('counts an edit as an unsaved content change', async () => {
    await openEditor();

    fireEvent.change(screen.getByLabelText('Mô tả quản trị'), { target: { value: 'Mô tả mới' } });

    expect(await screen.findByTestId('editor-dirty')).toBeInTheDocument();
    expect(saveButton()).toBeEnabled();
  });
});

/**
 * One save, one restore, one dirty state (§3 of the atomic-save prompt).
 *
 * There used to be two of each: the content and the contact configuration were written by different
 * endpoints, so the footer and card 4 each carried their own button and their own warning. An operator
 * had to remember two saves; either could succeed while the other failed; and the close dialog had to
 * name which of two groups was pending, which is a question that should not have existed.
 */
describe('TemplateManagement — one save, one restore, one dirty state (§3)', () => {
  it('offers exactly one save button and one restore button', async () => {
    await openEditor();
    await screen.findByTestId('contact-settings-panel');

    expect(screen.getAllByRole('button', { name: /Lưu thay đổi/ })).toHaveLength(1);
    expect(screen.getAllByRole('button', { name: /Khôi phục toàn bộ mặc định/ })).toHaveLength(1);

    // And none of the four the footer and card 4 used to carry between them.
    expect(screen.queryByRole('button', { name: /^Cập nhật$/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Lưu cấu hình liên hệ/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Phục hồi nội dung mẫu$/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Phục hồi mặc định$/ })).not.toBeInTheDocument();
  });

  /** One indicator too. Card 4's own "● Cấu hình liên hệ có thay đổi chưa lưu" is gone. */
  it('shows one unsaved-changes indicator, wherever the change was made', async () => {
    await openEditor();
    await screen.findByTestId('contact-settings-panel');

    fireEvent.click(screen.getByTestId('contact-requirement-OPTIONAL'));

    expect(await screen.findByTestId('editor-dirty')).toHaveTextContent('Có thay đổi chưa lưu');
    expect(screen.queryByTestId('contact-settings-dirty')).not.toBeInTheDocument();
    expect(saveButton()).toBeEnabled();
  });

  /**
   * §11: restoring is not conditional on having typed something. Putting a template back to the shipped
   * wording is a valid thing to do on a form nobody has touched — indeed it is the usual case, since the
   * reason to restore is normally something that was saved earlier.
   */
  it('leaves restore enabled on a clean form', async () => {
    await openEditor();
    await screen.findByText('Họ tên người nhận');

    expect(saveButton()).toBeDisabled();
    expect(screen.getByTestId('restore-default')).toBeEnabled();
  });

  it('keeps the save disabled until something has actually changed', async () => {
    await openEditor();
    await screen.findByText('Họ tên người nhận');

    expect(saveButton()).toBeDisabled();
    expect(screen.queryByTestId('editor-dirty')).not.toBeInTheDocument();

    editName();

    expect(await screen.findByTestId('editor-dirty')).toBeInTheDocument();
    expect(saveButton()).toBeEnabled();
  });

  it('reports the save in words that name its scope', async () => {
    await openEditor();
    editName();
    fireEvent.click(saveButton());

    await waitFor(() => expect(updateEmailTemplate).toHaveBeenCalled());
    await waitFor(() =>
      expect(pushToast).toHaveBeenCalledWith('success', expect.stringContaining('Đã lưu thay đổi')));
  });

  it('closes without asking when nothing is pending', async () => {
    await openEditor();
    await screen.findByText('Họ tên người nhận');

    fireEvent.click(screen.getByRole('button', { name: 'Hủy' }));

    // Back to the list, with no confirmation in the way.
    await screen.findByLabelText('Chỉnh sửa ACCOUNT_EMAIL_CONFIRMATION');
    expect(screen.queryByText(/thay đổi chưa lưu/)).not.toBeInTheDocument();
  });

  /**
   * §13: one question, whichever half is pending. It used to name the group, which is a distinction
   * that only mattered because there were two buttons to forget.
   */
  it('asks once when the content is unsaved', async () => {
    await openEditor();
    await screen.findByText('Họ tên người nhận');
    editName();

    fireEvent.click(screen.getByRole('button', { name: 'Hủy' }));

    const dialog = await screen.findByText(/Rời khỏi trang sẽ làm mất các thay đổi này/);
    expect(dialog.textContent).toMatch(/Đóng và bỏ những thay đổi này/);
  });

  it('asks the same question when only the contact settings are unsaved', async () => {
    await openEditor();
    await screen.findByTestId('contact-settings-panel');

    // A level other than the loaded NONE, so the change is entirely in card 4.
    fireEvent.click(screen.getByTestId('contact-requirement-OPTIONAL'));
    await screen.findByTestId('editor-dirty');

    fireEvent.click(screen.getByRole('button', { name: 'Hủy' }));

    expect(await screen.findByText(/Rời khỏi trang sẽ làm mất các thay đổi này/)).toBeInTheDocument();
  });

  /** The corner X is the same door as "Hủy" — it used to close outright, losing the work silently. */
  it('asks on the close icon too', async () => {
    await openEditor();
    await screen.findByText('Họ tên người nhận');
    editName();

    fireEvent.click(screen.getByLabelText('Đóng'));

    expect(await screen.findByText(/Rời khỏi trang sẽ làm mất các thay đổi này/)).toBeInTheDocument();
  });

  /**
   * §10: a failed save leaves the work unsaved, and the screen has to go on saying so.
   *
   * Re-baselining on failure is how a form comes to look clean while holding changes the server has
   * never seen — after which closing it discards them without asking.
   */
  it('keeps the unsaved warning when the save is refused', async () => {
    updateEmailTemplate.mockRejectedValue({
      response: { status: 500, data: { message: 'Máy chủ lỗi.' } },
    });

    await openEditor();
    editName();
    fireEvent.click(saveButton());

    await waitFor(() => expect(pushToast).toHaveBeenCalledWith('error', expect.anything()));

    expect(screen.getByTestId('editor-dirty')).toBeInTheDocument();
    expect(saveButton()).toBeEnabled();
    expect((screen.getByLabelText('Tên mẫu *') as HTMLInputElement).value).toBe('Tên mẫu đã sửa');
  });
});

/**
 * The contradiction between the display level and the body (the contact-block-visibility prompt).
 *
 * The defect: `AllowsSystemBlock` answered from CAPABILITY alone, so a template whose administrator had
 * switched the block off still accepted a body that carried it. The save succeeded at both layers, and
 * the send then replaced the placeholder with an empty string — so the mail went out looking correct and
 * the configuration mistake produced no signal anywhere.
 */
describe('TemplateManagement — the display level against the body', () => {
  /** A template that CAN carry the block, currently at a level that shows it. */
  const SUPPORTED_CONTRACT = {
    ...ACCOUNT_CONTRACT,
    templateCode: 'ACCOUNT_ROLE_CHANGED',
    contactSupported: true,
    contactRequirement: 'OPTIONAL',
    requiredSystemBlocks: [],
    optionalSystemBlocks: ['actionBlock', 'contactInformationBlock'],
  };

  const BODY_WITH_BLOCK_VI = '<p>Chào {{fullName}}.</p>{{contactInformationBlock}}';
  const BODY_WITH_BLOCK_EN = '<p>Hello {{fullName}}.</p>{{contactInformationBlock}}';

  const SUPPORTED_SETTINGS = {
    templateCode: 'ACCOUNT_ROLE_CHANGED', requirement: 'OPTIONAL', contactSource: 'CAMPUS_DEFAULT',
    showEmail: true, showPhone: true, showDepartment: false, showCampus: true, showSender: false,
    headingVi: 'Thông tin liên hệ', headingEn: 'Contact information', replyToSource: 'NONE',
    blockPlaceholder: '{{contactInformationBlock}}',
    bodyCarriesBlockVi: true, bodyCarriesBlockEn: true,
    availableRequirements: ['NONE', 'OPTIONAL', 'REQUIRED'],
    availableSources: ['HOST', 'CAMPUS_DEFAULT'], availableReplyToSources: ['NONE', 'CONTACT', 'SENDER'],
    capability: 'SUPPORTED',
    editable: true,
  };

  /** Opens a template whose bodies BOTH carry the block, under a level that permits it. */
  const openWithBlock = async () => {
    getEmailTemplateDetail.mockResolvedValue({
      data: {
        ...DETAIL, templateCode: 'ACCOUNT_ROLE_CHANGED',
        bodyVi: BODY_WITH_BLOCK_VI, bodyEn: BODY_WITH_BLOCK_EN,
      },
    });
    getEmailTemplateContract.mockResolvedValue({ data: SUPPORTED_CONTRACT });
    getEmailContactSettings.mockResolvedValue({ data: SUPPORTED_SETTINGS });

    await openEditor();
    await screen.findByTestId('contact-settings-panel');
  };

  /** The same template with the level already hidden — the state the whole prompt is about. */
  const openHidden = async () => {
    getEmailTemplateDetail.mockResolvedValue({
      data: {
        ...DETAIL, templateCode: 'ACCOUNT_ROLE_CHANGED',
        bodyVi: BODY_WITH_BLOCK_VI, bodyEn: BODY_WITH_BLOCK_EN,
      },
    });
    getEmailTemplateContract.mockResolvedValue({
      data: { ...SUPPORTED_CONTRACT, contactRequirement: 'NONE' },
    });
    getEmailContactSettings.mockResolvedValue({
      data: { ...SUPPORTED_SETTINGS, requirement: 'NONE' },
    });

    await openEditor();
    await screen.findByTestId('contact-settings-panel');
  };

  it('accepts the block while the level is Tùy chọn', async () => {
    await openWithBlock();

    expect(screen.queryByTestId('issues-bodyVi')).not.toBeInTheDocument();
    expect(screen.queryByTestId('contact-settings-cross-field-error')).not.toBeInTheDocument();
  });

  /**
   * §4: switching to "Không hiển thị" while the body still carries the block must ASK. The bodies are
   * the operator's text — deleting from them silently would be an edit they never made and cannot see —
   * and setting the level with the block in place produces a template that cannot be saved.
   */
  it('asks before switching to Không hiển thị when the body carries the block', async () => {
    await openWithBlock();

    fireEvent.click(screen.getByTestId('contact-requirement-NONE'));

    expect(await screen.findByRole('heading', { name: /Nội dung đang có khối thông tin liên hệ/ }))
      .toBeInTheDocument();
    expect(screen.getByText(/cả tiếng Việt và tiếng Anh/)).toBeInTheDocument();

    // Nothing has happened yet: not the level, not the bodies.
    expect((screen.getByTestId('quill') as HTMLTextAreaElement).value)
      .toContain('{{contactInformationBlock}}');
    expect(updateEmailTemplate).not.toHaveBeenCalled();
  });

  /** §4: "Giữ cấu hình hiện tại" changes nothing at all. */
  it('leaves both the level and the bodies alone when the operator keeps the configuration', async () => {
    await openWithBlock();

    fireEvent.click(screen.getByTestId('contact-requirement-NONE'));
    fireEvent.click(await screen.findByRole('button', { name: 'Giữ cấu hình hiện tại' }));

    await waitFor(() =>
      expect(screen.queryByRole('heading', { name: /Nội dung đang có khối/ })).not.toBeInTheDocument());

    expect((screen.getByTestId('quill') as HTMLTextAreaElement).value)
      .toContain('{{contactInformationBlock}}');
    expect((screen.getByTestId('contact-requirement-OPTIONAL') as HTMLInputElement).checked).toBe(true);
    // And nothing became unsaved, because nothing changed.
    expect(screen.queryByTestId('editor-dirty')).not.toBeInTheDocument();
  });

  /** §4: the other button removes the block from BOTH bodies, sets the level, and saves nothing. */
  it('removes the block from both languages and sets the level, without saving', async () => {
    await openWithBlock();

    fireEvent.click(screen.getByTestId('contact-requirement-NONE'));
    fireEvent.click(
      await screen.findByRole('button', { name: 'Xóa khối và chuyển sang Không hiển thị' }));

    await waitFor(() =>
      expect((screen.getByTestId('quill') as HTMLTextAreaElement).value)
        .not.toContain('{{contactInformationBlock}}'));

    // The English body too — a level is one setting for the whole template, so clearing one language
    // would leave a template that is still refused.
    fireEvent.click(screen.getByRole('button', { name: 'English' }));
    await waitFor(() =>
      expect((screen.getByTestId('quill') as HTMLTextAreaElement).value).toContain('Hello'));
    expect((screen.getByTestId('quill') as HTMLTextAreaElement).value)
      .not.toContain('{{contactInformationBlock}}');

    expect((screen.getByTestId('contact-requirement-NONE') as HTMLInputElement).checked).toBe(true);

    // Dirty, and not saved: this is an edit like any other and waits for the save button.
    expect(screen.getByTestId('editor-dirty')).toBeInTheDocument();
    expect(updateEmailTemplate).not.toHaveBeenCalled();
    expect(updateEmailContactSettings).not.toHaveBeenCalled();
  });

  /** Nothing to decide when neither body carries the block, so the level is applied straight away. */
  it('switches to Không hiển thị without asking when there is no block to remove', async () => {
    getEmailTemplateDetail.mockResolvedValue({
      data: { ...DETAIL, templateCode: 'ACCOUNT_ROLE_CHANGED' },
    });
    getEmailTemplateContract.mockResolvedValue({ data: SUPPORTED_CONTRACT });
    getEmailContactSettings.mockResolvedValue({
      data: { ...SUPPORTED_SETTINGS, bodyCarriesBlockVi: false, bodyCarriesBlockEn: false },
    });

    await openEditor();
    await screen.findByTestId('contact-settings-panel');

    fireEvent.click(screen.getByTestId('contact-requirement-NONE'));

    await waitFor(() =>
      expect((screen.getByTestId('contact-requirement-NONE') as HTMLInputElement).checked).toBe(true));
    expect(screen.queryByRole('heading', { name: /Nội dung đang có khối/ })).not.toBeInTheDocument();
  });

  /** §3: the level is already hidden and the body still has the block. Refused before a round trip. */
  it('refuses the save and names the contradiction when the level is already hidden', async () => {
    await openHidden();

    const issues = await screen.findByTestId('issues-bodyVi');
    expect(issues.querySelector(
      '[data-error-code="EMAIL_TEMPLATE_CONTACT_BLOCK_NOT_ALLOWED_WHEN_HIDDEN"]')).not.toBeNull();
    expect(issues).toHaveTextContent('Không hiển thị');

    // §13: the message reads as a sentence and the action is a separate button — not one run of text
    // ending in a raw code butted against a label.
    expect(issues.textContent).not.toMatch(/EMAIL_TEMPLATE_[A-Z_]+/);

    editName();
    expect(saveButton()).toBeDisabled();
    expect(updateEmailTemplate).not.toHaveBeenCalled();
  });

  /** The same refusal appears on the contact card, next to the radio that caused it. */
  it('shows the contradiction on the contact card as well as under the body', async () => {
    await openHidden();

    const onCard = await screen.findByTestId('contact-settings-cross-field-error');
    expect(onCard).toHaveTextContent('Không hiển thị');
    expect(within(onCard).getByRole('button', { name: 'Xóa khối khỏi nội dung' })).toBeInTheDocument();
  });

  /** §5: the action clears both languages, and leaves the level exactly where it was. */
  it('clears both bodies from the removal action without touching the level', async () => {
    await openHidden();
    await screen.findByTestId('issues-bodyVi');

    fireEvent.click(screen.getByTestId('remove-contact-block'));

    await waitFor(() => expect(screen.queryByTestId('issues-bodyVi')).not.toBeInTheDocument());
    expect((screen.getByTestId('quill') as HTMLTextAreaElement).value)
      .not.toContain('{{contactInformationBlock}}');

    // The level is untouched: the operator asked to remove a block, not to change a policy.
    expect((screen.getByTestId('contact-requirement-NONE') as HTMLInputElement).checked).toBe(true);
    expect(screen.getByTestId('editor-dirty')).toBeInTheDocument();
    expect(saveButton()).toBeEnabled();
  });

  /**
   * §5: removing the block under a REQUIRED level moves the operator from one error to another, and the
   * screen must say so rather than quietly lowering the level to make the error go away.
   */
  it('reports the missing block after a removal under Bắt buộc, without changing the level', async () => {
    await openWithBlock();

    fireEvent.click(screen.getByTestId('contact-requirement-REQUIRED'));
    await waitFor(() =>
      expect((screen.getByTestId('contact-requirement-REQUIRED') as HTMLInputElement).checked).toBe(true));

    // Removed from the body directly, as an operator editing the text would.
    fireEvent.change(screen.getByTestId('quill'), { target: { value: '<p>Chào {{fullName}}.</p>' } });

    const issues = await screen.findByTestId('issues-bodyVi');
    expect(issues.querySelector(
      '[data-error-code="EMAIL_TEMPLATE_REQUIRED_CONTACT_BLOCK_NOT_IN_BODY"]')).not.toBeNull();
    expect(issues).toHaveTextContent('thiếu khối thông tin liên hệ');

    // NOT silently demoted to NONE to make the message disappear.
    expect((screen.getByTestId('contact-requirement-REQUIRED') as HTMLInputElement).checked).toBe(true);
    expect(saveButton()).toBeDisabled();
  });

  /**
   * §12: undoing back to the baseline clears the warning. The dirty flag is derived from a comparison,
   * never a latch that is switched on and left on.
   */
  it('drops the unsaved warning when the level is put back', async () => {
    await openWithBlock();

    fireEvent.click(screen.getByTestId('contact-requirement-REQUIRED'));
    await screen.findByTestId('editor-dirty');

    fireEvent.click(screen.getByTestId('contact-requirement-OPTIONAL'));

    await waitFor(() => expect(screen.queryByTestId('editor-dirty')).not.toBeInTheDocument());
    expect(saveButton()).toBeDisabled();
  });

  /** §7 of the backend half, seen from the client: a refused save is relayed against the field. */
  it('shows the backend refusal against the field when the server catches it first', async () => {
    updateEmailTemplate.mockRejectedValue({
      response: {
        status: 422,
        data: {
          errorCode: 'EMAIL_TEMPLATE_CONTACT_BLOCK_NOT_ALLOWED_WHEN_HIDDEN',
          message: 'Không thể lưu mẫu vì mức hiển thị là “Không hiển thị” nhưng nội dung vẫn chứa khối thông tin liên hệ.',
        },
      },
    });

    await openWithBlock();
    editName();
    fireEvent.click(saveButton());

    const issues = await screen.findByTestId('issues-bodyVi');
    expect(issues.querySelector(
      '[data-error-code="EMAIL_TEMPLATE_CONTACT_BLOCK_NOT_ALLOWED_WHEN_HIDDEN"]')).not.toBeNull();
    expect(screen.getByTestId('remove-contact-block')).toBeInTheDocument();

    // A refused save leaves the work unsaved.
    expect(screen.getByTestId('editor-dirty')).toBeInTheDocument();
  });

  /**
   * §9: an unsupported template shows no form, and a body that has kept the block from an older release
   * is still warned about — next to the sentence that has just said there is nothing here to configure.
   */
  it('warns about a stale block on a template that has no contact form', async () => {
    getEmailTemplateDetail.mockResolvedValue({
      data: { ...DETAIL, bodyVi: BODY_WITH_BLOCK_VI },
    });
    getEmailTemplateContract.mockResolvedValue({
      data: {
        ...ACCOUNT_CONTRACT,
        contactSupported: false,
        contactReasonVi: 'Email chứa liên kết dùng một lần.',
      },
    });

    await openEditor();
    await screen.findByTestId('contact-settings-unsupported');

    const issues = await screen.findByTestId('issues-bodyVi');
    expect(issues.querySelector(
      '[data-error-code="EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWED"]')).not.toBeNull();
    expect(screen.getByTestId('remove-block-bodyVi-contactInformationBlock')).toBeInTheDocument();
    expect(saveButton()).toBeDisabled();
  });
});
