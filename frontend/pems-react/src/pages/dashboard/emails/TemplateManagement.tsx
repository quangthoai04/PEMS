import React, { useState, useEffect, useRef, useMemo } from 'react';
import { Search, Plus, Edit2, Check, X, ShieldAlert, Loader2 } from 'lucide-react';
import { emailsApi } from '../../../features/emails/api/emailsApi';
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';
import { ConfirmModal } from '../../../components/modals/ConfirmModal';

const QUILL_MODULES = {
  toolbar: [
    ['bold', 'italic', 'underline', 'strike'],
    [{ list: 'ordered' }, { list: 'bullet' }],
    [{ align: [] }],
    ['clean']
  ]
};

const commonEmailVariables = [
  { key: 'recipientName', label: 'Tên người nhận', sample: 'Nguyễn Văn A' },
  { key: 'delegationName', label: 'Tên đoàn', sample: 'Đoàn Đại học ABC' },
  { key: 'campusName', label: 'Cơ sở', sample: 'FPTU Hà Nội' },
  { key: 'detailUrl', label: 'Link xem chi tiết', sample: 'https://pems.fpt.edu.vn/...' },
  { key: 'statusLabel', label: 'Trạng thái hiển thị', sample: 'Đang xử lý' }
];

const logisticsEmailVariables = [
  { key: 'logisticsTitle', label: 'Tên yêu cầu hậu cần', sample: 'Chuẩn bị phòng họp' },
  { key: 'departmentName', label: 'Tên phòng ban', sample: 'Phòng Công tác sinh viên' },
  { key: 'departmentLeaderName', label: 'Trưởng phòng ban', sample: 'Trần Thị B' },
  { key: 'requesterName', label: 'Người yêu cầu', sample: 'Nguyễn Văn C' },
  { key: 'usageStartAt', label: 'Bắt đầu sử dụng', sample: '20/08/2026 08:00' },
  { key: 'usageEndAt', label: 'Kết thúc sử dụng', sample: '20/08/2026 11:00' }
];

const AVAILABLE_VARIABLES = [...commonEmailVariables, ...logisticsEmailVariables];

export function TemplateManagement({ pushToast }: { pushToast: (type: 'success' | 'error', msg: string) => void }) {
  const quillRef = useRef<any>(null);
  const subjectInputRef = useRef<HTMLInputElement>(null);
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
  const [varSearch, setVarSearch] = useState('');
  const [confirmState, setConfirmState] = useState<{isOpen: boolean; onConfirm: () => void; message: string; title: string; variant?: 'warning' | 'danger' | 'default'}>({isOpen: false, onConfirm: () => {}, message: '', title: ''});

  const parsedVars = useMemo(() => {
    const regex = /(?:\{\{|%7B%7B)\s*([a-zA-Z][a-zA-Z0-9_]*)\s*(?:\}\}|%7D%7D)/g;
    const subjMatches = Array.from(formData.subject.matchAll(regex)).map(m => m[1]);
    const contMatches = Array.from(formData.content.matchAll(regex)).map(m => m[1]);
    const rawVars = [...subjMatches, ...contMatches];
    
    const uniqueRawVars = Array.from(new Set(rawVars));
    
    const unknown = uniqueRawVars.filter(v => !AVAILABLE_VARIABLES.find(av => av.key === v));
    
    const casingSuggestions = unknown.map(v => {
      const match = AVAILABLE_VARIABLES.find(av => av.key.toLowerCase() === v.toLowerCase());
      return match ? { original: v, suggested: match.key } : null;
    }).filter(Boolean) as { original: string, suggested: string }[];
    
    const trulyUnknownVars = unknown.filter(v => !casingSuggestions.find(s => s.original === v));
    
    return { unknown, casingSuggestions, trulyUnknownVars };
  }, [formData.subject, formData.content]);

  const handleNormalizeVariables = () => {
    let newSubject = formData.subject;
    let newContent = formData.content;
    parsedVars.casingSuggestions.forEach(s => {
      const regex1 = new RegExp(`\\{\\{\\s*${s.original}\\s*\\}\\}`, 'g');
      const regex2 = new RegExp(`%7B%7B\\s*${s.original}\\s*%7D%7D`, 'g');
      newSubject = newSubject.replace(regex1, `{{${s.suggested}}}`).replace(regex2, `%7B%7B${s.suggested}%7D%7D`);
      newContent = newContent.replace(regex1, `{{${s.suggested}}}`).replace(regex2, `%7B%7B${s.suggested}%7D%7D`);
    });
    setFormData(prev => ({ ...prev, subject: newSubject, content: newContent }));
    pushToast('success', 'Đã chuẩn hóa biến thành công');
  };

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

  const handleSubmit = (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!formData.templateCode.trim() || !formData.name.trim() || !formData.subject.trim() || !formData.content.trim()) {
      pushToast('error', 'Vui lòng nhập đầy đủ các trường bắt buộc');
      return;
    }
    if (parsedVars.unknown.length > 0 && (!e || e.type !== 'submit')) { // check if event is passed (first click) vs from confirm (no event)
      setConfirmState({
        isOpen: true,
        title: 'Xác nhận lưu',
        message: 'Mẫu email còn biến chưa được định nghĩa hoặc sai chuẩn. Nếu tiếp tục lưu, khi gửi email các biến này có thể không được thay thế.\n\nBạn có chắc chắn muốn lưu không?',
        variant: 'warning',
        onConfirm: () => {
          setConfirmState(prev => ({...prev, isOpen: false}));
          executeSubmit();
        }
      });
      return;
    }
    executeSubmit();
  };

  const executeSubmit = async () => {
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

  const handleCancel = () => {
    if (formData.templateCode || formData.name || formData.subject || formData.content) {
      setConfirmState({
        isOpen: true,
        title: 'Hủy thay đổi',
        message: 'Bạn có thay đổi chưa lưu. Hủy và bỏ các thay đổi?',
        variant: 'danger',
        onConfirm: () => {
          setConfirmState(prev => ({...prev, isOpen: false}));
          setShowForm(false);
        }
      });
    } else {
      setShowForm(false);
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
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <div className="lg:col-span-2 space-y-6">
              {/* 1. Thông tin chung */}
              <div className="bg-gray-50/50 p-5 rounded-lg border border-gray-200">
                <h3 className="font-bold text-gray-800 mb-4 text-base border-b border-gray-200 pb-2">1. Thông tin chung</h3>
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-1">Mã mẫu *</label>
                    <input type="text" value={formData.templateCode} onChange={e => setFormData({...formData, templateCode: e.target.value})} disabled={!!editingId} className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm disabled:bg-gray-100 outline-none focus:border-[#004c91]" />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-1">Tên mẫu *</label>
                    <input type="text" value={formData.name} onChange={e => setFormData({...formData, name: e.target.value})} className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91]" />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-1">Trạng thái</label>
                    <select value={formData.status} onChange={e => setFormData({...formData, status: e.target.value})} className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91]">
                      <option value="ACTIVE">Đang hoạt động</option>
                      <option value="INACTIVE">Tạm khóa</option>
                    </select>
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-1">Mục đích/Mô tả</label>
                    <input type="text" value={formData.purpose} onChange={e => setFormData({...formData, purpose: e.target.value})} className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91]" />
                  </div>
                </div>
              </div>

              {/* 2. Nội dung email */}
              <div className="bg-gray-50/50 p-5 rounded-lg border border-gray-200">
                <h3 className="font-bold text-gray-800 mb-4 text-base border-b border-gray-200 pb-2">2. Nội dung email</h3>
                <div className="space-y-4">
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-1">Tiêu đề (Subject) *</label>
                    <input ref={subjectInputRef} type="text" value={formData.subject} onChange={e => setFormData({...formData, subject: e.target.value})} className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91]" />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-1">Nội dung HTML (Body) *</label>
                    <div className="border border-gray-300 rounded-lg overflow-hidden bg-white">
                      <ReactQuill ref={quillRef} theme="snow" value={formData.content} onChange={v => setFormData({...formData, content: v})} modules={QUILL_MODULES} className="min-h-[250px]" />
                    </div>
                  </div>
                </div>
              </div>

              {/* Xem trước hiển thị */}
              <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-sm mt-6">
                <h3 className="font-bold text-gray-800 mb-4 text-base border-b border-gray-200 pb-2 flex justify-between items-center">
                  Xem trước hiển thị
                  {parsedVars.casingSuggestions.length > 0 && (
                     <button type="button" onClick={handleNormalizeVariables} className="text-[11px] bg-blue-50 text-[#004c91] px-3 py-1.5 rounded border border-blue-200 hover:bg-[#004c91] hover:text-white transition-colors">
                       Chuẩn hóa biến
                     </button>
                  )}
                </h3>
                
                {parsedVars.unknown.length > 0 && (
                  <div className="mb-4 bg-orange-50 border-l-4 border-orange-400 p-3 rounded text-sm text-orange-800">
                    <div className="flex items-start gap-2">
                      <ShieldAlert className="w-4 h-4 mt-0.5 text-orange-500 flex-shrink-0" />
                      <div>
                        <span className="font-bold block mb-1">Một số biến chưa được định nghĩa hoặc sai định dạng:</span>
                        <ul className="list-disc list-inside text-xs space-y-1">
                          {parsedVars.casingSuggestions.map(s => (
                            <li key={s.original}>
                              <span className="font-mono">{'{{' + s.original + '}}'}</span> → gợi ý: <span className="font-mono font-bold text-[#004c91]">{'{{' + s.suggested + '}}'}</span>
                            </li>
                          ))}
                          {parsedVars.trulyUnknownVars.map(v => (
                            <li key={v}>
                              <span className="font-mono text-red-600 font-bold">{'{{' + v + '}}'}</span> — chưa có trong dictionary
                            </li>
                          ))}
                        </ul>
                      </div>
                    </div>
                  </div>
                )}
                
                <div className="border border-gray-200 rounded-lg p-4 text-sm text-gray-700 min-h-[150px] shadow-inner bg-gray-50/30">
                  <div className="font-bold border-b border-gray-100 pb-2 mb-2">
                    {formData.subject ? AVAILABLE_VARIABLES.reduce((acc, v) => acc.replace(new RegExp(`(?:\\{\\{|%7B%7B)\\s*${v.key}\\s*(?:\\}\\}|%7D%7D)`, 'g'), v.sample), formData.subject) : <span className="text-gray-400 italic">Chưa có tiêu đề...</span>}
                  </div>
                  <div className="prose prose-sm max-w-none" dangerouslySetInnerHTML={{ 
                    __html: formData.content ? AVAILABLE_VARIABLES.reduce((acc, v) => acc.replace(new RegExp(`(?:\\{\\{|%7B%7B)\\s*${v.key}\\s*(?:\\}\\}|%7D%7D)`, 'gi'), v.sample), formData.content) : '<span class="text-gray-400 italic">Chưa có nội dung...</span>' 
                  }} />
                </div>
              </div>
            </div>

            <div className="lg:col-span-1 space-y-6">
              {/* 3. Biến mẫu có thể chèn */}
              <div className="bg-[#f8fbff] p-5 rounded-lg border border-[#cce0ff] h-full flex flex-col">
                <h3 className="font-bold text-[#004c91] mb-4 text-base border-b border-[#cce0ff] pb-2">3. Biến mẫu có thể chèn</h3>
                
                <div className="relative mb-4 flex-shrink-0">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 w-4 h-4" />
                  <input type="text" value={varSearch} onChange={e => setVarSearch(e.target.value)} placeholder="Tìm biến..." className="w-full pl-9 pr-3 py-1.5 text-sm border border-gray-300 rounded-md focus:border-[#004c91] outline-none" />
                </div>

                <div className="space-y-5 overflow-y-auto flex-1 pr-2 pb-4">
                  <div>
                    <h4 className="text-[11px] font-bold text-gray-500 uppercase tracking-wider mb-2">Biến dùng chung</h4>
                    <div className="flex flex-wrap gap-1.5">
                      {commonEmailVariables.filter(v => v.label.toLowerCase().includes(varSearch.toLowerCase()) || v.key.toLowerCase().includes(varSearch.toLowerCase())).map(v => (
                        <button
                          key={v.key}
                          type="button"
                          onClick={() => {
                            const editor = quillRef.current?.getEditor();
                            if (editor && editor.hasFocus()) {
                              const range = editor.getSelection(true);
                              editor.insertText(range.index, `{{${v.key}}}`);
                              setFormData(prev => ({ ...prev, content: editor.root.innerHTML }));
                            } else if (document.activeElement === subjectInputRef.current) {
                              const input = subjectInputRef.current!;
                              const start = input.selectionStart || 0;
                              const end = input.selectionEnd || 0;
                              const newSubj = formData.subject.substring(0, start) + `{{${v.key}}}` + formData.subject.substring(end);
                              setFormData(prev => ({ ...prev, subject: newSubj }));
                            } else {
                              setFormData(prev => ({ ...prev, content: prev.content + `{{${v.key}}}` }));
                            }
                          }}
                          className="inline-flex items-center gap-1 bg-white border border-[#004c91] text-[#004c91] hover:bg-[#004c91] hover:text-white transition-colors px-2 py-1 rounded text-[11px] font-bold outline-none"
                          title={`{{${v.key}}} — Ví dụ: ${v.sample}`}
                        >
                          <Plus className="w-3 h-3" /> {v.label}
                        </button>
                      ))}
                      {commonEmailVariables.filter(v => v.label.toLowerCase().includes(varSearch.toLowerCase()) || v.key.toLowerCase().includes(varSearch.toLowerCase())).length === 0 && (
                        <div className="text-xs text-gray-400 italic">Không tìm thấy biến</div>
                      )}
                    </div>
                  </div>
                  
                  <div>
                    <h4 className="text-[11px] font-bold text-gray-500 uppercase tracking-wider mb-2">Biến hậu cần</h4>
                    <div className="flex flex-wrap gap-1.5">
                      {logisticsEmailVariables.filter(v => v.label.toLowerCase().includes(varSearch.toLowerCase()) || v.key.toLowerCase().includes(varSearch.toLowerCase())).map(v => (
                        <button
                          key={v.key}
                          type="button"
                          onClick={() => {
                            const editor = quillRef.current?.getEditor();
                            if (editor && editor.hasFocus()) {
                              const range = editor.getSelection(true);
                              editor.insertText(range.index, `{{${v.key}}}`);
                              setFormData(prev => ({ ...prev, content: editor.root.innerHTML }));
                            } else if (document.activeElement === subjectInputRef.current) {
                              const input = subjectInputRef.current!;
                              const start = input.selectionStart || 0;
                              const end = input.selectionEnd || 0;
                              const newSubj = formData.subject.substring(0, start) + `{{${v.key}}}` + formData.subject.substring(end);
                              setFormData(prev => ({ ...prev, subject: newSubj }));
                            } else {
                              setFormData(prev => ({ ...prev, content: prev.content + `{{${v.key}}}` }));
                            }
                          }}
                          className="inline-flex items-center gap-1 bg-white border border-[#004c91] text-[#004c91] hover:bg-[#004c91] hover:text-white transition-colors px-2 py-1 rounded text-[11px] font-bold outline-none"
                          title={`{{${v.key}}} — Ví dụ: ${v.sample}`}
                        >
                          <Plus className="w-3 h-3" /> {v.label}
                        </button>
                      ))}
                      {logisticsEmailVariables.filter(v => v.label.toLowerCase().includes(varSearch.toLowerCase()) || v.key.toLowerCase().includes(varSearch.toLowerCase())).length === 0 && (
                        <div className="text-xs text-gray-400 italic">Không tìm thấy biến</div>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
          <div className="flex justify-end gap-3 pt-4">
            <button type="button" onClick={handleCancel} className="px-4 py-2 font-bold text-gray-600 bg-gray-100 rounded-lg hover:bg-gray-200">Hủy</button>
            <button type="submit" disabled={submitting} className="flex items-center gap-2 px-4 py-2 font-bold text-white bg-[#004c91] rounded-lg hover:bg-[#013565] disabled:opacity-50">
              {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
              {editingId ? 'Cập nhật' : 'Tạo mới'}
            </button>
          </div>
        </form>
        <ConfirmModal
          isOpen={confirmState.isOpen}
          onClose={() => setConfirmState(prev => ({...prev, isOpen: false}))}
          onConfirm={confirmState.onConfirm}
          title={confirmState.title}
          message={confirmState.message}
          variant={confirmState.variant}
        />
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
