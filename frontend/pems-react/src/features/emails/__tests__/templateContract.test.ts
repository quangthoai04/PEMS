/**
 * The client-side half of the variable contract (G11-J).
 *
 * These exist because the screen used to validate against a list of eleven variables compiled into
 * the frontend, applied to whichever template happened to be open. The rules below are a mirror of
 * `EmailTemplateContentValidator`; the backend re-validates and stays the authority.
 */
import { describe, expect, it } from 'vitest';
import {
  SYSTEM_BLOCK_NAMES,
  TEMPLATE_ERROR_CODES,
  applySamples,
  applySystemBlocks,
  runtimeEditableOf,
  senderVariablesAllowedOf,
  describeSystemBlocks,
  errorCodeOf,
  extractPlaceholders,
  isSystemBlock,
  issuesFromError,
  removeSystemBlock,
  validateContent,
  type TemplateContract,
} from '../types/templateContract';

/** A stand-in for ACCOUNT_EMAIL_CONFIRMATION, shaped exactly as the API returns it. */
const accountContract: TemplateContract = {
  templateCode: 'ACCOUNT_EMAIL_CONFIRMATION',
  module: 'ACCOUNT',
  isSystemTemplate: true,
  variables: [
    { name: 'fullName', label: 'Họ tên người nhận', sample: 'Nguyễn Văn An', required: false, sensitive: false, forbiddenInSubject: false },
    { name: 'roleName', label: 'Vai trò', sample: 'Cán bộ', required: false, sensitive: false, forbiddenInSubject: false },
    { name: 'campusName', label: 'Cơ sở', sample: 'FPTU Hà Nội', required: false, sensitive: false, forbiddenInSubject: false },
    { name: 'expiresInHours', label: 'Hiệu lực (giờ)', sample: '24', required: false, sensitive: false, forbiddenInSubject: false },
  ],
  allowedVariables: ['fullName', 'roleName', 'campusName', 'expiresInHours'],
  requiredVariables: [],
  optionalVariables: ['fullName', 'roleName', 'campusName', 'expiresInHours'],
  requiredSystemBlocks: [],
  optionalSystemBlocks: ['actionBlock'],
  // What the backend sends: `<span>` buttons, no href, so a click cannot navigate.
  systemBlockPreviews: {
    actionBlock: '<div><span style="background:#9aa6b2">Chấp nhận</span>'
      + '<span style="background:#9aa6b2">Từ chối</span></div>',
  },
  sensitiveVariables: [],
  forbiddenInSubject: ['actionBlock'],
  actionSupported: false,
  actionRequired: false,
  systemActionDescription: null,
  carriesSecret: false,
  allowCc: false,
  allowBcc: false,
  securityClassification: 'SENSITIVE',
  editableFields: ['name', 'description', 'subjectVi', 'subjectEn', 'bodyVi', 'bodyEn'],
};

/** A stand-in for AUTH_PASSWORD_RESET_OTP — a credential-bearing template. */
const otpContract: TemplateContract = {
  ...accountContract,
  templateCode: 'AUTH_PASSWORD_RESET_OTP',
  module: 'AUTH',
  variables: [
    { name: 'fullName', label: 'Họ tên người nhận', sample: 'Nguyễn Văn An', required: false, sensitive: false, forbiddenInSubject: false },
    { name: 'otpCode', label: 'Mã OTP', sample: '000000', required: true, sensitive: true, forbiddenInSubject: true },
    { name: 'expireMinutes', label: 'Hiệu lực (phút)', sample: '10', required: false, sensitive: false, forbiddenInSubject: false },
  ],
  allowedVariables: ['fullName', 'otpCode', 'expireMinutes'],
  requiredVariables: ['otpCode'],
  optionalVariables: ['fullName', 'expireMinutes'],
  requiredSystemBlocks: [],
  optionalSystemBlocks: ['actionBlock'],
  sensitiveVariables: ['otpCode'],
  forbiddenInSubject: ['otpCode', 'actionBlock'],
};

/** A stand-in for VISIT_PARTICIPANT_INVITATION — the action block is mandatory. */
const invitationContract: TemplateContract = {
  ...accountContract,
  templateCode: 'VISIT_PARTICIPANT_INVITATION',
  module: 'VISIT_PARTICIPANT',
  variables: [
    { name: 'recipientName', label: 'Tên người nhận', sample: 'Nguyễn Văn An', required: false, sensitive: false, forbiddenInSubject: false },
  ],
  allowedVariables: ['recipientName'],
  requiredVariables: [],
  optionalVariables: ['recipientName'],
  requiredSystemBlocks: ['actionBlock'],
  optionalSystemBlocks: [],
  actionSupported: true,
  actionRequired: true,
  systemActionDescription: 'Fake action description',
};

/**
 * A stand-in for VISIT_SETUP_PROGRESS_UPDATE — the one template carrying TWO required blocks, whose
 * content IS its tables and whose text tells the guest to contact the Host.
 */
const setupProgressContract: TemplateContract = {
  ...accountContract,
  templateCode: 'VISIT_SETUP_PROGRESS_UPDATE',
  module: 'VISIT_SETUP',
  variables: [
    { name: 'recipientName', label: 'Tên người nhận', sample: 'Nguyễn Văn An', required: false, sensitive: false, forbiddenInSubject: false },
  ],
  allowedVariables: ['recipientName'],
  requiredVariables: [],
  optionalVariables: ['recipientName'],
  requiredSystemBlocks: ['setupSummaryBlock'],
  optionalSystemBlocks: ['actionBlock'],
  forbiddenInSubject: ['actionBlock', 'setupSummaryBlock'],
};

const content = (over: Partial<Record<'subjectVi' | 'bodyVi' | 'subjectEn' | 'bodyEn', string>>) => ({
  subjectVi: '', bodyVi: '', subjectEn: '', bodyEn: '', ...over,
});

describe('extractPlaceholders', () => {
  it('finds every distinct placeholder across several parts', () => {
    expect(extractPlaceholders('{{a}} {{b}}', '<p>{{b}} {{c}}</p>').sort()).toEqual(['a', 'b', 'c']);
  });

  it('reads the URL-encoded form a rich editor stores inside an href', () => {
    expect(extractPlaceholders('<a href="/x?u=%7B%7BfullName%7D%7D">l</a>')).toEqual(['fullName']);
  });

  it('is not confused by repeated calls (the /g regex is module scope)', () => {
    expect(extractPlaceholders('{{a}}')).toEqual(['a']);
    expect(extractPlaceholders('{{a}}')).toEqual(['a']);
  });

  it('ignores empty and nullish parts', () => {
    expect(extractPlaceholders(undefined, null, '')).toEqual([]);
  });
});

describe('validateContent', () => {
  it('reports nothing for canonical content using only declared variables', () => {
    const issues = validateContent(accountContract, content({
      subjectVi: 'Xác nhận tài khoản',
      bodyVi: '<p>Chào {{fullName}}, vai trò {{roleName}} tại {{campusName}} — {{expiresInHours}} giờ.</p>',
    }));

    expect(issues).toEqual([]);
  });

  /**
   * The reported defect, in one assertion: the six logistics variables the old hard-coded sidebar
   * offered on every template are not part of this one, and using one must be refused rather than
   * quietly saved.
   */
  it.each(['logisticsTitle', 'departmentName', 'departmentLeaderName', 'requesterName', 'usageStartAt', 'usageEndAt'])(
    'refuses %s on an account template',
    name => {
      const issues = validateContent(accountContract, content({
        subjectVi: 'Xác nhận', bodyVi: `<p>{{${name}}}</p>`,
      }));

      expect(issues).toHaveLength(1);
      expect(issues[0].code).toBe(TEMPLATE_ERROR_CODES.variableUnknown);
      expect(issues[0].variableName).toBe(name);
      expect(issues[0].field).toBe('bodyVi');
    },
  );

  /**
   * A run of spaces is an ERROR, so the save button goes dead on it (V4 §7.4).
   *
   * The screen disables "Lưu thay đổi" on any issue of this severity and never calls the save API, so
   * severity is the whole of the blocking behaviour here — and the backend answers the identical code,
   * which is what makes a save attempted around this screen come back the same way.
   */
  it('refuses a run of spaces, per field and per language', () => {
    const issues = validateContent(accountContract, content({
      subjectVi: 'Xác nhận tài khoản',
      bodyVi: '<p>Chào {{fullName}}, vai trò {{roleName}} tại {{campusName}} — {{expiresInHours}} giờ.</p>',
      subjectEn: 'Account confirmed',
      bodyEn: '<p>Role&nbsp;&nbsp;&nbsp;{{roleName}} at {{campusName}} — {{expiresInHours}}h, {{fullName}}.</p>',
    }));

    const spacing = issues.filter(i => i.code === TEMPLATE_ERROR_CODES.spaceRunUnsupported);

    expect(spacing).toHaveLength(1);
    expect(spacing[0].field).toBe('bodyEn');       // names the tab that is actually holding the save
    expect(spacing[0].severity).toBe('ERROR');
    expect(spacing[0].messageVi).toContain('căn lề, thụt lề hoặc bảng');
  });

  it('says nothing about the indentation in formatted markup', () => {
    const issues = validateContent(accountContract, content({
      subjectVi: 'Xác nhận',
      bodyVi: '<p>Chào {{fullName}}</p>\n    <table>\n      <tr><td>{{roleName}}</td></tr>\n'
        + '      <tr><td>{{campusName}}</td></tr>\n    </table>\n    <p>{{expiresInHours}} giờ</p>',
    }));

    expect(issues).toEqual([]);
  });

  it('addresses each issue to the field that carries it', () => {
    const issues = validateContent(accountContract, content({
      subjectVi: 'Xin chào {{ghostA}}',
      bodyVi: '<p>{{ghostB}}</p>',
      subjectEn: 'Hello {{ghostC}}',
      bodyEn: '<p>{{ghostD}}</p>',
    }));

    expect(issues.find(i => i.variableName === 'ghostA')?.field).toBe('subjectVi');
    expect(issues.find(i => i.variableName === 'ghostB')?.field).toBe('bodyVi');
    expect(issues.find(i => i.variableName === 'ghostC')?.field).toBe('subjectEn');
    expect(issues.find(i => i.variableName === 'ghostD')?.field).toBe('bodyEn');
  });

  it('refuses a credential in a subject', () => {
    const issues = validateContent(otpContract, content({
      subjectVi: 'Mã của bạn: {{otpCode}}',
      bodyVi: '<p>{{otpCode}}</p>',
    }));

    expect(issues.map(i => i.code)).toContain(TEMPLATE_ERROR_CODES.subjectForbiddenSensitive);
    expect(issues.find(i => i.code === TEMPLATE_ERROR_CODES.subjectForbiddenSensitive)?.field).toBe('subjectVi');
  });

  it('refuses removing the code from an OTP email', () => {
    const issues = validateContent(otpContract, content({
      subjectVi: 'Đặt lại mật khẩu',
      bodyVi: '<p>Chào {{fullName}}, kiểm tra ứng dụng.</p>',
    }));

    expect(issues).toHaveLength(1);
    expect(issues[0].code).toBe(TEMPLATE_ERROR_CODES.requiredVariableMissing);
    expect(issues[0].variableName).toBe('otpCode');
  });

  it('refuses removing the action block from an invitation', () => {
    const issues = validateContent(invitationContract, content({
      subjectVi: 'Thư mời',
      bodyVi: '<p>Chào {{recipientName}}.</p>',
    }));

    expect(issues).toHaveLength(1);
    expect(issues[0].code).toBe(TEMPLATE_ERROR_CODES.actionBlockRequired);
  });

  it('accepts an invitation that keeps the action block', () => {
    const issues = validateContent(invitationContract, content({
      subjectVi: 'Thư mời',
      bodyVi: '<p>Chào {{recipientName}}.</p>{{actionBlock}}',
    }));

    expect(issues).toEqual([]);
  });

  /** Ordinary editing: rewording a sentence so it no longer mentions the campus must save. */
  it('allows removing an optional variable', () => {
    const issues = validateContent(accountContract, content({
      subjectVi: 'Xác nhận', bodyVi: '<p>Chào {{fullName}}.</p>',
    }));

    expect(issues).toEqual([]);
  });

  /** A language nobody maintains for this template must not be judged as a half-finished edit. */
  it('does not demand required variables inside an empty language', () => {
    const issues = validateContent(otpContract, content({
      subjectVi: 'Mã', bodyVi: '<p>{{otpCode}}</p>',
      subjectEn: '', bodyEn: '',
    }));

    expect(issues).toEqual([]);
  });

  it('judges each language separately', () => {
    const issues = validateContent(otpContract, content({
      subjectVi: 'Mã', bodyVi: '<p>{{otpCode}}</p>',
      subjectEn: 'Code', bodyEn: '<p>Check the app.</p>',
    }));

    expect(issues).toHaveLength(1);
    expect(issues[0].field).toBe('bodyEn');
    expect(issues[0].code).toBe(TEMPLATE_ERROR_CODES.requiredVariableMissing);
  });
});

/**
 * System blocks are not variables and must never be judged by the variable rules.
 *
 * The defect these pin: `{{setupSummaryBlock}}` — legal on fourteen templates and MANDATORY on
 * them — was checked against `allowedVariables`, which by design never contained it, and so came back
 * as EMAIL_TEMPLATE_VARIABLE_UNKNOWN: "biến không tồn tại trong hệ thống". Every one of these fails
 * against the pre-split contract.
 */
describe('system blocks are judged as blocks, not variables', () => {
  it('accepts a required block without calling it an unknown variable', () => {
    const issues = validateContent(setupProgressContract, content({
      subjectVi: 'Cập nhật chuẩn bị',
      bodyVi: '<p>Chào {{recipientName}}.</p>{{setupSummaryBlock}}',
    }));

    expect(issues).toEqual([]);
  });

  it('accepts an optional action block on a template that does not require one', () => {
    const issues = validateContent(accountContract, content({
      subjectVi: 'Xác nhận',
      bodyVi: '<p>Chào {{fullName}}.</p>{{actionBlock}}',
    }));

    expect(issues).toEqual([]);
  });

  it('never reports a system block under the unknown-VARIABLE code', () => {
    const issues = validateContent(accountContract, content({
      subjectVi: 'Xác nhận',
      bodyVi: '<p>{{setupSummaryBlock}}</p>',
    }));

    expect(issues).not.toHaveLength(0);
    for (const issue of issues) {
      expect(issue.code).not.toBe(TEMPLATE_ERROR_CODES.variableUnknown);
      expect(issue.code).toBe(TEMPLATE_ERROR_CODES.systemBlockNotAllowed);
    }
  });

  /** The other half of the contract: a block in the wrong template is still refused. */
  it('refuses a block the template cannot resolve', () => {
    const issues = validateContent(accountContract, content({
      subjectVi: 'Xác nhận',
      bodyVi: '<p>Chào {{fullName}}.</p>{{setupSummaryBlock}}',
    }));

    expect(issues).toHaveLength(1);
    expect(issues[0].code).toBe(TEMPLATE_ERROR_CODES.systemBlockNotAllowed);
    expect(issues[0].variableName).toBe('setupSummaryBlock');
  });

  /** An ordinary variable outside the contract must STILL be unknown — the split is not a relaxation. */
  it('still reports a non-block variable outside the contract as unknown', () => {
    const issues = validateContent(accountContract, content({
      subjectVi: 'Xác nhận',
      bodyVi: '<p>Chào {{fullName}}, xe {{vehicleInfo}}.</p>',
    }));

    expect(issues).toHaveLength(1);
    expect(issues[0].code).toBe(TEMPLATE_ERROR_CODES.variableUnknown);
    expect(issues[0].variableName).toBe('vehicleInfo');
  });

  it('reports a missing content block under its own code, not the action-block one', () => {
    const issues = validateContent(setupProgressContract, content({
      subjectVi: 'Cập nhật chuẩn bị',
      bodyVi: '<p>Chào {{recipientName}}.</p>',
    }));

    expect(issues).toHaveLength(1);
    expect(issues[0].code).toBe(TEMPLATE_ERROR_CODES.requiredBlockMissing);
    expect(issues[0].variableName).toBe('setupSummaryBlock');
  });

  it('keeps a block out of the subject, where it would be stored in history', () => {
    const issues = validateContent(setupProgressContract, content({
      subjectVi: 'Liên hệ {{setupSummaryBlock}}',
      bodyVi: '<p>x</p>{{setupSummaryBlock}}',
    }));

    expect(issues).toHaveLength(1);
    expect(issues[0].field).toBe('subjectVi');
    expect(issues[0].code).toBe(TEMPLATE_ERROR_CODES.subjectForbiddenSensitive);
  });

  /**
   * The editor is not the preview. The placeholder has to survive in the content an operator edits —
   * they must be able to see where the block sits and move it — while the PREVIEW shows the rendered
   * sample. Substituting in the editor would delete the only handle they have on the block's position.
   */
  it('leaves the placeholder untouched in the editable content', () => {
    const body = '<p>Chào {{fullName}}.</p>{{actionBlock}}';

    // applySamples is what the editor path runs; it must not consume blocks.
    expect(applySamples(accountContract, body)).toContain('{{actionBlock}}');
  });

  it('turns the placeholder into the sample button markup in the preview', () => {
    const out = applySystemBlocks(
      accountContract,
      '<p>Chào Nguyễn Văn An.</p>{{actionBlock}}',
    );

    expect(out).not.toContain('{{actionBlock}}');
    expect(out).toContain('Chấp nhận');
    expect(out).toContain('Từ chối');
  });

  /** A preview mints nothing: no anchor, so a click cannot navigate, and no token to leak. */
  it('produces no link, token or business URL in the preview', () => {
    const out = applySystemBlocks(accountContract, '{{actionBlock}}');

    expect(out).not.toMatch(/<a\s/i);
    expect(out).not.toMatch(/href=/i);
    expect(out).not.toMatch(/https?:\/\//i);
    expect(out).not.toMatch(/token|otp|acceptUrl|declineUrl/i);
  });

  /** The sample comes from the backend per language, so VI and EN differ without a table in here. */
  it('follows the language the backend rendered the sample in', () => {
    const en: TemplateContract = {
      ...accountContract,
      systemBlockPreviews: { actionBlock: '<div><span>Accept</span><span>Decline</span></div>' },
    };

    expect(applySystemBlocks(en, '{{actionBlock}}')).toContain('Accept');
    expect(applySystemBlocks(accountContract, '{{actionBlock}}')).toContain('Chấp nhận');
  });

  /**
   * A template with no sample for a block renders nothing rather than raw braces. Leaving
   * `{{actionBlock}}` visible in a preview reads as an unresolved variable — the exact confusion this
   * whole change removes.
   */
  it('renders nothing for a block the backend supplied no sample for', () => {
    const bare: TemplateContract = { ...accountContract, systemBlockPreviews: {} };
    const out = applySystemBlocks(bare, '<p>x</p>{{actionBlock}}');

    expect(out).toBe('<p>x</p>');
  });

  /**
   * The sample comes from the CONTRACT and nowhere else.
   *
   * There used to be a third argument for blocks resolved outside it — the contact card, whose markup
   * depended on toggles the operator had not saved. Nothing is drafted on this screen any more, so a
   * second source would only be a way for the preview and the send to disagree.
   */
  it('substitutes a block from the sample the contract carries', () => {
    const withSample: TemplateContract = {
      ...setupProgressContract,
      systemBlockPreviews: { setupSummaryBlock: '<table><tr><td>Lịch trình</td></tr></table>' },
    };

    const out = applySystemBlocks(withSample, '<p>x</p>{{setupSummaryBlock}}');

    expect(out).toContain('Lịch trình');
    expect(out).not.toContain('{{setupSummaryBlock}}');
  });

  it('recognises exactly the blocks the backend registers', () => {
    expect([...SYSTEM_BLOCK_NAMES].sort()).toEqual(
      ['actionBlock', 'setupSummaryBlock'],
    );
    expect(isSystemBlock('setupSummaryBlock')).toBe(true);
    expect(isSystemBlock('fullName')).toBe(false);
  });
});

describe('applySamples', () => {
  it('substitutes every declared variable with its sample', () => {
    expect(applySamples(accountContract, 'Chào {{fullName}} tại {{campusName}}'))
      .toBe('Chào Nguyễn Văn An tại FPTU Hà Nội');
  });

  /**
   * Case-SENSITIVE, matching the backend parser. A case-insensitive replace here would render
   * {{FullName}} as a value while the real send left it unresolved and refused to go out — the preview
   * would look healthier than the message.
   */
  it('does not substitute a differently-cased name', () => {
    expect(applySamples(accountContract, 'Chào {{FullName}}')).toBe('Chào {{FullName}}');
  });

  it('substitutes the URL-encoded form too', () => {
    expect(applySamples(accountContract, '%7B%7BfullName%7D%7D')).toBe('Nguyễn Văn An');
  });
});

describe('reading API failures', () => {
  it('extracts structured issues', () => {
    const err = { response: { data: { issues: [{ field: 'bodyVi', code: 'X', messageVi: 'a', messageEn: 'b', severity: 'ERROR' }] } } };
    expect(issuesFromError(err)).toHaveLength(1);
  });

  it('returns an empty list rather than guessing when the shape is unfamiliar', () => {
    expect(issuesFromError({})).toEqual([]);
    expect(issuesFromError(null)).toEqual([]);
    expect(issuesFromError({ response: { data: { issues: 'nope' } } })).toEqual([]);
  });

  it('reads the stable error code', () => {
    expect(errorCodeOf({ response: { data: { errorCode: 'EMAIL_TEMPLATE_CONCURRENCY_CONFLICT' } } }))
      .toBe(TEMPLATE_ERROR_CODES.concurrencyConflict);
    expect(errorCodeOf({})).toBeUndefined();
  });
});

/**
 * One block, one description (§4.2).
 *
 * The screen used to render the action block twice — from `actionSupported` with the backend's
 * description, and again from the block lists with the generic one — so ACCOUNT_EMAIL_CONFIRMATION
 * announced both a "Xác nhận email" button and "đồng ý / từ chối / xem chi tiết" buttons it does not
 * have. The rule lives here now: listed once, specific beats generic, nothing concatenated.
 */
describe('describeSystemBlocks', () => {
  it('lists a block once even when both lists name it', () => {
    const notices = describeSystemBlocks({
      ...invitationContract,
      requiredSystemBlocks: ['actionBlock'],
      optionalSystemBlocks: ['actionBlock'],
    });

    expect(notices.map(n => n.name)).toEqual(['actionBlock']);
    expect(notices[0].required).toBe(true);   // required wins: it is the stronger claim
  });

  it('prefers the backend’s description for this template over the generic one', () => {
    const notices = describeSystemBlocks({
      ...invitationContract,
      systemActionDescription: 'Nút "Xác nhận email" sẽ được hệ thống tự gắn khi gửi.',
    });

    expect(notices[0].description).toBe('Nút "Xác nhận email" sẽ được hệ thống tự gắn khi gửi.');
    expect(notices[0].fromBackend).toBe(true);
    // The generic sentence is not appended to it.
    expect(notices[0].description).not.toMatch(/đồng ý \/ từ chối/);
  });

  it('falls back to the generic wording only when the backend supplied none', () => {
    const notices = describeSystemBlocks({
      ...invitationContract,
      systemActionDescription: null,
    });

    expect(notices[0].fromBackend).toBe(false);
    expect(notices[0].description).toMatch(/đồng ý \/ từ chối/);
  });

  it('says nothing about a block this template does not carry', () => {
    const notices = describeSystemBlocks({
      ...accountContract,
      requiredSystemBlocks: [],
      optionalSystemBlocks: [],
      actionSupported: false,
      actionRequired: false,
    });

    expect(notices).toEqual([]);
  });

  it('describes every block a template carries, each once', () => {
    const notices = describeSystemBlocks(setupProgressContract);

    expect(notices.map(n => n.name).sort()).toEqual(
      ['actionBlock', 'setupSummaryBlock'],
    );
    expect(new Set(notices.map(n => n.name)).size).toBe(notices.length);
    expect(notices.find(n => n.name === 'setupSummaryBlock')!.required).toBe(true);
    expect(notices.find(n => n.name === 'actionBlock')!.required).toBe(false);
  });
});

/**
 * Sender-variable capability (§3).
 */
describe('sender variables are judged by the template CAPABILITY', () => {
  /**
   * The rule the removed contact card could not express. Whether a message names a sender is fixed by
   * what the message IS — a one-time credential names nobody — rather than by a setting an operator
   * moves. There is no level to draft, no card to keep in step, and therefore no state in which the
   * screen and the save disagree.
   */
  const editable: TemplateContract = {
    ...accountContract,
    templateCode: 'VISIT_PARTICIPANT_INVITATION',
    allowedVariables: [...accountContract.allowedVariables, 'senderName', 'senderEmail'],
    optionalVariables: [...accountContract.optionalVariables, 'senderName', 'senderEmail'],
    senderVariableCapability: 'AVAILABLE_EDITABLE_RUNTIME',
    senderVariables: ['senderName', 'senderRole', 'senderEmail', 'senderPhone', 'senderDepartment', 'senderCampus'],
    senderVariablesAllowed: true,
    runtimeEditable: true,
  };

  const credential: TemplateContract = {
    ...accountContract,
    templateCode: 'ACCOUNT_EMAIL_CONFIRMATION',
    senderVariableCapability: 'NOT_AVAILABLE',
    senderVariables: [],
    senderVariablesAllowed: false,
    runtimeEditable: false,
    senderReasonVi: 'Mẫu này mang mã hoặc liên kết dùng một lần nên không hiển thị thông tin người gửi.',
  };

  it('accepts a sender variable on a template whose capability permits one', () => {
    const issues = validateContent(editable, content({
      subjectVi: 'Lời mời',
      bodyVi: '<p>{{fullName}}</p><p>{{senderName}} — {{senderEmail}}</p>{{actionBlock}}',
    }));

    expect(issues).toEqual([]);
  });

  /**
   * Under the generic unknown-variable rule this would read "Biến {{senderName}} không tồn tại trong hệ
   * thống" — false, since it resolves on twenty-eight other templates, and it sends the operator hunting
   * for a typo they did not make. Its own code says the real reason.
   */
  it('refuses one on a credential-bearing template, under its own code, and says why', () => {
    const issues = validateContent(credential, content({
      subjectVi: 'Xác nhận email',
      bodyVi: '<p>{{fullName}} {{roleName}} {{campusName}} {{expiresInHours}}</p><p>{{senderName}}</p>{{actionBlock}}',
    }));

    const refusal = issues.find(i => i.variableName === 'senderName');
    expect(refusal).toBeDefined();
    expect(refusal!.code).toBe(TEMPLATE_ERROR_CODES.senderVariableNotAllowed);
    expect(refusal!.code).not.toBe(TEMPLATE_ERROR_CODES.variableUnknown);
    expect(refusal!.messageVi).toContain('dùng một lần');
  });

  /** A sender variable is not a secret, so nothing stops one appearing in a subject. */
  it('allows a sender variable in a subject', () => {
    const issues = validateContent(editable, content({
      subjectVi: 'Lời mời từ {{senderName}}',
      bodyVi: '<p>{{fullName}}</p>{{actionBlock}}',
    }));

    expect(issues).toEqual([]);
  });

  /**
   * An API built before the capability field answers without it. Absent reads as allowed — the safe
   * direction: hiding the group everywhere until the backend catches up is a worse failure than
   * offering it on three templates that refuse it at save.
   */
  it('treats a contract with no capability field as permitting sender variables', () => {
    const legacy = { ...editable } as Record<string, unknown>;
    delete legacy.senderVariableCapability;
    delete legacy.senderVariablesAllowed;

    const issues = validateContent(
      legacy as unknown as TemplateContract,
      content({ subjectVi: 'x', bodyVi: '<p>{{fullName}}</p><p>{{senderName}}</p>{{actionBlock}}' }),
    );

    expect(issues).toEqual([]);
  });

  /** Capability is about the FLOW; the wording has no say in whether a runtime editor is offered. */
  it('reads runtimeEditable from the capability, not from the body', () => {
    expect(runtimeEditableOf(editable)).toBe(true);
    expect(runtimeEditableOf({ ...editable, senderVariableCapability: 'AVAILABLE_READ_ONLY_RUNTIME', runtimeEditable: false })).toBe(false);
    expect(senderVariablesAllowedOf({ ...editable, senderVariableCapability: 'AVAILABLE_READ_ONLY_RUNTIME', senderVariablesAllowed: true })).toBe(true);
    expect(senderVariablesAllowedOf(credential)).toBe(false);
  });
});


describe('removeSystemBlock', () => {
  it('removes every occurrence, in both placeholder forms', () => {
    const out = removeSystemBlock(
      '<p>a</p>{{setupSummaryBlock}}<p>b</p>%7B%7BsetupSummaryBlock%7D%7D',
      'setupSummaryBlock',
    );

    expect(out).toBe('<p>a</p><p>b</p>');
  });

  it('leaves other blocks and the surrounding text alone', () => {
    const out = removeSystemBlock('{{actionBlock}} giữ nguyên {{setupSummaryBlock}}', 'setupSummaryBlock');

    expect(out).toBe('{{actionBlock}} giữ nguyên ');
  });
});
