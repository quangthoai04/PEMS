import React from 'react';
import { NewsContributionStatus } from '../../../../features/delegations/types/delegations.types';
import { Newspaper, Lock, Edit3, PlusCircle } from 'lucide-react';

interface Props {
  visitInstanceId: string;
  data: NewsContributionStatus;
  canView: boolean;
  instanceStatus: string;
  onChanged: () => void;
}

export function NewsContributionSection({ visitInstanceId, data, canView, instanceStatus, onChanged }: Props) {
  if (!canView) return null;

  return (
    <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
      <div className="flex items-center gap-3 px-5 py-4 border-b border-slate-100">
        <div className="w-9 h-9 rounded-xl bg-orange-50 text-[#f37021] flex items-center justify-center shrink-0">
          <Newspaper className="w-5 h-5" />
        </div>
        <div className="flex-1">
          <h2 className="text-base font-black text-[#004c91]">Tin tức</h2>
          <p className="text-xs font-semibold text-slate-500">
            {data.hasNews ? data.status : (data.newsNotRequired ? 'Không yêu cầu' : 'Chưa tạo')}
          </p>
        </div>
      </div>
      <div className="p-5 space-y-4">
        {data.newsNotRequired && (
          <p className="text-sm font-semibold text-slate-500 italic">
            Chuyến thăm này không yêu cầu bài tin tức (theo xác nhận của Host).
          </p>
        )}

        {!data.mediaConsentAllowed && (
          <p className="text-sm font-semibold text-rose-500 italic">
            Khách không đồng ý truyền thông, không thể tạo bài tin.
          </p>
        )}

        {data.hasNews && (
          <div className="bg-slate-50 rounded-xl p-4 border border-slate-200">
            <h4 className="font-bold text-slate-800">{data.title || 'Không có tiêu đề'}</h4>
            {data.description && <p className="text-sm text-slate-600 mt-1">{data.description}</p>}
            <p className="text-xs text-slate-400 font-semibold mt-3">
              Tạo bởi: {data.createdByName || '—'}
            </p>
            {data.rejectionReason && (
              <p className="text-xs text-red-600 font-bold mt-2">
                Lý do từ chối: {data.rejectionReason}
              </p>
            )}
          </div>
        )}

        {data.canCurrentUserCreate && !data.hasNews && !data.newsNotRequired && data.mediaConsentAllowed && (
          <div className="pt-2">
            <button
              onClick={() => alert('Sẽ gọi màn hình tạo tin tức')}
              className="inline-flex items-center gap-2 px-4 py-2 bg-blue-50 text-[#004c91] hover:bg-blue-100 rounded-lg text-sm font-bold transition-colors"
            >
              <PlusCircle className="w-4 h-4" />
              Tạo bài tin
            </button>
          </div>
        )}

        {data.canCurrentUserEdit && data.hasNews && data.status !== 'APPROVED' && (
          <div className="pt-2">
            <button
              onClick={() => alert('Sẽ gọi màn hình sửa tin tức')}
              className="inline-flex items-center gap-2 px-4 py-2 bg-blue-50 text-[#004c91] hover:bg-blue-100 rounded-lg text-sm font-bold transition-colors"
            >
              <Edit3 className="w-4 h-4" />
              Sửa bài tin
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
