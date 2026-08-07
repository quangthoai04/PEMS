/**
 * The preview pane on the template screen shows what the EDITOR holds (V4 §21, §43).
 *
 * <b>What these are for.</b> The pipeline itself is unit-tested in `templateDraftPreview.test.ts`; what
 * cannot be tested there is that the SCREEN feeds it the canonical draft and renders the result. The
 * failure mode is specific and was live in this codebase before the pipeline was extracted: the preview
 * was assembled inline from `formData`, next to a pane that read `dangerouslySetInnerHTML` from a second
 * expression, and a change to either could quietly leave the two disagreeing — an operator formatting a
 * heading and seeing plain text, or seeing a variable name where a recipient will read a person's.
 *
 * The editor is doubled here, as it is in every other test about this screen: what a real Quill does with
 * the markup is `EmailRichTextEditor.test.tsx`'s subject. These drive the body through the double and
 * assert on the pane.
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

vi.mock('../../../features/emails/components/EmailRichTextEditor', async () => {
  const React = await vi.importActual<typeof import('react')>('react');
  return {
    EmailRichTextEditor: React.forwardRef((
      { value, onChange }: { value: string; onChange: (v: string) => void },
      ref: React.Ref<unknown>,
    ) => {
      React.useImperativeHandle(ref, () => ({
        insertVariable: (v: { name: string }) => onChange(`${value}{{${v.name}}}`),
        isReady: () => true,
      }), [value, onChange]);
      return <textarea data-testid="quill" value={value} onChange={e => onChange(e.target.value)} />;
    }),
  };
});
vi.mock('react-quill-new/dist/quill.snow.css', () => ({}));

import { TemplateManagement } from '../../../pages/dashboard/emails/TemplateManagement';

const CONTRACT = {
  templateCode: 'VISIT_PARTICIPANT_INVITATION',
  module: 'VISIT',
  isSystemTemplate: true,
  variables: [
    { name: 'recipientName', label: 'Họ tên người nhận', sample: 'Nguyễn Văn An', required: false, sensitive: false, forbiddenInSubject: false },
    { name: 'delegationName', label: 'Tên đoàn', sample: 'Đoàn THPT Chu Văn An', required: false, sensitive: false, forbiddenInSubject: false },
  ],
  allowedVariables: ['recipientName', 'delegationName'],
  requiredVariables: [],
  optionalVariables: ['recipientName', 'delegationName'],
  requiredSystemBlocks: [],
  optionalSystemBlocks: ['actionBlock'],
  systemBlockPreviews: {
    actionBlock:
      '<div style="text-align:center"><span style="background:#9aa6b2">Chấp nhận</span>'
      + '<span style="background:#9aa6b2">Từ chối</span></div>',
  },
  sensitiveVariables: [],
  forbiddenInSubject: [],
  actionSupported: true,
  actionRequired: false,
  systemActionDescription: null,
  senderVariableCapability: 'AVAILABLE_READ_ONLY_RUNTIME',
  senderVariables: [],
  senderVariablesAllowed: false,
  runtimeEditable: false,
  carriesSecret: false,
  allowCc: true,
  allowBcc: true,
  securityClassification: 'STANDARD',
  editableFields: ['name', 'description', 'subjectVi', 'subjectEn', 'bodyVi', 'bodyEn'],
};

const DETAIL = {
  emailTemplateId: 11,
  templateCode: 'VISIT_PARTICIPANT_INVITATION',
  name: 'Thư mời tham dự',
  description: '',
  subjectVi: 'Thư mời {{recipientName}}',
  subjectEn: 'Invitation for {{recipientName}}',
  bodyVi: '<p>Kính gửi {{recipientName}}</p>',
  bodyEn: '<p>Dear {{recipientName}}</p>',
  status: 'ACTIVE',
  createdAt: '2026-07-01T08:00:00+07:00',
  updatedAt: null,
  revision: 3,
  hasShippedDefault: true,
};

const pushToast = vi.fn();

async function openEditor(bodyVi = DETAIL.bodyVi) {
  getEmailTemplateDetail.mockResolvedValue({ data: { ...DETAIL, bodyVi } });
  render(<TemplateManagement pushToast={pushToast} />);
  fireEvent.click(await screen.findByLabelText('Chỉnh sửa VISIT_PARTICIPANT_INVITATION'));
  await screen.findByTestId('save-template');
}

/** Types a body through the doubled editor, exactly as an edit would arrive from the real one. */
function writeBody(html: string) {
  fireEvent.change(screen.getByTestId('quill'), { target: { value: html } });
}

const previewHtml = () => (screen.getByTestId('preview-body') as HTMLElement).innerHTML;

beforeEach(() => {
  vi.clearAllMocks();
  getEmailTemplateList.mockResolvedValue({
    data: { items: [{ emailTemplateId: 11, templateCode: 'VISIT_PARTICIPANT_INVITATION', name: 'Thư mời tham dự', description: '' }], totalItems: 1 },
  });
  getEmailTemplateContract.mockResolvedValue({ data: CONTRACT });
});

describe('TemplateManagement — the preview shows the draft', () => {
  it('substitutes the contract samples into the body and the subject', async () => {
    await openEditor();

    await waitFor(() => expect(previewHtml()).toContain('Nguyễn Văn An'));
    expect(previewHtml()).not.toContain('{{recipientName}}');
    expect(screen.getByText('Thư mời Nguyễn Văn An')).toBeInTheDocument();
  });

  it.each([
    ['centred text', '<p style="text-align: center;">Giữa</p>', 'text-align: center'],
    ['a font size', '<p><span style="font-size: 18px;">Cỡ</span></p>', 'font-size: 18px'],
    ['a colour', '<p><span style="color: rgb(255, 0, 0);">Đỏ</span></p>', 'color: rgb(255, 0, 0)'],
    ['a background', '<p><span style="background-color: rgb(255, 255, 0);">Nền</span></p>', 'background-color'],
    ['an indent', '<p style="margin-left: 16px;">Thụt</p>', 'margin-left: 16px'],
    ['bold', '<p><strong>Đậm</strong></p>', '<strong>'],
    ['a bullet list', '<ul><li>Một</li></ul>', '<ul>'],
    ['an ordered list', '<ol><li>Một</li></ol>', '<ol>'],
    ['a divider', '<p>a</p><hr style="border:none;border-top:1px solid #e2e8f0;margin:20px 0"><p>b</p>', '<hr'],
    ['a link', '<p><a href="https://pems.fpt.edu.vn/x">liên kết</a></p>', 'https://pems.fpt.edu.vn/x'],
  ])('keeps %s in the pane', async (_label, body, expected) => {
    await openEditor();
    writeBody(body);

    await waitFor(() => expect(previewHtml()).toContain(expected));
  });

  it('keeps a table with the borders that make it visible in mail', async () => {
    await openEditor();
    writeBody(
      '<table role="presentation" style="border-collapse:collapse"><tbody><tr>'
      + '<td style="border:1px solid #dbe4ee;padding:8px 10px">{{delegationName}}</td>'
      + '</tr></tbody></table>',
    );

    await waitFor(() => expect(previewHtml()).toContain('<table'));
    expect(previewHtml()).toContain('border:1px solid #dbe4ee');
    expect(previewHtml()).toContain('padding:8px 10px');
    // …and the variable inside the cell is a sample here, exactly as it is anywhere else.
    expect(previewHtml()).toContain('Đoàn THPT Chu Văn An');
  });

  /**
   * A system block is shown as the inert sample the backend supplied — never as the literal placeholder,
   * which reads as an unresolved variable, and never as something clickable, which would suggest this
   * pane is the message a recipient receives.
   */
  it('renders a system block as its inert sample, at the position it was written', async () => {
    await openEditor();
    writeBody('<p>Trước</p>{{actionBlock}}<p>Sau</p>');

    await waitFor(() => expect(previewHtml()).toContain('Chấp nhận'));
    expect(previewHtml()).not.toContain('{{actionBlock}}');
    expect(previewHtml()).not.toContain('href');
    expect(previewHtml().indexOf('Chấp nhận')).toBeGreaterThan(previewHtml().indexOf('Trước'));
    expect(previewHtml().indexOf('Chấp nhận')).toBeLessThan(previewHtml().indexOf('Sau'));
  });

  it('follows the language tab', async () => {
    await openEditor();

    fireEvent.click(screen.getByTestId('language-tab-EN'));

    await waitFor(() => expect(previewHtml()).toContain('Dear'));
    expect(previewHtml()).not.toContain('Kính gửi');
  });

  it('still refuses what an email may not carry', async () => {
    await openEditor();
    writeBody('<p onclick="steal()">a</p><script>alert(1)</script>');

    await waitFor(() => expect(previewHtml()).toContain('a</p>'));
    expect(previewHtml()).not.toContain('onclick');
    expect(previewHtml()).not.toContain('alert(1)');
  });
});
