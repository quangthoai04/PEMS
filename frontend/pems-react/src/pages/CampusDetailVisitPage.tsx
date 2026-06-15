/**
 * Trang CampusDetailVisitPage (Public)
 * Trang miêu tả không gian tham quan chi tiết của một cơ sở cụ thể.
 */

// Trang chi tiết tham quan campus (Campus Detail Visit Page), cung cấp trải nghiệm tham quan ảo tương tác với hình ảnh, không gian 360 độ, đặc điểm nổi bật và hệ thống thuyết minh âm thanh.
import React, { useState, useEffect } from "react";
import { useParams } from "react-router-dom";
import { motion, AnimatePresence } from "motion/react";
import {
  ChevronRight,
  ChevronLeft,
  MapPin,
  Image as ImageIcon,
  ZoomIn,
  X,
  ArrowLeft,
  Volume2,
  VolumeX,
  Square,
  View,
  Share2,
  Info,
  Facebook,
  Twitter,
  Link as LinkIcon,
} from "lucide-react";

import { useNavigate } from "react-router-dom";

// Imports
import fptLogo from "../assets/images/regenerated_image_1778552336496.png";
import loadingBg from "../assets/images/loading.png";
import bgHN from "../assets/FPTbanner_visit/hola_new.jpg";
import bgHCM from "../assets/FPTbanner_visit/HCM.png";
import bgCT from "../assets/FPTbanner_visit/CanTho.png";
import bgDN from "../assets/FPTbanner_visit/DaNang.png";
import bgQN from "../assets/FPTbanner_visit/QuyNhon.png";
import quanAPImg from "../assets/FPTbanner_visit/QuanAP.jpg";

const fptHolaModules = import.meta.glob("../assets/img_visit_detail/*.jpg", { eager: true });
const fptHolaImages = Object.values(fptHolaModules).map((module: any) => module.default);

const sharedMenu = [
  {
    id: "tongquan",
    label: "TỔNG QUAN",
    icon: <ImageIcon className="w-5 h-5" />,
    subItems: ["Toàn cảnh bên ngoài"],
  },
  {
    id: "alpha",
    label: "TÒA ALPHA",
    icon: <ImageIcon className="w-5 h-5" />,
    subItems: ["Trước tòa nhà Alpha", "Sảnh chính", "Phòng học", "Thư viện"],
  },
  {
    id: "beta",
    label: "TÒA BETA",
    icon: <ImageIcon className="w-5 h-5" />,
    subItems: ["Trước tòa nhà Beta", "Sảnh chính", "Phòng học", "Phòng hội trường"],
  },
  {
    id: "delta",
    label: "TÒA DELTA",
    icon: <ImageIcon className="w-5 h-5" />,
    subItems: [
      "Trước tòa nhà Delta",
      "Sảnh chính",
      "Thư viện",
      "Phòng học điển hình",
      "Trung tâm khởi nghiệp & nghiên cứu",
      "Phòng thí nghiệm đổi mới & sáng tạo SAP",
      "Phòng học Nhạc cụ dân tộc"
    ],
  },
  {
    id: "epsilon",
    label: "TÒA EPSILON",
    icon: <ImageIcon className="w-5 h-5" />,
    subItems: ["Trước tòa nhà Epsilon", "Căn tin", "Phòng học"],
  },
  {
    id: "gamma",
    label: "TÒA GAMMA",
    icon: <ImageIcon className="w-5 h-5" />,
    subItems: ["Trước tòa nhà Gamma", "Sảnh chính", "Văn phòng làm việc"],
  },
  {
    id: "dichvu",
    label: "KHU DỊCH VỤ",
    icon: <ImageIcon className="w-5 h-5" />,
    subItems: ["Quán Cafe", "Cửa hàng tiện ích", "Khu ăn uống"],
  },
  {
    id: "kytucxa",
    label: "KÝ TÚC XÁ",
    icon: <ImageIcon className="w-5 h-5" />,
    subItems: ["Khu KTX A", "Khu KTX B", "Siêu thị mini", "Sân cỏ nhân tạo"],
  },
  {
    id: "thethao",
    label: "KHU THỂ THAO",
    icon: <ImageIcon className="w-5 h-5" />,
    subItems: ["Sân bóng đá", "Sân bóng rổ", "Nhà thi đấu vovinam", "Khu tập gym"],
  },
];

const campusData: any = {
  hn: {
    name: "Campus Hà Nội",
    bg: bgHN,
    description:
      "Đại học FPT Hà Nội tọa lạc tại Khu Công nghệ cao Hòa Lạc. Nơi đây có kiến trúc hiện đại, không gian xanh và cơ sở vật chất tiên tiến được thiết kế cho một môi trường học tập tối ưu.",
    menu: sharedMenu,
  },
  hcm: {
    name: "Campus Hồ Chí Minh",
    bg: bgHCM,
    description:
      "Đại học FPT TP. HCM mang đến môi trường học thuật sôi động với công nghệ hiện đại và cộng đồng sinh viên năng động.",
    menu: sharedMenu,
  },
  dn: {
    name: "Campus Đà Nẵng",
    bg: bgDN,
    description:
      "Tọa lạc tại FPT City Đà Nẵng, cung cấp môi trường học tập hiện đại hòa mình cùng thiên nhiên.",
    menu: sharedMenu,
  },
  ct: {
    name: "Campus Cần Thơ",
    bg: bgCT,
    description:
      "Đại học FPT Cần Thơ như là một trung tâm công nghệ và giáo dục ở vùng đồng bằng sông Cửu Long.",
    menu: sharedMenu,
  },
  qn: {
    name: "Campus Quy Nhơn",
    bg: bgQN,
    description:
      "Đại học FPT Quy Nhơn chú trọng AI và toán học, được xây dựng tại thành phố biển xinh đẹp.",
    menu: sharedMenu,
  },
};

export function CampusDetailVisitPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const campus = campusData[id || ""] || campusData["hn"];
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  const [activeMenu, setActiveMenu] = useState<string | null>(null);
  const [hoveredMenu, setHoveredMenu] = useState<string | null>(null);
  const [isImageZoomed, setIsImageZoomed] = useState(false);
  const [showShareMenu, setShowShareMenu] = useState(false);
  const [zoomScale, setZoomScale] = useState(1);
  const [currentDetailImage, setCurrentDetailImage] = useState(quanAPImg);
  const [galleryImages, setGalleryImages] = useState<string[]>([quanAPImg]);
  const [currentGalleryIdx, setCurrentGalleryIdx] = useState(0);

  useEffect(() => {
    if (activeMenu) {
      document.body.style.overflow = "hidden";
      if (id === "hn" && fptHolaImages.length > 0) {
        const shuffled = [...fptHolaImages].sort(() => 0.5 - Math.random());
        const selected = shuffled.slice(0, 4);
        setGalleryImages(selected);
        setCurrentDetailImage(selected[0]);
        setCurrentGalleryIdx(0);
      } else {
        setGalleryImages([quanAPImg]);
        setCurrentDetailImage(quanAPImg);
        setCurrentGalleryIdx(0);
      }
    } else {
      document.body.style.overflow = "";
    }
    return () => {
      document.body.style.overflow = "";
    };
  }, [activeMenu, id]);

  const handlePrevMenu = () => {
    if (!activeMenu) return;
    const currentIndex = campus.menu.findIndex((m: any) => m.id === activeMenu);
    if (currentIndex > 0) {
      setActiveMenu(campus.menu[currentIndex - 1].id);
    } else {
      setActiveMenu(campus.menu[campus.menu.length - 1].id); // cycle
    }
  };

  const handleNextMenu = () => {
    if (!activeMenu) return;
    const currentIndex = campus.menu.findIndex((m: any) => m.id === activeMenu);
    if (currentIndex < campus.menu.length - 1) {
      setActiveMenu(campus.menu[currentIndex + 1].id);
    } else {
      setActiveMenu(campus.menu[0].id); // cycle
    }
  };

  return (
    <div className="relative min-h-[calc(100vh-64px)] w-full flex flex-col bg-gray-900">
      {/* Sidebar - Floating */}
      <div
        className={`fixed top-1/2 left-0 z-50 flex transition-transform duration-500 ease-in-out ${
          isSidebarOpen ? "translate-x-4 md:translate-x-6 -translate-y-1/2" : "-translate-x-full -translate-y-1/2"
        }`}
      >
        {/* Sidebar Frame */}
        <div className="w-56 h-auto max-h-[calc(100vh-140px)] bg-black/30 backdrop-blur-xl flex flex-col overflow-visible rounded-2xl shadow-[0_8px_32px_rgba(0,0,0,0.4)] border border-white/20">
          <nav className="flex-1 flex flex-col relative">
            <div className="absolute -inset-0.5 bg-gradient-to-b from-fpt-orange/20 to-transparent opacity-50 rounded-2xl pointer-events-none"></div>
            {campus.menu.map((item: any, index: number) => (
              <div 
                key={item.id}
                className="relative"
                onMouseEnter={() => setHoveredMenu(item.id)}
                onMouseLeave={() => setHoveredMenu(null)}
              >
                <button
                  onClick={() => setActiveMenu(item.id)}
                  className={`w-full flex items-center justify-between px-4 py-3 border-b border-white/10 transition-all duration-300 text-left group relative z-10 ${
                    activeMenu === item.id
                      ? "bg-[#F37021] text-white shadow-[0_0_20px_rgba(243,112,33,0.5)]"
                      : hoveredMenu === item.id 
                        ? "bg-[#eb742d]/80 text-white backdrop-blur-md" 
                        : "bg-transparent text-gray-200 hover:bg-[#eb742d]/80 hover:text-white"
                  } ${index === 0 ? "rounded-t-2xl" : ""} ${
                    index === campus.menu.length - 1 ? "border-b-0 rounded-b-2xl" : ""
                  }`}
                >
                  <span className="uppercase tracking-widest text-[11px] sm:text-xs font-semibold">
                    {item.label}
                  </span>
                  {item.icon && <span className="opacity-90">{item.icon}</span>}
                </button>
                
                {/* Submenu Popout */}
                <AnimatePresence>
                  {hoveredMenu === item.id && item.subItems && item.subItems.length > 0 && (
                    <motion.div
                      initial={{ opacity: 0, x: -10, scale: 0.95 }}
                      animate={{ opacity: 1, x: 0, scale: 1 }}
                      exit={{ opacity: 0, x: -10, scale: 0.95 }}
                      transition={{ duration: 0.2, ease: "easeOut" }}
                      className="absolute top-0 left-full ml-2 w-72 backdrop-blur-2xl rounded-2xl shadow-[0_10px_40px_rgba(0,0,0,0.5)] z-50 overflow-hidden border border-white/20"
                      style={{ background: "linear-gradient(135deg, rgba(235,116,45,0.85) 0%, rgba(200,80,30,0.95) 100%)" }}
                    >
                      <div className="flex flex-col py-3">
                        {item.subItems.map((sub: string, subIdx: number) => (
                          <button
                            key={subIdx}
                            className="w-full text-left px-5 py-3 text-sm text-white hover:bg-white/20 transition-all flex justify-between items-center group/sub"
                          >
                            <span className="font-medium tracking-wide drop-shadow-sm group-hover/sub:translate-x-1 transition-transform">{sub}</span>
                            <MapPin className="w-4 h-4 opacity-70 group-hover/sub:opacity-100 group-hover/sub:scale-110 transition-all" />
                          </button>
                        ))}
                      </div>
                    </motion.div>
                  )}
                </AnimatePresence>
              </div>
            ))}
          </nav>
        </div>

        {/* Sidebar Toggle Button */}
        <button
          onClick={() => setIsSidebarOpen(!isSidebarOpen)}
          className="absolute top-6 -right-8 w-8 h-12 bg-black/40 backdrop-blur-xl border border-white/20 border-l-0 rounded-r-xl flex items-center justify-center text-white hover:bg-fpt-orange hover:border-fpt-orange hover:shadow-[0_0_20px_rgba(243,112,33,0.5)] transition-all cursor-pointer shadow-xl group"
        >
          {isSidebarOpen ? (
            <ChevronLeft className="w-5 h-5 group-hover:-translate-x-1 transition-transform" />
          ) : (
            <ChevronRight className="w-5 h-5 group-hover:translate-x-1 transition-transform" />
          )}
        </button>
      </div>

      {/* Back Button */}
      <motion.button 
        initial={{ opacity: 0, x: -20 }}
        animate={{ opacity: 1, x: 0 }}
        transition={{ delay: 0.2 }}
        onClick={() => navigate("/visit-fptu")}
        className="absolute top-24 left-6 sm:top-28 z-40 p-3 bg-black/30 backdrop-blur-md rounded-full border border-white/20 text-white hover:bg-fpt-orange hover:border-fpt-orange hover:scale-110 hover:shadow-[0_0_20px_rgba(243,112,33,0.5)] transition-all flex items-center gap-2 group"
      >
        <ArrowLeft className="w-5 h-5 group-hover:-translate-x-1 transition-transform" />
        <span className="hidden sm:inline font-medium pr-2 text-sm tracking-wide">Trở Về</span>
      </motion.button>

      {/* Hero Section */}
      <div className="relative w-full h-[100vh] flex items-center justify-center overflow-hidden bg-black">
        {/* Background Image */}
        <div className="absolute inset-0 z-0">
          <motion.img
            initial={{ scale: 1.1, filter: "brightness(0.5)" }}
            animate={{ scale: 1, filter: "brightness(0.7)" }}
            transition={{ duration: 1.5, ease: "easeOut" }}
            src={campus.bg}
            alt={campus.name}
            className="w-full h-full object-cover"
          />
        </div>
        
        {/* Dark overlay gradient */}
        <div className="absolute inset-0 z-10 bg-gradient-to-t from-gray-900 via-gray-900/40 to-black/20 pointer-events-none" />
        
        {/* Hero Title */}
        <div className="relative z-20 text-center flex flex-col items-center mt-20 px-4">
          <motion.div
            initial={{ opacity: 0, y: 30 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.8, delay: 0.3, ease: "easeOut" }}
            className="inline-block px-4 py-1.5 bg-white/10 text-white font-medium text-xs tracking-[0.2em] uppercase rounded-full border border-white/30 backdrop-blur-md mb-6"
          >
            Virtual Tour
          </motion.div>
          <motion.h1 
            initial={{ opacity: 0, y: 30 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.8, delay: 0.5, ease: "easeOut" }}
            className="text-5xl sm:text-6xl md:text-7xl lg:text-8xl font-black text-transparent bg-clip-text bg-gradient-to-b from-white via-white to-white/70 tracking-tighter drop-shadow-2xl max-w-4xl leading-tight"
          >
            {campus.name}
          </motion.h1>
          <motion.p
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ duration: 1, delay: 0.8 }}
             className="mt-6 text-lg sm:text-xl text-gray-200 font-light max-w-2xl text-center leading-relaxed drop-shadow-md"
          >
            {campus.description || "Khám phá không gian học tập và trải nghiệm hiện đại. Click vào mũi tên bên trái để bắt đầu chuyến tham quan."}
          </motion.p>
          
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.8, delay: 1, ease: "easeOut" }}
            className="mt-10 flex flex-wrap justify-center gap-4"
          >
            <button 
              onClick={() => {
                if (!isSidebarOpen) setIsSidebarOpen(true);
                if (campus.menu && campus.menu.length > 0) {
                     setActiveMenu(campus.menu[0].id);
                }
              }}
              className="px-8 py-3.5 bg-fpt-orange hover:bg-fpt-orange/90 text-white rounded-full font-medium transition-all hover:scale-105 hover:shadow-[0_0_25px_rgba(243,112,33,0.6)] flex items-center gap-2 group"
            >
              Bắt đầu tham quan <ChevronRight className="w-5 h-5 ml-1 group-hover:translate-x-1 transition-transform" />
            </button>
            <button
               onClick={() => setIsSidebarOpen(true)}
               className="px-8 py-3.5 bg-white/10 hover:bg-white/20 text-white rounded-full font-medium backdrop-blur-md border border-white/20 transition-all hover:scale-105"
            >
               Xem các khu vực
            </button>
          </motion.div>

          {/* Highlights / Stats */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ duration: 1, delay: 1.2 }}
            className="mt-14 grid grid-cols-1 sm:grid-cols-3 gap-4 sm:gap-8 max-w-2xl w-full border-t border-white/10 pt-6"
          >
            <div className="flex flex-col items-center">
              <span className="text-2xl font-bold text-white mb-1 drop-shadow-md">30+</span>
              <span className="text-[9px] sm:text-[10px] font-semibold text-gray-300 uppercase tracking-[0.2em] text-center">Khu vực tham quan</span>
            </div>
            <div className="flex flex-col items-center">
              <span className="text-2xl font-bold text-white mb-1 drop-shadow-md">10K+</span>
              <span className="text-[9px] sm:text-[10px] font-semibold text-gray-300 uppercase tracking-[0.2em] text-center">Sinh viên</span>
            </div>
            <div className="flex flex-col items-center">
              <span className="text-2xl font-bold text-white mb-1 drop-shadow-md">5</span>
              <span className="text-[9px] sm:text-[10px] font-semibold text-gray-300 uppercase tracking-[0.2em] text-center">Cơ sở toàn quốc</span>
            </div>
          </motion.div>
        </div>
      </div>

      {/* Selected Content Overlay (Global) */}
      <AnimatePresence>
        {activeMenu && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.4 }}
            className={`fixed top-[64px] inset-x-0 bottom-0 z-40 flex items-center justify-center transition-all duration-500 ${isSidebarOpen ? 'md:pl-56' : ''} p-4 sm:p-6 md:p-8`}
          >
            <div 
              className="fixed inset-0 bg-black/50 backdrop-blur-md" 
              onClick={() => setActiveMenu(null)} 
            />
            
            <motion.div
              initial={{ opacity: 0, scale: 0.92, y: 20 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.96, y: 10 }}
              transition={{ duration: 0.5, ease: [0.22, 1, 0.36, 1] }}
              className="relative z-10 w-full max-w-6xl h-[80vh] lg:h-[500px] flex flex-col gap-4 overflow-y-auto [&::-webkit-scrollbar]:hidden [-ms-overflow-style:none] [scrollbar-width:none] drop-shadow-2xl"
            >
                {/* Close Button Top Right */}
                <div className="flex justify-end w-full sticky top-0 z-50 pointer-events-none pb-0">
                    <button
                      onClick={() => setActiveMenu(null)}
                      className="p-1 bg-white/30 hover:bg-white/50 text-white rounded-full transition-all duration-300 backdrop-blur-xl border border-white/40 hover:scale-110 shadow-[0_8px_32px_rgba(0,0,0,0.2)] pointer-events-auto group mt-2 mr-2"
                    >
                      <X className="w-4 h-4 group-hover:rotate-90 transition-transform duration-300" />
                    </button>
                </div>

                <div className="grid grid-cols-1 lg:grid-cols-12 gap-4 lg:gap-6 pb-6 pr-2 pl-2 h-full">
                  {/* Left Column: Info & Text (Bento 1 & 2) */}
                  <div className="lg:col-span-5 flex flex-col gap-4 h-full">
                    {/* Title Box */}
                    <motion.div 
                      className="bg-white/15 dark:bg-black/20 backdrop-blur-2xl border border-white/30 rounded-[1.5rem] sm:rounded-[2rem] p-5 sm:p-6 shadow-[0_8px_32px_rgba(0,0,0,0.1)] relative group transition-all duration-500 hover:shadow-[0_8px_40px_rgba(255,255,255,0.1)] shrink-0"
                    >
                      <div className="absolute inset-0 overflow-hidden rounded-[inherit] pointer-events-none">
                        <div className="absolute -top-20 -right-20 w-40 h-40 bg-fpt-orange/20 rounded-full blur-3xl group-hover:bg-fpt-orange/30 transition-colors duration-500"></div>
                        <div className="absolute -bottom-20 -left-20 w-40 h-40 bg-blue-500/20 rounded-full blur-3xl group-hover:bg-blue-400/30 transition-colors duration-500"></div>
                      </div>
                      
                      <div className="flex items-center justify-between mb-6 relative z-10">
                        <div className="inline-block px-4 py-1.5 bg-fpt-orange/90 text-white font-medium text-xs tracking-widest uppercase rounded-full border border-white/30 backdrop-blur-md shadow-[0_0_15px_rgba(243,112,33,0.4)] group-hover:scale-105 transition-transform origin-left">
                          Trải nghiệm không gian
                        </div>
                        <div className="flex gap-2 relative">
                          <button 
                            onClick={() => setShowShareMenu(!showShareMenu)}
                            className="text-white/70 hover:text-fpt-orange transition-all hover:scale-110 hover:drop-shadow-[0_0_15px_rgba(243,112,33,0.8)] group/share flex items-center justify-center p-1" 
                            title="Chia sẻ"
                          >
                            <Share2 className="w-5 h-5" />
                          </button>

                          {/* Share Menu Dropdown */}
                          <AnimatePresence>
                            {showShareMenu && (
                              <motion.div
                                initial={{ opacity: 0, y: 10, scale: 0.9 }}
                                animate={{ opacity: 1, y: 0, scale: 1 }}
                                exit={{ opacity: 0, y: 10, scale: 0.9 }}
                                className="absolute right-0 top-full mt-2 w-48 bg-white/10 backdrop-blur-xl border border-white/20 rounded-2xl shadow-2xl p-2 z-50 flex flex-col gap-1"
                              >
                                <button className="flex items-center gap-3 px-3 py-2 text-sm text-white/90 hover:text-white hover:bg-white/10 rounded-xl transition-colors text-left"
                                 onClick={() => {
                                   navigator.clipboard.writeText(window.location.href);
                                   alert("Đã sao chép liên kết!");
                                   setShowShareMenu(false);
                                 }}
                                >
                                  <LinkIcon className="w-4 h-4" /> Sao chép liên kết
                                </button>
                                <button className="flex items-center gap-3 px-3 py-2 text-sm text-white/90 hover:text-white hover:bg-blue-500/20 hover:text-blue-400 rounded-xl transition-colors text-left"
                                 onClick={() => {
                                   window.open(`https://www.facebook.com/sharer/sharer.php?u=${window.location.href}`, '_blank');
                                   setShowShareMenu(false);
                                 }}
                                >
                                  <Facebook className="w-4 h-4" /> Facebook
                                </button>
                                <button className="flex items-center gap-3 px-3 py-2 text-sm text-white/90 hover:text-white hover:bg-sky-500/20 hover:text-sky-400 rounded-xl transition-colors text-left"
                                 onClick={() => {
                                   window.open(`https://twitter.com/intent/tweet?url=${window.location.href}`, '_blank');
                                   setShowShareMenu(false);
                                 }}
                                >
                                  <Twitter className="w-4 h-4" /> Twitter
                                </button>
                              </motion.div>
                            )}
                          </AnimatePresence>
                        </div>
                      </div>
                      
                      <h3 className="text-3xl sm:text-4xl font-black text-transparent bg-clip-text bg-gradient-to-br from-white via-white to-white/70 mb-4 leading-tight tracking-tight drop-shadow-sm relative z-10">
                        {campus.menu.find((m: any) => m.id === activeMenu)?.label}
                      </h3>
                      <div className="w-24 h-1.5 bg-gradient-to-r from-fpt-orange to-transparent rounded-full opacity-80 relative z-10" />
                    </motion.div>

                    {/* Content Box */}
                    <div className="bg-white/80 dark:bg-white/10 backdrop-blur-3xl border border-white/40 rounded-[1.5rem] sm:rounded-[2rem] p-5 sm:p-6 shadow-[0_8px_32px_rgba(0,0,0,0.1)] grow flex flex-col justify-between overflow-y-auto relative">
                       <div className="flex flex-col mb-4">
                         <div className="flex justify-between items-start mb-6 gap-4">
                           <div className="prose prose-base sm:prose-lg text-black dark:text-white font-light leading-relaxed">
                            <p className="first-letter:text-4xl first-letter:font-bold first-letter:text-fpt-orange first-letter:mr-1 first-letter:float-left">
                              Không gian hiện đại được thiết kế mở đón ánh sáng tự nhiên, tô điểm bởi hệ thống mảng xanh đa dạng, mang đến cảm giác thư thái và nguồn năng lượng tích cực.
                            </p>
                            <p className="mt-4 text-black font-normal dark:text-gray-300 text-sm sm:text-base">
                              Kiến trúc mang đậm lối thiết kế sinh thái tạo ra sự gần gũi, giao hòa với thiên nhiên. Được trang bị đầy đủ các tiện ích công nghệ cao, đáp ứng mọi tiêu chuẩn quốc tế, giúp sinh viên và giảng viên được tự do kết nối, thảo luận nhóm và trải nghiệm môi trường học thuật lý tưởng.
                            </p>
                           </div>
                           <button 
                             className="shrink-0 flex items-center justify-center w-10 h-10 sm:w-12 sm:h-12 rounded-full bg-fpt-orange/10 text-fpt-orange hover:bg-fpt-orange hover:text-white transition-all duration-300 hover:scale-110 hover:shadow-[0_0_15px_rgba(243,112,33,0.4)]"
                             title="Nghe thuyết minh"
                           >
                             <Volume2 className="w-5 h-5 sm:w-6 sm:h-6" />
                           </button>
                         </div>

                       </div>

                       {/* Nav Buttons */}
                      <div className="flex items-center justify-between pt-6 border-t border-gray-200 dark:border-white/10 mt-auto">
                        <button 
                          onClick={handlePrevMenu}
                          className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-600 dark:text-gray-300 hover:text-fpt-orange dark:hover:text-fpt-orange hover:bg-fpt-orange/10 dark:hover:bg-fpt-orange/20 rounded-xl transition-all hover:scale-105 active:scale-95"
                        >
                          <ChevronLeft className="w-5 h-5" />
                          <span className="hidden sm:inline">Khu vực trước</span>
                        </button>
                        <button 
                          onClick={handleNextMenu}
                          className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-600 dark:text-gray-300 hover:text-fpt-orange dark:hover:text-fpt-orange hover:bg-fpt-orange/10 dark:hover:bg-fpt-orange/20 rounded-xl transition-all hover:scale-105 active:scale-95"
                        >
                          <span className="hidden sm:inline">Tiếp theo</span>
                          <ChevronRight className="w-5 h-5" />
                        </button>
                      </div>
                    </div>
                  </div>

                  {/* Right Column: Media (Bento 3) */}
                  <div className="lg:col-span-7 flex flex-col gap-4 lg:gap-6 min-h-[300px] md:min-h-[400px] lg:min-h-0 h-full">
                     {/* Big Image Gallery Box */}
                     <div 
                       className="bg-white/15 dark:bg-white/5 backdrop-blur-2xl border border-white/30 rounded-[1.5rem] sm:rounded-[2rem] shadow-[0_8px_32px_rgba(0,0,0,0.15)] w-full h-full relative overflow-hidden group hover:border-white/50 transition-all duration-500 flex flex-col"
                     >
                        <div 
                          className="relative w-full h-full rounded-[1.5rem] sm:rounded-[2rem] overflow-hidden bg-black/20 cursor-pointer"
                          onClick={() => {
                            setIsImageZoomed(true);
                            setZoomScale(1);
                          }}
                        >
                          <AnimatePresence mode="popLayout" initial={false}>
                            <motion.img 
                              key={currentGalleryIdx}
                              initial={{ opacity: 0, scale: 1.05 }}
                              animate={{ opacity: 1, scale: 1 }}
                              exit={{ opacity: 0, scale: 0.95 }}
                              transition={{ duration: 0.5, ease: "easeInOut" }}
                              src={galleryImages[currentGalleryIdx]} 
                              alt="FPTU Scene" 
                              className="absolute inset-0 w-full h-full object-cover transition-transform duration-[3s] group-hover:scale-110" 
                            />
                          </AnimatePresence>
                          <div className="absolute inset-0 bg-gradient-to-t from-black/70 via-transparent to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-500 flex items-end justify-center pb-12 gap-4">
                            <motion.span 
                              initial={{ y: 20, opacity: 0 }}
                              whileHover={{ scale: 1.05 }}
                              animate={{ y: 0, opacity: 1 }}
                              transition={{ delay: 0.1 }}
                              className="flex items-center gap-2 text-white font-medium px-6 py-3 rounded-full bg-white/20 backdrop-blur-md border border-white/40 shadow-xl"
                            >
                              <ZoomIn className="w-5 h-5" /> Phóng to
                            </motion.span>
                            <motion.span 
                              initial={{ y: 20, opacity: 0 }}
                              whileHover={{ scale: 1.05 }}
                              animate={{ y: 0, opacity: 1 }}
                              transition={{ delay: 0.15 }}
                              onClick={(e) => {
                                e.stopPropagation();
                                alert("Tính năng 360 độ đang được phát triển.");
                              }}
                              className="flex items-center gap-2 text-white font-medium px-6 py-3 rounded-full bg-fpt-orange/80 backdrop-blur-md border border-fpt-orange hover:bg-fpt-orange shadow-xl hover:shadow-[0_0_20px_rgba(243,112,33,0.5)] transition-all cursor-pointer"
                            >
                              <View className="w-5 h-5" /> Xem 360°
                            </motion.span>
                          </div>
                          
                          {/* Next/Prev Navigation overlay (only when gallery length > 1) */}
                          {galleryImages.length > 1 && (
                            <>
                              <button
                                onClick={(e) => {
                                  e.stopPropagation();
                                  const nextIdx = currentGalleryIdx === 0 ? galleryImages.length - 1 : currentGalleryIdx - 1;
                                  setCurrentGalleryIdx(nextIdx);
                                  setCurrentDetailImage(galleryImages[nextIdx]);
                                }}
                                className="absolute left-4 top-1/2 -translate-y-1/2 p-2 bg-white/10 hover:bg-white/30 border border-white/20 text-white rounded-full backdrop-blur-md transition-all z-10 opacity-0 group-hover:opacity-100 hover:scale-110 shadow-lg"
                              >
                                <ChevronLeft className="w-6 h-6" />
                              </button>
                              <button
                                onClick={(e) => {
                                  e.stopPropagation();
                                  const nextIdx = currentGalleryIdx === galleryImages.length - 1 ? 0 : currentGalleryIdx + 1;
                                  setCurrentGalleryIdx(nextIdx);
                                  setCurrentDetailImage(galleryImages[nextIdx]);
                                }}
                                className="absolute right-4 top-1/2 -translate-y-1/2 p-2 bg-white/10 hover:bg-white/30 border border-white/20 text-white rounded-full backdrop-blur-md transition-all z-10 opacity-0 group-hover:opacity-100 hover:scale-110 shadow-lg"
                              >
                                <ChevronRight className="w-6 h-6" />
                              </button>
                            </>
                          )}
                        </div>

                        {/* Gallery Navigation */}
                        {galleryImages.length > 1 && (
                          <div 
                            className="absolute bottom-6 left-1/2 -translate-x-1/2 flex items-center gap-3 z-20 px-4 py-2 bg-black/40 backdrop-blur-md rounded-full border border-white/20 hover:bg-black/60 transition-colors" 
                            onClick={e => e.stopPropagation()}
                          >
                            {galleryImages.map((_, idx) => (
                              <button
                                key={idx}
                                onClick={() => {
                                  setCurrentGalleryIdx(idx);
                                  setCurrentDetailImage(galleryImages[idx]);
                                }}
                                className={`w-2.5 h-2.5 rounded-full transition-all duration-300 hover:scale-125 focus:outline-none ${currentGalleryIdx === idx ? 'bg-white shadow-[0_0_12px_rgba(255,255,255,1)] w-6' : 'bg-white/50 hover:bg-white/80'}`}
                              />
                            ))}
                          </div>
                        )}
                     </div>
                  </div>
                </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Image Zoom Modal */}
      <AnimatePresence>
        {isImageZoomed && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-[100] flex items-center justify-center bg-black/95 backdrop-blur-md overflow-hidden"
            onWheel={(e) => {
              if (e.deltaY < 0) {
                setZoomScale(prev => Math.min(prev + 0.1, 4));
              } else {
                setZoomScale(prev => Math.max(prev - 0.1, 0.5));
              }
            }}
          >
            {/* Close button */}
            <button
              onClick={() => setIsImageZoomed(false)}
              className="absolute top-6 right-6 z-[110] p-3 bg-black/50 hover:bg-white/20 text-white rounded-full backdrop-blur-md transition-colors"
            >
              <X className="w-6 h-6" />
            </button>
            
            {/* Zoom help text */}
            <div className="absolute bottom-8 left-1/2 -translate-x-1/2 z-[110] px-6 py-3 bg-black/60 backdrop-blur-md text-white/80 text-sm flex items-center gap-2 pointer-events-none rounded-full">
              <ZoomIn className="w-4 h-4" />
              <span>Cuộn chuột để thu/phóng • Kéo để di chuyển</span>
            </div>

            <motion.div
              className="relative w-full h-full flex items-center justify-center overflow-hidden"
            >
              <motion.img
                drag
                dragConstraints={{ top: -500, bottom: 500, left: -500, right: 500 }}
                dragElastic={0.2}
                whileTap={{ cursor: "grabbing" }}
                src={currentDetailImage}
                alt="FPTU Scene Full"
                animate={{ scale: zoomScale }}
                transition={{ type: "spring", stiffness: 300, damping: 30 }}
                className="max-w-none max-h-none object-contain cursor-grab"
                style={{ maxWidth: "90vw", maxHeight: "90vh" }}
                draggable={false}
              />
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Campus Introduction Section */}
      <div className="w-full bg-white relative overflow-hidden py-24 md:py-32">
        {/* Background decorative blobs */}
        <div className="absolute top-0 left-0 w-full h-full overflow-hidden pointer-events-none">
          <div className="absolute -top-40 -right-40 w-[500px] h-[500px] bg-fpt-orange/5 rounded-full blur-[100px]"></div>
          <div className="absolute top-1/2 -left-40 w-[400px] h-[400px] bg-blue-500/5 rounded-full blur-[100px]"></div>
        </div>

        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 relative z-10">
          <div className="flex flex-col lg:flex-row gap-16 lg:gap-24 items-center">
            
            {/* Left Content (Typography focused) */}
            <motion.div 
              initial={{ opacity: 0, y: 30 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true, margin: "-100px" }}
              transition={{ duration: 0.8, ease: "easeOut" }}
              className="flex-1 lg:max-w-xl w-full"
            >
              <div className="flex items-center gap-4 mb-8">
                <div className="w-12 h-px bg-fpt-orange"></div>
                <span className="text-fpt-orange font-bold tracking-[0.2em] uppercase text-xs sm:text-sm">
                  Khám phá không gian
                </span>
              </div>
              
              <h2 className="text-3xl md:text-5xl lg:text-6xl font-black text-fpt-navy leading-[1.1] tracking-tight mb-8">
                Chào mừng đến với
                <br />
                <span className="text-transparent bg-clip-text bg-gradient-to-r from-fpt-orange to-[#ff9b66] drop-shadow-sm pb-2 inline-block">
                  {campus.name}
                </span>
              </h2>
              
              <p className="text-gray-600 text-lg md:text-xl leading-relaxed mb-10 font-light">
                {campus.description}
              </p>
              
              <div className="relative p-6 md:p-8 rounded-[1.5rem] bg-white border border-gray-100 shadow-[0_8px_30px_rgba(0,0,0,0.04)] group hover:shadow-[0_8px_40px_rgba(243,112,33,0.08)] transition-all duration-500 overflow-hidden">
                <div className="absolute top-0 left-0 w-1.5 h-full bg-gradient-to-b from-fpt-orange to-[#ff9b66] rounded-l-[1.5rem]"></div>
                <div className="absolute top-6 right-6 text-fpt-orange/10 transform rotate-6 group-hover:scale-110 transition-transform duration-700">
                   <svg width="56" height="56" viewBox="0 0 24 24" fill="currentColor"><path d="M14.017 21v-7.391c0-5.704 3.731-9.57 8.983-10.609l.995 2.151c-2.432.917-3.995 3.638-3.995 5.849h4v10h-9.983zm-14.017 0v-7.391c0-5.704 3.748-9.57 9-10.609l.996 2.151c-2.433.917-3.996 3.638-3.996 5.849h3.983v10h-9.983z"/></svg>
                </div>
                <p className="text-gray-700 italic font-medium leading-relaxed text-base md:text-lg relative z-10 w-11/12">
                  "Với thiết kế hài hòa giữa tự nhiên và công nghệ, cơ sở vật chất của chúng tôi mang lại trải nghiệm học tập và sinh hoạt tuyệt vời nhất cho sinh viên, khuyến khích sự sáng tạo và phát triển toàn diện."
                </p>
              </div>
            </motion.div>

            {/* Right Content (Bento Images) */}
            <div className="flex-1 w-full relative">
              <div className="grid grid-cols-2 gap-4 md:gap-6">
                <motion.div 
                  initial={{ opacity: 0, y: 50, scale: 0.95 }}
                  whileInView={{ opacity: 1, y: 0, scale: 1 }}
                  viewport={{ once: true, margin: "-100px" }}
                  transition={{ duration: 0.8, delay: 0.2, ease: "easeOut" }}
                  className="col-span-2 rounded-[2rem] overflow-hidden aspect-[16/10] shadow-[0_20px_40px_rgba(0,0,0,0.08)] group relative cursor-pointer"
                  onClick={() => {
                     setCurrentDetailImage(campus.bg);
                     setIsImageZoomed(true);
                     setZoomScale(1);
                  }}
                >
                  <img src={campus.bg} alt="Campus" className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-[2s] ease-out" />
                  <div className="absolute inset-0 bg-gradient-to-t from-gray-900/40 via-transparent to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-500 flex items-end justify-center pb-6">
                     <span className="bg-white/90 backdrop-blur-md text-gray-900 px-5 py-2.5 rounded-full text-sm font-semibold border border-white/50 flex items-center gap-2 shadow-lg transform translate-y-4 group-hover:translate-y-0 transition-transform duration-500">
                       <ZoomIn className="w-4 h-4" /> Phóng to hình ảnh
                     </span>
                  </div>
                </motion.div>
                
                <motion.div 
                  initial={{ opacity: 0, y: 50 }}
                  whileInView={{ opacity: 1, y: 0 }}
                  viewport={{ once: true, margin: "-100px" }}
                  transition={{ duration: 0.8, delay: 0.4, ease: "easeOut" }}
                  className="rounded-[2rem] overflow-hidden aspect-square shadow-[0_10px_30px_rgba(0,0,0,0.08)] bg-white p-6 md:p-8 flex flex-col justify-end relative group border border-gray-100"
                >
                   <img src={quanAPImg} className="absolute inset-0 w-full h-full object-cover group-hover:scale-110 transition-transform duration-[2s] ease-out" />
                   <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-black/20 to-transparent z-10" />
                   <div className="relative z-20 transform group-hover:-translate-y-2 transition-transform duration-500">
                     <h3 className="text-white font-black text-2xl md:text-3xl mb-1 md:mb-2 tracking-tight">Đẳng Cấp</h3>
                     <p className="text-white/80 text-sm md:text-base font-medium leading-tight">Kiến trúc hiện đại toàn cầu</p>
                   </div>
                </motion.div>

                <motion.div 
                  initial={{ opacity: 0, y: 50 }}
                  whileInView={{ opacity: 1, y: 0 }}
                  viewport={{ once: true, margin: "-100px" }}
                  transition={{ duration: 0.8, delay: 0.6, ease: "easeOut" }}
                  className="rounded-[2rem] overflow-hidden aspect-square shadow-[0_10px_30px_rgba(0,0,0,0.08)] bg-fpt-navy p-6 md:p-8 flex flex-col justify-end relative group"
                >
                   <div className="absolute inset-0 bg-gradient-to-tr from-fpt-navy via-fpt-navy/90 to-blue-800 z-10" />
                   <div className="absolute -top-20 -right-20 w-48 h-48 bg-white/5 rounded-full blur-[40px] group-hover:bg-white/10 transition-colors duration-700" />
                   <div className="absolute bottom-0 right-0 w-32 h-32 bg-fpt-orange/10 rounded-full blur-[30px] group-hover:bg-fpt-orange/20 transition-colors duration-700" />
                   
                   <div className="relative z-20 transform group-hover:-translate-y-2 transition-transform duration-500">
                     <h3 className="text-white font-black text-2xl md:text-3xl mb-1 md:mb-2 tracking-tight">Sinh Thái</h3>
                     <p className="text-white/80 text-sm md:text-base font-medium leading-tight">Không gian xanh bền vững</p>
                   </div>
                </motion.div>
              </div>
            </div>

          </div>
        </div>
      </div>
    </div>
  );
}
