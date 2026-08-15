import type { CampusVisitSchema } from '../schema/visitRequestV2.schema';

/**
 * Whether two rows of ONE campus's member lists describe the same person (ID-02) — the client half
 * of the rule the backend enforces in `MemberDuplicatePolicy`.
 *
 * <p>The two implementations must agree. Not because the server trusts this one — it does not, and
 * re-runs the whole check on the merged payload — but because a form that lets the user build
 * something the server will refuse has simply moved the failure to the end of a long page. This runs
 * while the rows are on screen, next to the rows in question, where "which of these two is
 * redundant?" is a question the user can actually answer.</p>
 *
 * <p><b>Why the lists have to be merged.</b> Guests and support staff are two doors into the same
 * table. The Excel importer de-duplicates inside the list it is importing and each array used to be
 * validated on its own, so the same human written into both was stored as two members with two
 * different `guest_member_id`s — after which every id-first rule downstream correctly concluded they
 * were two people, and the biên bản listed them twice with nothing to say otherwise.</p>
 */

export type MemberListKind = 'visitors' | 'supportTeam';

export interface MemberIdentityRow {
  kind: MemberListKind;
  rowIndex: number;
  /** The row's stable identity, when it has one — carried so callers can act on the row by key. */
  clientMemberKey: string | null;
  fullName: string;
  jobTitle: string;
  organization: string;
  organizationPartnerId: number | null;
  nationality: string;
}

export interface MemberDuplicatePair {
  /** Stable across renders (the two keys), so a dismissal or a banner can be keyed on it. */
  id: string;
  first: MemberIdentityRow;
  second: MemberIdentityRow;
  /** True when the pair spans the two lists — the case nothing used to catch at all. */
  crossList: boolean;
}

/**
 * Trim, collapse inner whitespace, lower-case. Vietnamese accents are deliberately LEFT ALONE:
 * folding them would make "Nguyễn Văn An" and "Nguyen Van Ân" the same person, which in a system
 * full of Vietnamese names is a merge waiting to happen. Mirrors `PersonIdentity.Normalize`.
 */
export const normalizePersonField = (value: string | null | undefined): string =>
  (value ?? '').trim().replace(/\s+/g, ' ').toLowerCase();

/**
 * The string two rows must share to be the same person, or `''` for a row that cannot be compared.
 *
 * <p>Name + job title + employer + nationality — every field the form collects about a member. A
 * pair matching on all four has nothing left that could distinguish them; a pair differing anywhere
 * is two people and is never reported. That is what makes "add the distinguishing detail" always an
 * available answer, and what makes refusing the identical case safe.</p>
 *
 * <p>The partner ID beats the organisation TEXT when both rows carry one, so "FPT University (FPTU)"
 * and "FPT University" stop being two employers the moment both point at the same profile. Name
 * alone is never enough on its own — two members of one delegation sharing a name is ordinary.</p>
 *
 * <p>An unnamed row returns `''` and matches nothing, rather than matching every other blank row:
 * half-typed rows are normal while a form is open.</p>
 */
export const memberFingerprint = (m: {
  fullName?: string | null;
  jobTitle?: string | null;
  organization?: string | null;
  organizationPartnerId?: number | null;
  nationality?: string | null;
}): string => {
  const name = normalizePersonField(m.fullName);
  if (!name) return '';
  const org = m.organizationPartnerId != null
    ? `partner:${m.organizationPartnerId}`
    : `text:${normalizePersonField(m.organization)}`;
  return [name, normalizePersonField(m.jobTitle), org, normalizePersonField(m.nationality)].join('|');
};

/** One campus's members, visitors then support, in the order they are rendered and submitted. */
export const campusMemberRows = (cv: Pick<CampusVisitSchema, 'visitors' | 'supportTeam'>): MemberIdentityRow[] => {
  const rowsOf = (rows: CampusVisitSchema['visitors'] | undefined, kind: MemberListKind): MemberIdentityRow[] =>
    (rows ?? []).map((m, rowIndex) => ({
      kind,
      rowIndex,
      clientMemberKey: m?.clientMemberKey ?? null,
      fullName: (m?.fullName ?? '').trim(),
      jobTitle: (m?.jobTitle ?? '').trim(),
      organization: (m?.organization ?? '').trim(),
      organizationPartnerId: m?.organizationPartnerId ?? null,
      nationality: (m?.nationality ?? '').trim(),
    }));
  return [...rowsOf(cv.visitors, 'visitors'), ...rowsOf(cv.supportTeam, 'supportTeam')];
};

/**
 * Every pair in the MERGED list that describes the same person.
 *
 * <p>Only the first partner is reported per fingerprint, so somebody entered three times is one
 * conflict to resolve rather than three.</p>
 */
export const findMemberDuplicates = (rows: MemberIdentityRow[]): MemberDuplicatePair[] => {
  const firstByFingerprint = new Map<string, MemberIdentityRow>();
  const reported = new Set<string>();
  const pairs: MemberDuplicatePair[] = [];

  for (const row of rows) {
    const fingerprint = memberFingerprint(row);
    if (!fingerprint) continue;
    const first = firstByFingerprint.get(fingerprint);
    if (!first) {
      firstByFingerprint.set(fingerprint, row);
      continue;
    }
    if (reported.has(fingerprint)) continue;
    reported.add(fingerprint);
    pairs.push({
      id: `${first.clientMemberKey ?? `${first.kind}-${first.rowIndex}`}::`
        + `${row.clientMemberKey ?? `${row.kind}-${row.rowIndex}`}`,
      first,
      second: row,
      crossList: first.kind !== row.kind,
    });
  }

  return pairs;
};

/** Convenience: the duplicates of one campus card, straight from its form values. */
export const findCampusMemberDuplicates = (
  cv: Pick<CampusVisitSchema, 'visitors' | 'supportTeam'>,
): MemberDuplicatePair[] => findMemberDuplicates(campusMemberRows(cv));

/**
 * The delegation members whose identity matches a typed operational-contact snapshot (ID-01).
 *
 * <p>A contact who chose "— Không nằm trong danh sách đoàn —" and then typed somebody who IS in the
 * list has no `clientMemberKey`, so the request stores them as a separate person and the biên bản
 * gets both. Matching is on name + job title + organisation together — the three fields the contact
 * snapshot and a member row have in common — never on the name alone.</p>
 *
 * <p>Returns EVERY match, not a decision. One match is worth asking about; several mean the evidence
 * does not name anybody, and the caller must not pick the first.</p>
 */
export const findContactMemberCandidates = (
  contact: { fullName?: string | null; jobTitle?: string | null; organization?: string | null } | null | undefined,
  rows: MemberIdentityRow[],
): MemberIdentityRow[] => {
  const name = normalizePersonField(contact?.fullName);
  if (!name) return [];
  const jobTitle = normalizePersonField(contact?.jobTitle);
  const organization = normalizePersonField(contact?.organization);
  return rows.filter(row =>
    !!row.clientMemberKey
    && normalizePersonField(row.fullName) === name
    && normalizePersonField(row.jobTitle) === jobTitle
    && normalizePersonField(row.organization) === organization);
};
