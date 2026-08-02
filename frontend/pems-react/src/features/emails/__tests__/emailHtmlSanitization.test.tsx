/**
 * G6.4 — every email HTML sink renders through the shared sanitizer.
 *
 * Two layers, because either alone would be misleading:
 *   1. the sanitizer itself, on the five required vectors;
 *   2. each email caller, proving it actually passes its HTML through that sanitizer rather than
 *      merely importing it. A renderer test alone would still pass if a component forgot the call.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { sanitizeHtml } from '../../../shared/security/sanitizeHtml';

// ── 1. The sanitizer, on the five required cases ──────────────────────────────

describe('sanitizeHtml — the five required vectors', () => {
  it('removes <script> elements', () => {
    const out = sanitizeHtml('<p>xin chào</p><script>alert(1)</script>');
    expect(out).not.toMatch(/<script/i);
    expect(out).not.toContain('alert(1)');
    expect(out).toContain('xin chào');
  });

  it('removes inline event handlers such as onerror', () => {
    const out = sanitizeHtml('<img src="x" onerror="alert(1)">');
    expect(out).not.toMatch(/onerror/i);
    expect(out).not.toContain('alert(1)');
  });

  it('neutralises javascript: URLs', () => {
    const out = sanitizeHtml('<a href="javascript:alert(1)">bấm</a>');
    expect(out).not.toMatch(/javascript:/i);
    expect(out).toContain('bấm');          // the text survives; only the scheme is dropped
  });

  it('keeps legitimate HTML intact', () => {
    const out = sanitizeHtml(
      '<p><strong>Kính gửi</strong> anh/chị,</p><ul><li>Mục một</li></ul>' +
      '<a href="https://pems.fpt.edu.vn/x">Xem chi tiết</a>');
    expect(out).toContain('<strong>');
    expect(out).toContain('<li>');
    expect(out).toContain('https://pems.fpt.edu.vn/x');
  });

  it('leaves plain text as text', () => {
    expect(sanitizeHtml('Chào anh Cảnh, 10 < 20 và 30 > 20')).toContain('Chào anh Cảnh');
  });

  it('also strips iframe/object/embed, not just script', () => {
    const out = sanitizeHtml('<iframe src="evil"></iframe><object data="x"></object><embed src="y">');
    expect(out).not.toMatch(/<iframe|<object|<embed/i);
  });
});

// ── 2. Each email caller renders through it ───────────────────────────────────

const XSS = '<p>an toàn</p><script>alert(1)</script><img src="x" onerror="alert(2)">'
  + '<a href="javascript:alert(3)">bấm</a>';

/** Nothing executable may reach the DOM of a rendered preview. */
const expectNeutralised = (container: HTMLElement) => {
  expect(container.querySelector('script')).toBeNull();
  expect(container.innerHTML).not.toMatch(/onerror=/i);
  expect(container.innerHTML).not.toMatch(/javascript:/i);
  expect(container.innerHTML).toContain('an toàn');   // legitimate content still shown
};

describe('EmailComposeModal preview', () => {
  const createDraft = vi.fn();
  const getRecipientLimits = vi.fn();
  const getEmailTemplateList = vi.fn();

  beforeEach(() => {
    vi.resetModules();
    vi.clearAllMocks();
    getRecipientLimits.mockResolvedValue({ data: { maxRecipients: 50 } });
    getEmailTemplateList.mockResolvedValue({ data: { items: [] } });
    createDraft.mockResolvedValue({ emailDraftId: 1, recipients: [], attachments: [] });
  });

  it('sanitises the composed body before showing it', async () => {
    vi.doMock('../api/emailDraftsApi', () => ({
      emailDraftsApi: {
        createDraft, updateDraft: vi.fn(), sendDraft: vi.fn(), getDraft: vi.fn(), discardDraft: vi.fn(),
      },
    }));
    vi.doMock('../api/emailsApi', () => ({
      emailsApi: { getRecipientLimits, getEmailTemplateList },
    }));
    vi.doMock('../../../shared/api/filesApi', () => ({ filesApi: { upload: vi.fn() } }));
    vi.doMock('../../../shared/auth/authStorage', () => ({ authStorage: { getToken: () => 't' } }));
    vi.doMock('react-quill-new', () => ({
      default: ({ value, onChange }: { value: string; onChange: (v: string) => void }) => (
        <textarea aria-label="body" value={value} onChange={e => onChange(e.target.value)} />
      ),
    }));
    vi.doMock('react-quill-new/dist/quill.snow.css', () => ({}));

    const { EmailComposeModal } = await import('../components/EmailComposeModal');

    const { container } = render(
      <EmailComposeModal open onClose={vi.fn()} pushToast={vi.fn()}
        initialRecipients="to@fpt.vn" initialSubject="Chủ đề" initialBodyHtml={XSS} />);

    await waitFor(() => expect(getRecipientLimits).toHaveBeenCalled());
    fireEvent.click(screen.getByRole('button', { name: /Xem trước/ }));

    await screen.findByText('Xem trước email');
    expectNeutralised(container);
  });
});

describe('EmailPreviewModal action block', () => {
  it('sanitises the locked action block before showing it', async () => {
    vi.resetModules();
    vi.doMock('../../../shared/auth/authStorage', () => ({ authStorage: { getToken: () => 't' } }));

    const { EmailPreviewModal } = await import('../../delegations/components/EmailPreviewModal');

    const { container } = render(
      <EmailPreviewModal
        open
        loading={false}
        sending={false}
        error={null}
        subject="Chủ đề"
        body="<p>nội dung</p>"
        // The block only renders for an action template — that is the branch under test.
        isActionTemplate
        lockedActionBlockHtml={XSS}
        canSend={false}
        sendLabel="Gửi"
        onSubjectChange={vi.fn()}
        onBodyChange={vi.fn()}
        onClose={vi.fn()}
        onSend={vi.fn()}
        onRestore={vi.fn()}
      />,
    );

    expectNeutralised(container);
  });
});

describe('EmailManagement template preview', () => {
  it('sanitises a template body fetched from the server before showing it', async () => {
    vi.resetModules();

    const getEmailTemplateDetail = vi.fn().mockResolvedValue({
      data: { emailTemplateId: 7, name: 'Mẫu mời', status: 'ACTIVE', subjectVi: 'Chủ đề', bodyVi: XSS },
    });
    vi.doMock('../api/emailsApi', () => ({
      emailsApi: {
        getEmailList: vi.fn().mockResolvedValue({ data: { items: [], totalCount: 0 } }),
        getEmailTemplateList: vi.fn().mockResolvedValue({
          data: { items: [{ emailTemplateId: 7, name: 'Mẫu mời', status: 'ACTIVE' }], totalCount: 1 },
        }),
        getEmailTemplateDetail,
      },
    }));
    vi.doMock('react-router-dom', () => ({
      useNavigate: () => vi.fn(),
      useLocation: () => ({ pathname: '/dashboard/emails', search: '' }),
      useSearchParams: () => [new URLSearchParams(), vi.fn()],
    }));
    vi.doMock('../../../shared/utils/vietnamTime', () => ({
      formatVietnamDateTime: (v: string) => v, formatVietnamTime: (v: string) => v,
    }));
    localStorage.setItem('currentUser', JSON.stringify({ role: 'STAFF' }));

    const { EmailManagement } = await import('../../../pages/dashboard/emails/EmailManagement');
    const { container } = render(<EmailManagement />);

    fireEvent.click(screen.getByRole('button', { name: /Xem mẫu mail/ }));
    // The list is debounced by 300ms, then the eye button loads the detail that carries the body.
    fireEvent.click(await screen.findByTitle('Xem chi tiết'));
    await waitFor(() => expect(getEmailTemplateDetail).toHaveBeenCalledWith(7));

    await screen.findByText('Nội dung mẫu (Preview)');
    expectNeutralised(container);
  });
});

describe('TemplateManagement editor preview', () => {
  it('sanitises the substituted template body, keeping the variable samples', async () => {
    vi.resetModules();

    // Reached through the EDIT path. The screen no longer has a "Thêm mẫu mới" button — the system
    // template catalog is fixed in code (G11-I) — so the editor is opened on an existing template.
    // What this test measures is unchanged: the preview pane sanitises AFTER substitution.
    vi.doMock('../api/emailsApi', () => ({
      emailsApi: {
        getEmailTemplateList: vi.fn().mockResolvedValue({
          data: {
            items: [{ emailTemplateId: 7, templateCode: 'VISIT_PARTICIPANT_INVITATION', name: 'Thư mời', status: 'ACTIVE' }],
            totalCount: 1,
          },
        }),
        getEmailTemplateDetail: vi.fn().mockResolvedValue({
          data: {
            emailTemplateId: 7,
            templateCode: 'VISIT_PARTICIPANT_INVITATION',
            name: 'Thư mời',
            status: 'ACTIVE',
            subjectVi: 'Thư mời',
            bodyVi: '<p>Kính gửi {{recipientName}}</p>',
            createdAt: '2026-07-01T08:00:00+07:00',
            updatedAt: null,
          },
        }),
        getEmailTemplateContract: vi.fn().mockResolvedValue({
          data: {
            templateCode: 'VISIT_PARTICIPANT_INVITATION',
            module: 'VISIT_PARTICIPANT',
            isSystemTemplate: true,
            variables: [{
              name: 'recipientName', label: 'Tên người nhận', sample: 'Nguyễn Văn A',
              required: false, sensitive: false, forbiddenInSubject: false,
            }],
            allowedVariables: ['recipientName', 'actionBlock'],
            requiredVariables: [],
            optionalVariables: ['recipientName', 'actionBlock'],
            sensitiveVariables: [],
            forbiddenInSubject: ['actionBlock'],
            requiresActionBlock: false,
            carriesSecret: false,
            allowCc: false,
            allowBcc: false,
            securityClassification: 'STANDARD',
            editableFields: ['name', 'description', 'subjectVi', 'subjectEn', 'bodyVi', 'bodyEn'],
          },
        }),
        updateEmailTemplate: vi.fn(),
      },
    }));
    vi.doMock('react-quill-new', async () => {
      const react = await import('react');
      return {
        default: react.forwardRef(
          (
            { value, onChange }: { value: string; onChange: (v: string) => void },
            ref: React.ForwardedRef<HTMLTextAreaElement>,
          ) => (
            <textarea ref={ref} aria-label="content" value={value} onChange={e => onChange(e.target.value)} />
          ),
        ),
      };
    });

    const { TemplateManagement } = await import('../../../pages/dashboard/emails/TemplateManagement');
    const { container } = render(<TemplateManagement pushToast={vi.fn()} />);

    fireEvent.click(await screen.findByLabelText('Chỉnh sửa VISIT_PARTICIPANT_INVITATION'));

    // Wait for the contract, so the sample the preview substitutes is the backend's rather than
    // whatever the screen had before it arrived.
    await screen.findByText('Tên người nhận');

    // A body that is dangerous only *after* substitution would still be caught, because the sanitizer
    // runs last; this asserts both halves — the sample landed, the script did not.
    fireEvent.change(screen.getByLabelText('content'), {
      target: { value: `<p>Kính gửi {{recipientName}}</p>${XSS}` },
    });

    // Scoped to the preview pane: the editor itself legitimately shows the raw source as *text*
    // (React escapes it into the textarea), so asserting over the whole page would be a false alarm.
    // `.pems-email-body`, not `.prose`. The preview pane used to carry Tailwind's typography classes,
    // which did nothing (the plugin is not installed) but told anyone reading the markup that this pane
    // restyles the mail — the one thing a preview of an email must not do. It now carries the isolation
    // class instead, which only undoes inherited layout and lets a wide table scroll.
    const preview = await waitFor(() => {
      const el = container.querySelector('.pems-email-body') as HTMLElement | null;
      expect(el?.innerHTML ?? '').toContain('Nguyễn Văn A');
      return el as HTMLElement;
    });
    expectNeutralised(preview);
  });
});

describe('SentEmailsModal history body', () => {
  it('sanitises the stored body snapshot before showing it', async () => {
    vi.resetModules();
    vi.doMock('../../../shared/auth/authStorage', () => ({ authStorage: { getToken: () => 't' } }));

    const { SentEmailsModal } = await import('../../delegations/components/SentEmailsModal');

    const { container } = render(
      <SentEmailsModal
        open
        title="Lịch sử email"
        targetKey={1}
        onClose={vi.fn()}
        load={async () => ({
          items: [{
            sentEmailId: 1,
            subject: 'Chủ đề',
            bodySnapshot: XSS,
            bodyFormat: 'HTML',
            emailStatus: 'SENT',
            recipients: [],
          }],
        })}
      />,
    );

    fireEvent.click(await screen.findByRole('button', { name: /Xem nội dung email đã gửi/ }));
    await screen.findByText(/bản xem lại email đã gửi/);
    expectNeutralised(container);
  });
});

describe('SentEmailDetail body', () => {
  it('sanitises the stored body snapshot before showing it', async () => {
    vi.resetModules();

    vi.doMock('../api/emailsApi', () => ({
      emailsApi: {
        getEmailDetail: vi.fn().mockResolvedValue({
          data: { subject: 'Chủ đề', status: 'SENT', bodySnapshot: XSS, recipients: [] },
        }),
      },
    }));
    vi.doMock('react-router-dom', () => ({
      useNavigate: () => vi.fn(),
      useParams: () => ({ sourceType: 'sent', id: '1' }),
    }));
    vi.doMock('react-hot-toast', () => ({ toast: { error: vi.fn(), success: vi.fn() } }));
    vi.doMock('../../../shared/utils/vietnamTime', () => ({ formatVietnamDateTime: (v: string) => v }));
    vi.doMock('react-quill-new', async () => {
      const react = await import('react');
      return {
        default: react.forwardRef((_p: unknown, ref: React.ForwardedRef<HTMLTextAreaElement>) => (
          <textarea ref={ref} aria-label="reply" readOnly value="" />
        )),
      };
    });

    const { SentEmailDetail } = await import('../../../pages/dashboard/emails/SentEmailDetail');
    const { container } = render(<SentEmailDetail />);

    // The title appears twice (breadcrumb + heading); either one means the page left its loading state.
    await screen.findAllByText('Chi tiết email đã gửi');
    expectNeutralised(container);
  });
});
