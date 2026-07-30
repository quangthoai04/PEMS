import { describe, it, expect } from 'vitest';
import {
  ACCOUNT_STATUS_META,
  resolveAccountStatusMeta,
} from '../adapters/accountStatusMeta';

/**
 * Status badge of the UC-98 detail modal (spec §11.3/§11.7/§13.7). Two things are proven here: the
 * four real statuses map to the right Vietnamese label, and an unknown/blank value degrades safely
 * instead of rendering empty. §13.8 — the detail response, not the list row, decides.
 */
describe('resolveAccountStatusMeta', () => {
  it.each([
    ['ACTIVE', 'Hoạt động'],
    ['INACTIVE', 'Vô hiệu hóa'],
    ['LOCKED', 'Bị khóa'],
    ['PENDING_EMAIL_CONFIRMATION', 'Chờ xác nhận email'],
  ])('maps %s to %j', (status, label) => {
    expect(resolveAccountStatusMeta(status).label).toBe(label);
    expect(resolveAccountStatusMeta(status).className).toBe(ACCOUNT_STATUS_META[status].className);
  });

  it('normalizes casing and surrounding whitespace', () => {
    expect(resolveAccountStatusMeta('  active  ').label).toBe('Hoạt động');
    expect(resolveAccountStatusMeta('Pending_Email_Confirmation').label).toBe('Chờ xác nhận email');
  });

  // §13.8 — the drawer stores details.status in rawStatus; a stale list row must never win.
  it('prefers the detail status over the list row status', () => {
    expect(resolveAccountStatusMeta('LOCKED', 'ACTIVE').label).toBe('Bị khóa');
  });

  it('falls back to the list row only while the detail request is still in flight', () => {
    expect(resolveAccountStatusMeta(undefined, 'ACTIVE').label).toBe('Hoạt động');
    expect(resolveAccountStatusMeta(null, 'INACTIVE').label).toBe('Vô hiệu hóa');
  });

  // §11.7 — an unrecognised status must not crash and must not render blank.
  it('shows an unknown status verbatim with neutral styling', () => {
    const meta = resolveAccountStatusMeta('SOME_UNKNOWN_STATUS');
    expect(meta.label).toBe('SOME_UNKNOWN_STATUS');
    expect(meta.className).toContain('slate');
  });

  it.each([undefined, null, '', '   '])('labels %j as "Không xác định"', (value) => {
    expect(resolveAccountStatusMeta(value).label).toBe('Không xác định');
  });
});
