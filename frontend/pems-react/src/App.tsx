/**
 * @license
 * SPDX-License-Identifier: Apache-2.0
 */

import React, { useEffect } from 'react';
import { Navigate, Routes, Route, useLocation } from 'react-router-dom';
import VisitContactInvitationPage from './pages/identity/VisitContactInvitationPage';
import ConfirmEmailPage from './pages/account/ConfirmEmailPage';
import VisitRequestV2Page from './pages/visit/VisitRequestV2Page';
import VisitRequestV2DetailPage from './pages/dashboard/visit/VisitRequestV2DetailPage';
import EditVisitRequestV2Page from './pages/dashboard/visit/EditVisitRequestV2Page';
import EditPendingCampusV2Page from './pages/dashboard/visit/EditPendingCampusV2Page';
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
import { MyDepartmentPage } from './pages/dashboard/my-department/MyDepartmentPage';
import { DeptReportManagement } from './pages/dashboard/reports/DeptReportManagement';
import { VisitProcess } from './pages/dashboard/visit/VisitProcess';
import { VisitContributionPage } from './pages/dashboard/visit/VisitContributionPage';
import { VisitProcessSummaryPage } from './pages/dashboard/visit/VisitProcessSummaryPage';
import { HoVisitProcessDetail } from './pages/dashboard/visit/HoVisitProcessDetail';
import { VisitRequestDetail } from './pages/dashboard/visit/VisitRequestDetail';
import { DocumentManagement } from './pages/dashboard/documents/DocumentManagement';
import { GalleryManagement } from './pages/dashboard/gallery/GalleryManagement';
import { LocationManagement } from './pages/dashboard/gallery/LocationManagement';
import { MinuteManagement } from './pages/dashboard/minutes/MinuteManagement';
import { PostVisitTaskManagement } from './pages/dashboard/post-visit-tasks/PostVisitTaskManagement';
import { HoReportManagement } from './pages/dashboard/reports/HoReportManagement';
import { StaffLeaderReportManagement } from './pages/dashboard/reports/StaffLeaderReportManagement';
import { FeedbackManagement } from './pages/dashboard/feedback/FeedbackManagement';
import { FeedbackDetail } from './pages/dashboard/feedback/FeedbackDetail';
import { AccountManagement } from './pages/dashboard/accounts/AccountManagement';
import { VisitFPTUPage } from './pages/VisitFPTUPage';
import { CampusDetailVisitPage } from './pages/CampusDetailVisitPage';
import { VisitRequestManagement } from './pages/dashboard/visit/VisitRequestManagement';
import { VisitPhotoManagement } from './pages/dashboard/visit/VisitPhotoManagement';
import { DeptLeadVisitTasksPage } from './pages/dashboard/visit/DeptLeadVisitTasksPage';
import { VisitParticipantInvitationDetail } from './pages/dashboard/visit/VisitParticipantInvitationDetail';
import { AgendaTemplateManagement } from './pages/dashboard/visit/AgendaTemplateManagement';
import { CreateVisitRequestEntry } from './pages/dashboard/visit/CreateVisitRequestEntry';

import { VisitFeedbackPage } from './pages/dashboard/visit/VisitFeedbackPage';
import { FAQManagement } from './pages/dashboard/faq/FAQManagement';
import { FAQDetail } from './pages/dashboard/faq/FAQDetail';
import { FAQPage } from './pages/FAQPage';
import { PrivacyPolicyPage } from './pages/legal/PrivacyPolicyPage';
import { TermsOfServicePage } from './pages/legal/TermsOfServicePage';
import { CampusManagement } from './pages/dashboard/campus/CampusManagement';
import { CampusDetail } from './pages/dashboard/campus/CampusDetail';

import { ApiManagement } from './pages/dashboard/apis/ApiManagement.tsx';
import { SessionManagement } from './pages/dashboard/admin/SessionManagement';
import { SecurityMonitoring } from './pages/dashboard/admin/SecurityMonitoring';
import { AuditLogManagement } from './pages/dashboard/admin/AuditLogManagement';

import { ForgotPasswordPage } from './pages/auth/ForgotPasswordPage';
import { ResetPasswordPage } from './pages/auth/ResetPasswordPage';
import { ChangePasswordPage } from './pages/auth/ChangePasswordPage';
import { ForbiddenPage } from './pages/ForbiddenPage';
import { InvalidAccountPage } from './pages/InvalidAccountPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { NotificationsPage } from './pages/notifications/NotificationsPage';
import { ProtectedRoute } from './shared/auth/ProtectedRoute';
import { RouteAccessGuard } from './shared/auth/RouteAccessGuard';
import { useAuth } from './shared/hooks/useAuth';
import { ErrorBoundary } from './components/layout/ErrorBoundary';
import { PerCampusV2CapabilityProvider } from './shared/features/perCampusV2Capability';


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

const BARE_ROUTES = ['/forgot-password', '/reset-password', '/change-password', '/403', '/invalid-account'];

export default function App() {
  const location = useLocation();
  const isDashboardRoute = location.pathname.startsWith('/dashboard');
  const isBareRoute = isDashboardRoute || BARE_ROUTES.includes(location.pathname);

  // Authorization reads the effective role from AuthContext, which derives it from the
  // profile the backend returned. It used to read localStorage's `currentUser`, so editing
  // that object in devtools re-wrote the route table — no request to the server involved.
  // `effectiveRole` here only picks WHICH component a shared path renders; whether the user
  // may be on that path at all is decided by <RouteAccessGuard routeKey=...>.
  const { effectiveRole } = useAuth();

  return (
    <PerCampusV2CapabilityProvider>
    <div className="font-sans text-gray-900 bg-white min-h-screen flex flex-col">
      {/* top: 96 = Header's fixed h-20 (80px) + 16px gutter. Without this the toast
          container overlaps the fixed header's nav links; since the toast itself has
          pointer-events:auto, the mouse resting there while navigating triggers
          react-hot-toast's built-in "pause on hover" and the toast never auto-dismisses. */}
      <Toaster position="top-right" containerStyle={{ zIndex: 9999, top: 96 }} />
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
          {/* Trang pháp lý công khai. Hai URL này được khai báo trong Google Auth Platform
              (privacy policy / terms of service link) nên phải mở được khi CHƯA đăng nhập —
              đặt ngoài mọi <ProtectedRoute>, và không nằm trong BARE_ROUTES để giữ Header/Footer. */}
          <Route path="/privacy" element={<PrivacyPolicyPage />} />
          <Route path="/terms" element={<TermsOfServicePage />} />

          {/* Authentication Routes */}
          <Route path="/login" element={<Navigate to="/" replace />} />
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="/reset-password" element={<ResetPasswordPage />} />
          <Route path="/confirm-email" element={<ConfirmEmailPage />} />
          <Route path="/change-password" element={<ProtectedRoute><ChangePasswordPage /></ProtectedRoute>} />
          <Route path="/403" element={<ForbiddenPage />} />
          <Route path="/invalid-account" element={<InvalidAccountPage />} />

          {/* Notification Center — yêu cầu đăng nhập, KHÔNG lồng trong /dashboard để giữ Header/Footer public */}
          <Route path="/notifications" element={<ProtectedRoute><NotificationsPage /></ProtectedRoute>} />

          {/* Per-campus v2 identity invitations: anonymous MASKED landing; accept/decline require the
              matching Google login (the page itself guides the user — no ProtectedRoute redirect).

              The FIRST path is the one the backend puts in the email today
              (OperationalContactInvitationService builds {FrontendBaseUrl}/operational-contact-confirmation/{token}).
              The two below it are the addresses the request-level flow used to send; they are kept
              because links already in somebody's inbox outlive the code that wrote them, and the page
              reads the invitation kind from the record rather than from the route. */}
          <Route path="/operational-contact-confirmation/:token" element={<VisitContactInvitationPage kind="claim" />} />
          <Route path="/visit-contact-claim/:token" element={<VisitContactInvitationPage kind="claim" />} />
          <Route path="/visit-contact-transfer/:token" element={<VisitContactInvitationPage kind="transfer" />} />

          {/* Per-campus form v2 (feature-flagged server-side; the v1 registration flow is untouched) */}
          <Route path="/visit-registration/v2" element={<VisitRequestV2Page mode="public" />} />
          <Route path="/visit/create-v2" element={<ProtectedRoute><VisitRequestV2Page mode="authenticated" /></ProtectedRoute>} />

          {/* Dashboard Routes.
              Every child carries a <RouteAccessGuard routeKey=...>; the policy for each key
              lives in shared/auth/dashboardRouteAccess.ts and is shared with the Sidebar, so
              a hidden menu item and a typed URL always reach the same verdict. */}
          <Route path="/dashboard" element={<ProtectedRoute><ErrorBoundary><DashboardLayout /></ErrorBoundary></ProtectedRoute>}>
            <Route
              index
              element={
                <RouteAccessGuard routeKey="DASHBOARD_HOME">
                  {effectiveRole === 'VISITOR' || effectiveRole === 'STUDENT'
                    ? <Navigate to="/dashboard/visit" replace />
                    : <DashboardHome />}
                </RouteAccessGuard>
              }
            />
            <Route path="profile" element={<RouteAccessGuard routeKey="PROFILE"><Profile /></RouteAccessGuard>} />

            <Route path="news" element={<RouteAccessGuard routeKey="NEWS_LIST"><NewsManagement /></RouteAccessGuard>} />
            <Route path="news/create" element={<RouteAccessGuard routeKey="NEWS_CREATE"><CreateNews /></RouteAccessGuard>} />
            <Route path="news/:id/edit" element={<RouteAccessGuard routeKey="NEWS_EDIT"><EditNews /></RouteAccessGuard>} />
            <Route path="news/:id" element={<RouteAccessGuard routeKey="NEWS_DETAIL"><NewsDetailDashboard /></RouteAccessGuard>} />

            <Route path="email" element={<RouteAccessGuard routeKey="EMAIL_LIST"><EmailManagement /></RouteAccessGuard>} />
            <Route path="email/create" element={<RouteAccessGuard routeKey="EMAIL_CREATE"><CreateEmail /></RouteAccessGuard>} />
            <Route path="email/detail/:sourceType/:id" element={<RouteAccessGuard routeKey="EMAIL_DETAIL"><SentEmailDetail /></RouteAccessGuard>} />
            <Route path="email/:id" element={<RouteAccessGuard routeKey="EMAIL_DETAIL"><EmailDetail /></RouteAccessGuard>} />
            <Route path="email/:id/edit" element={<RouteAccessGuard routeKey="EMAIL_EDIT"><EditEmail /></RouteAccessGuard>} />

            <Route path="partners" element={<RouteAccessGuard routeKey="PARTNER_LIST"><PartnerManagement /></RouteAccessGuard>} />
            <Route path="partners/create" element={<RouteAccessGuard routeKey="PARTNER_CREATE"><CreatePartner /></RouteAccessGuard>} />
            <Route path="partners/:id/edit" element={<RouteAccessGuard routeKey="PARTNER_EDIT"><PartnerEdit /></RouteAccessGuard>} />
            <Route path="partners/:id" element={<RouteAccessGuard routeKey="PARTNER_DETAIL"><PartnerDetail /></RouteAccessGuard>} />

            <Route path="departments" element={<RouteAccessGuard routeKey="DEPARTMENT_LIST"><DepartmentManagement /></RouteAccessGuard>} />
            {/* Department Leader personnel management. No :id — the department is resolved from the
                signed-in Leader server-side, so there is no id in the URL to tamper with. Non-Leaders
                get 403 here and are refused again by the API. */}
            <Route path="my-department" element={<RouteAccessGuard routeKey="MY_DEPARTMENT"><MyDepartmentPage /></RouteAccessGuard>} />
            {/* Legacy per-id department screen. A Department Leader is sent to their own screen:
                the id in this URL is client-supplied, and this page's personnel modal is the older
                one. Their single entry point is /dashboard/my-department. */}
            <Route
              path="departments/:id"
              element={
                <RouteAccessGuard routeKey="DEPARTMENT_DETAIL">
                  {effectiveRole === 'DEPARTMENT_LEAD'
                    ? <Navigate to="/dashboard/my-department" replace />
                    : <DepartmentDetailDashboard />}
                </RouteAccessGuard>
              }
            />

            <Route path="accounts" element={<RouteAccessGuard routeKey="ACCOUNT_LIST"><AccountManagement /></RouteAccessGuard>} />
            <Route path="campus" element={<RouteAccessGuard routeKey="CAMPUS_LIST"><CampusManagement /></RouteAccessGuard>} />
            <Route path="campus/:id" element={<RouteAccessGuard routeKey="CAMPUS_DETAIL"><CampusDetail /></RouteAccessGuard>} />
            <Route path="faq" element={<RouteAccessGuard routeKey="FAQ_LIST"><FAQManagement /></RouteAccessGuard>} />
            <Route path="faq/:id" element={<RouteAccessGuard routeKey="FAQ_DETAIL"><FAQDetail /></RouteAccessGuard>} />

            <Route
              path="visit"
              element={
                <RouteAccessGuard routeKey="VISIT_LIST">
                  {effectiveRole === 'DEPARTMENT_LEAD' ? <DeptLeadVisitTasksPage /> : <VisitRequestManagement />}
                </RouteAccessGuard>
              }
            />
            <Route path="visit/invitations/:participantId" element={<RouteAccessGuard routeKey="VISIT_INVITATION"><VisitParticipantInvitationDetail /></RouteAccessGuard>} />
            <Route path="visit/department-tasks/:participantId" element={<RouteAccessGuard routeKey="VISIT_INVITATION"><VisitParticipantInvitationDetail /></RouteAccessGuard>} />
            <Route path="visit/create" element={<RouteAccessGuard routeKey="VISIT_CREATE"><CreateVisitRequestEntry /></RouteAccessGuard>} />
            <Route path="visit/v2/:visitRequestId" element={<RouteAccessGuard routeKey="VISIT_DETAIL"><VisitRequestV2DetailPage /></RouteAccessGuard>} />
            <Route path="visit/v2/:visitRequestId/edit" element={<RouteAccessGuard routeKey="VISIT_EDIT"><EditVisitRequestV2Page mode="edit" /></RouteAccessGuard>} />
            <Route path="visit/v2/:visitRequestId/resubmit" element={<RouteAccessGuard routeKey="VISIT_EDIT"><EditVisitRequestV2Page mode="resubmit" /></RouteAccessGuard>} />
            {/* One campus of a request that may well be mixed. Same guard as the whole-request edit —
                the screen itself renders only on the backend's EDIT_PENDING_CAMPUS verdict. */}
            <Route path="visit/v2/:visitRequestId/campus/:visitInstanceId/edit" element={<RouteAccessGuard routeKey="VISIT_EDIT"><EditPendingCampusV2Page /></RouteAccessGuard>} />
            <Route path="visit/agenda-templates" element={<RouteAccessGuard routeKey="AGENDA_TEMPLATE"><AgendaTemplateManagement /></RouteAccessGuard>} />
            {/* Ảnh đoàn khách: guard chỉ chặn theo role; ảnh của instance nào thuộc về ai
                do backend quyết định theo assignment. */}
            <Route path="visit-photos" element={<RouteAccessGuard routeKey="VISIT_PHOTOS"><VisitPhotoManagement /></RouteAccessGuard>} />
            <Route path="visit/process/:id" element={<RouteAccessGuard routeKey="VISIT_PROCESS"><VisitProcess /></RouteAccessGuard>} />
            <Route path="visit/feedback/:visitInstanceId" element={<RouteAccessGuard routeKey="VISIT_FEEDBACK"><VisitFeedbackPage /></RouteAccessGuard>} />
            <Route path="visit/process-summary/:visitInstanceId" element={<RouteAccessGuard routeKey="VISIT_PROCESS"><VisitProcessSummaryPage /></RouteAccessGuard>} />
            <Route path="visit/contribution/:visitInstanceId" element={<RouteAccessGuard routeKey="VISIT_PROCESS"><VisitContributionPage /></RouteAccessGuard>} />
            <Route path="visit/reception-detail/:id" element={<RouteAccessGuard routeKey="VISIT_PROCESS"><VisitProcess /></RouteAccessGuard>} />
            <Route path="visit/ho-detail/:id" element={<RouteAccessGuard routeKey="VISIT_PROCESS"><HoVisitProcessDetail /></RouteAccessGuard>} />
            <Route path="visit/process/:id/request/:type" element={<RouteAccessGuard routeKey="VISIT_PROCESS"><VisitRequestDetail /></RouteAccessGuard>} />

            <Route path="documents" element={<RouteAccessGuard routeKey="DOCUMENTS"><DocumentManagement /></RouteAccessGuard>} />
            <Route path="gallery" element={<RouteAccessGuard routeKey="GALLERY"><GalleryManagement /></RouteAccessGuard>} />
            <Route path="gallery/locations" element={<RouteAccessGuard routeKey="GALLERY_LOCATIONS"><LocationManagement /></RouteAccessGuard>} />
            <Route path="minutes" element={<RouteAccessGuard routeKey="MINUTES"><MinuteManagement /></RouteAccessGuard>} />
            <Route path="post-visit-tasks" element={<RouteAccessGuard routeKey="POST_VISIT_TASKS"><PostVisitTaskManagement /></RouteAccessGuard>} />

            {/* One reports route, three role-specific screens. The guard decides who gets in;
                this picks which report view they see once inside. */}
            <Route
              path="reports"
              element={
                <RouteAccessGuard routeKey="REPORTS">
                  {effectiveRole === 'HO' ? <HoReportManagement />
                    : effectiveRole === 'STAFF_LEADER' ? <StaffLeaderReportManagement />
                      : <DeptReportManagement />}
                </RouteAccessGuard>
              }
            />
            <Route path="feedback" element={<RouteAccessGuard routeKey="FEEDBACK"><FeedbackManagement /></RouteAccessGuard>} />
            <Route path="feedback/:id" element={<RouteAccessGuard routeKey="FEEDBACK"><FeedbackDetail /></RouteAccessGuard>} />

            {/* System Administration Console (ADMIN-only) */}
            <Route path="apis" element={<RouteAccessGuard routeKey="API_MANAGEMENT"><ApiManagement /></RouteAccessGuard>} />
            <Route path="admin/sessions" element={<RouteAccessGuard routeKey="ADMIN_SESSIONS"><SessionManagement /></RouteAccessGuard>} />
            <Route path="admin/security" element={<RouteAccessGuard routeKey="ADMIN_SECURITY"><SecurityMonitoring /></RouteAccessGuard>} />
            <Route path="admin/audit-logs" element={<RouteAccessGuard routeKey="ADMIN_AUDIT_LOGS"><AuditLogManagement /></RouteAccessGuard>} />
          </Route>
          
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </main>

      {!isDashboardRoute && <Footer />}
    </div>
    </PerCampusV2CapabilityProvider>
  );
}
