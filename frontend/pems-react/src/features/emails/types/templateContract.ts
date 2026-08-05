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
import { SPACE_RUN_WARNING, htmlHasSpaceRun } from '../utils/emailEditorPaste';

/** Stable codes from `EmailErrorCodes`. Matched on the code, never on the Vietnamese message text. */
export const TEMPLATE_ERROR_CODES = {
  variableUnknown: 'EMAIL_TEMPLATE_VARIABLE_UNKNOWN',
  variableMalformed: 'EMAIL_TEMPLATE_VARIABLE_MALFORMED',
  requiredVariableMissing: 'EMAIL_TEMPLATE_REQUIRED_VARIABLE_MISSING',
  runtimeVariableMissing: 'EMAIL_TEMPLATE_RUNTIME_VARIABLE_MISSING',
  subjectForbiddenSensitive: 'EMAIL_TEMPLATE_SUBJECT_FORBIDDEN_SENSITIVE_VARIABLE',
  actionBlockRequired: 'EMAIL_TEMPLATE_ACTION_BLOCK_REQUIRED',
  requiredBlockMissing: 'EMAIL_TEMPLATE_REQUIRED_BLOCK_NOT_IN_BODY',
  systemBlockNotAllowed: 'EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWED',
  /**
   * A `{{sender*}}` placeholder on a template whose capability is NOT_AVAILABLE.
   *
   * Its own code rather than `variableUnknown`, which is what a generic check would produce. "Biến không
   * tồn tại" is false — the variable works on twenty-eight other templates — and it sends the operator
   * hunting for a typo they did not make. The real rule is that THIS message carries a one-time
   * credential and does not name a person.
   */
  senderVariableNotAllowed: 'EMAIL_TEMPLATE_SENDER_VARIABLE_NOT_ALLOWED',
  /**
   * The field lines something up with a run of spaces (V4 §7.4).
   *
   * Blocking rather than advisory: HTML collapses runs of plain spaces, so the column arrives ragged,
   * and the editor's own `&nbsp;` spelling is the worse outcome — it holds its width in the composer and
   * then refuses to wrap on a phone. The same code comes back from the server, so a save attempted
   * around this screen is answered identically.
   */
  spaceRunUnsupported: 'EMAIL_TEMPLATE_CONSECUTIVE_SPACES_NOT_ALLOWED',
  templateNotFound: 'EMAIL_TEMPLATE_NOT_FOUND',
  catalogFixed: 'EMAIL_TEMPLATE_CATALOG_FIXED',
  fieldImmutable: 'EMAIL_TEMPLATE_FIELD_IMMUTABLE',
  concurrencyConflict: 'EMAIL_TEMPLATE_CONCURRENCY_CONFLICT',
  defaultUnavailable: 'EMAIL_TEMPLATE_DEFAULT_UNAVAILABLE',
} as const;

/**
 * Every trusted block the backend can inject, mirroring `EmailTrustedBlocks.All`.
 *
 * Needed as a standalone list — not merely derived from the open contract — so that a block written
 * into the WRONG template can be told apart from a mistyped variable. Without it, pasting
 * `{{setupSummaryBlock}}` into an account notice answers "biến không tồn tại trong hệ thống" about a
 * placeholder that exists and is mandatory two templates away, which sends the operator looking for a
 * variable to define instead of a block to delete.
 *
 * `systemBlockNamesMatchBackend` in the tests pins this against the contract the API serves, so a block
 * added on the backend cannot quietly fall through to the variable path here.
 */
export const SYSTEM_BLOCK_NAMES = [
  'actionBlock',
  'setupSummaryBlock',
  // contactInformationBlock was here. It is not a block any more and not a variable either — it is six
  // ordinary {{sender*}} variables, which the picker offers under "Thông tin người gửi".
] as const;

export type SystemBlockName = (typeof SYSTEM_BLOCK_NAMES)[number];

/** True when the placeholder names a backend-built block rather than a data variable. */
export function isSystemBlock(name: string): name is SystemBlockName {
  return (SYSTEM_BLOCK_NAMES as readonly string[]).includes(name);
}

/** Human wording for each block, used wherever one is shown as a protected region. */
export const SYSTEM_BLOCK_LABELS: Record<string, { title: string; hint: string }> = {
  actionBlock: {
    title: 'Khu vực nút thao tác',
    hint: 'Hệ thống gắn các nút (đồng ý / từ chối / xem chi tiết) kèm liên kết thật khi gửi.',
  },
  setupSummaryBlock: {
    title: 'Bảng thông tin chuẩn bị',
    hint: 'Hệ thống dựng các bảng khách, thành phần, lịch trình và trạng thái chuẩn bị khi gửi.',
  },
};

/** The six sender variables, mirroring `EmailSenderVariableNames.All`. */
export const SENDER_VARIABLE_NAMES = [
  'senderName',
  'senderRole',
  'senderEmail',
  'senderPhone',
  'senderDepartment',
  'senderCampus',
] as const;

export type SenderVariableName = (typeof SENDER_VARIABLE_NAMES)[number];

/** True when the placeholder is one of the six sender variables. */
export function isSenderVariable(name: string): name is SenderVariableName {
  return (SENDER_VARIABLE_NAMES as readonly string[]).includes(name);
}

/** The four editable content fields, matching the API's property names. */
export type TemplateContentField = 'subjectVi' | 'subjectEn' | 'bodyVi' | 'bodyEn';

/** One system block as the editor lists it: named once, with ONE description. */
export interface SystemBlockNotice {
  name: string;
  required: boolean;
  /** The backend's description of this template's own action, or the generic wording for the block. */
  description: string;
  /** True when the sentence came from the backend for THIS template rather than from the table above. */
  fromBackend: boolean;
}

/**
 * Every system block this template may carry, each appearing EXACTLY ONCE.
 *
 * This is the fix for the duplicated action hint. The screen used to render the action block from
 * `actionSupported` — with the backend's specific description — and then render the required/optional
 * lists as well, which contain the same block, with the generic sentence from `SYSTEM_BLOCK_LABELS`. An
 * operator opening ACCOUNT_EMAIL_CONFIRMATION was told both that the system attaches a "Xác nhận email"
 * button and that it attaches "đồng ý / từ chối / xem chi tiết" buttons, one under the other. The second
 * sentence was not merely redundant, it was false for that template.
 *
 * The rule is stated once, here: a block is listed once, and a description the backend supplied for this
 * template beats the generic one. Nothing is concatenated.
 */
export function describeSystemBlocks(contract: TemplateContract): SystemBlockNotice[] {
  const seen = new Set<string>();
  const notices: SystemBlockNotice[] = [];

  const add = (name: string, required: boolean) => {
    if (seen.has(name)) return;
    seen.add(name);

    // Only the action block has per-template metadata today. It is read from the contract rather than
    // from a list in this file, so a template with no action spec shows no action block at all — which
    // is the second half of the same defect: the generic list would otherwise announce buttons on a
    // template whose send path attaches none.
    const specific = name === 'actionBlock' ? contract.systemActionDescription : null;

    notices.push({
      name,
      required,
      description: specific
        ?? SYSTEM_BLOCK_LABELS[name]?.hint
        ?? 'Hệ thống điền nội dung khi gửi.',
      fromBackend: Boolean(specific),
    });
  };

  for (const name of contract.requiredSystemBlocks ?? []) add(name, true);
  for (const name of contract.optionalSystemBlocks ?? []) add(name, false);

  return notices;
}

/**
 * True unless the backend says this template may never name a sender.
 *
 * Absent reads as allowed, matching the contract's own default: an API built before the field answers
 * without it, and the alternative — hiding the variable group on every template until the backend
 * catches up — would be a worse failure than showing it on three that refuse it at save.
 */
export function senderVariablesAllowedOf(
  contract: Pick<TemplateContract, 'senderVariablesAllowed' | 'senderVariableCapability'>,
): boolean {
  if (typeof contract.senderVariablesAllowed === 'boolean') return contract.senderVariablesAllowed;
  return contract.senderVariableCapability !== 'NOT_AVAILABLE';
}

/** True when the send flow may offer "Chỉnh sửa" for this template. */
export function runtimeEditableOf(
  contract: Pick<TemplateContract, 'runtimeEditable' | 'senderVariableCapability'>,
): boolean {
  if (typeof contract.runtimeEditable === 'boolean') return contract.runtimeEditable;
  return contract.senderVariableCapability === 'AVAILABLE_EDITABLE_RUNTIME';
}

/**
 * Removes every occurrence of one system block from a piece of content.
 *
 * Used by the "xóa khối không hợp lệ" action, which is offered — never applied automatically. A block an
 * operator did not expect to be illegal is still text they wrote around; deleting it under them would be
 * an edit they never made and cannot see.
 */
export function removeSystemBlock(content: string, name: string): string {
  return content.replace(
    new RegExp(`(?:\\{\\{|%7B%7B)\\s*${name}\\s*(?:\\}\\}|%7D%7D)`, 'g'),
    '',
  );
}

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
  /**
   * DATA VARIABLES ONLY — a system block is never listed here. A placeholder is checked against this
   * list only after it has been established that it is not a block; see `validateContent`.
   */
  allowedVariables: string[];
  requiredVariables: string[];
  optionalVariables: string[];
  /**
   * System blocks the body must keep. The backend builds their markup, so an operator may move one but
   * can neither author its contents nor supply a value — they are shown as protected regions, not as
   * variables.
   */
  requiredSystemBlocks: string[];
  /** System blocks this template may legally carry but does not have to. */
  optionalSystemBlocks: string[];
  /**
   * Inert sample markup per block, from the backend — the same helpers the send uses, so the preview
   * carries the real labels and styling. The buttons are `<span>`s with no href at all, so a click
   * cannot navigate and no token or business URL exists to leak.
   *
   */
  systemBlockPreviews: Record<string, string>;
  sensitiveVariables: string[];
  forbiddenInSubject: string[];
  /** True when the template has an action spec. */
  actionSupported: boolean;
  /** True when the action block is strictly required. */
  actionRequired: boolean;
  /** The backend-provided description of the system action. */
  systemActionDescription: string | null;
  /**
   * NOT_AVAILABLE / AVAILABLE_READ_ONLY_RUNTIME / AVAILABLE_EDITABLE_RUNTIME.
   *
   * This replaces the four contact fields the contract used to carry (supported / required /
   * requirement / settings-editable). Those described a CONFIGURATION an operator moved between three
   * levels, and the screen needed all four to work out which state it was in. There is no configuration
   * here — the capability is fixed by what the message is and how it is sent — so one value answers
   * every question the screen has.
   *
   * Optional on the type because an API built before it answers without one; absent reads as
   * AVAILABLE_READ_ONLY_RUNTIME, which offers the variables and no runtime editor — the safe direction.
   */
  senderVariableCapability?: 'NOT_AVAILABLE' | 'AVAILABLE_READ_ONLY_RUNTIME' | 'AVAILABLE_EDITABLE_RUNTIME' | string;
  /** The `{{sender*}}` names to offer under "Thông tin người gửi", or empty when not permitted. */
  senderVariables?: string[];
  /** True when the body may contain `{{sender*}}`. */
  senderVariablesAllowed?: boolean;
  /** True when the send flow may offer "Chỉnh sửa" — a property of the flow, not of the wording. */
  runtimeEditable?: boolean;
  /** Stable reason for the capability — matched on; the sentences below are for people. */
  senderReasonCode?: string | null;
  senderReasonVi?: string | null;
  senderReasonEn?: string | null;
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
/**
 * Nothing, now.
 *
 * It used to carry the contact card's UNSAVED level, because validation had to follow a draft the
 * contract knew nothing about: the same body was legal or illegal depending on a toggle elsewhere on the
 * screen. Sender variables have no such setting — a placeholder is permitted or not by the template's
 * fixed capability — so validation depends only on the contract and the content.
 *
 * Kept as a type so the signature does not churn if a future option appears.
 */
export interface ValidateContentOptions {
  [key: string]: never;
}

export function validateContent(
  contract: TemplateContract,
  content: Record<TemplateContentField, string>,
  _options: ValidateContentOptions = {},
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

  const senderAllowed = senderVariablesAllowedOf(contract);

  for (const field of fields) {
    const isSubject = field === 'subjectVi' || field === 'subjectEn';

    for (const name of extractPlaceholders(content[field])) {
      // A system block is judged as a block, before any variable rule can reach it — mirroring
      // EmailTemplateContentValidator. Checking it against `allowedVariables` was how a legal, and in
      // some templates REQUIRED, {{contactInformationBlock}} came to be reported as a variable that
      // "does not belong to this template": true of the variable list, and irrelevant, because the
      // block was never in it and was never supposed to be.
      if (isSystemBlock(name)) {
        const allowed = contract.requiredSystemBlocks.includes(name)
          || contract.optionalSystemBlocks.includes(name);

        if (!allowed) {
          issues.push({
            field,
            code: TEMPLATE_ERROR_CODES.systemBlockNotAllowed,
            variableName: name,
            messageVi: `Khối hệ thống {{${name}}} không dùng được ở mẫu ${contract.templateCode}; khi gửi sẽ không có gì thay thế vào chỗ này. Hãy xóa khối khỏi nội dung.`,
            messageEn: `System block {{${name}}} is not available on ${contract.templateCode}.`,
            severity: 'ERROR',
          });
          continue;
        }

        if (isSubject) {
          issues.push({
            field,
            code: TEMPLATE_ERROR_CODES.subjectForbiddenSensitive,
            variableName: name,
            messageVi: `Khối hệ thống {{${name}}} không được đặt trong tiêu đề: khối sinh ra HTML và có thể chứa liên kết dùng một lần, trong khi tiêu đề được lưu lại trong lịch sử email.`,
            messageEn: `System block {{${name}}} may not appear in a subject.`,
            severity: 'ERROR',
          });
        }

        continue;
      }

      // A sender variable on a template that may not name one is answered BEFORE the generic
      // unknown-variable rule, mirroring EmailTemplateContentValidator. Reported as "không tồn tại
      // trong hệ thống" it would be false — the variable works on twenty-eight other templates — and it
      // would send the operator hunting for a typo instead of telling them this message carries a
      // one-time credential and does not name a person.
      if (isSenderVariable(name) && !senderAllowed) {
        issues.push({
          field,
          code: TEMPLATE_ERROR_CODES.senderVariableNotAllowed,
          variableName: name,
          messageVi: `Biến {{${name}}} không dùng được ở mẫu ${contract.templateCode}: `
            + (contract.senderReasonVi ?? 'mẫu này không hiển thị thông tin người gửi.')
            + ` Hãy xóa biến khỏi ${describeFieldVi(field)}.`,
          messageEn: `Variable {{${name}}} is not available on ${contract.templateCode}: `
            + (contract.senderReasonEn ?? 'this template does not show sender information.'),
          severity: 'ERROR',
        });
        continue;
      }

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

    // Spacing is judged per field, so an operator with a clean Vietnamese tab is told that the English
    // one is what is holding the save. A subject counts too: it is one line of visible text, and a run
    // in it survives into the recipient's list view and into the stored history exactly as typed.
    if (htmlHasSpaceRun(content[field] ?? '')) {
      issues.push({
        field,
        code: TEMPLATE_ERROR_CODES.spaceRunUnsupported,
        variableName: null,
        messageVi: SPACE_RUN_WARNING,
        messageEn: 'The content has runs of consecutive spaces, which make the email render incorrectly '
          + 'on phones. Use alignment, indentation or a table instead.',
        severity: 'ERROR',
      });
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

      issues.push({
        field: body,
        code: TEMPLATE_ERROR_CODES.requiredVariableMissing,
        variableName: required,
        messageVi: `Mẫu này bắt buộc phải chứa biến {{${required}}}.`,
        messageEn: `This template must contain {{${required}}}.`,
        severity: 'ERROR',
      });
    }

    // Each missing block reports under the code that names ITS repair.
    //
    // Straight from the contract's own list now. The contact block used to be taken from the LEVEL
    // instead, because the level could change on screen after the contract was fetched — the whole
    // reason `validateContent` needed an options bag. A required block is a fact about the template
    // again, so there is nothing to override.
    for (const required of contract.requiredSystemBlocks) {
      if (present.includes(required)) continue;

      issues.push({
        field: body,
        code: blockMissingCode(required),
        variableName: required,
        messageVi: blockMissingMessageVi(required),
        messageEn: blockMissingMessageEn(required),
        severity: 'ERROR',
      });
    }
  }

  return issues;
}

/**
 * Which field a message is about, in words. The field NAME still travels on the issue so the screen can
 * anchor the message under the right input; this is what the SENTENCE says, and a sentence that does not
 * name the language leaves somebody with a clean Vietnamese tab wondering what is wrong with it.
 */
function describeFieldVi(field: TemplateContentField | string): string {
  switch (field) {
    case 'subjectVi': return 'tiêu đề tiếng Việt';
    case 'subjectEn': return 'tiêu đề tiếng Anh';
    case 'bodyVi': return 'nội dung tiếng Việt';
    case 'bodyEn': return 'nội dung tiếng Anh';
    default: return 'nội dung';
  }
}

function describeFieldEn(field: TemplateContentField | string): string {
  switch (field) {
    case 'subjectVi': return 'the Vietnamese subject';
    case 'subjectEn': return 'the English subject';
    case 'bodyVi': return 'the Vietnamese body';
    case 'bodyEn': return 'the English body';
    default: return 'the content';
  }
}

function blockMissingCode(block: string): string {
  if (block === 'actionBlock') return TEMPLATE_ERROR_CODES.actionBlockRequired;
  return TEMPLATE_ERROR_CODES.requiredBlockMissing;
}

function blockMissingMessageVi(block: string): string {
  switch (block) {
    case 'actionBlock':
      return 'Mẫu này cần {{actionBlock}} — khu vực nút thao tác do hệ thống gắn khi gửi. Bỏ nó đi thì người nhận không có nút nào để bấm.';
    case 'setupSummaryBlock':
      return 'Mẫu này cần {{setupSummaryBlock}} — các bảng thông tin chuẩn bị do hệ thống dựng khi gửi. Bỏ nó đi thì email chỉ còn câu dẫn, không có nội dung cập nhật nào.';
    default:
      return `Mẫu này bắt buộc phải chứa khối hệ thống {{${block}}}.`;
  }
}

function blockMissingMessageEn(block: string): string {
  switch (block) {
    case 'actionBlock':
      return 'This template needs {{actionBlock}} — the action area the system attaches when sending.';
    case 'setupSummaryBlock':
      return 'This template needs {{setupSummaryBlock}} — the setup tables the system builds when sending.';
    default:
      return `This template must contain the system block {{${block}}}.`;
  }
}

/**
 * Substitutes system blocks with the inert sample markup the backend supplied, so the preview pane
 * shows the action buttons a recipient would see instead of the literal text `{{actionBlock}}`.
 *
 * The EDITOR keeps the placeholder — an operator has to be able to see and move it — so this runs only
 * on the copy being previewed. A block with no sample renders as nothing rather than as raw braces,
 * which would read as an unresolved variable.
 */
export function applySystemBlocks(
  contract: TemplateContract,
  content: string,
): string {
  let out = content;
  const samples = contract.systemBlockPreviews;

  for (const name of SYSTEM_BLOCK_NAMES) {
    const pattern = new RegExp(`(?:\\{\\{|%7B%7B)\\s*${name}\\s*(?:\\}\\}|%7D%7D)`, 'g');
    if (!pattern.test(out)) continue;

    pattern.lastIndex = 0;
    out = out.replace(pattern, samples[name] ?? '');
  }

  return out;
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
