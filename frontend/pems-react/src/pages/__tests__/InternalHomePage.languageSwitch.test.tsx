/**
 * InternalHomePage — kiểm tra đổi ngôn ngữ runtime.
 *
 * Mục tiêu: chứng minh cả 4 khu vực trong phạm vi (Welcome Hero, Quick Access, Process Guide,
 * Final CTA) subscribe đúng với i18next — đổi VI ↔ EN là re-render, KHÔNG cần reload và KHÔNG
 * unmount component.
 *
 * Ba section ngoài phạm vi (News / Gallery / FAQ preview) được mock vì chúng gọi API riêng.
 */

import { describe, expect, it, beforeEach, afterAll } from 'vitest';
import { render, act } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';

vi.mock('../../components/home/NewsSection', () => ({ NewsSection: () => null }));
vi.mock('../../components/home/GalleryPreviewSection', () => ({ GalleryPreviewSection: () => null }));
vi.mock('../../components/home/FaqPreviewSection', () => ({ FaqPreviewSection: () => null }));

import i18n from '../../shared/i18n/config';
import { InternalHomePage } from '../InternalHomePage';
import {
  INTERNAL_ROLE_CASES,
  DYNAMIC,
  hasRawTranslationKey,
} from '../../components/home/internal/__tests__/internalRoleFixtures';

/** Một mốc text đại diện cho mỗi khu vực trong phạm vi. */
const AREA_MARKERS = {
  hero: { vi: 'Cổng thông tin nội bộ PEMS', en: 'PEMS Internal Portal' },
  quickAccess: { vi: 'Truy cập nhanh', en: 'Quick Access' },
  guide: { vi: 'Hướng dẫn quy trình', en: 'Process Guide' },
  cta: { vi: 'Sẵn sàng tiếp tục công việc?', en: 'Ready to continue working?' },
} as const;

describe('InternalHomePage runtime language switch', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
  });

  afterAll(async () => {
    await i18n.changeLanguage('en');
  });

  it.each(INTERNAL_ROLE_CASES)('swaps all four areas for $name without reloading', async ({ user }) => {
    await act(async () => {
      await i18n.changeLanguage('vi');
    });

    const { container } = render(
      <MemoryRouter>
        <InternalHomePage user={user} />
      </MemoryRouter>,
    );

    // 1-2. Render bằng VI rồi khẳng định text tiếng Việt.
    const heroNode = container.querySelector('h1');
    for (const marker of Object.values(AREA_MARKERS)) {
      expect(container.textContent).toContain(marker.vi);
    }
    expect(container.textContent).toContain(`Xin chào, ${DYNAMIC.fullName}`);

    // 3-4. Đổi ngôn ngữ mà KHÔNG unmount — cùng một DOM node được giữ lại.
    await act(async () => {
      await i18n.changeLanguage('en');
    });
    expect(container.querySelector('h1')).toBe(heroNode);

    // 5. Cả 4 khu vực đổi sang tiếng Anh.
    for (const marker of Object.values(AREA_MARKERS)) {
      expect(container.textContent).toContain(marker.en);
      expect(container.textContent).not.toContain(marker.vi);
    }
    expect(container.textContent).toContain(`Welcome, ${DYNAMIC.fullName}`);

    // Dữ liệu động không bị dịch, và không lộ raw key.
    expect(container.textContent).toContain(DYNAMIC.campusName);
    expect(container.textContent).toContain(DYNAMIC.departmentName);
    expect(hasRawTranslationKey(container.textContent ?? '')).toBe(false);

    // Quay lại VI.
    await act(async () => {
      await i18n.changeLanguage('vi');
    });
    for (const marker of Object.values(AREA_MARKERS)) {
      expect(container.textContent).toContain(marker.vi);
    }
  });
});
