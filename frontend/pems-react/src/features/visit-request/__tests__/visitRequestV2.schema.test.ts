import { describe, expect, it } from 'vitest';
import {
  buildVisitRequestV2Schema,
  buildCampusVisitSchema,
  buildPendingCampusEditSchema,
  V2_MAX_CAMPUSES,
  type VisitRequestV2Schema,
} from '../schema/visitRequestV2.schema';
import { createEmptyCampusVisit } from '../utils/visitRequestV2Form';

// Wall-clock strings far enough in the future that the 72h-advance rule can never flip
// the verdict regardless of the runner's timezone (parse is pinned to +07:00 anyway).
const futureAt = (extraMs = 0): string => {
  const d = new Date(Date.now() + 200 * 3600 * 1000 + extraMs);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
};

const t = (key: string) => key; // messages are irrelevant here — we assert paths

const validCampus = (campus = 'HN', startOffsetMs = 0, durationMs = 3 * 3600 * 1000) => ({
  ...createEmptyCampusVisit(`ck-${campus}-${startOffsetMs}`),
  campus,
  startDatetime: futureAt(startOffsetMs),
  endDatetime: futureAt(startOffsetMs + durationMs),
  delegationName: 'Đoàn Đại học ABC',
  visitType: 'MEETING' as const,
  purpose: 'Trao đổi hợp tác',
  workingContent: 'Nội dung làm việc chi tiết',
  visitors: [{ fullName: 'Nguyễn Văn A', jobTitle: 'Giảng viên', organization: 'ĐH ABC', nationality: 'Việt Nam' }],
  // All four operational-contact fields are required — this is the person the campus calls on the day.
  operationalContact: {
    fullName: 'Trần B', organization: 'ĐH ABC', jobTitle: 'Trưởng phòng Hợp tác', phone: '+84912345678', email: 'tranb@example.com',
  },
});

const validValues = (): VisitRequestV2Schema => ({
  registerInfo: {
    fullName: 'Người Đăng Ký',
    organization: 'ĐH ABC',
    jobTitle: 'Trưởng phòng',
    phone: '+84912345678',
    email: 'registrant@example.com',
    nationality: 'Việt Nam',
  },
  partnerSelectionMode: 'NEW_ORGANIZATION',
  partnerId: null,
  campusVisits: [validCampus('HN')],
});

describe('visitRequestV2 schema', () => {
  const schema = buildVisitRequestV2Schema(72, t);

  it('accepts a fully-resolved single-campus payload', () => {
    const result = schema.safeParse(validValues());
    expect(result.success).toBe(true);
  });

  it('accepts multiple campuses with DIFFERENT content (mixed request)', () => {
    const values = validValues();
    values.campusVisits = [
      validCampus('HN'),
      { ...validCampus('HCM', 24 * 3600 * 1000), delegationName: 'Đoàn khác hẳn', purpose: 'Mục đích khác' },
    ];
    expect(schema.safeParse(values).success).toBe(true);
  });

  it('rejects 29m59s and accepts exactly 30m (minute-level boundary, no auto-adjust)', () => {
    const values = validValues();
    values.campusVisits = [validCampus('HN', 0, 29 * 60 * 1000 + 59 * 1000)];
    const tooShort = schema.safeParse(values);
    expect(tooShort.success).toBe(false);
    if (!tooShort.success) {
      const issue = tooShort.error.issues.find(i => i.message === 'minDurationMinutes');
      expect(issue?.path).toEqual(['campusVisits', 0, 'endDatetime']);
    }

    values.campusVisits = [validCampus('HN', 0, 30 * 60 * 1000)];
    expect(schema.safeParse(values).success).toBe(true);
  });

  it('flags duplicate campuses on the LATER card (per-index path)', () => {
    const values = validValues();
    values.campusVisits = [validCampus('HN'), validCampus('HN', 24 * 3600 * 1000)];
    const result = schema.safeParse(values);
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find(i => i.message === 'duplicateCampus');
      expect(issue?.path).toEqual(['campusVisits', 1, 'campus']);
    }
  });

  it(`rejects more than ${V2_MAX_CAMPUSES} campuses`, () => {
    const values = validValues();
    values.campusVisits = Array.from({ length: V2_MAX_CAMPUSES + 1 }, (_, i) =>
      validCampus(`C${i}`, i * 24 * 3600 * 1000));
    expect(schema.safeParse(values).success).toBe(false);
  });

  it('requires visitTypeOther only when visitType is OTHER', () => {
    const values = validValues();
    values.campusVisits = [{ ...validCampus('HN'), visitType: 'OTHER' as const, visitTypeOther: '' }];
    const missing = schema.safeParse(values);
    expect(missing.success).toBe(false);
    if (!missing.success) {
      expect(missing.error.issues.some(i => i.message === 'visitTypeOtherRequired')).toBe(true);
    }

    values.campusVisits = [{ ...validCampus('HN'), visitType: 'OTHER' as const, visitTypeOther: 'Thăm lab' }];
    expect(schema.safeParse(values).success).toBe(true);
  });

  it('enforces the advance-hours threshold per campus', () => {
    const soon = new Date(Date.now() + 3600 * 1000); // 1h from now < 72h
    const p = (n: number) => String(n).padStart(2, '0');
    const fmt = (d: Date) =>
      `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
    const values = validValues();
    values.campusVisits = [{
      ...validCampus('HN'),
      startDatetime: fmt(soon),
      endDatetime: fmt(new Date(soon.getTime() + 3600 * 1000)),
    }];
    const result = schema.safeParse(values);
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues.some(i => i.message === 'startTimeMinAdvance')).toBe(true);
    }
  });

  // ── Operational-contact replay scope: an EXISTING campus's contact is read-only, and the backend's
  //    own split (OperationalContactV2Validator vs OperationalContactReplayV2Validator) never
  //    format-checks a replayed phone — only a FRESH one being written right now. ───────────────────

  it('does not format-check operational-contact phone on an EXISTING campus (visitInstanceId set)', () => {
    const campusSchema = buildCampusVisitSchema(72, t);
    const cv = { ...validCampus('HN'), visitInstanceId: 42 };
    cv.operationalContact = { ...cv.operationalContact, phone: '+8435352152512asdasdsadasd' };
    const result = campusSchema.safeParse(cv);
    expect(result.success).toBe(true);
  });

  it('still format-checks operational-contact phone on a NEW campus (visitInstanceId null)', () => {
    const campusSchema = buildCampusVisitSchema(72, t);
    const cv = { ...validCampus('HN'), visitInstanceId: null };
    cv.operationalContact = { ...cv.operationalContact, phone: '+8435352152512asdasdsadasd' };
    const result = campusSchema.safeParse(cv);
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues.some(i =>
        i.path.join('.') === 'operationalContact.phone' && i.message === 'phoneInvalidField')).toBe(true);
    }
  });

  it('an over-length operational-contact phone still fails on an EXISTING campus (structural bound survives)', () => {
    const campusSchema = buildCampusVisitSchema(72, t);
    const cv = { ...validCampus('HN'), visitInstanceId: 42 };
    cv.operationalContact = { ...cv.operationalContact, phone: '0'.repeat(51) };
    const result = campusSchema.safeParse(cv);
    expect(result.success).toBe(false);
  });

  // ── Short-notice floor (PEMS_INTERNAL_SELF_CREATE_SHORT_NOTICE_72H plan §8.1) ──
  // minAdvanceHours=0 is what the internal-self-registration hook computes; the ONLY rule left at
  // that floor is "must be in the future", never "at least 0 hours advance".

  it('minAdvanceHours=0 accepts a future start and rejects a past one', () => {
    const zeroSchema = buildVisitRequestV2Schema(0, t);
    const values = validValues();

    const soon = new Date(Date.now() + 5 * 60 * 1000); // 5 minutes from now
    const p = (n: number) => String(n).padStart(2, '0');
    const fmt = (d: Date) => `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
    values.campusVisits = [{
      ...validCampus('HN'),
      startDatetime: fmt(soon),
      endDatetime: fmt(new Date(soon.getTime() + 3600 * 1000)),
    }];
    expect(zeroSchema.safeParse(values).success).toBe(true);

    const past = new Date(Date.now() - 5 * 60 * 1000);
    values.campusVisits = [{
      ...validCampus('HN'),
      startDatetime: fmt(past),
      endDatetime: fmt(new Date(past.getTime() + 3600 * 1000)),
    }];
    const result = zeroSchema.safeParse(values);
    expect(result.success).toBe(false);
    if (!result.success) {
      // Never the "{{hours}}"-style message at 0 — that would misleadingly read "at least 0 hours".
      expect(result.error.issues.some(i => i.message === 'startTimeFutureOnly')).toBe(true);
      expect(result.error.issues.some(i => i.message === 'startTimeMinAdvance')).toBe(false);
    }
  });

  it('minAdvanceHours=0 still enforces the 30-minute minimum duration', () => {
    const zeroSchema = buildVisitRequestV2Schema(0, t);
    const values = validValues();
    values.campusVisits = [validCampus('HN', 5 * 60 * 1000, 29 * 60 * 1000 + 59 * 1000)];
    expect(zeroSchema.safeParse(values).success).toBe(false);

    values.campusVisits = [validCampus('HN', 5 * 60 * 1000, 30 * 60 * 1000)];
    expect(zeroSchema.safeParse(values).success).toBe(true);
  });

  it('buildPendingCampusEditSchema scopes to ONE campus and never validates registerInfo', () => {
    const pendingSchema = buildPendingCampusEditSchema(0, t);
    const cv = { ...validCampus('HN'), visitInstanceId: 42 };
    cv.operationalContact = { ...cv.operationalContact, phone: '+8435352152512asdasdsadasd' };
    // No registerInfo/partnerSelectionMode/partnerId at all — this screen never submits them.
    const result = pendingSchema.safeParse({ campusVisits: [cv] });
    expect(result.success).toBe(true);
  });
});
