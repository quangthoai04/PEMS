/**
 * Trang FAQPage (Public) — Help Center
 * Trung tâm trợ giúp công khai của trường — dữ liệu thật từ GET /api/public/faqs và
 * GET /api/public/faqs/type-counts. Chỉ hiển thị FAQ status=PUBLISHED, không dùng mock data.
 */

import React, { useEffect, useMemo, useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import {
  Search,
  ChevronDown,
  HelpCircle,
  ChevronLeft,
  ChevronRight,
  Loader2,
  RefreshCw,
  KeyRound,
  MapPinned,
  UsersRound,
  Truck,
  FileText,
  Bell,
  MoreHorizontal,
  ArrowRight,
  Mail,
  Sparkles,
} from 'lucide-react';
import { publicFaqApi } from '../features/public-faq/api/publicFaqApi';
import { VisitingFormPopup } from '../components/modals/VisitingFormPopup';
import { useVisitEntryCta } from '../shared/features/useVisitEntryCta';
import type { PublicFaqItem, PublicFaqTypeCount } from '../features/public-faq/types/publicFaq.types';
import { useTranslation } from 'react-i18next';

const ALL_TYPE = 'ALL';
const PAGE_SIZE = 10;
const SUGGESTED_SIZE = 6;

/** faqType -> icon, kept in sync with backend FaqConstants.Type.All (7 fixed values). */
const TYPE_ICONS: Record<string, React.ComponentType<{ className?: string }>> = {
  ACCOUNT_ACCESS: KeyRound,
  VISIT_REQUEST: MapPinned,
  DELEGATION_MANAGEMENT: UsersRound,
  LOGISTICS_RESOURCE: Truck,
  DOCUMENT_MEDIA: FileText,
  NOTIFICATION_EMAIL: Bell,
  OTHER: MoreHorizontal,
};

// We will move TYPE_DESCRIPTIONS inside the component or use translation directly.

/** Hook that returns a per-language label map for the 7 FAQ types. Falls back to the raw type key. */
function useFaqTypeLabels(): Record<string, string> {
  const { t } = useTranslation('faq');
  return {
    ACCOUNT_ACCESS: t('faq:typeLabels.ACCOUNT_ACCESS'),
    VISIT_REQUEST: t('faq:typeLabels.VISIT_REQUEST'),
    DELEGATION_MANAGEMENT: t('faq:typeLabels.DELEGATION_MANAGEMENT'),
    LOGISTICS_RESOURCE: t('faq:typeLabels.LOGISTICS_RESOURCE'),
    DOCUMENT_MEDIA: t('faq:typeLabels.DOCUMENT_MEDIA'),
    NOTIFICATION_EMAIL: t('faq:typeLabels.NOTIFICATION_EMAIL'),
    OTHER: t('faq:typeLabels.OTHER'),
  };
}

/** Hook that returns a per-language description map for the 7 FAQ types. */
function useFaqTypeDescriptions(): Record<string, string> {
  const { t } = useTranslation('faq');
  return {
    ACCOUNT_ACCESS: t('faq:descriptions.ACCOUNT_ACCESS'),
    VISIT_REQUEST: t('faq:descriptions.VISIT_REQUEST'),
    DELEGATION_MANAGEMENT: t('faq:descriptions.DELEGATION_MANAGEMENT'),
    LOGISTICS_RESOURCE: t('faq:descriptions.LOGISTICS_RESOURCE'),
    DOCUMENT_MEDIA: t('faq:descriptions.DOCUMENT_MEDIA'),
    NOTIFICATION_EMAIL: t('faq:descriptions.NOTIFICATION_EMAIL'),
    OTHER: t('faq:descriptions.OTHER'),
  };
}

function TopicCardSkeleton() {
  return (
    <div className="bg-white rounded-2xl border border-slate-200 p-5 animate-pulse">
      <div className="w-10 h-10 rounded-xl bg-slate-100 mb-4 skeleton-shimmer" />
      <div className="h-4 bg-slate-100 rounded w-3/4 mb-2 skeleton-shimmer" />
      <div className="h-3 bg-slate-100 rounded w-full skeleton-shimmer" />
    </div>
  );
}

function TopicCard({
  type, active, onClick, delay,
}: { type: PublicFaqTypeCount; active: boolean; onClick: () => void; delay: number; key?: React.Key }) {
  const typeLabels = useFaqTypeLabels();
  const typeDescriptions = useFaqTypeDescriptions();
  const Icon = TYPE_ICONS[type.value] ?? HelpCircle;
  return (
    <motion.button
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, delay, ease: 'easeOut' }}
      onClick={onClick}
      className={`text-left bg-white rounded-2xl border p-5 transition-all duration-300 hover:-translate-y-1 hover:shadow-md ${
        active ? 'border-[#004c91] ring-2 ring-[#004c91]/15' : 'border-slate-200'
      }`}
    >
      <div className={`w-10 h-10 rounded-xl flex items-center justify-center mb-4 ${active ? 'bg-[#004c91] text-white' : 'bg-[#004c91]/8 text-[#004c91]'}`}>
        <Icon className="w-5 h-5" />
      </div>
      <div className="flex items-center justify-between gap-2 mb-1.5">
        <h3 className="text-sm font-bold text-slate-900 leading-snug">{typeLabels[type.value] ?? type.label}</h3>
        <span className="shrink-0 text-xs font-bold text-[#f37021]">{type.count}</span>
      </div>
      <p className="text-xs text-slate-500 leading-relaxed line-clamp-2">
        {typeDescriptions[type.value] ?? ''}
      </p>
    </motion.button>
  );
}

function FaqAccordionItem({ faq, isOpen, onToggle }: { faq: PublicFaqItem; isOpen: boolean; onToggle: () => void; key?: React.Key }) {
  const typeLabels = useFaqTypeLabels();
  return (
    <motion.div
      layout
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0 }}
      className="bg-white rounded-2xl border border-slate-200 overflow-hidden hover:border-slate-300 transition-colors"
    >
      <button
        onClick={onToggle}
        className="w-full px-5 sm:px-6 py-4 sm:py-5 flex items-center justify-between gap-4 text-left"
      >
        <div className="flex flex-col sm:flex-row sm:items-center gap-2 sm:gap-3 min-w-0">
          <span className="inline-block w-fit px-2.5 py-1 bg-[#004c91]/8 text-[#004c91] text-[11px] font-bold rounded-lg whitespace-nowrap">
            {typeLabels[faq.faqType] ?? faq.faqTypeLabel}
          </span>
          <h3 className={`text-[15px] sm:text-base font-bold leading-snug ${isOpen ? 'text-[#f37021]' : 'text-slate-900'}`}>
            {faq.question}
          </h3>
        </div>
        <div className={`shrink-0 w-8 h-8 rounded-full flex items-center justify-center transition-transform duration-300 ${isOpen ? 'bg-orange-50 rotate-180' : 'bg-slate-50'}`}>
          <ChevronDown className={`w-4 h-4 ${isOpen ? 'text-[#f37021]' : 'text-slate-400'}`} />
        </div>
      </button>

      <AnimatePresence>
        {isOpen && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.3, ease: 'easeInOut' }}
            className="overflow-hidden"
          >
            <div className="px-5 sm:px-6 pb-5 sm:pb-6">
              <div className="p-4 sm:p-5 bg-slate-50 rounded-xl border-l-4 border-[#f37021]">
                <p className="text-slate-700 text-sm sm:text-[15px] leading-relaxed whitespace-pre-line">
                  {faq.answer}
                </p>
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  );
}

export function FAQPage() {
  const { t, i18n } = useTranslation(['faq']);
  // Public FAQ labels are localised server-side; refetch on language switch (same pattern as NewsPage).
  const lang = i18n.language;
  const typeLabels = useFaqTypeLabels();
  // Route to the v2 form when per-campus v2 is enabled; only open the v1 popup for a real backend OFF.
  const visitCta = useVisitEntryCta('public');

  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedKeyword, setDebouncedKeyword] = useState('');
  const [selectedType, setSelectedType] = useState(ALL_TYPE);
  const [openFaqId, setOpenFaqId] = useState<number | null>(null);
  const [currentPage, setCurrentPage] = useState(1);

  const [faqs, setFaqs] = useState<PublicFaqItem[]>([]);
  const [totalPages, setTotalPages] = useState(0);
  const [totalItems, setTotalItems] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  const [typeCounts, setTypeCounts] = useState<PublicFaqTypeCount[]>([]);
  const [typeCountsLoading, setTypeCountsLoading] = useState(true);

  const [suggested, setSuggested] = useState<PublicFaqItem[]>([]);
  const [suggestedLoading, setSuggestedLoading] = useState(true);

  // Topic type counts — reload when language changes.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      setTypeCountsLoading(true);
      try {
        const counts = await publicFaqApi.getFaqTypeCounts(lang);
        if (!cancelled) setTypeCounts(counts);
      } catch {
        if (!cancelled) setTypeCounts([]);
      } finally {
        if (!cancelled) setTypeCountsLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [reloadToken, lang]);

  // Suggested questions — reload when language changes.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      setSuggestedLoading(true);
      try {
        const res = await publicFaqApi.getPublicFaqs({ page: 1, pageSize: SUGGESTED_SIZE, languageCode: lang });
        if (!cancelled) setSuggested(res.items ?? []);
      } catch {
        if (!cancelled) setSuggested([]);
      } finally {
        if (!cancelled) setSuggestedLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [reloadToken, lang]);

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedKeyword(searchQuery.trim());
      setCurrentPage(1);
    }, 400);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      setError(null);
      try {
        const res = await publicFaqApi.getPublicFaqs({
          page: currentPage,
          pageSize: PAGE_SIZE,
          keyword: debouncedKeyword || undefined,
          faqType: selectedType === ALL_TYPE ? undefined : selectedType,
          languageCode: lang,
        });
        if (cancelled) return;
        setFaqs(res.items ?? []);
        setTotalPages(res.totalPages ?? 0);
        setTotalItems(res.totalItems ?? 0);
      } catch {
        if (!cancelled) {
          setError(t('faq:main.errorMsg'));
          setFaqs([]);
          setTotalPages(0);
          setTotalItems(0);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [debouncedKeyword, selectedType, currentPage, reloadToken, lang]);

  const handleTypeSelect = (type: string) => {
    setSelectedType(type);
    setCurrentPage(1);
    setOpenFaqId(null);
    document.getElementById('faq-main-section')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const handlePageChange = (page: number) => {
    setCurrentPage(page);
    setOpenFaqId(null);
    document.getElementById('faq-main-section')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const clearFilters = () => {
    setSearchQuery('');
    setSelectedType(ALL_TYPE);
  };

  const hasActiveFilter = debouncedKeyword !== '' || selectedType !== ALL_TYPE;
  const allTypesTotal = useMemo(() => typeCounts.reduce((sum, t) => sum + t.count, 0), [typeCounts]);

  return (
    <div className="bg-slate-50 min-h-screen pt-20 pb-16">
      {/* A. Compact Hero — navy background, badge, search */}
      <div className="bg-[#004c91] relative overflow-hidden">
        <div className="absolute top-0 right-0 opacity-10 pointer-events-none transform translate-x-16 -translate-y-16">
          <HelpCircle className="w-72 h-72 text-white" />
        </div>
        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-14 md:py-16 text-center relative z-10">
          <motion.div
            initial={{ opacity: 0, y: 16 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.5, ease: 'easeOut' }}
          >
            <div className="inline-flex items-center gap-2 px-3 py-1.5 bg-white/10 text-white border border-white/20 rounded-full text-xs font-bold uppercase tracking-wider mb-5">
              <Sparkles className="w-3.5 h-3.5 text-orange-300" />
              Help Center
            </div>
            <h1 className="text-2xl md:text-4xl font-bold text-white leading-tight mb-3">
              {t('faq:hero.title')}
            </h1>
            <p className="text-blue-100 text-sm md:text-base max-w-2xl mx-auto mb-8">
              {t('faq:hero.subtitle')}
            </p>

            <div className="relative max-w-xl mx-auto">
              <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                {loading && debouncedKeyword
                  ? <Loader2 className="h-5 w-5 text-slate-400 animate-spin" />
                  : <Search className="h-5 w-5 text-slate-400" />}
              </div>
              <input
                type="text"
                placeholder={t('faq:hero.searchPlaceholder')}
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="block w-full pl-12 pr-4 py-3.5 md:py-4 rounded-xl text-slate-900 bg-white placeholder:text-slate-400 focus:outline-none focus:ring-4 focus:ring-orange-400/30 text-sm md:text-base shadow-lg transition-shadow"
              />
            </div>
          </motion.div>
        </div>
      </div>

      <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 pt-10">
        {/* B. Topic cards */}
        <div className="mb-12">
          <h2 className="text-lg font-bold text-slate-900 mb-5">{t('faq:browse.title')}</h2>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <motion.button
              initial={{ opacity: 0, y: 16 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.4, ease: 'easeOut' }}
              onClick={() => handleTypeSelect(ALL_TYPE)}
              className={`text-left bg-white rounded-2xl border p-5 transition-all duration-300 hover:-translate-y-1 hover:shadow-md ${
                selectedType === ALL_TYPE ? 'border-[#004c91] ring-2 ring-[#004c91]/15' : 'border-slate-200'
              }`}
            >
              <div className={`w-10 h-10 rounded-xl flex items-center justify-center mb-4 ${selectedType === ALL_TYPE ? 'bg-[#004c91] text-white' : 'bg-[#004c91]/8 text-[#004c91]'}`}>
                <HelpCircle className="w-5 h-5" />
              </div>
              <div className="flex items-center justify-between gap-2 mb-1.5">
                <h3 className="text-sm font-bold text-slate-900">{t('faq:browse.all')}</h3>
                <span className="text-xs font-bold text-[#f37021]">{allTypesTotal}</span>
              </div>
              <p className="text-xs text-slate-500 leading-relaxed">{t('faq:browse.allDesc')}</p>
            </motion.button>

            {typeCountsLoading
              ? Array.from({ length: 3 }).map((_, i) => <TopicCardSkeleton key={i} />)
              : typeCounts.map((type, i) => (
                  <TopicCard
                    key={type.value}
                    type={type}
                    active={selectedType === type.value}
                    onClick={() => handleTypeSelect(type.value)}
                    delay={(i + 1) * 0.05}
                  />
                ))}
          </div>
        </div>

        {/* C. Suggested questions — only when there's real data, no filter/search applied */}
        {!hasActiveFilter && !suggestedLoading && suggested.length > 0 && (
          <div className="mb-12">
            <h2 className="text-lg font-bold text-slate-900 mb-5">{t('faq:suggested.title')}</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {suggested.map((faq) => (
                <button
                  key={faq.faqId}
                  onClick={() => {
                    setOpenFaqId(faq.faqId);
                    document.getElementById('faq-main-section')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
                  }}
                  className="text-left bg-white rounded-xl border border-slate-200 p-4 hover:border-[#004c91]/40 hover:shadow-sm transition-all"
                >
                  <span className="inline-block mb-2 px-2 py-0.5 bg-[#004c91]/8 text-[#004c91] text-[10px] font-bold rounded-md uppercase">
                    {typeLabels[faq.faqType] ?? faq.faqTypeLabel}
                  </span>
                  <p className="text-sm font-semibold text-slate-800 leading-snug line-clamp-2">{faq.question}</p>
                </button>
              ))}
            </div>
          </div>
        )}

        {/* D. Main FAQ section — left sticky topic nav (desktop) + right accordion */}
        <div id="faq-main-section" className="flex flex-col lg:flex-row gap-8">
          {/* Desktop: sticky left nav */}
          <aside className="hidden lg:block lg:w-64 shrink-0">
            <div className="sticky top-28">
              <p className="text-xs font-bold text-slate-400 uppercase tracking-wide mb-3">{t('faq:main.topics')}</p>
              <nav className="flex flex-col gap-1">
                <button
                  onClick={() => handleTypeSelect(ALL_TYPE)}
                  className={`text-left px-3 py-2.5 rounded-lg text-sm font-semibold transition-colors ${
                    selectedType === ALL_TYPE ? 'bg-[#004c91] text-white' : 'text-slate-600 hover:bg-slate-100'
                  }`}
                >
                  {t('faq:browse.all')} ({allTypesTotal})
                </button>
                {typeCounts.map((type) => (
                  <button
                    key={type.value}
                    onClick={() => handleTypeSelect(type.value)}
                    className={`text-left px-3 py-2.5 rounded-lg text-sm font-semibold transition-colors ${
                      selectedType === type.value ? 'bg-[#004c91] text-white' : 'text-slate-600 hover:bg-slate-100'
                    }`}
                  >
                    {typeLabels[type.value] ?? type.label} ({type.count})
                  </button>
                ))}
              </nav>
            </div>
          </aside>

          {/* Mobile/tablet: horizontal chips */}
          <div className="lg:hidden flex gap-2 overflow-x-auto no-scrollbar pb-1">
            <button
              onClick={() => handleTypeSelect(ALL_TYPE)}
              className={`shrink-0 px-4 py-2 rounded-full text-sm font-semibold whitespace-nowrap transition-colors border ${
                selectedType === ALL_TYPE ? 'bg-[#004c91] text-white border-[#004c91]' : 'bg-white text-slate-600 border-slate-200'
              }`}
            >
              {t('faq:browse.all')}
            </button>
            {typeCounts.map((type) => (
              <button
                key={type.value}
                onClick={() => handleTypeSelect(type.value)}
                className={`shrink-0 px-4 py-2 rounded-full text-sm font-semibold whitespace-nowrap transition-colors border ${
                  selectedType === type.value ? 'bg-[#004c91] text-white border-[#004c91]' : 'bg-white text-slate-600 border-slate-200'
                }`}
              >
                {typeLabels[type.value] ?? type.label} ({type.count})
              </button>
            ))}
          </div>

          {/* Right: accordion list */}
          <div className="flex-1 min-w-0">
            {!loading && !error && (
              <p className="text-sm text-slate-500 mb-4">
                {totalItems > 0 ? t('faq:main.found', { count: totalItems }) : ''}
              </p>
            )}

            {loading ? (
              <div className="space-y-4">
                {Array.from({ length: 5 }).map((_, i) => (
                  <div key={i} className="bg-white rounded-2xl border border-slate-200 h-20 animate-pulse skeleton-shimmer" />
                ))}
              </div>
            ) : error ? (
              <div className="text-center py-16 bg-white rounded-2xl border border-red-100">
                <HelpCircle className="w-14 h-14 text-red-200 mx-auto mb-4" />
                <h3 className="text-base font-bold text-slate-800 mb-1">{t('faq:main.errorTitle')}</h3>
                <p className="text-slate-500 text-sm mb-4">{error}</p>
                <button
                  onClick={() => setReloadToken((t) => t + 1)}
                  className="inline-flex items-center gap-2 px-5 py-2.5 bg-[#004c91] text-white text-sm font-bold rounded-xl hover:bg-[#003b70] transition-colors"
                >
                  <RefreshCw className="w-4 h-4" /> {t('faq:main.retry')}
                </button>
              </div>
            ) : faqs.length === 0 ? (
              <div className="text-center py-16 bg-white rounded-2xl border border-slate-200">
                <HelpCircle className="w-14 h-14 text-slate-300 mx-auto mb-4" />
                <h3 className="text-base font-bold text-slate-800 mb-1">{t('faq:main.noResultTitle')}</h3>
                <p className="text-slate-500 text-sm max-w-sm mx-auto mb-4">
                  {t('faq:main.noResultMsg')}
                </p>
                {hasActiveFilter && (
                  <button
                    onClick={clearFilters}
                    className="px-5 py-2.5 bg-[#004c91] text-white text-sm font-bold rounded-xl hover:bg-[#003b70] transition-colors"
                  >
                    {t('faq:main.clearFilter')}
                  </button>
                )}
              </div>
            ) : (
              <>
                <div className="space-y-4">
                  <AnimatePresence>
                    {faqs.map((faq) => (
                      <FaqAccordionItem
                        key={faq.faqId}
                        faq={faq}
                        isOpen={openFaqId === faq.faqId}
                        onToggle={() => setOpenFaqId(openFaqId === faq.faqId ? null : faq.faqId)}
                      />
                    ))}
                  </AnimatePresence>
                </div>

                {totalPages > 1 && (
                  <div className="mt-8 flex justify-center items-center gap-1.5">
                    <button
                      onClick={() => currentPage > 1 && handlePageChange(currentPage - 1)}
                      disabled={currentPage === 1}
                      className="w-9 h-9 rounded-xl flex items-center justify-center bg-white border border-slate-200 text-slate-600 disabled:opacity-40 hover:bg-[#004c91] hover:text-white hover:border-[#004c91] disabled:hover:bg-white disabled:hover:text-slate-600 transition-colors"
                    >
                      <ChevronLeft className="w-4 h-4" />
                    </button>
                    {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                      <button
                        key={page}
                        onClick={() => handlePageChange(page)}
                        className={`w-9 h-9 rounded-xl text-sm font-bold transition-colors border ${
                          currentPage === page
                            ? 'bg-[#f37021] border-[#f37021] text-white'
                            : 'bg-white border-slate-200 text-slate-600 hover:border-[#004c91]/40'
                        }`}
                      >
                        {page}
                      </button>
                    ))}
                    <button
                      onClick={() => currentPage < totalPages && handlePageChange(currentPage + 1)}
                      disabled={currentPage === totalPages}
                      className="w-9 h-9 rounded-xl flex items-center justify-center bg-white border border-slate-200 text-slate-600 disabled:opacity-40 hover:bg-[#004c91] hover:text-white hover:border-[#004c91] disabled:hover:bg-white disabled:hover:text-slate-600 transition-colors"
                    >
                      <ChevronRight className="w-4 h-4" />
                    </button>
                  </div>
                )}
              </>
            )}
          </div>
        </div>

        {/* E. Final CTA */}
        <div className="mt-14 rounded-2xl bg-gradient-to-r from-sky-50 to-orange-50 border border-slate-100 p-8 md:p-10 text-center">
          <h3 className="text-xl md:text-2xl font-bold text-slate-900 mb-2">{t('faq:cta.title')}</h3>
          <p className="text-slate-500 text-sm md:text-base max-w-xl mx-auto mb-6">
            {t('faq:cta.subtitle')}
          </p>
          <div className="flex flex-col sm:flex-row gap-3 justify-center">
            <button
              onClick={() => document.querySelector('footer')?.scrollIntoView({ behavior: 'smooth', block: 'start' })}
              className="inline-flex justify-center items-center gap-2 px-6 py-3 bg-white text-slate-600 font-bold border border-slate-200 rounded-xl hover:border-[#004c91] hover:text-[#004c91] transition-colors text-sm"
            >
              <Mail className="w-4 h-4" /> {t('faq:cta.contact')}
            </button>
            <button
              onClick={visitCta.trigger}
              className="inline-flex justify-center items-center gap-2 px-6 py-3 bg-[#f37021] text-white font-bold rounded-xl hover:bg-orange-600 transition-colors text-sm"
            >
              {t('faq:cta.register')} <ArrowRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      </div>

      <VisitingFormPopup isOpen={visitCta.popupOpen} onClose={visitCta.closePopup} />
    </div>
  );
}
