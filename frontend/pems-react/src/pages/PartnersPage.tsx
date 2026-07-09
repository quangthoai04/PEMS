/**
 * Trang PartnersPage (Public)
 * FPTU Partnership Directory — dữ liệu thật từ GET /api/public/partners (chỉ đối tác
 * APPROVED + PUBLIC), không dùng mock data, không dùng globe/map.
 */

import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  ArrowRight,
  Search,
  Globe,
  MapPin,
  ChevronLeft,
  ChevronRight,
  RefreshCw,
  AlertTriangle,
  Building2,
  Mail,
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import 'flag-icons/css/flag-icons.min.css';
import { VisitingFormPopup } from '../components/modals/VisitingFormPopup';
import { publicPartnersApi } from '../features/public-partners/api/publicPartnersApi';
import { usePublicPartnerImage } from '../features/public-partners/hooks/usePublicPartnerImage';
import { findMatchingCountryValue } from '../features/public-partners/utils/countryMatch';
import { getCountryIsoCode } from '../features/public-partners/utils/countryFlag';
import type {
  PublicPartner,
  PublicPartnerCountry,
  PublicPartnerType,
  PublicPartnerSort,
} from '../features/public-partners/types/publicPartners.types';
import { getNameInitials } from '../shared/utils/nameInitials';
import { useTranslation, Trans } from 'react-i18next';

const PAGE_SIZE = 12;

// Will move labels and sort options inside component to use translations

/* ───────────────────────── Partner Card ───────────────────────── */

function PublicPartnerCard({
  partner, index, onClick,
}: { partner: PublicPartner; index: number; onClick: () => void; key?: React.Key }) {
  const { t } = useTranslation(['partners']);
  const logoUrl = usePublicPartnerImage(partner.logoFileId);
  const location = [partner.city, partner.country].filter(Boolean).join(', ');

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.35, delay: (index % PAGE_SIZE) * 0.04, ease: 'easeOut' }}
      onClick={onClick}
      className="group bg-white rounded-2xl p-6 flex flex-col border border-slate-200 hover:border-sky-200 shadow-sm hover:shadow-md hover:-translate-y-1 transition-all duration-300 cursor-pointer"
    >
      <div className="flex items-start gap-4 mb-4">
        <div className="w-14 h-14 shrink-0 rounded-xl bg-slate-50 border border-slate-100 flex items-center justify-center overflow-hidden">
          {logoUrl ? (
            <img
              src={logoUrl}
              alt={partner.name}
              className="max-w-[80%] max-h-[80%] object-contain transition-transform duration-500 group-hover:scale-105"
            />
          ) : (
            <span className="text-base font-black text-[#004c91]">{getNameInitials(partner.name)}</span>
          )}
        </div>
        <div className="flex-1 min-w-0">
          <h3 className="text-[15px] font-bold text-slate-900 leading-snug line-clamp-2 group-hover:text-[#004c91] transition-colors">
            {partner.name}
          </h3>
          {location && (
            <div className="mt-1.5 flex items-center gap-1 text-slate-500 text-xs">
              <MapPin className="w-3.5 h-3.5 text-[#f37021] shrink-0" />
              <span className="truncate">{location}</span>
            </div>
          )}
        </div>
      </div>

      {partner.partnerType && (
        <span className="self-start mb-3 px-2.5 py-1 rounded-full bg-[#004c91]/5 text-[#004c91] text-[11px] font-bold uppercase tracking-wide">
          {partner.partnerType}
        </span>
      )}

      {partner.description && (
        <p className="text-sm text-slate-600 leading-relaxed line-clamp-2 mb-4 flex-1">
          {partner.description}
        </p>
      )}

      <span className="mt-auto inline-flex items-center gap-1.5 text-[#004c91] font-bold text-sm group-hover:gap-2.5 transition-all">
        {t('partners:list.viewProfile')} <ArrowRight className="w-3.5 h-3.5" />
      </span>
    </motion.div>
  );
}

function PartnerCardSkeleton() {
  return (
    <div className="bg-white rounded-2xl p-6 border border-slate-200 animate-pulse">
      <div className="flex items-start gap-4 mb-4">
        <div className="w-14 h-14 rounded-xl bg-slate-100 skeleton-shimmer" />
        <div className="flex-1 space-y-2 pt-1">
          <div className="h-4 bg-slate-100 rounded w-full skeleton-shimmer" />
          <div className="h-3 bg-slate-100 rounded w-2/3 skeleton-shimmer" />
        </div>
      </div>
      <div className="h-5 w-24 bg-slate-100 rounded-full mb-3 skeleton-shimmer" />
      <div className="h-3 bg-slate-100 rounded w-full mb-1.5 skeleton-shimmer" />
      <div className="h-3 bg-slate-100 rounded w-3/4 skeleton-shimmer" />
    </div>
  );
}

/* ───────────────────────── Hero showcase — country flags of partnered nations ───────────────────────── */

const FLAGS_PER_PAGE = 9;

function CountryFlagCard({ country, delay }: { country: PublicPartnerCountry; delay: number; key?: React.Key }) {
  const isoCode = getCountryIsoCode(country.value);
  return (
    <motion.div
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, delay, ease: 'easeOut' }}
      title={`${country.label} — ${country.count} đối tác`}
      className="aspect-[4/3] rounded-2xl border border-slate-200 shadow-sm overflow-hidden hover:shadow-md hover:-translate-y-0.5 transition-all duration-300 cursor-default"
    >
      {isoCode ? (
        <span
          className={`fi fi-${isoCode.toLowerCase()} fi-cover block`}
          role="img"
          aria-label={country.label}
        />
      ) : (
        <span className="w-full h-full bg-slate-100 flex items-center justify-center text-slate-400">
          <Globe className="w-6 h-6" />
        </span>
      )}
    </motion.div>
  );
}

function CountryFlagShowcase({ countries }: { countries: PublicPartnerCountry[] }) {
  const [pageIndex, setPageIndex] = useState(0);
  const totalPages = Math.max(1, Math.ceil(countries.length / FLAGS_PER_PAGE));
  const visible = countries.slice(pageIndex * FLAGS_PER_PAGE, pageIndex * FLAGS_PER_PAGE + FLAGS_PER_PAGE);

  if (countries.length === 0) {
    return (
      <div className="grid grid-cols-3 gap-4">
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="bg-white rounded-2xl border border-slate-200 p-4 h-[92px] animate-pulse">
            <div className="h-full w-full bg-slate-100 rounded-lg skeleton-shimmer" />
          </div>
        ))}
      </div>
    );
  }

  return (
    <div>
      <div className="grid grid-cols-3 gap-4">
        {visible.map((c, i) => (
          <CountryFlagCard key={c.value} country={c} delay={i * 0.05} />
        ))}
      </div>
      {totalPages > 1 && (
        <div className="mt-4 flex items-center justify-center gap-3">
          <button
            onClick={() => setPageIndex((p) => (p === 0 ? totalPages - 1 : p - 1))}
            aria-label="Xem quốc gia trước"
            className="w-9 h-9 rounded-full border border-slate-200 flex items-center justify-center text-slate-500 hover:bg-[#004c91] hover:text-white hover:border-[#004c91] transition-colors"
          >
            <ChevronLeft className="w-4 h-4" />
          </button>
          <span className="text-xs font-semibold text-slate-400">
            {pageIndex + 1} / {totalPages}
          </span>
          <button
            onClick={() => setPageIndex((p) => (p === totalPages - 1 ? 0 : p + 1))}
            aria-label="Xem quốc gia tiếp theo"
            className="w-9 h-9 rounded-full border border-slate-200 flex items-center justify-center text-slate-500 hover:bg-[#004c91] hover:text-white hover:border-[#004c91] transition-colors"
          >
            <ChevronRight className="w-4 h-4" />
          </button>
        </div>
      )}
    </div>
  );
}

export function PartnersPage() {
  const { t } = useTranslation(['partners']);
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [isVisitorFormOpen, setIsVisitorFormOpen] = useState(false);
  
  const ALL_COUNTRIES_LABEL = t('partners:list.allCountries');
  const ALL_TYPES_LABEL = t('partners:list.allTypes');

  const SORT_OPTIONS: { value: PublicPartnerSort; label: string }[] = [
    { value: 'name_asc', label: t('partners:list.sortNameAsc') },
    { value: 'newest', label: t('partners:list.sortNewest') },
    { value: 'country', label: t('partners:list.sortCountry') },
  ];

  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [selectedCountry, setSelectedCountry] = useState(ALL_COUNTRIES_LABEL);
  const [selectedType, setSelectedType] = useState(ALL_TYPES_LABEL);
  const [sort, setSort] = useState<PublicPartnerSort>('name_asc');
  const [currentPage, setCurrentPage] = useState(1);

  const [partners, setPartners] = useState<PublicPartner[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  const [countryOptions, setCountryOptions] = useState<PublicPartnerCountry[]>([]);
  const [typeOptions, setTypeOptions] = useState<PublicPartnerType[]>([]);

  // Trust metrics: total partners overall (unfiltered) + total countries.
  const [totalPartnersOverall, setTotalPartnersOverall] = useState<number | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [countries, types, overall] = await Promise.all([
          publicPartnersApi.getPublicPartnerCountries(),
          publicPartnersApi.getPublicPartnerTypes(),
          publicPartnersApi.getPublicPartners({ page: 1, pageSize: 1 }),
        ]);
        if (cancelled) return;
        setCountryOptions(countries);
        setTypeOptions(types);
        setTotalPartnersOverall(overall.totalCount);
      } catch {
        if (!cancelled) {
          setCountryOptions([]);
          setTypeOptions([]);
        }
      }
    })();
    return () => { cancelled = true; };
  }, []);

  // Deep-link support: /partners?country=<value>.
  useEffect(() => {
    if (countryOptions.length === 0) return;
    const fromUrl = searchParams.get('country');
    if (!fromUrl) return;
    const matched = findMatchingCountryValue(fromUrl, countryOptions);
    if (matched) setSelectedCountry(matched);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [countryOptions]);

  useEffect(() => {
    const handle = setTimeout(() => setDebouncedSearch(searchQuery.trim()), 400);
    return () => clearTimeout(handle);
  }, [searchQuery]);

  useEffect(() => {
    setCurrentPage(1);
  }, [debouncedSearch, selectedCountry, selectedType, sort]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    (async () => {
      try {
        const res = await publicPartnersApi.getPublicPartners({
          search: debouncedSearch || undefined,
          country: selectedCountry === ALL_COUNTRIES_LABEL ? undefined : selectedCountry,
          partnerType: selectedType === ALL_TYPES_LABEL ? undefined : selectedType,
          sort,
          page: currentPage,
          pageSize: PAGE_SIZE,
        });
        if (cancelled) return;
        setPartners(res.items);
        setTotalCount(res.totalCount);
      } catch {
        if (!cancelled) {
          setError(t('partners:list.loadError'));
          setPartners([]);
          setTotalCount(0);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [debouncedSearch, selectedCountry, selectedType, sort, currentPage, reloadToken]);

  const countryDropdownOptions = useMemo(
    () => [{ value: ALL_COUNTRIES_LABEL, label: ALL_COUNTRIES_LABEL, count: totalPartnersOverall ?? 0 }, ...countryOptions],
    [countryOptions, totalPartnersOverall],
  );
  const typeDropdownOptions = useMemo(
    () => [{ value: ALL_TYPES_LABEL, label: ALL_TYPES_LABEL, count: totalPartnersOverall ?? 0 }, ...typeOptions],
    [typeOptions, totalPartnersOverall],
  );
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));
  const hasActiveFilter = debouncedSearch !== '' || selectedCountry !== ALL_COUNTRIES_LABEL || selectedType !== ALL_TYPES_LABEL;

  const topCountryChips = useMemo(() => countryOptions.slice(0, 8), [countryOptions]);

  const paginate = (pageNumber: number) => {
    setCurrentPage(pageNumber);
    setTimeout(() => {
      document.getElementById('partners-directory')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }, 40);
  };

  const clearFilters = () => {
    setSearchQuery('');
    setSelectedCountry(ALL_COUNTRIES_LABEL);
    setSelectedType(ALL_TYPES_LABEL);
  };

  return (
    <>
      <div className="pt-20 pb-16 bg-white">
        {/* A. Compact Hero — no globe, real partner logos */}
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="min-h-[420px] lg:min-h-[480px] flex flex-col lg:flex-row items-center gap-10 py-12 lg:py-16">
            {/* Left content */}
            <motion.div
              initial={{ opacity: 0, y: 16 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.5, ease: 'easeOut' }}
              className="w-full lg:w-1/2"
            >
              <div className="inline-flex items-center gap-2 px-3 py-1.5 bg-sky-50 text-[#004c91] border border-sky-100 rounded-full text-xs font-bold uppercase tracking-wider mb-5">
                <Building2 className="w-3.5 h-3.5 text-[#f37021]" />
                {t('partners:list.heroTag')}
              </div>
              <h1 className="text-3xl md:text-4xl lg:text-[42px] font-bold text-slate-900 leading-tight mb-4">
                {t('partners:list.heroTitle')}
              </h1>
              <p className="text-slate-500 text-base leading-relaxed max-w-lg mb-8">
                {t('partners:list.heroDesc')}
              </p>
              <div className="flex flex-col sm:flex-row gap-3">
                <button
                  onClick={() => setIsVisitorFormOpen(true)}
                  className="inline-flex justify-center items-center gap-2 px-6 py-3 bg-[#f37021] text-white font-bold rounded-xl hover:bg-orange-600 transition-colors text-sm"
                >
                  {t('partners:list.bookVisit')}
                  <ArrowRight className="w-4 h-4" />
                </button>
                <button
                  onClick={() => navigate('/visit-fptu')}
                  className="inline-flex justify-center items-center px-6 py-3 bg-white text-slate-600 font-bold border border-slate-200 rounded-xl hover:border-[#004c91] hover:text-[#004c91] transition-colors text-sm"
                >
                  {t('partners:list.exploreVisit')}
                </button>
              </div>
            </motion.div>

            {/* Right: flag showcase of partnered countries (real data, no globe) */}
            <div className="w-full lg:w-1/2">
              <p className="text-xs font-bold text-slate-400 uppercase tracking-wide mb-3 text-center lg:text-left">
                {t('partners:list.partnersFrom')}
              </p>
              <CountryFlagShowcase countries={countryOptions} />
            </div>
          </div>

          {/* B. Trust metrics — real numbers only */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 pb-12 border-b border-slate-100">
            <div className="text-center">
              <div className="text-3xl font-black text-[#004c91]">{totalPartnersOverall ?? '–'}</div>
              <div className="text-slate-500 text-xs font-semibold uppercase tracking-wide mt-1">{t('partners:list.totalPartners')}</div>
            </div>
            <div className="text-center">
              <div className="text-3xl font-black text-[#004c91]">{countryOptions.length || '–'}</div>
              <div className="text-slate-500 text-xs font-semibold uppercase tracking-wide mt-1">{t('partners:list.countries')}</div>
            </div>
            {typeOptions.length > 0 && (
              <div className="text-center">
                <div className="text-3xl font-black text-[#004c91]">{typeOptions.length}</div>
                <div className="text-slate-500 text-xs font-semibold uppercase tracking-wide mt-1">{t('partners:list.partnerTypes')}</div>
              </div>
            )}
          </div>
        </div>

        {/* Directory section */}
        <div id="partners-directory" className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 pt-12">
          {/* C. Filter bar */}
          <div className="bg-white border border-slate-200 rounded-2xl p-5 md:p-6 mb-6">
            <div className="flex flex-col lg:flex-row gap-4">
              <div className="relative flex-1">
                <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                <input
                  type="text"
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  placeholder="Tìm tên đối tác, quốc gia, mô tả..."
                  className="w-full h-11 pl-10 pr-4 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-colors"
                />
              </div>

              <div className="grid grid-cols-2 lg:flex gap-3">
                <div className="relative">
                  <Globe className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400 pointer-events-none" />
                  <select
                    value={selectedCountry}
                    onChange={(e) => setSelectedCountry(e.target.value)}
                    className="w-full h-11 pl-9 pr-3 rounded-xl border border-slate-200 bg-white text-sm text-slate-700 focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-colors appearance-none"
                  >
                    {countryDropdownOptions.map((c) => (
                      <option key={c.value} value={c.value}>
                        {c.value === ALL_COUNTRIES_LABEL ? c.label : `${c.label} (${c.count})`}
                      </option>
                    ))}
                  </select>
                </div>

                {typeOptions.length > 0 && (
                  <select
                    value={selectedType}
                    onChange={(e) => setSelectedType(e.target.value)}
                    className="h-11 px-3 rounded-xl border border-slate-200 bg-white text-sm text-slate-700 focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-colors"
                  >
                    {typeDropdownOptions.map((t) => (
                      <option key={t.value} value={t.value}>
                        {t.value === ALL_TYPES_LABEL ? t.label : `${t.label} (${t.count})`}
                      </option>
                    ))}
                  </select>
                )}

                <select
                  value={sort}
                  onChange={(e) => setSort(e.target.value as PublicPartnerSort)}
                  className="h-11 px-3 rounded-xl border border-slate-200 bg-white text-sm text-slate-700 focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-colors"
                >
                  {SORT_OPTIONS.map((s) => (
                    <option key={s.value} value={s.value}>{s.label}</option>
                  ))}
                </select>
              </div>
            </div>

            {hasActiveFilter && (
              <div className="mt-4 flex items-center justify-between flex-wrap gap-3 bg-orange-50/60 border border-orange-100 rounded-xl px-4 py-3">
                <span className="text-xs font-semibold text-slate-700">
                  <Trans i18nKey="partners:list.foundMatches" count={totalCount}>
                    Tìm thấy <span className="font-bold text-[#f37021]">{totalCount}</span> đối tác phù hợp.
                  </Trans>
                </span>
                <button
                  onClick={clearFilters}
                  className="text-xs font-bold text-[#004c91] hover:text-[#f37021] transition-colors"
                >
                  {t('partners:list.clearFilters')}
                </button>
              </div>
            )}
          </div>

          {/* D. Country / region chips */}
          {topCountryChips.length > 0 && (
            <div className="mb-8">
              <p className="text-xs font-bold text-slate-400 uppercase tracking-wide mb-3">{t('partners:list.exploreByCountry')}</p>
              <div className="flex gap-2 overflow-x-auto no-scrollbar">
                {topCountryChips.map((c) => (
                  <button
                    key={c.value}
                    onClick={() => setSelectedCountry(c.value === selectedCountry ? ALL_COUNTRIES_LABEL : c.value)}
                    className={`relative shrink-0 px-4 py-2 rounded-full text-sm font-semibold whitespace-nowrap transition-colors border ${
                      selectedCountry === c.value
                        ? 'bg-[#004c91] text-white border-[#004c91]'
                        : 'bg-white text-slate-600 border-slate-200 hover:border-[#004c91]/40'
                    }`}
                  >
                    {c.label} ({c.count})
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* E. Partner grid */}
          <div className="mb-12">
            {error ? (
              <div className="bg-white rounded-2xl border border-red-100 py-16 px-6 text-center">
                <AlertTriangle className="w-10 h-10 text-red-400 mx-auto mb-4" />
                <h3 className="text-base font-bold text-slate-700 mb-1">{error}</h3>
                <button
                  onClick={() => setReloadToken((t) => t + 1)}
                  className="mt-4 inline-flex items-center gap-2 px-5 py-2.5 bg-[#004c91] text-white text-sm font-bold rounded-xl hover:bg-[#003b70] transition-colors"
                >
                  <RefreshCw className="w-4 h-4" /> {t('partners:list.retry')}
                </button>
              </div>
            ) : loading ? (
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
                {Array.from({ length: PAGE_SIZE }).map((_, i) => <PartnerCardSkeleton key={i} />)}
              </div>
            ) : (
              <AnimatePresence mode="popLayout">
                {partners.length > 0 ? (
                  <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
                    {partners.map((partner, index) => (
                      <PublicPartnerCard
                        key={partner.partnerId}
                        partner={partner}
                        index={index}
                        onClick={() => navigate(`/partners/${partner.publicSlug || partner.partnerId}`)}
                      />
                    ))}
                  </div>
                ) : (
                  <div className="bg-white rounded-2xl border border-slate-200 py-16 px-6 text-center">
                    <div className="w-16 h-16 rounded-full bg-slate-50 flex items-center justify-center mx-auto mb-4">
                      <RefreshCw className="w-8 h-8 text-slate-400" />
                    </div>
                    <h3 className="text-base font-bold text-slate-700 mb-1">
                      {hasActiveFilter ? t('partners:list.noMatchTitle') : t('partners:list.noDataTitle')}
                    </h3>
                    <p className="text-sm text-slate-400 max-w-sm mx-auto mb-4">
                      {hasActiveFilter
                        ? t('partners:list.adjustFilterMsg')
                        : t('partners:list.checkBackMsg')}
                    </p>
                    {hasActiveFilter && (
                      <button
                        onClick={clearFilters}
                        className="px-5 py-2.5 bg-[#004c91] text-white text-sm font-bold rounded-xl hover:bg-[#003b70] transition-colors"
                      >
                        {t('partners:list.viewAll')}
                      </button>
                    )}
                  </div>
                )}
              </AnimatePresence>
            )}

            {/* F. Pagination */}
            {!loading && !error && partners.length > 0 && totalPages > 1 && (
              <div className="mt-10 flex items-center justify-center gap-1.5">
                <button
                  onClick={() => currentPage > 1 && paginate(currentPage - 1)}
                  disabled={currentPage === 1}
                  className="w-9 h-9 rounded-xl border border-slate-200 flex items-center justify-center text-slate-600 hover:bg-[#004c91] hover:text-white hover:border-[#004c91] disabled:opacity-40 disabled:hover:bg-white disabled:hover:text-slate-600 transition-colors"
                >
                  <ChevronLeft className="w-4 h-4" />
                </button>
                {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                  <button
                    key={page}
                    onClick={() => paginate(page)}
                    className={`w-9 h-9 rounded-xl text-sm font-bold transition-colors border ${
                      page === currentPage
                        ? 'bg-[#f37021] border-[#f37021] text-white'
                        : 'bg-white border-slate-200 text-slate-600 hover:border-[#004c91]/40'
                    }`}
                  >
                    {page}
                  </button>
                ))}
                <button
                  onClick={() => currentPage < totalPages && paginate(currentPage + 1)}
                  disabled={currentPage === totalPages}
                  className="w-9 h-9 rounded-xl border border-slate-200 flex items-center justify-center text-slate-600 hover:bg-[#004c91] hover:text-white hover:border-[#004c91] disabled:opacity-40 disabled:hover:bg-white disabled:hover:text-slate-600 transition-colors"
                >
                  <ChevronRight className="w-4 h-4" />
                </button>
              </div>
            )}
          </div>

          {/* G. Final CTA — soft gradient */}
          <div className="rounded-2xl bg-gradient-to-r from-sky-50 to-orange-50 border border-slate-100 p-8 md:p-10 text-center">
            <h3 className="text-xl md:text-2xl font-bold text-slate-900 mb-2">
              {t('partners:list.ctaTitle')}
            </h3>
            <p className="text-slate-500 text-sm md:text-base max-w-xl mx-auto mb-6">
              {t('partners:list.ctaDesc')}
            </p>
            <div className="flex flex-col sm:flex-row gap-3 justify-center">
              <button
                onClick={() => setIsVisitorFormOpen(true)}
                className="inline-flex justify-center items-center gap-2 px-6 py-3 bg-[#f37021] text-white font-bold rounded-xl hover:bg-orange-600 transition-colors text-sm"
              >
                {t('partners:list.bookVisit')} <ArrowRight className="w-4 h-4" />
              </button>
              <button
                onClick={() => document.querySelector('footer')?.scrollIntoView({ behavior: 'smooth', block: 'start' })}
                className="inline-flex justify-center items-center gap-2 px-6 py-3 bg-white text-slate-600 font-bold border border-slate-200 rounded-xl hover:border-[#004c91] hover:text-[#004c91] transition-colors text-sm"
              >
                <Mail className="w-4 h-4" /> {t('partners:list.contactDept')}
              </button>
            </div>
          </div>
        </div>
      </div>
      <VisitingFormPopup isOpen={isVisitorFormOpen} onClose={() => setIsVisitorFormOpen(false)} />
    </>
  );
}
