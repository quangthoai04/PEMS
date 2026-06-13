/**
 * Component NewsSection
 * Khu vực hiển thị tin tức sự kiện nổi bật trên trang chủ.
 */

// Đây là component hiển thị các bài viết tin tức mới nhất ở trang chủ
import React from 'react';
import { ArrowRight } from 'lucide-react';
import { Link } from 'react-router-dom';
import newsPattern from '../../assets/images/news_pattern.svg';

const mockNews = [
  {
    id: 1,
    title: 'Đại học FPT mở rộng quan hệ hợp tác với các trường Đại học tại Nhật Bản',
    excerpt: 'Nhằm mang đến thêm nhiều cơ hội học tập và trải nghiệm quốc tế cho sinh viên, Đại học FPT đã ký kết thỏa thuận hợp tác với nhiều đối tác mới...',
    image: 'https://images.unsplash.com/photo-1541339907198-e08756dedf3f?ixlib=rb-4.0.3&auto=format&fit=crop&w=800&q=80',
    date: '10/05/2026'
  },
  {
    id: 2,
    title: 'Hơn 200 sinh viên quốc tế tham gia chương trình trao đổi kỳ Fall 2026',
    excerpt: 'Chương trình Inbound Learning tại Đại học FPT thu hút đông đảo sinh viên từ các nước trong khu vực và trên thế giới đến tham gia học tập...',
    image: 'https://images.unsplash.com/photo-1523240795612-9a054b0db644?ixlib=rb-4.0.3&auto=format&fit=crop&w=800&q=80',
    date: '08/05/2026'
  },
  {
    id: 3,
    title: 'Cơ hội nhận học bổng toàn phần du học chuyển tiếp tại Úc cho sinh viên IT',
    excerpt: 'Đại học FPT cùng với Đại học công nghệ Swinburne thông báo cấp học bổng toàn phần dành riêng cho sinh viên ngành Công nghệ thông tin...',
    image: 'https://images.unsplash.com/photo-1606761568499-6d2451b23c66?ixlib=rb-4.0.3&auto=format&fit=crop&w=800&q=80',
    date: '05/05/2026'
  }
];

export function NewsSection() {
  return (
    <section id="news" className="py-24 bg-slate-50/80 border-y border-gray-100 relative overflow-hidden">
      {/* Decorative Background Elements */}
      <div className="absolute inset-0 z-0 pointer-events-none opacity-50" style={{ backgroundImage: `url(${newsPattern})`, backgroundSize: '600px 400px' }}></div>
      
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 relative z-10">
        <div className="flex items-end justify-between mb-12">
          <div>
            <h2 className="text-3xl font-bold text-fpt-navy">Tin Tức Nổi Bật</h2>
            <div className="w-16 h-1.5 bg-fpt-orange mt-4 rounded-full"></div>
          </div>
          <Link to="/news" className="hidden sm:flex items-center gap-2 text-fpt-orange font-medium hover:text-fpt-orange-hover transition-colors">
            Xem tất cả <ArrowRight className="w-4 h-4" />
          </Link>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8">
          {mockNews.map((item) => (
            <Link to={`/news/${item.id}`} key={item.id} className="group bg-white border border-gray-100 rounded-2xl overflow-hidden shadow-sm hover:shadow-xl transition-all duration-300 flex flex-col">
              <div className="relative h-56 overflow-hidden">
                <img 
                  src={item.image} 
                  alt={item.title} 
                  className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-700 ease-in-out"
                />
                <div className="absolute top-4 left-4 bg-white/90 backdrop-blur px-3 py-1 rounded-full text-xs font-semibold text-fpt-navy shadow-sm">
                  {item.date}
                </div>
              </div>
              <div className="p-6 flex flex-col flex-grow">
                <h3 className="text-lg font-bold text-gray-900 group-hover:text-fpt-orange transition-colors line-clamp-2 mb-3">
                  {item.title}
                </h3>
                <p className="text-gray-600 text-sm leading-relaxed line-clamp-3 mb-6">
                  {item.excerpt}
                </p>
                <div className="mt-auto">
                  <span className="inline-flex items-center gap-1.5 text-sm font-semibold text-fpt-navy group-hover:text-fpt-orange transition-colors">
                    Đọc tiếp <ArrowRight className="w-4 h-4 translate-x-0 group-hover:translate-x-1 transition-transform" />
                  </span>
                </div>
              </div>
            </Link>
          ))}
        </div>
      </div>
    </section>
  );
}
