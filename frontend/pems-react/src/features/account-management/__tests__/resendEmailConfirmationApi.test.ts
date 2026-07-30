import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { AxiosError } from 'axios';

vi.mock('../../../shared/api/httpClient', () => ({
  default: { post: vi.fn(), get: vi.fn() },
}));

import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import { accountManagementApi } from '../api/accountManagementApi';
import { ACCOUNT_ERROR_MESSAGES, getAccountErrorMessage } from '../api/accountError';

const post = httpClient.post as unknown as ReturnType<typeof vi.fn>;

/** Minimal Axios-shaped error carrying a backend errorCode. */
function apiError(status: number, errorCode?: string, message?: string): AxiosError {
  return {
    isAxiosError: true,
    name: 'AxiosError',
    message: 'Request failed',
    toJSON: () => ({}),
    response: { status, data: { errorCode, message }, statusText: '', headers: {}, config: {} as never },
  } as AxiosError;
}

describe('accountManagementApi.resendEmailConfirmation', () => {
  beforeEach(() => post.mockReset());

  it('posts the declared endpoint with the account id — no hardcoded route', () => {
    expect(API_ENDPOINTS.accounts.resendEmailConfirmation).toBe('/accounts/resend-email-confirmation');
  });

  it('sends the userId and returns the parsed body', async () => {
    post.mockResolvedValueOnce({
      data: { success: true, emailNotificationStatus: 'SENT', resendCount: 2, message: 'Đã gửi lại email xác nhận.' },
    });

    const result = await accountManagementApi.resendEmailConfirmation({ userId: 700 });

    expect(post).toHaveBeenCalledTimes(1);
    expect(post).toHaveBeenCalledWith(API_ENDPOINTS.accounts.resendEmailConfirmation, { userId: 700 });
    expect(result.emailNotificationStatus).toBe('SENT');
    expect(result.resendCount).toBe(2);
  });

  // The address is never part of the request: a wrong email is corrected through the separate
  // edit-pending-email flow, so a resend cannot be turned into an address change.
  it('sends nothing but the id', async () => {
    post.mockResolvedValueOnce({ data: { success: true, emailNotificationStatus: 'SENT', resendCount: 1, message: '' } });

    await accountManagementApi.resendEmailConfirmation({ userId: 700 });

    expect(post.mock.calls[0][1]).toEqual({ userId: 700 });
  });

  it('surfaces the failure to the caller instead of swallowing it', async () => {
    post.mockRejectedValueOnce(apiError(409, 'RESEND_TOO_SOON'));

    await expect(accountManagementApi.resendEmailConfirmation({ userId: 700 })).rejects.toBeDefined();
  });
});

describe('resend error messages', () => {
  it.each([
    ['ACCOUNT_NOT_PENDING', 'Tài khoản không còn ở trạng thái chờ xác nhận email.'],
    ['RESEND_TOO_SOON', 'Vui lòng đợi một lát trước khi gửi lại email xác nhận.'],
    ['RESEND_LIMIT_REACHED', 'Đã đạt số lần gửi lại tối đa. Vui lòng chỉnh sửa email hoặc liên hệ quản trị.'],
  ])('maps %s to its localized message', (code, expected) => {
    expect(ACCOUNT_ERROR_MESSAGES[code]).toBe(expected);
    expect(getAccountErrorMessage(apiError(409, code))).toBe(expected);
  });

  it('falls back to the caller-supplied message on a network/server error', () => {
    const fallback = 'Không thể gửi lại email xác nhận. Vui lòng thử lại sau.';
    expect(getAccountErrorMessage(apiError(500), fallback)).toBe(fallback);
  });
});
