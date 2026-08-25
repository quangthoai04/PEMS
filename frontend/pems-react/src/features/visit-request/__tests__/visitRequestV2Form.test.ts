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
  resolveExactlyOne,
  resolvedFormToV2Schema,
  restoreCampusVisitFromDraft,
  withMemberKeys,
} from '../utils/visitRequestV2Form';
import type { CampusVisitSchema, VisitRequestV2Schema } from '../schema/visitRequestV2.schema';
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
    operationalContact: { fullName: `ĐM ${code}`, organization: 'ĐH X', jobTitle: 'Trưởng phòng Hợp tác', phone: '+84912345678', email: '' },
    workingLanguage: 'EN',
    mediaConsentStatus: 'AGREED',
    notes: `ghi chú ${code}`,
  });

const values = (campusVisits: CampusVisitSchema[]): VisitRequestV2Schema => ({
  registerInfo: {
    fullName: 'Người ĐK', organization: 'ĐH X', jobTitle: 'TP',
    phone: '+84912345678', email: 'reg@example.com', nationality: 'VN',
  },
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
    // Same PEOPLE, different identities: the copy becomes its own guest_member rows on the server,
    // so re-using the source's member keys — or its persisted guestMemberId, if the source campus
    // was an existing one — would make one identity name two rows (NP-03 / CanhIter3FixBug).
    expect(result.visitors.map(v => ({ ...v, clientMemberKey: undefined, guestMemberId: undefined })))
      .toEqual(source.visitors.map(v => ({ ...v, clientMemberKey: undefined, guestMemberId: undefined })));
    expect(result.visitors[0].clientMemberKey).toBeTruthy();
    expect(result.visitors[0].clientMemberKey).not.toBe(source.visitors[0].clientMemberKey);
    // The copy has no persisted identity of its own yet on the TARGET campus, even if the source row
    // did on the source campus — carrying that id over would silently point the copy's relation pick
    // at a VisitGuestMember row that belongs to a different instance entirely.
    expect(result.visitors[0].guestMemberId ?? null).toBeNull();
  });

  it('re-points the contact pick at the COPY of the person it named', () => {
    const source = filledCampus('ck-src', 'HN');
    const picked = 'member-key-hn';
    source.visitors[0].clientMemberKey = picked;
    source.operationalContactClientMemberKey = picked;

    const result = cloneCampusVisitContent(source, campus({ clientKey: 'ck-tgt', campus: 'DN' }));

    // Not the source's key (that names another campus's row) and not null (the pick is not lost).
    expect(result.operationalContactClientMemberKey).toBe(result.visitors[0].clientMemberKey);
    expect(result.operationalContactClientMemberKey).not.toBe(picked);
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
    expect(payload.campusVisits).toHaveLength(1);
    const cv = payload.campusVisits[0];
    expect(cv.campusId).toBe('HN'); // normalized code
    expect(cv.plannedStartAt).toBe('2026-08-01T09:00');
    expect(cv.visitTypeOther).toBeNull(); // not OTHER → null
    expect(cv.hostSelection).toBeNull(); // public: never names a reception host
    expect(payload).not.toHaveProperty('visitScope');
    expect(payload).not.toHaveProperty('hasMixedCampusDetails');
    expect(payload).not.toHaveProperty('sameForAll');
  });

  // The bug this pins: the form collected "Ghi chú gửi FPTU" and the mapper never put it in the
  // payload, so every note the guest typed was dropped at the browser boundary — the request
  // succeeded, and the note simply did not exist anywhere downstream.
  it('sends notes to the backend, and no longer sends mediaConsentNote', () => {
    const payload = buildV2CreatePayload(values([filledCampus('a', 'HN')]), 'sub-notes');
    const cv = payload.campusVisits[0];

    expect(cv.notes).toBe('ghi chú HN');
    expect(cv).not.toHaveProperty('mediaConsentNote');
  });

  it('trims notes and sends null rather than an empty string', () => {
    const blank = buildV2CreatePayload(
      values([{ ...filledCampus('a', 'HN'), notes: '   ' }]), 'sub-blank');
    expect(blank.campusVisits[0].notes).toBeNull();

    const padded = buildV2CreatePayload(
      values([{ ...filledCampus('a', 'HN'), notes: '  cần xe điện  ' }]), 'sub-pad');
    expect(padded.campusVisits[0].notes).toBe('cần xe điện');
  });

  // notes and media consent are independent: neither value of the consent gates the note.
  it.each([['AGREED'], ['DECLINED']] as const)(
    'sends notes regardless of mediaConsentStatus=%s', (status) => {
      const payload = buildV2CreatePayload(
        values([{ ...filledCampus('a', 'HN'), mediaConsentStatus: status, notes: 'hỗ trợ xe điện' }]),
        'sub-consent');
      expect(payload.campusVisits[0].mediaConsentStatus).toBe(status);
      expect(payload.campusVisits[0].notes).toBe('hỗ trợ xe điện');
    });

  it('carries notes through the EDIT payload too, so an edit cannot silently drop it', () => {
    const payload = buildV2EditPayload(values([filledCampus('a', 'HN')]), 7);
    expect(payload.campusVisits[0].notes).toBe('ghi chú HN');
    expect(payload.campusVisits[0]).not.toHaveProperty('mediaConsentNote');
  });

  it('attaches per-campus processing ONLY for matching campuses (authenticated mode)', () => {
    const payload = buildV2CreatePayload(
      values([filledCampus('a', 'HN'), filledCampus('b', 'HCM')]),
      'sub-2',
      [{ campusId: 'HCM', mode: 'SELF', proposedHostUserId: null }],
    );
    expect(payload.campusVisits[0].hostSelection).toBeNull();
    expect(payload.campusVisits[1].hostSelection).toEqual({ mode: 'SELF', proposedHostUserId: null, confirmedHostConflict: false });
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
    expect(payload.campusVisits[0].visitInstanceId).toBe(11);
    expect(payload.campusVisits[0].expectedRowVersion).toBe(4);
    expect(payload.campusVisits[1].visitInstanceId).toBeNull();
    expect(payload.campusVisits[1].expectedRowVersion).toBeNull();
  });
});


describe('operational-contact relation payload fail-safe (plan CanhIter3FixBug §16/§18)', () => {
  /** A fresh campus (no visitInstanceId) with a member picked and a valid, resolvable key. */
  const freshMemberCampus = (): CampusVisitSchema => {
    const cv = filledCampus('a', 'HN');
    cv.visitors[0].clientMemberKey = 'valid-key';
    cv.operationalContactClientMemberKey = 'valid-key';
    cv.operationalContactSource = 'MEMBER';
    return cv;
  };

  it('fresh + MEMBER + a key that resolves exactly once → serializes the relation', () => {
    const payload = buildV2CreatePayload(values([freshMemberCampus()]), 'sub');
    expect(payload.campusVisits[0].operationalContactClientMemberKey).toBe('valid-key');
  });

  it('fresh + EXTERNAL with a stray valid key in state → the relation is forced null, never leaked', () => {
    const cv = freshMemberCampus();
    cv.operationalContactSource = 'EXTERNAL'; // the key is left over from before the switch
    const payload = buildV2CreatePayload(values([cv]), 'sub');
    expect(payload.campusVisits[0].operationalContactClientMemberKey).toBeNull();
    expect(payload.campusVisits[0].operationalContactGuestMemberId).toBeNull();
  });

  it('fresh + null source with a stray valid key in state → same fail-safe applies', () => {
    const cv = freshMemberCampus();
    cv.operationalContactSource = null;
    const payload = buildV2CreatePayload(values([cv]), 'sub');
    expect(payload.campusVisits[0].operationalContactClientMemberKey).toBeNull();
  });

  it('existing campus (visitInstanceId set) keeps its relation regardless of source — the edit path never loses it', () => {
    const cv = freshMemberCampus();
    cv.visitInstanceId = 42;
    cv.expectedRowVersion = 3;
    cv.visitors[0].guestMemberId = 123;
    cv.operationalContactSource = null; // an existing campus never has an opinion on this field
    const payload = buildV2EditPayload(values([cv]), 1);
    expect(payload.campusVisits[0].operationalContactClientMemberKey).toBe('valid-key');
    expect(payload.campusVisits[0].operationalContactGuestMemberId).toBe(123);
  });

  it('operationalContactSource is READ to decide the relation, but never EMITTED on either payload shape', () => {
    const createPayload = buildV2CreatePayload(values([freshMemberCampus()]), 'sub');
    expect(createPayload.campusVisits[0]).not.toHaveProperty('operationalContactSource');

    const editCv = freshMemberCampus();
    editCv.visitInstanceId = 42;
    const editPayload = buildV2EditPayload(values([editCv]), 1);
    expect(editPayload.campusVisits[0]).not.toHaveProperty('operationalContactSource');
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
    // PrimaryContact is not a server path any more: there is no request-level contact to map.
    expect(mapServerFieldPathToFormPath('Form.PrimaryContact.Phone')).toBeNull();
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
    hasMixedCampusDetails: true,
    visitScope: 'MULTI_CAMPUS',
    requestStatus: 'PENDING_APPROVAL',
    createdSource: 'PUBLIC',
    submittedAt: '2026-07-15T08:00:00',
    partnerId: 3,
    cancelledByUserId: null, cancelledByName: null, cancelledAt: null, cancellationReason: null,
    registrant: { fullName: 'Reg', organization: 'ĐH X', jobTitle: 'TP', phone: '+8491', email: 'reg@x.vn', nationality: 'VN' },
    confirmationSummary: { total: 1, confirmed: 0, pending: 1, declined: 0, expired: 0, gateOpen: false },

    // Full-request scope in this fixture, so the backend sends the request-wide verdict.

    requestOutcome: { code: 'ALL_WAITING', total: 1, accepted: 0, inProgress: 0, waiting: 1, rejected: 0, cancelled: 0, closed: 0 },
    campusVisits: [
      {
        visitInstanceId: 10, campusId: 1, campusCode: 'HN', campusName: 'FPTU HN',
        plannedStartAt: '2026-08-01T09:00:00', plannedEndAt: '2026-08-01T11:30:00', timezone: 'Asia/Ho_Chi_Minh',
        instanceStatus: 'PENDING', currentHostUserId: null, currentHostName: null, decidedByUserId: null,
        decidedByName: null, decidedAt: null, decisionActorRole: null, decisionNote: null,
        delegationName: 'Đoàn HN', visitType: 'MEETING', visitTypeOther: null, purpose: 'MĐ HN', workingContent: 'ND HN',
        visitors: [{ guestMemberId: 1, memberType: 'VISITOR', fullName: 'Khách HN', organization: 'ĐH X', jobTitle: 'GV', nationality: 'VN', displayOrder: 1 }],
        supportMembers: [], operationalContact: { fullName: 'OP HN', organization: 'ĐH X', jobTitle: 'Trưởng phòng Hợp tác', phone: '+8493', email: 'op@x.vn', confirmationStatus: 'PENDING', confirmationSource: null, confirmedAt: null },
        currentHost: null, proposedHost: null,
        hostSelection: { canProposeSelfAsHost: false, canProposeOtherHost: false, canWaitForLaterAssignment: false, canUpdateProposedHost: false },
        workingLanguage: 'VI', transportationNote: null, mediaConsentStatus: 'DECLINED', notes: null,        formRevision: 2, approvalRevision: 1, rowVersion: 4, activeAmendment: null,
        cancelledByUserId: null, cancelledByName: null, cancelledAt: null,
        cancellationActorType: null, cancellationSource: null, cancellationReason: null,
      },
      {
        visitInstanceId: 11, campusId: 2, campusCode: 'HCM', campusName: 'FPTU HCM',
        plannedStartAt: '2026-08-02T13:00:00', plannedEndAt: '2026-08-02T15:00:00', timezone: 'Asia/Ho_Chi_Minh',
        instanceStatus: 'PENDING', currentHostUserId: null, currentHostName: null, decidedByUserId: null,
        decidedByName: null, decidedAt: null, decisionActorRole: null, decisionNote: null,
        delegationName: 'Đoàn HCM', visitType: 'WORKSHOP', visitTypeOther: null, purpose: 'MĐ HCM', workingContent: null,
        visitors: [{ guestMemberId: 2, memberType: 'VISITOR', fullName: 'Khách HCM', organization: 'ĐH Y', jobTitle: 'TS', nationality: 'VN', displayOrder: 1 }],
        supportMembers: [{ guestMemberId: 3, memberType: 'SUPPORT', fullName: 'HT HCM', organization: 'ĐH Y', jobTitle: 'TL', nationality: 'VN', displayOrder: 1 }],
        operationalContact: { fullName: 'OP HCM', organization: 'ĐH Y', jobTitle: 'Trưởng phòng Hợp tác', phone: '+8494', email: '', confirmationStatus: 'PENDING', confirmationSource: null, confirmedAt: null },
        currentHost: null, proposedHost: null,
        hostSelection: { canProposeSelfAsHost: false, canProposeOtherHost: false, canWaitForLaterAssignment: false, canUpdateProposedHost: false },
        workingLanguage: 'EN', transportationNote: 'xe 16 chỗ', mediaConsentStatus: 'AGREED', notes: 'ghi chú HCM',        formRevision: 3, approvalRevision: 2, rowVersion: 6, activeAmendment: null,
        cancelledByUserId: null, cancelledByName: null, cancelledAt: null,
        cancellationActorType: null, cancellationSource: null, cancellationReason: null,
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
    // "Ghi chú gửi FPTU" hydrates from THIS campus's stored value. It used to be hard-coded to ''
    // here, so opening an edit blanked the field and saving wrote that blank back over the note.
    expect(hn.notes).toBe('');            // this campus genuinely has none (server sent null)
    expect(hcm.notes).toBe('ghi chú HCM'); // this one does, and it survives the hydration
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

  it('maps partner + registrant request-level once', () => {
    const { values } = resolvedFormToV2Schema(resolved());
    expect(values.partnerSelectionMode).toBe('EXISTING_PARTNER');
    expect(values.partnerId).toBe(3);
    expect(values.registerInfo.email).toBe('reg@x.vn');

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

  // ── "Đầu mối là ai trong đoàn?" survives the round trip (NP-03) ──────────────

  it('restores the pick when the contact is one of the GUESTS', () => {
    const form = resolved();
    form.campusVisits[0].operationalContact.guestMemberId = 1;

    const { values } = resolvedFormToV2Schema(form);
    const hn = values.campusVisits[0];

    expect(hn.operationalContactClientMemberKey).toBe(hn.visitors[0].clientMemberKey);
    expect(hn.operationalContactClientMemberKey).toBeTruthy();
  });

  it('restores the pick when the contact is one of the SUPPORT staff', () => {
    // The previous version searched `visitors` only, so a contact who was the delegation's
    // interpreter reloaded as "chưa chọn" and the link was dropped on the next save.
    const form = resolved();
    form.campusVisits[1].operationalContact.guestMemberId = 3; // the HCM support member

    const { values } = resolvedFormToV2Schema(form);
    const hcm = values.campusVisits[1];

    expect(hcm.operationalContactClientMemberKey).toBe(hcm.supportTeam[0].clientMemberKey);
  });

  it('restores as "nobody" when the linked member is no longer in either list', () => {
    const form = resolved();
    form.campusVisits[0].operationalContact.guestMemberId = 9999;

    const { values } = resolvedFormToV2Schema(form);
    expect(values.campusVisits[0].operationalContactClientMemberKey).toBeNull();
  });

  it('gives every hydrated member its own key, and keeps campuses independent', () => {
    const { values } = resolvedFormToV2Schema(resolved());
    const keys = values.campusVisits.flatMap(cv =>
      [...cv.visitors, ...cv.supportTeam].map(m => m.clientMemberKey));

    expect(keys.every(Boolean)).toBe(true);
    expect(new Set(keys).size).toBe(keys.length);
  });

  it('sends the pick on to the edit payload, and drops one that names nobody', () => {
    const form = resolved();
    form.campusVisits[0].operationalContact.guestMemberId = 1;
    const { values, expectedRequestRowVersion } = resolvedFormToV2Schema(form);

    const payload = buildV2EditPayload(values, expectedRequestRowVersion);
    expect(payload.campusVisits[0].operationalContactClientMemberKey)
      .toBe(values.campusVisits[0].visitors[0].clientMemberKey);
    expect(payload.campusVisits[0].visitors[0].clientMemberKey)
      .toBe(values.campusVisits[0].visitors[0].clientMemberKey);

    // A key naming nobody is REFUSED by the backend, so a stale one must not be put on the wire —
    // that would turn a form the user has already corrected into a failed submit.
    values.campusVisits[0].operationalContactClientMemberKey = 'gone';
    expect(buildV2EditPayload(values, expectedRequestRowVersion)
      .campusVisits[0].operationalContactClientMemberKey).toBeNull();
  });

  it('accepts a support member as the pick on the wire', () => {
    const form = resolved();
    form.campusVisits[1].operationalContact.guestMemberId = 3;
    const { values, expectedRequestRowVersion } = resolvedFormToV2Schema(form);

    const payload = buildV2EditPayload(values, expectedRequestRowVersion);
    expect(payload.campusVisits[1].operationalContactClientMemberKey)
      .toBe(payload.campusVisits[1].externalSupportMembers[0].clientMemberKey);
  });
});

describe('withMemberKeys (restoring a draft written by an older build)', () => {
  it('mints an identity for every row that has none', () => {
    const stale = campus({ clientKey: 'ck', visitors: [
      { fullName: 'A', jobTitle: 'GV', organization: 'ĐH X', organizationPartnerId: null, nationality: 'VN' },
      { fullName: 'B', jobTitle: 'TS', organization: 'ĐH X', organizationPartnerId: null, nationality: 'VN' },
    ] });

    const healed = withMemberKeys(stale);
    const keys = healed.visitors.map(v => v.clientMemberKey);
    expect(keys.every(Boolean)).toBe(true);
    expect(new Set(keys).size).toBe(2);
  });

  it('translates a draft that still remembers the pick as an array INDEX', () => {
    // The last place that number is trusted. Read once, turned into the key of whatever row it
    // points at now, and never consulted again.
    const legacy = {
      ...campus({ clientKey: 'ck', visitors: [
        { fullName: 'A', jobTitle: 'GV', organization: 'ĐH X', organizationPartnerId: null, nationality: 'VN' },
        { fullName: 'B', jobTitle: 'TS', organization: 'ĐH X', organizationPartnerId: null, nationality: 'VN' },
      ] }),
      operationalContactVisitorIndex: 1,
    } as CampusVisitSchema;

    const healed = withMemberKeys(legacy);
    expect(healed.operationalContactClientMemberKey).toBe(healed.visitors[1].clientMemberKey);
  });

  it('leaves an existing identity alone — a re-minted key is as useless as an index', () => {
    const kept = campus({ clientKey: 'ck', visitors: [
      { clientMemberKey: 'stable-1', fullName: 'A', jobTitle: 'GV', organization: 'ĐH X', organizationPartnerId: null, nationality: 'VN' },
    ], operationalContactClientMemberKey: 'stable-1' });

    const healed = withMemberKeys(kept);
    expect(healed.visitors[0].clientMemberKey).toBe('stable-1');
    expect(healed.operationalContactClientMemberKey).toBe('stable-1');
  });

  it('drops a pick that names nobody in the restored lists', () => {
    const orphaned = campus({ clientKey: 'ck', operationalContactClientMemberKey: 'deleted-row' });
    expect(withMemberKeys(orphaned).operationalContactClientMemberKey).toBeNull();
  });
});

describe('resolveExactlyOne (plan CanhIter3FixBug — exact-one identity)', () => {
  const rows = [
    { clientMemberKey: 'a', name: 'A' },
    { clientMemberKey: 'b', name: 'B' },
    { clientMemberKey: 'b', name: 'B-duplicate' }, // should not be reachable in practice, but defensive
  ];

  it('resolves the single row that matches the key', () => {
    expect(resolveExactlyOne(rows, 'a')).toEqual({ clientMemberKey: 'a', name: 'A' });
  });

  it('returns null for zero matches', () => {
    expect(resolveExactlyOne(rows, 'nobody')).toBeNull();
  });

  it('returns null for a null/undefined key without scanning the rows', () => {
    expect(resolveExactlyOne(rows, null)).toBeNull();
    expect(resolveExactlyOne(rows, undefined)).toBeNull();
  });

  it('returns null — not the first match — when the key is ambiguous', () => {
    expect(resolveExactlyOne(rows, 'b')).toBeNull();
  });
});

describe('restoreCampusVisitFromDraft (plan CanhIter3FixBug — legacy draft source inference)', () => {
  const rawVisitor = (key?: string) => ({
    fullName: 'A', jobTitle: 'GV', organization: 'ĐH X', nationality: 'VN',
    ...(key ? { clientMemberKey: key } : {}),
  });

  it('preserves an explicit source field exactly, including explicit null — never re-infers it', () => {
    const raw = {
      clientKey: 'ck', visitors: [rawVisitor('kept-key')],
      operationalContactClientMemberKey: 'kept-key',
      operationalContactSource: null, // the user genuinely left it undecided under the NEW-format code
    };
    const result = restoreCampusVisitFromDraft(raw as any);
    expect(result.operationalContactSource).toBeNull();
  });

  it('a raw key that still resolves infers MEMBER', () => {
    const raw = {
      clientKey: 'ck', visitors: [rawVisitor('m1')],
      operationalContactClientMemberKey: 'm1',
    };
    const result = restoreCampusVisitFromDraft(raw as any);
    expect(result.operationalContactSource).toBe('MEMBER');
    expect(result.operationalContactClientMemberKey).toBe('m1');
  });

  it('a raw key that no longer resolves STILL infers MEMBER (never silently EXTERNAL)', () => {
    const raw = {
      clientKey: 'ck', visitors: [rawVisitor('someone-else')], // 'stale-key' names nobody here
      operationalContactClientMemberKey: 'stale-key',
    };
    const result = restoreCampusVisitFromDraft(raw as any);
    expect(result.operationalContactSource).toBe('MEMBER');
    expect(result.operationalContactClientMemberKey).toBeNull(); // repaired: key names nobody
  });

  it('a valid in-range legacy visitor index counts as member evidence', () => {
    const raw = {
      clientKey: 'ck', visitors: [rawVisitor(), rawVisitor()],
      operationalContactVisitorIndex: 1,
    };
    const result = restoreCampusVisitFromDraft(raw as any);
    expect(result.operationalContactSource).toBe('MEMBER');
  });

  it.each([-1, NaN, 2, 1.5])('an invalid legacy index (%s) is NOT member evidence', (legacyIndex) => {
    const raw = {
      clientKey: 'ck', visitors: [rawVisitor(), rawVisitor()],
      operationalContactVisitorIndex: legacyIndex,
    };
    const result = restoreCampusVisitFromDraft(raw as any);
    expect(result.operationalContactSource).not.toBe('MEMBER');
  });

  it('no key evidence but a filled contact snapshot infers EXTERNAL', () => {
    const raw = {
      clientKey: 'ck', visitors: [rawVisitor()],
      operationalContact: { fullName: 'Ngoài đoàn', organization: '', jobTitle: '', phone: '', email: '' },
    };
    const result = restoreCampusVisitFromDraft(raw as any);
    expect(result.operationalContactSource).toBe('EXTERNAL');
  });

  it('no key evidence and an empty contact snapshot infers null (not decided)', () => {
    const raw = { clientKey: 'ck', visitors: [rawVisitor()] };
    const result = restoreCampusVisitFromDraft(raw as any);
    expect(result.operationalContactSource).toBeNull();
  });

  it('still mints/repairs member keys like withMemberKeys did', () => {
    const raw = {
      clientKey: 'ck',
      visitors: [{ fullName: 'A', jobTitle: 'GV', organization: 'ĐH X', nationality: 'VN' }],
    };
    const result = restoreCampusVisitFromDraft(raw as any);
    expect(result.visitors[0].clientMemberKey).toBeTruthy();
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
