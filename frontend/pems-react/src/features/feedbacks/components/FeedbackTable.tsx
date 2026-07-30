import React from 'react';
import { Eye, Star, MessageSquare } from 'lucide-react';
import { FeedbackVisitSummaryItem, PaginatedResult } from '../types/feedbacks.types';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';

interface Props {
  summaries: PaginatedResult<FeedbackVisitSummaryItem> | null;
  loading: boolean;
  currentPage: number;
  pageSize: number;
  onView: (visitRequestId: number, visitInstanceId?: number | null) => void;
}

const formatDate = (dateStr: string) => formatVietnamDateTime(dateStr, { fallback: dateStr });

export function FeedbackTable({ summaries, loading, currentPage, pageSize, onView }: Props) {
  return (
    <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden flex flex-col relative min-h-[300px]">
      {loading && (
        <div className="absolute inset-0 bg-white/60 backdrop-blur-sm z-10 flex items-center justify-center">
          <div className="w-8 h-8 border-4 border-[#004c91] border-t-transparent rounded-full animate-spin"></div>
        </div>
      )}

      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="bg-[#004c91] text-white border-b border-[#004c91]">
              <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap text-center text-white">STT</th>
              <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap text-white">Đoàn khách / Phạm vi</th>
              <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap text-center text-white">Điểm TB</th>
              <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap text-white">Feedback mới nhất</th>
              <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap text-center text-white">Cảnh báo</th>
              <th className="p-4 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap text-white">Hành động</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {summaries?.items?.length ? (
              summaries.items.map((item, index) => (
                <tr key={`${item.visitRequestId}-${item.visitInstanceId}`} className="hover:bg-blue-50/30 transition-colors">
                  <td className="p-4 font-bold text-slate-500 text-center whitespace-nowrap">
                    {(currentPage - 1) * pageSize + index + 1}
                  </td>
                  <td className="p-4">
                    <p className="font-bold text-[#004c91] mb-1 line-clamp-1">{item.visitTitle}</p>
                    <div className="flex items-center gap-2 text-xs font-medium text-slate-500">
                      <span className="bg-slate-100 px-2 py-0.5 rounded whitespace-nowrap">REQ #{item.visitRequestId}</span>
                      {item.visitInstanceId && <span className="bg-slate-100 px-2 py-0.5 rounded whitespace-nowrap">INST #{item.visitInstanceId}</span>}
                      {item.campusName && <span className="text-slate-400 whitespace-nowrap">• {item.campusName}</span>}
                    </div>
                  </td>
                  <td className="p-4 text-center whitespace-nowrap">
                    <div className="flex items-center justify-center gap-1">
                      <span className="font-bold text-slate-700">{item.averageRating.toFixed(1)}</span>
                      <Star className="w-4 h-4 fill-yellow-400 text-yellow-400" />
                    </div>
                  </td>
                  <td className="p-4">
                    {item.latestSubmittedAt ? (
                      <>
                        <div className="text-sm font-medium text-slate-700 whitespace-nowrap">{formatDate(item.latestSubmittedAt)}</div>
                        <div className="text-xs text-slate-500 mt-0.5 truncate max-w-[150px]">{item.latestSubmitterName}</div>
                      </>
                    ) : <span className="text-slate-400">-</span>}
                  </td>
                  <td className="p-4 text-center whitespace-nowrap">
                    {item.lowRatingCount > 0 ? (
                      <span className="px-2 py-1 bg-red-100 text-red-700 text-xs font-bold rounded-lg border border-red-200">
                        {item.lowRatingCount} cảnh báo
                      </span>
                    ) : <span className="text-slate-400 text-sm">-</span>}
                  </td>
                  <td className="p-4 whitespace-nowrap">
                    <div className="flex items-center justify-center gap-2">
                      <button
                        onClick={() => onView(item.visitRequestId, item.visitInstanceId)}
                        className="w-8 h-8 rounded-lg bg-slate-50 text-slate-400 hover:text-[#004c91] hover:bg-blue-50 flex items-center justify-center transition-colors border border-slate-200 shadow-sm"
                        title="Xem chi tiết đoàn"
                      >
                        <Eye className="w-4 h-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))
            ) : !loading ? (
              <tr>
                <td colSpan={6} className="px-6 py-16 text-center text-slate-500">
                  <MessageSquare className="w-12 h-12 text-slate-300 mx-auto mb-3" />
                  <p className="font-medium text-slate-600 mb-1">Không tìm thấy feedback nào</p>
                  <p className="text-sm text-slate-400">Vui lòng thử thay đổi bộ lọc hoặc tìm kiếm khác</p>
                </td>
              </tr>
            ) : (
              <tr>
                <td colSpan={6} className="px-6 py-16 text-center text-slate-500">
                  <p className="font-medium text-slate-600 mb-1">Đang tải dữ liệu...</p>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
