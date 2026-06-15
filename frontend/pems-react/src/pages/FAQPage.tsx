/**
 * Trang FAQPage
 * Trang hiển thị các câu hỏi thường gặp dành cho người dùng công cộng.
 * Cho phép người dùng xem và tìm kiếm câu trả lời cho các thắc mắc chung.
 */

import React, { useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { Search, ChevronDown, Check, HelpCircle, ChevronLeft, ChevronRight } from 'lucide-react';
import { Link } from 'react-router-dom';

const mockFAQs = [
  { id: 1, type: 'Chương trình', question: 'Điều kiện để tham gia học kỳ trao đổi là gì?', answer: 'Sinh viên phải hoàn thành ít nhất 2 học kỳ tại trường, điểm trung bình >= 7.0, không nợ môn, và có chứng chỉ ngoại ngữ phù hợp với yêu cầu của trường đối tác.' },
  { id: 2, type: 'Học phí', question: 'Tôi có phải đóng học phí cho trường đối tác không?', answer: 'Tùy thuộc vào chương trình và thỏa thuận giữa 2 trường, đa phần là không đóng thêm học phí cho trường đối tác, chỉ đóng như bình thường tại FPTU.' },
  { id: 3, type: 'Visa', question: 'Trường có hỗ trợ làm visa không?', answer: 'Trường sẽ cung cấp các giấy tờ cần thiết như giấy chấp nhận nhập học, hướng dẫn thủ tục. Sinh viên sẽ phải tự đi nộp hồ sơ tại Đại sứ quán.' },
  { id: 4, type: 'Ký túc xá', question: 'Có bắt buộc ở ký túc xá khi học trao đổi không?', answer: 'Không bắt buộc, sinh viên có thể tự thuê ngoài nếu tìm được chỗ ở phù hợp, tuy nhiên ở ký túc xá sẽ hỗ trợ làm quen nhanh hơn.' },
  { id: 5, type: 'Chương trình', question: 'Có thể chuyển đổi tín chỉ như thế nào?', answer: 'Tín chỉ được chuyển đổi dựa trên sự tương đương của môn học giữa hai trường và cần được phê duyệt trước khi đi.' },
  { id: 6, type: 'Học phí', question: 'Ngoài học phí, tôi cần chuẩn bị những chi phí gì?', answer: 'Bạn cần chuẩn bị vé máy bay, bảo hiểm quốc tế, chi phí sinh hoạt (ăn, ở, đi lại) và phí visa.' },
  { id: 7, type: 'Ký túc xá', question: 'Trường có hỗ trợ tìm ký túc xá không?', answer: 'Trường đối tác thường sẽ gửi thông tin đăng ký ký túc xá, hoặc danh sách nhà ở được khuyến nghị.' },
  { id: 8, type: 'Visa', question: 'Có cần chứng minh tài chính khi làm visa không?', answer: 'Đa số các quốc gia sẽ yêu cầu chứng minh tài chính với một số tiền tối thiểu để đảm bảo khả năng sinh hoạt.' },
];

const faqTypes = ['Tất cả', 'Chương trình', 'Học phí', 'Visa', 'Ký túc xá'];

export function FAQPage() {
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedType, setSelectedType] = useState('Tất cả');
  const [openFAQ, setOpenFAQ] = useState<number | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const itemsPerPage = 5;

  const handleSearch = (e: React.ChangeEvent<HTMLInputElement>) => {
    setSearchQuery(e.target.value);
    setCurrentPage(1);
    setOpenFAQ(null);
  };

  const handleTypeChange = (type: string) => {
    setSelectedType(type);
    setCurrentPage(1);
    setOpenFAQ(null);
  };

  const filteredFAQs = mockFAQs.filter(faq => {
    const matchesSearch = faq.question.toLowerCase().includes(searchQuery.toLowerCase()) || 
                          faq.answer.toLowerCase().includes(searchQuery.toLowerCase());
    const matchesType = selectedType === 'Tất cả' || faq.type === selectedType;
    return matchesSearch && matchesType;
  });

  const totalPages = Math.ceil(filteredFAQs.length / itemsPerPage);
  const currentFAQs = filteredFAQs.slice((currentPage - 1) * itemsPerPage, currentPage * itemsPerPage);

  return (
    <div className="bg-slate-50 min-h-screen pt-24 pb-20">
      {/* Header section */}
      <div className="bg-[#004c91] text-white py-16 px-4 md:px-8 shadow-inner relative overflow-hidden mb-12">
        <div className="absolute top-0 right-0 p-8 opacity-10 pointer-events-none transform translate-x-12 -translate-y-12">
          <HelpCircle className="w-64 h-64 text-white" />
        </div>
        <div className="max-w-4xl mx-auto text-center relative z-10">
          <h1 className="text-4xl md:text-5xl font-extrabold mb-6">Câu hỏi thường gặp</h1>
          <p className="text-blue-100 text-lg md:text-xl max-w-2xl mx-auto mb-10">
            Tìm kiếm thông tin nhanh chóng hoặc duyệt qua các danh mục dưới đây để giải đáp thắc mắc của bạn về các chương trình hợp tác quốc tế.
          </p>
          
          {/* Search bar */}
          <div className="relative max-w-xl mx-auto">
            <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
              <Search className="h-6 w-6 text-gray-400" />
            </div>
            <input
              type="text"
              placeholder="Nhập từ khóa tìm kiếm (VD: visa, học phí...)"
              value={searchQuery}
              onChange={handleSearch}
              className="block w-full pl-12 pr-4 py-4 md:py-5 border-none rounded-2xl text-gray-900 bg-white placeholder-gray-400 focus:outline-none focus:ring-4 focus:ring-orange-500/30 text-lg shadow-xl"
            />
          </div>
        </div>
      </div>

      <div className="max-w-4xl mx-auto px-4 md:px-8">
        {/* Categories */}
        <div className="flex flex-wrap gap-3 justify-center mb-10">
          {faqTypes.map((type) => (
            <button
              key={type}
              onClick={() => handleTypeChange(type)}
              className={`px-5 py-2.5 rounded-full text-sm font-bold transition-all ${
                selectedType === type
                  ? 'bg-[#f37021] text-white shadow-md'
                  : 'bg-white text-gray-600 hover:bg-orange-50 hover:text-[#f37021] border border-gray-200'
              }`}
            >
              {type}
            </button>
          ))}
        </div>

        {/* FAQ List */}
        <div className="space-y-4">
          <AnimatePresence>
            {currentFAQs.map((faq) => {
              const isOpen = openFAQ === faq.id;
              
              return (
                <motion.div
                  key={faq.id}
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, scale: 0.95 }}
                  className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden hover:shadow-md transition-shadow"
                >
                  <button
                    onClick={() => setOpenFAQ(isOpen ? null : faq.id)}
                    className="w-full px-6 py-5 flex items-center justify-between text-left focus:outline-none focus:bg-slate-50/50"
                  >
                    <div className="flex flex-col md:flex-row md:items-center gap-2 md:gap-4 pr-8">
                      <span className="inline-block px-3 py-1 bg-blue-50 text-[#004c91] text-xs font-bold rounded-lg border border-blue-100 whitespace-nowrap self-start md:self-auto">
                        {faq.type}
                      </span>
                      <h3 className={`text-lg md:text-xl font-bold transition-colors ${isOpen ? 'text-[#f37021]' : 'text-[#004c91]'}`}>
                        {faq.question}
                      </h3>
                    </div>
                    
                    <div className={`flex-shrink-0 w-10 h-10 rounded-full flex items-center justify-center transition-transform duration-300 ${isOpen ? 'bg-orange-50 rotate-180' : 'bg-gray-50'}`}>
                      <ChevronDown className={`w-5 h-5 ${isOpen ? 'text-[#f37021]' : 'text-gray-400'}`} />
                    </div>
                  </button>
                  
                  <AnimatePresence>
                    {isOpen && (
                      <motion.div
                        initial={{ height: 0, opacity: 0 }}
                        animate={{ height: "auto", opacity: 1 }}
                        exit={{ height: 0, opacity: 0 }}
                        transition={{ duration: 0.3, ease: "easeInOut" }}
                        className="overflow-hidden"
                      >
                        <div className="px-6 pb-6 pt-2">
                          <div className="p-5 bg-slate-50 rounded-xl rounded-tl-none border-l-4 border-[#004c91]">
                            <p className="text-gray-700 leading-relaxed font-medium">
                              {faq.answer}
                            </p>
                          </div>
                        </div>
                      </motion.div>
                    )}
                  </AnimatePresence>
                </motion.div>
              );
            })}
          </AnimatePresence>
          
          {filteredFAQs.length === 0 && (
            <motion.div 
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              className="text-center py-20 bg-white rounded-3xl border border-gray-100"
            >
              <HelpCircle className="w-16 h-16 text-gray-300 mx-auto mb-4" />
              <h3 className="text-xl font-bold text-gray-900 mb-2">Không tìm thấy kết quả</h3>
              <p className="text-gray-500">
                Không tìm thấy câu hỏi nào phù hợp với "{searchQuery}". Vui lòng thử lại với từ khóa khác.
              </p>
            </motion.div>
          )}
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="mt-10 flex justify-center items-center gap-2">
            <button
              onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
              disabled={currentPage === 1}
              className="w-10 h-10 rounded-xl flex items-center justify-center bg-white border border-gray-200 text-gray-600 disabled:opacity-50 disabled:cursor-not-allowed hover:bg-gray-50 transition-colors"
            >
              <ChevronLeft className="w-5 h-5" />
            </button>
            
            {Array.from({ length: totalPages }).map((_, idx) => (
              <button
                key={idx}
                onClick={() => setCurrentPage(idx + 1)}
                className={`w-10 h-10 rounded-xl flex items-center justify-center font-bold text-sm transition-all ${
                  currentPage === idx + 1
                    ? 'bg-[#004c91] text-white shadow-sm'
                    : 'bg-white border border-gray-200 text-gray-600 hover:bg-gray-50'
                }`}
              >
                {idx + 1}
              </button>
            ))}

            <button
              onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
              disabled={currentPage === totalPages}
              className="w-10 h-10 rounded-xl flex items-center justify-center bg-white border border-gray-200 text-gray-600 disabled:opacity-50 disabled:cursor-not-allowed hover:bg-gray-50 transition-colors"
            >
              <ChevronRight className="w-5 h-5" />
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
