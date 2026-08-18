/**
 * Trang VisitFPTUPage (Public)
 * Màn hình giới thiệu tổng quan quá trình đi thăm quy mô trải nghiệm cơ sở vật chất khu vực campus.
 */

// Trang cổng thông tin tham quan FPTU (Visit FPTU Page), hiển thị bản đồ trực quan cho phép người dùng lựa chọn một trong 5 campus để bắt đầu hành trình trải nghiệm thực tế ảo.
import React, { useState } from "react";
import { motion, useScroll, useTransform } from "motion/react";
import { useNavigate } from "react-router-dom";
import { ArrowUp, Map, Sparkles, Globe, ArrowUpRight } from "lucide-react";
import bgHN from "../assets/FPTbanner_visit/hola_new.jpg";
import bgHCM from "../assets/FPTbanner_visit/HCM.png";
import bgCT from "../assets/FPTbanner_visit/CanTho.png";
import bgDN from "../assets/FPTbanner_visit/DaNang.png";
import bgQN from "../assets/FPTbanner_visit/QuyNhon.png";
import defaultBg from "../assets/FPTbanner_visit/5CS.png";
import { useTranslation } from "react-i18next";

const CAMPUSES = [
  { id: "hn", col: 1, row: 3, labelKey: "hanoi", img: bgHN },
  { id: "hcm", col: 2, row: 2, labelKey: "hochiminh", img: bgHCM },
  { id: "qn", col: 3, row: 1, labelKey: "quynhon", img: bgQN },
  { id: "ct", col: 2, row: 3, labelKey: "cantho", img: bgCT },
  { id: "dn", col: 3, row: 2, labelKey: "danang", img: bgDN },
];

export function VisitFPTUPage() {
  const { t } = useTranslation(['visitFptu']);
  const [activeBg, setActiveBg] = useState<string | null>(null);
  const navigate = useNavigate();
  const { scrollYProgress } = useScroll();
  const y = useTransform(scrollYProgress, [0, 1], ["0%", "50%"]);

  const scrollToTop = () => {
    window.scrollTo({
      top: 0,
      behavior: "smooth"
    });
  };

  return (
    <div className="w-full bg-gray-900 flex flex-col overflow-x-hidden">
      {/* Hero Interactive Section - Screen Height */}
      <div className="relative min-h-screen w-full flex flex-col items-center justify-center pt-24 sm:pt-28 pb-10 overflow-hidden bg-gray-900">
        {/* Background Underlay */}
        <div className="absolute inset-0 z-0 flex items-center justify-center">
          <img
            src={defaultBg}
            alt="Default FPTU"
            className="w-full h-full object-cover opacity-90"
          />
        </div>

        {CAMPUSES.map((c) => (
          <img
            key={c.id}
            src={c.img}
            alt={t(`visitFptu:hero.${c.labelKey}`)}
            className={`absolute inset-0 w-full h-full object-cover transition-opacity duration-700 ease-in-out z-0 ${
              activeBg === c.id ? "opacity-100" : "opacity-0"
            }`}
          />
        ))}

        {/* Dark overlay to make the outlines and text pop */}
        <div className={`absolute inset-0 z-0 transition-colors duration-500 ${activeBg ? "bg-black/40" : "bg-black/10"}`}></div>

        {/* Grid Container */}
        <div className="relative z-10 w-[300px] h-[300px] sm:w-[450px] sm:h-[450px] md:w-[550px] md:h-[550px] lg:w-[650px] lg:h-[650px] xl:w-[750px] xl:h-[750px] -mt-10 lg:-mt-16">
          <div className="absolute inset-0 rotate-45 transform-gpu grid grid-cols-3 grid-rows-3 gap-[1px]">
            {CAMPUSES.map((c, i) => (
              <motion.div
                key={c.id}
                initial={{ opacity: 0, scale: 0.8 }}
                animate={{ opacity: 1, scale: 1 }}
                transition={{ duration: 0.6, delay: i * 0.1 }}
                onMouseEnter={() => setActiveBg(c.id)}
                onMouseLeave={() => setActiveBg(null)}
                onClick={() => navigate(`/visit-fptu/${c.id}`)}
                style={{ gridColumn: c.col, gridRow: c.row }}
                className="border border-white/60 hover:border-white hover:bg-white/10 transition-all duration-300 flex items-center justify-center cursor-pointer group hover:shadow-[0_0_30px_rgba(255,255,255,0.7)] hover:z-20 relative backdrop-blur-[2px]"
              >
                <div className="-rotate-45 transform text-white font-bold text-sm sm:text-base md:text-lg lg:text-xl xl:text-2xl tracking-wide group-hover:scale-110 transition-transform duration-300 drop-shadow-lg text-center px-2 flex flex-col items-center justify-center leading-tight">
                  <span className="opacity-90 text-[0.8em]">{t('visitFptu:hero.campusPrefix')}</span>
                  <span className="whitespace-nowrap">{t(`visitFptu:hero.${c.labelKey}`)}</span>
                </div>
              </motion.div>
            ))}
          </div>
        </div>
        
        {/* Scroll Indicator */}
        <motion.div 
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ delay: 1, duration: 1 }}
          className="absolute bottom-8 left-1/2 -translate-x-1/2 flex flex-col items-center gap-2 z-20 text-white/70"
        >
          <span className="text-xs uppercase tracking-widest font-medium whitespace-nowrap">{t('visitFptu:hero.scrollDown')}</span>
          <div className="w-px h-12 bg-white/30 overflow-hidden relative">
            <motion.div 
              animate={{ y: [0, 48, 0] }}
              transition={{ repeat: Infinity, duration: 2, ease: "linear" }}
              className="w-full h-8 bg-white absolute top-0"
            />
          </div>
        </motion.div>
      </div>

      {/* Overview Section */}
      <div className="w-full bg-white relative z-10 py-24 md:py-32">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex flex-col lg:flex-row items-center gap-16 lg:gap-24">
            
            {/* Left side text */}
            <motion.div 
              initial={{ opacity: 0, x: -40 }}
              whileInView={{ opacity: 1, x: 0 }}
              viewport={{ once: true, margin: "-100px" }}
              transition={{ duration: 0.8, ease: "easeOut" }}
              className="lg:w-5/12 flex flex-col"
            >
              <div className="flex items-center gap-2 mb-6">
                <Globe className="w-5 h-5 text-fpt-orange shrink-0" />
                <span className="text-fpt-orange font-bold uppercase tracking-widest text-sm">{t('visitFptu:overview.badge')}</span>
              </div>
              <h2 className="text-4xl md:text-5xl lg:text-6xl font-black leading-[1.1] mb-6 tracking-tight">
                <span className="text-[#004c91] block">{t('visitFptu:overview.titleLine1')}</span>
                <span className="text-transparent bg-clip-text bg-gradient-to-r from-fpt-orange to-fpt-orange/80 block">{t('visitFptu:overview.titleLine2')}</span>
              </h2>
              <p className="text-lg text-gray-600 mb-8 leading-relaxed font-light">
                {t('visitFptu:overview.description')}
              </p>
              
              <ul className="flex flex-col gap-5 mb-12">
                {[
                  t('visitFptu:overview.benefit1'),
                  t('visitFptu:overview.benefit2'),
                  t('visitFptu:overview.benefit3')
                ].map((item, index) => (
                  <li key={index} className="flex items-center gap-4 group">
                    <div className="w-10 h-10 rounded-full bg-orange-50 flex items-center justify-center shrink-0 group-hover:bg-fpt-orange transition-colors">
                      <Sparkles className="w-4 h-4 text-fpt-orange group-hover:text-white transition-colors" />
                    </div>
                    <span className="text-gray-700 font-normal">{item}</span>
                  </li>
                ))}
              </ul>
              
              <motion.button 
                whileHover={{ scale: 1.05 }}
                whileTap={{ scale: 0.95 }}
                onClick={scrollToTop}
                className="group relative self-start inline-flex items-center justify-center gap-3 px-8 py-4 bg-fpt-orange/10 hover:bg-fpt-orange text-fpt-orange hover:text-white rounded-full font-bold transition-all duration-300 overflow-hidden"
              >
                <span className="relative z-10 text-sm uppercase tracking-wider truncate max-w-[200px]">{t('visitFptu:overview.cta')}</span>
                <ArrowUp className="w-4 h-4 relative z-10 shrink-0 group-hover:-translate-y-1 transition-transform" />
              </motion.button>
            </motion.div>
            
            {/* Right side Images */}
            <motion.div 
              initial={{ opacity: 0 }}
              whileInView={{ opacity: 1 }}
              viewport={{ once: true, margin: "-100px" }}
              transition={{ duration: 1, ease: "easeOut" }}
              className="lg:w-7/12 relative h-[500px] md:h-[600px] w-full"
            >
              <div className="absolute top-0 right-0 w-[80%] h-[80%] rounded-[2rem] overflow-hidden shadow-2xl z-10 border-8 border-white">
                <img src={bgHCM} alt="FPTU HCM" className="w-full h-full object-cover hover:scale-105 transition-transform duration-[2s]" />
              </div>
              <div className="absolute bottom-0 left-0 w-[55%] h-[55%] rounded-[2rem] overflow-hidden shadow-2xl z-20 border-8 border-white">
                <img src={bgDN} alt="FPTU DN" className="w-full h-full object-cover hover:scale-105 transition-transform duration-[2s]" />
              </div>
              
              {/* Decorative shapes */}
              <div className="absolute -top-10 -right-10 w-40 h-40 bg-fpt-orange/5 rounded-full blur-2xl -z-10"></div>
              <div className="absolute -bottom-10 -left-10 w-40 h-40 bg-blue-500/5 rounded-full blur-2xl -z-10"></div>
            </motion.div>
          </div>
        </div>
      </div>
    </div>
  );
}
