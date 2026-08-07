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
      { value, onChange, onEditorActivated }: {
        value: string;
        onChange: (v: string) => void;
        onEditorActivated?: () => void;
      },
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
          // The real editor reports this from Quill's own selection — see `onEditorActivated`. The
          // stand-in reports it from focus, which is the same statement: "the body is what is being
          // written in now". Without it this mock would test a screen that cannot be told.
          onFocus={() => onEditorActivated?.()}
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
    // Long, multi-line or confidential values the contract will not allow in a heading — the subject is
    // one line a mail client shows in a list.
    { name: 'agendaTable', label: 'Bảng lịch trình', sample: '…', required: false, sensitive: false, forbiddenInSubject: true },
  ],
  allowedVariables: ['fullName', 'campusName', 'agendaTable'],
  requiredVariables: [],
  optionalVariables: ['fullName', 'campusName', 'agendaTable'],
  requiredSystemBlocks: [],
  optionalSystemBlocks: [],
  systemBlockPreviews: {},
  sensitiveVariables: [],
  forbiddenInSubject: ['agendaTable'],
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

  /**
   * The defect this suite was extended for: a variable picked while the caret was in the BODY went into
   * the SUBJECT.
   *
   * The screen learns about the subject from that input's own focus and select handlers, and used to
   * learn nothing at all about the body — the shared editor keeps its caret to itself, rightly, so
   * `lastInsertTarget` was left saying "subject" for the rest of the session. Every variable an operator
   * picked afterwards was appended to the heading, in front of them, while they were looking at the body.
   */
  it('sends the variable to the body after the caret has moved there from the subject', async () => {
    await openEditor(BODY_VI, BODY_EN);

    const input = subjectInput();
    input.focus();
    input.setSelectionRange(3, 3);
    fireEvent.select(input);

    // …and then into the body, which is the step the screen could not see.
    fireEvent.focus(screen.getByTestId('quill'));
    insertFullName();

    await waitFor(() => expect(inserted).toEqual([{ name: 'fullName', label: 'Họ tên' }]));
    expect(subjectInput().value).toBe('Vai trò của bạn đã thay đổi');
  });

  it('sends it back to the subject when the caret returns there', async () => {
    await openEditor(BODY_VI, BODY_EN);

    fireEvent.focus(screen.getByTestId('quill'));

    const input = subjectInput();
    input.focus();
    input.setSelectionRange(0, 0);
    fireEvent.select(input);

    insertFullName();

    await waitFor(() =>
      expect(subjectInput().value).toBe('{{fullName}}Vai trò của bạn đã thay đổi'));
    expect(inserted).toEqual([]);
  });

  /** The two tabs are two documents: a target remembered in one must not decide the other. */
  it('keeps the target apart across a language switch', async () => {
    await openEditor(BODY_VI, BODY_EN);

    // Vietnamese: the subject.
    const viInput = subjectInput();
    viInput.focus();
    viInput.setSelectionRange(0, 0);
    fireEvent.select(viInput);

    // English: the body.
    fireEvent.click(screen.getByTestId('language-tab-EN'));
    await waitFor(() => expect(subjectInput().value).toBe('Your role has changed'));
    fireEvent.focus(screen.getByTestId('quill'));

    insertFullName();

    // Into the English BODY — not into the English subject, and not into the Vietnamese one.
    await waitFor(() => expect(inserted).toEqual([{ name: 'fullName', label: 'Họ tên' }]));
    expect(subjectInput().value).toBe('Your role has changed');

    fireEvent.click(screen.getByTestId('language-tab-VI'));
    await waitFor(() => expect(subjectInput().value).toBe('Vai trò của bạn đã thay đổi'));
  });

  it('keeps the target apart the other way round', async () => {
    await openEditor(BODY_VI, BODY_EN);

    // Vietnamese: the body.
    fireEvent.focus(screen.getByTestId('quill'));

    // English: the subject.
    fireEvent.click(screen.getByTestId('language-tab-EN'));
    await waitFor(() => expect(subjectInput().value).toBe('Your role has changed'));
    const enInput = subjectInput();
    enInput.focus();
    enInput.setSelectionRange(0, 0);
    fireEvent.select(enInput);

    insertFullName();

    await waitFor(() =>
      expect(subjectInput().value).toBe('{{fullName}}Your role has changed'));
    expect(inserted).toEqual([]);
  });

  /**
   * A subject is one line of plain text shown in a mail client's list, and the contract marks the
   * variables that may not appear in one. The save refuses them and so does the backend — but a refusal
   * at save time names a field, not the chip that put the placeholder there, so the operator is left
   * hunting. Said at the click instead, and nothing is written.
   */
  it('refuses a forbidden-in-subject variable at the click, rather than at the save', async () => {
    await openEditor(BODY_VI, BODY_EN);

    const input = subjectInput();
    input.focus();
    input.setSelectionRange(0, 0);
    fireEvent.select(input);

    fireEvent.click(screen.getByTitle(/^\{\{agendaTable\}\}/));

    await waitFor(() => expect(pushToast).toHaveBeenCalledWith(
      'error', expect.stringContaining('không được đặt trong tiêu đề'),
    ));
    expect(subjectInput().value).toBe('Vai trò của bạn đã thay đổi');
    expect(inserted).toEqual([]);
  });

  /** The same variable is perfectly legal in the body, and must still go there. */
  it('allows that variable in the body', async () => {
    await openEditor(BODY_VI, BODY_EN);

    fireEvent.focus(screen.getByTestId('quill'));
    fireEvent.click(screen.getByTitle(/^\{\{agendaTable\}\}/));

    await waitFor(() =>
      expect(inserted).toEqual([{ name: 'agendaTable', label: 'Bảng lịch trình' }]));
  });

  /**
   * A caret belongs to the text it was measured in. Carried into the next template it decides where that
   * template's first variable lands — and an offset taken in a long heading points into the middle of a
   * short one, or past the end of it.
   */
  it('forgets the subject target when the editor is closed and opened again', async () => {
    await openEditor(BODY_VI, BODY_EN);

    const input = subjectInput();
    input.focus();
    input.setSelectionRange(8, 8);
    fireEvent.select(input);

    fireEvent.click(screen.getByRole('button', { name: 'Hủy' }));
    await waitFor(() => expect(screen.queryByTestId('save-template')).toBeNull());

    fireEvent.click(await screen.findByLabelText('Chỉnh sửa ACCOUNT_ROLE_CHANGED'));
    await screen.findByTestId('save-template');

    insertFullName();

    // The body, which is where a variable goes when nothing has been focused — not the subject the
    // PREVIOUS session was writing in.
    await waitFor(() => expect(inserted).toEqual([{ name: 'fullName', label: 'Họ tên' }]));
    expect(subjectInput().value).toBe('Vai trò của bạn đã thay đổi');
  });

  it('forgets it after a restore to the shipped wording', async () => {
    restoreEmailTemplateDefault.mockResolvedValue({
      data: {
        ...BASE_DETAIL,
        subjectVi: 'Mặc định',
        bodyVi: BODY_VI,
        subjectEn: 'Default',
        bodyEn: BODY_EN,
        revision: 5,
        message: 'Đã khôi phục',
      },
    });

    await openEditor(BODY_VI, BODY_EN);

    const input = subjectInput();
    input.focus();
    input.setSelectionRange(20, 20);   // deep into a heading that is about to be replaced
    fireEvent.select(input);

    fireEvent.click(screen.getByTestId('restore-default'));
    fireEvent.click(await screen.findByRole('button', { name: 'Khôi phục mặc định' }));
    await waitFor(() => expect(subjectInput().value).toBe('Mặc định'));

    insertFullName();

    await waitFor(() => expect(inserted).toEqual([{ name: 'fullName', label: 'Họ tên' }]));
    expect(subjectInput().value).toBe('Mặc định');
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
