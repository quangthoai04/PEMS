/**
 * Component InternalFinalCta
 * CTA cuối trang cho homepage nội bộ — điều hướng vào Dashboard đúng role.
 */

import React from 'react';
import { useNavigate } from 'react-router-dom';
import { LayoutDashboard } from 'lucide-react';
import type { AuthUser } from '../../../features/authentication/types/authentication.types';
import { getDashboardRoute } from '../../../shared/auth/dashboardRoute';

interface InternalFinalCtaProps {
  user: AuthUser;
}

export function InternalFinalCta({ user }: InternalFinalCtaProps) {
  const navigate = useNavigate();

  return (
    <section className="py-14 sm:py-16 lg:py-20 bg-fpt-navy relative overflow-hidden">
      <div className="absolute inset-0 opacity-[0.05] pointer-events-none" style={{ backgroundImage: 'radial-gradient(#fff 2px, transparent 2px)', backgroundSize: '40px 40px' }}></div>

      <div className="max-w-3xl mx-auto px-4 sm:px-6 lg:px-8 text-center relative z-10">
        <h2 className="text-3xl md:text-4xl font-bold text-white mb-4">Sẵn sàng tiếp tục công việc?</h2>
        <p className="text-blue-200 text-lg mb-10">
          Vào Dashboard để xử lý các nhiệm vụ và yêu cầu đang chờ bạn.
        </p>

        <button
          onClick={() => navigate(getDashboardRoute(user))}
          className="inline-flex items-center justify-center gap-2 bg-fpt-orange text-white font-bold px-8 py-4 rounded-2xl hover:bg-fpt-orange-hover hover:-translate-y-1 transition-all duration-300 shadow-xl"
        >
          <LayoutDashboard className="w-5 h-5" />
          Vào Dashboard
        </button>
      </div>
    </section>
  );
}
