// Đây là component cửa sổ bật lên để tìm kiếm thông tin
import React, { useEffect, useRef } from 'react';
import { Search, X } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';

interface SearchPopupProps {
  isOpen: boolean;
  onClose: () => void;
}

export function SearchPopup({ isOpen, onClose }: SearchPopupProps) {
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (isOpen) {
      setTimeout(() => {
        inputRef.current?.focus();
      }, 100);
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = 'unset';
    }
    return () => {
      document.body.style.overflow = 'unset';
    };
  }, [isOpen]);

  const campuses = [
    {
      city: 'HÀ NỘI',
      address: 'Khu Giáo dục và Đào tạo - Khu Công nghệ cao Hòa Lạc - Km29 Đại lộ Thăng Long, Xã Thạch Hòa, Huyện Thạch Thất, TP. Hà Nội',
      hotline: '(024) 7300 5588',
      email: 'tuyensinhhanoi@fpt.edu.vn'
    },
    {
      city: 'TP. HỒ CHÍ MINH',
      address: 'Lô E2a-7, Đường D1, Khu Công nghệ cao, Phường Tăng Nhơn Phú B, TP. Hồ Chí Minh',
      hotline: '(028) 7300 5588',
      email: 'tuyensinhhcm@fpt.edu.vn'
    },
    {
      city: 'ĐÀ NẴNG',
      address: 'Khu đô thị công nghệ FPT Đà Nẵng, Phường Hòa Hải, Quận Ngũ Hành Sơn, TP. Đà Nẵng',
      hotline: '(0236) 730 0999',
      email: 'tuyensinhdanang@fpt.edu.vn'
    },
    {
      city: 'CẦN THƠ',
      address: 'Số 600, đường Nguyễn Văn Cừ (nối dài), Phường An Bình, Quận Ninh Kiều, Thành phố Cần Thơ',
      hotline: '(0292) 730 3636',
      email: 'tuyensinhcantho@fpt.edu.vn'
    },
    {
      city: 'QUY NHƠN',
      address: 'Khu đô thị mới An Phú Thịnh, Phường Nhơn Bình, TP. Quy Nhơn, Tỉnh Bình Định',
      hotline: '(0256) 7300 999',
      email: 'tuyensinhquynhon@fpt.edu.vn'
    }
  ];

  const suggestions = [
    'Học kỳ nước ngoài',
    'Trao đổi sinh viên',
    'Chương trình tiếng Anh',
    'Thông tin tuyển sinh',
    'Học bổng',
    'Đối tác quốc tế',
    'Short course',
    'Chuyển tiếp'
  ];

  return (
    <AnimatePresence>
      {isOpen && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          className="fixed inset-0 z-[100] bg-black/50 backdrop-blur-sm flex items-center justify-center p-4 sm:p-6"
          onClick={onClose}
        >
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            transition={{ duration: 0.2 }}
            onClick={(e) => e.stopPropagation()}
            className="bg-[#f4f4f4] w-full max-w-[1200px] max-h-[90vh] rounded-2xl shadow-2xl flex flex-col overflow-hidden relative"
          >
            {/* Header Close Button */}
            <div className="absolute top-4 right-4 sm:top-6 sm:right-6 z-10">
              <button 
                onClick={onClose}
                className="p-2 bg-white rounded-full text-gray-500 hover:text-gray-900 shadow-sm border border-gray-200 transition-all hover:bg-gray-50 flex items-center justify-center"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="flex-1 overflow-y-auto flex flex-col">
              {/* Search Area */}
              <div className="flex-none flex flex-col items-center justify-center pt-16 sm:pt-20 pb-10 px-4 sm:px-6">
                <h2 className="text-2xl sm:text-3xl md:text-4xl font-bold text-center text-gray-800 mb-8">
                  Bạn đang tìm kiếm gì?
                </h2>
            
            <div className="w-full max-w-3xl relative">
              <input
                ref={inputRef}
                type="text"
                placeholder="Nhập từ khóa tìm kiếm..."
                className="w-full h-16 pl-6 pr-16 text-lg sm:text-xl rounded-full border-2 border-gray-200 shadow-sm focus:border-fpt-orange focus:ring-0 outline-none transition-colors"
                onKeyDown={(e) => {
                  if (e.key === 'Escape') onClose();
                }}
              />
              <button className="absolute right-3 top-1/2 -translate-y-1/2 w-10 h-10 bg-fpt-orange text-white rounded-full flex items-center justify-center hover:bg-orange-600 transition-colors">
                <Search className="w-5 h-5" />
              </button>
            </div>

            <div className="w-full max-w-3xl mt-8">
              <h3 className="text-sm uppercase tracking-wider text-gray-500 font-semibold mb-4 text-center">Gợi ý tìm kiếm phổ biến</h3>
              <div className="flex flex-wrap justify-center gap-3">
                {suggestions.map((item, index) => (
                  <button 
                    key={index}
                    className="px-4 py-2 bg-white border border-gray-200 rounded-full text-sm text-gray-600 hover:text-fpt-orange hover:border-fpt-orange transition-colors"
                  >
                    {item}
                  </button>
                ))}
              </div>
            </div>
          </div>

          {/* Footer Contacts Area */}
          <div className="mt-auto bg-[#fafafa] border-t border-gray-200 pt-16 pb-8 px-4 sm:px-8">
            <div className="max-w-[1400px] mx-auto grid grid-cols-1 md:grid-cols-3 lg:grid-cols-5 gap-8">
              {campuses.map((campus, index) => (
                <div key={index} className="flex flex-col">
                  <h4 className="text-[16px] font-bold text-gray-900 flex items-center mb-4 uppercase tracking-wide">
                    <span className="w-[3px] h-4 bg-fpt-orange mr-2 inline-block"></span>
                    {campus.city}
                  </h4>
                  <div className="space-y-4 text-[14px] text-gray-600 leading-relaxed font-medium">
                    <div>
                      <p className="text-gray-500 font-bold mb-1">Địa chỉ</p>
                      <p className="text-gray-800">{campus.address}</p>
                    </div>
                    <div>
                      <p className="text-gray-500 font-bold inline-block mr-1">Hotline</p>
                      <p className="inline-block text-gray-800">{campus.hotline}</p>
                    </div>
                    <div>
                      <p className="text-gray-500 font-bold inline-block mr-1">Email</p>
                      <p className="inline-block text-gray-800">{campus.email}</p>
                    </div>
                  </div>
                </div>
              ))}
            </div>

            <div className="max-w-[1400px] mx-auto mt-16 pt-6 border-t border-gray-200 flex flex-col md:flex-row items-center justify-between gap-4">
              <p className="text-[14px] font-bold text-gray-800">
                © 2026 Bản quyền thuộc về Trường Đại học FPT.
              </p>
              <div className="flex items-center gap-5">
                {/* Facebook */}
                <a href="#" className="text-fpt-orange hover:text-orange-600 transition-colors">
                  <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                    <path fillRule="evenodd" d="M22 12c0-5.523-4.477-10-10-10S2 6.477 2 12c0 4.991 3.657 9.128 8.438 9.878v-6.987h-2.54V12h2.54V9.797c0-2.506 1.492-3.89 3.777-3.89 1.094 0 2.238.195 2.238.195v2.46h-1.26c-1.243 0-1.63.771-1.63 1.562V12h2.773l-.443 2.89h-2.33v6.988C18.343 21.128 22 16.991 22 12z" clipRule="evenodd" />
                  </svg>
                </a>
                {/* Zalo */}
                <a href="#" className="hidden md:flex text-fpt-orange hover:text-orange-600 transition-colors font-bold text-[18px] leading-none tracking-tighter">
                  Zalo
                </a>
                {/* Youtube */}
                <a href="#" className="text-fpt-orange hover:text-orange-600 transition-colors">
                  <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 24 24">
                    <path d="M23.498 6.186a3.016 3.016 0 0 0-2.122-2.136C19.505 3.545 12 3.545 12 3.545s-7.505 0-9.377.505A3.017 3.017 0 0 0 .502 6.186C0 8.07 0 12 0 12s0 3.93.502 5.814a3.016 3.016 0 0 0 2.122 2.136c1.871.505 9.376.505 9.376.505s7.505 0 9.377-.505a3.015 3.015 0 0 0 2.122-2.136C24 15.93 24 12 24 12s0-3.93-.502-5.814zM9.545 15.568V8.432L15.818 12l-6.273 3.568z"/>
                  </svg>
                </a>
                {/* Tiktok */}
                <a href="#" className="text-fpt-orange hover:text-orange-600 transition-colors">
                  <svg className="w-5 h-5" viewBox="0 0 24 24" fill="currentColor">
                    <path d="M12.525.02c1.31-.02 2.61-.01 3.91-.01.08 1.53.63 3.09 1.75 4.17 1.12 1.11 2.7 1.62 4.24 1.79v4.03c-1.44-.05-2.89-.35-4.2-.97-.57-.26-1.1-.59-1.62-.93-.01 2.92.01 5.84-.02 8.75-.08 2.78-1.15 5.54-3.33 7.39-2.2 1.88-5.31 2.52-8.14 1.96-2.73-.54-5.14-2.58-6.15-5.18-1.03-2.68-.58-5.83 1.3-8.08 1.9-2.25 4.93-3.25 7.74-2.73v4.06c-1.37-.36-2.88-.16-4 .67-1.14.85-1.7 2.37-1.46 3.77.26 1.48 1.5 2.65 2.98 2.87 1.48.22 3.05-.38 3.96-1.48.91-1.1 1.25-2.6 1.22-4.08-.03-4.82-.01-9.65-.01-14.48h1.83z" />
                  </svg>
                </a>
              </div>
            </div>
          </div>
        </div>
        </motion.div>
      </motion.div>
      )}
    </AnimatePresence>
  );
}
