/**
 * Component WelcomeHero
 * Chào mừng người dùng nội bộ, hiển thị role/campus/department — nền banner ảnh cơ sở của user.
 */

import React from 'react';
import { LayoutDashboard, School, Building2 } from 'lucide-react';
import type { AuthUser } from '../../../features/authentication/types/authentication.types';

import bgHN from '../../../assets/FPTbanner_visit/hola_new.jpg';
import bgHCM from '../../../assets/FPTbanner_visit/HCM.png';
import bgDN from '../../../assets/FPTbanner_visit/DaNang.png';
import bgCT from '../../../assets/FPTbanner_visit/CanTho.png';
import bgQN from '../../../assets/FPTbanner_visit/QuyNhon.png';
import bgDefault from '../../../assets/FPTbanner_visit/5CS.png';

const ROLE_LABELS: Record<string, string> = {
  ADMIN: 'Quản trị viên',
  HO: 'Head Office',
  STAFF: 'Nhân viên Phòng HTQT',
  DEPARTMENT: 'Nhân viên Phòng ban',
  STUDENT: 'Sinh viên',
  VISITOR: 'Khách',
};

// Banner theo cơ sở (campusCode) — cùng bộ ảnh với trang VisitFPTU công khai. Không khớp/không có
// cơ sở (vd HO, Admin đa cơ sở) → dùng ảnh 5 cơ sở gộp làm mặc định.
const CAMPUS_BANNERS: Record<string, string> = {
  HN: bgHN,
  HCM: bgHCM,
  DN: bgDN,
  CT: bgCT,
  QN: bgQN,
};

interface WelcomeHeroProps {
  user: AuthUser;
}

export function WelcomeHero({ user }: WelcomeHeroProps) {
  const roleLabel = user.roleName ?? ROLE_LABELS[user.roleCode] ?? user.roleCode;
  const bannerBg = (user.campusCode && CAMPUS_BANNERS[user.campusCode]) || bgDefault;

  const textShadow = { textShadow: '0 2px 4px rgba(0,0,0,0.85), 0 1px 12px rgba(0,0,0,0.6)' } as const;
  const textStroke = { WebkitTextStroke: '0.6px rgba(0,0,0,0.35)', ...textShadow } as const;

  return (
    <section className="relative w-full aspect-[1512/700] min-h-[300px] flex items-end overflow-hidden">
      <div className="absolute inset-0">
        <img src={bannerBg} alt="" className="w-full h-full object-cover" />
        <div className="absolute inset-0 bg-fpt-navy/30"></div>
      </div>
      <div className="absolute inset-0 opacity-[0.06] pointer-events-none" style={{ backgroundImage: 'radial-gradient(#fff 2px, transparent 2px)', backgroundSize: '36px 36px' }}></div>
      <div className="absolute right-[-10%] top-[-20%] w-96 h-96 bg-fpt-orange/10 rounded-full blur-[100px] pointer-events-none"></div>

      <div className="w-full max-w-[1536px] mx-auto px-4 sm:px-6 lg:px-8 pb-6 sm:pb-8 lg:pb-10 relative z-10">
        <span className="inline-block py-1 px-3 rounded-full bg-fpt-orange shadow-sm text-white font-semibold text-xs mb-4">
          Cổng thông tin nội bộ PEMS
        </span>
        <h1
          className="text-3xl sm:text-4xl md:text-5xl lg:text-6xl font-bold text-white tracking-tight leading-tight mb-3"
          style={textStroke}
        >
          Xin chào, {user.fullName}
        </h1>

        <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-white text-base lg:text-lg" style={textShadow}>
          <span className="inline-flex items-center gap-1.5 font-medium">
            <LayoutDashboard className="w-4 h-4 lg:w-5 lg:h-5" /> {roleLabel}
          </span>
          {user.campusName && (
            <span className="inline-flex items-center gap-1.5 font-medium">
              <School className="w-4 h-4 lg:w-5 lg:h-5" /> {user.campusName}
            </span>
          )}
          {user.departmentName && (
            <span className="inline-flex items-center gap-1.5 font-medium">
              <Building2 className="w-4 h-4 lg:w-5 lg:h-5" /> {user.departmentName}
            </span>
          )}
        </div>
      </div>
    </section>
  );
}
