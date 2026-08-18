/**
 * SearchPopup — public site-wide search.
 *
 * Four content groups: News, Partners, Gallery, FAQ (GET /api/public/search). Opening the popup
 * performs no network call at all; the only request is the search itself. Results are matched and
 * displayed in the current site language, and every row links to the exact content it matched.
 *
 * The campus contact block is kept at the foot of the scroll area (owner's call): it is the last
 * thing a visitor who found nothing can fall back on, so it sits below the results rather than
 * competing with them.
 */

import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Search, X, Loader2, Newspaper, Building2, HelpCircle, Images,
  ArrowRight, RefreshCw, Phone, Mail, MapPin, SearchX,
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { publicSearchApi } from '../../features/public-search/api/publicSearchApi';
import {
  normalizePublicSearchLanguage,
  publicSearchLocale,
} from '../../features/public-search/types/publicSearch.types';
import type { SearchInformationResult } from '../../features/public-search/types/publicSearch.types';

interface SearchPopupProps {
  isOpen: boolean;
  onClose: () => void;
}

const DEBOUNCE_MS = 350;
const PER_SECTION_LIMIT = 5;

/**
 * One colour per content group, so a long result list can be scanned by hue instead of by reading
 * every section label. News and Partners take the two brand colours; Gallery and FAQ take a green and
 * a violet that sit at a similar lightness, so no section shouts louder than the others.
 *
 * Written out as whole class strings on purpose — Tailwind only sees classes it can find literally in
 * the source, so a composed `bg-${colour}-500` would silently produce no CSS.
 */
type SectionTheme = {
  tile: string;
  count: string;
  card: string;
  bar: string;
  title: string;
  arrow: string;
  chip: string;
};

const THEMES: Record<'news' | 'partners' | 'gallery' | 'faq', SectionTheme> = {
  news: {
    tile: 'bg-[#004c91]/10 text-[#004c91]',
    count: 'bg-[#004c91]/10 text-[#004c91]',
    card: 'hover:border-[#004c91]/45 hover:shadow-[0_3px_18px_-6px_rgba(0,76,145,0.45)]',
    bar: 'bg-[#004c91]',
    title: 'group-hover:text-[#004c91]',
    arrow: 'group-hover:text-[#004c91]',
    chip: 'bg-[#004c91]/8 text-[#004c91] border-[#004c91]/20',
  },
  partners: {
    tile: 'bg-[#f37021]/12 text-[#f37021]',
    count: 'bg-[#f37021]/12 text-[#c8551a]',
    card: 'hover:border-[#f37021]/50 hover:shadow-[0_3px_18px_-6px_rgba(243,112,33,0.45)]',
    bar: 'bg-[#f37021]',
    title: 'group-hover:text-[#c8551a]',
    arrow: 'group-hover:text-[#f37021]',
    chip: 'bg-[#f37021]/8 text-[#c8551a] border-[#f37021]/25',
  },
  gallery: {
    tile: 'bg-emerald-500/12 text-emerald-600',
    count: 'bg-emerald-500/12 text-emerald-700',
    card: 'hover:border-emerald-400/60 hover:shadow-[0_3px_18px_-6px_rgba(16,185,129,0.45)]',
    bar: 'bg-emerald-500',
    title: 'group-hover:text-emerald-700',
    arrow: 'group-hover:text-emerald-600',
    chip: 'bg-emerald-500/8 text-emerald-700 border-emerald-500/25',
  },
  faq: {
    tile: 'bg-violet-500/12 text-violet-600',
    count: 'bg-violet-500/12 text-violet-700',
    card: 'hover:border-violet-400/60 hover:shadow-[0_3px_18px_-6px_rgba(139,92,246,0.45)]',
    bar: 'bg-violet-500',
    title: 'group-hover:text-violet-700',
    arrow: 'group-hover:text-violet-600',
    chip: 'bg-violet-500/8 text-violet-700 border-violet-500/25',
  },
};

/** The five campuses. Labels/addresses come from the `search` namespace, keyed by `key`. */
const CAMPUS_CONTACTS = [
  { key: 'Hanoi', hotline: '(024) 7300 5588', email: 'tuyensinhhanoi@fpt.edu.vn' },
  { key: 'Hcm', hotline: '(028) 7300 5588', email: 'tuyensinhhcm@fpt.edu.vn' },
  { key: 'Danang', hotline: '(0236) 730 0999', email: 'tuyensinhdanang@fpt.edu.vn' },
  { key: 'Cantho', hotline: '(0292) 730 3636', email: 'tuyensinhcantho@fpt.edu.vn' },
  { key: 'Quynhon', hotline: '(0256) 7300 999', email: 'tuyensinhquynhon@fpt.edu.vn' },
] as const;

/**
 * Highlights keyword occurrences as React nodes. Regex metacharacters in the user's keyword are
 * escaped so a keyword like "C++" is matched literally instead of blowing up the pattern, and the
 * match is rendered as elements — never via dangerouslySetInnerHTML, which would make search results
 * an injection surface for any content an author can type.
 */
function Highlight({ text, keyword }: { text: string; keyword: string }) {
  const trimmed = keyword.trim();
  if (!trimmed) return <>{text}</>;

  const escaped = trimmed.replace(/[.*+?^${}()|[\]\\-]/g, '\\$&');
  const parts = text.split(new RegExp(`(${escaped})`, 'gi'));
  const lowered = trimmed.toLowerCase();

  return (
    <>
      {parts.map((part, i) =>
        part.toLowerCase() === lowered ? (
          <mark key={i} className="bg-[#f37021]/20 text-[#b8480f] rounded-[3px] px-0.5 font-semibold">
            {part}
          </mark>
        ) : (
          <React.Fragment key={i}>{part}</React.Fragment>
        ),
      )}
    </>
  );
}

/** Section label: coloured icon tile + name + how many rows are under it. */
function SectionHeading({
  icon: Icon, label, count, theme,
}: {
  icon: React.ComponentType<{ className?: string }>;
  label: string; count: number; theme: SectionTheme;
}) {
  return (
    <div className="flex items-center gap-2.5 mb-2.5">
      <span className={`w-7 h-7 rounded-lg flex items-center justify-center ${theme.tile}`}>
        <Icon className="w-4 h-4" />
      </span>
      <span className="text-[11px] font-bold text-slate-600 uppercase tracking-[0.08em]">{label}</span>
      <span className={`text-[11px] font-bold rounded-full px-2 py-0.5 leading-none ${theme.count}`}>
        {count}
      </span>
      <span className="flex-1 h-px bg-slate-200" />
    </div>
  );
}

/** Shared shell for every result row: white card, section-coloured hover, accent bar that wipes in. */
function ResultCard({
  onClick, theme, children,
}: { onClick: () => void; theme: SectionTheme; children: React.ReactNode }) {
  return (
    <button
      onClick={onClick}
      className={`relative w-full text-left bg-white rounded-xl border border-slate-200/80 pl-4 pr-3.5 py-3
                  flex items-center gap-3 overflow-hidden transition-all duration-150 group
                  focus:outline-none focus-visible:ring-2 focus-visible:ring-offset-1 focus-visible:ring-slate-400
                  ${theme.card}`}
    >
      <span
        className={`absolute left-0 top-0 bottom-0 w-[3px] origin-center scale-y-0
                    group-hover:scale-y-100 transition-transform duration-200 ${theme.bar}`}
      />
      <span className="min-w-0 flex-1">{children}</span>
      <ArrowRight
        className={`shrink-0 w-4 h-4 text-slate-300 group-hover:translate-x-0.5 transition-all ${theme.arrow}`}
      />
    </button>
  );
}

function ResultSkeleton() {
  return (
    <div className="space-y-2.5">
      {Array.from({ length: 4 }).map((_, i) => (
        <div key={i} className="bg-white rounded-xl border border-slate-200/80 px-4 py-3.5">
          <div className="h-3.5 bg-slate-100 rounded-full w-2/3 mb-2.5 animate-pulse" />
          <div className="h-2.5 bg-slate-100 rounded-full w-1/3 animate-pulse" />
        </div>
      ))}
    </div>
  );
}

/** Campus contacts. Phone and email are real links — they are contact details, so let them dial/compose. */
function ContactFooter() {
  const { t } = useTranslation(['search']);

  return (
    <div className="mt-7 -mx-4 sm:-mx-6 -mb-5 px-4 sm:px-6 pt-6 pb-5 bg-gradient-to-b from-white to-slate-50 border-t border-slate-200">
      <div className="flex items-center gap-2.5 mb-5">
        <span className="w-7 h-7 rounded-lg bg-[#f37021]/12 text-[#f37021] flex items-center justify-center">
          <MapPin className="w-4 h-4" />
        </span>
        <span className="text-[11px] font-bold text-slate-600 uppercase tracking-[0.08em]">
          {t('search:contactTitle')}
        </span>
        <span className="flex-1 h-px bg-slate-200" />
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-x-6 gap-y-5">
        {CAMPUS_CONTACTS.map((campus) => (
          <div key={campus.key} className="min-w-0">
            <h4 className="flex items-center text-[12px] font-bold text-slate-900 uppercase tracking-wide mb-2">
              <span className="w-[3px] h-3.5 bg-gradient-to-b from-[#f37021] to-[#004c91] rounded-full mr-2 shrink-0" />
              {t(`search:contacts${campus.key}`)}
            </h4>

            <p className="text-[12px] text-slate-500 leading-relaxed mb-2 line-clamp-2">
              {t(`search:contacts${campus.key}Address`)}
            </p>

            <div className="space-y-1">
              <a
                href={`tel:${campus.hotline.replace(/[^\d+]/g, '')}`}
                className="flex items-center gap-1.5 text-[12px] text-slate-600 hover:text-[#f37021] transition-colors"
              >
                <Phone className="w-3.5 h-3.5 shrink-0 text-[#f37021]/70" />
                <span className="font-normal">{campus.hotline}</span>
              </a>
              <a
                href={`mailto:${campus.email}`}
                className="flex items-center gap-1.5 text-[12px] text-slate-600 hover:text-[#004c91] transition-colors min-w-0"
              >
                <Mail className="w-3.5 h-3.5 shrink-0 text-[#004c91]/70" />
                <span className="truncate">{campus.email}</span>
              </a>
            </div>
          </div>
        ))}
      </div>

      <p className="mt-6 pt-4 border-t border-slate-200/70 text-[11px] text-slate-400 text-center">
        {t('search:copyright')}
      </p>
    </div>
  );
}

export function SearchPopup({ isOpen, onClose }: SearchPopupProps) {
  const { t, i18n } = useTranslation(['search']);
  const navigate = useNavigate();
  const inputRef = useRef<HTMLInputElement>(null);

  const language = normalizePublicSearchLanguage(i18n.resolvedLanguage ?? i18n.language);
  const locale = publicSearchLocale(language);

  const [keyword, setKeyword] = useState('');
  const [debouncedKeyword, setDebouncedKeyword] = useState('');
  const [result, setResult] = useState<SearchInformationResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);
  // Bumped by "Try again" to re-run the effect with an unchanged keyword/language.
  const [retryToken, setRetryToken] = useState(0);

  const dateFormatter = useMemo(
    () =>
      new Intl.DateTimeFormat(
        locale,
        language === 'en'
          ? { month: 'short', day: 'numeric', year: 'numeric' }   // Aug 5, 2026
          : { day: '2-digit', month: '2-digit', year: 'numeric' }, // 05/08/2026
      ),
    [locale, language],
  );

  const formatDate = useCallback(
    (iso?: string | null) => {
      if (!iso) return '';
      const parsed = new Date(iso);
      return Number.isNaN(parsed.getTime()) ? '' : dateFormatter.format(parsed);
    },
    [dateFormatter],
  );

  useEffect(() => {
    if (isOpen) {
      const focusTimer = setTimeout(() => inputRef.current?.focus(), 60);
      document.body.style.overflow = 'hidden';
      return () => {
        clearTimeout(focusTimer);
        document.body.style.overflow = '';
      };
    }
    document.body.style.overflow = '';
    // Re-opening starts clean rather than flashing the previous search.
    setKeyword('');
    setDebouncedKeyword('');
    setResult(null);
    setError(false);
    setLoading(false);
    return undefined;
  }, [isOpen]);

  // Escape closes from anywhere in the popup, not only while the input has focus.
  useEffect(() => {
    if (!isOpen) return undefined;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [isOpen, onClose]);

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedKeyword(keyword.trim()), DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [keyword]);

  /**
   * The request identity is (keyword, language) — both are in the dependency list, so switching
   * language tears this effect down and starts a fresh one. The abort on teardown is what stops a
   * slow Vietnamese response from landing after the English request and overwriting it; clearing the
   * result up front is what stops the old language's rows from being shown while the new one loads.
   */
  useEffect(() => {
    if (!debouncedKeyword) {
      setResult(null);
      setError(false);
      setLoading(false);
      return undefined;
    }

    const controller = new AbortController();
    setResult(null);
    setError(false);
    setLoading(true);

    publicSearchApi
      .search({ keyword: debouncedKeyword, limit: PER_SECTION_LIMIT, languageCode: language }, controller.signal)
      .then((data) => {
        if (controller.signal.aborted) return;
        setResult(data);
        setLoading(false);
      })
      .catch(() => {
        if (controller.signal.aborted) return; // superseded, not failed
        setError(true);
        setResult(null);
        setLoading(false);
      });

    return () => controller.abort();
  }, [debouncedKeyword, language, retryToken]);

  const goTo = useCallback(
    (path: string) => {
      onClose();
      navigate(path);
    },
    [navigate, onClose],
  );

  const runSearchNow = useCallback(() => setDebouncedKeyword(keyword.trim()), [keyword]);

  const clearKeyword = useCallback(() => {
    setKeyword('');
    setDebouncedKeyword('');
    setResult(null);
    setError(false);
    inputRef.current?.focus();
  }, []);

  const hasResults = !!result && result.totalCount > 0;
  const scopes = [
    { icon: Newspaper, label: t('search:news'), theme: THEMES.news },
    { icon: Building2, label: t('search:partners'), theme: THEMES.partners },
    { icon: Images, label: t('search:gallery'), theme: THEMES.gallery },
    { icon: HelpCircle, label: t('search:faq'), theme: THEMES.faq },
  ];

  return (
    <AnimatePresence>
      {isOpen && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.15 }}
          className="fixed inset-0 z-[100] bg-slate-900/60 backdrop-blur-[3px] flex items-start justify-center p-3 sm:p-6 md:pt-[7vh]"
          onClick={onClose}
        >
          <motion.div
            initial={{ opacity: 0, scale: 0.97, y: -8 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.97, y: -8 }}
            transition={{ duration: 0.18, ease: [0.16, 1, 0.3, 1] }}
            onClick={(e) => e.stopPropagation()}
            role="dialog"
            aria-modal="true"
            aria-label={t('search:title')}
            className="bg-white w-full max-w-4xl max-h-[92vh] sm:max-h-[85vh] rounded-2xl sm:rounded-[20px]
                       shadow-[0_24px_60px_-12px_rgba(15,23,42,0.45)] ring-1 ring-slate-900/5
                       flex flex-col overflow-hidden"
          >
            {/* ── Navy header: title, close, and the search field floating on it ── */}
            <div className="relative flex-none bg-gradient-to-br from-[#00396e] via-[#004c91] to-[#0a5fa8] px-4 sm:px-6 pt-4 sm:pt-5 pb-5 overflow-hidden">
              {/* Warm glow in the corner so the navy does not read as a flat slab. */}
              <div className="pointer-events-none absolute -top-20 -right-12 w-56 h-56 rounded-full bg-[#f37021]/25 blur-3xl" />
              <div className="pointer-events-none absolute -bottom-24 -left-16 w-56 h-56 rounded-full bg-sky-400/15 blur-3xl" />

              <div className="relative flex items-center justify-between gap-3 mb-3.5">
                <h2 className="text-[15px] sm:text-base font-bold text-white tracking-tight">
                  {t('search:title')}
                </h2>
                <button
                  onClick={onClose}
                  aria-label={t('search:closeAria')}
                  className="shrink-0 w-8 h-8 rounded-lg flex items-center justify-center text-white/70
                             hover:text-white hover:bg-white/15 transition-colors"
                >
                  <X className="w-[18px] h-[18px]" />
                </button>
              </div>

              <div className="relative flex items-center rounded-xl bg-white shadow-lg shadow-slate-900/15
                              ring-1 ring-white/20 focus-within:ring-4 focus-within:ring-[#f37021]/40 transition-all">
                <span className="pl-3.5 pr-2 shrink-0">
                  {loading
                    ? <Loader2 className="w-[18px] h-[18px] animate-spin text-[#f37021]" />
                    : <Search className="w-[18px] h-[18px] text-[#004c91]" />}
                </span>
                <input
                  ref={inputRef}
                  type="text"
                  value={keyword}
                  onChange={(e) => setKeyword(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') runSearchNow();
                  }}
                  placeholder={t('search:placeholder')}
                  aria-label={t('search:searchAria')}
                  // text-base (16px) on purpose: anything smaller makes iOS Safari zoom on focus.
                  className="flex-1 min-w-0 h-12 bg-transparent text-base text-slate-900
                             placeholder:text-slate-400 outline-none"
                />
                {keyword && (
                  <button
                    onClick={clearKeyword}
                    aria-label={t('search:clear')}
                    className="shrink-0 w-7 h-7 mr-1.5 rounded-full flex items-center justify-center
                               text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors"
                  >
                    <X className="w-3.5 h-3.5" />
                  </button>
                )}
                <kbd className="hidden sm:flex shrink-0 items-center gap-1 mr-3 px-1.5 py-0.5 rounded-md
                                border border-slate-200 bg-slate-50 text-[10px] font-semibold text-slate-400">
                  ESC
                </kbd>
              </div>
            </div>

            {/* ── Only this area scrolls ── */}
            <div className="flex-1 overflow-y-auto overscroll-contain px-4 sm:px-6 py-5 bg-slate-50/70">
              {!debouncedKeyword ? (
                <div className="text-center pt-8 pb-4">
                  <div className="w-14 h-14 mx-auto mb-4 rounded-2xl bg-gradient-to-br from-[#004c91] to-[#0a5fa8] shadow-lg shadow-[#004c91]/25 flex items-center justify-center">
                    <Search className="w-6 h-6 text-white" />
                  </div>
                  <p className="text-sm text-slate-500 max-w-md mx-auto mb-5">{t('search:initialHint')}</p>
                  {/* What is searchable, in each section's own colour — informational, not clickable. */}
                  <div className="flex flex-wrap justify-center gap-2">
                    {scopes.map(({ icon: Icon, label, theme }) => (
                      <span
                        key={label}
                        className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full border text-xs font-semibold ${theme.chip}`}
                      >
                        <Icon className="w-3.5 h-3.5" /> {label}
                      </span>
                    ))}
                  </div>
                </div>
              ) : loading ? (
                <ResultSkeleton />
              ) : error ? (
                <div className="text-center py-10">
                  <div className="w-14 h-14 mx-auto mb-4 rounded-2xl bg-gradient-to-br from-red-500 to-rose-600 shadow-lg shadow-red-500/25 flex items-center justify-center">
                    <RefreshCw className="w-6 h-6 text-white" />
                  </div>
                  <p className="text-sm text-slate-600 mb-4">{t('search:error')}</p>
                  <button
                    onClick={() => setRetryToken((n) => n + 1)}
                    className="inline-flex items-center gap-2 px-4 py-2.5 bg-[#004c91] text-white text-sm
                               font-bold rounded-xl shadow-md shadow-[#004c91]/25 hover:bg-[#003b70] transition-colors"
                  >
                    <RefreshCw className="w-4 h-4" /> {t('search:retry')}
                  </button>
                </div>
              ) : !hasResults ? (
                <div className="text-center py-10 px-4">
                  <div className="w-14 h-14 mx-auto mb-4 rounded-2xl bg-slate-200/70 flex items-center justify-center">
                    <SearchX className="w-6 h-6 text-slate-500" />
                  </div>
                  <p className="text-sm font-normal text-slate-700 mb-1">
                    {t('search:noResult', { keyword: debouncedKeyword })}
                  </p>
                  <p className="text-sm text-slate-500">{t('search:noResultHint')}</p>
                </div>
              ) : (
                <div className="space-y-6">
                  <p className="text-xs font-normal text-slate-500">
                    {t('search:resultCount', { count: result!.totalCount, keyword: debouncedKeyword })}
                  </p>

                  {/* ── News ── */}
                  {result!.news.length > 0 && (
                    <section>
                      <SectionHeading icon={Newspaper} label={t('search:news')} count={result!.news.length} theme={THEMES.news} />
                      <div className="space-y-2">
                        {result!.news.map((n) => (
                          <ResultCard key={n.newsId} theme={THEMES.news} onClick={() => goTo(`/news/${n.newsId}`)}>
                            <span className="flex items-start justify-between gap-3">
                              <span className={`text-sm font-semibold text-slate-800 transition-colors line-clamp-2 ${THEMES.news.title}`}>
                                <Highlight text={n.title} keyword={debouncedKeyword} />
                              </span>
                              {n.publishedAt && (
                                <span className="shrink-0 text-[11px] text-slate-400 whitespace-nowrap pt-0.5">
                                  {formatDate(n.publishedAt)}
                                </span>
                              )}
                            </span>
                            {n.summary && (
                              <span className="mt-1 text-xs text-slate-500 line-clamp-1">
                                <Highlight text={n.summary} keyword={debouncedKeyword} />
                              </span>
                            )}
                          </ResultCard>
                        ))}
                      </div>
                    </section>
                  )}

                  {/* ── Partners ── */}
                  {result!.partners.length > 0 && (
                    <section>
                      <SectionHeading icon={Building2} label={t('search:partners')} count={result!.partners.length} theme={THEMES.partners} />
                      <div className="space-y-2">
                        {result!.partners.map((p) => (
                          <ResultCard
                            key={p.partnerId}
                            theme={THEMES.partners}
                            onClick={() => goTo(`/partners/${p.publicSlug || p.partnerId}`)}
                          >
                            <span className="flex items-start justify-between gap-3">
                              <span className={`text-sm font-semibold text-slate-800 transition-colors line-clamp-2 ${THEMES.partners.title}`}>
                                <Highlight text={p.name} keyword={debouncedKeyword} />
                              </span>
                              {p.country && (
                                <span className={`shrink-0 text-[11px] font-semibold rounded-full px-2 py-0.5 whitespace-nowrap ${THEMES.partners.count}`}>
                                  {p.country}
                                </span>
                              )}
                            </span>
                            {p.descriptionPreview && (
                              <span className="mt-1 text-xs text-slate-500 line-clamp-1">
                                <Highlight text={p.descriptionPreview} keyword={debouncedKeyword} />
                              </span>
                            )}
                          </ResultCard>
                        ))}
                      </div>

                      {/*
                        Belongs to the Partner section, and only appears when there are partner matches
                        the popup could not show. As a footer of the whole result list it used to render
                        for a news-only search, promising "more partners" that did not exist.
                      */}
                      {result!.hasMore.partners && (
                        <button
                          onClick={() => goTo(`/partners?search=${encodeURIComponent(debouncedKeyword)}`)}
                          className="mt-2 w-full flex items-center justify-center gap-1.5 py-2.5 text-xs font-bold
                                     text-white bg-gradient-to-r from-[#f37021] to-[#e05f12] rounded-xl
                                     shadow-md shadow-[#f37021]/25 hover:brightness-105 transition-all"
                        >
                          {t('search:viewMore')} <ArrowRight className="w-3.5 h-3.5" />
                        </button>
                      )}
                    </section>
                  )}

                  {/* ── Gallery ── */}
                  {result!.galleries.length > 0 && (
                    <section>
                      <SectionHeading icon={Images} label={t('search:gallery')} count={result!.galleries.length} theme={THEMES.gallery} />
                      <div className="space-y-2">
                        {result!.galleries.map((g) => (
                          <ResultCard
                            key={g.galleryItemId}
                            theme={THEMES.gallery}
                            onClick={() =>
                              goTo(
                                `/visit-fptu/${g.campusCode.toLowerCase()}?locationId=${g.locationId}&itemId=${g.galleryItemId}`,
                              )
                            }
                          >
                            <span className="flex items-center gap-3">
                              <span className="relative shrink-0 w-16 h-12 rounded-lg bg-emerald-500/10 overflow-hidden flex items-center justify-center">
                                <Images className="w-4 h-4 text-emerald-500/60" />
                                {g.thumbnailUrl && (
                                  // Sits on top of the placeholder icon and removes itself if the file
                                  // will not load, so a dead thumbnail shows the icon, not a blank box.
                                  <img
                                    src={g.thumbnailUrl}
                                    alt=""
                                    loading="lazy"
                                    onError={(e) => { e.currentTarget.style.display = 'none'; }}
                                    className="absolute inset-0 w-full h-full object-cover"
                                  />
                                )}
                              </span>
                              <span className="min-w-0 flex-1">
                                <span className={`block text-sm font-semibold text-slate-800 transition-colors line-clamp-1 ${THEMES.gallery.title}`}>
                                  <Highlight text={g.title} keyword={debouncedKeyword} />
                                </span>
                                {/* Wraps on narrow screens rather than forcing the row to scroll sideways. */}
                                <span className="mt-1 flex flex-wrap items-center gap-x-1.5 gap-y-0.5 text-[11px] text-slate-500">
                                  <span>{g.campusName}</span>
                                  <span className="text-emerald-400">·</span>
                                  <span>{g.areaName}</span>
                                  <span className="text-emerald-400">·</span>
                                  <span className="font-normal text-emerald-700">{g.locationName}</span>
                                </span>
                              </span>
                            </span>
                          </ResultCard>
                        ))}
                      </div>
                    </section>
                  )}

                  {/* ── FAQ ── */}
                  {result!.faqs.length > 0 && (
                    <section>
                      <SectionHeading icon={HelpCircle} label={t('search:faq')} count={result!.faqs.length} theme={THEMES.faq} />
                      <div className="space-y-2">
                        {result!.faqs.map((f) => (
                          <ResultCard key={f.faqId} theme={THEMES.faq} onClick={() => goTo(`/faq?faqId=${f.faqId}`)}>
                            {/*
                              Stacked on mobile: the FAQ type label is the longest badge in the popup
                              ("Quản lý đoàn tiếp khách"), and beside the question it squeezes it to a
                              couple of words on a phone.
                            */}
                            <span className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-1.5 sm:gap-3">
                              <span className={`text-sm font-semibold text-slate-800 transition-colors line-clamp-2 ${THEMES.faq.title}`}>
                                <Highlight text={f.question} keyword={debouncedKeyword} />
                              </span>
                              <span className={`self-start shrink-0 text-[11px] font-semibold rounded-full px-2 py-0.5 whitespace-nowrap ${THEMES.faq.count}`}>
                                {f.faqTypeLabel}
                              </span>
                            </span>
                            {f.answerPreview && (
                              <span className="mt-1 text-xs text-slate-500 line-clamp-1">
                                <Highlight text={f.answerPreview} keyword={debouncedKeyword} />
                              </span>
                            )}
                          </ResultCard>
                        ))}
                      </div>
                    </section>
                  )}
                </div>
              )}

              <ContactFooter />
            </div>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
