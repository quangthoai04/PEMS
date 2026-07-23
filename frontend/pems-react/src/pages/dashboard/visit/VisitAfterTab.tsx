/**
 * Component VisitAfterTab
 * Module hậu cần sau diễn ra thăm quan thực địa.
 */

import React, { useState, useRef, useEffect } from 'react';
import {
  Sparkles, FileText, AlertCircle,
  ArrowRight, FolderOpen, ExternalLink, RefreshCw,
  Check, X, Star, Loader2
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { useAuthContext } from '../../../shared/auth/AuthContext';
import { VisitNewsSection } from './VisitNewsSection';
import { LogisticsHandoverSection } from '../../../features/delegations/components/LogisticsHandoverSection';
import { GeneralExpensePanel } from './GeneralExpensePanel';
import { FaceScanPanel } from '../../../features/delegations/components/FaceScanPanel';
import { VisitFeedbackModal } from '../../../features/feedbacks/components/VisitFeedbackModal';
import { useVisitFeedback } from '../../../features/feedbacks/hooks/useVisitFeedback';
import { FeedbackGroupSection } from '../../../features/feedbacks/components/FeedbackGroupSection';
import { visitPhotosApi } from '../../../features/delegations/api/visitPhotosApi';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import { showLoadingToast, updateToastSuccess, updateToastError } from '../../../shared/utils/toast';

// Helper cho collapse header
function CollapsibleSection({ 
  number, 
  title, 
  subtitle, 
  children, 
  defaultExpanded = true,
  rightElement
}: { 
  number: string | number; 
  title: React.ReactNode; 
  subtitle?: string; 
  children: React.ReactNode; 
  defaultExpanded?: boolean;
  rightElement?: React.ReactNode;
}) {
  const [isExpanded, setIsExpanded] = useState(defaultExpanded);
  return (
    <div className="bg-white border border-gray-200 rounded-xl shadow-sm overflow-hidden">
      <div 
        className="flex items-center justify-between px-5 py-3.5 bg-[#004c91] text-white cursor-pointer select-none transition-colors"
        onClick={() => setIsExpanded(!isExpanded)}
      >
        <div className="flex items-center gap-3">
          <span className="w-8 h-8 rounded-full bg-[#f37021] flex items-center justify-center text-sm font-black text-white shrink-0">
            {number}
          </span>
          <div>
            <h2 className="text-sm font-bold tracking-tight uppercase flex items-center gap-2">{title}</h2>
          </div>
        </div>
        <div className="flex items-center gap-3">
          {rightElement}
          <div className="p-1 hover:bg-white/10 rounded-md transition-colors text-white">
            <svg className={`w-5 h-5 transform transition-transform ${!isExpanded ? 'rotate-180' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
            </svg>
          </div>
        </div>
      </div>
      {isExpanded && (
        <div className="bg-white">
          {children}
        </div>
      )}
    </div>
  );
}

function VisitFeedbackInlineSection({ 
  visitInstanceId, 
  onOpenModal 
}: { 
  visitInstanceId: number; 
  onOpenModal: () => void; 
}) {
  const { data, loading, loadError } = useVisitFeedback(visitInstanceId);
  
  if (loading) {
    return (
      <CollapsibleSection number="3" title="Đánh giá chuyến thăm" subtitle="Đánh giá đoàn khách, các bên hỗ trợ setup và bên hậu cần theo dữ liệu thật.">
        <div className="p-4 sm:p-5 flex justify-center text-gray-500 py-10">
          <Loader2 className="w-5 h-5 animate-spin" />
        </div>
      </CollapsibleSection>
    );
  }
  
  if (loadError || !data) {
    return (
      <CollapsibleSection number="3" title="Đánh giá chuyến thăm" subtitle="Đánh giá đoàn khách, các bên hỗ trợ setup và bên hậu cần theo dữ liệu thật.">
        <div className="p-4 sm:p-5">
          <div className="bg-slate-50 rounded-xl border border-slate-200 px-6 py-4 flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-2.5 min-w-0">
              <div className="p-1.5 bg-orange-100 rounded-lg shrink-0"><Star className="w-5 h-5 text-[#f37021]" /></div>
              <div className="min-w-0">
                <p className="text-sm font-bold text-gray-800">Hãy dành chút thời gian đánh giá chất lượng phục vụ của chuyến thăm</p>
              </div>
            </div>
            <button
              type="button"
              onClick={onOpenModal}
              className="inline-flex items-center gap-1.5 rounded-lg border border-[#004c91] bg-white px-3.5 py-1.5 text-xs font-bold text-[#004c91] hover:bg-[#f0f7ff] outline-none"
            >
              <Star className="w-3.5 h-3.5" /> Đánh giá ngay
            </button>
          </div>
        </div>
      </CollapsibleSection>
    );
  }
  
  if (data.alreadySubmittedAllRequired) {
    let runningIndex = 1;
    return (
      <CollapsibleSection number="3" title="Đánh giá chuyến thăm" subtitle="Đánh giá đoàn khách, các bên hỗ trợ setup và bên hậu cần theo dữ liệu thật.">
        <div className="p-4 sm:p-5 space-y-4 bg-slate-50 border-t border-gray-100">
          <div className="flex items-center gap-2 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm font-semibold text-emerald-700">
            <Check className="h-4 w-4 shrink-0" />
            {data.actorType === 'VISITOR' ? 'Bạn đã đánh giá chuyến thăm này.' : 'Bạn đã đánh giá đầy đủ các mục của chuyến thăm này.'}
          </div>
          
          <div className="space-y-2.5 pointer-events-none">
            {data.groups.map((g) => {
              const start = runningIndex;
              runningIndex += g.targets.length;
              return (
                <FeedbackGroupSection
                  key={g.groupCode}
                  group={g}
                  startIndex={start}
                  drafts={{}}
                  disabled={true}
                  forceShowComment={data.actorType === 'VISITOR'}
                  onRate={() => {}}
                  onChangeComment={() => {}}
                />
              );
            })}
          </div>
        </div>
      </CollapsibleSection>
    );
  }
  
  return (
    <CollapsibleSection number="3" title="Đánh giá chuyến thăm" subtitle="Đánh giá đoàn khách, các bên hỗ trợ setup và bên hậu cần theo dữ liệu thật.">
      <div className="p-4 sm:p-5">
        <div className="bg-slate-50 rounded-xl border border-slate-200 px-6 py-4 flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-2.5 min-w-0">
            <div className="p-1.5 bg-orange-100 rounded-lg shrink-0"><Star className="w-5 h-5 text-[#f37021]" /></div>
            <div className="min-w-0">
              <p className="text-sm font-bold text-gray-800">Hãy dành chút thời gian đánh giá chất lượng phục vụ của chuyến thăm</p>
            </div>
          </div>
          <button
            type="button"
            onClick={onOpenModal}
            className="inline-flex items-center gap-1.5 rounded-lg border border-[#004c91] bg-white px-3.5 py-1.5 text-xs font-bold text-[#004c91] hover:bg-[#f0f7ff] outline-none"
          >
            <Star className="w-3.5 h-3.5" /> Đánh giá ngay
          </button>
        </div>
      </div>
    </CollapsibleSection>
  );
}

interface VisitAfterTabProps {
  onTourCloseSuccess?: () => void;
  isReadOnly?: boolean;
  isDept?: boolean;
  visitInstanceId?: number;
}

export function VisitAfterTab({ onTourCloseSuccess, isReadOnly = false, isDept = false, visitInstanceId }: VisitAfterTabProps) {
  const navigate = useNavigate();
  const { t } = useTranslation('visitFaceScan');
  const { user } = useAuthContext();
  const roleCode = (user?.roleCode || '').toUpperCase();
  const isStudent = roleCode === 'STUDENT' || roleCode === 'VISITOR';

  // Modal đánh giá chuyến thăm — chuyển từ tab Trong tiếp khách sang: chỉ đánh giá ở giai đoạn Sau tiếp khách.
  const [isFeedbackModalOpen, setIsFeedbackModalOpen] = useState(false);
  const [feedbackRefreshKey, setFeedbackRefreshKey] = useState(0);

  // Part 1: Images state — real visit_photos rows (id/url/name only; face-scan state lives inside
  // FaceScanPanel, fetched per photo from the backend, not mocked here).
  const [uploadedImages, setUploadedImages] = useState<Array<{
    id: number;
    url: string;
    name: string;
  }>>([]);

  const [driveConfig, setDriveConfig] = useState({
    isConnected: true,
    folderName: `vr-${visitInstanceId || '3063'}`,
    folderUrl: '',
    syncStatus: 'synced', // 'synced' | 'syncing' | 'error'
    lastSynced: '-',
    uploaderName: '-'
  });
  const [isDriveConfirmed, setIsDriveConfirmed] = useState(isReadOnly);

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

  // "Lưu ý" info modal + the dept article-preview modal. The real "đóng đoàn" CTA lives ONLY in the
  // VisitProcess stage bar (single source of truth); this tab no longer owns a close button so the
  // same action can never appear twice on one tab (đặc tả mục 1.6 / 8.3).
  const [showNoticeModal, setShowNoticeModal] = useState(false);
  const [isArticleModalOpen, setIsArticleModalOpen] = useState(false);

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

  // Run initial news generation and fetch real photo/drive metadata
  useEffect(() => {
    if (!newsContentVi) {
      handleAutoGenerateNews();
    }
  }, []);

  const loadDriveMetadata = async () => {
    if (!visitInstanceId) return;
    try {
      const data = await visitPhotosApi.byInstance(visitInstanceId);
      
      let latestPhoto = null;
      if (data.photos && data.photos.length > 0) {
        latestPhoto = data.photos.reduce((latest, current) => {
          return new Date(current.uploadedAt) > new Date(latest.uploadedAt) ? current : latest;
        }, data.photos[0]);
        
        // Real visit_photos rows — url is an authenticated `/api/files/{id}/content` route
        // (rendered via useAuthenticatedImage inside FaceScanPanel, never a plain public <img src>).
        const realImages = data.photos.map(p => ({
          id: p.visitPhotoId,
          url: `/api${p.url.replace(/^\/api/, '')}`,
          name: p.fileName,
        }));
        setUploadedImages(realImages);
      } else {
        setUploadedImages([]);
      }

      setDriveConfig(prev => ({
        ...prev,
        folderName: data.folderName || `vr-${visitInstanceId}`,
        folderUrl: data.folderWebViewUrl || prev.folderUrl,
        uploaderName: latestPhoto?.uploadedByName || '-',
        lastSynced: latestPhoto?.uploadedAt ? formatVietnamDateTime(latestPhoto.uploadedAt) : '-'
      }));
    } catch (e) {
      console.error("Failed to fetch visit photos metadata", e);
    }
  };

  useEffect(() => {
    loadDriveMetadata();
  }, [visitInstanceId]);

  // Handle real file upload to backend
  const fileInputRef = useRef<HTMLInputElement>(null);
  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (files && files.length > 0 && visitInstanceId) {
      const fileList = Array.from(files);
      const toastId = showLoadingToast(`Đang tải lên ${fileList.length} ảnh để quét nhận diện khuôn mặt...`, 'visit-photo-upload');
      
      setDriveConfig(prev => ({
        ...prev,
        syncStatus: 'syncing',
        lastSynced: 'Đang đồng bộ tập tin...'
      }));
      
      try {
        await visitPhotosApi.upload(visitInstanceId, fileList);
        // Refresh data from backend to show the new photos and metadata
        await loadDriveMetadata();
        
        setDriveConfig(prev => ({
          ...prev,
          syncStatus: 'synced'
        }));
        updateToastSuccess(toastId, `Đã tải lên thành công ${fileList.length} ảnh. Bạn có thể chọn ảnh để quét khuôn mặt.`);
      } catch (err) {
        console.error("Upload error", err);
        updateToastError(toastId, err, 'Không thể tải ảnh lên. Vui lòng kiểm tra dung lượng hoặc định dạng file (hỗ trợ .jpg, .png lên tới 10MB).');
        setDriveConfig(prev => ({
          ...prev,
          syncStatus: 'error',
          lastSynced: 'Đồng bộ lỗi'
        }));
      } finally {
        if (e.target) e.target.value = '';
      }
    }
  };

  return (
    <div className="space-y-8 text-left">

      {/* Ký trả tài sản hậu cần — phần đầu tiên của tab Sau tiếp khách (real handover API). */}
      {visitInstanceId && !isStudent && (
        <LogisticsHandoverSection visitInstanceId={visitInstanceId} canManage={!isReadOnly && !isDept} handoverPhase="RETURN" sectionNumber="1" theme="blue" />
      )}

      {/* Chi phí chung (General Expense) */}
      {visitInstanceId && !isStudent && !isDept && (
        <GeneralExpensePanel visitInstanceId={visitInstanceId} isReadOnly={isReadOnly} sectionNumber="2" />
      )}

      {/* Đánh giá chuyến thăm — chuyển từ tab Trong tiếp khách sang đây: đánh giá được thực hiện
          sau khi Host đã xác nhận kết thúc tiếp khách (instance ở AFTER_VISIT trở đi). */}
      {visitInstanceId && !isStudent && !isDept && (
        <VisitFeedbackInlineSection 
          key={`feedback-${feedbackRefreshKey}`}
          visitInstanceId={visitInstanceId} 
          onOpenModal={() => setIsFeedbackModalOpen(true)} 
        />
      )}

      {/* Modal đánh giá — mở tại chỗ trong Visit Process */}
      <VisitFeedbackModal
        open={isFeedbackModalOpen}
        visitInstanceId={visitInstanceId ?? null}
        onClose={() => setIsFeedbackModalOpen(false)}
        onSubmitted={() => setFeedbackRefreshKey(prev => prev + 1)}
      />

      {/* SECTION 4: PHOTO ALBUM & FACE SCANNING */}
      <CollapsibleSection number="4" title="Lưu trữ ảnh của đoàn khách" subtitle="Bắt buộc tải lên tối thiểu 1 ảnh thực tế diễn ra tiếp khách (với mọi đoàn không có media) để lưu trữ minh chứng.">
        <div className="p-4 sm:p-6 md:p-8 space-y-8">

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
                    <div className="text-gray-500 text-xs flex flex-col sm:flex-row sm:items-center gap-1 sm:gap-2 font-semibold">
                      <span className="flex items-center gap-1">
                        Người upload: <span className="text-[#004c91]">{driveConfig.uploaderName}</span>
                      </span>
                      <span className="hidden sm:inline text-gray-300">|</span>
                      <span className="flex items-center gap-1">
                        Thời gian: 
                        {driveConfig.syncStatus === 'syncing' ? (
                          <RefreshCw className="w-3.5 h-3.5 animate-spin text-[#004c91]" />
                        ) : (
                          <span className="text-emerald-600 font-bold">{driveConfig.lastSynced}</span>
                        )}
                      </span>
                    </div>
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
          {!isDept && (
            <>
              <div className="py-2">
                <hr className="border-t border-gray-200" />
              </div>

              {/* SUBSECTION 2: FACE SCANNING — real Google Cloud Vision face detection + manual
                  guest tagging (FaceScanPanel owns all scan/tag state, fetched from the backend). */}
              <div className="space-y-4">
                <div className="flex items-center gap-2.5">
                  <span className="w-6 h-6 rounded-lg bg-[#004c91]/10 flex items-center justify-center text-xs font-bold text-[#004c91]">2</span>
                  <h3 className="text-base font-semibold text-[#004c91]">{t('title')}</h3>
                </div>

                {visitInstanceId && (
                  <>
                    <input
                      type="file"
                      ref={fileInputRef}
                      onChange={handleFileUpload}
                      accept="image/jpeg,image/png,image/webp,image/*"
                      multiple
                      className="hidden"
                    />
                    <FaceScanPanel
                      visitInstanceId={visitInstanceId}
                      photos={uploadedImages.map((img) => ({ visitPhotoId: img.id, url: img.url, name: img.name }))}
                      isReadOnly={isReadOnly}
                      onUploadClick={() => fileInputRef.current?.click()}
                      folderUrl={driveConfig.folderUrl}
                      onRefreshPhotos={loadDriveMetadata}
                    />
                  </>
                )}
              </div>
            </>
          )}
        </div>
      </CollapsibleSection>

      {/* SECTION 5: NEWS */}
      {visitInstanceId ? (
        <VisitNewsSection visitInstanceId={visitInstanceId} sectionNumber="5" />
      ) : (
        <CollapsibleSection number="5" title={isDept ? 'Tin tức đoàn khách' : 'Tạo tin tức'} subtitle={isDept ? 'Bài viết truyền thông về hoạt động tiếp khách' : 'Tạo tin tức dựa trên biên bản cuộc họp'}>
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
        </CollapsibleSection>
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
                        <li>Kiểm tra lại chi phí của đoàn — các đơn yêu cầu phải được phòng ban nhập chi phí hoặc xác nhận "Không có chi phí"</li>
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
