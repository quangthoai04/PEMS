/**
 * InternalFinalCta — i18n cho khu vực CTA cuối trang nội bộ.
 *
 * Kiểm tra tiêu đề / mô tả / nhãn nút dịch đúng và nút vẫn điều hướng về đúng route Dashboard.
 */

import { describe, expect, it, vi, beforeEach, afterAll } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';

const navigateMock = vi.fn();
vi.mock('react-router-dom', () => ({ useNavigate: () => navigateMock }));

import i18n from '../../../../shared/i18n/config';
import { InternalFinalCta } from '../InternalFinalCta';
import { INTERNAL_ROLE_CASES, makeUser, hasRawTranslationKey } from './internalRoleFixtures';

const COPY = {
  vi: {
    title: 'Sẵn sàng tiếp tục công việc?',
    description: 'Vào Dashboard để xử lý các nhiệm vụ và yêu cầu đang chờ bạn.',
    button: 'Vào Dashboard',
  },
  en: {
    title: 'Ready to continue working?',
    description: 'Go to the Dashboard to handle your pending tasks and requests.',
    button: 'Go to Dashboard',
  },
} as const;

describe('InternalFinalCta i18n', () => {
  beforeEach(async () => {
    navigateMock.mockClear();
    await i18n.changeLanguage('en');
  });

  afterAll(async () => {
    await i18n.changeLanguage('en');
  });

  it.each(['vi', 'en'] as const)('renders the %s copy', async (lng) => {
    await act(async () => {
      await i18n.changeLanguage(lng);
    });

    const { container } = render(<InternalFinalCta user={makeUser({ roleCode: 'HO', subRole: null })} />);
    const text = container.textContent ?? '';

    expect(text).toContain(COPY[lng].title);
    expect(text).toContain(COPY[lng].description);
    expect(screen.getByRole('button')).toHaveTextContent(COPY[lng].button);
    expect(hasRawTranslationKey(text)).toBe(false);
  });

  it('swaps the copy on a language change without unmounting', async () => {
    await act(async () => {
      await i18n.changeLanguage('vi');
    });

    const { container } = render(<InternalFinalCta user={makeUser({ roleCode: 'STUDENT', subRole: null })} />);
    expect(container.textContent).toContain(COPY.vi.title);

    await act(async () => {
      await i18n.changeLanguage('en');
    });

    expect(container.textContent).toContain(COPY.en.title);
    expect(container.textContent).toContain(COPY.en.description);
    expect(container.textContent).not.toContain(COPY.vi.title);
  });

  it.each(INTERNAL_ROLE_CASES)('still navigates to /dashboard for $name in both languages', async ({ user }) => {
    for (const lng of ['vi', 'en'] as const) {
      await act(async () => {
        await i18n.changeLanguage(lng);
      });

      const { unmount } = render(<InternalFinalCta user={user} />);
      fireEvent.click(screen.getByRole('button'));

      expect(navigateMock).toHaveBeenCalledWith('/dashboard');
      navigateMock.mockClear();
      unmount();
    }
  });
});
