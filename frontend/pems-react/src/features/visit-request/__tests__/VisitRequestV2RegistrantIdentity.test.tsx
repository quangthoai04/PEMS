import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

/**
 * "Tôi là người đăng ký" (plan §5.1) and the identity banner it feeds.
 *
 * The behaviours worth pinning are the ones that cost the user real work when they regress:
 * the button must never fire on its own (that is what silently overwrites a restored draft), the
 * banner must track the email field as it is edited rather than latching, and an internal member of
 * staff must be stopped from copying themselves into the contact block — the backend rejects that
 * payload outright, so letting them fill it in only to fail on submit is wasted typing.
 */

const authUser = vi.fn();
vi.mock('../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ user: authUser() }),
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
  useTranslation: () => ({ t: (key: string) => key, i18n: { language: 'vi' } }),
}));

import { VisitRequestFormV2 } from '../components/v2/VisitRequestFormV2';

const STAFF = {
  userId: 5, roleCode: 'STAFF', subRole: 'STAFF', campusCode: 'HN', email: 'staff.hn@fpt.edu.vn',
};
const VISITOR = {
  userId: 9, roleCode: 'VISITOR', subRole: null, campusCode: null, email: 'guest@partner.example.com',
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

const registrantEmail = () => screen.getByTestId('v2-registrant-email') as HTMLInputElement;
const field = (name: 'fullName' | 'phone' | 'jobTitle') =>
  screen.getByTestId(`v2-registrant-${name}`) as HTMLInputElement;

describe('VisitRequestFormV2 — registrant identity', () => {
  beforeEach(() => {
    authUser.mockReset();
    getMyProfile.mockReset();
    localStorage.clear();
  });

  it('does not fetch the profile or fill anything until the button is pressed', () => {
    authUser.mockReturnValue(STAFF);
    getMyProfile.mockResolvedValue(STAFF_PROFILE);

    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u5" onSuccess={vi.fn()} />);

    expect(getMyProfile).not.toHaveBeenCalled();
    expect(registrantEmail().value).toBe('');
  });

  it('fills the registrant block from the profile when pressed', async () => {
    authUser.mockReturnValue(STAFF);
    getMyProfile.mockResolvedValue(STAFF_PROFILE);

    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u5" onSuccess={vi.fn()} />);
    fireEvent.click(screen.getByTestId('v2-registrant-use-me'));

    await waitFor(() => expect(registrantEmail().value).toBe('staff.hn@fpt.edu.vn'));
    expect(field('fullName').value).toBe('Nguyễn Văn A');
    expect(field('phone').value).toBe('+84912345678');
    expect(field('jobTitle').value).toBe('Nhân viên');
  });

  it('leaves fields the profile has no value for blank instead of inventing one', async () => {
    authUser.mockReturnValue(VISITOR);
    getMyProfile.mockResolvedValue({
      ...STAFF_PROFILE,
      email: VISITOR.email,
      phone: null,
      nationality: null,
      displayPosition: null,          // a Visitor has no internal position
      displayDepartmentName: null,
      department: null,
      displayCampusName: null,
    });

    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u9" onSuccess={vi.fn()} />);
    fireEvent.click(screen.getByTestId('v2-registrant-use-me'));

    await waitFor(() => expect(registrantEmail().value).toBe(VISITOR.email));
    expect(field('jobTitle').value).toBe('');
    expect(field('phone').value).toBe('');
  });

  it('shows the no-OTP state once the email matches, and drops it the moment it is edited', async () => {
    authUser.mockReturnValue(STAFF);
    getMyProfile.mockResolvedValue(STAFF_PROFILE);

    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u5" onSuccess={vi.fn()} />);

    // Nothing typed yet — an empty field is nobody, so it is NOT treated as the signed-in user.
    expect(screen.getByTestId('v2-registrant-delegated')).toBeTruthy();

    fireEvent.click(screen.getByTestId('v2-registrant-use-me'));
    await waitFor(() => expect(screen.getByTestId('v2-registrant-self')).toBeTruthy());

    fireEvent.change(registrantEmail(), { target: { value: 'someone.else@partner.example.com' } });
    expect(screen.queryByTestId('v2-registrant-self')).toBeNull();
    expect(screen.getByTestId('v2-registrant-delegated')).toBeTruthy();
  });

  it('offers no autofill button on the public form', () => {
    authUser.mockReturnValue(null);
    render(<VisitRequestFormV2 mode="public" onSuccess={vi.fn()} />);

    expect(screen.queryByTestId('v2-registrant-use-me')).toBeNull();
    expect(screen.queryByTestId('v2-registrant-self')).toBeNull();
    expect(screen.queryByTestId('v2-registrant-delegated')).toBeNull();
  });

  // ── Primary contact copy rule (plan §7) ──────────────────────────────────────

  it('keeps the copy button for a staff member who is registering somebody else', async () => {
    // The rule is about the internal user being the CONTACT of their own delegation. Once the
    // registrant is an external guest, copying that guest's details across is legitimate.
    authUser.mockReturnValue(STAFF);

    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u5" onSuccess={vi.fn()} />);
    fireEvent.change(registrantEmail(), { target: { value: 'guest@partner.example.com' } });

    // The per-campus copy button follows whether the REGISTRANT block is usable — this test only
    // sets the email, so it stays off until a name is entered too.
    const copyButton = screen.getByTestId('campus-opcontact-use-registrant-0') as HTMLButtonElement;
    expect(copyButton.disabled).toBe(true);
    expect(screen.queryByText('visitRequestV2:sections.contactInternalNotAllowed')).toBeNull();
  });

  it('keeps the copy button for a Visitor registering themselves', async () => {
    authUser.mockReturnValue(VISITOR);
    getMyProfile.mockResolvedValue({ ...STAFF_PROFILE, email: VISITOR.email });

    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u9" onSuccess={vi.fn()} />);
    fireEvent.click(screen.getByTestId('v2-registrant-use-me'));

    await waitFor(() => expect(screen.getByTestId('v2-registrant-self')).toBeTruthy());
    expect((screen.getByTestId('campus-opcontact-use-registrant-0') as HTMLButtonElement).disabled).toBe(false);
  });
});
