/**
 * Component GuideStepsSection
 * Hướng dẫn quy trình theo role — dạng timeline/checklist tĩnh, KHÔNG phải task thật.
 */

import React from 'react';
import { motion } from 'motion/react';
import type { AuthUser } from '../../../features/authentication/types/authentication.types';
import { resolveHomeRoleBucket, HomeRoleBucket } from '../../../shared/auth/resolveHomeRoleBucket';

const GUIDE_STEPS: Record<HomeRoleBucket, string[]> = {
  STUDENT: [
    'Kiểm tra lời mời tham gia hỗ trợ đoàn tham quan trong Dashboard.',
    'Chấp nhận hoặc từ chối lời mời trước hạn phản hồi.',
    'Xem lịch hỗ trợ đã xác nhận và chuẩn bị theo hướng dẫn của Host.',
    'Tham gia hỗ trợ đoàn đúng thời gian, địa điểm đã lên lịch.',
  ],
  HO: [
    'Theo dõi các yêu cầu tham quan liên cơ sở trong phạm vi phụ trách.',
    'Quản lý tin tức và FAQ hiển thị công khai cho đối tác.',
    'Cập nhật thông tin campus khi có thay đổi.',
    'Giám sát tiến độ tiếp đón qua Dashboard.',
  ],
  ADMIN: [
    'Quản lý tài khoản người dùng và phân quyền hệ thống.',
    'Cấu hình các API tích hợp (OCR, dịch thuật, v.v.).',
    'Theo dõi nhật ký hoạt động và bảo mật hệ thống.',
  ],
  STAFF_LEADER: [
    'Duyệt các yêu cầu tham quan mới trong campus.',
    'Gán Host phụ trách tiếp đón cho từng đoàn.',
    'Quản lý tài khoản và phòng ban trong campus.',
    'Cập nhật Gallery/Tin tức giới thiệu campus.',
  ],
  STAFF: [
    'Kiểm tra các đơn tham quan được phân công phụ trách.',
    'Chuẩn bị chương trình đón tiếp trước ngày visit.',
    'Ghi nhận biên bản, tin tức, hình ảnh sau buổi tham quan.',
  ],
  DEPT_LEADER: [
    'Phân công nhân sự phòng ban hỗ trợ các đoàn tham quan.',
    'Theo dõi nhiệm vụ và tiến độ của phòng ban.',
    'Ký bàn giao khi hoàn tất công tác hỗ trợ.',
    'Phối hợp với Phòng Hợp tác Quốc tế (IC) khi cần.',
  ],
  DEPT_STAFF: [
    'Xem lịch hỗ trợ được phòng ban phân công.',
    'Phản hồi nhiệm vụ được giao đúng hạn.',
    'Ký nhận / ký trả khi bàn giao công việc.',
    'Cập nhật kết quả hỗ trợ sau khi hoàn tất.',
  ],
  VISITOR: [],
};

interface GuideStepsSectionProps {
  user: AuthUser;
}

export function GuideStepsSection({ user }: GuideStepsSectionProps) {
  const bucket = resolveHomeRoleBucket(user);
  if (!bucket) return null;

  const steps = GUIDE_STEPS[bucket];
  if (!steps || steps.length === 0) return null;

  return (
    <section className="py-14 sm:py-16 lg:py-20 bg-slate-50 relative overflow-hidden">
      <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="mb-12">
          <h2 className="text-3xl font-bold text-fpt-navy">Hướng dẫn quy trình</h2>
          <div className="w-16 h-1.5 bg-fpt-orange mt-4 rounded-full"></div>
        </div>

        <div className="space-y-4">
          {steps.map((step, idx) => (
            <motion.div
              key={idx}
              initial={{ opacity: 0, x: -10 }}
              whileInView={{ opacity: 1, x: 0 }}
              viewport={{ once: true }}
              transition={{ duration: 0.4, delay: idx * 0.08 }}
              className="flex items-start gap-4 bg-white rounded-2xl border border-slate-200 p-5 shadow-sm"
            >
              <div className="w-8 h-8 rounded-full bg-fpt-navy text-white font-bold text-sm flex items-center justify-center flex-shrink-0">
                {idx + 1}
              </div>
              <p className="text-slate-700 leading-relaxed pt-1">{step}</p>
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  );
}
