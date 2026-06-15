/**
 * Trang PartnerDetailPage (Public)
 * Trang công khai thể hiện chiến lược thông tin chi tiết của một đối tác bên ngoài.
 */

// file: /src/pages/PartnerDetailPage.tsx
// Trang chi tiết đối tác liên kết duy nhất thiết kế chung cho toàn bộ hệ thống đối tác của Đại Học FPT.
// Do hệ thống đang trong giai đoạn mẫu thử nghiệm với dữ liệu giả lập (mock data), tất cả các lượt bấm 
// từ danh sách đối tác sẽ được điều hướng đồng quy về một trang thông tin chi tiết cố định này.

import React from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'motion/react';
import { 
  ArrowLeft, 
  MapPin, 
  Building2, 
  ExternalLink, 
  BookOpen, 
  Home
} from 'lucide-react';

// Dynamically import partner logos from assets/Logo using Vite's glob function
const logoModules = import.meta.glob("../assets/Logo/*", {
  eager: true,
  import: "default",
});
const logoList = Object.values(logoModules) as string[];

export function PartnerDetailPage() {
  const navigate = useNavigate();

  // Unified Single Specimen Partner (Swinburne University) for direct shared usage
  const activePartner = {
    id: 8,
    name: "Đại học Công nghệ Swinburne (Swinburne University of Technology)",
    code: "SWINBURNE",
    country: "Melbourne, Úc",
    type: "University",
    category: "Đối tác Chiến lược",
    web: "https://www.swinburne.edu.au",
    description: "Đại học Công nghệ Swinburne là trường đại học công lập đẳng cấp quốc tế có trụ sở chính tại Melbourne, tiểu bang Victoria, Úc. Nằm trong top 1% các trường đại học hàng đầu toàn diện toàn cầu, Swinburne tiên phong tuyệt đối trong công tác số hóa, giảng dạy và kiến tạo giá trị công nghệ cao đột phá liên quan đến Trí tuệ nhân đạo (AI), Kỹ nghệ phần mềm và Chuyển đổi số. Hợp tác chiến lược đặc sắc giữa Swinburne và Trường Đại học FPT được bồi đắp many năm, mở ra cơ hội liên kết trao đổi học kỳ quốc tế, nghiên cứu học thuật sâu sắc và nâng bước chuyển tiếp cử nhân phát triển sự nghiệp toàn cầu.",
    email: "international@swinburne.edu.au",
    phone: "+61 3 9214 8000",
    address: "John St, Hawthorn, Victoria 3122, Melbourne, Australia",
    coverImage: "https://images.unsplash.com/photo-1541339907198-e08756dedf3f?q=80&w=1200&auto=format&fit=crop"
  };

  // Select index 7 as Swinburne logo from list
  const logoSrc = logoList && logoList.length > 0 
    ? logoList[7 % logoList.length] 
    : "";

  return (
    <div className="pt-24 pb-24 bg-[#f8fafc] min-h-screen">
      <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
        
        {/* Simple Static Breadcrumbs with no motion animations based on user preference */}
        <div className="mb-6 flex flex-wrap items-center gap-2 text-sm font-semibold select-none text-slate-400 py-1">
          <button 
            onClick={() => navigate('/')} 
            className="hover:text-[#004c91] transition-colors flex items-center gap-1.5 cursor-pointer px-2 py-1 rounded-lg"
          >
            <Home className="w-4 h-4 text-slate-400" />
            <span>Trang chủ</span>
          </button>
          
          <span className="text-slate-300">/</span>
          
          <button 
            onClick={() => navigate('/partners')} 
            className="text-[#f37021] hover:text-orange-600 font-bold transition-colors px-2 py-1 rounded-lg cursor-pointer"
          >
            đối tác liên kết
          </button>
          
          <span className="text-slate-300">/</span>
          
          <span className="text-slate-600 font-bold truncate max-w-[200px] sm:max-w-none">
            {activePartner.name}
          </span>
        </div>

        {/* Back Button */}
        <button
          onClick={() => navigate('/partners')}
          className="mb-8 inline-flex items-center gap-2 px-4 py-2 bg-white hover:bg-slate-55 border border-slate-200 rounded-xl text-slate-500 font-bold text-sm shadow-sm transition-all cursor-pointer"
        >
          <ArrowLeft className="w-4 h-4 text-[#f37021]" />
          Quay lại danh sách đối tác
        </button>

        {/* Main Content Body Stack */}
        <div className="flex flex-col gap-6">

          {/* 1. Hero Cover Photo - round corners nicely & significantly taller layout */}
          <motion.div 
            initial={{ opacity: 0, y: 25 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.5, ease: "easeOut" }}
            className="relative w-full h-[380px] sm:h-[480px] md:h-[550px] lg:h-[620px] rounded-2xl overflow-hidden shadow-md"
          >
            {/* Cover photo behind */}
            <img 
              src={activePartner.coverImage} 
              alt={activePartner.name}
              className="w-full h-full object-cover select-none"
              referrerPolicy="no-referrer"
            />
            {/* Perfect dark linear gradient cover for readable text */}
            <div className="absolute inset-0 bg-gradient-to-t from-slate-950/95 via-slate-900/40 to-transparent z-10" />
            <div className="absolute inset-x-0 bottom-0 h-1/2 bg-gradient-to-t from-slate-950/80 to-transparent z-15" />

            {/* In-Cover Bottom-Left Corner Branding elements */}
            <div className="absolute bottom-6 left-6 right-6 z-20 flex flex-col sm:flex-row items-center sm:items-end gap-4 sm:gap-6 text-center sm:text-left">
              
              {/* Partner Logo - rounded slightly */}
              <div className="relative shrink-0 w-20 h-20 sm:w-24 sm:h-24 bg-white p-2.5 rounded-xl sm:rounded-2xl shadow-lg flex items-center justify-center border border-white/20 select-none">
                {logoSrc ? (
                  <img 
                    src={logoSrc} 
                    alt={activePartner.name}
                    className="max-w-[95%] max-h-[95%] object-contain"
                    referrerPolicy="no-referrer"
                  />
                ) : (
                  <Building2 className="w-10 h-10 text-[#004c91]" />
                )}
              </div>

              {/* Text side beside logo */}
              <div className="flex-1 text-white">
                {/* Multi-Device typography fitting user instruction: Tên đối tác được viết cỡ to */}
                <h2 className="text-xl sm:text-2xl md:text-3xl font-black tracking-tight leading-tight line-clamp-2 drop-shadow-md">
                  {activePartner.name}
                </h2>

                {/* Quốc gia kèm icon location phía trước */}
                <div className="flex items-center justify-center sm:justify-start gap-1.5 mt-2.5 text-xs sm:text-sm font-bold text-orange-200">
                  <MapPin className="w-4 h-4 text-[#f37021] fill-orange-500/10" />
                  <span>{activePartner.country}</span>
                </div>
              </div>

            </div>
          </motion.div>

          {/* 2. Giới thiệu chung Block */}
          <motion.div 
            initial={{ opacity: 0, y: 15 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.4, delay: 0.1 }}
            className="bg-white rounded-2xl p-6 sm:p-8 border border-slate-100 shadow-[0_4px_20px_rgba(0,0,0,0.015)] relative overflow-hidden"
          >
            {/* Orange border line accent at top */}
            <div className="absolute top-0 left-0 w-full h-[3px] bg-gradient-to-r from-[#f37021] to-orange-400" />
            
            <div className="flex items-center gap-3 mb-4 select-none">
              <div className="w-9 h-9 rounded-xl bg-orange-100/50 flex items-center justify-center text-[#f37021]">
                <BookOpen className="w-4.5 h-4.5" />
              </div>
              <h3 className="text-xl font-extrabold text-[#004c91] tracking-tight">
                Giới thiệu chung
              </h3>
            </div>

            <p className="text-slate-600 text-base leading-relaxed font-medium">
              {activePartner.description}
            </p>
          </motion.div>

          {/* 3. Địa chỉ trụ sở kèm icon address */}
          <motion.div 
            initial={{ opacity: 0, y: 15 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.4, delay: 0.15 }}
            className="bg-white rounded-2xl p-6 sm:p-8 border border-slate-100 shadow-[0_4px_20px_rgba(0,0,0,0.015)] relative overflow-hidden"
          >
            <div className="flex items-center gap-3 mb-4 select-none">
              <div className="w-9 h-9 rounded-xl bg-orange-100/40 flex items-center justify-center text-[#f37021]">
                <MapPin className="w-5 h-5 text-[#f37021]" />
              </div>
              <h3 className="text-xl font-extrabold text-[#004c91] tracking-tight">
                Địa chỉ trụ sở chính
              </h3>
            </div>

            <div className="p-5 bg-gradient-to-r from-orange-50/30 to-slate-50 rounded-xl border border-slate-100">
              <p className="font-bold text-slate-700 leading-relaxed text-base">
                {activePartner.address}
              </p>
            </div>
            
            <div className="mt-3.5 flex items-center gap-1.5 text-xs text-slate-400 font-extrabold uppercase tracking-wider pl-1 select-none">
              <Building2 className="w-3.5 h-3.5 text-[#004c91]" />
              <span>Campus trung tâm chính của học viện</span>
            </div>
          </motion.div>

          {/* 4. Ghé thăm website button CTA */}
          <motion.div 
            initial={{ opacity: 0, y: 15 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.4, delay: 0.2 }}
            className="flex flex-col items-center mt-2 relative select-none w-full"
          >
            <motion.a 
              href={activePartner.web} 
              target="_blank" 
              rel="noopener noreferrer"
              whileHover={{ 
                scale: 1.02, 
                backgroundColor: '#e65c00',
                boxShadow: '0 10px 25px rgba(243, 112, 33, 0.35)'
              }}
              whileTap={{ scale: 0.98 }}
              className="w-full py-4.5 bg-[#f37021] font-black text-white text-center rounded-2xl shadow-md transition-all duration-300 flex items-center justify-center gap-2 cursor-pointer text-base"
            >
              <span>Ghé thăm website đối tác</span>
              <ExternalLink className="w-5 h-5" />
            </motion.a>
            <p className="text-slate-400 text-[10px] font-black uppercase tracking-widest mt-2">
              Bảo mật và bảo hộ bởi tổ chức giáo dục FPT
            </p>
          </motion.div>

        </div>

      </div>
    </div>
  );
}
