import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

/**
 * The v2 form shipped with a complete per-campus contract (the payload mapper already carried a
 * `processing` block per campus) but the form never rendered any way to CHOOSE one — so every
 * authenticated create silently fell back to "send for review". These tests pin the wiring:
 * the authenticated form must offer the choice inside each campus card, and the public form
 * must never show internal processing at all.
 *
 * Authenticated create is self-registration ONLY (plan CanhIter3FixBug) — there is no more delegated
 * state for the panel to hide under. Every authenticated test here waits for the auto-loaded profile
 * before touching a campus card, since the campus section does not render until it has settled.
 */

const authUser = vi.fn();
vi.mock('../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ user: authUser(), isReady: true, effectiveRole: authUser()?.effectiveRole ?? null }),
}));

vi.mock('../hooks/useRegistrationCampuses', () => ({
  useRegistrationCampuses: () => ({
    campuses: [
      { campusCode: 'HN', campusName: 'Hòa Lạc' },
      { campusCode: 'HCM', campusName: 'TP. Hồ Chí Minh' },
    ],
    loading: false,
  }),
}));

const getCreateHostCandidates = vi.fn().mockResolvedValue([]);
vi.mock('../api/visitRequestApi', () => ({
  visitRequestApi: {
    getCreateHostCandidates: (...a: unknown[]) => getCreateHostCandidates(...a),
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

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key, i18n: { language: 'vi' } }),
}));

const getMyProfile = vi.fn();
vi.mock('../../profile/api/profileApi', () => ({
  profileApi: { getMyProfile: (...a: unknown[]) => getMyProfile(...a) },
}));

import { VisitRequestFormV2 } from '../components/v2/VisitRequestFormV2';

const STAFF_HN = {
  userId: 5, roleCode: 'STAFF', subRole: 'STAFF', campusCode: 'HN', email: 'staff.hn@fpt.edu.vn',
  effectiveRole: 'STAFF',
};
const LEADER_HN = {
  userId: 6, roleCode: 'STAFF', subRole: 'LEADER', campusCode: 'HN', email: 'leader.hn@fpt.edu.vn',
  effectiveRole: 'STAFF_LEADER',
};
const VISITOR = {
  userId: 9, roleCode: 'VISITOR', subRole: null, campusCode: null, email: 'visitor@example.com',
  effectiveRole: 'VISITOR',
};

const profileFor = (user: typeof STAFF_HN) => ({
  userId: user.userId,
  fullName: 'Người Test',
  email: user.email,
  phone: '+84912345678',
  nationality: 'VN',
  displayPosition: user.roleCode === 'STAFF' ? (user.subRole === 'LEADER' ? 'Trưởng phòng' : 'Nhân viên') : null,
  displayDepartmentName: user.roleCode === 'STAFF' ? 'Phòng Hợp tác Quốc tế' : null,
  displayCampusName: 'Hòa Lạc',
  department: user.roleCode === 'STAFF' ? { departmentId: 1, name: 'Phòng Hợp tác Quốc tế', departmentType: 'IC' } : null,
});

/** Waits for the auto-loaded profile to settle and the registrant summary to render. */
const waitForReady = () => waitFor(() => expect(screen.getByTestId('v2-registrant-readonly')).toBeTruthy());

/**
 * Picks a campus in the first (only) card, which is what reveals its processing panel. Targets the
 * native campus <select> by its option set — the form also renders react-select comboboxes for
 * organization/nationality, so "the first combobox" is not a stable handle.
 */
function selectFirstCampus(code: string) {
  const campusSelect = screen.getAllByRole('combobox').find(el =>
    el.tagName === 'SELECT'
    && Array.from((el as HTMLSelectElement).options).some(o => o.value === code));
  if (!campusSelect) throw new Error(`No campus <select> offering "${code}"`);
  fireEvent.change(campusSelect, { target: { value: code } });
}

describe('VisitRequestFormV2 — per-campus processing wiring', () => {
  beforeEach(() => {
    authUser.mockReset();
    getCreateHostCandidates.mockClear();
    getMyProfile.mockReset();
  });

  it('offers a Staff the processing choice inside their own campus card', async () => {
    authUser.mockReturnValue(STAFF_HN);
    getMyProfile.mockResolvedValue(profileFor(STAFF_HN));
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u5" onSuccess={vi.fn()} />);
    await waitForReady();

    selectFirstCampus('HN');

    expect(screen.getByTestId('campus-host-selection-SELF-HN')).toBeTruthy();
    expect(screen.getByTestId('campus-host-selection-WAIT_FOR_LATER-HN')).toBeTruthy();
    // Assigning someone else is a Leader-only capability.
    expect(screen.queryByTestId('campus-host-selection-SELECTED-HN')).toBeNull();
  });

  it('offers a Staff Leader the assign option too', async () => {
    authUser.mockReturnValue(LEADER_HN);
    getMyProfile.mockResolvedValue(profileFor(LEADER_HN));
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u6" onSuccess={vi.fn()} />);
    await waitForReady();

    selectFirstCampus('HN');

    expect(screen.getByTestId('campus-host-selection-SELECTED-HN')).toBeTruthy();
  });

  it('locks a campus outside the creator scope to a read-only routed notice', async () => {
    authUser.mockReturnValue(LEADER_HN);
    getMyProfile.mockResolvedValue(profileFor(LEADER_HN));
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u6" onSuccess={vi.fn()} />);
    await waitForReady();

    selectFirstCampus('HCM');

    expect(screen.getByTestId('campus-host-selection-readonly-HCM')).toBeTruthy();
    expect(screen.queryByTestId('campus-host-selection-SELF-HCM')).toBeNull();
    expect(getCreateHostCandidates).not.toHaveBeenCalled();
  });

  it('never renders internal processing on the PUBLIC form', () => {
    authUser.mockReturnValue(null);
    render(<VisitRequestFormV2 mode="public" onSuccess={vi.fn()} />);

    selectFirstCampus('HN');

    expect(screen.queryByTestId('campus-host-selection-HN')).toBeNull();
    expect(screen.queryByTestId('campus-host-selection-SELF-HN')).toBeNull();
    expect(screen.queryByTestId('campus-host-selection-readonly-HN')).toBeNull();
  });

  it('never renders internal processing for an authenticated Visitor', async () => {
    authUser.mockReturnValue(VISITOR);
    getMyProfile.mockResolvedValue(profileFor(VISITOR));
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u9" onSuccess={vi.fn()} />);
    await waitForReady();

    selectFirstCampus('HN');

    expect(screen.queryByTestId('campus-host-selection-SELF-HN')).toBeNull();
    expect(screen.queryByTestId('campus-host-selection-readonly-HN')).toBeNull();
  });
});
