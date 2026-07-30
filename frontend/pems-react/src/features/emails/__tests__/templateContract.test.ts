/**
 * The client-side half of the variable contract (G11-J).
 *
 * These exist because the screen used to validate against a list of eleven variables compiled into
 * the frontend, applied to whichever template happened to be open. The rules below are a mirror of
 * `EmailTemplateContentValidator`; the backend re-validates and stays the authority.
 */
import { describe, expect, it } from 'vitest';
import {
  TEMPLATE_ERROR_CODES,
  applySamples,
  errorCodeOf,
  extractPlaceholders,
  issuesFromError,
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
  allowedVariables: ['fullName', 'roleName', 'campusName', 'expiresInHours', 'actionBlock'],
  requiredVariables: [],
  optionalVariables: ['fullName', 'roleName', 'campusName', 'expiresInHours', 'actionBlock'],
  sensitiveVariables: [],
  forbiddenInSubject: ['actionBlock'],
  requiresActionBlock: false,
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
  allowedVariables: ['fullName', 'otpCode', 'expireMinutes', 'actionBlock'],
  requiredVariables: ['otpCode'],
  optionalVariables: ['fullName', 'expireMinutes', 'actionBlock'],
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
  allowedVariables: ['recipientName', 'actionBlock'],
  requiredVariables: ['actionBlock'],
  optionalVariables: ['recipientName'],
  requiresActionBlock: true,
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
