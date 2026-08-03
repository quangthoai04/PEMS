/**
 * The template-management screen (G11-I catalog, G11-J contract).
 *
 * Two reported defects are pinned here as behaviour rather than as markup:
 *   - opening a canonical template must produce NO warning, and must not offer another module's
 *     variables (the screen used to do both, from a list compiled into the frontend);
 *   - the catalog is fixed, so there is no "Thêm mẫu mới" and no per-row status toggle.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

const getEmailTemplateList = vi.fn();
const getEmailTemplateDetail = vi.fn();
const getEmailTemplateContract = vi.fn();
const updateEmailTemplate = vi.fn();
const restoreEmailTemplateDefault = vi.fn();
const getEmailContactSettings = vi.fn();
const updateEmailContactSettings = vi.fn();

vi.mock('../../../features/emails/api/emailsApi', () => ({
  emailsApi: {
    getEmailTemplateList: (...a: unknown[]) => getEmailTemplateList(...a),
    getEmailTemplateDetail: (...a: unknown[]) => getEmailTemplateDetail(...a),
    getEmailTemplateContract: (...a: unknown[]) => getEmailTemplateContract(...a),
    updateEmailTemplate: (...a: unknown[]) => updateEmailTemplate(...a),
    restoreEmailTemplateDefault: (...a: unknown[]) => restoreEmailTemplateDefault(...a),
    getEmailContactSettings: (...a: unknown[]) => getEmailContactSettings(...a),
    updateEmailContactSettings: (...a: unknown[]) => updateEmailContactSettings(...a),
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
  allowedVariables: ['fullName', 'roleName', 'campusName', 'expiresInHours', 'actionBlock'],
  requiredVariables: [],
  optionalVariables: ['fullName', 'roleName', 'campusName', 'expiresInHours', 'actionBlock'],
  sensitiveVariables: [],
  forbiddenInSubject: ['actionBlock'],
  requiresActionBlock: false,
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
  requiresActionBlock: false,
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

describe('TemplateManagement — the catalog is fixed (G11-I)', () => {
  it('offers no way to create a template', async () => {
    render(<TemplateManagement pushToast={pushToast} />);
    await screen.findByText('ACCOUNT_EMAIL_CONFIRMATION');

    expect(screen.queryByText('Thêm mẫu mới')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /thêm mẫu/i })).not.toBeInTheDocument();
  });

  it('shows status as read-only text, not a toggle', async () => {
    render(<TemplateManagement pushToast={pushToast} />);
    const status = await screen.findByText('Đang hoạt động');

    // A <span>, not a <button>: deactivating a system template breaks the feature that sends it.
    expect(status.tagName).not.toBe('BUTTON');
    expect(status.closest('button')).toBeNull();
  });

  it('shows the template code as locked, with no input to change it', async () => {
    await openEditor();

    // Shown in several places (heading, locked field, sidebar note) — all of them read-only.
    expect(screen.getAllByText('ACCOUNT_EMAIL_CONFIRMATION').length).toBeGreaterThan(0);

    // The old form had a "Mã mẫu *" text input; there must be no editable field for it now.
    expect(screen.queryByLabelText(/Mã mẫu \*/)).not.toBeInTheDocument();
    expect(document.querySelector('input[value="ACCOUNT_EMAIL_CONFIRMATION"]')).toBeNull();
  });

  it('sends only content fields plus the concurrency token', async () => {
    await openEditor();
    fireEvent.click(screen.getByRole('button', { name: /Cập nhật/ }));

    await waitFor(() => expect(updateEmailTemplate).toHaveBeenCalled());

    const [, payload] = updateEmailTemplate.mock.calls[0];
    expect(Object.keys(payload).sort()).toEqual(
      ['bodyEn', 'bodyVi', 'description', 'expectedRevision', 'name', 'subjectEn', 'subjectVi'],
    );
    expect(payload).not.toHaveProperty('templateCode');
    expect(payload).not.toHaveProperty('status');
    expect(payload).not.toHaveProperty('purpose');
    expect(payload).not.toHaveProperty('variablesText');
  });

  /**
   * The token is the server's revision, sent back exactly as received. It replaced updated_at, which
   * could not tell two saves inside the same second apart.
   */
  it('sends the revision the server gave it, unmodified', async () => {
    await openEditor();
    fireEvent.click(screen.getByRole('button', { name: /Cập nhật/ }));

    await waitFor(() => expect(updateEmailTemplate).toHaveBeenCalled());
    expect(updateEmailTemplate.mock.calls[0][1].expectedRevision).toBe(DETAIL.revision);
  });

  /** The old form overwrote the English content with the Vietnamese text on every save. */
  it('does not overwrite the English content with the Vietnamese one', async () => {
    await openEditor();
    fireEvent.click(screen.getByRole('button', { name: /Cập nhật/ }));

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

    expect(screen.queryByText('actionBlock')).not.toBeInTheDocument();
  });

  it('flags a variable from another module, naming the field and the variable', async () => {
    await openEditor();
    await screen.findByText('Họ tên người nhận');

    fireEvent.change(screen.getByTestId('quill'), {
      target: { value: '<p>{{logisticsTitle}}</p>' },
    });

    const issues = await screen.findByTestId('issues-bodyVi');
    expect(issues).toHaveTextContent('logisticsTitle');
    expect(issues).toHaveTextContent('EMAIL_TEMPLATE_VARIABLE_UNKNOWN');
  });

  it('disables the save button while a blocking issue is present', async () => {
    await openEditor();
    await screen.findByText('Họ tên người nhận');

    fireEvent.change(screen.getByTestId('quill'), { target: { value: '<p>{{ghost}}</p>' } });

    await waitFor(() =>
      expect(screen.getByRole('button', { name: /Cập nhật/ })).toBeDisabled());
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
    fireEvent.click(screen.getByRole('button', { name: /Cập nhật/ }));

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
    fireEvent.click(screen.getByRole('button', { name: /Cập nhật/ }));

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
    // The confirmation is deliberate: this discards work rather than saving it. Matched on the exact
    // label — /Phục hồi/ would also match the button that opened the dialog.
    fireEvent.click(await screen.findByRole('button', { name: 'Xác nhận' }));
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

    // The dialog names the template and says explicitly that BOTH languages are replaced — an operator
    // who spent an afternoon on the Vietnamese copy must not discover that after the fact. The code is
    // matched inside the dialog text rather than on the page, where it legitimately appears several
    // times (heading, locked field, sidebar note).
    const dialogText = await screen.findByText(/Phục hồi mẫu "ACCOUNT_EMAIL_CONFIRMATION"/);
    expect(dialogText).toBeInTheDocument();
    expect(dialogText.textContent).toMatch(/tiếng Việt và tiếng Anh/);
    expect(dialogText.textContent).toMatch(/không thay đổi/);
  });

  it('sends the template id and the revision it was shown', async () => {
    await openAndRestore();

    await waitFor(() => expect(restoreEmailTemplateDefault).toHaveBeenCalled());
    expect(restoreEmailTemplateDefault.mock.calls[0][0]).toBe(DETAIL.emailTemplateId);
    expect(restoreEmailTemplateDefault.mock.calls[0][1]).toBe(DETAIL.revision);
  });

  it('shows the restored content and keeps the new revision', async () => {
    await openAndRestore();

    await waitFor(() => expect(pushToast).toHaveBeenCalledWith('success', expect.stringContaining('phục hồi')));

    // The operator's discarded text must not stay on screen next to a bumped revision — that would
    // invite them to "save" it straight back.
    await waitFor(() =>
      expect((screen.getByLabelText('Tên mẫu *') as HTMLInputElement).value).toBe('Tên hệ thống'));

    // The next save carries the revision the restore returned, not the one it replaced.
    fireEvent.click(screen.getByRole('button', { name: /Cập nhật/ }));
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

  it('renders every template in the catalog, including the last one', async () => {
    getEmailTemplateList.mockImplementation(servingCatalog(30));

    render(<TemplateManagement pushToast={pushToast} />);
    await screen.findByText('TEMPLATE_01');

    // The 30th is the one a PageSize=10 request loses; asserting only "some rows rendered" would pass
    // against the defect.
    expect(screen.getByText('TEMPLATE_30')).toBeInTheDocument();
    expect(screen.getAllByLabelText(/^Chỉnh sửa TEMPLATE_/)).toHaveLength(30);
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
    expect(screen.getByRole('button', { name: /Cập nhật/ })).toBeDisabled();
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
    expect(screen.getByRole('button', { name: /Cập nhật/ })).toBeDisabled();
    expect(screen.queryByTestId('contract-not-system')).not.toBeInTheDocument();
  });
});
