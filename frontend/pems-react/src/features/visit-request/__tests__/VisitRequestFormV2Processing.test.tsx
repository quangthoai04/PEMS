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

const STAFF_HN = { userId: 5, roleCode: 'STAFF', subRole: 'STAFF', campusCode: 'HN', email: 'staff.hn@fpt.edu.vn' };
const LEADER_HN = { userId: 6, roleCode: 'STAFF', subRole: 'LEADER', campusCode: 'HN', email: 'leader.hn@fpt.edu.vn' };
const VISITOR = { userId: 9, roleCode: 'VISITOR', subRole: null, campusCode: null, email: 'visitor@example.com' };

/**
 * Types an address into the registrant email field. Processing controls exist ONLY on a
 * self-registration, so every processing assertion has to state whose name the form is in —
 * otherwise it is asserting against an empty email, which is nobody.
 */
function typeRegistrantEmail(value: string) {
  fireEvent.change(screen.getByTestId('v2-registrant-email'), { target: { value } });
}

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

    typeRegistrantEmail(STAFF_HN.email);
    selectFirstCampus('HN');

    expect(screen.getByTestId('campus-processing-SELF_HOST-HN')).toBeTruthy();
    expect(screen.getByTestId('campus-processing-SEND_FOR_REVIEW-HN')).toBeTruthy();
    // Assigning someone else is a Leader-only capability.
    expect(screen.queryByTestId('campus-processing-ASSIGN_HOST-HN')).toBeNull();
  });

  it('offers a Staff Leader the assign option too', () => {
    authUser.mockReturnValue(LEADER_HN);
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u6" onSuccess={vi.fn()} />);

    typeRegistrantEmail(LEADER_HN.email);
    selectFirstCampus('HN');

    expect(screen.getByTestId('campus-processing-ASSIGN_HOST-HN')).toBeTruthy();
  });

  it('recognises the signed-in account regardless of case and padding', () => {
    authUser.mockReturnValue(STAFF_HN);
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u5" onSuccess={vi.fn()} />);

    typeRegistrantEmail('  STAFF.HN@FPT.EDU.VN  ');
    selectFirstCampus('HN');

    expect(screen.getByTestId('v2-registrant-self')).toBeTruthy();
    expect(screen.getByTestId('campus-processing-SELF_HOST-HN')).toBeTruthy();
  });

  it('locks a campus outside the creator scope to a read-only routed notice', () => {
    authUser.mockReturnValue(LEADER_HN);
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u6" onSuccess={vi.fn()} />);

    typeRegistrantEmail(LEADER_HN.email);
    selectFirstCampus('HCM');

    expect(screen.getByTestId('campus-processing-readonly-HCM')).toBeTruthy();
    expect(screen.queryByTestId('campus-processing-SELF_HOST-HCM')).toBeNull();
    expect(getCreateHostCandidates).not.toHaveBeenCalled();
  });

  // ── Delegated submissions carry no processing at all ──────────────────────────

  it('hides every processing control once the registrant is somebody else', () => {
    authUser.mockReturnValue(LEADER_HN);
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u6" onSuccess={vi.fn()} />);

    typeRegistrantEmail(LEADER_HN.email);
    selectFirstCampus('HN');
    expect(screen.getByTestId('campus-processing-SELF_HOST-HN')).toBeTruthy();

    // The Leader retypes the registrant as an external guest — this is now a delegated submission.
    typeRegistrantEmail('guest@partner.example.com');

    expect(screen.queryByTestId('campus-processing-SELF_HOST-HN')).toBeNull();
    expect(screen.queryByTestId('campus-processing-ASSIGN_HOST-HN')).toBeNull();
    // Not even the read-only routed notice: the whole panel belongs to self-registration.
    expect(screen.queryByTestId('campus-processing-readonly-HN')).toBeNull();
    expect(screen.getByTestId('v2-registrant-delegated')).toBeTruthy();
  });

  it('treats a plus-alias of the signed-in address as somebody else', () => {
    // Identity is trim + lower-case only: folding "+alias" would let the account act as an address
    // it has never proven it controls.
    authUser.mockReturnValue(STAFF_HN);
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace="u5" onSuccess={vi.fn()} />);

    typeRegistrantEmail('staff.hn+delegated@fpt.edu.vn');
    selectFirstCampus('HN');

    expect(screen.getByTestId('v2-registrant-delegated')).toBeTruthy();
    expect(screen.queryByTestId('campus-processing-SELF_HOST-HN')).toBeNull();
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

    // Self-registration — so the absence of controls is about the ROLE, not the identity gate.
    typeRegistrantEmail(VISITOR.email);
    selectFirstCampus('HN');

    expect(screen.queryByTestId('campus-processing-SELF_HOST-HN')).toBeNull();
    expect(screen.queryByTestId('campus-processing-readonly-HN')).toBeNull();
  });
});
