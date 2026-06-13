/**
 * Component DashboardLayout
 * Layout cấu trúc bố cục cho các trang quản trị (Dashboard).
 * Bao gồm Sidebar bên trái và nội dung chính bên phải.
 */

// Đây là component layout bao bọc toàn bộ các trang trong khu vực quản trị (Dashboard)
import React, { useState } from 'react';
import { Outlet } from 'react-router-dom';
import { Sidebar } from '../dashboard/Sidebar';
import { NotificationBell } from '../dashboard/NotificationBell';
import { Menu } from 'lucide-react';
import logo from '../../assets/images/2021-FPTU-Eng.png';

export function DashboardLayout() {
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false);

  return (
    <div className="flex h-screen bg-[#fafafa] overflow-hidden flex-col lg:flex-row shadow-inner">
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
        <div className="flex items-center gap-2">
          <NotificationBell />
          <img src={logo} alt="FPT Logo" className="h-8 md:h-9 object-contain" />
        </div>
      </header>

      {/* Sidebar with mobile toggle hooks */}
      <Sidebar 
        isMobileOpen={isMobileSidebarOpen} 
        onCloseMobile={() => setIsMobileSidebarOpen(false)} 
      />

      {/* Main dashboard content container */}
      <main className="flex-1 max-h-screen overflow-y-auto bg-[#F8FAFC] relative flex flex-col">
        {/* Desktop floating notification bell */}
        <div className="hidden lg:flex fixed top-4 right-6 z-40">
          <div className="bg-white/80 backdrop-blur-md shadow-sm rounded-full border border-gray-100 p-1">
             <NotificationBell />
          </div>
        </div>
        
        <div className="p-4 sm:p-6 md:p-8 flex-1 w-full max-w-7xl mx-auto">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
