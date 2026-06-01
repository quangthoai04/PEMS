// Đây là trang hiển thị chi tiết một bài viết tin tức ở giao diện phía người dùng
import React, { useEffect } from 'react';
import { Calendar, User } from 'lucide-react';
import { useParams } from 'react-router-dom';

const articleMock = {
  id: 1,
  title: 'TỪ MABUHAY ĐẾN SALAMAT VÀ MỘT CÁCH LÀM VIỆC KHÁC BIỆT: HÀNH TRANG MÌNH MANG VỀ SAU KỲ OJT TẠI MANILA, PHILIPPINES',
  date: '18/03/2026',
  author: 'Phòng Hợp tác Quốc tế',
  category: 'NEWS',
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

export function NewsDetailPage() {
  const { id } = useParams();
  
  // In a real app we'd fetch the article using the ID. Here we use mock data.
  const article = articleMock;

  // Scroll to top when loading the details page
  useEffect(() => {
    window.scrollTo(0, 0);
  }, [id]);

  return (
    <div className="pt-24 md:pt-28 pb-12 md:pb-20 bg-white min-h-screen">
      <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
        
        {/* Title */}
        <h1 className="text-3xl md:text-4xl font-bold text-gray-900 leading-tight mb-6 mt-0">
          {article.title}
        </h1>
        
        {/* Meta Info */}
        <div className="flex items-center flex-wrap gap-4 mb-6">
          <div className="flex items-center gap-1.5 text-gray-500 text-[15px]">
            <Calendar className="w-4 h-4" />
            <span>{article.date}</span>
          </div>
          <div className="flex items-center gap-1.5 text-gray-500 text-[15px]">
            <User className="w-4 h-4" />
            <span>{article.author}</span>
          </div>
          <span className="bg-[#f0fdf4] text-[#054826] text-[11px] font-bold px-2.5 py-0.5 rounded-[4px] uppercase tracking-wide">
            {article.category}
          </span>
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
        <div className="mb-10 rounded-lg overflow-hidden">
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

        {/* Copyright */}
        <p className="text-[14px] text-gray-400 opacity-80 font-medium pb-8 border-b border-white">
          © Bản quyền thuộc về Phòng Hợp tác Quốc tế - Đại học FPT
        </p>

      </div>
    </div>
  );
}
