import { describe, expect, it } from 'vitest';
import {
  buildVisitRequestV2Schema,
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
});
