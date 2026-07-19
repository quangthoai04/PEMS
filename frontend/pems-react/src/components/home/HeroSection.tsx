/**
 * Component HeroSection
 * Hero chữ ký của Homepage — bố cục split-screen (chữ lớn bên trái, quả cầu 3D bên phải),
 * cùng ngôn ngữ thị giác với trang Đối tác để tạo bản sắc riêng cho cả hệ thống.
 */

import React, { useState } from 'react';
import { motion } from 'motion/react';
import { useNavigate } from 'react-router-dom';
import { CalendarDays, ArrowRight, ArrowUpRight } from 'lucide-react';
import { VisitingFormPopup } from '../modals/VisitingFormPopup';
import { LazyGlobeShowcase } from './LazyGlobeShowcase';
import { useTranslation } from 'react-i18next';
import { usePerCampusV2Capability } from '../../shared/features/perCampusV2Capability';
import { resolvePublicVisitEntry } from '../../shared/features/perCampusV2Entry';

export function HeroSection() {
  const { t } = useTranslation(['home']);
  const [isVisitorFormOpen, setIsVisitorFormOpen] = useState(false);
  const navigate = useNavigate();

  // Default entry point cutover: when the per-campus v2 capability is enabled the CTA routes
  // to the v2 registration page; otherwise (OFF / still loading / errored) it opens the v1 popup.
  const { status: v2Status, enabled: v2Enabled } = usePerCampusV2Capability();
  const capabilityResolving = v2Status === 'loading';
  const handlePrimaryCta = () => {
    const entry = resolvePublicVisitEntry(v2Enabled);
    if (entry.kind === 'v2-route') navigate(entry.to);
    else setIsVisitorFormOpen(true);
  };

  return (
    <>
      <section className="relative pt-20 pb-6 lg:pt-28 lg:pb-12 bg-[#f8fafc] overflow-x-clip">
        <div className="absolute inset-0 opacity-[0.04] pointer-events-none" style={{ backgroundImage: 'radial-gradient(#004c91 1.5px, transparent 1.5px)', backgroundSize: '28px 28px' }}></div>
        <div className="absolute left-[-15%] top-[10%] w-[500px] h-[500px] bg-fpt-navy/5 rounded-full blur-[110px] pointer-events-none"></div>

        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 relative z-10">
          <div className="flex flex-col lg:flex-row gap-6 lg:gap-12 lg:items-center">

            {/* Left: headline + CTA */}
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.6 }}
              className="w-full lg:w-1/2 flex flex-col items-start relative z-10"
            >
              <div className="flex items-center gap-3 sm:gap-4 mb-4 sm:mb-6">
                <div className="h-[2px] w-8 sm:w-12 bg-fpt-orange"></div>
                <span className="text-fpt-orange font-bold tracking-widest uppercase text-[11px] sm:text-xs">
                  {t('home:hero.department')}
                </span>
              </div>

              <h1 className="max-w-[760px] text-3xl sm:text-4xl md:text-5xl lg:text-[64px] font-black leading-[1.15] lg:leading-[1.1] mb-4 sm:mb-6 tracking-tight">
                <span className="block text-fpt-navy mb-1 sm:mb-2">{t('home:hero.titleLine1')}</span>
                <span className="block text-fpt-orange">{t('home:hero.titleLine2')}</span>
              </h1>

              <p className="text-gray-500 text-sm sm:text-[15px] md:text-base font-medium mb-6 sm:mb-10 max-w-[480px] leading-relaxed">
                {t('home:hero.description')}
              </p>

              <div className="flex flex-col sm:flex-row gap-3 sm:gap-4 w-full sm:w-auto">
                <button
                  onClick={handlePrimaryCta}
                  disabled={capabilityResolving}
                  aria-busy={capabilityResolving}
                  className="inline-flex items-center justify-center gap-2 sm:gap-2.5 w-full sm:w-[300px] h-14 sm:h-16 px-4 bg-fpt-navy text-white font-bold rounded-xl shadow-[0_8px_25px_rgba(0,76,145,0.25)] hover:-translate-y-1 hover:shadow-[0_12px_30px_rgba(0,76,145,0.35)] transition-all duration-300 group text-base sm:text-lg disabled:opacity-70 disabled:cursor-wait disabled:translate-y-0"
                >
                  <CalendarDays className="w-4 h-4 sm:w-5 sm:h-5 shrink-0" />
                  <span className="truncate">{t('home:hero.primaryCta')}</span>
                  <ArrowRight className="w-4 h-4 sm:w-5 sm:h-5 shrink-0 group-hover:translate-x-1 transition-transform" />
                </button>

                <button
                  onClick={() => navigate('/visit-fptu')}
                  className="inline-flex items-center justify-center gap-2 sm:gap-2.5 w-full sm:w-[300px] h-14 sm:h-16 px-4 bg-white text-gray-600 font-bold border-2 border-gray-200 rounded-xl hover:text-white hover:bg-fpt-orange hover:border-fpt-orange hover:shadow-lg transition-all duration-300 text-base sm:text-lg"
                >
                  <span className="truncate">{t('home:hero.secondaryCta')}</span>
                  <ArrowUpRight className="w-4 h-4 sm:w-5 sm:h-5 shrink-0" />
                </button>
              </div>
            </motion.div>

            {/* Right: 3D globe — real partner countries, lazy-loaded */}
            <motion.div
              initial={{ opacity: 0, scale: 0.9 }}
              animate={{ opacity: 1, scale: 1 }}
              transition={{ duration: 0.7, delay: 0.15 }}
              className="w-full lg:w-1/2 h-[260px] sm:h-[420px] lg:h-[700px] flex items-center justify-center relative lg:-mr-24 xl:-mr-32"
            >
              <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[220px] h-[220px] sm:w-[360px] sm:h-[360px] lg:w-[460px] lg:h-[460px] bg-sky-200/25 blur-[70px] sm:blur-[100px] lg:blur-[110px] rounded-full pointer-events-none"></div>
              <LazyGlobeShowcase />
            </motion.div>
          </div>
        </div>
      </section>

      <VisitingFormPopup isOpen={isVisitorFormOpen} onClose={() => setIsVisitorFormOpen(false)} />
    </>
  );
}
