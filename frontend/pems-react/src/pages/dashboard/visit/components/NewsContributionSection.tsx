import React, { useState } from 'react';
import { NewsContributionStatus } from '../../../../features/delegations/types/delegations.types';
import { Newspaper, Edit3, PlusCircle, Send } from 'lucide-react';
import httpClient from '../../../../shared/api/httpClient';
import { toast } from 'react-hot-toast';

interface Props {
  visitInstanceId: string;
  data: NewsContributionStatus;
  canView: boolean;
  instanceStatus: string;
  onChanged: () => void;
}

export function NewsContributionSection({ visitInstanceId, data, canView, instanceStatus, onChanged }: Props) {
  const [loading, setLoading] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const [modalMode, setModalMode] = useState<'CREATE' | 'EDIT'>('CREATE');
  const [formData, setFormData] = useState({ title: '', summary: '', body: '' });

  const openCreateModal = () => {
    setModalMode('CREATE');
    setFormData({ title: '', summary: '', body: '' });
    setShowModal(true);
  };

  const openEditModal = () => {
    setModalMode('EDIT');
    setFormData({ title: data.title || '', summary: data.description || '', body: '' });
    setShowModal(true);
  };

  const handleSaveModal = async () => {
    if (!formData.title.trim()) {
      toast.error('Vui lòng nhập tiêu đề bài tin.');
      return;
    }
    if (!formData.body.trim()) {
      toast.error('Vui lòng nhập nội dung bài tin.');
      return;
    }
    try {
      setLoading(true);
      if (modalMode === 'CREATE') {
        await httpClient.post(`/news/visit-instances/${visitInstanceId}`, formData);
        toast.success('Tạo bài tin thành công.');
      } else {
        await httpClient.put(`/news/visit-instance-news/${data.newsId}`, { ...formData, rowVersion: 0 });
        toast.success('Cập nhật bài tin thành công.');
      }
      setShowModal(false);
      onChanged();
    } catch (e: any) {
      if (e.response?.status === 403) toast.error('Bạn không có quyền thực hiện.');
      else if (e.response?.status === 409) toast.error('Lỗi xung đột dữ liệu (409).');
      else toast.error('Lỗi khi lưu bài tin.');
    } finally {
      setLoading(false);
    }
  };

  const handleSubmitReview = async () => {
    if (!data.newsId) return;
    try {
      setLoading(true);
      await httpClient.post(`/news/visit-instance-news/${data.newsId}/submit-review`);
      toast.success('Đã gửi bài tin đi duyệt.');
      onChanged();
    } catch (e: any) {
      if (e.response?.status === 403) toast.error('Bạn không có quyền thực hiện.');
      else toast.error('Lỗi khi gửi duyệt.');
    } finally {
      setLoading(false);
    }
  };

  if (!canView) return null;

  return (
    <div className="py-5">
      <div className="flex items-center gap-3 mb-4">
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
      <div className="space-y-4">
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
              disabled={loading}
              onClick={openCreateModal}
              className="inline-flex items-center gap-2 px-4 py-2 bg-blue-50 text-[#004c91] hover:bg-blue-100 rounded-lg text-sm font-bold transition-colors disabled:opacity-50"
            >
              <PlusCircle className="w-4 h-4" />
              Tạo bài tin
            </button>
          </div>
        )}

        {data.canCurrentUserEdit && data.hasNews && data.status !== 'APPROVED' && (
          <div className="pt-2 flex gap-2">
            <button
              disabled={loading}
              onClick={openEditModal}
              className="inline-flex items-center gap-2 px-4 py-2 bg-blue-50 text-[#004c91] hover:bg-blue-100 rounded-lg text-sm font-bold transition-colors disabled:opacity-50"
            >
              <Edit3 className="w-4 h-4" />
              Sửa bài tin
            </button>
            <button
              disabled={loading}
              onClick={handleSubmitReview}
              className="inline-flex items-center gap-2 px-4 py-2 bg-indigo-50 text-indigo-700 hover:bg-indigo-100 rounded-lg text-sm font-bold transition-colors disabled:opacity-50"
            >
              <Send className="w-4 h-4" />
              Gửi duyệt
            </button>
          </div>
        )}
      </div>

      {/* MODAL NHẬP TIN TỨC */}
      {showModal && (
        <div className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-2xl overflow-hidden flex flex-col max-h-full">
            <div className="px-6 py-4 border-b border-slate-100 flex items-center justify-between bg-slate-50">
              <h3 className="text-lg font-black text-[#004c91]">
                {modalMode === 'CREATE' ? 'Tạo bài tin tức' : 'Chỉnh sửa bài tin tức'}
              </h3>
              <button
                onClick={() => setShowModal(false)}
                className="text-slate-400 hover:text-slate-600 font-bold px-3 py-1 bg-slate-200/50 hover:bg-slate-200 rounded-lg transition-colors"
              >
                Đóng
              </button>
            </div>
            <div className="p-6 overflow-y-auto space-y-4">
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">Tiêu đề <span className="text-red-500">*</span></label>
                <input
                  type="text"
                  value={formData.title}
                  onChange={(e) => setFormData({ ...formData, title: e.target.value })}
                  placeholder="Nhập tiêu đề..."
                  className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-4 focus:ring-blue-50 transition-all font-semibold outline-none"
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">Mô tả / Tóm tắt</label>
                <textarea
                  value={formData.summary}
                  onChange={(e) => setFormData({ ...formData, summary: e.target.value })}
                  placeholder="Nhập mô tả..."
                  rows={2}
                  className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-4 focus:ring-blue-50 transition-all text-sm outline-none resize-none"
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">Nội dung <span className="text-red-500">*</span></label>
                <textarea
                  value={formData.body}
                  onChange={(e) => setFormData({ ...formData, body: e.target.value })}
                  placeholder="Nhập nội dung bài tin..."
                  rows={8}
                  className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-4 focus:ring-blue-50 transition-all text-sm outline-none resize-none"
                />
              </div>
            </div>
            <div className="px-6 py-4 border-t border-slate-100 flex items-center justify-end gap-3 bg-slate-50">
              <button
                onClick={() => setShowModal(false)}
                className="px-5 py-2.5 rounded-xl font-bold text-slate-600 hover:bg-slate-200 transition-colors"
              >
                Hủy
              </button>
              <button
                disabled={loading}
                onClick={handleSaveModal}
                className="px-5 py-2.5 rounded-xl font-bold bg-[#004c91] text-white hover:bg-blue-800 transition-colors disabled:opacity-50 flex items-center gap-2"
              >
                {loading ? 'Đang lưu...' : 'Lưu nháp'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
