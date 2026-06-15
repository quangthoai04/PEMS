/**
 * Component Header
 * Thanh điều hướng chính (Top navbar) của ứng dụng.
 * Chứa logo, liên kết điều hướng và menu người dùng.
 */

// Đây là component phần điều hướng / thanh công cụ ở đầu trang (Header)
import React, { useState, useEffect } from 'react';
import { Search, Globe, LogIn, Menu, LayoutDashboard, User, LogOut, ChevronDown, X } from 'lucide-react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import fptLogo from '../../assets/images/regenerated_image_1778552336496.png';
import avatarImg from '../../assets/Avatar/AvatarDefault.png';
import { SearchPopup } from '../modals/SearchPopup';
import { LoginModal } from '../modals/LoginModal';
import { motion, AnimatePresence } from 'motion/react';

export function Header() {
  const [lang, setLang] = React.useState('VI');
  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [isLoginOpen, setIsLoginOpen] = useState(false);
  const [isProfileMenuOpen, setIsProfileMenuOpen] = useState(false);
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const location = useLocation();
  const navigate = useNavigate();

  // Close mobile menu on navigate
  useEffect(() => {
    setIsMobileMenuOpen(false);
  }, [location.pathname]);

  // Re-read user state anytime the component renders (since we navigate back, it might remount/render)
  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;

  const handleLogout = () => {
    localStorage.removeItem('currentUser');
    navigate('/');
    setIsProfileMenuOpen(false);
    setIsMobileMenuOpen(false);
  };

  const getLinkClass = (path: string) => {
    const isActive = path === '/' ? location.pathname === '/' : location.pathname.startsWith(path);
    const baseClass = "px-3 py-2 transition-colors relative block";
    if (isActive) {
      return `${baseClass} text-[#f37021] font-bold after:content-[''] after:absolute after:-bottom-1 after:left-3 after:right-3 after:h-[2px] after:bg-[#f37021]`;
    }
    return `${baseClass} text-gray-700 hover:text-[#f37021]`;
  };

  const getMobileLinkClass = (path: string) => {
    const isActive = path === '/' ? location.pathname === '/' : location.pathname.startsWith(path);
    const baseClass = "w-full px-4 py-3 rounded-xl font-bold transition-all flex items-center";
    if (isActive) {
      return `${baseClass} bg-orange-50 text-[#f37021] border-l-4 border-[#f37021]`;
    }
    return `${baseClass} text-gray-700 hover:bg-slate-50 hover:text-[#f37021]`;
  };

  return (
    <>
      <header className="fixed top-0 left-0 right-0 z-50 bg-white/95 backdrop-blur shadow-sm border-b border-gray-100">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-20">
            {/* Logo */}
            <div className="flex-shrink-0 flex items-center cursor-pointer gap-2">
              <Link to="/">
                <img src={fptLogo} alt="FPT Logo" className="w-[140px] h-14 sm:w-[200px] sm:h-[90px] object-contain" />
              </Link>
              <span className="text-fpt-navy ml-2 text-sm border-l border-gray-300 pl-2 hidden sm:inline-block font-medium">Phòng HTQT</span>
            </div>

            {/* Desktop Nav */}
            <nav className="hidden lg:flex items-center space-x-1 font-medium text-[15px] whitespace-nowrap flex-shrink-0">
              <Link to="/" className={getLinkClass('/')}>Trang chủ</Link>
              <Link to="/news" className={getLinkClass('/news')}>Tin tức</Link>
              <Link to="/partners" className={getLinkClass('/partners')}>Đối tác</Link>
              <a href="https://outbound.fpt.edu.vn/" target="_blank" rel="noopener noreferrer" className="px-3 py-2 text-gray-700 hover:text-[#f37021] transition-colors relative block">Outbound</a>
              <a href="https://international.fpt.edu.vn/" target="_blank" rel="noopener noreferrer" className="px-3 py-2 text-gray-700 hover:text-[#f37021] transition-colors relative block">Inbound</a>
              <Link to="/visit-fptu" className={getLinkClass('/visit-fptu')}>Visit FPTU</Link>
              <Link to="/faq" className={getLinkClass('/faq')}>FAQ</Link>
            </nav>

            {/* Actions */}
            <div className="hidden lg:flex items-center space-x-2 xl:space-x-4 flex-shrink-0">
              <button 
                onClick={() => setIsSearchOpen(true)}
                className="p-2 text-gray-600 hover:text-fpt-navy hover:bg-gray-50 rounded-full transition-colors"
                aria-label="Open search"
              >
                <Search className="w-5 h-5" />
              </button>
              
              <div className="relative group/lang">
                <button className="flex items-center gap-1 p-2 text-gray-600 hover:text-fpt-navy hover:bg-gray-50 rounded-lg transition-colors font-medium">
                  <Globe className="w-5 h-5" />
                  <span>{lang}</span>
                </button>
                {/* Dropdown Languages */}
                <div className="absolute right-0 mt-1 w-32 bg-white border border-gray-100 rounded-lg shadow-lg opacity-0 invisible group-hover/lang:opacity-100 group-hover/lang:visible transition-all duration-200">
                  <div className="p-1">
                    {['VI', 'EN', 'CN', 'JP', 'KR'].map(l => (
                      <button key={l} onClick={() => setLang(l)} className={`block w-full text-left px-3 py-2 text-sm rounded ${lang === l ? 'bg-orange-50 text-fpt-orange font-medium' : 'text-gray-700 hover:bg-gray-50'}`}>
                        {l === 'VI' ? 'Tiếng Việt' : l === 'EN' ? 'English' : l === 'CN' ? '中文' : l === 'JP' ? '日本語' : '한국어'}
                      </button>
                    ))}
                  </div>
                </div>
              </div>

              {user ? (
                <div className="relative">
                  <button 
                    onClick={() => setIsProfileMenuOpen(!isProfileMenuOpen)}
                    className="flex items-center justify-between gap-1.5 px-2 py-1.5 rounded-full hover:bg-gray-50 transition-colors border border-transparent hover:border-gray-200 max-w-[100px] lg:max-w-[130px] xl:max-w-[180px]"
                  >
                    <img src={avatarImg} alt="Avatar" className="w-8 h-8 flex-shrink-0 rounded-full border border-gray-200 object-cover" />
                    <span className="font-bold text-[#004c91] text-sm truncate flex-1 block overflow-hidden">{user.name}</span>
                    <ChevronDown className="w-4 h-4 flex-shrink-0 text-gray-500" />
                  </button>

                  {isProfileMenuOpen && (
                    <div className="fixed inset-0 z-10" onClick={() => setIsProfileMenuOpen(false)} />
                  )}
                  <AnimatePresence>
                    {isProfileMenuOpen && (
                      <motion.div 
                        initial={{ opacity: 0, y: 10, scale: 0.95 }}
                        animate={{ opacity: 1, y: 0, scale: 1 }}
                        exit={{ opacity: 0, y: 10, scale: 0.95 }}
                        transition={{ duration: 0.15 }}
                        className="absolute right-0 mt-2 w-56 bg-white rounded-2xl shadow-xl border border-[#d2e5f5] overflow-hidden z-[60] py-2"
                      >
                        <button 
                          onClick={() => { navigate('/dashboard'); setIsProfileMenuOpen(false); }}
                          className="w-full flex items-center gap-3 px-5 py-3 text-sm font-semibold text-gray-700 hover:text-[#004c91] hover:bg-[#d2e5f5] transition-colors"
                        >
                          <LayoutDashboard className="w-4 h-4" />
                          Trang quản trị
                        </button>
                        <button 
                          onClick={() => { navigate('/dashboard/profile'); setIsProfileMenuOpen(false); }}
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
              ) : (
                <button 
                  onClick={() => setIsLoginOpen(true)}
                  className="flex items-center gap-2 px-5 py-2.5 bg-fpt-navy text-white rounded-lg hover:bg-fpt-navy-hover transition-colors font-medium text-sm shadow-sm"
                >
                  <LogIn className="w-4 h-4" />
                  Đăng nhập
                </button>
              )}
            </div>

            {/* Mobile menu button */}
            <div className="lg:hidden flex items-center gap-2 text-gray-600">
              <button 
                onClick={() => setIsSearchOpen(true)}
                className="p-2 text-gray-600 hover:text-[#004c91] hover:bg-slate-50 rounded-full transition-colors"
                aria-label="Tìm kiếm"
              >
                <Search className="w-5 h-5" />
              </button>
              <button 
                onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)}
                className="p-2 text-gray-600 hover:text-[#004c91] hover:bg-slate-50 rounded-full transition-colors"
                aria-label="Mở menu"
              >
                {isMobileMenuOpen ? <X className="w-6 h-6 text-[#f37021]" /> : <Menu className="w-6 h-6" />}
              </button>
            </div>
          </div>
        </div>

      </header>

      {/* Mobile Navigation Drawer Overlay */}
      <AnimatePresence>
        {isMobileMenuOpen && (
          <motion.div
            key="mobile-overlay"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 top-20 bg-black/40 backdrop-blur-xs z-[90] lg:hidden"
            onClick={() => setIsMobileMenuOpen(false)}
          />
        )}
      </AnimatePresence>

      {/* Mobile Navigation Drawer Container */}
      <AnimatePresence>
        {isMobileMenuOpen && (
          <motion.div
            key="mobile-drawer"
            initial={{ x: "100%" }}
            animate={{ x: 0 }}
            exit={{ x: "100%" }}
            transition={{ type: "spring", bounce: 0, duration: 0.3 }}
            className="fixed top-20 right-0 bottom-0 w-full sm:w-80 bg-white border-l border-slate-100 z-[100] flex flex-col justify-between overflow-y-auto lg:hidden shadow-2xl"
          >
            <div className="p-5 space-y-6">
              {/* Language Selector in Drawer */}
              <div>
                <h4 className="text-xs uppercase font-extrabold text-slate-400 tracking-wider mb-2.5 px-1">Ngôn ngữ / Language</h4>
                <div className="grid grid-cols-5 gap-1.5 bg-slate-50 p-1 rounded-xl border border-slate-100">
                  {['VI', 'EN', 'CN', 'JP', 'KR'].map(l => (
                    <button
                      key={l}
                      onClick={() => setLang(l)}
                      className={`py-2 text-xs font-bold rounded-lg transition-all ${
                        lang === l 
                          ? 'bg-[#004c91] text-white shadow-sm' 
                          : 'text-slate-600 hover:bg-slate-100/60'
                      }`}
                    >
                      {l}
                    </button>
                  ))}
                </div>
              </div>

              {/* Nav Links */}
              <div className="space-y-1">
                <h4 className="text-xs uppercase font-extrabold text-slate-400 tracking-wider mb-2.5 px-1">Danh mục</h4>
                <Link to="/" className={getMobileLinkClass('/')} onClick={() => setIsMobileMenuOpen(false)}>Trang chủ</Link>
                <Link to="/news" className={getMobileLinkClass('/news')} onClick={() => setIsMobileMenuOpen(false)}>Tin tức</Link>
                <Link to="/partners" className={getMobileLinkClass('/partners')} onClick={() => setIsMobileMenuOpen(false)}>Đối tác</Link>
                <a href="https://outbound.fpt.edu.vn/" target="_blank" rel="noopener noreferrer" className="w-full px-4 py-3 rounded-xl font-bold transition-all flex items-center text-gray-700 hover:bg-slate-50 hover:text-[#f37021]">Outbound</a>
                <a href="https://international.fpt.edu.vn/" target="_blank" rel="noopener noreferrer" className="w-full px-4 py-3 rounded-xl font-bold transition-all flex items-center text-gray-700 hover:bg-slate-50 hover:text-[#f37021]">Inbound</a>
                <Link to="/visit-fptu" className={getMobileLinkClass('/visit-fptu')} onClick={() => setIsMobileMenuOpen(false)}>Visit FPTU</Link>
                <Link to="/faq" className={getMobileLinkClass('/faq')} onClick={() => setIsMobileMenuOpen(false)}>FAQ</Link>
              </div>
            </div>

            {/* Footer/Account Actions in Drawer */}
            <div className="p-5 border-t border-slate-100 bg-slate-50/50">
              {user ? (
                <div className="space-y-3">
                  <div className="flex items-center gap-3 px-3 py-2 bg-white rounded-xl border border-slate-100 shadow-xs">
                    <img src={avatarImg} alt="Avatar" className="w-10 h-10 rounded-full border border-slate-250 object-cover" />
                    <div className="min-w-0 flex-1">
                      <p className="text-sm font-bold text-[#004c91] truncate">{user.name}</p>
                      <p className="text-xs text-slate-405 truncate">{user.role || 'Đối tác / Khách'}</p>
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-2">
                    <button 
                      onClick={() => { navigate('/dashboard'); setIsMobileMenuOpen(false); }}
                      className="px-3 py-2.5 bg-white text-xs font-bold text-slate-700 border border-slate-205 rounded-xl hover:text-[#004c91] hover:bg-[#d2e5f5] transition-all flex items-center justify-center gap-1.5"
                    >
                      <LayoutDashboard className="w-3.5 h-3.5 text-[#004c91]" />
                      Quản lý
                    </button>
                    <button 
                      onClick={() => { navigate('/dashboard/profile'); setIsMobileMenuOpen(false); }}
                      className="px-3 py-2.5 bg-white text-xs font-bold text-slate-700 border border-slate-205 rounded-xl hover:text-[#004c91] hover:bg-[#d2e5f5] transition-all flex items-center justify-center gap-1.5"
                    >
                      <User className="w-3.5 h-3.5 text-[#004c91]" />
                      Hồ sơ
                    </button>
                  </div>

                  <button 
                    onClick={handleLogout}
                    className="w-full py-3 bg-red-50 hover:bg-red-100 text-xs font-extrabold text-red-650 rounded-xl transition-colors flex items-center justify-center gap-2 border border-red-200/40"
                  >
                    <LogOut className="w-3.5 h-3.5" />
                    Đăng xuất tài khoản
                  </button>
                </div>
              ) : (
                <button 
                  onClick={() => { setIsLoginOpen(true); setIsMobileMenuOpen(false); }}
                  className="w-full py-3.5 bg-gradient-to-r from-[#004c91] to-[#0461b5] text-white font-black text-sm rounded-xl hover:opacity-95 shadow-md flex items-center justify-center gap-2"
                >
                  <LogIn className="w-4 h-4" />
                  Đăng nhập hệ thống
                </button>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      <SearchPopup isOpen={isSearchOpen} onClose={() => setIsSearchOpen(false)} />
      <LoginModal isOpen={isLoginOpen} onClose={() => setIsLoginOpen(false)} />
    </>
  );
}
