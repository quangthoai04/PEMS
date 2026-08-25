import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

/**
 * Authenticated create is self-registration ONLY (plan CanhIter3FixBug). The registrant block is
 * profile-locked and read-only: no "Tôi là người đăng ký" button, no different-account banner, no
 * editable textbox a user could type somebody else into. The profile loads automatically the moment
 * auth has settled — there is no button to press and nothing renders it as an option.
 *
 * Public stays exactly as before: fully editable Registrant, OTP round trip, no profile involved.
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

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string, opts?: Record<string, unknown>) => (opts?.fields ? `${key}:${opts.fields}` : key), i18n: { language: 'vi' } }),
}));

import { VisitRequestFormV2 } from '../components/v2/VisitRequestFormV2';

const STAFF = {
  userId: 5, roleCode: 'STAFF', subRole: 'STAFF', campusCode: 'HN', email: 'staff.hn@fpt.edu.vn',
  effectiveRole: 'STAFF',
};
const STAFF_LEADER = {
  userId: 6, roleCode: 'STAFF', subRole: 'LEADER', campusCode: 'HN', email: 'leader.hn@fpt.edu.vn',
  effectiveRole: 'STAFF_LEADER',
};
const VISITOR = {
  userId: 9, roleCode: 'VISITOR', subRole: null, campusCode: null, email: 'guest@partner.example.com',
  effectiveRole: 'VISITOR',
};

const STAFF_PROFILE = {
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

describe('VisitRequestFormV2 — authenticated registrant is profile-locked and read-only', () => {
  beforeEach(() => {
    authUser.mockReset();
    getMyProfile.mockReset();
    localStorage.clear();
  });

  it('loads the profile automatically — no button, no click needed', async () => {
    authUser.mockReturnValue(STAFF);
    getMyProfile.mockResolvedValue(STAFF_PROFILE);

    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u5" onSuccess={vi.fn()} />);

    await waitFor(() => expect(getMyProfile).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect(screen.getByTestId('v2-registrant-readonly').textContent).toContain('Nguyễn Văn A'));
  });

  it('never renders the old "Tôi là người đăng ký" button or the self/delegated banner', async () => {
    authUser.mockReturnValue(STAFF);
    getMyProfile.mockResolvedValue(STAFF_PROFILE);

    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u5" onSuccess={vi.fn()} />);
    await waitFor(() => expect(screen.getByTestId('v2-registrant-readonly')).toBeTruthy());

    expect(screen.queryByTestId('v2-registrant-use-me')).toBeNull();
    expect(screen.queryByTestId('v2-registrant-self')).toBeNull();
    expect(screen.queryByTestId('v2-registrant-delegated')).toBeNull();
  });

  it('renders no editable registrant textbox — nothing the user could type a different person into', async () => {
    authUser.mockReturnValue(STAFF);
    getMyProfile.mockResolvedValue(STAFF_PROFILE);

    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u5" onSuccess={vi.fn()} />);
    await waitFor(() => expect(screen.getByTestId('v2-registrant-readonly')).toBeTruthy());

    expect(screen.queryByTestId('v2-registrant-fullName')).toBeNull();
    expect(screen.queryByTestId('v2-registrant-email')).toBeNull();
    expect(screen.queryByTestId('v2-registrant-jobTitle')).toBeNull();
    expect(screen.queryByTestId('v2-registrant-phone')).toBeNull();
  });

  it('shows a loading state while the profile round trip is in flight, then the summary', async () => {
    authUser.mockReturnValue(STAFF);
    let resolveProfile: (value: typeof STAFF_PROFILE) => void = () => {};
    getMyProfile.mockReturnValue(new Promise(resolve => { resolveProfile = resolve; }));

    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u5" onSuccess={vi.fn()} />);

    expect(screen.getByTestId('v2-registrant-loading')).toBeTruthy();
    expect(screen.queryByTestId('v2-registrant-readonly')).toBeNull();
    expect(screen.queryByTestId('v2-submit')).toBeNull();

    resolveProfile(STAFF_PROFILE);
    await waitFor(() => expect(screen.getByTestId('v2-registrant-readonly')).toBeTruthy());
    expect(screen.queryByTestId('v2-registrant-loading')).toBeNull();
  });

  it('maps fullName/email/phone/jobTitle/organization/nationality straight from the canonical profile fields', async () => {
    authUser.mockReturnValue(STAFF_LEADER);
    getMyProfile.mockResolvedValue({
      ...STAFF_PROFILE,
      email: STAFF_LEADER.email,
      displayPosition: 'Trưởng phòng',
    });

    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u6" onSuccess={vi.fn()} />);

    const summary = await waitFor(() => screen.getByTestId('v2-registrant-readonly'));
    expect(summary.textContent).toContain('Nguyễn Văn A');
    expect(summary.textContent).toContain('Trưởng phòng');
    expect(summary.textContent).toContain('Phòng Hợp tác Quốc tế');
    expect(summary.textContent).toContain(STAFF_LEADER.email);
    expect(summary.textContent).toContain('+84912345678');
    expect(summary.textContent).toContain('VN');
  });

  // ── Visitor exception (plan §6): org/jobTitle have no profile source, so they are NOT part of
  // the "profile incomplete" gate and stay editable — everything else (identity) is still locked. ──

  it('a Visitor with no organization/jobTitle in their profile is NOT blocked — those two stay editable instead', async () => {
    authUser.mockReturnValue(VISITOR);
    getMyProfile.mockResolvedValue({
      ...STAFF_PROFILE,
      email: VISITOR.email,
      phone: null,
      displayPosition: null,
      displayDepartmentName: null,
      department: null,
      displayCampusName: null,
    });

    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u9" onSuccess={vi.fn()} />);
    const summary = await waitFor(() => screen.getByTestId('v2-registrant-readonly'));
    expect(summary.textContent).toContain(VISITOR.email);
    expect(screen.queryByTestId('v2-profile-incomplete')).toBeNull();
    // Identity is still locked — no editable name/email box…
    expect(screen.queryByTestId('v2-registrant-fullName')).toBeNull();
    expect(screen.queryByTestId('v2-registrant-email')).toBeNull();
    // …but organization/jobTitle ARE editable, starting blank (never fabricated).
    expect((screen.getByTestId('v2-registrant-jobTitle') as HTMLInputElement).value).toBe('');
    fireEvent.change(screen.getByTestId('v2-registrant-jobTitle'), { target: { value: 'Giảng viên' } });
    expect((screen.getByTestId('v2-registrant-jobTitle') as HTMLInputElement).value).toBe('Giảng viên');
  });

  it('a Staff/Staff Leader profile with no organization IS blocked — org/jobTitle are a fixed HR attribute for them', async () => {
    authUser.mockReturnValue(STAFF);
    getMyProfile.mockResolvedValue({
      ...STAFF_PROFILE, displayDepartmentName: null, department: null, displayCampusName: null,
    });

    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u5" onSuccess={vi.fn()} />);
    await waitFor(() => expect(screen.getByTestId('v2-profile-incomplete')).toBeTruthy());
  });

  // ── Profile round trip failure states ──────────────────────────────────────────────────────────

  it('shows a clear error with a Retry when the profile fails to load — never an empty editable form', async () => {
    authUser.mockReturnValue(STAFF);
    getMyProfile.mockRejectedValueOnce(new Error('network down'));

    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u5" onSuccess={vi.fn()} />);

    await waitFor(() => expect(screen.getByTestId('v2-profile-error')).toBeTruthy());
    expect(screen.queryByTestId('v2-registrant-readonly')).toBeNull();
    expect(screen.queryByTestId('v2-registrant-fullName')).toBeNull();
    expect(screen.queryByTestId('v2-submit')).toBeNull();

    getMyProfile.mockResolvedValueOnce(STAFF_PROFILE);
    fireEvent.click(screen.getByTestId('v2-profile-retry'));

    await waitFor(() => expect(screen.getByTestId('v2-registrant-readonly')).toBeTruthy());
  });

  it('blocks with a profile-incomplete notice — never a fabricated default — when a required field is missing', async () => {
    authUser.mockReturnValue(STAFF);
    getMyProfile.mockResolvedValue({ ...STAFF_PROFILE, nationality: null });

    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u5" onSuccess={vi.fn()} />);

    await waitFor(() => expect(screen.getByTestId('v2-profile-incomplete')).toBeTruthy());
    expect(screen.queryByTestId('v2-registrant-readonly')).toBeNull();
    expect(screen.queryByTestId('v2-submit')).toBeNull();
    // No editable escape hatch for the missing field.
    expect(screen.queryByTestId('v2-registrant-fullName')).toBeNull();
    // `t` is mocked to echo the key (see the react-i18next mock above), so the missing-field label
    // reads back as its own translation key rather than real Vietnamese text — still proof enough
    // that THIS field (nationality) is the one named, and not organization/jobTitle/email.
    expect(screen.getByTestId('v2-profile-incomplete').textContent).toContain('registrant.nationality');
    expect(screen.getByTestId('v2-profile-goto')).toBeTruthy();
  });

  // ── Public form: unchanged ───────────────────────────────────────────────────────────────────

  it('public mode stays fully editable, with no profile fetch and no read-only summary', () => {
    authUser.mockReturnValue(null);
    render(<VisitRequestFormV2 mode="public" onSuccess={vi.fn()} />);

    expect(getMyProfile).not.toHaveBeenCalled();
    expect(screen.queryByTestId('v2-registrant-readonly')).toBeNull();
    expect(screen.queryByTestId('v2-registrant-use-me')).toBeNull();
    expect((screen.getByTestId('v2-registrant-email') as HTMLInputElement).value).toBe('');

    fireEvent.change(screen.getByTestId('v2-registrant-fullName'), { target: { value: 'Ai đó' } });
    expect((screen.getByTestId('v2-registrant-fullName') as HTMLInputElement).value).toBe('Ai đó');
  });
});
