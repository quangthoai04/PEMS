/**
 * Runs of spaces are refused, and nothing else is (V4 §7.1, §7.4).
 *
 * <para>
 * The mirror of `EmailSpaceRunsTests` on the backend, which is the authority. This layer exists so the
 * author is told while they are still editing — but it has to agree with the server, or the screen blocks
 * a save that would have succeeded, or waves through one that will not.
 * </para>
 */
import { describe, expect, it } from 'vitest';
import { SPACE_RUN_WARNING, hasSpaceRun, htmlHasSpaceRun } from '../utils/emailEditorPaste';

const NBSP = ' ';

describe('htmlHasSpaceRun catches a run however it is spelled', () => {
  it('sees plain spaces typed between two words', () => {
    expect(htmlHasSpaceRun('<p>Cột A   Cột B</p>')).toBe(true);
  });

  /** The commonest case: this is what the editor emits for a typed run. */
  it('sees non-breaking spaces, which are the worse version', () => {
    expect(htmlHasSpaceRun('<p>Cột A&nbsp;&nbsp;&nbsp;Cột B</p>')).toBe(true);
  });

  it.each(['&#160;', '&#xA0;', '&#x00A0;'])('sees %s, the same character written differently', (e) => {
    expect(htmlHasSpaceRun(`<p>A${e}${e}${e}B</p>`)).toBe(true);
  });

  it('sees a mixture of the two', () => {
    expect(htmlHasSpaceRun(`<p>A ${NBSP}${NBSP}B</p>`)).toBe(true);
  });

  it('sees a run inside a table cell, which is visible text like any other', () => {
    expect(htmlHasSpaceRun(
      '<table><tr><td style="padding:8px 12px">A&nbsp;&nbsp;&nbsp;B</td></tr></table>',
    )).toBe(true);
  });

  it('sees a leading run of non-breaking spaces — somebody indenting by hand', () => {
    expect(htmlHasSpaceRun('<p>&nbsp;&nbsp;&nbsp;&nbsp;Kính gửi Quý vị,</p>')).toBe(true);
  });

  it('sees a run in a link label', () => {
    expect(htmlHasSpaceRun('<p><a href="https://pems.example.com">Xem   chi tiết</a></p>')).toBe(true);
  });
});

describe('htmlHasSpaceRun does not cry wolf', () => {
  it('says nothing about ordinary prose', () => {
    expect(htmlHasSpaceRun('<p>Kính gửi Quý vị, đây là nội dung bình thường.</p>')).toBe(false);
  });

  it('says nothing about a double space, which is ordinary typing', () => {
    expect(htmlHasSpaceRun('<p>Xong.  Tiếp theo là phần hai.</p>')).toBe(false);
  });

  /**
   * <b>The false positive that matters.</b> Formatted markup puts a newline and an indent between
   * elements. Those are separate text nodes and were never one run — but flattening the document to
   * `textContent`, or matching the markup with a pattern, joins them. Measured against the 31 shipped
   * templates that mistake reports 62 offending fields, every one of them spurious, which would mean an
   * operator opening a template and being told they may not save it.
   */
  it('says nothing about the indentation between two elements', () => {
    expect(htmlHasSpaceRun('<p>Xin chào</p>\n      <table>\n        <tr><td>A</td></tr>\n      </table>'))
      .toBe(false);
  });

  it('does not look inside a style attribute', () => {
    expect(htmlHasSpaceRun('<p style="margin:0   0   16px   0">Xin chào</p>')).toBe(false);
  });

  it('does not look inside a URL', () => {
    expect(htmlHasSpaceRun('<p><a href="https://x.test/a?q=1%20%20%20z">Chi tiết</a></p>')).toBe(false);
  });

  /** The dispatcher builds the action area; its spacing is not an editorial decision. */
  it('says nothing about text the system owns', () => {
    expect(htmlHasSpaceRun('<div data-system-block="action"><span>Chấp   nhận</span></div>')).toBe(false);
  });

  it('handles an empty document', () => {
    expect(htmlHasSpaceRun('')).toBe(false);
    expect(htmlHasSpaceRun('<p><br></p>')).toBe(false);
  });
});

describe('the two detectors agree on the same content', () => {
  /**
   * `hasSpaceRun` answers about a STRING — the paste path, where the fragment is read before the editor
   * has touched it. `htmlHasSpaceRun` answers about a document. They must not disagree about a run that
   * is plainly inside one paragraph, or the warning fires on paste and not on typing, which is the bug
   * this pair replaced.
   */
  it.each([
    'Cột A   Cột B',
    `Cột A${NBSP}${NBSP}${NBSP}Cột B`,
  ])('%s is a run to both', (text) => {
    expect(hasSpaceRun(text)).toBe(true);
    expect(htmlHasSpaceRun(`<p>${text}</p>`)).toBe(true);
  });
});

describe('the wording an author reads', () => {
  it('names the tools that do work and says what is blocked', () => {
    expect(SPACE_RUN_WARNING).toContain('căn lề, thụt lề hoặc bảng');
    expect(SPACE_RUN_WARNING).toContain('điện thoại');
    expect(SPACE_RUN_WARNING).toMatch(/lưu|xem trước/);
  });
});
