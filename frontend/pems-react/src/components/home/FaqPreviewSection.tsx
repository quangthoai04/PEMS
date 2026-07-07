/**
 * Component FaqPreviewSection
 * CTA gọn tới trang FAQ đầy đủ — không tải danh sách câu hỏi trên trang chủ.
 */

import React from 'react';
import { motion } from 'motion/react';
import { HelpCircle, ArrowRight } from 'lucide-react';
import { Link } from 'react-router-dom';

export function FaqPreviewSection() {
  return (
    <section className="py-14 sm:py-20 lg:py-24 bg-slate-50 relative overflow-hidden">
      <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          transition={{ duration: 0.5 }}
          className="bg-white rounded-3xl border border-gray-100 shadow-sm px-6 py-12 sm:px-12 sm:py-16 text-center flex flex-col items-center"
        >
          <div className="w-16 h-16 rounded-2xl bg-orange-50 flex items-center justify-center mb-6">
            <HelpCircle className="w-8 h-8 text-fpt-orange" />
          </div>
          <h2 className="text-2xl sm:text-3xl font-bold text-fpt-navy mb-3">Câu hỏi thường gặp</h2>
          <p className="text-slate-500 text-[15px] max-w-lg mb-8">
            Giải đáp về tài khoản, đăng ký tham quan, quản lý đoàn, hậu cần, tài liệu và thông báo trong hệ thống PEMS.
          </p>
          <Link
            to="/faq"
            className="inline-flex items-center gap-2 px-6 py-3 bg-fpt-orange text-white font-bold rounded-xl hover:bg-fpt-orange-hover transition-colors text-sm"
          >
            Xem tất cả câu hỏi <ArrowRight className="w-4 h-4" />
          </Link>
        </motion.div>
      </div>
    </section>
  );
}
