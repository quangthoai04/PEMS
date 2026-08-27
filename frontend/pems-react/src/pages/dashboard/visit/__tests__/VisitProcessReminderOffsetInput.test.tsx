/**
 * "3. Nhắc nhở chuyến thăm" — 2026-08-28 redesign: the preset dropdown ("1 ngày ▼" / "Tùy chỉnh...")
 * is gone. "Nhắc trước" is now exactly 2 controls, ALWAYS both shown: a numeric input (positive
 * integer only, no decimals) and a unit select (phút/giờ/ngày/tuần). There is no "custom mode" to
 * toggle into — these are the only two controls, always visible, for both of the 2 audience cards
 * (Người phụ trách / Thành phần tham gia).
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const getVisitProcessPermissions = vi.fn();
const getVisitProcessDetail = vi.fn();
const getReminderSettings = vi.fn();
const saveReminderSettings = vi.fn();
const cancelReminderSettings = vi.fn();

vi.mock('../../../../features/delegations/api/delegationsApi', () => ({
  delegationsApi: {
    getVisitProcessPermissions: (...a: unknown[]) => getVisitProcessPermissions(...a),
    getVisitProcessDetail: (...a: unknown[]) => getVisitProcessDetail(...a),
    getReminderSettings: (...a: unknown[]) => getReminderSettings(...a),
    saveReminderSettings: (...a: unknown[]) => saveReminderSettings(...a),
    cancelReminderSettings: (...a: unknown[]) => cancelReminderSettings(...a),
  },
}));

vi.mock('../../../../features/agenda-templates/components/AgendaSetupPanel', () => ({
  AgendaSetupPanel: () => <div>agenda-setup-panel</div>,
}));
vi.mock('../../../../features/delegations/components/ParticipantInvitationSection', () => ({
  ParticipantInvitationSection: () => <div>participant-invitation-section</div>,
}));
vi.mock('../../../../features/delegations/components/LogisticsRequestSection', () => ({
  LogisticsRequestSection: () => <div>logistics-request-section</div>,
}));
vi.mock('../../../../features/delegations/components/RequestInfoReadOnly', () => ({
  RegistrantInfoReadOnly: () => <div>registrant-info</div>,
  DelegationInfoReadOnly: () => <div>delegation-info</div>,
}));
vi.mock('../VisitDuringTab', () => ({ VisitDuringTab: () => <div>during-tab</div> }));
vi.mock('../VisitAfterTab', () => ({ VisitAfterTab: () => <div>after-tab</div> }));
vi.mock('../VisitorVisitDetailPage', () => ({ VisitorVisitDetailPage: () => <div>visitor-page</div> }));

vi.mock('../../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ user: { userId: '77', roleCode: 'STAFF' } }),
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => vi.fn(),
  useParams: () => ({ id: '501' }),
  useLocation: () => ({ state: null, pathname: '/dashboard/visit/process/501' }),
}));

import { VisitProcess } from '../VisitProcess';

const HOST_PERMISSION = {
  visitInstanceId: 501,
  visitRequestId: 9001,
  requestStatus: 'APPROVED',
  instanceStatus: 'BEFORE_VISIT',
  relation: 'HOST',
  hostAssigned: true,
  canViewOriginalRequest: true,
  canViewOverview: true,
  canViewBeforeVisit: true,
  canEditBeforeVisit: true,
  canViewDuringVisit: true,
  canEditDuringVisit: true,
  canViewAfterVisit: true,
  canEditAfterVisit: true,
  canAssignHost: false,
  canViewMinutes: true,
  canCreateMinutes: true,
  canEditMinutes: true,
  canViewNews: true,
  canCreateNews: true,
  canStartVisit: true,
  canCompleteVisit: false,
  canCloseVisit: false,
};

const DETAIL = {
  visitInstanceId: 501,
  visitRequestId: 9001,
  relation: 'HOST',
  instanceStatus: 'BEFORE_VISIT',
  delegationName: 'Đoàn ĐH Quốc gia',
  campusName: 'FPTU HCM',
  hostName: 'Trần Cảnh',
  plannedStartAt: '2026-08-20T09:00:00',
  plannedEndAt: '2026-08-20T11:30:00',
  agendaItems: [],
  requestSummary: {},
};

const hostCard = () => within(screen.getByTestId('reminder-card-HOST'));
const participantsCard = () => within(screen.getByTestId('reminder-card-PARTICIPANTS'));

const renderReady = async () => {
  render(<VisitProcess />);
  await waitFor(() => expect(screen.getByTestId('reminder-card-HOST')).toBeTruthy());
  await waitFor(() => expect(screen.getByTestId('reminder-card-PARTICIPANTS')).toBeTruthy());
};

beforeEach(() => {
  vi.clearAllMocks();
  getVisitProcessPermissions.mockResolvedValue(HOST_PERMISSION);
  getVisitProcessDetail.mockResolvedValue(DETAIL);
  getReminderSettings.mockResolvedValue({ items: [] });
  saveReminderSettings.mockResolvedValue({ items: [], message: 'Đã lưu cấu hình cảnh báo.' });
  cancelReminderSettings.mockResolvedValue({ cancelledCount: 0, message: 'Đã hủy lịch gửi cảnh báo.' });
});

describe('Nhắc nhở chuyến thăm — numeric input + unit select (no preset dropdown)', () => {
  it('renders exactly 2 cards, each with one numeric input and one unit select', async () => {
    await renderReady();

    expect(screen.getByText('Người phụ trách')).toBeTruthy();
    expect(screen.getByText('Thành phần tham gia', { selector: 'div' })).toBeTruthy();

    for (const card of [hostCard(), participantsCard()]) {
      expect(card.getByRole('spinbutton')).toBeTruthy();
      expect(card.getByRole('combobox')).toBeTruthy();
    }
  });

  it('never shows the old preset dropdown or a "Tùy chỉnh..." option', async () => {
    await renderReady();

    expect(screen.queryByText('Tùy chỉnh...')).toBeNull();
    expect(screen.queryByText('10 phút')).toBeNull();
    expect(screen.queryByText('1 tuần')).toBeNull();
    // The unit select never carries a numeric preset value like "1440" as an option.
    expect(screen.queryByRole('option', { name: '1 ngày' })).toBeNull();
  });

  it('the numeric input and unit select are always visible — no toggle reveals them', async () => {
    await renderReady();
    // Before this redesign, a second pair of controls only appeared after picking "Tùy chỉnh...".
    // Now there is exactly one input + one select per card from the very first render.
    expect(hostCard().getAllByRole('spinbutton')).toHaveLength(1);
    expect(hostCard().getAllByRole('combobox')).toHaveLength(1);
  });

  it('offers exactly phút/giờ/ngày/tuần as unit options', async () => {
    await renderReady();

    const options = hostCard().getByRole('combobox').querySelectorAll('option');
    const labels = Array.from(options).map((o) => o.textContent);
    expect(labels).toEqual(['phút', 'giờ', 'ngày', 'tuần']);
  });
});

describe('Numeric validation — integer > 0 only, no decimals', () => {
  it('accepts 1, 30, 90 with no error and no message shown', async () => {
    const user = userEvent.setup();
    await renderReady();
    const card = hostCard();
    await user.click(card.getByRole('checkbox', { name: /Thông báo hệ thống/ }));

    for (const value of ['1', '30', '90']) {
      await user.clear(card.getByRole('spinbutton'));
      await user.type(card.getByRole('spinbutton'), value);
      expect(card.queryByText(/Vui lòng nhập|phải lớn hơn 0|phải là số nguyên/)).toBeNull();
    }
  });

  it('rejects 0 with "phải lớn hơn 0"', async () => {
    const user = userEvent.setup();
    await renderReady();
    const card = hostCard();
    await user.click(card.getByRole('checkbox', { name: /Thông báo hệ thống/ }));
    await user.clear(card.getByRole('spinbutton'));
    await user.type(card.getByRole('spinbutton'), '0');

    expect(await card.findByText('Thời gian nhắc trước phải lớn hơn 0.')).toBeTruthy();
  });

  it('rejects -1 with "phải lớn hơn 0"', async () => {
    const user = userEvent.setup();
    await renderReady();
    const card = hostCard();
    await user.click(card.getByRole('checkbox', { name: /Thông báo hệ thống/ }));
    await user.clear(card.getByRole('spinbutton'));
    await user.type(card.getByRole('spinbutton'), '-1');

    expect(await card.findByText('Thời gian nhắc trước phải lớn hơn 0.')).toBeTruthy();
  });

  it('rejects 1.5 with "phải là số nguyên", not a rounded/truncated value', async () => {
    const user = userEvent.setup();
    await renderReady();
    const card = hostCard();
    await user.click(card.getByRole('checkbox', { name: /Thông báo hệ thống/ }));
    await user.clear(card.getByRole('spinbutton'));
    await user.type(card.getByRole('spinbutton'), '1.5');

    expect(await card.findByText('Thời gian nhắc trước phải là số nguyên.')).toBeTruthy();
  });

  it('rejects an empty value with "Vui lòng nhập..." once a channel is checked', async () => {
    const user = userEvent.setup();
    await renderReady();
    const card = hostCard();
    await user.click(card.getByRole('checkbox', { name: /Thông báo hệ thống/ }));
    await user.clear(card.getByRole('spinbutton'));

    expect(await card.findByText('Vui lòng nhập thời gian nhắc trước.')).toBeTruthy();
  });

  it('shows no validation error for an empty/invalid value while the card has no channel checked', async () => {
    await renderReady();
    const card = hostCard();
    // Default state: both channels off, offset defaults to "1" — but even if it were blank, an
    // inactive card must not nag the user (Mục 7).
    expect(card.queryByText(/Vui lòng nhập|phải lớn hơn 0|phải là số nguyên/)).toBeNull();
  });

  it('blocks Save and never calls the API when an active card has an invalid offset', async () => {
    const user = userEvent.setup();
    await renderReady();
    const card = hostCard();
    await user.click(card.getByRole('checkbox', { name: /Thông báo hệ thống/ }));
    await user.clear(card.getByRole('spinbutton'));
    await user.type(card.getByRole('spinbutton'), '0');

    await user.click(screen.getByRole('button', { name: /Lưu cảnh báo/ }));

    expect(saveReminderSettings).not.toHaveBeenCalled();
  });
});

describe('Conversion — value + unit maps to offsetMinutes on save', () => {
  const saveWith = async (value: string, unitLabel: string) => {
    const user = userEvent.setup();
    await renderReady();
    const card = hostCard();
    await user.click(card.getByRole('checkbox', { name: /Thông báo hệ thống/ }));
    await user.clear(card.getByRole('spinbutton'));
    await user.type(card.getByRole('spinbutton'), value);
    await user.selectOptions(card.getByRole('combobox'), unitLabel);
    await user.click(screen.getByRole('button', { name: /Lưu cảnh báo/ }));
    await waitFor(() => expect(saveReminderSettings).toHaveBeenCalledTimes(1));
    return saveReminderSettings.mock.calls[0][1] as Array<{ channel: string; targetGroup: string; offsetMinutes: number; enabled: boolean }>;
  };

  it('30 phút → 30', async () => {
    const items = await saveWith('30', 'phút');
    const item = items.find((i) => i.channel === 'IN_APP' && i.targetGroup === 'HOST')!;
    expect(item.offsetMinutes).toBe(30);
  });

  it('2 giờ → 120', async () => {
    const items = await saveWith('2', 'giờ');
    const item = items.find((i) => i.channel === 'IN_APP' && i.targetGroup === 'HOST')!;
    expect(item.offsetMinutes).toBe(120);
  });

  it('1 ngày → 1440', async () => {
    const items = await saveWith('1', 'ngày');
    const item = items.find((i) => i.channel === 'IN_APP' && i.targetGroup === 'HOST')!;
    expect(item.offsetMinutes).toBe(1440);
  });

  it('1 tuần → 10080', async () => {
    const items = await saveWith('1', 'tuần');
    const item = items.find((i) => i.channel === 'IN_APP' && i.targetGroup === 'HOST')!;
    expect(item.offsetMinutes).toBe(10080);
  });

  it('90 phút (1 giờ 30 phút) is entered directly as 90, not as 1.5 giờ', async () => {
    const items = await saveWith('90', 'phút');
    const item = items.find((i) => i.channel === 'IN_APP' && i.targetGroup === 'HOST')!;
    expect(item.offsetMinutes).toBe(90);
  });
});

describe('Channel combinations', () => {
  const enabledMap = (items: Array<{ channel: string; targetGroup: string; enabled: boolean }>, group: string) =>
    Object.fromEntries(items.filter((i) => i.targetGroup === group).map((i) => [i.channel, i.enabled]));

  it('Notification only', async () => {
    const user = userEvent.setup();
    await renderReady();
    await user.click(hostCard().getByRole('checkbox', { name: /Thông báo hệ thống/ }));
    await user.click(screen.getByRole('button', { name: /Lưu cảnh báo/ }));
    await waitFor(() => expect(saveReminderSettings).toHaveBeenCalledTimes(1));

    const items = saveReminderSettings.mock.calls[0][1];
    expect(enabledMap(items, 'HOST')).toEqual({ IN_APP: true, EMAIL: false });
  });

  it('Email only', async () => {
    const user = userEvent.setup();
    await renderReady();
    await user.click(hostCard().getByRole('checkbox', { name: /Email/ }));
    await user.click(screen.getByRole('button', { name: /Lưu cảnh báo/ }));
    await waitFor(() => expect(saveReminderSettings).toHaveBeenCalledTimes(1));

    const items = saveReminderSettings.mock.calls[0][1];
    expect(enabledMap(items, 'HOST')).toEqual({ IN_APP: false, EMAIL: true });
  });

  it('both channels', async () => {
    const user = userEvent.setup();
    await renderReady();
    await user.click(hostCard().getByRole('checkbox', { name: /Thông báo hệ thống/ }));
    await user.click(hostCard().getByRole('checkbox', { name: /Email/ }));
    await user.click(screen.getByRole('button', { name: /Lưu cảnh báo/ }));
    await waitFor(() => expect(saveReminderSettings).toHaveBeenCalledTimes(1));

    const items = saveReminderSettings.mock.calls[0][1];
    expect(enabledMap(items, 'HOST')).toEqual({ IN_APP: true, EMAIL: true });
  });

  it('no channel — card is inactive, Save still succeeds without requiring a valid offset', async () => {
    const user = userEvent.setup();
    await renderReady();
    await user.click(screen.getByRole('button', { name: /Lưu cảnh báo/ }));
    await waitFor(() => expect(saveReminderSettings).toHaveBeenCalledTimes(1));

    const items = saveReminderSettings.mock.calls[0][1];
    expect(enabledMap(items, 'HOST')).toEqual({ IN_APP: false, EMAIL: false });
  });
});

describe('Existing configuration — load without losing data', () => {
  it('loads a saved 120-minute offset as "2" + "giờ" with the matching channel checked', async () => {
    getReminderSettings.mockResolvedValue({
      items: [
        { reminderSettingId: 1, channel: 'EMAIL', targetGroup: 'HOST', offsetMinutes: 120, scheduledAt: '2026-08-19T21:00:00', status: 'PENDING' },
      ],
    });

    await renderReady();
    const card = hostCard();
    await waitFor(() => expect((card.getByRole('spinbutton') as HTMLInputElement).value).toBe('2'));
    expect((card.getByRole('combobox') as HTMLSelectElement).value).toBe('HOUR');
    expect((card.getByRole('checkbox', { name: /Email/ }) as HTMLInputElement).checked).toBe(true);
    expect((card.getByRole('checkbox', { name: /Thông báo hệ thống/ }) as HTMLInputElement).checked).toBe(false);
  });

  it('a value that does not divide evenly into a bigger unit stays in phút (no lossy rounding)', async () => {
    getReminderSettings.mockResolvedValue({
      items: [
        { reminderSettingId: 1, channel: 'IN_APP', targetGroup: 'PARTICIPANTS', offsetMinutes: 90, scheduledAt: '2026-08-19T21:00:00', status: 'PENDING' },
      ],
    });

    await renderReady();
    const card = participantsCard();
    await waitFor(() => expect((card.getByRole('spinbutton') as HTMLInputElement).value).toBe('90'));
    expect((card.getByRole('combobox') as HTMLSelectElement).value).toBe('MINUTE');
  });

  it('save then reload keeps the same value and unit (no data loss round-trip)', async () => {
    const user = userEvent.setup();
    await renderReady();
    const card = hostCard();
    await user.click(card.getByRole('checkbox', { name: /Email/ }));
    await user.clear(card.getByRole('spinbutton'));
    await user.type(card.getByRole('spinbutton'), '2');
    await user.selectOptions(card.getByRole('combobox'), 'ngày');

    getReminderSettings.mockResolvedValue({
      items: [
        { reminderSettingId: 1, channel: 'EMAIL', targetGroup: 'HOST', offsetMinutes: 2880, scheduledAt: '2026-08-18T09:00:00', status: 'PENDING' },
      ],
    });
    await user.click(screen.getByRole('button', { name: /Lưu cảnh báo/ }));

    await waitFor(() => expect(getReminderSettings).toHaveBeenCalledTimes(2)); // initial + post-save reload
    await waitFor(() => expect((card.getByRole('spinbutton') as HTMLInputElement).value).toBe('2'));
    expect((card.getByRole('combobox') as HTMLSelectElement).value).toBe('DAY');
  });
});

describe('Regression — layout and existing Save/Cancel behavior untouched', () => {
  it('still exactly 2 cards laid out on a 2-column grid', async () => {
    await renderReady();
    const grid = screen.getByTestId('reminder-card-HOST').parentElement!;
    expect(grid.className).toContain('md:grid-cols-2');
    expect(grid.children).toHaveLength(2);
  });

  it('Save still calls saveReminderSettings and Cancel still calls cancelReminderSettings', async () => {
    const user = userEvent.setup();
    await renderReady();

    await user.click(screen.getByRole('button', { name: /Lưu cảnh báo/ }));
    await waitFor(() => expect(saveReminderSettings).toHaveBeenCalledTimes(1));

    await user.click(screen.getByRole('button', { name: /Tắt tất cả cảnh báo/ }));
    await waitFor(() => expect(cancelReminderSettings).toHaveBeenCalledTimes(1));
  });
});
