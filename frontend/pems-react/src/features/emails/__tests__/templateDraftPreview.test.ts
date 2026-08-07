/**
 * The template screen's preview pipeline (V4 §21, §22), as a unit.
 *
 * It used to be four expressions inside a 1700-line component, which made the one rule that keeps it
 * correct — samples BEFORE blocks — a comment next to some JSX rather than something a test could hold.
 * These cover that rule, the formats an email may carry surviving to the pane, and the boundary the
 * preview must never cross: sample values are not evidence that a send will work.
 */
import { describe, expect, it } from 'vitest';
import { buildTemplateDraftPreview } from '../utils/templateDraftPreview';
import type { TemplateContract } from '../types/templateContract';

const CONTRACT = {
  templateCode: 'VISIT_INVITATION',
  module: 'VISIT',
  isSystemTemplate: true,
  variables: [
    { name: 'fullName', label: 'Họ tên', sample: 'Nguyễn Văn An', required: true, sensitive: false, forbiddenInSubject: false },
    { name: 'delegationName', label: 'Tên đoàn', sample: 'Đoàn THPT Chu Văn An', required: false, sensitive: false, forbiddenInSubject: false },
    { name: 'senderName', label: 'Họ tên người gửi', sample: 'Trần Thị Bình', required: false, sensitive: false, forbiddenInSubject: false },
  ],
  allowedVariables: ['fullName', 'delegationName', 'senderName'],
  requiredVariables: ['fullName'],
  optionalVariables: ['delegationName', 'senderName'],
  requiredSystemBlocks: ['actionBlock'],
  optionalSystemBlocks: ['setupSummaryBlock'],
  systemBlockPreviews: {
    actionBlock: '<div class="pems-action-sample"><a href="#" style="color:#fff">Đồng ý</a></div>',
    setupSummaryBlock: '<table role="presentation"><tbody><tr><td>Mẫu chuẩn bị</td></tr></tbody></table>',
  },
  sensitiveVariables: [],
  forbiddenInSubject: [],
  actionSupported: true,
  actionRequired: true,
  systemActionDescription: null,
  senderVariableCapability: 'AVAILABLE_READ_ONLY_RUNTIME',
  senderVariables: ['senderName'],
  senderVariablesAllowed: true,
  runtimeEditable: false,
  carriesSecret: false,
  allowCc: true,
  allowBcc: true,
  securityClassification: 'STANDARD',
  editableFields: ['name', 'description', 'subjectVi', 'subjectEn', 'bodyVi', 'bodyEn'],
} as unknown as TemplateContract;

const preview = (subject: string, body: string) =>
  buildTemplateDraftPreview(CONTRACT, { subject, body });

describe('buildTemplateDraftPreview', () => {
  it('substitutes the contract samples into the subject and the body', () => {
    const out = preview('Mời {{fullName}}', '<p>Kính gửi {{fullName}}, đoàn {{delegationName}}.</p>');

    expect(out.subject).toBe('Mời Nguyễn Văn An');
    expect(out.bodyHtml).toContain('Nguyễn Văn An');
    expect(out.bodyHtml).toContain('Đoàn THPT Chu Văn An');
    expect(out.bodyHtml).not.toContain('{{');
  });

  it('renders a system block as the inert sample the backend supplied', () => {
    const out = preview('x', '<p>a</p>{{actionBlock}}<p>b</p>');

    expect(out.bodyHtml).toContain('Đồng ý');
    expect(out.bodyHtml).not.toContain('{{actionBlock}}');
    // In place, not appended: where the author put it is what the recipient will see.
    expect(out.bodyHtml.indexOf('Đồng ý')).toBeGreaterThan(out.bodyHtml.indexOf('a</p>'));
    expect(out.bodyHtml.indexOf('Đồng ý')).toBeLessThan(out.bodyHtml.indexOf('b</p>'));
  });

  /**
   * The ordering rule. A block's sample is trusted markup built by the backend; substituting it BEFORE
   * the variables would put it through the variable pass, so a sample containing something that looks
   * like a placeholder would be rewritten — a preview showing content no send would ever produce.
   */
  it('substitutes variables before blocks, so block markup is never scanned', () => {
    const contract = {
      ...CONTRACT,
      systemBlockPreviews: { ...CONTRACT.systemBlockPreviews, actionBlock: '<p>Mẫu {{fullName}}</p>' },
    } as TemplateContract;

    const out = buildTemplateDraftPreview(contract, { subject: '', body: '<p>{{fullName}}</p>{{actionBlock}}' });

    // The author's variable is substituted; the one inside the block's own markup is left as it came.
    expect(out.bodyHtml).toContain('<p>Nguyễn Văn An</p>');
    expect(out.bodyHtml).toContain('Mẫu {{fullName}}');
  });

  it('shows a block with no sample as nothing, rather than as raw braces', () => {
    const contract = { ...CONTRACT, systemBlockPreviews: {} } as unknown as TemplateContract;

    const out = buildTemplateDraftPreview(contract, { subject: '', body: '<p>a</p>{{setupSummaryBlock}}' });

    expect(out.bodyHtml).not.toContain('{{setupSummaryBlock}}');
    expect(out.bodyHtml).toContain('<p>a</p>');
  });

  // ── §14 every format the editor offers reaches the pane ───────────────────

  it.each([
    ['font', '<p><span style="font-family: Georgia;">a</span></p>', 'font-family'],
    ['size', '<p><span style="font-size: 18px;">a</span></p>', 'font-size'],
    ['colour', '<p><span style="color: rgb(255, 0, 0);">a</span></p>', 'color'],
    ['background', '<p><span style="background-color: rgb(255, 255, 0);">a</span></p>', 'background-color'],
    ['alignment', '<p style="text-align: center;">a</p>', 'text-align'],
    ['indent', '<p style="margin-left: 16px;">a</p>', 'margin-left'],
    ['bold', '<p><strong>a</strong></p>', '<strong>'],
    ['italic', '<p><em>a</em></p>', '<em>'],
    ['underline', '<p><u>a</u></p>', '<u>'],
    ['strike', '<p><s>a</s></p>', '<s>'],
    ['bullet list', '<ul><li>a</li></ul>', '<ul>'],
    ['ordered list', '<ol><li>a</li></ol>', '<ol>'],
    ['divider', '<hr style="border:none;border-top:1px solid #e2e8f0;margin:20px 0">', '<hr'],
    ['table', '<table role="presentation" style="border-collapse:collapse"><tbody><tr><td style="border:1px solid #dbe4ee">ô</td></tr></tbody></table>', 'border:1px solid #dbe4ee'],
    ['link', '<p><a href="https://pems.fpt.edu.vn/x">a</a></p>', 'href="https://pems.fpt.edu.vn/x"'],
  ])('keeps %s in the preview', (_label, body, expected) => {
    expect(preview('', body).bodyHtml).toContain(expected);
  });

  // ── §49 and it is still a sanitiser ───────────────────────────────────────

  it.each([
    ['a script', '<p>a</p><script>alert(1)</script>', 'alert(1)'],
    ['an iframe', '<p>a</p><iframe src="https://evil"></iframe>', '<iframe'],
    ['an event handler', '<p onclick="steal()">a</p>', 'onclick'],
    ['a javascript: link', '<p><a href="javascript:steal()">a</a></p>', 'javascript:'],
  ])('strips %s before it can reach the pane', (_label, body, forbidden) => {
    expect(preview('', body).bodyHtml).not.toContain(forbidden);
  });

  it('sanitises what a SAMPLE brings, not only what the author wrote', () => {
    const contract = {
      ...CONTRACT,
      systemBlockPreviews: { actionBlock: '<div onclick="steal()">Đồng ý</div>' },
    } as unknown as TemplateContract;

    const out = buildTemplateDraftPreview(contract, { subject: '', body: '{{actionBlock}}' });

    expect(out.bodyHtml).toContain('Đồng ý');
    expect(out.bodyHtml).not.toContain('onclick');
  });

  // ── the contract may not have arrived yet ─────────────────────────────────

  it('shows the draft as it stands when no contract is available', () => {
    const out = buildTemplateDraftPreview(null, {
      subject: 'Mời {{fullName}}',
      body: '<p>Kính gửi {{fullName}}</p>',
    });

    // Placeholders left visible rather than substituted from a contract that is not this template's.
    expect(out.subject).toBe('Mời {{fullName}}');
    expect(out.bodyHtml).toContain('{{fullName}}');
  });

  it('is empty for an empty draft rather than throwing', () => {
    expect(buildTemplateDraftPreview(CONTRACT, { subject: '', body: '' }))
      .toEqual({ subject: '', bodyHtml: '' });
  });
});
