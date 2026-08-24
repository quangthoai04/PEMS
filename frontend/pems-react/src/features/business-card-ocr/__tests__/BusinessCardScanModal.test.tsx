import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BusinessCardScanModal } from '../components/BusinessCardScanModal';
import type { BusinessCardOcrJob } from '../types/businessCardOcr.types';

/**
 * Plan CanhIter3FixBug "Partner Contact / Business Card Data Capture" — the reviewer confirming an OCR
 * draft is allowed to save the actual text read off the card (extension, foreign format, garbled OCR
 * text). Before this fix, `confirm()` re-ran the Visit-domain phone/email format rule client-side and
 * blocked exactly these values with the same message the backend used to reject them with.
 */

vi.mock('../api/businessCardOcrApi', () => ({
  businessCardOcrApi: { scan: vi.fn(), getJob: vi.fn(), confirmContact: vi.fn(), discard: vi.fn() },
}));
vi.mock('../../partners/api/partnersApi', () => ({
  partnersApi: { getPartners: vi.fn().mockResolvedValue({ items: [] }), createPartner: vi.fn() },
}));

import { businessCardOcrApi } from '../api/businessCardOcrApi';

const job = (overrides: Partial<BusinessCardOcrJob> = {}): BusinessCardOcrJob => ({
  ocrJobId: 501,
  status: 'SUCCEEDED',
  providerName: 'GOOGLE_DOCUMENT_AI',
  confidenceScore: 92,
  scannedCardFileId: 1,
  parsed: {
    fullName: 'Kim Min Jae',
    email: 'kim.minjae@seoultech.example',
    phone: '+821012340001',
    jobTitle: 'International Partnerships Manager',
    organization: 'SeoulTech Global Engagement Center',
  },
  matchedPartner: null,
  createdAt: '2026-08-24T00:00:00',
  ...overrides,
});

async function openAtReviewStep(context: { partnerId?: number | null; partnerName?: string | null } = { partnerId: 7, partnerName: 'SeoulTech' }) {
  vi.mocked(businessCardOcrApi.scan).mockResolvedValue(job());
  const onConfirmed = vi.fn();
  render(<BusinessCardScanModal open onClose={() => {}} context={context} onConfirmed={onConfirmed} />);

  const file = new File(['x'], 'card.jpg', { type: 'image/jpeg' });
  const input = document.querySelector('input[type="file"]') as HTMLInputElement;
  fireEvent.change(input, { target: { files: [file] } });
  fireEvent.click(await screen.findByRole('button', { name: 'Quét danh thiếp' }));
  await screen.findByText('Họ tên *');
  return onConfirmed;
}

describe('BusinessCardScanModal — confirm step', () => {
  beforeEach(() => vi.clearAllMocks());

  it('F10: an arbitrary/foreign phone with an extension does not block confirm', async () => {
    vi.mocked(businessCardOcrApi.confirmContact).mockResolvedValue({
      ocrJobId: 501, status: 'CONFIRMED', partnerId: 7, contactId: 99,
    });
    const onConfirmed = await openAtReviewStep();

    const phoneInputs = screen.getAllByRole('textbox');
    // Second grid row: Email then Phone — locate by current value instead of order to stay robust.
    const phoneInput = screen.getByDisplayValue('+821012340001');
    fireEvent.change(phoneInput, { target: { value: '+1 (212) 555-1234 ext. 208' } });

    const confirmBtn = screen.getByRole('button', { name: 'Lưu người liên hệ' });
    expect(confirmBtn).not.toBeDisabled();
    fireEvent.click(confirmBtn);

    await waitFor(() => expect(businessCardOcrApi.confirmContact).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(businessCardOcrApi.confirmContact).mock.calls[0];
    expect(payload.phone).toBe('+1 (212) 555-1234 ext. 208');
    expect(onConfirmed).toHaveBeenCalledWith({ partnerId: 7, contactId: 99 });
    void phoneInputs;
  });

  it('F9/F11: a nonstandard/garbled phone AND email do not block confirm', async () => {
    vi.mocked(businessCardOcrApi.confirmContact).mockResolvedValue({
      ocrJobId: 501, status: 'CONFIRMED', partnerId: 7, contactId: 100,
    });
    await openAtReviewStep();

    fireEvent.change(screen.getByDisplayValue('+821012340001'), { target: { value: 'ádsad' } });
    fireEvent.change(screen.getByDisplayValue('kim.minjae@seoultech.example'), { target: { value: 'not-an-email' } });
    fireEvent.click(screen.getByRole('button', { name: 'Lưu người liên hệ' }));

    await waitFor(() => expect(businessCardOcrApi.confirmContact).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(businessCardOcrApi.confirmContact).mock.calls[0];
    expect(payload.phone).toBe('ádsad');
    expect(payload.email).toBe('not-an-email');
    expect(screen.queryByText(/không hợp lệ|không đúng định dạng/i)).toBeNull();
  });

  it('F12: a missing FullName still blocks confirm (only real requirement left)', async () => {
    await openAtReviewStep();
    const nameInput = screen.getByDisplayValue('Kim Min Jae');
    fireEvent.change(nameInput, { target: { value: '   ' } });

    expect(screen.getByRole('button', { name: 'Lưu người liên hệ' })).toBeDisabled();
    expect(businessCardOcrApi.confirmContact).not.toHaveBeenCalled();
  });

  it('maps a backend Phone length error onto the field instead of a generic toast', async () => {
    vi.mocked(businessCardOcrApi.confirmContact).mockRejectedValue(
      Object.assign(new Error('422'), {
        isAxiosError: true,
        response: { status: 422, data: { errorCode: 'VALIDATION_ERROR', errors: { Phone: ['Phone quá dài.'] } } },
      }),
    );
    await openAtReviewStep();
    fireEvent.click(screen.getByRole('button', { name: 'Lưu người liên hệ' }));

    await waitFor(() => expect(businessCardOcrApi.confirmContact).toHaveBeenCalledTimes(1));
    expect(await screen.findByText('Phone quá dài.')).toBeInTheDocument();
  });
});
