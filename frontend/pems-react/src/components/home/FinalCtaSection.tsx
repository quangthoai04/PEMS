/**
 * Component FinalCtaSection
 * CTA cuối trang trước Footer — Đăng ký tham quan / Liên hệ Phòng HTQT.
 */

import { CalendarDays, Mail } from 'lucide-react';
import { VisitingFormPopup } from '../modals/VisitingFormPopup';
import { useTranslation } from 'react-i18next';
import { useVisitEntryCta } from '../../shared/features/useVisitEntryCta';

export function FinalCtaSection() {
  const { t } = useTranslation(['home']);
  // Shared entry decision (mirrors HeroSection): v2 route when enabled, v1 popup only for a real
  // backend OFF, error+retry on a capability fetch failure — never a silent v1 fallback.
  const visitCta = useVisitEntryCta('public');
  const capabilityResolving = visitCta.isResolving;
  const handleBookVisit = visitCta.trigger;

  return (
    <>
      <section className="py-14 sm:py-16 lg:py-20 bg-fpt-navy relative overflow-hidden">
        <div className="absolute inset-0 opacity-[0.05] pointer-events-none" style={{ backgroundImage: 'radial-gradient(#fff 2px, transparent 2px)', backgroundSize: '40px 40px' }}></div>
        <div className="absolute left-0 top-0 w-96 h-96 bg-fpt-orange/10 rounded-full blur-[100px] pointer-events-none"></div>

        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 text-center relative z-10">
          <h2 className="text-3xl md:text-4xl font-bold text-white mb-4">{t('home:cta.title')}</h2>
          <p className="text-blue-200 text-lg mb-10 max-w-2xl mx-auto">
            {t('home:cta.subtitle')}
          </p>

          <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
            <button
              onClick={handleBookVisit}
              disabled={capabilityResolving}
              aria-busy={capabilityResolving}
              className="inline-flex items-center justify-center gap-2 w-full sm:w-[280px] h-14 bg-fpt-orange text-white font-bold px-6 rounded-2xl hover:bg-fpt-orange-hover hover:-translate-y-1 transition-all duration-300 shadow-xl disabled:opacity-70 disabled:cursor-wait disabled:translate-y-0"
            >
              <CalendarDays className="w-5 h-5 shrink-0" />
              <span className="truncate">{t('home:cta.bookVisit')}</span>
            </button>
            <a
              href="mailto:ic@fpt.edu.vn"
              className="inline-flex items-center justify-center gap-2 w-full sm:w-[280px] h-14 bg-white/10 text-white font-bold px-6 rounded-2xl border border-white/20 hover:bg-white/20 hover:-translate-y-1 transition-all duration-300"
            >
              <Mail className="w-5 h-5 shrink-0" />
              <span className="truncate">{t('home:cta.contact')}</span>
            </a>
          </div>
        </div>
      </section>

      <VisitingFormPopup isOpen={visitCta.popupOpen} onClose={visitCta.closePopup} />
    </>
  );
}
