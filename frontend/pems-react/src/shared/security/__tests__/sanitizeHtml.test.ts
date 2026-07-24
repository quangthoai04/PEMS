import { describe, expect, it } from 'vitest';
import { sanitizeHtml, htmlToPlainText } from '../sanitizeHtml';

describe('sanitizeHtml — safe rich-text render', () => {
  it('keeps valid formatting tags', () => {
    const out = sanitizeHtml('<h2>Tiêu đề</h2><p>Một <strong>đoạn</strong> văn.</p><ul><li>mục</li></ul>');
    expect(out).toContain('<h2>');
    expect(out).toContain('<strong>');
    expect(out).toContain('<li>');
    expect(out).toContain('Tiêu đề');
  });

  it('strips <script> entirely', () => {
    const out = sanitizeHtml('<p>ok</p><script>alert(1)</script>');
    expect(out).not.toContain('<script');
    expect(out.toLowerCase()).not.toContain('alert(1)');
    expect(out).toContain('ok');
  });

  it('removes inline event handlers', () => {
    const out = sanitizeHtml('<p onclick="steal()">x</p><img src="x" onerror="hack()">');
    expect(out).not.toMatch(/onclick/i);
    expect(out).not.toMatch(/onerror/i);
  });

  it('neutralizes javascript: URLs', () => {
    const out = sanitizeHtml('<a href="javascript:alert(1)">click</a>');
    expect(out).not.toMatch(/href=["']javascript:/i);
  });

  it('drops iframes', () => {
    const out = sanitizeHtml('<p>a</p><iframe src="https://evil"></iframe>');
    expect(out).not.toContain('<iframe');
  });

  it('is empty for empty input', () => {
    expect(sanitizeHtml('')).toBe('');
    expect(sanitizeHtml(null)).toBe('');
    expect(sanitizeHtml(undefined)).toBe('');
  });
});

describe('htmlToPlainText — list-preview excerpt', () => {
  it('strips all tags, leaving readable text', () => {
    const out = htmlToPlainText('<h2>Biên bản</h2><p>Nội dung <strong>quan trọng</strong>.</p>');
    expect(out).not.toContain('<');
    expect(out).toContain('Biên bản');
    expect(out).toContain('quan trọng');
  });

  it('collapses whitespace and truncates with an ellipsis', () => {
    const long = '<p>' + 'a'.repeat(300) + '</p>';
    const out = htmlToPlainText(long, 50);
    expect(out.length).toBeLessThanOrEqual(51); // 50 + ellipsis
    expect(out.endsWith('…')).toBe(true);
  });

  it('is empty for empty input', () => {
    expect(htmlToPlainText('')).toBe('');
    expect(htmlToPlainText(null)).toBe('');
  });
});
