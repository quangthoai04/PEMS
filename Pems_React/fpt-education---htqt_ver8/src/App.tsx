/**
 * @license
 * SPDX-License-Identifier: Apache-2.0
 */

import React, { useEffect } from 'react';
import { Routes, Route, useLocation } from 'react-router-dom';
import { Header } from './components/Header';
import { Footer } from './components/Footer';
import { HomePage } from './pages/HomePage';
import { NewsPage } from './pages/NewsPage';
import { NewsDetailPage } from './pages/NewsDetailPage';
import { PartnersPage } from './pages/PartnersPage';
import { DashboardLayout } from './layouts/DashboardLayout';
import { DashboardHome } from './pages/dashboard/DashboardHome';
import { NewsManagement } from './pages/dashboard/NewsManagement';
import { NewsDetailDashboard } from './pages/dashboard/NewsDetailDashboard';
import { CreateNews } from './pages/dashboard/CreateNews';
import { EditNews } from './pages/dashboard/EditNews';
import { Profile } from './pages/dashboard/Profile';
import { EmailManagement } from './pages/dashboard/EmailManagement';
import { CreateEmail } from './pages/dashboard/CreateEmail';
import { EmailDetail } from './pages/dashboard/EmailDetail';
import { EditEmail } from './pages/dashboard/EditEmail';
import { SentEmailDetail } from './pages/dashboard/SentEmailDetail';
import { PartnerManagement } from './pages/dashboard/PartnerManagement';
import { VisitFPTUPage } from './pages/VisitFPTUPage';
import { CampusDetailVisitPage } from './pages/CampusDetailVisitPage';

function ScrollToTop() {
  const { pathname } = useLocation();

  useEffect(() => {
    if (pathname.startsWith('/dashboard')) {
      const mainContent = document.querySelector('main');
      if (mainContent) {
        mainContent.scrollTo(0, 0);
      }
    } else {
      window.scrollTo(0, 0);
    }
  }, [pathname]);

  return null;
}

export default function App() {
  const location = useLocation();
  const isDashboardRoute = location.pathname.startsWith('/dashboard');

  return (
    <div className="font-sans text-gray-900 bg-white min-h-screen flex flex-col">
      <ScrollToTop />
      
      {/* Conditionally render Header and Footer based on route */}
      {!isDashboardRoute && <Header />}
      
      <main className="flex-grow">
        <Routes>
          {/* Public Routes */}
          <Route path="/" element={<HomePage />} />
          <Route path="/news" element={<NewsPage />} />
          <Route path="/news/:id" element={<NewsDetailPage />} />
          <Route path="/partners" element={<PartnersPage />} />
          <Route path="/visit-fptu" element={<VisitFPTUPage />} />
          <Route path="/visit-fptu/:id" element={<CampusDetailVisitPage />} />

          {/* Dashboard Routes */}
          <Route path="/dashboard" element={<DashboardLayout />}>
            <Route index element={<DashboardHome />} />
            <Route path="profile" element={<Profile />} />
            <Route path="news" element={<NewsManagement />} />
            <Route path="news/create" element={<CreateNews />} />
            <Route path="news/:id/edit" element={<EditNews />} />
            <Route path="news/:id" element={<NewsDetailDashboard />} />
            <Route path="email" element={<EmailManagement />} />
            <Route path="email/create" element={<CreateEmail />} />
            <Route path="email/sent/:id" element={<SentEmailDetail />} />
            <Route path="email/:id" element={<EmailDetail />} />
            <Route path="email/:id/edit" element={<EditEmail />} />
            <Route path="partners" element={<PartnerManagement />} />
          </Route>
        </Routes>
      </main>

      {!isDashboardRoute && <Footer />}
    </div>
  );
}
