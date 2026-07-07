/**
 * Component FinalCtaSection
 * CTA cuối trang trước Footer — Đăng ký tham quan / Liên hệ Phòng HTQT.
 */

import React, { useState } from 'react';
import { CalendarDays, Mail } from 'lucide-react';
import { VisitingFormPopup } from '../modals/VisitingFormPopup';

export function FinalCtaSection() {
  const [isVisitorFormOpen, setIsVisitorFormOpen] = useState(false);

  return (
    <>
      <section className="py-14 sm:py-16 lg:py-20 bg-fpt-navy relative overflow-hidden">
        <div className="absolute inset-0 opacity-[0.05] pointer-events-none" style={{ backgroundImage: 'radial-gradient(#fff 2px, transparent 2px)', backgroundSize: '40px 40px' }}></div>
        <div className="absolute left-0 top-0 w-96 h-96 bg-fpt-orange/10 rounded-full blur-[100px] pointer-events-none"></div>

        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 text-center relative z-10">
          <h2 className="text-3xl md:text-4xl font-bold text-white mb-4">Sẵn sàng kết nối cùng FPT University?</h2>
          <p className="text-blue-200 text-lg mb-10 max-w-2xl mx-auto">
            Đăng ký tham quan hoặc liên hệ trực tiếp với Phòng Hợp tác Quốc tế để bắt đầu hành trình hợp tác.
          </p>

          <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
            <button
              onClick={() => setIsVisitorFormOpen(true)}
              className="inline-flex items-center gap-2 bg-fpt-orange text-white font-bold px-8 py-4 rounded-2xl hover:bg-fpt-orange-hover hover:-translate-y-1 transition-all duration-300 shadow-xl"
            >
              <CalendarDays className="w-5 h-5" />
              Đăng ký tham quan
            </button>
            <a
              href="mailto:ic@fpt.edu.vn"
              className="inline-flex items-center gap-2 bg-white/10 text-white font-bold px-8 py-4 rounded-2xl border border-white/20 hover:bg-white/20 hover:-translate-y-1 transition-all duration-300"
            >
              <Mail className="w-5 h-5" />
              Liên hệ Phòng HTQT
            </a>
          </div>
        </div>
      </section>

      <VisitingFormPopup isOpen={isVisitorFormOpen} onClose={() => setIsVisitorFormOpen(false)} />
    </>
  );
}
