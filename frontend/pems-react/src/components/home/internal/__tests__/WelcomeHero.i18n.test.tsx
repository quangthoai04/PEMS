/**
 * WelcomeHero — i18n cho đủ 7 nhóm role nội bộ, 2 ngôn ngữ.
 *
 * Khẳng định: badge / lời chào / nhãn role đổi theo ngôn ngữ, còn tên người dùng, campus và
 * department (dữ liệu backend) thì KHÔNG đổi.
 */

import { describe, expect, it, beforeEach, afterAll } from 'vitest';
import { render, act } from '@testing-library/react';
import i18n from '../../../../shared/i18n/config';
import { WelcomeHero } from '../WelcomeHero';
import {
  INTERNAL_ROLE_CASES,
  DYNAMIC,
  makeUser,
  hasRawTranslationKey,
  type InternalBucket,
} from './internalRoleFixtures';

const EXPECTED_ROLE_LABEL: Record<InternalBucket, { vi: string; en: string }> = {
  ADMIN: { vi: 'Quản trị viên', en: 'Administrator' },
  HO: { vi: 'Điều phối viên Head Office', en: 'Head Office Coordinator' },
  STAFF_LEADER: { vi: 'Trưởng phòng Hợp tác Quốc tế', en: 'International Relations Staff Leader' },
  STAFF: { vi: 'Nhân viên Phòng Hợp tác Quốc tế', en: 'International Relations Staff' },
  DEPT_LEADER: { vi: 'Trưởng phòng ban', en: 'Department Leader' },
  DEPT_STAFF: { vi: 'Nhân viên phòng ban', en: 'Department Staff' },
  STUDENT: { vi: 'Sinh viên', en: 'Student' },
};

const BADGE = { vi: 'Cổng thông tin nội bộ PEMS', en: 'PEMS Internal Portal' };
const GREETING = {
  vi: `Xin chào, ${DYNAMIC.fullName}`,
  en: `Welcome, ${DYNAMIC.fullName}`,
};

describe('WelcomeHero i18n', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
  });

  afterAll(async () => {
    await i18n.changeLanguage('en');
  });

  describe.each(INTERNAL_ROLE_CASES)('$name', ({ bucket, user }) => {
    it.each(['vi', 'en'] as const)('renders the %s copy', async (lng) => {
      await act(async () => {
        await i18n.changeLanguage(lng);
      });

      const { container } = render(<WelcomeHero user={user} />);
      const text = container.textContent ?? '';

      expect(text).toContain(BADGE[lng]);
      expect(text).toContain(GREETING[lng]);
      expect(text).toContain(EXPECTED_ROLE_LABEL[bucket][lng]);

      // Dữ liệu động giữ nguyên ở cả hai ngôn ngữ.
      expect(text).toContain(DYNAMIC.fullName);
      expect(text).toContain(DYNAMIC.campusName);
      expect(text).toContain(DYNAMIC.departmentName);

      // Nhãn role KHÔNG lấy từ backend roleName, và không lộ raw key.
      expect(text).not.toContain(DYNAMIC.roleName);
      expect(hasRawTranslationKey(text)).toBe(false);
    });
  });

  it('swaps badge, greeting and role label when the language changes without unmounting', async () => {
    await act(async () => {
      await i18n.changeLanguage('vi');
    });

    const user = makeUser({ roleCode: 'HO', subRole: null });
    const { container } = render(<WelcomeHero user={user} />);
    expect(container.textContent).toContain(BADGE.vi);
    expect(container.textContent).toContain(EXPECTED_ROLE_LABEL.HO.vi);

    await act(async () => {
      await i18n.changeLanguage('en');
    });

    expect(container.textContent).toContain(BADGE.en);
    expect(container.textContent).toContain(GREETING.en);
    expect(container.textContent).toContain(EXPECTED_ROLE_LABEL.HO.en);
    expect(container.textContent).not.toContain(BADGE.vi);
    // Tên người dùng và campus vẫn nguyên sau khi đổi ngôn ngữ.
    expect(container.textContent).toContain(DYNAMIC.fullName);
    expect(container.textContent).toContain(DYNAMIC.campusName);
  });

  it('falls back to the backend role name for a bucket outside the seven internal roles', async () => {
    await act(async () => {
      await i18n.changeLanguage('en');
    });

    const visitor = makeUser({ roleCode: 'VISITOR', subRole: null, roleName: 'Khách' });
    const { container } = render(<WelcomeHero user={visitor} />);

    expect(container.textContent).toContain('Khách');
    expect(hasRawTranslationKey(container.textContent ?? '')).toBe(false);
  });
});
