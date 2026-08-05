/**
 * "● Nội dung mẫu có thay đổi chưa lưu", on a template nobody has edited.
 *
 * The other TemplateManagement suite mocks the editor as a plain textarea, which is right for what it
 * tests and is exactly why it could not see this defect: the warning came from ReactQuill, not from the
 * screen. Quill is a controlled component that converts whatever html it is handed into its own
 * canonical form and reports the result through `onChange` as it mounts — source `api`, nobody typing.
 * The stored bodies are not in that form ({{actionBlock}} sits between two paragraphs as bare text, the
 * footers carry inline `style` attributes Quill's format list does not include), so opening any template
 * produced a change event, the form moved away from its baseline, and the screen announced unsaved work
 * before the operator had touched anything. Switching the language tab did it again.
 *
 * The mock below is therefore the opposite of the other one: it reproduces the normalisation rather than
 * avoiding it, in the two ways that actually differ (block-level bare text gets wrapped; unsupported
 * inline styles are dropped), and reports it with the source Quill uses.
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
 * How a rich-text editor RESPELLS markup it is handed, without changing what it says.
 *
 * <b>What changed here, and why.</b> This used to also strip inline `style` attributes, because the old
 * four-button editor genuinely dropped them. The screen no longer compares body strings — it compares
 * meaning — and losing every `style` in a body is a change of meaning, not of spelling: a footer that
 * arrives unstyled is a different email. Simulating that now would be asserting the screen should IGNORE
 * a real edit.
 *
 * So the double does only what a respelling does: notation differences a reader cannot see. If the
 * screen's comparison ever regresses to string equality, every test below fails.
 *
 * Idempotent, like a real serialiser: applied to its own output it changes nothing. One that kept finding
 * work would loop against a controlled editor rather than settle — a property of the double, not the
 * screen.
 */
const respellLikeAnEditor = (html: string) =>
  html
    .replace(/<br\s*\/>/g, '<br>')
    .replace(/;"/g, '"')                       // a dropped trailing semicolon
    + (/<p><br><\/p>$/.test(html) ? '' : '<p><br></p>');   // the trailing blank line editors keep

/**
 * The SHARED editor, doubled.
 *
 * The screen renders `EmailRichTextEditor`, so that is what is stood in for — mocking `react-quill-new`
 * underneath it would leave the component's own conversions running against a stub and test neither
 * thing properly. Real-editor behaviour is covered where it belongs: `EmailRichTextEditor.test.tsx`,
 * `emailEditorNodes.test.ts` and `emailHtmlCanonicalizer.test.ts` all drive a real Quill.
 */
vi.mock('../../../features/emails/components/EmailRichTextEditor', async () => {
  const React = await vi.importActual<typeof import('react')>('react');

  return {
    EmailRichTextEditor: React.forwardRef((
      { value, onChange }: { value: string; onChange: (v: string) => void },
      ref: React.Ref<{ insertVariable: (v: { name: string; label: string }) => void; isReady: () => boolean }>,
    ) => {
      // On mount, and whenever it is handed html it has not already respelled: what a controlled editor
      // does when it loads a document.
      React.useEffect(() => {
        const respelled = respellLikeAnEditor(value);
        if (respelled !== value) onChange(respelled);
      }, [value]);

      React.useImperativeHandle(ref, () => ({
        insertVariable: (v: { name: string; label: string }) => onChange(`${value}{{${v.name}}}`),
        isReady: () => true,
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

/** Stored exactly as the shipped default has it — bare block, inline styles, `<br/>`. */
const STORED_BODY_VI =
  '<p>Xin chào <strong>{{fullName}}</strong>,</p>'
  + '<p>Tài khoản của bạn đang chờ xác nhận.</p>'
  + '{{actionBlock}}'
  + '<p style="color:#6b7280;font-size:12px">Liên kết có hiệu lực {{expiresInHours}} giờ.<br/>PEMS</p>';

const STORED_BODY_EN =
  '<p>Hello <strong>{{fullName}}</strong>,</p>'
  + '{{actionBlock}}'
  + '<p style="color:#6b7280;font-size:12px">The link is valid for {{expiresInHours}} hours.<br/>PEMS</p>';

const DETAIL = {
  emailTemplateId: 7,
  templateCode: 'ACCOUNT_EMAIL_CONFIRMATION',
  name: 'Xác nhận email tài khoản',
  description: 'Gửi khi tài khoản được tạo',
  subjectVi: '[PEMS] Xác nhận địa chỉ email',
  bodyVi: STORED_BODY_VI,
  subjectEn: '[PEMS] Confirm your email',
  bodyEn: STORED_BODY_EN,
  status: 'ACTIVE',
  createdAt: '2026-07-01T08:00:00+07:00',
  updatedAt: null,
  revision: 4,
  hasShippedDefault: true,
};

const SECOND_DETAIL = {
  ...DETAIL,
  emailTemplateId: 9,
  templateCode: 'VISIT_PARTICIPANT_INVITATION',
  name: 'Thư mời tham dự',
  description: 'Gửi cho người được mời tham dự',
  bodyVi: '<p>Kính mời {{fullName}}.</p>{{actionBlock}}',
  bodyEn: '<p>Dear {{fullName}}.</p>{{actionBlock}}',
  revision: 2,
};

const CONTRACT = {
  templateCode: 'ACCOUNT_EMAIL_CONFIRMATION',
  module: 'ACCOUNT',
  isSystemTemplate: true,
  variables: [
    { name: 'fullName', label: 'Họ tên người nhận', sample: 'Nguyễn Văn An', required: false, sensitive: false, forbiddenInSubject: false },
    { name: 'expiresInHours', label: 'Hiệu lực (giờ)', sample: '24', required: false, sensitive: false, forbiddenInSubject: false },
  ],
  allowedVariables: ['fullName', 'expiresInHours'],
  requiredVariables: [],
  optionalVariables: ['fullName', 'expiresInHours'],
  requiredSystemBlocks: [],
  optionalSystemBlocks: ['actionBlock'],
  systemBlockPreviews: { actionBlock: '<div><span>Xác nhận</span></div>' },
  sensitiveVariables: [],
  forbiddenInSubject: ['actionBlock'],
  actionSupported: true,
  actionRequired: false,
  systemActionDescription: 'Nút xác nhận địa chỉ email.',
  carriesSecret: true,
  allowCc: false,
  allowBcc: false,
  securityClassification: 'SENSITIVE',
  editableFields: ['name', 'description', 'subjectVi', 'subjectEn', 'bodyVi', 'bodyEn'],
  senderVariableCapability: 'NOT_AVAILABLE',
  senderVariables: [],
  senderVariablesAllowed: false,
  runtimeEditable: false,
  senderReasonCode: 'ONE_TIME_CREDENTIAL',
  senderReasonVi: 'Mẫu này mang mã hoặc liên kết dùng một lần nên không hiển thị thông tin người gửi.',
};

const pushToast = vi.fn();

beforeEach(() => {
  vi.clearAllMocks();
  getEmailTemplateList.mockResolvedValue({
    data: {
      items: [
        { emailTemplateId: 7, templateCode: 'ACCOUNT_EMAIL_CONFIRMATION', name: 'Xác nhận email tài khoản', description: '' },
        { emailTemplateId: 9, templateCode: 'VISIT_PARTICIPANT_INVITATION', name: 'Thư mời tham dự', description: '' },
      ],
      totalItems: 2,
    },
  });
  getEmailTemplateDetail.mockResolvedValue({ data: DETAIL });
  getEmailTemplateContract.mockResolvedValue({ data: CONTRACT });
});

const openEditor = async (code = 'ACCOUNT_EMAIL_CONFIRMATION') => {
  render(<TemplateManagement pushToast={pushToast} />);
  fireEvent.click(await screen.findByLabelText(`Chỉnh sửa ${code}`));
  await screen.findByText('Chỉnh sửa nội dung mẫu email');
  // The editor has mounted and normalised by the time the variable sidebar is on screen.
  await screen.findByText('Họ tên người nhận');
};

const quill = () => screen.getByTestId('quill') as HTMLTextAreaElement;
const saveButton = () => screen.getByRole('button', { name: /Lưu thay đổi/ });

describe('opening a template is not an edit', () => {
  it('shows no unsaved-changes warning when nothing has been touched', async () => {
    await openEditor();

    // The editor really did rewrite the stored html — this is the event that used to raise the warning.
    await waitFor(() => expect(quill().value).not.toBe(STORED_BODY_VI));

    expect(screen.queryByTestId('editor-dirty')).not.toBeInTheDocument();
    expect(saveButton()).toBeDisabled();
  });

  it('stays clean when the language tab is switched', async () => {
    await openEditor();

    fireEvent.click(screen.getByRole('button', { name: 'English' }));
    await waitFor(() => expect(quill().value).toContain('Hello'));

    expect(screen.queryByTestId('editor-dirty')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Tiếng Việt' }));
    await waitFor(() => expect(quill().value).toContain('Xin chào'));

    expect(screen.queryByTestId('editor-dirty')).not.toBeInTheDocument();
  });

  /**
   * The English editor's change event used to be delivered to the handler bound to the Vietnamese body,
   * because ReactQuill loads a new value from inside shouldComponentUpdate, before its props are
   * committed. Switching the tab therefore overwrote the Vietnamese content with the English one.
   */
  it('does not let the English body overwrite the Vietnamese one', async () => {
    await openEditor();

    fireEvent.click(screen.getByRole('button', { name: 'English' }));
    await waitFor(() => expect(quill().value).toContain('Hello'));
    fireEvent.click(screen.getByRole('button', { name: 'Tiếng Việt' }));

    await waitFor(() => expect(quill().value).toContain('Xin chào'));
    expect(quill().value).not.toContain('Hello');
  });

  it('closes without a confirmation, because nothing is pending', async () => {
    await openEditor();

    fireEvent.click(screen.getByRole('button', { name: 'Hủy' }));

    await screen.findByLabelText('Chỉnh sửa ACCOUNT_EMAIL_CONFIRMATION');
    expect(screen.queryByText(/thay đổi chưa lưu/)).not.toBeInTheDocument();
  });

  it('does not carry a baseline from the previously opened template', async () => {
    await openEditor();
    fireEvent.click(screen.getByRole('button', { name: 'Hủy' }));

    getEmailTemplateDetail.mockResolvedValue({ data: SECOND_DETAIL });
    getEmailTemplateContract.mockResolvedValue({
      data: { ...CONTRACT, templateCode: 'VISIT_PARTICIPANT_INVITATION' },
    });

    fireEvent.click(await screen.findByLabelText('Chỉnh sửa VISIT_PARTICIPANT_INVITATION'));
    await screen.findByText('Chỉnh sửa nội dung mẫu email');
    await waitFor(() => expect(quill().value).toContain('Kính mời'));

    expect(screen.queryByTestId('editor-dirty')).not.toBeInTheDocument();
    expect(saveButton()).toBeDisabled();
  });
});

describe('a real edit is reported, and only while it stands', () => {
  it('reports typing in the body', async () => {
    await openEditor();
    const normalized = quill().value;

    fireEvent.change(quill(), { target: { value: `${normalized}<p>Thêm một dòng.</p>` } });

    expect(await screen.findByTestId('editor-dirty')).toBeInTheDocument();
    expect(saveButton()).toBeEnabled();
  });

  /** Undo is not a save, and the screen must stop claiming unsaved work when the content comes back. */
  it('drops the warning when the edit is undone', async () => {
    await openEditor();
    const normalized = quill().value;

    fireEvent.change(quill(), { target: { value: `${normalized}<p>Thêm một dòng.</p>` } });
    await screen.findByTestId('editor-dirty');

    fireEvent.change(quill(), { target: { value: normalized } });

    await waitFor(() => expect(screen.queryByTestId('editor-dirty')).not.toBeInTheDocument());
    expect(saveButton()).toBeDisabled();
  });

  it('treats a difference of trailing whitespace as no change', async () => {
    await openEditor();

    fireEvent.change(screen.getByLabelText('Tên mẫu *'), {
      target: { value: `${DETAIL.name}   ` },
    });

    expect(screen.queryByTestId('editor-dirty')).not.toBeInTheDocument();
  });

  it('reports an inserted variable, which the editor also re-serialises', async () => {
    await openEditor();

    fireEvent.click(screen.getByRole('button', { name: /Hiệu lực \(giờ\)/ }));

    expect(await screen.findByTestId('editor-dirty')).toBeInTheDocument();
    expect(quill().value).toContain('{{expiresInHours}}');
  });

  it('clears the warning after a successful save', async () => {
    // The full stored snapshot, as the API now answers with: the editor re-baselines from what the
    // database holds rather than from what it sent, so a response carrying only the revision would leave
    // it comparing the new form against blank fields and reporting everything as unsaved.
    updateEmailTemplate.mockResolvedValue({
      data: {
        emailTemplateId: 7,
        templateCode: 'ACCOUNT_EMAIL_CONFIRMATION',
        revision: 5,
        updatedAt: '2026-08-04T09:00:00+07:00',
        name: 'Tên mới',
        description: DETAIL.description,
        subjectVi: DETAIL.subjectVi,
        bodyVi: DETAIL.bodyVi,
        subjectEn: DETAIL.subjectEn,
        bodyEn: DETAIL.bodyEn,
      },
    });

    await openEditor();
    fireEvent.change(screen.getByLabelText('Tên mẫu *'), { target: { value: 'Tên mới' } });
    await screen.findByTestId('editor-dirty');

    fireEvent.click(saveButton());

    await waitFor(() => expect(updateEmailTemplate).toHaveBeenCalledTimes(1));
    // The editor closes on a successful save; reopening must not resurrect the warning.
    fireEvent.click(await screen.findByLabelText('Chỉnh sửa ACCOUNT_EMAIL_CONFIRMATION'));
    await screen.findByText('Họ tên người nhận');

    expect(screen.queryByTestId('editor-dirty')).not.toBeInTheDocument();
  });
});

/**
 * A credential-bearing template offers no sender variables at all (§3.1).
 *
 * The picker is where that shows: the group is absent rather than present-and-refused, so an operator
 * cannot insert a placeholder the save would then reject. This replaces the read-only "card 4" the
 * contact feature showed for the same templates — there is no card to render, because there was never
 * anything on it to configure.
 */
describe('a credential-bearing template offers no sender variables', () => {
  it('shows no sender group in the variable picker', async () => {
    await openEditor();

    expect(screen.queryByTestId('sender-variable-group')).not.toBeInTheDocument();
    expect(screen.queryByText('Thông tin người gửi')).not.toBeInTheDocument();
  });
});

/** §2.4 — the two sentences that explained what the controls beside them already show. */
describe('the editor header carries no redundant explanation', () => {
  it('drops both helper paragraphs but keeps the fields', async () => {
    await openEditor();

    expect(screen.queryByText(/do hệ thống quản lý và không thể thay đổi/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Ghi chú nội bộ cho người quản trị/)).not.toBeInTheDocument();

    // What replaces them: the locked code, and a full-width textarea for the description.
    expect(screen.getByText('Mã mẫu')).toBeInTheDocument();
    const description = screen.getByLabelText('Mô tả quản trị') as HTMLTextAreaElement;
    expect(description.tagName).toBe('TEXTAREA');
    expect(description.className).toContain('w-full');
    expect(Number(description.rows)).toBeGreaterThanOrEqual(2);
  });
});
