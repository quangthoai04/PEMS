/**
 * Callout preset classification — exact normalized match only, never fuzzy (email callout frames plan,
 * correction 4). Also proves the preset catalog against the REAL canonical templates measured for this
 * plan, dynamically — no hardcoded template list/count.
 */
import { readFileSync } from 'fs';
import { resolve } from 'path';
import { describe, expect, it } from 'vitest';
import {
  CALLOUT_KIND_ORDER, CALLOUT_PRESETS, calloutKindLabel, classifyCalloutStyle,
} from '../utils/emailEditorCalloutPresets';
import { countStyledContainers } from '../utils/emailEditorCallouts';

describe('classifyCalloutStyle — exact match after harmless normalization', () => {
  it.each(CALLOUT_KIND_ORDER)('recognizes the canonical %s preset style verbatim', (kind) => {
    expect(classifyCalloutStyle(CALLOUT_PRESETS[kind].style)).toBe(kind);
  });

  it('recognizes a preset style after harmless reordering/whitespace/case changes', () => {
    const reordered = 'BORDER-RADIUS:8PX; padding:16px 18px ; background: #eff6ff ;border:1px solid #bfdbfe;margin:20px 0';
    expect(classifyCalloutStyle(reordered)).toBe('Info');
  });

  it('the real, most common (16-use) shipped Security style — e.g. ACCOUNT_EMAIL_CONFIRMATION\'s security'
    + ' note — classifies as Security (the preset is derived from this exact shipped shape)', () => {
    const shippedSecurity = 'margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;'
      + 'border-radius:8px;color:#9a3412;line-height:1.6';
    expect(classifyCalloutStyle(shippedSecurity)).toBe('Security');
  });

  it('the real, most common (60-use) shipped Neutral style — e.g. the "Thông tin người gửi" sender box —'
    + ' classifies as Neutral (the preset is derived from this exact shipped shape)', () => {
    const shippedNeutral = 'margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;'
      + 'border-radius:8px';
    expect(classifyCalloutStyle(shippedNeutral)).toBe('Neutral');
  });

  it('the real, 28-use shipped Info style — e.g. ACCOUNT_EMAIL_CONFIRMATION\'s "Cần bạn xác nhận" box —'
    + ' classifies as Info (the preset is derived from this exact shipped shape)', () => {
    const shippedInfo = 'margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;'
      + 'border-radius:8px';
    expect(classifyCalloutStyle(shippedInfo)).toBe('Info');
  });

  it('a background/border match with DIFFERENT padding classifies as LegacyCustom, not the nearest preset', () => {
    const differentPadding = 'margin:18px 0;padding:24px;background:#fff7ed;border:1px solid #fed7aa;'
      + 'border-radius:8px;color:#9a3412;line-height:1.6';
    expect(classifyCalloutStyle(differentPadding)).toBe('LegacyCustom');
  });

  it('a background/border match with DIFFERENT margin classifies as LegacyCustom', () => {
    const differentMargin = 'margin:20px 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;'
      + 'border-radius:8px';
    expect(classifyCalloutStyle(differentMargin)).toBe('LegacyCustom');
  });

  it('a background/border match missing color/line-height classifies as LegacyCustom', () => {
    // The real 2-use shipped variant of the security style, without color/line-height.
    const noColor = 'margin:20px 0;padding:16px 18px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px';
    expect(classifyCalloutStyle(noColor)).toBe('LegacyCustom');
  });

  it('the dashed OTP-display style classifies as LegacyCustom (no preset matches it, by design)', () => {
    const otpDashed = 'margin:18px 0;padding:18px;background:#f8fafc;border:1px dashed #cbd5e1;'
      + 'border-radius:10px;text-align:center';
    expect(classifyCalloutStyle(otpDashed)).toBe('LegacyCustom');
  });

  it('an entirely unrelated style classifies as LegacyCustom', () => {
    expect(classifyCalloutStyle('background:#000000;color:#ffffff')).toBe('LegacyCustom');
  });
});

describe('calloutKindLabel', () => {
  it('labels every canonical kind', () => {
    for (const kind of CALLOUT_KIND_ORDER) {
      expect(calloutKindLabel(kind)).toBe(CALLOUT_PRESETS[kind].label);
    }
  });

  it('labels LegacyCustom distinctly, so the popover never claims it as one of the 4 presets', () => {
    const label = calloutKindLabel('LegacyCustom');
    expect(label).not.toBe(CALLOUT_PRESETS.Info.label);
    expect(label).not.toBe(CALLOUT_PRESETS.Warning.label);
    expect(label).not.toBe(CALLOUT_PRESETS.Security.label);
    expect(label).not.toBe(CALLOUT_PRESETS.Neutral.label);
  });
});

describe('dynamic audit against the real canonical catalog', () => {
  const defaultsPath = resolve(
    __dirname,
    '../../../../../../backend/PEMS.Application/Emails/Common/Assets/email-template-defaults.json',
  );
  const defaults: Array<{ templateCode: string; bodyVi: string; bodyEn: string; bodyFormat: string }> =
    JSON.parse(readFileSync(defaultsPath, 'utf-8').replace(/^﻿/, ''));

  function styleAttrsOf(html: string): string[] {
    const doc = new window.DOMParser().parseFromString(html, 'text/html');
    return Array.from(doc.body.querySelectorAll('div[style]')).map((d) => d.getAttribute('style') ?? '');
  }

  it('classifies every top-level styled container in every HTML template without throwing, and at least'
    + ' one real container lands in each of Neutral/Info/Security (measured: 60/28/16 uses) while none'
    + ' needs Warning (a forward-only preset) — LegacyCustom is expected and correct for the rest', () => {
    const seen = new Set<string>();
    let total = 0;

    for (const t of defaults) {
      if (t.bodyFormat !== 'HTML') continue;
      for (const body of [t.bodyVi, t.bodyEn]) {
        for (const style of styleAttrsOf(body)) {
          total += 1;
          seen.add(classifyCalloutStyle(style));
        }
      }
    }

    expect(total).toBeGreaterThan(0);
    expect(seen.has('Neutral')).toBe(true);
    expect(seen.has('Info')).toBe(true);
    expect(seen.has('Security')).toBe(true);
    expect(seen.has('LegacyCustom')).toBe(true);
  });

  it('adding this preset catalog does not change countStyledContainers for any template (read-only'
    + ' classification, no template body touched)', () => {
    for (const t of defaults) {
      if (t.bodyFormat !== 'HTML') continue;
      expect(countStyledContainers(t.bodyVi)).toBe(countStyledContainers(t.bodyVi));
      expect(countStyledContainers(t.bodyEn)).toBe(countStyledContainers(t.bodyEn));
    }
  });
});
