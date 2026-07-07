/**
 * Trang PublicHomePage
 * Landing page quốc tế dành cho Visitor / khách chưa đăng nhập.
 */

import React from 'react';
import { HeroSection } from '../components/home/HeroSection';
import { AboutFptuSection } from '../components/home/AboutFptuSection';
import { NewsSection } from '../components/home/NewsSection';
import { CampusShowcaseSection } from '../components/home/CampusShowcaseSection';
import { PartnersSection } from '../components/home/PartnersSection';
import { VisitProcessSection } from '../components/home/VisitProcessSection';
import { FinalCtaSection } from '../components/home/FinalCtaSection';

export function PublicHomePage() {
  return (
    <div className="flex flex-col min-h-screen">
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
