/**
 * The template variable contract, as the backend defines it (G11-J).
 *
 * There is deliberately no variable list in this file. The screen used to carry one — eleven names,
 * five labelled "common" and six "logistics" — and applied it to whichever template was open. Opening
 * `ACCOUNT_EMAIL_CONFIRMATION`, whose variables are fullName / roleName / campusName / expiresInHours,
 * matched none of them, so a canonical template nobody had touched greeted the operator with "Một số
 * biến chưa được định nghĩa hoặc sai định dạng" for every variable it legitimately used, while the
 * sidebar offered logistics variables that template can never be given a value for.
 *
 * Everything below is fetched per template code. `GET /email-templates/contract/{code}`.
 */

/** Stable codes from `EmailErrorCodes`. Matched on the code, never on the Vietnamese message text. */
export const TEMPLATE_ERROR_CODES = {
  variableUnknown: 'EMAIL_TEMPLATE_VARIABLE_UNKNOWN',
  variableMalformed: 'EMAIL_TEMPLATE_VARIABLE_MALFORMED',
  requiredVariableMissing: 'EMAIL_TEMPLATE_REQUIRED_VARIABLE_MISSING',
  runtimeVariableMissing: 'EMAIL_TEMPLATE_RUNTIME_VARIABLE_MISSING',
  subjectForbiddenSensitive: 'EMAIL_TEMPLATE_SUBJECT_FORBIDDEN_SENSITIVE_VARIABLE',
  actionBlockRequired: 'EMAIL_TEMPLATE_ACTION_BLOCK_REQUIRED',
  catalogFixed: 'EMAIL_TEMPLATE_CATALOG_FIXED',
  fieldImmutable: 'EMAIL_TEMPLATE_FIELD_IMMUTABLE',
  concurrencyConflict: 'EMAIL_TEMPLATE_CONCURRENCY_CONFLICT',
  defaultUnavailable: 'EMAIL_TEMPLATE_DEFAULT_UNAVAILABLE',
} as const;

/** The four editable content fields, matching the API's property names. */
export type TemplateContentField = 'subjectVi' | 'subjectEn' | 'bodyVi' | 'bodyEn';

export interface TemplateContractVariable {
  name: string;
  /** Human label in the requested language. */
  label: string;
  /** What the preview substitutes. Never a real secret — the OTP sample is a fixed fake. */
  sample: string;
  /** Removing this from the content breaks the message; the save is refused. */
  required: boolean;
  /** The value is a credential. */
  sensitive: boolean;
  /** May not appear in a subject: subjects are stored and shown in the email history. */
  forbiddenInSubject: boolean;
}

export interface TemplateContract {
  templateCode: string;
  /** ACCOUNT / AUTH / VISIT_REQUEST / LOGISTICS / REPORT … */
  module: string;
  /**
   * False for a historical row — kept because a sent email or draft still points at it — which is
   * therefore not editable. The screen says so instead of showing a broken editor.
   */
  isSystemTemplate: boolean;
  variables: TemplateContractVariable[];
  allowedVariables: string[];
  requiredVariables: string[];
  optionalVariables: string[];
  sensitiveVariables: string[];
  forbiddenInSubject: string[];
  /** The body must keep `{{actionBlock}}`. */
  requiresActionBlock: boolean;
  /** The message carries a one-time code or a personal action link. */
  carriesSecret: boolean;
  allowCc: boolean;
  allowBcc: boolean;
  securityClassification: 'SENSITIVE' | 'STANDARD' | string;
  editableFields: string[];
}

/** One refused edit, addressed to a field and — where it applies — a variable. */
export interface TemplateContentIssue {
  field: TemplateContentField | string;
  code: string;
  variableName?: string | null;
  messageVi: string;
  messageEn: string;
  severity: 'ERROR' | 'WARNING' | string;
}

/**
 * How far the contract has got. Validation runs ONLY in `ready`.
 *
 * The previous screen had no such notion: it validated from its first render, against a list that had
 * nothing to do with the open template, so the warning was on screen before any request had been made.
 * `error` deliberately does not fall back to another template's contract — showing warnings derived
 * from the wrong template is worse than showing none.
 */
export type ContractState =
  | { status: 'idle' }
  | { status: 'loading'; templateCode: string }
  | { status: 'ready'; templateCode: string; contract: TemplateContract }
  | { status: 'error'; templateCode: string; message: string };

/** The placeholder form the backend parser accepts, plus the URL-encoded form a rich editor stores. */
const PLACEHOLDER_PATTERN = /(?:\{\{|%7B%7B)\s*([A-Za-z_][A-Za-z0-9_]*)\s*(?:\}\}|%7D%7D)/g;

/** Every distinct placeholder name written in the given content. */
export function extractPlaceholders(...parts: (string | undefined | null)[]): string[] {
  const found = new Set<string>();

  for (const part of parts) {
    if (!part) continue;
    // matchAll needs a fresh lastIndex; the regex is module-scope and /g is stateful.
    PLACEHOLDER_PATTERN.lastIndex = 0;
    for (const m of part.matchAll(PLACEHOLDER_PATTERN)) found.add(m[1]);
  }

  return [...found];
}

/**
 * The client-side mirror of `EmailTemplateContentValidator`, so the operator is told at the field
 * instead of after a round trip. The backend re-validates and stays the authority; this never permits
 * something the backend would refuse, and it never refuses something the backend would permit.
 */
export function validateContent(
  contract: TemplateContract,
  content: Record<TemplateContentField, string>,
): TemplateContentIssue[] {
  // A historical row has no contract to validate against. The API answers one for it anyway — so the
  // editor can say what the row IS rather than showing a failed request — but with empty variable
  // lists, and validating against those declares every placeholder in the body unknown. Opening
  // VISIT_REQUEST_APPROVED, a template from the pre-registry catalog, produced four ERROR rows for
  // {{RequestCode}}, {{RecipientName}}, {{DelegationName}} and {{DecisionNote}} — every variable it
  // legitimately uses — and those errors then disabled the save button, which read as "this template
  // is broken" rather than "this template is not ours to check".
  //
  // Returning nothing is not a relaxation: the backend refuses to save a non-system template outright
  // with EMAIL_TEMPLATE_CATALOG_FIXED, so there is no content this could wave through. The screen
  // states the row's status instead; see the notice in TemplateManagement.
  if (!contract.isSystemTemplate) return [];

  const issues: TemplateContentIssue[] = [];
  const fields: TemplateContentField[] = ['subjectVi', 'bodyVi', 'subjectEn', 'bodyEn'];

  for (const field of fields) {
    const isSubject = field === 'subjectVi' || field === 'subjectEn';

    for (const name of extractPlaceholders(content[field])) {
      if (!contract.allowedVariables.includes(name)) {
        issues.push({
          field,
          code: TEMPLATE_ERROR_CODES.variableUnknown,
          variableName: name,
          messageVi: `Biến {{${name}}} không thuộc mẫu ${contract.templateCode}; khi gửi sẽ không có giá trị.`,
          messageEn: `Variable {{${name}}} does not belong to ${contract.templateCode}.`,
          severity: 'ERROR',
        });
        continue;
      }

      if (isSubject && contract.forbiddenInSubject.includes(name)) {
        issues.push({
          field,
          code: TEMPLATE_ERROR_CODES.subjectForbiddenSensitive,
          variableName: name,
          messageVi: `Biến {{${name}}} không được đặt trong tiêu đề: tiêu đề được lưu lại và hiển thị trong lịch sử email.`,
          messageEn: `Variable {{${name}}} may not appear in a subject.`,
          severity: 'ERROR',
        });
      }
    }
  }

  // Required variables are judged per language across subject + body together, the same way the backend
  // does it: a template may legitimately keep the code in the body and the subject plain — and it must,
  // because a credential in a subject is refused outright above.
  const languages: { subject: TemplateContentField; body: TemplateContentField }[] = [
    { subject: 'subjectVi', body: 'bodyVi' },
    { subject: 'subjectEn', body: 'bodyEn' },
  ];

  for (const { subject, body } of languages) {
    // An untouched language is not a partial edit; it means this language is not maintained here.
    if (!content[subject]?.trim() && !content[body]?.trim()) continue;

    const present = extractPlaceholders(content[subject], content[body]);

    for (const required of contract.requiredVariables) {
      if (present.includes(required)) continue;

      const isActionBlock = required === 'actionBlock';
      issues.push({
        field: body,
        code: isActionBlock
          ? TEMPLATE_ERROR_CODES.actionBlockRequired
          : TEMPLATE_ERROR_CODES.requiredVariableMissing,
        variableName: required,
        messageVi: isActionBlock
          ? 'Mẫu này cần {{actionBlock}} — khu vực nút thao tác do hệ thống gắn khi gửi. Bỏ nó đi thì người nhận không có nút nào để bấm.'
          : `Mẫu này bắt buộc phải chứa biến {{${required}}}.`,
        messageEn: isActionBlock
          ? 'This template needs {{actionBlock}} — the action area the system attaches when sending.'
          : `This template must contain {{${required}}}.`,
        severity: 'ERROR',
      });
    }
  }

  return issues;
}

/** Substitutes the contract's samples so the operator sees the shape of a real message. */
export function applySamples(contract: TemplateContract, content: string): string {
  let out = content;

  for (const v of contract.variables) {
    // Case-SENSITIVE, matching the backend parser. A case-insensitive replace here would render
    // {{FullName}} as a value while the real send left it unresolved and refused to go out.
    out = out.replace(
      new RegExp(`(?:\\{\\{|%7B%7B)\\s*${v.name}\\s*(?:\\}\\}|%7D%7D)`, 'g'),
      v.sample,
    );
  }

  return out;
}

/** Reads structured issues out of an API error, falling back to nothing rather than guessing. */
export function issuesFromError(error: unknown): TemplateContentIssue[] {
  const data = (error as { response?: { data?: { issues?: unknown } } })?.response?.data;
  return Array.isArray(data?.issues) ? (data.issues as TemplateContentIssue[]) : [];
}

/** The stable error code of an API failure, if it carried one. */
export function errorCodeOf(error: unknown): string | undefined {
  return (error as { response?: { data?: { errorCode?: string } } })?.response?.data?.errorCode;
}
