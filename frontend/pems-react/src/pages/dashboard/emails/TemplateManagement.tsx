import React, { useState, useEffect, useRef, useMemo } from 'react';
import { Search, Plus, Edit2, Check, X, ShieldAlert, Loader2 } from 'lucide-react';
import { emailsApi } from '../../../features/emails/api/emailsApi';
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';

const QUILL_MODULES = {
  toolbar: [
    ['bold', 'italic', 'underline', 'strike'],
    [{ list: 'ordered' }, { list: 'bullet' }],
    [{ align: [] }],
    ['clean']
  ]
};

export function TemplateManagement({ pushToast }: { pushToast: (type: 'success' | 'error', msg: string) => void }) {
  const [data, setData] = useState<any[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [formData, setFormData] = useState({
    templateCode: '',
    name: '',
    purpose: '',
    subject: '',
    content: '',
    status: 'ACTIVE'
  });
  const [submitting, setSubmitting] = useState(false);

  const fetchData = async () => {
    setIsLoading(true);
    try {
      const res = await emailsApi.getEmailTemplateList();
      setData(res.data.items || res.data.templates || []);
    } catch {
      pushToast('error', 'Không thể tải danh sách mẫu email');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  const handleEdit = async (id: number) => {
    try {
      const res = await emailsApi.getEmailTemplateDetail(id);
      const t = res.data;
      setFormData({
        templateCode: t.templateCode || '',
        name: t.name || '',
        purpose: t.purpose || t.description || '',
        subject: t.subjectVi || t.subject || '',
        content: t.bodyVi || t.content || '',
        status: t.status || 'ACTIVE'
      });
      setEditingId(t.emailTemplateId || id);
      setShowForm(true);
    } catch {
      pushToast('error', 'Không thể tải chi tiết mẫu email');
    }
  };

  const handleToggleStatus = async (id: number, currentStatus: string) => {
    try {
      const newStatus = currentStatus === 'ACTIVE' ? 'INACTIVE' : 'ACTIVE';
      await emailsApi.toggleEmailTemplateStatus(id, newStatus);
      pushToast('success', 'Đã cập nhật trạng thái thành công');
      fetchData();
    } catch {
      pushToast('error', 'Cập nhật trạng thái thất bại');
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.templateCode.trim() || !formData.name.trim() || !formData.subject.trim() || !formData.content.trim()) {
      pushToast('error', 'Vui lòng nhập đầy đủ các trường bắt buộc');
      return;
    }
    setSubmitting(true);
    try {
      const payload = {
        templateCode: formData.templateCode,
        name: formData.name,
        purpose: formData.purpose,
        description: formData.purpose,
        subjectVi: formData.subject,
        bodyVi: formData.content,
        subjectEn: formData.subject,
        bodyEn: formData.content,
        status: formData.status
      };
      if (editingId) {
        await emailsApi.updateEmailTemplate(editingId, payload);
        pushToast('success', 'Đã cập nhật mẫu email');
      } else {
        await emailsApi.createEmailTemplate(payload);
        pushToast('success', 'Đã tạo mẫu email mới');
      }
      setShowForm(false);
      fetchData();
    } catch (err: any) {
      pushToast('error', err.response?.data?.message || 'Có lỗi xảy ra khi lưu mẫu email');
    } finally {
      setSubmitting(false);
    }
  };

  const filteredData = data.filter(item => {
    const term = searchQuery.toLowerCase();
    const matchSearch = (item.name || '').toLowerCase().includes(term) || (item.templateCode || '').toLowerCase().includes(term);
    const matchStatus = statusFilter ? item.status === statusFilter : true;
    return matchSearch && matchStatus;
  });

  if (showForm) {
    return (
      <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
        <div className="flex items-center justify-between border-b border-gray-100 pb-4 mb-6">
          <h2 className="text-xl font-bold text-[#004c91]">{editingId ? 'Chỉnh sửa mẫu email' : 'Tạo mẫu email mới'}</h2>
          <button onClick={() => setShowForm(false)} className="text-gray-400 hover:text-gray-700">
            <X className="w-6 h-6" />
          </button>
        </div>
        <form onSubmit={handleSubmit} className="space-y-6">
          <div className="grid grid-cols-2 gap-6">
            <div>
              <label className="block text-sm font-bold text-gray-700 mb-1">Mã mẫu *</label>
              <input type="text" value={formData.templateCode} onChange={e => setFormData({...formData, templateCode: e.target.value})} disabled={!!editingId} className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm disabled:bg-gray-100 outline-none focus:border-[#004c91]" />
            </div>
            <div>
              <label className="block text-sm font-bold text-gray-700 mb-1">Tên mẫu *</label>
              <input type="text" value={formData.name} onChange={e => setFormData({...formData, name: e.target.value})} className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91]" />
            </div>
          </div>
          <div>
            <label className="block text-sm font-bold text-gray-700 mb-1">Mục đích/Mô tả</label>
            <input type="text" value={formData.purpose} onChange={e => setFormData({...formData, purpose: e.target.value})} className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91]" />
          </div>
          <div>
            <label className="block text-sm font-bold text-gray-700 mb-1">Trạng thái</label>
            <select value={formData.status} onChange={e => setFormData({...formData, status: e.target.value})} className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91]">
              <option value="ACTIVE">Đang hoạt động</option>
              <option value="INACTIVE">Tạm khóa</option>
            </select>
          </div>
          <div className="border-t border-gray-100 pt-6">
            <h3 className="font-bold text-gray-800 mb-4">Nội dung Email</h3>
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-bold text-gray-700 mb-1">Tiêu đề (Subject) *</label>
                <input type="text" value={formData.subject} onChange={e => setFormData({...formData, subject: e.target.value})} className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91]" />
              </div>
              <div>
                <label className="block text-sm font-bold text-gray-700 mb-1">Nội dung HTML (Body) *</label>
                <div className="border border-gray-300 rounded-lg overflow-hidden bg-white">
                  <ReactQuill theme="snow" value={formData.content} onChange={v => setFormData({...formData, content: v})} modules={QUILL_MODULES} />
                </div>
              </div>
            </div>
          </div>
          <div className="flex justify-end gap-3 pt-4">
            <button type="button" onClick={() => setShowForm(false)} className="px-4 py-2 font-bold text-gray-600 bg-gray-100 rounded-lg hover:bg-gray-200">Hủy</button>
            <button type="submit" disabled={submitting} className="flex items-center gap-2 px-4 py-2 font-bold text-white bg-[#004c91] rounded-lg hover:bg-[#013565] disabled:opacity-50">
              {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
              {editingId ? 'Cập nhật' : 'Tạo mới'}
            </button>
          </div>
        </form>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden flex flex-col">
      <div className="p-4 border-b border-gray-100 flex items-center justify-between bg-gray-50/50">
        <div className="flex items-center gap-3">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 w-4 h-4" />
            <input type="text" value={searchQuery} onChange={e => setSearchQuery(e.target.value)} placeholder="Tìm kiếm mẫu..." className="pl-9 pr-3 py-1.5 text-sm border border-gray-300 rounded-md focus:border-[#004c91] outline-none" />
          </div>
          <select value={statusFilter} onChange={e => setStatusFilter(e.target.value)} className="border border-gray-300 rounded-md px-3 py-1.5 text-sm outline-none">
            <option value="">Tất cả trạng thái</option>
            <option value="ACTIVE">Đang hoạt động</option>
            <option value="INACTIVE">Tạm khóa</option>
          </select>
        </div>
        <button onClick={() => { setEditingId(null); setFormData({templateCode: '', name: '', purpose: '', subject: '', content: '', status: 'ACTIVE'}); setShowForm(true); }} className="flex items-center gap-1.5 bg-[#004c91] text-white px-3 py-1.5 rounded-md text-sm font-bold hover:bg-[#013565] transition-colors">
          <Plus className="w-4 h-4" /> Thêm mẫu mới
        </button>
      </div>
      
      <div className="overflow-x-auto">
        <table className="w-full text-sm text-left">
          <thead className="bg-[#004c91] text-white text-xs uppercase tracking-wider">
            <tr>
              <th className="px-4 py-3 font-bold text-center w-[60px]">STT</th>
              <th className="px-4 py-3 font-bold">Mã mẫu</th>
              <th className="px-4 py-3 font-bold">Tên mẫu</th>
              <th className="px-4 py-3 font-bold text-center">Trạng thái</th>
              <th className="px-4 py-3 font-bold text-center w-[150px]">Hành động</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {isLoading ? (
              <tr><td colSpan={5} className="p-8 text-center text-gray-500">Đang tải...</td></tr>
            ) : filteredData.length === 0 ? (
              <tr><td colSpan={5} className="p-8 text-center text-gray-500">Không tìm thấy mẫu email nào</td></tr>
            ) : (
              filteredData.map((item, index) => (
                <tr key={item.emailTemplateId} className="hover:bg-gray-50">
                  <td className="px-4 py-3 text-center text-gray-500">{index + 1}</td>
                  <td className="px-4 py-3 font-bold text-[#004c91]">{item.templateCode}</td>
                  <td className="px-4 py-3 font-medium">{item.name}</td>
                  <td className="px-4 py-3 text-center">
                    <button onClick={() => handleToggleStatus(item.emailTemplateId, item.status)} className={`px-2.5 py-1 rounded-full text-xs font-bold border transition-colors ${item.status === 'ACTIVE' ? 'bg-[#eaffe4] text-[#0aa14f] border-[#ceefda] hover:bg-red-50 hover:text-red-600 hover:border-red-200' : 'bg-gray-100 text-gray-500 border-gray-200 hover:bg-[#eaffe4] hover:text-[#0aa14f] hover:border-[#ceefda]'}`} title="Nhấn để đổi trạng thái">
                      {item.status === 'ACTIVE' ? 'Đang hoạt động' : 'Tạm khóa'}
                    </button>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <button onClick={() => handleEdit(item.emailTemplateId)} className="p-1.5 text-gray-500 hover:text-[#004c91] hover:bg-blue-50 rounded" title="Chỉnh sửa">
                      <Edit2 className="w-4 h-4" />
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
