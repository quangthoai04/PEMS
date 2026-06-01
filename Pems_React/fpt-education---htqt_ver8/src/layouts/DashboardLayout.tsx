// Đây là component layout bao bọc toàn bộ các trang trong khu vực quản trị (Dashboard)
import React, { useState } from 'react';
import { Outlet } from 'react-router-dom';
import { Sidebar } from '../components/dashboard/Sidebar';
import { Menu } from 'lucide-react';
import logo from '../assets/images/2021-FPTU-Eng.png';

export function DashboardLayout() {
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false);

  return (
    <div className="flex h-screen bg-[#fafafa] overflow-hidden flex-col lg:flex-row">
      {/* Mobile top app bar */}
      <header className="lg:hidden bg-white border-b border-gray-200 px-4 py-3 flex items-center justify-between w-full h-16 shrink-0 z-30">
        <div className="flex items-center gap-3">
          <button
            onClick={() => setIsMobileSidebarOpen(true)}
            className="p-2 -ml-2 text-gray-600 hover:bg-gray-100 rounded-lg transition-colors"
            aria-label="Mở menu điều hướng"
          >
            <Menu className="w-6 h-6 text-[#004c91]" />
          </button>
          <span className="font-bold text-[#004c91] text-base">Dashboard</span>
        </div>
        <img src={logo} alt="FPT Logo" className="h-9 object-contain" />
      </header>

      {/* Sidebar with mobile toggle hooks */}
      <Sidebar 
        isMobileOpen={isMobileSidebarOpen} 
        onCloseMobile={() => setIsMobileSidebarOpen(false)} 
      />

      {/* Main dashboard content container */}
      <main className="flex-1 overflow-y-auto bg-[#f8f9fa] p-4 sm:p-6 md:p-8">
        <Outlet />
      </main>
    </div>
  );
}
