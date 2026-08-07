/**
 * Trang PrivacyPolicyPage (Public) — /privacy
 * Chính sách bảo mật công khai của PEMS, là URL được khai báo trong Google Auth Platform
 * (Application privacy policy link) phục vụ Google OAuth verification. Trang không yêu cầu
 * đăng nhập và không gọi API — nội dung lấy từ namespace i18n `legal` (VI/EN).
 */

import React from 'react';
import { useTranslation } from 'react-i18next';
import { LegalPageLayout, type LegalSection } from '../../components/layout/LegalPageLayout';

/**
 * Phần Google Drive / Google user data được làm nổi bật: đây là mục Google reviewer đọc kỹ nhất
 * khi xét Limited Use, nên nó phải tách khỏi khối văn bản chung chứ không chìm giữa 14 mục.
 */
const HIGHLIGHT_SECTIONS = ['google-drive'];

export function PrivacyPolicyPage() {
  const { t } = useTranslation(['legal']);
  const sections = t('legal:privacy.sections', { returnObjects: true }) as LegalSection[];

  return (
    <LegalPageLayout
      documentTitle={t('legal:privacy.documentTitle')}
      title={t('legal:privacy.title')}
      subtitle={t('legal:privacy.subtitle')}
      description={t('legal:privacy.description')}
      breadcrumbLabel={t('legal:privacy.title')}
      sections={Array.isArray(sections) ? sections : []}
      highlightSectionIds={HIGHLIGHT_SECTIONS}
      contactHeading={t('legal:contactCard.privacyTitle')}
    />
  );
}
