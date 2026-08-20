/**
 * Process page cleanup (§G2): the always-visible "Chỉ Host phụ trách mới có thể mời thành phần tham
 * gia. Danh sách dưới đây ở chế độ xem." sentence is gone. A viewer who cannot manage participants
 * already sees this from the invite controls simply not being there — repeating it in prose added
 * nothing a screen reader or a sighted user did not already get from the missing buttons.
 */
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ParticipantInvitationSection } from '../components/ParticipantInvitationSection';

describe('ParticipantInvitationSection — read-only viewer', () => {
  it('does not render the "only the Host may invite" helper sentence for a non-Host viewer', () => {
    render(
      <ParticipantInvitationSection
        visitInstanceId={501}
        relation="STAFF_LEADER"
        instanceStatus="BEFORE_VISIT"
        currentUserId={9}
        host={null}
        participants={[]}
        onChanged={() => {}}
        pushToast={vi.fn()}
      />,
    );

    expect(screen.queryByText(/Chỉ Host phụ trách mới có thể mời/)).not.toBeInTheDocument();
    // The invite controls are hidden the same way the sentence used to explain — that is the actual
    // "view only" signal now.
    expect(screen.queryByPlaceholderText('Tìm theo tên...')).not.toBeInTheDocument();
  });
});
