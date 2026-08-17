/**
 * Component Sidebar
 * Thanh điều hướng menu bên (Sidebar) dành cho khu vực Dashboard.
 * Phân quyền hiển thị theo vai trò người dùng (Admin, HO, Dept, v.v.).
 */


// Đây là component thanh bên (Sidebar) để điều hướng trong khu vực quản trị (Dashboard)
import React, { useState } from "react";
import { Link, NavLink, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import {
  Home,
  Newspaper,
  School,
  User,
  LogOut,
  ChevronDown,
  ChevronUp,
  ShieldCheck,
  Mail,
  Users,
  X,
  Building2,
  UserCog,
  Briefcase,
  HelpCircle,
  MapPin,
  FileText,
  ClipboardList,
  BarChart2,
  Shield,
  Cpu,
  Image,
  MessageSquare,
  PanelLeftClose,
  PanelLeftOpen,
  KeyRound,
  ScrollText,
  Camera,
  ListChecks,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import logo from "../../assets/images/2021-FPTU-Eng.png";
import avatarImg from "../../assets/Avatar/AvatarDefault.png";
import { motion, AnimatePresence } from "motion/react";
import { useAuth } from "../../shared/hooks/useAuth";
import { useAuthenticatedImage } from "../../shared/hooks/useAuthenticatedImage";
import {
  getVisibleSidebarItems,
  type DashboardRouteKey,
} from "../../shared/auth/dashboardRouteAccess";

/**
 * Icon per menu entry. Kept here rather than in dashboardRouteAccess.ts so the policy
 * module stays free of JSX and can be unit-tested as plain data.
 */
const ROUTE_ICONS: Partial<Record<DashboardRouteKey, LucideIcon>> = {
  DASHBOARD_HOME: Home,
  ACCOUNT_LIST: UserCog,
  ADMIN_SESSIONS: KeyRound,
  ADMIN_SECURITY: Shield,
  API_MANAGEMENT: Cpu,
  ADMIN_AUDIT_LOGS: ScrollText,
  NEWS_LIST: Newspaper,
  EMAIL_LIST: Mail,
  PARTNER_LIST: Users,
  DEPARTMENT_LIST: Building2,
  MY_DEPARTMENT: Building2,
  CAMPUS_LIST: MapPin,
  VISIT_LIST: Briefcase,
  VISIT_PHOTOS: Camera,
  POST_VISIT_TASKS: ListChecks,
  DOCUMENTS: FileText,
  GALLERY: Image,
  GALLERY_LOCATIONS: MapPin,
  MINUTES: ClipboardList,
  FEEDBACK: MessageSquare,
  REPORTS: BarChart2,
  FAQ_LIST: HelpCircle,
};


interface SidebarProps {
  isMobileOpen?: boolean;
  onCloseMobile?: () => void;
  /** Trạng thái thu gọn (desktop) — do DashboardLayout quản lý + persist localStorage. */
  isCollapsed?: boolean;
  onToggleCollapsed?: () => void;
}


export function Sidebar({ isMobileOpen = false, onCloseMobile, isCollapsed = false, onToggleCollapsed }: SidebarProps) {
  const [isProfileMenuOpen, setIsProfileMenuOpen] = useState(false);
  // Mobile drawer luôn hiển thị đầy đủ, chỉ desktop mới thu gọn.
  const collapsed = isCollapsed && !isMobileOpen;
  const navigate = useNavigate();
  const { logout, user: authUser, effectiveRole } = useAuth();
  const { t, i18n } = useTranslation(['visitRequestV2', 'publicLayout']);
  // Sidebar is shared by every role; only the Visitor-facing chrome was scoped for English
  // (same boundary as the "Đơn của tôi" list itself — see VisitRequestManagement.tsx's `tt`).
  // Every other role keeps reading Vietnamese regardless of the site-wide language setting.
  const isVisitor = effectiveRole === 'VISITOR';
  const tt = (key: string, options?: Record<string, unknown>) =>
    t(key, { ...(options || {}), lng: isVisitor ? i18n.language : 'vi' });

  // Menu comes from the same policy table the route guards use, so "menu hidden" and
  // "URL typed by hand" can never disagree. It used to be a second, hand-maintained role
  // matrix built from localStorage's `currentUser` — which drifted from App.tsx and could
  // be rewritten from devtools.
  const menuItems = getVisibleSidebarItems(effectiveRole);

  // Display-only profile fields, read from AuthContext instead of localStorage.
  // These never gate anything — authorization uses `effectiveRole` above.
  // displayRole intentionally shows the raw roleCode (STAFF, DEPARTMENT, ...) rather than the
  // effective role (STAFF_LEADER, DEPARTMENT_LEAD, ...): it is the label users already know,
  // and this card is cosmetic. Changing it would alter the UI for Staff Leader and Department
  // Lead without any security benefit.
  const displayName = authUser?.fullName ?? tt('publicLayout:sidebar.guestName');
  const displayCampus = authUser?.campusName ?? authUser?.campusCode ?? tt('publicLayout:sidebar.unknownCampus');
  // Every other role intentionally shows the raw roleCode (see comment above) — only VISITOR is
  // ever shown to an audience that switches language, so only VISITOR gets a localized label.
  const displayRole = isVisitor ? tt('publicLayout:roles.VISITOR') : (authUser?.roleCode ?? "GUEST");

  // Avatar comes from the shared AuthContext so it updates reactively right after an upload.
  // It lives behind an authenticated endpoint, so fetch it as a blob; fall back to the default.
  const fetchedAvatar = useAuthenticatedImage(authUser?.avatarUrl ?? null);
  const avatarSrc = fetchedAvatar ?? avatarImg;


  const handleLogout = async () => {
    // Navigate away FIRST: /dashboard/* is wrapped by <ProtectedRoute>, and logout()'s
    // state update flips isAuthenticated to false while we're still on that route —
    // if we awaited logout() before navigating, ProtectedRoute's own redirect to
    // /login can fire first and win the race against this navigate("/").
    navigate("/");
    if (onCloseMobile) onCloseMobile();
    // Clear the real session (token + pems_user + legacy currentUser) via the
    // auth context, otherwise the user stays authenticated after "logging out".
    await logout();
  };


  // Khi thu gọn: chỉ còn icon căn giữa (icon to hơn, vùng click cao hơn), ẩn label (span).
  const navItemClass = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-3 rounded-xl transition-colors font-medium ${collapsed
      ? "justify-center px-0 py-3.5 [&>span]:hidden [&>svg]:w-6 [&>svg]:h-6"
      : "px-4 py-3"
    } ${isActive
      ? "bg-[#d2e5f5] text-[#004c91]"
      : "text-gray-600 hover:bg-[#d2e5f5] hover:text-[#004c91]"
    }`;


  const getRoleIcon = () => {
    return <ShieldCheck className="w-4 h-4 flex-shrink-0" />;
  };


  const handleLinkClick = () => {
    if (onCloseMobile) {
      onCloseMobile();
    }
  };


  return (
    <>
      {/* Mobile background overlay */}
      <AnimatePresence>
        {isMobileOpen && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 bg-black/45 backdrop-blur-xs z-40 lg:hidden"
            onClick={onCloseMobile}
          />
        )}
      </AnimatePresence>


      <aside className={`${collapsed ? "w-[84px]" : "w-[290px]"} bg-white border-r border-gray-200 h-screen flex flex-col pt-6 pb-4 shadow-sm z-50 transition-all duration-300
        ${isMobileOpen ? "fixed top-0 left-0 h-full w-[290px]" : "hidden lg:flex lg:sticky lg:top-0"}
      `}>
        {/* Close mobile button */}
        {isMobileOpen && (
          <button
            onClick={onCloseMobile}
            className="absolute top-4 right-4 p-2 text-gray-500 hover:bg-gray-100 rounded-full lg:hidden"
            aria-label={tt('publicLayout:sidebar.closeMenu')}
          >
            <X className="w-5 h-5 text-gray-600" />
          </button>
        )}


        {/* Nút thu vào / mở ra — desktop, dùng chung mọi trang & mọi role */}
        {!isMobileOpen && (
          <button
            onClick={onToggleCollapsed}
            className="hidden lg:flex absolute -right-4 top-1/2 -translate-y-1/2 z-50 h-8 w-8 items-center justify-center rounded-full border border-slate-200 bg-white text-slate-500 shadow-md hover:text-[#004c91] hover:border-[#004c91]/40 hover:bg-blue-50 transition-all focus:outline-none focus:ring-2 focus:ring-[#004c91]/20"
            aria-label={collapsed ? tt('publicLayout:sidebar.expandMenu') : tt('publicLayout:sidebar.collapseMenu')}
            title={collapsed ? tt('publicLayout:sidebar.expandMenu') : tt('publicLayout:sidebar.collapseMenu')}
          >
            {collapsed ? <PanelLeftOpen className="w-5 h-5" /> : <PanelLeftClose className="w-5 h-5" />}
          </button>
        )}

        {/* Logo — click to go back to the public home page. Khi thu gọn: logo nhỏ + đẩy menu
            xuống thấp hơn (mb lớn hơn) để icon không dính sát logo. */}
        <div className={`flex justify-center flex-shrink-0 ${collapsed ? "px-2 mb-10 mt-2" : "px-6 mb-8"}`}>
          <Link to="/" className="block cursor-pointer" aria-label={tt('publicLayout:sidebar.backToHome')} title={tt('publicLayout:sidebar.backToHome')}>
            <img src={logo} alt="FPT University" className={`${collapsed ? "h-10" : "h-20"} object-contain transition-all duration-300`} />
          </Link>
        </div>


        {/* Navigation.
            One list for every role. Which entries appear is decided by
            getVisibleSidebarItems(effectiveRole), i.e. the same policy table the route
            guards read — so a visible item always leads somewhere the user can enter, and
            a hidden item is a 403 if typed into the address bar.
            ADMIN sees only the System Administration entries for exactly that reason: the
            policy grants ADMIN no business route, so no business entry can render here. */}
        <nav className={`flex-grow space-y-2 overflow-y-auto ${collapsed ? "px-3" : "px-4"}`}>
          {menuItems.map((item) => {
            const Icon = ROUTE_ICONS[item.key];
            // The rest of the sidebar policy table is Vietnamese-only data (see
            // dashboardRouteAccess.ts) — VISIT_LIST is the one entry a Visitor can see, so it's
            // the one entry that needs to follow the site language.
            const label = item.key === 'VISIT_LIST' ? tt('visitRequestV2:list.breadcrumb') : item.sidebarLabel;
            return (
              <NavLink
                key={item.key}
                to={item.path}
                // `end` keeps the Dashboard entry from staying highlighted on every
                // /dashboard/* child route.
                end={item.path === "/dashboard"}
                className={navItemClass}
                onClick={handleLinkClick}
                title={collapsed ? label : undefined}
              >
                {Icon ? <Icon className="w-5 h-5 flex-shrink-0" /> : null}
                <span className="flex items-center gap-1.5">
                  {label}
                </span>
              </NavLink>
            );
          })}
        </nav>


        {/* User Info */}
        <div className={`mt-6 relative flex-shrink-0 ${collapsed ? "px-2" : "px-4"}`}>
          <div
            className={`bg-white border text-left border-[#d2e5f5] hover:bg-[#e6eff7] rounded-2xl cursor-pointer hover:shadow-md transition-all relative z-10 ${collapsed ? "p-2" : "p-4"}`}
            onClick={() => setIsProfileMenuOpen(!isProfileMenuOpen)}
            title={collapsed ? `${displayName} (${displayRole})` : undefined}
          >
            <div className={`flex items-center ${collapsed ? "justify-center" : "gap-3"}`}>
              <div className={`${collapsed ? "w-10 h-10" : "w-14 h-14"} rounded-full overflow-hidden border-2 border-gray-100 bg-gray-50 flex-shrink-0 flex items-center justify-center`}>
                <img
                  src={avatarSrc}
                  alt="Avatar"
                  className="w-[115%] h-[115%] object-cover max-w-none"
                  onError={(e) => { e.currentTarget.src = avatarImg; }}
                />
              </div>
              {!collapsed && (
                <>
                  <div className="flex-1 min-w-0">
                    <p className="text-[17px] font-bold text-[#004c91] break-words leading-snug tracking-tight">
                      {displayName}
                    </p>
                    {/* Visitor is an external guest, not tied to any campus — showing "Campus
                        Không rõ" would just be noise. */}
                    {!isVisitor && (
                      <p className="text-[12px] text-[#004c91] flex items-center gap-1 mt-0.5 truncate font-medium">
                        <School className="w-3.5 h-3.5 flex-shrink-0" />
                        Campus {displayCampus}
                      </p>
                    )}
                    <p className="text-[13px] font-bold text-[#004c91] mt-1 flex items-center gap-1.5 truncate uppercase tracking-wide">
                      {getRoleIcon()}
                      {displayRole}
                    </p>
                  </div>
                  <div className="text-gray-400">
                    {isProfileMenuOpen ? (
                      <ChevronUp className="w-5 h-5" />
                    ) : (
                      <ChevronDown className="w-5 h-5" />
                    )}
                  </div>
                </>
              )}
            </div>
          </div>


          {isProfileMenuOpen && (
            <div
              className="fixed inset-0 z-10"
              onClick={() => setIsProfileMenuOpen(false)}
            />
          )}
          {/* Dropdown Menu */}
          <AnimatePresence>
            {isProfileMenuOpen && (
              <motion.div
                initial={{ opacity: 0, y: 10, scale: 0.95 }}
                animate={{ opacity: 1, y: 0, scale: 1 }}
                exit={{ opacity: 0, y: 10, scale: 0.95 }}
                transition={{ duration: 0.15 }}
                className={`absolute bottom-full mb-3 bg-white rounded-2xl shadow-xl border border-[#d2e5f5] overflow-hidden z-20 py-2 ${collapsed ? "left-2 w-56" : "left-4 right-4"}`}
              >
                <button
                  onClick={() => {
                    navigate("/");
                    handleLinkClick();
                  }}
                  className="w-full flex items-center gap-3 px-5 py-3 text-sm font-semibold text-gray-700 hover:text-[#004c91] hover:bg-[#d2e5f5] transition-colors"
                >
                  <Home className="w-4 h-4" />
                  {tt('publicLayout:profile.backToHome')}
                </button>
                <button
                  onClick={() => {
                    navigate("/dashboard/profile");
                    setIsProfileMenuOpen(false);
                    handleLinkClick();
                  }}
                  className="w-full flex items-center gap-3 px-5 py-3 text-sm font-semibold text-gray-700 hover:text-[#004c91] hover:bg-[#d2e5f5] transition-colors"
                >
                  <User className="w-4 h-4" />
                  {tt('publicLayout:profile.userProfile')}
                </button>
                <div className="h-px bg-gray-100 my-1 mx-4"></div>
                <button
                  onClick={handleLogout}
                  className="w-full flex items-center gap-3 px-5 py-3 text-sm font-semibold text-gray-700 hover:text-red-700 hover:bg-red-50 transition-colors"
                >
                  <LogOut className="w-4 h-4" />
                  {tt('publicLayout:profile.logout')}
                </button>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      </aside>
    </>
  );
}



