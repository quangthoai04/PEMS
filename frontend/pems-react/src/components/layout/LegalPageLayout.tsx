/**
 * Component LegalPageLayout
 * Khung dùng chung cho các trang pháp lý công khai (/privacy, /terms).
 *
 * Trang pháp lý là trang PUBLIC — Google reviewer phải mở được khi chưa đăng nhập, nên component
 * này không gọi API, không đọc AuthContext và không phụ thuộc bất kỳ state đăng nhập nào.
 * Header/Footer công khai do App.tsx render sẵn cho mọi route ngoài /dashboard, vì vậy ở đây chỉ
 * dựng phần thân: breadcrumb → hero → (TOC + nội dung) → thẻ liên hệ.
 */

import React, { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { format } from 'date-fns';
import { vi as viDateLocale, enUS as enDateLocale } from 'date-fns/locale';
import { ChevronRight, ShieldCheck, CalendarClock, ListOrdered, Mail, Phone, Globe, Building2 } from 'lucide-react';

/** Ngày công bố của cả hai văn bản pháp lý. Đổi ở đây là đổi cho mọi ngôn ngữ. */
export const LEGAL_LAST_UPDATED = new Date(2026, 7, 7); // 07/08/2026

export interface LegalSection {
  id: string;
  title: string;
  paragraphs: string[];
}

interface LegalPageLayoutProps {
  /** Đặt vào document.title; trả lại tiêu đề cũ khi rời trang. */
  documentTitle: string;
  title: string;
  subtitle: string;
  description: string;
  sections: LegalSection[];
  /** Nhãn breadcrumb cho chính trang này (thường trùng `title`). */
  breadcrumbLabel: string;
  /** Section được làm nổi bật bằng viền cam — dùng cho phần Google Drive / Google user data. */
  highlightSectionIds?: string[];
  /** Nội dung phụ chèn cuối một section (vd link sang Chính sách bảo mật trong Điều khoản). */
  sectionExtras?: Record<string, React.ReactNode>;
  /** Câu hỏi mở đầu thẻ liên hệ cuối trang. */
  contactHeading: string;
}

/**
 * Ngày cập nhật theo locale đang chọn: 07/08/2026 (VI) và August 7, 2026 (EN).
 * Dùng date-fns đã có sẵn trong project (cùng pattern NewsDetailPage) thay vì thêm thư viện.
 */
function useLastUpdatedLabel(): string {
  const { t, i18n } = useTranslation(['legal']);
  const isEnglish = i18n.language?.startsWith('en');
  const date = isEnglish
    ? format(LEGAL_LAST_UPDATED, 'MMMM d, yyyy', { locale: enDateLocale })
    : format(LEGAL_LAST_UPDATED, 'dd/MM/yyyy', { locale: viDateLocale });
  return t('legal:lastUpdated', { date });
}

/**
 * Theo dõi section đang đọc để tô đậm mục tương ứng trong TOC.
 * IntersectionObserver không tồn tại trong jsdom, nên thoát sớm thay vì ném lỗi khi chạy unit test.
 */
function useActiveSection(sections: LegalSection[]): string {
  const [activeId, setActiveId] = useState<string>(sections[0]?.id ?? '');

  useEffect(() => {
    if (typeof IntersectionObserver === 'undefined') return undefined;
    const elements = sections
      .map((section) => document.getElementById(section.id))
      .filter((el): el is HTMLElement => el !== null);
    if (elements.length === 0) return undefined;

    const observer = new IntersectionObserver(
      (entries) => {
        const visible = entries
          .filter((entry) => entry.isIntersecting)
          .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);
        if (visible[0]) setActiveId(visible[0].target.id);
      },
      // Bù chiều cao header cố định (80px) ở mép trên; mép dưới thu hẹp để mục "đang đọc"
      // là mục ở đầu khung nhìn chứ không phải mục cuối cùng còn ló ra.
      { rootMargin: '-120px 0px -55% 0px', threshold: 0 },
    );
    elements.forEach((el) => observer.observe(el));
    return () => observer.disconnect();
  }, [sections]);

  return activeId;
}

/** Thẻ thông tin liên hệ — nội dung của section cuối ("Liên hệ") và cũng là CTA cuối trang. */
function LegalContactCard({ heading }: { heading: string }) {
  const { t } = useTranslation(['legal']);
  const email = t('legal:org.email');
  const phone = t('legal:org.phone');
  const website = t('legal:org.website');

  return (
    <div className="rounded-2xl bg-gradient-to-r from-sky-50 to-orange-50 border border-slate-200 p-6 sm:p-8">
      <h3 className="text-lg sm:text-xl font-bold text-slate-900 mb-5">{heading}</h3>

      <ul className="space-y-3 text-sm text-slate-700">
        <li className="flex items-start gap-3">
          <Building2 className="w-4 h-4 text-fpt-navy shrink-0 mt-0.5" aria-hidden="true" />
          <span className="font-semibold">{t('legal:org.name')}</span>
        </li>
        <li className="flex items-center gap-3">
          <Mail className="w-4 h-4 text-fpt-navy shrink-0" aria-hidden="true" />
          <a href={`mailto:${email}`} className="hover:text-fpt-orange transition-colors break-all">
            {email}
          </a>
        </li>
        <li className="flex items-center gap-3">
          <Phone className="w-4 h-4 text-fpt-navy shrink-0" aria-hidden="true" />
          <a href={`tel:${phone.replace(/\s/g, '')}`} className="hover:text-fpt-orange transition-colors">
            {phone}
          </a>
        </li>
        <li className="flex items-center gap-3">
          <Globe className="w-4 h-4 text-fpt-navy shrink-0" aria-hidden="true" />
          <a href={website} className="hover:text-fpt-orange transition-colors break-all">
            {website}
          </a>
        </li>
      </ul>

      <a
        href={`mailto:${email}`}
        className="mt-6 inline-flex items-center gap-2 px-6 py-3 bg-fpt-orange text-white text-sm font-bold rounded-xl hover:bg-fpt-orange-hover transition-colors"
      >
        <Mail className="w-4 h-4" aria-hidden="true" />
        {t('legal:contactCard.cta')}
      </a>
    </div>
  );
}

export function LegalPageLayout({
  documentTitle,
  title,
  subtitle,
  description,
  sections,
  breadcrumbLabel,
  highlightSectionIds = [],
  sectionExtras = {},
  contactHeading,
}: LegalPageLayoutProps) {
  const { t } = useTranslation(['legal']);
  const lastUpdatedLabel = useLastUpdatedLabel();
  const activeId = useActiveSection(sections);
  const mobileTocRef = useRef<HTMLDetailsElement>(null);

  useEffect(() => {
    const previous = document.title;
    document.title = documentTitle;
    return () => {
      document.title = previous;
    };
  }, [documentTitle]);

  return (
    <div className="bg-slate-50 min-h-dvh pt-20">
      {/* Breadcrumb */}
      <nav aria-label="Breadcrumb" className="bg-white border-b border-slate-200">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-3">
          <ol className="flex items-center gap-1.5 text-xs sm:text-sm text-slate-500">
            <li>
              <Link to="/" className="hover:text-fpt-navy transition-colors">
                {t('legal:breadcrumb.home')}
              </Link>
            </li>
            <li aria-hidden="true">
              <ChevronRight className="w-3.5 h-3.5 text-slate-400" />
            </li>
            <li className="font-semibold text-slate-800" aria-current="page">
              {breadcrumbLabel}
            </li>
          </ol>
        </div>
      </nav>

      {/* Hero */}
      <header className="bg-fpt-navy relative overflow-hidden">
        <div className="absolute top-0 right-0 opacity-10 pointer-events-none transform translate-x-16 -translate-y-16">
          <ShieldCheck className="w-72 h-72 text-white" aria-hidden="true" />
        </div>
        <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-12 md:py-16 relative z-10">
          <div className="inline-flex items-center gap-2 px-3 py-1.5 bg-white/10 text-white border border-white/20 rounded-full text-xs font-bold uppercase tracking-wider mb-5">
            <ShieldCheck className="w-3.5 h-3.5 text-orange-300" aria-hidden="true" />
            {subtitle}
          </div>
          <h1 className="text-2xl md:text-4xl font-bold text-white leading-tight mb-4">{title}</h1>
          <p className="text-blue-100 text-sm md:text-base max-w-3xl leading-relaxed">{description}</p>
          <p className="mt-6 inline-flex items-center gap-2 px-3 py-1.5 bg-white/10 border border-white/20 rounded-lg text-xs text-blue-50">
            <CalendarClock className="w-3.5 h-3.5" aria-hidden="true" />
            {lastUpdatedLabel}
          </p>
        </div>
      </header>

      {/* Nội dung */}
      <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-10 flex flex-col lg:flex-row gap-8">
        {/* TOC desktop — sticky dưới header cố định */}
        <aside className="hidden lg:block lg:w-64 xl:w-72 shrink-0">
          <div className="sticky top-28">
            <p className="flex items-center gap-2 text-xs font-bold text-slate-400 uppercase tracking-wide mb-3">
              <ListOrdered className="w-3.5 h-3.5" aria-hidden="true" />
              {t('legal:toc.title')}
            </p>
            <nav aria-label={t('legal:toc.title')} className="flex flex-col gap-0.5">
              {sections.map((section, index) => (
                <a
                  key={section.id}
                  href={`#${section.id}`}
                  className={`px-3 py-2 rounded-lg text-sm transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-fpt-orange ${
                    activeId === section.id
                      ? 'bg-fpt-navy text-white font-semibold'
                      : 'text-slate-600 hover:bg-slate-100 hover:text-fpt-navy'
                  }`}
                >
                  <span className="mr-1.5 tabular-nums opacity-70">{index + 1}.</span>
                  {section.title}
                </a>
              ))}
            </nav>
          </div>
        </aside>

        {/* TOC mobile/tablet — accordion gốc của trình duyệt, không cần JS để mở */}
        <details ref={mobileTocRef} className="lg:hidden bg-white rounded-2xl border border-slate-200 overflow-hidden">
          <summary className="px-5 py-4 text-sm font-bold text-slate-800 cursor-pointer select-none flex items-center gap-2">
            <ListOrdered className="w-4 h-4 text-fpt-navy" aria-hidden="true" />
            {t('legal:toc.mobileLabel')}
          </summary>
          <nav aria-label={t('legal:toc.title')} className="px-3 pb-3 flex flex-col gap-0.5 border-t border-slate-100 pt-3">
            {sections.map((section, index) => (
              <a
                key={section.id}
                href={`#${section.id}`}
                onClick={() => {
                  if (mobileTocRef.current) mobileTocRef.current.open = false;
                }}
                className="px-3 py-2 rounded-lg text-sm text-slate-600 hover:bg-slate-100 hover:text-fpt-navy transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-fpt-orange"
              >
                <span className="mr-1.5 tabular-nums opacity-70">{index + 1}.</span>
                {section.title}
              </a>
            ))}
          </nav>
        </details>

        <main className="flex-1 min-w-0 space-y-6">
          {sections.map((section, index) => {
            const isHighlighted = highlightSectionIds.includes(section.id);
            const isContact = section.id === 'contact';

            if (isContact) {
              return (
                <section key={section.id} id={section.id} className="scroll-mt-28">
                  <h2 className="sr-only">{section.title}</h2>
                  <LegalContactCard heading={contactHeading} />
                </section>
              );
            }

            return (
              <section
                key={section.id}
                id={section.id}
                className={`scroll-mt-28 bg-white rounded-2xl border p-6 sm:p-8 ${
                  isHighlighted ? 'border-fpt-orange/40 border-l-4 border-l-fpt-orange' : 'border-slate-200'
                }`}
              >
                <h2 className="flex items-start gap-3 text-lg sm:text-xl font-bold text-slate-900 mb-4 leading-snug">
                  <span
                    className={`shrink-0 w-7 h-7 rounded-lg flex items-center justify-center text-xs font-bold tabular-nums ${
                      isHighlighted ? 'bg-fpt-orange text-white' : 'bg-fpt-navy/10 text-fpt-navy'
                    }`}
                    aria-hidden="true"
                  >
                    {index + 1}
                  </span>
                  {section.title}
                </h2>
                <div className="space-y-4 text-sm sm:text-[15px] text-slate-700 leading-relaxed">
                  {section.paragraphs.map((paragraph) => (
                    <p key={paragraph}>{paragraph}</p>
                  ))}
                </div>
                {sectionExtras[section.id] ? <div className="mt-5">{sectionExtras[section.id]}</div> : null}
              </section>
            );
          })}
        </main>
      </div>
    </div>
  );
}
