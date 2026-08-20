import { z } from 'zod';

/**
 * Email identity comparison — the frontend half of the rule the backend enforces in
 * `RegistrantIdentityRules` (PEMS.Application/Delegations/Commands/CreateVisitRequestV2).
 *
 * Both sides normalise with trim + lower-case and NOTHING else. Gmail dot-folding, `+alias`
 * stripping and domain rewriting are deliberately not applied: `a.b@gmail.com` and `ab@gmail.com`
 * are different mailboxes for identity purposes, and folding them would let one account act
 * under an address it has never proven it controls.
 *
 * The frontend answer only decides which flow to offer (direct submit vs OTP) and what to show.
 * The backend re-derives it and is the decision that counts.
 */

/** trim + lower-case. Mirrors `RegistrantIdentityRules.Normalize`. */
export const normalizeEmail = (email: string | null | undefined): string =>
  (email ?? '').trim().toLowerCase();

/**
 * Canonical SYNTAX check (Patch 5) — the one place a raw `<input>` (Partner Contact, Business Card
 * OCR confirm, and any future non-zod form) should ask "is this shaped like an email" before
 * submit. Not a duplicate regex: reuses zod's own `.email()` engine, the same one
 * `visitRequestV2.schema.ts`'s `buildEmailSchema` already relies on, so a malformed value can never
 * pass one screen's check and fail another's just because two screens hand-rolled different
 * patterns (the pre-Patch-5 state: `ContactIdentityActions.tsx` and `PartnerDetail.tsx` each
 * carried their own byte-identical-but-independent `/^[^\s@]+@[^\s@]+\.[^\s@]+$/`).
 *
 * UX-only, like every frontend check here — the backend's FluentValidation `.EmailAddress()` is
 * the authority and is re-checked on every write path regardless of what this returns.
 */
export const isValidEmailSyntax = (email: string | null | undefined): boolean =>
  z.string().trim().min(1).email().safeParse(email ?? '').success;

/**
 * True only when both addresses are present AND equal after normalisation.
 * Two blanks are NOT the same identity — a signed-in user with no email on record must never
 * match an empty form field and slip into the "no OTP needed" path.
 */
export const isSameEmailIdentity = (
  left: string | null | undefined,
  right: string | null | undefined,
): boolean => {
  const a = normalizeEmail(left);
  return a.length > 0 && a === normalizeEmail(right);
};
