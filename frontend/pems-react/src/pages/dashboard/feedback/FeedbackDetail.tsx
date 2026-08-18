/**
 * Trang FeedbackDetail
 * Chi tiết đánh giá đoàn khách
 */

import React, { useEffect, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ChevronLeft } from 'lucide-react';
import { useAuth } from '../../../shared/hooks/useAuth';
import { useFeedbacks } from '../../../features/feedbacks/hooks/useFeedbacks';
import { FeedbackSummaryCompact } from '../../../features/feedbacks/components/FeedbackSummaryCompact';
import { FeedbackTypeSection } from '../../../features/feedbacks/components/FeedbackTypeSection';
import { FEEDBACK_TYPES } from '../../../features/feedbacks/constants/feedbackTypes';

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

  const groups = useMemo(() => {
    const items = rawFeedbacks?.items || [];
    return {
      hostDelegationOverall: items.filter(fb => fb.feedbackType === FEEDBACK_TYPES.HOST_DELEGATION_OVERALL),
      visitorOverall: items.filter(fb => fb.feedbackType === FEEDBACK_TYPES.VISITOR_OVERALL),
      hostParticipant: items.filter(fb => fb.feedbackType === FEEDBACK_TYPES.HOST_PARTICIPANT),
      hostLogistics: items.filter(fb => fb.feedbackType === FEEDBACK_TYPES.HOST_LOGISTICS),
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

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto pb-12 animate-in fade-in duration-500 font-sans">
      <div className="flex items-center gap-4 mb-6">
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
          <p className="text-gray-500 mt-1 font-normal">{visitTitle} (REQ #{id})</p>
        </div>
      </div>

      {stats && (
        <FeedbackSummaryCompact
          totalFeedbacks={stats.total}
          avgRating={stats.avg}
          lowRating={stats.low}
          latest={stats.latest}
        />
      )}

      {/* Feedback groups: trái = Host đánh giá, phải = Khách đánh giá */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-x-8 gap-y-6 mt-2">
        <div className="space-y-5">
          <FeedbackTypeSection title="Host đánh giá đoàn khách" items={groups.hostDelegationOverall} />
          {groups.hostParticipant.length > 0 && (
            <FeedbackTypeSection title="Host đánh giá bên tham gia" items={groups.hostParticipant} />
          )}
          {groups.hostLogistics.length > 0 && (
            <FeedbackTypeSection title="Host đánh giá hậu cần / đồ mượn" items={groups.hostLogistics} />
          )}
        </div>

        <div className="md:border-l md:border-slate-200 md:pl-8">
          <FeedbackTypeSection title="Khách đánh giá" items={groups.visitorOverall} />
        </div>
      </div>
    </div>
  );
}
