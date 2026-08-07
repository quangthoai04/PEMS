/**
 * Trang pháp lý công khai (/privacy, /terms) — kiểm tra nội dung + song ngữ.
 *
 * Hai trang này là URL khai báo với Google Auth Platform, nên bài test canh đúng những điểm
 * làm hồ sơ verification trượt nếu sai:
 *   1. Đủ 14 mục và mục lục khớp số mục (Google reviewer đọc theo TOC).
 *   2. Có mặt các câu bắt buộc: Limited Use, không quảng cáo/không bán dữ liệu, thu hồi quyền,
 *      giải thích OAuth credential, liên hệ.
 *   3. Đổi VI ↔ EN là đổi TOÀN BỘ — không sót tiếng Việt trong chế độ tiếng Anh.
 *   4. Không rò key i18n thô (`legal:...`) ra giao diện — lỗi im lặng khi namespace chưa đăng ký.
 */

import { describe, expect, it, beforeEach, afterAll } from 'vitest';
import { render, act } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

import i18n from '../../shared/i18n/config';
import { PrivacyPolicyPage } from '../legal/PrivacyPolicyPage';
import { TermsOfServicePage } from '../legal/TermsOfServicePage';

const SECTION_COUNT = 14;

/** Dấu tiếng Việt — không ký tự nào trong nhóm này được xuất hiện ở chế độ EN. */
const VIETNAMESE_DIACRITICS =
  /[àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ]/i;

async function setLanguage(lng: 'vi' | 'en') {
  await act(async () => {
    await i18n.changeLanguage(lng);
  });
}

function renderPage(Page: () => React.JSX.Element) {
  return render(
    <MemoryRouter>
      <Page />
    </MemoryRouter>,
  );
}

describe.each([
  ['PrivacyPolicyPage', PrivacyPolicyPage, 'privacy'] as const,
  ['TermsOfServicePage', TermsOfServicePage, 'terms'] as const,
])('%s', (_name, Page, docKey) => {
  beforeEach(async () => {
    await setLanguage('vi');
  });

  afterAll(async () => {
    await setLanguage('vi');
  });

  it('renders exactly one h1 and all 14 sections', () => {
    const { container } = renderPage(Page);

    expect(container.querySelectorAll('h1')).toHaveLength(1);
    expect(container.querySelectorAll('section[id]')).toHaveLength(SECTION_COUNT);
  });

  it('gives every section a matching in-page TOC anchor', () => {
    const { container } = renderPage(Page);

    const sectionIds = Array.from(container.querySelectorAll('section[id]')).map((el) => el.id);
    // Mỗi mục xuất hiện 2 lần trong TOC: bản desktop (sticky) và bản mobile (accordion).
    for (const id of sectionIds) {
      expect(container.querySelectorAll(`a[href="#${id}"]`).length).toBeGreaterThanOrEqual(1);
    }
    expect(new Set(sectionIds).size).toBe(SECTION_COUNT);
  });

  it('links to real routes, never "#"', () => {
    const { container } = renderPage(Page);

    const deadLinks = Array.from(container.querySelectorAll('a')).filter(
      (a) => a.getAttribute('href') === '#',
    );
    expect(deadLinks).toEqual([]);
  });

  it('shows contact details in both languages', async () => {
    for (const lng of ['vi', 'en'] as const) {
      await setLanguage(lng);
      const { container, unmount } = renderPage(Page);

      expect(container.textContent).toContain('international.fptu@fpt.edu.vn');
      expect(container.textContent).toContain('024 6680 5912');
      expect(container.querySelector('a[href^="mailto:"]')).not.toBeNull();
      unmount();
    }
  });

  it('leaks no raw i18n key into the UI', async () => {
    for (const lng of ['vi', 'en'] as const) {
      await setLanguage(lng);
      const { container, unmount } = renderPage(Page);

      expect(container.textContent).not.toContain('legal:');
      expect(container.textContent).not.toContain(`${docKey}.sections`);
      unmount();
    }
  });

  it('switches every visible string to English, leaving no Vietnamese behind', async () => {
    await setLanguage('en');
    const { container } = renderPage(Page);

    const vietnamese = (container.textContent ?? '').match(VIETNAMESE_DIACRITICS);
    expect(vietnamese).toBeNull();
  });

  it('sets a localized document title', async () => {
    await setLanguage('vi');
    const { unmount } = renderPage(Page);
    expect(document.title).toContain('PEMS - FPT University');
    const viTitle = document.title;
    unmount();

    await setLanguage('en');
    const { unmount: unmountEn } = renderPage(Page);
    expect(document.title).toContain('PEMS - FPT University');
    expect(document.title).not.toBe(viTitle);
    unmountEn();
  });
});

describe('PrivacyPolicyPage Google OAuth disclosures', () => {
  beforeEach(async () => {
    await setLanguage('en');
  });

  afterAll(async () => {
    await setLanguage('vi');
  });

  it.each([
    ['Google Drive API', 'Google Drive API'],
    ['Limited Use', 'Limited Use requirements'],
    ['no advertising / no sale', 'does not use data obtained from Google Drive for advertising'],
    ['OAuth credential handling', 'OAuth refresh tokens are protected before being stored'],
    ['revoking access', 'After access is revoked'],
    ['retention', 'retains information for as long as reasonably necessary'],
  ])('states %s', (_label, sentence) => {
    const { container } = renderPage(PrivacyPolicyPage);
    expect(container.textContent).toContain(sentence);
  });

  it('highlights the Google Drive section so a reviewer cannot miss it', () => {
    const { container } = renderPage(PrivacyPolicyPage);
    const googleSection = container.querySelector('#google-drive');
    expect(googleSection?.className).toContain('border-l-fpt-orange');
  });
});

describe('TermsOfServicePage', () => {
  beforeEach(async () => {
    await setLanguage('en');
  });

  afterAll(async () => {
    await setLanguage('vi');
  });

  it('links its Privacy section to /privacy', () => {
    const { container } = renderPage(TermsOfServicePage);
    const link = container.querySelector('#privacy a[href="/privacy"]');
    expect(link).not.toBeNull();
  });

  it('covers authorized use, account responsibility and prohibited conduct', () => {
    const { container } = renderPage(TermsOfServicePage);
    const text = container.textContent ?? '';

    expect(text).toContain('only for purposes consistent with the system');
    expect(text).toContain('responsible for protecting their authentication credentials');
    expect(text).toContain('must not attempt to bypass security controls');
    expect(text).toContain('Google Drive API');
  });
});
