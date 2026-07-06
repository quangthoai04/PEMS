/**
 * Component QuickAccessSection
 * Shortcut theo role tới các khu vực Dashboard hiện có — không hiển thị số liệu/task list
 * (những con số đó đã có sẵn trong Dashboard của từng role).
 */

import React from 'react';
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

interface QuickLink {
  label: string;
  description: string;
  to: string;
  icon: LucideIcon;
}

function buildLinks(bucket: HomeRoleBucket, user: AuthUser): QuickLink[] {
  switch (bucket) {
    case 'STUDENT':
      return [
        { label: 'Student Portal', description: 'Vào không gian làm việc của bạn', to: '/dashboard', icon: LayoutDashboard },
        { label: 'Lời mời tham gia hỗ trợ', description: 'Xem và phản hồi lời mời tham gia đoàn', to: '/dashboard/visit', icon: Briefcase },
        { label: 'Câu hỏi thường gặp', description: 'Hướng dẫn khi tham gia hỗ trợ đoàn', to: '/faq', icon: HelpCircle },
      ];
    case 'HO':
      return [
        { label: 'HO Dashboard', description: 'Theo dõi visit và hoạt động theo scope', to: '/dashboard', icon: LayoutDashboard },
        { label: 'Quản lý tiếp khách', description: 'Theo dõi yêu cầu tham quan liên cơ sở', to: '/dashboard/visit', icon: Briefcase },
        { label: 'Quản lý tin tức', description: 'Duyệt và quản lý tin tức', to: '/dashboard/news', icon: Newspaper },
        { label: 'Quản lý FAQ', description: 'Quản lý câu hỏi thường gặp', to: '/dashboard/faq', icon: HelpCircle },
        { label: 'Quản lý Campus', description: 'Quản lý danh sách campus', to: '/dashboard/campus', icon: MapPin },
      ];
    case 'ADMIN':
      return [
        { label: 'Admin Dashboard', description: 'Tổng quan hệ thống', to: '/dashboard', icon: LayoutDashboard },
        { label: 'Quản lý API tích hợp', description: 'Cấu hình các API tích hợp hệ thống', to: '/dashboard/apis', icon: Cpu },
      ];
    case 'STAFF_LEADER':
      return [
        { label: 'Campus Dashboard', description: 'Tổng quan hoạt động campus', to: '/dashboard', icon: LayoutDashboard },
        { label: 'Quản lý tiếp khách', description: 'Duyệt đơn và gán host', to: '/dashboard/visit', icon: Briefcase },
        { label: 'Quản lý tài khoản', description: 'Quản lý tài khoản trong campus', to: '/dashboard/accounts', icon: UserCog },
        { label: 'Quản lý Gallery', description: 'Quản lý ảnh/video Visit FPTU', to: '/dashboard/gallery', icon: Image },
        { label: 'Quản lý tin tức', description: 'Duyệt và quản lý tin tức', to: '/dashboard/news', icon: Newspaper },
      ];
    case 'STAFF':
      return [
        { label: 'My Workspace', description: 'Vào không gian làm việc của bạn', to: '/dashboard', icon: LayoutDashboard },
        { label: 'Đơn phụ trách', description: 'Xem các đơn tham quan bạn phụ trách', to: '/dashboard/visit', icon: Briefcase },
        { label: 'Biên bản & Tin tức', description: 'Đóng góp biên bản, tin tức, hình ảnh', to: '/dashboard/minutes', icon: ClipboardList },
      ];
    case 'DEPT_LEADER':
      return [
        { label: 'Department Dashboard', description: 'Tổng quan hoạt động phòng ban', to: '/dashboard', icon: LayoutDashboard },
        {
          label: 'Phòng ban của tôi',
          description: 'Phân công nhân sự, quản lý nhiệm vụ phòng ban',
          to: user.departmentId ? `/dashboard/departments/${user.departmentId}` : '/dashboard',
          icon: Building2,
        },
        { label: 'Báo cáo phòng ban', description: 'Xem báo cáo hoạt động', to: '/dashboard/reports', icon: BarChart2 },
      ];
    case 'DEPT_STAFF':
      return [
        { label: 'My Tasks', description: 'Vào không gian làm việc của bạn', to: '/dashboard', icon: LayoutDashboard },
        { label: 'Tin tức', description: 'Xem tin tức, đóng góp media hỗ trợ', to: '/dashboard/news', icon: Newspaper },
      ];
    default:
      return [];
  }
}

interface QuickAccessSectionProps {
  user: AuthUser;
}

export function QuickAccessSection({ user }: QuickAccessSectionProps) {
  const bucket = resolveHomeRoleBucket(user);
  if (!bucket) return null;

  const links = buildLinks(bucket, user);
  if (links.length === 0) return null;

  return (
    <section className="py-14 sm:py-16 lg:py-20 bg-white relative overflow-hidden">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="mb-12">
          <h2 className="text-3xl font-bold text-fpt-navy">Truy cập nhanh</h2>
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
                  <h3 className="font-bold text-slate-900 mb-1">{link.label}</h3>
                  <p className="text-sm text-slate-500 leading-relaxed">{link.description}</p>
                </div>
              </Link>
            );
          })}
        </div>
      </div>
    </section>
  );
}
