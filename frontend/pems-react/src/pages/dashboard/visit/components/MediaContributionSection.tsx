import React, { useRef, useState } from 'react';
import { MediaContributionStatus } from '../../../../features/delegations/types/delegations.types';
import { Camera, Image as ImageIcon, UploadCloud } from 'lucide-react';
import httpClient from '../../../../shared/api/httpClient';
import { toast } from 'react-hot-toast';
import { VisitPhotoPanel } from '../../../../features/delegations/components/VisitPhotoPanel';

interface Props {
  visitInstanceId: string;
  data: MediaContributionStatus;
  canView: boolean;
  instanceStatus: string;
  onChanged: () => void;
  relation?: string;
  isReadOnly?: boolean;
}

export function MediaContributionSection({ visitInstanceId, data, canView, instanceStatus, onChanged, relation, isReadOnly = false }: Props) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [loading, setLoading] = useState(false);
  // Khối "Ảnh đoàn khách" chỉ dành cho Student ACCEPTED của instance — backend trả 403 cho người
  // khác; khi đó ẩn khối con này (phần media cũ giữ nguyên).
  const [showStudentPhotos, setShowStudentPhotos] = useState(true);

  const handleUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!e.target.files || e.target.files.length === 0) return;
    const files = Array.from(e.target.files) as File[];

    // In Phase 3, we mock the call to the actual backend endpoint which might be a stub
    const formData = new FormData();
    files.forEach(f => formData.append('files', f));

    try {
      setLoading(true);
      // Giả định backend endpoint cho Media Upload của Visit
      await httpClient.post(`/delegations/visit-instances/${visitInstanceId}/media`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      toast.success('Tải lên Media thành công.');
      onChanged();
    } catch (err: any) {
      if (err.response?.status === 403) toast.error('Bạn không có quyền tải lên.');
      else if (err.response?.status === 404) toast.error('Tính năng đang được hoàn thiện trên server.');
      else toast.error('Lỗi khi tải lên Media.');
    } finally {
      setLoading(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  if (!canView) return null;

  const isStudent = relation === 'STUDENT_RELATED';
  const mediaStatusLabel = data.isRequirementSatisfied
    ? 'Đạt yêu cầu'
    : `${data.uploadedCount} / tối thiểu ${data.requiredMinimumCount} file`;

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4">
      {!isStudent && (
        <>
          <div className="flex items-start justify-between gap-3">
            <div className="flex items-center gap-2.5 min-w-0">
              <span className="w-8 h-8 rounded-lg bg-orange-50 text-[#f37021] flex items-center justify-center shrink-0">
                <ImageIcon className="w-4 h-4" />
              </span>
              <h2 className="text-sm sm:text-base font-semibold text-[#004c91]">Ảnh / Media</h2>
            </div>
            <span className={
              data.isRequirementSatisfied
                ? 'inline-flex px-2.5 py-0.5 rounded-full border border-emerald-200 bg-emerald-50 text-emerald-700 text-xs font-bold shrink-0'
                : 'text-xs font-bold text-slate-500 shrink-0 pt-1'
            }>
              {mediaStatusLabel}
            </span>
          </div>
          <div className="mt-3 space-y-3">
            {data.items.length === 0 ? (
              <p className="text-sm font-normal text-slate-400">Chưa có file media nào được tải lên.</p>
            ) : (
              <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-4">
                {data.items.map(m => (
                  <div key={m.mediaId} className="relative group rounded-xl overflow-hidden border border-slate-200 aspect-square">
                    {m.fileType.startsWith('image/') ? (
                      <img src={m.thumbnailUrl || m.url} alt={m.fileName} className="w-full h-full object-cover" />
                    ) : (
                      <div className="w-full h-full bg-slate-100 flex items-center justify-center text-slate-400 font-normal text-xs p-2 text-center break-all">
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

            {data.canCurrentUserUpload && !isReadOnly && (
              <div className="pt-2 relative">
                <input
                  type="file"
                  multiple
                  className="hidden"
                  ref={fileInputRef}
                  onChange={handleUpload}
                  accept="image/*,video/*"
                />
                <button
                  disabled={loading}
                  onClick={() => fileInputRef.current?.click()}
                  className="inline-flex items-center gap-2 px-4 py-2 bg-blue-50 text-[#004c91] hover:bg-blue-100 rounded-lg text-sm font-bold transition-colors disabled:opacity-50"
                >
                  <UploadCloud className="w-4 h-4" />
                  {loading ? 'Đang tải lên...' : 'Tải lên Ảnh / Video'}
                </button>
              </div>
            )}
          </div>
        </>
      )}

      {/* Ảnh đoàn khách (Student) — lưu Drive VR-{request}/{campus}, bảng visit_photos */}
      {showStudentPhotos && (
        <div className={!isStudent ? "pt-3 mt-3 border-t border-slate-100" : ""}>
          <div className="flex items-center gap-2.5 mb-3">
            {isStudent ? (
              <span className="w-8 h-8 rounded-lg bg-orange-50 text-[#f37021] flex items-center justify-center shrink-0">
                <Camera className="w-4 h-4" />
              </span>
            ) : (
              <Camera className="w-4 h-4 text-[#f37021]" />
            )}
            <h3 className={isStudent ? "text-sm sm:text-base font-semibold text-[#004c91]" : "text-sm font-bold text-[#004c91]"}>Ảnh đoàn khách</h3>
          </div>
          <VisitPhotoPanel
            visitInstanceId={visitInstanceId}
            mode={isReadOnly ? 'view' : 'edit'}
            columns={6}
            maxInitialItems={18}
            onForbidden={() => setShowStudentPhotos(false)}
          />
        </div>
      )}
    </div>
  );
}
