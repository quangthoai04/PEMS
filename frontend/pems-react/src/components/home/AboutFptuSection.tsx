/**
 * Component AboutFptuSection
 * Giới thiệu FPT University — dải màu thương hiệu (cam/xanh dương/xanh lá) chạy dọc bên trái
 * làm trục xuyên suốt, nội dung + mini-stat xếp dọc theo — khác biệt so với bố cục "3 cột
 * icon ngang" thường thấy ở trang giới thiệu đại học. Trên mobile rút về dạng dọc đơn giản.
 */

import React from 'react';
import { motion } from 'motion/react';
import { GraduationCap, Globe2, Rocket, Building2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';

export function AboutFptuSection() {
  const { t } = useTranslation(['home']);
  const HIGHLIGHTS = [
    { icon: Building2, value: '5', label: t('home:about.stat1'), bar: 'bg-fpt-orange' },
    { icon: Globe2, value: '40+', label: t('home:about.stat2'), bar: 'bg-fpt-navy' },
    { icon: Rocket, value: '100%', label: t('home:about.stat3'), bar: 'bg-emerald-500' },
  ];


  return (
    <section className="py-14 sm:py-20 lg:py-24 bg-white relative overflow-hidden">
      <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex flex-col lg:flex-row gap-2 lg:gap-0">

          {/* Dải màu thương hiệu chạy dọc — chữ ký thị giác của section */}
          <div className="hidden lg:flex flex-col w-2 rounded-full overflow-hidden flex-shrink-0 mr-12">
            <div className="flex-1 bg-fpt-orange" />
            <div className="flex-1 bg-fpt-navy" />
            <div className="flex-1 bg-emerald-500" />
          </div>
          <div className="lg:hidden h-1.5 w-24 rounded-full mb-8 bg-gradient-to-r from-fpt-orange via-fpt-navy to-emerald-500" />

          <div className="flex-1">
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ duration: 0.5 }}
              className="mb-12 max-w-2xl"
            >
              <div className="flex items-center gap-3 mb-5">
                <GraduationCap className="w-7 h-7 text-fpt-orange" />
                <span className="text-fpt-orange font-bold tracking-widest uppercase text-xs">{t('home:about.sectionTitle')}</span>
              </div>
              <h2 className="text-2xl md:text-3xl lg:text-[34px] font-bold text-fpt-navy leading-snug mb-4">
                {t('home:about.headline')}
              </h2>
              <p className="text-slate-500 text-[15px] leading-relaxed">
                {t('home:about.description')}
              </p>
            </motion.div>

            {/* Mini-stat xếp dọc, mỗi dòng nối với 1 màu trên dải trục */}
            <div className="space-y-4">
              {HIGHLIGHTS.map((item, idx) => {
                const Icon = item.icon;
                return (
                  <motion.div
                    key={item.label}
                    initial={{ opacity: 0, x: -16 }}
                    whileInView={{ opacity: 1, x: 0 }}
                    viewport={{ once: true }}
                    transition={{ duration: 0.45, delay: 0.1 + idx * 0.08 }}
                    className="flex items-center gap-5 bg-slate-50 border border-slate-200 rounded-2xl px-6 py-6 hover:border-fpt-orange/30 hover:bg-orange-50/20 transition-colors"
                  >
                    <span className={`w-1.5 h-12 rounded-full flex-shrink-0 ${item.bar}`} />
                    <div className="w-14 h-14 rounded-xl bg-white shadow-sm flex items-center justify-center flex-shrink-0">
                      <Icon className="w-6 h-6 text-fpt-navy" />
                    </div>
                    <div className="flex items-baseline gap-3 flex-wrap">
                      <p className="text-4xl font-black text-fpt-navy leading-none">{item.value}</p>
                      <p className="text-base text-slate-600 font-semibold">{item.label}</p>
                    </div>
                  </motion.div>
                );
              })}
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
