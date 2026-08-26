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
 * The reconciliation offer (plan v10 §6.2, §6.4, §6.5), now a two-level disclosure: a small icon next
 * to the contact card's title (mức 1), and a popover the icon opens with the question, the diff, and
 * the two actions (mức 2). No third step in between.
 *
 * The prompt is only ever rendered from a `profileDifference` the SERVER chose to send, and the server
 * sends it to one account only — so what these tests pin is the behaviour of the two levels and the
 * two answers, not who can see it. "Bỏ qua" must write nothing at all, and "Cập nhật hồ sơ" must send
 * only the fields that actually differ.
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

  const openPopover = async () => {
    render(<ContactProfileSyncPrompt visitInstanceId={10} difference={bothDiffer} onSynced={vi.fn()} />);
    await userEvent.click(screen.getByTestId('profile-sync-trigger-10'));
    return screen.getByTestId('profile-sync-popover-10');
  };

  it('MỨC 1: renders only the icon trigger, no popover, until clicked', () => {
    render(<ContactProfileSyncPrompt visitInstanceId={10} difference={bothDiffer} onSynced={vi.fn()} />);

    expect(screen.getByTestId('profile-sync-trigger-10')).toBeInTheDocument();
    expect(screen.queryByTestId('profile-sync-popover-10')).not.toBeInTheDocument();
  });

  it('the trigger is a real button with popover a11y wiring', () => {
    render(<ContactProfileSyncPrompt visitInstanceId={10} difference={bothDiffer} onSynced={vi.fn()} />);
    const trigger = screen.getByTestId('profile-sync-trigger-10');

    expect(trigger.tagName).toBe('BUTTON');
    expect(trigger).toHaveAttribute('aria-haspopup', 'dialog');
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    expect(trigger).toHaveAttribute('aria-controls', 'profile-sync-popover-10');
  });

  it('uses the profile/attention icon (BadgeAlert), never the Info/help glyph', () => {
    render(<ContactProfileSyncPrompt visitInstanceId={10} difference={bothDiffer} onSynced={vi.fn()} />);
    const trigger = screen.getByTestId('profile-sync-trigger-10');

    // lucide-react stamps every icon's own kebab-case name as a class on its <svg>, so this pins the
    // actual glyph rendered rather than just "some svg exists".
    expect(trigger.querySelector('svg.lucide-badge-alert')).not.toBeNull();
    expect(trigger.querySelector('svg.lucide-info')).toBeNull();
    expect(trigger.querySelector('svg.lucide-circle-alert')).toBeNull();
    expect(trigger.querySelector('svg.lucide-help-circle')).toBeNull();
  });

  it('carries the specific "differs from PEMS profile" tooltip, not a generic hint', () => {
    render(<ContactProfileSyncPrompt visitInstanceId={10} difference={bothDiffer} onSynced={vi.fn()} />);
    const trigger = screen.getByTestId('profile-sync-trigger-10');

    expect(trigger).toHaveAttribute('aria-label', 'visitRequestV2:profileSync.tooltip');
    expect(trigger).toHaveAttribute('title', 'visitRequestV2:profileSync.tooltip');
  });

  it('MỨC 2: clicking the icon opens the popover with the question and both changed fields', async () => {
    const popover = await openPopover();

    expect(popover).toBeInTheDocument();
    expect(screen.getByText('visitRequestV2:profileSync.question')).toBeInTheDocument();
    expect(screen.getByTestId('profile-sync-fullname').textContent).toContain('Nguyen Van A');
    expect(screen.getByTestId('profile-sync-fullname').textContent).toContain('Nguyễn Văn A (Trưởng đoàn)');
    expect(screen.getByTestId('profile-sync-phone').textContent).toContain('+84900000111');
    // No third step — the popover is the whole of mức 2, no "xem thay đổi" disclosure inside it.
    expect(screen.queryByText(/xem thay đổi|view changes/i)).not.toBeInTheDocument();
  });

  it('shows only the field that differs', async () => {
    render(
      <ContactProfileSyncPrompt
        visitInstanceId={10}
        difference={{ ...bothDiffer, phoneDiffers: false }}
        onSynced={vi.fn()}
      />,
    );
    await userEvent.click(screen.getByTestId('profile-sync-trigger-10'));

    expect(screen.getByTestId('profile-sync-fullname')).toBeInTheDocument();
    expect(screen.queryByTestId('profile-sync-phone')).not.toBeInTheDocument();
  });

  it('clicking the icon again closes the popover', async () => {
    render(<ContactProfileSyncPrompt visitInstanceId={10} difference={bothDiffer} onSynced={vi.fn()} />);
    const trigger = screen.getByTestId('profile-sync-trigger-10');

    await userEvent.click(trigger);
    expect(screen.getByTestId('profile-sync-popover-10')).toBeInTheDocument();

    await userEvent.click(trigger);
    expect(screen.queryByTestId('profile-sync-popover-10')).not.toBeInTheDocument();
  });

  it('Escape closes the popover', async () => {
    await openPopover();

    await userEvent.keyboard('{Escape}');
    expect(screen.queryByTestId('profile-sync-popover-10')).not.toBeInTheDocument();
  });

  it('a click outside the popover closes it', async () => {
    await openPopover();

    await userEvent.click(document.body);
    expect(screen.queryByTestId('profile-sync-popover-10')).not.toBeInTheDocument();
  });

  it('PROFILE-SYNC-04: "Bỏ qua" writes nothing and closes the popover', async () => {
    const onSynced = vi.fn();
    render(<ContactProfileSyncPrompt visitInstanceId={10} difference={bothDiffer} onSynced={onSynced} />);
    await userEvent.click(screen.getByTestId('profile-sync-trigger-10'));

    await userEvent.click(screen.getByTestId('profile-sync-keep'));

    expect(syncOwnAccountProfile).not.toHaveBeenCalled();
    expect(onSynced).not.toHaveBeenCalled();
    expect(screen.queryByTestId('profile-sync-popover-10')).not.toBeInTheDocument();
    // The icon itself is untouched by "Bỏ qua" — only the caller clearing `difference` removes it.
    expect(screen.getByTestId('profile-sync-trigger-10')).toBeInTheDocument();
  });

  it('PROFILE-SYNC-05: "Cập nhật hồ sơ" sends only full name and phone, then closes', async () => {
    const onSynced = vi.fn();
    render(<ContactProfileSyncPrompt visitInstanceId={10} difference={bothDiffer} onSynced={onSynced} />);
    await userEvent.click(screen.getByTestId('profile-sync-trigger-10'));

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
    expect(screen.queryByTestId('profile-sync-popover-10')).not.toBeInTheDocument();
  });

  it('sends only the differing field when just one differs', async () => {
    render(
      <ContactProfileSyncPrompt
        visitInstanceId={10}
        difference={{ ...bothDiffer, fullNameDiffers: false }}
        onSynced={vi.fn()}
      />,
    );
    await userEvent.click(screen.getByTestId('profile-sync-trigger-10'));

    await userEvent.click(screen.getByTestId('profile-sync-apply'));

    await waitFor(() => expect(syncOwnAccountProfile).toHaveBeenCalledTimes(1));
    expect(syncOwnAccountProfile).toHaveBeenCalledWith({ phone: '+84900000111' });
  });

  it('a failed update surfaces the error and leaves the popover open, so the person can retry', async () => {
    vi.mocked(syncOwnAccountProfile).mockRejectedValue(new Error('boom'));
    const onSynced = vi.fn();
    render(<ContactProfileSyncPrompt visitInstanceId={10} difference={bothDiffer} onSynced={onSynced} />);
    await userEvent.click(screen.getByTestId('profile-sync-trigger-10'));

    await userEvent.click(screen.getByTestId('profile-sync-apply'));

    await waitFor(() => expect(showErrorToast).toHaveBeenCalledTimes(1));
    expect(onSynced).not.toHaveBeenCalled();
    // Still open, so the offer is not lost to a network blip.
    expect(screen.getByTestId('profile-sync-popover-10')).toBeInTheDocument();
  });
});
