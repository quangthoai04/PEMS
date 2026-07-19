import { describe, expect, it } from 'vitest';
import {
  clearVisitRequestV2Draft,
  loadVisitRequestV2Draft,
  loadVisitRequestV2DraftWithMigration,
  sanitizeV2Draft,
  saveVisitRequestV2Draft,
  V2_DRAFT_SCHEMA_VERSION,
} from '../utils/visitRequestV2DraftStorage';
import { saveVisitRequestDraft } from '../utils/visitRequestDraftStorage';
import { createEmptyCampusVisit } from '../utils/visitRequestV2Form';
import type { VisitRequestV2Schema } from '../schema/visitRequestV2.schema';

const v2Data = (): Partial<VisitRequestV2Schema> => ({
  registerInfo: {
    fullName: 'Người ĐK', organization: 'ĐH X', jobTitle: 'TP',
    phone: '+84912345678', email: 'reg@example.com', nationality: 'VN',
  },
  contactPoint: { fullName: 'ĐM', organization: '', phone: '+84987654321', email: 'c@example.com' },
  partnerSelectionMode: 'NEW_ORGANIZATION',
  partnerId: null,
  campusVisits: [
    { ...createEmptyCampusVisit('stable-key-1'), campus: 'HN', delegationName: 'Đoàn A' },
    { ...createEmptyCampusVisit('stable-key-2'), campus: 'DN', delegationName: 'Đoàn B' },
  ],
});

describe('visitRequestV2DraftStorage', () => {
  it('round-trips the per-campus draft with STABLE clientKeys and schema version', () => {
    const saved = saveVisitRequestV2Draft(v2Data());
    expect(saved.success).toBe(true);

    const loaded = loadVisitRequestV2Draft();
    expect(loaded).not.toBeNull();
    expect(loaded!.draftSchemaVersion).toBe(V2_DRAFT_SCHEMA_VERSION);
    expect(typeof loaded!.savedAt).toBe('number');
    expect(loaded!.data.campusVisits?.map(cv => cv.clientKey)).toEqual(['stable-key-1', 'stable-key-2']);
    expect(loaded!.data.campusVisits?.[1].delegationName).toBe('Đoàn B');
  });

  it('refuses to save an empty form and clears an expired draft on load', () => {
    expect(saveVisitRequestV2Draft({}).success).toBe(false);

    const saved = saveVisitRequestV2Draft(v2Data(), -1); // already expired
    expect(saved.success).toBe(true);
    expect(loadVisitRequestV2Draft()).toBeNull();
  });

  it('namespaces authenticated drafts per user', () => {
    saveVisitRequestV2Draft(v2Data(), undefined, 'user-a@example.com');
    expect(loadVisitRequestV2Draft('user-b@example.com')).toBeNull();
    expect(loadVisitRequestV2Draft('user-a@example.com')).not.toBeNull();
    clearVisitRequestV2Draft('user-a@example.com');
    expect(loadVisitRequestV2Draft('user-a@example.com')).toBeNull();
  });

  it('migrates the GLOBAL (v1-form) draft when no per-campus draft exists', () => {
    saveVisitRequestDraft({
      registerInfo: {
        fullName: 'Cũ', organization: 'ĐH Cũ', jobTitle: 'GV',
        phone: '+84911111111', email: 'old@example.com', nationality: 'VN',
      },
      delegationName: 'Đoàn Cũ',
      visits: [
        { campus: 'HN', startDatetime: '2026-08-10T08:00', endDatetime: '2026-08-10T11:00' },
        { campus: 'HCM', startDatetime: '2026-08-11T08:00', endDatetime: '2026-08-11T11:00' },
      ],
    });

    const { draft, migratedFromGlobalDraft } = loadVisitRequestV2DraftWithMigration();
    expect(migratedFromGlobalDraft).toBe(true);
    expect(draft).not.toBeNull();
    expect(draft!.data.campusVisits).toHaveLength(2);
    expect(draft!.data.campusVisits![0].delegationName).toBe('Đoàn Cũ');
    expect(draft!.data.campusVisits![1].delegationName).toBe('Đoàn Cũ');

    // In-memory only: nothing was written under the per-campus key…
    expect(loadVisitRequestV2Draft()).toBeNull();
  });

  it('NEVER lets the older global draft shadow an existing per-campus draft', () => {
    saveVisitRequestV2Draft(v2Data());
    saveVisitRequestDraft({ delegationName: 'Global mới hơn cũng mặc kệ', visits: [{ campus: 'CT', startDatetime: '2026-09-01T08:00', endDatetime: '2026-09-01T11:00' }] });

    const { draft, migratedFromGlobalDraft } = loadVisitRequestV2DraftWithMigration();
    expect(migratedFromGlobalDraft).toBe(false);
    expect(draft!.data.campusVisits?.[0].clientKey).toBe('stable-key-1'); // the v2 draft won
  });

  it('sanitize strips OTP/session/file material from whatever is persisted', () => {
    const dirty = {
      ...v2Data(),
      otpCode: 'OTP-SECRET-VALUE',
      sessionToken: 'SESSION-SECRET-VALUE',
      maskedEmail: 'x***@y.z',
      uploadedFile: 'base64…',
    } as Partial<VisitRequestV2Schema>;

    const clean = sanitizeV2Draft(dirty) as Record<string, unknown>;
    expect(clean.otpCode).toBeUndefined();
    expect(clean.sessionToken).toBeUndefined();
    expect(clean.maskedEmail).toBeUndefined();
    expect(clean.uploadedFile).toBeUndefined();
    expect((clean.campusVisits as unknown[]).length).toBe(2);

    saveVisitRequestV2Draft(dirty);
    const raw = localStorage.getItem('pems_visit_registration_draft_percampus') ?? '';
    expect(raw.length).toBeGreaterThan(0);
    expect(raw).not.toContain('SESSION-SECRET-VALUE');
    expect(raw).not.toContain('OTP-SECRET-VALUE');
    const persisted = JSON.parse(raw) as { data: Record<string, unknown> };
    expect(persisted.data.otpCode).toBeUndefined();
    expect(persisted.data.sessionToken).toBeUndefined();
  });
});
