// Đây là trang xem chi tiết một bài báo tin tức trong khu vực quản trị
import React, { useEffect } from 'react';
import { Calendar, User, Clock } from 'lucide-react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion } from 'motion/react';

const articleMock = {
  id: 1,
  title: 'TỪ MABUHAY ĐẾN SALAMAT VÀ MỘT CÁCH LÀM VIỆC KHÁC BIỆT: HÀNH TRANG MÌNH MANG VỀ SAU KỲ OJT TẠI MANILA, PHILIPPINES',
  date: '18/03/2026',
  author: 'Nguyễn Văn A',
  campus: 'Hà Nội',
  category: 'NEWS',
  status: 'Đã Duyệt',
  updatedAt: '10/05/2024 14:20',
  sapo: 'Từ "Mabuhay" đến "Salamat" là cả một hành trình: Đi để làm việc, nhưng trở về với nhiều hơn cả một trải nghiệm nghề nghiệp. Ở Manila, mình học được rằng khác biệt không phải rào cản mà chính là điểm tựa để chúng ta vươn xa hơn trong một môi trường làm việc quốc tế.',
  image: 'https://images.unsplash.com/photo-1543269865-cbf427effbad?w=1200&q=80',
  content: `
    <h3>1. Hành trình bắt đầu từ những điều khác biệt</h3>
    <p>Khi mới đặt chân đến Manila, điều đầu tiên mình cảm nhận được là sự nhộn nhịp và một chút xa lạ. Ngôn ngữ tiếng Anh tuy là ngôn ngữ chung nhưng cách phát âm (accent) và văn hoá giao tiếp của người bản địa mang nét rất riêng. Lúc đầu mình gặp đôi chút khó khăn trong việc bắt nhịp, nhưng chính sự thân thiện của con người nơi đây đã giúp mình nhanh chóng hoà nhập.</p>
    
    <h3>2. "Một cách làm việc khác biệt"</h3>
    <p>Tại công ty thực tập, mọi thứ diễn ra rất nhanh và đòi hỏi tính tự lập cao. Thay vì được cầm tay chỉ việc, mình được giao những dự án nhỏ và phải tự tìm cách giải quyết. Điều này ban đầu khá áp lực, nhưng nó rèn luyện cho mình kỹ năng giải quyết vấn đề (problem-solving) và sự chủ động trong công việc. Mình nhận ra rằng, ở môi trường quốc tế, kết quả công việc và thái độ làm việc được trân trọng hơn bất kỳ điều gì.</p>
    
    <img src="https://images.unsplash.com/photo-1522071820081-009f0129c71c?w=1200&q=80" alt="Working in team" />
    
    <blockquote>
      "Trải nghiệm quốc tế không chỉ là việc bạn đi được bao xa, mà là bạn mở rộng được tư duy của mình đến đâu." 
    </blockquote>
    
    <h3>3. Những bài học quý giá</h3>
    <p>Sau kỳ OJT, hành trang mình mang về không chỉ là những kiến thức chuyên môn hay chứng nhận thực tập, mà lớn lao hơn là sự trưởng thành trong suy nghĩ. Mình học được cách tôn trọng sự đa dạng văn hoá, cách làm việc nhóm hiệu quả với những con người đến từ nhiều quốc gia khác nhau, và đặc biệt là sự tự tin vào khả năng của bản thân.</p>
    <p>Manila đã dạy cho mình rằng, thế giới ngoài kia rất rộng lớn và đầy rẫy những cơ hội. Đừng ngại bước ra khỏi vùng an toàn của mình, bởi vì mỗi trải nghiệm dù là khó khăn nhất cũng sẽ là một bài học vô giá trên con đường phát triển sự nghiệp sau này.</p>
  `
};

export function NewsDetailDashboard() {
  const { id } = useParams();
  const navigate = useNavigate();
  
  const article = articleMock;

  return (
    <motion.div 
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      transition={{ duration: 0.3 }}
      className="p-8 pb-12 min-h-full max-w-4xl mx-auto"
    >
      <div className="mb-6 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors">Dashboard</button>
        <span className="mx-2">/</span>
        <button onClick={() => navigate('/dashboard/news')} className="hover:text-[#004c91] transition-colors">Quản lý tin tức</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Xem chi tiết</span>
      </div>

      <div className="mb-8">
        <h1 className="text-3xl font-bold text-[#004c91]">Xem chi tiết</h1>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-8 sm:p-12">
        
        {/* Title */}
        <h1 className="text-3xl md:text-4xl font-bold text-gray-900 leading-tight mb-6 mt-0">
          {article.title}
        </h1>
        
        {/* Meta Info */}
        <div className="flex items-center flex-wrap gap-2.5 mb-6">
          <div className="flex items-center gap-1 text-gray-500 text-[14px] font-medium">
            <Calendar className="w-4 h-4" />
            <span>{article.date}</span>
          </div>
          <div className="flex items-center gap-1 text-gray-500 text-[14px] font-medium">
            <User className="w-4 h-4" />
            <span>{article.author}</span>
          </div>
          <div className="text-gray-500 text-[14px] font-medium">
            Campus: {article.campus}
          </div>
          <span className="bg-[#eaffe4] text-[#0aa14f] text-[11px] font-black px-2 py-0.5 rounded-full uppercase tracking-wider shadow-sm border border-[#ceefda]">
            {article.category}
          </span>
          <span className="bg-[#eaffe4] text-[#0aa14f] text-[11px] font-bold px-2 py-0.5 rounded-full shadow-sm border border-[#ceefda] whitespace-nowrap">
            {article.status}
          </span>
          <span className="text-gray-300">|</span>
          <div className="flex items-center gap-1 text-gray-500 text-[14px] font-medium italic">
            <Clock className="w-4 h-4" />
            <span>Cập nhật: {article.updatedAt}</span>
          </div>
        </div>

        {/* Light Gray Line */}
        <div className="h-[1px] bg-gray-200 w-full mb-8"></div>

        {/* Sapo */}
        <div className="flex mb-8">
          <div className="w-[3px] bg-[#f37021] shrink-0 mr-4 rounded-sm"></div>
          <p className="text-[17px] text-gray-700 italic leading-relaxed">
            {article.sapo}
          </p>
        </div>

        {/* Main Image */}
        <div className="mb-10 rounded-lg overflow-hidden border border-gray-100 shadow-sm">
          <img 
            src={article.image} 
            alt={article.title} 
            className="w-full h-auto object-cover" 
          />
        </div>

        {/* Article Content */}
        <div className="news-content text-gray-800">
          <div dangerouslySetInnerHTML={{ __html: article.content }}></div>
        </div>

        <style>{`
          .news-content h3 {
            font-size: 1.5rem;
            font-weight: 700;
            color: #111827;
            margin-top: 2.5rem;
            margin-bottom: 1.25rem;
          }
          .news-content p {
            font-size: 1.05rem;
            margin-bottom: 1.25rem;
            line-height: 1.8;
          }
          .news-content img {
            width: 100%;
            border-radius: 0.5rem;
            margin-top: 2rem;
            margin-bottom: 2rem;
            border: 1px solid #f3f4f6;
            box-shadow: 0 1px 2px 0 rgba(0, 0, 0, 0.05);
          }
          .news-content blockquote {
            border-left: 4px solid #f37021;
            padding-left: 1.25rem;
            font-style: italic;
            color: #4b5563;
            margin: 2rem 0;
            background-color: #fff7ed;
            padding: 1.25rem;
            border-radius: 0 0.5rem 0.5rem 0;
          }
        `}</style>
        
        {/* Light Gray Line Bottom */}
        <div className="h-[1px] bg-gray-200 w-full mt-12 mb-6"></div>

        {/* Back button */}
        <button 
          onClick={() => navigate('/dashboard/news')}
          className="text-[#004c91] font-semibold hover:underline flex items-center gap-1"
        >
          &larr; Quay lại danh sách
        </button>

      </div>
    </motion.div>
  );
}
