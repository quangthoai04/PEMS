/**
 * Dirty-state parity (V4 §15.2, §15.3).
 *
 * The load → edit → serialise → save → reload round trip must be canonical-equivalent, and a document
 * that has merely been OPENED must not report itself as changed. The second half is tested against a real
 * Quill, because the whole difficulty is that the editor rewrites what it is given — and a mock rewrites
 * nothing, so a mocked test of this would pass no matter how wrong the comparison was.
 */
import { beforeAll, describe, expect, it } from 'vitest';
import {
  canonicalizeEmailHtml, isEmailHtmlDirty, isSameEmailHtml,
} from '../utils/emailHtmlCanonicalizer';
import { registerEmailEditorFormats } from '../utils/emailEditorFormats';
import { fromEditorHtml, toEditorHtml } from '../utils/emailEditorSystemNodes';
import { SYSTEM_ACTION_NODE } from '../utils/systemActionNode';

/* eslint-disable @typescript-eslint/no-explicit-any */
let Quill: any;

beforeAll(async () => {
  Quill = (await import('react-quill-new')).Quill;
  registerEmailEditorFormats();
});

/** What the editor makes of a document it is merely shown. */
function throughEditor(html: string): string {
  const host = document.body.appendChild(document.createElement('div'));
  const q = new Quill(host, { modules: { toolbar: false } });
  q.clipboard.dangerouslyPasteHTML(toEditorHtml(html));
  return fromEditorHtml(q.root.innerHTML);
}

describe('notation that must not count as a change', () => {
  it.each([
    ['<p>a<br>b</p>', '<p>a<br />b</p>'],
    ['<p style="color:#fff">x</p>', '<p style="color:#fff;">x</p>'],
    ['<p style="color:#fff;margin:0">x</p>', '<p style="margin:0;color:#fff">x</p>'],
    ['<p style="COLOR:#FFF">x</p>', '<p style="color:#fff">x</p>'],
    ['<p>a</p>  <p>b</p>', '<p>a</p><p>b</p>'],
    ['<p>a&nbsp;b</p>', '<p>a b</p>'],
    ['<p class="ql-x">a</p>', '<p>a</p>'],
    ['<p>a</p><p></p>', '<p>a</p>'],
    ['<p>a</p><p><br></p>', '<p>a</p>'],
    ['<a href="https://x.test" title="t">l</a>', '<a title="t" href="https://x.test">l</a>'],
  ])('treats %s and %s as the same document', (a, b) => {
    expect(isSameEmailHtml(a, b)).toBe(true);
    expect(isEmailHtmlDirty(a, b)).toBe(false);
  });
});

describe('changes a reader would notice', () => {
  it.each([
    ['<p>a</p>', '<p>b</p>', 'different words'],
    ['<p>a</p><p>b</p>', '<p>b</p><p>a</p>', 'different order'],
    ['<p style="color:#fff">x</p>', '<p style="color:#000">x</p>', 'different colour'],
    ['<p>x</p>', '<p style="text-align:center">x</p>', 'newly centred'],
    ['<p>x</p>', `<p>x</p>${SYSTEM_ACTION_NODE}`, 'an action block appeared'],
    ['<ul><li>a</li></ul>', '<ol><li>a</li></ol>', 'list type changed'],
    ['<p>a</p>', '<p>a</p><img src="https://x.test/i.png">', 'an image appeared'],
  ])('reports %s vs %s as changed (%s)', (a, b) => {
    expect(isSameEmailHtml(a, b)).toBe(false);
    expect(isEmailHtmlDirty(a, b)).toBe(true);
  });

  it('does not treat an empty block holding an embed as empty', () => {
    // The action node IS an empty div. Collapsing it would make its removal invisible to the save button.
    expect(isSameEmailHtml('<p>x</p>', `<p>x</p>${SYSTEM_ACTION_NODE}`)).toBe(false);
  });
});

describe('a document that was only opened is not dirty', () => {
  it.each([
    '<p>Kính gửi anh Nam,</p><p>Trân trọng,</p>',
    '<p style="text-align:center">Giữa</p>',
    '<p style="margin-left:32px">Thụt lề</p>',
    '<ul><li>Một</li><li>Hai</li></ul>',
    '<p><strong>Đậm</strong> và <em>nghiêng</em></p>',
    '<p>Liên kết <a href="https://pems.fpt.edu.vn/x">ở đây</a></p>',
    `<p>Trước</p>${SYSTEM_ACTION_NODE}<p>Sau</p>`,
  ])('survives a real editor round trip: %s', (html) => {
    const reloaded = throughEditor(html);

    expect(isEmailHtmlDirty(html, reloaded)).toBe(false);
  });

  it('is stable across repeated open-and-close cycles', () => {
    let body = `<p>Kính gửi anh Nam,</p>${SYSTEM_ACTION_NODE}<p>Trân trọng,</p>`;

    for (let i = 0; i < 3; i += 1) {
      const next = throughEditor(body);
      expect(isEmailHtmlDirty(body, next)).toBe(false);
      body = next;
    }
  });
});

describe('diagnosability', () => {
  it('exposes the canonical form, so a failing comparison can be read', () => {
    expect(canonicalizeEmailHtml('<p  class="x"  style="color:#FFF;">a<br />b</p>'))
      .toBe('<p style="color:#fff">a<br>b</p>');
  });

  it('handles empty and nullish input', () => {
    expect(canonicalizeEmailHtml('')).toBe('');
    expect(canonicalizeEmailHtml(null)).toBe('');
    expect(isSameEmailHtml(null, '')).toBe(true);
  });
});
