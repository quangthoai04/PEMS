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
    <div id="dashboard-root" className="flex h-screen bg-[#fafafa] overflow-hidden flex-col lg:flex-row shadow-inner">
      {/* Khung layout tổng (h-screen/overflow-hidden trên #dashboard-root, max-h-screen/overflow-y-auto
          trên #dashboard-main) ép cứng đúng 1 màn hình — bất kỳ nội dung nào in bên trong (biên bản
          nhúng inline, không dùng portal) đều bị cắt ở ranh giới đó dù trang con đã tự reset overflow.
          Reset riêng 2 khung này cho print, không đụng lúc xem màn hình bình thường. */}
      <style type="text/css" media="print">
        {`
          #dashboard-root, #dashboard-main {
            display: block !important;
            height: auto !important;
            max-height: none !important;
            overflow: visible !important;
          }
        `}
      </style>
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
      <main id="dashboard-main" className="flex-1 max-h-screen overflow-y-auto bg-[#F8FAFC] relative flex flex-col">
        {/* Desktop notification bell: floating overlay in top right corner */}
        <div className="hidden lg:flex absolute top-3 right-6 z-40 pointer-events-none">
          <div className="pointer-events-auto bg-white/80 backdrop-blur-md shadow-sm rounded-full border border-gray-100 p-1">
             <NotificationBellButton variant="dashboard" />
          </div>
        </div>

        {/* Nội dung căn sát sidebar và tràn hết chiều ngang cho mọi role */}
        <div className={`flex-1 w-full max-w-none ${isSidebarCollapsed ? 'p-2 sm:p-3 md:p-4' : 'p-3 sm:p-4 md:p-5'}`}>
          <Outlet />
        </div>
      </main>
    </div>
  );
}
