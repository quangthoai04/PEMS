// Đây là component phần chân trang của website (Footer)
import React from 'react';
import { ArrowUp, MapPin, Phone, Mail, Facebook, Youtube, Globe as GlobeIcon } from 'lucide-react';
import footerLogo from '../assets/images/2021-FPTU-Eng.png';

export function Footer() {
  const scrollToTop = () => {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  return (
    <footer className="bg-fpt-footer text-gray-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-12 gap-8">
          
          {/* Column 1: Info (Spans 5 cols) */}
          <div className="lg:col-span-5 space-y-4">
            <img src={footerLogo} alt="FPT Education" className="h-12 object-contain mb-4 bg-white p-2 rounded-lg" />
            
            <h3 className="text-white font-bold tracking-wide uppercase text-base mb-3">Phòng Hợp Tác Quốc Tế</h3>
            
            <ul className="space-y-2">
              <li className="flex items-start gap-3">
                <MapPin className="w-4 h-4 text-gray-400 shrink-0 mt-0.5" />
                <span className="text-xs">Khu Công nghệ cao Hòa Lạc, Hà Nội</span>
              </li>
              <li className="flex items-center gap-3">
                <Phone className="w-4 h-4 text-gray-400 shrink-0" />
                <span className="text-xs">024 6680 5912</span>
              </li>
              <li className="flex items-center gap-3">
                <Mail className="w-4 h-4 text-gray-400 shrink-0" />
                <a href="mailto:international.fptu@fpt.edu.vn" className="text-xs hover:text-fpt-orange transition-colors">international.fptu@fpt.edu.vn</a>
              </li>
            </ul>
          </div>

          {/* Column 2: System (Spans 4 cols) */}
          <div className="lg:col-span-4">
            <h3 className="text-fpt-orange font-bold tracking-wide uppercase text-base mb-4">Hệ Thống Đào Tạo</h3>
            <ul className="space-y-2 text-xs text-gray-300">
              <li className="flex items-center gap-2">
                 <MapPin className="w-3.5 h-3.5 text-gray-500" />
                 Hà Nội: Khu CNC Hòa Lạc
              </li>
              <li className="flex items-center gap-2">
                 <MapPin className="w-3.5 h-3.5 text-gray-500" />
                 Đà Nẵng: KĐT FPT City
              </li>
              <li className="flex items-center gap-2">
                 <MapPin className="w-3.5 h-3.5 text-gray-500" />
                 TP. HCM: Khu CNC, Thủ Đức
              </li>
              <li className="flex items-center gap-2">
                 <MapPin className="w-3.5 h-3.5 text-gray-500" />
                 Cần Thơ: Số 600, đường Nguyễn Văn Cừ
              </li>
              <li className="flex items-center gap-2">
                 <MapPin className="w-3.5 h-3.5 text-gray-500" />
                 Quy Nhơn: Khu Đô Thị An Phú Thịnh
              </li>
            </ul>
          </div>

          {/* Column 3: Connect (Spans 3 cols) */}
          <div className="lg:col-span-3">
            <h3 className="text-fpt-orange font-bold tracking-wide uppercase text-base mb-4">Kết Nối Với Chúng Tôi</h3>
            <div className="flex gap-3 mb-4">
              <a href="#" className="w-8 h-8 rounded-full border border-gray-500 flex items-center justify-center hover:bg-fpt-orange hover:border-fpt-orange transition-colors">
                <Facebook className="w-3.5 h-3.5" />
              </a>
              <a href="#" className="w-8 h-8 rounded-full border border-gray-500 flex items-center justify-center hover:bg-fpt-orange hover:border-fpt-orange transition-colors">
                <Youtube className="w-3.5 h-3.5" />
              </a>
              <a href="#" className="w-8 h-8 rounded-full border border-gray-500 flex items-center justify-center hover:bg-fpt-orange hover:border-fpt-orange transition-colors">
                <GlobeIcon className="w-3.5 h-3.5" />
              </a>
            </div>
            
            <ul className="space-y-2 text-xs text-gray-300">
              <li><a href="#" className="hover:text-white transition-colors">Chính sách bảo mật</a></li>
              <li><a href="#" className="hover:text-white transition-colors">Điều khoản sử dụng</a></li>
              <li><a href="#" className="hover:text-white transition-colors">FAQs</a></li>
            </ul>
          </div>

        </div>

        {/* Bottom bar */}
        <div className="mt-8 pt-4 border-t border-white/10 flex flex-col items-center justify-center gap-4 relative">
          <div className="text-center text-xs text-gray-400 space-y-1 mt-2">
            <p>© 2024 Hệ thống quản lý tiếp khách và hợp tác quốc tế - ĐẠI HỌC FPT</p>
          </div>
          
          <button 
            onClick={scrollToTop}
            className="md:absolute right-0 -top-10 bg-white text-fpt-footer w-12 h-12 rounded-full flex justify-center items-center hover:bg-gray-100 shadow-xl hover:-translate-y-1 transition-all duration-300 group"
            aria-label="Trở lại đầu trang"
          >
            <ArrowUp className="w-6 h-6 group-hover:text-fpt-orange transition-colors" />
          </button>
        </div>
      </div>
    </footer>
  );
}
