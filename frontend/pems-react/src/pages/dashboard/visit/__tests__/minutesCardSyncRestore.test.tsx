/**
 * Removing a person from the biên bản, then pressing "Đồng bộ người mới".
 *
 * <b>What this pins.</b> Deleting a row in the editor changes the DRAFT only — nothing is written until
 * Save, which is deliberate: an accidental click must not mutate a meeting record. But "Đồng bộ người
 * mới" asks the backend which source people are MISSING from the biên bản, and the backend answers from
 * the database, where the deleted row is still present. So it reported the person as already recorded
 * and returned nothing: the row was gone from the screen and no sync could bring it back. The only way
 * out was to cancel the whole editing session and lose every other change.
 *
 * The fix holds deleted persisted rows for the length of the session and restores them on sync — WITH
 * their original minuteParticipantId, which is the part that matters at Save: a row carrying its id is
 * updated in place, while a row without one is an insert, and the original would then be deleted as
 * "dropped by the client". Same person, new id, lost attendance history.
 *
 * What must NOT happen is auto-restore: deleting and saving still deletes. Sync is the undo, not time.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MinutesCard } from '../MinutesCard';
import { delegationsApi } from '../../../../features/delegations/api/delegationsApi';
import { partnersApi } from '../../../../features/partners/api/partnersApi';

vi.mock('../../../../features/delegations/api/delegationsApi', () => ({
  delegationsApi: {
    minutes: {
      get: vi.fn(),
      createOrLock: vi.fn(),
      acquireLock: vi.fn(),
      save: vi.fn(),
      releaseLock: vi.fn(),
      newParticipantCandidates: vi.fn(),
    },
    getAgendaResponsibleCandidates: vi.fn(),
  },
}));
vi.mock('../../../../features/partners/api/partnersApi', () => ({
  partnersApi: { getVisitPartnerLinks: vi.fn() },
}));
// Quill belongs to the editor's own suite; here it is only the note field of a form.
vi.mock('../../../../shared/components/RichTextEditor', () => ({
  RichTextEditor: ({ value, onChange }: any) => (
    <textarea aria-label="noi-dung" value={value} onChange={(e) => onChange(e.target.value)} />
  ),
}));
vi.mock('../../../../features/partners/components/ParticipantPartnerCell', () => ({
  ParticipantPartnerCell: () => null,
}));

const VISIT_INSTANCE_ID = 88;
const MINUTES_ID = 5;

/** An internal participant already saved in the biên bản. */
const savedHost = {
  minuteParticipantId: 11, userId: 100, guestMemberId: null,
  fullNameSnapshot: 'Nguyễn Văn Host', roleSnapshot: 'Host', organizationSnapshot: 'Phòng HTQT',
  emailSnapshot: 'host@fpt.edu.vn', attendanceStatus: 'PRESENT', attendanceNote: '',
  participantKind: 'INTERNAL', guestNationality: null,
};
/** A guest already saved in the biên bản — the row these tests delete and re-sync. */
const savedGuest = {
  minuteParticipantId: 12, userId: null, guestMemberId: 205,
  fullNameSnapshot: 'Nguyễn Văn A', roleSnapshot: 'Manager', organizationSnapshot: 'Đại học ABC',
  emailSnapshot: null, attendanceStatus: 'PRESENT', attendanceNote: 'ghi chú cũ',
  participantKind: 'GUEST', guestNationality: 'Vietnam',
};

const lockedMinute = (participants: any[] = [savedHost, savedGuest]) => ({
  exists: true, minutesId: MINUTES_ID, visitInstanceId: VISIT_INSTANCE_ID,
  title: 'Biên bản cuộc họp', content: '<p>noi dung</p>', status: 'SAVED', rowVersion: 3,
  updatedAt: '2026-08-01T10:00:00+07:00',
  editLockToken: 'lock-token', editLockExpiresAt: new Date(Date.now() + 10 * 60_000).toISOString(),
  isLockedByOther: false, isLockedByMe: true,
  canView: true, canCreate: false, canEdit: true,
  participants, actionItems: [],
});

/** Renders the card and opens an editing session on it. */
async function renderEditing(user: ReturnType<typeof userEvent.setup>) {
  render(<MinutesCard visitInstanceId={VISIT_INSTANCE_ID} />);
  await screen.findByDisplayValue('Biên bản cuộc họp');
  await user.click(screen.getByRole('button', { name: /Sửa biên bản/ }));
  await screen.findByRole('button', { name: /Lưu biên bản/ });
}

/** The participant table row for a person, by the name shown in it. */
const rowOf = (fullName: string) => screen.getByText(fullName).closest('tr')!;
const nameIsListed = (fullName: string) => screen.queryAllByText(fullName).length;
/** The row's delete control — the trailing button of the row, after the attendance tick. */
const deleteButtonIn = (row: HTMLElement) => within(row).getAllByRole('button').at(-1)!;

const clickSync = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.click(screen.getByRole('button', { name: /Đồng bộ người mới/ }));
  await waitFor(() => expect(delegationsApi.minutes.newParticipantCandidates).toHaveBeenCalled());
};

/** The participants array of the most recent save call. */
const savedParticipants = () =>
  (vi.mocked(delegationsApi.minutes.save).mock.calls.at(-1) as any)[1].participants;

describe('MinutesCard — deleting a participant then syncing', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(delegationsApi.minutes.get).mockResolvedValue(lockedMinute() as any);
    vi.mocked(delegationsApi.minutes.acquireLock).mockResolvedValue(lockedMinute() as any);
    vi.mocked(delegationsApi.minutes.releaseLock).mockResolvedValue({} as any);
    vi.mocked(delegationsApi.minutes.save).mockResolvedValue(lockedMinute() as any);
    // The backend answers from the DATABASE, where the just-deleted row still exists: nobody is
    // missing, so it offers nobody. This is the exact condition the fix has to work under.
    vi.mocked(delegationsApi.minutes.newParticipantCandidates).mockResolvedValue([] as any);
    vi.mocked(delegationsApi.getAgendaResponsibleCandidates).mockResolvedValue([] as any);
    vi.mocked(partnersApi.getVisitPartnerLinks).mockResolvedValue([] as any);
  });

  // ── TC-SYNC-01 ─────────────────────────────────────────────────────────────────────────────────
  it('restores the deleted person, keeping the row the database already has', async () => {
    const user = userEvent.setup();
    await renderEditing(user);

    await user.click(deleteButtonIn(rowOf('Nguyễn Văn A')));
    expect(nameIsListed('Nguyễn Văn A')).toBe(0);

    await clickSync(user);

    await waitFor(() => expect(nameIsListed('Nguyễn Văn A')).toBe(1));
    // Saving now proves the restored row is the ORIGINAL one: a restore that dropped the id would
    // send null here, and the backend would delete row 12 and insert a second person in its place.
    await user.click(screen.getByRole('button', { name: /Lưu biên bản/ }));
    await waitFor(() => expect(delegationsApi.minutes.save).toHaveBeenCalled());
    const guestRow = savedParticipants().find((p: any) => p.guestMemberId === 205);
    expect(guestRow.minuteParticipantId).toBe(12);
    // Its attendance came back with it, rather than resetting to a fresh row's default.
    expect(guestRow.attendanceStatus).toBe('PRESENT');
    expect(guestRow.attendanceNote).toBe('ghi chú cũ');
  });

  // ── TC-SYNC-02 ─────────────────────────────────────────────────────────────────────────────────
  it('syncing repeatedly lists the restored person exactly once', async () => {
    const user = userEvent.setup();
    await renderEditing(user);

    await user.click(deleteButtonIn(rowOf('Nguyễn Văn A')));
    await clickSync(user);
    await waitFor(() => expect(nameIsListed('Nguyễn Văn A')).toBe(1));
    await clickSync(user);
    await clickSync(user);

    await waitFor(() => expect(delegationsApi.minutes.newParticipantCandidates).toHaveBeenCalledTimes(3));
    expect(nameIsListed('Nguyễn Văn A')).toBe(1);
  });

  // ── TC-SYNC-03 ─────────────────────────────────────────────────────────────────────────────────
  it('deleting and saving WITHOUT syncing still removes the person', async () => {
    const user = userEvent.setup();
    await renderEditing(user);

    await user.click(deleteButtonIn(rowOf('Nguyễn Văn A')));
    await user.click(screen.getByRole('button', { name: /Lưu biên bản/ }));

    await waitFor(() => expect(delegationsApi.minutes.save).toHaveBeenCalled());
    // Omitted from the payload is how the backend is told to drop it — the fix must not smuggle it back.
    expect(savedParticipants().some((p: any) => p.guestMemberId === 205)).toBe(false);
    expect(savedParticipants()).toHaveLength(1);
  });

  // ── TC-SYNC-04 ─────────────────────────────────────────────────────────────────────────────────
  it('delete → sync → save keeps the person, and only once', async () => {
    const user = userEvent.setup();
    await renderEditing(user);

    await user.click(deleteButtonIn(rowOf('Nguyễn Văn A')));
    await clickSync(user);
    await waitFor(() => expect(nameIsListed('Nguyễn Văn A')).toBe(1));
    await user.click(screen.getByRole('button', { name: /Lưu biên bản/ }));

    await waitFor(() => expect(delegationsApi.minutes.save).toHaveBeenCalled());
    expect(savedParticipants().filter((p: any) => p.guestMemberId === 205)).toHaveLength(1);
    expect(savedParticipants()).toHaveLength(2);
  });

  // ── TC-SYNC-05 ─────────────────────────────────────────────────────────────────────────────────
  it('a manually added person is not restored by a sync — there is no source to sync from', async () => {
    const user = userEvent.setup();
    await renderEditing(user);

    await user.click(screen.getByRole('button', { name: /Thêm người tham gia/ }));
    const manualInput = await screen.findByPlaceholderText('Nhập họ tên...');
    await user.type(manualInput, 'Khách ngoài hệ thống');
    await user.click(deleteButtonIn(manualInput.closest('tr')!));

    await clickSync(user);

    expect(screen.queryByDisplayValue('Khách ngoài hệ thống')).toBeNull();
  });

  /**
   * The held rows describe the database as it was when the session started. Cancelling abandons that
   * session, so a later one must not be able to restore from it — by then the record may have moved on.
   */
  it('forgets the deletions when the editing session is cancelled', async () => {
    const user = userEvent.setup();
    await renderEditing(user);

    await user.click(deleteButtonIn(rowOf('Nguyễn Văn A')));
    await user.click(screen.getByRole('button', { name: /Hủy chỉnh sửa/ }));
    await waitFor(() => expect(delegationsApi.minutes.releaseLock).toHaveBeenCalled());

    // A new session on the SAME data: the person is present again (nothing was ever saved), and a
    // sync in this session has nothing of its own to restore.
    await user.click(await screen.findByRole('button', { name: /Sửa biên bản/ }));
    await screen.findByRole('button', { name: /Lưu biên bản/ });
    expect(nameIsListed('Nguyễn Văn A')).toBe(1);

    await clickSync(user);
    expect(nameIsListed('Nguyễn Văn A')).toBe(1);
  });

  /**
   * An edit made WHILE the sync request is in flight survives the response.
   *
   * <para>
   * The sync reads the draft, awaits the server, then writes the draft back. Computing the new list
   * from the copy captured before the await would silently discard anything the user did in between —
   * a sync takes a moment, and ticking attendance during it is entirely natural. Merging against the
   * CURRENT draft inside a functional update is what makes the round trip additive instead of
   * last-write-wins.
   * </para>
   */
  it('does not overwrite draft edits made while the sync is in flight', async () => {
    let releaseCandidates: (rows: any[]) => void = () => {};
    vi.mocked(delegationsApi.minutes.newParticipantCandidates).mockReturnValue(
      new Promise<any>((resolve) => { releaseCandidates = resolve; }));
    const user = userEvent.setup();
    await renderEditing(user);

    // Host starts PRESENT (from the saved minute) and the guest is deleted, so the sync has both a
    // restore and an addition to apply.
    await user.click(deleteButtonIn(rowOf('Nguyễn Văn A')));
    await user.click(screen.getByRole('button', { name: /Đồng bộ người mới/ }));
    await waitFor(() => expect(delegationsApi.minutes.newParticipantCandidates).toHaveBeenCalled());

    // Mid-flight: the user un-ticks the host's attendance.
    const hostAttendance = within(rowOf('Nguyễn Văn Host')).getAllByRole('button')[0];
    await user.click(hostAttendance);
    expect(hostAttendance).toHaveAttribute('aria-pressed', 'false');

    releaseCandidates([{
      minuteParticipantId: 0, userId: 301, guestMemberId: null,
      fullNameSnapshot: 'Trần Thị Mới', roleSnapshot: 'Cán bộ IC', organizationSnapshot: 'Phòng HTQT',
      emailSnapshot: 'moi@fpt.edu.vn', attendanceStatus: 'ABSENT', attendanceNote: null,
      participantKind: 'INTERNAL',
    }]);

    await waitFor(() => expect(screen.queryAllByDisplayValue('Trần Thị Mới')).toHaveLength(1));
    // The response applied its restore and its addition WITHOUT reverting the mid-flight edit.
    expect(within(rowOf('Nguyễn Văn Host')).getAllByRole('button')[0])
      .toHaveAttribute('aria-pressed', 'false');
    expect(nameIsListed('Nguyễn Văn A')).toBe(1);

    await user.click(screen.getByRole('button', { name: /Lưu biên bản/ }));
    await waitFor(() => expect(delegationsApi.minutes.save).toHaveBeenCalled());
    // What is persisted is the user's latest intent, not the snapshot the sync started from.
    expect(savedParticipants().find((p: any) => p.userId === 100).attendanceStatus).toBe('ABSENT');
  });

  /**
   * Restoring must not crowd out the sync's real job: a genuinely new person still arrives, as a new
   * row (no id), alongside the restored one.
   */
  it('still adds people the backend reports as new', async () => {
    vi.mocked(delegationsApi.minutes.newParticipantCandidates).mockResolvedValue([{
      minuteParticipantId: 0, userId: 301, guestMemberId: null,
      fullNameSnapshot: 'Trần Thị Mới', roleSnapshot: 'Cán bộ IC', organizationSnapshot: 'Phòng HTQT',
      emailSnapshot: 'moi@fpt.edu.vn', attendanceStatus: 'ABSENT', attendanceNote: null,
      participantKind: 'INTERNAL',
    }] as any);
    const user = userEvent.setup();
    await renderEditing(user);

    await user.click(deleteButtonIn(rowOf('Nguyễn Văn A')));
    await clickSync(user);

    // A newly synced row has no id yet, so the card renders its name as an editable field, while the
    // restored row keeps its id and stays read-only text — the two arrive by different paths and this
    // is what tells them apart on screen.
    await waitFor(() => expect(screen.queryAllByDisplayValue('Trần Thị Mới')).toHaveLength(1));
    expect(nameIsListed('Nguyễn Văn A')).toBe(1);

    await user.click(screen.getByRole('button', { name: /Lưu biên bản/ }));
    await waitFor(() => expect(delegationsApi.minutes.save).toHaveBeenCalled());
    expect(savedParticipants().find((p: any) => p.userId === 301).minuteParticipantId).toBeNull();
    expect(savedParticipants().find((p: any) => p.guestMemberId === 205).minuteParticipantId).toBe(12);
  });
});
