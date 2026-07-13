/**
 * @license
 * SPDX-License-Identifier: Apache-2.0
 */

import React, { useEffect } from 'react';
import { Navigate, Routes, Route, useLocation } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
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
import { PartnerEdit } from './pages/dashboard/partners/PartnerEdit';
import { DepartmentManagement } from './pages/dashboard/departments/DepartmentManagement';
import { DepartmentDetailDashboard } from './pages/dashboard/departments/DepartmentDetailDashboard';
import { DeptReportManagement } from './pages/dashboard/reports/DeptReportManagement';
import { TaskDetail } from './pages/dashboard/departments/TaskDetail';
import { TaskInvitationDetail } from './pages/dashboard/departments/TaskInvitationDetail';
import { VisitProcess } from './pages/dashboard/visit/VisitProcess';
import { VisitContributionPage } from './pages/dashboard/visit/VisitContributionPage';
import { VisitProcessSummaryPage } from './pages/dashboard/visit/VisitProcessSummaryPage';
import { HoVisitProcessDetail } from './pages/dashboard/visit/HoVisitProcessDetail';
import { VisitRequestDetail } from './pages/dashboard/visit/VisitRequestDetail';
import { DocumentManagement } from './pages/dashboard/documents/DocumentManagement';
import { GalleryManagement } from './pages/dashboard/gallery/GalleryManagement';
import { LocationManagement } from './pages/dashboard/gallery/LocationManagement';
import { MinuteManagement } from './pages/dashboard/minutes/MinuteManagement';
import { HoReportManagement } from './pages/dashboard/reports/HoReportManagement';
import { StaffLeaderReportManagement } from './pages/dashboard/reports/StaffLeaderReportManagement';
import { FeedbackManagement } from './pages/dashboard/feedback/FeedbackManagement';
import { FeedbackDetail } from './pages/dashboard/feedback/FeedbackDetail';
import { AccountManagement } from './pages/dashboard/accounts/AccountManagement';
import { VisitFPTUPage } from './pages/VisitFPTUPage';
import { CampusDetailVisitPage } from './pages/CampusDetailVisitPage';
import { VisitRequestManagement } from './pages/dashboard/visit/VisitRequestManagement';
import { DeptLeadVisitTasksPage } from './pages/dashboard/visit/DeptLeadVisitTasksPage';
import { VisitParticipantInvitationDetail } from './pages/dashboard/visit/VisitParticipantInvitationDetail';
import { AgendaTemplateManagement } from './pages/dashboard/visit/AgendaTemplateManagement';
import { CreateVisitRequest } from './pages/dashboard/visit/CreateVisitRequest';
import { EditVisitRequest } from './pages/dashboard/visit/EditVisitRequest';
import { VisitFeedbackPage } from './pages/dashboard/visit/VisitFeedbackPage';
import { FAQManagement } from './pages/dashboard/faq/FAQManagement';
import { FAQDetail } from './pages/dashboard/faq/FAQDetail';
import { FAQPage } from './pages/FAQPage';
import { CampusManagement } from './pages/dashboard/campus/CampusManagement';
import { CampusDetail } from './pages/dashboard/campus/CampusDetail';

import { ApiManagement } from './pages/dashboard/apis/ApiManagement.tsx';
import { SessionManagement } from './pages/dashboard/admin/SessionManagement';
import { SecurityMonitoring } from './pages/dashboard/admin/SecurityMonitoring';
import { AuditLogManagement } from './pages/dashboard/admin/AuditLogManagement';

import { LoginPage } from './pages/auth/LoginPage';
import { ForgotPasswordPage } from './pages/auth/ForgotPasswordPage';
import { ResetPasswordPage } from './pages/auth/ResetPasswordPage';
import { ChangePasswordPage } from './pages/auth/ChangePasswordPage';
import { ForbiddenPage } from './pages/ForbiddenPage';
import { InvalidAccountPage } from './pages/InvalidAccountPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { NotificationsPage } from './pages/notifications/NotificationsPage';
import { ProtectedRoute } from './shared/auth/ProtectedRoute';
import { ErrorBoundary } from './components/layout/ErrorBoundary';


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

const BARE_ROUTES = ['/login', '/forgot-password', '/reset-password', '/change-password', '/403', '/invalid-account'];

export default function App() {
  const location = useLocation();
  const isDashboardRoute = location.pathname.startsWith('/dashboard');
  const isBareRoute = isDashboardRoute || BARE_ROUTES.includes(location.pathname);

  const userStr = localStorage.getItem("currentUser");
  const user = userStr ? JSON.parse(userStr) : null;
  const isDeptLeader = user?.role?.toUpperCase() === 'DEPARTMENT' && user?.subRole?.toUpperCase() === 'LEADER';
  const isDeptStaff = user?.role?.toUpperCase() === 'DEPARTMENT' && !isDeptLeader;
  const isHO = user?.role?.toUpperCase() === 'HO';
  const isStaffLeader = user?.role?.toUpperCase() === 'STAFF' && user?.subRole?.toUpperCase() === 'LEADER';

  return (
    <div className="font-sans text-gray-900 bg-white min-h-screen flex flex-col">
      <Toaster position="top-right" containerStyle={{ zIndex: 9999 }} />
      <ScrollToTop />

      {/* Conditionally render Header and Footer based on route */}
      {!isBareRoute && <Header />}

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

          {/* Authentication Routes */}
          <Route path="/login" element={<LoginPage />} />
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="/reset-password" element={<ResetPasswordPage />} />
          <Route path="/change-password" element={<ProtectedRoute><ChangePasswordPage /></ProtectedRoute>} />
          <Route path="/403" element={<ForbiddenPage />} />
          <Route path="/invalid-account" element={<InvalidAccountPage />} />

          {/* Notification Center — yêu cầu đăng nhập, KHÔNG lồng trong /dashboard để giữ Header/Footer public */}
          <Route path="/notifications" element={<ProtectedRoute><NotificationsPage /></ProtectedRoute>} />

          {/* Dashboard Routes (require authentication) */}
          <Route path="/dashboard" element={<ProtectedRoute><ErrorBoundary><DashboardLayout /></ErrorBoundary></ProtectedRoute>}>
            <Route index element={['VISITOR', 'STUDENT'].includes(user?.role?.toUpperCase()) ? <Navigate to="/dashboard/visit" replace /> : <DashboardHome />} />
            <Route path="profile" element={<Profile />} />
            <Route path="news" element={<NewsManagement />} />
            <Route path="news/create" element={<CreateNews />} />
            <Route path="news/:id/edit" element={<EditNews />} />
            <Route path="news/:id" element={<NewsDetailDashboard />} />
            <Route path="email" element={isDeptStaff ? <Navigate to="/dashboard" replace /> : <ProtectedRoute><EmailManagement /></ProtectedRoute>} />
            <Route path="email/create" element={isDeptStaff ? <Navigate to="/dashboard" replace /> : <ProtectedRoute><CreateEmail /></ProtectedRoute>} />
            <Route path="email/detail/:sourceType/:id" element={isDeptStaff ? <Navigate to="/dashboard" replace /> : <ProtectedRoute><SentEmailDetail /></ProtectedRoute>} />
            <Route path="email/:id" element={isDeptStaff ? <Navigate to="/dashboard" replace /> : <ProtectedRoute><EmailDetail /></ProtectedRoute>} />
            <Route path="email/:id/edit" element={isDeptStaff ? <Navigate to="/dashboard" replace /> : <ProtectedRoute><EditEmail /></ProtectedRoute>} />
            <Route path="partners" element={<PartnerManagement />} />
            <Route path="partners/create" element={<CreatePartner />} />
            <Route path="partners/:id/edit" element={<PartnerEdit />} />
            <Route path="partners/:id" element={<PartnerDetail />} />
            <Route path="departments" element={<DepartmentManagement />} />
            <Route path="departments/:id" element={<DepartmentDetailDashboard />} />
            <Route path="departments/:id/tasks/:taskId" element={<TaskDetail />} />
            <Route path="departments/:id/invitations/:taskId" element={<TaskInvitationDetail />} />
            <Route path="accounts" element={<ProtectedRoute><AccountManagement /></ProtectedRoute>} />
            <Route path="campus" element={<ProtectedRoute><CampusManagement /></ProtectedRoute>} />
            <Route path="campus/:id" element={<ProtectedRoute><CampusDetail /></ProtectedRoute>} />
            <Route path="faq" element={<FAQManagement />} />
            <Route path="faq/:id" element={<FAQDetail />} />
            <Route path="visit" element={isDeptStaff ? <Navigate to="/dashboard" replace /> : isDeptLeader ? <DeptLeadVisitTasksPage /> : <VisitRequestManagement />} />
            <Route path="visit/invitations/:participantId" element={<VisitParticipantInvitationDetail />} />
            <Route path="visit/department-tasks/:participantId" element={<VisitParticipantInvitationDetail />} />
            <Route path="visit/create" element={<CreateVisitRequest />} />
            {/* Visitor sửa đơn pending / sửa & gửi lại đơn bị từ chối (owner-only, backend re-check) */}
            <Route path="visit/edit/:visitRequestId" element={<EditVisitRequest />} />
            <Route path="visit/resubmit/:visitRequestId" element={<EditVisitRequest />} />
            <Route path="visit/agenda-templates" element={<AgendaTemplateManagement />} />
            <Route path="visit/process/:id" element={<VisitProcess />} />
            <Route path="visit/feedback/:visitInstanceId" element={<VisitFeedbackPage />} />
            <Route path="visit/process-summary/:visitInstanceId" element={<VisitProcessSummaryPage />} />
            <Route path="visit/contribution/:visitInstanceId" element={<VisitContributionPage />} />
            <Route path="visit/reception-detail/:id" element={<VisitProcess />} />
            <Route path="visit/ho-detail/:id" element={<HoVisitProcessDetail />} />
            <Route path="visit/process/:id/request/:type" element={<VisitRequestDetail />} />
            <Route path="documents" element={<DocumentManagement />} />
            <Route path="gallery" element={<GalleryManagement />} />
            <Route path="gallery/locations" element={<LocationManagement />} />
            <Route path="minutes" element={<MinuteManagement />} />
            <Route path="reports" element={<ProtectedRoute>{isDeptLeader ? <DeptReportManagement /> : isHO ? <HoReportManagement /> : isStaffLeader ? <StaffLeaderReportManagement /> : <Navigate to="/dashboard" replace />}</ProtectedRoute>} />
            <Route path="feedback" element={<FeedbackManagement />} />
            <Route path="feedback/:id" element={<FeedbackDetail />} />
            <Route path="apis" element={<ProtectedRoute roles={['ADMIN']}><ApiManagement /></ProtectedRoute>} />
            {/* System Administration Console (ADMIN-only) */}
            <Route path="admin/sessions" element={<ProtectedRoute roles={['ADMIN']}><SessionManagement /></ProtectedRoute>} />
            <Route path="admin/security" element={<ProtectedRoute roles={['ADMIN']}><SecurityMonitoring /></ProtectedRoute>} />
            <Route path="admin/audit-logs" element={<ProtectedRoute roles={['ADMIN']}><AuditLogManagement /></ProtectedRoute>} />
          </Route>
          
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </main>

      {!isDashboardRoute && <Footer />}
    </div>
  );
}
