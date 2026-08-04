/**
 * Two defects in the template editor, pinned as behaviour.
 *
 * <b>1. A refusal on the tab you are not looking at.</b> With the contact level at "Bắt buộc", a
 * Vietnamese body carrying `{{contactInformationBlock}}` and an English one that does not, "Lưu thay
 * đổi" went dead while the Vietnamese tab showed a clean form. The explanation existed — under the
 * English body, on a tab the operator had no reason to open. The save is refused across BOTH languages,
 * so the explanation has to be visible from both.
 *
 * <b>2. Variables appended to the end of the body.</b> `insertVariable` asked the DOM what was focused,
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

/**
 * A Quill stand-in with the parts `insertVariable` actually uses.
 *
 * The other suites mock the editor as a bare textarea, which is right for tests about validation — but
 * useless here, because the whole point is WHERE the token lands inside the document. This one keeps a
 * text model, honours a selection, and exposes the same five methods the component calls, so an insert
 * at index 6 can be asserted as an insert at index 6 rather than as "onChange fired".
 */
let liveEditor: {
  select: (index: number, length?: number) => void;
  blur: () => void;
  text: () => string;
} | null = null;

vi.mock('react-quill-new', async () => {
  const { forwardRef, useImperativeHandle, useRef } = await import('react');

  type Props = {
    value: string;
    onChange: (v: string, delta: unknown, source: string) => void;
    onChangeSelection?: (range: { index: number; length: number } | null) => void;
  };

  const QuillMock = forwardRef<unknown, Props>(({ value, onChange, onChangeSelection }, ref) => {
    // Mutated in place by insertText/deleteText so `root.innerHTML` reflects the edit within the same
    // call — exactly as Quill does, and what the component reads to update its form state.
    const model = useRef({ text: value ?? '' });
    model.current.text = value ?? '';
    const selection = useRef<{ index: number; length: number } | null>(null);

    useImperativeHandle(ref, () => ({
      getEditor: () => ({
        getSelection: () => selection.current,
        getLength: () => model.current.text.length + 1,
        insertText(index: number, text: string) {
          const t = model.current.text;
          model.current.text = t.slice(0, index) + text + t.slice(index);
        },
        deleteText(index: number, length: number) {
          const t = model.current.text;
          model.current.text = t.slice(0, index) + t.slice(index + length);
        },
        setSelection(index: number, length = 0) {
          selection.current = { index, length };
        },
        focus: () => {},
        get root() {
          return { get innerHTML() { return model.current.text; } };
        },
      }),
    }));

    liveEditor = {
      select: (index: number, length = 0) => {
        selection.current = { index, length };
        onChangeSelection?.({ index, length });
      },
      // What clicking a chip does: Quill reports a null range as it loses the focus.
      blur: () => {
        selection.current = null;
        onChangeSelection?.(null);
      },
      text: () => model.current.text,
    };

    return (
      <textarea
        data-testid="quill"
        value={value}
        onChange={e => onChange(e.target.value, null, 'user')}
      />
    );
  });

  return { default: QuillMock };
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
  optionalSystemBlocks: ['contactInformationBlock'],
  systemBlockPreviews: {},
  sensitiveVariables: [],
  forbiddenInSubject: [],
  actionSupported: false,
  actionRequired: false,
  systemActionDescription: null,
  contactSupported: true,
  contactRequirement: 'REQUIRED',
  carriesSecret: false,
  allowCc: true,
  allowBcc: true,
  securityClassification: 'STANDARD',
  editableFields: ['name', 'description', 'subjectVi', 'subjectEn', 'bodyVi', 'bodyEn'],
};

const SETTINGS = {
  templateCode: 'ACCOUNT_ROLE_CHANGED', requirement: 'REQUIRED', contactSource: 'CAMPUS_DEFAULT',
  showEmail: true, showPhone: true, showDepartment: false, showCampus: true, showSender: false,
  headingVi: 'Thông tin liên hệ', headingEn: 'Contact information', replyToSource: 'NONE',
  blockPlaceholder: '{{contactInformationBlock}}',
  bodyCarriesBlockVi: true, bodyCarriesBlockEn: true,
  availableRequirements: ['NONE', 'OPTIONAL', 'REQUIRED'],
  availableSources: ['HOST', 'CAMPUS_DEFAULT'], availableReplyToSources: ['NONE', 'CONTACT', 'SENDER'],
  capability: 'SUPPORTED',
  editable: true,
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

const WITH_BLOCK_VI = '<p>Chào bạn.</p>{{contactInformationBlock}}';
const WITH_BLOCK_EN = '<p>Hello.</p>{{contactInformationBlock}}';
const WITHOUT_BLOCK_VI = '<p>Chào bạn.</p>';
const WITHOUT_BLOCK_EN = '<p>Hello.</p>';

const pushToast = vi.fn();

async function openEditor(bodyVi: string, bodyEn: string) {
  getEmailTemplateDetail.mockResolvedValue({ data: { ...BASE_DETAIL, bodyVi, bodyEn } });
  render(<TemplateManagement pushToast={pushToast} />);
  fireEvent.click(await screen.findByLabelText('Chỉnh sửa ACCOUNT_ROLE_CHANGED'));
  await screen.findByTestId('save-template');
  await screen.findByTestId('contact-settings-panel');
}

beforeEach(() => {
  vi.clearAllMocks();
  liveEditor = null;
  getEmailTemplateList.mockResolvedValue({
    data: { items: [{ emailTemplateId: 7, templateCode: 'ACCOUNT_ROLE_CHANGED', name: 'Thay đổi vai trò', description: '' }], totalItems: 1 },
  });
  getEmailTemplateContract.mockResolvedValue({ data: CONTRACT });
  getEmailContactSettings.mockResolvedValue({ data: SETTINGS });
  previewEmailContactBlock.mockResolvedValue({ data: { html: '<div>contact</div>', rendersBlock: true } });
});

describe('TemplateManagement — a refusal in the other language is visible from this one', () => {
  it('shows the English failure while the Vietnamese tab is open', async () => {
    await openEditor(WITH_BLOCK_VI, WITHOUT_BLOCK_EN);

    // The tab that is open is the clean one; without the summary this screen showed nothing at all.
    const summary = await screen.findByTestId('validation-summary');
    expect(summary).toBeInTheDocument();
    expect(screen.getByTestId('validation-summary-EN')).toBeInTheDocument();
    expect(screen.queryByTestId('validation-summary-VI')).not.toBeInTheDocument();

    // And the save it explains is genuinely off.
    expect(screen.getByTestId('save-template')).toBeDisabled();
  });

  it('marks the failing tab and not the clean one', async () => {
    await openEditor(WITH_BLOCK_VI, WITHOUT_BLOCK_EN);
    await screen.findByTestId('validation-summary');

    expect(screen.getByTestId('language-tab-error-EN')).toBeInTheDocument();
    expect(screen.queryByTestId('language-tab-error-VI')).not.toBeInTheDocument();
    expect(screen.getByTestId('language-tab-EN')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByTestId('language-tab-VI')).toHaveAttribute('aria-invalid', 'false');
  });

  it('reports the Vietnamese failure when English is the sound one', async () => {
    await openEditor(WITHOUT_BLOCK_VI, WITH_BLOCK_EN);
    await screen.findByTestId('validation-summary');

    expect(screen.getByTestId('validation-summary-VI')).toBeInTheDocument();
    expect(screen.queryByTestId('validation-summary-EN')).not.toBeInTheDocument();
    expect(screen.getByTestId('language-tab-error-VI')).toBeInTheDocument();
  });

  it('reports both when neither language has the block', async () => {
    await openEditor(WITHOUT_BLOCK_VI, WITHOUT_BLOCK_EN);
    await screen.findByTestId('validation-summary');

    expect(screen.getByTestId('validation-summary-VI')).toBeInTheDocument();
    expect(screen.getByTestId('validation-summary-EN')).toBeInTheDocument();
    expect(screen.getByTestId('language-tab-error-VI')).toBeInTheDocument();
    expect(screen.getByTestId('language-tab-error-EN')).toBeInTheDocument();
  });

  it('switches to the failing language when the action is pressed', async () => {
    await openEditor(WITH_BLOCK_VI, WITHOUT_BLOCK_EN);
    await screen.findByTestId('validation-summary');

    expect(screen.getByTestId('language-tab-VI')).toHaveAttribute('aria-pressed', 'true');

    fireEvent.click(screen.getByTestId('goto-issue-EN'));

    await waitFor(() =>
      expect(screen.getByTestId('language-tab-EN')).toHaveAttribute('aria-pressed', 'true'));
    // And the field the message is about is now on screen, under the English body.
    expect(screen.getByTestId('issues-bodyEn')).toBeInTheDocument();
  });

  it('lists both languages on the contact card, marking the one that is missing the block', async () => {
    await openEditor(WITH_BLOCK_VI, WITHOUT_BLOCK_EN);

    const vi = await screen.findByTestId('contact-block-status-VI');
    const en = screen.getByTestId('contact-block-status-EN');

    expect(vi).toHaveAttribute('data-has-block', 'true');
    expect(en).toHaveAttribute('data-has-block', 'false');
    expect(vi.textContent).toContain('đã có khối thông tin liên hệ');
    expect(en.textContent).toContain('chưa có khối thông tin liên hệ');
  });

  it('says nothing when both languages satisfy the level', async () => {
    await openEditor(WITH_BLOCK_VI, WITH_BLOCK_EN);
    await screen.findByTestId('contact-settings-panel');

    expect(screen.queryByTestId('validation-summary')).not.toBeInTheDocument();
    expect(screen.queryByTestId('language-tab-error-VI')).not.toBeInTheDocument();
    expect(screen.queryByTestId('language-tab-error-EN')).not.toBeInTheDocument();
  });
});

describe('TemplateManagement — variables land at the caret', () => {
  const subjectInput = () => screen.getByLabelText('Tiêu đề (Subject)') as HTMLInputElement;
  const insertFullName = () => fireEvent.click(screen.getByTitle(/^\{\{fullName\}\}/));

  it('inserts into the subject at the caret, not at the end', async () => {
    await openEditor(WITH_BLOCK_VI, WITH_BLOCK_EN);

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
    await openEditor(WITH_BLOCK_VI, WITH_BLOCK_EN);

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
    await openEditor(WITH_BLOCK_VI, WITH_BLOCK_EN);

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

  it('inserts into the body at the remembered caret after the editor has lost focus', async () => {
    await openEditor(WITH_BLOCK_VI, WITH_BLOCK_EN);
    expect(liveEditor).not.toBeNull();

    // Caret inside the paragraph, then the chip steals the focus and Quill reports a null range.
    liveEditor!.select(3, 0);
    liveEditor!.blur();

    insertFullName();

    await waitFor(() =>
      expect(screen.getByTestId('quill')).toHaveValue('<p>{{fullName}}Chào bạn.</p>{{contactInformationBlock}}'));
  });

  it('replaces a selected run in the body', async () => {
    await openEditor(WITH_BLOCK_VI, WITH_BLOCK_EN);

    // Select "Chào" (indices 3..7).
    liveEditor!.select(3, 4);
    liveEditor!.blur();

    insertFullName();

    await waitFor(() =>
      expect(screen.getByTestId('quill')).toHaveValue('<p>{{fullName}} bạn.</p>{{contactInformationBlock}}'));
  });

  it('does not append to the end when nothing has been focused', async () => {
    await openEditor(WITH_BLOCK_VI, WITH_BLOCK_EN);

    insertFullName();

    // The head of the body, which is visible and movable — never the tail, which is where the token
    // used to disappear to.
    await waitFor(() => {
      const body = (screen.getByTestId('quill') as HTMLTextAreaElement).value;
      expect(body.startsWith('{{fullName}}')).toBe(true);
      expect(body.endsWith('{{fullName}}')).toBe(false);
    });
  });
});
