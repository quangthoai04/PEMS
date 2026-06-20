/**
 * Trang FAQDetail
 * Trình chỉnh sửa chi tiết của bảng trả lời thông tin lưu trữ Q/A cục bộ.
 */

import React, { useEffect, useState } from 'react';
import { HelpCircle, ChevronLeft, MessageCircle, Info, Edit2, Save, X } from 'lucide-react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion } from 'motion/react';

const mockFAQ = {
  id: 1, 
  type: 'Chương trình', 
  question: 'Điều kiện để tham gia học kỳ trao đổi là gì?', 
  answer: 'Sinh viên phải hoàn thành ít nhất 2 học kỳ tại trường, điểm trung bình >= 7.0, không nợ môn, và có chứng chỉ ngoại ngữ phù hợp với yêu cầu của trường đối tác.', 
  status: 'Hiển thị'
};

export function FAQDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [faq, setFaq] = useState(mockFAQ);
  const [isEditing, setIsEditing] = useState(false);
  const [editingFaq, setEditingFaq] = useState(mockFAQ);

  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase();
  const isHO = userRole === 'HO';
  const isAdmin = userRole === 'ADMIN' || (userRole === 'STAFF' && user?.subRole?.toUpperCase() === 'LEADER');
  const isFullAccess = isHO || isAdmin;

  useEffect(() => {
    // In real app, fetch FAQ by ID based on `id` params
    window.scrollTo(0, 0);
  }, [id]);

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Hiển thị': return <span className="inline-block px-3 py-1 bg-[#eaffe4] text-[#0aa14f] font-bold rounded-full text-xs border border-[#ceefda]">Hiển thị</span>;
      case 'Ẩn': return <span className="inline-block px-3 py-1 bg-gray-100 text-gray-600 font-bold rounded-full text-xs border border-gray-200">Ẩn</span>;
      default: return null;
    }
  };

  return (
    <motion.div 
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      transition={{ duration: 0.3 }}
      className="p-4 md:p-8 space-y-6 bg-gray-50/50 min-h-screen"
    >
      {/* Breadcrumb & Tiêu đề */}
      <div className="flex items-center gap-2 text-sm text-gray-500 mb-6">
        <span className="hover:text-[#004c91] cursor-pointer transition-colors" onClick={() => navigate('/dashboard')}>Dashboard</span>
        <span className="mx-2">/</span>
        <span className="hover:text-[#004c91] cursor-pointer transition-colors" onClick={() => navigate('/dashboard/faq')}>Quản lý FAQ</span>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-medium">Chi tiết FAQ</span>
      </div>
      <div className="border-b border-gray-100 pb-4 mb-6 text-left flex justify-start">
        <h1 className="text-3xl font-bold text-[#004c91]">Chi tiết FAQ</h1>
      </div>

      <div className="bg-white rounded-3xl shadow-sm border border-gray-100 overflow-hidden w-full">
        {/* Header Section (Question & Meta) */}
        <div className="bg-[#004c91] p-8 md:p-10 border-b border-[#003366] relative overflow-hidden">
          <div className="absolute top-0 right-0 p-8 opacity-10 pointer-events-none transform translate-x-4 -translate-y-4">
            <HelpCircle className="w-48 h-48 text-white" />
          </div>
          
          <div className="relative z-10 flex flex-col items-start gap-4">
            <div className="flex items-center justify-between w-full">
              <div className="flex items-center gap-3">
                {!isEditing ? (
                  <span className="inline-flex items-center gap-1.5 px-3 py-1 bg-white rounded-full text-xs font-bold text-[#004c91] shadow-sm">
                    <Info className="w-3.5 h-3.5" />
                    {faq.type}
                  </span>
                ) : (
                  <select 
                    value={editingFaq.type}
                    onChange={(e) => setEditingFaq({...editingFaq, type: e.target.value})}
                    className="px-3 py-1 bg-white rounded-full text-xs font-bold text-[#004c91] shadow-sm outline-none border-none cursor-pointer"
                  >
                    <option value="Chương trình">Chương trình</option>
                    <option value="Học phí">Học phí</option>
                    <option value="Visa">Visa</option>
                    <option value="Ký túc xá">Ký túc xá</option>
                  </select>
                )}
                {getStatusBadge(faq.status)}
              </div>
              {!isEditing && isFullAccess && (
                <button 
                  onClick={() => {
                    setEditingFaq(faq);
                    setIsEditing(true);
                  }}
                  className="p-2 text-white/90 hover:text-white bg-transparent border border-white/30 hover:bg-white/10 rounded-xl transition-all cursor-pointer flex items-center justify-center"
                  title="Chỉnh sửa"
                >
                  <Edit2 className="w-[20px] h-[20px]" />
                </button>
              )}
            </div>
            
            {isEditing ? (
              <textarea 
                value={editingFaq.question}
                onChange={(e) => setEditingFaq({...editingFaq, question: e.target.value})}
                className="w-full text-2xl md:text-3xl font-bold text-white bg-transparent border border-white/30 focus:border-white focus:bg-white/10 p-3 rounded-2xl outline-none transition-all resize-none mt-2 placeholder:text-white/50"
                rows={2}
                placeholder="Nhập câu hỏi..."
              />
            ) : (
              <h2 className="text-2xl md:text-3xl font-bold text-white leading-snug mt-2">
                {faq.question}
              </h2>
            )}
          </div>
        </div>

        {/* Content Section (Answer) */}
        <div className="p-4 sm:p-6 md:p-8 md:p-10">
          <h3 className="text-xs font-bold tracking-widest text-[#f37021] uppercase mb-4 flex items-center gap-2">
             <MessageCircle className="w-4 h-4" /> 
             Câu trả lời
          </h3>
          <div className="bg-[#e6eff7] rounded-2xl p-6 md:p-8 border border-blue-100/50 min-h-[250px]">
            {isEditing ? (
              <textarea 
                value={editingFaq.answer}
                onChange={(e) => setEditingFaq({...editingFaq, answer: e.target.value})}
                className="w-full text-gray-900 leading-relaxed text-[15px] p-4 bg-white/80 focus:bg-white border border-blue-200 rounded-xl outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all resize-none min-h-[200px]"
                placeholder="Nhập câu trả lời..."
              />
            ) : (
              <p className="text-gray-700 leading-relaxed text-[15px] whitespace-pre-line font-medium">
                {faq.answer}
              </p>
            )}
          </div>
          
          {/* Actions */}
          <div className="mt-10 flex items-center justify-end gap-3 pt-6 border-t border-gray-100">
            {!isEditing ? (
              <button 
                onClick={() => navigate('/dashboard/faq')}
                className="flex items-center gap-2 px-6 py-2.5 bg-white border border-gray-200 text-gray-700 font-bold rounded-xl hover:bg-gray-50 hover:text-[#004c91] transition-colors shadow-sm cursor-pointer"
              >
                <ChevronLeft className="w-4 h-4" />
                <span>Quay lại</span>
              </button>
            ) : (
              <>
                <button 
                  onClick={() => setIsEditing(false)}
                  className="flex items-center gap-2 px-6 py-2.5 bg-white border border-gray-200 text-gray-700 font-bold rounded-xl hover:bg-gray-50 transition-colors shadow-sm cursor-pointer"
                >
                  <X className="w-4 h-4" />
                  <span>Hủy</span>
                </button>
                <button 
                  onClick={() => {
                    setFaq(editingFaq);
                    setIsEditing(false);
                  }}
                  className="flex items-center gap-2 px-6 py-2.5 bg-[#004c91] text-white font-bold rounded-xl hover:bg-[#003366] transition-colors shadow-sm cursor-pointer"
                >
                  <Save className="w-4 h-4" />
                  <span>Lưu thay đổi</span>
                </button>
              </>
            )}
          </div>
        </div>
      </div>
    </motion.div>
  );
}
