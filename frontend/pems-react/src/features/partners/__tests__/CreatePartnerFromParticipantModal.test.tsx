import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { CreatePartnerFromParticipantModal } from '../components/CreatePartnerFromParticipantModal';

/**
 * "Tạo hoặc liên kết đối tác" từ một dòng người tham gia — the P1-core validation-highlight fix
 * (PEMS_PROMPT_FIX_VALIDATION_UX_PLURALIZATION §5). Before this, an empty "Tên đối tác" was a
 * banner-only message and a silently-disabled Submit; a malformed Website (the one field the backend
 * actually format-checks — `CreatePartnerCommandValidator.WebsiteUrl`) had NO client check at all.
 */

vi.mock('../api/partnersApi', () => ({
  partnersApi: {
    matchPartner: vi.fn(),
    createPartnerFromGuest: vi.fn(),
    getPartnerDetail: vi.fn(),
    linkGuestToPartner: vi.fn(),
  },
}));

import { partnersApi } from '../api/partnersApi';

const noop = () => {};

const axiosError = (fields: Record<string, string[]>) =>
  Object.assign(new Error('400'), {
    isAxiosError: true,
    response: { status: 400, data: { errorCode: 'VALIDATION_ERROR', message: 'nope', errors: fields } },
  });

describe('CreatePartnerFromParticipantModal — validation highlighting', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(partnersApi.matchPartner).mockResolvedValue({ matchStatus: 'NONE', candidates: [] } as never);
  });

  it('blocks an empty partner name and focuses the field, without calling the API', async () => {
    render(
      <CreatePartnerFromParticipantModal
        open visitInstanceId={1} guestMemberId={2} minuteParticipantId={null}
        onClose={noop} onDone={noop}
      />,
    );
    const nameInput = await screen.findByTestId('create-partner-field-name');
    fireEvent.click(screen.getByRole('button', { name: 'Tạo đối tác' }));

    await waitFor(() => expect(nameInput.closest('[data-field-error="true"]')).not.toBeNull());
    expect(nameInput).toHaveAttribute('aria-invalid', 'true');
    await waitFor(() => expect(document.activeElement).toBe(nameInput));
    expect(partnersApi.createPartnerFromGuest).not.toHaveBeenCalled();
  });

  it('rejects a malformed website URL client-side, mirroring the backend Uri.TryCreate rule', async () => {
    render(
      <CreatePartnerFromParticipantModal
        open visitInstanceId={1} guestMemberId={2} minuteParticipantId={null}
        onClose={noop} onDone={noop}
      />,
    );
    fireEvent.change(await screen.findByTestId('create-partner-field-name'), { target: { value: 'Đại học Y' } });
    const websiteInput = screen.getByTestId('create-partner-field-websiteUrl');
    fireEvent.change(websiteInput, { target: { value: 'not a url' } });
    fireEvent.click(screen.getByRole('button', { name: 'Tạo đối tác' }));

    await waitFor(() => expect(websiteInput.closest('[data-field-error="true"]')).not.toBeNull());
    expect(screen.getByTestId('create-partner-field-name').closest('[data-field-error="true"]')).toBeNull();
    expect(partnersApi.createPartnerFromGuest).not.toHaveBeenCalled();
  });

  it('accepts a bare domain (no scheme) for website, same as the backend does', async () => {
    vi.mocked(partnersApi.createPartnerFromGuest).mockResolvedValue({
      partnerId: 5, profileStatus: 'PENDING_APPROVAL', linkId: 1, initialContactId: null,
    });
    const onDone = vi.fn();
    render(
      <CreatePartnerFromParticipantModal
        open visitInstanceId={1} guestMemberId={2} minuteParticipantId={null}
        onClose={noop} onDone={onDone}
      />,
    );
    fireEvent.change(await screen.findByTestId('create-partner-field-name'), { target: { value: 'Đại học Y' } });
    fireEvent.change(screen.getByTestId('create-partner-field-websiteUrl'), { target: { value: 'example.edu.vn' } });
    fireEvent.click(screen.getByRole('button', { name: 'Tạo đối tác' }));

    await waitFor(() => expect(partnersApi.createPartnerFromGuest).toHaveBeenCalledTimes(1));
    expect(onDone).toHaveBeenCalledTimes(1);
  });

  it('maps a backend WebsiteUrl validation error onto the field without losing the typed name', async () => {
    vi.mocked(partnersApi.createPartnerFromGuest).mockRejectedValue(
      axiosError({ WebsiteUrl: ['Website không hợp lệ.'] }),
    );
    render(
      <CreatePartnerFromParticipantModal
        open visitInstanceId={1} guestMemberId={2} minuteParticipantId={null}
        onClose={noop} onDone={noop}
      />,
    );
    const nameInput = await screen.findByTestId('create-partner-field-name');
    fireEvent.change(nameInput, { target: { value: 'Đại học Y' } });
    fireEvent.click(screen.getByRole('button', { name: 'Tạo đối tác' }));

    await waitFor(() => expect(partnersApi.createPartnerFromGuest).toHaveBeenCalledTimes(1));
    const errorText = await screen.findByText('Website không hợp lệ.');
    expect(errorText).toHaveAttribute('role', 'alert');
    // Không mất dữ liệu đã nhập khi API fail.
    expect(nameInput).toHaveValue('Đại học Y');
  });

  it('still routes a 409 name-duplicate refusal through the existing link-suggestion banner, unchanged', async () => {
    vi.mocked(partnersApi.createPartnerFromGuest).mockRejectedValue(
      Object.assign(new Error('409'), {
        isAxiosError: true,
        response: { status: 409, data: { errorCode: 'PARTNER_NAME_DUPLICATED', message: 'trùng tên' } },
      }),
    );
    vi.mocked(partnersApi.matchPartner).mockResolvedValueOnce({ matchStatus: 'NONE', candidates: [] } as never)
      .mockResolvedValueOnce({
        matchStatus: 'MATCHED',
        candidates: [{
          partnerId: 3, name: 'Đại học Y', profileStatus: 'APPROVED', visibility: 'PUBLIC',
          ownerCampusId: 1, ownerCampusName: 'FPTU', country: null, city: null,
          matchScore: 95, matchReason: 'Tên trùng', canLink: true, blockedReason: null, recommendedAction: 'LINK',
        }],
      } as never);
    render(
      <CreatePartnerFromParticipantModal
        open visitInstanceId={1} guestMemberId={2} minuteParticipantId={null}
        onClose={noop} onDone={noop}
      />,
    );
    fireEvent.change(await screen.findByTestId('create-partner-field-name'), { target: { value: 'Đại học Y' } });
    fireEvent.click(screen.getByRole('button', { name: 'Tạo đối tác' }));

    await screen.findByText(/Tên đối tác đã tồn tại/);
    expect(screen.getByTestId('create-partner-field-name').closest('[data-field-error="true"]')).toBeNull();
  });
});
