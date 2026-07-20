import { describe, expect, it } from 'vitest';
import { buildVisitRequestV2Schema } from '../schema/visitRequestV2.schema';
import { createEmptyCampusVisit } from '../utils/visitRequestV2Form';

/**
 * The fields that were optional on one side of the wire and required on the other. Each case here
 * is a value the form used to accept and the database or the campus staff could not actually use:
 * a request with no working content, an operational contact nobody can phone or email, a support
 * row that violates a NOT NULL column. Whitespace counts as empty everywhere.
 */

const t = (key: string) => key; // paths are what matter, not messages
const schema = buildVisitRequestV2Schema(72, t);

const futureAt = (extraMs = 0): string => {
  const d = new Date(Date.now() + 200 * 3600 * 1000 + extraMs);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
};

const validCampus = () => ({
  ...createEmptyCampusVisit('ck-1'),
  campus: 'HN',
  startDatetime: futureAt(),
  endDatetime: futureAt(3 * 3600 * 1000),
  delegationName: 'Đoàn A',
  visitType: 'MEETING' as const,
  purpose: 'Trao đổi',
  workingContent: 'Nội dung làm việc',
  visitors: [{ fullName: 'Khách 1', jobTitle: 'GV', organization: 'ĐH X', nationality: 'VN' }],
  supportTeam: [],
  operationalContact: {
    fullName: 'ĐM CS', organization: 'ĐH X', phone: '+84911111111', email: 'op@example.com',
  },
});

const validValues = () => ({
  registerInfo: {
    fullName: 'Người ĐK', organization: 'ĐH X', jobTitle: 'TP',
    phone: '+84912345678', email: 'reg@example.com', nationality: 'VN',
  },
  contactPoint: {
    fullName: 'ĐM', organization: 'ĐH X', phone: '+84987654321', email: 'contact@example.com',
  },
  partnerSelectionMode: 'NEW_ORGANIZATION' as const,
  partnerId: null,
  campusVisits: [validCampus()],
});

/** Returns the dot-joined issue paths for a mutated copy of the valid payload. */
const pathsFor = (mutate: (v: ReturnType<typeof validValues>) => void): string[] => {
  const values = validValues();
  mutate(values);
  const result = schema.safeParse(values);
  return result.success ? [] : result.error.issues.map(i => i.path.join('.'));
};

describe('v2 required-field contract', () => {
  it('accepts the fully-populated baseline', () => {
    expect(schema.safeParse(validValues()).success).toBe(true);
  });

  describe.each([
    ['registerInfo.jobTitle', (v: ReturnType<typeof validValues>) => { v.registerInfo.jobTitle = ''; }],
    ['registerInfo.nationality', (v: ReturnType<typeof validValues>) => { v.registerInfo.nationality = ''; }],
    ['contactPoint.organization', (v: ReturnType<typeof validValues>) => { v.contactPoint.organization = ''; }],
    ['campusVisits.0.workingContent', (v: ReturnType<typeof validValues>) => { v.campusVisits[0].workingContent = ''; }],
    ['campusVisits.0.operationalContact.organization', (v: ReturnType<typeof validValues>) => { v.campusVisits[0].operationalContact.organization = ''; }],
    ['campusVisits.0.operationalContact.email', (v: ReturnType<typeof validValues>) => { v.campusVisits[0].operationalContact.email = ''; }],
  ])('%s', (path, mutate) => {
    it('is rejected when empty', () => expect(pathsFor(mutate)).toContain(path));
  });

  describe.each([
    ['registerInfo.jobTitle', (v: ReturnType<typeof validValues>) => { v.registerInfo.jobTitle = '   '; }],
    ['contactPoint.organization', (v: ReturnType<typeof validValues>) => { v.contactPoint.organization = '   '; }],
    ['campusVisits.0.workingContent', (v: ReturnType<typeof validValues>) => { v.campusVisits[0].workingContent = '   '; }],
    ['campusVisits.0.operationalContact.organization', (v: ReturnType<typeof validValues>) => { v.campusVisits[0].operationalContact.organization = '  '; }],
  ])('%s', (path, mutate) => {
    it('treats whitespace-only as empty', () => expect(pathsFor(mutate)).toContain(path));
  });

  it('rejects an invalid operational contact email', () => {
    expect(pathsFor(v => { v.campusVisits[0].operationalContact.email = 'not-an-email'; }))
      .toContain('campusVisits.0.operationalContact.email');
  });

  it('accepts a national phone number, not only E.164', () => {
    expect(pathsFor(v => { v.campusVisits[0].operationalContact.phone = '0912345678'; })).toEqual([]);
  });

  it('rejects a phone number that only looks like one', () => {
    expect(pathsFor(v => { v.campusVisits[0].operationalContact.phone = '090abc123'; }))
      .toContain('campusVisits.0.operationalContact.phone');
  });

  describe('support team', () => {
    it('may be empty', () => {
      expect(pathsFor(v => { v.campusVisits[0].supportTeam = []; })).toEqual([]);
    });

    it('requires every column once a row exists (the DB columns are NOT NULL)', () => {
      const paths = pathsFor(v => {
        v.campusVisits[0].supportTeam = [
          { fullName: 'Hỗ trợ 1', jobTitle: '', organization: '', nationality: '' },
        ];
      });
      expect(paths).toContain('campusVisits.0.supportTeam.0.jobTitle');
      expect(paths).toContain('campusVisits.0.supportTeam.0.organization');
      expect(paths).toContain('campusVisits.0.supportTeam.0.nationality');
    });

    it('accepts a fully-filled row', () => {
      expect(pathsFor(v => {
        v.campusVisits[0].supportTeam = [
          { fullName: 'Hỗ trợ 1', jobTitle: 'CV', organization: 'ĐH X', nationality: 'VN' },
        ];
      })).toEqual([]);
    });
  });

  describe('length boundaries', () => {
    it('accepts exactly the limit', () => {
      expect(pathsFor(v => { v.campusVisits[0].purpose = 'x'.repeat(2000); })).toEqual([]);
      expect(pathsFor(v => { v.campusVisits[0].workingContent = 'x'.repeat(4000); })).toEqual([]);
    });

    it('rejects one character over', () => {
      expect(pathsFor(v => { v.campusVisits[0].purpose = 'x'.repeat(2001); }))
        .toContain('campusVisits.0.purpose');
      expect(pathsFor(v => { v.campusVisits[0].workingContent = 'x'.repeat(4001); }))
        .toContain('campusVisits.0.workingContent');
      expect(pathsFor(v => { v.registerInfo.fullName = 'x'.repeat(151); }))
        .toContain('registerInfo.fullName');
      expect(pathsFor(v => { v.contactPoint.organization = 'x'.repeat(201); }))
        .toContain('contactPoint.organization');
    });
  });

  it('reports an error against the campus that actually has it', () => {
    const values = validValues();
    values.campusVisits = [
      validCampus(),
      { ...validCampus(), clientKey: 'ck-2', campus: 'HCM', workingContent: '' },
    ];
    const result = schema.safeParse(values);
    const paths = result.success ? [] : result.error.issues.map(i => i.path.join('.'));
    expect(paths).toContain('campusVisits.1.workingContent');
    expect(paths).not.toContain('campusVisits.0.workingContent');
  });
});
