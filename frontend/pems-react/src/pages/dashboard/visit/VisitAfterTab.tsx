/**
 * Component VisitAfterTab
 * Module hậu cần sau diễn ra thăm quan thực địa.
 */

import React, { useState, useRef, useEffect } from 'react';
import {
  FileText, AlertCircle,
  FolderOpen, Eye, UploadCloud, X, Camera, RefreshCw,
  Check, Star, Loader2
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { useAuthContext } from '../../../shared/auth/AuthContext';
import { VisitNewsSection } from './VisitNewsSection';
import { LogisticsHandoverSection } from '../../../features/delegations/components/LogisticsHandoverSection';
import { GeneralExpensePanel } from './GeneralExpensePanel';
import { FaceScanPanel, type FaceScanPanelHandle } from '../../../features/delegations/components/FaceScanPanel';
import { VisitPhotoPanel } from '../../../features/delegations/components/VisitPhotoPanel';
import { VisitFeedbackModal } from '../../../features/feedbacks/components/VisitFeedbackModal';
import { useVisitFeedback } from '../../../features/feedbacks/hooks/useVisitFeedback';
import { FeedbackGroupSection } from '../../../features/feedbacks/components/FeedbackGroupSection';
import { visitPhotosApi } from '../../../features/delegations/api/visitPhotosApi';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import { showLoadingToast, updateToastSuccess, updateToastError, showMessageErrorToast } from '../../../shared/utils/toast';
import { EmptyState } from '../../../shared/components/state';
import { validateFile } from '../../../shared/utils/fileValidation';

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
                <p className="text-sm font-normal text-gray-800">Hãy dành chút thời gian đánh giá chất lượng phục vụ của chuyến thăm</p>
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
          <div className="flex items-center gap-2 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm font-normal text-emerald-700">
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
              <p className="text-sm font-normal text-gray-800">Hãy dành chút thời gian đánh giá chất lượng phục vụ của chuyến thăm</p>
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
  /**
   * The campus instance's lifecycle status, passed straight through to the expense panel: whether a
   * cost sheet may be opened at all is a lifecycle question, and guessing it from `isReadOnly` gets
   * a CLOSED visit and a still-running one confused.
   */
  instanceStatus?: string;
}

export function VisitAfterTab({ onTourCloseSuccess, isReadOnly = false, isDept = false, visitInstanceId, instanceStatus }: VisitAfterTabProps) {
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
    folderName: `vr-${visitInstanceId ?? ''}`,
    folderUrl: '',
    syncStatus: 'synced', // 'synced' | 'syncing' | 'error'
    lastSynced: '-',
    uploaderName: '-'
  });
  const [showPhotoModal, setShowPhotoModal] = useState(false);

  // "Lưu ý" info modal. The real "đóng đoàn" CTA lives ONLY in the VisitProcess stage bar (single
  // source of truth); this tab no longer owns a close button so the same action can never appear
  // twice on one tab (đặc tả mục 1.6 / 8.3). News is per-instance and handled entirely by the real
  // News workflow (VisitNewsSection → VisitNewsPostList → the shared News Management form); this tab
  // holds no separate news editor and no mock preview.
  const [showNoticeModal, setShowNoticeModal] = useState(false);

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
  const faceScanPanelRef = useRef<FaceScanPanelHandle>(null);
  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (files && files.length > 0 && visitInstanceId) {
      const fileList = Array.from(files);
      if (e.target) e.target.value = '';
      // Client-side pre-check mirrors the backend canonical visit-photo contract (JPEG/PNG/WebP,
      // 5 MB/file, max 10/request). Backend re-enforces; this is UX only, to avoid a doomed upload.
      for (const f of fileList) {
        const check = validateFile(f, 'VISIT_REQUEST_PHOTO');
        if (!check.ok) { showMessageErrorToast(`${f.name}: ${check.message ?? 'Ảnh không hợp lệ.'}`); return; }
      }
      if (fileList.length > 10) { showMessageErrorToast('Chỉ được tải lên tối đa 10 ảnh mỗi lần.'); return; }
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
        updateToastError(toastId, err, 'Không thể tải ảnh lên. Chỉ chấp nhận JPG/PNG/WEBP, tối đa 5MB mỗi file và 10 ảnh mỗi lần.');
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

      {/* Input file dùng chung cho nút "Upload ảnh" (subsection 1) và Face Scan Panel (subsection 2) */}
      <input
        type="file"
        ref={fileInputRef}
        onChange={handleFileUpload}
        accept=".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp"
        multiple
        className="hidden"
      />

      {/* Ký trả tài sản hậu cần — phần đầu tiên của tab Sau tiếp khách (real handover API). */}
      {visitInstanceId && !isStudent && (
        <LogisticsHandoverSection visitInstanceId={visitInstanceId} canManage={!isReadOnly && !isDept} handoverPhase="RETURN" sectionNumber="1" theme="blue" />
      )}

      {/* Chi phí chung (General Expense) */}
      {visitInstanceId && !isStudent && !isDept && (
        <GeneralExpensePanel visitInstanceId={visitInstanceId} isReadOnly={isReadOnly} sectionNumber="2" instanceStatus={instanceStatus} />
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
                    Thư mục: <span className="font-mono font-normal text-gray-700">{driveConfig.folderName}</span>
                  </p>
                  <div className="flex items-center gap-3 mt-2">
                    <button
                      type="button"
                      onClick={() => setShowPhotoModal(true)}
                      className="text-[#004c91] hover:text-[#00386b] hover:underline font-extrabold text-xs inline-flex items-center gap-1 bg-white hover:bg-slate-50 px-3 py-1.5 border border-gray-200 rounded-lg shadow-sm transition-colors cursor-pointer"
                    >
                      <Eye className="w-3.5 h-3.5" /> Mở thư mục ảnh
                    </button>
                    <span className="text-gray-300 text-xs">|</span>
                    <div className="text-gray-500 text-xs flex flex-col sm:flex-row sm:items-center gap-1 sm:gap-2 font-normal">
                      <span className="flex items-center gap-1">
                        Người upload: <span className="text-[#004c91]">{driveConfig.uploaderName}</span>
                      </span>
                      <span className="hidden sm:inline text-gray-300">|</span>
                      <span className="flex items-center gap-1">
                        Thời gian: 
                        {driveConfig.syncStatus === 'syncing' ? (
                          <RefreshCw className="w-3.5 h-3.5 animate-spin text-[#004c91]" />
                        ) : (
                          <span className="text-emerald-600 font-normal">{driveConfig.lastSynced}</span>
                        )}
                      </span>
                    </div>
                  </div>
                </div>
              </div>

              {!isDept && !isReadOnly && (
                <div className="shrink-0 flex items-center">
                  <button
                    type="button"
                    onClick={() => fileInputRef.current?.click()}
                    className="inline-flex items-center gap-2 px-5 py-3.5 bg-[#004c91] hover:bg-[#00386b] text-white font-extrabold text-sm rounded-xl shadow-sm active:scale-95 transition-all cursor-pointer whitespace-nowrap"
                  >
                    <UploadCloud className="w-4 h-4" /> Upload ảnh
                  </button>
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
                <div className="flex items-center justify-between flex-wrap gap-2.5">
                  <div className="flex items-center gap-2.5">
                    <span className="w-6 h-6 rounded-lg bg-[#004c91]/10 flex items-center justify-center text-xs font-bold text-[#004c91]">2</span>
                    <h3 className="text-base font-semibold text-[#004c91]">{t('title')}</h3>
                  </div>

                  {visitInstanceId && (
                    <div className="flex items-center gap-2 flex-wrap">
                      {!isReadOnly && (
                        <button
                          type="button"
                          onClick={() => fileInputRef.current?.click()}
                          className="inline-flex items-center gap-1.5 px-3.5 py-1.5 bg-[#004c91] hover:bg-[#00386b] text-white rounded-lg text-xs font-extrabold shadow-sm transition-all active:scale-95 cursor-pointer"
                        >
                          <UploadCloud className="w-3.5 h-3.5" /> Tải lên ảnh chụp thực tế
                        </button>
                      )}
                      <button
                        type="button"
                        onClick={() => faceScanPanelRef.current?.openAlbumModal()}
                        className="inline-flex items-center gap-1.5 px-3.5 py-1.5 bg-white text-[#004c91] border border-[#004c91] hover:bg-blue-50 rounded-lg text-xs font-extrabold shadow-sm transition-all active:scale-95 cursor-pointer"
                      >
                        <FolderOpen className="w-3.5 h-3.5" /> Chọn từ thư mục ảnh đoàn
                      </button>
                    </div>
                  )}
                </div>

                {visitInstanceId && (
                  <FaceScanPanel
                    ref={faceScanPanelRef}
                    visitInstanceId={visitInstanceId}
                    photos={uploadedImages.map((img) => ({ visitPhotoId: img.id, url: img.url, name: img.name }))}
                    isReadOnly={isReadOnly}
                    onUploadClick={() => fileInputRef.current?.click()}
                    folderUrl={driveConfig.folderUrl}
                    onRefreshPhotos={loadDriveMetadata}
                  />
                )}
              </div>
            </>
          )}
        </div>
      </CollapsibleSection>

      {/* SECTION 5: NEWS — the real per-instance news workflow. VisitNewsPostList lists the actual
          posts (role-scoped by the backend) and its "Tạo bài tin tức" button opens the shared News
          Management form with this visit_instance_id preselected. There is no second editor and no
          mock preview here. News is inherently per-instance, so with no instance there is nothing to
          show — an honest empty state, never a fabricated sample article. */}
      {visitInstanceId ? (
        <VisitNewsSection visitInstanceId={visitInstanceId} sectionNumber="5" />
      ) : (
        <CollapsibleSection number="5" title="Tin tức đoàn khách" subtitle="Bài viết truyền thông về hoạt động tiếp khách">
          <div className="p-6">
            <EmptyState
              title="Chưa thể hiển thị tin tức"
              description="Tin tức gắn với từng chuyến thăm. Hãy mở đúng chuyến thăm để xem hoặc tạo bài viết."
            />
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
                    <p className="text-sm font-normal text-amber-900 leading-relaxed bg-transparent">
                      <strong className="text-amber-700 font-extrabold block mb-0.5">CHỐT HỒ SƠ TIẾP ĐÓN:</strong>
                      Khi bấm nút này, hệ thống sẽ chốt toàn bộ dữ liệu hiện có trong quy trình tiếp khách. Bạn sẽ không thể chỉnh sửa hay thực hiện bất kì thao tác nào sau khi ấn nút này.
                    </p>
                  </div>

                  <div className="flex items-start gap-3 p-5 bg-blue-50 border border-blue-200 rounded-2xl text-left shadow-sm font-sans">
                    <FileText className="w-5 h-5 text-blue-600 shrink-0 mt-0.5" />
                    <div className="text-sm font-normal text-blue-950 leading-relaxed">
                      <strong className="text-blue-800 font-extrabold block mb-0.5">ĐIỀU KIỆN ĐỂ CÓ THỂ ĐÓNG ĐOÀN KHÁCH:</strong>
                      <ul className="list-decimal pl-4 space-y-1 mt-1 text-xs font-normal text-blue-900">
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

      {/* PHOTO FOLDER MODAL — cùng giao diện với modal "Xem ảnh" trong Quản lý ảnh đoàn khách. */}
      {showPhotoModal && visitInstanceId && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm flex items-center justify-center z-[110] p-4 sm:p-6">
          <div className="bg-white rounded-3xl shadow-2xl w-full max-w-6xl max-h-[calc(100dvh-2rem)] sm:max-h-[calc(100dvh-3rem)] flex flex-col overflow-hidden animate-in zoom-in-95">
            <div className="px-6 py-3.5 border-b border-slate-100 flex items-center justify-between gap-3 bg-slate-50/50 shrink-0">
              <div className="min-w-0 flex items-center gap-3">
                <div className="w-9 h-9 rounded-xl bg-orange-50 text-[#f37021] flex items-center justify-center shrink-0 border border-orange-100 shadow-sm">
                  <Camera className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-base font-extrabold text-[#004c91] truncate">Thư mục ảnh đoàn khách</h3>
                  <p className="text-xs text-slate-500 font-normal truncate">{driveConfig.folderName}</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => { setShowPhotoModal(false); loadDriveMetadata(); }}
                className="p-2 rounded-xl text-slate-400 hover:text-slate-700 hover:bg-slate-200/60 transition-colors cursor-pointer"
                aria-label="Đóng"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="p-6 overflow-y-auto flex-1 bg-white">
              <VisitPhotoPanel
                visitInstanceId={visitInstanceId}
                mode={isReadOnly ? 'view' : 'edit'}
                showFaceTags={!isStudent}
              />
            </div>
          </div>
        </div>
      )}

    </div>
  );
}
