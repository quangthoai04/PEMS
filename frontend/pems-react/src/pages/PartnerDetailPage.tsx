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
  ArrowLeft, MapPin, ExternalLink, BookOpen, Home, Loader2, AlertTriangle,
} from 'lucide-react';
import { publicPartnersApi } from '../features/public-partners/api/publicPartnersApi';
import { usePublicPartnerImage } from '../features/public-partners/hooks/usePublicPartnerImage';
import type { PublicPartner } from '../features/public-partners/types/publicPartners.types';
import { getNameInitials } from '../shared/utils/nameInitials';

export function PartnerDetailPage() {
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
        else setError('Không thể tải thông tin đối tác. Vui lòng thử lại sau.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [id]);

  const coverUrl = usePublicPartnerImage(partner?.coverFileId);
  const logoUrl = usePublicPartnerImage(partner?.logoFileId);

  if (loading) {
    return (
      <div className="pt-24 pb-24 bg-[#f8fafc] min-h-screen flex items-center justify-center">
        <Loader2 className="w-8 h-8 text-[#004c91] animate-spin" />
      </div>
    );
  }

  if (notFound || !partner) {
    return (
      <div className="pt-24 pb-24 bg-[#f8fafc] min-h-screen flex items-center justify-center px-4">
        <div className="max-w-md w-full bg-white rounded-2xl border border-slate-100 shadow-xl py-14 px-6 text-center">
          <AlertTriangle className="w-10 h-10 text-amber-400 mx-auto mb-4" />
          <h3 className="text-lg font-bold text-slate-700 mb-2">
            Không tìm thấy đối tác hoặc đối tác chưa được công khai.
          </h3>
          <button
            onClick={() => navigate('/partners')}
            className="mt-4 px-5 py-2.5 bg-[#004c91] hover:bg-[#f37021] text-white text-sm font-bold rounded-lg transition-all shadow-md active:scale-95 cursor-pointer"
          >
            Quay lại danh sách đối tác
          </button>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="pt-24 pb-24 bg-[#f8fafc] min-h-screen flex items-center justify-center px-4">
        <div className="max-w-md w-full bg-white rounded-2xl border border-red-100 shadow-xl py-14 px-6 text-center">
          <AlertTriangle className="w-10 h-10 text-red-400 mx-auto mb-4" />
          <h3 className="text-lg font-bold text-slate-700 mb-2">{error}</h3>
          <button
            onClick={() => window.location.reload()}
            className="mt-4 px-5 py-2.5 bg-[#004c91] hover:bg-[#f37021] text-white text-sm font-bold rounded-lg transition-all shadow-md active:scale-95 cursor-pointer"
          >
            Thử lại
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="pt-24 pb-24 bg-[#f8fafc] min-h-screen">
      <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">

        {/* Breadcrumbs */}
        <div className="mb-6 flex flex-wrap items-center gap-2 text-sm font-semibold select-none text-slate-400 py-1">
          <button
            onClick={() => navigate('/')}
            className="hover:text-[#004c91] transition-colors flex items-center gap-1.5 cursor-pointer px-2 py-1 rounded-lg"
          >
            <Home className="w-4 h-4 text-slate-400" />
            <span>Trang chủ</span>
          </button>

          <span className="text-slate-300">/</span>

          <button
            onClick={() => navigate('/partners')}
            className="text-[#f37021] hover:text-orange-600 font-bold transition-colors px-2 py-1 rounded-lg cursor-pointer"
          >
            đối tác liên kết
          </button>

          <span className="text-slate-300">/</span>

          <span className="text-slate-600 font-bold truncate max-w-[200px] sm:max-w-none">
            {partner.name}
          </span>
        </div>

        {/* Back Button */}
        <button
          onClick={() => navigate('/partners')}
          className="mb-8 inline-flex items-center gap-2 px-4 py-2 bg-white hover:bg-slate-55 border border-slate-200 rounded-xl text-slate-500 font-bold text-sm shadow-sm transition-all cursor-pointer"
        >
          <ArrowLeft className="w-4 h-4 text-[#f37021]" />
          Quay lại danh sách đối tác
        </button>

        {/* Main Content Body Stack */}
        <div className="flex flex-col gap-6">

          {/* 1. Hero Cover Photo — real coverFileId if any, otherwise a branded gradient (never a
              stock photo, never broken). */}
          <motion.div
            initial={{ opacity: 0, y: 25 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.5, ease: 'easeOut' }}
            className="relative w-full h-[380px] sm:h-[480px] md:h-[550px] lg:h-[620px] rounded-2xl overflow-hidden shadow-md"
          >
            {coverUrl ? (
              <img
                src={coverUrl}
                alt={partner.name}
                className="w-full h-full object-cover select-none"
              />
            ) : (
              <div
                className="w-full h-full bg-gradient-to-br from-[#004c91] via-[#003a70] to-[#f37021]/60"
                style={{
                  backgroundImage: 'radial-gradient(rgba(255,255,255,0.08) 2px, transparent 2px)',
                  backgroundSize: '28px 28px',
                }}
              />
            )}
            {/* Perfect dark linear gradient cover for readable text */}
            <div className="absolute inset-0 bg-gradient-to-t from-slate-950/95 via-slate-900/40 to-transparent z-10" />
            <div className="absolute inset-x-0 bottom-0 h-1/2 bg-gradient-to-t from-slate-950/80 to-transparent z-15" />

            {/* In-Cover Bottom-Left Corner Branding elements */}
            <div className="absolute bottom-6 left-6 right-6 z-20 flex flex-col sm:flex-row items-center sm:items-end gap-4 sm:gap-6 text-center sm:text-left">

              {/* Partner Logo — real logoFileId if any, otherwise initials */}
              <div className="relative shrink-0 w-20 h-20 sm:w-24 sm:h-24 bg-white p-2.5 rounded-xl sm:rounded-2xl shadow-lg flex items-center justify-center border border-white/20 select-none overflow-hidden">
                {logoUrl ? (
                  <img
                    src={logoUrl}
                    alt={partner.name}
                    className="max-w-[95%] max-h-[95%] object-contain"
                  />
                ) : (
                  <span className="text-2xl font-black text-[#004c91]">{getNameInitials(partner.name)}</span>
                )}
              </div>

              {/* Text side beside logo */}
              <div className="flex-1 text-white">
                <h2 className="text-xl sm:text-2xl md:text-3xl font-black tracking-tight leading-tight line-clamp-2 drop-shadow-md">
                  {partner.name}
                </h2>

                {(partner.country || partner.city) && (
                  <div className="flex items-center justify-center sm:justify-start gap-1.5 mt-2.5 text-xs sm:text-sm font-bold text-orange-200">
                    <MapPin className="w-4 h-4 text-[#f37021] fill-orange-500/10" />
                    <span>{[partner.city, partner.country].filter(Boolean).join(', ')}</span>
                  </div>
                )}
              </div>

            </div>
          </motion.div>

          {/* 2. Giới thiệu chung Block */}
          <motion.div
            initial={{ opacity: 0, y: 15 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.4, delay: 0.1 }}
            className="bg-white rounded-2xl p-6 sm:p-8 border border-slate-100 shadow-[0_4px_20px_rgba(0,0,0,0.015)] relative overflow-hidden"
          >
            <div className="absolute top-0 left-0 w-full h-[3px] bg-gradient-to-r from-[#f37021] to-orange-400" />

            <div className="flex items-center gap-3 mb-4 select-none">
              <div className="w-9 h-9 rounded-xl bg-orange-100/50 flex items-center justify-center text-[#f37021]">
                <BookOpen className="w-4.5 h-4.5" />
              </div>
              <h3 className="text-xl font-extrabold text-[#004c91] tracking-tight">
                Giới thiệu chung
              </h3>
            </div>

            <p className="text-slate-600 text-base leading-relaxed font-medium">
              {partner.description || 'Đối tác chưa cập nhật mô tả.'}
            </p>
          </motion.div>

          {/* 3. Địa chỉ trụ sở — chỉ hiển thị khi có dữ liệu thật (không bịa). */}
          {partner.address && (
            <motion.div
              initial={{ opacity: 0, y: 15 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.4, delay: 0.15 }}
              className="bg-white rounded-2xl p-6 sm:p-8 border border-slate-100 shadow-[0_4px_20px_rgba(0,0,0,0.015)] relative overflow-hidden"
            >
              <div className="flex items-center gap-3 mb-4 select-none">
                <div className="w-9 h-9 rounded-xl bg-orange-100/40 flex items-center justify-center text-[#f37021]">
                  <MapPin className="w-5 h-5 text-[#f37021]" />
                </div>
                <h3 className="text-xl font-extrabold text-[#004c91] tracking-tight">
                  Địa chỉ trụ sở chính
                </h3>
              </div>

              <div className="p-5 bg-gradient-to-r from-orange-50/30 to-slate-50 rounded-xl border border-slate-100">
                <p className="font-bold text-slate-700 leading-relaxed text-base">
                  {partner.address}
                </p>
              </div>
            </motion.div>
          )}

          {/* 4. Ghé thăm website button CTA — chỉ hiện khi partner có website */}
          {partner.websiteUrl && (
            <motion.div
              initial={{ opacity: 0, y: 15 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.4, delay: 0.2 }}
              className="flex flex-col items-center mt-2 relative select-none w-full"
            >
              <motion.a
                href={partner.websiteUrl.startsWith('http') ? partner.websiteUrl : `https://${partner.websiteUrl}`}
                target="_blank"
                rel="noopener noreferrer"
                whileHover={{
                  scale: 1.02,
                  backgroundColor: '#e65c00',
                  boxShadow: '0 10px 25px rgba(243, 112, 33, 0.35)',
                }}
                whileTap={{ scale: 0.98 }}
                className="w-full py-4.5 bg-[#f37021] font-black text-white text-center rounded-2xl shadow-md transition-all duration-300 flex items-center justify-center gap-2 cursor-pointer text-base"
              >
                <span>Ghé thăm website đối tác</span>
                <ExternalLink className="w-5 h-5" />
              </motion.a>
              <p className="text-slate-400 text-[10px] font-black uppercase tracking-widest mt-2">
                Bảo mật và bảo hộ bởi tổ chức giáo dục FPT
              </p>
            </motion.div>
          )}

        </div>

      </div>
    </div>
  );
}
