import React, { useState } from 'react';
import { MinutesContributionStatus } from '../../../../features/delegations/types/delegations.types';
import { FileText, Lock, Edit3, Save, X } from 'lucide-react';
import { delegationsApi } from '../../../../features/delegations/api/delegationsApi';
import { toast } from 'react-hot-toast';

interface Props {
  visitInstanceId: string;
  data: MinutesContributionStatus;
  canView: boolean;
  instanceStatus: string;
  onChanged: () => void;
}

export function MinutesContributionSection({ visitInstanceId, data, canView, instanceStatus, onChanged }: Props) {
  const [isEditing, setIsEditing] = useState(false);
  const [content, setContent] = useState(data.content || '');
  const [loading, setLoading] = useState(false);
  const [lockToken, setLockToken] = useState<string | null>(null);
  const [minutesId, setMinutesId] = useState<number | null>(null);
  const [rowVersion, setRowVersion] = useState<number>(0);

  if (!canView) return null;

  const handleEdit = async () => {
    try {
      setLoading(true);
      const res = await delegationsApi.minutes.createOrLock(visitInstanceId, 'Biên bản chuyến thăm');
      setMinutesId(res.minutesId);
      setLockToken(res.editLockToken || null);
      setRowVersion(res.rowVersion);
      setContent(res.content || '');
      setIsEditing(true);
    } catch (e: any) {
      if (e.response?.status === 403) toast.error('Bạn không có quyền chỉnh sửa biên bản.');
      else if (e.response?.status === 409) toast.error('Biên bản đang được người khác chỉnh sửa.');
      else toast.error('Lỗi khi mở biên bản.');
    } finally {
      setLoading(false);
    }
  };

  const handleCancel = async () => {
    setIsEditing(false);
    if (minutesId && lockToken) {
      try {
        await delegationsApi.minutes.releaseLock(minutesId, lockToken);
      } catch (e) {
        // ignore
      }
    }
    onChanged();
  };

  const handleSave = async () => {
    if (!minutesId || !lockToken) return;
    try {
      setLoading(true);
      await delegationsApi.minutes.save(minutesId, {
        title: 'Biên bản chuyến thăm',
        content,
        editLockToken: lockToken,
        rowVersion,
        participants: [],
        actionItems: []
      });
      toast.success('Lưu biên bản thành công.');
      setIsEditing(false);
      onChanged();
    } catch (e: any) {
      if (e.response?.status === 403) toast.error('Bạn không có quyền lưu biên bản.');
      else if (e.response?.status === 409) toast.error('Xung đột phiên bản. Vui lòng tải lại.');
      else toast.error('Lỗi khi lưu biên bản.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
      <div className="flex items-center gap-3 px-5 py-4 border-b border-slate-100">
        <div className="w-9 h-9 rounded-xl bg-orange-50 text-[#f37021] flex items-center justify-center shrink-0">
          <FileText className="w-5 h-5" />
        </div>
        <div className="flex-1">
          <h2 className="text-base font-black text-[#004c91]">Biên bản</h2>
          <p className="text-xs font-semibold text-slate-500">
            {data.hasMinutes ? 'Đã khởi tạo' : 'Chưa có biên bản'}
          </p>
        </div>
        {data.status === 'COMPLETED' && (
          <span className="inline-flex px-2.5 py-1 rounded-full border border-emerald-200 bg-emerald-50 text-emerald-700 text-[11px] font-bold">
            Hoàn tất
          </span>
        )}
      </div>
      <div className="p-5 space-y-4">
        {isEditing ? (
          <div className="space-y-3">
            <textarea
              className="w-full min-h-[200px] border border-slate-200 rounded-xl p-3 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              value={content}
              onChange={e => setContent(e.target.value)}
              placeholder="Nhập nội dung biên bản..."
              disabled={loading}
            />
            <div className="flex gap-2">
              <button
                onClick={handleSave}
                disabled={loading}
                className="inline-flex items-center gap-2 px-4 py-2 bg-[#004c91] text-white hover:bg-[#003b70] rounded-lg text-sm font-bold transition-colors disabled:opacity-50"
              >
                <Save className="w-4 h-4" />
                Lưu biên bản
              </button>
              <button
                onClick={handleCancel}
                disabled={loading}
                className="inline-flex items-center gap-2 px-4 py-2 bg-slate-100 text-slate-700 hover:bg-slate-200 rounded-lg text-sm font-bold transition-colors disabled:opacity-50"
              >
                <X className="w-4 h-4" />
                Hủy bỏ
              </button>
            </div>
          </div>
        ) : (
          <>
            {data.content ? (
              <div className="prose prose-sm max-w-none text-slate-700" dangerouslySetInnerHTML={{ __html: data.content }} />
            ) : (
              <p className="text-sm font-semibold text-slate-400">Nội dung biên bản trống.</p>
            )}

            {data.lockedByName && (
              <div className="flex items-center gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-2.5 text-sm font-semibold text-amber-700">
                <Lock className="w-4 h-4 shrink-0" />
                Đang được chỉnh sửa bởi {data.lockedByName}
              </div>
            )}

            {data.canCurrentUserEdit && data.status !== 'COMPLETED' && (
              <div className="pt-2 flex flex-wrap gap-3">
                <button
                  onClick={handleEdit}
                  disabled={loading || !!data.lockedByName}
                  className="inline-flex items-center gap-2 px-4 py-2 bg-blue-50 text-[#004c91] hover:bg-blue-100 rounded-lg text-sm font-bold transition-colors disabled:opacity-50"
                >
                  <Edit3 className="w-4 h-4" />
                  {data.hasMinutes ? 'Chỉnh sửa biên bản' : 'Tạo biên bản'}
                </button>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
