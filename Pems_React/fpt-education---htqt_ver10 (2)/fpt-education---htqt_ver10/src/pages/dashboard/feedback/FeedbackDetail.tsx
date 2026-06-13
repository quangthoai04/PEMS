/**
 * Trang FeedbackDetail
 * Nhật ký khiếu nại đánh giá chi tiết theo dõi báo cáo sau tham quan.
 */

import React from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ChevronLeft, Star, Users, MessageSquare } from 'lucide-react';
import { MOCK_VISIT_FEEDBACKS } from './mockData';

export function FeedbackDetail() {
  const { id } = useParams();
  const navigate = useNavigate();

  const visit = MOCK_VISIT_FEEDBACKS.find(v => v.id === Number(id));

  if (!visit) {
    return (
      <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto pb-12 flex flex-col items-center justify-center min-h-[60vh]">
        <h2 className="text-2xl font-bold text-slate-700">Không tìm thấy đoàn khách</h2>
        <button 
          onClick={() => navigate('/dashboard/feedback')}
          className="mt-4 px-6 py-2 bg-[#004c91] text-white rounded-xl font-bold hover:bg-[#00386b] transition-colors"
        >
          Quay lại danh sách
        </button>
      </div>
    );
  }

  const renderStars = (rating: number) => {
    return Array.from({ length: 5 }).map((_, i) => (
      <Star key={i} className={`w-4 h-4 ${i < rating ? 'fill-yellow-400 text-yellow-400' : 'fill-slate-100 text-slate-200'}`} />
    ));
  };

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto pb-12 animate-in fade-in duration-500 font-sans">
      {/* Breadcrumbs */}
      <div className="flex items-center gap-2 text-sm font-medium text-slate-500 mb-6">
        <span className="cursor-pointer hover:text-[#004c91]" onClick={() => navigate('/dashboard')}>Dashboard</span>
        <span>/</span>
        <span className="cursor-pointer hover:text-[#004c91]" onClick={() => navigate('/dashboard/feedback')}>Quản lý feedback</span>
        <span>/</span>
        <span className="text-[#004c91] font-bold">Chi tiết đánh giá</span>
      </div>

      <div className="flex items-center gap-4 mb-8">
        <button 
          onClick={() => navigate('/dashboard/feedback')}
          className="w-10 h-10 flex items-center justify-center rounded-xl border border-slate-200 bg-white text-slate-500 hover:text-[#004c91] hover:border-blue-200 hover:bg-blue-50 transition-colors cursor-pointer shadow-sm"
        >
          <ChevronLeft className="w-5 h-5" />
        </button>
        <div>
          <h1 className="text-3xl font-bold text-[#004c91] tracking-tight">Chi tiết đánh giá đoàn khách</h1>
          <p className="text-gray-500 mt-1 font-medium">{visit.guestName}</p>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-2 gap-6 mb-8 w-full">
         <div className="bg-[#004c91] text-white p-6 rounded-2xl shadow-md flex items-start flex-col relative overflow-hidden group hover:shadow-lg transition-all duration-300">
            <div className="absolute -right-4 -bottom-4 p-4 opacity-[0.07] group-hover:scale-110 transition-transform duration-500">
               <Star className="w-32 h-32" />
            </div>
            <div className="w-10 h-10 rounded-xl bg-white/10 flex items-center justify-center backdrop-blur-md border border-white/10 z-10 mb-4">
               <Star className="w-5 h-5 fill-yellow-400 text-yellow-400" />
            </div>
            <div className="z-10 w-full">
               <p className="text-sm font-medium text-blue-100 uppercase tracking-wider mb-2">Trung bình đánh giá</p>
               <div className="flex items-center gap-3">
                  <p className="text-4xl font-black">{visit.averageRating.toFixed(1)}</p>
                  <div className="flex gap-1">
                     {Array.from({ length: 5 }).map((_, i) => (
                       <Star key={i} className={`w-4 h-4 ${i < Math.round(visit.averageRating) ? 'fill-yellow-400 text-yellow-400' : 'fill-white/20 text-white/20'}`} />
                     ))}
                  </div>
               </div>
            </div>
         </div>
         <div className="bg-gradient-to-br from-[#f37021] to-[#e85c0d] text-white p-6 rounded-2xl shadow-md flex items-start flex-col relative overflow-hidden group hover:shadow-lg transition-all duration-300">
            <div className="absolute -right-4 -bottom-4 p-4 opacity-[0.08] group-hover:scale-110 transition-transform duration-500">
               <Users className="w-32 h-32" />
            </div>
            <div className="w-10 h-10 rounded-xl bg-white/20 flex items-center justify-center backdrop-blur-md border border-white/20 z-10 mb-4">
               <Users className="w-5 h-5" />
            </div>
            <div className="z-10 w-full">
               <p className="text-sm font-medium text-orange-100 uppercase tracking-wider mb-2">Số lượng phản hồi</p>
               <p className="text-4xl font-black">{visit.feedbacks.length} <span className="text-lg text-orange-100 font-medium ml-1">người</span></p>
            </div>
         </div>
      </div>

      {/* Feedback List */}
      <div className="space-y-6">
         <h3 className="text-xl font-bold text-slate-800 flex items-center gap-2">
            <MessageSquare className="w-5 h-5 text-[#004c91]" />
            Danh sách đánh giá cá nhân
         </h3>
         
         <div className="flex flex-col gap-6 w-full">
            {visit.feedbacks.map((fb, index) => (
               <div key={fb.id} className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden flex flex-col min-h-[250px]">
                  <div className="p-5 border-b border-white/10 bg-[#004c91] flex justify-between items-start">
                     <div>
                        <p className="font-bold text-white text-lg">{fb.reviewer}</p>
                        <p className="text-sm text-blue-100 font-medium mt-0.5">{fb.date}</p>
                     </div>
                     <div className="flex gap-0.5 bg-white/10 backdrop-blur-md px-3 py-1.5 rounded-lg border border-white/20 shadow-sm">
                        {renderStars(fb.rating)}
                     </div>
                  </div>
                  
                  <div className="p-6 flex-1 flex flex-col gap-5">
                     <div className="grid grid-cols-2 gap-4">
                        <div className="space-y-1.5 p-3 rounded-xl bg-blue-50/30 border border-blue-50/50">
                           <p className="text-[11px] font-bold text-slate-500 uppercase tracking-wide">Không gian tham quan</p>
                           <div className="flex gap-0.5">{renderStars(fb.spaceRating)}</div>
                        </div>
                        <div className="space-y-1.5 p-3 rounded-xl bg-blue-50/30 border border-blue-50/50">
                           <p className="text-[11px] font-bold text-slate-500 uppercase tracking-wide">Chất lượng hỗ trợ</p>
                           <div className="flex gap-0.5">{renderStars(fb.supportRating)}</div>
                        </div>
                     </div>
                     
                     {fb.comment && (
                        <div className="mt-2 flex-1">
                           <p className="text-[11px] font-bold text-slate-400 uppercase tracking-wide mb-2">Góp ý thêm</p>
                           <div className="bg-slate-50 p-4 rounded-xl border border-slate-100 h-full">
                              <p className="text-sm text-slate-600 leading-relaxed italic">"{fb.comment}"</p>
                           </div>
                        </div>
                     )}
                     {!fb.comment && (
                         <div className="mt-2 flex-1 flex items-center justify-center p-4">
                            <p className="text-sm text-slate-400 italic">Không có góp ý thêm.</p>
                         </div>
                     )}
                  </div>
               </div>
            ))}
         </div>
      </div>
    </div>
  );
}
