/**
 * Component VisitProcessSection
 * Quy trình gửi yêu cầu tham quan (submit → review → host assigned → confirmed).
 * Bố cục timeline dọc, so le trái/phải trên desktop quanh 1 trục nối ở giữa — trên mobile
 * (dưới lg) tự động rút về timeline dọc đơn giản bám lề trái.
 * Nội dung hướng dẫn tĩnh — không phải data cần lấy từ API.
 */

import React from 'react';
import { motion } from 'motion/react';
import { FileEdit, ClipboardCheck, UserCheck, CalendarCheck } from 'lucide-react';

const STEPS = [
  {
    icon: FileEdit,
    title: 'Gửi yêu cầu',
    desc: 'Điền thông tin đăng ký tham quan và thành phần đoàn qua form trực tuyến.',
  },
  {
    icon: ClipboardCheck,
    title: 'FPTU xem xét',
    desc: 'Phòng Hợp tác Quốc tế tiếp nhận và xem xét yêu cầu của quý đối tác.',
  },
  {
    icon: UserCheck,
    title: 'Phân công tiếp đón',
    desc: 'Cán bộ phụ trách (host) được phân công chuẩn bị và tiếp đón đoàn.',
  },
  {
    icon: CalendarCheck,
    title: 'Xác nhận lịch',
    desc: 'Lịch trình tham quan được xác nhận và gửi lại cho quý đối tác.',
  },
];

export function VisitProcessSection() {
  return (
    <section className="py-14 sm:py-20 lg:py-24 bg-slate-50 relative overflow-hidden">
      <div className="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 relative z-10">
        <div className="text-center mb-10 sm:mb-16 lg:mb-24">
          <h2 className="text-3xl font-bold text-fpt-navy mb-2">Quy trình gửi yêu cầu tham quan</h2>
          <div className="w-24 h-1.5 bg-fpt-orange mt-4 mb-6 mx-auto rounded-full"></div>
          <p className="text-gray-600 max-w-2xl mx-auto text-lg">
            Đơn giản, minh bạch — từ lúc gửi yêu cầu đến khi buổi tham quan diễn ra.
          </p>
        </div>

        <div className="relative">
          {/* Trục nối dọc: bám lề trái trên mobile, ở giữa trên desktop */}
          <div className="absolute left-6 lg:left-1/2 top-0 bottom-0 w-0.5 lg:-translate-x-1/2 bg-gradient-to-b from-fpt-navy/20 via-fpt-orange/40 to-fpt-navy/20" />

          <div className="space-y-12 lg:space-y-0">
            {STEPS.map((step, idx) => {
              const Icon = step.icon;
              const isEven = idx % 2 === 0;
              return (
                <motion.div
                  key={step.title}
                  initial={{ opacity: 0, x: isEven ? -20 : 20 }}
                  whileInView={{ opacity: 1, x: 0 }}
                  viewport={{ once: true }}
                  transition={{ duration: 0.5, delay: idx * 0.12 }}
                  className={`relative flex items-start gap-6 lg:gap-0 lg:mb-16 last:lg:mb-0 ${
                    isEven ? 'lg:flex-row' : 'lg:flex-row-reverse'
                  }`}
                >
                  {/* Nút số thứ tự trên trục */}
                  <div className="absolute left-6 lg:left-1/2 -translate-x-1/2 top-0 z-10 w-12 h-12 rounded-full bg-white border-2 border-fpt-orange shadow-md flex items-center justify-center flex-shrink-0">
                    <span className="text-fpt-orange font-black text-sm">{String(idx + 1).padStart(2, '0')}</span>
                  </div>

                  {/* Nội dung: nhường chỗ cho trục bên trái trên mobile; 1 nửa bên desktop */}
                  <div className="pl-20 lg:pl-0 lg:w-1/2 w-full">
                    <div className={`lg:max-w-md ${isEven ? 'lg:ml-auto lg:pr-14 lg:text-right' : 'lg:mr-auto lg:pl-14'}`}>
                      <div className={`inline-flex items-center gap-3 mb-3 ${isEven ? 'lg:flex-row-reverse' : ''}`}>
                        <div className="w-10 h-10 rounded-xl bg-fpt-navy/10 flex items-center justify-center flex-shrink-0">
                          <Icon className="w-5 h-5 text-fpt-navy" />
                        </div>
                        <h3 className="text-lg font-bold text-slate-900">{step.title}</h3>
                      </div>
                      <p className="text-sm text-slate-500 leading-relaxed">{step.desc}</p>
                    </div>
                  </div>

                  {/* Khoảng trống đối xứng phía bên kia trục (chỉ desktop) */}
                  <div className="hidden lg:block lg:w-1/2" />
                </motion.div>
              );
            })}
          </div>
        </div>
      </div>
    </section>
  );
}
