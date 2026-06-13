/**
 * Trang NewsPage (Public)
 * Trang tin tức chính thức công cộng của trường.
 */

// Đây là trang hiển thị danh sách các bài viết tin tức ở giao diện phía người dùng
import React, { useState } from 'react';
import { ChevronLeft, ChevronRight, Calendar } from 'lucide-react';
import { Link } from 'react-router-dom';

const allArticles = [
  {
    id: 1,
    title: 'TỪ MABUHAY ĐẾN SALAMAT VÀ MỘT CÁCH LÀM VIỆC KHÁC BIỆT: HÀNH TRANG MÌNH MANG VỀ SAU KỲ OJT TẠI MANILA, PHILIPPINES',
    summary: 'Từ "Mabuhay" đến "Salamat" là cả một hành trình: Đi để làm việc, nhưng trở về với nhiều hơn cả một trải nghiệm nghề nghiệp. Ở Manila, mình học được rằng khác biệt không phải rào cản mà chính là [...]',
    image: 'https://images.unsplash.com/photo-1543269865-cbf427effbad?w=800&q=80',
    date: '18/03/2026',
  },
  {
    id: 2,
    title: 'TRẢI NGHIỆM THAM QUAN THỰC TẾ TẠI CÔNG TY CỔ PHẦN IN SỐ 7',
    summary: 'Ngày 18/03/2026, các bạn sinh viên ngành Thiết kế Mỹ thuật số Trường Đại học FPT đã có chuyến tham quan thực tế đầy thú vị tại Công ty Cổ phần In số 7. Tại đây, các bạn đã được [...]',
    image: 'https://images.unsplash.com/photo-1521737604893-d14cc237f11d?w=800&q=80',
    date: '18/03/2026',
  },
  {
    id: 3,
    title: 'Trường ĐH FPT tổ chức Hội nghị quốc tế EAI FIS...',
    summary: 'Sự kiện mang đến nhiều thông tin hữu ích về trí tuệ nhân tạo và các công nghệ tiên tiến.',
    image: 'https://images.unsplash.com/photo-1505373877841-8d25f7d46678?w=800&q=80',
    date: '15/03/2026',
  },
  {
    id: 4,
    title: 'Ra mắt "Trạm Học" - Nền tảng học tập số đồng hàn...',
    summary: 'Một không gian mới dành cho sinh viên thử sức và học hỏi các kỹ năng mềm quan trọng.',
    image: 'https://images.unsplash.com/photo-1516321497487-e288fb19713f?w=800&q=80',
    date: '12/03/2026',
  },
  {
    id: 5,
    title: 'Trải nghiệm khởi nghiệp - Giá trị khác biệt dành cho...',
    summary: 'Tìm hiểu về những khó khăn và bài học đắt giá trong quá trình khởi nghiệp của cựu sinh viên.',
    image: 'https://images.unsplash.com/photo-1522071820081-009f0129c71c?w=800&q=80',
    date: '10/03/2026',
  },
  {
    id: 6,
    title: 'Sinh viên ngành Trí tuệ nhân tạo FPTU ghi dấu ấ...',
    summary: 'Những thành tích đáng nể tại các cuộc thi công nghệ quy mô quốc gia và khu vực.',
    image: 'https://images.unsplash.com/photo-1531482615713-2afd69097998?w=800&q=80',
    date: '08/03/2026',
  },
  {
    id: 7,
    title: 'Học kỳ 5 cày thật, thành phẩm thật!',
    summary: 'Những dự án capstone ấn tượng của sinh viên khóa 17 sau một học kỳ đầy nỗ lực.',
    image: 'https://images.unsplash.com/photo-1524178232363-1fb2b075b655?w=800&q=80',
    date: '05/03/2026',
  },
  {
    id: 8,
    title: 'Gặp gỡ những gương mặt thủ khoa đầu vào khóa 19',
    summary: 'Họ là ai và bí quyết nào giúp họ đạt điểm cao trong kỳ thi tuyển sinh vừa qua?',
    image: 'https://images.unsplash.com/photo-1523240795612-9a054b0db644?w=800&q=80',
    date: '02/03/2026',
  },
  {
    id: 9,
    title: 'Hội thảo: Tương lai của ngành Công nghệ thông tin',
    summary: 'Các chuyên gia hàng đầu thảo luận về những xu hướng công nghệ sẽ định hình tương lai.',
    image: 'https://images.unsplash.com/photo-1531297180771-469b228f4d96?w=800&q=80',
    date: '28/02/2026',
  },
  {
    id: 10,
    title: 'Chương trình trao đổi sinh viên mùa Thu 2026',
    summary: 'Cơ hội tuyệt vời để trải nghiệm môi trường học tập quốc tế tại các trường đối tác.',
    image: 'https://images.unsplash.com/photo-1523050854058-8df90110c9f1?w=800&q=80',
    date: '25/02/2026',
  },
];

const latestArticles = [
  allArticles[2],
  allArticles[3],
  allArticles[4],
];

export function NewsPage() {
  const [currentPage, setCurrentPage] = useState(1);
  const itemsPerPage = 5;
  const totalPages = Math.ceil(allArticles.length / itemsPerPage);

  const startIndex = (currentPage - 1) * itemsPerPage;
  const currentArticles = allArticles.slice(startIndex, startIndex + itemsPerPage);

  const handleNextPage = () => {
    if (currentPage < totalPages) {
      setCurrentPage(currentPage + 1);
    }
  };

  const handlePrevPage = () => {
    if (currentPage > 1) {
      setCurrentPage(currentPage - 1);
    }
  };

  return (
    <div className="pt-28 md:pt-36 pb-12 md:pb-20 bg-white">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="text-center mb-16">
          <h1 className="text-3xl md:text-3xl font-bold text-[#004c91] uppercase mb-4">
            Bảng tin
          </h1>
          <div className="w-24 h-1 bg-[#f37021] mx-auto rounded-full"></div>
        </div>

        {/* Body */}
        <div className="flex flex-col lg:flex-row gap-12 lg:gap-0">
          
          {/* Left Part: 5 Articles */}
          <div className="lg:w-2/3 lg:pr-10 lg:border-r lg:border-gray-200">
            <div className="flex flex-col space-y-10">
              {currentArticles.map((article) => (
                <Link to={`/news/${article.id}`} key={article.id} className="flex flex-col sm:flex-row gap-6 items-start group block">
                  {/* Image */}
                  <div className="w-full sm:w-[300px] shrink-0">
                    <div className="aspect-[4/3] sm:aspect-[4/2.5] rounded-lg overflow-hidden ring-1 ring-black/5">
                      <img 
                        src={article.image} 
                        alt={article.title} 
                        className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                      />
                    </div>
                  </div>
                  {/* Content */}
                  <div className="flex-1 flex flex-col">
                    <h2 className="text-[17px] font-bold text-gray-900 leading-snug uppercase group-hover:text-[#ea580c] transition-colors cursor-pointer">
                      {article.title}
                    </h2>
                    <div className="flex items-center gap-3 mt-3 mb-3">
                      <div className="flex items-center gap-1.5 text-gray-500 text-[15px]">
                        <Calendar className="w-4 h-4" />
                        <span>{article.date}</span>
                      </div>
                    </div>
                    <p className="text-[15px] text-gray-600 line-clamp-3 leading-relaxed">
                      {article.summary}
                    </p>
                  </div>
                </Link>
              ))}
            </div>

            {/* Pagination */}
            <div className="mt-12 flex items-center justify-center space-x-2">
              <button 
                onClick={handlePrevPage}
                disabled={currentPage === 1}
                className="p-2 border border-gray-300 rounded hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                aria-label="Previous page"
              >
                <ChevronLeft className="w-5 h-5 text-gray-600" />
              </button>
              
              {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                <button
                  key={page}
                  onClick={() => setCurrentPage(page)}
                  className={`w-10 h-10 rounded text-sm font-medium transition-colors ${
                    currentPage === page 
                      ? 'bg-[#f37021] text-white border border-[#f37021]' 
                      : 'border border-gray-300 text-gray-700 hover:bg-gray-50'
                  }`}
                >
                  {page}
                </button>
              ))}

              <button 
                onClick={handleNextPage}
                disabled={currentPage === totalPages}
                className="p-2 border border-gray-300 rounded hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                aria-label="Next page"
              >
                <ChevronRight className="w-5 h-5 text-gray-600" />
              </button>
            </div>
          </div>

          {/* Right Part: Latest Articles */}
          <div className="lg:w-1/3 lg:pl-10">
            <h3 className="text-xl font-bold text-gray-900 mb-3">
              Bài viết mới nhất
            </h3>
            <div className="h-0.5 w-full bg-[#ea580c] mb-6"></div>
            
            <div className="flex flex-col space-y-6">
              {latestArticles.map((article) => (
                <Link to={`/news/${article.id}`} key={`latest-${article.id}`} className="flex gap-4 items-start group cursor-pointer border-b border-gray-100 pb-4 last:border-0 last:pb-0 block">
                  {/* Thumbnail */}
                  <div className="w-[100px] h-[100px] shrink-0 rounded-lg overflow-hidden">
                    <img 
                      src={article.image} 
                      alt={article.title} 
                      className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                    />
                  </div>
                  {/* Content */}
                  <div className="flex-1 flex flex-col pt-1">
                    <h4 className="text-[15px] font-bold text-gray-900 group-hover:text-[#ea580c] transition-colors leading-snug line-clamp-3">
                      {article.title}
                    </h4>
                    <div className="flex items-center gap-2 mt-3">
                      <div className="flex items-center gap-1.5 text-gray-500 text-[13px]">
                        <Calendar className="w-3.5 h-3.5" />
                        <span>{article.date}</span>
                      </div>
                    </div>
                  </div>
                </Link>
              ))}
            </div>
          </div>

        </div>
      </div>
    </div>
  );
}
