/**
 * Component CTASection
 * Liên kết dẫn dắt hành động ở trang chủ, điều hướng người xem đặt lịch tham quan.
 */

// Đây là component hiển thị phần Kêu gọi hành động (Call to Action)
import React from 'react';
import { Calendar, MonitorPlay } from 'lucide-react';

export function CTASection() {
  return (
    <section className="py-12 bg-white">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          
          <button className="group relative overflow-hidden bg-fpt-navy text-white rounded-2xl p-10 flex flex-col items-center justify-center text-center hover:-translate-y-1 transition-all duration-300 shadow-lg hover:shadow-xl hover:shadow-fpt-navy/20">
            <div className="absolute inset-0 bg-[url('https://images.unsplash.com/photo-1541339907198-e08756dedf3f?ixlib=rb-4.0.3')] bg-cover bg-center opacity-10 group-hover:opacity-20 transition-opacity blur-sm"></div>
            <div className="absolute inset-0 bg-gradient-to-t from-fpt-navy via-fpt-navy/90 to-fpt-navy/80"></div>
            
            <div className="relative z-10 flex flex-col items-center">
              <div className="w-16 h-16 bg-white/10 rounded-full flex items-center justify-center mb-6 group-hover:scale-110 group-hover:bg-fpt-orange transition-all duration-300">
                <Calendar className="w-8 h-8" />
              </div>
              <h3 className="text-2xl sm:text-3xl font-bold mb-3 uppercase tracking-wide">Đăng ký thăm quan trường</h3>
              <p className="text-blue-100 font-medium">Dành cho đối tác đăng ký làm việc trực tiếp</p>
            </div>
          </button>

          <button className="group relative overflow-hidden bg-fpt-orange text-white rounded-2xl p-10 flex flex-col items-center justify-center text-center hover:-translate-y-1 transition-all duration-300 shadow-lg hover:shadow-xl hover:shadow-fpt-orange/20">
            <div className="absolute inset-0 bg-[url('https://images.unsplash.com/photo-1622359407337-b7654edfe8ba?ixlib=rb-4.0.3')] bg-cover bg-center opacity-10 group-hover:opacity-20 transition-opacity blur-sm"></div>
            <div className="absolute inset-0 bg-gradient-to-t from-fpt-orange via-fpt-orange/90 to-fpt-orange/80"></div>
            
            <div className="relative z-10 flex flex-col items-center">
              <div className="w-16 h-16 bg-white/20 rounded-full flex items-center justify-center mb-6 group-hover:scale-110 group-hover:bg-white group-hover:text-fpt-orange transition-all duration-300">
                <MonitorPlay className="w-8 h-8" />
              </div>
              <h3 className="text-2xl sm:text-3xl font-bold mb-3 uppercase tracking-wide">Visit FPTU Online</h3>
              <p className="text-orange-100 font-medium">Trải nghiệm không gian 360 độ trực tuyến</p>
            </div>
          </button>

        </div>
      </div>
    </section>
  );
}
