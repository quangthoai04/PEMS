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
