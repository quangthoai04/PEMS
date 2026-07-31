/**
 * Component QuickAccessSection
 * Shortcut theo role tới các khu vực Dashboard hiện có — không hiển thị số liệu/task list
 * (những con số đó đã có sẵn trong Dashboard của từng role).
 */

import React from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import type { LucideIcon } from 'lucide-react';
import {
  LayoutDashboard,
  Briefcase,
  HelpCircle,
  Newspaper,
  MapPin,
  UserCog,
  Image,
  ClipboardList,
  Building2,
  BarChart2,
  Cpu,
} from 'lucide-react';
import type { AuthUser } from '../../../features/authentication/types/authentication.types';
import { resolveHomeRoleBucket, HomeRoleBucket } from '../../../shared/auth/resolveHomeRoleBucket';

// Chỉ phần TEXT chuyển sang translation key. Route / icon / thứ tự / điều kiện hiển thị theo role
// giữ nguyên như cũ — đây vẫn là nguồn duy nhất quyết định card nào hiện cho bucket nào.
interface QuickLink {
  labelKey: string;
  descriptionKey: string;
  to: string;
  icon: LucideIcon;
}

function buildLinks(bucket: HomeRoleBucket, user: AuthUser): QuickLink[] {
  switch (bucket) {
    case 'STUDENT':
      return [
        { labelKey: 'internal.quickAccess.STUDENT.portal.label', descriptionKey: 'internal.quickAccess.STUDENT.portal.description', to: '/dashboard', icon: LayoutDashboard },
        { labelKey: 'internal.quickAccess.STUDENT.supportInvitations.label', descriptionKey: 'internal.quickAccess.STUDENT.supportInvitations.description', to: '/dashboard/visit', icon: Briefcase },
        { labelKey: 'internal.quickAccess.STUDENT.faq.label', descriptionKey: 'internal.quickAccess.STUDENT.faq.description', to: '/faq', icon: HelpCircle },
      ];
    case 'HO':
      return [
        { labelKey: 'internal.quickAccess.HO.dashboard.label', descriptionKey: 'internal.quickAccess.HO.dashboard.description', to: '/dashboard', icon: LayoutDashboard },
        { labelKey: 'internal.quickAccess.HO.visitManagement.label', descriptionKey: 'internal.quickAccess.HO.visitManagement.description', to: '/dashboard/visit', icon: Briefcase },
        { labelKey: 'internal.quickAccess.HO.newsManagement.label', descriptionKey: 'internal.quickAccess.HO.newsManagement.description', to: '/dashboard/news', icon: Newspaper },
        { labelKey: 'internal.quickAccess.HO.faqManagement.label', descriptionKey: 'internal.quickAccess.HO.faqManagement.description', to: '/dashboard/faq', icon: HelpCircle },
        { labelKey: 'internal.quickAccess.HO.campusManagement.label', descriptionKey: 'internal.quickAccess.HO.campusManagement.description', to: '/dashboard/campus', icon: MapPin },
      ];
    case 'ADMIN':
      return [
        { labelKey: 'internal.quickAccess.ADMIN.dashboard.label', descriptionKey: 'internal.quickAccess.ADMIN.dashboard.description', to: '/dashboard', icon: LayoutDashboard },
        { labelKey: 'internal.quickAccess.ADMIN.apiIntegrations.label', descriptionKey: 'internal.quickAccess.ADMIN.apiIntegrations.description', to: '/dashboard/apis', icon: Cpu },
      ];
    case 'STAFF_LEADER':
      return [
        { labelKey: 'internal.quickAccess.STAFF_LEADER.dashboard.label', descriptionKey: 'internal.quickAccess.STAFF_LEADER.dashboard.description', to: '/dashboard', icon: LayoutDashboard },
        { labelKey: 'internal.quickAccess.STAFF_LEADER.visitManagement.label', descriptionKey: 'internal.quickAccess.STAFF_LEADER.visitManagement.description', to: '/dashboard/visit', icon: Briefcase },
        { labelKey: 'internal.quickAccess.STAFF_LEADER.accountManagement.label', descriptionKey: 'internal.quickAccess.STAFF_LEADER.accountManagement.description', to: '/dashboard/accounts', icon: UserCog },
        { labelKey: 'internal.quickAccess.STAFF_LEADER.galleryManagement.label', descriptionKey: 'internal.quickAccess.STAFF_LEADER.galleryManagement.description', to: '/dashboard/gallery', icon: Image },
        { labelKey: 'internal.quickAccess.STAFF_LEADER.newsManagement.label', descriptionKey: 'internal.quickAccess.STAFF_LEADER.newsManagement.description', to: '/dashboard/news', icon: Newspaper },
      ];
    case 'STAFF':
      return [
        { labelKey: 'internal.quickAccess.STAFF.workspace.label', descriptionKey: 'internal.quickAccess.STAFF.workspace.description', to: '/dashboard', icon: LayoutDashboard },
        { labelKey: 'internal.quickAccess.STAFF.assignedRequests.label', descriptionKey: 'internal.quickAccess.STAFF.assignedRequests.description', to: '/dashboard/visit', icon: Briefcase },
        { labelKey: 'internal.quickAccess.STAFF.minutesAndNews.label', descriptionKey: 'internal.quickAccess.STAFF.minutesAndNews.description', to: '/dashboard/minutes', icon: ClipboardList },
      ];
    case 'DEPT_LEADER':
      return [
        { labelKey: 'internal.quickAccess.DEPT_LEADER.dashboard.label', descriptionKey: 'internal.quickAccess.DEPT_LEADER.dashboard.description', to: '/dashboard', icon: LayoutDashboard },
        {
          labelKey: 'internal.quickAccess.DEPT_LEADER.myDepartment.label',
          descriptionKey: 'internal.quickAccess.DEPT_LEADER.myDepartment.description',
          to: user.departmentId ? `/dashboard/departments/${user.departmentId}` : '/dashboard',
          icon: Building2,
        },
        { labelKey: 'internal.quickAccess.DEPT_LEADER.departmentReports.label', descriptionKey: 'internal.quickAccess.DEPT_LEADER.departmentReports.description', to: '/dashboard/reports', icon: BarChart2 },
      ];
    case 'DEPT_STAFF':
      return [
        { labelKey: 'internal.quickAccess.DEPT_STAFF.myTasks.label', descriptionKey: 'internal.quickAccess.DEPT_STAFF.myTasks.description', to: '/dashboard', icon: LayoutDashboard },
        { labelKey: 'internal.quickAccess.DEPT_STAFF.news.label', descriptionKey: 'internal.quickAccess.DEPT_STAFF.news.description', to: '/dashboard/news', icon: Newspaper },
      ];
    default:
      return [];
  }
}

interface QuickAccessSectionProps {
  user: AuthUser;
}

export function QuickAccessSection({ user }: QuickAccessSectionProps) {
  const { t } = useTranslation('home');
  const bucket = resolveHomeRoleBucket(user);
  if (!bucket) return null;

  const links = buildLinks(bucket, user);
  if (links.length === 0) return null;

  return (
    <section className="py-14 sm:py-16 lg:py-20 bg-white relative overflow-hidden">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="mb-12">
          <h2 className="text-3xl font-bold text-fpt-navy">{t('internal.quickAccess.title')}</h2>
          <div className="w-16 h-1.5 bg-fpt-orange mt-4 rounded-full"></div>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          {links.map((link, idx) => {
            const Icon = link.icon;
            return (
              <Link
                key={idx}
                to={link.to}
                className="group flex items-start gap-4 bg-white border border-slate-200 rounded-2xl p-6 hover:shadow-lg hover:border-fpt-navy/30 hover:-translate-y-0.5 transition-all duration-300"
              >
                <div className="w-11 h-11 rounded-xl bg-fpt-navy/10 flex items-center justify-center flex-shrink-0 group-hover:bg-fpt-navy transition-colors">
                  <Icon className="w-5 h-5 text-fpt-navy group-hover:text-white transition-colors" />
                </div>
                <div>
                  <h3 className="font-bold text-slate-900 mb-1">{t(link.labelKey)}</h3>
                  <p className="text-sm text-slate-500 leading-relaxed">{t(link.descriptionKey)}</p>
                </div>
              </Link>
            );
          })}
        </div>
      </div>
    </section>
  );
}
