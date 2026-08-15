import { describe, expect, it } from 'vitest';
import {
  campusMemberRows,
  findCampusMemberDuplicates,
  findContactMemberCandidates,
  findMemberDuplicates,
  memberFingerprint,
  type MemberIdentityRow,
} from '../utils/memberDuplicates';

/**
 * ID-01 / ID-02. The delegation list and the support list are two doors into `visit_guest_members`.
 * Nothing compared them: the Excel importer de-duplicates inside the list it is importing and each
 * array was validated on its own, so the same human written into both was stored as two members with
 * two different `guest_member_id`s — after which every id-first rule downstream correctly read that
 * as two people, and the biên bản listed them twice with nothing able to say otherwise.
 *
 * These pin the rule the form and the backend (`MemberDuplicatePolicy`) must agree on.
 */

const member = (over: Partial<MemberIdentityRow> = {}): MemberIdentityRow => ({
  kind: 'visitors',
  rowIndex: 0,
  clientMemberKey: 'k1',
  fullName: 'Nguyễn Văn An',
  jobTitle: 'Giám đốc',
  organization: 'ABC University',
  organizationPartnerId: null,
  nationality: 'Việt Nam',
  ...over,
});

describe('memberFingerprint', () => {
  it('ignores letter case and stray whitespace', () => {
    expect(memberFingerprint(member({ fullName: '  nguyễn   VĂN an ' })))
      .toBe(memberFingerprint(member()));
  });

  it('keeps Vietnamese accents — folding them would merge two real people', () => {
    expect(memberFingerprint(member({ fullName: 'Nguyen Van An' })))
      .not.toBe(memberFingerprint(member()));
  });

  it('is empty without a name, so half-typed rows never match each other', () => {
    expect(memberFingerprint(member({ fullName: '   ' }))).toBe('');
  });

  it('lets the partner id settle two spellings of one employer', () => {
    expect(memberFingerprint(member({ organization: 'ABC University (ABC)', organizationPartnerId: 7 })))
      .toBe(memberFingerprint(member({ organization: 'ABC University', organizationPartnerId: 7 })));
  });

  it('keeps two different partner profiles apart even under the same name', () => {
    expect(memberFingerprint(member({ organizationPartnerId: 7 })))
      .not.toBe(memberFingerprint(member({ organizationPartnerId: 8 })));
  });
});

describe('findMemberDuplicates', () => {
  it('catches the pair that spans the two lists', () => {
    const pairs = findMemberDuplicates([
      member({ kind: 'visitors', rowIndex: 0, clientMemberKey: 'a' }),
      member({ kind: 'supportTeam', rowIndex: 0, clientMemberKey: 'b' }),
    ]);
    expect(pairs).toHaveLength(1);
    expect(pairs[0].crossList).toBe(true);
    expect(pairs[0].first.kind).toBe('visitors');
    expect(pairs[0].second.kind).toBe('supportTeam');
  });

  it('catches a repeat inside one list too', () => {
    const pairs = findMemberDuplicates([
      member({ rowIndex: 0, clientMemberKey: 'a' }),
      member({ rowIndex: 1, clientMemberKey: 'b' }),
    ]);
    expect(pairs).toHaveLength(1);
    expect(pairs[0].crossList).toBe(false);
  });

  it('never concludes from a shared name alone', () => {
    // Same delegation, same name, different job — two members of one family is ordinary.
    expect(findMemberDuplicates([
      member({ clientMemberKey: 'a' }),
      member({ clientMemberKey: 'b', jobTitle: 'Trợ lý' }),
    ])).toEqual([]);
  });

  it('treats a different nationality as a different person', () => {
    expect(findMemberDuplicates([
      member({ clientMemberKey: 'a' }),
      member({ clientMemberKey: 'b', nationality: 'Hàn Quốc' }),
    ])).toEqual([]);
  });

  it('reports one conflict for a person entered three times, not three', () => {
    const pairs = findMemberDuplicates([
      member({ rowIndex: 0, clientMemberKey: 'a' }),
      member({ rowIndex: 1, clientMemberKey: 'b' }),
      member({ rowIndex: 2, clientMemberKey: 'c' }),
    ]);
    expect(pairs).toHaveLength(1);
  });

  it('ignores blank rows — a form in progress is full of them', () => {
    expect(findMemberDuplicates([
      member({ fullName: '', jobTitle: '', organization: '', nationality: '' }),
      member({ fullName: '', jobTitle: '', organization: '', nationality: '', clientMemberKey: 'b' }),
    ])).toEqual([]);
  });
});

describe('findCampusMemberDuplicates', () => {
  it('reads the two lists off a campus card in submit order', () => {
    const rows = campusMemberRows({
      visitors: [{ clientMemberKey: 'a', fullName: 'A', jobTitle: 'J', organization: 'O', organizationPartnerId: null, nationality: 'N' }],
      supportTeam: [{ clientMemberKey: 'b', fullName: 'A', jobTitle: 'J', organization: 'O', organizationPartnerId: null, nationality: 'N' }],
    } as never);
    expect(rows.map(r => r.kind)).toEqual(['visitors', 'supportTeam']);

    const pairs = findCampusMemberDuplicates({
      visitors: [{ clientMemberKey: 'a', fullName: 'A', jobTitle: 'J', organization: 'O', organizationPartnerId: null, nationality: 'N' }],
      supportTeam: [{ clientMemberKey: 'b', fullName: 'A', jobTitle: 'J', organization: 'O', organizationPartnerId: null, nationality: 'N' }],
    } as never);
    expect(pairs).toHaveLength(1);
  });
});

/**
 * ID-01. Choosing "— Không nằm trong danh sách đoàn —" and then typing somebody who IS in the list
 * leaves two records of one person with no link between them. One exact match is worth a question;
 * several mean the evidence names nobody.
 */
describe('findContactMemberCandidates', () => {
  const rows = [
    member({ clientMemberKey: 'a', fullName: 'Nguyễn Văn An' }),
    member({ clientMemberKey: 'b', rowIndex: 1, fullName: 'Trần Thị Bình', jobTitle: 'Trợ lý' }),
  ];

  it('finds the member a hand-typed contact describes', () => {
    const found = findContactMemberCandidates(
      { fullName: ' nguyễn văn an ', jobTitle: 'Giám đốc', organization: 'ABC University' }, rows);
    expect(found.map(m => m.clientMemberKey)).toEqual(['a']);
  });

  it('does not match on the name alone', () => {
    expect(findContactMemberCandidates(
      { fullName: 'Nguyễn Văn An', jobTitle: 'Phó giám đốc', organization: 'ABC University' }, rows))
      .toEqual([]);
  });

  it('returns every match rather than picking one', () => {
    const twins = [member({ clientMemberKey: 'a' }), member({ clientMemberKey: 'b', rowIndex: 1 })];
    expect(findContactMemberCandidates(
      { fullName: 'Nguyễn Văn An', jobTitle: 'Giám đốc', organization: 'ABC University' }, twins))
      .toHaveLength(2);
  });

  it('says nothing about an empty contact block', () => {
    expect(findContactMemberCandidates({ fullName: '', jobTitle: '', organization: '' }, rows)).toEqual([]);
    expect(findContactMemberCandidates(null, rows)).toEqual([]);
  });

  it('ignores a member row with no stable key — there would be nothing to link to', () => {
    expect(findContactMemberCandidates(
      { fullName: 'Nguyễn Văn An', jobTitle: 'Giám đốc', organization: 'ABC University' },
      [member({ clientMemberKey: null })]))
      .toEqual([]);
  });
});
