/**
 * Centralized callout/frame presets (email callout frames plan).
 *
 * <b>Where these came from.</b> Measured directly against every body in
 * `email-template-defaults.json` (33 templates × 2 languages): only 5 distinct container styles exist
 * today, clustering into 3 semantic kinds —
 *
 *     60 uses  background:#f8fafc border:#e2e8f0 solid            → Neutral (sender-info boxes)
 *     28 uses  background:#eff6ff border:#bfdbfe solid            → Info (confirmation/action boxes)
 *     16 uses  background:#fff7ed border:#fed7aa solid, colored   → Security (security notes)
 *      4 uses  background:#f8fafc border:#cbd5e1 dashed, centered → an OTP-code display, not a general
 *                                                                    content frame — deliberately left
 *                                                                    unrecognized (see below)
 *      2 uses  background:#fff7ed border:#fed7aa solid, no color  → a minor authoring variant of Security
 *
 * `Warning` has no existing match — it is added purely for forward use by "Thêm khung", using a distinct
 * amber scheme. The 4- and 2-use outliers above are intentionally NOT given their own preset: recognizing
 * every historical one-off would defeat the point of a small, centralized set, and `LegacyCustom` already
 * covers them correctly (preserved exactly, never silently migrated — see `classifyCalloutStyle`).
 *
 * <b>Presets use the REAL measured margin/padding for each kind, not an invented uniform value.</b> "Derived
 * from existing PEMS styles where possible" means exactly that: Neutral keeps `margin:20px 0 0;padding:
 * 14px 16px` (its own 60-use shipped shape), Info keeps `margin:20px 0;padding:16px 18px` (its own 28-use
 * shape), Security keeps `margin:18px 0;padding:14px 16px` (its own 16-use shape) — forcing all three onto
 * one uniform margin/padding would have made every existing Neutral/Info container in the catalog classify
 * as `LegacyCustom` on day one, which is exactly the "existing frame silently orphaned from its own preset"
 * failure this file exists to avoid. Only `Warning`, with no shipped precedent, gets an invented style.
 *
 * <b>Why classification is exact, not fuzzy.</b> A historical container matching Security's background and
 * border colors but with different padding (`padding:24px` instead of `padding:14px 16px`) is a genuinely
 * different container — that padding is what a reader sees — and must classify as `LegacyCustom`, not be
 * nudged into the nearest-looking preset. `canonicalStyle` (reused from `emailHtmlCanonicalizer.ts`, the
 * same normalizer that already decides "did the operator change anything") normalizes declaration order,
 * whitespace, trailing semicolons, and equivalent numeric/case spelling — nothing that changes what is
 * rendered — and the match after that is exact equality, never "close enough".
 */
import { canonicalStyle } from './emailHtmlCanonicalizer';

export type CalloutKind = 'Info' | 'Warning' | 'Security' | 'Neutral' | 'LegacyCustom';

export interface CalloutPreset {
  style: string;
  /** What the "Đổi kiểu khung" popover calls it. */
  label: string;
}

export const CALLOUT_PRESETS: Record<Exclude<CalloutKind, 'LegacyCustom'>, CalloutPreset> = {
  // Real, shipped Info shape (28 uses) — e.g. ACCOUNT_EMAIL_CONFIRMATION's "Cần bạn xác nhận" box.
  Info: {
    style: 'margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px',
    label: 'Thông tin',
  },
  // No shipped precedent — invented for forward use, following the same margin/padding/line-height/color
  // convention as Security (the other "colored text" kind) with a distinct amber scheme.
  Warning: {
    style: 'margin:18px 0;padding:14px 16px;background:#fefce8;border:1px solid #fde047;border-radius:8px;'
      + 'color:#854d0e;line-height:1.6',
    label: 'Cảnh báo',
  },
  // Real, shipped Security shape (16 uses) — e.g. ACCOUNT_EMAIL_CONFIRMATION's "Lưu ý bảo mật" note.
  Security: {
    style: 'margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;'
      + 'color:#9a3412;line-height:1.6',
    label: 'Bảo mật',
  },
  // Real, shipped Neutral shape (60 uses, the most common) — e.g. the "Thông tin người gửi" sender box.
  Neutral: {
    style: 'margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px',
    label: 'Trung tính',
  },
};

/** Display order for the "Đổi kiểu khung" / "Thêm khung" popovers. */
export const CALLOUT_KIND_ORDER: Exclude<CalloutKind, 'LegacyCustom'>[] = [
  'Info', 'Warning', 'Security', 'Neutral',
];

/**
 * Classifies an observed container `style` string against the canonical presets.
 *
 * Exact match only, after harmless CSS normalization (`canonicalStyle`) — never a partial/near match.
 * Anything that does not normalize to byte-identical with a known preset is `LegacyCustom`: preserved
 * exactly as authored, and never silently rewritten just because a similarly-colored preset exists.
 */
export function classifyCalloutStyle(style: string): CalloutKind {
  const normalized = canonicalStyle(style);

  for (const kind of CALLOUT_KIND_ORDER) {
    if (canonicalStyle(CALLOUT_PRESETS[kind].style) === normalized) return kind;
  }
  return 'LegacyCustom';
}

/** What the "Đổi kiểu khung" popover shows for the CURRENT style, including the legacy case. */
export function calloutKindLabel(kind: CalloutKind): string {
  if (kind === 'LegacyCustom') return 'Kiểu tùy chỉnh (cũ)';
  return CALLOUT_PRESETS[kind].label;
}
