/**
 * Component CampusShowcaseSection
 * Giới thiệu 5 cơ sở FPT University — bố cục "hành trình" so le trái/phải dọc theo 1 trục
 * ở giữa (không phải grid ngang đều), dùng ảnh thật đã có sẵn trong assets/FPTbanner_visit.
 * Trên mobile (dưới lg) rút về dạng thẻ dọc bám lề trái, đơn giản dễ đọc.
 */

import React from 'react';
import { motion } from 'motion/react';
import { Link } from 'react-router-dom';
import { ArrowUpRight, MapPin } from 'lucide-react';
import bgHN from '../../assets/FPTbanner_visit/hola_new.jpg';
import bgHCM from '../../assets/FPTbanner_visit/HCM.png';
import bgCT from '../../assets/FPTbanner_visit/CanTho.png';
import bgDN from '../../assets/FPTbanner_visit/DaNang.png';
import bgQN from '../../assets/FPTbanner_visit/QuyNhon.png';
import { useTranslation } from 'react-i18next';

const CAMPUSES = [
  { code: 'HN', key: 'HN', img: bgHN },
  { code: 'HCM', key: 'HCM', img: bgHCM },
  { code: 'DN', key: 'DN', img: bgDN },
  { code: 'CT', key: 'CT', img: bgCT },
  { code: 'QN', key: 'QN', img: bgQN },
];

export function CampusShowcaseSection() {
  const { t } = useTranslation(['home']);
  return (
    <section className="py-14 sm:py-20 lg:py-24 bg-slate-50 relative overflow-hidden">
      <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="mb-10 sm:mb-16 lg:mb-24 text-center">
          <h2 className="text-3xl font-bold text-fpt-navy mb-2">{t('home:campus.title')}</h2>
          <div className="w-24 h-1.5 bg-fpt-orange mt-4 mb-6 mx-auto rounded-full"></div>
          <p className="text-gray-600 max-w-2xl mx-auto text-lg leading-relaxed">
            {t('home:campus.subtitle')}
          </p>
        </div>

        <div className="relative">
          {/* Trục hành trình dọc: bám lề trái trên mobile, ở giữa trên desktop */}
          <div className="absolute left-6 lg:left-1/2 top-0 bottom-0 w-0.5 lg:-translate-x-1/2 bg-gradient-to-b from-fpt-orange/30 via-fpt-navy/30 to-fpt-orange/30" />

          <div className="space-y-14 lg:space-y-4">
            {CAMPUSES.map((campus, idx) => {
              const isEven = idx % 2 === 0;
              return (
                <motion.div
                  key={campus.code}
                  initial={{ opacity: 0, x: isEven ? -24 : 24 }}
                  whileInView={{ opacity: 1, x: 0 }}
                  viewport={{ once: true }}
                  transition={{ duration: 0.5, delay: idx * 0.1 }}
                  className={`relative flex items-center gap-6 lg:gap-0 ${isEven ? 'lg:flex-row' : 'lg:flex-row-reverse'}`}
                >
                  {/* Chấm mốc trên trục */}
                  <div className="absolute left-6 lg:left-1/2 -translate-x-1/2 z-10 w-4 h-4 rounded-full bg-fpt-orange border-4 border-white shadow-md flex-shrink-0" />

                  <div className="pl-16 lg:pl-0 lg:w-1/2 w-full">
                    <Link
                      to={`/visit-fptu/${campus.code}`}
                      className={`group flex ${isEven ? 'lg:flex-row lg:mr-14' : 'lg:flex-row-reverse lg:ml-14'} items-center gap-5 bg-white rounded-3xl border border-slate-200 p-3 shadow-sm hover:shadow-xl hover:border-fpt-orange/30 transition-all duration-300`}
                    >
                      <div className="relative w-28 h-28 sm:w-32 sm:h-32 rounded-2xl overflow-hidden flex-shrink-0 bg-slate-200">
                        <img
                          src={campus.img}
                          alt={`Campus ${t(`home:campus.${campus.key}`)}`}
                          loading="lazy"
                          className="w-full h-full object-cover group-hover:scale-110 transition-transform duration-700 ease-out"
                        />
                      </div>
                      <div className={`flex-1 min-w-0 ${isEven ? 'lg:text-right' : ''}`}>
                        <p className="text-fpt-orange text-xs font-bold uppercase tracking-widest mb-1">FPT University</p>
                        <div className={`flex items-center gap-2 mb-1 ${isEven ? 'lg:flex-row-reverse' : ''}`}>
                          <MapPin className="w-4 h-4 text-fpt-navy flex-shrink-0" />
                          <h3 className="text-lg font-bold text-slate-900">{t(`home:campus.${campus.key}`)}</h3>
                        </div>
                        <div className={`inline-flex items-center gap-1.5 text-sm font-semibold text-fpt-navy group-hover:text-fpt-orange transition-colors ${isEven ? 'lg:flex-row-reverse' : ''}`}>
                          {t('home:campus.explore')} <ArrowUpRight className="w-3.5 h-3.5 group-hover:translate-x-0.5 group-hover:-translate-y-0.5 transition-transform" />
                        </div>
                      </div>
                    </Link>
                  </div>

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
