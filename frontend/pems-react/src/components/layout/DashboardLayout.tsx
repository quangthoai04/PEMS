/**
 * Component DashboardLayout
 * Layout cấu trúc bố cục cho các trang quản trị (Dashboard).
 * Bao gồm Sidebar bên trái và nội dung chính bên phải.
 */

// Đây là component layout bao bọc toàn bộ các trang trong khu vực quản trị (Dashboard)
import React, { useState } from 'react';
import { Outlet } from 'react-router-dom';
import { Sidebar } from '../dashboard/Sidebar';
import { NotificationBellButton } from '../../features/notifications/components/NotificationBellButton';
import { Menu } from 'lucide-react';
import logo from '../../assets/images/2021-FPTU-Eng.png';

// Key localStorage lưu trạng thái thu gọn sidebar — dùng chung mọi trang, mọi role.
const SIDEBAR_COLLAPSED_KEY = 'pems_sidebar_collapsed';

export function DashboardLayout() {
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false);
  // Sidebar thu vào / mở ra (desktop) — persist qua localStorage để giữ sau reload.
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(
    () => localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === '1',
  );
  const toggleSidebarCollapsed = () =>
    setIsSidebarCollapsed((prev) => {
      localStorage.setItem(SIDEBAR_COLLAPSED_KEY, prev ? '0' : '1');
      return !prev;
    });

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
          <NotificationBellButton variant="dashboard" />
          <img src={logo} alt="FPT Logo" className="h-8 md:h-9 object-contain" />
        </div>
      </header>

      {/* Sidebar with mobile toggle hooks + desktop collapse */}
      <Sidebar
        isMobileOpen={isMobileSidebarOpen}
        onCloseMobile={() => setIsMobileSidebarOpen(false)}
        isCollapsed={isSidebarCollapsed}
        onToggleCollapsed={toggleSidebarCollapsed}
      />

      {/* Main dashboard content container */}
      <main className="flex-1 max-h-screen overflow-y-auto bg-[#F8FAFC] relative flex flex-col">
        {/* Desktop floating notification bell */}
        <div className="hidden lg:flex fixed top-4 right-6 z-40">
          <div className="bg-white/80 backdrop-blur-md shadow-sm rounded-full border border-gray-100 p-1">
             <NotificationBellButton variant="dashboard" />
          </div>
        </div>
        
        {/* Nội dung căn sát sidebar (không mx-auto để tránh khoảng trắng lớn bên trái);
            khi sidebar thu gọn thì dùng gần full chiều ngang. */}
        <div className={`flex-1 w-full ${isSidebarCollapsed ? 'p-2 sm:p-3 md:pl-4 md:pr-4 md:py-4 max-w-none' : 'p-3 sm:p-4 md:p-5 max-w-[1560px]'}`}>
          <Outlet />
        </div>
      </main>
    </div>
  );
}
