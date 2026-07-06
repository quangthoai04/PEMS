/**
 * Component WelcomeHero
 * Chào mừng người dùng nội bộ, hiển thị role/campus/department, CTA vào Dashboard.
 */

import React from 'react';
import { useNavigate } from 'react-router-dom';
import { LayoutDashboard, Newspaper, School, Building2 } from 'lucide-react';
import type { AuthUser } from '../../../features/authentication/types/authentication.types';
import { getDashboardRoute } from '../../../shared/auth/dashboardRoute';

const ROLE_LABELS: Record<string, string> = {
  ADMIN: 'Quản trị viên',
  HO: 'Head Office',
  STAFF: 'Nhân viên Phòng HTQT',
  DEPARTMENT: 'Nhân viên Phòng ban',
  STUDENT: 'Sinh viên',
  VISITOR: 'Khách',
};

interface WelcomeHeroProps {
  user: AuthUser;
}

export function WelcomeHero({ user }: WelcomeHeroProps) {
  const navigate = useNavigate();
  const roleLabel = user.roleName ?? ROLE_LABELS[user.roleCode] ?? user.roleCode;

  return (
    <section className="relative pt-16 pb-12 lg:pt-24 lg:pb-16 bg-gradient-to-br from-fpt-navy to-[#003360] overflow-hidden">
      <div className="absolute inset-0 opacity-[0.06] pointer-events-none" style={{ backgroundImage: 'radial-gradient(#fff 2px, transparent 2px)', backgroundSize: '36px 36px' }}></div>
      <div className="absolute right-[-10%] top-[-20%] w-96 h-96 bg-fpt-orange/10 rounded-full blur-[100px] pointer-events-none"></div>

      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 relative z-10">
        <span className="inline-block py-1 px-3 rounded-full bg-fpt-orange shadow-sm text-white font-semibold text-xs mb-4">
          Cổng thông tin nội bộ PEMS
        </span>
        <h1 className="text-3xl md:text-4xl lg:text-5xl font-bold text-white tracking-tight leading-tight mb-3">
          Xin chào, {user.fullName}
        </h1>

        <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-blue-100 mb-10">
          <span className="inline-flex items-center gap-1.5 font-medium">
            <LayoutDashboard className="w-4 h-4" /> {roleLabel}
          </span>
          {user.campusName && (
            <span className="inline-flex items-center gap-1.5 font-medium">
              <School className="w-4 h-4" /> {user.campusName}
            </span>
          )}
          {user.departmentName && (
            <span className="inline-flex items-center gap-1.5 font-medium">
              <Building2 className="w-4 h-4" /> {user.departmentName}
            </span>
          )}
        </div>

        <div className="flex flex-col sm:flex-row gap-4">
          <button
            onClick={() => navigate(getDashboardRoute(user))}
            className="inline-flex items-center justify-center gap-2 bg-fpt-orange text-white font-bold px-6 py-3.5 rounded-2xl hover:bg-fpt-orange-hover hover:-translate-y-0.5 transition-all duration-300 shadow-xl"
          >
            <LayoutDashboard className="w-5 h-5" />
            Vào Dashboard
          </button>
          <button
            onClick={() => navigate('/news')}
            className="inline-flex items-center justify-center gap-2 bg-white/10 text-white font-semibold px-6 py-3.5 rounded-2xl border border-white/20 hover:bg-white/20 transition-all duration-300"
          >
            <Newspaper className="w-5 h-5" />
            Tin tức mới
          </button>
        </div>
      </div>
    </section>
  );
}
