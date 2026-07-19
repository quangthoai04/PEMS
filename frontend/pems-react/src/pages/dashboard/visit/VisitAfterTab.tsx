/**
 * Component VisitAfterTab
 * Module hậu cần sau diễn ra thăm quan thực địa.
 */

import React, { useState, useRef, useEffect } from 'react';
import { 
  Upload, Image as ImageIcon, Sparkles, User, Tag, 
  FileText, Link2, Globe, CheckCircle2, AlertCircle, 
  ArrowRight, FolderOpen, ExternalLink, RefreshCw, 
  Search, Check, Trash2, Camera, Plus, Minimize2, ZoomIn, X
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useNavigate } from 'react-router-dom';
import { useAuthContext } from '../../../shared/auth/AuthContext';
import { VisitNewsSection } from './VisitNewsSection';
import { LogisticsHandoverSection } from '../../../features/delegations/components/LogisticsHandoverSection';
import { GeneralExpensePanel } from './GeneralExpensePanel';

// Default Delegation Members for tag dropdown and display
const DEFAULT_GUESTS = [
  { id: 'g1', name: 'Prof. Kenji Takahashi', role: 'Head of Delegation', cardVisit: 'Card Visit #1' },
  { id: 'g2', name: 'Dr. Yoko Tanaka', role: 'Senior Academic Partner', cardVisit: 'Card Visit #2' },
  { id: 'g3', name: 'Mr. Akira Sato', role: 'International Relations Coordinator', cardVisit: 'Card Visit #3' },
  { id: 'g4', name: 'Nguyễn Văn A', role: 'Chủ trì đón tiếp (FPT Host)', cardVisit: 'FPT Staff Card' },
  { id: 'g5', name: 'Trần Thị B', role: 'Hỗ trợ sự kiện (IC Staff)', cardVisit: 'FPT IC Card' }
];

// Presets of group photos with predefined face coordinate estimates
const PRESET_PHOTOS = [
  {
    id: 'preset1',
    url: '/src/assets/FPTbanner_visit/5CS.png',
    name: 'Đoàn Tokyo tại sảnh tòa nhà Alpha',
    faces: [
      { id: 'f1', x: 28, y: 35, width: 10, height: 13, taggedUser: null },
      { id: 'f2', x: 45, y: 32, width: 10, height: 13, taggedUser: null },
      { id: 'f3', x: 62, y: 36, width: 10, height: 13, taggedUser: null }
    ]
  },
  {
    id: 'preset2',
    url: 'https://images.unsplash.com/photo-1543269865-cbf427effbad?auto=format&fit=crop&w=1200&q=80',
    name: 'Họp song phương tại phòng VIP 1',
    faces: [
      { id: 'f4', x: 32, y: 28, width: 11, height: 14, taggedUser: null },
      { id: 'f5', x: 50, y: 25, width: 11, height: 14, taggedUser: null }
    ]
  }
];

interface VisitAfterTabProps {
  onTourCloseSuccess?: () => void;
  isReadOnly?: boolean;
  isDept?: boolean;
  visitInstanceId?: number;
}

export function VisitAfterTab({ onTourCloseSuccess, isReadOnly = false, isDept = false, visitInstanceId }: VisitAfterTabProps) {
  const navigate = useNavigate();
  const { user } = useAuthContext();
  const roleCode = (user?.roleCode || '').toUpperCase();
  const isStudent = roleCode === 'STUDENT' || roleCode === 'VISITOR';

  // Part 1: Images state
  const [uploadedImages, setUploadedImages] = useState<Array<{
    id: string;
    url: string;
    name: string;
    faces: Array<{
      id: string;
      x: number; // percentage
      y: number; // percentage
      width: number;
      height: number;
      taggedUser: string | null; // Guest ID
    }>;
    isScanning?: boolean;
    isScanned?: boolean;
  }>>(() => [
    {
      id: 'img-1',
      url: PRESET_PHOTOS[0].url,
      name: PRESET_PHOTOS[0].name,
      faces: PRESET_PHOTOS[0].faces.map((f, idx) => ({
        ...f,
        taggedUser: isReadOnly ? (idx === 0 ? 'g1' : idx === 1 ? 'g2' : 'g3') : null
      })),
      isScanned: isReadOnly,
      isScanning: false
    }
  ]);

  const [selectedImageId, setSelectedImageId] = useState<string>('img-1');
  const [driveConfig, setDriveConfig] = useState({
    isConnected: true,
    folderName: 'IC_Visits_Archive_2026/Tokyo_Delegation',
    folderUrl: 'https://drive.google.com/drive/folders/1nme7TcwWStEizpT1RUplWjCBOqWki7aG?usp=sharing',
    syncStatus: 'synced', // 'synced' | 'syncing' | 'error'
    lastSynced: 'Vừa xong'
  });
  const [isDriveConfirmed, setIsDriveConfirmed] = useState(isReadOnly);

  const [searchGuestKeyword, setSearchGuestKeyword] = useState('');
  const [activeFaceSelection, setActiveFaceSelection] = useState<{ imgId: string, faceId: string } | null>(null);

  // Part 2: News state
  const [newsTitleVi, setNewsTitleVi] = useState('Nâng tầm hợp tác đào tạo cùng Đại học Tokyo: Chuyến thăm thắt chặt tình hữu nghị');
  const [newsTitleEn, setNewsTitleEn] = useState('Elevating Academic Training Collaboration with University of Tokyo: A Warm Visit');
  const [meetingMinutesSummary, setMeetingMinutesSummary] = useState(
    'Biên bản cuộc họp (15/10/2026):\n' +
    '1. Đại học Tokyo nhất trí trao đổi 15 sinh viên sang FPT học tập kỳ chuyên ngành.\n' +
    '2. FPT Campus Tour được triển khai trọn vẹn, gây ấn tượng sâu sắc về cơ sở vật chất.\n' +
    '3. Thống nhất biên soạn giáo trình chung cho ngành Công nghệ Bán dẫn.'
  );
  const [newsContentVi, setNewsContentVi] = useState('');
  const [newsContentEn, setNewsContentEn] = useState('');
  const [fbPostLink, setFbPostLink] = useState('');
  const [attachedImageIds, setAttachedImageIds] = useState<string[]>(['img-1']);

  // "Lưu ý" info modal + the dept article-preview modal. The real "đóng đoàn" CTA lives ONLY in the
  // VisitProcess stage bar (single source of truth); this tab no longer owns a close button so the
  // same action can never appear twice on one tab (đặc tả mục 1.6 / 8.3).
  const [showNoticeModal, setShowNoticeModal] = useState(false);
  const [isArticleModalOpen, setIsArticleModalOpen] = useState(false);

  // States for interactive scan UI
  const selectedImage = uploadedImages.find(img => img.id === selectedImageId);

  // Handle auto-writing News content based on Meeting Minutes
  const handleAutoGenerateNews = () => {
    // VI contents auto filled using Meeting Minutes
    const viGenerated = `Đại học FPT vừa qua đã vinh dự đón tiếp Đoàn đối tác cấp cao từ Đại học Tokyo, Nhật Bản đến tham quan và làm việc.\n\n` +
      `Theo Biên bản cuộc họp, hai bên đã thống nhất những điều khoản quan trọng trong chương trình nâng tầm hợp tác quốc tế. Đặc biệt: \n` +
      `- Triển khai chương trình trao đổi 15 sinh viên ưu tú sang học tập kỳ chuyên ngành tại cơ sở của FPT.\n` +
      `- Hoạt động Campus Tour diễn ra thành công ấn tượng, giúp đoàn thấu hiểu sâu sắc hơn về hạ tầng giảng dạy kỹ thuật tốt bậc nhất.\n` +
      `- Nhất trí cùng phối hợp thiết kế chương trình học cho lĩnh vực Đào tạo Công nghệ Vi mạch Bán dẫn mới.\n\n` +
      `Buổi họp trao đổi khép lại trong không khí hữu nghị, mở ra tương lai hợp tác rạng dỡ giữa hai đơn vị hàng đầu Việt Nam và xứ sở hoa anh đào.`;

    // EN contents auto filled using Meeting Minutes
    const enGenerated = `FPT University recently had the honor of welcoming a high-ranking delegation from the University of Tokyo, Japan for a productive visit.\n\n` +
      `Based on the official meeting minutes, both parties have aligned on strategic cooperation milestones. Highlights include:\n` +
      `- A mutual commitment to launch a high-caliber exchange academic plan for 15 distinguished students.\n` +
      `- An impressive Campus Tour that showcased FPT University's world-class facilities.\n` +
      `- Mutual agreement to collaborative design and deliver academic curricula in Semiconductor Engineering.\n\n` +
      `The bilateral discussion concluded on a highly promising note, fostering international excellence and long-term relations.`;

    setNewsContentVi(viGenerated);
    setNewsContentEn(enGenerated);
  };

  // Run initial news generation
  useEffect(() => {
    if (!newsContentVi) {
      handleAutoGenerateNews();
    }
  }, []);

  // Handle custom upload simulation
  const fileInputRef = useRef<HTMLInputElement>(null);
  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      const file = e.target.files[0];
      const imageUrl = URL.createObjectURL(file);
      const newImgId = 'upload-' + Date.now();
      
      const newImage = {
        id: newImgId,
        url: imageUrl,
        name: file.name,
        faces: [
          { id: 'fu-1', x: 25, y: 30, width: 12, height: 15, taggedUser: null },
          { id: 'fu-2', x: 55, y: 28, width: 12, height: 15, taggedUser: null },
          { id: 'fu-3', x: 40, y: 45, width: 12, height: 15, taggedUser: null }
        ],
        isScanned: false,
        isScanning: false
      };

      setUploadedImages(prev => [...prev, newImage]);
      setSelectedImageId(newImgId);
      
      // Auto-trigger sync logging
      setDriveConfig(prev => ({
        ...prev,
        syncStatus: 'syncing',
        lastSynced: 'Đang đồng bộ tập tin...'
      }));
      
      setTimeout(() => {
        setDriveConfig(prev => ({
          ...prev,
          syncStatus: 'synced',
          lastSynced: 'Đã đồng bộ lên Drive vừa xong'
        }));
      }, 1500);
    }
  };

  // Simulate scanning of faces
  const handleTriggerScan = (imgId: string) => {
    setUploadedImages(prev => prev.map(img => {
      if (img.id === imgId) {
        return { ...img, isScanning: true };
      }
      return img;
    }));

    setTimeout(() => {
      setUploadedImages(prev => prev.map(img => {
        if (img.id === imgId) {
          return { ...img, isScanning: false, isScanned: true };
        }
        return img;
      }));
    }, 1800);
  };

  // Tag guest to face
  const handleTagGuest = (faceId: string, guestId: string | null) => {
    setUploadedImages(prev => prev.map(img => {
      if (img.id === selectedImageId) {
        return {
          ...img,
          faces: img.faces.map(face => {
            if (face.id === faceId) {
              return { ...face, taggedUser: guestId };
            }
            return face;
          })
        };
      }
      return img;
    }));
    setActiveFaceSelection(null);
  };

  // Remove uploaded image
  const handleDeleteImage = (imgId: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (uploadedImages.length <= 1) {
      alert("Cần giữ lại tối thiểu 1 ảnh chụp đoàn khách để hoàn tất quy trình (Bắt buộc tối thiểu 1 ảnh).");
      return;
    }
    const filtered = uploadedImages.filter(img => img.id !== imgId);
    setUploadedImages(filtered);
    setAttachedImageIds(prev => prev.filter(id => id !== imgId));
    if (selectedImageId === imgId) {
      setSelectedImageId(filtered[0].id);
    }
  };

  // Handle Toggle image attachment to News Post
  const handleToggleAttachImage = (imgId: string) => {
    if (attachedImageIds.includes(imgId)) {
      setAttachedImageIds(prev => prev.filter(id => id !== imgId));
    } else {
      setAttachedImageIds(prev => [...prev, imgId]);
    }
  };

  return (
    <div className="space-y-8 text-left">

      {/* Ký trả tài sản hậu cần — phần đầu tiên của tab Sau tiếp khách (real handover API). */}
      {visitInstanceId && !isStudent && (
        <LogisticsHandoverSection visitInstanceId={visitInstanceId} canManage={!isReadOnly && !isDept} handoverPhase="RETURN" />
      )}

      {/* Chi phí chung (General Expense) */}
      {visitInstanceId && !isStudent && !isDept && (
        <GeneralExpensePanel visitInstanceId={visitInstanceId} isReadOnly={isReadOnly} />
      )}

      {/* SECTION 1: PHOTO ALBUM & FACE SCANNING */}
      <div className="bg-white rounded-[2rem] border border-gray-200 shadow-sm overflow-hidden flex flex-col">
        
        {/* Title layer with traditional blue background */}
        <div className="bg-[#004c91] p-6 sm:p-8 text-white flex flex-col lg:flex-row justify-between items-start lg:items-center gap-4">
          <div>
            <div className="flex items-center gap-3">
              <span className="w-8 h-8 rounded-full bg-[#f37021] flex items-center justify-center text-sm font-black text-white">1</span>
              <h2 className="text-xl font-bold text-white">Lưu trữ ảnh của đoàn khách</h2>
            </div>
            <p className="text-xs text-blue-100/80 mt-1 pl-11 font-medium">
              Bắt buộc tải lên tối thiểu 1 ảnh thực tế diễn ra tiếp khách (với mọi đoàn không có media) để lưu trữ minh chứng.
            </p>
          </div>
        </div>

        {/* Content area */}
        <div className="p-6 sm:p-8 space-y-8">

          {/* SUBSECTION 1: GOOGLE DRIVE */}
          <div className="space-y-4">
            <div className="flex items-center gap-2.5">
              <span className="w-6 h-6 rounded-lg bg-[#004c91]/10 flex items-center justify-center text-xs font-bold text-[#004c91]">1</span>
              <h3 className="text-base font-semibold text-[#004c91]">Lưu trữ ảnh cho Google Drive</h3>
            </div>
            
            {/* GOOGLE DRIVE LINK & CONFIRMATION BLOCK */}
            <div className="bg-gradient-to-r from-blue-50 to-indigo-50/40 border border-blue-100 rounded-2xl p-6 shadow-sm flex flex-col md:flex-row items-stretch md:items-center justify-between gap-6 font-sans">
              <div className="flex items-start sm:items-center gap-4">
                <div className="w-12 h-12 rounded-xl bg-blue-100 flex items-center justify-center text-[#004c91] shrink-0 border border-blue-200 shadow-sm">
                  <FolderOpen className="w-6 h-6" />
                </div>
                <div className="text-left flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <h4 className="font-bold text-gray-800 text-sm sm:text-base">Thư mục Google Drive liên kết</h4>
                    <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-[10px] font-extrabold bg-emerald-100 text-emerald-800 border border-emerald-200">
                      <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
                      Thời gian thực
                    </span>
                  </div>
                  <p className="text-xs text-gray-500 mt-1 truncate" title={driveConfig.folderName}>
                    Thư mục: <span className="font-mono font-bold text-gray-700">{driveConfig.folderName}</span>
                  </p>
                  <div className="flex items-center gap-3 mt-2">
                    <a 
                      href={driveConfig.folderUrl} 
                      target="_blank" 
                      rel="noreferrer" 
                      className="text-[#004c91] hover:text-[#00386b] hover:underline font-extrabold text-xs inline-flex items-center gap-1 bg-white hover:bg-slate-50 px-3 py-1.5 border border-gray-200 rounded-lg shadow-sm transition-colors cursor-pointer"
                    >
                      Mở link Drive <ExternalLink className="w-3.5 h-3.5" />
                    </a>
                    <span className="text-gray-300 text-xs">|</span>
                    <span className="text-gray-500 text-xs flex items-center gap-1 font-semibold">
                      Đồng bộ: 
                      {driveConfig.syncStatus === 'syncing' ? (
                        <RefreshCw className="w-3.5 h-3.5 animate-spin text-[#004c91]" />
                      ) : (
                        <span className="text-emerald-600 font-bold">{driveConfig.lastSynced}</span>
                      )}
                    </span>
                  </div>
                </div>
              </div>

              {!isDept && (
                <div className={`p-4 rounded-xl border transition-all duration-200 shrink-0 md:max-w-xs xl:max-w-sm flex items-center ${
                  isDriveConfirmed 
                    ? 'bg-emerald-50/70 border-emerald-200 hover:border-emerald-300 shadow-inner' 
                    : 'bg-white border-gray-200 hover:border-gray-300'
                } shadow-sm`}>
                  <label className="flex items-center gap-3.5 cursor-pointer select-none w-full">
                    <input 
                      type="checkbox" 
                      checked={isDriveConfirmed}
                      disabled={isReadOnly}
                      onChange={(e) => setIsDriveConfirmed(e.target.checked)}
                      className={`w-5 h-5 rounded border-gray-300 text-[#004c91] focus:ring-[#004c91] cursor-pointer transition-transform duration-100 ${isReadOnly ? 'cursor-not-allowed opacity-50' : 'active:scale-95'}`}
                    />
                    <div className="text-left font-sans">
                      <p className="text-xs sm:text-sm font-extrabold text-gray-800 leading-snug whitespace-nowrap">Xác nhận lưu trữ đầy đủ</p>
                      <p className="text-[10px] text-gray-400 font-semibold mt-0.5 leading-normal">
                        Đã đồng bộ ảnh & biên bản lên Drive
                      </p>
                    </div>
                  </label>
                </div>
              )}
            </div>
          </div>

          {/* DISTINCT SEPARATOR LINE */}
          {!isDept && !isStudent && (
            <>
              <div className="py-2">
                <hr className="border-t border-gray-200" />
              </div>

              {/* SUBSECTION 2: FACE SCANNING */}
          <div className="space-y-4">
            <div className="flex items-center gap-2.5">
              <span className="w-6 h-6 rounded-lg bg-[#004c91]/10 flex items-center justify-center text-xs font-bold text-[#004c91]">2</span>
              <h3 className="text-base font-semibold text-[#004c91]">Scan và gán tên khuôn mặt</h3>
            </div>
            
            {/* Gallery grid of uploaded photos */}
            <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
              
              {/* Column left side: Album items List & Upload Control */}
              <div className="lg:col-span-1 space-y-4 flex flex-col justify-between animate-fadeIn">
                <div className="space-y-3">
                <div className="space-y-2 max-h-[300px] overflow-y-auto pr-2">
                  {uploadedImages.map((img) => (
                  <div 
                    key={img.id}
                    onClick={() => {
                      setSelectedImageId(img.id);
                      setActiveFaceSelection(null);
                    }}
                    className={`flex items-center gap-3 p-2 rounded-xl border cursor-pointer group transition-all ${selectedImageId === img.id ? 'bg-[#004c91]/5 border-[#004c91] ring-2 ring-[#004c91]/10' : 'bg-white border-gray-100 hover:border-gray-300'}`}
                  >
                    <div className="w-12 h-12 rounded-lg bg-gray-100 overflow-hidden shrink-0 border border-gray-200 relative">
                      <img src={img.url} alt={img.name} className="w-full h-full object-cover" />
                      {img.isScanned && (
                        <div className="absolute right-0.5 bottom-0.5 bg-emerald-500 text-white p-0.5 rounded-full" title="Đã quét tìm mặt">
                          <Check className="w-2.5 h-2.5 stroke-[3]" />
                        </div>
                      )}
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="text-xs font-bold text-gray-800 truncate" title={img.name}>{img.name}</p>
                      <p className="text-[10px] text-gray-400 font-semibold mt-0.5">
                        {img.faces.filter(f => f.taggedUser).length}/{img.faces.length} mặt đã định danh
                      </p>
                    </div>
                    {!isReadOnly && (
                      <button
                        onClick={(e) => handleDeleteImage(img.id, e)}
                        type="button"
                        className="p-1 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-all opacity-0 group-hover:opacity-100"
                        title="Xóa ảnh khỏi album"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    )}
                  </div>
                ))}
              </div>
            </div>

            {/* Custom file uploader button trigger */}
            {!isReadOnly && (
              <div className="pt-2">
                <input 
                  type="file" 
                  ref={fileInputRef} 
                  onChange={handleFileUpload}
                  accept="image/*"
                  className="hidden" 
                />
                <button
                  type="button"
                  onClick={() => fileInputRef.current?.click()}
                  className="w-full flex items-center justify-center gap-2 px-4 py-3 bg-[#004c91] text-white hover:bg-[#003e79] rounded-xl font-bold text-sm shadow-md transition-all active:scale-[0.98]"
                >
                  <Upload className="w-4 h-4" /> Tải lên ảnh chụp thực tế
                </button>
                <p className="text-[10px] text-gray-400 mt-1 text-center font-medium">Hỗ trợ các file .jpg, .png lên tới 10MB</p>
              </div>
            )}
          </div>

          {/* Column right side: Interactive big preview area & Face Scanning control */}
          <div className="lg:col-span-3 border border-gray-200 rounded-2xl overflow-hidden bg-gray-50 flex flex-col min-h-[420px] relative">
            {selectedImage ? (
              <>
                {/* Upper bar with name and action button */}
                <div className="bg-white px-5 py-3 border-b border-gray-200 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <ImageIcon className="w-4 h-4 text-gray-500" />
                    <span className="text-xs font-bold text-gray-700 truncate max-w-[260px]" title={selectedImage.name}>
                      {selectedImage.name}
                    </span>
                  </div>

                  <div className="flex items-center gap-3">
                    {/* Fast Search Personal photo input search */}
                    <div className="relative hidden sm:block">
                      <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400 w-3.5 h-3.5" />
                      <input 
                        type="text"
                        placeholder="Tìm ảnh cá nhân trong đoàn..."
                        value={searchGuestKeyword}
                        onChange={(e) => setSearchGuestKeyword(e.target.value)}
                        className="pl-8 pr-3 py-1 bg-gray-100 hover:bg-gray-200/50 focus:bg-white text-xs border border-transparent focus:border-[#004c91] rounded-lg outline-none w-[200px]"
                      />
                    </div>

                    {!selectedImage.isScanned ? (
                      <button
                        type="button"
                        onClick={() => handleTriggerScan(selectedImage.id)}
                        disabled={selectedImage.isScanning}
                        className="px-3.5 py-1.5 bg-[#f37021] hover:bg-orange-600 disabled:opacity-50 text-white rounded-lg text-xs font-extrabold shadow-sm transition-all flex items-center gap-1.5"
                      >
                        <Sparkles className="w-3.5 h-3.5" />
                        {selectedImage.isScanning ? 'Đang phân tích khuôn mặt...' : 'Quét khuôn mặt (Scan Face)'}
                      </button>
                    ) : (
                      <span className="px-3 py-1 bg-emerald-50 text-emerald-700 text-xs font-bold rounded-lg border border-emerald-100 flex items-center gap-1">
                        <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600" /> Đã quét khuôn mặt thành công
                      </span>
                    )}
                  </div>
                </div>

                {/* Sub-bar for Fast Search dropdown output match */}
                {searchGuestKeyword && (
                  <div className="bg-yellow-50 px-5 py-2 border-b border-yellow-100 text-xs font-semibold text-yellow-900 flex items-center gap-2">
                    <Tag className="w-3.5 h-3.5 text-[#f37021]" />
                    <span>Lọc tìm khuôn mặt gắn với tên chứa: <strong className="text-[#004c91]">"{searchGuestKeyword}"</strong></span>
                    <button 
                      onClick={() => setSearchGuestKeyword('')}
                      className="ml-auto underline hover:text-[#004c91] text-gray-500 font-bold"
                    >
                      Xóa bộ lọc
                    </button>
                  </div>
                )}

                {/* Big Preview Area with Box overlays */}
                <div className="flex-1 flex items-center justify-center p-3 relative select-none overflow-hidden">
                  
                  {/* Container of the image */}
                  <div className="relative max-w-full max-h-[380px] rounded-lg overflow-hidden border border-gray-300">
                    <img 
                      src={selectedImage.url} 
                      alt={selectedImage.name} 
                      className={`max-w-full max-h-[360px] object-contain transition-all ${selectedImage.isScanning ? 'brightness-50' : ''}`} 
                    />

                    {/* Laser line simulator during scan */}
                    {selectedImage.isScanning && (
                      <motion.div 
                        initial={{ top: '0%' }}
                        animate={{ top: '100%' }}
                        transition={{ repeat: Infinity, duration: 1.5, ease: 'linear' }}
                        className="absolute left-0 right-0 h-1 bg-gradient-to-r from-orange-500 via-yellow-400 to-orange-500 shadow-[0_0_15px_4px_rgba(243,112,33,1)] z-10"
                      />
                    )}

                    {/* Face Bounding Box Overlays */}
                    {selectedImage.isScanned && selectedImage.faces.map((face) => {
                      const matchedGuest = DEFAULT_GUESTS.find(g => g.id === face.taggedUser);
                      
                      // Highlight search match
                      const isHighlightedBySearch = searchGuestKeyword 
                        ? matchedGuest?.name.toLowerCase().includes(searchGuestKeyword.toLowerCase()) 
                        : true;

                      // Skip rendering if search is active and this face is NOT a match
                      if (searchGuestKeyword && !isHighlightedBySearch) return null;

                      return (
                        <div
                          key={face.id}
                          style={{
                            left: `${face.x}%`,
                            top: `${face.y}%`,
                            width: `${face.width}%`,
                            height: `${face.height}%`
                          }}
                          className={`absolute border-2 ${isReadOnly ? 'cursor-default border-emerald-500/80 bg-emerald-500/5' : 'cursor-pointer'} transition-all rounded-md group/box ${
                            matchedGuest 
                              ? 'border-emerald-500 bg-emerald-500/10 hover:bg-emerald-500/20' 
                              : 'border-orange-500 bg-orange-500/10 hover:bg-orange-600/30'
                          }`}
                          onClick={() => {
                            if (isReadOnly) return;
                            setActiveFaceSelection(
                              activeFaceSelection?.faceId === face.id 
                                ? null 
                                : { imgId: selectedImage.id, faceId: face.id }
                            );
                          }}
                        >
                          {/* Label above face */}
                          <div className={`absolute bottom-full left-1/2 -translate-x-1/2 mb-1.5 transition-opacity whitespace-nowrap z-20`}>
                            <span className={`px-2 py-0.5 rounded-md text-[9px] font-bold shadow-md text-white ${matchedGuest ? 'bg-emerald-600' : 'bg-orange-600'}`}>
                              {matchedGuest ? matchedGuest.name : 'Chưa định danh'}
                            </span>
                          </div>

                          {/* Hover Details overlay */}
                          {matchedGuest && (
                            <div className="absolute top-full left-1/2 -translate-x-1/2 mt-1 hidden group-hover/box:block bg-slate-900 border border-slate-800 text-white p-2 rounded-xl text-[10px] font-medium shadow-xl pointer-events-none z-30 whitespace-nowrap space-y-0.5">
                              <p className="font-bold text-[#f37021]">{matchedGuest.name}</p>
                              <p className="text-slate-300">{matchedGuest.role}</p>
                              <p className="text-slate-400 font-mono text-[9px]">{matchedGuest.cardVisit}</p>
                            </div>
                          )}

                          {/* Target Tag Selector Popover if clicked */}
                          {activeFaceSelection?.faceId === face.id && (
                            <div 
                              className="absolute top-full left-1/2 -translate-x-1/2 mt-2 bg-white border border-gray-200 p-3 rounded-2xl shadow-xl z-40 w-52 text-left space-y-2.5 animate-in fade-in slide-in-from-top-1"
                              onClick={(e) => e.stopPropagation()}
                            >
                              <div className="flex items-center justify-between border-b border-gray-100 pb-1.5">
                                <span className="text-[10px] uppercase font-bold text-gray-500">Gán danh tính (Tag)</span>
                                <button 
                                  onClick={() => setActiveFaceSelection(null)}
                                  className="text-gray-400 hover:text-gray-600 p-0.5 hover:bg-gray-100 rounded-full"
                                >
                                  <Minimize2 className="w-3 h-3" />
                                </button>
                              </div>
                              
                              <div className="space-y-1 max-h-[160px] overflow-y-auto pr-1">
                                <button
                                  type="button"
                                  onClick={() => handleTagGuest(face.id, null)}
                                  className={`w-full text-left px-2 py-1.5 rounded-lg text-xs font-semibold flex items-center justify-between transition-colors ${!face.taggedUser ? 'bg-orange-50 text-[#f37021]' : 'hover:bg-slate-50 text-gray-600'}`}
                                >
                                  <span>Bỏ gán tên (Chưa rõ)</span>
                                  {!face.taggedUser && <Check className="w-3.5 h-3.5" />}
                                </button>
                                
                                {DEFAULT_GUESTS.map((guest) => (
                                  <button
                                    key={guest.id}
                                    type="button"
                                    onClick={() => handleTagGuest(face.id, guest.id)}
                                    className={`w-full text-left px-2 py-1.5 rounded-lg text-xs font-semibold flex flex-col transition-colors ${face.taggedUser === guest.id ? 'bg-[#004c91]/5 text-[#004c91]' : 'hover:bg-slate-50 text-gray-700'}`}
                                  >
                                    <div className="flex items-center justify-between w-full font-bold">
                                      <span>{guest.name}</span>
                                      {face.taggedUser === guest.id && <Check className="w-3.5 h-3.5 text-[#004c91]" />}
                                    </div>
                                    <span className="text-[9px] text-gray-400 mt-0.5">{guest.role} ({guest.cardVisit})</span>
                                  </button>
                                ))}
                              </div>
                            </div>
                          )}
                        </div>
                      );
                    })}

                  </div>

                  {/* Empty state overlay while scanning */}
                  {selectedImage.isScanning && (
                    <div className="absolute inset-0 flex flex-col items-center justify-center text-white p-6 bg-slate-900/60 z-10">
                      <div className="animate-spin rounded-full h-8 w-8 border-2 border-t-transparent border-white mb-2"></div>
                      <p className="text-sm font-bold tracking-wide">Trí tuệ Nhân tạo thực thi định vị khuôn mặt...</p>
                    </div>
                  )}

                  {/* Instructions watermark on layout */}
                  {!selectedImage.isScanned && !selectedImage.isScanning && (
                    <div className="absolute bottom-3 left-1/2 -translate-x-1/2 bg-slate-900/80 backdrop-blur-sm px-4 py-1.5 rounded-full text-[10px] text-white font-bold tracking-wide shadow-md">
                      Nhấp nút Quét mặt phía trên để tự động định danh khách mời
                    </div>
                  )}

                  {selectedImage.isScanned && (
                    <div className="absolute bottom-3 left-1/2 -translate-x-1/2 bg-emerald-900/90 backdrop-blur-sm px-4 py-1.5 rounded-full text-[10px] text-white font-bold tracking-wide shadow-md">
                      Mẹo: Click từng hộp khung bao màu cam để gán tên tương ứng!
                    </div>
                  )}
                </div>

                {/* Tag summary list on bottom bar */}
                {selectedImage.isScanned && (
                  <div className="bg-white border-t border-gray-100 p-4">
                    <p className="text-xs font-bold text-gray-500 uppercase tracking-wide mb-2">
                      Danh sách định danh khuôn mặt trong bức hình này:
                    </p>
                    <div className="flex flex-wrap gap-2">
                      {selectedImage.faces.map((f, fIdx) => {
                        const matchedG = DEFAULT_GUESTS.find(g => g.id === f.taggedUser);
                        return (
                          <div 
                            key={f.id}
                            className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-xl text-xs font-bold border transition-colors ${matchedG ? 'bg-emerald-50 text-emerald-800 border-emerald-200' : 'bg-gray-100 text-gray-500 border-gray-200'}`}
                          >
                            <User className="w-3.5 h-3.5" />
                            <span>Vị trí #{fIdx + 1}: {matchedG ? `${matchedG.name} (${matchedG.cardVisit})` : 'Chưa gán tên'}</span>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                )}
              </>
            ) : (
              <div className="flex-1 flex flex-col items-center justify-center text-gray-400 p-8">
                <ImageIcon className="w-12 h-12 text-gray-300 mb-2" />
                <p className="text-sm font-medium">Chọn một ảnh từ album hoặc tải ảnh mới để bắt đầu.</p>
              </div>
            )}
          </div>

        </div>
        
          </div>
            </>
          )}

      </div>

    </div>

      {/* SECTION 2: NEWS — bản thật (backend, nhiều bài/instance) khi có visitInstanceId; nếu không, dùng mock cũ */}
      {visitInstanceId ? (
        <VisitNewsSection visitInstanceId={visitInstanceId} />
      ) : (
      <div className="bg-white rounded-[2rem] border border-gray-200 shadow-sm overflow-hidden">

        {/* Title layout with traditional blue background */}
        <div className="bg-[#004c91] p-6 sm:p-8 text-white flex justify-start items-center gap-3">
          <span className="w-8 h-8 rounded-full bg-[#f37021] flex items-center justify-center text-sm font-black text-white">2</span>
          <div>
            <h2 className="text-xl font-bold text-white">{isDept ? 'Tin tức đoàn khách' : 'Tạo tin tức'}</h2>
            <p className="text-xs text-blue-100/80 mt-1 font-medium">{isDept ? 'Bài viết truyền thông về hoạt động tiếp khách' : 'Tạo tin tức dựa trên biên bản cuộc họp'}</p>
          </div>
        </div>

        {/* Content area */}
        <div className="p-4 sm:p-6 md:p-8 text-center flex flex-col items-center justify-center space-y-4">
          <p className="text-sm font-semibold text-gray-500 max-w-lg font-sans">
            {isDept ? 'Xem chi tiết bài viết truyền thông đã được đăng lên hệ thống tin tức.' : 'Ấn nút bên dưới để chuyển sang tạo tin tức'}
          </p>
          {!isDept && (
            <div className="my-2 px-4 py-3 bg-[#e8f5e9]/70 border-l-4 border-[#00a651] rounded-r-xl max-w-xl text-left shadow-sm">
              <p className="text-xs font-bold text-emerald-800 flex items-center gap-1.5">
                <Sparkles className="w-4 h-4 text-[#00a651]" />
                <span>Không bắt buộc phải tạo tin tức, nếu khách không xác nhận truyền thông.</span>
              </p>
            </div>
          )}
          {isDept ? (
            <div 
              onClick={() => setIsArticleModalOpen(true)}
              className="mt-4 w-full max-w-2xl bg-white border border-gray-200 rounded-2xl overflow-hidden shadow-sm hover:shadow-md transition-shadow cursor-pointer text-left group"
            >
              <div className="h-48 bg-gray-100 relative overflow-hidden">
                <img 
                  src="https://images.unsplash.com/photo-1542744173-8e7e53415bb0?auto=format&fit=crop&q=80&w=800" 
                  alt="News Cover" 
                  className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                />
              </div>
              <div className="p-5">
                <div className="mb-2 text-xs text-gray-500 font-medium font-sans">
                  Đăng ngày 15/10/2026 • 5 phút đọc
                </div>
                <h3 className="text-lg font-bold text-gray-900 leading-snug group-hover:text-[#004c91] transition-colors mb-2">
                  Chuyến thăm và làm việc của Đoàn khách Đại học Monash tại cơ sở
                </h3>
                <p className="text-sm text-gray-600 line-clamp-2">
                  Buổi làm việc đã tạo ra các cơ hội hợp tác chiến lược giữa hai trường đại học trong tương lai gần, cùng với các hoạt động giao lưu học thuật.
                </p>
                <div className="mt-4 flex items-center text-[#004c91] text-sm font-bold gap-1 group-hover:gap-2 transition-all">
                  Xem bài viết <ArrowRight className="w-4 h-4" />
                </div>
              </div>
            </div>
           ) : (
            <button
              type="button"
              // Người dùng muốn TẠO tin → đi thẳng vào form tạo (không điều hướng sang list).
              onClick={() => !isReadOnly && navigate('/dashboard/news/create')}
              disabled={isReadOnly}
              className={`inline-flex items-center gap-2 px-6 py-3 font-extrabold rounded-xl transition-all text-sm outline-none ${
                isReadOnly
                  ? 'bg-gray-100 text-gray-400 border border-gray-200 cursor-default shadow-none'
                  : 'bg-[#004c91] hover:bg-[#00386b] text-white shadow-md hover:shadow-lg active:scale-95 cursor-pointer'
              }`}
            >
              Chuyển sang trang tạo tin tức
              <ExternalLink className="w-4 h-4 stroke-[2.5]" />
            </button>
           )}
        </div>

      </div>
      )}

      {/* ACTION BLOCK: chỉ còn nút thông tin "Lưu ý". Việc đóng đoàn (AFTER_VISIT → CLOSED) được
          thực hiện qua DUY NHẤT một CTA ở stage bar của VisitProcess ("Hoàn tất & đóng đoàn") để
          không có 2 nút cùng tác dụng trên một tab (đặc tả mục 1.6 / 8.3). */}
      {!isReadOnly && !isDept && !isStudent ? (
        <div className="border-t border-gray-200 pt-8 flex flex-col md:flex-row justify-center md:justify-end items-center gap-4 pb-12">
          <button
            type="button"
            onClick={() => setShowNoticeModal(true)}
            className="px-6 py-4 bg-amber-50 hover:bg-amber-100 text-amber-700 font-bold rounded-2xl border border-amber-300 shadow-sm active:scale-95 transition-all text-sm outline-none flex items-center gap-2 cursor-pointer"
          >
            <AlertCircle className="w-5 h-5" /> Lưu ý trước khi đóng đoàn
          </button>
        </div>
      ) : (
        <div className="border-t border-gray-200 pt-8 flex justify-end pb-12">
          <button 
            type="button"
            onClick={() => navigate('/dashboard/visit')}
            className="px-8 py-3.5 font-bold text-white bg-[#004c91] hover:bg-[#003366] rounded-xl transition-all shadow-md hover:shadow-lg outline-none cursor-pointer flex items-center gap-2"
          >
            Quay lại danh sách tiếp khách
          </button>
        </div>
      )}

      {/* NOTICE MODAL */}
      <AnimatePresence>
        {showNoticeModal && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              onClick={() => setShowNoticeModal(false)}
              className="absolute inset-0 bg-slate-900/60 backdrop-blur-sm cursor-pointer"
            />
            
            <motion.div
              initial={{ scale: 0.95, opacity: 0, y: 15 }}
              animate={{ scale: 1, opacity: 1, y: 0 }}
              exit={{ scale: 0.95, opacity: 0, y: 15 }}
              className="bg-white rounded-3xl shadow-xl w-full max-w-2xl overflow-hidden relative border border-gray-100 z-10 text-left font-sans"
            >
              <div className="p-6">
                <div className="flex items-center gap-3 mb-6">
                  <div className="w-12 h-12 bg-amber-100 rounded-full flex items-center justify-center text-amber-600">
                    <AlertCircle className="w-6 h-6 stroke-[2]" />
                  </div>
                  <h3 className="text-xl font-bold text-gray-900">
                    Lưu ý trước khi chốt đoàn
                  </h3>
                </div>
                
                <div className="flex flex-col gap-4">
                  <div className="flex items-start gap-3 p-5 bg-amber-50 border border-amber-300 rounded-2xl text-left shadow-sm">
                    <AlertCircle className="w-5 h-5 text-amber-600 shrink-0 mt-0.5" />
                    <p className="text-sm font-semibold text-amber-900 leading-relaxed bg-transparent">
                      <strong className="text-amber-700 font-extrabold block mb-0.5">CHỐT HỒ SƠ TIẾP ĐÓN:</strong>
                      Khi bấm nút này, hệ thống sẽ chốt toàn bộ dữ liệu hiện có trong quy trình tiếp khách. Bạn sẽ không thể chỉnh sửa hay thực hiện bất kì thao tác nào sau khi ấn nút này.
                    </p>
                  </div>

                  <div className="flex items-start gap-3 p-5 bg-blue-50 border border-blue-200 rounded-2xl text-left shadow-sm font-sans">
                    <FileText className="w-5 h-5 text-blue-600 shrink-0 mt-0.5" />
                    <div className="text-sm font-semibold text-blue-950 leading-relaxed">
                      <strong className="text-blue-800 font-extrabold block mb-0.5">ĐIỀU KIỆN ĐỂ CÓ THỂ ĐÓNG ĐOÀN KHÁCH:</strong>
                      <ul className="list-decimal pl-4 space-y-1 mt-1 text-xs font-bold text-blue-900">
                        <li>Còn đầu mục công việc chưa tích xác nhận trong biên bản cuộc họp</li>
                        <li>Chưa upload ảnh của đoàn khách</li>
                        <li>Tin tức chưa được duyệt (nếu có)</li>
                      </ul>
                    </div>
                  </div>
                </div>
                
                <div className="mt-8 flex justify-end">
                  <button
                    type="button"
                    onClick={() => setShowNoticeModal(false)}
                    className="px-6 py-2.5 rounded-xl bg-gray-100 hover:bg-gray-200 text-gray-800 font-bold text-sm transition-colors cursor-pointer"
                  >
                    Đã hiểu
                  </button>
                </div>
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>

      {/* ARTICLE MODAL */}
      <AnimatePresence>
        {isArticleModalOpen && (
          <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 sm:p-6">
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              onClick={() => setIsArticleModalOpen(false)}
              className="absolute inset-0 bg-slate-900/60 backdrop-blur-sm cursor-pointer"
            />
            <motion.div
              initial={{ scale: 0.95, opacity: 0, y: 15 }}
              animate={{ scale: 1, opacity: 1, y: 0 }}
              exit={{ scale: 0.95, opacity: 0, y: 15 }}
              className="bg-white rounded-3xl shadow-xl w-full max-w-3xl overflow-hidden relative border border-gray-100 z-10 text-left font-sans flex flex-col max-h-[90vh]"
            >
              {/* Header */}
              <div className="px-6 py-4 flex items-center justify-between border-b border-gray-100 shrink-0">
                <h3 className="font-bold text-gray-900">Chi tiết bài viết</h3>
                <button
                  type="button"
                  onClick={() => setIsArticleModalOpen(false)}
                  className="p-2 hover:bg-gray-100 rounded-full text-gray-500 transition-colors cursor-pointer"
                >
                  <X className="w-5 h-5 stroke-[2.5]" />
                </button>
              </div>
              
              {/* Content */}
              <div className="p-6 overflow-y-auto space-y-6">
                <div className="aspect-video w-full rounded-2xl overflow-hidden bg-gray-100">
                  <img 
                    src="https://images.unsplash.com/photo-1542744173-8e7e53415bb0?auto=format&fit=crop&q=80&w=800" 
                    alt="Cover" 
                    className="w-full h-full object-cover"
                  />
                </div>
                
                <div className="space-y-4">
                  <div className="text-sm text-gray-500 font-medium pb-2 border-b border-gray-100">
                    Đăng ngày 15/10/2026 • Bởi Ban Truyền thông
                  </div>
                  <h1 className="text-2xl sm:text-3xl font-extrabold text-[#004c91] leading-tight">
                    Chuyến thăm và làm việc của Đoàn khách Đại học Monash tại cơ sở
                  </h1>
                  <div className="space-y-4 text-gray-700 leading-relaxed">
                    <p>
                      Sáng ngày 15/10/2026, cơ sở đào tạo hân hạnh đón tiếp đoàn công tác cấp cao từ Đại học Monash (Úc). Chuyến thăm nhằm mục đích tăng cường hợp tác, trao đổi học thuật và thảo luận về các chương trình liên kết đào tạo trong tương lai.
                    </p>
                    <p>
                      Trong khuôn khổ buổi làm việc, Ban lãnh đạo hai bên đã tiến hành thảo luận chi tiết về cơ hội trao đổi sinh viên, nghiên cứu chung và chia sẻ kinh nghiệm giảng dạy. Đặc biệt, nội dung về trí tuệ nhân tạo và ứng dụng công nghệ trong giáo dục được sự quan tâm rất lớn từ cả hai phía.
                    </p>
                    <p>
                      Buổi làm việc đã tạo ra các cơ hội hợp tác chiến lược giữa hai trường đại học trong tương lai gần, cùng với các hoạt động giao lưu học thuật dự kiến sẽ sớm được triển khai.
                    </p>
                  </div>
                </div>
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>

    </div>
  );
}
