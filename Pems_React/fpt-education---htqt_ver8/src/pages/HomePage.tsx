// Đây là trang chủ của ứng dụng
import React from 'react';
import { HeroSection } from '../components/HeroSection';
import { NewsSection } from '../components/NewsSection';
import { StatsSection } from '../components/StatsSection';
import { PartnersSection } from '../components/PartnersSection';

export function HomePage() {
  return (
    <div className="flex flex-col min-h-screen">
      <HeroSection />
      <NewsSection />
      <StatsSection />
      <PartnersSection />
      {/* Footer padding to deal with the last section being white and footer being dark ok */}
      <div className="h-12 bg-white"></div>
    </div>
  );
}

