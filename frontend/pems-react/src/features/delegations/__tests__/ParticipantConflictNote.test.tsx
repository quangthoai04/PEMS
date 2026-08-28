import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';

/**
 * The schedule note in the invite dropdowns (Staff hỗ trợ IC / Sinh viên hỗ trợ) is prose, not a
 * chip: lowercase italic text carrying its meaning in colour alone. It used to be a bordered,
 * tinted, bold pill, which read as a status of the row rather than as a remark about the person —
 * and stacked two pill shapes ("Không trùng lịch" + "Chưa có email") into one cramped line.
 *
 * The invitation StatusBadge is deliberately NOT covered here: an invitation really is in a state,
 * so it keeps the chip.
 */

vi.mock('../api/delegationsApi', () => ({
  delegationsApi: {
    getParticipantCandidates: vi.fn(),
    getSupportDepartments: vi.fn(),
    previewEmailTemplate: vi.fn(),
    inviteVisitParticipant: vi.fn(),
    removeVisitParticipant: vi.fn(),
    getParticipantSentEmails: vi.fn(),
  },
}));
vi.mock('../components/EmailPreviewModal', () => ({ EmailPreviewModal: () => null }));
vi.mock('../components/SentEmailsModal', () => ({ SentEmailsModal: () => null }));

import { delegationsApi } from '../api/delegationsApi';
import { ParticipantInvitationSection } from '../components/ParticipantInvitationSection';

const api = delegationsApi as unknown as Record<string, ReturnType<typeof vi.fn>>;

const candidate = (over: Record<string, unknown> = {}) => ({
  userId: 9,
  fullName: 'Trần Văn Support',
  email: 'support@fpt.edu.vn',
  departmentName: 'Phòng Hợp tác Quốc tế',
  campusName: 'Hòa Lạc',
  subRole: 'STAFF',
  studentCode: null,
  conflictCount: 0,
  hasPrivateConflict: false,
  canInvite: true,
  ...over,
});

function renderSection() {
  return render(
    <ParticipantInvitationSection
      visitInstanceId={5}
      relation="HOST"
      instanceStatus="BEFORE_VISIT"
      currentUserId={100}
      host={{ userId: 100, fullName: 'Nguyễn Văn Host', departmentName: 'IC', statusLabel: 'Đã được phân công' } as never}
      participants={[]}
      onChanged={() => {}}
      pushToast={() => {}}
      delegationName="Đoàn ABC"
      campusName="Hòa Lạc"
      plannedStartAt="2026-08-01T09:00"
      plannedEndAt="2026-08-01T11:00"
    />,
  );
}

/** The note element itself — the <span> wrapping the label text. */
const noteFor = (text: string | RegExp) => screen.getByText(text).closest('span')!;

const expectsProseNote = (note: HTMLElement, colour: string) => {
  expect(note.className).toContain('italic');
  expect(note.className).toContain('font-normal');
  expect(note.className).toContain(colour);
  // No pill left: no rounded corners, no border, no tinted fill, and not bold.
  expect(note.className).not.toContain('rounded');
  expect(note.className).not.toContain('border');
  expect(note.className).not.toContain('bg-');
  expect(note.className).not.toContain('font-bold');
};

describe('the candidate schedule note is italic prose, not a chip', () => {
  beforeEach(() => {
    Object.values(api).forEach((fn) => fn.mockReset());
    api.getParticipantCandidates.mockResolvedValue([]);
    api.getSupportDepartments.mockResolvedValue([]);
  });

  it('renders "Không trùng lịch" in green italic for a free IC staff candidate', async () => {
    api.getParticipantCandidates.mockResolvedValue([candidate()]);
    renderSection();

    fireEvent.focus(screen.getByPlaceholderText('Tìm theo tên...'));
    await waitFor(() => expect(screen.getByText('Không trùng lịch')).toBeInTheDocument());

    expectsProseNote(noteFor('Không trùng lịch'), 'text-emerald-700');
  });

  it('renders the clash count in amber italic, keeping the warning icon', async () => {
    api.getParticipantCandidates.mockResolvedValue([candidate({ conflictCount: 2 })]);
    renderSection();

    fireEvent.focus(screen.getByPlaceholderText('Tìm theo tên...'));
    await waitFor(() => expect(screen.getByText(/Có 2 lịch trùng/)).toBeInTheDocument());

    const note = noteFor(/Có 2 lịch trùng/);
    expectsProseNote(note, 'text-amber-700');
    expect(note.querySelector('svg')).not.toBeNull();
  });

  it('applies to the student dropdown too', async () => {
    api.getParticipantCandidates.mockResolvedValue([
      candidate({ userId: 11, fullName: 'Sinh Viên Hỗ Trợ', studentCode: 'SE190001' }),
    ]);
    renderSection();

    fireEvent.focus(screen.getByPlaceholderText('Tìm theo tên / mã SV...'));
    await waitFor(() => expect(screen.getByText('Không trùng lịch')).toBeInTheDocument());

    expectsProseNote(noteFor('Không trùng lịch'), 'text-emerald-700');
  });
});
