/**
 * Component Footer
 * Chân trang của ứng dụng công cộng.
 * Hiển thị thông tin bản quyền, liên hệ và các liên kết hữu ích.
 */

// Đây là component phần chân trang của website (Footer)
import React from 'react';
import { ArrowUp, MapPin, Phone, Mail, Facebook, Youtube, Globe as GlobeIcon } from 'lucide-react';
import { Link } from 'react-router-dom';
import footerLogo from '../../assets/images/2021-FPTU-Eng.png';
import { useTranslation } from 'react-i18next';

export function Footer() {
  const { t } = useTranslation(['publicLayout']);
  const scrollToTop = () => {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  return (
    <footer className="bg-fpt-footer text-gray-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-12 gap-8">
          
          {/* Column 1: Info (Spans 5 cols) */}
          <div className="lg:col-span-5 space-y-4">
            <img src={footerLogo} alt="FPT Education" className="h-12 object-contain mb-4 bg-white p-2 rounded-lg" />
            
            <h3 className="text-white font-bold tracking-wide uppercase text-base mb-3">{t('publicLayout:footer.deptName')}</h3>
            
            <ul className="space-y-2">
              <li className="flex items-start gap-3">
                <MapPin className="w-4 h-4 text-gray-400 shrink-0 mt-0.5" />
                <span className="text-xs">{t('publicLayout:footer.hqAddress')}</span>
              </li>
              <li className="flex items-center gap-3">
                <Phone className="w-4 h-4 text-gray-400 shrink-0" />
                <span className="text-xs">024 6680 5912</span>
              </li>
              <li className="flex items-center gap-3">
                <Mail className="w-4 h-4 text-gray-400 shrink-0" />
                <a href="mailto:international.fptu@fpt.edu.vn" className="text-xs hover:text-fpt-orange transition-colors">international.fptu@fpt.edu.vn</a>
              </li>
            </ul>
          </div>

          {/* Column 2: System (Spans 4 cols) */}
          <div className="lg:col-span-4">
            <h3 className="text-fpt-orange font-bold tracking-wide uppercase text-base mb-4">{t('publicLayout:footer.system')}</h3>
            <ul className="space-y-2 text-xs text-gray-300">
              <li className="flex items-center gap-2">
                 <MapPin className="w-3.5 h-3.5 text-gray-500" />
                 {t('publicLayout:footer.hanoi')}
              </li>
              <li className="flex items-center gap-2">
                 <MapPin className="w-3.5 h-3.5 text-gray-500" />
                 {t('publicLayout:footer.danang')}
              </li>
              <li className="flex items-center gap-2">
                 <MapPin className="w-3.5 h-3.5 text-gray-500" />
                 {t('publicLayout:footer.hcm')}
              </li>
              <li className="flex items-center gap-2">
                 <MapPin className="w-3.5 h-3.5 text-gray-500" />
                 {t('publicLayout:footer.cantho')}
              </li>
              <li className="flex items-center gap-2">
                 <MapPin className="w-3.5 h-3.5 text-gray-500" />
                 {t('publicLayout:footer.quynhon')}
              </li>
            </ul>
          </div>

          {/* Column 3: Connect (Spans 3 cols) */}
          <div className="lg:col-span-3">
            <h3 className="text-fpt-orange font-bold tracking-wide uppercase text-base mb-4">{t('publicLayout:footer.connect')}</h3>
            <div className="flex gap-3 mb-4">
              <a href="#" className="w-8 h-8 rounded-full border border-gray-500 flex items-center justify-center hover:bg-fpt-orange hover:border-fpt-orange transition-colors">
                <Facebook className="w-3.5 h-3.5" />
              </a>
              <a href="#" className="w-8 h-8 rounded-full border border-gray-500 flex items-center justify-center hover:bg-fpt-orange hover:border-fpt-orange transition-colors">
                <Youtube className="w-3.5 h-3.5" />
              </a>
              <a href="#" className="w-8 h-8 rounded-full border border-gray-500 flex items-center justify-center hover:bg-fpt-orange hover:border-fpt-orange transition-colors">
                <GlobeIcon className="w-3.5 h-3.5" />
              </a>
            </div>
            
            <ul className="space-y-2 text-xs text-gray-300">
              <li><Link to="/privacy" className="hover:text-white transition-colors">{t('publicLayout:footer.privacy')}</Link></li>
              <li><Link to="/terms" className="hover:text-white transition-colors">{t('publicLayout:footer.terms')}</Link></li>
              <li><Link to="/faq" className="hover:text-white transition-colors">FAQs</Link></li>
            </ul>
          </div>

        </div>

        {/* Bottom bar */}
        <div className="mt-8 pt-4 border-t border-white/10 flex flex-col items-center justify-center gap-4 relative">
          <div className="text-center text-xs text-gray-400 space-y-1 mt-2">
            <p>{t('publicLayout:footer.copyright')}</p>
          </div>
          
          <button 
            onClick={scrollToTop}
            className="md:absolute right-0 -top-10 bg-white text-fpt-footer w-12 h-12 rounded-full flex justify-center items-center hover:bg-gray-100 shadow-xl hover:-translate-y-1 transition-all duration-300 group"
            aria-label={t('publicLayout:footer.backToTop')}
          >
            <ArrowUp className="w-6 h-6 group-hover:text-fpt-orange transition-colors" />
          </button>
        </div>
      </div>
    </footer>
  );
}
