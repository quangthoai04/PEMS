import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';

/**
 * NP-02 — an error must not outlive the problem it describes, continued under the new business rule
 * (plan CanhIter3FixBug).
 *
 * <p>The ORIGINAL report: a Staff Leader pressed "Tôi là người đăng ký", their profile had no country
 * on it, the form immediately showed "Quốc tịch không được để trống", they picked a country — and the
 * red message stayed. That flow (manual autofill into an editable form) no longer exists: the
 * registrant is now profile-locked and read-only, loaded automatically, and a profile missing a
 * REQUIRED field is a hard block (a dedicated notice, §7/§20) rather than an editable form with a
 * stuck error. This file now pins the two things that replaced it:</p>
 * <ol>
 *   <li>a COMPLETE profile auto-loads with zero inline validation errors before the user has
 *   attempted anything — the automatic load must not validate any more than the old manual click
 *   did;</li>
 *   <li>an INCOMPLETE profile blocks with the dedicated notice, never a stuck error on a field that
 *   does not even render.</li>
 * </ol>
 */

const authUser = vi.fn();
vi.mock('../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ user: authUser(), isReady: true, effectiveRole: authUser()?.effectiveRole ?? null }),
}));

vi.mock('../hooks/useRegistrationCampuses', () => ({
  useRegistrationCampuses: () => ({
    campuses: [{ campusCode: 'HN', campusName: 'Hòa Lạc' }],
    loading: false,
  }),
}));

const getMyProfile = vi.fn();
vi.mock('../../profile/api/profileApi', () => ({
  profileApi: { getMyProfile: (...a: unknown[]) => getMyProfile(...a) },
}));

vi.mock('../api/visitRequestApi', () => ({
  visitRequestApi: {
    getCreateHostCandidates: vi.fn().mockResolvedValue([]),
    initiate: vi.fn(),
    resendOtp: vi.fn(),
    recoverOtp: vi.fn(),
  },
}));

vi.mock('../api/visitRequestV2Api', () => ({
  createVisitRequestV2: vi.fn(),
  initiateVisitRequestV2: vi.fn(),
  verifyAndCreateVisitRequestV2: vi.fn(),
}));

import { VisitRequestFormV2 } from '../components/v2/VisitRequestFormV2';

const STAFF = {
  userId: 5, roleCode: 'STAFF', subRole: 'STAFF', campusCode: 'HN', email: 'staff.hn@fpt.edu.vn',
  effectiveRole: 'STAFF',
};

const COMPLETE_PROFILE = {
  userId: 5,
  fullName: 'Nguyễn Văn A',
  email: 'staff.hn@fpt.edu.vn',
  phone: '+84912345678',
  nationality: 'VN',
  displayPosition: 'Nhân viên',
  displayDepartmentName: 'Phòng Hợp tác Quốc tế',
  displayCampusName: 'Hòa Lạc',
  department: { departmentId: 1, name: 'Phòng Hợp tác Quốc tế', departmentType: 'IC' },
};

/** A real profile with the one gap that triggered the original report: no country on file. */
const PROFILE_WITHOUT_COUNTRY = { ...COMPLETE_PROFILE, nationality: null };

/**
 * Any FIELD-level validation error message currently rendered — excludes the dedicated
 * profile-incomplete NOTICE (`v2-profile-incomplete`), which legitimately says "required" as part of
 * its own intentional UI and is a different thing from a stuck field error.
 */
const inlineErrors = () =>
  Array.from(document.querySelectorAll('p'))
    .filter(p => !p.closest('[data-testid="v2-profile-incomplete"]'))
    .map(p => p.textContent ?? '')
    .filter(text => /không được để trống|required/i.test(text));

describe('NP-02, continued: the automatic profile load never validates prematurely', () => {
  beforeEach(() => {
    authUser.mockReset();
    getMyProfile.mockReset();
    localStorage.clear();
    authUser.mockReturnValue(STAFF);
  });

  it('a complete profile loads with no inline error anywhere on the form', async () => {
    getMyProfile.mockResolvedValue(COMPLETE_PROFILE);
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="np02-a" onSuccess={vi.fn()} />);

    await waitFor(() =>
      expect(screen.getByTestId('v2-registrant-readonly').textContent).toContain('Nguyễn Văn A'));
    expect(inlineErrors()).toEqual([]);
  });

  it('an incomplete profile (missing nationality) blocks with the dedicated notice — not a stuck field error', async () => {
    getMyProfile.mockResolvedValue(PROFILE_WITHOUT_COUNTRY);
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="np02-b" onSuccess={vi.fn()} />);

    await waitFor(() => expect(screen.getByTestId('v2-profile-incomplete')).toBeTruthy());
    // The old bug's shape — a red "Quốc tịch không được để trống" surviving a fix — cannot recur:
    // there is no field for it to be attached to any more.
    expect(inlineErrors()).toEqual([]);
    expect(screen.queryByTestId('v2-registrant-readonly')).toBeNull();
  });
});
