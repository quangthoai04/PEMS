/**
 * GuideStepsSection — i18n cho đủ 7 nhóm role nội bộ, 2 ngôn ngữ.
 *
 * Nội dung nghiệp vụ của từng bước không đổi, chỉ đổi ngôn ngữ: test khoá lại số bước,
 * thứ tự bước và số badge, đồng thời kiểm tra tiêu đề + nội dung bước dịch đúng.
 */

import { describe, expect, it, beforeEach, afterAll } from 'vitest';
import { render, act } from '@testing-library/react';
import type { AuthUser } from '../../../../features/authentication/types/authentication.types';
import i18n from '../../../../shared/i18n/config';
import { GuideStepsSection } from '../GuideStepsSection';
import {
  INTERNAL_ROLE_CASES,
  makeUser,
  hasRawTranslationKey,
  hasVietnameseDiacritics,
  type InternalBucket,
} from './internalRoleFixtures';

const SECTION_TITLE = { vi: 'Hướng dẫn quy trình', en: 'Process Guide' };

/** Toàn bộ bước theo đúng thứ tự render — 1 dòng = 1 bước. */
const EXPECTED_STEPS: Record<InternalBucket, { vi: string; en: string }[]> = {
  ADMIN: [
    { vi: 'Quản lý tài khoản người dùng và phân quyền hệ thống.', en: 'Manage user accounts and system permissions.' },
    { vi: 'Cấu hình các API tích hợp (OCR, dịch thuật, v.v.).', en: 'Configure the integration APIs (OCR, translation, etc.).' },
    { vi: 'Theo dõi nhật ký hoạt động và bảo mật hệ thống.', en: 'Monitor system activity logs and security.' },
  ],
  HO: [
    { vi: 'Theo dõi các yêu cầu tham quan liên cơ sở trong phạm vi phụ trách.', en: 'Track cross-campus visit requests within your scope.' },
    { vi: 'Quản lý tin tức và FAQ hiển thị công khai cho đối tác.', en: 'Manage the news and FAQs shown publicly to partners.' },
    { vi: 'Cập nhật thông tin campus khi có thay đổi.', en: 'Update campus information whenever it changes.' },
    { vi: 'Giám sát tiến độ tiếp đón qua Dashboard.', en: 'Monitor reception progress through the Dashboard.' },
  ],
  STAFF_LEADER: [
    { vi: 'Duyệt các yêu cầu tham quan mới trong campus.', en: 'Approve new visit requests within the campus.' },
    { vi: 'Gán Host phụ trách tiếp đón cho từng đoàn.', en: 'Assign the Host responsible for receiving each delegation.' },
    { vi: 'Quản lý tài khoản và phòng ban trong campus.', en: 'Manage accounts and departments within the campus.' },
    { vi: 'Cập nhật Gallery/Tin tức giới thiệu campus.', en: 'Update the Gallery and news introducing the campus.' },
  ],
  STAFF: [
    { vi: 'Kiểm tra các đơn tham quan được phân công phụ trách.', en: 'Check the visit requests assigned to you.' },
    { vi: 'Chuẩn bị chương trình đón tiếp trước ngày visit.', en: 'Prepare the reception programme before the visit date.' },
    { vi: 'Ghi nhận biên bản, tin tức, hình ảnh sau buổi tham quan.', en: 'Record minutes, news and photos after the visit.' },
  ],
  DEPT_LEADER: [
    { vi: 'Phân công nhân sự phòng ban hỗ trợ các đoàn tham quan.', en: 'Assign department personnel to support visiting delegations.' },
    { vi: 'Theo dõi nhiệm vụ và tiến độ của phòng ban.', en: 'Track the tasks and progress of your department.' },
    { vi: 'Ký bàn giao khi hoàn tất công tác hỗ trợ.', en: 'Sign the handover once the support work is complete.' },
    { vi: 'Phối hợp với Phòng Hợp tác Quốc tế (IC) khi cần.', en: 'Coordinate with the International Relations Department (IC) when needed.' },
  ],
  DEPT_STAFF: [
    { vi: 'Xem lịch hỗ trợ được phòng ban phân công.', en: 'View the support schedule assigned by your department.' },
    { vi: 'Phản hồi nhiệm vụ được giao đúng hạn.', en: 'Respond to assigned tasks on time.' },
    { vi: 'Ký nhận / ký trả khi bàn giao công việc.', en: 'Sign for receipt / return when handing over work.' },
    { vi: 'Cập nhật kết quả hỗ trợ sau khi hoàn tất.', en: 'Update the support results once completed.' },
  ],
  STUDENT: [
    { vi: 'Kiểm tra lời mời tham gia hỗ trợ đoàn tham quan trong Dashboard.', en: 'Check your delegation support invitations in the Dashboard.' },
    { vi: 'Chấp nhận hoặc từ chối lời mời trước hạn phản hồi.', en: 'Accept or decline the invitation before the response deadline.' },
    { vi: 'Xem lịch hỗ trợ đã xác nhận và chuẩn bị theo hướng dẫn của Host.', en: 'Review the confirmed support schedule and prepare as instructed by the Host.' },
    { vi: 'Tham gia hỗ trợ đoàn đúng thời gian, địa điểm đã lên lịch.', en: 'Join the delegation support at the scheduled time and place.' },
  ],
};

function readSteps(container: HTMLElement) {
  return Array.from(container.querySelectorAll('p')).map((p) => p.textContent ?? '');
}

function readBadges(container: HTMLElement) {
  return Array.from(container.querySelectorAll('.rounded-full')).map((el) => el.textContent ?? '');
}

function renderSection(user: AuthUser) {
  return render(<GuideStepsSection user={user} />);
}

describe('GuideStepsSection i18n', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
  });

  afterAll(async () => {
    await i18n.changeLanguage('en');
  });

  describe.each(INTERNAL_ROLE_CASES)('$name', ({ bucket, user }) => {
    const expected = EXPECTED_STEPS[bucket];

    it.each(['vi', 'en'] as const)('renders the title and every step in %s', async (lng) => {
      await act(async () => {
        await i18n.changeLanguage(lng);
      });

      const { container } = renderSection(user);

      expect(container.textContent).toContain(SECTION_TITLE[lng]);
      expect(readSteps(container)).toEqual(expected.map((step) => step[lng]));
      expect(hasRawTranslationKey(container.textContent ?? '')).toBe(false);
    });

    it('keeps step count, order and number badges across a language switch', async () => {
      await act(async () => {
        await i18n.changeLanguage('vi');
      });

      const { container } = renderSection(user);
      const viBadges = readBadges(container);
      expect(readSteps(container)).toHaveLength(expected.length);

      await act(async () => {
        await i18n.changeLanguage('en');
      });

      expect(readSteps(container)).toEqual(expected.map((step) => step.en));
      expect(readBadges(container)).toEqual(viBadges);
      // Badge số vẫn là 1..N theo đúng thứ tự.
      expect(readBadges(container).filter((b) => /^\d+$/.test(b))).toEqual(
        expected.map((_, idx) => String(idx + 1)),
      );
    });

    it('leaves no Vietnamese text behind in EN mode', async () => {
      await act(async () => {
        await i18n.changeLanguage('en');
      });

      const { container } = renderSection(user);
      expect(hasVietnameseDiacritics(container.textContent ?? '')).toBe(false);
    });
  });

  it('renders nothing for a VISITOR account', async () => {
    const { container } = renderSection(makeUser({ roleCode: 'VISITOR', subRole: null }));
    expect(container).toBeEmptyDOMElement();
  });
});
