import { describe, expect, it } from 'vitest';
import {
  FORM_VERSION_UPGRADE_REQUIRED,
  isFormVersionUpgradeRequired,
  v2DetailPath,
} from '../utils/formVersionErrors';

const axiosError = (data: unknown) =>
  Object.assign(new Error('conflict'), { isAxiosError: true, response: { status: 409, data } });

describe('formVersionErrors (legacy 409 → v2 routing)', () => {
  it('recognizes the stable backend code, never message text', () => {
    expect(isFormVersionUpgradeRequired(axiosError({ errorCode: FORM_VERSION_UPGRADE_REQUIRED }))).toBe(true);
    expect(isFormVersionUpgradeRequired(axiosError({ errorCode: 'SOMETHING_ELSE' }))).toBe(false);
    expect(isFormVersionUpgradeRequired(axiosError({ message: 'FORM_VERSION_UPGRADE_REQUIRED mentioned in text' }))).toBe(false);
    expect(isFormVersionUpgradeRequired(new Error('FORM_VERSION_UPGRADE_REQUIRED'))).toBe(false);
    expect(isFormVersionUpgradeRequired(null)).toBe(false);
  });

  it('routes to the per-campus detail screen', () => {
    expect(v2DetailPath(42)).toBe('/dashboard/visit/v2/42');
    expect(v2DetailPath('7')).toBe('/dashboard/visit/v2/7');
  });
});
