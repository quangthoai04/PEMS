import { describe, it, expect } from 'vitest';
import type { AxiosError } from 'axios';
import {
  ACCOUNT_ERROR_MESSAGES,
  getAccountErrorMessage,
  getAccountRoleChangeBlockers,
  type AccountRoleChangeBlockedData,
} from '../api/accountError';

/**
 * The 409 a Staff Leader gets when the account they are re-roling still runs a delegation
 * (backend AccountErrorCodes.AccountRoleChangeBlockedByActiveResponsibilities, spec §16).
 *
 * The property that matters: the STATIC message must never win over the backend's, because only the
 * backend knows how many visits and which department are involved — the numbers are the whole
 * reason the user can act on the error.
 */
const BLOCKED_CODE = 'ACCOUNT_ROLE_CHANGE_BLOCKED_BY_ACTIVE_RESPONSIBILITIES';

function conflictError(
  message: string | undefined,
  data?: AccountRoleChangeBlockedData,
  errorCode: string = BLOCKED_CODE,
): AxiosError {
  return {
    isAxiosError: true,
    response: { status: 409, data: { success: false, errorCode, message, data } },
  } as unknown as AxiosError;
}

const sampleData: AccountRoleChangeBlockedData = {
  affectedVisitCount: 3,
  blockers: [
    {
      type: 'ACTIVE_HOST_ASSIGNMENTS',
      count: 1,
      affectedVisitCount: 1,
      sampleVisitInstanceIds: [5001],
      message: 'Đang là Host chính của 1 đoàn khách đang hoạt động.',
    },
    {
      type: 'ACTIVE_LOGISTICS_RESPONSIBILITIES',
      count: 2,
      affectedVisitCount: 2,
      sampleVisitInstanceIds: [5002, 5003],
      message: 'Còn 2 nhiệm vụ hậu cần cá nhân chưa hoàn tất.',
    },
  ],
};

describe('getAccountErrorMessage — blocked role change', () => {
  it('prefers the backend message over the static mapping', () => {
    const backendMessage =
      'Không thể thay đổi vai trò vì tài khoản còn trách nhiệm đang hoạt động:\n' +
      '- Đang là Host chính của 1 đoàn khách đang hoạt động.\n' +
      '- Còn 2 nhiệm vụ hậu cần cá nhân chưa hoàn tất.';

    const message = getAccountErrorMessage(conflictError(backendMessage, sampleData));

    expect(message).toBe(backendMessage);
    // The counts survive — that is exactly what a static string would have destroyed.
    expect(message).toContain('1 đoàn khách');
    expect(message).toContain('2 nhiệm vụ hậu cần');
    expect(message).not.toBe(ACCOUNT_ERROR_MESSAGES[BLOCKED_CODE]);
  });

  it('falls back to the static mapping when the body carries no message', () => {
    expect(getAccountErrorMessage(conflictError(undefined, sampleData)))
      .toBe(ACCOUNT_ERROR_MESSAGES[BLOCKED_CODE]);
  });

  it('still prefers the static mapping for codes that are not data-driven', () => {
    const error = conflictError('Raw backend text', undefined, 'EMAIL_ALREADY_EXISTS');
    expect(getAccountErrorMessage(error)).toBe(ACCOUNT_ERROR_MESSAGES.EMAIL_ALREADY_EXISTS);
  });

  it('maps the out-of-scope target code', () => {
    const error = {
      isAxiosError: true,
      response: {
        status: 403,
        data: { success: false, errorCode: 'ACCOUNT_ROLE_TARGET_NOT_MANAGEABLE' },
      },
    } as unknown as AxiosError;

    expect(getAccountErrorMessage(error))
      .toBe(ACCOUNT_ERROR_MESSAGES.ACCOUNT_ROLE_TARGET_NOT_MANAGEABLE);
  });
});

describe('getAccountRoleChangeBlockers', () => {
  it('returns the breakdown so the drawer can list what to hand over', () => {
    const blockers = getAccountRoleChangeBlockers(conflictError('x', sampleData));

    expect(blockers).not.toBeNull();
    expect(blockers!.affectedVisitCount).toBe(3);
    expect(blockers!.blockers.map(b => b.type)).toEqual([
      'ACTIVE_HOST_ASSIGNMENTS',
      'ACTIVE_LOGISTICS_RESPONSIBILITIES',
    ]);
  });

  it('returns null for a different error code', () => {
    expect(getAccountRoleChangeBlockers(conflictError('x', sampleData, 'EMAIL_ALREADY_EXISTS')))
      .toBeNull();
  });

  it('returns null when the blocked code arrives without a data payload', () => {
    expect(getAccountRoleChangeBlockers(conflictError('x', undefined))).toBeNull();
  });

  it('returns null for a non-axios error', () => {
    expect(getAccountRoleChangeBlockers(new Error('boom'))).toBeNull();
  });
});
