import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

/**
 * The v2 form shipped with a complete per-campus contract (the payload mapper already carried a
 * `processing` block per campus) but the form never rendered any way to CHOOSE one — so every
 * authenticated create silently fell back to "send for review". These tests pin the wiring:
 * the authenticated form must offer the choice inside each campus card, and the public form
 * must never show internal processing at all.
 */

const authUser = vi.fn();
vi.mock('../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ user: authUser() }),
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

import { VisitRequestFormV2 } from '../components/v2/VisitRequestFormV2';

const STAFF_HN = { userId: 5, roleCode: 'STAFF', subRole: 'STAFF', campusCode: 'HN' };
const LEADER_HN = { userId: 6, roleCode: 'STAFF', subRole: 'LEADER', campusCode: 'HN' };
const VISITOR = { userId: 9, roleCode: 'VISITOR', subRole: null, campusCode: null };

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
  });

  it('offers a Staff the processing choice inside their own campus card', () => {
    authUser.mockReturnValue(STAFF_HN);
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u5" onSuccess={vi.fn()} />);

    selectFirstCampus('HN');

    expect(screen.getByTestId('campus-processing-SELF_HOST-HN')).toBeTruthy();
    expect(screen.getByTestId('campus-processing-SEND_FOR_REVIEW-HN')).toBeTruthy();
    // Assigning someone else is a Leader-only capability.
    expect(screen.queryByTestId('campus-processing-ASSIGN_HOST-HN')).toBeNull();
  });

  it('offers a Staff Leader the assign option too', () => {
    authUser.mockReturnValue(LEADER_HN);
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u6" onSuccess={vi.fn()} />);

    selectFirstCampus('HN');

    expect(screen.getByTestId('campus-processing-ASSIGN_HOST-HN')).toBeTruthy();
  });

  it('locks a campus outside the creator scope to a read-only routed notice', () => {
    authUser.mockReturnValue(LEADER_HN);
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u6" onSuccess={vi.fn()} />);

    selectFirstCampus('HCM');

    expect(screen.getByTestId('campus-processing-readonly-HCM')).toBeTruthy();
    expect(screen.queryByTestId('campus-processing-SELF_HOST-HCM')).toBeNull();
    expect(getCreateHostCandidates).not.toHaveBeenCalled();
  });

  it('never renders internal processing on the PUBLIC form', () => {
    authUser.mockReturnValue(null);
    render(<VisitRequestFormV2 mode="public" onSuccess={vi.fn()} />);

    selectFirstCampus('HN');

    expect(screen.queryByTestId('campus-processing-HN')).toBeNull();
    expect(screen.queryByTestId('campus-processing-SELF_HOST-HN')).toBeNull();
    expect(screen.queryByTestId('campus-processing-readonly-HN')).toBeNull();
  });

  it('never renders internal processing for an authenticated Visitor', () => {
    authUser.mockReturnValue(VISITOR);
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u9" onSuccess={vi.fn()} />);

    selectFirstCampus('HN');

    expect(screen.queryByTestId('campus-processing-SELF_HOST-HN')).toBeNull();
    expect(screen.queryByTestId('campus-processing-readonly-HN')).toBeNull();
  });
});
