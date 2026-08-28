import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, act, waitFor } from '@testing-library/react';
import {
  hasMeaningfulV2Data,
  saveVisitRequestV2Draft,
  loadVisitRequestV2Draft,
} from '../utils/visitRequestV2DraftStorage';
import { createEmptyAdditionalRequirements, createEmptyCampusVisit } from '../utils/visitRequestV2Form';
import type { VisitRequestV2Schema } from '../schema/visitRequestV2.schema';

/**
 * The draft MECHANISM (debounced autosave, namespacing, TTL) already existed; what was missing was
 * the part the user can see. v1 asked before touching your work in both directions — on open
 * ("restore or discard?") and on close ("save, keep editing, or throw away") — and v2 silently
 * hydrated instead, so a stale draft could overwrite a form with no way to refuse.
 */

// TWO campuses, because the "add campus" ceiling is the number of campuses open for registration:
// with one, that button is permanently disabled and any test that clicks it proves nothing.
vi.mock('../hooks/useRegistrationCampuses', () => ({
  useRegistrationCampuses: () => ({
    campuses: [
      { campusCode: 'HN', campusName: 'Hòa Lạc' },
      { campusCode: 'HCM', campusName: 'TP.HCM' },
    ],
    loading: false,
  }),
}));

const authUser = vi.fn(() => null as null | { userId: number; email: string; effectiveRole: string });
vi.mock('../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ user: authUser(), isReady: true, effectiveRole: authUser()?.effectiveRole ?? null }),
}));

const getMyProfile = vi.fn();
vi.mock('../../profile/api/profileApi', () => ({
  profileApi: { getMyProfile: (...a: unknown[]) => getMyProfile(...a) },
}));

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
import { VisitRequestV2Modal } from '../components/v2/VisitRequestV2Modal';

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

  it('offers a way back into a verification that was already requested', async () => {
    // Restored typing with no way to finish the OTP is a dead end: the user would have to submit
    // again, burning a code that may already be sitting in their inbox.
    saveVisitRequestV2Draft(
      {
        registerInfo: {
          fullName: 'Người Nháp', organization: 'ĐH Nháp', jobTitle: 'TP',
          phone: '+84912345678', email: 'draft@example.com', nationality: 'VN',
        },
      } as never,
      undefined,
      NS,
      {
        submissionId: 'sub-1',
        otp: {
          targetEmail: 'draft@example.com', maskedEmail: 'dr***@example.com',
          expiresAt: null, resendAfterSeconds: 60, savedAt: Date.now(),
        },
      },
    );
    renderForm();
    await act(async () => { fireEvent.click(screen.getByTestId('v2-draft-restore')); });

    expect(screen.getByTestId('v2-otp-resume')).toBeTruthy();
    expect(screen.getByTestId('v2-otp-resume-continue')).toBeTruthy();

    // Forgetting the code leaves the answers alone — it is the challenge being dropped, not the form.
    await act(async () => { fireEvent.click(screen.getByTestId('v2-otp-resume-discard')); });
    expect(screen.queryByTestId('v2-otp-resume')).toBeNull();
    const name = screen.getAllByRole('textbox')[0] as HTMLInputElement;
    expect(name.value).toBe('Người Nháp');
    expect(loadVisitRequestV2Draft(NS)?.otp).toBeUndefined();
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
    await waitFor(() => {
      expect(screen.queryByTestId('v2-draft-prompt')).toBeNull();
    });
  });

  it('discarding removes the stored draft so it cannot come back', async () => {
    seedDraft();
    expect(loadVisitRequestV2Draft(NS)).not.toBeNull();

    renderForm();
    await act(async () => { fireEvent.click(screen.getByTestId('v2-draft-discard')); });

    await waitFor(() => {
      expect(screen.queryByTestId('v2-draft-prompt')).toBeNull();
    });
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

// ── Which draft is THIS person's? ────────────────────────────────────────────
/**
 * The namespace is `u{userId}` and it arrives from AuthContext one render LATE. Detecting once at
 * mount therefore asked the wrong key: it looked in the PUBLIC draft, found nothing, and never
 * looked again — so whether the restore prompt appeared came down to whether the user had loaded
 * first. Same person, same draft, prompt some of the time.
 *
 * Authenticated create is self-registration ONLY (plan CanhIter3FixBug), so these tests also double
 * as the pin for §9 — "a restored authenticated draft can never bring back somebody else's
 * registrant identity": every seeded draft below carries a STALE registrant snapshot on purpose, and
 * what must come back after restore is the LIVE profile, never that snapshot. Campus content (which
 * IS legitimately the user's own typing) still restores normally — only the registrant is overridden.
 */
describe('draft detection waits for the account namespace, then runs once per namespace', () => {
  const U15 = { userId: 15, email: 'u15@fpt.edu.vn', effectiveRole: 'STAFF' };
  const profileFor = (u: typeof U15, fullName: string) => ({
    userId: u.userId, fullName, email: u.email, phone: '+84900000000', nationality: 'VN',
    displayPosition: 'Nhân viên', displayDepartmentName: 'Phòng ABC', displayCampusName: 'Hòa Lạc',
    department: { departmentId: 1, name: 'Phòng ABC', departmentType: 'IC' },
  });

  /** Every seeded draft's registrant is a STALE snapshot that must never survive a restore. */
  const seedFor = (namespace: string | undefined, delegationName: string) =>
    saveVisitRequestV2Draft(
      {
        registerInfo: {
          fullName: 'Người khác (nháp cũ)', organization: 'Tổ chức khác', jobTitle: 'CV',
          phone: '', email: 'nguoikhac@example.com', nationality: 'VN',
        },
        campusVisits: [{ ...createEmptyCampusVisit('ck-1'), delegationName }],
      } as never,
      undefined,
      namespace,
    );

  const renderAuthed = (draftNamespace?: string) =>
    render(<VisitRequestFormV2 mode="authenticated" draftNamespace={draftNamespace} onSuccess={vi.fn()} />);

  const authedForm = (draftNamespace?: string) => (
    <VisitRequestFormV2 mode="authenticated" draftNamespace={draftNamespace} onSuccess={vi.fn()} />
  );

  const delegationNameInput = (): HTMLElement => {
    const wrapper = screen.getByText('visitRequestV2:card.delegationName').closest('div.flex.flex-col.gap-2');
    const control = wrapper?.querySelector('textarea, input');
    if (!control) throw new Error('Delegation name control not found');
    return control as HTMLElement;
  };

  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    authUser.mockReturnValue(U15);
    getMyProfile.mockResolvedValue(profileFor(U15, 'Người U15 (hồ sơ thật)'));
  });

  it('finds the account draft that only became addressable after the user loaded', async () => {
    seedFor('u15', 'Đoàn của u15');
    const { rerender } = renderAuthed(undefined);

    // AuthContext has not answered yet: nothing may be offered, because nothing is known.
    expect(screen.queryByTestId('v2-draft-prompt')).toBeNull();

    rerender(authedForm('u15'));

    await waitFor(() => expect(screen.getByTestId('v2-draft-prompt')).toBeTruthy());
    await act(async () => { fireEvent.click(screen.getByTestId('v2-draft-restore')); });

    // Campus content from the draft: restored.
    await waitFor(() => expect(delegationNameInput()).toHaveValue('Đoàn của u15'));
    // Registrant identity: the LIVE profile always wins — never the stale draft snapshot (plan §9).
    const summary = screen.getByTestId('v2-registrant-readonly');
    expect(summary.textContent).toContain('Người U15 (hồ sơ thật)');
    expect(summary.textContent).not.toContain('Người khác (nháp cũ)');
  });

  it('never falls back to the public draft while the account is still unknown', async () => {
    saveVisitRequestV2Draft(
      {
        registerInfo: { fullName: 'Khách vãng lai', organization: '', jobTitle: '', phone: '', email: '', nationality: '' },
        campusVisits: [{ ...createEmptyCampusVisit('ck-1'), delegationName: 'Đoàn công khai' }],
      } as never,
      undefined,
      undefined,
    );
    const { rerender } = renderAuthed(undefined);

    expect(screen.queryByTestId('v2-draft-prompt')).toBeNull();

    // …and the signed-in form must not write over it either, now that the account key is known.
    // (The registrant block is read-only now, so the edit that exercises autosave is a campus field.)
    rerender(authedForm('u15'));
    await waitFor(() => expect(screen.getByTestId('v2-registrant-readonly')).toBeTruthy());
    await act(async () => {
      fireEvent.change(delegationNameInput(), { target: { value: 'Đoàn của tài khoản đã đăng nhập' } });
      await new Promise(r => setTimeout(r, 900));
    });

    expect(screen.queryByTestId('v2-draft-prompt')).toBeNull();
    expect(loadVisitRequestV2Draft(undefined)?.data.campusVisits?.[0]?.delegationName).toBe('Đoàn công khai');
    expect(loadVisitRequestV2Draft('u15')?.data.campusVisits?.[0]?.delegationName)
      .toBe('Đoàn của tài khoản đã đăng nhập');
  });

  it('does not re-detect on an ordinary re-render of the same namespace', async () => {
    seedFor('u15', 'Đoàn của u15');
    const reads = vi.spyOn(Storage.prototype, 'getItem');
    const { rerender } = renderAuthed('u15');
    await waitFor(() => expect(screen.getByTestId('v2-draft-prompt')).toBeTruthy());

    const countFor = (key: string) => reads.mock.calls.filter(([k]) => k === key).length;
    const after = countFor('pems_visit_registration_draft_percampus::u15');

    rerender(authedForm('u15'));
    rerender(authedForm('u15'));
    rerender(authedForm('u15'));

    expect(countFor('pems_visit_registration_draft_percampus::u15')).toBe(after);
    reads.mockRestore();
  });

  it('detects the NEW account when the namespace changes underneath a mounted form', async () => {
    seedFor('u15', 'Đoàn của u15');
    seedFor('u16', 'Đoàn của u16');
    const { rerender } = renderAuthed('u15');
    await waitFor(() => expect(screen.getByTestId('v2-draft-prompt')).toBeTruthy());
    await act(async () => { fireEvent.click(screen.getByTestId('v2-draft-discard')); });
    expect(loadVisitRequestV2Draft('u15')).toBeNull();

    // (The profile itself loads once per MOUNT, not per namespace — a real account switch remounts
    // this component entirely via a route change, so it is out of scope for this test; what matters
    // here is purely draft-namespace isolation.)
    rerender(authedForm('u16'));

    await waitFor(() => expect(screen.getByTestId('v2-draft-prompt')).toBeTruthy());
    await act(async () => { fireEvent.click(screen.getByTestId('v2-draft-restore')); });
    // u16's own campus content — never the previous account's.
    await waitFor(() => expect(delegationNameInput()).toHaveValue('Đoàn của u16'));
    // …and the registrant identity is STILL the live profile, never the stale draft snapshot either
    // account's draft carried.
    expect(screen.getByTestId('v2-registrant-readonly').textContent).not.toContain('Người khác (nháp cũ)');
  });

  it('still offers the public draft in public mode, where there is no account to wait for', () => {
    seedFor(undefined, 'Khách vãng lai');
    render(<VisitRequestFormV2 mode="public" onSuccess={vi.fn()} />);
    expect(screen.getByTestId('v2-draft-prompt')).toBeTruthy();
  });
});

// ── Closing the modal ────────────────────────────────────────────────────────
/**
 * The close prompt used to be gated on `hasMeaningfulV2Data`, which answers "is there enough here
 * to save?" rather than "has anything changed?". Every field that question did not cover — a job
 * title, a working language, an operational contact — closed the modal outright and threw the
 * typing away without asking.
 */
describe('closing the create modal asks before it throws typed data away', () => {
  const renderModal = (onClose = vi.fn()) => {
    const utils = render(
      <VisitRequestV2Modal isOpen onClose={onClose} mode="public" draftNamespace={NS} onSuccess={vi.fn()} />,
    );
    return { ...utils, onClose };
  };

  /** The control inside the FormField whose visible label is exactly `label`. */
  const controlFor = (label: string): HTMLElement => {
    const wrapper = screen.getByText(label).closest('div.flex.flex-col.gap-2');
    const control = wrapper?.querySelector('textarea, input, select');
    if (!control) throw new Error(`No control found for the field labelled "${label}"`);
    return control as HTMLElement;
  };

  const clickClose = () => fireEvent.click(screen.getByTestId('v2-modal-close'));
  const promptShown = () => screen.queryByTestId('v2-modal-discard') !== null;

  beforeEach(() => { localStorage.clear(); vi.clearAllMocks(); });

  it('closes straight away while nothing has been touched', () => {
    const { onClose } = renderModal();
    clickClose();
    expect(promptShown()).toBe(false);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it.each([
    ['the registrant name', () => fireEvent.change(screen.getByTestId('v2-registrant-fullName'), { target: { value: 'N' } })],
    ['a job title alone', () => fireEvent.change(screen.getByTestId('v2-registrant-jobTitle'), { target: { value: 'Trưởng phòng' } })],
    ['the working content alone', () => fireEvent.change(controlFor('visitRequestV2:card.workingContent'), { target: { value: 'Nội dung' } })],
    ['the operational contact alone', () => {
      // The free-text fields only exist once a source is chosen (plan CanhIter3FixBug) — picking one
      // is itself the smallest possible "changed the operational contact" action now.
      fireEvent.click(screen.getByTestId('campus-opcontact-source-external-0'));
      fireEvent.change(screen.getByTestId('campus-opcontact-name'), { target: { value: 'Đầu mối' } });
    }],
    ['a campus selection alone', () => fireEvent.change(controlFor('visitRequestV2:card.campus'), { target: { value: 'HN' } })],
    ['a visit type alone', () => fireEvent.change(controlFor('visitRequestV2:card.visitType'), { target: { value: 'MEETING' } })],
    ['a working language alone', () => fireEvent.change(controlFor('visitRequestV2:card.workingLanguage'), { target: { value: 'EN' } })],
    ['a media-consent choice alone', () => fireEvent.change(controlFor('visitRequestV2:card.mediaConsent'), { target: { value: 'DECLINED' } })],
  ])('warns before closing when the user changed %s', async (_label, change) => {
    const { onClose } = renderModal();
    await act(async () => { change(); });

    clickClose();

    expect(promptShown()).toBe(true);
    expect(onClose).not.toHaveBeenCalled();
  });

  it('does not nag when the typing was undone back to the baseline', async () => {
    const { onClose } = renderModal();
    const name = screen.getByTestId('v2-registrant-fullName');
    await act(async () => { fireEvent.change(name, { target: { value: 'Gõ nhầm' } }); });
    await act(async () => { fireEvent.change(name, { target: { value: '' } }); });

    clickClose();

    expect(promptShown()).toBe(false);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('warns on Escape and on the backdrop, not only on the X', async () => {
    const { onClose, container } = renderModal();
    await act(async () => {
      fireEvent.change(screen.getByTestId('v2-registrant-fullName'), { target: { value: 'Ai đó' } });
    });

    fireEvent.keyDown(document, { key: 'Escape' });
    expect(promptShown()).toBe(true);
    expect(onClose).not.toHaveBeenCalled();

    // Dismissed and gone (the prompt animates out) before the backdrop is asked the same question,
    // so what the last assertion sees is the backdrop's own doing.
    fireEvent.click(screen.getByTestId('v2-modal-continue-editing'));
    await waitFor(() => expect(promptShown()).toBe(false));

    fireEvent.mouseDown(container.firstChild as Element);
    expect(promptShown()).toBe(true);
    expect(onClose).not.toHaveBeenCalled();
  });

  it('treats a restored draft as the new baseline — and warns again once it is edited', async () => {
    seedDraft();
    const { onClose } = renderModal();
    await act(async () => { fireEvent.click(screen.getByTestId('v2-draft-restore')); });

    // Restored but untouched: closing is not a threat to anything, and the draft stays put.
    clickClose();
    expect(promptShown()).toBe(false);
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(loadVisitRequestV2Draft(NS)?.data.registerInfo?.fullName).toBe('Người Nháp');

    await act(async () => {
      fireEvent.change(screen.getByTestId('v2-registrant-jobTitle'), { target: { value: 'Chức danh mới' } });
    });
    clickClose();
    expect(promptShown()).toBe(true);
  });

  it('saves on demand — without waiting for the debounce — and only then closes', async () => {
    const { onClose } = renderModal();
    await act(async () => {
      fireEvent.change(screen.getByTestId('v2-registrant-fullName'), { target: { value: 'Lưu ngay' } });
    });

    clickClose();
    fireEvent.click(screen.getByTestId('v2-modal-save-draft'));

    // No 700ms wait anywhere in this test: the force-save reads the form directly.
    expect(loadVisitRequestV2Draft(NS)?.data.registerInfo?.fullName).toBe('Lưu ngay');
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('"keep editing" leaves both the modal and every answer exactly as they were', async () => {
    const { onClose } = renderModal();
    await act(async () => {
      fireEvent.change(screen.getByTestId('v2-registrant-fullName'), { target: { value: 'Vẫn điền tiếp' } });
    });

    clickClose();
    fireEvent.click(screen.getByTestId('v2-modal-continue-editing'));

    await waitFor(() => expect(promptShown()).toBe(false));
    expect(onClose).not.toHaveBeenCalled();
    expect(screen.getByTestId('v2-create-modal')).toBeTruthy();
    expect((screen.getByTestId('v2-registrant-fullName') as HTMLInputElement).value).toBe('Vẫn điền tiếp');
  });

  it('"discard" deletes this namespace\'s draft and nobody else\'s', async () => {
    saveVisitRequestV2Draft(
      { registerInfo: { fullName: 'Nháp người khác' } } as never, undefined, 'u99');
    const { onClose } = renderModal();
    await act(async () => {
      fireEvent.change(screen.getByTestId('v2-registrant-fullName'), { target: { value: 'Bỏ đi' } });
      await new Promise(r => setTimeout(r, 900)); // let the autosave actually write one
    });
    expect(loadVisitRequestV2Draft(NS)).not.toBeNull();

    clickClose();
    fireEvent.click(screen.getByTestId('v2-modal-discard'));

    expect(loadVisitRequestV2Draft(NS)).toBeNull();
    expect(loadVisitRequestV2Draft('u99')?.data.registerInfo?.fullName).toBe('Nháp người khác');
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('a keystroke armed just before "discard" cannot put the draft back', async () => {
    const { onClose } = renderModal();
    await act(async () => {
      fireEvent.change(screen.getByTestId('v2-registrant-fullName'), { target: { value: 'Vừa gõ xong' } });
    });

    // Inside the 700ms window: the debounced save is armed but has not fired.
    clickClose();
    fireEvent.click(screen.getByTestId('v2-modal-discard'));
    await act(async () => { await new Promise(r => setTimeout(r, 900)); });

    expect(loadVisitRequestV2Draft(NS)).toBeNull();
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});

// ── An empty form is not "unsaved work" ──────────────────────────────────────
/**
 * `isDirty` alone was the wrong question a second time. React Hook Form counts a STRUCTURAL change
 * as dirty, so adding an empty guest row — or any other move that leaves the form exactly as empty
 * as it started — made X open a prompt whose own save button then answered "there is nothing to
 * save". The prompt is worth showing only when the change puts real data, or a stored draft, at
 * risk; and when the form has been emptied over a stored draft the honest primary action is DELETE,
 * not save.
 */
describe('an empty form closes quietly; an emptied draft asks what to do with it', () => {
  const seedMinimalDraft = () =>
    saveVisitRequestV2Draft(
      { registerInfo: { fullName: 'Nháp cũ', organization: '', jobTitle: '', phone: '', email: '', nationality: '' } } as never,
      undefined,
      NS,
    );

  const renderModal = (onClose = vi.fn(), draftNamespace: string | undefined = NS) => {
    const utils = render(
      <VisitRequestV2Modal isOpen onClose={onClose} mode="public" draftNamespace={draftNamespace} onSuccess={vi.fn()} />,
    );
    return { ...utils, onClose };
  };

  const clickClose = () => fireEvent.click(screen.getByTestId('v2-modal-close'));
  const promptShown = () => screen.queryByTestId('v2-modal-discard') !== null;

  /** Restores the seeded draft and then empties the one field it carried. */
  const restoreThenEmpty = async () => {
    await act(async () => { fireEvent.click(screen.getByTestId('v2-draft-restore')); });
    await act(async () => {
      fireEvent.change(screen.getByTestId('v2-registrant-fullName'), { target: { value: '' } });
    });
  };

  beforeEach(() => { localStorage.clear(); vi.clearAllMocks(); });

  it('closes without a word when the only change was adding an empty guest row', async () => {
    const { onClose } = renderModal();
    await act(async () => { fireEvent.click(screen.getByText('visitRequestV2:card.addVisitor')); });

    clickClose();

    expect(promptShown()).toBe(false);
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(loadVisitRequestV2Draft(NS)).toBeNull();
  });

  it('closes without a word when the only change was adding an empty campus card', async () => {
    const { onClose } = renderModal();
    await act(async () => { fireEvent.click(screen.getByTestId('v2-add-campus')); });

    clickClose();

    expect(promptShown()).toBe(false);
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(loadVisitRequestV2Draft(NS)).toBeNull();
  });

  it('still asks when the form was emptied over a stored draft — with no save button to refuse', async () => {
    seedMinimalDraft();
    const { onClose } = renderModal();
    await restoreThenEmpty();

    clickClose();

    expect(promptShown()).toBe(true);
    expect(onClose).not.toHaveBeenCalled();
    // Nothing is left to save, so that button is not offered at all — the two honest ways on are
    // to keep filling the form in or to leave. The leave button carries its label and nothing else:
    // no second line spelling out what is about to be deleted.
    expect(screen.queryByTestId('v2-modal-save-draft')).toBeNull();
    const discard = screen.getByTestId('v2-modal-discard');
    expect(discard.textContent).toBe('visitRequest:cancelConfirm.discard');
    expect(discard.textContent).not.toContain('visitRequestV2:draft.exitDeletesDraft');
  });

  it('"exit without saving" deletes the stored draft after the form was emptied', async () => {
    seedMinimalDraft();
    const { onClose, unmount } = renderModal();
    await restoreThenEmpty();
    clickClose();

    fireEvent.click(screen.getByTestId('v2-modal-discard'));

    expect(loadVisitRequestV2Draft(NS)).toBeNull();
    expect(onClose).toHaveBeenCalledTimes(1);

    // …and reopening has nothing left to offer.
    unmount();
    render(<VisitRequestFormV2 mode="public" draftNamespace={NS} onSuccess={vi.fn()} />);
    expect(screen.queryByTestId('v2-draft-prompt')).toBeNull();
  });

  it('"exit without saving" deletes a RESTORED draft too — including one autosave already rewrote', async () => {
    // The case the whole rule turns on. The draft was there before this session, the user restored
    // it, edited it, and autosave has already replaced the stored copy with those edits. "Exit
    // without saving" means exactly one thing: nothing is kept — not the edits, not the draft they
    // came from. Nothing is snapshotted and nothing is put back.
    seedMinimalDraft();
    const { onClose, unmount } = renderModal();
    await act(async () => { fireEvent.click(screen.getByTestId('v2-draft-restore')); });
    await act(async () => {
      fireEvent.change(screen.getByTestId('v2-registrant-fullName'), { target: { value: 'Sửa thành B' } });
      await new Promise(r => setTimeout(r, 900)); // let the autosave actually rewrite the stored draft
    });
    // Proof that storage really holds B before the exit, or the assertion after it proves nothing.
    expect(loadVisitRequestV2Draft(NS)?.data.registerInfo?.fullName).toBe('Sửa thành B');

    clickClose();
    fireEvent.click(screen.getByTestId('v2-modal-discard'));

    expect(loadVisitRequestV2Draft(NS)).toBeNull();
    expect(onClose).toHaveBeenCalledTimes(1);

    // Neither A nor B comes back, and reopening offers nothing to restore.
    unmount();
    render(<VisitRequestFormV2 mode="public" draftNamespace={NS} onSuccess={vi.fn()} />);
    expect(screen.queryByTestId('v2-draft-prompt')).toBeNull();
    expect(loadVisitRequestV2Draft(NS)).toBeNull();
  });

  it('"save draft and close" after editing a restored draft stores the EDIT', async () => {
    seedMinimalDraft();
    const { onClose } = renderModal();
    await act(async () => { fireEvent.click(screen.getByTestId('v2-draft-restore')); });
    await act(async () => {
      fireEvent.change(screen.getByTestId('v2-registrant-fullName'), { target: { value: 'Sửa thành B' } });
    });

    clickClose();
    fireEvent.click(screen.getByTestId('v2-modal-save-draft'));

    expect(loadVisitRequestV2Draft(NS)?.data.registerInfo?.fullName).toBe('Sửa thành B');
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('"keep editing" after an edit changes nothing on disk and keeps the edit on screen', async () => {
    seedMinimalDraft();
    const { onClose } = renderModal();
    await act(async () => { fireEvent.click(screen.getByTestId('v2-draft-restore')); });
    await act(async () => {
      fireEvent.change(screen.getByTestId('v2-registrant-fullName'), { target: { value: 'Sửa thành B' } });
    });

    clickClose();
    fireEvent.click(screen.getByTestId('v2-modal-continue-editing'));

    await waitFor(() => expect(promptShown()).toBe(false));
    expect(onClose).not.toHaveBeenCalled();
    expect((screen.getByTestId('v2-registrant-fullName') as HTMLInputElement).value).toBe('Sửa thành B');
    expect(loadVisitRequestV2Draft(NS)).not.toBeNull();
  });

  it('"keep editing" from the emptied prompt keeps the modal, the empty form AND the draft', async () => {
    seedMinimalDraft();
    const { onClose } = renderModal();
    await restoreThenEmpty();
    clickClose();

    fireEvent.click(screen.getByTestId('v2-modal-continue-editing'));

    await waitFor(() => expect(promptShown()).toBe(false));
    expect(onClose).not.toHaveBeenCalled();
    expect(screen.getByTestId('v2-create-modal')).toBeTruthy();
    expect((screen.getByTestId('v2-registrant-fullName') as HTMLInputElement).value).toBe('');
    expect(loadVisitRequestV2Draft(NS)?.data.registerInfo?.fullName).toBe('Nháp cũ');
  });

  it('an autosave armed before "exit without saving" cannot bring the deleted draft back', async () => {
    seedMinimalDraft();
    renderModal();
    await act(async () => { fireEvent.click(screen.getByTestId('v2-draft-restore')); });
    await act(async () => {
      fireEvent.change(screen.getByTestId('v2-registrant-fullName'), { target: { value: 'Sửa dở dang' } });
    });

    // Inside the 700ms window: the debounced save is armed but has not fired.
    clickClose();
    fireEvent.click(screen.getByTestId('v2-modal-discard'));
    expect(loadVisitRequestV2Draft(NS)).toBeNull();

    await act(async () => { await new Promise(r => setTimeout(r, 900)); });

    expect(loadVisitRequestV2Draft(NS)).toBeNull();
  });

  it('deleting one account\'s draft leaves the other account and the public one untouched', async () => {
    saveVisitRequestV2Draft(
      { registerInfo: { fullName: 'Nháp của u15' } } as never, undefined, 'u15');
    saveVisitRequestV2Draft(
      { registerInfo: { fullName: 'Nháp công khai' } } as never, undefined, undefined);
    saveVisitRequestV2Draft(
      { registerInfo: { fullName: 'Nháp của u16', organization: '', jobTitle: '', phone: '', email: '', nationality: '' } } as never,
      undefined, 'u16');

    renderModal(vi.fn(), 'u16');
    await restoreThenEmpty();
    clickClose();
    fireEvent.click(screen.getByTestId('v2-modal-discard'));

    expect(loadVisitRequestV2Draft('u16')).toBeNull();
    expect(loadVisitRequestV2Draft('u15')?.data.registerInfo?.fullName).toBe('Nháp của u15');
    expect(loadVisitRequestV2Draft(undefined)?.data.registerInfo?.fullName).toBe('Nháp công khai');
  });
});

// ── What counts as worth saving ──────────────────────────────────────────────
/**
 * `hasMeaningfulV2Data` guards the WRITE. Every field it did not know about was a field the user
 * could fill in and then lose: the save returned "No meaningful data to save" and said nothing.
 */
describe('a draft is written for anything the user filled in, and for nothing they did not', () => {
  const empty = (): VisitRequestV2Schema => ({
    registerInfo: { fullName: '', organization: '', jobTitle: '', phone: '', email: '', nationality: '' },
    partnerSelectionMode: 'NEW_ORGANIZATION',
    partnerId: null,
    campusVisits: [createEmptyCampusVisit('ck-1')],
  });

  const withRegistrant = (patch: Partial<VisitRequestV2Schema['registerInfo']>): VisitRequestV2Schema =>
    ({ ...empty(), registerInfo: { ...empty().registerInfo, ...patch } });

  const withCampus = (patch: Partial<VisitRequestV2Schema['campusVisits'][number]>): VisitRequestV2Schema =>
    ({ ...empty(), campusVisits: [{ ...createEmptyCampusVisit('ck-1'), ...patch }] });

  const withAdditional = (
    patch: Partial<NonNullable<VisitRequestV2Schema['additionalRequirements']>>,
  ): VisitRequestV2Schema =>
    ({ ...empty(), additionalRequirements: { ...createEmptyAdditionalRequirements(), ...patch } });

  beforeEach(() => localStorage.clear());

  it.each([
    ['a job title on its own', withRegistrant({ jobTitle: 'Trưởng phòng' })],
    ['a nationality on its own', withRegistrant({ nationality: 'Việt Nam' })],
    ['a phone number on its own', withRegistrant({ phone: '+84912345678' })],
    ['the working content', withCampus({ workingContent: 'Nội dung làm việc' })],
    ['a visit type that is not the default', withCampus({ visitType: 'MEETING' })],
    // Per-campus "Yêu cầu bổ sung" — still checked for backward compatibility with the per-campus
    // EDIT screens, which keep writing these 4 fields directly onto the campus card.
    ['a working language that is not the default (per-campus, EDIT)', withCampus({ workingLanguage: 'EN' })],
    ['a media-consent answer that is not the default (per-campus, EDIT)', withCampus({ mediaConsentStatus: 'DECLINED' })],
    ['a transportation note (per-campus, EDIT)', withCampus({ transportationNote: 'Xe 16 chỗ' })],
    ['a note to the campus (per-campus, EDIT)', withCampus({ notes: 'Ghi chú thêm' })],
    // Request-level "Yêu cầu bổ sung" — the CREATE form's actual source of truth now.
    ['a working language that is not the default (request-level, CREATE)', withAdditional({ workingLanguage: 'EN' })],
    ['a media-consent answer that is not the default (request-level, CREATE)', withAdditional({ mediaConsentStatus: 'DECLINED' })],
    ['a transportation note (request-level, CREATE)', withAdditional({ transportationNote: 'Xe 16 chỗ' })],
    ['a note to the campus (request-level, CREATE)', withAdditional({ notes: 'Ghi chú thêm' })],
    ['an operational contact email', withCampus({
      operationalContact: { fullName: '', organization: '', jobTitle: '', phone: '', email: 'dm@example.com' },
    })],
    ['a visitor row with only an organization', withCampus({
      visitors: [{ fullName: '', jobTitle: '', organization: 'ĐH Đối Tác', nationality: '' }],
    })],
    ['a support row with only a job title', withCampus({
      supportTeam: [{ fullName: '', jobTitle: 'Phiên dịch', organization: '', nationality: '' }],
    })],
    ['an existing partner', { ...empty(), partnerSelectionMode: 'EXISTING_PARTNER' as const, partnerId: 4 }],
  ])('saves a draft carrying %s', (_label, values) => {
    expect(hasMeaningfulV2Data(values)).toBe(true);
    expect(saveVisitRequestV2Draft(values, undefined, NS).success).toBe(true);
    expect(loadVisitRequestV2Draft(NS)).not.toBeNull();
  });

  it('writes nothing for a form that is still exactly as it opened', () => {
    // Client keys, one empty campus card, one empty visitor row, the default enums: all present,
    // none of it typed by anybody.
    expect(hasMeaningfulV2Data(empty())).toBe(false);
    const outcome = saveVisitRequestV2Draft(empty(), undefined, NS);
    expect(outcome.success).toBe(false);
    expect(loadVisitRequestV2Draft(NS)).toBeNull();
  });
});

// ── "Yêu cầu bổ sung": one note, not two ─────────────────────────────────────
/**
 * The card used to carry TWO free-text notes: a general one and a media-consent one that appeared
 * only while consent was AGREED. The business kept one note and dropped the media one, so the
 * conditional block is gone — and with it the reason for a card to render differently depending on
 * an answer given two fields earlier.
 *
 * `t` is mocked to echo the key, so these assertions read label KEYS rather than Vietnamese text:
 * a mistranslation cannot make this pass, and a removed key cannot make it fail silently.
 */
describe('the campus card offers exactly one free-text note', () => {
  const renderForm = () => render(<VisitRequestFormV2 mode="public" onSuccess={vi.fn()} />);

  const consentSelect = (): HTMLSelectElement => {
    const wrapper = screen.getByText('visitRequestV2:card.mediaConsent').closest('div.flex.flex-col.gap-2');
    return wrapper!.querySelector('select') as HTMLSelectElement;
  };

  beforeEach(() => { localStorage.clear(); vi.clearAllMocks(); });

  it('shows "Ghi chú gửi FPTU" and the consent answer, and no media note', () => {
    renderForm();
    expect(screen.getByText('visitRequestV2:card.notes')).toBeTruthy();
    expect(screen.getByText('visitRequestV2:card.mediaConsent')).toBeTruthy();
    expect(screen.queryByText('visitRequestV2:card.mediaNote')).toBeNull();
  });

  it.each([['AGREED'], ['DECLINED']])(
    'keeps the note field and adds no second one when consent is %s', (status) => {
      renderForm();
      fireEvent.change(consentSelect(), { target: { value: status } });

      expect(consentSelect().value).toBe(status);
      expect(screen.getByText('visitRequestV2:card.notes')).toBeTruthy();
      // The old card grew an extra textarea here on AGREED. Nothing about the note depends on
      // the consent answer any more, in either direction.
      expect(screen.queryByText('visitRequestV2:card.mediaNote')).toBeNull();
    });
});

/**
 * Media consent gets its own end-to-end pass over the draft/dirty machinery because it is the one
 * enum whose default is a business decision rather than a placeholder: a new card is born "Đồng ý",
 * the answer nearly every delegation gives, so the common case is not a box everybody has to change
 * by hand.
 *
 * What these tests actually pin is that "untouched" is measured against THAT value and no other.
 * When the born value and the draft layer's idea of it were two separate literals they drifted, and
 * the field they disagreed about then read as untouched whatever the user answered — so closing the
 * modal threw the answer away without asking and no draft was written. The sentinel is now read from
 * `createEmptyCampusVisit`, and these run the same round trip with the answers the other way up.
 */
describe('media consent survives the draft round trip', () => {
  const renderModal = (onClose = vi.fn()) => {
    const utils = render(
      <VisitRequestV2Modal isOpen onClose={onClose} mode="public" draftNamespace={NS} onSuccess={vi.fn()} />,
    );
    return { ...utils, onClose };
  };

  const consentSelect = (): HTMLSelectElement => {
    const wrapper = screen.getByText('visitRequestV2:card.mediaConsent').closest('div.flex.flex-col.gap-2');
    return wrapper!.querySelector('select') as HTMLSelectElement;
  };

  const clickClose = () => fireEvent.click(screen.getByTestId('v2-modal-close'));
  const promptShown = () => screen.queryByTestId('v2-modal-discard') !== null;

  const baseline = (): VisitRequestV2Schema => ({
    registerInfo: { fullName: '', organization: '', jobTitle: '', phone: '', email: '', nationality: '' },
    partnerSelectionMode: 'NEW_ORGANIZATION',
    partnerId: null,
    campusVisits: [createEmptyCampusVisit('ck-1')],
  });

  // The consent answer now lives at request level (`additionalRequirements`), not on the campus
  // card — the UI moved, the "born value" comparison did not.
  const withConsent = (status: 'AGREED' | 'DECLINED'): VisitRequestV2Schema => ({
    ...baseline(),
    additionalRequirements: { ...createEmptyAdditionalRequirements(), mediaConsentStatus: status },
  });

  beforeEach(() => { localStorage.clear(); vi.clearAllMocks(); });

  it('MEDIA-DRAFT-01: a new form starts on "Đồng ý" and still writes no draft', () => {
    expect(createEmptyCampusVisit('ck-1').mediaConsentStatus).toBe('AGREED');
    // The default the user was given is not typing they did: an untouched form is still empty.
    expect(hasMeaningfulV2Data(baseline())).toBe(false);
    expect(saveVisitRequestV2Draft(baseline(), undefined, NS).success).toBe(false);
    expect(loadVisitRequestV2Draft(NS)).toBeNull();
  });

  it('MEDIA-DRAFT-01b: the form on screen shows that default, not an unanswered box', () => {
    renderModal();
    expect(consentSelect().value).toBe('AGREED');
  });

  it('MEDIA-DRAFT-02: answering the consent question on its own is meaningful', () => {
    expect(hasMeaningfulV2Data(withConsent('DECLINED'))).toBe(true);
  });

  it('MEDIA-DRAFT-03: a consent-only answer warns before the modal closes', async () => {
    const { onClose } = renderModal();
    await act(async () => { fireEvent.change(consentSelect(), { target: { value: 'DECLINED' } }); });

    clickClose();

    expect(promptShown()).toBe(true);
    expect(onClose).not.toHaveBeenCalled();
  });

  it('MEDIA-DRAFT-04: a consent-only answer is persisted', () => {
    expect(saveVisitRequestV2Draft(withConsent('DECLINED'), undefined, NS).success).toBe(true);
    expect(loadVisitRequestV2Draft(NS)).not.toBeNull();
  });

  it('MEDIA-DRAFT-05: the restored draft still carries the answer', () => {
    saveVisitRequestV2Draft(withConsent('DECLINED'), undefined, NS);

    const restored = loadVisitRequestV2Draft(NS);
    // Sanitising must not drop it on the way out, which is what made the answer look untouched.
    expect(restored?.data.additionalRequirements?.mediaConsentStatus).toBe('DECLINED');
  });

  it('MEDIA-DRAFT-05b: a saved "Không đồng ý" is what comes back, not the default', async () => {
    saveVisitRequestV2Draft(withConsent('DECLINED'), undefined, NS);
    renderModal();

    await act(async () => { fireEvent.click(screen.getByTestId('v2-draft-restore')); });

    expect(consentSelect().value).toBe('DECLINED');
  });

  it('MEDIA-DRAFT-06: answering and then changing back leaves nothing dirty', async () => {
    // Canonical-value comparison, not a sticky "was clicked" flag: returning to the default with the
    // rest of the form untouched means there is genuinely nothing to keep.
    expect(hasMeaningfulV2Data(withConsent('AGREED'))).toBe(false);

    const { onClose } = renderModal();
    await act(async () => { fireEvent.change(consentSelect(), { target: { value: 'DECLINED' } }); });
    await act(async () => { fireEvent.change(consentSelect(), { target: { value: 'AGREED' } }); });

    clickClose();

    expect(promptShown()).toBe(false);
    expect(onClose).toHaveBeenCalled();
  });
});
