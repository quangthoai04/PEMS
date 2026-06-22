/**
 * Component PartnersSection
 * Khu vực hiển thị danh sách và biểu trưng của các đối tác chiến lược.
 */

// Đây là component hiển thị danh sách các đối tác
import React, { useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { ChevronLeft, ChevronRight } from 'lucide-react';

import logo01 from '../../assets/Logo/logo01.png';
import logo02 from '../../assets/Logo/logo02.png';
import logo03 from '../../assets/Logo/logo03.png';
import logo04 from '../../assets/Logo/logo04.png';
import logo05 from '../../assets/Logo/logo05.png';
import logo06 from '../../assets/Logo/logo06.png';
import logo07 from '../../assets/Logo/logo07.png';
import logo08 from '../../assets/Logo/logo08.png';
import logo09 from '../../assets/Logo/logo09.png';
import logo10 from '../../assets/Logo/logo10.png';
import logo11 from '../../assets/Logo/logo11.png';
import logo12 from '../../assets/Logo/logo12.png';
import logo13 from '../../assets/Logo/logo13.jpg';
import logo14 from '../../assets/Logo/logo14.png';
import logo15 from '../../assets/Logo/logo15.png';
import logo16 from '../../assets/Logo/logo16.png';
import logo17 from '../../assets/Logo/logo17.png';
import logo18 from '../../assets/Logo/logo18.png';

const partnersData = [
  // Page 1
  { name: 'Partner 1', logo: logo01 },
  { name: 'Partner 2', logo: logo02 },
  { name: 'Partner 3', logo: logo03 },
  { name: 'Partner 4', logo: logo04 },
  { name: 'Partner 5', logo: logo05 },
  { name: 'Partner 6', logo: logo06 },
  { name: 'Partner 7', logo: logo07 },
  { name: 'Partner 8', logo: logo08 },
  { name: 'Partner 9', logo: logo09 },
  { name: 'Partner 10', logo: logo10 },
  { name: 'Partner 11', logo: logo11 },
  { name: 'Partner 12', logo: logo12 },
  { name: 'Partner 13', logo: logo13 },
  { name: 'Partner 14', logo: logo14 },
  { name: 'Partner 15', logo: logo15 },
  { name: 'Partner 16', logo: logo16 },
  { name: 'Partner 17', logo: logo17 },
  { name: 'Partner 18', logo: logo18 },
  // Page 2
  { name: 'Partner 19', logo: logo01 },
  { name: 'Partner 20', logo: logo02 },
  { name: 'Partner 21', logo: logo03 },
  { name: 'Partner 22', logo: logo04 },
  { name: 'Partner 23', logo: logo05 },
  { name: 'Partner 24', logo: logo06 },
  { name: 'Partner 25', logo: logo07 },
  { name: 'Partner 26', logo: logo08 },
  { name: 'Partner 27', logo: logo09 },
  { name: 'Partner 28', logo: logo10 },
  { name: 'Partner 29', logo: logo11 },
  { name: 'Partner 30', logo: logo12 },
  { name: 'Partner 31', logo: logo13 },
  { name: 'Partner 32', logo: logo14 },
  { name: 'Partner 33', logo: logo15 },
  { name: 'Partner 34', logo: logo16 },
  { name: 'Partner 35', logo: logo17 },
  { name: 'Partner 36', logo: logo18 },
];

export function PartnersSection() {
  const [currentPage, setCurrentPage] = useState(0);
  const itemsPerPage = 18;
  const totalPages = Math.ceil(partnersData.length / itemsPerPage);

  const nextPage = () => {
    setCurrentPage((prev) => (prev + 1) % totalPages);
  };

  const prevPage = () => {
    setCurrentPage((prev) => (prev - 1 + totalPages) % totalPages);
  };

  const currentPartners = partnersData.slice(
    currentPage * itemsPerPage,
    (currentPage + 1) * itemsPerPage
  );

  return (
    <section id="partners" className="py-24 bg-white overflow-hidden relative">
      <div className="absolute left-[-20%] bottom-[-20%] w-[600px] h-[600px] bg-fpt-orange/5 rounded-full blur-[100px] pointer-events-none"></div>
      <div className="absolute right-[-10%] top-[-10%] w-[500px] h-[500px] bg-fpt-navy/5 rounded-full blur-[80px] pointer-events-none"></div>
      <div className="absolute inset-0 opacity-[0.03] pointer-events-none" style={{ backgroundImage: 'radial-gradient(#F37021 2px, transparent 2px)', backgroundSize: '32px 32px' }}></div>
      
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 mb-12 text-center relative z-10 w-full">
        <h2 className="text-3xl font-bold text-fpt-navy mb-2">Đối tác & Hợp tác quốc tế</h2>
        <div className="w-24 h-1.5 bg-fpt-orange mt-4 mb-6 mx-auto rounded-full"></div>
        <p className="text-gray-600 max-w-3xl mx-auto text-lg leading-relaxed">
          Mạng lưới đối tác bền chặt với hàng trăm trường Đại học và doanh nghiệp trong và ngoài nước, mang lại nhiều cơ hội trải nghiệm thực tế.
        </p>
      </div>

      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 relative z-10 w-full overflow-visible">
        <div className="relative flex items-center group">
          <button 
            onClick={prevPage}
            className="absolute -left-5 sm:-left-8 lg:-left-16 z-20 w-10 h-10 sm:w-12 sm:h-12 rounded-full border border-gray-200 bg-white flex items-center justify-center hover:bg-fpt-navy hover:text-white hover:border-fpt-navy transition-all shadow-md sm:opacity-0 sm:group-hover:opacity-100"
          >
            <ChevronLeft className="w-6 h-6" />
          </button>
          
          <div className="w-full overflow-hidden px-1 py-4">
            <AnimatePresence mode="wait">
              <motion.div
                key={currentPage}
                initial={{ opacity: 0, x: 20 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: -20 }}
                transition={{ duration: 0.3, ease: "easeInOut" }}
                className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 sm:gap-6"
              >
                {currentPartners.map((partner, index) => (
                  <div 
                    key={index} 
                    className="bg-white border border-gray-100 rounded-2xl h-24 sm:h-28 lg:h-32 shadow-sm hover:shadow-lg flex items-center justify-center p-4 hover:-translate-y-1 transition-all duration-300 group/item"
                  >
                    <img 
                      src={partner.logo} 
                      alt={partner.name} 
                      className="max-w-full max-h-full object-contain transition-all duration-300"
                    />
                  </div>
                ))}
              </motion.div>
            </AnimatePresence>
          </div>

          <button 
            onClick={nextPage}
            className="absolute -right-5 sm:-right-8 lg:-right-16 z-20 w-10 h-10 sm:w-12 sm:h-12 rounded-full border border-gray-200 bg-white flex items-center justify-center hover:bg-fpt-navy hover:text-white hover:border-fpt-navy transition-all shadow-md sm:opacity-0 sm:group-hover:opacity-100"
          >
            <ChevronRight className="w-6 h-6" />
          </button>
        </div>
        
        {/* Pagination Dots */}
        <div className="flex justify-center gap-3 mt-8">
           {Array.from({ length: totalPages }).map((_, i) => (
             <button
               key={i}
               onClick={() => setCurrentPage(i)}
               className={`h-2.5 rounded-full transition-all duration-300 ${i === currentPage ? 'bg-fpt-orange w-8' : 'bg-gray-300 w-2.5 hover:bg-gray-400'}`}
             />
           ))}
        </div>
      </div>
    </section>
  );
}

