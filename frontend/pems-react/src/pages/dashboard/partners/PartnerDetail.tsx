/**
 * Trang PartnerDetail
 * Báo cáo tương tác tiến độ lưu trữ đối ngoại của tổ chức liên kết.
 */

import React, { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ChevronRight, Info, History, FileText, Plus, Trash2, MapPin, Globe, CheckCircle, ArrowLeft, Edit3, Check, Eye, X } from 'lucide-react';


// For thumbnails
import coverImage from '../../../assets/images/banner_partner.png';
const logoModules = import.meta.glob("../../../assets/Logo/*", { eager: true });
const logoList = Object.values(logoModules).map((m: any) => m.default || m) as string[];

export function PartnerDetail() {
  const navigate = useNavigate();
  const { id } = useParams();
  
  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase() || '';
  const isStaffLeader = user?.role?.toUpperCase() === 'STAFF' && user?.subRole?.toUpperCase() === 'LEADER';
  const isHO = userRole === 'HO';

  // Mock contact person data
  const [contacts, setContacts] = useState([
    { id: 1, name: 'Nguyễn Văn A', phone: '0123456789', email: 'a@example.com', role: 'Trưởng phòng', department: 'Tuyển sinh', company: 'Đại học Deakin', website: 'https://deakin.edu.au', address: 'Victoria, Úc' },
    { id: 2, name: 'Trần Thị B', phone: '0987654321', email: 'b@example.com', role: 'Nhân viên', department: 'Đào tạo', company: 'Đại học Deakin', website: 'https://deakin.edu.au', address: 'Victoria, Úc' }
  ]);
  const [selectedContact, setSelectedContact] = useState<any>(null);
  const [isContactModalOpen, setIsContactModalOpen] = useState(false);
  const [isDeleteContactModalOpen, setIsDeleteContactModalOpen] = useState(false);
  const [contactToDelete, setContactToDelete] = useState<number | null>(null);
  const [isEditingContacts, setIsEditingContacts] = useState(false);

  const [isEditingPartner, setIsEditingPartner] = useState(false);
  const [partnerDetails, setPartnerDetails] = useState({
    code: 'DK-001',
    name: 'Đại học Deakin',
    status: 'Đã Duyệt',
    country: 'Úc',
    website: 'https://deakin.edu.au',
    description: 'Đại học Deakin (Deakin University) là một trường đại học công lập ở bang Victoria, Úc. Chuyên đào tạo và cấp bằng về các ngành kỹ thuật, kinh tế, công nghệ thông tin...'
  });

  const updatePartnerDetail = (field: string, value: string) => {
    setPartnerDetails(prev => ({ ...prev, [field]: value }));
  };

  const [coverImagePreview, setCoverImagePreview] = useState(coverImage);
  const [logoPreview, setLogoPreview] = useState(logoList[0] || 'https://via.placeholder.com/150');

  const handleImageUpload = (e: React.ChangeEvent<HTMLInputElement>, type: 'cover' | 'logo') => {
    const file = e.target.files?.[0];
    if (file) {
      const url = URL.createObjectURL(file);
      if (type === 'cover') setCoverImagePreview(url);
      else setLogoPreview(url);
    }
  };

  const addContact = () => {
    const newId = contacts.length ? Math.max(...contacts.map(c => c.id)) + 1 : 1;
    setContacts([...contacts, { id: newId, name: '', phone: '', email: '', role: '', department: '', company: partnerDetails.name, website: partnerDetails.website, address: '' }]);
  };

  const removeContact = (id: number) => {
    setContacts(contacts.filter(c => c.id !== id));
  };

  const confirmDeleteContact = () => {
    if (contactToDelete !== null) {
      removeContact(contactToDelete);
      setIsDeleteContactModalOpen(false);
      setContactToDelete(null);
    }
  };

  const updateContact = (id: number, field: string, value: string) => {
    setContacts(contacts.map(c => c.id === id ? { ...c, [field]: value } : c));
  };

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-7xl mx-auto w-full">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500 mb-6 font-medium">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors">Dashboard</button>
        <span className="text-gray-400">/</span>
        <button onClick={() => navigate('/dashboard/partners')} className="hover:text-[#004c91] transition-colors">Quản lý đối tác</button>
        <span className="text-gray-400">/</span>
        <span className="text-[#004c91]">Chi tiết đối tác</span>
      </div>

      <div className="mb-6 flex items-center justify-between">
        <button 
          onClick={() => navigate(-1)} 
          className="flex items-center gap-2 px-4 py-2.5 rounded-xl border border-gray-200 bg-white shadow-sm hover:border-[#004c91] hover:text-[#004c91] transition-all duration-300 font-bold text-gray-700 outline-none group"
        >
          <ArrowLeft className="w-5 h-5 group-hover:-translate-x-1 transition-transform" />
          <span>Quay lại</span>
        </button>
      {!isHO && !isStaffLeader && (
      <div className="flex gap-3 ml-auto">
        <button
          onClick={() => setIsEditingPartner(!isEditingPartner)}
            className={`flex items-center gap-2 px-5 py-2.5 rounded-xl border shadow-sm transition-all duration-200 font-bold outline-none ${
              isEditingPartner 
                ? 'bg-[#0aa14f] text-white hover:bg-[#088a42] border-[#0aa14f]' 
                : 'bg-white text-[#004c91] hover:bg-[#f0f6ff] border-[#004c91]'
            }`}
          >
            {isEditingPartner ? (
              <>
                <Check className="w-5 h-5" /> Lưu thay đổi
              </>
            ) : (
              <>
                <Edit3 className="w-5 h-5" /> Chỉnh sửa
              </>
            )}
          </button>
        </div>
      )}
      </div>

      {/* Cover & Logo Section */}
      <div className="relative mb-10 w-full h-[500px] rounded-[24px] bg-gray-100 shadow-sm overflow-hidden group">
        <img 
          src={coverImagePreview} 
          alt="Cover" 
          className="w-full h-full object-cover"
        />
        <div className="absolute inset-0 bg-gradient-to-t from-black/60 to-transparent"></div>
        {isEditingPartner && (
            <label className="absolute inset-0 bg-black/20 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity cursor-pointer">
              <div className="bg-white/90 px-4 py-2 rounded-xl flex items-center gap-2 font-bold text-gray-800 shadow-lg">
                <Edit3 className="w-5 h-5" /> Đổi ảnh bìa
              </div>
              <input type="file" accept="image/*" className="hidden" onChange={(e) => handleImageUpload(e, 'cover')} />
            </label>
        )}
        
        {/* Logo overlay */}
        <div className="absolute -bottom-2 -left-2 p-6 flex items-end">
          <div className="relative group/logo">
            <div className="w-28 h-28 rounded-[20px] bg-white shadow-xl p-2 z-10 border-4 border-white overflow-hidden flex items-center justify-center">
               <img src={logoPreview} alt="Logo" className="w-full h-full object-contain" />
            </div>
            {isEditingPartner && (
              <label className="absolute inset-x-2 inset-y-2 bg-black/40 rounded-2xl z-20 flex items-center justify-center opacity-0 group-hover/logo:opacity-100 transition-opacity cursor-pointer">
                <Edit3 className="w-6 h-6 text-white" />
                <input type="file" accept="image/*" className="hidden" onChange={(e) => handleImageUpload(e, 'logo')} />
              </label>
            )}
          </div>
          <div className="ml-6 mb-4 text-white z-10">
            <h1 className="text-3xl font-bold tracking-tight shadow-sm drop-shadow-md">{partnerDetails.name}</h1>
            <div className="flex items-center gap-2 mt-2 opacity-90 font-medium">
              <MapPin className="w-4 h-4" /> {partnerDetails.country}
            </div>
          </div>
        </div>
      </div>

      {/* Thông tin cơ bản */}
      <div className="bg-white rounded-3xl shadow-[0_4px_24px_rgba(0,0,0,0.03)] border border-gray-100 overflow-hidden mb-8">
        <div className="bg-[#004c91] px-6 py-4 flex items-center gap-2.5">
          <Info className="w-6 h-6 text-white" />
          <h2 className="text-lg font-bold text-white uppercase tracking-wider">Thông tin cơ bản</h2>
        </div>
        <div className="p-4 sm:p-6 md:p-8">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-x-8 gap-y-8">
            {/* Row 1 */}
            <div>
              <span className="block text-[13px] font-bold text-[#004c91] uppercase tracking-wider mb-1.5">Mã đối tác <span className="text-red-500">*</span></span>
              {isEditingPartner ? (
                <input type="text" value={partnerDetails.code} onChange={e => updatePartnerDetail('code', e.target.value)} className="w-full border border-gray-300 rounded-lg px-3 py-2 text-[15px] font-medium text-gray-900 focus:outline-none focus:border-[#004c91] transition-colors shadow-sm" />
              ) : (
                <div className="text-[16px] font-bold text-gray-900">{partnerDetails.code}</div>
              )}
            </div>
            <div>
              <span className="block text-[13px] font-bold text-[#004c91] uppercase tracking-wider mb-1.5">Tên đối tác <span className="text-red-500">*</span></span>
              {isEditingPartner ? (
                <input type="text" value={partnerDetails.name} onChange={e => updatePartnerDetail('name', e.target.value)} className="w-full border border-gray-300 rounded-lg px-3 py-2 text-[15px] font-medium text-gray-900 focus:outline-none focus:border-[#004c91] transition-colors shadow-sm" />
              ) : (
                <div className="text-[15px] font-medium text-gray-900">{partnerDetails.name}</div>
              )}
            </div>
            <div>
              <span className="block text-[13px] font-bold text-[#004c91] uppercase tracking-wider mb-1.5">Trạng thái</span>
              {isEditingPartner ? (
                <select value={partnerDetails.status} onChange={e => updatePartnerDetail('status', e.target.value)} className="w-full border border-gray-300 rounded-lg px-3 py-2 text-[15px] font-medium text-gray-900 focus:outline-none focus:border-[#004c91] outline-none transition-colors shadow-sm bg-white cursor-pointer -webkit-appearance-none appearance-none bg-[url('data:image/svg+xml;charset=US-ASCII,%3Csvg%20width%3D%2224%22%20height%3D%2224%22%20viewBox%3D%220%200%24%2024%22%20fill%3D%22none%22%20xmlns%3D%22http%3A%2F%2Fwww.w3.org%2F2000%2Fsvg%22%3E%3Cpath%20d%3D%22M7%2010L12%2015L17%2010%22%20stroke%3D%22%236B7280%22%20stroke-width%3D%222%22%20stroke-linecap%3D%22round%22%20stroke-linejoin%3D%22round%22%2F%3E%3C%2Fsvg%3E')] bg-[length:24px_24px] bg-no-repeat bg-[position:right_8px_center]">
                  <option value="Đã Duyệt">Đã Duyệt</option>
                  <option value="Chờ Duyệt">Chờ Duyệt</option>
                  <option value="Từ Chối">Từ Chối</option>
                </select>
              ) : (
                <div className={`inline-flex items-center gap-2 px-3 py-1.5 rounded-xl font-bold text-sm ${
                  partnerDetails.status === 'Đã Duyệt' ? 'text-[#0aa14f] bg-[#eaffe4] border border-[#ceefda]' :
                  partnerDetails.status === 'Từ Chối' ? 'text-red-600 bg-red-50 border border-red-200' :
                  'text-yellow-600 bg-yellow-50 border border-yellow-200'
                }`}>
                  <CheckCircle className={`w-4 h-4 ${partnerDetails.status !== 'Đã Duyệt' && 'hidden'}`} /> {partnerDetails.status}
                </div>
              )}
            </div>

            {/* Row 2 */}
            <div>
              <span className="block text-[13px] font-bold text-[#004c91] uppercase tracking-wider mb-1.5">Quốc gia <span className="text-red-500">*</span></span>
              {isEditingPartner ? (
                <div className="flex items-center gap-2 relative">
                  <Globe className="w-4 h-4 text-gray-400 absolute left-3 z-10" />
                  <input type="text" value={partnerDetails.country} onChange={e => updatePartnerDetail('country', e.target.value)} className="w-full border border-gray-300 rounded-lg pl-9 pr-3 py-2 text-[15px] font-medium text-gray-900 focus:outline-none focus:border-[#004c91] transition-colors shadow-sm" />
                </div>
              ) : (
                <div className="text-[15px] font-medium text-gray-900 flex items-center gap-2">
                  <Globe className="w-4 h-4 text-gray-400" /> {partnerDetails.country}
                </div>
              )}
            </div>
            <div>
              <span className="block text-[13px] font-bold text-[#004c91] uppercase tracking-wider mb-1.5">Website</span>
              {isEditingPartner ? (
                <input type="text" value={partnerDetails.website} onChange={e => updatePartnerDetail('website', e.target.value)} className="w-full border border-gray-300 rounded-lg px-3 py-2 text-[15px] font-medium text-gray-900 focus:outline-none focus:border-[#004c91] transition-colors shadow-sm" />
              ) : (
                <a href={partnerDetails.website} target="_blank" rel="noopener noreferrer" className="text-[15px] font-medium text-[#004c91] hover:underline break-words">{partnerDetails.website}</a>
              )}
            </div>
            <div className="hidden md:block"></div> {/* Placeholder for 3rd column */}

            {/* Row 3 */}
            <div className={`md:col-span-3 rounded-2xl ${isEditingPartner ? 'mt-2' : 'bg-gray-50/80 p-5 border border-gray-100 mt-2'}`}>
              <span className={`block text-[13px] font-bold text-[#004c91] uppercase tracking-wider ${isEditingPartner ? 'mb-1.5' : 'mb-2'}`}>Mô tả chung</span>
              {isEditingPartner ? (
                <textarea 
                  value={partnerDetails.description} 
                  onChange={e => updatePartnerDetail('description', e.target.value)} 
                  className="w-full border border-gray-300 rounded-lg px-3 py-3 text-[15px] font-medium text-gray-900 focus:outline-none focus:border-[#004c91] min-h-[100px] resize-y transition-colors shadow-sm" 
                />
              ) : (
                <div className="text-[15px] font-medium text-gray-700 leading-relaxed">
                  {partnerDetails.description}
                </div>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Grid: Lịch sử & Văn bản */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-8 mb-8">
        {/* Lịch sử hợp tác */}
        <div className="bg-white rounded-3xl shadow-[0_4px_24px_rgba(0,0,0,0.03)] border border-gray-100 overflow-hidden flex flex-col">
          <div className="bg-[#004c91] px-6 py-4 flex items-center justify-between">
            <div className="flex items-center gap-2.5">
              <History className="w-6 h-6 text-white" />
              <h2 className="text-lg font-bold text-white uppercase tracking-wider">Lịch sử hợp tác</h2>
            </div>
            {!isHO && (
              <button className="w-8 h-8 rounded-lg bg-[#f37021] flex items-center justify-center text-white hover:bg-[#d9621a] transition-colors shadow-sm outline-none">
                <Plus className="w-5 h-5" />
              </button>
            )}
          </div>
          <div className="p-6 flex-1 bg-white">
            <div className="relative border-l-2 border-dashed border-[#004c91]/30 ml-3 py-2 space-y-8">
              <div className="relative pl-6">
                <span className="absolute -left-[11px] top-1 w-5 h-5 rounded-full bg-[#f37021] border-4 border-white shadow-sm"></span>
                <div className="text-sm font-bold text-[#004c91] mb-1.5">10/05/2024</div>
                <div className="text-gray-800 font-medium text-[15px] bg-gray-50/60 backdrop-blur-sm p-4 rounded-xl border border-gray-100 shadow-sm">Ký kết MOU trao đổi sinh viên</div>
              </div>
              <div className="relative pl-6">
                <span className="absolute -left-[11px] top-1 w-5 h-5 rounded-full bg-gray-300 border-4 border-white shadow-sm"></span>
                <div className="text-sm font-bold text-gray-500 mb-1.5">15/08/2023</div>
                <div className="text-gray-800 font-medium text-[15px] bg-gray-50/60 backdrop-blur-sm p-4 rounded-xl border border-gray-100 shadow-sm">Tổ chức hội thảo chung về công nghệ AI</div>
              </div>
            </div>
          </div>
        </div>

        {/* Văn bản & Tài liệu */}
        <div className="bg-white rounded-3xl shadow-[0_4px_24px_rgba(0,0,0,0.03)] border border-gray-100 overflow-hidden flex flex-col">
          <div className="bg-[#004c91] px-6 py-4 flex items-center justify-between">
            <div className="flex items-center gap-2.5">
              <FileText className="w-6 h-6 text-white" />
              <h2 className="text-lg font-bold text-white uppercase tracking-wider">Văn bản & Tài liệu</h2>
            </div>
            {!isHO && (
              <button className="w-8 h-8 rounded-lg bg-[#f37021] flex items-center justify-center text-white hover:bg-[#d9621a] transition-colors shadow-sm outline-none">
                <Plus className="w-5 h-5" />
              </button>
            )}
          </div>
          <div className="p-6 flex-1 bg-white">
            <div className="flex flex-col gap-4">
              <div className="bg-gradient-to-r from-blue-50/80 to-[#e6f0fa]/80 p-4 rounded-xl border border-blue-100/50 shadow-sm backdrop-blur-sm flex items-center gap-4 group cursor-pointer hover:border-[#004c91]/50 transition-colors">
                <div className="w-10 h-10 rounded-lg bg-blue-100 flex items-center justify-center text-[#004c91]">
                  <FileText className="w-5 h-5" />
                </div>
                <div>
                  <div className="text-gray-800 font-bold text-[15px] group-hover:text-[#004c91] transition-colors">MOU_Exchange_2024.pdf</div>
                  <div className="text-xs text-gray-500 font-medium mt-0.5">2.4 MB • 10/05/2024</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Danh sách người liên hệ */}
      <div className="bg-white rounded-3xl shadow-[0_4px_24px_rgba(0,0,0,0.03)] border border-gray-100 overflow-hidden">
        <div className="bg-[#00a651] px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="bg-white/20 p-1.5 rounded-lg text-white">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
            </div>
            <h2 className="text-lg font-bold text-white uppercase tracking-wider">Danh sách người liên hệ</h2>
          </div>
          {!isHO && !isStaffLeader && (
            <button 
              onClick={() => setIsEditingContacts(!isEditingContacts)}
              className="flex items-center gap-2 bg-white/20 hover:bg-white/30 text-white px-4 py-2 rounded-xl transition-colors font-bold text-sm outline-none shadow-sm"
            >
              {isEditingContacts ? (
                <>
                  <Check className="w-4 h-4" /> Lưu
                </>
              ) : (
                <>
                  <Edit3 className="w-4 h-4" /> Chỉnh sửa
                </>
              )}
            </button>
          )}
        </div>
        <div className="p-6">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[800px] border-collapse">
              <thead>
                <tr className="border-b-2 border-gray-200">
                  <th className="py-4 text-left text-[13px] font-bold text-gray-500 uppercase tracking-wider w-[20%] pl-4">Tên người liên hệ</th>
                  <th className="py-4 text-center text-[13px] font-bold text-gray-500 uppercase tracking-wider w-[20%] pl-4">Email</th>
                  <th className="py-4 text-center text-[13px] font-bold text-gray-500 uppercase tracking-wider w-[20%] pl-4">SĐT</th>
                  <th className="py-4 text-center text-[13px] font-bold text-gray-500 uppercase tracking-wider w-[20%] pl-4">Chức vụ</th>
                  <th className="py-4 text-center text-[13px] font-bold text-gray-500 uppercase tracking-wider w-[10%] pl-4">Hành động</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {(contacts.length === 0 && !isEditingContacts) ? (
                  <tr>
                    <td colSpan={5} className="p-4 sm:p-6 md:p-8 text-center text-gray-500 font-medium bg-gray-50/50">
                      Danh sách trống
                    </td>
                  </tr>
                ) : (
                  contacts.map((contact) => (
                    <tr key={contact.id} className="hover:bg-gradient-to-r hover:from-[#eaffe4] hover:to-[#ceefda]/40 transition-colors group">
                      <td className="p-3">
                        <input 
                          type="text" 
                          value={contact.name} 
                          onChange={(e) => updateContact(contact.id, 'name', e.target.value)}
                          placeholder="Nhập tên..."
                          readOnly={!isEditingContacts}
                          className={`w-full bg-transparent px-3 py-2 text-sm focus:outline-none transition-colors font-medium text-gray-800 ${isEditingContacts ? 'border border-gray-200 rounded-lg focus:border-[#00a651] bg-white group-hover:bg-[#f8fdf9]' : 'cursor-default'}`}
                        />
                      </td>
                      <td className="p-3">
                        <input 
                          type="text" 
                          value={contact.email || ''} 
                          onChange={(e) => updateContact(contact.id, 'email', e.target.value)}
                          placeholder="Email..."
                          readOnly={!isEditingContacts}
                          className={`w-full text-center bg-transparent px-3 py-2 text-sm focus:outline-none transition-colors font-medium text-gray-800 ${isEditingContacts ? 'border border-gray-200 rounded-lg focus:border-[#00a651] bg-white group-hover:bg-[#f8fdf9]' : 'cursor-default'}`}
                        />
                      </td>
                      <td className="p-3">
                        <input 
                          type="text" 
                          value={contact.phone || ''} 
                          onChange={(e) => updateContact(contact.id, 'phone', e.target.value)}
                          placeholder="SĐT..."
                          readOnly={!isEditingContacts}
                          className={`w-full text-center bg-transparent px-3 py-2 text-sm focus:outline-none transition-colors font-medium text-gray-800 ${isEditingContacts ? 'border border-gray-200 rounded-lg focus:border-[#00a651] bg-white group-hover:bg-[#f8fdf9]' : 'cursor-default'}`}
                        />
                      </td>
                      <td className="p-3">
                        <input 
                          type="text" 
                          value={contact.role || ''} 
                          onChange={(e) => updateContact(contact.id, 'role', e.target.value)}
                          placeholder="Chức vụ..."
                          readOnly={!isEditingContacts}
                          className={`w-full text-center bg-transparent px-3 py-2 text-sm focus:outline-none transition-colors font-medium text-gray-800 ${isEditingContacts ? 'border border-gray-200 rounded-lg focus:border-[#00a651] bg-white group-hover:bg-[#f8fdf9]' : 'cursor-default'}`}
                        />
                      </td>
                      <td className="p-3 text-center">
                        <div className="flex items-center justify-center gap-2">
                          <button 
                            onClick={() => { setSelectedContact(contact); setIsContactModalOpen(true); }}
                            className="p-1.5 text-gray-400 hover:text-[#00a651] hover:bg-[#eaffe4] rounded-lg transition-colors border border-transparent hover:border-[#ceefda] outline-none flex items-center justify-center"
                            title="Xem chi tiết"
                          >
                            <Eye className="w-5 h-5" />
                          </button>
                          {!isHO && !isStaffLeader && (
                            <button 
                              onClick={() => { setContactToDelete(contact.id); setIsDeleteContactModalOpen(true); }}
                              className="p-1.5 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors border border-transparent hover:border-red-200 outline-none flex items-center justify-center"
                              title="Xóa"
                            >
                              <Trash2 className="w-5 h-5" />
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
          
          {isEditingContacts && (
            <div className="mt-6">
              <button 
                onClick={addContact}
                className="px-5 py-2.5 bg-[#eaffe4] text-[#00a651] font-bold rounded-xl border border-[#ceefda] flex items-center gap-2 hover:bg-[#d4f5dd] transition-colors outline-none"
              >
                <Plus className="w-5 h-5" /> Thêm dòng
              </button>
            </div>
          )}
        </div>
      </div>


      {/* Contact Detail Modal */}
      {isContactModalOpen && selectedContact && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg overflow-hidden animate-in fade-in zoom-in-95 duration-200">
            <div className="flex items-center justify-between p-5 border-b border-gray-100 bg-[#00a651]">
              <h3 className="text-xl font-bold text-white">Thông tin chi tiết</h3>
              <button 
                onClick={() => setIsContactModalOpen(false)}
                className="p-2 text-white/80 hover:text-white hover:bg-white/20 rounded-lg transition-colors outline-none"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            
            <div className="p-6 space-y-6 bg-gray-50/50">
              <div className="flex items-center gap-4 pb-4 border-b border-gray-200">
                <div className="w-14 h-14 bg-gradient-to-br from-[#eaffe4] to-[#ceefda] rounded-xl flex items-center justify-center text-[#00a651] font-black text-2xl shrink-0 shadow-sm border border-[#00a651]/20">
                  {selectedContact.name ? selectedContact.name.charAt(0) : '?'}
                </div>
                <div>
                   <h4 className="font-black text-gray-900 text-xl tracking-tight">{selectedContact.name}</h4>
                   <p className="text-sm font-bold text-[#00a651] uppercase tracking-wide mt-1">{selectedContact.role} {selectedContact.department ? `- ${selectedContact.department}` : ''}</p>
                </div>
              </div>
              
              <div className="grid grid-cols-2 gap-5 bg-white p-5 rounded-xl border border-gray-100 shadow-sm">
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-1.5 flex items-center gap-1"><Info className="w-3.5 h-3.5"/> Công ty / Đối tác</label>
                  <p className="text-[15px] font-bold text-gray-800">{selectedContact.company || partnerDetails.name}</p>
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-1.5 flex items-center gap-1"><MapPin className="w-3.5 h-3.5"/> Địa chỉ</label>
                  <p className="text-[15px] font-medium text-gray-800 max-w-[200px] truncate" title={selectedContact.address || partnerDetails.country || 'Chưa cập nhật'}>{selectedContact.address || partnerDetails.country || 'Chưa cập nhật'}</p>
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-1.5 flex items-center gap-1"><Info className="w-3.5 h-3.5"/> SĐT</label>
                  <p className="text-[15px] font-bold text-gray-800">{selectedContact.phone || 'Chưa cập nhật'}</p>
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-1.5 flex items-center gap-1"><Globe className="w-3.5 h-3.5"/> Email</label>
                  <p className="text-[15px] font-bold text-[#004c91] truncate">{selectedContact.email || 'Chưa cập nhật'}</p>
                </div>
                <div className="col-span-2">
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-1.5 flex items-center gap-1"><Globe className="w-3.5 h-3.5"/> Website</label>
                  <a href={selectedContact.website || partnerDetails.website} target="_blank" rel="noopener noreferrer" className="text-[15px] font-bold text-[#00a651] hover:underline">
                    {selectedContact.website || partnerDetails.website}
                  </a>
                </div>
              </div>
            </div>
            
            <div className="p-5 border-t border-gray-100 bg-white flex justify-end">
              <button 
                onClick={() => setIsContactModalOpen(false)}
                className="px-6 py-2.5 bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold rounded-xl transition-colors outline-none cursor-pointer"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
      {/* Delete Contact Modal */}
      {isDeleteContactModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-sm overflow-hidden animate-in fade-in zoom-in-95 duration-200">
            <div className="p-6 text-center">
              <div className="w-16 h-16 bg-red-100 rounded-full flex items-center justify-center mx-auto mb-4">
                <Trash2 className="w-8 h-8 text-red-500" />
              </div>
              <h3 className="text-xl font-bold text-gray-900 mb-2">Xác nhận xóa</h3>
              <p className="text-gray-500 font-medium">Bạn có chắc chắn muốn xóa người liên hệ này? Hành động này không thể hoàn tác.</p>
            </div>
            
            <div className="p-4 border-t border-gray-100 bg-gray-50 flex justify-end gap-3">
              <button 
                onClick={() => { setIsDeleteContactModalOpen(false); setContactToDelete(null); }}
                className="px-4 py-2 bg-white hover:bg-gray-100 text-gray-700 font-bold rounded-xl transition-colors border border-gray-200 outline-none cursor-pointer"
              >
                Hủy
              </button>
              <button 
                onClick={confirmDeleteContact}
                className="px-4 py-2 bg-red-500 hover:bg-red-600 text-white font-bold rounded-xl transition-colors outline-none cursor-pointer shadow-sm shadow-red-200"
              >
                Xóa ngay
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
