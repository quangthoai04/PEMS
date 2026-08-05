/**
 * Variables appended to the end of the body — one defect, pinned as behaviour.
 *
 * The cross-language refusal suite that used to sit beside this one is gone with the contact block:
 * every case in it was "the level says Bắt buộc and the OTHER language dropped the block", and there
 * is no level any more. A sender variable is permitted or not by the template, identically in both
 * languages, so a body cannot be legal in Vietnamese and refused in English.
 *
 * What remains: `insertVariable` asked the DOM what was focused,
 * but clicking a variable chip focuses the CHIP — so neither the subject nor the editor ever matched and
 * every insert fell through to `body + token`. A variable meant for the middle of a greeting arrived
 * after the signature, and one meant for the subject went into the body instead.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

const getEmailTemplateList = vi.fn();
const getEmailTemplateDetail = vi.fn();
const getEmailTemplateContract = vi.fn();
const updateEmailTemplate = vi.fn();
const restoreEmailTemplateDefault = vi.fn();

vi.mock('../../../features/emails/api/emailsApi', () => ({
  emailsApi: {
    getEmailTemplateList: (...a: unknown[]) => getEmailTemplateList(...a),
    getEmailTemplateDetail: (...a: unknown[]) => getEmailTemplateDetail(...a),
    getEmailTemplateContract: (...a: unknown[]) => getEmailTemplateContract(...a),
    updateEmailTemplate: (...a: unknown[]) => updateEmailTemplate(...a),
    restoreEmailTemplateDefault: (...a: unknown[]) => restoreEmailTemplateDefault(...a),
  },
}));

/**
 * What the screen asked the body editor to insert.
 *
 * The screen no longer manipulates the document: it calls `insertVariable` on the shared editor, which
 * owns the caret. So the assertion available here — and the honest one — is about the REQUEST, not about
 * the resulting html. Where the token actually lands is asserted against a real Quill in
 * `EmailRichTextEditor.test.tsx`.
 */
let inserted: { name: string; label: string }[] = [];

/** Lets one test drive the "no live editor attached" fallback. */
let editorReady = true;

vi.mock('../../../features/emails/components/EmailRichTextEditor', async () => {
  const React = await vi.importActual<typeof import('react')>('react');

  return {
    EmailRichTextEditor: React.forwardRef((
      { value, onChange }: { value: string; onChange: (v: string) => void },
      ref: React.Ref<unknown>,
    ) => {
      React.useImperativeHandle(ref, () => ({
        insertVariable: (v: { name: string; label: string }) => { inserted.push(v); },
        isReady: () => editorReady,
      }), [value, onChange]);

      return (
        <textarea
          data-testid="quill"
          value={value}
          onChange={e => onChange(e.target.value)}
        />
      );
    }),
  };
});
vi.mock('react-quill-new/dist/quill.snow.css', () => ({}));

import { TemplateManagement } from '../../../pages/dashboard/emails/TemplateManagement';

const CONTRACT = {
  templateCode: 'ACCOUNT_ROLE_CHANGED',
  module: 'ACCOUNT',
  isSystemTemplate: true,
  variables: [
    { name: 'fullName', label: 'Họ tên', sample: 'Nguyễn Văn An', required: false, sensitive: false, forbiddenInSubject: false },
    { name: 'campusName', label: 'Cơ sở', sample: 'FPTU Hà Nội', required: false, sensitive: false, forbiddenInSubject: false },
  ],
  allowedVariables: ['fullName', 'campusName'],
  requiredVariables: [],
  optionalVariables: ['fullName', 'campusName'],
  requiredSystemBlocks: [],
  optionalSystemBlocks: [],
  systemBlockPreviews: {},
  sensitiveVariables: [],
  forbiddenInSubject: [],
  actionSupported: false,
  actionRequired: false,
  systemActionDescription: null,
  senderVariableCapability: 'AVAILABLE_READ_ONLY_RUNTIME',
  senderVariables: ['senderName', 'senderRole', 'senderEmail', 'senderPhone', 'senderDepartment', 'senderCampus'],
  senderVariablesAllowed: true,
  runtimeEditable: false,
  carriesSecret: false,
  allowCc: true,
  allowBcc: true,
  securityClassification: 'STANDARD',
  editableFields: ['name', 'description', 'subjectVi', 'subjectEn', 'bodyVi', 'bodyEn'],
};

const BASE_DETAIL = {
  emailTemplateId: 7,
  templateCode: 'ACCOUNT_ROLE_CHANGED',
  name: 'Thay đổi vai trò',
  description: 'Gửi khi vai trò thay đổi',
  subjectVi: 'Vai trò của bạn đã thay đổi',
  subjectEn: 'Your role has changed',
  status: 'ACTIVE',
  createdAt: '2026-07-01T08:00:00+07:00',
  updatedAt: null,
  revision: 4,
  hasShippedDefault: true,
};

const BODY_VI = '<p>Chào bạn.</p>';
const BODY_EN = '<p>Hello.</p>';

const pushToast = vi.fn();

async function openEditor(bodyVi: string, bodyEn: string) {
  getEmailTemplateDetail.mockResolvedValue({ data: { ...BASE_DETAIL, bodyVi, bodyEn } });
  render(<TemplateManagement pushToast={pushToast} />);
  fireEvent.click(await screen.findByLabelText('Chỉnh sửa ACCOUNT_ROLE_CHANGED'));
  await screen.findByTestId('save-template');
}

beforeEach(() => {
  vi.clearAllMocks();
  inserted = [];
  editorReady = true;
  getEmailTemplateList.mockResolvedValue({
    data: { items: [{ emailTemplateId: 7, templateCode: 'ACCOUNT_ROLE_CHANGED', name: 'Thay đổi vai trò', description: '' }], totalItems: 1 },
  });
  getEmailTemplateContract.mockResolvedValue({ data: CONTRACT });
});

describe('TemplateManagement — variables land at the caret', () => {
  const subjectInput = () => screen.getByLabelText('Tiêu đề (Subject)') as HTMLInputElement;
  const insertFullName = () => fireEvent.click(screen.getByTitle(/^\{\{fullName\}\}/));

  it('inserts into the subject at the caret, not at the end', async () => {
    await openEditor(BODY_VI, BODY_EN);

    const input = subjectInput();
    expect(input.value).toBe('Vai trò của bạn đã thay đổi');

    // Caret after "Vai trò " (index 8), then the focus moves to the chip — which is exactly the
    // sequence that used to defeat the DOM-focus check.
    input.focus();
    input.setSelectionRange(8, 8);
    fireEvent.select(input);

    insertFullName();

    await waitFor(() =>
      expect(subjectInput().value).toBe('Vai trò {{fullName}}của bạn đã thay đổi'));
  });

  it('replaces the selected run of subject text', async () => {
    await openEditor(BODY_VI, BODY_EN);

    const input = subjectInput();
    // Select "Vai trò" (0..7).
    input.focus();
    input.setSelectionRange(0, 7);
    fireEvent.select(input);

    insertFullName();

    await waitFor(() =>
      expect(subjectInput().value).toBe('{{fullName}} của bạn đã thay đổi'));
  });

  it('keeps the two languages\' subject carets apart', async () => {
    await openEditor(BODY_VI, BODY_EN);

    // A caret deep in the Vietnamese subject…
    const viInput = subjectInput();
    viInput.focus();
    viInput.setSelectionRange(8, 8);
    fireEvent.select(viInput);

    // …must not be applied to the English one, which has its own (and shorter) text.
    fireEvent.click(screen.getByTestId('language-tab-EN'));
    await waitFor(() => expect(subjectInput().value).toBe('Your role has changed'));

    const enInput = subjectInput();
    enInput.focus();
    enInput.setSelectionRange(0, 0);
    fireEvent.select(enInput);

    insertFullName();

    await waitFor(() =>
      expect(subjectInput().value).toBe('{{fullName}}Your role has changed'));

    // The Vietnamese subject is untouched — the insert went to one language only.
    fireEvent.click(screen.getByTestId('language-tab-VI'));
    await waitFor(() => expect(subjectInput().value).toBe('Vai trò của bạn đã thay đổi'));
  });

  /**
   * WHERE a variable lands inside the body is no longer this screen's responsibility.
   *
   * The caret, the selection-replacement and the null-range-on-blur rule all moved into
   * `EmailRichTextEditor`, which has to track them for its own toolbar anyway — one copy of that rule
   * instead of two that can disagree. Those behaviours are now tested against a REAL Quill in
   * `EmailRichTextEditor.test.tsx`; a hand-written text model, however careful, could only ever confirm
   * the model.
   *
   * What remains this screen's job, and is asserted here: routing the click to the BODY editor rather
   * than the subject, and passing the variable's human label along with its name.
   */
  it('asks the body editor to insert, rather than editing the html itself', async () => {
    await openEditor(BODY_VI, BODY_EN);

    // Focus the body, so the screen routes there rather than to the subject.
    fireEvent.focus(screen.getByTestId('quill'));
    insertFullName();

    await waitFor(() => expect(inserted).toEqual([{ name: 'fullName', label: 'Họ tên' }]));
  });

  it('falls back to the head of the body when no editor is attached', async () => {
    editorReady = false;
    try {
      await openEditor(BODY_VI, BODY_EN);
      fireEvent.focus(screen.getByTestId('quill'));

      insertFullName();

      // The head, which is visible and movable — never the tail, which is where the token used to
      // disappear to below the fold.
      await waitFor(() => {
        const body = (screen.getByTestId('quill') as HTMLTextAreaElement).value;
        expect(body.startsWith('{{fullName}}')).toBe(true);
        expect(body.endsWith('{{fullName}}')).toBe(false);
      });
    } finally {
      editorReady = true;
    }
  });
});
