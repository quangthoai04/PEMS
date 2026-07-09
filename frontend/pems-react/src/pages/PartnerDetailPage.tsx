/**
 * Trang PartnerDetailPage (Public)
 * Trang công khai hiển thị chi tiết một đối tác — dữ liệu thật từ
 * GET /api/public/partners/{idOrSlug} (chỉ đối tác APPROVED + PUBLIC). Không còn hard-code một
 * đối tác cố định: mỗi id/slug trên route hiển thị đúng đối tác đó.
 */

import React, { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { motion } from 'motion/react';
import {
  ArrowLeft, MapPin, ExternalLink, BookOpen, Home, Loader2, AlertTriangle, Building2, Globe2,
} from 'lucide-react';
import { publicPartnersApi } from '../features/public-partners/api/publicPartnersApi';
import { usePublicPartnerImage } from '../features/public-partners/hooks/usePublicPartnerImage';
import type { PublicPartner } from '../features/public-partners/types/publicPartners.types';
import { getNameInitials } from '../shared/utils/nameInitials';
import { useTranslation } from 'react-i18next';

export function PartnerDetailPage() {
  const { t } = useTranslation(['partners']);
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();

  const [partner, setPartner] = useState<PublicPartner | null>(null);
  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    setNotFound(false);
    (async () => {
      try {
        const data = await publicPartnersApi.getPublicPartnerDetail(id);
        if (!cancelled) setPartner(data);
      } catch (e: any) {
        if (cancelled) return;
        if (e?.response?.status === 404) setNotFound(true);
        else setError(t('partners:detail.loadError'));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [id]);

  useEffect(() => {
    window.scrollTo(0, 0);
  }, [id]);

  const coverUrl = usePublicPartnerImage(partner?.coverFileId);
  const logoUrl = usePublicPartnerImage(partner?.logoFileId);

  if (loading) {
    return (
      <div className="pt-24 pb-24 bg-white min-h-screen flex items-center justify-center">
        <Loader2 className="w-8 h-8 text-[#004c91] animate-spin" />
      </div>
    );
  }

  if (notFound || !partner) {
    return (
      <div className="pt-24 pb-24 bg-white min-h-screen flex items-center justify-center px-4">
        <div className="max-w-md w-full bg-white rounded-2xl border border-slate-200 py-14 px-6 text-center">
          <AlertTriangle className="w-10 h-10 text-amber-400 mx-auto mb-4" />
          <h3 className="text-lg font-bold text-slate-700 mb-2">
            {t('partners:detail.notFound')}
          </h3>
          <button
            onClick={() => navigate('/partners')}
            className="mt-4 px-5 py-2.5 bg-[#004c91] hover:bg-[#003b70] text-white text-sm font-bold rounded-xl transition-colors"
          >
            {t('partners:detail.backToList')}
          </button>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="pt-24 pb-24 bg-white min-h-screen flex items-center justify-center px-4">
        <div className="max-w-md w-full bg-white rounded-2xl border border-red-100 py-14 px-6 text-center">
          <AlertTriangle className="w-10 h-10 text-red-400 mx-auto mb-4" />
          <h3 className="text-lg font-bold text-slate-700 mb-2">{error}</h3>
          <button
            onClick={() => window.location.reload()}
            className="mt-4 px-5 py-2.5 bg-[#004c91] hover:bg-[#003b70] text-white text-sm font-bold rounded-xl transition-colors"
          >
            {t('partners:list.retry')}
          </button>
        </div>
      </div>
    );
  }

  const location = [partner.city, partner.country].filter(Boolean).join(', ');
  const websiteHref = partner.websiteUrl
    ? (partner.websiteUrl.startsWith('http') ? partner.websiteUrl : `https://${partner.websiteUrl}`)
    : null;

  return (
    <div className="pt-20 pb-16 bg-white min-h-screen">
      <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8">

        {/* Breadcrumb */}
        <div className="mb-4 flex flex-wrap items-center gap-2 text-sm font-semibold text-slate-400 py-1">
          <button onClick={() => navigate('/')} className="hover:text-[#004c91] transition-colors flex items-center gap-1.5">
            <Home className="w-4 h-4" /> {t('partners:detail.home')}
          </button>
          <span className="text-slate-300">/</span>
          <button onClick={() => navigate('/partners')} className="hover:text-[#004c91] transition-colors">
            {t('partners:detail.partners')}
          </button>
          <span className="text-slate-300">/</span>
          <span className="text-slate-600 font-bold truncate max-w-[200px] sm:max-w-none">{partner.name}</span>
        </div>

        {/* Back button */}
        <button
          onClick={() => navigate('/partners')}
          className="mb-6 inline-flex items-center gap-2 px-4 py-2 bg-white hover:bg-slate-50 border border-slate-200 rounded-xl text-slate-500 font-bold text-sm transition-colors"
        >
          <ArrowLeft className="w-4 h-4" /> {t('partners:detail.backToList')}
        </button>

        {/* Two-column layout */}
        <div className="flex flex-col lg:flex-row gap-8">

          {/* LEFT: Profile panel — sticky on desktop */}
          <motion.aside
            initial={{ opacity: 0, x: -16 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ duration: 0.4, ease: 'easeOut' }}
            className="lg:w-[320px] shrink-0"
          >
            <div className="lg:sticky lg:top-28 bg-white rounded-2xl border border-slate-200 overflow-hidden">
              {/* Cover image — real coverFileId if any, otherwise a branded gradient */}
              <div className="relative w-full h-40 sm:h-48">
                {coverUrl ? (
                  <img src={coverUrl} alt={partner.name} className="w-full h-full object-cover" />
                ) : (
                  <div className="w-full h-full bg-gradient-to-br from-[#004c91] to-[#f37021]/60" />
                )}
              </div>

              <div className="p-6 -mt-10 relative">
                {/* Logo card */}
                <div className="w-20 h-20 bg-white rounded-2xl border border-slate-200 shadow-sm flex items-center justify-center overflow-hidden mb-4">
                  {logoUrl ? (
                    <img src={logoUrl} alt={partner.name} className="max-w-[80%] max-h-[80%] object-contain" />
                  ) : (
                    <span className="text-xl font-black text-[#004c91]">{getNameInitials(partner.name)}</span>
                  )}
                </div>

                <h1 className="text-xl font-bold text-slate-900 leading-tight mb-1.5">{partner.name}</h1>
                {partner.shortName && (
                  <p className="text-sm text-slate-400 font-medium mb-3">{partner.shortName}</p>
                )}

                {location && (
                  <div className="flex items-center gap-1.5 text-slate-500 text-sm mb-2">
                    <MapPin className="w-4 h-4 text-[#f37021] shrink-0" />
                    <span>{location}</span>
                  </div>
                )}

                {partner.partnerType && (
                  <div className="flex items-center gap-1.5 text-slate-500 text-sm">
                    <Building2 className="w-4 h-4 text-[#004c91] shrink-0" />
                    <span className="px-2 py-0.5 rounded-full bg-[#004c91]/5 text-[#004c91] text-xs font-bold uppercase tracking-wide">
                      {partner.partnerType}
                    </span>
                  </div>
                )}

                {websiteHref && (
                  <a
                    href={websiteHref}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="mt-6 w-full inline-flex items-center justify-center gap-2 px-5 py-3 bg-[#f37021] hover:bg-orange-600 text-white font-bold text-sm rounded-xl transition-colors"
                  >
                    {t('partners:detail.visitWebsite')} <ExternalLink className="w-4 h-4" />
                  </a>
                )}
              </div>
            </div>
          </motion.aside>

          {/* RIGHT: Detail panel */}
          <div className="flex-1 min-w-0 flex flex-col gap-6">
            {/* Giới thiệu chung */}
            <motion.div
              initial={{ opacity: 0, y: 15 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.4, delay: 0.05 }}
              className="bg-white rounded-2xl p-6 sm:p-8 border border-slate-200"
            >
              <div className="flex items-center gap-3 mb-4">
                <div className="w-9 h-9 rounded-xl bg-orange-50 flex items-center justify-center text-[#f37021]">
                  <BookOpen className="w-4.5 h-4.5" />
                </div>
                <h2 className="text-lg font-bold text-[#004c91]">{t('partners:detail.overview')}</h2>
              </div>
              <p className="text-slate-600 text-base leading-relaxed">
                {partner.description || t('partners:detail.noDescription')}
              </p>
            </motion.div>

            {/* Thông tin tổ chức */}
            <motion.div
              initial={{ opacity: 0, y: 15 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.4, delay: 0.1 }}
              className="bg-white rounded-2xl p-6 sm:p-8 border border-slate-200"
            >
              <div className="flex items-center gap-3 mb-4">
                <div className="w-9 h-9 rounded-xl bg-sky-50 flex items-center justify-center text-[#004c91]">
                  <Globe2 className="w-4.5 h-4.5" />
                </div>
                <h2 className="text-lg font-bold text-[#004c91]">{t('partners:detail.orgInfo')}</h2>
              </div>
              <dl className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                {partner.country && (
                  <div>
                    <dt className="text-xs font-bold text-slate-400 uppercase tracking-wide mb-1">{t('partners:detail.country')}</dt>
                    <dd className="text-slate-700 font-semibold">{partner.country}</dd>
                  </div>
                )}
                {partner.city && (
                  <div>
                    <dt className="text-xs font-bold text-slate-400 uppercase tracking-wide mb-1">{t('partners:detail.city')}</dt>
                    <dd className="text-slate-700 font-semibold">{partner.city}</dd>
                  </div>
                )}
                {partner.address && (
                  <div className="sm:col-span-2">
                    <dt className="text-xs font-bold text-slate-400 uppercase tracking-wide mb-1">{t('partners:detail.address')}</dt>
                    <dd className="text-slate-700 font-semibold leading-relaxed">{partner.address}</dd>
                  </div>
                )}
                {!partner.country && !partner.city && !partner.address && (
                  <p className="text-sm text-slate-400 sm:col-span-2">{t('partners:detail.noLocation')}</p>
                )}
              </dl>
            </motion.div>
          </div>
        </div>
      </div>
    </div>
  );
}
