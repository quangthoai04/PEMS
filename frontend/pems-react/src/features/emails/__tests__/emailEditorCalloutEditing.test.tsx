/**
 * Editable/Addable/Removable callout frames (email callout frames plan), against a REAL Quill and a REAL
 * rendered `EmailRichTextEditor` — including the nested callout-content mini-editor, since that is a
 * second, independent Quill instance whose own capabilities are what actually prevent nesting.
 *
 * Deliberately not mocked, for the same reason `EmailRichTextEditor.test.tsx` and `emailEditorCallouts.
 * test.ts` are not: a mocked editor tests the mock, and every defect this feature guards against (a frame
 * becoming read-only, a nested callout, a lost variable) is exactly the kind that a mock cannot reproduce.
 */
import React, { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import {
  act, render, screen, fireEvent, waitFor, within,
} from '@testing-library/react';
// @ts-ignore - react-quill-new ships without bundled types in this project
import { Quill } from 'react-quill-new';
import { EmailRichTextEditor } from '../components/EmailRichTextEditor';
import { CALLOUT_WRAPPER_CLASS } from '../utils/emailEditorCallouts';
import { isSameEmailHtml } from '../utils/emailHtmlCanonicalizer';

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
    html: () => root.innerHTML,
    emitted: () => seen,
  };
}

/** The real Quill instance behind whichever `.ql-container` sits inside `el`. */
function quillIn(el: HTMLElement): any {
  /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
  return (Quill as any).find(el.querySelector('.ql-container') as HTMLElement);
}

/**
 * Normalizes `&nbsp;` back to a plain space before a text-content assertion.
 *
 * A real Quill mount routinely re-spells an ordinary space as `&nbsp;` on load (the same characteristic
 * `emailHtmlCanonicalizer.ts`'s own `canonicalizeEmailHtml` already treats as equivalent to a plain space
 * — see its `.replace(/&nbsp;| /g, ' ')`). Asserting readable Vietnamese text with literal spaces needs the
 * same normalization, or an assertion fails on notation rather than on content.
 */
function norm(html: string): string {
  return html.replace(/&nbsp;/g, ' ');
}

const SECURITY_HTML = '<div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;'
  + 'border-radius:8px;color:#9a3412;line-height:1.6"><strong>Lưu ý bảo mật:</strong> Không chia sẻ liên kết này.</div>';

const SENDER_VARIABLES = [
  { name: 'senderName', label: 'Họ tên người gửi' },
  { name: 'senderRole', label: 'Chức vụ người gửi' },
  { name: 'campusName', label: 'Tên cơ sở' },
];

// Real ACCOUNT_ACTIVATED shape: ONE paragraph, three internal <br>s joining the variables.
const SENDER_INFO_HTML = '<div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;'
  + 'border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;'
  + 'color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155">'
  + '<strong>{{senderName}}</strong><br/>{{senderRole}}</p></div>';

const ACTION_HTML = '<div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;'
  + 'border-radius:8px"><p>Cần bạn xác nhận</p><p>Bấm nút bên dưới.</p>{{actionBlock}}</div>';

const SYSTEM_BLOCKS = [{ name: 'actionBlock', label: 'Khu vực nút thao tác' }];

// ── A. Security callout — edit prose, add sentence, bold, save/reload keeps the style ──────────────

describe('A. Security callout content editing', () => {
  it('edit title/body, add a sentence, save/reload — style preserved', async () => {
    const { container, emitted } = setup({ value: `<p>trên</p>${SECURITY_HTML}<p>dưới</p>` });

    fireEvent.click(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)!);
    fireEvent.click(screen.getByRole('button', { name: 'Sửa nội dung khung' }));

    const dialog = await screen.findByRole('dialog', { name: 'Sửa nội dung khung' });
    const inner = quillIn(dialog);
    await act(async () => {
      inner.setSelection(inner.getLength() - 1, 0, 'user');
      inner.insertText(inner.getLength() - 1, ' Thêm một câu nữa.', 'user');
    });

    fireEvent.click(within(dialog).getByTestId('callout-content-dialog-apply'));

    await waitFor(() => expect(norm(emitted().at(-1) ?? '')).toContain('Thêm một câu nữa.'));
    const saved = emitted().at(-1) ?? '';
    expect(saved).toContain('background:#fff7ed');
    expect(saved).toContain('border:1px solid #fed7aa');
    expect(saved).toContain('trên');
    expect(saved).toContain('dưới');
  });

  it('Apply Content Edit is one logical undo step: one undo restores the pre-edit content, one redo re-applies it', async () => {
    const { container, emitted } = setup({ value: SECURITY_HTML });
    const q = quillIn(container);

    fireEvent.click(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)!);
    fireEvent.click(screen.getByRole('button', { name: 'Sửa nội dung khung' }));
    const dialog = await screen.findByRole('dialog', { name: 'Sửa nội dung khung' });
    const inner = quillIn(dialog);
    await act(async () => { inner.insertText(inner.getLength() - 1, ' Thêm.', 'user'); });
    fireEvent.click(within(dialog).getByTestId('callout-content-dialog-apply'));

    await waitFor(() => expect(norm(emitted().at(-1) ?? '')).toContain('Thêm.'));

    await act(async () => { q.history.undo(); });
    await waitFor(() => expect(norm(emitted().at(-1) ?? '')).not.toContain('Thêm.'));
    // The frame itself must not have been partially undone — it is still there, one whole step back.
    expect(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)).toBeTruthy();

    await act(async () => { q.history.redo(); });
    await waitFor(() => expect(norm(emitted().at(-1) ?? '')).toContain('Thêm.'));
  });

  it('bold applied inside the mini-editor survives into the saved template', async () => {
    const { container, emitted } = setup({ value: SECURITY_HTML });

    fireEvent.click(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)!);
    fireEvent.click(screen.getByRole('button', { name: 'Sửa nội dung khung' }));
    const dialog = await screen.findByRole('dialog', { name: 'Sửa nội dung khung' });
    const inner = quillIn(dialog);

    await act(async () => { inner.setSelection(0, inner.getLength() - 1, 'user'); });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Đậm' }));
    fireEvent.click(within(dialog).getByTestId('callout-content-dialog-apply'));

    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('<strong>'));
  });
});

// ── B. Sender-info callout — variables stay structured through a real edit ─────────────────────────

describe('B. Sender-info callout variable preservation', () => {
  it('adds prose around existing variables and inserts a new one, keeping every variable identity', async () => {
    const { container, emitted } = setup({ value: SENDER_INFO_HTML, variables: SENDER_VARIABLES });

    fireEvent.click(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)!);
    fireEvent.click(screen.getByRole('button', { name: 'Sửa nội dung khung' }));
    const dialog = await screen.findByRole('dialog', { name: 'Sửa nội dung khung' });
    const inner = quillIn(dialog);

    // Retitle "Thông tin người gửi" → "Người phụ trách email".
    await act(async () => {
      inner.deleteText(0, 'Thông tin người gửi'.length, 'user');
      inner.insertText(0, 'Người phụ trách email', 'user');
    });

    // Insert a new variable via the SAME picker contract, at the end of the document.
    await act(async () => { inner.setSelection(inner.getLength() - 1, 0, 'user'); });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Chèn biến' }));
    fireEvent.click(within(dialog).getByRole('button', { name: /Tên cơ sở/ }));

    fireEvent.click(within(dialog).getByTestId('callout-content-dialog-apply'));

    await waitFor(() => expect(norm(emitted().at(-1) ?? '')).toContain('Người phụ trách email'));
    const saved = emitted().at(-1) ?? '';
    expect(saved).toContain('{{senderName}}');
    expect(saved).toContain('{{senderRole}}');
    expect(saved).toContain('{{campusName}}');
    expect(saved).not.toContain('Họ tên người gửi');   // the label, never stored
    expect(saved).not.toContain('pems-variable-chip');
  });

  it('rejects malformed raw variable editing — a chip can only be inserted/removed whole, never retyped', async () => {
    const { container } = setup({ value: SENDER_INFO_HTML, variables: SENDER_VARIABLES });

    fireEvent.click(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)!);
    fireEvent.click(screen.getByRole('button', { name: 'Sửa nội dung khung' }));
    const dialog = await screen.findByRole('dialog', { name: 'Sửa nội dung khung' });

    // The chip's label is a non-editable content node inside the chip (see emailEditorVariableChips.ts) —
    // there is no keystroke that mutates `data-variable` to an unknown name; the chip is atomic.
    const chip = dialog.querySelector('[data-variable="senderName"]');
    expect(chip).toBeTruthy();
    expect(chip!.querySelector('[contenteditable="false"]')).toBeTruthy();
  });
});

// ── C. Action callout — prose editable, {{actionBlock}} protected, never duplicable ─────────────────

describe('C. Action callout: protected system block', () => {
  it('edits prose before/after {{actionBlock}}; exactly one survives; no insert-another control exists', async () => {
    const { container, emitted } = setup({ value: ACTION_HTML, systemBlocks: SYSTEM_BLOCKS });

    fireEvent.click(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)!);
    fireEvent.click(screen.getByRole('button', { name: 'Sửa nội dung khung' }));
    const dialog = await screen.findByRole('dialog', { name: 'Sửa nội dung khung' });

    // No control to insert a NEW action/template block inside the mini-editor.
    expect(within(dialog).queryByRole('button', { name: /Chèn khối/ })).toBeNull();

    const inner = quillIn(dialog);
    await act(async () => { inner.insertText(0, 'X', 'user'); });
    fireEvent.click(within(dialog).getByTestId('callout-content-dialog-apply'));

    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('X'));
    const saved = emitted().at(-1) ?? '';
    expect((saved.match(/\{\{actionBlock\}\}/g) ?? []).length).toBe(1);
    expect(saved).not.toContain('data-system-block');   // no live/resolved token leaked into a template
  });
});

// ── D. Add Frame ─────────────────────────────────────────────────────────────────────────────────

describe('D. Add Frame', () => {
  it.each(['Thông tin', 'Cảnh báo', 'Bảo mật', 'Trung tính'])(
    'wraps a selection as %s, remains editable, and can gain new text',
    async (label) => {
      const { container, emitted } = setup({ value: '<p>Dòng một</p><p>Dòng hai</p>' });
      const q = quillIn(container);

      await act(async () => { q.setSelection(0, q.getLength() - 1, 'user'); });
      fireEvent.click(screen.getByRole('button', { name: 'Thêm khung' }));
      fireEvent.click(screen.getByRole('button', { name: label }));

      await waitFor(() => expect(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)).toBeTruthy());
      expect(emitted().at(-1) ?? '').toContain('Dòng một');
      expect(emitted().at(-1) ?? '').toContain('Dòng hai');

      // Still editable: open it and add a sentence.
      fireEvent.click(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)!);
      fireEvent.click(screen.getByRole('button', { name: 'Sửa nội dung khung' }));
      const dialog = await screen.findByRole('dialog', { name: 'Sửa nội dung khung' });
      const inner = quillIn(dialog);
      await act(async () => { inner.insertText(inner.getLength() - 1, ' thêm.', 'user'); });
      fireEvent.click(within(dialog).getByTestId('callout-content-dialog-apply'));

      await waitFor(() => expect(emitted().at(-1) ?? '').toContain('thêm.'));
    },
  );

  it('Add Frame is one logical undo step: one undo restores the plain paragraphs, one redo re-wraps them', async () => {
    const { container, emitted } = setup({ value: '<p>Dòng một</p><p>Dòng hai</p>' });
    const q = quillIn(container);

    await act(async () => { q.setSelection(0, q.getLength() - 1, 'user'); });
    fireEvent.click(screen.getByRole('button', { name: 'Thêm khung' }));
    fireEvent.click(screen.getByRole('button', { name: 'Thông tin' }));
    await waitFor(() => expect(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)).toBeTruthy());

    await act(async () => { q.history.undo(); });
    await waitFor(() => expect(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)).toBeNull());
    expect(norm(emitted().at(-1) ?? '')).toContain('Dòng một');
    expect(norm(emitted().at(-1) ?? '')).toContain('Dòng hai');

    await act(async () => { q.history.redo(); });
    await waitFor(() => expect(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)).toBeTruthy());
    expect(norm(emitted().at(-1) ?? '')).toContain('Dòng một');
    expect(norm(emitted().at(-1) ?? '')).toContain('Dòng hai');
  });

  it('refuses a partial/unsupported selection without mutating the document', async () => {
    const { container, emitted, onNotice } = setup({ value: '<p>Hello world</p>' });
    const q = quillIn(container);
    const before = emitted().length;

    // "Hello" only — starts at 0 (fine) but ends mid-line.
    await act(async () => { q.setSelection(0, 5, 'user'); });
    fireEvent.click(screen.getByRole('button', { name: 'Thêm khung' }));
    fireEvent.click(screen.getByRole('button', { name: 'Thông tin' }));

    expect(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)).toBeNull();
    expect(onNotice).toHaveBeenCalled();
    expect(emitted().length).toBe(before);   // no document mutation at all
  });

  it('refuses wrapping a selection that already contains an existing callout (no nested frame)', async () => {
    const { container, onNotice } = setup({ value: `<p>Before</p>${SECURITY_HTML}<p>After</p>` });
    const q = quillIn(container);

    await act(async () => { q.setSelection(0, q.getLength() - 1, 'user'); });
    fireEvent.click(screen.getByRole('button', { name: 'Thêm khung' }));
    fireEvent.click(screen.getByRole('button', { name: 'Thông tin' }));

    expect(onNotice).toHaveBeenCalled();
    // Still exactly one callout — the original — never two nested inside each other.
    expect(container.querySelectorAll(`.${CALLOUT_WRAPPER_CLASS}`).length).toBe(1);
  });
});

// ── E. Remove Frame ──────────────────────────────────────────────────────────────────────────────

describe('E. Remove Frame', () => {
  it('preserves all inner content; one undo restores the frame, one redo removes it again', async () => {
    const { container, emitted } = setup({ value: SENDER_INFO_HTML, variables: SENDER_VARIABLES });
    const q = quillIn(container);

    fireEvent.click(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)!);
    fireEvent.click(screen.getByRole('button', { name: 'Xóa khung' }));

    await waitFor(() => expect(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)).toBeNull());
    const afterRemove = emitted().at(-1) ?? '';
    expect(afterRemove).toContain('{{senderName}}');
    expect(afterRemove).toContain('{{senderRole}}');
    expect(norm(afterRemove)).toContain('Thông tin người gửi');

    await act(async () => { q.history.undo(); });
    await waitFor(() => expect(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)).toBeTruthy());

    await act(async () => { q.history.redo(); });
    await waitFor(() => expect(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)).toBeNull());
  });
});

// ── F. Change Frame Type ─────────────────────────────────────────────────────────────────────────

describe('F. Change Frame Type', () => {
  it('changes only presentation; inner content stays semantically equal; undo/redo atomic', async () => {
    const { container, emitted } = setup({ value: SECURITY_HTML });
    const q = quillIn(container);
    const before = emitted().length;

    fireEvent.click(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)!);
    fireEvent.click(screen.getByRole('button', { name: 'Đổi kiểu khung' }));
    fireEvent.click(screen.getByRole('button', { name: 'Thông tin' }));

    await waitFor(() => expect(emitted().length).toBeGreaterThan(before));
    const changed = emitted().at(-1) ?? '';
    expect(changed).toContain('background:#eff6ff');
    expect(changed).not.toContain('background:#fff7ed');

    // Frame type controls the WHOLE style (margin/padding/background/border/color/line-height together,
    // not just background/border) — what must stay identical is the content INSIDE the div, independent
    // of its style attribute entirely.
    const innerOf = (html: string) => new DOMParser().parseFromString(html, 'text/html')
      .body.querySelector('div')?.innerHTML ?? '';
    expect(isSameEmailHtml(innerOf(changed), innerOf(SECURITY_HTML))).toBe(true);

    await act(async () => { q.history.undo(); });
    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('background:#fff7ed'));

    await act(async () => { q.history.redo(); });
    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('background:#eff6ff'));
  });
});

// ── G. Legacy/unknown historical styles are never silently migrated ────────────────────────────────

describe('G. LegacyCustom', () => {
  const LEGACY_HTML = '<div style="margin:18px 0;padding:24px;background:#fff7ed;border:1px solid #fed7aa;'
    + 'border-radius:8px"><p>Legacy content</p></div>';

  it('editing unrelated template text leaves an unknown historical style untouched', async () => {
    const { container, emitted } = setup({ value: `<p>trên</p>${LEGACY_HTML}` });
    const q = quillIn(container);

    await act(async () => { q.insertText(0, 'X', 'user'); });

    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('X'));
    expect(emitted().at(-1)).toContain('padding:24px');
  });

  it('editing prose INSIDE a legacy callout changes content but leaves its style untouched', async () => {
    const { container, emitted } = setup({ value: LEGACY_HTML });

    fireEvent.click(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)!);
    fireEvent.click(screen.getByRole('button', { name: 'Sửa nội dung khung' }));
    const dialog = await screen.findByRole('dialog', { name: 'Sửa nội dung khung' });
    const inner = quillIn(dialog);
    await act(async () => { inner.insertText(0, 'X', 'user'); });
    fireEvent.click(within(dialog).getByTestId('callout-content-dialog-apply'));

    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('X'));
    expect(emitted().at(-1)).toContain('padding:24px');   // untouched — only "Đổi kiểu khung" may change it
  });

  it('only an EXPLICIT "Đổi kiểu khung → Thông tin" converts a legacy style to the canonical preset', async () => {
    const { container, emitted } = setup({ value: LEGACY_HTML });

    fireEvent.click(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)!);
    fireEvent.click(screen.getByRole('button', { name: 'Đổi kiểu khung' }));
    expect(screen.getByText('Hiện tại: Kiểu tùy chỉnh (cũ)')).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Thông tin' }));

    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('background:#eff6ff'));
    expect(emitted().at(-1)).not.toContain('padding:24px');
  });
});

// ── H. No nested callout from inside the mini-editor ────────────────────────────────────────────────

describe('H. No nested frame', () => {
  it('the mini-editor exposes no frame-management control', async () => {
    const { container } = setup({ value: SECURITY_HTML });

    fireEvent.click(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)!);
    fireEvent.click(screen.getByRole('button', { name: 'Sửa nội dung khung' }));
    const dialog = await screen.findByRole('dialog', { name: 'Sửa nội dung khung' });

    expect(within(dialog).queryByRole('button', { name: 'Sửa nội dung khung' })).toBeNull();
    expect(within(dialog).queryByRole('button', { name: 'Đổi kiểu khung' })).toBeNull();
    expect(within(dialog).queryByRole('button', { name: 'Xóa khung' })).toBeNull();
    expect(within(dialog).queryByRole('button', { name: 'Thêm khung' })).toBeNull();
  });

  it('content shaped like a styled container, typed inside the mini-editor, never becomes a nested callout', async () => {
    const { container, emitted } = setup({ value: '<p>trên</p><p>dưới</p>' });

    fireEvent.click(container.querySelector('p')!.parentElement!.parentElement!); // no-op guard, see below
    // No callout exists yet — open one via Add Frame first so there is something to edit.
    const q = quillIn(container);
    await act(async () => { q.setSelection(0, q.getLength() - 1, 'user'); });
    fireEvent.click(screen.getByRole('button', { name: 'Thêm khung' }));
    fireEvent.click(screen.getByRole('button', { name: 'Thông tin' }));
    await waitFor(() => expect(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)).toBeTruthy());

    fireEvent.click(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)!);
    fireEvent.click(screen.getByRole('button', { name: 'Sửa nội dung khung' }));
    const dialog = await screen.findByRole('dialog', { name: 'Sửa nội dung khung' });
    const inner = quillIn(dialog);

    // Paste a styled div INSIDE the mini-editor — with callout authoring disabled there, this must never
    // become an atomic pemsEmailCallout embed, whatever the pasted markup looks like.
    await act(async () => {
      inner.clipboard.dangerouslyPasteHTML(
        inner.getLength() - 1,
        '<div style="background:#fff7ed;padding:14px"><p>injected</p></div>',
        'user',
      );
    });
    expect(dialog.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)).toBeNull();

    fireEvent.click(within(dialog).getByTestId('callout-content-dialog-apply'));

    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('injected'));
    const saved = emitted().at(-1) ?? '';
    // Exactly the ONE outer callout — never two, never nested.
    const outerMatches = saved.match(/background:#eff6ff/g) ?? [];
    expect(outerMatches.length).toBe(1);
    expect(saved).not.toContain(CALLOUT_WRAPPER_CLASS);
  });
});

// ── J. Artifact leak ─────────────────────────────────────────────────────────────────────────────

describe('J. Editor-artifact leak', () => {
  it('stored canonical HTML carries no editor-only marker after an Add Frame + edit-content round trip', async () => {
    const { container, emitted } = setup({ value: '<p>Nội dung</p>', variables: SENDER_VARIABLES });
    const q = quillIn(container);

    await act(async () => { q.setSelection(0, q.getLength() - 1, 'user'); });
    fireEvent.click(screen.getByRole('button', { name: 'Thêm khung' }));
    fireEvent.click(screen.getByRole('button', { name: 'Trung tính' }));
    await waitFor(() => expect(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)).toBeTruthy());

    fireEvent.click(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)!);
    fireEvent.click(screen.getByRole('button', { name: 'Sửa nội dung khung' }));
    const dialog = await screen.findByRole('dialog', { name: 'Sửa nội dung khung' });
    const inner = quillIn(dialog);
    await act(async () => { inner.insertText(inner.getLength() - 1, '!', 'user'); });
    fireEvent.click(within(dialog).getByTestId('callout-content-dialog-apply'));

    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('!'));
    const saved = emitted().at(-1) ?? '';

    expect(saved).not.toContain('data-pems-callout-style');
    expect(saved).not.toContain(CALLOUT_WRAPPER_CLASS);
    expect(saved).not.toContain('contenteditable');
    expect(saved).not.toContain('data-selected');
    expect(saved).not.toContain('callout-content-dialog');
  });
});

// ── K. Conversion order — shared pipeline, no drift between the main and nested editor ─────────────

describe('K. Conversion order', () => {
  it('{{actionBlock}} stays protected (not a generic chip) inside the mini-editor', async () => {
    const { container } = setup({ value: ACTION_HTML, systemBlocks: SYSTEM_BLOCKS });

    fireEvent.click(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)!);
    fireEvent.click(screen.getByRole('button', { name: 'Sửa nội dung khung' }));
    const dialog = await screen.findByRole('dialog', { name: 'Sửa nội dung khung' });

    expect(dialog.querySelector('[data-template-block="actionBlock"]')).toBeTruthy();
    expect(dialog.querySelector('[data-variable="actionBlock"]')).toBeNull();
  });
});

// ── M. COMPOSE-specific ──────────────────────────────────────────────────────────────────────────

describe('M. COMPOSE callout authoring', () => {
  const COMPOSE_RESOLVED_HTML = '<div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;'
    + 'border:1px solid #e2e8f0;border-radius:8px"><p>Thông tin</p><p>Nguyễn Văn A</p></div>';

  it('edits already-resolved text as ordinary prose; no template-variable picker ever appears', async () => {
    const { container, emitted } = setup({ mode: 'COMPOSE', value: COMPOSE_RESOLVED_HTML });

    fireEvent.click(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)!);
    fireEvent.click(screen.getByRole('button', { name: 'Sửa nội dung khung' }));
    const dialog = await screen.findByRole('dialog', { name: 'Sửa nội dung khung' });

    expect(within(dialog).queryByRole('button', { name: 'Chèn biến' })).toBeNull();

    const inner = quillIn(dialog);
    await act(async () => { inner.insertText(0, 'X', 'user'); });
    fireEvent.click(within(dialog).getByTestId('callout-content-dialog-apply'));

    await waitFor(() => expect(norm(emitted().at(-1) ?? '')).toContain('XThông tin'));
    expect(norm(emitted().at(-1) ?? '')).toContain('Nguyễn Văn A');
  });

  it('COMPOSE main editor shows all four frame controls, and Add Frame works there too', async () => {
    const { container } = setup({ mode: 'COMPOSE', value: '<p>Nội dung soạn</p>' });
    const q = quillIn(container);

    expect(screen.getByRole('button', { name: 'Thêm khung' })).toBeTruthy();

    await act(async () => { q.setSelection(0, q.getLength() - 1, 'user'); });
    fireEvent.click(screen.getByRole('button', { name: 'Thêm khung' }));
    fireEvent.click(screen.getByRole('button', { name: 'Bảo mật' }));

    await waitFor(() => expect(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)).toBeTruthy());
    expect(screen.getByRole('button', { name: 'Sửa nội dung khung' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Đổi kiểu khung' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Xóa khung' })).toBeTruthy();
  });

  it('a callout wrapping the existing protected action node keeps it protected and unduplicated', async () => {
    const composeAction = '<div style="margin:20px 0;padding:16px 18px;background:#eff6ff;'
      + 'border:1px solid #bfdbfe;border-radius:8px"><p>Cần bạn phản hồi</p>'
      + '<div data-system-block="action"></div></div>';
    const { container, emitted } = setup({ mode: 'COMPOSE', value: composeAction });

    fireEvent.click(container.querySelector(`.${CALLOUT_WRAPPER_CLASS}`)!);
    fireEvent.click(screen.getByRole('button', { name: 'Sửa nội dung khung' }));
    const dialog = await screen.findByRole('dialog', { name: 'Sửa nội dung khung' });

    expect(dialog.querySelector('[data-system-block="action"]')).toBeTruthy();
    expect(within(dialog).queryByRole('button', { name: 'Chèn khối nút phản hồi' })).toBeNull();

    const inner = quillIn(dialog);
    await act(async () => { inner.insertText(0, 'X', 'user'); });
    fireEvent.click(within(dialog).getByTestId('callout-content-dialog-apply'));

    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('X'));
    const saved = emitted().at(-1) ?? '';
    expect((saved.match(/data-system-block="action"/g) ?? []).length).toBe(1);
  });
});
