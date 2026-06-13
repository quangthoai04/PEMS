/**
 * Trang EditNews
 * Form cập nhật thông tin và sửa bài báo hiển thị ở trang mạng nền tảng.
 */

// Đây là trang chỉnh sửa một bài viết tin tức đã có trong khu vực quản trị
import React, { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { motion } from 'motion/react';
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';
import { UploadCloud, Plus, Undo2 } from 'lucide-react';
import banner02 from '../../../assets/images/banner02.png';

export function EditNews() {
  const navigate = useNavigate();
  const { id } = useParams();

  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const isStudent = user?.role?.toUpperCase() === 'STUDENT';

  // Fake pre-filled data
  const [title, setTitle] = useState('Đại chiến giữa các game thủ chuyên nghiệp tại giải đấu mùa xuân');
  const [description, setDescription] = useState('Trận đấu nghẹt thở đã diễn ra với sự góp mặt của những tuyển thủ hàng đầu thế giới, mang đến một màn trình diễn mãn nhãn cho khán giả.');
  const [campus, setCampus] = useState('Hà Nội'); // Fake pre-filled data
  const [imagePreview, setImagePreview] = useState<string | null>(banner02);
  const isHO = user?.role?.toUpperCase() === 'HO';
  
  const handleImageUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      const url = URL.createObjectURL(file);
      setImagePreview(url);
    }
  };

  const [contents, setContents] = useState([
    { id: 1, heading: 'Khởi đầu đầy kịch tính', description: 'Hai đội tuyển bước vào trận đấu với tinh thần quyết tâm cao. Những pha xử lý kỹ năng cá nhân điêu luyện đã xuất hiện ngay từ những phút đầu tiên.' },
    { id: 2, heading: 'Bước ngoặt của trận đấu', description: 'Một pha giao tranh bất ngờ ở khu vực Rồng đã làm thay đổi hoàn toàn cục diện, giúp đội xanh có được lợi thế khổng lồ về tài nguyên và tinh thần.' },
    { id: 3, heading: 'Chung cuộc và những dư âm', description: 'Sau 45 phút căng thẳng, đội xanh đã phá vỡ nhà chính của đối phương, giành lấy tấm vé bước tiếp vào vòng bán kết.' }
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
      className="p-4 sm:p-6 md:p-8 pb-12 max-w-5xl mx-auto"
    >
      <div className="mb-6 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors">Dashboard</button>
        <span className="mx-2">/</span>
        <button onClick={() => navigate('/dashboard/news')} className="hover:text-[#004c91] transition-colors">Quản lý tin tức</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Chỉnh sửa tin tức</span>
      </div>

      <div className="mb-8">
        <h1 className="text-3xl font-bold text-[#004c91]">Chỉnh sửa tin tức</h1>
      </div>

      <div className="space-y-8">
        {/* Section 1: LOẠI TIN & THÔNG TIN CƠ BẢN */}
        <section className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
          <div className="bg-[#004c91] px-6 py-3.5">
            <h2 className="text-[15px] font-bold text-white uppercase tracking-wide">1. THÔNG TIN CƠ BẢN</h2>
          </div>
          
          <div className="p-6 flex flex-col gap-6">
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
            Lưu thay đổi
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
