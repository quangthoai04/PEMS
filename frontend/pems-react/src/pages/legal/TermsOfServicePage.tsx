/**
 * Trang TermsOfServicePage (Public) — /terms
 * Điều khoản sử dụng công khai của PEMS, là URL được khai báo trong Google Auth Platform
 * (Application terms of service link). Trang không yêu cầu đăng nhập và không gọi API —
 * nội dung lấy từ namespace i18n `legal` (VI/EN).
 */

import React from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowRight } from 'lucide-react';
import { LegalPageLayout, type LegalSection } from '../../components/layout/LegalPageLayout';

const HIGHLIGHT_SECTIONS = ['google-drive'];

export function TermsOfServicePage() {
  const { t } = useTranslation(['legal']);
  const sections = t('legal:terms.sections', { returnObjects: true }) as LegalSection[];

  // Mục "Quyền riêng tư" phải dẫn thẳng sang /privacy — link thật, không phải "#".
  const sectionExtras = {
    privacy: (
      <Link
        to="/privacy"
        className="inline-flex items-center gap-2 px-5 py-2.5 bg-fpt-navy text-white text-sm font-bold rounded-xl hover:bg-fpt-navy-hover transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-fpt-orange"
      >
        {t('legal:terms.privacyLinkLabel')}
        <ArrowRight className="w-4 h-4" aria-hidden="true" />
      </Link>
    ),
  };

  return (
    <LegalPageLayout
      documentTitle={t('legal:terms.documentTitle')}
      title={t('legal:terms.title')}
      subtitle={t('legal:terms.subtitle')}
      description={t('legal:terms.description')}
      breadcrumbLabel={t('legal:terms.title')}
      sections={Array.isArray(sections) ? sections : []}
      highlightSectionIds={HIGHLIGHT_SECTIONS}
      sectionExtras={sectionExtras}
      contactHeading={t('legal:contactCard.termsTitle')}
    />
  );
}
