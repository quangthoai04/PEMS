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

  return (
    <div className="py-5">
      {!isStudent && (
        <>
          <div className="flex items-center gap-3 mb-4">
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
          <div className="space-y-4">
            {data.items.length === 0 ? (
              <p className="text-sm font-semibold text-slate-400">Chưa có file media nào được tải lên.</p>
            ) : (
              <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-4">
                {data.items.map(m => (
                  <div key={m.mediaId} className="relative group rounded-xl overflow-hidden border border-slate-200 aspect-square">
                    {m.fileType.startsWith('image/') ? (
                      <img src={m.thumbnailUrl || m.url} alt={m.fileName} className="w-full h-full object-cover" />
                    ) : (
                      <div className="w-full h-full bg-slate-100 flex items-center justify-center text-slate-400 font-bold text-xs p-2 text-center break-all">
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
        <div className={!isStudent ? "pt-4 mt-4 border-t border-slate-100" : ""}>
          <div className="flex items-center gap-2 mb-3">
            <Camera className="w-4 h-4 text-[#f37021]" />
            <h3 className="text-sm font-black text-[#004c91]">Ảnh đoàn khách</h3>
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
