/**
 * The system action node survives a REAL Quill round trip (V4 §9.3, §9.4).
 *
 * These deliberately do NOT mock `react-quill-new`. Every other editor test in this project does, which is
 * exactly how the defect below stayed invisible: the node was dropped by the real editor while every
 * mocked test agreed it was fine.
 *
 * Measured before the blot existed, on quill 2.0.3 — the version `react-quill-new` bundles:
 *
 *     in   <p>hello</p><div data-system-block="action"></div><p>bye</p>
 *     out  <p>hello</p><p>bye</p>
 *
 * The position was destroyed by opening the editor, and the send then appended the block at the end.
 */
import { beforeAll, describe, expect, it } from 'vitest';
import {
  ACTION_BLOT_CLASS,
  fromEditorHtml,
  registerSystemActionBlot,
  toEditorHtml,
} from '../utils/emailEditorSystemNodes';
import { SYSTEM_ACTION_NODE, countSystemActionNodes, hasSystemActionNode } from '../utils/systemActionNode';

/* eslint-disable @typescript-eslint/no-explicit-any */
let Quill: any;

/** A real editor instance, parsing `html` the way ReactQuill parses a `value`. */
function roundTrip(html: string): string {
  const host = document.createElement('div');
  document.body.appendChild(host);
  const q = new Quill(host);
  q.clipboard.dangerouslyPasteHTML(html);
  return q.root.innerHTML;
}

let didRegister = false;

beforeAll(async () => {
  Quill = (await import('react-quill-new')).Quill;
  didRegister = registerSystemActionBlot();
});

describe('registration', () => {
  /**
   * `registerSystemActionBlot` reports rather than throws, because it runs at module scope and several
   * other test files mock `react-quill-new` with a stub carrying no `Quill`. That tolerance must not be
   * allowed to hide a genuine failure here, where the real editor IS present.
   */
  it('actually registers against the real editor', () => {
    expect(didRegister).toBe(true);
    expect(Quill.import('formats/pemsSystemActionBlock')).toBeTruthy();
  });
});

describe('the editor loads the quill the app actually uses', () => {
  it('is quill 2.x, not the top-level 1.3.7 copy', () => {
    // If this ever reads 1.x, the blot is being registered against the wrong module instance and the
    // registration silently does nothing — which compiles, runs, and breaks the feature.
    expect(String(Quill.version)).toMatch(/^2\./);
  });
});

describe('round trip through a real editor', () => {
  it('keeps the node, in position, instead of dropping it', () => {
    const out = roundTrip(toEditorHtml(`<p>INTRO</p>${SYSTEM_ACTION_NODE}<p>SIGNATURE</p>`));

    expect(hasSystemActionNode(out)).toBe(true);
    expect(out.indexOf('INTRO')).toBeLessThan(out.indexOf('data-system-block'));
    expect(out.indexOf('data-system-block')).toBeLessThan(out.indexOf('SIGNATURE'));
  });

  it('an unprepared node is still dropped — which is why toEditorHtml exists', () => {
    // Pinning the behaviour that made this necessary: Parchment matches on tag AND class, so the raw
    // backend node (no class) is not recognised. If Quill ever starts preserving it, this test fails and
    // tells us toEditorHtml can go.
    expect(hasSystemActionNode(roundTrip(`<p>a</p>${SYSTEM_ACTION_NODE}<p>b</p>`))).toBe(false);
  });

  it('normalises back to the canonical node the backend expects', () => {
    const out = fromEditorHtml(roundTrip(toEditorHtml(SYSTEM_ACTION_NODE)));

    expect(countSystemActionNodes(out)).toBe(1);
    expect(out).toContain(SYSTEM_ACTION_NODE);
    // The editor-only affordances must not reach the backend, still less a recipient.
    expect(out).not.toContain(ACTION_BLOT_CLASS);
    expect(out).not.toContain('contenteditable');
    expect(out).not.toContain('Khối nút phản hồi');
  });

  /**
   * The property that makes a CONTROLLED editor safe.
   *
   * `EmailPreviewModal` keeps the canonical body in React state, so every keystroke goes
   * quill → fromEditorHtml → state → toEditorHtml → quill. If that last conversion did not reproduce
   * Quill's own serialisation byte for byte, ReactQuill would see the value as changed on every render
   * and reload the document — moving the caret to the start of the message as the sender types.
   *
   * It holds today because `toEditorHtml` emits the attributes in the order Quill emits them. That is a
   * real coupling, so it is pinned here rather than left as a coincidence.
   */
  it('reproduces quill\'s own serialisation exactly, so a controlled editor cannot loop', () => {
    const quillOut = roundTrip(toEditorHtml(`<p>INTRO</p>${SYSTEM_ACTION_NODE}<p>SIGNATURE</p>`));

    expect(toEditorHtml(fromEditorHtml(quillOut))).toBe(quillOut);
  });

  it('is stable across repeated open-and-close cycles', () => {
    let body = `<p>INTRO</p>${SYSTEM_ACTION_NODE}<p>SIGNATURE</p>`;

    for (let i = 0; i < 3; i += 1) body = fromEditorHtml(roundTrip(toEditorHtml(body)));

    // Neither lost nor multiplied: an editor that duplicated it would mint one token into two buttons.
    expect(countSystemActionNodes(body)).toBe(1);
    expect(body.indexOf('INTRO')).toBeLessThan(body.indexOf('data-system-block'));
    expect(body.indexOf('data-system-block')).toBeLessThan(body.indexOf('SIGNATURE'));
  });

  it('carries the sender\'s surrounding edits through unharmed', () => {
    const out = fromEditorHtml(roundTrip(toEditorHtml(
      `<p>Kính gửi anh Nam,</p>${SYSTEM_ACTION_NODE}<p><strong>Trân trọng</strong></p>`,
    )));

    expect(out).toContain('Kính gửi anh Nam,');
    expect(out).toContain('<strong>');
    expect(hasSystemActionNode(out)).toBe(true);
  });
});

describe('what the sender may and may not do to it', () => {
  it('is marked non-editable, so it can be moved but not typed into', () => {
    const out = roundTrip(toEditorHtml(SYSTEM_ACTION_NODE));

    expect(out).toContain('contenteditable="false"');
  });

  it('renders as one indivisible object rather than editable text', () => {
    const host = document.createElement('div');
    document.body.appendChild(host);
    const q = new Quill(host);
    q.clipboard.dangerouslyPasteHTML(toEditorHtml(SYSTEM_ACTION_NODE));

    // A block embed occupies exactly one position in the document, so the sender cannot split it or
    // place a cursor inside — they select the whole thing or nothing.
    const embeds = q.root.querySelectorAll(`.${ACTION_BLOT_CLASS}`);
    expect(embeds).toHaveLength(1);
    expect(embeds[0].getAttribute('data-system-block')).toBe('action');
  });

  it('deleting it is possible, and reports as zero nodes rather than as corruption', () => {
    // §9.5 treats "none" as a condition to report, not a crash. The backend decides what to do about it.
    expect(countSystemActionNodes(fromEditorHtml(roundTrip('<p>chỉ có chữ</p>')))).toBe(0);
  });
});

describe('toEditorHtml / fromEditorHtml as pure functions', () => {
  it('leaves a body with no node untouched in both directions', () => {
    const plain = '<p>không có khối hành động</p>';
    expect(toEditorHtml(plain)).toBe(plain);
    expect(fromEditorHtml(plain)).toBe(plain);
  });

  it('handles empty and nullish input', () => {
    expect(toEditorHtml('')).toBe('');
    expect(toEditorHtml(null)).toBe('');
    expect(fromEditorHtml(undefined)).toBe('');
  });

  it('is idempotent, so a double conversion cannot duplicate the node', () => {
    expect(toEditorHtml(toEditorHtml(SYSTEM_ACTION_NODE))).toBe(toEditorHtml(SYSTEM_ACTION_NODE));
    expect(countSystemActionNodes(toEditorHtml(toEditorHtml(SYSTEM_ACTION_NODE)))).toBe(1);
    expect(fromEditorHtml(fromEditorHtml(SYSTEM_ACTION_NODE))).toBe(SYSTEM_ACTION_NODE);
  });

  /**
   * The editor's label must never be treated as message content. If the frontend ever failed to
   * normalise, the pattern still matches the node WITH its label, so the backend substitutes the real
   * block over the whole thing rather than appending a block and delivering the label as prose.
   */
  it('recognises the editor form, label and all, so the label can never be delivered', () => {
    const editorForm = toEditorHtml(SYSTEM_ACTION_NODE);

    expect(editorForm).toContain('Khối nút phản hồi');
    expect(hasSystemActionNode(editorForm)).toBe(true);
    expect(countSystemActionNodes(editorForm)).toBe(1);
    expect(fromEditorHtml(editorForm)).toBe(SYSTEM_ACTION_NODE);
  });
});
