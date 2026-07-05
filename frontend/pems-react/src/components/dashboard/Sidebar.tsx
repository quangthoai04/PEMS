/**
 * Component Sidebar
 * Thanh điều hướng menu bên (Sidebar) dành cho khu vực Dashboard.
 * Phân quyền hiển thị theo vai trò người dùng (Admin, HO, Dept, v.v.).
 */


// Đây là component thanh bên (Sidebar) để điều hướng trong khu vực quản trị (Dashboard)
import React, { useState } from "react";
import { Link, NavLink, useNavigate } from "react-router-dom";
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
} from "lucide-react";
import logo from "../../assets/images/2021-FPTU-Eng.png";
import avatarImg from "../../assets/Avatar/AvatarDefault.png";
import { motion, AnimatePresence } from "motion/react";
import { useAuth } from "../../shared/hooks/useAuth";
import { useAuthenticatedImage } from "../../shared/hooks/useAuthenticatedImage";


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
  const { logout, user: authUser } = useAuth();

  // Get user from localStorage (name / campus / role display — unchanged).
  const userStr = localStorage.getItem("currentUser");
  const user = userStr
    ? JSON.parse(userStr)
    : {
      name: "Khách",
      campus: "Không rõ",
      role: "GUEST",
    };

  // Avatar comes from the shared AuthContext so it updates reactively right after an upload.
  // It lives behind an authenticated endpoint, so fetch it as a blob; fall back to the default.
  const fetchedAvatar = useAuthenticatedImage(authUser?.avatarUrl ?? null);
  const avatarSrc = fetchedAvatar ?? avatarImg;


  const isStaffLeader = user?.role?.toUpperCase() === 'STAFF' && user?.subRole?.toUpperCase() === 'LEADER';
  const roleForSidebar = user?.role?.toUpperCase() || 'GUEST';
  const isDeptLeader = roleForSidebar === 'DEPARTMENT' && user?.subRole?.toUpperCase() === 'LEADER';
  // isDeptStaff: DEPARTMENT + không phải LEADER (bao gồm subRole null/undefined)
  const isDeptStaff = roleForSidebar === 'DEPARTMENT' && !isDeptLeader;
  const isRealAdmin = roleForSidebar === 'ADMIN';


  const handleLogout = async () => {
    // Clear the real session (token + pems_user + legacy currentUser) via the
    // auth context, otherwise the user stays authenticated after "logging out".
    await logout();
    navigate("/");
    if (onCloseMobile) onCloseMobile();
  };


  // Khi thu gọn: chỉ còn icon căn giữa (icon to hơn, vùng click cao hơn), ẩn label (span).
  const navItemClass = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-3 rounded-xl transition-colors font-medium ${
      collapsed
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
            aria-label="Đóng menu"
          >
            <X className="w-5 h-5 text-gray-600" />
          </button>
        )}


        {/* Nút thu vào / mở ra — desktop, dùng chung mọi trang & mọi role */}
        {!isMobileOpen && (
          <button
            onClick={onToggleCollapsed}
            className="hidden lg:flex absolute -right-4 top-1/2 -translate-y-1/2 z-50 h-8 w-8 items-center justify-center rounded-full border border-slate-200 bg-white text-slate-500 shadow-md hover:text-[#004c91] hover:border-[#004c91]/40 hover:bg-blue-50 transition-all focus:outline-none focus:ring-2 focus:ring-[#004c91]/20"
            aria-label={collapsed ? "Mở rộng menu" : "Thu gọn menu"}
            title={collapsed ? "Mở rộng menu" : "Thu gọn menu"}
          >
            {collapsed ? <PanelLeftOpen className="w-5 h-5" /> : <PanelLeftClose className="w-5 h-5" />}
          </button>
        )}

        {/* Logo — click to go back to the public home page. Khi thu gọn: logo nhỏ + đẩy menu
            xuống thấp hơn (mb lớn hơn) để icon không dính sát logo. */}
        <div className={`flex justify-center flex-shrink-0 ${collapsed ? "px-2 mb-10 mt-2" : "px-6 mb-8"}`}>
          <Link to="/" className="block cursor-pointer" aria-label="Về trang chủ" title="Về trang chủ">
            <img src={logo} alt="FPT University" className={`${collapsed ? "h-10" : "h-20"} object-contain transition-all duration-300`} />
          </Link>
        </div>


        {/* Navigation */}
        <nav className={`flex-grow space-y-2 overflow-y-auto ${collapsed ? "px-3" : "px-4"}`}>
          <NavLink to="/dashboard" end className={navItemClass} onClick={handleLinkClick}>
            <Home className="w-5 h-5 flex-shrink-0" />
            <span>Dashboard</span>
          </NavLink>
          {roleForSidebar !== "DEPARTMENT" && roleForSidebar !== "VISITOR" && !isRealAdmin && (
            <NavLink to="/dashboard/news" className={navItemClass} onClick={handleLinkClick}>
              <Newspaper className="w-5 h-5 flex-shrink-0" />
              <span>Quản lý tin tức</span>
            </NavLink>
          )}
          {(["HO", "STAFF", "DEPARTMENT"].includes(roleForSidebar)) && !isRealAdmin && !isDeptStaff && (
            <NavLink to="/dashboard/email" className={navItemClass} onClick={handleLinkClick}>
              <Mail className="w-5 h-5 flex-shrink-0" />
              <span>Quản lý email</span>
            </NavLink>
          )}
          {["STAFF", "ADMIN", "HO"].includes(
            roleForSidebar,
          ) && !isRealAdmin && (
              <NavLink to="/dashboard/partners" className={navItemClass} onClick={handleLinkClick}>
                <Users className="w-5 h-5 flex-shrink-0" />
                <span>Quản lý đối tác</span>
              </NavLink>
            )}
          {(((["ADMIN", "DEPARTMENT"].includes(roleForSidebar) && !isRealAdmin) || isStaffLeader) && !isDeptStaff) && (
            <NavLink to={roleForSidebar === "DEPARTMENT" ? `/dashboard/departments/${user?.departmentId || '1'}` : "/dashboard/departments"} className={navItemClass} end={roleForSidebar !== "DEPARTMENT"} onClick={handleLinkClick}>
              <Building2 className="w-5 h-5 flex-shrink-0" />
              <span>Quản lý phòng ban</span>
            </NavLink>
          )}
          {(roleForSidebar === "HO" || isStaffLeader) && (
            <NavLink to="/dashboard/accounts" className={navItemClass} onClick={handleLinkClick}>
              <UserCog className="w-5 h-5 flex-shrink-0" />
              <span>Quản lý tài khoản</span>
            </NavLink>
          )}
          {roleForSidebar === "HO" && (
            <NavLink to="/dashboard/campus" className={navItemClass} onClick={handleLinkClick}>
              <MapPin className="w-5 h-5 flex-shrink-0" />
              <span>Quản lý campus</span>
            </NavLink>
          )}
          {(["HO", "STAFF", "DEPARTMENT", "STUDENT", "VISITOR"].includes(roleForSidebar)) && !isDeptStaff && (
            <NavLink to="/dashboard/visit" className={navItemClass} onClick={handleLinkClick}>
              <Briefcase className="w-5 h-5 flex-shrink-0" />
              <span>Quản lý tiếp khách</span>
            </NavLink>
          )}
          {(["HO", "STAFF"].includes(roleForSidebar)) && (
            <>
              <NavLink to="/dashboard/documents" className={navItemClass} onClick={handleLinkClick}>
                <FileText className="w-5 h-5 flex-shrink-0" />
                <span>Quản lý tài liệu</span>
              </NavLink>
              {(roleForSidebar !== 'HO' && isStaffLeader) && (
                <NavLink to="/dashboard/gallery" className={navItemClass} onClick={handleLinkClick}>
                  <Image className="w-5 h-5 flex-shrink-0" />
                  <span>Quản lý Gallery</span>
                </NavLink>
              )}
              <NavLink to="/dashboard/minutes" className={navItemClass} onClick={handleLinkClick}>
                <ClipboardList className="w-5 h-5 flex-shrink-0" />
                <span>Quản lý biên bản</span>
              </NavLink>
              <NavLink to="/dashboard/feedback" className={navItemClass} onClick={handleLinkClick}>
                <MessageSquare className="w-5 h-5 flex-shrink-0" />
                <span>Quản lý feedback</span>
              </NavLink>
            </>
          )}
          {(roleForSidebar === "HO" || isStaffLeader || isDeptLeader) && (
            <NavLink to="/dashboard/reports" className={navItemClass} onClick={handleLinkClick}>
              <BarChart2 className="w-5 h-5 flex-shrink-0" />
              <span>Quản lý báo cáo</span>
            </NavLink>
          )}
          {["HO"].includes(roleForSidebar) && !isStaffLeader && (
            <NavLink to="/dashboard/faq" className={navItemClass} onClick={handleLinkClick}>
              <HelpCircle className="w-5 h-5 flex-shrink-0" />
              <span>Quản lý FAQ</span>
            </NavLink>
          )}
          {roleForSidebar === "ADMIN" && (
            <>
              <NavLink to="/dashboard/apis" className={navItemClass} onClick={handleLinkClick}>
                <Cpu className="w-5 h-5 flex-shrink-0" />
                <span>Quản lý API</span>
              </NavLink>
            </>
          )}
        </nav>


        {/* User Info */}
        <div className={`mt-6 relative flex-shrink-0 ${collapsed ? "px-2" : "px-4"}`}>
          <div
            className={`bg-white border text-left border-[#d2e5f5] hover:bg-[#e6eff7] rounded-2xl cursor-pointer hover:shadow-md transition-all relative z-10 ${collapsed ? "p-2" : "p-4"}`}
            onClick={() => setIsProfileMenuOpen(!isProfileMenuOpen)}
            title={collapsed ? `${user.name} (${user.role})` : undefined}
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
                    <p className="text-[17px] font-bold text-[#004c91] truncate tracking-tight">
                      {user.name}
                    </p>
                    <p className="text-[12px] text-[#004c91] flex items-center gap-1 mt-0.5 truncate font-medium">
                      <School className="w-3.5 h-3.5 flex-shrink-0" />
                      Campus {user.campus}
                    </p>
                    <p className="text-[13px] font-bold text-[#004c91] mt-1 flex items-center gap-1.5 truncate uppercase tracking-wide">
                      {getRoleIcon()}
                      {user.role}
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
                  Quay về trang chủ
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
                  Hồ sơ cá nhân
                </button>
                <div className="h-px bg-gray-100 my-1 mx-4"></div>
                <button
                  onClick={handleLogout}
                  className="w-full flex items-center gap-3 px-5 py-3 text-sm font-semibold text-gray-700 hover:text-red-700 hover:bg-red-50 transition-colors"
                >
                  <LogOut className="w-4 h-4" />
                  Đăng xuất
                </button>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      </aside>
    </>
  );
}



