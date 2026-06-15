/**
 * Trang AgendaTemplateManagement
 * Giao diện cài đặt và cấu trúc kế hoạch lịch trình mẫu mặc định theo chuyến đi.
 */

import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Home, ChevronRight, Plus, Trash2, Edit2, Save, X, Settings2, Clock, MapPin, User, FileText } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';

interface AgendaItem {
  id: string;
  time: string;
  activity: string;
  pic: string;
  location: string;
}

interface AgendaTemplate {
  id: string;
  name: string;
  type: string; // e.g. "Campus Tour", "Họp trao đổi", "Khách VIP"
  description: string;
  items: AgendaItem[];
}

const mockTemplates: AgendaTemplate[] = [
  {
    id: '1',
    name: 'Mẫu Campus Tour Chuẩn',
    type: 'Tham quan cơ sở vật chất (Campus Tour)',
    description: 'Chương trình tham quan cơ bản dành cho các đoàn học sinh THPT, thời lượng khoảng 2 tiếng.',
    items: [
      { id: '1-1', time: '08:30 - 09:00', activity: 'Đón khách tại sảnh chính, check-in', pic: 'Lễ tân, Support', location: 'Sảnh chính' },
      { id: '1-2', time: '09:00 - 09:45', activity: 'Giới thiệu chung về trường, chiếu video', pic: 'Cán bộ tuyển sinh', location: 'Phòng Hội thảo' },
      { id: '1-3', time: '09:45 - 10:30', activity: 'Tham quan thư viện, phòng thực hành, ký túc xá', pic: 'Sinh viên tình nguyện', location: 'Campus' },
      { id: '1-4', time: '10:30 - 10:45', activity: 'Giao lưu, giải đáp thắc mắc & tặng quà', pic: 'Cán bộ TS & CTV', location: 'Trống Đồng' },
    ]
  },
  {
    id: '2',
    name: 'Mẫu Họp Đối Tác - Ký Kết',
    type: 'Trao đổi hợp tác/Ký kết',
    description: 'Chương trình làm việc tiêu chuẩn cho các đoàn đối tác, doanh nghiệp.',
    items: [
      { id: '2-1', time: '14:00 - 14:15', activity: 'Đoàn đến, đón tiếp và hướng dẫn lên phòng VIP', pic: 'Lễ tân, Cán bộ đầu mối', location: 'Sảnh chính -> Phòng VIP' },
      { id: '2-2', time: '14:15 - 15:30', activity: 'Họp trao đổi nội dung hợp tác', pic: 'Ban Giám đốc, Trưởng phòng', location: 'Phòng họp VIP' },
      { id: '2-3', time: '15:30 - 16:00', activity: 'Ký kết biên bản ghi nhớ (MOU) & Chụp ảnh lưu niệm', pic: 'Lãnh đạo 2 bên, Media', location: 'Phòng họp VIP / Hội trường' },
      { id: '2-4', time: '16:00 - 16:30', activity: 'Tham quan không gian triển lãm (nếu có) & Tiễn khách', pic: 'Cán bộ đầu mối', location: 'Khu triển lãm -> Cổng chính' },
    ]
  }
];

export function AgendaTemplateManagement() {
  const navigate = useNavigate();
  const [templates, setTemplates] = useState<AgendaTemplate[]>(mockTemplates);
  const [activeTemplateId, setActiveTemplateId] = useState<string>(mockTemplates[0].id);
  const [isEditing, setIsEditing] = useState(false);
  const [editingTemplate, setEditingTemplate] = useState<AgendaTemplate | null>(null);

  const activeTemplate = templates.find(t => t.id === activeTemplateId);

  const handleEdit = () => {
    if (activeTemplate) {
      setEditingTemplate(JSON.parse(JSON.stringify(activeTemplate)));
      setIsEditing(true);
    }
  };

  const handleSave = () => {
    if (editingTemplate) {
      setTemplates(templates.map(t => t.id === editingTemplate.id ? editingTemplate : t));
      setIsEditing(false);
    }
  };

  const [isCreating, setIsCreating] = useState(false);

  const handleCreateNew = () => {
    const newTemplate: AgendaTemplate = {
      id: Date.now().toString(),
      name: 'Mẫu Agenda Mới',
      type: 'Loại hình tham quan',
      description: 'Mô tả ngắn gọn về mẫu agenda này...',
      items: [
        { id: Date.now().toString() + '-1', time: '08:00 - 09:00', activity: 'Đón khách', pic: 'Cán bộ phụ trách', location: 'Sảnh chính' }
      ]
    };
    setEditingTemplate(newTemplate);
    setIsCreating(true);
    setIsEditing(true);
  };

  const handleSaveNew = () => {
    if (editingTemplate) {
      setTemplates([...templates, editingTemplate]);
      setActiveTemplateId(editingTemplate.id);
      setIsEditing(false);
      setIsCreating(false);
    }
  };

  const handleCancel = () => {
    setIsEditing(false);
    setIsCreating(false);
    setEditingTemplate(null);
  };

  const handleAddItem = () => {
    if (editingTemplate) {
      setEditingTemplate({
        ...editingTemplate,
        items: [
          ...editingTemplate.items,
          { id: Date.now().toString(), time: '', activity: '', pic: '', location: '' }
        ]
      });
    }
  };

  const handleRemoveItem = (itemId: string) => {
    if (editingTemplate) {
      setEditingTemplate({
        ...editingTemplate,
        items: editingTemplate.items.filter(item => item.id !== itemId)
      });
    }
  };

  const handleUpdateItem = (itemId: string, field: keyof AgendaItem, value: string) => {
    if (editingTemplate) {
      setEditingTemplate({
        ...editingTemplate,
        items: editingTemplate.items.map(item => 
          item.id === itemId ? { ...item, [field]: value } : item
        )
      });
    }
  };

  return (
    <div className="flex-1 w-full bg-[#f8fbff] min-h-[calc(100vh-64px)]">
      {/* Breadcrumb */}
        <div className="mb-4 flex items-center text-sm font-medium text-gray-500 px-4 md:px-8 mt-4">
          <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Dashboard</button>
          <span className="mx-2">/</span>
          <button onClick={() => navigate('/dashboard/visit')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Quản lý campus</button>
          <span className="mx-2">/</span>
          <span className="text-[#004c91] font-bold">Quản lý mẫu Agenda</span>
        </div>

        <div className="max-w-[1400px] mx-auto px-4 md:px-8 pb-12 w-full">
          {/* Header */}
          <div className="mb-8 flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div>
              <h1 className="text-3xl font-bold text-[#004c91]">Quản lý mẫu Agenda</h1>
              <p className="text-gray-500 mt-2 font-medium">Tạo và quản lý các lịch trình mẫu cho từng loại hình tham quan.</p>
            </div>
            {!isEditing && (
              <button 
                onClick={handleCreateNew}
                className="flex items-center justify-center gap-2 bg-[#f37021] hover:bg-orange-600 outline-none text-white px-5 py-2.5 rounded-xl font-bold shadow-sm transition-colors w-full md:w-auto"
              >
                <Plus className="w-5 h-5" /> Thêm mẫu mới
              </button>
            )}
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
            {/* Left Column: Template List */}
            <div className={`lg:col-span-4 ${isEditing && 'opacity-50 pointer-events-none transition-opacity'}`}>
              <div className="bg-white rounded-2xl shadow-sm border border-gray-200 overflow-hidden">
                <div className="p-4 border-b border-gray-100 bg-gray-50/50">
                  <h3 className="font-bold text-gray-800 flex items-center gap-2">
                    <Settings2 className="w-5 h-5 text-[#004c91]" />
                    Danh sách mẫu
                  </h3>
                </div>
                <div className="p-2 space-y-1 max-h-[600px] overflow-y-auto">
                  {templates.map(template => (
                    <button
                      key={template.id}
                      onClick={() => setActiveTemplateId(template.id)}
                      className={`w-full text-left p-4 rounded-xl transition-all border outline-none ${
                        activeTemplateId === template.id 
                          ? 'bg-blue-50 border-[#004c91]/30 shadow-sm relative overflow-hidden' 
                          : 'bg-white border-transparent hover:bg-gray-50'
                      }`}
                    >
                      {activeTemplateId === template.id && (
                        <div className="absolute left-0 top-0 bottom-0 w-1 bg-[#004c91]" />
                      )}
                      <h4 className={`font-bold text-base mb-1 ${activeTemplateId === template.id ? 'text-[#004c91]' : 'text-gray-800'}`}>
                        {template.name}
                      </h4>
                      <div className="flex items-center gap-1.5 text-xs font-medium text-gray-500 mb-2">
                        <FileText className="w-3.5 h-3.5" />
                        <span className="truncate">{template.type}</span>
                      </div>
                      <p className="text-xs text-gray-500 line-clamp-2 leading-relaxed">
                        {template.description}
                      </p>
                    </button>
                  ))}
                </div>
              </div>
            </div>

            {/* Right Column: Template Detail / Edit */}
            <div className="lg:col-span-8">
              {!isEditing ? (
                <div className="bg-white rounded-2xl shadow-sm border border-gray-200 overflow-hidden h-full">
                  {activeTemplate ? (
                    <div className="p-6 md:p-8">
                      <div className="flex justify-between items-start mb-6">
                        <div>
                          <div className="inline-flex items-center gap-1.5 px-3 py-1 bg-gray-100 text-gray-600 rounded-full text-xs font-bold mb-3 border border-gray-200">
                            {activeTemplate.type}
                          </div>
                          <h2 className="text-2xl font-bold text-gray-900">{activeTemplate.name}</h2>
                          <p className="text-gray-500 mt-2 font-medium leading-relaxed">{activeTemplate.description}</p>
                        </div>
                        <button 
                          onClick={handleEdit}
                          className="flex items-center gap-1.5 px-4 py-2 bg-[#004c91]/10 text-[#004c91] font-bold rounded-xl hover:bg-[#004c91] hover:text-white transition-colors outline-none shrink-0"
                        >
                          <Edit2 className="w-4 h-4" /> Chỉnh sửa
                        </button>
                      </div>

                      <div className="mt-8">
                        <h3 className="text-lg font-bold text-gray-800 mb-4 border-b border-gray-100 pb-2">Chi tiết lịch trình</h3>
                        <div className="space-y-4">
                          {activeTemplate.items.map((item, index) => (
                            <div key={item.id} className="flex gap-4 p-4 rounded-xl border border-gray-100 bg-gray-50 hover:border-gray-200 transition-colors">
                              <div className="shrink-0 w-32 flex flex-col pt-0.5">
                                <span className="text-[#f37021] font-bold text-sm flex items-center gap-1.5">
                                  <Clock className="w-4 h-4" /> {item.time}
                                </span>
                              </div>
                              <div className="flex-1">
                                <h4 className="font-bold text-gray-900 mb-2">{item.activity}</h4>
                                <div className="flex flex-wrap gap-4 text-sm font-medium text-gray-500">
                                  <span className="flex items-center gap-1.5"><User className="w-4 h-4" /> {item.pic}</span>
                                  <span className="flex items-center gap-1.5"><MapPin className="w-4 h-4" /> {item.location}</span>
                                </div>
                              </div>
                            </div>
                          ))}
                        </div>
                      </div>
                    </div>
                  ) : (
                    <div className="flex flex-col items-center justify-center h-full min-h-[400px] text-gray-400">
                      <FileText className="w-16 h-16 text-gray-200 mb-4" />
                      <p className="font-medium">Vui lòng chọn hoặc tạo mẫu mới.</p>
                    </div>
                  )}
                </div>
              ) : (
                <div className="bg-white rounded-2xl shadow-sm border border-[#004c91]/20 overflow-hidden ring-4 ring-[#004c91]/5">
                  {editingTemplate && (
                    <div className="p-6 md:p-8">
                      <div className="flex justify-between items-center mb-6 border-b border-gray-100 pb-4">
                        <h2 className="text-xl font-bold text-[#004c91]">
                          {isCreating ? 'Tạo mẫu Agenda mới' : 'Chỉnh sửa mẫu Agenda'}
                        </h2>
                        <div className="flex items-center gap-2">
                          <button 
                            onClick={handleCancel}
                            className="px-4 py-2 rounded-xl font-bold text-gray-600 hover:bg-gray-100 transition-colors outline-none"
                          >
                            Hủy
                          </button>
                          <button 
                            onClick={isCreating ? handleSaveNew : handleSave}
                            className="px-5 py-2 flex items-center gap-2 bg-[#004c91] hover:bg-[#00386b] text-white font-bold rounded-xl shadow-sm transition-colors outline-none"
                          >
                            <Save className="w-4 h-4" /> Lưu
                          </button>
                        </div>
                      </div>

                      <div className="space-y-5">
                        <div>
                          <label className="block text-sm font-bold text-gray-700 mb-1.5">Tên mẫu <span className="text-red-500">*</span></label>
                          <input 
                            type="text" 
                            className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none font-medium text-gray-900 bg-white shadow-sm"
                            value={editingTemplate.name}
                            onChange={(e) => setEditingTemplate({...editingTemplate, name: e.target.value})}
                          />
                        </div>

                        <div>
                          <label className="block text-sm font-bold text-gray-700 mb-1.5">Loại hình tham quan <span className="text-red-500">*</span></label>
                          <select 
                            className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none font-medium text-gray-900 bg-white shadow-sm appearance-none"
                            value={editingTemplate.type}
                            onChange={(e) => setEditingTemplate({...editingTemplate, type: e.target.value})}
                          >
                            <option value="Tham quan cơ sở vật chất (Campus Tour)">Tham quan cơ sở vật chất (Campus Tour)</option>
                            <option value="Sinh hoạt chuyên đề/Workshop/Talkshow">Sinh hoạt chuyên đề/Workshop/Talkshow</option>
                            <option value="Trao đổi hợp tác/Ký kết">Trao đổi hợp tác/Ký kết</option>
                            <option value="Sự kiện/Lễ hội đặc biệt">Sự kiện/Lễ hội đặc biệt</option>
                            <option value="Khác">Khác</option>
                          </select>
                        </div>

                        <div>
                          <label className="block text-sm font-bold text-gray-700 mb-1.5">Mô tả chi tiết</label>
                          <textarea 
                            className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none font-medium text-gray-900 bg-white shadow-sm min-h-[80px]"
                            value={editingTemplate.description}
                            onChange={(e) => setEditingTemplate({...editingTemplate, description: e.target.value})}
                          />
                        </div>

                        <div className="pt-4 border-t border-gray-100">
                          <div className="flex items-center justify-between mb-4">
                            <h3 className="text-base font-bold text-gray-800">Các hoạt động trong lịch trình</h3>
                            <button 
                              type="button"
                              onClick={handleAddItem}
                              className="px-3 py-1.5 bg-[#f37021]/10 text-[#f37021] font-bold rounded-lg hover:bg-[#f37021] hover:text-white transition-colors flex items-center gap-1.5 text-sm"
                            >
                              <Plus className="w-4 h-4" /> Thêm hoạt động
                            </button>
                          </div>

                          <div className="space-y-3">
                            <AnimatePresence>
                              {editingTemplate.items.map((item, index) => (
                                <motion.div 
                                  key={item.id}
                                  initial={{ opacity: 0, height: 0, y: -10 }}
                                  animate={{ opacity: 1, height: 'auto', y: 0 }}
                                  exit={{ opacity: 0, height: 0, y: -10 }}
                                  className="relative group bg-gray-50 border border-gray-200 rounded-xl p-4 pr-12 focus-within:border-[#004c91]/50 focus-within:bg-white transition-colors"
                                >
                                  <div className="grid grid-cols-1 md:grid-cols-12 gap-4">
                                    <div className="md:col-span-3">
                                      <label className="block text-xs font-bold text-gray-500 mb-1">Thời gian</label>
                                      <input 
                                        type="text" 
                                        placeholder="08:00 - 09:00"
                                        className="w-full px-3 py-2 bg-white rounded-lg border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium"
                                        value={item.time}
                                        onChange={(e) => handleUpdateItem(item.id, 'time', e.target.value)}
                                      />
                                    </div>
                                    <div className="md:col-span-9">
                                      <label className="block text-xs font-bold text-gray-500 mb-1">Hoạt động / Nội dung</label>
                                      <input 
                                        type="text" 
                                        placeholder="Đón khách tại sảnh chính..."
                                        className="w-full px-3 py-2 bg-white rounded-lg border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium"
                                        value={item.activity}
                                        onChange={(e) => handleUpdateItem(item.id, 'activity', e.target.value)}
                                      />
                                    </div>
                                    <div className="md:col-span-6">
                                      <label className="block text-xs font-bold text-gray-500 mb-1">Người phụ trách (PIC)</label>
                                      <input 
                                        type="text" 
                                        placeholder="Lễ tân, Cán bộ..."
                                        className="w-full px-3 py-2 bg-white rounded-lg border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium"
                                        value={item.pic}
                                        onChange={(e) => handleUpdateItem(item.id, 'pic', e.target.value)}
                                      />
                                    </div>
                                    <div className="md:col-span-6">
                                      <label className="block text-xs font-bold text-gray-500 mb-1">Địa điểm</label>
                                      <input 
                                        type="text" 
                                        placeholder="Phòng VIP, Sảnh chính..."
                                        className="w-full px-3 py-2 bg-white rounded-lg border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium"
                                        value={item.location}
                                        onChange={(e) => handleUpdateItem(item.id, 'location', e.target.value)}
                                      />
                                    </div>
                                  </div>
                                  <button 
                                    onClick={() => handleRemoveItem(item.id)}
                                    className="absolute right-3 top-1/2 -translate-y-1/2 p-2 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors outline-none"
                                  >
                                    <Trash2 className="w-5 h-5" />
                                  </button>
                                </motion.div>
                              ))}
                            </AnimatePresence>
                            {editingTemplate.items.length === 0 && (
                              <div className="text-center py-8 border-2 border-dashed border-gray-200 rounded-xl">
                                <p className="text-sm font-medium text-gray-500">Chưa có hoạt động nào. Hãy thêm hoạt động để hoàn thành mẫu agenda.</p>
                              </div>
                            )}
                          </div>
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
      </div>
    </div>
  );
}
