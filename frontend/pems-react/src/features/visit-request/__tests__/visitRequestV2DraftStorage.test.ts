import { describe, expect, it } from 'vitest';
import {
  clearVisitRequestV2Draft,
  loadVisitRequestV2Draft,
  loadVisitRequestV2DraftWithMigration,
  sanitizeV2Draft,
  saveVisitRequestV2Draft,
  V2_DRAFT_SCHEMA_VERSION,
} from '../utils/visitRequestV2DraftStorage';

import { createEmptyCampusVisit } from '../utils/visitRequestV2Form';
import type { VisitRequestV2Schema } from '../schema/visitRequestV2.schema';

const v2Data = (): Partial<VisitRequestV2Schema> => ({
  registerInfo: {
    fullName: 'Người ĐK', organization: 'ĐH X', jobTitle: 'TP',
    phone: '+84912345678', email: 'reg@example.com', nationality: 'VN',
  },
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



  // ── plan CanhIter3FixBug §14/§26 ────────────────────────────────────────────────────────────
  it('a campus whose only content is a chosen operational-contact source is still meaningful', () => {
    const data: Partial<VisitRequestV2Schema> = {
      campusVisits: [
        { ...createEmptyCampusVisit('ck'), operationalContactSource: 'EXTERNAL' },
      ],
    };
    expect(saveVisitRequestV2Draft(data).success).toBe(true);

    const memberOnly: Partial<VisitRequestV2Schema> = {
      campusVisits: [
        { ...createEmptyCampusVisit('ck2'), operationalContactSource: 'MEMBER' },
      ],
    };
    expect(saveVisitRequestV2Draft(memberOnly).success).toBe(true);

    // A fresh, still-undecided campus is genuinely empty — the baseline this guards against regressing.
    const untouched: Partial<VisitRequestV2Schema> = { campusVisits: [createEmptyCampusVisit('ck3')] };
    expect(saveVisitRequestV2Draft(untouched).success).toBe(false);
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
