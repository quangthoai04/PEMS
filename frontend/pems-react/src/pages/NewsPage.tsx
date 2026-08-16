/**
 * Trang NewsPage (Public)
 * Trang tin tức chính thức công cộng của trường — phong cách International Newsroom.
 * Chỉ hiển thị bài viết đã PUBLISHED — dữ liệu thật từ GET /api/public/news.
 */

import React, { useEffect, useMemo, useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import {
  Calendar,
  Newspaper,
  RefreshCw,
  Search,
  MapPin,
  Compass,
  ArrowRight,
} from 'lucide-react';
import { Link } from 'react-router-dom';
import { publicContentApi } from '../features/public-content/api/publicContentApi';
import { authenticationApi } from '../features/authentication/api/authenticationApi';
import { PublicNewsListItem } from '../features/public-content/types/publicContent.types';
import { CampusOption } from '../features/authentication/types/authentication.types';
import { resolveFileUrl } from '../shared/utils/resolveFileUrl';
import { useTranslation } from 'react-i18next';
import { formatLocalizedDate, type UiLanguage } from '../shared/utils/vietnamTime';

const LATEST_INITIAL_SIZE = 3;
const LATEST_LOAD_MORE_STEP = 3;
const TOP_STORIES_SIZE = 3;
// However many articles are marked featured should rotate through — 50 is the public news
// endpoint's own MaxPageSize clamp (ViewNewsQuery.cs), not an arbitrary frontend limit.
const HERO_ROTATE_SIZE = 50;
const HERO_ROTATE_INTERVAL_MS = 5000;
const CAMPUS_STORIES_INITIAL_SIZE = 3;
const CAMPUS_STORIES_LOAD_MORE_STEP = 3;

type TypeFilter = 'all' | 'featured' | 'general';
type SortOrder = 'latest' | 'oldest';

// We'll generate TYPE_OPTIONS using useTranslation inside the component or via a function.

function formatDate(iso: string | null | undefined, language: UiLanguage): string {
  return formatLocalizedDate(iso, language, { fallback: '' });
}

const fadeUp = {
  hidden: { opacity: 0, y: 24 },
  visible: { opacity: 1, y: 0 },
};

function FadeSection({
  children,
  className,
  delay = 0,
}: {
  children: React.ReactNode;
  className?: string;
  delay?: number;
}) {
  return (
    <motion.section
      className={className}
      initial="hidden"
      whileInView="visible"
      viewport={{ once: true, margin: '-60px' }}
      variants={fadeUp}
      transition={{ duration: 0.5, delay, ease: 'easeOut' }}
    >
      {children}
    </motion.section>
  );
}

function ArticleImage({
  item,
  className,
}: {
  item: Pick<PublicNewsListItem, 'coverUrl' | 'title'>;
  className: string;
}) {
  const [failed, setFailed] = useState(false);
  const src = !failed && item.coverUrl ? resolveFileUrl(item.coverUrl) : null;

  if (!src) {
    return (
      <div className={`${className} flex items-center justify-center bg-gradient-to-br from-[#004c91] to-[#00284d]`}>
        <span className="text-white/90 font-bold tracking-wide text-sm sm:text-base uppercase">FPT University</span>
      </div>
    );
  }

  return (
    <img
      src={src}
      alt={item.title}
      loading="lazy"
      onError={() => setFailed(true)}
      className={className}
    />
  );
}

function CampusBadge({ item }: { item: PublicNewsListItem }) {
  if (!item.campusName) return null;
  return (
    <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-[#004c91]/8 text-[#004c91] text-[11px] font-bold uppercase tracking-wide">
      <MapPin className="w-3 h-3" />
      {item.campusName}
    </span>
  );
}

function SkeletonCard({ variant = 'grid' }: { variant?: 'grid' | 'row' | 'hero'; key?: React.Key }) {
  if (variant === 'hero') {
    return (
      <div className="animate-pulse">
        <div className="aspect-[16/9] sm:aspect-[21/9] rounded-2xl bg-slate-200/70 skeleton-shimmer" />
        <div className="mt-6 h-8 bg-slate-200/70 rounded w-3/4 skeleton-shimmer" />
        <div className="mt-3 h-4 bg-slate-200/70 rounded w-full skeleton-shimmer" />
        <div className="mt-2 h-4 bg-slate-200/70 rounded w-2/3 skeleton-shimmer" />
      </div>
    );
  }
  if (variant === 'row') {
    return (
      <div className="flex gap-4 items-start animate-pulse">
        <div className="w-[100px] h-[100px] shrink-0 rounded-lg bg-slate-200/70 skeleton-shimmer" />
        <div className="flex-1 space-y-2 pt-1">
          <div className="h-4 bg-slate-200/70 rounded w-full skeleton-shimmer" />
          <div className="h-4 bg-slate-200/70 rounded w-2/3 skeleton-shimmer" />
        </div>
      </div>
    );
  }
  return (
    <div className="animate-pulse">
      <div className="aspect-[16/9] rounded-xl bg-slate-200/70 skeleton-shimmer" />
      <div className="mt-4 h-5 bg-slate-200/70 rounded w-11/12 skeleton-shimmer" />
      <div className="mt-2 h-4 bg-slate-200/70 rounded w-full skeleton-shimmer" />
      <div className="mt-2 h-4 bg-slate-200/70 rounded w-3/4 skeleton-shimmer" />
    </div>
  );
}

function EmptyState({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <div className="py-16 text-center">
      <div className="w-16 h-16 bg-slate-100 text-slate-400 rounded-full flex items-center justify-center mx-auto mb-4">
        <Newspaper className="w-8 h-8" />
      </div>
      <p className="text-slate-700 font-bold mb-1">{title}</p>
      {subtitle && <p className="text-slate-500 text-sm">{subtitle}</p>}
    </div>
  );
}

/* ───────────────────────── Hero ───────────────────────── */

function NewsroomHero({ item, loading }: { item: PublicNewsListItem | null; loading: boolean }) {
  const { t, i18n } = useTranslation(['news']);
  if (loading) {
    return (
      <div className="relative left-1/2 right-1/2 -mx-[50vw] w-screen">
        <div className="max-w-[1600px] mx-auto px-8 sm:px-12 lg:px-16">
          <SkeletonCard variant="hero" />
        </div>
      </div>
    );
  }
  if (!item) return null;

  return (
    <div className="relative left-1/2 right-1/2 -mx-[50vw] w-screen">
      <AnimatePresence mode="wait">
        <motion.div
          key={item.newsId}
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.6, ease: 'easeInOut' }}
        >
          <Link to={`/news/${item.newsId}`} className="group block">
            <div className="relative aspect-[16/10] sm:aspect-[21/9] overflow-hidden">
              <ArticleImage
                item={item}
                className="w-full h-full object-cover transition-transform duration-700 group-hover:scale-105"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-black/20 to-transparent" />
              <div className="absolute inset-x-0 bottom-0 px-8 sm:px-12 lg:px-16 pb-5 sm:pb-10 lg:pb-14 max-w-[1600px] mx-auto">
                <div className="flex items-center gap-3 mb-3">
                  {item.isFeatured && (
                    <span className="px-3 py-1 rounded-full bg-[#f37021] text-white text-xs font-bold uppercase tracking-wide">
                      {t('news:card.featured')}
                    </span>
                  )}
                  {item.publishedAt && (
                    <span className="flex items-center gap-1.5 text-white/80 text-sm">
                      <Calendar className="w-3.5 h-3.5" />
                      {formatDate(item.publishedAt, i18n.language as UiLanguage)}
                    </span>
                  )}
                </div>
                <h1 className="text-xl sm:text-3xl lg:text-4xl font-bold text-white leading-tight mb-3 max-w-3xl group-hover:text-orange-100 transition-colors">
                  {item.title}
                </h1>
                {item.summary && (
                  <p className="hidden sm:block text-white/85 text-base leading-relaxed max-w-2xl line-clamp-2 mb-4">
                    {item.summary}
                  </p>
                )}
                <span className="inline-flex items-center gap-1.5 text-white font-bold text-sm group-hover:gap-2.5 transition-all">
                  {t('news:card.readMore')} <ArrowRight className="w-4 h-4" />
                </span>
              </div>
            </div>
          </Link>
        </motion.div>
      </AnimatePresence>
    </div>
  );
}

/* ───────────────────────── Filter Bar ───────────────────────── */

function FilterBar({
  keyword,
  onKeyword,
  type,
  onType,
  campusId,
  onCampus,
  campuses,
  sort,
  onSort,
}: {
  keyword: string;
  onKeyword: (v: string) => void;
  type: TypeFilter;
  onType: (v: TypeFilter) => void;
  campusId: number | undefined;
  onCampus: (v: number | undefined) => void;
  campuses: CampusOption[];
  sort: SortOrder;
  onSort: (v: SortOrder) => void;
}) {
  const { t } = useTranslation(['news']);
  
  const TYPE_OPTIONS: { value: TypeFilter; label: string }[] = [
    { value: 'all', label: t('news:types.all') },
    { value: 'featured', label: t('news:types.featured') },
    { value: 'general', label: t('news:types.general') },
  ];

  return (
    <FadeSection className="mb-10">
      <div className="flex flex-col lg:flex-row lg:items-center gap-4">
        {/* Search */}
        <div className="relative flex-1 lg:max-w-sm">
          <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <input
            type="text"
            value={keyword}
            onChange={(e) => onKeyword(e.target.value)}
            placeholder={t('news:filter.searchPlaceholder')}
            className="w-full h-11 pl-10 pr-4 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-colors"
          />
        </div>

        {/* Type chips — scrollable on mobile */}
        <div className="flex gap-2 overflow-x-auto no-scrollbar -mx-1 px-1 lg:mx-0 lg:px-0">
          {TYPE_OPTIONS.map((opt) => {
            const active = type === opt.value;
            return (
              <button
                key={opt.value}
                onClick={() => onType(opt.value)}
                className={`relative shrink-0 h-11 px-4 rounded-xl text-sm font-semibold whitespace-nowrap transition-colors ${
                  active
                    ? 'bg-[#004c91] text-white'
                    : 'bg-white text-slate-600 border border-slate-200 hover:border-[#004c91]/40 hover:text-[#004c91]'
                }`}
              >
                {opt.label}
                {active && (
                  <motion.span
                    layoutId="type-filter-underline"
                    className="absolute left-4 right-4 -bottom-1.5 h-0.5 bg-[#f37021] rounded-full"
                  />
                )}
              </button>
            );
          })}
        </div>

        <div className="flex gap-3 lg:ml-auto">
          {/* Campus filter */}
          {campuses.length > 0 && (
            <select
              value={campusId ?? ''}
              onChange={(e) => onCampus(e.target.value ? Number(e.target.value) : undefined)}
              className="h-11 px-3 rounded-xl border border-slate-200 bg-white text-sm text-slate-700 focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-colors"
            >
              <option value="">{t('news:filter.allCampuses')}</option>
              {campuses.map((c) => (
                <option key={c.campusId} value={c.campusId}>
                  {c.campusName}
                </option>
              ))}
            </select>
          )}

          {/* Sort */}
          <select
            value={sort}
            onChange={(e) => onSort(e.target.value as SortOrder)}
            className="h-11 px-3 rounded-xl border border-slate-200 bg-white text-sm text-slate-700 focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-colors"
          >
            <option value="latest">{t('news:filter.sortLatest')}</option>
            <option value="oldest">{t('news:filter.sortOldest')}</option>
          </select>
        </div>
      </div>
    </FadeSection>
  );
}

/* ───────────────────────── Editorial Top Stories ───────────────────────── */

function TopStories({ items, loading }: { items: PublicNewsListItem[]; loading: boolean }) {
  const { t, i18n } = useTranslation(['news']);
  if (loading) {
    return (
      <FadeSection className="mb-14">
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <SkeletonCard />
          <div className="flex flex-col gap-6">
            <SkeletonCard />
            <SkeletonCard />
          </div>
        </div>
      </FadeSection>
    );
  }
  if (items.length === 0) return null;

  const [big, ...rest] = items;
  const small = rest.slice(0, 2);

  return (
    <FadeSection className="mb-14">
      <h2 className="text-lg font-bold text-slate-900 uppercase tracking-wide mb-5 flex items-center gap-2">
        <span className="w-1.5 h-5 bg-[#f37021] rounded-full" /> {t('news:sections.topStories')}
      </h2>
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Link to={`/news/${big.newsId}`} className="group block">
          <div className="aspect-[16/10] rounded-xl overflow-hidden ring-1 ring-black/5 mb-4">
            <ArticleImage item={big} className="w-full h-full object-cover transition-transform duration-500 group-hover:scale-105" />
          </div>
          <div className="flex items-center gap-2 mb-2">
            <CampusBadge item={big} />
            {big.publishedAt && (
              <span className="flex items-center gap-1 text-slate-500 text-xs">
                <Calendar className="w-3.5 h-3.5" /> {formatDate(big.publishedAt, i18n.language as UiLanguage)}
              </span>
            )}
          </div>
          <h3 className="text-lg font-bold text-slate-900 leading-snug group-hover:text-[#004c91] transition-colors line-clamp-2">
            {big.title}
          </h3>
          {big.summary && <p className="mt-2 text-sm text-slate-600 line-clamp-2">{big.summary}</p>}
        </Link>

        <div className="flex flex-col gap-6">
          {small.map((item) => (
            <Link to={`/news/${item.newsId}`} key={item.newsId} className="group flex gap-4 items-start">
              <div className="w-32 sm:w-40 shrink-0 aspect-[4/3] rounded-lg overflow-hidden ring-1 ring-black/5">
                <ArticleImage item={item} className="w-full h-full object-cover transition-transform duration-500 group-hover:scale-105" />
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 mb-1.5">
                  <CampusBadge item={item} />
                </div>
                <h4 className="text-sm font-bold text-slate-900 leading-snug group-hover:text-[#004c91] transition-colors line-clamp-3">
                  {item.title}
                </h4>
                {item.publishedAt && (
                  <span className="mt-1.5 flex items-center gap-1 text-slate-500 text-xs">
                    <Calendar className="w-3 h-3" /> {formatDate(item.publishedAt, i18n.language as UiLanguage)}
                  </span>
                )}
              </div>
            </Link>
          ))}
        </div>
      </div>
    </FadeSection>
  );
}

/* ───────────────────────── Visit Highlights (vertical, left column) ───────────────────────── */

function VisitHighlights({ items }: { items: PublicNewsListItem[] }) {
  const { t, i18n } = useTranslation(['news']);
  return (
    <FadeSection>
      <h2 className="text-lg font-bold text-slate-900 uppercase tracking-wide mb-5 flex items-center gap-2">
        <Compass className="w-4 h-4 text-[#f37021]" /> {t('news:sections.visitHighlights')}
      </h2>
      <div className="flex flex-col gap-6">
        {items.map((item) => (
          <Link to={`/news/${item.newsId}`} key={item.newsId} className="group flex gap-4 items-start">
            <div className="w-24 h-24 shrink-0 rounded-lg overflow-hidden ring-1 ring-black/5">
              <ArticleImage item={item} className="w-full h-full object-cover transition-transform duration-500 group-hover:scale-105" />
            </div>
            <div className="flex-1 min-w-0 pt-1">
              <CampusBadge item={item} />
              <h4 className="mt-1.5 text-sm font-bold text-slate-900 leading-snug group-hover:text-[#004c91] transition-colors line-clamp-3">
                {item.title}
              </h4>
              {item.publishedAt && (
                <span className="mt-1.5 flex items-center gap-1 text-slate-500 text-xs">
                  <Calendar className="w-3 h-3" /> {formatDate(item.publishedAt, i18n.language as UiLanguage)}
                </span>
              )}
            </div>
          </Link>
        ))}
      </div>
    </FadeSection>
  );
}

/* ───────────────────────── Latest News Grid ───────────────────────── */

function NewsCard({ item, index }: { item: PublicNewsListItem; index: number; key?: React.Key }) {
  const { t, i18n } = useTranslation(['news']);
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, margin: '-40px' }}
      transition={{ duration: 0.4, delay: Math.min(index, 6) * 0.05, ease: 'easeOut' }}
    >
      <Link
        to={`/news/${item.newsId}`}
        className="group flex flex-col h-full rounded-xl overflow-hidden bg-white ring-1 ring-slate-200 hover:-translate-y-1 hover:shadow-lg transition-all duration-300"
      >
        <div className="aspect-[16/9] overflow-hidden">
          <ArticleImage item={item} className="w-full h-full object-cover transition-transform duration-500 group-hover:scale-105" />
        </div>
        <div className="flex-1 flex flex-col p-4">
          <div className="flex items-center gap-2 flex-wrap mb-2">
            {item.isFeatured && (
              <span className="px-2 py-0.5 rounded-full bg-[#f37021]/10 text-[#f37021] text-[11px] font-bold uppercase">
                {t('news:card.featured')}
              </span>
            )}
            <CampusBadge item={item} />
          </div>
          <h3 className="text-[15px] font-bold text-slate-900 leading-snug line-clamp-2 group-hover:text-[#004c91] transition-colors">
            {item.title}
          </h3>
          {item.summary && (
            <p className="mt-2 text-sm text-slate-600 line-clamp-3 leading-relaxed flex-1">{item.summary}</p>
          )}
          <div className="mt-3 flex items-center justify-between">
            {item.publishedAt && (
              <span className="flex items-center gap-1.5 text-slate-500 text-xs">
                <Calendar className="w-3.5 h-3.5" /> {formatDate(item.publishedAt, i18n.language as UiLanguage)}
              </span>
            )}
            <span className="text-[#004c91] text-xs font-bold opacity-0 group-hover:opacity-100 transition-opacity">
              {t('news:card.readMoreArrow')}
            </span>
          </div>
        </div>
      </Link>
    </motion.div>
  );
}

/* ───────────────────────── Page ───────────────────────── */

export function NewsPage() {
  const { t, i18n } = useTranslation(['news']);
  // Public news content is translated server-side (news_translations); refetch on language switch.
  const lang = i18n.language;

  // Filters
  const [keyword, setKeyword] = useState('');
  const [debouncedKeyword, setDebouncedKeyword] = useState('');
  const [type, setType] = useState<TypeFilter>('all');
  const [campusId, setCampusId] = useState<number | undefined>(undefined);
  const [sort, setSort] = useState<SortOrder>('latest');

  useEffect(() => {
    const t = setTimeout(() => setDebouncedKeyword(keyword.trim()), 350);
    return () => clearTimeout(t);
  }, [keyword]);

  // Hero + top stories (only meaningful with no active filter/search — otherwise fold into grid)
  const [heroItems, setHeroItems] = useState<PublicNewsListItem[]>([]);
  const [heroIndex, setHeroIndex] = useState(0);
  const [heroLoading, setHeroLoading] = useState(true);
  const [topStories, setTopStories] = useState<PublicNewsListItem[]>([]);
  const [topLoading, setTopLoading] = useState(true);

  // Latest grid (drives all filters) — "load more" style: pageIndex stays 1, pageSize grows.
  const [items, setItems] = useState<PublicNewsListItem[]>([]);
  const [totalItems, setTotalItems] = useState(0);
  const [gridPageSize, setGridPageSize] = useState(LATEST_INITIAL_SIZE);
  const [gridLoading, setGridLoading] = useState(true);
  const [gridError, setGridError] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  // Campus list (for filter + campus stories section)
  const [campuses, setCampuses] = useState<CampusOption[]>([]);
  const [campusGroups, setCampusGroups] = useState<Record<number, PublicNewsListItem[]>>({});
  const [campusGroupTotals, setCampusGroupTotals] = useState<Record<number, number>>({});
  const [campusPageSize, setCampusPageSize] = useState(CAMPUS_STORIES_INITIAL_SIZE);
  const [campusGroupsLoading, setCampusGroupsLoading] = useState(true);
  const [activeCampusTab, setActiveCampusTab] = useState<number | 'all'>('all');

  // Visit highlights
  const [visitHighlights, setVisitHighlights] = useState<PublicNewsListItem[]>([]);
  const [visitLoading, setVisitLoading] = useState(true);

  const hasActiveFilter = debouncedKeyword !== '' || type !== 'all' || campusId !== undefined;

  // Hero + top stories — load once (independent of filters)
  useEffect(() => {
    let cancelled = false;
    (async () => {
      setHeroLoading(true);
      setTopLoading(true);
      try {
        const featuredRes = await publicContentApi.getPublicNewsList({ pageIndex: 1, pageSize: HERO_ROTATE_SIZE, isFeatured: true, languageCode: lang });
        const featured = featuredRes.items ?? [];
        if (!cancelled) {
          setHeroItems(featured);
          setHeroIndex(0);
        }
      } catch {
        if (!cancelled) setHeroItems([]);
      } finally {
        if (!cancelled) setHeroLoading(false);
      }

      try {
        const res = await publicContentApi.getPublicNewsList({ pageIndex: 1, pageSize: TOP_STORIES_SIZE, sort: 'latest', languageCode: lang });
        if (!cancelled) setTopStories(res.items ?? []);
      } catch {
        if (!cancelled) setTopStories([]);
      } finally {
        if (!cancelled) setTopLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [reloadKey, lang]);

  // Rotate the hero banner through all featured items, looping every 5s.
  useEffect(() => {
    if (heroItems.length <= 1) return;
    const timer = setInterval(() => {
      setHeroIndex(i => (i + 1) % heroItems.length);
    }, HERO_ROTATE_INTERVAL_MS);
    return () => clearInterval(timer);
  }, [heroItems]);

  // Latest grid — reacts to filters + load-more (pageIndex stays 1, pageSize grows)
  useEffect(() => {
    let cancelled = false;
    (async () => {
      setGridLoading(true);
      setGridError(false);
      try {
        const data = await publicContentApi.getPublicNewsList({
          pageIndex: 1,
          pageSize: gridPageSize,
          keyword: debouncedKeyword || undefined,
          type,
          campusId,
          sort,
          languageCode: lang,
        });
        if (cancelled) return;
        setItems(data.items ?? []);
        setTotalItems(data.totalItems ?? 0);
      } catch {
        if (!cancelled) setGridError(true);
      } finally {
        if (!cancelled) setGridLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [gridPageSize, debouncedKeyword, type, campusId, sort, reloadKey, lang]);

  useEffect(() => {
    setGridPageSize(LATEST_INITIAL_SIZE);
  }, [debouncedKeyword, type, campusId, sort]);

  // Campuses + campus stories groups — reacts to load-more
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const list = await authenticationApi.getActiveCampuses();
        if (cancelled) return;
        setCampuses(list ?? []);

        setCampusGroupsLoading(true);
        const groups: Record<number, PublicNewsListItem[]> = {};
        const totals: Record<number, number> = {};
        await Promise.all(
          (list ?? []).map(async (c) => {
            try {
              const res = await publicContentApi.getPublicNewsList({
                pageIndex: 1,
                pageSize: campusPageSize,
                campusId: Number(c.campusId),
                sort: 'latest',
                languageCode: lang,
              });
              groups[Number(c.campusId)] = res.items ?? [];
              totals[Number(c.campusId)] = res.totalItems ?? 0;
            } catch {
              groups[Number(c.campusId)] = [];
              totals[Number(c.campusId)] = 0;
            }
          })
        );
        if (!cancelled) {
          setCampusGroups(groups);
          setCampusGroupTotals(totals);
        }
      } catch {
        if (!cancelled) setCampuses([]);
      } finally {
        if (!cancelled) setCampusGroupsLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [campusPageSize, reloadKey, lang]);

  useEffect(() => {
    setCampusPageSize(CAMPUS_STORIES_INITIAL_SIZE);
  }, [activeCampusTab]);

  // Visit highlights — load once
  useEffect(() => {
    let cancelled = false;
    (async () => {
      setVisitLoading(true);
      try {
        const res = await publicContentApi.getPublicNewsList({ pageIndex: 1, pageSize: 4, isPinned: true, sort: 'latest', languageCode: lang });
        if (!cancelled) setVisitHighlights(res.items ?? []);
      } catch {
        if (!cancelled) setVisitHighlights([]);
      } finally {
        if (!cancelled) setVisitLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [reloadKey, lang]);

  const visibleCampusItems = useMemo(() => {
    if (activeCampusTab === 'all') {
      const all = (Object.values(campusGroups) as PublicNewsListItem[][]).flat();
      return all
        .sort((a, b) => new Date(b.publishedAt ?? 0).getTime() - new Date(a.publishedAt ?? 0).getTime())
        .slice(0, campusPageSize);
    }
    return campusGroups[activeCampusTab] ?? [];
  }, [activeCampusTab, campusGroups, campusPageSize]);

  const campusTotalForTab =
    activeCampusTab === 'all'
      ? (Object.values(campusGroupTotals) as number[]).reduce((sum, n) => sum + n, 0)
      : campusGroupTotals[activeCampusTab] ?? 0;

  const canLoadMoreGrid = !gridLoading && !gridError && items.length < totalItems;
  const canLoadMoreCampus = !campusGroupsLoading && visibleCampusItems.length < campusTotalForTab;

  return (
    <div className={`pb-12 md:pb-20 bg-slate-50 ${hasActiveFilter ? 'pt-24 md:pt-32' : 'pt-20'}`}>
      {/* A. Hero — full-bleed viewport width, only when no filter is active */}
      {!hasActiveFilter && (
        <div className="mb-14">
          <NewsroomHero item={heroItems[heroIndex] ?? null} loading={heroLoading} />
        </div>
      )}

      <div className="max-w-[1600px] mx-auto px-8 sm:px-12 lg:px-16">
        <div className="flex flex-col lg:flex-row gap-6">
          {/* Left column: filter bar, top stories, latest grid, campus stories */}
          <div className="flex-1 min-w-0">
            {/* B. Filter bar */}
            <FilterBar
              keyword={keyword}
              onKeyword={setKeyword}
              type={type}
              onType={setType}
              campusId={campusId}
              onCampus={setCampusId}
              campuses={campuses}
              sort={sort}
              onSort={setSort}
            />

            {/* C. Editorial top stories — only when no filter is active */}
            {!hasActiveFilter && <TopStories items={topStories} loading={topLoading} />}

            {/* D. Latest news grid */}
            <FadeSection className="mb-16">
              <h2 className="text-lg font-bold text-slate-900 uppercase tracking-wide mb-5 flex items-center gap-2">
                <span className="w-1.5 h-5 bg-[#004c91] rounded-full" /> {hasActiveFilter ? t('news:sections.searchResults') : t('news:sections.latestNews')}
              </h2>

              {gridLoading && items.length === 0 ? (
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
                  {Array.from({ length: 3 }).map((_, i) => (
                    <SkeletonCard key={i} />
                  ))}
                </div>
              ) : gridError ? (
                <div className="py-16 text-center">
                  <p className="text-slate-700 font-bold mb-2">{t('news:status.loadErrorTitle')}</p>
                  <p className="text-slate-500 text-sm mb-6">{t('news:status.loadErrorMsg')}</p>
                  <button
                    onClick={() => setReloadKey((k) => k + 1)}
                    className="inline-flex items-center gap-2 px-5 py-2.5 bg-[#004c91] text-white font-bold rounded-xl hover:bg-[#003b70] transition-colors"
                  >
                    <RefreshCw className="w-4 h-4" /> {t('news:status.retry')}
                  </button>
                </div>
              ) : items.length === 0 ? (
                <EmptyState
                  title={hasActiveFilter ? t('news:status.noSearchResultTitle') : t('news:status.noNewsTitle')}
                  subtitle={hasActiveFilter ? t('news:status.adjustFilterMsg') : t('news:status.comeBackLaterMsg')}
                />
              ) : (
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
                  {items.map((article, idx) => (
                    <NewsCard item={article} index={idx} key={article.newsId} />
                  ))}
                </div>
              )}

              {/* Load more */}
              {canLoadMoreGrid && (
                <div className="mt-10 flex justify-center">
                  <button
                    onClick={() => setGridPageSize((s) => s + LATEST_LOAD_MORE_STEP)}
                    disabled={gridLoading}
                    className="px-6 py-2.5 rounded-xl border border-[#004c91] text-[#004c91] font-bold text-sm hover:bg-[#004c91] hover:text-white transition-colors disabled:opacity-50"
                  >
                    {gridLoading ? t('news:status.loading') : t('news:status.loadMore')}
                  </button>
                </div>
              )}
            </FadeSection>

            {/* E. Campus stories */}
            {campuses.length > 0 && (
              <FadeSection className="mb-16">
                <h2 className="text-lg font-bold text-slate-900 uppercase tracking-wide mb-5 flex items-center gap-2">
                  <MapPin className="w-4 h-4 text-[#004c91]" /> {t('news:sections.newsByCampus')}
                </h2>

                <div className="flex gap-2 overflow-x-auto no-scrollbar mb-6">
                  <button
                    onClick={() => setActiveCampusTab('all')}
                    className={`shrink-0 px-4 py-2 rounded-full text-sm font-semibold whitespace-nowrap transition-colors ${
                      activeCampusTab === 'all'
                        ? 'bg-[#004c91] text-white'
                        : 'bg-white text-slate-600 border border-slate-200 hover:border-[#004c91]/40'
                    }`}
                  >
                    {t('news:sections.allCampuses')}
                  </button>
                  {campuses.map((c) => (
                    <button
                      key={c.campusId}
                      onClick={() => setActiveCampusTab(Number(c.campusId))}
                      className={`shrink-0 px-4 py-2 rounded-full text-sm font-semibold whitespace-nowrap transition-colors ${
                        activeCampusTab === Number(c.campusId)
                          ? 'bg-[#004c91] text-white'
                          : 'bg-white text-slate-600 border border-slate-200 hover:border-[#004c91]/40'
                      }`}
                    >
                      {c.campusName}
                    </button>
                  ))}
                </div>

                {campusGroupsLoading && visibleCampusItems.length === 0 ? (
                  <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
                    {Array.from({ length: 3 }).map((_, i) => (
                      <SkeletonCard key={i} />
                    ))}
                  </div>
                ) : visibleCampusItems.length === 0 ? (
                  <EmptyState title={t('news:status.campusNoNewsTitle')} subtitle={t('news:status.comeBackLaterMsg')} />
                ) : (
                  <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
                    {visibleCampusItems.map((article, idx) => (
                      <NewsCard item={article} index={idx} key={`campus-${article.newsId}`} />
                    ))}
                  </div>
                )}

                {/* Load more */}
                {canLoadMoreCampus && (
                  <div className="mt-10 flex justify-center">
                    <button
                      onClick={() => setCampusPageSize((s) => s + CAMPUS_STORIES_LOAD_MORE_STEP)}
                      disabled={campusGroupsLoading}
                      className="px-6 py-2.5 rounded-xl border border-[#004c91] text-[#004c91] font-bold text-sm hover:bg-[#004c91] hover:text-white transition-colors disabled:opacity-50"
                    >
                      {campusGroupsLoading ? t('news:status.loading') : t('news:status.loadMore')}
                    </button>
                  </div>
                )}
              </FadeSection>
            )}
          </div>

          {/* Right column: Visit highlights — vertical list, only rendered when real data exists */}
          {!visitLoading && visitHighlights.length > 0 && (
            <aside className="lg:w-[300px] shrink-0">
              <div className="lg:sticky lg:top-28">
                <VisitHighlights items={visitHighlights} />
              </div>
            </aside>
          )}
        </div>
      </div>
    </div>
  );
}
