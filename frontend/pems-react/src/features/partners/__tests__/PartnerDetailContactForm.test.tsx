import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { PartnerDetail } from '../../../pages/dashboard/partners/PartnerDetail';
import type { PartnerDetail as PartnerDetailType, PartnerContact } from '../types/partners.types';

/**
 * "Thêm/Chỉnh sửa người liên hệ" — the P1-core validation-highlight fix for Partner Contact
 * Create/Edit (PEMS_PROMPT_FIX_VALIDATION_UX_PLURALIZATION §4). Before this, the only feedback on an
 * empty "Họ tên" was a silently-disabled Lưu button; a backend field refusal (FluentValidation
 * `errors` dict) fell through to a generic toast with no indication of WHICH field was wrong.
 */

vi.mock('../api/partnersApi', () => ({
  partnersApi: {
    getPartnerDetail: vi.fn(),
    getContacts: vi.fn(),
    getAliases: vi.fn(),
    getDocuments: vi.fn(),
    getVisitHistory: vi.fn(),
    createContact: vi.fn(),
    updateContact: vi.fn(),
  },
}));

import { partnersApi } from '../api/partnersApi';

const partner = (overrides: Partial<PartnerDetailType> = {}): PartnerDetailType => ({
  partnerId: 1,
  partnerCode: 'PTN-001',
  name: 'Đại học X',
  shortName: 'ĐHX',
  country: 'Việt Nam',
  city: 'Hà Nội',
  websiteUrl: null,
  address: null,
  description: null,
  partnerType: 'UNIVERSITY',
  cooperationStatus: 'ACTIVE',
  profileStatus: 'APPROVED',
  visibility: 'PUBLIC',
  logoFileId: null,
  coverFileId: null,
  ownerCampusId: 1,
  ownerCampusName: 'FPTU Hà Nội',
  creatorName: 'Người tạo',
  createdAt: '2026-01-01T00:00:00',
  reviewNote: null,
  reviewedAt: null,
  reviewerName: null,
  allowedActions: ['VIEW', 'EDIT', 'MANAGE_CHILDREN'],
  ...overrides,
} as PartnerDetailType);

const contact = (overrides: Partial<PartnerContact> = {}): PartnerContact => ({
  contactId: 9,
  partnerId: 1,
  fullName: 'Nguyễn Văn A',
  email: 'a@x.edu.vn',
  phone: '0912345678',
  jobTitle: 'Trưởng phòng',
  departmentName: null,
  note: null,
  sourceType: 'MANUAL',
  scannedCardFileId: null,
  avatarFileId: null,
  avatarUrl: null,
  ocrConfidence: null,
  isPrimary: false,
  status: 'ACTIVE',
  createdAt: '2026-01-01T00:00:00',
  ...overrides,
});

const renderPage = () =>
  render(
    <MemoryRouter initialEntries={['/dashboard/partners/1']}>
      <Routes>
        <Route path="/dashboard/partners/:id" element={<PartnerDetail />} />
      </Routes>
    </MemoryRouter>,
  );

const axiosError = (fields: Record<string, string[]>) =>
  Object.assign(new Error('400'), {
    isAxiosError: true,
    response: { status: 400, data: { errorCode: 'VALIDATION_ERROR', message: 'nope', errors: fields } },
  });

describe('PartnerDetail — contact form validation highlighting', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(partnersApi.getPartnerDetail).mockResolvedValue(partner());
    vi.mocked(partnersApi.getContacts).mockResolvedValue([contact()]);
    vi.mocked(partnersApi.getAliases).mockResolvedValue([]);
    vi.mocked(partnersApi.getDocuments).mockResolvedValue([]);
    vi.mocked(partnersApi.getVisitHistory).mockResolvedValue([]);
  });

  it('blocks an empty Họ tên and focuses the field, without calling the API', async () => {
    renderPage();
    fireEvent.click(await screen.findByText('Thêm liên hệ'));
    const nameInput = await screen.findByTestId('partner-contact-field-fullName');
    fireEvent.change(nameInput, { target: { value: '   ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));

    await waitFor(() => expect(nameInput.closest('[data-field-error="true"]')).not.toBeNull());
    expect(nameInput).toHaveAttribute('aria-invalid', 'true');
    await waitFor(() => expect(document.activeElement).toBe(nameInput));
    expect(partnersApi.createContact).not.toHaveBeenCalled();
  });

  it('clears the fullName error the moment it becomes valid, without waiting for a second submit', async () => {
    renderPage();
    fireEvent.click(await screen.findByText('Thêm liên hệ'));
    const nameInput = await screen.findByTestId('partner-contact-field-fullName');
    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));
    await waitFor(() => expect(nameInput.closest('[data-field-error="true"]')).not.toBeNull());

    fireEvent.change(nameInput, { target: { value: 'Trần Thị B' } });
    await waitFor(() => expect(nameInput.closest('[data-field-error="true"]')).toBeNull());
  });

  // Plan CanhIter3FixBug "Partner Contact / Business Card Data Capture" — Partner Contact is external
  // business-card/partner-supplied data, not an identity field. F1-F9: Email/Phone format is no longer
  // client-side blocked, only FullName remains required; the backend mirrors this (relaxed validators).

  it('F5: does not reject a nonstandard email client-side, and submits it verbatim', async () => {
    vi.mocked(partnersApi.createContact).mockResolvedValue(contact({ email: 'not-an-email' }));
    renderPage();
    fireEvent.click(await screen.findByText('Thêm liên hệ'));
    fireEvent.change(await screen.findByTestId('partner-contact-field-fullName'), { target: { value: 'Trần Thị B' } });
    const emailInput = screen.getByTestId('partner-contact-field-email');
    fireEvent.change(emailInput, { target: { value: 'not-an-email' } });
    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));

    await waitFor(() => expect(partnersApi.createContact).toHaveBeenCalledTimes(1));
    expect(emailInput.closest('[data-field-error="true"]')).toBeNull();
    expect(partnersApi.createContact).toHaveBeenCalledWith('1', expect.objectContaining({ email: 'not-an-email' }));
  });

  it('F1/F2/F3: does not reject an arbitrary phone client-side (extension/foreign format/garbled), submits verbatim', async () => {
    vi.mocked(partnersApi.createContact).mockResolvedValue(contact({ phone: '+1 (212) 555-1234 ext. 208' }));
    renderPage();
    fireEvent.click(await screen.findByText('Thêm liên hệ'));
    fireEvent.change(await screen.findByTestId('partner-contact-field-fullName'), { target: { value: 'Trần Thị B' } });
    const phoneInput = screen.getByTestId('partner-contact-field-phone');
    fireEvent.change(phoneInput, { target: { value: '+1 (212) 555-1234 ext. 208' } });
    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));

    await waitFor(() => expect(partnersApi.createContact).toHaveBeenCalledTimes(1));
    expect(phoneInput.closest('[data-field-error="true"]')).toBeNull();
    expect(partnersApi.createContact).toHaveBeenCalledWith('1',
      expect.objectContaining({ phone: '+1 (212) 555-1234 ext. 208' }));
  });

  it('F4/F9: a blank phone submits as null, never shows a "required"/format error', async () => {
    vi.mocked(partnersApi.createContact).mockResolvedValue(contact({ phone: null }));
    renderPage();
    fireEvent.click(await screen.findByText('Thêm liên hệ'));
    fireEvent.change(await screen.findByTestId('partner-contact-field-fullName'), { target: { value: 'Trần Thị B' } });
    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));

    await waitFor(() => expect(partnersApi.createContact).toHaveBeenCalledTimes(1));
    expect(partnersApi.createContact).toHaveBeenCalledWith('1', expect.objectContaining({ phone: null }));
    expect(screen.queryByText(/không hợp lệ/i)).toBeNull();
  });

  it('maps a backend FullName validation error onto the field on create', async () => {
    vi.mocked(partnersApi.createContact).mockRejectedValue(
      axiosError({ FullName: ['Họ tên người liên hệ là bắt buộc.'] }),
    );
    renderPage();
    fireEvent.click(await screen.findByText('Thêm liên hệ'));
    fireEvent.change(await screen.findByTestId('partner-contact-field-fullName'), { target: { value: 'Trần Thị B' } });
    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));

    await waitFor(() => expect(partnersApi.createContact).toHaveBeenCalledTimes(1));
    const errorText = await screen.findByText('Họ tên người liên hệ là bắt buộc.');
    expect(errorText).toHaveAttribute('role', 'alert');
    expect(screen.getByTestId('partner-contact-field-fullName')).toHaveAttribute('aria-invalid', 'true');
  });

  it('F8: Update accepts the same arbitrary phone/email Create does — identical rule', async () => {
    vi.mocked(partnersApi.updateContact).mockResolvedValue(contact({ phone: 'ádsad', email: 'một giá trị user nhập' }));
    renderPage();
    await screen.findByText('Nguyễn Văn A');
    fireEvent.click(screen.getByTitle('Sửa'));

    const phoneInput = await screen.findByTestId('partner-contact-field-phone');
    fireEvent.change(phoneInput, { target: { value: 'ádsad' } });
    const emailInput = screen.getByTestId('partner-contact-field-email');
    fireEvent.change(emailInput, { target: { value: 'một giá trị user nhập' } });
    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));

    await waitFor(() => expect(partnersApi.updateContact).toHaveBeenCalledTimes(1));
    expect(phoneInput.closest('[data-field-error="true"]')).toBeNull();
    expect(emailInput.closest('[data-field-error="true"]')).toBeNull();
    expect(partnersApi.updateContact).toHaveBeenCalledWith('1', 9,
      expect.objectContaining({ phone: 'ádsad', email: 'một giá trị user nhập' }));
  });

  it('submits a valid edit exactly once and closes the form', async () => {
    vi.mocked(partnersApi.updateContact).mockResolvedValue(contact({ jobTitle: 'Phó phòng' }));
    renderPage();
    await screen.findByText('Nguyễn Văn A');
    fireEvent.click(screen.getByTitle('Sửa'));

    const titleInput = await screen.findByTestId('partner-contact-field-jobTitle');
    fireEvent.change(titleInput, { target: { value: 'Phó phòng' } });
    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));

    await waitFor(() => expect(partnersApi.updateContact).toHaveBeenCalledTimes(1));
    // `id` comes from useParams(), always a string.
    expect(partnersApi.updateContact).toHaveBeenCalledWith('1', 9, expect.objectContaining({
      fullName: 'Nguyễn Văn A', jobTitle: 'Phó phòng',
    }));
    await waitFor(() => expect(screen.queryByTestId('partner-contact-field-fullName')).not.toBeInTheDocument());
  });
});
