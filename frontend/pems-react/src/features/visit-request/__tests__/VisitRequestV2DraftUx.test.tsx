import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import {
  saveVisitRequestV2Draft,
  loadVisitRequestV2Draft,
} from '../utils/visitRequestV2DraftStorage';

/**
 * The draft MECHANISM (debounced autosave, namespacing, TTL) already existed; what was missing was
 * the part the user can see. v1 asked before touching your work in both directions — on open
 * ("restore or discard?") and on close ("save, keep editing, or throw away") — and v2 silently
 * hydrated instead, so a stale draft could overwrite a form with no way to refuse.
 */

vi.mock('../hooks/useRegistrationCampuses', () => ({
  useRegistrationCampuses: () => ({
    campuses: [{ campusCode: 'HN', campusName: 'Hòa Lạc' }],
    loading: false,
  }),
}));

vi.mock('../../../shared/auth/AuthContext', () => ({ useAuthContext: () => ({ user: null }) }));

vi.mock('../api/visitRequestApi', () => ({
  visitRequestApi: {
    getCreateHostCandidates: vi.fn().mockResolvedValue([]),
    initiate: vi.fn(), resendOtp: vi.fn(), recoverOtp: vi.fn(),
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

const NS = 'draft-ux-test';

const seedDraft = () =>
  saveVisitRequestV2Draft(
    {
      registerInfo: {
        fullName: 'Người Nháp', organization: 'ĐH Nháp', jobTitle: 'TP',
        phone: '+84912345678', email: 'draft@example.com', nationality: 'VN',
      },
    } as never,
    undefined,
    NS,
  );

const renderForm = () =>
  render(<VisitRequestFormV2 mode="public" draftNamespace={NS} onSuccess={vi.fn()} />);

describe('v2 draft UX', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  it('offers the draft instead of applying it silently', () => {
    seedDraft();
    renderForm();

    expect(screen.getByTestId('v2-draft-prompt')).toBeTruthy();
    // The form is still untouched until the user chooses.
    const name = screen.getAllByRole('textbox')[0] as HTMLInputElement;
    expect(name.value).toBe('');
  });

  it('shows no prompt when there is no draft', () => {
    renderForm();
    expect(screen.queryByTestId('v2-draft-prompt')).toBeNull();
  });

  it('restores the saved values on request', async () => {
    seedDraft();
    renderForm();

    await act(async () => { fireEvent.click(screen.getByTestId('v2-draft-restore')); });

    const name = screen.getAllByRole('textbox')[0] as HTMLInputElement;
    expect(name.value).toBe('Người Nháp');
    expect(screen.queryByTestId('v2-draft-prompt')).toBeNull();
  });

  it('discarding removes the stored draft so it cannot come back', async () => {
    seedDraft();
    expect(loadVisitRequestV2Draft(NS)).not.toBeNull();

    renderForm();
    await act(async () => { fireEvent.click(screen.getByTestId('v2-draft-discard')); });

    expect(screen.queryByTestId('v2-draft-prompt')).toBeNull();
    expect(loadVisitRequestV2Draft(NS)).toBeNull();
  });

  it('does not overwrite the offered draft while the user is still deciding', async () => {
    seedDraft();
    const before = loadVisitRequestV2Draft(NS);
    renderForm();

    // Typing before choosing must not autosave over the draft being offered.
    const name = screen.getAllByRole('textbox')[0];
    await act(async () => {
      fireEvent.change(name, { target: { value: 'Gõ đè' } });
      await new Promise(r => setTimeout(r, 900)); // past the 700ms debounce
    });

    expect(loadVisitRequestV2Draft(NS)?.data.registerInfo?.fullName)
      .toBe(before?.data.registerInfo?.fullName);
  });

  it('resumes autosaving once the draft has been dealt with', async () => {
    seedDraft();
    renderForm();
    await act(async () => { fireEvent.click(screen.getByTestId('v2-draft-discard')); });

    const name = screen.getAllByRole('textbox')[0];
    await act(async () => {
      fireEvent.change(name, { target: { value: 'Sau khi bỏ nháp' } });
      await new Promise(r => setTimeout(r, 900));
    });

    expect(loadVisitRequestV2Draft(NS)?.data.registerInfo?.fullName).toBe('Sau khi bỏ nháp');
  });

  it('never stores OTP or session material in the draft', async () => {
    renderForm();
    const name = screen.getAllByRole('textbox')[0];
    await act(async () => {
      fireEvent.change(name, { target: { value: 'Ai đó' } });
      await new Promise(r => setTimeout(r, 900));
    });

    const raw = JSON.stringify(loadVisitRequestV2Draft(NS));
    expect(raw).not.toMatch(/otp|sessionToken|accessToken/i);
  });
});
