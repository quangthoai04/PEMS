// Đây là component hiển thị phần nội dung nổi bật ở trang chủ (Hero section)
import React, { useState } from 'react';
import { motion } from 'motion/react';
import { Link } from 'react-router-dom';
import { CalendarDays } from 'lucide-react';
import bannerImg from '../assets/images/banner02.png';
import { VisitingFormPopup } from './VisitingFormPopup';

export function HeroSection() {
  const [isVisitorFormOpen, setIsVisitorFormOpen] = useState(false);

  return (
    <>
      <section className="relative pt-16 pb-12 lg:pt-24 lg:pb-16 min-h-[350px] lg:min-h-[500px] flex items-center overflow-hidden border-b border-gray-200 bg-gray-50">
        {/* Full-width Background Banner */}
        <div className="absolute inset-0 z-0">
          <img 
            src={bannerImg} 
            alt="FPT University Banner" 
            className="w-full h-full object-cover lg:object-contain lg:object-right-bottom lg:scale-110 lg:origin-right"
          />
          {/* Soft white overlay to ensure text remains readable with a stronger white tint */}
          <div className="absolute inset-0 bg-gradient-to-r from-gray-50/95 via-gray-50/80 to-transparent sm:to-transparent"></div>
          <div className="absolute inset-0 bg-gradient-to-t from-gray-50/50 via-transparent to-transparent"></div>
        </div>

        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 relative z-10 w-full">
          <div className="max-w-4xl">
            
            <motion.div 
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.6, delay: 0.2 }}
              className="max-w-3xl"
            >
              <span className="inline-block py-1 px-3 rounded-full bg-fpt-orange shadow-sm text-white font-semibold text-xs mb-4">
                Phòng Hợp tác Quốc tế
              </span>
              <h1 className="text-3xl md:text-4xl lg:text-5xl xl:text-6xl font-bold text-fpt-navy tracking-tight leading-tight mb-4">
                Kết nối toàn cầu cùng <br className="hidden sm:block" /><span className="text-fpt-orange">Đại học FPT</span>
              </h1>
              <p className="text-base md:text-lg text-gray-700 font-medium leading-relaxed mb-10 max-w-2xl">
                 Mở rộng mạng lưới đối tác, thúc đẩy các chương trình trao đổi sinh viên và trải nghiệm chuẩn quốc tế.
              </p>
            </motion.div>

            {/* Solid Large Buttons directly on banner */}
            <motion.div 
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.6, delay: 0.3 }}
              className="grid sm:grid-cols-2 gap-4 lg:gap-6 max-w-2xl"
            >
              {/* Blue Button */}
              <button 
                onClick={() => setIsVisitorFormOpen(true)}
                className="bg-[#004C91] text-white rounded-2xl py-6 px-5 flex flex-col items-center justify-center text-center hover:bg-[#003a70] hover:-translate-y-1 transition-all duration-300 border-b-4 border-[#003360] shadow-2xl shadow-fpt-navy/20 group"
              >
                <CalendarDays className="w-8 h-8 mb-3 group-hover:scale-110 transition-transform" />
                <h3 className="text-base sm:text-lg font-bold mb-1 uppercase tracking-wide whitespace-nowrap">Đăng ký tham quan</h3>
                <p className="text-blue-100 text-xs font-medium px-2">Dành cho đối tác đăng ký làm việc trực tiếp</p>
              </button>

              {/* Orange Button */}
              <Link to="/visit-fptu" className="bg-[#F37021] text-white rounded-2xl py-6 px-5 flex flex-col items-center justify-center text-center hover:bg-[#d9621a] hover:-translate-y-1 transition-all duration-300 border-b-4 border-[#c25515] shadow-2xl shadow-fpt-orange/20 group">
                <div className="mb-3 group-hover:scale-110 transition-transform">
                  {/* VR Headset Custom SVG Icon */}
                  <svg width="32" height="32" viewBox="0 0 24 24" fill="currentColor" xmlns="http://www.w3.org/2000/svg">
                    <path fillRule="evenodd" clipRule="evenodd" d="M4 15V7a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2h-2.5l-1.5-1.5a1 1 0 0 0-1.4 0L10.5 17H6a2 2 0 0 1-2-2zm4.5-3a2 2 0 1 0 0-4 2 2 0 0 0 0 4zm7 0a2 2 0 1 0 0-4 2 2 0 0 0 0 4z" />
                  </svg>
                </div>
                <h3 className="text-base sm:text-lg font-bold mb-1 uppercase tracking-wide whitespace-nowrap">Visit FPTU Online</h3>
                <p className="text-orange-100 text-xs font-medium px-2">Trải nghiệm không gian 360 độ trực tuyến</p>
              </Link>
            </motion.div>
          
          </div>
        </div>
      </section>
      
      <VisitingFormPopup isOpen={isVisitorFormOpen} onClose={() => setIsVisitorFormOpen(false)} />
    </>
  );
}
