import { describe, expect, it } from 'vitest';
import {
  applyContentToAllCampuses,
  applyImportedMembersToCampus,
  buildV2CreatePayload,
  buildV2EditPayload,
  campusVisitHasUserContent,
  cloneCampusVisitContent,
  createEmptyCampusVisit,
  listOverwrittenCampuses,
  mapServerFieldPathToFormPath,
  migrateV1DraftToV2,
  projectV2ToV1FormValues,
  resolvedFormToV2Schema,
} from '../utils/visitRequestV2Form';
import type { CampusVisitSchema, VisitRequestV2Schema } from '../schema/visitRequestV2.schema';
import type { VisitRequestSchema } from '../schema/visitRequest.schema';
import type { ResolvedVisitForm } from '../api/visitRequestV2Api';

const campus = (overrides: Partial<CampusVisitSchema>): CampusVisitSchema => ({
  ...createEmptyCampusVisit(overrides.clientKey ?? 'ck-test'),
  ...overrides,
});

const filledCampus = (key: string, code: string): CampusVisitSchema =>
  campus({
    clientKey: key,
    campus: code,
    startDatetime: '2026-08-01T09:00',
    endDatetime: '2026-08-01T11:30',
    delegationName: `Đoàn ${code}`,
    visitType: 'MEETING',
    purpose: `Mục đích ${code}`,
    workingContent: `Nội dung ${code}`,
    visitors: [{ fullName: `Khách ${code}`, jobTitle: 'GV', organization: 'ĐH X', nationality: 'VN' }],
    supportTeam: [{ fullName: `HT ${code}`, jobTitle: '', organization: '', nationality: '' }],
    operationalContact: { fullName: `ĐM ${code}`, organization: 'ĐH X', phone: '+84912345678', email: '' },
    workingLanguage: 'EN',
    mediaConsentStatus: 'AGREED',
    mediaConsentNote: 'note',
    notes: `ghi chú ${code}`,
  });

const values = (campusVisits: CampusVisitSchema[]): VisitRequestV2Schema => ({
  registerInfo: {
    fullName: 'Người ĐK', organization: 'ĐH X', jobTitle: 'TP',
    phone: '+84912345678', email: 'reg@example.com', nationality: 'VN',
  },
  contactPoint: { fullName: 'Đầu Mối', organization: 'ĐH X', phone: '+84987654321', email: 'contact@example.com' },
  partnerSelectionMode: 'NEW_ORGANIZATION',
  partnerId: null,
  campusVisits,
});

describe('cloneCampusVisitContent', () => {
  it('deep-clones content but PRESERVES target identity/campus/schedule', () => {
    const source = filledCampus('ck-src', 'HN');
    const target = campus({
      clientKey: 'ck-tgt', campus: 'HCM',
      startDatetime: '2026-09-01T08:00', endDatetime: '2026-09-01T10:00',
      visitInstanceId: 42, expectedRowVersion: 3,
    });
    const result = cloneCampusVisitContent(source, target);

    expect(result.clientKey).toBe('ck-tgt');
    expect(result.campus).toBe('HCM');
    expect(result.startDatetime).toBe('2026-09-01T08:00');
    expect(result.endDatetime).toBe('2026-09-01T10:00');
    expect(result.visitInstanceId).toBe(42);
    expect(result.expectedRowVersion).toBe(3);
    expect(result.delegationName).toBe('Đoàn HN');
    expect(result.visitors).toEqual(source.visitors);
  });

  it('editing the copy never mutates the source (no shared references)', () => {
    const source = filledCampus('ck-src', 'HN');
    const result = cloneCampusVisitContent(source, campus({ clientKey: 'ck-tgt', campus: 'DN' }));

    result.visitors[0].fullName = 'ĐÃ SỬA';
    result.operationalContact.fullName = 'ĐÃ SỬA';
    result.supportTeam.push({ fullName: 'Thêm', jobTitle: '', organization: '', nationality: '' });

    expect(source.visitors[0].fullName).toBe('Khách HN');
    expect(source.operationalContact.fullName).toBe('ĐM HN');
    expect(source.supportTeam).toHaveLength(1);
  });
});

describe('applyContentToAllCampuses', () => {
  it('overwrites every OTHER campus with independent copies; source untouched', () => {
    const list = [filledCampus('a', 'HN'), filledCampus('b', 'HCM'), filledCampus('c', 'DN')];
    const next = applyContentToAllCampuses(list, 1);

    expect(next[1]).toBe(list[1]); // source identity preserved
    expect(next[0].delegationName).toBe('Đoàn HCM');
    expect(next[2].delegationName).toBe('Đoàn HCM');
    // Identity + schedule of the targets preserved:
    expect(next[0].clientKey).toBe('a');
    expect(next[0].campus).toBe('HN');
    // The copies are independent of each other AND the source:
    next[0].visitors[0].fullName = 'X';
    expect(next[2].visitors[0].fullName).toBe('Khách HCM');
    expect(next[1].visitors[0].fullName).toBe('Khách HCM');
  });

  it('listOverwrittenCampuses names only NON-EMPTY other cards (confirm dialog input)', () => {
    const list = [filledCampus('a', 'HN'), campus({ clientKey: 'b', campus: 'HCM' }), filledCampus('c', 'DN')];
    const labels = listOverwrittenCampuses(list, 0, (cv) => cv.campus);
    expect(labels).toEqual(['DN']); // the empty HCM card is not "overwritten content"
  });
});

describe('campusVisitHasUserContent', () => {
  it('is false for a fresh card and true once content is typed', () => {
    expect(campusVisitHasUserContent(createEmptyCampusVisit('x'))).toBe(false);
    expect(campusVisitHasUserContent(campus({ clientKey: 'x', purpose: 'abc' }))).toBe(true);
    expect(campusVisitHasUserContent(campus({
      clientKey: 'x',
      visitors: [{ fullName: 'A', jobTitle: '', organization: '', nationality: '' }],
    }))).toBe(true);
  });
});

describe('buildV2CreatePayload', () => {
  it('produces the real v2 contract: fully-resolved snapshots, no sameForAll/scope fields', () => {
    const payload = buildV2CreatePayload(values([filledCampus('a', 'hn')]), 'sub-1');

    expect(payload.submissionId).toBe('sub-1');
    expect(payload.registrant.email).toBe('reg@example.com');
    expect(payload.primaryContact.email).toBe('contact@example.com');
    expect(payload.campusVisits).toHaveLength(1);
    const cv = payload.campusVisits[0];
    expect(cv.campusId).toBe('HN'); // normalized code
    expect(cv.plannedStartAt).toBe('2026-08-01T09:00');
    expect(cv.visitTypeOther).toBeNull(); // not OTHER → null
    expect(cv.processing).toBeNull(); // public: never sends processing
    expect(payload).not.toHaveProperty('visitScope');
    expect(payload).not.toHaveProperty('hasMixedCampusDetails');
    expect(payload).not.toHaveProperty('sameForAll');
  });

  it('attaches per-campus processing ONLY for matching campuses (authenticated mode)', () => {
    const payload = buildV2CreatePayload(
      values([filledCampus('a', 'HN'), filledCampus('b', 'HCM')]),
      'sub-2',
      [{ campusId: 'HCM', mode: 'SELF_HOST', hostUserId: null }],
    );
    expect(payload.campusVisits[0].processing).toBeNull();
    expect(payload.campusVisits[1].processing).toEqual({ mode: 'SELF_HOST', hostUserId: null });
  });

  it('sends partnerId only in EXISTING_PARTNER mode', () => {
    const v = values([filledCampus('a', 'HN')]);
    v.partnerId = 7;
    expect(buildV2CreatePayload(v, 's').partnerId).toBeNull();
    v.partnerSelectionMode = 'EXISTING_PARTNER';
    expect(buildV2CreatePayload(v, 's').partnerId).toBe(7);
  });
});

describe('buildV2EditPayload', () => {
  it('carries request row version + per-instance stable ids/versions (null id = added campus)', () => {
    const existing = { ...filledCampus('a', 'HN'), visitInstanceId: 11, expectedRowVersion: 4 };
    const added = filledCampus('b', 'HCM'); // no instance id
    const payload = buildV2EditPayload(values([existing, added]), 9);

    expect(payload.expectedRequestRowVersion).toBe(9);
    expect(payload.registrant.email).toBe('reg@example.com');
    expect(payload.primaryContact.fullName).toBe('Đầu Mối');
    expect(payload.campusVisits[0].visitInstanceId).toBe(11);
    expect(payload.campusVisits[0].expectedRowVersion).toBe(4);
    expect(payload.campusVisits[1].visitInstanceId).toBeNull();
    expect(payload.campusVisits[1].expectedRowVersion).toBeNull();
  });
});

describe('projectV2ToV1FormValues (OTP initiate projection only)', () => {
  it('maps all campus slots, merges people (deduped), and takes first-campus content', () => {
    const a = filledCampus('a', 'HN');
    const b = filledCampus('b', 'HCM');
    b.visitors = [
      { fullName: 'Khách HN', jobTitle: 'GV', organization: 'ĐH X', nationality: 'VN' }, // duplicate of A's guest
      { fullName: 'Khách riêng HCM', jobTitle: 'PGS', organization: 'ĐH Y', nationality: 'VN' },
    ];
    const v1 = projectV2ToV1FormValues(values([a, b]));

    expect(v1.visitMode).toBe('multiple');
    expect(v1.visits).toEqual([
      { campus: 'HN', startDatetime: '2026-08-01T09:00', endDatetime: '2026-08-01T11:30' },
      { campus: 'HCM', startDatetime: '2026-08-01T09:00', endDatetime: '2026-08-01T11:30' },
    ]);
    expect(v1.delegationName).toBe('Đoàn HN');
    // B's first guest is an exact duplicate of A's guest → deduped once.
    expect(v1.visitors.map(x => x.fullName)).toEqual(['Khách HN', 'Khách riêng HCM']);
    expect(v1.contactPoint.email).toBe('contact@example.com');
  });
});

describe('migrateV1DraftToV2 (global draft → per-campus duplication)', () => {
  const v1Draft: Partial<VisitRequestSchema> = {
    registerInfo: {
      fullName: 'Cũ', organization: 'ĐH Cũ', jobTitle: 'GV',
      phone: '+84911111111', email: 'old@example.com', nationality: 'VN',
    },
    delegationName: 'Đoàn Cũ',
    visitType: 'WORKSHOP',
    purpose: 'Mục đích cũ',
    workingContent: 'ND cũ',
    visits: [
      { campus: 'HN', startDatetime: '2026-08-10T08:00', endDatetime: '2026-08-10T11:00' },
      { campus: 'DN', startDatetime: '2026-08-12T13:00', endDatetime: '2026-08-12T16:00' },
    ],
    visitors: [{ fullName: 'Khách Cũ', jobTitle: 'GV', organization: 'ĐH Cũ', nationality: 'VN' }],
    supportTeam: [{ fullName: 'HT Cũ', jobTitle: 'TL', organization: 'ĐH Cũ', nationality: 'VN' }],
    contactPoint: { fullName: 'ĐM Cũ', organization: 'ĐH Cũ', phone: '+84922222222', email: 'dm@example.com' },
    workingLanguage: 'EN',
    mediaConsentStatus: 'AGREED',
    notes: 'ghi chú cũ',
  };

  it('duplicates the global snapshot into EVERY selected campus, keeping per-campus schedule', () => {
    const v2 = migrateV1DraftToV2(v1Draft);
    expect(v2.campusVisits).toHaveLength(2);
    const [hn, dn] = v2.campusVisits!;
    expect(hn.campus).toBe('HN');
    expect(hn.startDatetime).toBe('2026-08-10T08:00');
    expect(dn.campus).toBe('DN');
    expect(dn.startDatetime).toBe('2026-08-12T13:00');
    for (const cv of [hn, dn]) {
      expect(cv.delegationName).toBe('Đoàn Cũ');
      expect(cv.visitors[0].fullName).toBe('Khách Cũ');
      expect(cv.supportTeam[0].fullName).toBe('HT Cũ');
      expect(cv.operationalContact.email).toBe('dm@example.com');
      expect(cv.workingLanguage).toBe('EN');
      expect(cv.mediaConsentStatus).toBe('AGREED');
      expect(cv.clientKey).toBeTruthy();
    }
    expect(hn.clientKey).not.toBe(dn.clientKey);
    // The duplicated lists are independent copies, not shared references:
    hn.visitors[0].fullName = 'SỬA HN';
    expect(dn.visitors[0].fullName).toBe('Khách Cũ');
    // Request-level data preserved once:
    expect(v2.registerInfo?.email).toBe('old@example.com');
    expect(v2.contactPoint?.email).toBe('dm@example.com');
  });

  it('yields one empty campus card when the v1 draft had no slots', () => {
    const v2 = migrateV1DraftToV2({ delegationName: 'Chỉ tên đoàn' });
    expect(v2.campusVisits).toHaveLength(1);
    expect(v2.campusVisits![0].campus).toBe('');
    expect(v2.campusVisits![0].delegationName).toBe('Chỉ tên đoàn');
  });
});

describe('mapServerFieldPathToFormPath', () => {
  it('maps campus + nested member paths to the exact RHF path', () => {
    expect(mapServerFieldPathToFormPath('Form.CampusVisits[2].Visitors[0].FullName'))
      .toBe('campusVisits.2.visitors.0.fullName');
    expect(mapServerFieldPathToFormPath('Form.CampusVisits[0].ExternalSupportMembers[3].Organization'))
      .toBe('campusVisits.0.supportTeam.3.organization');
    expect(mapServerFieldPathToFormPath('Form.CampusVisits[1].CampusId'))
      .toBe('campusVisits.1.campus');
    expect(mapServerFieldPathToFormPath('Form.CampusVisits[1].PlannedEndAt'))
      .toBe('campusVisits.1.endDatetime');
    expect(mapServerFieldPathToFormPath('Form.CampusVisits[0].OperationalContact.FullName'))
      .toBe('campusVisits.0.operationalContact.fullName');
    expect(mapServerFieldPathToFormPath('Form.Registrant.Email')).toBe('registerInfo.email');
    expect(mapServerFieldPathToFormPath('Form.PrimaryContact.Phone')).toBe('contactPoint.phone');
  });

  it('returns null for unmappable paths (they stay on the generic banner)', () => {
    expect(mapServerFieldPathToFormPath('Form.SubmissionId')).toBeNull();
    expect(mapServerFieldPathToFormPath('SomethingElse')).toBeNull();
    expect(mapServerFieldPathToFormPath('')).toBeNull();
  });
});

describe('resolvedFormToV2Schema (edit/resubmit hydration)', () => {
  const resolved = (overrides: Partial<ResolvedVisitForm> = {}): ResolvedVisitForm => ({
    visitRequestId: 5,
    requestCode: 'VR-5',
    rowVersion: 7,
    formSchemaVersion: 2,
    hasMixedCampusDetails: true,
    visitScope: 'MULTI_CAMPUS',
    requestStatus: 'PENDING_APPROVAL',
    createdSource: 'PUBLIC',
    submittedAt: '2026-07-15T08:00:00',
    partnerId: 3,
    registrant: { fullName: 'Reg', organization: 'ĐH X', jobTitle: 'TP', phone: '+8491', email: 'reg@x.vn', nationality: 'VN' },
    primaryContact: { fullName: 'ĐM', organization: 'ĐH X', phone: '+8492', email: 'c@x.vn', accessStatus: 'ACTIVE', verifiedAt: null },
    campusVisits: [
      {
        visitInstanceId: 10, campusId: 1, campusCode: 'HN', campusName: 'FPTU HN',
        plannedStartAt: '2026-08-01T09:00:00', plannedEndAt: '2026-08-01T11:30:00', timezone: 'Asia/Ho_Chi_Minh',
        instanceStatus: 'PENDING', currentHostUserId: null, currentHostName: null, decidedByUserId: null,
        decidedByName: null, decidedAt: null, decisionActorRole: null, decisionNote: null,
        delegationName: 'Đoàn HN', visitType: 'MEETING', visitTypeOther: null, purpose: 'MĐ HN', workingContent: 'ND HN',
        visitors: [{ guestMemberId: 1, memberType: 'VISITOR', fullName: 'Khách HN', organization: 'ĐH X', jobTitle: 'GV', nationality: 'VN', displayOrder: 1 }],
        supportMembers: [], operationalContact: { fullName: 'OP HN', organization: 'ĐH X', phone: '+8493', email: 'op@x.vn' },
        workingLanguage: 'VI', transportationNote: null, mediaConsentStatus: 'DECLINED', mediaConsentNote: null, noteToFptu: 'ghi chú HN',
        formRevision: 2, approvalRevision: 1, rowVersion: 4, activeAmendment: null,
      },
      {
        visitInstanceId: 11, campusId: 2, campusCode: 'HCM', campusName: 'FPTU HCM',
        plannedStartAt: '2026-08-02T13:00:00', plannedEndAt: '2026-08-02T15:00:00', timezone: 'Asia/Ho_Chi_Minh',
        instanceStatus: 'PENDING', currentHostUserId: null, currentHostName: null, decidedByUserId: null,
        decidedByName: null, decidedAt: null, decisionActorRole: null, decisionNote: null,
        delegationName: 'Đoàn HCM', visitType: 'WORKSHOP', visitTypeOther: null, purpose: 'MĐ HCM', workingContent: null,
        visitors: [{ guestMemberId: 2, memberType: 'VISITOR', fullName: 'Khách HCM', organization: 'ĐH Y', jobTitle: 'TS', nationality: 'VN', displayOrder: 1 }],
        supportMembers: [{ guestMemberId: 3, memberType: 'SUPPORT', fullName: 'HT HCM', organization: 'ĐH Y', jobTitle: 'TL', nationality: 'VN', displayOrder: 1 }],
        operationalContact: { fullName: 'OP HCM', organization: 'ĐH Y', phone: '+8494', email: '' },
        workingLanguage: 'EN', transportationNote: 'xe 16 chỗ', mediaConsentStatus: 'AGREED', mediaConsentNote: 'ok', noteToFptu: null,
        formRevision: 3, approvalRevision: 2, rowVersion: 6, activeAmendment: null,
      },
    ],
    viewer: { relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false, allowedActions: ['VIEW'] },
    ...overrides,
  });

  it('carries request + per-instance rowVersions and stable visitInstanceIds for optimistic concurrency', () => {
    const { values, expectedRequestRowVersion } = resolvedFormToV2Schema(resolved());
    expect(expectedRequestRowVersion).toBe(7);
    expect(values.campusVisits.map(c => c.visitInstanceId)).toEqual([10, 11]);
    expect(values.campusVisits.map(c => c.expectedRowVersion)).toEqual([4, 6]);
    // Fresh, distinct client keys minted (the read model has none):
    expect(values.campusVisits[0].clientKey).toBeTruthy();
    expect(values.campusVisits[0].clientKey).not.toBe(values.campusVisits[1].clientKey);
  });

  it('hydrates each campus with ITS OWN content — never a first-campus projection', () => {
    const { values } = resolvedFormToV2Schema(resolved());
    const [hn, hcm] = values.campusVisits;
    expect(hn.campus).toBe('HN');
    expect(hn.delegationName).toBe('Đoàn HN');
    expect(hn.startDatetime).toBe('2026-08-01T09:00'); // datetime-local (16 chars)
    expect(hn.workingLanguage).toBe('VI');
    expect(hn.notes).toBe('ghi chú HN');
    expect(hcm.campus).toBe('HCM');
    expect(hcm.delegationName).toBe('Đoàn HCM');
    expect(hcm.visitType).toBe('WORKSHOP');
    expect(hcm.workingLanguage).toBe('EN');
    expect(hcm.mediaConsentStatus).toBe('AGREED');
    expect(hcm.supportTeam[0].fullName).toBe('HT HCM');
    // The two campuses are independent copies:
    hn.visitors[0].fullName = 'SỬA HN';
    expect(hcm.visitors[0].fullName).toBe('Khách HCM');
  });

  it('maps partner + registrant/contact request-level once', () => {
    const { values } = resolvedFormToV2Schema(resolved());
    expect(values.partnerSelectionMode).toBe('EXISTING_PARTNER');
    expect(values.partnerId).toBe(3);
    expect(values.registerInfo.email).toBe('reg@x.vn');
    expect(values.contactPoint.email).toBe('c@x.vn');

    const noPartner = resolvedFormToV2Schema(resolved({ partnerId: null }));
    expect(noPartner.values.partnerSelectionMode).toBe('NEW_ORGANIZATION');
    expect(noPartner.values.partnerId).toBeNull();
  });

  it('round-trips through buildV2EditPayload with the correct row versions and instance ids', () => {
    const { values, expectedRequestRowVersion } = resolvedFormToV2Schema(resolved());
    const payload = buildV2EditPayload(values, expectedRequestRowVersion);
    expect(payload.expectedRequestRowVersion).toBe(7);
    expect(payload.campusVisits[0].visitInstanceId).toBe(10);
    expect(payload.campusVisits[0].expectedRowVersion).toBe(4);
    expect(payload.campusVisits[1].visitInstanceId).toBe(11);
    expect(payload.campusVisits[1].expectedRowVersion).toBe(6);
    expect(payload.campusVisits[1].campusId).toBe('HCM');
  });
});

describe('applyImportedMembersToCampus (per-campus Excel import)', () => {
  it('replaces members of the TARGET campus only', () => {
    const list = [filledCampus('a', 'HN'), filledCampus('b', 'HCM')];
    const rows = [
      { fullName: 'Import 1', jobTitle: 'GV', organization: 'ĐH Z', nationality: 'VN' },
      { fullName: 'Import 2', jobTitle: 'TS', organization: 'ĐH Z', nationality: 'VN' },
    ];
    const next = applyImportedMembersToCampus(list, 1, 'visitors', rows);

    expect(next[0].visitors.map(v => v.fullName)).toEqual(['Khách HN']); // untouched
    expect(next[1].visitors.map(v => v.fullName)).toEqual(['Import 1', 'Import 2']);
    // Imported rows are copies — mutating the input array later changes nothing:
    rows[0].fullName = 'MUTATED';
    expect(next[1].visitors[0].fullName).toBe('Import 1');
  });
});
