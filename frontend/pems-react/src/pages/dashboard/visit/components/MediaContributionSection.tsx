import React from 'react';
import { MediaContributionStatus } from '../../../../features/delegations/types/delegations.types';
import { Image as ImageIcon, UploadCloud } from 'lucide-react';

interface Props {
  visitInstanceId: string;
  data: MediaContributionStatus;
  canView: boolean;
  instanceStatus: string;
  onChanged: () => void;
}

export function MediaContributionSection({ visitInstanceId, data, canView, instanceStatus, onChanged }: Props) {
  if (!canView) return null;

  return (
    <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
      <div className="flex items-center gap-3 px-5 py-4 border-b border-slate-100">
        <div className="w-9 h-9 rounded-xl bg-orange-50 text-[#f37021] flex items-center justify-center shrink-0">
          <ImageIcon className="w-5 h-5" />
        </div>
        <div className="flex-1">
          <h2 className="text-base font-black text-[#004c91]">Ảnh / Media</h2>
          <p className="text-xs font-semibold text-slate-500">
            {data.uploadedCount} / tối thiểu {data.requiredMinimumCount} file
          </p>
        </div>
        {data.isRequirementSatisfied && (
          <span className="inline-flex px-2.5 py-1 rounded-full border border-emerald-200 bg-emerald-50 text-emerald-700 text-[11px] font-bold">
            Đạt yêu cầu
          </span>
        )}
      </div>
      <div className="p-5 space-y-4">
        {data.items.length === 0 ? (
          <p className="text-sm font-semibold text-slate-400">Chưa có file media nào được tải lên.</p>
        ) : (
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-4">
            {data.items.map(m => (
              <div key={m.mediaId} className="relative group rounded-xl overflow-hidden border border-slate-200 aspect-square">
                {m.fileType.startsWith('image/') ? (
                  <img src={m.thumbnailUrl || m.url} alt={m.fileName} className="w-full h-full object-cover" />
                ) : (
                  <div className="w-full h-full bg-slate-100 flex items-center justify-center text-slate-400 font-bold text-xs p-2 text-center">
                    {m.fileName}
                  </div>
                )}
                <div className="absolute inset-0 bg-black/60 opacity-0 group-hover:opacity-100 transition-opacity flex flex-col items-center justify-center gap-2">
                  <a href={m.url} target="_blank" rel="noreferrer" className="text-white text-xs font-bold hover:underline">Xem file</a>
                </div>
              </div>
            ))}
          </div>
        )}

        {data.canCurrentUserUpload && (
          <div className="pt-2">
            <button
              onClick={() => alert('Sẽ gọi màn hình upload media')}
              className="inline-flex items-center gap-2 px-4 py-2 bg-blue-50 text-[#004c91] hover:bg-blue-100 rounded-lg text-sm font-bold transition-colors"
            >
              <UploadCloud className="w-4 h-4" />
              Tải lên Ảnh / Video
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
