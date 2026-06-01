// Đây là trang tạo mới một bài viết tin tức trong khu vực quản trị
import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'motion/react';
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';
import { UploadCloud, Plus, Undo2 } from 'lucide-react';

export function CreateNews() {
  const navigate = useNavigate();
  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const isStudent = user?.role?.toUpperCase() === 'STUDENT';
  
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [newsType, setNewsType] = useState(isStudent ? 'Review' : 'News');
  const [campus, setCampus] = useState('');
  const [imagePreview, setImagePreview] = useState<string | null>(null);
  const isHO = user?.role?.toUpperCase() === 'HO';
  
  const handleImageUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      const url = URL.createObjectURL(file);
      setImagePreview(url);
    }
  };

  const [contents, setContents] = useState([
    { id: 1, heading: '', description: '' }
  ]);

  const addContent = () => {
    if (contents.length < 10) {
      setContents([...contents, { id: Date.now(), heading: '', description: '' }]);
    }
  };

  const updateContent = (index: number, field: 'heading' | 'description', value: string) => {
    const newContents = [...contents];
    newContents[index][field] = value;
    setContents(newContents);
  };

  const [contentToDelete, setContentToDelete] = useState<number | null>(null);

  const confirmDelete = () => {
    if (contentToDelete !== null) {
      const newContents = [...contents];
      newContents.splice(contentToDelete, 1);
      setContents(newContents);
      setContentToDelete(null);
    }
  };

  return (
    <motion.div 
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      transition={{ duration: 0.3 }}
      className="p-8 pb-12 max-w-5xl mx-auto"
    >
      <div className="mb-6 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors">Dashboard</button>
        <span className="mx-2">/</span>
        <button onClick={() => navigate('/dashboard/news')} className="hover:text-[#004c91] transition-colors">Quản lý tin tức</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Tạo tin mới</span>
      </div>

      <div className="mb-8">
        <h1 className="text-3xl font-bold text-[#004c91]">Tạo tin tức</h1>
      </div>

      <div className="space-y-8">
        {/* Section 1: LOẠI TIN & THÔNG TIN CƠ BẢN */}
        <section className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
          <div className="bg-[#004c91] px-6 py-3.5">
            <h2 className="text-[15px] font-bold text-white uppercase tracking-wide">1. LOẠI TIN & THÔNG TIN CƠ BẢN</h2>
          </div>
          
          <div className="p-6 flex flex-col gap-6">
            {/* Loại Tin Tức */}
            <div>
              <label className="block text-gray-900 font-bold mb-3">
                Loại tin tức<span className="text-red-500 ml-1">*</span>
              </label>
              <div className="flex items-center gap-6">
                {!isStudent && (
                  <label className="flex items-center gap-2 cursor-pointer group">
                    <div className="w-5 h-5 rounded-full border-2 border-gray-300 flex items-center justify-center group-hover:border-[#004c91] transition-colors relative">
                      {newsType === 'News' && <div className="w-2.5 h-2.5 bg-[#004c91] rounded-full"></div>}
                    </div>
                    <input type="radio" className="hidden" name="newsType" value="News" checked={newsType === 'News'} onChange={() => setNewsType('News')} />
                    <span className="font-medium text-gray-700">News</span>
                  </label>
                )}
                <label className="flex items-center gap-2 cursor-pointer group">
                  <div className="w-5 h-5 rounded-full border-2 border-gray-300 flex items-center justify-center group-hover:border-[#004c91] transition-colors relative">
                    {newsType === 'Review' && <div className="w-2.5 h-2.5 bg-[#004c91] rounded-full"></div>}
                  </div>
                  <input type="radio" className="hidden" name="newsType" value="Review" checked={newsType === 'Review'} onChange={() => setNewsType('Review')} />
                  <span className="font-medium text-gray-700">Review</span>
                </label>
              </div>
            </div>

            {/* Cơ Sở (For HO) */}
            {isHO && (
              <div>
                <label className="block text-gray-900 font-bold mb-2">
                  Cơ sở<span className="text-red-500 ml-1">*</span>
                </label>
                <select
                  value={campus}
                  onChange={(e) => setCampus(e.target.value)}
                  className="w-full px-4 py-3 border border-gray-300 rounded-xl focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] hover:border-[#004c91] transition-colors text-gray-800 bg-white"
                >
                  <option value="" disabled>Chọn cơ sở</option>
                  <option value="Hà Nội">Hà Nội</option>
                  <option value="Hồ Chí Minh">Hồ Chí Minh</option>
                  <option value="Đà Nẵng">Đà Nẵng</option>
                  <option value="Cần Thơ">Cần Thơ</option>
                  <option value="Quy Nhơn">Quy Nhơn</option>
                </select>
              </div>
            )}

            {/* Tiêu Đề Tin Tức */}
            <div>
              <label className="block text-gray-900 font-bold mb-2">
                Tiêu đề tin tức<span className="text-red-500 ml-1">*</span>
              </label>
              <div className="relative">
                <input 
                  type="text" 
                  maxLength={150}
                  placeholder="Nhập tiêu đề tin tức..."
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  className="w-full pr-16 pl-4 py-3 border border-gray-300 rounded-xl focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] hover:border-[#004c91] transition-colors text-gray-800"
                />
                <span className="absolute right-4 top-1/2 -translate-y-1/2 text-sm text-gray-400 font-medium">
                  {title.length}/150
                </span>
              </div>
            </div>

            {/* Mô Tả Ngắn */}
            <div>
              <label className="block text-gray-900 font-bold mb-2">
                Mô tả ngắn<span className="text-red-500 ml-1">*</span>
              </label>
              <textarea 
                rows={3}
                maxLength={250}
                placeholder="Nhập mô tả ngắn gọn..."
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                className="w-full p-4 border border-gray-300 rounded-xl focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] hover:border-[#004c91] transition-colors text-gray-800 resize-none"
              ></textarea>
              <div className="flex justify-end mt-1 text-sm text-gray-400 font-medium">
                {description.length}/250
              </div>
            </div>
          </div>
        </section>

        {/* Section 2: ẢNH ĐẠI DIỆN */}
        <section className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
          <div className="bg-[#004c91] px-6 py-3.5">
            <h2 className="text-[15px] font-bold text-white uppercase tracking-wide">2. ẢNH ĐẠI DIỆN</h2>
          </div>
          <div className="p-6">
            <label className="block w-full">
              <input type="file" accept="image/png, image/jpeg, image/jpg" className="hidden" onChange={handleImageUpload} />
              <div className={`bg-[#eef5fa] border-2 border-dashed border-[#b6d4f0] rounded-xl flex flex-col items-center justify-center text-center cursor-pointer hover:bg-[#e4f0fa] transition-colors group relative overflow-hidden ${imagePreview ? 'p-2' : 'p-12 min-h-[300px]'}`}>
                {imagePreview ? (
                   <img src={imagePreview} alt="Preview" className="w-full max-h-[400px] object-contain rounded-lg" />
                ) : (
                  <>
                    <div className="w-16 h-16 bg-white rounded-full flex items-center justify-center mb-4 shadow-sm group-hover:scale-110 transition-transform">
                      <UploadCloud className="w-8 h-8 text-[#004c91]" />
                    </div>
                    <h3 className="text-lg font-bold text-[#004c91] mb-1">Kéo thả ảnh vào đây</h3>
                    <p className="text-gray-500 text-sm">Hoặc click để tải ảnh lên (PNG, JPG, JPEG - Max 5MB)</p>
                  </>
                )}
              </div>
            </label>
          </div>
        </section>

        {/* Section 3: NỘI DUNG CHI TIẾT */}
        <section className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
          <div className="bg-[#004c91] px-6 py-3.5">
            <h2 className="text-[15px] font-bold text-white uppercase tracking-wide">3. NỘI DUNG CHI TIẾT</h2>
          </div>

          <div className="p-6 flex flex-col gap-8">
            {contents.map((content, index) => (
              <div key={content.id} className="relative">
                {index > 0 && <div className="h-px bg-gray-100 w-full mb-8"></div>}
                <div className="flex flex-col gap-6">
                  {/* Heading N */}
                  <div>
                    <div className="flex items-center justify-between mb-2">
                      <label className="block text-gray-900 font-bold">
                        Tiêu đề {index + 1}<span className="text-red-500 ml-1">*</span>
                      </label>
                      <div className="flex items-center gap-2">
                        {index > 0 && (
                          <button 
                            onClick={() => setContentToDelete(index)}
                            className="flex items-center gap-1.5 bg-red-50 hover:bg-red-100 text-red-600 px-3 py-1.5 rounded-lg text-sm font-bold transition-colors"
                          >
                            <Undo2 className="w-4 h-4" />
                            Xóa
                          </button>
                        )}
                        {index === contents.length - 1 && contents.length < 10 && (
                          <button 
                            onClick={addContent}
                            className="flex items-center gap-1.5 bg-[#004c91] hover:bg-[#003a70] text-white px-3 py-1.5 rounded-lg text-sm font-bold transition-colors shadow-sm"
                          >
                            <Plus className="w-4 h-4" />
                            Thêm nội dung
                          </button>
                        )}
                      </div>
                    </div>
                    <input 
                      type="text" 
                      placeholder="Nhập tiêu đề nội dung..."
                      value={content.heading}
                      onChange={(e) => updateContent(index, 'heading', e.target.value)}
                      className="w-full px-4 py-3 border border-gray-300 rounded-xl focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] hover:border-[#004c91] transition-colors text-gray-800"
                    />
                  </div>

                  {/* Description N */}
                  <div>
                    <label className="block text-gray-900 font-bold mb-2">
                      Miêu tả<span className="text-red-500 ml-1">*</span>
                    </label>
                    <div className="border border-gray-300 rounded-lg overflow-hidden focus-within:border-[#004c91] focus-within:ring-1 focus-within:ring-[#004c91] transition-colors group">
                      {/* Editor area */}
                      {/* @ts-ignore */}
                      <ReactQuill
                        theme="snow"
                        value={content.description}
                        onChange={(value) => updateContent(index, 'description', value)}
                        placeholder="Nhập nội dung chi tiết..."
                        className="bg-white custom-quill-no-border"
                        modules={{
                          toolbar: [
                            ['bold', 'italic', 'underline', 'strike'],
                            [{ 'align': [] }],
                            [{ 'list': 'ordered'}, { 'list': 'bullet' }],
                            ['link', 'image'],
                            ['clean']
                          ],
                        }}
                      />
                    </div>
                  </div>
                </div>
              </div>
            ))}
            
            <div className="text-right text-sm text-gray-500 italic mt-6">
              Đã tạo {contents.length}/10 nội dung
            </div>
          </div>
        </section>

        {/* Buttons */}
        <div className="flex items-center justify-end gap-3 pt-6 border-t border-gray-200">
          <button 
            onClick={() => navigate('/dashboard/news')}
            className="px-6 py-2.5 rounded-xl border border-gray-300 text-gray-700 font-bold hover:border-[#004c91] hover:text-[#004c91] transition-colors"
          >
            Quay lại
          </button>
          
          <button 
            className="px-8 py-2.5 text-white bg-[#f37021] rounded-xl font-bold hover:-translate-y-1 hover:shadow-lg transition-all duration-300"
          >
            Đăng Tin
          </button>
        </div>
      </div>

      {/* Delete Confirmation Modal */}
      {contentToDelete !== null && (
        <div className="fixed inset-0 z-50 flex items-center justify-center">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setContentToDelete(null)} />
          <motion.div 
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            className="bg-white rounded-2xl p-6 max-w-md w-full mx-4 relative z-10 shadow-xl"
          >
            <h3 className="text-xl font-bold text-gray-900 mb-2">Xác nhận hoàn tác</h3>
            <p className="text-gray-600 mb-6 leading-relaxed">
              Bạn có chắc chắn muốn xóa bỏ nội dung của <strong>Tiêu đề {contentToDelete + 1}</strong> không? Hành động này sẽ làm mất dữ liệu bạn đã nhập cho phần này.
            </p>
            <div className="flex gap-3 justify-end">
              <button 
                onClick={() => setContentToDelete(null)}
                className="px-5 py-2 rounded-xl text-gray-600 font-bold hover:bg-gray-100 transition-colors"
              >
                Hủy
              </button>
              <button 
                onClick={confirmDelete}
                className="px-5 py-2 rounded-xl bg-red-600 text-white font-bold hover:bg-red-700 transition-colors shadow-sm"
              >
                Xác nhận xóa
              </button>
            </div>
          </motion.div>
        </div>
      )}
    </motion.div>
  );
}
