/**
 * Byte-for-byte mirror of backend `PersonIdentity.Key`/`Normalize` (PEMS.Application.Delegations.Common) —
 * a client-side pre-check ONLY. The backend re-validates on every save and is the sole authority; this
 * exists purely to give the user an inline error before a round trip (plan CanhIter3FixBug).
 *
 * Deliberately no accent stripping: "Nguyễn Văn An" and "Nguyen Van Ân" must NOT collapse to the same
 * key, or a merge the backend does not intend would appear to succeed here.
 */

/** Trimmed, lower-cased, inner whitespace collapsed. Empty for null/blank — mirrors PersonIdentity.Normalize. */
export function normalizePersonIdentityPart(value: string | null | undefined): string {
  if (!value) return '';
  const trimmed = value.trim();
  if (trimmed.length === 0) return '';
  return trimmed.toLowerCase().replace(/\s+/g, ' ');
}

/**
 * The identity fingerprint two records must share to count as the same person (rule 3 of
 * PersonIdentity's doc) — full name, role and organization, normalized. Empty when there is no name to
 * match on, mirroring the backend's "no opinion" answer for an unnamed row.
 */
export function personIdentityKey(
  fullName: string | null | undefined,
  role: string | null | undefined,
  organization: string | null | undefined,
): string {
  const name = normalizePersonIdentityPart(fullName);
  if (name.length === 0) return '';
  return `${name}|${normalizePersonIdentityPart(role)}|${normalizePersonIdentityPart(organization)}`;
}
