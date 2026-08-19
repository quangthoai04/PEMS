/**
 * Trang PublicHomePage
 * Landing page quốc tế dành cho Visitor / khách chưa đăng nhập.
 */

import React, { useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import { HeroSection } from '../components/home/HeroSection';
import { AboutFptuSection } from '../components/home/AboutFptuSection';
import { NewsSection } from '../components/home/NewsSection';
import { CampusShowcaseSection } from '../components/home/CampusShowcaseSection';
import { PartnersSection } from '../components/home/PartnersSection';
import { VisitProcessSection } from '../components/home/VisitProcessSection';
import { FinalCtaSection } from '../components/home/FinalCtaSection';
import { FORCED_LOGOUT_REASON_KEY } from '../shared/api/httpClient';

/**
 * UC-86 / BR-AUTH-CAMPUS-08 (doc 08 §12 step 6): "Hiển thị thông báo campus đã ngừng hoạt động" —
 * a forced logout must not be silent. httpClient's 403 CAMPUS_INACTIVE_ACCESS_DENIED interceptor
 * writes the reason to sessionStorage and redirects here (ProtectedRoute sends unauthenticated
 * users to "/"); this is the one-shot consumer. Read + removed synchronously on first render, so
 * a later reload never re-shows it.
 */
function useForcedLogoutReason(): string | null {
  const [reason] = useState<string | null>(() => {
    const value = sessionStorage.getItem(FORCED_LOGOUT_REASON_KEY);
    if (value) sessionStorage.removeItem(FORCED_LOGOUT_REASON_KEY);
    return value;
  });
  return reason;
}

export function PublicHomePage() {
  const forcedLogoutReason = useForcedLogoutReason();

  return (
    <div className="flex flex-col min-h-dvh">
      {forcedLogoutReason && (
        <div
          role="alert"
          className="mx-4 mt-20 flex items-start gap-3 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 sm:mx-6 lg:mx-8"
        >
          <AlertTriangle className="h-5 w-5 shrink-0" aria-hidden="true" />
          <span>{forcedLogoutReason}</span>
        </div>
      )}
      <HeroSection />
      <AboutFptuSection />
      <NewsSection />
      <CampusShowcaseSection />
      <PartnersSection />
      <VisitProcessSection />
      <FinalCtaSection />
    </div>
  );
}
