import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('../api/visitRequestV2Api', () => ({
  syncOwnAccountProfile: vi.fn(),
}));

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

const showSuccessToast = vi.fn();
const showErrorToast = vi.fn();
vi.mock('../../../shared/utils/toast', () => ({
  showSuccessToast: (...args: unknown[]) => showSuccessToast(...args),
  showErrorToast: (...args: unknown[]) => showErrorToast(...args),
}));

import ContactProfileSyncPrompt from '../components/ContactProfileSyncPrompt';
import { syncOwnAccountProfile } from '../api/visitRequestV2Api';

/**
 * The reconciliation offer (plan v10 §6.2, §6.4, §6.5).
 *
 * The prompt is only ever rendered from a `profileDifference` the SERVER chose to send, and the server
 * sends it to one account only — so what these tests pin is the behaviour of the two answers, not who
 * can see it. "Keep" must write nothing at all, and "Update" must send only the fields that actually
 * differ.
 */
describe('ContactProfileSyncPrompt', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(syncOwnAccountProfile).mockResolvedValue({});
  });

  const bothDiffer = {
    fullNameDiffers: true,
    phoneDiffers: true,
    accountFullName: 'Nguyen Van A',
    accountPhone: '+84912345678',
    snapshotFullName: 'Nguyễn Văn A (Trưởng đoàn)',
    snapshotPhone: '+84900000111',
  };

  it('PROFILE-SYNC-02/03: shows the question and both changed fields', () => {
    render(<ContactProfileSyncPrompt difference={bothDiffer} onSynced={vi.fn()} />);

    expect(screen.getByTestId('contact-profile-sync-prompt')).toBeInTheDocument();
    expect(screen.getByText('visitRequestV2:profileSync.question')).toBeInTheDocument();
    expect(screen.getByTestId('profile-sync-fullname').textContent).toContain('Nguyen Van A');
    expect(screen.getByTestId('profile-sync-fullname').textContent).toContain('Nguyễn Văn A (Trưởng đoàn)');
    expect(screen.getByTestId('profile-sync-phone').textContent).toContain('+84900000111');
  });

  it('shows only the field that differs', () => {
    render(
      <ContactProfileSyncPrompt
        difference={{ ...bothDiffer, phoneDiffers: false }}
        onSynced={vi.fn()}
      />,
    );

    expect(screen.getByTestId('profile-sync-fullname')).toBeInTheDocument();
    expect(screen.queryByTestId('profile-sync-phone')).not.toBeInTheDocument();
  });

  it('PROFILE-SYNC-04: "keep my profile" writes nothing and dismisses', async () => {
    const onSynced = vi.fn();
    render(<ContactProfileSyncPrompt difference={bothDiffer} onSynced={onSynced} />);

    await userEvent.click(screen.getByTestId('profile-sync-keep'));

    expect(syncOwnAccountProfile).not.toHaveBeenCalled();
    expect(onSynced).not.toHaveBeenCalled();
    expect(screen.queryByTestId('contact-profile-sync-prompt')).not.toBeInTheDocument();
  });

  it('PROFILE-SYNC-05: "update my profile" sends only full name and phone', async () => {
    const onSynced = vi.fn();
    render(<ContactProfileSyncPrompt difference={bothDiffer} onSynced={onSynced} />);

    await userEvent.click(screen.getByTestId('profile-sync-apply'));

    await waitFor(() => expect(syncOwnAccountProfile).toHaveBeenCalledTimes(1));
    expect(syncOwnAccountProfile).toHaveBeenCalledWith({
      fullName: 'Nguyễn Văn A (Trưởng đoàn)',
      phone: '+84900000111',
    });
    // Nothing else may travel: no email, no organization, no job title, no user id.
    const payload = vi.mocked(syncOwnAccountProfile).mock.calls[0][0];
    expect(Object.keys(payload).sort()).toEqual(['fullName', 'phone']);

    await waitFor(() => expect(onSynced).toHaveBeenCalledTimes(1));
    expect(showSuccessToast).toHaveBeenCalledTimes(1);
  });

  it('sends only the differing field when just one differs', async () => {
    render(
      <ContactProfileSyncPrompt
        difference={{ ...bothDiffer, fullNameDiffers: false }}
        onSynced={vi.fn()}
      />,
    );

    await userEvent.click(screen.getByTestId('profile-sync-apply'));

    await waitFor(() => expect(syncOwnAccountProfile).toHaveBeenCalledTimes(1));
    expect(syncOwnAccountProfile).toHaveBeenCalledWith({ phone: '+84900000111' });
  });

  it('a failed update surfaces the error and leaves the offer standing', async () => {
    vi.mocked(syncOwnAccountProfile).mockRejectedValue(new Error('boom'));
    const onSynced = vi.fn();
    render(<ContactProfileSyncPrompt difference={bothDiffer} onSynced={onSynced} />);

    await userEvent.click(screen.getByTestId('profile-sync-apply'));

    await waitFor(() => expect(showErrorToast).toHaveBeenCalledTimes(1));
    expect(onSynced).not.toHaveBeenCalled();
    // Still there, so the person can try again rather than losing the offer to a network blip.
    expect(screen.getByTestId('contact-profile-sync-prompt')).toBeInTheDocument();
  });
});
