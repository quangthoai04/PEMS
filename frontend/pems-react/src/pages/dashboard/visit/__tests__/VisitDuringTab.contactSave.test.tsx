import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { VisitDuringTab } from '../VisitDuringTab';
import type { BusinessCardOcrJob } from '../../../../features/business-card-ocr/types/businessCardOcr.types';
import type { VisitProcessGuestMember } from '../../../../features/delegations/types/delegations.types';

/**
 * Plan CanhIter3FixBug "Partner Contact / Business Card Data Capture" — /dashboard/visit/process/47123
 * "Scan Card Visit" → "Lưu thông tin liên hệ" is the EXACT screen the bug was reported on. Neither the
 * OCR-confirm path nor the no-OCR manual path in this component ever ran a client-side phone/email
 * format check (confirmed by reading the source before writing this file) — the rejection came from the
 * backend validator, now relaxed. These tests pin BOTH paths accept arbitrary phone/email text and send
 * it through unmodified, matching F13-F16.
 */

vi.mock('../MinutesCard', () => ({ MinutesCard: () => null }));
vi.mock('../../../../features/business-card-ocr/api/businessCardOcrApi', () => ({
  businessCardOcrApi: { scan: vi.fn(), confirmContact: vi.fn() },
}));
vi.mock('../../../../features/partners/api/partnersApi', () => ({
  partnersApi: {
    matchPartner: vi.fn().mockResolvedValue({ partnerName: null, candidates: [] }),
    getPartners: vi.fn().mockResolvedValue({ items: [] }),
    createPartner: vi.fn().mockResolvedValue({ partnerId: 7 }),
    createContact: vi.fn(),
  },
}));
vi.mock('../../../../features/delegations/api/visitDocumentsApi', () => ({
  visitDocumentsApi: { upload: vi.fn() },
}));

import { businessCardOcrApi } from '../../../../features/business-card-ocr/api/businessCardOcrApi';
import { partnersApi } from '../../../../features/partners/api/partnersApi';

const job = (overrides: Partial<BusinessCardOcrJob> = {}): BusinessCardOcrJob => ({
  ocrJobId: 501,
  status: 'SUCCEEDED',
  providerName: 'GOOGLE_DOCUMENT_AI',
  scannedCardFileId: 1,
  parsed: {
    fullName: 'Kim Min Jae',
    email: 'kim.minjae@seoultech.example',
    phone: '+821012340001',
    jobTitle: 'International Partnerships Manager',
    organization: 'SeoulTech',
  },
  matchedPartner: null,
  createdAt: '2026-08-24T00:00:00',
  ...overrides,
});

// getAvailablePartners()'s "drafts" group is derived from guest/support members' organization field —
// giving the render one delegation member makes "SeoulTech" a real, selectable <option>.
const guestMembers: VisitProcessGuestMember[] = [
  { guestMemberId: 1, memberType: 'GUEST', fullName: 'Đoàn viên A', organization: 'SeoulTech', displayOrder: 1 },
];

function cardFileInput(): HTMLInputElement {
  const inputs = Array.from(document.querySelectorAll('input[type="file"]')) as HTMLInputElement[];
  const found = inputs.find(i => i.accept === 'image/*');
  if (!found) throw new Error('Card Visit file input not found');
  return found;
}

/** The "SĐT" (phone) field has no id/label association in the source markup — located structurally. */
function phoneField(): HTMLInputElement {
  const label = screen.getByText('SĐT').closest('div')!;
  return label.querySelector('input') as HTMLInputElement;
}

function partnerSelect(): HTMLSelectElement {
  return screen.getByText('Chọn đối tác liên kết thông tin của Card Visit này')
    .closest('div')!.querySelector('select') as HTMLSelectElement;
}

async function scanCard() {
  const file = new File(['x'], 'card.jpg', { type: 'image/jpeg' });
  fireEvent.change(cardFileInput(), { target: { files: [file] } });
  await screen.findByDisplayValue('Kim Min Jae');
}

describe('VisitDuringTab — Card Visit contact save (Partner Contact relaxed validation)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(businessCardOcrApi.scan).mockResolvedValue(job());
  });

  it('F14: OCR-confirm path accepts an arbitrary phone and submits it unmodified', async () => {
    vi.mocked(businessCardOcrApi.confirmContact).mockResolvedValue({
      ocrJobId: 501, status: 'CONFIRMED', partnerId: 7, contactId: 200,
    });
    render(<MemoryRouter><VisitDuringTab visitInstanceId={47123} guestMembers={guestMembers} /></MemoryRouter>);
    await scanCard();

    fireEvent.change(phoneField(), { target: { value: 'ádsad' } });
    fireEvent.change(partnerSelect(), { target: { value: 'SeoulTech' } });
    fireEvent.click(screen.getByRole('button', { name: /Lưu thông tin liên hệ/i }));

    await waitFor(() => expect(businessCardOcrApi.confirmContact).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(businessCardOcrApi.confirmContact).mock.calls[0];
    expect(payload.phone).toBe('ádsad');
    expect(partnersApi.createContact).not.toHaveBeenCalled();
  });

  it('F13/F15: no-OCR manual path (currentOcrJobId never set — form filled by hand) accepts arbitrary phone, sends it unmodified', async () => {
    vi.mocked(partnersApi.createContact).mockResolvedValue({ contactId: 201 } as never);
    render(<MemoryRouter><VisitDuringTab visitInstanceId={47123} guestMembers={guestMembers} /></MemoryRouter>);
    // No scan at all — currentOcrJobId stays null from the start, exactly the "no OCR job" manual branch.
    await screen.findByText('Chọn đối tác liên kết thông tin của Card Visit này');

    fireEvent.change(screen.getByText('Họ tên người').closest('div')!.querySelector('input')!,
      { target: { value: 'Kim Min Jae' } });
    fireEvent.change(phoneField(), { target: { value: '+82 10-1234-0001' } });
    fireEvent.change(partnerSelect(), { target: { value: 'SeoulTech' } });

    fireEvent.click(screen.getByRole('button', { name: /Lưu thông tin liên hệ/i }));

    await waitFor(() => expect(partnersApi.createContact).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(partnersApi.createContact).mock.calls[0];
    expect(payload.phone).toBe('+82 10-1234-0001');
    expect(businessCardOcrApi.confirmContact).not.toHaveBeenCalled();
  });

  it('F16: a selected partner is still required — neither path is called without one', async () => {
    render(<MemoryRouter><VisitDuringTab visitInstanceId={47123} guestMembers={guestMembers} /></MemoryRouter>);
    await scanCard();

    fireEvent.click(screen.getByRole('button', { name: /Lưu thông tin liên hệ/i }));

    await waitFor(() => {
      expect(businessCardOcrApi.confirmContact).not.toHaveBeenCalled();
      expect(partnersApi.createContact).not.toHaveBeenCalled();
    });
  });
});
