/**
 * The shared email editor (V4 §5–§7), against a REAL Quill.
 *
 * These deliberately do not mock `react-quill-new`. Every pre-existing editor test in this project does,
 * and that is precisely how two defects reached this branch unnoticed: the action node being dropped, and
 * every alignment arriving at the recipient as a `ql-align-center` class no mail client has a rule for.
 * An editor test that mocks the editor tests the mock.
 */
import React, { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
// @ts-ignore - react-quill-new ships without bundled types in this project
import { Quill } from 'react-quill-new';
import { EmailRichTextEditor } from '../components/EmailRichTextEditor';
import { EMAIL_FONTS, EMAIL_SIZES } from '../utils/emailEditorFormats';
import { SYSTEM_ACTION_NODE, countSystemActionNodes } from '../utils/systemActionNode';
import { cleanInlineStyle, hasSpaceRun, normalizeSpaceRuns } from '../utils/emailEditorPaste';
import { COMPOSE_CAPABILITIES, TEMPLATE_CAPABILITIES, capabilitiesFor } from '../utils/emailEditorCapabilities';

/**
 * Renders the editor the way a host screen actually uses it: STATEFUL.
 *
 * A static `value` with a spy `onChange` is not a simplification here, it is a different component — the
 * editor is controlled, so an unchanging prop makes React re-render with the original document and Quill
 * reload it, reverting every edit a moment after it happens. Tests written that way pass or fail on
 * timing rather than on behaviour.
 */
function setup(props: Partial<React.ComponentProps<typeof EmailRichTextEditor>> = {}) {
  const onNotice = vi.fn();
  const seen: string[] = [];

  function Host() {
    const [html, setHtml] = useState(props.value ?? '<p>xin chào</p>');
    return (
      <EmailRichTextEditor
        mode="TEMPLATE"
        onNotice={onNotice}
        data-testid="editor"
        {...props}
        value={html}
        onChange={(next) => { seen.push(next); setHtml(next); }}
      />
    );
  }

  const utils = render(<Host />);
  const root = utils.container.querySelector('.ql-editor') as HTMLElement;

  return {
    ...utils,
    onNotice,
    root,
    /**
     * The document as it stands now, read from the live editor rather than from a spy's argument.
     * `.ql-editor`'s innerHTML IS Quill's document — no lookup needed, and it stays correct across the
     * re-renders a controlled component goes through.
     */
    html: () => root.innerHTML,
    /** Every value the editor has emitted, i.e. what the host screen would have stored. */
    emitted: () => seen,
  };
}

/**
 * Puts QUILL's own selection over the whole document, which is what an inline format applies to.
 * A DOM Range is not enough: Quill tracks its selection separately, and a format applied while its
 * selection is collapsed lands on the cursor rather than on the text.
 */
function quillOf(container: HTMLElement) {
  return (Quill as any).find(container.querySelector('.ql-container') as HTMLElement);
}

function selectAll(container: HTMLElement) {
  const q = quillOf(container);
  q.setSelection(0, q.getLength(), 'user');
  return q;
}

describe('it mounts a real editor', () => {
  it('renders the Quill surface and the toolbar', () => {
    const { container } = setup();

    expect(container.querySelector('.ql-editor')).toBeTruthy();
    expect(screen.getByRole('toolbar', { name: /Định dạng nội dung email/ })).toBeTruthy();
  });

  it('shows the document it was given', () => {
    const { root } = setup({ value: '<p>Kính gửi anh Nam</p>' });
    expect(root.textContent).toContain('Kính gửi anh Nam');
  });
});

// ── §6.1 the toolbar exists, in full ────────────────────────────────────────

describe('the toolbar offers what V4 §6.1 lists', () => {
  it.each([
    'Hoàn tác', 'Làm lại', 'Đậm', 'Nghiêng', 'Gạch chân', 'Gạch ngang',
    'Căn trái', 'Căn giữa', 'Căn phải',
    'Danh sách đánh số', 'Danh sách gạch đầu dòng',
    'Giảm thụt lề', 'Tăng thụt lề',
    'Chèn liên kết', 'Chèn đường kẻ ngang', 'Xóa định dạng', 'Toàn màn hình',
  ])('has a %s control', (label) => {
    setup();
    expect(screen.getByRole('button', { name: label })).toBeTruthy();
  });

  it('offers the font and size ladders, and nothing outside them', () => {
    setup();

    const fonts = screen.getByLabelText('Phông chữ') as HTMLSelectElement;
    const sizes = screen.getByLabelText('Cỡ chữ') as HTMLSelectElement;

    // The empty first option is the "unset" placeholder, not a value.
    expect(Array.from(fonts.options).slice(1).map((o) => o.value)).toEqual([...EMAIL_FONTS]);
    expect(Array.from(sizes.options).slice(1).map((o) => o.value)).toEqual([...EMAIL_SIZES]);
  });

  it('hides the image button when the host provides no uploader', () => {
    setup();
    expect(screen.queryByRole('button', { name: 'Chèn ảnh' })).toBeNull();
  });

  it('shows the image button when it can actually upload', () => {
    setup({ onUploadImage: vi.fn() });
    expect(screen.getByRole('button', { name: 'Chèn ảnh' })).toBeTruthy();
  });
});

// ── §6.2 / §6.3 / §7.2 the output an email can carry ────────────────────────

describe('formatting produces inline CSS, not classes', () => {
  it('aligns with a style attribute', async () => {
    const { container, html } = setup({ value: '<p>giữa</p>' });
    selectAll(container);

    fireEvent.click(screen.getByRole('button', { name: 'Căn giữa' }));

    await waitFor(() => expect(html()).toContain('text-align: center'));
    // The default Quill behaviour, which no mail client can render.
    expect(html()).not.toContain('ql-align');
  });

  it('indents with margin-left, stepping through the fixed ladder', async () => {
    const { container, html } = setup({ value: '<p>thụt lề</p>' });
    selectAll(container);

    fireEvent.click(screen.getByRole('button', { name: 'Tăng thụt lề' }));

    await waitFor(() => expect(html()).toContain('margin-left: 16px'));
    expect(html()).not.toContain('ql-indent');
  });

  it('sets a size from the ladder as a style', async () => {
    const { container, html } = setup({ value: '<p>cỡ chữ</p>' });
    selectAll(container);

    fireEvent.change(screen.getByLabelText('Cỡ chữ'), { target: { value: '18px' } });

    await waitFor(() => expect(html()).toContain('font-size: 18px'));
  });

  it('sets a font from the whitelist as a style', async () => {
    const { container, html } = setup({ value: '<p>phông</p>' });
    selectAll(container);

    fireEvent.change(screen.getByLabelText('Phông chữ'), { target: { value: 'Georgia' } });

    await waitFor(() => expect(html()).toContain('font-family: Georgia'));
  });

  it('inserts a divider, which Quill drops without a blot for it', async () => {
    const { html } = setup({ value: '<p>trên</p>' });

    fireEvent.click(screen.getByRole('button', { name: 'Chèn đường kẻ ngang' }));

    await waitFor(() => expect(html()).toContain('<hr'));
  });
});

// ── §9 the action block, through the shared editor ──────────────────────────

describe('the system action block', () => {
  it('is offered in TEMPLATE mode and withheld in COMPOSE', () => {
    const { unmount } = setup({ mode: 'TEMPLATE' });
    expect(screen.getByRole('button', { name: 'Chèn khối nút phản hồi' })).toBeTruthy();
    unmount();

    setup({ mode: 'COMPOSE' });
    expect(screen.queryByRole('button', { name: 'Chèn khối nút phản hồi' })).toBeNull();
  });

  it('survives the editor and comes back in canonical form', async () => {
    const { emitted, html } = setup({ value: `<p>a</p>${SYSTEM_ACTION_NODE}<p>b</p>` });

    // Mounting alone round-trips the document through Quill — the node must still be there afterwards.
    expect(html()).toContain('data-system-block');

    // …and what the host would store is the CANONICAL node, exactly once: no editor class, no label.
    if (emitted().length > 0) {
      expect(countSystemActionNodes(emitted().at(-1)!)).toBe(1);
      expect(emitted().at(-1)!).not.toContain('pems-system-action-block');
    }
  });

  it('refuses a second one rather than minting one token into two buttons', () => {
    const { onNotice } = setup({ value: `<p>a</p>${SYSTEM_ACTION_NODE}` });

    fireEvent.click(screen.getByRole('button', { name: 'Chèn khối nút phản hồi' }));

    expect(onNotice).toHaveBeenCalledWith(expect.stringContaining('một khối nút phản hồi'));
  });
});

// ── §8.1 variables ──────────────────────────────────────────────────────────

describe('variable insertion', () => {
  const variables = [
    { name: 'senderName', label: 'Họ tên người gửi' },
    { name: 'delegationName', label: 'Tên đoàn' },
  ];

  it('is offered in TEMPLATE mode only — COMPOSE text is already substituted', () => {
    const { unmount } = setup({ mode: 'TEMPLATE', variables });
    expect(screen.getByRole('button', { name: 'Chèn biến' })).toBeTruthy();
    unmount();

    setup({ mode: 'COMPOSE', variables });
    expect(screen.queryByRole('button', { name: 'Chèn biến' })).toBeNull();
  });

  it('inserts the placeholder the renderer will substitute', async () => {
    const { html } = setup({ mode: 'TEMPLATE', variables, value: '<p>Kính gửi </p>' });

    fireEvent.click(screen.getByRole('button', { name: 'Chèn biến' }));
    fireEvent.click(screen.getByRole('button', { name: /Họ tên người gửi/ }));

    await waitFor(() => expect(html()).toContain('{{senderName}}'));
  });
});

// ── §14.5 links ─────────────────────────────────────────────────────────────

describe('links', () => {
  it('refuses a scheme an email may not carry', () => {
    const prompt = vi.spyOn(window, 'prompt').mockReturnValue('javascript:alert(1)');
    const { container, onNotice } = setup();
    selectAll(container);

    fireEvent.click(screen.getByRole('button', { name: 'Chèn liên kết' }));

    expect(onNotice).toHaveBeenCalledWith(expect.stringContaining('http, https, mailto hoặc tel'));
    prompt.mockRestore();
  });

  it('accepts an ordinary https link', async () => {
    const prompt = vi.spyOn(window, 'prompt').mockReturnValue('https://pems.fpt.edu.vn/x');
    const { container, html, onNotice } = setup({ value: '<p>bấm đây</p>' });
    selectAll(container);

    fireEvent.click(screen.getByRole('button', { name: 'Chèn liên kết' }));

    await waitFor(() => expect(html()).toContain('https://pems.fpt.edu.vn/x'));
    expect(onNotice).not.toHaveBeenCalled();
    prompt.mockRestore();
  });
});

// ── §5.4 capabilities ───────────────────────────────────────────────────────

describe('capabilities', () => {
  it('never permits raw HTML in either mode', () => {
    expect(TEMPLATE_CAPABILITIES.allowRawHtml).toBe(false);
    expect(COMPOSE_CAPABILITIES.allowRawHtml).toBe(false);
    expect(capabilitiesFor('COMPOSE').allowRawHtml).toBe(false);
  });

  it('lets COMPOSE move the action block but not create or delete one', () => {
    expect(COMPOSE_CAPABILITIES.allowSystemBlockMove).toBe(true);
    expect(COMPOSE_CAPABILITIES.allowSystemBlockInsert).toBe(false);
    expect(COMPOSE_CAPABILITIES.allowSystemBlockDelete).toBe(false);
  });

  it('lets a caller override where its flow genuinely differs', () => {
    setup({ mode: 'COMPOSE', capabilities: { allowSystemBlockInsert: true } });
    expect(screen.getByRole('button', { name: 'Chèn khối nút phản hồi' })).toBeTruthy();
  });
});

// ── §7 / §14.8 paste and whitespace, as pure functions ──────────────────────

describe('paste cleanup', () => {
  it.each([
    'position:absolute', 'z-index:99', 'transform:scale(2)', 'animation:x 1s',
    'display:none', 'visibility:hidden', 'opacity:0', 'font-size:0',
  ])('drops %s', (decl) => {
    expect(cleanInlineStyle(decl)).toBeNull();
  });

  it('keeps the formatting a sender actually wanted', () => {
    const kept = cleanInlineStyle('color:#374151;position:absolute;text-align:center');
    expect(kept).toContain('color');
    expect(kept).toContain('text-align');
    expect(kept).not.toContain('position');
  });

  it('normalises runs of spaces rather than turning them into nbsp', () => {
    expect(normalizeSpaceRuns('Số điện thoại:     0901234567')).toBe('Số điện thoại: 0901234567');
    expect(hasSpaceRun('a   b')).toBe(true);
    expect(hasSpaceRun('a b')).toBe(false);
  });
});
