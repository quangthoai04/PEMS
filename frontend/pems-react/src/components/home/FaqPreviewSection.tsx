/**
 * Component FaqPreviewSection
 * Xem trước FAQ published — lấy 5 câu mới nhất từ GET /api/public/faqs (không dùng mock).
 */

import React, { useEffect, useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { ChevronDown, HelpCircle, ArrowRight } from 'lucide-react';
import { Link } from 'react-router-dom';
import { publicContentApi } from '../../features/public-content/api/publicContentApi';
import type { PublicFaqItem } from '../../features/public-content/types/publicContent.types';

export function FaqPreviewSection() {
  const [faqs, setFaqs] = useState<PublicFaqItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [openFaqId, setOpenFaqId] = useState<number | null>(null);

  useEffect(() => {
    let cancelled = false;
    publicContentApi
      .getPublicFaqs({ page: 1, pageSize: 5 })
      .then((data) => { if (!cancelled) setFaqs(data.items ?? []); })
      .catch(() => { /* trang chủ vẫn hiển thị bình thường khi API lỗi */ })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  if (!loading && faqs.length === 0) return null;

  return (
    <section className="py-14 sm:py-20 lg:py-24 bg-slate-50 relative overflow-hidden">
      <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-end justify-between mb-12">
          <div>
            <h2 className="text-3xl font-bold text-fpt-navy">Câu hỏi thường gặp</h2>
            <div className="w-16 h-1.5 bg-fpt-orange mt-4 rounded-full"></div>
          </div>
          <Link to="/faq" className="hidden sm:flex items-center gap-2 text-fpt-orange font-medium hover:text-fpt-orange-hover transition-colors">
            Xem tất cả <ArrowRight className="w-4 h-4" />
          </Link>
        </div>

        <div className="space-y-4">
          {loading
            ? Array.from({ length: 3 }).map((_, i) => (
                <div key={i} className="bg-white rounded-2xl border border-gray-100 h-20 animate-pulse" />
              ))
            : (
              <AnimatePresence>
                {faqs.map((faq) => {
                  const isOpen = openFaqId === faq.faqId;
                  return (
                    <motion.div
                      key={faq.faqId}
                      initial={{ opacity: 0, y: 10 }}
                      animate={{ opacity: 1, y: 0 }}
                      className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden hover:shadow-md transition-shadow"
                    >
                      <button
                        onClick={() => setOpenFaqId(isOpen ? null : faq.faqId)}
                        className="w-full px-6 py-5 flex items-center justify-between text-left"
                      >
                        <h3 className={`text-base font-bold pr-4 transition-colors ${isOpen ? 'text-fpt-orange' : 'text-fpt-navy'}`}>
                          {faq.question}
                        </h3>
                        <div className={`flex-shrink-0 w-8 h-8 rounded-full flex items-center justify-center transition-transform duration-300 ${isOpen ? 'bg-orange-50 rotate-180' : 'bg-gray-50'}`}>
                          <ChevronDown className={`w-4 h-4 ${isOpen ? 'text-fpt-orange' : 'text-gray-400'}`} />
                        </div>
                      </button>
                      <AnimatePresence>
                        {isOpen && (
                          <motion.div
                            initial={{ height: 0, opacity: 0 }}
                            animate={{ height: 'auto', opacity: 1 }}
                            exit={{ height: 0, opacity: 0 }}
                            transition={{ duration: 0.3, ease: 'easeInOut' }}
                            className="overflow-hidden"
                          >
                            <div className="px-6 pb-6">
                              <p className="text-gray-700 leading-relaxed whitespace-pre-line text-sm">{faq.answer}</p>
                            </div>
                          </motion.div>
                        )}
                      </AnimatePresence>
                    </motion.div>
                  );
                })}
              </AnimatePresence>
            )}

          {!loading && faqs.length === 0 && (
            <div className="text-center py-16 bg-white rounded-3xl border border-gray-100">
              <HelpCircle className="w-12 h-12 text-gray-300 mx-auto mb-4" />
              <p className="text-gray-500">Chưa có câu hỏi thường gặp nào.</p>
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
