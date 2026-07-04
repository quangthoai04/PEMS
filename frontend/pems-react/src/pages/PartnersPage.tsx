/**
 * Trang PartnersPage (Public)
 * Trang biểu thị danh mục lưới liên kết tổng hợp đối ngoại — dữ liệu thật từ
 * GET /api/public/partners (chỉ đối tác APPROVED + PUBLIC), không còn mock data.
 */

import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  ArrowRight,
  Search,
  Globe,
  MapPin,
  ChevronLeft,
  ChevronRight,
  Sparkles,
  Layers,
  ArrowUpRight,
  FilterX,
  RefreshCw,
  AlertTriangle,
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import GlobeComponent from '../components/partners/GlobeComponent';
import { VisitingFormPopup } from '../components/modals/VisitingFormPopup';
import { publicPartnersApi } from '../features/public-partners/api/publicPartnersApi';
import { usePublicPartnerImage } from '../features/public-partners/hooks/usePublicPartnerImage';
import { findMatchingCountryValue } from '../features/public-partners/utils/countryMatch';
import type { PublicPartner, PublicPartnerCountry } from '../features/public-partners/types/publicPartners.types';
import { getNameInitials } from '../shared/utils/nameInitials';

const ALL_COUNTRIES_LABEL = 'Tất cả quốc gia';

/** One partner card — a separate component so `usePublicPartnerImage` (a hook) can be called
 *  once per card instead of inside a `.map()` callback. */
function PublicPartnerCard({
  partner, index, onClick,
}: { partner: PublicPartner; index: number; onClick: () => void }) {
  const logoUrl = usePublicPartnerImage(partner.logoFileId);

  return (
    <motion.div
      initial={{ opacity: 0, y: 25, scale: 0.95 }}
      animate={{ opacity: 1, y: 0, scale: 1 }}
      exit={{ opacity: 0, scale: 0.95 }}
      transition={{ duration: 0.3, delay: (index % 4) * 0.05, ease: 'easeOut' }}
      onClick={onClick}
      className="group bg-white rounded-3xl p-6 flex flex-col items-center text-center shadow-[0_5px_20px_-3px_rgba(0,0,0,0.05)] hover:shadow-[0_20px_40px_-5px_rgba(0,76,145,0.12)] hover:-translate-y-2.5 transition-all duration-300 relative overflow-hidden cursor-pointer"
    >
      {/* Modern glowing accents in gradient matching FPT University */}
      <div className="absolute top-0 left-0 w-full h-[3px] bg-gradient-to-r from-[#004c91] via-orange-400 to-[#f37021] transform origin-left scale-x-0 group-hover:scale-x-100 transition-transform duration-500" />
      <div className="absolute inset-0 bg-gradient-to-b from-[#004c91]/2 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-300 pointer-events-none" />

      {/* 1. Logo / initials fallback */}
      <div className="w-full h-24 flex items-center justify-center p-2 transition-all duration-300 relative my-1">
        {logoUrl ? (
          <img
            src={logoUrl}
            alt={partner.name}
            className="max-w-[85%] max-h-[85%] object-contain scale-100 group-hover:scale-110 transition-transform duration-500"
          />
        ) : (
          <div className="w-14 h-14 bg-[#004c91]/5 text-[#004c91] font-black text-lg rounded-full flex items-center justify-center uppercase border border-[#004c91]/10 shadow-inner">
            {getNameInitials(partner.name)}
          </div>
        )}
      </div>

      {/* 2. Partner Name in center — default to brand navy blue #004c91 */}
      <div className="flex-grow flex flex-col justify-center my-3 w-full">
        <h3 className="text-[17px] font-black text-[#004c91] leading-snug group-hover:text-orange-600 transition-colors line-clamp-2 px-1">
          {partner.name}
        </h3>
      </div>

      {/* Thin decorative dotted divider line */}
      <div className="w-full border-t border-dashed border-slate-100 my-2" />

      {/* 3. Partner Country Badge */}
      <div className="w-full flex justify-center mt-2.5">
        <div className="inline-flex items-center gap-1.5 py-1.5 px-4 bg-[#004c91]/5 border border-[#004c91]/10 text-[#004c91] text-xs font-bold rounded-xl group-hover:bg-orange-50 group-hover:border-orange-200/50 group-hover:text-[#f37021] transition-all duration-300 shadow-sm shadow-[#004c91]/2">
          <MapPin className="w-3.5 h-3.5 text-[#f37021]" />
          <span>{partner.country || 'Chưa cập nhật'}</span>
        </div>
      </div>
    </motion.div>
  );
}

function PartnerCardSkeleton() {
  return (
    <div className="bg-white rounded-3xl p-6 flex flex-col items-center shadow-[0_5px_20px_-3px_rgba(0,0,0,0.05)] animate-pulse">
      <div className="w-14 h-14 rounded-full bg-slate-100 my-1" />
      <div className="h-4 w-3/4 bg-slate-100 rounded-full my-4" />
      <div className="w-full border-t border-dashed border-slate-100 my-2" />
      <div className="h-6 w-24 bg-slate-100 rounded-xl mt-2.5" />
    </div>
  );
}

export function PartnersPage() {
  const navigate = useNavigate();
  const [isVisitorFormOpen, setIsVisitorFormOpen] = useState(false);

  // Search / filter / pagination — all server-driven (GET /api/public/partners).
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [selectedCountry, setSelectedCountry] = useState(ALL_COUNTRIES_LABEL);
  const [currentPage, setCurrentPage] = useState(1);
  const itemsPerPage = 12;

  const [partners, setPartners] = useState<PublicPartner[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  // Country filter options — real distinct countries among APPROVED + PUBLIC partners
  // (GET /public/partners/countries), not inferred from a page of results.
  const [countryOptions, setCountryOptions] = useState<PublicPartnerCountry[]>([]);
  // Transient notice when GlobeComponent's pin click doesn't match any real partner country —
  // never silently filters by a bogus value.
  const [globeNotice, setGlobeNotice] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await publicPartnersApi.getPublicPartnerCountries();
        if (!cancelled) setCountryOptions(res);
      } catch {
        // Country list failing is non-fatal — dropdown just falls back to "Tất cả quốc gia" only.
        if (!cancelled) setCountryOptions([]);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  // Debounce the search box (400ms) before it drives a server request.
  useEffect(() => {
    const handle = setTimeout(() => setDebouncedSearch(searchQuery.trim()), 400);
    return () => clearTimeout(handle);
  }, [searchQuery]);

  // Reset to page 1 whenever the filters change.
  useEffect(() => {
    setCurrentPage(1);
  }, [debouncedSearch, selectedCountry]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    (async () => {
      try {
        const res = await publicPartnersApi.getPublicPartners({
          search: debouncedSearch || undefined,
          country: selectedCountry === ALL_COUNTRIES_LABEL ? undefined : selectedCountry,
          page: currentPage,
          pageSize: itemsPerPage,
        });
        if (cancelled) return;
        setPartners(res.items);
        setTotalCount(res.totalCount);
      } catch {
        if (!cancelled) {
          setError('Không thể tải danh sách đối tác. Vui lòng thử lại sau.');
          setPartners([]);
          setTotalCount(0);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [debouncedSearch, selectedCountry, currentPage, reloadToken]);

  const countryDropdownOptions = useMemo(
    () => [{ value: ALL_COUNTRIES_LABEL, label: ALL_COUNTRIES_LABEL, count: totalCount }, ...countryOptions],
    [countryOptions, totalCount],
  );
  const totalPages = Math.max(1, Math.ceil(totalCount / itemsPerPage));
  const hasActiveFilter = debouncedSearch !== '' || selectedCountry !== ALL_COUNTRIES_LABEL;

  // Auto-dismiss the "no match" globe notice.
  useEffect(() => {
    if (!globeNotice) return;
    const handle = setTimeout(() => setGlobeNotice(null), 5000);
    return () => clearTimeout(handle);
  }, [globeNotice]);

  const handleGlobeSelectCountry = (rawCountry: string) => {
    const matched = findMatchingCountryValue(rawCountry, countryOptions);
    if (!matched) {
      setGlobeNotice('Chưa có đối tác công khai tại quốc gia này.');
      return;
    }
    setGlobeNotice(null);
    setSelectedCountry(matched);
    setTimeout(() => {
      const tableElement = document.getElementById('partners-directory');
      if (tableElement) {
        tableElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }
    }, 80);
  };

  const paginate = (pageNumber: number) => {
    setCurrentPage(pageNumber);
    setTimeout(() => {
      const tableElement = document.getElementById('partners-directory');
      if (tableElement) {
        tableElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }
    }, 40);
  };

  const clearFilters = () => {
    setSearchQuery('');
    setSelectedCountry(ALL_COUNTRIES_LABEL);
  };

  return (
    <>
      <div className="pt-24 pb-20 bg-[#f8fafc] min-h-screen overflow-x-clip">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">

          {/* Top Hero and Globe Section */}
          <div className="flex flex-col lg:flex-row gap-12 lg:items-start items-center">

            {/* Left Content */}
            <div className="w-full lg:w-1/2 flex flex-col items-start relative z-10 lg:pt-10">
              {/* Tag */}
              <div className="flex items-center gap-4 mb-6">
                <div className="h-[2px] w-12 bg-[#f37021]"></div>
                <span className="text-[#f37021] font-bold tracking-widest uppercase text-xs">
                  FPT University
                </span>
                <div className="h-[2px] w-12 bg-[#f37021]"></div>
              </div>

              {/* Slogan */}
              <h1 className="text-5xl md:text-6xl lg:text-[76px] font-black leading-[1.1] mb-6">
                <span className="text-[#004c91] block mb-2 tracking-tight">Kết sức mạnh,</span>
                <span className="text-[#f37021] block tracking-tight">Nối tầm nhìn</span>
              </h1>

              {/* Subtitle */}
              <p className="text-gray-500 text-[14px] md:text-[15px] font-medium mb-8 max-w-[480px] leading-relaxed">
                Cộng hưởng giá trị tri thức toàn cầu — Bệ phóng cho những ý tưởng đổi mới và khởi nghiệp thành công.
              </p>

              {/* Buttons */}
              <div className="flex flex-col sm:flex-row gap-4 w-full sm:w-auto">
                <button
                  onClick={() => setIsVisitorFormOpen(true)}
                  className="flex justify-center items-center gap-2 px-8 py-3.5 bg-[#f37021] text-white font-bold rounded-lg shadow-[0_8px_25px_rgba(243,112,33,0.35)] hover:-translate-y-1 hover:shadow-[0_12px_30px_rgba(243,112,33,0.45)] transition-all duration-300 group text-[15px] cursor-pointer"
                >
                  Đăng ký ghé thăm
                  <ArrowRight className="w-4 h-4 group-hover:translate-x-1 transition-transform" />
                </button>

                <button
                  onClick={() => navigate('/visit-fptu')}
                  className="flex justify-center items-center px-8 py-3.5 bg-white text-gray-600 font-bold border-[1.5px] border-gray-200 rounded-lg hover:text-white hover:bg-[#004c91] hover:border-[#004c91] hover:shadow-lg transition-all duration-300 text-[15px] cursor-pointer"
                >
                  Khám phá trực tuyến
                </button>
              </div>

              {/* Statistics */}
              <div className="w-full mt-12 pt-8 border-t border-gray-200/80">
                <div className="grid grid-cols-1 md:grid-cols-3 gap-6 text-center divide-y md:divide-y-0 md:divide-x divide-gray-200/80">
                  {/* Stat 1 */}
                  <div className="flex flex-col items-center px-2 py-4 md:py-0">
                    <span className="text-[40px] leading-tight font-black text-[#f37021] mb-2 tracking-tight">180+</span>
                    <span className="text-gray-500 text-sm leading-relaxed max-w-[200px]">
                      <strong>Đối tác chiến lược:</strong> Mạng lưới các tập đoàn lớn (Nvidia, Microsoft, AWS...) khẳng định uy tín toàn cầu.
                    </span>
                  </div>
                  {/* Stat 2 */}
                  <div className="flex flex-col items-center px-2 py-4 md:py-0">
                    <span className="text-[40px] leading-tight font-black text-[#f37021] mb-2 tracking-tight">200+</span>
                    <span className="text-gray-500 text-sm leading-relaxed max-w-[200px]">
                      <strong>Tổ chức giáo dục:</strong> Mở rộng hợp tác, mang đến môi trường học thuật và cơ hội trao đổi quốc tế đa dạng.
                    </span>
                  </div>
                  {/* Stat 3 */}
                  <div className="flex flex-col items-center px-2 py-4 md:py-0">
                    <span className="text-[40px] leading-tight font-black text-[#f37021] mb-2 tracking-tight">40+</span>
                    <span className="text-gray-500 text-sm leading-relaxed max-w-[200px]">
                      <strong>Quốc gia & Vùng lãnh thổ:</strong> Dấu ấn toàn cầu, đón hơn 2.000 sinh viên quốc tế đến Việt Nam mỗi năm.
                    </span>
                  </div>
                </div>
              </div>
            </div>

            {/* Right Globe */}
            <div className="w-full lg:w-1/2 h-[450px] sm:h-[550px] lg:h-[700px] flex items-start justify-center relative mt-10 lg:mt-0 lg:-mr-32">
              {/* Soft glow behind the globe */}
              <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[400px] h-[400px] bg-sky-200/30 blur-[100px] rounded-full z-0"></div>
              <GlobeComponent onSelectCountry={handleGlobeSelectCountry} />
              {globeNotice && (
                <div className="absolute bottom-4 left-1/2 -translate-x-1/2 z-20 bg-slate-900/90 text-white text-xs font-medium px-4 py-2.5 rounded-xl shadow-lg backdrop-blur-sm whitespace-nowrap">
                  {globeNotice}
                </div>
              )}
            </div>

          </div>

          {/* Interactive Partners Directory Section */}
          <div id="partners-directory" className="mt-20 pt-16 border-t border-slate-200 relative">
            <div className="absolute top-0 left-1/2 -translate-x-1/2 w-[40%] h-[150px] bg-gradient-to-b from-[#004c91]/5 to-transparent blur-[80px] rounded-full pointer-events-none"></div>

            {/* Header of Section */}
            <div className="text-center max-w-3xl mx-auto mb-12">
              <div className="inline-flex items-center gap-2 px-3 py-1.5 bg-sky-50 text-[#004c91] border border-sky-100 rounded-full text-xs font-bold uppercase tracking-wider mb-4">
                <Sparkles className="w-4 h-4 text-[#f37021]" />
                Mạng Lưới Quốc Tế
              </div>
              <h2 className="text-3xl md:text-4xl font-black text-slate-800 tracking-tight leading-tight">
                Danh sách <span className="text-transparent bg-clip-text bg-gradient-to-r from-[#004c91] to-[#f37021]">đối tác chiến lược & liên kết</span>
              </h2>
              <p className="text-slate-500 text-sm md:text-base mt-2">
                Hỗ trợ tra cứu nhanh chóng cơ sở liên kết, thông tin lĩnh vực hợp tác học thuật của trường Đại học FPT với các tổ chức hàng đầu thế giới.
              </p>
            </div>

            {/* SEARCH AND FILTERS GRID PANEL */}
            <div className="bg-white border border-slate-100 rounded-2xl shadow-lg p-6 md:p-8 mb-8 relative z-20">
              <div className="grid grid-cols-1 md:grid-cols-12 gap-6 items-end">

                {/* Search Bar */}
                <div className="md:col-span-8 flex flex-col gap-2">
                  <label htmlFor="partner-name-search" className="text-xs font-bold text-slate-700 uppercase tracking-widest pl-1">
                    Tên đối tác
                  </label>
                  <div className="relative group/search">
                    <div className="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none">
                      <Search className="w-5 h-5 text-slate-400 group-focus-within/search:text-[#f37021] transition-colors" />
                    </div>
                    <input
                      id="partner-name-search"
                      type="text"
                      placeholder="Nhập tên đối tác (VD: Nvidia, Swinburne, HELP...)"
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      className="w-full pl-11 pr-10 py-3.5 bg-slate-50/50 hover:bg-slate-50 border border-slate-200 hover:border-slate-300 focus:border-[#f37021] focus:bg-white text-slate-800 text-sm font-medium rounded-xl outline-none transition-all focus:ring-4 focus:ring-orange-100 shadow-inner"
                    />
                    {searchQuery && (
                      <button
                        onClick={() => setSearchQuery('')}
                        className="absolute inset-y-0 right-0 pr-3 flex items-center text-slate-400 hover:text-slate-600 focus:outline-none text-xs font-bold cursor-pointer"
                      >
                        Xóa
                      </button>
                    )}
                  </div>
                </div>

                {/* Dropdownlist for country filter */}
                <div className="md:col-span-4 flex flex-col gap-2">
                  <label htmlFor="country-filter" className="text-xs font-bold text-slate-700 uppercase tracking-widest pl-1">
                    Quốc gia
                  </label>
                  <div className="relative">
                    <div className="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none">
                      <Globe className="w-5 h-5 text-slate-400" />
                    </div>
                    <select
                      id="country-filter"
                      value={selectedCountry}
                      onChange={(e) => setSelectedCountry(e.target.value)}
                      className="w-full pl-11 pr-10 py-3.5 bg-slate-50/50 border border-slate-200 hover:border-slate-300 focus:border-[#f37021] focus:bg-white text-slate-800 text-sm font-semibold rounded-xl outline-none transition-all cursor-pointer focus:ring-4 focus:ring-orange-100 appearance-none shadow-inner"
                    >
                      {countryDropdownOptions.map((country) => (
                        <option key={country.value} value={country.value} className="font-medium text-slate-800 py-2">
                          {country.value === ALL_COUNTRIES_LABEL ? country.label : `${country.label} (${country.count})`}
                        </option>
                      ))}
                    </select>
                    {/* Visual caret down decoration */}
                    <div className="absolute inset-y-0 right-0 pr-4 flex items-center pointer-events-none text-slate-400">
                      <ChevronRight className="w-4 h-4 rotate-90" />
                    </div>
                  </div>
                </div>

              </div>

              {/* Filter metrics status */}
              {hasActiveFilter && (
                <div className="mt-5 flex items-center justify-between flex-wrap gap-3 bg-orange-50/40 border border-orange-100/50 rounded-xl p-3.5">
                  <div className="flex items-center gap-2 text-xs font-medium text-slate-700">
                    <Layers className="w-4 h-4 text-[#f37021]" />
                    Tìm thấy <span className="font-bold text-[#f37021]">{totalCount}</span> kết quả phù hợp cho bộ lọc hiện tại.
                  </div>
                  <button
                    onClick={clearFilters}
                    className="flex items-center gap-1.5 text-xs font-bold text-[#004c91] hover:text-[#f37021] transition-colors focus:outline-none"
                  >
                    <FilterX className="w-4 h-4" /> Làm mới bộ lọc
                  </button>
                </div>
              )}
            </div>

            {/* MAIN PARTNER DIRECTORY / GRID BENTO LAYOUT */}
            <div className="mb-12 relative z-10">
              {error ? (
                <div className="bg-white rounded-2xl border border-red-100 shadow-xl py-16 px-6 text-center">
                  <div className="max-w-md mx-auto flex flex-col items-center">
                    <AlertTriangle className="w-10 h-10 text-red-400 mb-4" />
                    <h3 className="text-base font-bold text-slate-700 mb-1">{error}</h3>
                    <button
                      onClick={() => setReloadToken((t) => t + 1)}
                      className="mt-4 px-5 py-2.5 bg-[#004c91] hover:bg-[#f37021] text-white text-xs font-bold rounded-lg transition-all shadow-md active:scale-95 cursor-pointer"
                    >
                      Thử lại
                    </button>
                  </div>
                </div>
              ) : loading ? (
                <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6">
                  {Array.from({ length: itemsPerPage }).map((_, i) => <PartnerCardSkeleton key={i} />)}
                </div>
              ) : (
                <AnimatePresence mode="popLayout">
                  {partners.length > 0 ? (
                    <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6">
                      {partners.map((partner, index) => (
                        <PublicPartnerCard
                          key={partner.partnerId}
                          partner={partner}
                          index={index}
                          onClick={() => navigate(`/partners/${partner.partnerId}`)}
                        />
                      ))}
                    </div>
                  ) : (
                    <div className="bg-white rounded-2xl border border-slate-100 shadow-xl py-16 px-6 text-center">
                      <div className="max-w-md mx-auto flex flex-col items-center">
                        <div className="w-16 h-16 rounded-full bg-slate-50 flex items-center justify-center text-slate-355 mb-4">
                          <RefreshCw className="w-8 h-8" />
                        </div>
                        <h3 className="text-base font-bold text-slate-700 mb-1">
                          {hasActiveFilter ? 'Không tìm thấy đối tác liên kết' : 'Chưa có đối tác công khai'}
                        </h3>
                        <p className="text-xs text-slate-400 max-w-sm mb-4">
                          {hasActiveFilter
                            ? `Không tìm thấy kết quả phù hợp với từ khóa "${debouncedSearch}" trong danh mục quốc gia "${selectedCountry}".`
                            : 'Danh mục đối tác công khai hiện chưa có dữ liệu. Vui lòng quay lại sau.'}
                        </p>
                        {hasActiveFilter && (
                          <button
                            onClick={clearFilters}
                            className="px-5 py-2.5 bg-[#004c91] hover:bg-[#f37021] text-white text-xs font-bold rounded-lg transition-all shadow-md active:scale-95 cursor-pointer"
                          >
                            Thử lại tất cả đối tác
                          </button>
                        )}
                      </div>
                    </div>
                  )}
                </AnimatePresence>
              )}

              {/* PAGINATION PANEL FOR MODERN CARD GRID */}
              {!loading && !error && partners.length > 0 && totalPages >= 1 && (
                <div className="mt-12 flex justify-center w-full">

                  {/* Navigation key buttons */}
                  <div className="flex items-center gap-1.5 justify-center">

                    {/* Previous Button */}
                    <button
                      type="button"
                      onClick={(e) => {
                        e.preventDefault();
                        e.currentTarget.blur();
                        if (currentPage > 1) {
                          paginate(currentPage - 1);
                        }
                      }}
                      disabled={currentPage === 1}
                      className={`w-9 h-9 rounded-xl border flex items-center justify-center transition-all ${
                        currentPage === 1
                          ? 'bg-slate-50 border-slate-100 text-slate-300 cursor-not-allowed'
                          : 'bg-white border-slate-200 text-slate-600 hover:bg-[#004c91] hover:text-white hover:border-[#004c91] cursor-pointer shadow-sm hover:shadow'
                      }`}
                      title="Trang trước"
                    >
                      <ChevronLeft className="w-4 h-4" />
                    </button>

                    {/* Numeric buttons generator */}
                    {Array.from({ length: totalPages }).map((_, i) => {
                      const pageNum = i + 1;
                      const isActive = pageNum === currentPage;

                      return (
                        <button
                          key={pageNum}
                          type="button"
                          onClick={(e) => {
                            e.preventDefault();
                            e.currentTarget.blur();
                            paginate(pageNum);
                          }}
                          className={`w-9 h-9 rounded-xl font-bold text-xs transition-all flex items-center justify-center border cursor-pointer shadow-sm hover:shadow ${
                            isActive
                              ? 'bg-[#f37021] border-[#f37021] text-white shadow-md shadow-orange-500/10 scale-105'
                              : 'bg-white border-slate-200 text-slate-600 hover:bg-[#004c91]/10 hover:border-[#004c91]/20 hover:text-[#004c91]'
                          }`}
                        >
                          {pageNum}
                        </button>
                      );
                    })}

                    {/* Next Button */}
                    <button
                      type="button"
                      onClick={(e) => {
                        e.preventDefault();
                        e.currentTarget.blur();
                        if (currentPage < totalPages) {
                          paginate(currentPage + 1);
                        }
                      }}
                      disabled={currentPage === totalPages}
                      className={`w-9 h-9 rounded-xl border flex items-center justify-center transition-all ${
                        currentPage === totalPages
                          ? 'bg-slate-50 border-slate-100 text-slate-300 cursor-not-allowed'
                          : 'bg-white border-slate-200 text-slate-600 hover:bg-[#004c91] hover:text-white hover:border-[#004c91] cursor-pointer shadow-sm hover:shadow'
                      }`}
                      title="Trang tiếp"
                    >
                      <ChevronRight className="w-4 h-4" />
                    </button>

                  </div>

                </div>
              )}
            </div>

            {/* Quick Strategic Footer Call to Action Banner */}
            <div className="relative rounded-3xl overflow-hidden bg-gradient-to-br from-sky-50/50 via-white to-orange-50/30 p-8 md:p-12 shadow-[0_20px_50px_-12px_rgba(0,76,145,0.08)] hover:shadow-[0_30px_70px_-10px_rgba(243,112,33,0.15)] border border-slate-100 hover:border-orange-200/30 hover:-translate-y-1.5 transition-all duration-500 group">

              {/* Dual custom gradient washes (blue left, orange right) */}
              <div className="absolute top-0 left-0 bottom-0 w-1/2 bg-gradient-to-r from-[#004c91]/5 via-sky-500/1 to-transparent pointer-events-none rounded-l-3xl transition-opacity duration-700" />
              <div className="absolute top-0 right-0 bottom-0 w-1/2 bg-gradient-to-l from-[#f37021]/8 via-orange-400/2 to-transparent pointer-events-none rounded-r-3xl transition-opacity duration-700" />

              {/* Elevated animated glowing orbs - Orange on right, Blue on left */}
              <div className="absolute -top-[20%] -right-[10%] w-[420px] h-[420px] bg-gradient-to-br from-[#f37021]/15 to-orange-300/5 rounded-full blur-[90px] pointer-events-none group-hover:scale-115 transition-transform duration-1000" />
              <div className="absolute -bottom-[20%] -left-[10%] w-[380px] h-[380px] bg-gradient-to-tr from-[#004c91]/15 to-sky-400/5 rounded-full blur-[90px] pointer-events-none group-hover:scale-115 transition-transform duration-1000" />

              <div className="relative z-10 grid grid-cols-1 lg:grid-cols-12 gap-8 items-center">
                {/* Left side info column */}
                <div className="lg:col-span-7 space-y-5">
                  <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-[#004c91]/5 border border-[#004c91]/10 text-[#004c91] text-xs font-black uppercase tracking-wider">
                    <Globe className="w-3.5 h-3.5 text-[#f37021] animate-spin" style={{ animationDuration: '20s' }} />
                    <span>Mạng Lưới Toàn Cầu</span>
                  </div>

                  <h3 className="text-3xl md:text-4xl font-black tracking-tight leading-tight">
                    <span className="text-[#004c91]">Bạn mong muốn cùng xây dựng</span> <br className="hidden sm:inline" />
                    <span className="text-transparent bg-clip-text bg-gradient-to-r from-[#004c91] to-[#f37021] font-extrabold">
                      tương lai giáo dục?
                    </span>
                  </h3>

                  <p className="text-slate-600 text-[15px] font-medium leading-relaxed max-w-xl">
                    Đại học FPT liên tục đồng hành cùng đối tác chiến lược quốc tế, kiến tạo môi trường học tập không giới hạn cho sinh viên toàn cầu. Đăng ký để thảo luận chương trình liên kết đào tạo, chuyển giao học thuật ngay hôm nay.
                  </p>

                  {/* Trust markers */}
                  <div className="pt-2 flex flex-wrap gap-5 text-xs text-slate-500 font-bold">
                    <div className="flex items-center gap-2">
                      <span className="w-4 h-4 rounded-full bg-emerald-50 text-emerald-600 flex items-center justify-center font-black">✓</span>
                      Hỗ trợ 24/7
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="w-4 h-4 rounded-full bg-emerald-50 text-emerald-600 flex items-center justify-center font-black">✓</span>
                      Quy trình tinh gọn
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="w-4 h-4 rounded-full bg-emerald-50 text-emerald-600 flex items-center justify-center font-black">✓</span>
                      Hơn 40 quốc gia
                    </div>
                  </div>
                </div>

                {/* Right side contact CTA & stats */}
                <div className="lg:col-span-5 flex flex-col justify-center items-center lg:items-end gap-6">
                  {/* Micro dashboard layout inside cards */}
                  <div className="grid grid-cols-2 gap-4 w-full max-w-sm">
                    <div className="bg-white/70 backdrop-blur-sm p-4 rounded-2xl border border-slate-100 shadow-sm text-center">
                      <div className="text-2xl font-black text-[#004c91]">40+</div>
                      <div className="text-[10px] text-slate-400 font-bold uppercase tracking-wider">Quốc Gia</div>
                    </div>
                    <div className="bg-white/70 backdrop-blur-sm p-4 rounded-2xl border border-slate-100 shadow-sm text-center">
                      <div className="text-2xl font-black text-[#f37021]">500+</div>
                      <div className="text-[10px] text-slate-400 font-bold uppercase tracking-wider">Đối Tác Quốc Tế</div>
                    </div>
                  </div>

                  {/* Main Call to Action button */}
                  <div className="w-full max-w-sm text-center lg:text-right">
                    <button
                      onClick={() => setIsVisitorFormOpen(true)}
                      className="w-full px-6 py-4 bg-gradient-to-r from-[#004c91] to-[#0461b5] hover:from-[#f37021] hover:to-orange-600 font-extrabold text-sm rounded-2xl text-center text-white shadow-lg shadow-[#004c91]/25 hover:shadow-orange-500/25 transition-all duration-300 flex items-center justify-center gap-2 hover:-translate-y-1 active:translate-y-0 cursor-pointer"
                    >
                      <span>Gửi yêu cầu hợp tác</span>
                      <ArrowUpRight className="w-4 h-4" />
                    </button>
                  </div>
                </div>
              </div>
            </div>

          </div>

        </div>
      </div>
      <VisitingFormPopup isOpen={isVisitorFormOpen} onClose={() => setIsVisitorFormOpen(false)} />
    </>
  );
}
