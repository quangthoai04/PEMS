/**
 * Trang FeedbackDetail
 * Chi tiết đánh giá đoàn khách
 */

import React, { useEffect, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ChevronLeft, Star, Users, MessageSquare } from 'lucide-react';
import { useAuth } from '../../../shared/hooks/useAuth';
import { useFeedbacks } from '../../../features/feedbacks/hooks/useFeedbacks';

export function FeedbackDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { user } = useAuth();
  const { rawFeedbacks, loading, fetchRawFeedbacks } = useFeedbacks();

  useEffect(() => {
    if (id) {
      fetchRawFeedbacks({ visitRequestId: Number(id), pageSize: 100 });
    }
  }, [id, fetchRawFeedbacks]);

  const stats = useMemo(() => {
    if (!rawFeedbacks?.items?.length) return null;
    let total = rawFeedbacks.items.length;
    let sum = 0;
    let low = 0;
    let latest = '';
    rawFeedbacks.items.forEach(fb => {
      sum += fb.rating;
      if (fb.rating <= 2) low++;
      if (!latest || new Date(fb.submittedAt) > new Date(latest)) latest = fb.submittedAt;
    });
    return {
      avg: sum / total,
      total,
      low,
      latest
    };
  }, [rawFeedbacks]);

  const visitTitle = rawFeedbacks?.items?.[0]?.visitTitle || 'Đoàn khách';

  if (loading) {
     return <div className="p-8 text-center text-slate-500">Đang tải dữ liệu...</div>;
  }

  if (!rawFeedbacks?.items?.length) {
    return (
      <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto pb-12 flex flex-col items-center justify-center min-h-[60vh]">
        <h2 className="text-2xl font-bold text-slate-700">Không tìm thấy dữ liệu hoặc đoàn không có đánh giá</h2>
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
  
  // Format date
  const formatDate = (dateStr: string) => {
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    } catch {
      return dateStr;
    }
  };

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto pb-12 animate-in fade-in duration-500 font-sans">
      <div className="flex items-center gap-4 mb-8">
        <button 
          onClick={() => navigate('/dashboard/feedback')}
          className="w-10 h-10 flex items-center justify-center rounded-xl border border-slate-200 bg-white text-slate-500 hover:text-[#004c91] hover:border-blue-200 hover:bg-blue-50 transition-colors cursor-pointer shadow-sm"
        >
          <ChevronLeft className="w-5 h-5" />
        </button>
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-bold text-[#004c91] tracking-tight">Chi tiết đánh giá đoàn khách</h1>
            {user?.roleCode === 'STAFF' && user?.campusName && (
               <span className="px-2.5 py-1 bg-blue-100 text-blue-700 text-xs font-bold rounded-lg border border-blue-200">
                 Campus: {user.campusName}
               </span>
            )}
          </div>
          <p className="text-gray-500 mt-1 font-medium">{visitTitle} (REQ #{id})</p>
        </div>
      </div>

      {stats && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8 w-full">
           <div className="bg-[#004c91] text-white p-6 rounded-2xl shadow-md flex flex-col justify-center relative overflow-hidden group">
              <p className="text-sm font-medium text-blue-100 uppercase tracking-wider mb-2 relative z-10">Trung bình đánh giá</p>
              <div className="flex items-center gap-3 relative z-10">
                 <p className="text-4xl font-black">{stats.avg.toFixed(1)}</p>
                 <Star className="w-6 h-6 fill-yellow-400 text-yellow-400" />
              </div>
           </div>
           <div className="bg-gradient-to-br from-[#f37021] to-[#e85c0d] text-white p-6 rounded-2xl shadow-md flex flex-col justify-center relative overflow-hidden group">
              <p className="text-sm font-medium text-orange-100 uppercase tracking-wider mb-2 relative z-10">Số lượng phản hồi</p>
              <p className="text-4xl font-black relative z-10">{stats.total} <span className="text-lg font-medium">lượt</span></p>
           </div>
           <div className="bg-white border border-slate-200 p-6 rounded-2xl shadow-sm flex flex-col justify-center">
              <p className="text-sm font-bold text-slate-500 uppercase tracking-wider mb-2">Cảnh báo (1-2★)</p>
              <p className={`text-4xl font-black ${stats.low > 0 ? 'text-red-500' : 'text-slate-700'}`}>{stats.low}</p>
           </div>
           <div className="bg-white border border-slate-200 p-6 rounded-2xl shadow-sm flex flex-col justify-center">
              <p className="text-sm font-bold text-slate-500 uppercase tracking-wider mb-2">Feedback mới nhất</p>
              <p className="text-xl font-bold text-slate-700">{formatDate(stats.latest)}</p>
           </div>
        </div>
      )}

      {/* Feedback List */}
      <div className="space-y-6">
         <h3 className="text-xl font-bold text-slate-800 flex items-center gap-2">
            <MessageSquare className="w-5 h-5 text-[#004c91]" />
            Danh sách đánh giá cá nhân
         </h3>
         
         <div className="grid grid-cols-1 md:grid-cols-2 gap-6 w-full">
            {rawFeedbacks.items.map((fb) => (
               <div key={fb.feedbackId} className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden flex flex-col">
                  <div className="p-5 border-b border-slate-100 bg-slate-50 flex justify-between items-start">
                     <div>
                        <p className="font-bold text-slate-800 text-lg mb-1">{fb.submitterNameSnapshot}</p>
                        <div className="flex items-center flex-wrap gap-2 mb-1">
                           <span className="px-2 py-0.5 bg-blue-100 text-blue-700 text-[10px] rounded font-bold uppercase">{fb.submitterRole}</span>
                           <span className="text-slate-500 text-xs font-medium">đánh giá</span>
                           <span className="px-2 py-0.5 bg-slate-200 text-slate-700 text-[10px] rounded font-bold uppercase">{fb.targetRole}</span>
                           <span className="font-bold text-slate-700 text-sm">{fb.targetNameSnapshot}</span>
                        </div>
                        <p className="text-xs text-slate-400 font-medium">{formatDate(fb.submittedAt)}</p>
                     </div>
                     <div className="flex gap-0.5 bg-white px-2 py-1.5 rounded-lg border border-slate-200 shadow-sm">
                        {renderStars(fb.rating)}
                     </div>
                  </div>
                  
                  <div className="p-6 flex-1 flex flex-col">
                     {fb.commentPreview ? (
                        <div className="flex-1">
                           <p className="text-[11px] font-bold text-slate-400 uppercase tracking-wide mb-2">Nội dung góp ý</p>
                           <div className="bg-slate-50 p-4 rounded-xl border border-slate-100 h-full">
                              <p className="text-sm text-slate-600 leading-relaxed italic">"{fb.commentPreview}"</p>
                           </div>
                        </div>
                     ) : (
                         <div className="flex-1 flex items-center justify-center p-4">
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
