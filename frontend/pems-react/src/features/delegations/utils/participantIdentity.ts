/**
 * Identity matching for the biên bản participant list ("Đồng bộ người mới").
 *
 * A participant reaches the biên bản from two independent lists: internal people carry a `userId`,
 * members of the delegation carry a `guestMemberId`. The same person can be on both — invited as
 * support AND listed among the delegation's members — and then neither id matches the other, so
 * checking ids alone let them be appended twice.
 *
 * Matching therefore falls back to a fingerprint of name + role + organisation, and only ACROSS the
 * two sources: a guest is dropped when an internal row already describes that person, because a
 * `userId` is the stronger identity. Two guests are never merged with each other, and a shared name
 * alone is never a match — same name at a different organisation, or in a different role, is a
 * different person. The backend applies the same rule (`MinuteAutoFill`), so this is a duplicate the
 * user never sees rather than the only thing preventing it.
 */

export interface ParticipantIdentityFields {
  userId: number | null;
  guestMemberId: number | null;
  fullNameSnapshot?: string | null;
  roleSnapshot?: string | null;
  organizationSnapshot?: string | null;
}

/** Trim, lower-case, collapse whitespace. Accents are kept — they distinguish real names. */
function normalize(value?: string | null): string {
  return (value ?? '').trim().toLowerCase().replace(/\s+/g, ' ');
}

/**
 * The fingerprint two rows must share to be the same person. Empty when there is no name to match
 * on, which means "never merge this row".
 */
export function participantIdentityKey(row: ParticipantIdentityFields): string {
  const name = normalize(row.fullNameSnapshot);
  if (!name) return '';
  return `${name}|${normalize(row.roleSnapshot)}|${normalize(row.organizationSnapshot)}`;
}

/**
 * Picks the sync candidates that are genuinely new for a draft: not already present by id, and — for
 * a guest — not already present as an internal person. Candidates are considered in order, so an
 * internal candidate accepted in this batch also blocks a guest duplicate later in the same batch.
 */
export function selectNewSyncCandidates<T extends ParticipantIdentityFields>(
  draft: readonly ParticipantIdentityFields[],
  candidates: readonly T[],
): T[] {
  const haveUserIds = new Set<number>();
  const haveGuestIds = new Set<number>();
  const internalIdentities = new Set<string>();

  const remember = (row: ParticipantIdentityFields) => {
    if (row.userId != null) {
      haveUserIds.add(row.userId);
      const key = participantIdentityKey(row);
      if (key) internalIdentities.add(key);
    } else if (row.guestMemberId != null) {
      haveGuestIds.add(row.guestMemberId);
    }
  };
  draft.forEach(remember);

  const fresh: T[] = [];
  for (const candidate of candidates) {
    if (candidate.userId != null) {
      if (haveUserIds.has(candidate.userId)) continue;
    } else if (candidate.guestMemberId != null) {
      if (haveGuestIds.has(candidate.guestMemberId)) continue;
      const key = participantIdentityKey(candidate);
      if (key && internalIdentities.has(key)) continue; // already here as an internal person
    } else {
      continue; // a candidate with neither id is not something sync produces
    }
    fresh.push(candidate);
    remember(candidate);
  }
  return fresh;
}
