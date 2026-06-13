/**
 * @license
 * SPDX-License-Identifier: Apache-2.0
 */

import React, { useEffect } from 'react';
import { Routes, Route, useLocation } from 'react-router-dom';
import { Header } from './components/layout/Header';
import { Footer } from './components/layout/Footer';
import { HomePage } from './pages/HomePage';
import { NewsPage } from './pages/NewsPage';
import { NewsDetailPage } from './pages/NewsDetailPage';
import { PartnersPage } from './pages/PartnersPage';
import { PartnerDetailPage } from './pages/PartnerDetailPage';
import { DashboardLayout } from './components/layout/DashboardLayout';
import { DashboardHome } from './pages/dashboard/home/DashboardHome';
import { NewsManagement } from './pages/dashboard/news/NewsManagement';
import { NewsDetailDashboard } from './pages/dashboard/news/NewsDetailDashboard';
import { CreateNews } from './pages/dashboard/news/CreateNews';
import { EditNews } from './pages/dashboard/news/EditNews';
import { Profile } from './pages/dashboard/profile/Profile';
import { EmailManagement } from './pages/dashboard/emails/EmailManagement';
import { CreateEmail } from './pages/dashboard/emails/CreateEmail';
import { EmailDetail } from './pages/dashboard/emails/EmailDetail';
import { EditEmail } from './pages/dashboard/emails/EditEmail';
import { SentEmailDetail } from './pages/dashboard/emails/SentEmailDetail';
import { PartnerManagement } from './pages/dashboard/partners/PartnerManagement';
import { CreatePartner } from './pages/dashboard/partners/CreatePartner';
import { PartnerDetail } from './pages/dashboard/partners/PartnerDetail';
import { DepartmentManagement } from './pages/dashboard/departments/DepartmentManagement';
import { DepartmentDetailDashboard } from './pages/dashboard/departments/DepartmentDetailDashboard';
import { TaskDetail } from './pages/dashboard/departments/TaskDetail';
import { TaskInvitationDetail } from './pages/dashboard/departments/TaskInvitationDetail';
import { VisitProcess } from './pages/dashboard/visit/VisitProcess';
import { HoVisitProcessDetail } from './pages/dashboard/visit/HoVisitProcessDetail';
import { VisitRequestDetail } from './pages/dashboard/visit/VisitRequestDetail';
import { DocumentManagement } from './pages/dashboard/documents/DocumentManagement';
import { GalleryManagement } from './pages/dashboard/gallery/GalleryManagement';
import { LocationManagement } from './pages/dashboard/gallery/LocationManagement';
import { MinuteManagement } from './pages/dashboard/minutes/MinuteManagement';
import { ReportManagement } from './pages/dashboard/reports/ReportManagement';
import { FeedbackManagement } from './pages/dashboard/feedback/FeedbackManagement';
import { FeedbackDetail } from './pages/dashboard/feedback/FeedbackDetail';
import { AccountManagement } from './pages/dashboard/accounts/AccountManagement';
import { VisitFPTUPage } from './pages/VisitFPTUPage';
import { CampusDetailVisitPage } from './pages/CampusDetailVisitPage';
import { VisitRequestManagement } from './pages/dashboard/visit/VisitRequestManagement';
import { AgendaTemplateManagement } from './pages/dashboard/visit/AgendaTemplateManagement';
import { CreateVisitRequest } from './pages/dashboard/visit/CreateVisitRequest';
import { FAQManagement } from './pages/dashboard/faq/FAQManagement';
import { FAQDetail } from './pages/dashboard/faq/FAQDetail';
import { FAQPage } from './pages/FAQPage';
import { CampusManagement } from './pages/dashboard/campus/CampusManagement';
import { CampusDetail } from './pages/dashboard/campus/CampusDetail';
import { PermissionManagement } from './pages/dashboard/permissions/PermissionManagement';

import { ApiManagement } from './pages/dashboard/apis/ApiManagement.tsx';

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

const PlaceholderPage = ({ title }: { title: string }) => (
  <div className="p-8 h-full flex flex-col items-center justify-center pt-24">
    <h2 className="text-2xl font-bold text-[#004c91] mb-2">{title}</h2>
    <p className="text-gray-500">Tính năng đang trong quá trình phát triển.</p>
  </div>
);

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
          <Route path="/partners/:id" element={<PartnerDetailPage />} />
          <Route path="/visit-fptu" element={<VisitFPTUPage />} />
          <Route path="/visit-fptu/:id" element={<CampusDetailVisitPage />} />
          <Route path="/faq" element={<FAQPage />} />

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
            <Route path="partners/create" element={<CreatePartner />} />
            <Route path="partners/:id" element={<PartnerDetail />} />
            <Route path="departments" element={<DepartmentManagement />} />
            <Route path="departments/:id" element={<DepartmentDetailDashboard />} />
            <Route path="departments/:id/tasks/:taskId" element={<TaskDetail />} />
            <Route path="departments/:id/invitations/:taskId" element={<TaskInvitationDetail />} />
            <Route path="accounts" element={<AccountManagement />} />
            <Route path="campus" element={<CampusManagement />} />
            <Route path="campus/:id" element={<CampusDetail />} />
            <Route path="faq" element={<FAQManagement />} />
            <Route path="faq/:id" element={<FAQDetail />} />
            <Route path="visit" element={<VisitRequestManagement />} />
            <Route path="visit/create" element={<CreateVisitRequest />} />
            <Route path="visit/agenda-templates" element={<AgendaTemplateManagement />} />
            <Route path="visit/process/:id" element={<VisitProcess />} />
            <Route path="visit/reception-detail/:id" element={<VisitProcess />} />
            <Route path="visit/ho-detail/:id" element={<HoVisitProcessDetail />} />
            <Route path="visit/process/:id/request/:type" element={<VisitRequestDetail />} />
            <Route path="documents" element={<DocumentManagement />} />
            <Route path="gallery" element={<GalleryManagement />} />
            <Route path="gallery/locations" element={<LocationManagement />} />
            <Route path="minutes" element={<MinuteManagement />} />
            <Route path="reports" element={<ReportManagement />} />
            <Route path="feedback" element={<FeedbackManagement />} />
            <Route path="feedback/:id" element={<FeedbackDetail />} />
            <Route path="permissions" element={<PermissionManagement />} />
            <Route path="apis" element={<ApiManagement />} />
          </Route>
        </Routes>
      </main>

      {!isDashboardRoute && <Footer />}
    </div>
  );
}
