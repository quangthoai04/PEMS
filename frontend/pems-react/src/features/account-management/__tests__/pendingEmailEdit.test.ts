import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { AxiosError } from 'axios';

vi.mock('../../../shared/api/httpClient', () => ({
  default: { post: vi.fn(), get: vi.fn() },
}));

import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import { accountManagementApi } from '../api/accountManagementApi';
import { ACCOUNT_ERROR_MESSAGES, getAccountErrorMessage } from '../api/accountError';
import {
  isPendingEmailConfirmation,
  pendingEmailEditFeedback,
  shouldUsePendingEmailEdit,
} from '../adapters/accountPendingEmailEdit';

const post = httpClient.post as unknown as ReturnType<typeof vi.fn>;

function apiError(status: number, errorCode?: string, message?: string): AxiosError {
  return {
    isAxiosError: true,
    name: 'AxiosError',
    message: 'Request failed',
    toJSON: () => ({}),
    response: { status, data: { errorCode, message }, statusText: '', headers: {}, config: {} as never },
  } as AxiosError;
}

/**
 * The branch this whole change exists for. Both endpoints answer 200 to a plausible payload, so
 * picking the wrong one is invisible in the UI and only shows up as an account nobody can activate —
 * which is exactly why the decision lives in a tested function rather than inline in the page.
 */
describe('shouldUsePendingEmailEdit', () => {
  const pending = 'PENDING_EMAIL_CONFIRMATION';

  it('routes a pending account with a NEW address to the pending-email endpoint', () => {
    expect(shouldUsePendingEmailEdit({
      rawStatus: pending, oldEmail: 'old@fpt.edu.vn', newEmail: 'new@fpt.edu.vn',
    })).toBe(true);
  });

  // Nothing to re-issue: the backend would refuse this with EMAIL_UNCHANGED. A name-only edit on a
  // pending account is an ordinary basic-info update.
  it('leaves a pending account with an unchanged address on the basic-info endpoint', () => {
    expect(shouldUsePendingEmailEdit({
      rawStatus: pending, oldEmail: 'old@fpt.edu.vn', newEmail: 'old@fpt.edu.vn',
    })).toBe(false);
  });

  it('treats a casing/whitespace-only edit as unchanged', () => {
    expect(shouldUsePendingEmailEdit({
      rawStatus: pending, oldEmail: 'old@fpt.edu.vn', newEmail: '  OLD@FPT.EDU.VN ',
    })).toBe(false);
  });

  // A provisioned account has already proven its address; changing it is the ordinary re-verify flow.
  it.each(['ACTIVE', 'INACTIVE', 'LOCKED'])('leaves %s on the basic-info endpoint', (rawStatus) => {
    expect(shouldUsePendingEmailEdit({
      rawStatus, oldEmail: 'old@fpt.edu.vn', newEmail: 'new@fpt.edu.vn',
    })).toBe(false);
  });

  it('normalizes the status casing and whitespace the server sent', () => {
    expect(shouldUsePendingEmailEdit({
      rawStatus: '  pending_email_confirmation ', oldEmail: 'old@fpt.edu.vn', newEmail: 'new@fpt.edu.vn',
    })).toBe(true);
  });

  // "Pending" is the list row's display value, not the detail status. Accepting it would let a stale
  // row decide the endpoint.
  it('does not accept the list row display status', () => {
    expect(shouldUsePendingEmailEdit({
      rawStatus: 'Pending', oldEmail: 'old@fpt.edu.vn', newEmail: 'new@fpt.edu.vn',
    })).toBe(false);
  });

  it('stays off while the detail has not resolved a status', () => {
    expect(shouldUsePendingEmailEdit({ oldEmail: 'old@fpt.edu.vn', newEmail: 'new@fpt.edu.vn' })).toBe(false);
    expect(shouldUsePendingEmailEdit({
      rawStatus: null, oldEmail: 'old@fpt.edu.vn', newEmail: 'new@fpt.edu.vn',
    })).toBe(false);
  });

  it('does not fire on an empty address', () => {
    expect(shouldUsePendingEmailEdit({ rawStatus: pending, oldEmail: 'old@fpt.edu.vn', newEmail: '' })).toBe(false);
  });
});

describe('isPendingEmailConfirmation', () => {
  it('matches only the raw DB status', () => {
    expect(isPendingEmailConfirmation('PENDING_EMAIL_CONFIRMATION')).toBe(true);
    expect(isPendingEmailConfirmation(' pending_email_confirmation ')).toBe(true);
    expect(isPendingEmailConfirmation('Pending')).toBe(false);
    expect(isPendingEmailConfirmation(undefined)).toBe(false);
  });
});

describe('accountManagementApi.editPendingAccountEmail', () => {
  beforeEach(() => post.mockReset());

  it('posts the declared endpoint — no hardcoded route', () => {
    expect(API_ENDPOINTS.accounts.editPendingEmail).toBe('/accounts/edit-pending-email');
    expect(API_ENDPOINTS.accounts.editPendingEmail).not.toBe(API_ENDPOINTS.accounts.updateBasicInfo);
  });

  // One request, not two: a name saved by one call and an address rejected by another would leave
  // the account half-edited with no way to tell which half landed.
  it('carries the name and the address in a single request', async () => {
    post.mockResolvedValueOnce({
      data: {
        success: true, email: 'new@fpt.edu.vn', emailNotificationStatus: 'SENT',
        message: 'Đã cập nhật email và gửi lại xác nhận.',
      },
    });

    const result = await accountManagementApi.editPendingAccountEmail({
      userId: 700, newEmail: 'new@fpt.edu.vn', fullName: 'Nguyễn Văn A',
    });

    expect(post).toHaveBeenCalledTimes(1);
    expect(post).toHaveBeenCalledWith(API_ENDPOINTS.accounts.editPendingEmail, {
      userId: 700, newEmail: 'new@fpt.edu.vn', fullName: 'Nguyễn Văn A',
    });
    expect(result.email).toBe('new@fpt.edu.vn');
    expect(result.emailNotificationStatus).toBe('SENT');
  });

  it('surfaces the failure to the caller instead of swallowing it', async () => {
    post.mockRejectedValueOnce(apiError(409, 'EMAIL_UNCHANGED'));

    await expect(accountManagementApi.editPendingAccountEmail({
      userId: 700, newEmail: 'old@fpt.edu.vn',
    })).rejects.toBeDefined();
  });
});

/**
 * The address is committed in every branch; only the delivery differs. A message that claims a link
 * went out when it did not leaves HO waiting on a confirmation that can never arrive.
 */
describe('pendingEmailEditFeedback', () => {
  const email = 'new.owner@fpt.edu.vn';

  it('names the address and says activation is still pending on a real send', () => {
    const feedback = pendingEmailEditFeedback('SENT', email);
    expect(feedback.kind).toBe('success');
    expect(feedback.message).toContain(email);
    expect(feedback.message).toContain('Tài khoản sẽ được kích hoạt sau khi người nhận hoàn tất xác nhận.');
  });

  it('does not report a skipped send as success, and points at the resend action', () => {
    const feedback = pendingEmailEditFeedback('SKIPPED', email);
    expect(feedback.kind).toBe('warning');
    expect(feedback.message).toContain('Đã cập nhật email');
    expect(feedback.message).not.toContain('Đã cập nhật email và gửi liên kết xác nhận');
    expect(feedback.message).toContain('Gửi lại email xác nhận');
  });

  it('says plainly on a failure that the address was saved but no link went out', () => {
    const feedback = pendingEmailEditFeedback('FAILED', email);
    expect(feedback.kind).toBe('error');
    expect(feedback.message).toContain('Đã cập nhật email');
    expect(feedback.message).toContain('vẫn ở trạng thái chờ xác nhận email');
    expect(feedback.message).toContain('Gửi lại email xác nhận');
  });

  it.each([undefined, null, '', 'SOMETHING_NEW'])(
    'stays non-committal about delivery for %j while still confirming the update',
    (status) => {
      const feedback = pendingEmailEditFeedback(status, email);
      expect(feedback.kind).toBe('warning');
      expect(feedback.message).toBe(
        'Đã cập nhật email nhưng chưa xác định được trạng thái gửi email xác nhận.',
      );
    },
  );

  it('normalizes casing from the server', () => {
    expect(pendingEmailEditFeedback('sent', email).kind).toBe('success');
    expect(pendingEmailEditFeedback(' Failed ', email).kind).toBe('error');
  });

  // Wire values mean nothing to a Vietnamese-speaking operator.
  it.each(['SENT', 'SKIPPED', 'FAILED', 'WAT'])('never prints the raw status %s', (status) => {
    expect(pendingEmailEditFeedback(status, email).message).not.toMatch(/SENT|SKIPPED|FAILED|WAT/);
  });
});

describe('pending email edit error messages', () => {
  it.each([
    ['ACCOUNT_NOT_PENDING', 'Tài khoản không còn ở trạng thái chờ xác nhận email.'],
    ['EMAIL_UNCHANGED', 'Email mới trùng với email hiện tại.'],
    ['EMAIL_ALREADY_EXISTS', 'Email này đã tồn tại trong hệ thống. Vui lòng dùng email khác.'],
  ])('maps %s to its localized message', (code, expected) => {
    expect(ACCOUNT_ERROR_MESSAGES[code]).toBe(expected);
    expect(getAccountErrorMessage(apiError(409, code))).toBe(expected);
  });

  // Every address-shaped refusal must be recognisable as one, so the page can pin it under the email
  // input rather than dropping it into the generic alert.
  it.each(['EMAIL_UNCHANGED', 'EMAIL_ALREADY_EXISTS'])('keeps %s recognisable as an email error', (code) => {
    expect(getAccountErrorMessage(apiError(409, code))).toMatch(/email/i);
  });

  it('falls back to the caller-supplied message on a network/server error', () => {
    const fallback = 'Không thể cập nhật email tài khoản. Vui lòng thử lại sau.';
    expect(getAccountErrorMessage(apiError(500), fallback)).toBe(fallback);
  });
});
