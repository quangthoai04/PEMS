/**
 * Component PartnersSection
 * Khu vực hiển thị danh sách và biểu trưng của các đối tác chiến lược.
 * Lấy đối tác APPROVED + PUBLIC thật từ GET /api/public/partners (không dùng mock).
 */

import React, { useEffect, useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Link } from 'react-router-dom';
import { publicPartnersApi } from '../../features/public-partners/api/publicPartnersApi';
import { PublicPartner } from '../../features/public-partners/types/publicPartners.types';
import { usePublicPartnerImage } from '../../features/public-partners/hooks/usePublicPartnerImage';
import { useTranslation } from 'react-i18next';

const ITEMS_PER_PAGE = 18;

function PartnerLogo({ partner }: { partner: PublicPartner }) {
  // `resolveFileUrl` expects a path that already has `/api` in it (the URLs it's designed for),
  // but `mediaContent()` deliberately omits `/api` so it can be handed to httpClient (whose
  // baseURL already carries it) — feeding that path through resolveFileUrl instead dropped the
  // `/api` segment and 404'd, silently falling back to text for almost every partner logo.
  // usePublicPartnerImage fetches through httpClient like PartnersPage's cards do, which is why
  // those rendered correctly while this section didn't.
  const logoUrl = usePublicPartnerImage(partner.logoFileId);

  if (!logoUrl) {
    return (
      <span className="text-sm font-semibold text-fpt-navy text-center px-2 line-clamp-2">
        {partner.shortName ?? partner.name}
      </span>
    );
  }

  return (
    <img
      src={logoUrl}
      alt={partner.name}
      loading="lazy"
      className="max-w-full max-h-full object-contain transition-all duration-300"
    />
  );
}

export function PartnersSection() {
  const { t, i18n } = useTranslation(['home']);
  const [partners, setPartners] = useState<PublicPartner[]>([]);
  const [loading, setLoading] = useState(true);
  const [currentPage, setCurrentPage] = useState(0);

  useEffect(() => {
    let cancelled = false;
    publicPartnersApi
      .getPublicPartners({ pageSize: 36, languageCode: i18n.language })
      .then((data) => { if (!cancelled) setPartners(data.items ?? []); })
      .catch(() => { /* trang chủ vẫn hiển thị bình thường khi API lỗi */ })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [i18n.language]);

  if (!loading && partners.length === 0) return null;

  const totalPages = Math.max(1, Math.ceil(partners.length / ITEMS_PER_PAGE));
  const currentPartners = partners.slice(
    currentPage * ITEMS_PER_PAGE,
    (currentPage + 1) * ITEMS_PER_PAGE,
  );

  const nextPage = () => setCurrentPage((prev) => (prev + 1) % totalPages);
  const prevPage = () => setCurrentPage((prev) => (prev - 1 + totalPages) % totalPages);

  return (
    <section id="partners" className="py-14 sm:py-20 lg:py-24 bg-white overflow-hidden relative">
      <div className="absolute left-[-20%] bottom-[-20%] w-[600px] h-[600px] bg-fpt-orange/5 rounded-full blur-[100px] pointer-events-none"></div>
      <div className="absolute right-[-10%] top-[-10%] w-[500px] h-[500px] bg-fpt-navy/5 rounded-full blur-[80px] pointer-events-none"></div>
      <div className="absolute inset-0 opacity-[0.03] pointer-events-none" style={{ backgroundImage: 'radial-gradient(#F37021 2px, transparent 2px)', backgroundSize: '32px 32px' }}></div>

      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 mb-12 text-center relative z-10 w-full">
        <h2 className="text-3xl font-bold text-fpt-navy mb-2">{t('home:partners.title')}</h2>
        <div className="w-24 h-1.5 bg-fpt-orange mt-4 mb-6 mx-auto rounded-full"></div>
        <p className="text-gray-600 max-w-3xl mx-auto text-lg leading-relaxed">
          {t('home:partners.subtitle')}
        </p>
      </div>

      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 relative z-10 w-full overflow-visible">
        <div className="relative flex items-center group">
          {totalPages > 1 && (
            <button
              onClick={prevPage}
              className="absolute -left-5 sm:-left-8 lg:-left-16 z-20 w-10 h-10 sm:w-12 sm:h-12 rounded-full border border-gray-200 bg-white flex items-center justify-center hover:bg-fpt-navy hover:text-white hover:border-fpt-navy transition-all shadow-md sm:opacity-0 sm:group-hover:opacity-100"
            >
              <ChevronLeft className="w-6 h-6" />
            </button>
          )}

          <div className="w-full overflow-hidden px-1 py-4">
            <AnimatePresence mode="wait">
              <motion.div
                key={loading ? 'loading' : currentPage}
                initial={{ opacity: 0, x: 20 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: -20 }}
                transition={{ duration: 0.3, ease: 'easeInOut' }}
                className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 sm:gap-6"
              >
                {loading
                  ? Array.from({ length: 12 }).map((_, i) => (
                      <div key={i} className="bg-gray-100 border border-gray-100 rounded-2xl h-24 sm:h-28 lg:h-32 animate-pulse" />
                    ))
                  : currentPartners.map((partner) => (
                      <Link
                        to={`/partners/${partner.publicSlug ?? partner.partnerId}`}
                        key={partner.partnerId}
                        className="bg-white border border-gray-100 rounded-2xl h-24 sm:h-28 lg:h-32 shadow-sm hover:shadow-lg flex items-center justify-center p-4 hover:-translate-y-1 transition-all duration-300 group/item"
                      >
                        <PartnerLogo partner={partner} />
                      </Link>
                    ))}
              </motion.div>
            </AnimatePresence>
          </div>

          {totalPages > 1 && (
            <button
              onClick={nextPage}
              className="absolute -right-5 sm:-right-8 lg:-right-16 z-20 w-10 h-10 sm:w-12 sm:h-12 rounded-full border border-gray-200 bg-white flex items-center justify-center hover:bg-fpt-navy hover:text-white hover:border-fpt-navy transition-all shadow-md sm:opacity-0 sm:group-hover:opacity-100"
            >
              <ChevronRight className="w-6 h-6" />
            </button>
          )}
        </div>

        {totalPages > 1 && (
          <div className="flex justify-center gap-3 mt-8">
            {Array.from({ length: totalPages }).map((_, i) => (
              <button
                key={i}
                onClick={() => setCurrentPage(i)}
                className={`h-2.5 rounded-full transition-all duration-300 ${i === currentPage ? 'bg-fpt-orange w-8' : 'bg-gray-300 w-2.5 hover:bg-gray-400'}`}
              />
            ))}
          </div>
        )}
      </div>
    </section>
  );
}
