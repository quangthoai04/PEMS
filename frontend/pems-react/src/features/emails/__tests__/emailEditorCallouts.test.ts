/**
 * Styled callout/panel containers survive a REAL Quill round trip (Phase A of the email fidelity plan).
 *
 * Deliberately does NOT mock `react-quill-new`, for the same reason `emailEditorSystemNodes.test.ts`
 * doesn't: every other editor test in this project mocks it, which is exactly how a real editor dropping
 * unformatted structural HTML stayed invisible. See `emailEditorCallouts.ts`'s own doc comment for the
 * Phase A.0 spike findings this implementation is built from (a live Parchment Container was tried first
 * and rejected; this is the atomic-embed shape that passed).
 *
 * Audit boundary (Final Correction 2 of the plan): this file iterates the CANONICAL defaults catalog
 * (`email-template-defaults.json`) directly and dynamically — never a hardcoded template count/list, and
 * never by reading backend `.cs` source. The backend owns proving its registry matches that same JSON
 * (see `EmailTemplateDefaultsParityTests.cs`/`CanonicalSeedStructureTests.cs`).
 */
import { readFileSync } from 'fs';
import { resolve } from 'path';
import { beforeAll, describe, expect, it } from 'vitest';
import { registerSystemActionBlot, toEditorHtml, fromEditorHtml } from '../utils/emailEditorSystemNodes';
import { registerTemplateBlockBlot } from '../utils/emailEditorTemplateBlocks';
import { registerEmailTableBlot } from '../utils/emailEditorTable';
import { registerEmailEditorFormats } from '../utils/emailEditorFormats';
import {
  CALLOUT_WRAPPER_CLASS, calloutsToNodes, countStyledContainers, nodesToCallouts, registerEmailCalloutBlot,
} from '../utils/emailEditorCallouts';
import { editorHtmlToStored, storedToEditorHtml } from '../utils/emailEditorConversion';
/**
 * The ordered list of top-level styled-div `style` values in `html` — used to assert callout fidelity
 * SPECIFICALLY, rather than whole-document semantic equality.
 *
 * Whole-document comparison (`isSameEmailHtml`) is the wrong tool here: this editor's `color`/`size`
 * attributors are registered inline-only (see `emailEditorFormats.ts`), so an ORDINARY `<p style="margin:
 * ...;color:...">` outside any callout already loses `margin`/`line-height` on any round trip through this
 * editor, callout or not — a pre-existing, already-accepted limitation with no `margin`/`line-height`
 * attributor registered anywhere for plain paragraphs, entirely out of Phase A's scope (styled CONTAINERS,
 * not every inline style on every paragraph). Scoping the fidelity assertion to just the callouts'
 * `style` attributes is what actually tests what Phase A promises.
 */
function calloutStyles(html: string): string[] {
  if (typeof window === 'undefined' || !window.DOMParser) return [];
  const doc = new window.DOMParser().parseFromString(html, 'text/html');
  const candidates = Array.from(doc.body.querySelectorAll('div[style]'));
  return candidates
    .filter((div) => {
      const ancestorStyled = div.parentElement?.closest('div[style]');
      return !(ancestorStyled && candidates.includes(ancestorStyled as HTMLDivElement));
    })
    .map((div) => div.getAttribute('style') ?? '');
}

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

const noLabel = () => undefined;

/**
 * The full toEditor/fromEditor pipeline for a TEMPLATE body — the SAME shared functions
 * `EmailRichTextEditor.tsx` itself calls (email callout frames plan, correction 3: one pipeline, not a
 * test-local copy of it), with `preserveCallouts: true` matching both real editing screens.
 */
function toEditorPipeline(stored: string): string {
  return storedToEditorHtml(stored, { isTemplate: true, labelOf: noLabel, preserveCallouts: true });
}
function fromEditorPipeline(editorHtml: string): string {
  // `onlyTrailingAfterObject: true` matches EmailRichTextEditor.tsx's own `fromEditor` exactly — the same
  // options, not merely the same functions, is what "one pipeline" actually proves.
  return editorHtmlToStored(editorHtml, {
    isTemplate: true, preserveCallouts: true, onlyTrailingAfterObject: true,
  });
}

beforeAll(async () => {
  Quill = (await import('react-quill-new')).Quill;
  registerEmailEditorFormats();
  registerSystemActionBlot();
  registerTemplateBlockBlot();
  registerEmailTableBlot();
  registerEmailCalloutBlot();
});

describe('registration', () => {
  it('actually registers against the real editor', () => {
    expect(Quill.import('formats/pemsEmailCallout')).toBeTruthy();
  });
});

// Real canonical ACCOUNT_EMAIL_CONFIRMATION bodies (VI/EN), copied verbatim from
// backend/PEMS.Application/Emails/Common/Assets/email-template-defaults.json — not toy HTML. Both contain:
// a table, a blue "confirmation required" callout wrapping {{actionBlock}}, an expiry sentence, and an
// orange "security note" callout.
const ACCOUNT_EMAIL_CONFIRMATION_VI = '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Một tài khoản PEMS đã được khởi tạo cho bạn. Tài khoản đang ở trạng thái chờ xác nhận email và chưa đăng nhập được.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Vai trò</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{roleName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{campusName}}</td></tr></tbody></table><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Cần bạn xác nhận</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Bấm nút bên dưới để xác nhận địa chỉ email này. Sau khi xác nhận, tài khoản sẽ chuyển sang trạng thái hoạt động.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Liên kết xác nhận có hiệu lực trong <strong>{{expiresInHours}}</strong> giờ và chỉ dùng được một lần.</p><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Lưu ý bảo mật:</strong> Không chia sẻ liên kết này với bất kỳ ai. Nếu bạn không mong đợi email này, vui lòng bỏ qua và không bấm vào liên kết.</div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>';

const ACCOUNT_EMAIL_CONFIRMATION_EN = '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">A PEMS account has been created for you. It is pending email confirmation and cannot be signed in to yet.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Role</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{roleName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{campusName}}</td></tr></tbody></table><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Confirmation required</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Use the button below to confirm this email address. The account becomes active once it is confirmed.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The confirmation link is valid for <strong>{{expiresInHours}}</strong> hours and can be used once.</p><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Security note:</strong> Do not share this link with anyone. If you were not expecting this email, please ignore it and do not open the link.</div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>';

describe.each([
  ['VI', ACCOUNT_EMAIL_CONFIRMATION_VI],
  ['EN', ACCOUNT_EMAIL_CONFIRMATION_EN],
])('ACCOUNT_EMAIL_CONFIRMATION (%s) — real canonical template', (_lang, stored) => {
  it('has exactly two styled callouts to preserve (blue + orange)', () => {
    expect(countStyledContainers(stored)).toBe(2);
  });

  it('no-edit round trip: both callouts keep their exact style, {{actionBlock}} survives', () => {
    const editorHtml = roundTrip(toEditorPipeline(stored));
    const saved = fromEditorPipeline(editorHtml);

    // Callout fidelity — exact, order-preserving — is what Phase A promises.
    expect(calloutStyles(saved)).toEqual(calloutStyles(stored));
    expect(saved).toContain('{{actionBlock}}');
    expect((saved.match(/\{\{actionBlock\}\}/g) ?? []).length).toBe(1);

    // The table (a pre-existing, separately-solved fidelity problem) is unaffected by callout wrapping.
    expect(saved).toContain('border-collapse:collapse');
    expect(saved).toContain('{{roleName}}');
  });

  it('editing the very first character does not disturb either callout or the action-block placeholder', () => {
    const loaded = toEditorPipeline(stored);
    const host = document.createElement('div');
    document.body.appendChild(host);
    const q = new Quill(host);
    q.clipboard.dangerouslyPasteHTML(loaded);
    q.history?.cutoff?.();

    q.insertText(0, 'X', 'user');
    expect(q.root.innerHTML).toContain('X');

    const saved = fromEditorPipeline(q.root.innerHTML);
    expect(saved).toContain('{{actionBlock}}');
    expect(saved).toContain('background:#eff6ff');
    expect(saved).toContain('background:#fff7ed');
    expect(saved).toMatch(/Lưu ý bảo mật|Security note/);
  });

  it('undo/redo of an unrelated edit leaves both callouts intact', () => {
    const loaded = toEditorPipeline(stored);
    const host = document.createElement('div');
    document.body.appendChild(host);
    const q = new Quill(host);
    q.clipboard.dangerouslyPasteHTML(loaded);
    q.history?.cutoff?.();

    q.insertText(0, 'X', 'user');
    if (typeof q.history?.undo === 'function') {
      q.history.undo();
      expect(q.root.innerHTML).toContain(CALLOUT_WRAPPER_CLASS);
      expect((q.root.innerHTML.match(new RegExp(CALLOUT_WRAPPER_CLASS, 'g')) ?? []).length).toBe(2);
      q.history.redo();
      expect(q.root.innerHTML).toContain('X');
    }
  });

  it('restore-default round trip (save immediately after load, no edit) is stable', () => {
    const editorHtml = roundTrip(toEditorPipeline(stored));
    const saved = fromEditorPipeline(editorHtml);
    const savedAgain = fromEditorPipeline(roundTrip(toEditorPipeline(saved)));
    expect(calloutStyles(savedAgain)).toEqual(calloutStyles(stored));
    expect((savedAgain.match(/\{\{actionBlock\}\}/g) ?? []).length).toBe(1);
  });
});

describe('multiple callouts and non-template (compose) content', () => {
  it('two callouts in one document round-trip without merging or duplicating', () => {
    const blue = '<div style="background:#eff6ff;padding:16px"><p>blue</p></div>';
    const orange = '<div style="background:#fff7ed;padding:14px"><p>orange</p></div>';
    const loaded = calloutsToNodes(`${blue}<p>middle</p>${orange}`);

    const out = roundTrip(loaded);
    expect((out.match(new RegExp(CALLOUT_WRAPPER_CLASS, 'g')) ?? []).length).toBe(2);

    const saved = nodesToCallouts(out);
    expect((saved.match(/background:#eff6ff/g) ?? []).length).toBe(1);
    expect((saved.match(/background:#fff7ed/g) ?? []).length).toBe(1);
    expect(saved.indexOf('blue')).toBeLessThan(saved.indexOf('middle'));
    expect(saved.indexOf('middle')).toBeLessThan(saved.indexOf('orange'));
  });

  it('a resolved system-action node (COMPOSE spelling) nested in a callout survives', () => {
    const composeBody = '<div style="background:#eff6ff;padding:16px"><p>Bấm nút.</p>'
      + '<div data-system-block="action"></div></div>';

    const loaded = calloutsToNodes(toEditorHtml(composeBody));
    const out = roundTrip(loaded);
    expect(out).toContain('data-system-block="action"');

    const saved = fromEditorHtml(nodesToCallouts(out));
    expect(saved).toContain('<div data-system-block="action"></div>');
    expect(saved).not.toContain('contenteditable');
    expect(saved).not.toContain(CALLOUT_WRAPPER_CLASS);
  });

  it('a variable chip nested in a callout survives (via the shared string-level converters)', () => {
    // variablesToChips/chipsToVariables run alongside calloutsToNodes/nodesToCallouts at the same
    // boundary in EmailRichTextEditor.tsx; simulated here without importing the component.
    const styled = '<div style="background:#fff7ed;padding:14px">'
      + '<span class="pems-variable-chip" data-variable="fullName" data-label="Họ tên">Họ tên</span></div>';

    const out = roundTrip(calloutsToNodes(styled));
    expect(out).toContain('data-variable="fullName"');

    const saved = nodesToCallouts(out);
    expect(saved).toContain('data-variable="fullName"');
  });

  it('no editor-only attribute leaks into the saved HTML', () => {
    const styled = '<div style="background:#fff7ed;padding:14px"><p>text</p></div>';
    const out = roundTrip(calloutsToNodes(styled));
    const saved = nodesToCallouts(out);

    expect(saved).not.toContain('data-pems-callout-style');
    expect(saved).not.toContain(CALLOUT_WRAPPER_CLASS);
    expect(saved).not.toContain('contenteditable');
  });
});

describe('dynamic audit of every canonical template with a styled container', () => {
  const defaultsPath = resolve(
    __dirname,
    '../../../../../../backend/PEMS.Application/Emails/Common/Assets/email-template-defaults.json',
  );
  const defaults: Array<{ templateCode: string; bodyVi: string; bodyEn: string; bodyFormat: string }> =
    JSON.parse(readFileSync(defaultsPath, 'utf-8').replace(/^﻿/, ''));

  // No hardcoded count: whatever the catalog currently contains, filtered dynamically to HTML templates
  // carrying at least one styled container.
  const withStyledContainers = defaults.filter(
    (t) => t.bodyFormat === 'HTML'
      && (countStyledContainers(t.bodyVi) > 0 || countStyledContainers(t.bodyEn) > 0),
  );

  it('found at least one HTML template with a styled container (sanity check the audit ran)', () => {
    expect(withStyledContainers.length).toBeGreaterThan(0);
  });

  it.each(withStyledContainers.map((t) => [t.templateCode, t] as const))(
    '%s — every styled container round-trips (no-edit) with exact style fidelity, both languages',
    (_code, template) => {
      for (const body of [template.bodyVi, template.bodyEn]) {
        if (countStyledContainers(body) === 0) continue;

        const editorHtml = roundTrip(toEditorPipeline(body));
        const saved = fromEditorPipeline(editorHtml);

        // Exact, order-preserving style fidelity for the containers themselves — see calloutStyles' doc
        // comment for why whole-document comparison is the wrong tool (ordinary paragraph margin/
        // line-height loss is a pre-existing, out-of-scope editor limitation, not a callout defect).
        expect(calloutStyles(saved)).toEqual(calloutStyles(body));
        expect(countStyledContainers(saved)).toBe(countStyledContainers(body));
      }
    },
  );
});
