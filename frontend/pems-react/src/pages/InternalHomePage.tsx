/**
 * Trang InternalHomePage
 * Cổng vào hệ thống dành cho các role nội bộ — KHÔNG phải Dashboard (không hiển thị lại
 * task list / số liệu, những nội dung đó đã có trong Dashboard riêng của từng role).
 */

import React from 'react';
import { WelcomeHero } from '../components/home/internal/WelcomeHero';
import { QuickAccessSection } from '../components/home/internal/QuickAccessSection';
import { NewsSection } from '../components/home/NewsSection';
import { GuideStepsSection } from '../components/home/internal/GuideStepsSection';
import { GalleryPreviewSection } from '../components/home/GalleryPreviewSection';
import { FaqPreviewSection } from '../components/home/FaqPreviewSection';
import { InternalFinalCta } from '../components/home/internal/InternalFinalCta';
import type { AuthUser } from '../features/authentication/types/authentication.types';

interface InternalHomePageProps {
  user: AuthUser;
}

export function InternalHomePage({ user }: InternalHomePageProps) {
  return (
    <div className="flex flex-col min-h-screen">
      <WelcomeHero user={user} />
      <QuickAccessSection user={user} />
      <NewsSection />
      <GuideStepsSection user={user} />
      <GalleryPreviewSection preferredCampusCode={user.campusCode ?? null} />
      <FaqPreviewSection />
      <InternalFinalCta user={user} />
    </div>
  );
}
