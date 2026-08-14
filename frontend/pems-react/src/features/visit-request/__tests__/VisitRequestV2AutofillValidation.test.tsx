import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

/**
 * NP-02 — an error must not outlive the problem it describes.
 *
 * <p>The reported sequence: a Staff Leader presses "Tôi là người đăng ký", their profile has no
 * country on it, the form immediately shows "Quốc tịch không được để trống", they pick a country —
 * and the red message stays.</p>
 *
 * <p>Two independent causes, both fixed:</p>
 * <ol>
 *   <li>The autofill called `setValue(..., { shouldValidate: true })` unconditionally, so it accused
 *   the profile of being incomplete before the user had submitted anything.</li>
 *   <li>The form runs `mode: 'onSubmit' / reValidateMode: 'onChange'`, so before the first submit
 *   NOTHING revalidates on change — and `CountrySelect` is a custom control that only calls
 *   `field.onChange`, so not even RHF's own input handling ran. The error had nothing to clear it.</li>
 * </ol>
 *
 * <p>Fixing only the first would leave a mapped server error or a manual `setError` stuck in exactly
 * the same way, which is why both are pinned here.</p>
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

import { VisitRequestFormV2 } from '../components/v2/VisitRequestFormV2';

const STAFF = {
  userId: 5, roleCode: 'STAFF', subRole: 'STAFF', campusCode: 'HN', email: 'staff.hn@fpt.edu.vn',
};

/** A real profile with the one gap that triggered the report: no country on file. */
const PROFILE_WITHOUT_COUNTRY = {
  userId: 5,
  fullName: 'Nguyễn Văn A',
  email: 'staff.hn@fpt.edu.vn',
  phone: '+84912345678',
  nationality: null,
  displayPosition: 'Nhân viên',
  displayDepartmentName: 'Phòng Hợp tác Quốc tế',
  displayCampusName: 'Hòa Lạc',
  department: { departmentId: 1, name: 'Phòng Hợp tác Quốc tế', departmentType: 'IC' },
};

/** Any inline error message currently rendered anywhere in the registrant block. */
const inlineErrors = () =>
  Array.from(document.querySelectorAll('p'))
    .map(p => p.textContent ?? '')
    .filter(text => /không được để trống|required/i.test(text));

describe('NP-02: autofill does not accuse the user before they submit', () => {
  beforeEach(() => {
    authUser.mockReset();
    getMyProfile.mockReset();
    localStorage.clear();
    authUser.mockReturnValue(STAFF);
    getMyProfile.mockResolvedValue(PROFILE_WITHOUT_COUNTRY);
  });

  it('writes no inline error when the profile is missing a required field', async () => {
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="np02-a" onSuccess={vi.fn()} />);

    fireEvent.click(screen.getByTestId('v2-registrant-use-me'));

    // The autofill DID run — the fields it could fill are filled…
    await waitFor(() =>
      expect((screen.getByTestId('v2-registrant-fullName') as HTMLInputElement).value)
        .toBe('Nguyễn Văn A'));
    // …and it did NOT turn the gaps it left behind into accusations. (Reverting the autofill to
    // `shouldValidate: true` puts "Nationality is required" here, which is the reported bug.)
    expect(inlineErrors()).toEqual([]);
  });

  it('leaves the field itself empty rather than inventing a value', async () => {
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="np02-b" onSuccess={vi.fn()} />);

    fireEvent.click(screen.getByTestId('v2-registrant-use-me'));

    await waitFor(() =>
      expect((screen.getByTestId('v2-registrant-fullName') as HTMLInputElement).value)
        .toBe('Nguyễn Văn A'));
    // Padding the country with a placeholder would be worse than leaving it blank: the user would
    // submit somebody's nationality without ever having been asked for it.
    expect((screen.getByTestId('v2-registrant-jobTitle') as HTMLInputElement).value)
      .toBe('Nhân viên');
  });
});
