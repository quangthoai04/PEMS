/**
 * QuickAccessSection — i18n cho đủ 7 nhóm role nội bộ, 2 ngôn ngữ.
 *
 * Ngoài việc kiểm tra label/description dịch đúng, test còn khoá lại metadata phải GIỮ NGUYÊN:
 * số card, thứ tự card, route và icon không được đổi khi chuyển ngôn ngữ.
 */

import { describe, expect, it, beforeEach, afterAll } from 'vitest';
import { render, act } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { AuthUser } from '../../../../features/authentication/types/authentication.types';
import i18n from '../../../../shared/i18n/config';
import { QuickAccessSection } from '../QuickAccessSection';
import {
  INTERNAL_ROLE_CASES,
  makeUser,
  hasRawTranslationKey,
  hasVietnameseDiacritics,
  type InternalBucket,
} from './internalRoleFixtures';

interface ExpectedCard {
  route: string;
  vi: [label: string, description: string];
  en: [label: string, description: string];
}

const SECTION_TITLE = { vi: 'Truy cập nhanh', en: 'Quick Access' };

/** Nguồn kỳ vọng đầy đủ: 1 dòng = 1 card, đúng thứ tự render. */
const EXPECTED_CARDS: Record<InternalBucket, ExpectedCard[]> = {
  ADMIN: [
    { route: '/dashboard', vi: ['Admin Dashboard', 'Tổng quan hệ thống'], en: ['Admin Dashboard', 'System overview'] },
    { route: '/dashboard/apis', vi: ['Quản lý API tích hợp', 'Cấu hình các API tích hợp hệ thống'], en: ['API Integrations', 'Configure the system integration APIs'] },
  ],
  HO: [
    { route: '/dashboard', vi: ['HO Dashboard', 'Theo dõi visit và hoạt động theo scope'], en: ['HO Dashboard', 'Track visits and activities within your scope'] },
    { route: '/dashboard/visit', vi: ['Quản lý tiếp khách', 'Theo dõi yêu cầu tham quan liên cơ sở'], en: ['Visit Management', 'Track cross-campus visit requests'] },
    { route: '/dashboard/news', vi: ['Quản lý tin tức', 'Duyệt và quản lý tin tức'], en: ['News Management', 'Review and manage news'] },
    { route: '/dashboard/faq', vi: ['Quản lý FAQ', 'Quản lý câu hỏi thường gặp'], en: ['FAQ Management', 'Manage frequently asked questions'] },
    { route: '/dashboard/campus', vi: ['Quản lý Campus', 'Quản lý danh sách campus'], en: ['Campus Management', 'Manage the campus list'] },
  ],
  STAFF_LEADER: [
    { route: '/dashboard', vi: ['Campus Dashboard', 'Tổng quan hoạt động campus'], en: ['Campus Dashboard', 'Campus activity overview'] },
    { route: '/dashboard/visit', vi: ['Quản lý tiếp khách', 'Duyệt đơn và gán host'], en: ['Visit Management', 'Approve requests and assign hosts'] },
    { route: '/dashboard/accounts', vi: ['Quản lý tài khoản', 'Quản lý tài khoản trong campus'], en: ['Account Management', 'Manage accounts within the campus'] },
    { route: '/dashboard/gallery', vi: ['Quản lý Gallery', 'Quản lý ảnh/video Visit FPTU'], en: ['Gallery Management', 'Manage Visit FPTU photos and videos'] },
    { route: '/dashboard/news', vi: ['Quản lý tin tức', 'Duyệt và quản lý tin tức'], en: ['News Management', 'Review and manage news'] },
  ],
  STAFF: [
    { route: '/dashboard', vi: ['My Workspace', 'Vào không gian làm việc của bạn'], en: ['My Workspace', 'Enter your workspace'] },
    { route: '/dashboard/visit', vi: ['Đơn phụ trách', 'Xem các đơn tham quan bạn phụ trách'], en: ['Assigned Requests', 'View the visit requests you are responsible for'] },
    { route: '/dashboard/minutes', vi: ['Biên bản & Tin tức', 'Đóng góp biên bản, tin tức, hình ảnh'], en: ['Minutes & News', 'Contribute minutes, news and photos'] },
  ],
  DEPT_LEADER: [
    { route: '/dashboard', vi: ['Department Dashboard', 'Tổng quan hoạt động phòng ban'], en: ['Department Dashboard', 'Department activity overview'] },
    { route: '/dashboard/departments/dept-9', vi: ['Phòng ban của tôi', 'Phân công nhân sự, quản lý nhiệm vụ phòng ban'], en: ['My Department', 'Assign personnel and manage department tasks'] },
    { route: '/dashboard/reports', vi: ['Báo cáo phòng ban', 'Xem báo cáo hoạt động'], en: ['Department Reports', 'View activity reports'] },
  ],
  DEPT_STAFF: [
    { route: '/dashboard', vi: ['My Tasks', 'Vào không gian làm việc của bạn'], en: ['My Tasks', 'Enter your workspace'] },
    { route: '/dashboard/news', vi: ['Tin tức', 'Xem tin tức, đóng góp media hỗ trợ'], en: ['News', 'View news and contribute supporting media'] },
  ],
  STUDENT: [
    { route: '/dashboard', vi: ['Student Portal', 'Vào không gian làm việc của bạn'], en: ['Student Portal', 'Enter your workspace'] },
    { route: '/dashboard/visit', vi: ['Lời mời tham gia hỗ trợ', 'Xem và phản hồi lời mời tham gia đoàn'], en: ['Support Invitations', 'View and respond to delegation support invitations'] },
    { route: '/faq', vi: ['Câu hỏi thường gặp', 'Hướng dẫn khi tham gia hỗ trợ đoàn'], en: ['Frequently Asked Questions', 'Guidance for supporting a delegation'] },
  ],
};

interface RenderedCard {
  href: string | null;
  icon: string;
  label: string;
  description: string;
}

function readCards(container: HTMLElement): RenderedCard[] {
  return Array.from(container.querySelectorAll('a')).map((anchor) => ({
    href: anchor.getAttribute('href'),
    // Class của lucide chứa tên icon — so sánh giữa VI/EN để chứng minh icon không đổi.
    icon: anchor.querySelector('svg')?.getAttribute('class') ?? '',
    label: anchor.querySelector('h3')?.textContent ?? '',
    description: anchor.querySelector('p')?.textContent ?? '',
  }));
}

function renderSection(user: AuthUser) {
  return render(
    <MemoryRouter>
      <QuickAccessSection user={user} />
    </MemoryRouter>,
  );
}

describe('QuickAccessSection i18n', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
  });

  afterAll(async () => {
    await i18n.changeLanguage('en');
  });

  describe.each(INTERNAL_ROLE_CASES)('$name', ({ bucket, user }) => {
    const expected = EXPECTED_CARDS[bucket];

    it.each(['vi', 'en'] as const)('renders every card in %s', async (lng) => {
      await act(async () => {
        await i18n.changeLanguage(lng);
      });

      const { container } = renderSection(user);
      const cards = readCards(container);

      expect(container.textContent).toContain(SECTION_TITLE[lng]);
      expect(cards).toHaveLength(expected.length);
      expect(cards.map((c) => c.href)).toEqual(expected.map((e) => e.route));
      expect(cards.map((c) => c.label)).toEqual(expected.map((e) => e[lng][0]));
      expect(cards.map((c) => c.description)).toEqual(expected.map((e) => e[lng][1]));
      expect(hasRawTranslationKey(container.textContent ?? '')).toBe(false);
    });

    it('keeps card count, order, route and icon identical across a language switch', async () => {
      await act(async () => {
        await i18n.changeLanguage('vi');
      });

      const { container } = renderSection(user);
      const viCards = readCards(container);

      await act(async () => {
        await i18n.changeLanguage('en');
      });

      const enCards = readCards(container);

      expect(enCards.map((c) => c.href)).toEqual(viCards.map((c) => c.href));
      expect(enCards.map((c) => c.icon)).toEqual(viCards.map((c) => c.icon));
      expect(enCards).toHaveLength(viCards.length);
      expect(enCards.map((c) => c.label)).toEqual(expected.map((e) => e.en[0]));
    });

    it('leaves no Vietnamese text behind in EN mode', async () => {
      await act(async () => {
        await i18n.changeLanguage('en');
      });

      const { container } = renderSection(user);
      expect(hasVietnameseDiacritics(container.textContent ?? '')).toBe(false);
    });

    it('does not leak cards belonging to other roles', async () => {
      await act(async () => {
        await i18n.changeLanguage('vi');
      });

      const { container } = renderSection(user);
      const labels = readCards(container).map((c) => c.label);

      const foreignLabels = (Object.keys(EXPECTED_CARDS) as InternalBucket[])
        .filter((other) => other !== bucket)
        .flatMap((other) => EXPECTED_CARDS[other].map((card) => card.vi[0]))
        // Một số nhãn dùng chung giữa các role (vd "Quản lý tin tức") — chỉ đối chiếu nhãn riêng.
        .filter((label) => !expected.some((card) => card.vi[0] === label));

      for (const label of new Set(foreignLabels)) {
        expect(labels).not.toContain(label);
      }
    });
  });

  it('renders nothing for a VISITOR account (public homepage is out of scope)', async () => {
    await act(async () => {
      await i18n.changeLanguage('vi');
    });

    const { container } = renderSection(makeUser({ roleCode: 'VISITOR', subRole: null }));
    expect(container.querySelectorAll('a')).toHaveLength(0);
  });
});
