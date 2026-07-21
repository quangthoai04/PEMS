/**
 * Component SearchPopup
 * Popup hỗ trợ tìm kiếm nội dung chung trên toàn hệ thống công cộng — dữ liệu thật từ
 * GET /api/public/search (tin tức PUBLISHED, đối tác APPROVED+PUBLIC, FAQ PUBLISHED, campus ACTIVE).
 */

import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Search, X, Loader2, Newspaper, Building2, HelpCircle, MapPin, ArrowRight } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useNavigate } from 'react-router-dom';
import { publicSearchApi } from '../../features/public-search/api/publicSearchApi';
import { publicPartnersApi } from '../../features/public-partners/api/publicPartnersApi';
import { publicFaqApi } from '../../features/public-faq/api/publicFaqApi';
import { authenticationApi } from '../../features/authentication/api/authenticationApi';
import type { SearchInformationResult } from '../../features/public-search/types/publicSearch.types';
import { useTranslation } from 'react-i18next';

import { formatVietnamDate } from '../../shared/utils/vietnamTime';
interface SearchPopupProps {
  isOpen: boolean;
  onClose: () => void;
}

// FAQ types most relevant to this system's core purpose (visitor reception) — surfaced first
// among the suggestion chips when real data is available for them.
const VISIT_RELATED_FAQ_TYPES = ['VISIT_REQUEST', 'DELEGATION_MANAGEMENT', 'LOGISTICS_RESOURCE'];

const CAMPUS_CONTACTS = [
  {
    city: 'HÀ NỘI',
    addressKey: 'search:contactsHanoiAddress',
    hotline: '(024) 7300 5588',
    email: 'tuyensinhhanoi@fpt.edu.vn'
  },
  {
    city: 'TP. HỒ CHÍ MINH',
    addressKey: 'search:contactsHcmAddress',
    hotline: '(028) 7300 5588',
    email: 'tuyensinhhcm@fpt.edu.vn'
  },
  {
    city: 'ĐÀ NẴNG',
    addressKey: 'search:contactsDanangAddress',
    hotline: '(0236) 730 0999',
    email: 'tuyensinhdanang@fpt.edu.vn'
  },
  {
    city: 'CẦN THƠ',
    addressKey: 'search:contactsCanthoAddress',
    hotline: '(0292) 730 3636',
    email: 'tuyensinhcantho@fpt.edu.vn'
  },
  {
    city: 'QUY NHƠN',
    addressKey: 'search:contactsQuynhonAddress',
    hotline: '(0256) 7300 999',
    email: 'tuyensinhquynhon@fpt.edu.vn'
  }
];

function formatDate(iso?: string | null): string {
  if (!iso) return '';
  try {
    return formatVietnamDate(iso);
  } catch {
    return '';
  }
}

export function SearchPopup({ isOpen, onClose }: SearchPopupProps) {
  const { t, i18n } = useTranslation(['search']);
  const navigate = useNavigate();
  const inputRef = useRef<HTMLInputElement>(null);

  const [keyword, setKeyword] = useState('');
  const [debouncedKeyword, setDebouncedKeyword] = useState('');
  const [result, setResult] = useState<SearchInformationResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);

  const [suggestions, setSuggestions] = useState<string[]>([]);

  // Update contacts with translations inside component to respond to locale change
  const translatedContacts = useMemo(() => [
    { ...CAMPUS_CONTACTS[0], city: t('search:contactsHanoi'), address: t(CAMPUS_CONTACTS[0].addressKey as unknown as TemplateStringsArray) },
    { ...CAMPUS_CONTACTS[1], city: t('search:contactsHcm'), address: t(CAMPUS_CONTACTS[1].addressKey as unknown as TemplateStringsArray) },
    { ...CAMPUS_CONTACTS[2], city: t('search:contactsDanang'), address: t(CAMPUS_CONTACTS[2].addressKey as unknown as TemplateStringsArray) },
    { ...CAMPUS_CONTACTS[3], city: t('search:contactsCantho'), address: t(CAMPUS_CONTACTS[3].addressKey as unknown as TemplateStringsArray) },
    { ...CAMPUS_CONTACTS[4], city: t('search:contactsQuynhon'), address: t(CAMPUS_CONTACTS[4].addressKey as unknown as TemplateStringsArray) }
  ], [t]);

  useEffect(() => {
    if (isOpen) {
      setTimeout(() => {
        inputRef.current?.focus();
      }, 100);
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = 'unset';
      // Reset search state when closed so re-opening starts fresh.
      setKeyword('');
      setDebouncedKeyword('');
      setResult(null);
      setError(false);
    }
    return () => {
      document.body.style.overflow = 'unset';
    };
  }, [isOpen]);

  // Suggestion chips — built from real data across campuses, partner types, and FAQ topics
  // (visit/delegation/logistics topics prioritized, since this system is visitor-reception-focused).
  useEffect(() => {
    if (!isOpen) return;
    let cancelled = false;
    (async () => {
      try {
        const [campuses, partnerTypes, faqTypes] = await Promise.all([
          authenticationApi.getActiveCampuses().catch(() => []),
          publicPartnersApi.getPublicPartnerTypes(i18n.language).catch(() => []),
          publicFaqApi.getFaqTypeCounts(i18n.language).catch(() => []),
        ]);
        if (cancelled) return;

        const visitFaqLabels = faqTypes
          .filter((t) => VISIT_RELATED_FAQ_TYPES.includes(t.value) && t.count > 0)
          .map((t) => t.label);
        const otherFaqLabels = faqTypes
          .filter((t) => !VISIT_RELATED_FAQ_TYPES.includes(t.value) && t.count > 0)
          .map((t) => t.label);
        const partnerTypeLabels = partnerTypes.filter((t) => t.count > 0).map((t) => t.label);
        const campusLabels = campuses.map((c) => c.campusName);

        // Prioritize visit-reception topics, then blend in campuses / partner types / other FAQ topics.
        const merged = [...visitFaqLabels, ...campusLabels, ...partnerTypeLabels, ...otherFaqLabels];
        const unique = Array.from(new Set(merged)).slice(0, 8);
        setSuggestions(unique);
      } catch {
        if (!cancelled) setSuggestions([]);
      }
    })();
    return () => { cancelled = true; };
  }, [isOpen, i18n.language]);

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedKeyword(keyword.trim()), 350);
    return () => clearTimeout(timer);
  }, [keyword]);

  useEffect(() => {
    if (!debouncedKeyword) {
      setResult(null);
      setError(false);
      setLoading(false);
      return;
    }
    let cancelled = false;
    (async () => {
      setLoading(true);
      setError(false);
      try {
        const data = await publicSearchApi.search({ keyword: debouncedKeyword, limit: 5, languageCode: i18n.language });
        if (!cancelled) setResult(data);
      } catch {
        if (!cancelled) {
          setError(true);
          setResult(null);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [debouncedKeyword, i18n.language]);

  const handleChipClick = (label: string) => {
    setKeyword(label);
    inputRef.current?.focus();
  };

  const goTo = (path: string) => {
    onClose();
    navigate(path);
  };

  const hasResults = useMemo(
    () => !!result && result.totalCount > 0,
    [result],
  );

  return (
    <AnimatePresence>
      {isOpen && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          className="fixed inset-0 z-[100] bg-black/50 backdrop-blur-sm flex items-center justify-center p-4 sm:p-6"
          onClick={onClose}
        >
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            transition={{ duration: 0.2 }}
            onClick={(e) => e.stopPropagation()}
            className="bg-[#f4f4f4] w-full max-w-[1200px] max-h-[90vh] rounded-2xl shadow-2xl flex flex-col overflow-hidden relative"
          >
            {/* Header Close Button */}
            <div className="absolute top-4 right-4 sm:top-6 sm:right-6 z-10">
              <button
                onClick={onClose}
                className="p-2 bg-white rounded-full text-gray-500 hover:text-gray-900 shadow-sm border border-gray-200 transition-all hover:bg-gray-50 flex items-center justify-center"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="flex-1 overflow-y-auto flex flex-col">
              {/* Search Area */}
              <div className="flex-none flex flex-col items-center justify-center pt-16 sm:pt-20 pb-10 px-4 sm:px-6">
                <h2 className="text-2xl sm:text-3xl md:text-4xl font-bold text-center text-gray-800 mb-8">
                  {t('search:title')}
                </h2>

                <div className="w-full max-w-3xl relative">
                  <input
                    ref={inputRef}
                    type="text"
                    value={keyword}
                    onChange={(e) => setKeyword(e.target.value)}
                    placeholder={t('search:placeholder')}
                    className="w-full h-16 pl-6 pr-16 text-lg sm:text-xl rounded-full border-2 border-gray-200 shadow-sm focus:border-fpt-orange focus:ring-0 outline-none transition-colors"
                    onKeyDown={(e) => {
                      if (e.key === 'Escape') onClose();
                    }}
                  />
                  <button
                    onClick={() => setDebouncedKeyword(keyword.trim())}
                    aria-label={t('search:searchAria')}
                    className="absolute right-3 top-1/2 -translate-y-1/2 w-10 h-10 bg-fpt-orange text-white rounded-full flex items-center justify-center hover:bg-orange-600 transition-colors"
                  >
                    {loading ? <Loader2 className="w-5 h-5 animate-spin" /> : <Search className="w-5 h-5" />}
                  </button>
                </div>

                {/* Live search results */}
                {debouncedKeyword && (
                  <div className="w-full max-w-3xl mt-6">
                    {loading ? (
                      <div className="flex justify-center py-8">
                        <Loader2 className="w-6 h-6 text-gray-400 animate-spin" />
                      </div>
                    ) : error ? (
                      <p className="text-center text-sm text-gray-500 py-6">
                        {t('search:error')}
                      </p>
                    ) : !hasResults ? (
                      <p className="text-center text-sm text-gray-500 py-6">
                        {t('search:noResult', { keyword: debouncedKeyword })}
                      </p>
                    ) : (
                      <div className="bg-white rounded-2xl border border-gray-200 shadow-sm divide-y divide-gray-100 overflow-hidden text-left">
                        {result!.news.length > 0 && (
                          <div className="p-4">
                            <p className="flex items-center gap-1.5 text-xs font-bold text-gray-400 uppercase tracking-wide mb-2">
                              <Newspaper className="w-3.5 h-3.5" /> {t('search:news')}
                            </p>
                            {result!.news.map((n) => (
                              <button
                                key={n.newsId}
                                onClick={() => goTo(`/news/${n.newsId}`)}
                                className="w-full text-left py-2 flex items-center justify-between gap-3 group"
                              >
                                <span className="text-sm font-semibold text-gray-800 group-hover:text-fpt-orange transition-colors line-clamp-1">
                                  {n.title}
                                </span>
                                <span className="shrink-0 text-xs text-gray-400">{formatDate(n.publishedAt)}</span>
                              </button>
                            ))}
                          </div>
                        )}

                        {result!.partners.length > 0 && (
                          <div className="p-4">
                            <p className="flex items-center gap-1.5 text-xs font-bold text-gray-400 uppercase tracking-wide mb-2">
                              <Building2 className="w-3.5 h-3.5" /> {t('search:partners')}
                            </p>
                            {result!.partners.map((p) => (
                              <button
                                key={p.partnerId}
                                onClick={() => goTo(`/partners/${p.publicSlug || p.partnerId}`)}
                                className="w-full text-left py-2 flex items-center justify-between gap-3 group"
                              >
                                <span className="text-sm font-semibold text-gray-800 group-hover:text-fpt-orange transition-colors line-clamp-1">
                                  {p.name}
                                </span>
                                {p.country && <span className="shrink-0 text-xs text-gray-400">{p.country}</span>}
                              </button>
                            ))}
                          </div>
                        )}

                        {result!.faqs.length > 0 && (
                          <div className="p-4">
                            <p className="flex items-center gap-1.5 text-xs font-bold text-gray-400 uppercase tracking-wide mb-2">
                              <HelpCircle className="w-3.5 h-3.5" /> {t('search:faq')}
                            </p>
                            {result!.faqs.map((f) => (
                              <button
                                key={f.faqId}
                                onClick={() => goTo('/faq')}
                                className="w-full text-left py-2 flex items-center justify-between gap-3 group"
                              >
                                <span className="text-sm font-semibold text-gray-800 group-hover:text-fpt-orange transition-colors line-clamp-1">
                                  {f.question}
                                </span>
                                <span className="shrink-0 text-xs text-gray-400">{f.faqTypeLabel}</span>
                              </button>
                            ))}
                          </div>
                        )}

                        {result!.campuses.length > 0 && (
                          <div className="p-4">
                            <p className="flex items-center gap-1.5 text-xs font-bold text-gray-400 uppercase tracking-wide mb-2">
                              <MapPin className="w-3.5 h-3.5" /> {t('search:campuses')}
                            </p>
                            {result!.campuses.map((c) => (
                              <div key={c.campusId} className="w-full py-2 flex items-center justify-between gap-3">
                                <span className="text-sm font-semibold text-gray-800 line-clamp-1">{c.name}</span>
                                <span className="shrink-0 text-xs text-gray-400">{c.campusCode}</span>
                              </div>
                            ))}
                          </div>
                        )}

                        <button
                          onClick={() => goTo(`/partners?search=${encodeURIComponent(debouncedKeyword)}`)}
                          className="w-full flex items-center justify-center gap-1.5 py-3 text-xs font-bold text-fpt-orange hover:bg-orange-50 transition-colors"
                        >
                          {t('search:morePartners')} <ArrowRight className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    )}
                  </div>
                )}

                {!debouncedKeyword && suggestions.length > 0 && (
                  <div className="w-full max-w-3xl mt-8">
                    <h3 className="text-sm uppercase tracking-wider text-gray-500 font-semibold mb-4 text-center">{t('search:suggestions')}</h3>
                    <div className="flex flex-wrap justify-center gap-3">
                      {suggestions.map((item) => (
                        <button
                          key={item}
                          onClick={() => handleChipClick(item)}
                          className="px-4 py-2 bg-white border border-gray-200 rounded-full text-sm text-gray-600 hover:text-fpt-orange hover:border-fpt-orange transition-colors"
                        >
                          {item}
                        </button>
                      ))}
                    </div>
                  </div>
                )}
              </div>

              {/* Footer Contacts Area */}
              <div className="mt-auto bg-[#fafafa] border-t border-gray-200 pt-16 pb-8 px-4 sm:px-8">
                <div className="max-w-[1400px] mx-auto grid grid-cols-1 md:grid-cols-3 lg:grid-cols-5 gap-8">
                  {translatedContacts.map((campus, index) => (
                    <div key={index} className="flex flex-col">
                      <h4 className="text-[16px] font-bold text-gray-900 flex items-center mb-4 uppercase tracking-wide">
                        <span className="w-[3px] h-4 bg-fpt-orange mr-2 inline-block"></span>
                        {campus.city}
                      </h4>
                      <div className="space-y-4 text-[14px] text-gray-600 leading-relaxed font-medium">
                        <div>
                          <p className="text-gray-500 font-bold mb-1">{t('search:address')}</p>
                          <p className="text-gray-800">{campus.address}</p>
                        </div>
                        <div>
                          <p className="text-gray-500 font-bold inline-block mr-1">{t('search:hotline')}</p>
                          <p className="inline-block text-gray-800">{campus.hotline}</p>
                        </div>
                        <div>
                          <p className="text-gray-500 font-bold inline-block mr-1">{t('search:email')}</p>
                          <p className="inline-block text-gray-800">{campus.email}</p>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>

                <div className="max-w-[1400px] mx-auto mt-16 pt-6 border-t border-gray-200 flex flex-col md:flex-row items-center justify-between gap-4">
                  <p className="text-[14px] font-bold text-gray-800">
                    {t('search:copyright')}
                  </p>
                  <div className="flex items-center gap-5">
                    {/* Facebook */}
                    <a href="#" className="text-fpt-orange hover:text-orange-600 transition-colors">
                      <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                        <path fillRule="evenodd" d="M22 12c0-5.523-4.477-10-10-10S2 6.477 2 12c0 4.991 3.657 9.128 8.438 9.878v-6.987h-2.54V12h2.54V9.797c0-2.506 1.492-3.89 3.777-3.89 1.094 0 2.238.195 2.238.195v2.46h-1.26c-1.243 0-1.63.771-1.63 1.562V12h2.773l-.443 2.89h-2.33v6.988C18.343 21.128 22 16.991 22 12z" clipRule="evenodd" />
                      </svg>
                    </a>
                    {/* Zalo */}
                    <a href="#" className="hidden md:flex text-fpt-orange hover:text-orange-600 transition-colors font-bold text-[18px] leading-none tracking-tighter">
                      Zalo
                    </a>
                    {/* Youtube */}
                    <a href="#" className="text-fpt-orange hover:text-orange-600 transition-colors">
                      <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 24 24">
                        <path d="M23.498 6.186a3.016 3.016 0 0 0-2.122-2.136C19.505 3.545 12 3.545 12 3.545s-7.505 0-9.377.505A3.017 3.017 0 0 0 .502 6.186C0 8.07 0 12 0 12s0 3.93.502 5.814a3.016 3.016 0 0 0 2.122 2.136c1.871.505 9.376.505 9.376.505s7.505 0 9.377-.505a3.015 3.015 0 0 0 2.122-2.136C24 15.93 24 12 24 12s0-3.93-.502-5.814zM9.545 15.568V8.432L15.818 12l-6.273 3.568z"/>
                      </svg>
                    </a>
                    {/* Tiktok */}
                    <a href="#" className="text-fpt-orange hover:text-orange-600 transition-colors">
                      <svg className="w-5 h-5" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M12.525.02c1.31-.02 2.61-.01 3.91-.01.08 1.53.63 3.09 1.75 4.17 1.12 1.11 2.7 1.62 4.24 1.79v4.03c-1.44-.05-2.89-.35-4.2-.97-.57-.26-1.1-.59-1.62-.93-.01 2.92.01 5.84-.02 8.75-.08 2.78-1.15 5.54-3.33 7.39-2.2 1.88-5.31 2.52-8.14 1.96-2.73-.54-5.14-2.58-6.15-5.18-1.03-2.68-.58-5.83 1.3-8.08 1.9-2.25 4.93-3.25 7.74-2.73v4.06c-1.37-.36-2.88-.16-4 .67-1.14.85-1.7 2.37-1.46 3.77.26 1.48 1.5 2.65 2.98 2.87 1.48.22 3.05-.38 3.96-1.48.91-1.1 1.25-2.6 1.22-4.08-.03-4.82-.01-9.65-.01-14.48h1.83z" />
                      </svg>
                    </a>
                  </div>
                </div>
              </div>
            </div>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
